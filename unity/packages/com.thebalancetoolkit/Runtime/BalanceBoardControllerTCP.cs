using UnityEngine;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

public class BalanceBoardControllerTCP : MonoBehaviour
{
    [Header("TCP Connection Settings")]
    public string serverAddress = "127.0.0.1";
    [Tooltip("Raw stream port of The Balance Toolkit desktop app (default 11223)")]
    public int serverPort = BalanceToolkitTcpProtocol.DefaultRawPort;
    
    [Header("Movement Settings")]
    public Transform parentPlane;
    [Range(1f, 20f)]
    public float smoothingFactor = 10f;
    
    [Header("Connection Options")]
    public bool autoReconnect = true;
    public float reconnectDelay = 3f;
    public int maxReconnectAttempts = 5;
    
    [Header("Objects to Hide on Connection")]
    [Tooltip("Objects with MeshRenderer that will be hidden when connected")]
    public GameObject[] objectsToHide = new GameObject[0];
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    
    // Private variables
    private bool isConnected = false;
    private bool isConnecting = false;
    private Vector2 currentCOP = Vector2.zero;
    private float[] sensorValues = new float[4]; // TL, TR, BL, BR
    private Vector3 initialPosition;
    private int reconnectAttempts = 0;
    private bool reconnectScheduled = false; // an Invoke of ConnectToTCP/AttemptReconnect is pending
    private ulong macAddress = 0;
    
    // Thread safety
    private readonly object dataLock = new object();
    private volatile bool hasNewData = false;
    private Vector2 latestCOP = Vector2.zero;
    private float[] latestSensorValues = new float[4];
    private ulong latestMacAddress = 0;
    
    // Performance tracking
    private int samplesReceived = 0;
    private float lastStatsUpdate = 0f;
    private int samplesPerSecond = 0;
    
    // TCP components
    private TcpClient tcpClient;
    private NetworkStream networkStream;
    private Thread tcpReadThread;
    private volatile bool tcpThreadRunning = false;
    private readonly BalanceToolkitTcpProtocol.RecordFramer framer =
        new BalanceToolkitTcpProtocol.RecordFramer(BalanceToolkitTcpProtocol.RawRecordSize);
    
    void Start()
    {
        initialPosition = transform.position;
        ValidateSetup();
        
        if (showDebugInfo)
            Debug.Log($"[TCP Controller] Starting - Server: {serverAddress}:{serverPort}");
        
        reconnectScheduled = true;
        Invoke(nameof(ConnectToTCP), 0.5f);
    }
    
    void Update()
    {
        if (isConnected)
        {
            UpdatePosition();
            UpdatePerformanceStats();
        }
        
        // Auto-reconnect: schedule one attempt at a time. A separate flag is used so the
        // pending attempt is not mistaken for a connection in progress.
        if (!isConnected && !isConnecting && !reconnectScheduled && autoReconnect && reconnectAttempts < maxReconnectAttempts)
        {
            reconnectScheduled = true;
            Invoke(nameof(AttemptReconnect), reconnectDelay);
        }
    }
    
    void OnDestroy()
    {
        DisconnectFromTCP();
    }
    
    private void ValidateSetup()
    {
        if (parentPlane == null)
        {
            parentPlane = transform.parent;
            if (parentPlane == null)
            {
                Debug.LogError("[TCP Controller] No parent plane assigned!");
                return;
            }
            Debug.Log($"[TCP Controller] Using parent transform: {parentPlane.name}");
        }
        
        if (serverPort <= 0 || serverPort > 65535)
        {
            Debug.LogWarning($"[TCP Controller] Invalid port {serverPort}, using default 11223");
            serverPort = 11223;
        }
    }
    
    public void ConnectToTCP()
    {
        reconnectScheduled = false;
        if (isConnected)
        {
            Debug.LogWarning("[TCP Controller] Already connected");
            return;
        }
        
        if (isConnecting)
        {
            Debug.LogWarning("[TCP Controller] Connection in progress");
            return;
        }
        
        isConnecting = true;
        
        try
        {
            Debug.Log($"[TCP Controller] Connecting to {serverAddress}:{serverPort}");
            
            tcpClient = new TcpClient();
            tcpClient.ReceiveTimeout = 5000;
            tcpClient.SendTimeout = 5000;
            
            tcpClient.Connect(serverAddress, serverPort);
            networkStream = tcpClient.GetStream();
            
            framer.Reset();
            
            tcpThreadRunning = true;
            tcpReadThread = new Thread(TCPReadThread);
            tcpReadThread.IsBackground = true;
            tcpReadThread.Start();
            
            isConnected = true;
            isConnecting = false;
            reconnectAttempts = 0;
            
            SetObjectsVisibility(false);
            
            Debug.Log($"[TCP Controller] Connected successfully!");
            Debug.Log($"  - Server: {serverAddress}:{serverPort}");
            Debug.Log($"  - Hidden {objectsToHide.Length} objects");
        }
        catch (Exception e)
        {
            Debug.LogError($"[TCP Controller] Connection failed: {e.Message}");
            isConnected = false;
            isConnecting = false;
            CleanupTCP();
            
            reconnectAttempts++;
            if (reconnectAttempts >= maxReconnectAttempts)
            {
                Debug.LogError($"[TCP Controller] Max reconnect attempts ({maxReconnectAttempts}) reached");
                autoReconnect = false;
            }
        }
    }
    
