using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Wynnyng.Configuration;
using Wynnyng.Presentation;
using Wynnyng.Runtime;

namespace Wynnyng.Editor
{
    public static class ProjectPreparation
    {
        private const string Root = "Assets/Wynnyng";
        public const string ScenePath = Root + "/Scenes/Phase01A.unity";
        public const string MatchScenePath = Root + "/Scenes/FootballMatch.unity";
        private const string PipelinePath = Root + "/Rendering/WynnyngURP.asset";
        private const string RendererPath = Root + "/Rendering/WynnyngRenderer.asset";
        private static bool waitingForTmp;
        private static bool exitBatchWhenReady;
        private static bool startAfterPreparation;
        private static string startSceneAfterPreparation = ScenePath;
        private static Action preparationReady;
        private static Action<Exception> preparationFailure;

        public static void PrepareForBuild(Action onReady, Action<Exception> onFailure)
        {
            if (onReady == null) throw new ArgumentNullException(nameof(onReady));
            if (onFailure == null) throw new ArgumentNullException(nameof(onFailure));
            if (waitingForTmp || preparationReady != null)
            {
                onFailure(new InvalidOperationException("A preparacao de recursos ja esta em andamento."));
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                onFailure(new InvalidOperationException("Pare o Play antes de gerar o executavel."));
                return;
            }
            exitBatchWhenReady = false;
            startAfterPreparation = false;
            preparationReady = onReady;
            preparationFailure = onFailure;
            try { Prepare(); }
            catch (Exception exception) { FailPreparation(exception); }
        }

        [MenuItem("Wynnyng/Preparar e iniciar laboratorio")]
        public static void PrepareAndPlay()
        {
            PrepareAndPlayScene(ScenePath);
        }

        [MenuItem("Wynnyng/Preparar e jogar partida")]
        public static void PrepareAndPlayMatch()
        {
            PrepareAndPlayScene(MatchScenePath);
        }

        private static void PrepareAndPlayScene(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Wynnyng] Pare o Play antes de preparar a cena.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            startSceneAfterPreparation = path;
            startAfterPreparation = true;
            try { Prepare(); }
            catch { startAfterPreparation = false; throw; }
        }

