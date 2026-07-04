<table border="0">
  <tr>
    <td width="150" valign="middle" align="center">
      <img src="assets/logo.svg" width="130" alt="The Balance Toolkit logo">
    </td>
    <td valign="middle">
      <h1>The Balance Toolkit - Apps</h1>
      <p><em>Applications, integrations, and companion files for our CHI PLAY 2026 paper.</em></p>
    </td>
  </tr>
</table>

**[Windows](windows/)** &middot; **[Android](android/)** &middot; **[Unity](unity/)** &middot; **[Python](python/)**

---

This repository holds the software that accompanies the paper **_The Balance Toolkit: Democratizing Balance-Based Interaction Through Open-Source Software for Repurposed Wii Balance Boards_** (CHI PLAY 2026). It contains the ready-to-run applications and the integration code described in the paper, so that readers can reproduce and build on our work.

The Balance Toolkit turns an inexpensive consumer Wii Balance Board (WBB) into a research and development platform for balance-based games, rehabilitation, and accessible play. It handles Bluetooth pairing, reads the four force sensors, computes center of pressure (CoP) and posturography metrics, and streams the data in real time to games and analysis tools over TCP and Lab Streaming Layer (LSL).

## Video walkthrough

<a href="https://youtu.be/_8UwrUgqUao">
  <img src="https://img.youtube.com/vi/_8UwrUgqUao/maxresdefault.jpg" width="640" alt="Watch The Balance Toolkit walkthrough on YouTube">
</a>

Watch the full walkthrough on [YouTube](https://youtu.be/_8UwrUgqUao).

## Repository layout

Each folder has its own README with install and run instructions.

| Folder | Contents | Guide |
|---|---|---|
| [`windows/`](windows/) | Desktop application for Windows (`.exe`) | [Read](windows/README.md) |
| [`android/`](android/) | Android companion application (`.apk`) | [Read](android/README.md) |
| [`unity/`](unity/) | Unity package for receiving live CoP data in a game engine | [Read](unity/README.md) |
| [`python/`](python/) | Python scripts for inspecting the LSL and TCP data streams | [Read](python/README.md) |

The desktop application is the core of the toolkit. The Unity and Python folders are integration examples that consume the data it streams. The Android app is a companion for live visualization and standalone capture.

## What the toolkit does

- **Cross-platform desktop app** for Windows, macOS, and Linux, built in Rust with a Tauri interface. Only the Windows build is included here. See [windows/README.md](windows/README.md) for other platforms.
- **Bluetooth connectivity** to one or two Wii Balance Boards at once (dual-board support).
- **Data processing pipeline** that computes center of pressure, sway, spatial and stability metrics, and frequency analysis.
- **Real-time streaming** over TCP (binary protocol) and LSL, so games and analysis tools can consume live data.
- **Six posturography activity templates** (Eyes Open-Close, Functional Reach Test, Single Leg Stance, Tandem Stance, Timed Up and Go, and Squat) that double as validated game mechanics and clinical protocols.
- **Session recording and replay** for later analysis.
- **Game engine and Python integration** through the samples in this repository.

## Quick start

1. Connect a Wii Balance Board and run the desktop application from [`windows/`](windows/) (or build it for macOS/Linux).
2. Add your board under the **Devices** menu, then add it to a session under the **Session** menu.
3. Toggle **TCP** or **LSL** streaming on, and start recording.
4. Consume the live stream from **Unity** ([`unity/`](unity/)) or **Python** ([`python/`](python/)).

The Android companion app in [`android/`](android/) runs on its own and connects to a board directly over Bluetooth.

## Data stream reference

The toolkit exposes two streams at 100 Hz:

- **`the-balance-toolkit_basic`** (8 channels): `timestamp`, `mac_address`, `top_right`, `bottom_right`, `top_left`, `bottom_left`, `cop_x`, `cop_y`.
- **`the-balance-toolkit_complex`** (9 channels): adds derived stability measures (CoP velocity, stability index, and directional posturography indices).

Force values are in kilograms. CoP values are normalized to roughly -1 to +1. Full binary and channel layouts are documented in [python/README.md](python/README.md).

## Citation

If you use The Balance Toolkit in your research, please cite our paper.

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

## License

See the paper and repository for licensing terms. The toolkit is released as open-source software to broaden access to the more than 42 million Wii Balance Boards already manufactured.

---

<sub>The Balance Toolkit &middot; The Empathic Computing Laboratory, Auckland Bioengineering Institute, University of Auckland</sub>
