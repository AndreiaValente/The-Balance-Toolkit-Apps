using UnityEditor;
using UnityEngine;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using TheBalanceToolkit.Editor;

public class TheBalanceToolkitWindow : EditorWindow
{
    public enum ConnectionType { None, LSL, TCP }
    
    #region Configuration Constants
    
    private const float CIRCLE_RADIUS = 8f;
    private const float CORNER_RADIUS_RATIO = 0.08f;
    private const float MARGIN_RATIO = 0.12f;
    private const float BOARD_ASPECT_RATIO = 56f / 31f;
    private const int MAX_DATA_POINTS = 100;
    
    #endregion
    
    #region UI State
    
    private Vector2 scrollPosition = Vector2.zero;
    private float totalContentHeight = 0f;
    
    #endregion
    
    #region Connection Settings
    
    [SerializeField] private ConnectionType connectionType = ConnectionType.TCP;
    [SerializeField] private string streamName = "the-balance-toolkit";
    [SerializeField] private string tcpAddress = "127.0.0.1";
    [SerializeField] private int tcpRawPort = BalanceToolkitTcpProtocol.DefaultRawPort;
    [SerializeField] private int tcpProcessedPort = BalanceToolkitTcpProtocol.DefaultProcessedPort;
    private bool isConnected = false;
    
    #endregion
    
    #region Data Storage
    
    private Queue<float> copXData = new Queue<float>();
    private Queue<float> copYData = new Queue<float>();
    private Queue<float> vCopXData = new Queue<float>();
    private Queue<float> vCopYData = new Queue<float>();
    
    private float[] sensorValues = new float[4]; // TL, TR, BL, BR
    private Vector2 centerOfPressure = Vector2.zero;
    private Vector2 velocityOfCOP = Vector2.zero;
    private float currentStability = 0f;
    private ulong macAddress = 0;
    
    // Additional complex stream data
    private float dpsiMLSI = 0f;
    private float dpsiAPSI = 0f;
    private float dpsiVSI = 0f;
    private float dpsiOverall = 0f;
    
    #endregion
    
    #region Thread Safety
    
    private readonly object dataLock = new object();
    private volatile bool hasNewBasicData = false;
    private volatile bool hasNewComplexData = false;
    private Vector2 latestCOP;
    private Vector2 latestVelocity;
    private float[] latestSensorValues = new float[4];
    private float latestStability = 0f;
    private ulong latestMacAddress = 0;
    private float latestDpsiMLSI = 0f;
    private float latestDpsiAPSI = 0f;
    private float latestDpsiVSI = 0f;
    private float latestDpsiOverall = 0f;
    private double lastUpdateTime = 0;
    
    #endregion
    
    #region Debug
    
    private int debugSampleCount = 0;
    private double debugLastTime = 0;
    
    #endregion
    
    #region TCP Components
    
    private TcpClient tcpClientBasic;
    private NetworkStream tcpStreamBasic;
    private Thread tcpThreadBasic;
    private bool tcpRunningBasic = false;
    
    private TcpClient tcpClientComplex;
    private NetworkStream tcpStreamComplex;
    private Thread tcpThreadComplex;
    private bool tcpRunningComplex = false;
    
    private readonly BalanceToolkitTcpProtocol.RecordFramer rawFramer =
        new BalanceToolkitTcpProtocol.RecordFramer(BalanceToolkitTcpProtocol.RawRecordSize);
    private readonly BalanceToolkitTcpProtocol.RecordFramer processedFramer =
        new BalanceToolkitTcpProtocol.RecordFramer(BalanceToolkitTcpProtocol.ProcessedRecordSize);
    
    #endregion
    
    #region Colors
    
    private readonly Color CIRCLE_COLOR = new Color(229f/255f, 0f/255f, 18f/255f, 1f);
    private readonly Color COP_X_COLOR = new Color(57f/255f, 122f/255f, 172f/255f, 1f);
    private readonly Color COP_Y_COLOR = new Color(229f/255f, 0f/255f, 18f/255f, 1f);
    private readonly Color GRAY_COLOR = new Color(108f/255f, 117f/255f, 125f/255f, 1f);
    
    #endregion
    
    #region Unity Editor Integration
    
    [MenuItem("Balance Toolkit Demo/Monitor", false, 1)]
    public static void ShowWindow()
    {
        var window = GetWindow<TheBalanceToolkitWindow>(false, "The Balance Toolkit", true);
        window.minSize = new Vector2(350, 300);
    }

    private void OnEnable()
    {
        InitializeDataQueues();
        EditorApplication.update += Update;
    }

    private void OnDisable()
    {
        DisconnectFromStream();
        EditorApplication.update -= Update;
    }

    private void OnGUI()
    {
        UpdateUIFromLatestData();
        
        DrawWindowTitle();
        float headerHeight = DrawConnectionControls();
        
        CalculateTotalContentHeight();
        
        Rect scrollViewRect = new Rect(0, headerHeight, position.width, position.height - headerHeight);
        Rect contentRect = new Rect(0, 0, position.width - 20f, totalContentHeight);
        
        scrollPosition = GUI.BeginScrollView(scrollViewRect, scrollPosition, contentRect);
        
        Rect boardRect = DrawBalanceBoard(0f);
        DrawCornerSensorValues(boardRect);
        DrawPositionReadout(boardRect);
        DrawStabilityMeter(boardRect);
        DrawDataVisualization(boardRect);
        ProcessAnimation(boardRect);
        
        GUI.EndScrollView();
    }
    
    #endregion
    
