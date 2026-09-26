# Balance Toolkit compatibility build

Upstream: https://github.com/labstreaminglayer/LSL4Unity
Upstream commit: `75d54ab160edaf02d2173350bbd91f1afbdb66d8` (package version 1.16.0).
Local version: `1.16.1-balance.1`. This is a local compatibility build, not an upstream release.

Changes: `Runtime/Scripts/BaseOutlet.cs` uses `GetEntityId().ToString()` instead of removed `GetInstanceID()` on Unity 6.6+. Earlier editors retain the original code. `Editor/LSLEditorIntegration.cs` moves the **Show Streams** menu item from `LSL > Show Streams` to `Balance Toolkit Demo > LSL Streams`, so every toolkit menu item sits under one menu. Native libraries and the LSL binding are unchanged. Package name and assembly names are retained for interoperability.

The original LICENSE is included. Native plugins retain their upstream terms and notices. The optional toolkit installer selects this build; it replaces an existing upstream package with the same package name when needed. No Unity package cache files are patched in place.