    public void DisconnectFromTCP()
    {
        if (!isConnected && !isConnecting) return;
        
        isConnected = false;
        isConnecting = false;
        
        SetObjectsVisibility(true);
        CleanupTCP();
        ResetPosition();
        
        Debug.Log("[TCP Controller] Disconnected - Objects restored, position reset");
    }
    
    private void CleanupTCP()
    {
        tcpThreadRunning = false;
        
        if (tcpReadThread != null && tcpReadThread.IsAlive)
        {
            try
            {
                tcpReadThread.Join(1000);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TCP Controller] Thread cleanup error: {e.Message}");
            }
            tcpReadThread = null;
        }
        
        try
        {
            if (networkStream != null)
            {
                networkStream.Close();
                networkStream = null;
            }
            
            if (tcpClient != null)
            {
                tcpClient.Close();
                tcpClient = null;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[TCP Controller] Network cleanup error: {e.Message}");
        }
        
        framer.Reset();
    }
    
    private void AttemptReconnect()
    {
        reconnectScheduled = false;
        if (isConnected || isConnecting || !autoReconnect) return;
        
        Debug.Log($"[TCP Controller] Reconnect attempt {reconnectAttempts + 1}/{maxReconnectAttempts}");
        ConnectToTCP();
    }
    
    private void TCPReadThread()
    {
        byte[] buffer = new byte[4096];
        
        while (tcpThreadRunning && tcpClient != null && tcpClient.Connected)
        {
            try
            {
                if (networkStream.DataAvailable)
                {
                    int bytesRead = networkStream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        framer.Feed(buffer, bytesRead, ProcessRecord);
                    }
                }
                
                Thread.Sleep(1);
            }
            catch (Exception e)
            {
                if (tcpThreadRunning)
                {
                    Debug.LogError($"[TCP Controller] Read thread error: {e.Message}");
                    
                    // Trigger reconnection on main thread
                    UnityMainThreadDispatcher.Instance().Enqueue(() => {
                        if (isConnected) DisconnectFromTCP();
                    });
                }
                break;
            }
        }
        