    #region Content Height Calculation
    
    private void CalculateTotalContentHeight()
    {
        float height = 0f;
        
        float dynamicMargin = position.width * MARGIN_RATIO;
        float boardWidth = position.width - (dynamicMargin * 2);
        float boardHeight = boardWidth / BOARD_ASPECT_RATIO;
        
        float minBoardWidth = 100f;
        float minBoardHeight = minBoardWidth / BOARD_ASPECT_RATIO;
        
        if (boardWidth < minBoardWidth)
        {
            boardWidth = minBoardWidth;
            boardHeight = minBoardHeight;
        }
        
        height += boardHeight + 20f;
        height += 35f; // Position readout
        height += 40f; // Stability meter
        
        float graphHeight = GetAdaptiveGraphHeight();
        float graphSpacing = 25f;
        height += (graphHeight + graphSpacing) * 4; // 4 graphs
        height += 20f; // Bottom margin
        
        totalContentHeight = height;
    }
    
    #endregion
    
    #region UI Drawing
    
    private void DrawWindowTitle()
    {
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 15,
            alignment = TextAnchor.UpperCenter
        };
        GUI.Label(new Rect(0, 10, position.width, 30), "The Balance Toolkit", titleStyle);
    }

    private float DrawConnectionControls()
    {
        const float START_Y = 40f;
        const float MARGIN = 20f;
        const float SPACING = 5f;
        const float TOGGLE_HEIGHT = 20f;
        
        float currentY = START_Y;
        float toggleWidth = (position.width - MARGIN * 3) * 0.5f;
        
        GUI.enabled = !isConnected;
        
        // Radio buttons: a click selects that option; clicking the selected one keeps it selected.
        bool lslSelected = GUI.Toggle(new Rect(MARGIN, currentY, toggleWidth, TOGGLE_HEIGHT),
                                     connectionType == ConnectionType.LSL, "LSL", EditorStyles.radioButton);
        bool tcpSelected = GUI.Toggle(new Rect(MARGIN + toggleWidth + MARGIN, currentY, toggleWidth, TOGGLE_HEIGHT),
                                     connectionType == ConnectionType.TCP, "TCP", EditorStyles.radioButton);

        if (lslSelected && connectionType != ConnectionType.LSL)
        {
            connectionType = ConnectionType.LSL;
            GUI.FocusControl(null);
        }
        else if (tcpSelected && connectionType != ConnectionType.TCP)
        {
            connectionType = ConnectionType.TCP;
            GUI.FocusControl(null);
        }
        
        GUI.enabled = true;
        currentY += TOGGLE_HEIGHT + SPACING * 2;
        
        if (connectionType == ConnectionType.LSL && LslMonitor.Source == null)
        {
            currentY = DrawLslSetupHint(currentY);
        }
        else if (connectionType != ConnectionType.None)
        {
            currentY = DrawInputFields(currentY);
            currentY = DrawConnectionButton(currentY + SPACING);
        }
        
        return currentY + 20f;
    }

    private float DrawInputFields(float startY)
    {
        const float MARGIN = 20f;
        const float LABEL_HEIGHT = 15f;
        const float FIELD_HEIGHT = 20f;
        const float SPACING = 5f;
        const float FIELD_GAP = 10f;
        
        float currentY = startY;
        float availableWidth = position.width - (MARGIN * 2);
        float fieldWidth = (availableWidth - FIELD_GAP) * 0.5f;
        
        if (connectionType == ConnectionType.LSL)
        {
            GUI.Label(new Rect(MARGIN, currentY, availableWidth, LABEL_HEIGHT), "Stream Name");
            currentY += LABEL_HEIGHT + SPACING;
            
            GUI.enabled = !isConnected;
            streamName = GUI.TextField(new Rect(MARGIN, currentY, availableWidth, FIELD_HEIGHT), streamName);
            GUI.enabled = true;
        }
        else if (connectionType == ConnectionType.TCP)
        {
            float portWidth = (availableWidth - fieldWidth - FIELD_GAP * 2) * 0.5f;
            float rawPortX = MARGIN + fieldWidth + FIELD_GAP;
            float processedPortX = rawPortX + portWidth + FIELD_GAP;
            
            GUI.Label(new Rect(MARGIN, currentY, fieldWidth, LABEL_HEIGHT), "Host");
            GUI.Label(new Rect(rawPortX, currentY, portWidth, LABEL_HEIGHT), "Raw port");
            GUI.Label(new Rect(processedPortX, currentY, portWidth, LABEL_HEIGHT), "Processed port");
            currentY += LABEL_HEIGHT + SPACING;
            
            GUI.enabled = !isConnected;
            tcpAddress = GUI.TextField(new Rect(MARGIN, currentY, fieldWidth, FIELD_HEIGHT), tcpAddress);
            tcpRawPort = EditorGUI.IntField(new Rect(rawPortX, currentY, portWidth, FIELD_HEIGHT), tcpRawPort);
            tcpProcessedPort = EditorGUI.IntField(new Rect(processedPortX, currentY, portWidth, FIELD_HEIGHT), tcpProcessedPort);
            GUI.enabled = true;
        }
        
        return currentY + FIELD_HEIGHT;
    }

    private float DrawLslSetupHint(float startY)
    {
        const float MARGIN = 20f;
        const float HINT_HEIGHT = 40f;
        const float BUTTON_HEIGHT = 25f;
        const float SPACING = 5f;
        
        float width = position.width - MARGIN * 2;
        EditorGUI.HelpBox(new Rect(MARGIN, startY, width, HINT_HEIGHT),
            "LSL needs the LSL4Unity build and the LSL Integration sample.", MessageType.Info);
        
        float buttonY = startY + HINT_HEIGHT + SPACING;
        if (GUI.Button(new Rect(MARGIN, buttonY, width, BUTTON_HEIGHT), "Open Setup"))
        {
            ToolkitSetupWindow.Open();
        }
        return buttonY + BUTTON_HEIGHT;
    }

    private float DrawConnectionButton(float startY)
    {
        const float MARGIN = 20f;
        const float BUTTON_HEIGHT = 25f;
        
        Color originalBG = GUI.backgroundColor;
        if (isConnected) GUI.backgroundColor = Color.red;
        
        string buttonLabel = isConnected ? "Disconnect" : "Connect";
        
        if (GUI.Button(new Rect(MARGIN, startY, position.width - MARGIN * 2, BUTTON_HEIGHT), buttonLabel))
        {
            ToggleConnection();
        }
        
        GUI.backgroundColor = originalBG;
        return startY + BUTTON_HEIGHT;
    }

    private Rect DrawBalanceBoard(float startY)
    {
        float dynamicMargin = position.width * MARGIN_RATIO;
        float boardWidth = position.width - (dynamicMargin * 2);
        float boardHeight = boardWidth / BOARD_ASPECT_RATIO;
        
        float minBoardWidth = 200f;
        float minBoardHeight = minBoardWidth / BOARD_ASPECT_RATIO;
        
        if (boardWidth < minBoardWidth)
        {
            boardWidth = minBoardWidth;
            boardHeight = minBoardHeight;
        }
        
        float boardX = (position.width - boardWidth) * 0.5f;
        Rect boardRect = new Rect(boardX, startY + 20f, boardWidth, boardHeight);
        
        DrawRoundedRectangle(boardRect, boardWidth * CORNER_RADIUS_RATIO);
        DrawBoardAxes(boardRect);
        
        return boardRect;
    }

    private float GetAdaptiveGraphHeight()
    {
        if (position.height < 400f) return 40f;
        else if (position.height < 600f) return 50f;
        else if (position.height < 800f) return 65f;
        else return 80f;
    }

    private void DrawBoardAxes(Rect boardRect)
    {
        Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
        
        Vector2 adjustedCenter = new Vector2(boardRect.center.x, boardRect.center.y - scrollPosition.y);
        Vector2 adjustedMin = new Vector2(boardRect.xMin, boardRect.yMin - scrollPosition.y);
        Vector2 adjustedMax = new Vector2(boardRect.xMax, boardRect.yMax - scrollPosition.y);
        
        Vector3 horizontalStart = new Vector3(boardRect.xMin + boardRect.width * 0.1f, adjustedCenter.y, 0);
        Vector3 horizontalEnd = new Vector3(boardRect.xMax - boardRect.width * 0.1f, adjustedCenter.y, 0);
        Handles.DrawLine(horizontalStart, horizontalEnd);
        
        Vector3 verticalStart = new Vector3(adjustedCenter.x, adjustedMin.y + boardRect.height * 0.1f, 0);
        Vector3 verticalEnd = new Vector3(adjustedCenter.x, adjustedMax.y - boardRect.height * 0.1f, 0);
        Handles.DrawLine(verticalStart, verticalEnd);
        
        DrawAxisLabels(boardRect);
    }

    private void DrawAxisLabels(Rect boardRect)
    {
        GUIStyle axisLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 9,
            normal = { textColor = new Color(0.4f, 0.4f, 0.4f, 1f) },
            alignment = TextAnchor.MiddleCenter
        };
        
        GUI.Label(new Rect(boardRect.xMin + boardRect.width * 0.05f, boardRect.center.y - 15f, 20f, 15f), "-1", axisLabelStyle);
        GUI.Label(new Rect(boardRect.center.x - 10f, boardRect.center.y - 15f, 20f, 15f), "0", axisLabelStyle);
        GUI.Label(new Rect(boardRect.xMax - boardRect.width * 0.05f - 20f, boardRect.center.y - 15f, 20f, 15f), "1", axisLabelStyle);
        
        GUI.Label(new Rect(boardRect.center.x + 5f, boardRect.yMin + boardRect.height * 0.05f, 20f, 15f), "1", axisLabelStyle);
        GUI.Label(new Rect(boardRect.center.x + 5f, boardRect.center.y - 7f, 20f, 15f), "0", axisLabelStyle);
        GUI.Label(new Rect(boardRect.center.x + 5f, boardRect.yMax - boardRect.height * 0.05f - 15f, 20f, 15f), "-1", axisLabelStyle);
    }

    private void DrawRoundedRectangle(Rect bounds, float cornerRadius)
    {
        Color fillColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        Color borderColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        
        var points = CreateRoundedRectanglePoints(bounds, cornerRadius);
        
        for (int i = 0; i < points.Count; i++)
        {
            points[i] = new Vector3(points[i].x, points[i].y - scrollPosition.y, points[i].z);
        }
        
        Handles.color = fillColor;
        Handles.DrawAAConvexPolygon(points.ToArray());
        Handles.color = borderColor;
        Handles.DrawAAPolyLine(points.ToArray());
    }

    private List<Vector3> CreateRoundedRectanglePoints(Rect bounds, float radius)
    {
        var points = new List<Vector3>();
        const int SEGMENTS_PER_CORNER = 16;
        
        AddArcPoints(points, bounds.xMin + radius, bounds.yMin + radius, radius, Mathf.PI, SEGMENTS_PER_CORNER);
        points.Add(new Vector3(bounds.xMax - radius, bounds.yMin, 0));
        
        AddArcPoints(points, bounds.xMax - radius, bounds.yMin + radius, radius, -Mathf.PI / 2, SEGMENTS_PER_CORNER);
        points.Add(new Vector3(bounds.xMax, bounds.yMax - radius, 0));
        
        AddArcPoints(points, bounds.xMax - radius, bounds.yMax - radius, radius, 0, SEGMENTS_PER_CORNER);
        points.Add(new Vector3(bounds.xMin + radius, bounds.yMax, 0));
        
        AddArcPoints(points, bounds.xMin + radius, bounds.yMax - radius, radius, Mathf.PI / 2, SEGMENTS_PER_CORNER);
        points.Add(new Vector3(bounds.xMin, bounds.yMin + radius, 0));
        
        return points;
    }

    private void AddArcPoints(List<Vector3> points, float centerX, float centerY, float radius, float startAngle, int segments)
    {
        for (int i = 0; i <= segments; i++)
        {
            float angle = startAngle + (Mathf.PI / 2) * (i / (float)segments);
            float x = centerX + Mathf.Cos(angle) * radius;
            float y = centerY + Mathf.Sin(angle) * radius;
            points.Add(new Vector3(x, y, 0));
        }
    }

    private void DrawCornerSensorValues(Rect boardRect)
    {
        const float OFFSET = 5f;
        const float LABEL_WIDTH = 30f;
        const float LABEL_HEIGHT = 30f;
        
        GUIStyle sensorStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            normal = { textColor = new Color(0.2f, 0.2f, 0.2f, 1f) },
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        
        Color boxColor = new Color(1f, 1f, 1f, 0.8f);
        
        Rect tlRect = new Rect(boardRect.xMin - OFFSET - LABEL_WIDTH, boardRect.yMin - OFFSET, LABEL_WIDTH, LABEL_HEIGHT);
        EditorGUI.DrawRect(tlRect, boxColor);
        GUI.Label(tlRect, $"TL\n{sensorValues[0]:F1}", sensorStyle);
        
        Rect trRect = new Rect(boardRect.xMax + OFFSET, boardRect.yMin - OFFSET, LABEL_WIDTH, LABEL_HEIGHT);
        EditorGUI.DrawRect(trRect, boxColor);
        GUI.Label(trRect, $"TR\n{sensorValues[1]:F1}", sensorStyle);
        
        Rect blRect = new Rect(boardRect.xMin - OFFSET - LABEL_WIDTH, boardRect.yMax + OFFSET - LABEL_HEIGHT, LABEL_WIDTH, LABEL_HEIGHT);
        EditorGUI.DrawRect(blRect, boxColor);
        GUI.Label(blRect, $"BL\n{sensorValues[2]:F1}", sensorStyle);
        
        Rect brRect = new Rect(boardRect.xMax + OFFSET, boardRect.yMax + OFFSET - LABEL_HEIGHT, LABEL_WIDTH, LABEL_HEIGHT);
        EditorGUI.DrawRect(brRect, boxColor);
        GUI.Label(brRect, $"BR\n{sensorValues[3]:F1}", sensorStyle);
    }

    private void DrawPositionReadout(Rect boardRect)
    {
        float displayY = boardRect.yMax + 15f;
        const float LABEL_WIDTH = 20f;
        const float VALUE_WIDTH = 80f;
        const float SPACING = 20f;
        
        float totalWidth = (LABEL_WIDTH + VALUE_WIDTH) * 2 + SPACING;
        float startX = (position.width - totalWidth) * 0.5f;
        
        DrawCoordinateField(startX, displayY, "X:", centerOfPressure.x);
        DrawCoordinateField(startX + LABEL_WIDTH + VALUE_WIDTH + SPACING, displayY, "Y:", centerOfPressure.y);
    }

    private void DrawCoordinateField(float x, float y, string label, float value)
    {
        const float LABEL_WIDTH = 20f;
        const float VALUE_WIDTH = 80f;
        const float HEIGHT = 20f;
        
        GUI.Label(new Rect(x, y, LABEL_WIDTH, HEIGHT), label);
        
        string displayValue = isConnected ? value.ToString("F2") : "0.00";
        GUI.enabled = false;
        GUI.TextField(new Rect(x + LABEL_WIDTH, y, VALUE_WIDTH, HEIGHT), displayValue);
        GUI.enabled = true;
    }

    private void DrawStabilityMeter(Rect boardRect)
    {
        float meterY = boardRect.yMax + 50f;
        const float METER_HEIGHT = 20f;
        const float MARGIN = 20f;
        
        float magnitude = Mathf.Clamp01(currentStability);
        DrawStabilityLabels(MARGIN, meterY, magnitude, METER_HEIGHT);
        DrawStabilityBar(MARGIN, meterY, magnitude, METER_HEIGHT);
    }

    private void DrawStabilityLabels(float x, float y, float value, float height)
    {
        const float LABEL_WIDTH = 60f;
        const float VALUE_WIDTH = 40f;
        const float PADDING = 10f;
        
        GUIStyle textStyle = new GUIStyle(GUI.skin.label) 
        { 
            alignment = TextAnchor.MiddleLeft, 
            fontSize = 12 
        };
        
        GUI.Label(new Rect(x, y, LABEL_WIDTH, height), "Stability:", textStyle);
        GUI.Label(new Rect(x + LABEL_WIDTH + PADDING, y, VALUE_WIDTH, height), value.ToString("F2"), textStyle);
    }

    private void DrawStabilityBar(float x, float y, float value, float height)
    {
        const float MARGIN = 20f;
        const float LABEL_WIDTH = 60f;
        const float VALUE_WIDTH = 40f;
        const float PADDING = 10f;
        
        float availableWidth = position.width - (MARGIN * 2);
        float labelAreaWidth = LABEL_WIDTH + PADDING + VALUE_WIDTH + PADDING;
        float barX = x + labelAreaWidth;
        float barWidth = availableWidth - labelAreaWidth;
        
        Rect barRect = new Rect(barX, y, barWidth, height);
        GUI.Box(barRect, "");
        
        if (value > 0f)
        {
            float fillWidth = barRect.width * value;
            Rect fillRect = new Rect(barRect.x + 2f, barRect.y + 2f, fillWidth - 4f, barRect.height - 4f);
            DrawSolidRectangle(fillRect, GRAY_COLOR);
        }
    }

    private void DrawSolidRectangle(Rect rect, Color color)
    {
        Handles.color = color;
        Vector3[] corners = {
            new Vector3(rect.xMin, rect.yMin, 0),
            new Vector3(rect.xMax, rect.yMin, 0),
            new Vector3(rect.xMax, rect.yMax, 0),
            new Vector3(rect.xMin, rect.yMax, 0)
        };
        Handles.DrawAAConvexPolygon(corners);
    }

    private void DrawDataVisualization(Rect boardRect)
    {
        float graphHeight = GetAdaptiveGraphHeight();
        float graphSpacing = 25f;
        
        float currentY = boardRect.yMax + 105f;
        DrawDataGraph(currentY, "COP X", COP_X_COLOR, copXData, true, graphHeight);
        
        currentY += graphHeight + graphSpacing;
        DrawDataGraph(currentY, "COP Y", COP_Y_COLOR, copYData, true, graphHeight);
        
        currentY += graphHeight + graphSpacing;
        DrawDataGraph(currentY, "Velocity COP X", COP_X_COLOR, vCopXData, false, graphHeight);
        
        currentY += graphHeight + graphSpacing;
        DrawDataGraph(currentY, "Velocity COP Y", COP_Y_COLOR, vCopYData, false, graphHeight);
    }

    private void DrawDataGraph(float yPosition, string title, Color lineColor, Queue<float> data, bool fixedRange = false, float graphHeight = 80f)
    {
        const float MARGIN = 20f;
        const float TITLE_TOP_MARGIN = 17f;
        const float TITLE_SIDE_MARGIN = 10f;
        const float Y_AXIS_LABEL_WIDTH = 30f;
        
        float graphWidth = position.width - (MARGIN * 2) - Y_AXIS_LABEL_WIDTH;
        Rect graphRect = new Rect(MARGIN + Y_AXIS_LABEL_WIDTH, yPosition, graphWidth, graphHeight);
        
        EditorGUI.DrawRect(graphRect, new Color(0.13f, 0.13f, 0.13f, 1f));
        
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 10,
            normal = { textColor = Color.white }
        };
        
        Rect titleRect = new Rect(graphRect.x + TITLE_SIDE_MARGIN, yPosition - TITLE_TOP_MARGIN, 
                                 graphRect.width - TITLE_SIDE_MARGIN * 2, 15f);
        GUI.Label(titleRect, title, titleStyle);
        
        DrawGraphGrid(graphRect);
        
        if (fixedRange)
        {
            DrawFixedRangeGraphData(graphRect, lineColor, data);
            DrawYAxisLabels(graphRect, -1f, 1f);
        }
        else
        {
            DrawRealTimeGraphData(graphRect, lineColor, data);
            if (data.Count > 0)
            {
                float[] dataArray = data.ToArray();
                float minVal = dataArray.Min();
                float maxVal = dataArray.Max();
                DrawYAxisLabels(graphRect, minVal, maxVal);
            }
        }
    }

    private void DrawYAxisLabels(Rect graphRect, float minValue, float maxValue)
    {
        const float LABEL_WIDTH = 25f;
        const float LABEL_HEIGHT = 15f;
        
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 8,
            normal = { textColor = new Color(0.7f, 0.7f, 0.7f, 1f) },
            alignment = TextAnchor.MiddleRight
        };
        
        for (int i = 0; i <= 4; i++)
        {
            float t = i / 4f;
            float value = Mathf.Lerp(maxValue, minValue, t);
            float y = graphRect.y + (graphRect.height / 4f) * i - LABEL_HEIGHT / 2f;
            
            Rect labelRect = new Rect(graphRect.x - LABEL_WIDTH, y, LABEL_WIDTH - 5f, LABEL_HEIGHT);
            GUI.Label(labelRect, value.ToString("F1"), labelStyle);
        }
    }

    private void DrawFixedRangeGraphData(Rect rect, Color lineColor, Queue<float> data)
    {
        if (data.Count < 2) return;
        
        float[] dataArray = data.ToArray();
        Vector3[] positions = CalculateFixedRangePositions(rect, dataArray, dataArray.Length);
        
        DrawDataLines(positions, lineColor);
        DrawDataMarkers(positions);
        
        Handles.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        
        float bottomY = rect.yMax;
        Handles.DrawLine(new Vector3(rect.xMin, bottomY, 0), new Vector3(rect.xMax, bottomY, 0));
        
        float middleY = rect.yMin + rect.height * 0.5f;
        Handles.DrawLine(new Vector3(rect.xMin, middleY, 0), new Vector3(rect.xMax, middleY, 0));
        
        float topY = rect.yMin;
        Handles.DrawLine(new Vector3(rect.xMin, topY, 0), new Vector3(rect.xMax, topY, 0));
    }

    private Vector3[] CalculateFixedRangePositions(Rect rect, float[] values, int count)
    {
        Vector3[] positions = new Vector3[count];
        
        const float minVal = -1f;
        const float maxVal = 1f;
        const float range = 2f;
        
        for (int i = 0; i < count; i++)
        {
            float x = rect.x + (rect.width / (count - 1)) * i;
            float clampedValue = Mathf.Clamp(values[i], minVal, maxVal);
            float normalizedValue = (clampedValue - minVal) / range;
            float y = rect.yMax - (normalizedValue * rect.height);
            positions[i] = new Vector3(x, y, 0);
        }
        
        return positions;
    }

    private void DrawGraphGrid(Rect rect)
    {
        Handles.color = new Color(0.7f, 0.7f, 0.7f, 0.5f);
        
        for (int i = 0; i <= 4; i++)
        {
            float y = rect.y + (rect.height / 4f) * i;
            Handles.DrawLine(new Vector3(rect.x, y), new Vector3(rect.xMax, y));
        }
        
        for (int i = 0; i <= 5; i++)
        {
            float x = rect.x + (rect.width / 5f) * i;
            Handles.DrawLine(new Vector3(x, rect.y), new Vector3(x, rect.yMax));
        }
    }

    private void DrawRealTimeGraphData(Rect rect, Color lineColor, Queue<float> data)
    {
        if (data.Count < 2) return;
        
        float[] dataArray = data.ToArray();
        Vector3[] positions = CalculateGraphPositions(rect, dataArray, dataArray.Length);
        
        DrawDataLines(positions, lineColor);
        DrawDataMarkers(positions);
    }

    private Vector3[] CalculateGraphPositions(Rect rect, float[] values, int count)
    {
        Vector3[] positions = new Vector3[count];
        
        float minVal = values.Min();
        float maxVal = values.Max();
        float range = maxVal - minVal;
        if (range == 0) range = 1f;
        
        for (int i = 0; i < count; i++)
        {
            float x = rect.x + (rect.width / (count - 1)) * i;
            float normalizedValue = (values[i] - minVal) / range;
            float y = rect.yMax - (normalizedValue * rect.height);
            positions[i] = new Vector3(x, y, 0);
        }
        
        return positions;
    }

    private void DrawDataLines(Vector3[] positions, Color lineColor)
    {
        Handles.color = lineColor;
        for (int i = 0; i < positions.Length - 1; i++)
        {
            Handles.DrawLine(positions[i], positions[i + 1]);
        }
    }

    private void DrawDataMarkers(Vector3[] positions)
    {
        Handles.color = GRAY_COLOR;
        for (int i = 0; i < positions.Length; i++)
        {
            Handles.DrawSolidDisc(positions[i], Vector3.forward, 2f);
        }
    }

    private void ProcessAnimation(Rect boardRect)
    {
        if (isConnected)
        {
            RenderCircle(boardRect);
            Repaint();
        }
    }

    private void RenderCircle(Rect boardRect)
    {
        Vector2 screenPosition = new Vector2(
            boardRect.center.x + centerOfPressure.x * boardRect.width * 0.4f,
            boardRect.center.y - centerOfPressure.y * boardRect.height * 0.4f - scrollPosition.y
        );
        
        Handles.color = CIRCLE_COLOR;
        Handles.DrawSolidDisc(screenPosition, Vector3.forward, CIRCLE_RADIUS);
    }
    
    #endregion
    
    #region Thread-Safe Data Updates
    
    private void UpdateUIFromLatestData()
    {
        if (!hasNewBasicData && !hasNewComplexData) return;
        
        lock (dataLock)
        {
            if (hasNewBasicData)
            {
                centerOfPressure = latestCOP;
                macAddress = latestMacAddress;
                Array.Copy(latestSensorValues, sensorValues, 4);
                
                copXData.Enqueue(latestCOP.x);
                copYData.Enqueue(latestCOP.y);
                
                while (copXData.Count > MAX_DATA_POINTS) copXData.Dequeue();
                while (copYData.Count > MAX_DATA_POINTS) copYData.Dequeue();
                
                hasNewBasicData = false;
            }
            
            if (hasNewComplexData)
            {
                velocityOfCOP = latestVelocity;
                currentStability = latestStability;
                dpsiMLSI = latestDpsiMLSI;
                dpsiAPSI = latestDpsiAPSI;
                dpsiVSI = latestDpsiVSI;
                dpsiOverall = latestDpsiOverall;
                
                vCopXData.Enqueue(latestVelocity.x);
                vCopYData.Enqueue(latestVelocity.y);
                
                while (vCopXData.Count > MAX_DATA_POINTS) vCopXData.Dequeue();
                while (vCopYData.Count > MAX_DATA_POINTS) vCopYData.Dequeue();
                
                hasNewComplexData = false;
            }
        }
    }
    
    #endregion
    
    #region Connection Management
    
    private void ToggleConnection()
    {
        if (isConnected)
        {
            DisconnectFromStream();
        }
        else
        {
            ConnectToStream();
        }
        Repaint();
    }

    private void ConnectToStream()
    {
        try
        {
            switch (connectionType)
            {
                case ConnectionType.LSL:
                    ConnectLSL();
                    break;
                case ConnectionType.TCP:
                    ConnectTCP();
                    break;
                default:
                    Debug.LogWarning("No connection type selected");
                    return;
            }
            
            isConnected = true;
            Debug.Log($"Connected via {connectionType}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Connection failed: {e.Message}");
            isConnected = false;
        }
    }

    private void DisconnectFromStream()
    {
        switch (connectionType)
        {
            case ConnectionType.LSL:
                DisconnectLSL();
                break;
            case ConnectionType.TCP:
                DisconnectTCP();
                break;
        }
        
        isConnected = false;
        ResetData();
        Debug.Log("Disconnected from stream");
    }
    
    #endregion
    
    #region LSL Implementation
    
    private void ConnectLSL()
    {
        if (LslMonitor.Source == null)
        {
            throw new Exception("LSL support not available. Install it from Balance Toolkit Demo > Setup.");
        }
        LslMonitor.Source.Connect(streamName);
    }

    private void DisconnectLSL()
    {
        LslMonitor.Source?.Disconnect();
    }

    private void UpdateLSLData()
    {
        LslMonitor.Source?.Poll(ProcessLSLBasicStream, ProcessLSLComplexStream);
    }
    
    #endregion
    
    #region TCP Implementation
    
    private void ConnectTCP()
    {
        try
        {
            ConnectTCPDualStreams();
        }
        catch (Exception e)
        {
            DisconnectTCP();
            throw new Exception($"TCP connection failed: {e.Message}");
        }
    }

    /// <summary>
    /// The desktop app broadcasts two binary streams and accepts no commands:
    /// raw samples (default port 11223) and processed metrics (default port 11224).
    /// The processed stream is optional; the window still works with the raw stream alone.
    /// </summary>
    private void ConnectTCPDualStreams()
    {
        ValidatePort(tcpRawPort, "Raw port");
        ValidatePort(tcpProcessedPort, "Processed port");
        
        rawFramer.Reset();
        processedFramer.Reset();
        
        tcpClientBasic = new TcpClient();
        tcpClientBasic.Connect(tcpAddress, tcpRawPort);
        tcpStreamBasic = tcpClientBasic.GetStream();
        
        tcpRunningBasic = true;
        tcpThreadBasic = new Thread(() => TCPReadThreadBasic());
        tcpThreadBasic.IsBackground = true;
        tcpThreadBasic.Start();
        
        Debug.Log($"Connected to raw stream at {tcpAddress}:{tcpRawPort}");
        
        try
        {
            tcpClientComplex = new TcpClient();
            tcpClientComplex.Connect(tcpAddress, tcpProcessedPort);
            tcpStreamComplex = tcpClientComplex.GetStream();
            
            tcpRunningComplex = true;
            tcpThreadComplex = new Thread(() => TCPReadThreadComplex());
            tcpThreadComplex.IsBackground = true;
            tcpThreadComplex.Start();
            
            Debug.Log($"Connected to processed stream at {tcpAddress}:{tcpProcessedPort}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Processed stream unavailable at {tcpAddress}:{tcpProcessedPort} ({e.Message}); velocity and stability will stay at zero.");
            if (tcpClientComplex != null) { tcpClientComplex.Close(); tcpClientComplex = null; }
            tcpStreamComplex = null;
        }
    }

    private static void ValidatePort(int port, string label)
    {
        if (port <= 0 || port > 65535)
        {
            throw new Exception($"{label} {port} is outside 1-65535");
        }
    }

    private void DisconnectTCP()
    {
        tcpRunningBasic = false;
        if (tcpThreadBasic != null && tcpThreadBasic.IsAlive)
        {
            tcpThreadBasic.Join(1000);
        }
        if (tcpStreamBasic != null)
        {
            tcpStreamBasic.Close();
            tcpStreamBasic = null;
        }
        if (tcpClientBasic != null)
        {
            tcpClientBasic.Close();
            tcpClientBasic = null;
        }
        
        tcpRunningComplex = false;
        if (tcpThreadComplex != null && tcpThreadComplex.IsAlive)
        {
            tcpThreadComplex.Join(1000);
        }
        if (tcpStreamComplex != null)
        {
            tcpStreamComplex.Close();
            tcpStreamComplex = null;
        }
        if (tcpClientComplex != null)
        {
            tcpClientComplex.Close();
            tcpClientComplex = null;
        }
    }

    private void TCPReadThreadBasic()
    {
        byte[] buffer = new byte[4096];
        
        while (tcpRunningBasic && tcpClientBasic != null && tcpClientBasic.Connected)
        {
            try
            {
                if (tcpStreamBasic.DataAvailable)
                {
                    int bytesRead = tcpStreamBasic.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        rawFramer.Feed(buffer, bytesRead, ProcessRawRecord);
                    }
                }
                Thread.Sleep(10);
            }
            catch (Exception e)
            {
                if (tcpRunningBasic)
                {
                    Debug.LogError($"TCP Basic stream read error: {e.Message}");
                }
                break;
            }
        }
    }

    private void TCPReadThreadComplex()
    {
        byte[] buffer = new byte[4096];
        
        while (tcpRunningComplex && tcpClientComplex != null && tcpClientComplex.Connected)
        {
            try
            {
                if (tcpStreamComplex.DataAvailable)
                {
                    int bytesRead = tcpStreamComplex.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        processedFramer.Feed(buffer, bytesRead, ProcessProcessedRecord);
                    }
                }
                Thread.Sleep(10);
            }
            catch (Exception e)
            {
                if (tcpRunningComplex)
                {
                    Debug.LogError($"TCP Complex stream read error: {e.Message}");
                }
                break;
            }
        }
    }
    
    #endregion
    
    #region Data Processing
    
    private void ProcessLSLBasicStream(float[] sample)
    {
        try
        {
            // Based on Python script: timestamp, mac_address, top_right, bottom_right, top_left, bottom_left, cop_x, cop_y
            if (sample.Length >= 8)
            {
                float timestamp = sample[0];
                ulong macAddr = (ulong)sample[1];
                float topRight = sample[2];
                float bottomRight = sample[3];
                float topLeft = sample[4];
                float bottomLeft = sample[5];
                float copX = sample[6];
                float copY = sample[7];
                
                Vector2 newCOP = new Vector2(copX, copY);
                
                lock (dataLock)
                {
                    latestSensorValues[0] = topLeft;      // TL
                    latestSensorValues[1] = topRight;     // TR
                    latestSensorValues[2] = bottomLeft;   // BL
                    latestSensorValues[3] = bottomRight;  // BR
                    
                    latestCOP = newCOP;
                    latestMacAddress = macAddr;
                    hasNewBasicData = true;
                }
                
                debugSampleCount++;
                double currentTime = EditorApplication.timeSinceStartup;
                if (currentTime - debugLastTime > 1.0)
                {
                    Debug.Log($"LSL Basic samples per second: {debugSampleCount}");
                    debugSampleCount = 0;
                    debugLastTime = currentTime;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"LSL Basic stream processing error: {e.Message}");
        }
    }

    private void ProcessLSLComplexStream(float[] sample)
    {
        try
        {
            // Based on Python script: timestamp, mac_address, v_cop_x, v_cop_y, stability_index, 
            // dpsi_mlsi, dpsi_apsi, dpsi_vsi, dpsi_overall
            if (sample.Length >= 9)
            {
                float timestamp = sample[0];
                float macAddr = sample[1];
                float vCopX = sample[2];
                float vCopY = sample[3];
                float stabilityIndex = sample[4];
                float dpsi_mlsi = sample[5];
                float dpsi_apsi = sample[6];
                float dpsi_vsi = sample[7];
                float dpsi_overall = sample[8];
                
                lock (dataLock)
                {
                    latestVelocity = new Vector2(vCopX, vCopY);
                    latestStability = Mathf.Clamp01(stabilityIndex);
                    latestDpsiMLSI = dpsi_mlsi;
                    latestDpsiAPSI = dpsi_apsi;
                    latestDpsiVSI = dpsi_vsi;
                    latestDpsiOverall = dpsi_overall;
                    hasNewComplexData = true;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"LSL Complex stream processing error: {e.Message}");
        }
    }

    private void ProcessRawRecord(byte[] record, int offset)
    {
        try
        {
            var sample = BalanceToolkitTcpProtocol.DecodeRaw(record, offset);
            
            lock (dataLock)
            {
                latestSensorValues[0] = sample.TopLeft;      // TL
                latestSensorValues[1] = sample.TopRight;     // TR
                latestSensorValues[2] = sample.BottomLeft;   // BL
                latestSensorValues[3] = sample.BottomRight;  // BR
                
                latestCOP = new Vector2(sample.CopX, sample.CopY);
                latestMacAddress = sample.MacAddress;
                hasNewBasicData = true;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Raw stream processing error: {e.Message}");
        }
    }

    private void ProcessProcessedRecord(byte[] record, int offset)
    {
        try
        {
            var sample = BalanceToolkitTcpProtocol.DecodeProcessed(record, offset);
            
            lock (dataLock)
            {
                latestVelocity = new Vector2(sample.VelocityCopX, sample.VelocityCopY);
                latestStability = Mathf.Clamp01(sample.StabilityIndex);
                latestDpsiMLSI = sample.DpsiMlsi;
                latestDpsiAPSI = sample.DpsiApsi;
                latestDpsiVSI = sample.DpsiVsi;
                latestDpsiOverall = sample.DpsiOverall;
                hasNewComplexData = true;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Processed stream processing error: {e.Message}");
        }
    }

    private void InitializeDataQueues()
    {
        copXData.Clear();
        copYData.Clear();
        vCopXData.Clear();
        vCopYData.Clear();
    }

    private void ResetData()
    {
        centerOfPressure = Vector2.zero;
        velocityOfCOP = Vector2.zero;
        currentStability = 0f;
        macAddress = 0;
        dpsiMLSI = 0f;
        dpsiAPSI = 0f;
        dpsiVSI = 0f;
        dpsiOverall = 0f;
        sensorValues = new float[4];
        InitializeDataQueues();
        
        lock (dataLock)
        {
            latestCOP = Vector2.zero;
            latestVelocity = Vector2.zero;
            latestStability = 0f;
            latestMacAddress = 0;
            latestDpsiMLSI = 0f;
            latestDpsiAPSI = 0f;
            latestDpsiVSI = 0f;
            latestDpsiOverall = 0f;
            latestSensorValues = new float[4];
            hasNewBasicData = false;
            hasNewComplexData = false;
        }
    }
    
    #endregion
    
    #region Update Loop
    
    private void Update()
    {
        if (isConnected && connectionType == ConnectionType.LSL)
        {
            UpdateLSLData();
            
            double currentTime = EditorApplication.timeSinceStartup;
            if (currentTime - lastUpdateTime > 0.016) // ~60 FPS
            {
                Repaint();
                lastUpdateTime = currentTime;
            }
        }
    }
    
    #endregion
}
