using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using VRRehab.Core;
using VRRehab.EditorTools;
using VRRehab.Interactive.Task13;

namespace VRRehab.EditorAutoSetup
{
    /// <summary>
    /// Builder e validador automatizado para a TASK-13:
    /// Fauna e Flora Reativa ao Laser (Cogumelos Xilofone Pentatônicos, Pássaros Guia e Lanternas Rúnicas)
    /// </summary>
    public static class Task13_SceneAndPrefabBuilder
    {
        private const string ModelsDir = "Assets/Models/Generated/Task13";
        private const string PrefabsDir = "Assets/Prefabs/Interactive";
        private const string AudioDir = "Assets/Audio/Generated/Task13";
        private const string ForestScenePath = "Assets/Scenes/Floresta_e_Rio.unity";
        private const string OutVisualQADir = ".agents/visual_qa";

        private const string MushroomModelPath = "Assets/Models/Generated/Task13/XylophoneMushroom.fbx";
        private const string LanternModelPath = "Assets/Models/Generated/Task13/RunePathLantern.fbx";
        private const string BirdModelPath = "Assets/Models/Generated/Task13/LowPolyForestBird.fbx";

        private const string SfxC4Path = "Assets/Audio/Generated/Task13/marimba_C4.wav";
        private const string SfxD4Path = "Assets/Audio/Generated/Task13/marimba_D4.wav";
        private const string SfxE4Path = "Assets/Audio/Generated/Task13/marimba_E4.wav";
        private const string SfxG4Path = "Assets/Audio/Generated/Task13/marimba_G4.wav";
        private const string SfxA4Path = "Assets/Audio/Generated/Task13/marimba_A4.wav";
        private const string SfxIgnitePath = "Assets/Audio/Generated/Task13/lantern_ignite_whoosh.wav";
        private const string SfxChirpPath = "Assets/Audio/Generated/Task13/bird_chirp_cheerful.wav";
        private const string SfxFlutterPath = "Assets/Audio/Generated/Task13/bird_wing_flutter.wav";