        [MenuItem("Wynnyng/Preparar Fases 0 e 1A")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Wynnyng] Stop Play Mode before preparing the project.");
                return;
            }
            if (!TmpResources.TryGetDefaultFont(out _))
            {
                if (waitingForTmp) return;
                UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly);
                if (package == null) throw new InvalidOperationException("Cannot locate the installed uGUI/TMP package.");
                string[] packages = Directory.GetFiles(package.resolvedPath, "TMP Essential Resources.unitypackage", SearchOption.AllDirectories);
                if (packages.Length != 1) throw new InvalidOperationException("Install uGUI 2.0.0 with TMP Essential Resources using Package Manager.");
                waitingForTmp = true;
                AssetDatabase.importPackageCompleted += OnTmpImported;
                AssetDatabase.importPackageFailed += OnTmpFailed;
                AssetDatabase.importPackageCancelled += OnTmpCancelled;
                AssetDatabase.ImportPackage(packages[0], false);
                return;
            }
            FinishPreparation();
        }
        private static void OnTmpImported(string packageName)
        {
            StopWaiting();
            EditorApplication.delayCall += FinishPreparation;
        }
        private static void OnTmpFailed(string packageName, string error)
        {
            FailPreparation(new InvalidOperationException("TMP import failed: " + error));
        }
        private static void OnTmpCancelled(string packageName)
        {
            FailPreparation(new InvalidOperationException("TMP import cancelled. Run preparation again before Play."));
        }
        private static void StopWaiting()
        {
            waitingForTmp = false;
            AssetDatabase.importPackageCompleted -= OnTmpImported;
            AssetDatabase.importPackageFailed -= OnTmpFailed;
            AssetDatabase.importPackageCancelled -= OnTmpCancelled;
        }
        private static void FinishPreparation()
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                _ = TmpResources.RequireDefaultFont();
                PrepareAssets();
                if (preparationReady != null)
                {
                    Action ready = preparationReady;
                    Action<Exception> failed = preparationFailure;
                    preparationReady = null;
                    preparationFailure = null;
                    try { ready(); }
                    catch (Exception exception) { failed(exception); }
                    return;
                }
                if (exitBatchWhenReady) EditorApplication.Exit(0);
                if (startAfterPreparation)
                {
                    startAfterPreparation = false;
                    EditorSceneManager.OpenScene(startSceneAfterPreparation);
                    EditorApplication.isPlaying = true;
                }
            }
            catch (Exception exception)
            {
                FailPreparation(exception);
            }
        }
        private static void FailPreparation(Exception exception)
        {
            startAfterPreparation = false;
            StopWaiting();
            Action<Exception> failed = preparationFailure;
            preparationReady = null;
            preparationFailure = null;
            if (failed != null) { failed(exception); return; }
            Debug.LogException(exception);
            if (exitBatchWhenReady) EditorApplication.Exit(1);
        }
        private static void PrepareAssets()
        {
            SimulationConfig simulation = EnsureConfig<SimulationConfig>("Simulation");
            PitchConfig pitch = EnsureConfig<PitchConfig>("Pitch");
            BallConfig ball = EnsureConfig<BallConfig>("Ball");
            PresentationConfig presentation = EnsureConfig<PresentationConfig>("Presentation");
            FootballConfig football = EnsureConfig<FootballConfig>("Football");
            simulation.ValidateOrThrow(); ball.ValidateOrThrow(); pitch.ValidateOrThrow(ball.Radius); presentation.ValidateOrThrow();
            football.ValidateOrThrow();
            PrepareRendering();
            RenderingResources.RequireConfigured();
            Time.fixedDeltaTime = 1f / simulation.FixedFrequency;
            Time.maximumDeltaTime = simulation.MaximumFrameDelta;
            Physics.gravity = simulation.Gravity;
            Physics.bounceThreshold = simulation.BounceThreshold;
            EnsureFolder(Root + "/Scenes");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) CreateScene();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MatchScenePath) == null) CreateMatchScene();
            EnsureBuildScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[Wynnyng] Ready. Open Assets/Wynnyng/Scenes/FootballMatch.unity and press Play. Phase01A remains available as the physics laboratory.");
        }
        private static T EnsureConfig<T>(string name) where T : ScriptableObject
        {
            string folder = Root + "/Resources/Config";
            EnsureFolder(folder);
            string path = folder + "/" + name + ".asset";
            T config = AssetDatabase.LoadAssetAtPath<T>(path);
            if (config != null) return config;
            config = ScriptableObject.CreateInstance<T>();
            config.name = name;
            AssetDatabase.CreateAsset(config, path);
            return config;
        }
        private static void PrepareRendering()
        {
            EnsureFolder(Root + "/Rendering");
            UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
                if (renderer == null)
                {
                    renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(renderer, RendererPath);
                }
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.name = "WynnyngURP";
                pipeline.supportsHDR = false;
                pipeline.msaaSampleCount = 2;
                pipeline.shadowCascadeCount = 2;
                pipeline.useSRPBatcher = true;
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            // Apply to all quality levels so changing quality cannot silently switch back to Built-in RP.
            int previousQuality = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
                QualitySettings.vSyncCount = 0;
            }
            QualitySettings.SetQualityLevel(previousQuality, false);
        }
        private static void CreateScene()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var bootstrap = new GameObject("Wynnyng Bootstrap");
            if (bootstrap.scene != scene) SceneManager.MoveGameObjectToScene(bootstrap, scene);
            bootstrap.AddComponent<MatchBootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
        private static void EnsureBuildScene()
        {
            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
            int otherCount = 0;
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i].path != MatchScenePath && existing[i].path != ScenePath) otherCount++;
            }
            var scenes = new EditorBuildSettingsScene[otherCount + 2];
            scenes[0] = new EditorBuildSettingsScene(MatchScenePath, true);
            scenes[1] = new EditorBuildSettingsScene(ScenePath, true);
            int next = 2;
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i].path == MatchScenePath || existing[i].path == ScenePath) continue;
                scenes[next++] = existing[i];
            }
            EditorBuildSettings.scenes = scenes;
        }

        private static void CreateMatchScene()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var bootstrap = new GameObject("Wynnyng Football Match");
            if (bootstrap.scene != scene) SceneManager.MoveGameObjectToScene(bootstrap, scene);
            bootstrap.AddComponent<FootballBootstrap>();
            EditorSceneManager.SaveScene(scene, MatchScenePath);
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        [MenuItem("Wynnyng/Abrir laboratorio Fase 1A")]
        public static void OpenLaboratory()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Wynnyng] Pare o Play antes de abrir a cena do laboratorio.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!TmpResources.TryGetDefaultFont(out _) || !RenderingResources.IsConfigured ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Debug.Log("[Wynnyng] Preparando recursos. Aguarde Ready e escolha Abrir laboratorio novamente.");
                Prepare();
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Wynnyng/Abrir partida")]
        public static void OpenMatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Wynnyng] Pare o Play antes de abrir a partida.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!TmpResources.TryGetDefaultFont(out _) || !RenderingResources.IsConfigured ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(MatchScenePath) == null)
            {
                Debug.Log("[Wynnyng] Preparando recursos. Aguarde Ready e escolha Abrir partida novamente.");
                Prepare();
                return;
            }
            EditorSceneManager.OpenScene(MatchScenePath);
        }
        /// <summary>Batch entry point. Do not add -quit: a first TMP import completes asynchronously.</summary>
        public static void PrepareBatch()
        {
            exitBatchWhenReady = Application.isBatchMode;
            try { Prepare(); }
            catch (Exception exception)
            {
                if (!exitBatchWhenReady) throw;
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
