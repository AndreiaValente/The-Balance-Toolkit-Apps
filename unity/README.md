<table border="0">
  <tr>
    <td width="90" valign="middle" align="center">
      <a href="../README.md"><img src="../assets/logo.svg" width="72" alt="The Balance Toolkit"></a>
    </td>
    <td valign="middle">
      <h1>Game Engine Integration &middot; Unity</h1>
      <p><strong>The Balance Toolkit - Apps</strong></p>
    </td>
  </tr>
</table>

[← All apps](../README.md) &middot; [Windows](../windows/) &middot; [Android](../android/) &middot; **Unity** &middot; [Python](../python/) &middot; [Source code](https://github.com/trecitano/The-Balance-Toolkit)

---

> This is part of **The Balance Toolkit - Apps**, the software accompanying our CHI PLAY 2026 paper. See the [main README](../README.md) for the full overview.

A Unity Package Manager (UPM) package for receiving **Wii Balance Board** center-of-pressure (COP) data from [The Balance Toolkit](../windows/) desktop application and mapping it to GameObject movement in your scene.

The package installs **TCP** support, which needs nothing else. Everything optional ships as a sample you import when you want it:

| Sample | Contents | Requires |
|---|---|---|
| **TCP Example** | Generates a ready-made starting scene | Nothing |
| **LSL Integration** | LSL controller, and LSL input for the Editor monitor | The bundled LSL4Unity compatibility build |
| **Demo** | Original art, URP effects, camera controls and benchmarks | Unity 6.6 with a URP project |

Imported samples are copied into `Assets/Samples`, where you can edit them or take what you need. Core requires Unity 2020.3 or later; this release was validated with Unity **6000.6.0f1**.

---

## Install

1. Download this repository: clone it, or on GitHub choose **Code → Download ZIP** and extract it. Keep the folder; Unity references the package from it rather than copying it into your project.
2. In Unity, open **Window → Package Manager**, click **+** and choose **Install package from disk**.
3. Select [`unity/packages/com.thebalancetoolkit/package.json`](packages/com.thebalancetoolkit/package.json).

The toolkit's setup window opens once the package compiles. It installs sample dependencies and imports samples for you. Reopen it at any time through **Balance Toolkit Demo → Setup**.

The LSL sample needs the bundled LSL4Unity compatibility build (`1.16.1-balance.1`), which fixes an upstream call removed in Unity 6.6 and replaces any other LSL4Unity install. The setup window installs it from [`packages/com.labstreaminglayer.lsl4unity`](packages/com.labstreaminglayer.lsl4unity), next to the toolkit in the downloaded folder. See its [provenance notes](packages/com.labstreaminglayer.lsl4unity/BALANCE-COMPATIBILITY.md).

In the setup window, **Add the demo scene** installs any missing dependencies (URP, Cinemachine, Timeline, Input System), imports the Demo sample and generates the scene in one step; packages you already have are skipped, and your project settings are not changed. Use a URP project with Render Graph, and enable the Input System if you use its first-person controls. Its original `TBT.unity` scene is incomplete (its Game prefab is missing from the source archive). The generated scene replaces it: it contains a playable scene from the demo art, with the board model tilting under the center of pressure, the Balu character cheering on connection, and a status panel. The scene and its animator controller are written to `Assets/TheBalanceToolkit Demo/`.

---

## Quick Start

**Generated example:** open **Balance Toolkit Demo → Setup** and import **TCP Example**, then choose **Balance Toolkit Demo → Create TCP Example Scene**. Save the scene, start the TCP stream in The Balance Toolkit, and press Play.

**Manual setup:**

1. Create a **Plane** in your scene. This defines the movement area.
2. Create a child **GameObject** (e.g. a Sphere) under it. This is the cursor.
3. Attach `BalanceBoardControllerTCP` to the cursor and assign the plane as **Parent Plane**.
4. Start the TCP stream in The Balance Toolkit, then press Play.

```
Scene Hierarchy
└── MovementPlane        ← assign as "Parent Plane"
    └── Cursor (Sphere)  ← attach controller script here
```

For LSL, import the **LSL Integration** sample and attach `BalanceBoardControllerLSL` instead. The Editor monitor is under **Balance Toolkit Demo → Monitor**; choose **TCP** (host, raw port and processed port are editable) or **LSL** (available once the LSL Integration sample is imported).

---

## Inspector Settings

| Property | Type | Description |
|---|---|---|
| **Stream Name** | `string` | Name of the Balance Toolkit data stream (default `the-balance-toolkit_basic`). Must match exactly. |
| **Parent Plane** | `Transform` | The plane defining the movement area. Auto-detects the parent transform if left empty. |
| **Smoothing Factor** | `float` (1–20) | Higher values = snappier response; lower values = smoother but laggier movement. |
| **Objects to Hide** | `GameObject[]` | Renderers hidden on connection and restored on disconnection. Useful for placeholder visuals. |
| **Show Debug Info** | `bool` | On-screen HUD and console logs with connection status, COP, sensor data and sample rate. Scene gizmos show the plane bounds and COP target when the object is selected. |

TCP only:

| Property | Type | Description |
|---|---|---|
| **Server Address** | `string` | IP address of the TCP server (default `127.0.0.1`). |
| **Server Port** | `int` | TCP port (default `11223`). |
| **Auto Reconnect** | `bool` | Reconnect automatically on disconnection. |
| **Reconnect Delay** | `float` | Seconds between reconnection attempts. |
| **Max Reconnect Attempts** | `int` | Retries before giving up. |

---

## How It Works

Each sample contains 8 channels: `timestamp, mac_address, top_right, bottom_right, top_left, bottom_left, cop_x, cop_y`. Sensor values are in kg; `cop_x` and `cop_y` are normalised to approximately −1 to +1.

On `Start()` the script connects to the stream (TCP socket on a background thread, or an LSL inlet polled each frame). COP values are mapped to world-space positions within 90% of the parent plane's bounds and interpolated each frame with `Vector3.Lerp`. On disconnect or scene exit the connection is closed, hidden objects are restored, and the cursor resets to the plane centre.

---

## Public API

```csharp
var controller = GetComponent<BalanceBoardControllerTCP>();

bool connected  = controller.IsConnected;
Vector2 cop     = controller.CenterOfPressure;  // normalised COP (x, y)
float[] sensors = controller.SensorValues;       // [TL, TR, BL, BR]
int sampleRate  = controller.SamplesPerSecond;
ulong mac       = controller.MacAddress;    // board MAC, format with BalanceToolkitTcpProtocol.FormatMac
```

| Method | Description |
|---|---|
| `Connect()` | Manually initiate connection. |
| `Disconnect()` | Disconnect and reset. |
| `ResetToCenter()` | Reset cursor to the centre of the parent plane. |
| `ResetReconnectCounter()` | TCP only. Reset the reconnect counter and re-enable auto-reconnect. |

All methods are also available from the component's context menu in the Inspector.

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| `No parent plane assigned!` | Assign a Transform to **Parent Plane** or make the object a child of your movement plane. |
| LSL script disables itself | Install the LSL integration through **Balance Toolkit Demo → Setup**. |
| `Connection failed` (TCP) | Check that The Balance Toolkit TCP server is running and **Server Address** / **Server Port** are correct. |
| No movement / 0 samples/sec | Make sure **Stream Name** matches the name configured in The Balance Toolkit. |
| Jerky movement | Lower the **Smoothing Factor**. |
| Cursor overshoots plane edges | Values are clamped to ±1; check your Balance Toolkit calibration. |

---

## Package Development

Sources live in `packages/`. The toolkit is one package; the patched LSL4Unity fork is kept as a separate package because it carries native libraries and must be resolvable by name. Because projects install the packages from disk, edits under `packages/` show up in those projects the next time Unity recompiles.

---

## Citation

This software accompanies our CHI PLAY 2026 paper. If you use The Balance Toolkit in your research, please cite it.

**APA**

> Valente, A., Kothari, N., Ahmed-Mahmoud, H., Esteves, A., & Billinghurst, M. (2026). The Balance Toolkit: Democratizing balance-based interaction through open-source software for repurposed Wii Balance Boards. In *Proceedings of the Annual Symposium on Computer-Human Interaction in Play (CHI PLAY '26)*. ACM.

**BibTeX**

```bibtex
@inproceedings{valente2026balancetoolkit,
  title     = {The Balance Toolkit: Democratizing Balance-Based Interaction
               Through Open-Source Software for Repurposed Wii Balance Boards},
  author    = {Valente, Andreia and Kothari, Nidhi and Ahmed-Mahmoud, Hana
               and Esteves, Augusto and Billinghurst, Mark},
  booktitle = {Annual Symposium on Computer-Human Interaction in Play (CHI PLAY '26)},
  year      = {2026},
  address   = {York, UK},
  publisher = {ACM}
}
```

DOI to be added on publication.

---

<sub><a href="../README.md">The Balance Toolkit - Apps</a> &middot; The Empathic Computing Laboratory, University of Auckland</sub>