        if (showDebugInfo)
            Debug.Log("[TCP Controller] Read thread stopped");
    }
    
    private void ProcessRecord(byte[] record, int offset)
    {
        try
        {
            var sample = BalanceToolkitTcpProtocol.DecodeRaw(record, offset);
            
            lock (dataLock)
            {
                // Map to sensor array: [TL, TR, BL, BR]
                latestSensorValues[0] = sample.TopLeft;
                latestSensorValues[1] = sample.TopRight;
                latestSensorValues[2] = sample.BottomLeft;
                latestSensorValues[3] = sample.BottomRight;
                
                latestCOP = new Vector2(sample.CopX, sample.CopY);
                latestMacAddress = sample.MacAddress;
                hasNewData = true;
            }
            
            samplesReceived++;
        }
        catch (Exception e)
        {
            Debug.LogError($"[TCP Controller] Data processing error: {e.Message}");
        }
    }
    
    private void UpdatePosition()
    {
        if (!hasNewData) return;
        
        lock (dataLock)
        {
            if (hasNewData)
            {
                currentCOP = latestCOP;
                macAddress = latestMacAddress;
                Array.Copy(latestSensorValues, sensorValues, 4);
                hasNewData = false;
            }
        }
        
        if (parentPlane != null)
        {
            Vector3 targetPosition = CalculateWorldPosition(currentCOP);
            transform.position = Vector3.Lerp(transform.position, targetPosition, smoothingFactor * Time.deltaTime);
        }
    }
    
    private Vector3 CalculateWorldPosition(Vector2 copValues)
    {
        float clampedX = Mathf.Clamp(copValues.x, -1f, 1f);
        float clampedY = Mathf.Clamp(copValues.y, -1f, 1f);
        
        // Get the actual bounds of the parent plane
        Bounds parentBounds;
        Renderer parentRenderer = parentPlane.GetComponent<Renderer>();
        if (parentRenderer != null)
        {
            parentBounds = parentRenderer.bounds;
        }
        else
        {
            // Fallback to using transform scale
            Vector3 scale = parentPlane.lossyScale;
            parentBounds = new Bounds(parentPlane.position, scale);
        }
        
        // Calculate movement range (use 90% of bounds to keep circle inside)
        float moveRangeX = parentBounds.size.x * 0.45f; // 45% from center = 90% total
        float moveRangeZ = parentBounds.size.z * 0.45f;
        
        // Calculate local offset from center
        Vector3 localOffset = new Vector3(
            clampedX * moveRangeX,
            0f,
            clampedY * moveRangeZ
        );
        
        // Transform by parent's rotation and add to parent center
        Vector3 worldOffset = parentPlane.transform.TransformDirection(localOffset);
        return parentBounds.center + worldOffset;
    }
    
    private void SetObjectsVisibility(bool visible)
    {
        if (objectsToHide == null) return;
        
        int hiddenCount = 0;
        foreach (GameObject obj in objectsToHide)
        {
            if (obj != null)
            {
                bool rendererFound = false;
                
                // Check for MeshRenderer
                MeshRenderer meshRenderer = obj.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.enabled = visible;
                    hiddenCount++;
                    rendererFound = true;
                }
                
                // Check for SkinnedMeshRenderer
                SkinnedMeshRenderer skinnedRenderer = obj.GetComponent<SkinnedMeshRenderer>();
                if (skinnedRenderer != null)
                {
                    skinnedRenderer.enabled = visible;
                    hiddenCount++;
                    rendererFound = true;
                }
                
                if (!rendererFound && showDebugInfo)
                {
                    Debug.LogWarning($"[TCP Controller] {obj.name} has no MeshRenderer or SkinnedMeshRenderer");
                }
            }
        }
        
        if (showDebugInfo && hiddenCount > 0)
        {
            Debug.Log($"[TCP Controller] Set {hiddenCount} renderers to {(visible ? "visible" : "hidden")}");
        }
    }
    
    private void ResetPosition()
    {
        if (parentPlane != null)
        {
            transform.position = new Vector3(parentPlane.position.x, transform.position.y, parentPlane.position.z);
        }
        else
        {
            transform.position = initialPosition;
        }
        
        currentCOP = Vector2.zero;
        macAddress = 0;
        sensorValues = new float[4];
    }
    
    private void UpdatePerformanceStats()
    {
        float currentTime = Time.time;
        if (currentTime - lastStatsUpdate >= 1f)
        {
            samplesPerSecond = samplesReceived;
            samplesReceived = 0;
            lastStatsUpdate = currentTime;
            
            if (showDebugInfo && samplesPerSecond > 0)
            {
                Debug.Log($"[TCP Controller] Performance: {samplesPerSecond} samples/sec, COP: ({currentCOP.x:F2}, {currentCOP.y:F2})");
            }
        }
    }
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.white;
        style.fontSize = 12;
        style.normal.background = Texture2D.blackTexture;
        
        string status = isConnected ? "Connected" : isConnecting ? "Connecting..." : "Disconnected";
        string macDisplay = BalanceToolkitTcpProtocol.FormatMac(macAddress);
        string debugText = $"TCP Balance Controller\n" +
                          $"Status: {status}\n" +
                          $"Server: {serverAddress}:{serverPort}\n" +
                          $"MAC: {macDisplay}\n" +
                          $"Samples/sec: {samplesPerSecond}\n" +
                          $"Reconnects: {reconnectAttempts}/{maxReconnectAttempts}\n" +
                          $"COP: ({currentCOP.x:F3}, {currentCOP.y:F3})\n" +
                          $"Sensors: TL:{sensorValues[0]:F1} TR:{sensorValues[1]:F1}\n" +
                          $"         BL:{sensorValues[2]:F1} BR:{sensorValues[3]:F1}";
        
        GUI.Label(new Rect(10, 10, 350, 200), debugText, style);
    }
    
    void OnDrawGizmosSelected()
    {
        if (parentPlane == null) return;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(parentPlane.position, parentPlane.localScale);
        
        if (Application.isPlaying && isConnected)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(CalculateWorldPosition(currentCOP), 0.1f);
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(parentPlane.position, CalculateWorldPosition(currentCOP));
            
            Gizmos.color = isConnected ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.2f);
        }
    }
    
    [ContextMenu("Connect")]
    public void Connect() => ConnectToTCP();
    
    [ContextMenu("Disconnect")]
    public void Disconnect() => DisconnectFromTCP();
    
    [ContextMenu("Reset Position")]
    public void ResetToCenter() => ResetPosition();
    
    [ContextMenu("Reset Reconnect Counter")]
    public void ResetReconnectCounter()
    {
        reconnectAttempts = 0;
        autoReconnect = true;
        Debug.Log("[TCP Controller] Reconnect counter reset and auto-reconnect enabled");
    }
    
    // Public properties
    public bool IsConnected => isConnected;
    public bool IsConnecting => isConnecting;
    public Vector2 CenterOfPressure => currentCOP;
    public float[] SensorValues => (float[])sensorValues.Clone();
    public int SamplesPerSecond => samplesPerSecond;
    public int ReconnectAttempts => reconnectAttempts;
    public ulong MacAddress => macAddress;
}

// Helper class for main thread dispatching
public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;
    private Queue<System.Action> _executionQueue = new Queue<System.Action>();
    
    public static UnityMainThreadDispatcher Instance()
    {
        if (!_instance)
        {
#if UNITY_2023_1_OR_NEWER
            _instance = FindAnyObjectByType<UnityMainThreadDispatcher>();
#else
            _instance = FindObjectOfType<UnityMainThreadDispatcher>();
#endif
            if (!_instance)
            {
                GameObject go = new GameObject("MainThreadDispatcher");
                _instance = go.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
        }
        return _instance;
    }
    
    public void Enqueue(System.Action action)
    {
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }
    
    void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                _executionQueue.Dequeue().Invoke();
            }
        }
    }
}