        [MenuItem("VRRehab/AutoBuild/Build and Verify TASK-13")]
        public static void BuildAndVerify()
        {
            Debug.Log("[TASK-13] Iniciando construção completa de Fauna e Flora Reativa na Floresta...");

            // 1. Reimporta modelos e áudios
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(MushroomModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(LanternModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(BirdModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            AssetDatabase.ImportAsset(SfxC4Path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxD4Path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxE4Path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxG4Path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxA4Path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxIgnitePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxChirpPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxFlutterPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            // 2. Garante pastas e materiais
            EnsureDirectories();
            EnsureMaterials();

            // 3. Constrói Prefabs
            GameObject mushroomPrefab = BuildMushroomPrefab();
            GameObject lanternPrefab = BuildLanternPrefab();
            GameObject birdPrefab = BuildBirdPrefab();

            // 4. Configura cena da Floresta
            SetupForestScene(mushroomPrefab, lanternPrefab, birdPrefab);

            // 5. Captura Visual QA
            CaptureVisualQA();

            Debug.Log("[TASK-13] Conclusão com sucesso de todos os estágios da TASK-13!");
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Models/Generated/Task13"))
            {
                AssetDatabase.CreateFolder("Assets/Models/Generated", "Task13");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Interactive"))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "Interactive");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Audio/Generated/Task13"))
            {
                AssetDatabase.CreateFolder("Assets/Audio/Generated", "Task13");
            }
            if (!Directory.Exists(OutVisualQADir))
            {
                Directory.CreateDirectory(OutVisualQADir);
            }
        }

        public static void EnsureMaterials()
        {
            // 1. Materiais do Cogumelo
            EnsureMaterial($"{ModelsDir}/M_Mushroom_Stem.mat", "M_Mushroom_Stem", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Mushroom_Stem", new Color(0.92f, 0.88f, 0.80f), 0.35f, 0.05f);
            });

            EnsureMaterial($"{ModelsDir}/M_Mushroom_Gills.mat", "M_Mushroom_Gills", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Mushroom_Gills", new Color(0.82f, 0.76f, 0.65f), 0.25f, 0.02f);
            });

            // 5 Notas Pentatônicas: C4 (Cyan), D4 (Emerald), E4 (Gold), G4 (Orange), A4 (Violet)
            Color[] capColors = new Color[] {
                new Color(0.00f, 0.70f, 0.85f), // C4 Cyan
                new Color(0.00f, 0.72f, 0.38f), // D4 Emerald
                new Color(0.90f, 0.72f, 0.00f), // E4 Gold
                new Color(0.90f, 0.32f, 0.00f), // G4 Orange
                new Color(0.68f, 0.00f, 0.95f)  // A4 Violet
            };

            Color[] glowColors = new Color[] {
                new Color(0.00f, 0.95f, 1.00f), // C4 Cyan Glow
                new Color(0.00f, 0.95f, 0.45f), // D4 Emerald Glow
                new Color(1.00f, 0.85f, 0.00f), // E4 Gold Glow
                new Color(1.00f, 0.45f, 0.00f), // G4 Orange Glow
                new Color(0.85f, 0.00f, 1.00f)  // A4 Violet Glow
            };

            string[] noteNames = new string[] { "C4", "D4", "E4", "G4", "A4" };

            for (int i = 0; i < 5; i++)
            {
                string note = noteNames[i];
                Color capCol = capColors[i];
                Color glowCol = glowColors[i];

                EnsureMaterial($"{ModelsDir}/M_Mushroom_Cap_{note}.mat", $"M_Mushroom_Cap_{note}", () =>
                {
                    Material m = MaterialShaderHelper.CreateLitMaterial($"M_Mushroom_Cap_{note}", capCol, 0.55f, 0.05f);
                    MaterialShaderHelper.SetEmission(m, glowCol * 0.35f);
                    return m;
                });

                EnsureMaterial($"{ModelsDir}/M_Mushroom_Glow_{note}.mat", $"M_Mushroom_Glow_{note}", () =>
                {
                    Material m = MaterialShaderHelper.CreateEmissiveMaterial($"M_Mushroom_Glow_{note}", glowCol, glowCol, 3.5f);
                    MaterialShaderHelper.SetSmoothness(m, 0.85f);
                    return m;
                });
            }

            // 2. Materiais da Lanterna Rúnica
            EnsureMaterial($"{ModelsDir}/M_Lantern_Stone.mat", "M_Lantern_Stone", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Lantern_Stone", new Color(0.42f, 0.45f, 0.42f), 0.20f, 0.05f);
            });

            EnsureMaterial($"{ModelsDir}/M_Lantern_Wood.mat", "M_Lantern_Wood", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Lantern_Wood", new Color(0.32f, 0.20f, 0.12f), 0.25f, 0.05f);
            });

            EnsureMaterial($"{ModelsDir}/M_Lantern_Runes.mat", "M_Lantern_Runes", () =>
            {
                Material m = MaterialShaderHelper.CreateEmissiveMaterial("M_Lantern_Runes", new Color(1.0f, 0.72f, 0.15f), new Color(1.0f, 0.72f, 0.15f), 3.0f);
                MaterialShaderHelper.SetSmoothness(m, 0.80f);
                return m;
            });

            EnsureMaterial($"{ModelsDir}/M_Lantern_Iron.mat", "M_Lantern_Iron", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Lantern_Iron", new Color(0.18f, 0.19f, 0.20f), 0.45f, 0.85f);
            });

            EnsureMaterial($"{ModelsDir}/M_Lantern_Flame.mat", "M_Lantern_Flame", () =>
            {
                Material m = MaterialShaderHelper.CreateEmissiveMaterial("M_Lantern_Flame", new Color(1.0f, 0.60f, 0.08f), new Color(1.0f, 0.60f, 0.08f), 5.0f);
                return m;
            });

