<table border="0">
  <tr>
    <td width="90" valign="middle" align="center">
      <a href="../README.md"><img src="../assets/logo.svg" width="72" alt="The Balance Toolkit"></a>
    </td>
    <td valign="middle">
      <h1>Companion App &middot; Android</h1>
      <p><strong>The Balance Toolkit - Apps</strong></p>
    </td>
  </tr>
</table>

[◀ All apps](../README.md) &middot; [Windows](../windows/) &middot; **Android** &middot; [Unity](../unity/) &middot; [Python](../python/)

---

The Android companion app connects to a Wii Balance Board over Bluetooth for live center of pressure visualization and standalone capture on a phone or tablet. It runs on its own and does not require the desktop application.

This folder contains the prebuilt Android package.

## Contents

| File | Description |
|---|---|
| `the-balance-toolkit.apk` | Android application package (sideloaded install) |

## Requirements

- Android device with Bluetooth
- Permission to install apps from outside the Play Store (sideloading)
- A Wii Balance Board

## Install

The app is distributed as an APK and installed by sideloading, since it is not on the Play Store.

1. Transfer `the-balance-toolkit.apk` to your Android device (USB, email, or download it directly on the device).
2. Open the file with a file manager.
3. When prompted, allow installs from this source:
   - **Settings, Apps, Special access, Install unknown apps**, then enable the app you are installing from (for example your browser or file manager).
4. Tap **Install**, then **Open**.

If your device warns that the app was scanned or is from an unknown developer, this is expected for a sideloaded research app. Choose to install anyway.

## Use

1. Turn on your Wii Balance Board by pressing its sync button.
2. Open the app and grant the Bluetooth and location permissions it requests. Android requires location permission for Bluetooth scanning.
3. Pair and connect to the board from within the app.
4. View live center of pressure and force data, and capture sessions on the device.

## Troubleshooting

| Symptom | Fix |
|---|---|
| Install is blocked | Enable install from unknown sources for the app you are installing from (see Install step 3). |
| No boards found when scanning | Grant Bluetooth and location permissions, press the board's sync button again, and rescan. |
| Board disconnects | Replace the board's batteries and keep the device within a few meters. |
