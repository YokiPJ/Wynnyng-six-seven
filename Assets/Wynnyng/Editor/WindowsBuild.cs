using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Wynnyng.Editor
{
    /// <summary>Builds the current playable project. A Windows player is produced only by Unity's build pipeline.</summary>
    public static class WindowsBuild
    {
        private const string DefaultOutput = "Builds/Windows/Wynnyng.exe";
        private const string ReceiptRelativePath = "work/WindowsBuild/build-result.json";
        private static bool running;
        private static bool exitWhenFinished;
        private static string outputPath;
        private static BuildReceipt lastReceipt;

        [Serializable]
        private sealed class BuildReceipt
        {
            public int schemaVersion = 1;
            public bool succeeded;
            public string unityVersion;
            public string outputPath;
            public string scene;
            public int errors;
            public int warnings;
            public ulong sizeBytes;
            public double durationSeconds;
            public string failure;
        }

        [MenuItem("Wynnyng/Gerar EXE Windows")]
        public static void BuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Wynnyng] Pare o Play antes de gerar o executavel.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            exitWhenFinished = false;
            BeginBuild();
        }

        /// <summary>CLI entry point. Omit -quit; this method exits after asynchronous preparation and build.</summary>
        public static void BuildBatch()
        {
            exitWhenFinished = Application.isBatchMode;
            BeginBuild();
        }

        private static void BeginBuild()
        {
            if (running) throw new InvalidOperationException("A Windows build is already running.");
            running = true;
            lastReceipt = null;
            try
            {
                string requestedPath = FindArgument("-wynnyngBuildPath");
                string destination = string.IsNullOrEmpty(requestedPath) ? DefaultOutput : requestedPath;
                outputPath = Path.GetFullPath(Path.IsPathRooted(destination) ? destination : Path.Combine(ProjectRoot, destination));
                if (!string.Equals(Path.GetExtension(outputPath), ".exe", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The Windows build output must end with .exe.");
                WriteReceipt(new BuildReceipt { failure = "Build has not completed." });
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                    throw new InvalidOperationException("Instale Windows Build Support (Mono) para Unity " +
                        Application.unityVersion + " em Unity Hub > Instalacoes > engrenagem > Adicionar modulos.");
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                    throw new InvalidOperationException("Abra o projeto com alvo Windows x86_64. " +
                        "Na linha de comando use -buildTarget Win64, ou selecione Windows no Build Profile antes de gerar.");
                ProjectPreparation.PrepareForBuild(QueuePreparedBuild, Fail);
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void QueuePreparedBuild()
        {
            // Let the import callback finish before invoking the build pipeline.
            EditorApplication.delayCall += BuildPreparedProject;
        }

        private static void BuildPreparedProject()
        {
            try
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ProjectPreparation.ScenePath },
                    target = BuildTarget.StandaloneWindows64,
                    locationPathName = outputPath,
                    options = BuildOptions.StrictMode
                });
                BuildSummary summary = report.summary;
                bool complete = summary.result == BuildResult.Succeeded && summary.totalErrors == 0 &&
                    File.Exists(outputPath) &&
                    File.Exists(Path.Combine(Path.GetDirectoryName(outputPath), "UnityPlayer.dll")) &&
                    Directory.Exists(Path.Combine(Path.GetDirectoryName(outputPath),
                        Path.GetFileNameWithoutExtension(outputPath) + "_Data"));
                WriteReceipt(new BuildReceipt
                {
                    succeeded = complete,
                    errors = summary.totalErrors,
                    warnings = summary.totalWarnings,
                    sizeBytes = summary.totalSize,
                    durationSeconds = summary.totalTime.TotalSeconds,
                    failure = complete ? "" : "Unity build result: " + summary.result
                });
                if (!complete)
                    throw new InvalidOperationException("O Unity nao concluiu um player Windows valido. " +
                        "Consulte work/WindowsBuild/build.log e build-result.json.");
                running = false;
                Debug.Log("[Wynnyng] Executavel Windows gerado: " + outputPath);
                if (exitWhenFinished) EditorApplication.Exit(0);
                else EditorUtility.RevealInFinder(outputPath);
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void Fail(Exception exception)
        {
            running = false;
            BuildReceipt receipt = lastReceipt ?? new BuildReceipt();
            receipt.succeeded = false;
            receipt.failure = exception.Message;
            try { WriteReceipt(receipt); }
            catch (Exception receiptException) { Debug.LogError("Could not write build-result.json: " + receiptException.Message); }
            Debug.LogException(exception);
            if (exitWhenFinished) EditorApplication.Exit(1);
        }

        private static void WriteReceipt(BuildReceipt receipt)
        {
            lastReceipt = receipt;
            receipt.unityVersion = Application.unityVersion;
            receipt.outputPath = outputPath ?? "";
            receipt.scene = ProjectPreparation.ScenePath;
            string path = Path.Combine(ProjectRoot, ReceiptRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(receipt, true));
        }

        private static string FindArgument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
                if (string.Equals(arguments[i], name, StringComparison.OrdinalIgnoreCase)) return arguments[i + 1];
            return null;
        }
        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }
}
