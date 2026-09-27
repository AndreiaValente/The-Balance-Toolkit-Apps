using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace TheBalanceToolkit.Editor
{
    /// <summary>
    /// Installs the dependencies of the optional samples and imports them.
    /// TCP support needs nothing; the LSL sample needs the bundled LSL4Unity build and the demo needs URP and friends.
    /// </summary>
    [InitializeOnLoad]
    public sealed class ToolkitSetupWindow : EditorWindow
    {
        private const string Package = "com.thebalancetoolkit";
        private const string LslLibrary = "com.labstreaminglayer.lsl4unity";
        private const string LslLibraryVersion = "1.16.1-balance.1";
        private const string TcpSample = "TCP Example";
        private const string LslSample = "LSL Integration";
        private const string DemoSample = "Demo";
        private const string StatusKey = "TheBalanceToolkit.Setup.Status";

        // Requested without versions so Package Manager resolves the versions recommended for the running editor.
        private static readonly string[] DemoDependencies =
        {
            "com.unity.render-pipelines.universal",
            "com.unity.cinemachine",
            "com.unity.timeline",
            "com.unity.inputsystem",
            "com.unity.ugui",
            "com.unity.burst",
            "com.unity.mathematics"
        };

        private static AddAndRemoveRequest request;

        [MenuItem("Balance Toolkit Demo/Setup", false, 2)]
        public static void Open() => GetWindow<ToolkitSetupWindow>("Balance Toolkit Setup");

        private void OnGUI()
        {
            bool busy = request != null || EditorApplication.isCompiling || EditorApplication.isUpdating;
            EditorGUILayout.LabelField("The Balance Toolkit", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("TCP works out of the box. Everything below is optional.", MessageType.Info);

            DrawSample(TcpSample, "Generates a starting scene. After importing, use Balance Toolkit Demo > Create TCP Example Scene.", true, busy);

            bool lslReady = Installed(LslLibrary, LslLibraryVersion);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("LSL Integration", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Adds LSL as an input option alongside TCP. Step 1 replaces any other LSL4Unity package in the project.", EditorStyles.wordWrappedLabel);
            using (new EditorGUI.DisabledScope(busy || lslReady))
                if (GUILayout.Button(lslReady ? "1. LSL4Unity installed" : "1. Install LSL4Unity compatibility build")) InstallLslLibrary();
            DrawImportButton(LslSample, "2. Import LSL Integration sample", busy || !lslReady);

            bool demoSupported = SupportsDemo(Application.unityVersion);
            bool demoRunning = SessionState.GetBool(DemoPendingKey, false);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Demo", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Creates a playable demo scene. Missing dependencies are installed automatically.", EditorStyles.wordWrappedLabel);
            if (!demoSupported) EditorGUILayout.HelpBox("The demo requires Unity 6.6 or later.", MessageType.Warning);
            using (new EditorGUI.DisabledScope(busy || demoRunning || !demoSupported))
                if (GUILayout.Button(demoRunning ? "Adding the demo scene..." : "Add the demo scene")) AddDemoScene();

            string status = SessionState.GetString(StatusKey, "");
            EditorGUILayout.Space();
            if (status.Length > 0) EditorGUILayout.HelpBox(status, MessageType.Info);
            if (GUILayout.Button("Open monitor"))
                EditorApplication.ExecuteMenuItem("Balance Toolkit Demo/Monitor");
        }

        private static void DrawSample(string name, string description, bool available, bool busy)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedLabel);
            DrawImportButton(name, "Import " + name, busy || !available);
        }

        private static void DrawImportButton(string sampleName, string label, bool disabled)
        {
            var sample = FindSample(sampleName);
            bool imported = sample.HasValue && sample.Value.isImported;
            using (new EditorGUI.DisabledScope(disabled || imported || !sample.HasValue))
                if (GUILayout.Button(imported ? label + " (imported)" : label)) ImportSample(sampleName);
        }

        private static PackageInfo ToolkitPackage() => PackageInfo.FindForAssembly(typeof(ToolkitSetupWindow).Assembly);

        private static bool Installed(string name, string version = null) =>
            PackageInfo.GetAllRegisteredPackages().Any(p => p.name == name && (version == null || p.version == version));

        private static bool SupportsDemo(string version)
        {
            var parts = version.Split('.');
            return parts.Length >= 2 && int.TryParse(parts[0], out var major) &&
                int.TryParse(parts[1], out var minor) && (major > 6000 || major == 6000 && minor >= 6);
        }

        // The generator lives in the Demo sample, so it only exists once the sample has compiled.
        private static bool DemoGeneratorExists() =>
            AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "TheBalanceToolkit.Demo.Scene.Editor");

        private static bool SampleImported(string displayName)
        {
            var sample = FindSample(displayName);
            return sample.HasValue && sample.Value.isImported;
        }

        private static Sample? FindSample(string displayName)
        {
            var package = ToolkitPackage();
            if (package == null) return null;
            foreach (var sample in Sample.FindByPackage(Package, package.version))
                if (sample.displayName == displayName) return sample;
            return null;
        }

        internal static void ImportSample(string displayName)
        {
            try
            {
                var sample = FindSample(displayName);
                if (sample == null) throw new InvalidOperationException("Sample not found: " + displayName);
                if (sample.Value.isImported || sample.Value.Import(Sample.ImportOptions.OverridePreviousImports))
                    SessionState.SetString(StatusKey, displayName + " imported into Assets/Samples.");
                else
                    throw new InvalidOperationException("Import was cancelled or failed.");
            }
            catch (Exception error)
            {
                Fail(error);
            }
        }

        internal static void InstallLslLibrary()
        {
            try
            {
                var source = LocateLslLibrary();
                if (source == null) return; // The folder picker was cancelled.
                Request(new[] { source }, "LSL4Unity compatibility build");
            }
            catch (Exception error)
            {
                Fail(error);
            }
        }

        internal static void InstallDemoDependencies()
        {
            try
            {
                if (!SupportsDemo(Application.unityVersion))
                    throw new InvalidOperationException("The demo requires Unity 6.6 or later.");
                Request(DemoDependencies, "demo dependencies");
            }
            catch (Exception error)
            {
                Fail(error);
            }
        }

        private static void Request(string[] packages, string what)
        {
            request = Client.AddAndRemove(packages);
            SessionState.SetString(StatusKey, "Installing " + what + ". Unity may reload scripts; see Package Manager for resolution errors.");
            EditorApplication.update += Poll;
        }

        // ---- Demo scene pipeline -------------------------------------------------------------
        // Installing packages and importing the sample each trigger a script reload, which wipes
        // static state. The pipeline therefore records its intent in SessionState and resumes
        // from the static constructor after every reload until the scene exists.

        private const string DemoPendingKey = "TheBalanceToolkit.Setup.DemoPending";
        private const string DemoSceneMenu = "Balance Toolkit Demo/Create Demo Scene";

        /// <summary>One-click demo: install missing dependencies, import the sample, create the scene.</summary>
        internal static void AddDemoScene()
        {
            if (!SupportsDemo(Application.unityVersion))
            {
                Fail(new InvalidOperationException("The demo requires Unity 6.6 or later."));
                return;
            }
            SessionState.SetBool(DemoPendingKey, true);
            SessionState.SetString(StatusKey, "Adding the demo scene...");
            EditorApplication.delayCall += ContinueDemo;
        }

        private static void ContinueDemo()
        {
            if (!SessionState.GetBool(DemoPendingKey, false)) return;
            if (request != null || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ContinueDemo;
                return;
            }
            try
            {
                var missing = MissingDemoDependencies();
                if (missing.Length > 0)
                {
                    // Poll() resumes the pipeline when the request completes; a script reload resumes it too.
                    Request(missing, "demo dependencies (" + string.Join(", ", missing) + ")");
                    return;
                }
                if (!SampleImported(DemoSample))
                {
                    // Importing triggers a reload; the static constructor picks the pipeline up again.
                    ImportSample(DemoSample);
                    if (!SampleImported(DemoSample)) throw new InvalidOperationException("The Demo sample could not be imported.");
                    SessionState.SetString(StatusKey, "Demo sample imported; waiting for Unity to compile it...");
                    EditorApplication.delayCall += ContinueDemo;
                    return;
                }
                if (!DemoGeneratorExists())
                {
                    // Imported but not yet compiled (or its assembly reload has not happened yet).
                    EditorApplication.delayCall += ContinueDemo;
                    return;
                }
                SessionState.SetBool(DemoPendingKey, false);
                if (!EditorApplication.ExecuteMenuItem(DemoSceneMenu))
                    throw new InvalidOperationException("The demo scene generator menu item was not found.");
                SessionState.SetString(StatusKey, "Demo scene created. Start the desktop app with TCP streaming on, then press Play.");
            }
            catch (Exception error)
            {
                SessionState.SetBool(DemoPendingKey, false);
                Fail(error);
            }
        }

        // Cinemachine 2 ships a different namespace than the demo uses, so it counts as missing and is upgraded.
        private static string[] MissingDemoDependencies()
        {
            var packages = PackageInfo.GetAllRegisteredPackages();
            return DemoDependencies.Where(name =>
            {
                var installed = packages.FirstOrDefault(p => p.name == name);
                if (installed == null) return true;
                return name == "com.unity.cinemachine" && MajorVersion(installed.version) < 3;
            }).ToArray();
        }

        private static int MajorVersion(string version)
        {
            var dot = version.IndexOf('.');
            return int.TryParse(dot > 0 ? version.Substring(0, dot) : version, out var major) ? major : 0;
        }

        private static void Fail(Exception error)
        {
            SessionState.SetString(StatusKey, "Failed: " + error.Message);
            Debug.LogException(error);
        }

        private static string ProjectKey(string setting) => "TheBalanceToolkit.Setup." + setting + ":" + Application.dataPath;

        static ToolkitSetupWindow()
        {
            // The static constructor can run while Unity constructs a ToolkitSetupWindow
            // restored from a saved layout, where editor state such as Application.isBatchMode
            // must not be read. Defer every such query to the delayed callback.
            EditorApplication.delayCall += OpenOnFirstInstall;
            EditorApplication.delayCall += ContinueDemo;
        }

        private static void OpenOnFirstInstall()
        {
            if (Application.isBatchMode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += OpenOnFirstInstall;
                return;
            }
            if (EditorPrefs.GetBool(ProjectKey("Opened"), false)) return;
            Open();
            EditorPrefs.SetBool(ProjectKey("Opened"), true);
        }

        /// <summary>Directory of the tarball the toolkit was installed from, or null when it was not installed from a tarball.</summary>
        private static string BundleDirectory(string packageId, string projectDirectory)
        {
            const string marker = "@file:";
            int index = packageId.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0) return null;
            string source = packageId.Substring(index + marker.Length).Replace('\\', '/');
            if (!source.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase)) return null;
            if (!Path.IsPathRooted(source)) source = Path.Combine(projectDirectory, "Packages", source);
            return Path.GetDirectoryName(Path.GetFullPath(source));
        }

        /// <summary>Git URL of a sibling package when the toolkit itself was installed from a Git URL with a ?path= query.</summary>
        private static string SiblingGitUrl(string packageId, string name)
        {
            int at = packageId.IndexOf('@');
            if (at < 0) return null;
            string url = packageId.Substring(at + 1);
            int query = url.IndexOf("?path=", StringComparison.Ordinal);
            if (!url.Contains(".git") || query < 0) return null;
            int end = url.IndexOf('#', query);
            string path = end < 0 ? url.Substring(query + 6) : url.Substring(query + 6, end - query - 6);
            int slash = path.TrimEnd('/').LastIndexOf('/');
            string sibling = (slash < 0 ? "" : path.Substring(0, slash + 1)) + name;
            return url.Substring(0, query + 6) + sibling + (end < 0 ? "" : url.Substring(end));
        }

        /// <summary>A file: source for the library found in <paramref name="directory"/>, as a tarball or an unpacked package folder.</summary>
        private static string FindLibrary(string directory)
        {
            if (string.IsNullOrEmpty(directory)) return null;
            string tarball = Path.Combine(directory, LslLibrary + "-" + LslLibraryVersion + ".tgz");
            if (File.Exists(tarball)) return "file:" + Path.GetFullPath(tarball).Replace('\\', '/');
            string folder = Path.Combine(directory, LslLibrary);
            if (File.Exists(Path.Combine(folder, "package.json"))) return "file:" + Path.GetFullPath(folder).Replace('\\', '/');
            if (File.Exists(Path.Combine(directory, "package.json")) && Path.GetFileName(directory.TrimEnd('/', '\\')) == LslLibrary)
                return "file:" + Path.GetFullPath(directory).Replace('\\', '/');
            return null;
        }

        private static string LocateLslLibrary()
        {
            var toolkit = ToolkitPackage();
            string source = null;
            if (toolkit != null)
            {
                // Checkout: packages/com.thebalancetoolkit and packages/com.labstreaminglayer.lsl4unity are siblings.
                if (toolkit.source == PackageSource.Local)
                    source = FindLibrary(Path.GetFullPath(Path.Combine(toolkit.resolvedPath, "..")));
                // Git URL: same repository, sibling path.
                if (source == null && toolkit.source == PackageSource.Git)
                    source = SiblingGitUrl(toolkit.packageId, LslLibrary);
                // Tarball: the library tarball was downloaded next to the toolkit tarball.
                if (source == null)
                    source = FindLibrary(BundleDirectory(toolkit.packageId, Path.GetDirectoryName(Application.dataPath)));
            }
            if (source == null) source = FindLibrary(EditorPrefs.GetString(ProjectKey("LibraryFolder"), ""));
            if (source != null) return source;
            if (Application.isBatchMode)
                throw new FileNotFoundException("The LSL4Unity compatibility build could not be found next to the toolkit package.");
            var folder = EditorUtility.OpenFolderPanel("Locate the folder containing " + LslLibrary + "-" + LslLibraryVersion + ".tgz", "", "");
            if (string.IsNullOrEmpty(folder)) return null;
            source = FindLibrary(folder);
            if (source == null)
                throw new FileNotFoundException("That folder does not contain the LSL4Unity compatibility build. Download it from the same release as the toolkit and place it next to the toolkit tarball.");
            EditorPrefs.SetString(ProjectKey("LibraryFolder"), folder);
            return source;
        }

        private static void Poll()
        {
            if (request == null || !request.IsCompleted) return;
            bool demoPending = SessionState.GetBool(DemoPendingKey, false);
            if (request.Status == StatusCode.Success)
                SessionState.SetString(StatusKey, demoPending ? "Dependencies installed; continuing with the demo scene..." : "Installation completed. You can now import the sample.");
            else
            {
                SessionState.SetString(StatusKey, "Installation failed: " + request.Error.message);
                SessionState.SetBool(DemoPendingKey, false);
            }
            request = null;
            EditorApplication.update -= Poll;
            if (demoPending) EditorApplication.delayCall += ContinueDemo;
            foreach (var window in Resources.FindObjectsOfTypeAll<ToolkitSetupWindow>()) window.Repaint();
        }

        private void OnInspectorUpdate() => Repaint();
    }
}
