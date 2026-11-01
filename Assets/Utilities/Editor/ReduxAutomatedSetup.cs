using System;
using System.IO;
using System.Linq;
using ThunderKit.Core.Config;
using ThunderKit.Core.Config.Common;
using ThunderKit.Core.Data;
using ThunderKit.Core.Pipelines;
using UnityEditor;
using UnityEngine;

namespace Redux.Template.Editor
{
    // Drives the ThunderKit game import and the "Import KSP2 to Editor" pipeline unattended, so the
    // Redux SDK Manager can finish setting up a freshly created/ingested project without the user
    // opening the editor by hand. It engages only when Unity is launched with -redux-run-setup, so it
    // stays inert during normal editor use.
    //
    // The work runs in two phases, each its own Unity launch, because the two stages need different
    // domain states and neither can safely span the domain reload the other causes. The current phase
    // lives in the status file, so it survives reloads and full relaunches:
    //
    //   Phase A ("import"):   the game's assemblies are not in the project yet, so the SDK's own editor
    //                         code does not compile - the Manager launches with -ignoreCompilerErrors so
    //                         the editor loads the assemblies that did compile (this watcher, ThunderKit)
    //                         instead of dropping into Safe Mode. ThunderKit's StepImporters advances the
    //                         disk-persisted import cursor across the assembly-import reload; this watcher
    //                         re-attaches via [InitializeOnLoad] to notice completion, write import-done,
    //                         and quit.
    //
    //   Phase B ("pipeline"): a fresh boot. The game package now exists, so the whole project (including
    //                         the SDK editor code) compiles and the domain is stable. The pipeline only
    //                         copies data - it adds no compilable code - so with assembly reloads locked
    //                         it completes in one process without a reload to loop on.
    //
    // Command line (passed by the Manager):
    //   -redux-run-setup                engages this watcher
    //   -redux-ksp2=<KSP2_x64.exe>      the game executable to import from
    //   -redux-status=<file>            phase/progress file (default: <project>/redux-setup-status.txt)
    //   -ignoreCompilerErrors           (Unity flag) load despite the phase-A compile errors
    //   -disable-assembly-updater       (ThunderKit flag) suppress the mid-import editor restart
    [InitializeOnLoad]
    public static class ReduxAutomatedSetup
    {
        private const string RunArg = "-redux-run-setup";
        private const string Ksp2Arg = "-redux-ksp2=";
        private const string StatusArg = "-redux-status=";
        private const string DefaultStatusFile = "redux-setup-status.txt";

        private const string PhaseImport = "import";
        private const string PhaseImportDone = "import-done";
        private const string PhasePipeline = "pipeline";
        private const string PhaseDone = "done";
        private const string PhaseError = "error";

        private static bool _pipelineKicked;
        private static string _lastStatus;

        static ReduxAutomatedSetup()
        {
            if (!Engaged()) return;
            EditorApplication.update += Tick;
        }

        // Optional -executeMethod anchor. The [InitializeOnLoad] constructor already engages off the
        // -redux-run-setup flag, but naming a method to run guarantees the editor boots far enough to
        // register the watcher even when nothing else forces it to.
        public static void RunSetup() { }

        private static void Tick()
        {
            // Never step while Unity is busy: the import cursor and StepImporters both bail on these, and
            // we want the phase-A game-package recompile fully settled before we move on.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            try
            {
                var phase = ReadPhase();
                if (phase == PhaseDone || phase == PhaseError)
                {
                    Quit(phase == PhaseError ? 1 : 0);
                    return;
                }

                if (phase == PhaseImportDone || phase == PhasePipeline)
                {
                    RunPipelinePhase();
                    return;
                }

                DriveImportPhase();
            }
            catch (Exception e)
            {
                Fail(e.Message);
            }
        }

        // Phase A: advance the ThunderKit import cursor to completion, then hand back to the Manager.
        private static void DriveImportPhase()
        {
            var settings = ThunderKitSetting.GetOrCreateSettings<ThunderKitSettings>();
            var import = ThunderKitSetting.GetOrCreateSettings<ImportConfiguration>();
            if (import.ConfigurationExecutors == null || import.ConfigurationExecutors.Length == 0) return;

            EnsureConfigured(settings, import);

            if (!ImportComplete(import))
            {
                // Kick the cursor once; ThunderKit's StepImporters advances it from here, including across
                // the domain reload the assembly import causes.
                if (import.ConfigurationIndex < 0) import.ConfigurationIndex = 0;
                WriteStatus(PhaseImport, CurrentStep(import));
                return;
            }

            // Import finished. Quit so the Manager can relaunch into a clean domain for the pipeline.
            WriteStatus(PhaseImportDone, "");
            AssetDatabase.SaveAssets();
            Quit(0);
        }

