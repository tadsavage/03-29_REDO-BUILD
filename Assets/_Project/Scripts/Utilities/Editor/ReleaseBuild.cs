using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GameCore.Utilities.Editor
{
    /// <summary>
    /// One-click RELEASE (non-development) Windows 64 build. Applies the performance-oriented player settings, then
    /// builds every scene ticked in File > Build Profiles / Build Settings to <see cref="OutputFolder"/>.
    ///
    /// Run from Tools > Build > Release Windows Build, or headless:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod GameCore.Utilities.Editor.ReleaseBuild.BuildRelease
    /// (the editor must not have the project open for the headless form).
    /// </summary>
    public static class ReleaseBuild
    {
        public const string OutputFolder = @"C:\dev\BUILD";
        public const string ExeName = "Warehouse.exe";

        /// <summary>Applies the release settings without building, so File > Build Profiles > Build
        /// produces the same player as the menu item below.</summary>
        [MenuItem("Tools/Build/Apply Release Settings")]
        public static void ApplyReleaseSettingsMenu()
        {
            ApplyReleaseSettings();
            Debug.Log("[ReleaseBuild] Release player settings applied.");
        }

        [MenuItem("Tools/Build/Release Windows Build")]
        public static void BuildRelease() => BuildReleaseTo(OutputFolder);

        /// <summary>Same release build into another folder (an existing build there is updated incrementally).</summary>
        public static void BuildReleaseTo(string outputFolder)
        {
            ApplyReleaseSettings();

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[ReleaseBuild] No enabled scenes in the build settings.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            Directory.CreateDirectory(outputFolder);
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(outputFolder, ExeName),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,   // NOT Development, no autoconnect profiler, no script debugging
            };

            Debug.Log($"[ReleaseBuild] Building {scenes.Length} scene(s) -> {options.locationPathName}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            string line = $"[ReleaseBuild] {summary.result}: {summary.totalSize / (1024 * 1024)} MB, " +
                          $"{summary.totalErrors} error(s), {summary.totalWarnings} warning(s), {summary.totalTime}";
            if (summary.result == BuildResult.Succeeded) Debug.Log(line); else Debug.LogError(line);
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "release_build_result.txt"), line);

            if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>Release-oriented player settings. Safe to run repeatedly.</summary>
        public static void ApplyReleaseSettings()
        {
            var target = NamedBuildTarget.Standalone;

            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;
            EditorUserBuildSettings.buildWithDeepProfilingSupport = false;

            // IL2CPP, fully optimised native code. (Mono is not installed for this editor; IL2CPP is also the faster runtime.)
            PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(target, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetIl2CppCodeGeneration(target, Il2CppCodeGeneration.OptimizeSpeed);

            // Low stripping: trims unused engine/managed code but keeps reflection-reachable types intact
            // (JsonUtility save data, ScriptableObjects loaded by name from Resources).
            PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.Low);
            PlayerSettings.stripEngineCode = true;

            // Keep Player.log (it was off) so a crash in a release build can still be diagnosed, but stop paying for
            // stack traces on ordinary log lines.
            PlayerSettings.usePlayerLog = true;
            PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            PlayerSettings.SetStackTraceLogType(LogType.Error, StackTraceLogType.ScriptOnly);
            PlayerSettings.SetStackTraceLogType(LogType.Exception, StackTraceLogType.ScriptOnly);
            PlayerSettings.SetStackTraceLogType(LogType.Assert, StackTraceLogType.ScriptOnly);

            // DX11 only: it is the API the game has been developed and tested on. Shipping DX12 as well doubled the
            // shader compile time and the build size for no tested benefit.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
                new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });

            PlayerSettings.enableInternalProfiler = false;
            PlayerSettings.gcIncremental = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.forceSingleInstance = false;

            // Borderless fullscreen at the monitor's own resolution: alt-tab friendly, and no forced
            // 2560x1440 on a 1080p screen (defaultScreenWidth/Height are only used for windowed mode).
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;

            // App UI (com.unity.dt.app-ui) is a dependency of the editor AI tools; the game never uses it,
            // but without this define its runtime assemblies ship in the player and create a per-frame
            // updater object at startup. APP_UI_EDITOR_ONLY is the package's own switch to exclude them.
            AddDefine(target, "APP_UI_EDITOR_ONLY");

            AssetDatabase.SaveAssets();
        }

        private static void AddDefine(NamedBuildTarget target, string define)
        {
            PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);
            if (defines.Contains(define)) return;
            PlayerSettings.SetScriptingDefineSymbols(target, defines.Append(define).ToArray());
        }
    }
}
