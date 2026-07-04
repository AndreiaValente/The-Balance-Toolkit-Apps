<table border="0">
  <tr>
    <td width="90" valign="middle" align="center">
      <a href="../README.md"><img src="../assets/logo.svg" width="72" alt="The Balance Toolkit"></a>
    </td>
    <td valign="middle">
      <h1>Stream Inspectors &middot; Python</h1>
      <p><strong>The Balance Toolkit - Apps</strong></p>
    </td>
  </tr>
</table>

[◀ All apps](../README.md) &middot; [Windows](../windows/) &middot; [Android](../android/) &middot; [Unity](../unity/) &middot; **Python**

---

Two small Python scripts for inspecting and monitoring the live data streams from The Balance Toolkit desktop application. Use them to verify that a board is streaming correctly and to see the raw force and center of pressure values in a table.

## Contents

| Script | Protocol | Dependencies |
|---|---|---|
| `tbt_stream_lsl.py` | Lab Streaming Layer (LSL) | `pylsl` |
| `tbt_stream_tcp.py` | TCP binary socket | None (standard library only) |

Both connect to a running instance of the desktop application (see [../windows/README.md](../windows/README.md)). They do not talk to the board directly.

## Prerequisites

Before running either script, start and configure the desktop application:

1. Launch The Balance Toolkit application.
2. Open the **Devices** menu and add a board.
3. Open the **Session** menu and add the board to your session.
4. Toggle the matching stream on: **LSL** for `tbt_stream_lsl.py`, **TCP** for `tbt_stream_tcp.py`.
5. Start recording to begin streaming.

## Requirements

- Python 3.7 or later

## Setup

Create and activate a virtual environment, then install dependencies.

**Windows (PowerShell):**

```powershell
python -m venv venv
venv\Scripts\activate
pip install pylsl   # only needed for the LSL script
```

**macOS / Linux:**

```bash
python3 -m venv venv
source venv/bin/activate
pip install pylsl   # only needed for the LSL script
```

The TCP script uses only the standard library, so `pip install` is not required for it.

## Run

**LSL inspector:**

```bash
python tbt_stream_lsl.py
```

It lists all available LSL streams with their metadata, lets you pick one by number, and prints incoming samples as a live table. Press `Ctrl+C` to stop.

**TCP inspector:**

```bash
python tbt_stream_tcp.py
```

It connects to the toolkit's TCP endpoint (`localhost:11223`, IPv6), tests connectivity, and streams decoded samples along with timing statistics. Press `Ctrl+C` to stop.

## Stream reference

### LSL streams (100 Hz)

- **`the-balance-toolkit_basic`** (8 channels): `timestamp`, `mac_address`, `top_right`, `bottom_right`, `top_left`, `bottom_left`, `cop_x`, `cop_y`.
- **`the-balance-toolkit_complex`** (9 channels): velocity of center of pressure, stability index, and directional posturography indices.

### TCP binary format (40 bytes per sample, big-endian)

| Bytes | Field | Type |
|---|---|---|
| 0-7 | `timestamp` (microseconds) | int64 |
| 8-15 | `mac_address` | uint64 |
| 16-19 | `top_right` (kg) | float32 |
| 20-23 | `bottom_right` (kg) | float32 |
| 24-27 | `top_left` (kg) | float32 |
| 28-31 | `bottom_left` (kg) | float32 |
| 32-35 | `cop_x` (normalized) | float32 |
| 36-39 | `cop_y` (normalized) | float32 |

## Troubleshooting

| Symptom | Fix |
|---|---|
| No LSL streams found | Confirm the app is running, a board is added to a session, LSL streaming is on, and recording is started. Check that the firewall allows LSL. |
| TCP connection fails | Confirm TCP streaming is on and recording is started. Verify the app is on the same machine and the firewall allows port 11223. |
| `ModuleNotFoundError: pylsl` | Activate the virtual environment and run `pip install pylsl`. |