        // Phase B: run the copy-only pipeline once, with reloads locked so it can neither be interrupted
        // nor re-dispatched. Kicked once per launch; the domain is stable here so it will not reload.
        private static async void RunPipelinePhase()
        {
            if (_pipelineKicked) return;
            _pipelineKicked = true;

            EditorApplication.LockReloadAssemblies();
            var exitCode = 0;
            try
            {
                WriteStatus(PhasePipeline, "Import KSP2 to Editor");

                var path = FindPipeline();
                if (string.IsNullOrEmpty(path))
                    throw new FileNotFoundException("ImportKsp2ToEditor.asset not found in the project.");

                var pipeline = AssetDatabase.LoadAssetAtPath<Pipeline>(path);
                if (pipeline == null)
                    throw new FileNotFoundException($"Could not load a pipeline at {path}.");

                await Pipeline.RunPipelineWithManifest(pipeline, null);

                WriteStatus(PhaseDone, "");
                AssetDatabase.SaveAssets();
            }
            catch (Exception e)
            {
                exitCode = 1;
                WriteStatus(PhaseError, e.Message);
                Debug.LogError($"Redux automated setup pipeline failed: {e}");
            }
            finally
            {
                EditorApplication.UnlockReloadAssemblies();
                EditorApplication.Exit(exitCode);
            }
        }

        private static void EnsureConfigured(ThunderKitSettings settings, ImportConfiguration import)
        {
            var exe = GetArgValue(Ksp2Arg);
            if (!string.IsNullOrEmpty(exe) && string.IsNullOrEmpty(settings.GamePath))
            {
                settings.GamePath = Path.GetDirectoryName(exe);
                settings.GameExecutable = Path.GetFileName(exe);
                EditorUtility.SetDirty(settings);
            }

            // PromptRestart (the final executor) tries to relaunch the editor at the end of the import,
            // which we neither want nor can answer in batch mode. Disabling it makes ImportGame skip it.
            // DisableAssemblyUpdater's own restart is suppressed by the -disable-assembly-updater flag the
            // Manager passes on the command line.
            foreach (var executor in import.ConfigurationExecutors)
            {
                if (executor is PromptRestart && executor.enabled)
                {
                    executor.enabled = false;
                    EditorUtility.SetDirty(executor);
                }
            }
        }

        private static bool ImportComplete(ImportConfiguration import)
            => import.ConfigurationIndex >= import.ConfigurationExecutors.Length;

        private static string CurrentStep(ImportConfiguration import)
        {
            var i = import.ConfigurationIndex;
            if (i < 0 || i >= import.ConfigurationExecutors.Length) return "";
            var executor = import.ConfigurationExecutors[i];
            return executor != null ? executor.Name : "";
        }

        // The SDK copies ImportKsp2ToEditor.asset into Assets/ on load in non-Redux projects, but keeps
        // it in the package for Redux templates. Prefer whatever the AssetDatabase already knows about,
        // then fall back to the two known locations.
        private static string FindPipeline()
        {
            var guid = AssetDatabase.FindAssets("ImportKsp2ToEditor").FirstOrDefault();
            if (!string.IsNullOrEmpty(guid))
            {
                var fromDb = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(fromDb)) return fromDb;
            }

            foreach (var candidate in new[]
                     {
                         "Assets/ImportKsp2ToEditor.asset",
                         "Packages/ksp2community.ksp2unitytools/ImportKsp2ToEditor.asset"
                     })
                if (File.Exists(candidate)) return candidate;

            return null;
        }

        private static void Fail(string message)
        {
            WriteStatus(PhaseError, message);
            Debug.LogError($"Redux automated setup failed: {message}");
            Quit(1);
        }

        private static void Quit(int exitCode)
        {
            EditorApplication.update -= Tick;
            EditorApplication.Exit(exitCode);
        }

        private static bool Engaged()
            => Environment.GetCommandLineArgs().Any(a => a == RunArg);

        private static string GetArgValue(string prefix)
        {
            var arg = Environment.GetCommandLineArgs()
                .FirstOrDefault(a => a.StartsWith(prefix, StringComparison.Ordinal));
            return arg?.Substring(prefix.Length);
        }

        private static string StatusFilePath()
        {
            var custom = GetArgValue(StatusArg);
            return !string.IsNullOrEmpty(custom)
                ? custom
                : Path.Combine(Directory.GetCurrentDirectory(), DefaultStatusFile);
        }

        private static string ReadPhase()
        {
            try
            {
                var path = StatusFilePath();
                if (!File.Exists(path)) return "";
                var line = File.ReadAllText(path);
                var bar = line.IndexOf('|');
                return (bar >= 0 ? line.Substring(0, bar) : line).Trim();
            }
            catch
            {
                return "";
            }
        }

        private static void WriteStatus(string phase, string step)
        {
            var line = $"{phase}|{step}";
            if (line == _lastStatus) return;
            _lastStatus = line;
            try { File.WriteAllText(StatusFilePath(), line); }
            catch { /* status is best-effort; never fail the setup over it */ }
            Debug.Log($"[ReduxSetup] {line}");
        }
    }
}
