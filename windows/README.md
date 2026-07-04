<table border="0">
  <tr>
    <td width="90" valign="middle" align="center">
      <a href="../README.md"><img src="../assets/logo.svg" width="72" alt="The Balance Toolkit"></a>
    </td>
    <td valign="middle">
      <h1>Desktop Application &middot; Windows</h1>
      <p><strong>The Balance Toolkit - Apps</strong></p>
    </td>
  </tr>
</table>

[← All apps](../README.md) &middot; **Windows** &middot; [Android](../android/) &middot; [Unity](../unity/) &middot; [Python](../python/)

---

> This is part of **The Balance Toolkit - Apps**, the software accompanying our CHI PLAY 2026 paper. See the [main README](../README.md) for the full overview.

The desktop application is the core of The Balance Toolkit. It pairs with one or two Wii Balance Boards over Bluetooth, computes center of pressure and posturography metrics, records sessions, and streams live data over TCP and LSL for use in games and analysis tools.

The app is built in Rust with a Tauri interface. This folder contains the prebuilt Windows executable.

## Contents

| File | Description |
|---|---|
| `the-balance-toolkit.exe` | Standalone Windows application (no installer required) |

## Requirements

- Windows 10 or 11 (64-bit)
- Bluetooth adapter (built-in or USB dongle)
- One or two Wii Balance Boards

## Install and run

1. Download `the-balance-toolkit.exe` from this folder.
2. Double-click to launch. No installation is needed.
3. If Windows SmartScreen shows a warning because the app is not code-signed, click **More info**, then **Run anyway**.

## First-use walkthrough

1. Turn on your Wii Balance Board by pressing its sync button.
2. In the app, open the **Devices** menu and add the board. Follow the Bluetooth pairing prompts.
3. Open the **Session** menu and add the paired board to a session.
4. To send data to Unity, Python, or another tool, toggle **TCP** or **LSL** streaming on in the session configuration.
5. Press record to begin capturing and streaming data.

Once streaming is on, connect a client from the [`unity/`](../unity/) or [`python/`](../python/) folder, or use the Android companion app in [`android/`](../android/).

## Other platforms (macOS and Linux)

The toolkit also runs on macOS and Linux. Those builds are not included in this repository. To use them, build from the main Balance Toolkit source (see the root [README.md](../README.md)) or check the project's releases.

## Troubleshooting

| Symptom | Fix |
|---|---|
| Windows blocks the app on launch | Click **More info**, then **Run anyway**. The binary is unsigned. |
| Board will not pair | Press the board's sync button again, remove any stale pairing in Windows Bluetooth settings, and retry from the **Devices** menu. |
| No data reaches Unity or Python | Confirm TCP or LSL streaming is toggled on and recording is started. Check that Windows Firewall allows the app. |
| App will not start | Confirm you are on 64-bit Windows 10 or 11. |

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
