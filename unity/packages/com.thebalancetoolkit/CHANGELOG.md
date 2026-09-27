# Changelog

## 1.0.0

- All menu items live under one top-level **Balance Toolkit Demo** menu: Monitor, Setup, Create Demo Scene, Create TCP Example Scene and LSL Streams. They were previously split across Tools, Window and LSL.
- One monitor window (Balance Toolkit Demo > Monitor) for both TCP and LSL. The TCP host, raw port and processed port are editable and kept across script reloads. LSL input comes from the LSL Integration sample, which registers an LSL source with the window instead of shipping a second window; the core package still has no LSL dependency. The LSL stream name follows the app's naming (`<name>_basic` and `<name>_complex`), or connects to a single stream when the name ends in `_basic` or `_complex`.
- The setup window's Demo section is a single **Add the demo scene** button that installs missing dependencies, imports the sample and creates the scene, resuming across script reloads. Added Balance Toolkit Demo > Create Demo Scene to assemble a playable scene from the Demo sample's art, since the original scene's Game prefab is missing. The demo scene compiles with the core package alone; the template effects, camera controls and benchmarks are constrained to compile only when URP 17, Cinemachine 3, Input System and Timeline are present, so a missing dependency no longer breaks the whole sample.
- Fixed TCP reception: the desktop app streams fixed-size big-endian binary records (40 bytes raw on port 11223, 44 bytes processed on port 11224) and accepts no commands. The controller and the monitor window previously parsed comma-separated text and sent a `STREAM` command, so no sample was ever decoded. Added `BalanceToolkitTcpProtocol` (shared decoder and record framer); `MacAddress` is now a `ulong`; the `streamName` field was removed from `BalanceBoardControllerTCP`; the monitor window reads the processed stream from port 11224.
- Repackaged the legacy `.unitypackage` as a single UPM package. TCP support is built in; LSL integration and the demo are opt-in samples.
- Added a setup window (Balance Toolkit Demo > Setup) that installs sample dependencies and imports samples.
- Added a TCP Example sample that generates a starting scene.
- Scoped the `LSL_AVAILABLE` define to the LSL sample assemblies through version defines; no global scripting define is needed.
- Unity 6.6 compatibility: removed legacy `Execute` render-pass overrides from `FullscreenEffect` and `OasisFog` (Render Graph path retained); imported `UnityEngine.Rendering` for `GraphicsStateCollection`; made the first-person input namespace import unconditional; removed an unused `UnityEditor` import from `QualityLevelToggle`; the TCP dispatcher lookup uses `FindAnyObjectByType` on newer editors.
- Bundled an LSL4Unity `1.16.1-balance.1` compatibility build that fixes a `GetInstanceID()` call removed in Unity 6.6. See its BALANCE-COMPATIBILITY.md.
- Dropped the third-party Animation Rigging samples that the legacy archive copied in.