            // 3. Materiais do Pássaro Guia
            EnsureMaterial($"{ModelsDir}/M_Bird_Body.mat", "M_Bird_Body", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Bird_Body", new Color(0.18f, 0.48f, 0.85f), 0.50f, 0.05f);
            });

            EnsureMaterial($"{ModelsDir}/M_Bird_Chest.mat", "M_Bird_Chest", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Bird_Chest", new Color(0.95f, 0.52f, 0.12f), 0.45f, 0.05f);
            });

            EnsureMaterial($"{ModelsDir}/M_Bird_Wings.mat", "M_Bird_Wings", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Bird_Wings", new Color(0.12f, 0.32f, 0.65f), 0.55f, 0.05f);
            });

            EnsureMaterial($"{ModelsDir}/M_Bird_Beak.mat", "M_Bird_Beak", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Bird_Beak", new Color(0.98f, 0.75f, 0.05f), 0.60f, 0.10f);
            });

            EnsureMaterial($"{ModelsDir}/M_Bird_Eyes.mat", "M_Bird_Eyes", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Bird_Eyes", new Color(0.04f, 0.04f, 0.04f), 0.95f, 0.50f);
            });

            // 4. Material de Partículas
            EnsureMaterial($"{ModelsDir}/M_Music_Sparkle.mat", "M_Music_Sparkle", () =>
            {
                return MaterialShaderHelper.CreateTransparentMaterial("M_Music_Sparkle", new Color(1.0f, 0.95f, 0.40f, 0.90f), 0.90f);
            });
        }

        private static void EnsureMaterial(string path, string name, Func<Material> factory)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = factory();
                AssetDatabase.CreateAsset(mat, path);
                AssetDatabase.SaveAssets();
            }
            else
            {
                MaterialShaderHelper.EnsureValidShader(mat);
            }
        }

        #region Prefab Builders
        private static GameObject BuildMushroomPrefab()
        {
            string prefabPath = $"{PrefabsDir}/XylophoneMushroom.prefab";

            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MushroomModelPath);
            if (fbxAsset == null)
            {
                Debug.LogError($"[TASK-13] FBX do Cogumelo não encontrado em {MushroomModelPath}");
                return null;
            }

            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
            go.name = "XylophoneMushroom";

            // Atribui materiais padrão C4
            AssignMaterial(go, "Mushroom_Stem", $"{ModelsDir}/M_Mushroom_Stem.mat");
            AssignMaterial(go, "Mushroom_Cap", $"{ModelsDir}/M_Mushroom_Cap_C4.mat");
            AssignMaterial(go, "Mushroom_Gills", $"{ModelsDir}/M_Mushroom_Gills.mat");
            AssignMaterial(go, "Mushroom_Glow_Spots", $"{ModelsDir}/M_Mushroom_Glow_C4.mat");

            // Colisor de toque e mira laser sobre o chapéu
            Transform cap = go.transform.Find("Mushroom_Cap");
            SphereCollider capCol = go.AddComponent<SphereCollider>();
            capCol.center = new Vector3(0f, 0.22f, 0f);
            capCol.radius = 0.16f;

            // AudioSource 3D
            AudioSource audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1.0f;
            audio.minDistance = 0.8f;
            audio.maxDistance = 25.0f;

            // Partículas de notas musicais
            GameObject psGo = new GameObject("MusicSparkles");
            psGo.transform.SetParent(go.transform, false);
            psGo.transform.localPosition = new Vector3(0f, 0.26f, 0f);
            ParticleSystem ps = psGo.AddComponent<ParticleSystem>();
            ConfigureSparklesParticleSystem(ps);

            // Script MusicalMushroom
            MusicalMushroom script = go.AddComponent<MusicalMushroom>();
            AudioClip clipC4 = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxC4Path);
            Renderer capRend = cap != null ? cap.GetComponent<Renderer>() : null;
            Transform glowChild = go.transform.Find("Mushroom_Glow_Spots");
            Renderer glowRend = glowChild != null ? glowChild.GetComponent<Renderer>() : null;

            Color cyanGlow = new Color(0.0f, 0.95f, 1.0f);
            Color cyanFlash = new Color(0.80f, 1.0f, 1.0f);
            script.Configure(0, "C4", 261.63f, clipC4, cyanGlow, cyanFlash, capRend, glowRend, ps);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log($"[TASK-13] Prefab XylophoneMushroom salvo em {prefabPath}");
            return prefab;
        }

        private static GameObject BuildLanternPrefab()
        {
            string prefabPath = $"{PrefabsDir}/RunePathLantern.prefab";

            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(LanternModelPath);
            if (fbxAsset == null)
            {
                Debug.LogError($"[TASK-13] FBX da Lanterna não encontrado em {LanternModelPath}");
                return null;
            }

            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
            go.name = "RunePathLantern";

            // Atribui materiais
            AssignMaterial(go, "Lantern_Stone_Base", $"{ModelsDir}/M_Lantern_Stone.mat");
            AssignMaterial(go, "Lantern_Wood_Post", $"{ModelsDir}/M_Lantern_Wood.mat");
            AssignMaterial(go, "Lantern_Runes", $"{ModelsDir}/M_Lantern_Runes.mat");
            AssignMaterial(go, "Lantern_Iron_Cage", $"{ModelsDir}/M_Lantern_Iron.mat");
            AssignMaterial(go, "Lantern_Flame", $"{ModelsDir}/M_Lantern_Flame.mat");

            // Colisor de mira laser e aproximação
            BoxCollider col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.50f, 0f);
            col.size = new Vector3(0.36f, 1.02f, 0.36f);

            // Luz pontual suave dentro da cúpula
            GameObject lightGo = new GameObject("LanternLight");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.78f, 0f);
            Light lgt = lightGo.AddComponent<Light>();
            lgt.type = LightType.Point;
            lgt.color = new Color(1.0f, 0.68f, 0.18f);
            lgt.range = 4.5f;
            lgt.intensity = 1.8f;
            lgt.shadows = LightShadows.None;
            lgt.enabled = false;

            // Partículas de faíscas / brasas ascendentes
            GameObject sparksGo = new GameObject("FlameSparks");
            sparksGo.transform.SetParent(go.transform, false);
            sparksGo.transform.localPosition = new Vector3(0f, 0.78f, 0f);
            ParticleSystem sparksPs = sparksGo.AddComponent<ParticleSystem>();
            ConfigureSparksParticleSystem(sparksPs);

            // AudioSource
            AudioSource audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1.0f;
            audio.minDistance = 1.0f;
            audio.maxDistance = 20.0f;

            // Script PathLanternInteractable
            PathLanternInteractable script = go.AddComponent<PathLanternInteractable>();
            Transform flameChild = go.transform.Find("Lantern_Flame");
            Transform runesChild = go.transform.Find("Lantern_Runes");
            Renderer runesRend = runesChild != null ? runesChild.GetComponent<Renderer>() : null;
            AudioClip igniteSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxIgnitePath);

            script.Configure(flameChild != null ? flameChild.gameObject : null, lgt, runesRend, sparksPs, igniteSfx, false);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log($"[TASK-13] Prefab RunePathLantern salvo em {prefabPath}");
            return prefab;
        }

        private static GameObject BuildBirdPrefab()
        {
            string prefabPath = $"{PrefabsDir}/LowPolyForestBird.prefab";

            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BirdModelPath);
            if (fbxAsset == null)
            {
                Debug.LogError($"[TASK-13] FBX do Pássaro não encontrado em {BirdModelPath}");
                return null;
            }

            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
            go.name = "LowPolyForestBird";

            // Atribui materiais
            AssignMaterial(go, "Bird_Body", $"{ModelsDir}/M_Bird_Body.mat");
            AssignMaterial(go, "Bird_Beak", $"{ModelsDir}/M_Bird_Beak.mat");
            AssignMaterial(go, "Bird_Eyes", $"{ModelsDir}/M_Bird_Eyes.mat");
            AssignMaterial(go, "Wing_L", $"{ModelsDir}/M_Bird_Wings.mat");
            AssignMaterial(go, "Wing_R", $"{ModelsDir}/M_Bird_Wings.mat");

            // Colisor de mira laser do pássaro
            SphereCollider col = go.AddComponent<SphereCollider>();
            col.center = new Vector3(0f, 0.10f, 0f);
            col.radius = 0.15f;

            // AudioSource
            AudioSource audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1.0f;
            audio.minDistance = 0.5f;
            audio.maxDistance = 25.0f;

            // Script ReactiveForestBird
            ReactiveForestBird script = go.AddComponent<ReactiveForestBird>();
            Transform wL = go.transform.Find("Wing_L");
            Transform wR = go.transform.Find("Wing_R");
            Transform bBody = go.transform.Find("Bird_Body");
            AudioClip chirp = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxChirpPath);
            AudioClip flutter = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxFlutterPath);

            script.Configure(wL, wR, bBody, chirp, flutter, null, null);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log($"[TASK-13] Prefab LowPolyForestBird salvo em {prefabPath}");
            return prefab;
        }

        private static void AssignMaterial(GameObject root, string childName, string materialPath)
        {
            Transform t = root.transform.Find(childName);
            if (t != null)
            {
                Renderer rend = t.GetComponent<Renderer>();
                if (rend != null)
                {
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (mat != null) rend.sharedMaterial = mat;
                }
            }
        }

        private static void ConfigureSparklesParticleSystem(ParticleSystem ps)
        {
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            main.gravityModifier = -0.1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(1.0f, 0.95f, 0.40f);

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 15, 25) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Music_Sparkle.mat");
        }

        private static void ConfigureSparksParticleSystem(ParticleSystem ps)
        {
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1.0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.gravityModifier = -0.25f; // Flutua para cima
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(1.0f, 0.70f, 0.15f);

            var emission = ps.emission;
            emission.rateOverTime = 8f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.04f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Music_Sparkle.mat");
        }
        #endregion

        #region Scene Setup
        private static void SetupForestScene(GameObject mushroomPrefab, GameObject lanternPrefab, GameObject birdPrefab)
        {
            EditorSceneManager.OpenScene(ForestScenePath, OpenSceneMode.Single);

            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                Debug.LogError("[TASK-13] Terrain não encontrado na cena da Floresta!");
                return;
            }

            // Remove instâncias antigas de Task13
            string oldGroupTag = "[TASK-13] ForestFaunaFlora_Group";
            var oldObj = GameObject.Find(oldGroupTag);
            if (oldObj != null) UnityEngine.Object.DestroyImmediate(oldObj);

            GameObject rootGroup = new GameObject(oldGroupTag);

            // ==================================================================
            // 1. Recanto dos Cogumelos Xilofone Pentatônicos (Arco Musical)
            // ==================================================================
            GameObject gladeGroup = new GameObject("XylophoneMushrooms_Glade");
            gladeGroup.transform.SetParent(rootGroup.transform);

            // 5 notas com coordenadas em arco, escalas proporcionais e afinações
            string[] notes = new string[] { "C4", "D4", "E4", "G4", "A4" };
            float[] freqs = new float[] { 261.63f, 293.66f, 329.63f, 392.00f, 440.00f };
            float[] scales = new float[] { 0.70f, 0.85f, 1.00f, 1.20f, 1.45f }; // Escalonamento de tamanho
            string[] sfxPaths = new string[] { SfxC4Path, SfxD4Path, SfxE4Path, SfxG4Path, SfxA4Path };

            Color[] capColors = new Color[] {
                new Color(0.00f, 0.70f, 0.85f),
                new Color(0.00f, 0.72f, 0.38f),
                new Color(0.90f, 0.72f, 0.00f),
                new Color(0.90f, 0.32f, 0.00f),
                new Color(0.68f, 0.00f, 0.95f)
            };

            Color[] glowColors = new Color[] {
                new Color(0.00f, 0.95f, 1.00f),
                new Color(0.00f, 0.95f, 0.45f),
                new Color(1.00f, 0.85f, 0.00f),
                new Color(1.00f, 0.45f, 0.00f),
                new Color(0.85f, 0.00f, 1.00f)
            };

            // Posições em arco crescente focado na relva aberta da margem da trilha:
            Vector3[] arcPositions = new Vector3[] {
                new Vector3(51.60f, 0f, 64.60f), // M1: C4 (Cyan, ~0.20m)
                new Vector3(51.15f, 0f, 64.95f), // M2: D4 (Emerald, ~0.25m)
                new Vector3(50.80f, 0f, 65.40f), // M3: E4 (Gold, ~0.30m)
                new Vector3(50.55f, 0f, 65.90f), // M4: G4 (Orange, ~0.37m)
                new Vector3(50.40f, 0f, 66.45f)  // M5: A4 (Violet, ~0.45m)
            };

            for (int i = 0; i < 5; i++)
            {
                Vector3 basePos = arcPositions[i];
                float y = terrain.SampleHeight(basePos) + terrain.transform.position.y;
                Vector3 worldPos = new Vector3(basePos.x, y, basePos.z);

                GameObject mGo = (GameObject)PrefabUtility.InstantiatePrefab(mushroomPrefab, gladeGroup.transform);
                mGo.name = $"XylophoneMushroom_{notes[i]}";
                mGo.transform.position = worldPos;
                mGo.transform.localScale = Vector3.one * scales[i];
                // Rotaciona em arco voltado para a trilha de onde o paciente vem caminhando
                mGo.transform.rotation = Quaternion.Euler(0f, 115f + (i * 14f), 0f);

                // Aplica materiais exclusivos da nota
                AssignMaterial(mGo, "Mushroom_Cap", $"{ModelsDir}/M_Mushroom_Cap_{notes[i]}.mat");
                AssignMaterial(mGo, "Mushroom_Glow_Spots", $"{ModelsDir}/M_Mushroom_Glow_{notes[i]}.mat");

                // Configura o componente com seu áudio e cores
                MusicalMushroom script = mGo.GetComponent<MusicalMushroom>();
                if (script != null)
                {
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(sfxPaths[i]);
                    Transform capT = mGo.transform.Find("Mushroom_Cap");
                    Transform glowT = mGo.transform.Find("Mushroom_Glow_Spots");
                    ParticleSystem ps = mGo.GetComponentInChildren<ParticleSystem>();
                    Renderer capR = capT != null ? capT.GetComponent<Renderer>() : null;
                    Renderer glowR = glowT != null ? glowT.GetComponent<Renderer>() : null;

                    Color flashCol = glowColors[i] + new Color(0.3f, 0.3f, 0.3f);
                    script.Configure(i, notes[i], freqs[i], clip, glowColors[i], flashCol, capR, glowR, ps);
                }
            }

            // ==================================================================
            // 2. Lanternas Rúnicas ao Longo da Trilha
            // ==================================================================
            GameObject lanternsGroup = new GameObject("RuneLanterns_Trail");
            lanternsGroup.transform.SetParent(rootGroup.transform);

            Vector3[] lanternPositions = new Vector3[] {
                new Vector3(55.40f, 0f, 63.80f), // Lanterna 1: Margem do portão de largada (sem obstruir o centro da trilha)
                new Vector3(47.20f, 0f, 63.80f), // Lanterna 2: Meio da caminhada
                new Vector3(36.50f, 0f, 66.80f)  // Lanterna 3: Entrada da ponte de madeira
            };

            GameObject[] lanternGos = new GameObject[3];

            for (int i = 0; i < lanternPositions.Length; i++)
            {
                Vector3 basePos = lanternPositions[i];
                float y = terrain.SampleHeight(basePos) + terrain.transform.position.y;
                Vector3 worldPos = new Vector3(basePos.x, y, basePos.z);

                GameObject lGo = (GameObject)PrefabUtility.InstantiatePrefab(lanternPrefab, lanternsGroup.transform);
                lGo.name = $"RunePathLantern_{i + 1}";
                lGo.transform.position = worldPos;
                lGo.transform.rotation = Quaternion.Euler(0f, -15f + (i * 20f), 0f);
                lanternGos[i] = lGo;

                // Deixa a lanterna 1 pré-acesa para demonstrar a iluminação acolhedora e calorosa na cena
                if (i == 0)
                {
                    PathLanternInteractable lScript = lGo.GetComponent<PathLanternInteractable>();
                    if (lScript != null)
                    {
                        Transform flameT = lGo.transform.Find("Lantern_Flame");
                        Light lgt = lGo.GetComponentInChildren<Light>();
                        Transform runesT = lGo.transform.Find("Lantern_Runes");
                        Renderer runesR = runesT != null ? runesT.GetComponent<Renderer>() : null;
                        ParticleSystem ps = lGo.GetComponentInChildren<ParticleSystem>();
                        AudioClip sfx = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxIgnitePath);

                        lScript.Configure(flameT != null ? flameT.gameObject : null, lgt, runesR, ps, sfx, true);
                    }
                }
            }

            // ==================================================================
            // 3. Pássaro Guia da Floresta (Mourão da Cerca -> Lanterna 2 -> Ponte)
            // ==================================================================
            GameObject birdGroup = new GameObject("GuideBird_Perch");
            birdGroup.transform.SetParent(rootGroup.transform);

            Vector3 perch1Pos = new Vector3(54.325f, 4.525f, 65.367f); // Topo exato do mourão mais alto da cerca
            Quaternion perch1Rot = Quaternion.Euler(0f, 260f, 0f);      // Olhando para a trilha e para o paciente

            // Poleiro 2: Topo da Lanterna 2
            Vector3 lantern2Pos = lanternGos[1].transform.position;
            Vector3 perch2Pos = new Vector3(lantern2Pos.x, lantern2Pos.y + 1.02f, lantern2Pos.z);
            Quaternion perch2Rot = Quaternion.Euler(0f, 275f, 0f);

            // Poleiro 3: Balaustrada da ponte de madeira
            Vector3 perch3Pos = new Vector3(35.50f, 4.48f, 68.30f);
            Quaternion perch3Rot = Quaternion.Euler(0f, 280f, 0f);

            Vector3[] waypoints = new Vector3[] { perch1Pos, perch2Pos, perch3Pos };
            Quaternion[] waypointRotations = new Quaternion[] { perch1Rot, perch2Rot, perch3Rot };

            GameObject birdGo = (GameObject)PrefabUtility.InstantiatePrefab(birdPrefab, birdGroup.transform);
            birdGo.name = "LowPolyForestBird_Guide";
            birdGo.transform.position = perch1Pos;
            birdGo.transform.rotation = perch1Rot;

            ReactiveForestBird birdScript = birdGo.GetComponent<ReactiveForestBird>();
            if (birdScript != null)
            {
                Transform wL = birdGo.transform.Find("Wing_L");
                Transform wR = birdGo.transform.Find("Wing_R");
                Transform bBody = birdGo.transform.Find("Bird_Body");
                AudioClip chirp = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxChirpPath);
                AudioClip flutter = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxFlutterPath);

                birdScript.Configure(wL, wR, bBody, chirp, flutter, waypoints, waypointRotations);
            }

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[TASK-13] Cena Floresta_e_Rio configurada e salva com sucesso!");
        }
        #endregion

        #region Visual QA Captures
        public static void CaptureVisualQA()
        {
            string outDir = Path.GetFullPath(OutVisualQADir);
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            if (!EditorSceneManager.GetActiveScene().path.EndsWith("Floresta_e_Rio.unity"))
            {
                EditorSceneManager.OpenScene(ForestScenePath, OpenSceneMode.Single);
            }

            // Print 1: Recanto dos 5 Cogumelos Musicais em arco na relva aberta da margem
            string pathGlade = Path.Combine(outDir, "TASK-13_Xylophone_Glade.png");
            GameObject m3 = GameObject.Find("XylophoneMushroom_E4");
            Vector3 m3Pos = m3 != null ? m3.transform.position : new Vector3(50.80f, 4.47f, 65.40f);
            Vector3 camPos1 = m3Pos + new Vector3(1.35f, 0.85f, -1.15f);
            Vector3 lookAt1 = m3Pos + new Vector3(-0.15f, 0.10f, 0.15f);
            VisualQACaptureEditor.CaptureCameraView(pathGlade, camPos1, lookAt1, 1920, 1080);
            Debug.Log($"[TASK-13 VisualQA] Capturado Xylophone Glade: {pathGlade}");

            // Print 2: Lanterna Rúnica acesa iluminando o poste de madeira e a trilha
            string pathLantern = Path.Combine(outDir, "TASK-13_Lantern_Ignited.png");
            GameObject l1 = GameObject.Find("RunePathLantern_1");
            Vector3 l1Pos = l1 != null ? l1.transform.position : new Vector3(55.40f, 4.45f, 63.80f);
            Vector3 camPos2 = l1Pos + new Vector3(0.95f, 0.50f, -0.85f);
            Vector3 lookAt2 = l1Pos + new Vector3(0f, 0.55f, 0f);
            VisualQACaptureEditor.CaptureCameraView(pathLantern, camPos2, lookAt2, 1920, 1080);
            Debug.Log($"[TASK-13 VisualQA] Capturado Lantern Ignited: {pathLantern}");

            // Print 3: Pássaro Guia pousado no mourão do portão da trilha
            string pathBird = Path.Combine(outDir, "TASK-13_Guide_Bird_Perched.png");
            GameObject birdGo = GameObject.Find("LowPolyForestBird_Guide");
            Vector3 birdPos = birdGo != null ? birdGo.transform.position : new Vector3(54.325f, 4.525f, 65.367f);
            Vector3 camPos3 = birdPos + new Vector3(0.42f, 0.16f, -0.32f);
            Vector3 lookAt3 = birdPos + new Vector3(0f, 0.08f, 0f);
            VisualQACaptureEditor.CaptureCameraView(pathBird, camPos3, lookAt3, 1920, 1080);
            Debug.Log($"[TASK-13 VisualQA] Capturado Guide Bird Perched: {pathBird}");
        }
        #endregion
    }
}
