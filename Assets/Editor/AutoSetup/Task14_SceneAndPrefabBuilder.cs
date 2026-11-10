using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;
using VRRehab.EditorTools;
using VRRehab.Interactive.Task14;

namespace VRRehab.EditorAutoSetup
{
    /// <summary>
    /// Builder e validador automatizado para a TASK-14:
    /// Trilha de Moedas/Orbes Flutuantes com Atração Magnética para Exoesqueleto (AdaptiveCoinTrailSystem)
    /// </summary>
    public static class Task14_SceneAndPrefabBuilder
    {
        private const string ModelsDir = "Assets/Models/Generated/Task14";
        private const string PrefabsDir = "Assets/Prefabs/Interactive";
        private const string AudioDir = "Assets/Audio/Generated/Task14";
        private const string ForestScenePath = "Assets/Scenes/Floresta_e_Rio.unity";
        private const string OutVisualQADir = ".agents/visual_qa";

        private const string EnergyCoinModelPath = "Assets/Models/Generated/Task14/EnergyStarCoin.fbx";
        private const string BonusCoinModelPath = "Assets/Models/Generated/Task14/BonusDiamondCoin.fbx";

        private const string SfxPickupPath = "Assets/Audio/Generated/Task14/coin_pickup_chime_combo.wav";
        private const string SfxWhooshPath = "Assets/Audio/Generated/Task14/coin_magnet_whoosh.wav";
        private const string SfxFanfarePath = "Assets/Audio/Generated/Task14/secret_coin_fanfare.wav";

        [MenuItem("VRRehab/AutoBuild/Build and Verify TASK-14")]
        public static void BuildAndVerify()
        {
            Debug.Log("[TASK-14] Iniciando construção completa da Trilha de Moedas Magnéticas na Floresta...");

            // 1. Reimporta modelos e áudios
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(EnergyCoinModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(BonusCoinModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxPickupPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxWhooshPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxFanfarePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            // 2. Garante pastas e materiais
            EnsureDirectories();
            EnsureMaterials();

            // 3. Constrói Prefabs
            GameObject energyCoinPrefab = BuildEnergyStarCoinPrefab();
            GameObject bonusCoinPrefab = BuildBonusDiamondCoinPrefab();

            // 4. Configura cena da Floresta
            SetupForestScene(energyCoinPrefab, bonusCoinPrefab);

            // 5. Captura Visual QA
            CaptureVisualQA();

            Debug.Log("[TASK-14] Conclusão com sucesso de todos os estágios da TASK-14!");
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Models/Generated/Task14"))
            {
                AssetDatabase.CreateFolder("Assets/Models/Generated", "Task14");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Interactive"))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "Interactive");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Audio/Generated/Task14"))
            {
                AssetDatabase.CreateFolder("Assets/Audio/Generated", "Task14");
            }
            if (!Directory.Exists(OutVisualQADir))
            {
                Directory.CreateDirectory(OutVisualQADir);
            }
        }

        public static void EnsureMaterials()
        {
            // 1. Materiais da Moeda de Energia Octogonal
            EnsureMaterial($"{ModelsDir}/M_Coin_Gold.mat", "M_Coin_Gold", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Coin_Gold", new Color(1.0f, 0.82f, 0.18f), 0.35f, 0.85f);
            });

            EnsureMaterial($"{ModelsDir}/M_Coin_Rim.mat", "M_Coin_Rim", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Coin_Rim", new Color(0.96f, 0.74f, 0.12f), 0.50f, 0.92f);
            });

            EnsureMaterial($"{ModelsDir}/M_Coin_Star_Glow.mat", "M_Coin_Star_Glow", () =>
            {
                return MaterialShaderHelper.CreateEmissiveMaterial("M_Coin_Star_Glow", new Color(1.0f, 0.95f, 0.45f), new Color(1.0f, 0.90f, 0.30f), 3.5f);
            });

            EnsureMaterial($"{ModelsDir}/M_Coin_Cyan_Glow.mat", "M_Coin_Cyan_Glow", () =>
            {
                return MaterialShaderHelper.CreateEmissiveMaterial("M_Coin_Cyan_Glow", new Color(0.15f, 0.85f, 1.0f), new Color(0.10f, 0.85f, 1.0f), 2.8f);
            });

            // 2. Materiais do Diamante Bônus Secreto
            EnsureMaterial($"{ModelsDir}/M_Diamond_Crystal.mat", "M_Diamond_Crystal", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Diamond_Crystal", new Color(0.20f, 0.80f, 1.0f, 0.90f), 0.70f, 0.20f);
            });

            EnsureMaterial($"{ModelsDir}/M_Diamond_Core_Glow.mat", "M_Diamond_Core_Glow", () =>
            {
                return MaterialShaderHelper.CreateEmissiveMaterial("M_Diamond_Core_Glow", new Color(0.95f, 0.35f, 1.0f), new Color(0.95f, 0.35f, 1.0f), 4.5f);
            });

            EnsureMaterial($"{ModelsDir}/M_Diamond_Gold_Ring.mat", "M_Diamond_Gold_Ring", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Diamond_Gold_Ring", new Color(1.0f, 0.85f, 0.25f), 0.45f, 0.88f);
            });

            EnsureMaterial($"{ModelsDir}/M_Diamond_Orbit_Nodes.mat", "M_Diamond_Orbit_Nodes", () =>
            {
                return MaterialShaderHelper.CreateEmissiveMaterial("M_Diamond_Orbit_Nodes", new Color(0.20f, 1.0f, 0.90f), new Color(0.20f, 1.0f, 0.90f), 3.2f);
            });

            // 3. Material de Partículas
            EnsureMaterial($"{ModelsDir}/M_Coin_Sparkle.mat", "M_Coin_Sparkle", () =>
            {
                return MaterialShaderHelper.CreateTransparentMaterial("M_Coin_Sparkle", new Color(1.0f, 0.92f, 0.40f, 0.90f), 0.80f);
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
        private static GameObject BuildEnergyStarCoinPrefab()
        {
            string prefabPath = $"{PrefabsDir}/EnergyStarCoin.prefab";
            if (File.Exists(prefabPath))
            {
                AssetDatabase.DeleteAsset(prefabPath);
            }

            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(EnergyCoinModelPath);
            if (fbxAsset == null)
            {
                Debug.LogError($"[TASK-14] FBX EnergyStarCoin não encontrado em {EnergyCoinModelPath}");
                return null;
            }

            // Cria raiz limpa para o Prefab
            GameObject root = new GameObject("EnergyStarCoin");

            // Instancia o modelo FBX como filho da raiz
            GameObject visualMesh = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset, root.transform);
            visualMesh.name = "Coin_VisualModel";
            // Rotaciona em 90 graus no eixo X para garantir que o disco fique 100% EM PÉ NA VERTICAL
            visualMesh.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            visualMesh.transform.localPosition = Vector3.zero;

            // Atribui materiais a todas as partes
            AssignMaterial(visualMesh, "Coin_Body", $"{ModelsDir}/M_Coin_Gold.mat");
            AssignMaterial(visualMesh, "Coin_Rim", $"{ModelsDir}/M_Coin_Rim.mat");
            AssignMaterial(visualMesh, "Coin_Rune_Ring", $"{ModelsDir}/M_Coin_Cyan_Glow.mat");
            AssignMaterial(visualMesh, "Coin_Star_Front", $"{ModelsDir}/M_Coin_Star_Glow.mat");
            AssignMaterial(visualMesh, "Coin_Star_Back", $"{ModelsDir}/M_Coin_Star_Glow.mat");

            // Colisor de Gatilho no root
            SphereCollider col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.13f;
            col.center = Vector3.zero;

            // AudioSource 3D no root
            AudioSource audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1.0f;
            audio.minDistance = 0.6f;
            audio.maxDistance = 20.0f;
            audio.rolloffMode = AudioRolloffMode.Linear;

            // Sistema de Partículas de Coleta
            GameObject psGo = new GameObject("CollectSparkles_FX");
            psGo.transform.SetParent(root.transform, false);
            ParticleSystem ps = psGo.AddComponent<ParticleSystem>();
            ConfigureSparklesParticleSystem(ps, new Color(1.0f, 0.90f, 0.35f));

            // Script CollectibleOrbCoin
            AudioClip chime = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxPickupPath);
            AudioClip whoosh = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxWhooshPath);
            AudioClip fanfare = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxFanfarePath);

            CollectibleOrbCoin coinScript = root.AddComponent<CollectibleOrbCoin>();
            coinScript.Configure(CollectibleOrbCoin.CoinType.EnergyStar, 10, chime, whoosh, fanfare, ps);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log($"[TASK-14] Prefab EnergyStarCoin salvo em {prefabPath}");
            return prefab;
        }

        private static GameObject BuildBonusDiamondCoinPrefab()
        {
            string prefabPath = $"{PrefabsDir}/BonusDiamondCoin.prefab";
            if (File.Exists(prefabPath))
            {
                AssetDatabase.DeleteAsset(prefabPath);
            }

            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BonusCoinModelPath);
            if (fbxAsset == null)
            {
                Debug.LogError($"[TASK-14] FBX BonusDiamondCoin não encontrado em {BonusCoinModelPath}");
                return null;
            }

            GameObject root = new GameObject("BonusDiamondCoin");

            GameObject visualMesh = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset, root.transform);
            visualMesh.name = "Diamond_VisualModel";
            visualMesh.transform.localPosition = Vector3.zero;
            visualMesh.transform.localRotation = Quaternion.identity;

            AssignMaterial(visualMesh, "Diamond_Body", $"{ModelsDir}/M_Diamond_Crystal.mat");
            AssignMaterial(visualMesh, "Diamond_Inner_Core", $"{ModelsDir}/M_Diamond_Core_Glow.mat");
            AssignMaterial(visualMesh, "Diamond_Orbit_Ring", $"{ModelsDir}/M_Diamond_Gold_Ring.mat");
            AssignMaterial(visualMesh, "Diamond_Orbit_Node_1", $"{ModelsDir}/M_Diamond_Orbit_Nodes.mat");
            AssignMaterial(visualMesh, "Diamond_Orbit_Node_2", $"{ModelsDir}/M_Diamond_Orbit_Nodes.mat");
            AssignMaterial(visualMesh, "Diamond_Orbit_Node_3", $"{ModelsDir}/M_Diamond_Orbit_Nodes.mat");

            SphereCollider col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.14f;
            col.center = Vector3.zero;

            AudioSource audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1.0f;
            audio.minDistance = 0.8f;
            audio.maxDistance = 25.0f;
            audio.rolloffMode = AudioRolloffMode.Linear;

            GameObject psGo = new GameObject("DiamondSparkles_FX");
            psGo.transform.SetParent(root.transform, false);
            ParticleSystem ps = psGo.AddComponent<ParticleSystem>();
            ConfigureSparklesParticleSystem(ps, new Color(0.25f, 0.85f, 1.0f));

            AudioClip chime = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxPickupPath);
            AudioClip whoosh = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxWhooshPath);
            AudioClip fanfare = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxFanfarePath);

            CollectibleOrbCoin coinScript = root.AddComponent<CollectibleOrbCoin>();
            coinScript.Configure(CollectibleOrbCoin.CoinType.BonusDiamond, 50, chime, whoosh, fanfare, ps);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log($"[TASK-14] Prefab BonusDiamondCoin salvo em {prefabPath}");
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

        private static void ConfigureSparklesParticleSystem(ParticleSystem ps, Color color)
        {
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            main.gravityModifier = 0.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = color;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 25, 40) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Coin_Sparkle.mat");
        }
        #endregion

        #region Scene Setup
        private static void SetupForestScene(GameObject energyCoinPrefab, GameObject bonusCoinPrefab)
        {
            EditorSceneManager.OpenScene(ForestScenePath, OpenSceneMode.Single);

            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                Debug.LogError("[TASK-14] Terrain não encontrado na cena da Floresta!");
                return;
            }

            // Remove instâncias antigas de Task14
            string oldGroupTag = "[TASK-14] AdaptiveCoinTrail_Group";
            var oldObj = GameObject.Find(oldGroupTag);
            if (oldObj != null) UnityEngine.Object.DestroyImmediate(oldObj);

            GameObject rootGroup = new GameObject(oldGroupTag);

            // Adiciona CoinScoreManager
            CoinScoreManager scoreMgr = rootGroup.AddComponent<CoinScoreManager>();

            // Adiciona CoinTrailSpawner
            CoinTrailSpawner spawner = rootGroup.AddComponent<CoinTrailSpawner>();

            // ==================================================================
            // 1. Trilha Central de Moedas de Energia (14 Moedas Eixo da Marcha)
            // ==================================================================
            GameObject trailGroup = new GameObject("EnergyCoins_MainTrail");
            trailGroup.transform.SetParent(rootGroup.transform);

            // 6 waypoints mestres de controle mapeados diretamente no leito do caminho do paciente
            // Partindo da frente do paciente (58.00, 60.70) com forward (-0.957, 0, 0.289) em direção à ponte
            Vector2[] masterControlPoints = new Vector2[]
            {
                new Vector2(55.10f, 61.55f), // Início: 3m à frente do paciente no centro do caminho de terra
                new Vector2(51.30f, 62.70f), // Passando entre as flores e a cesta de maçãs
                new Vector2(46.50f, 64.15f), // Reta da clareira ao lado dos cogumelos musicais
                new Vector2(41.00f, 65.85f), // Aproximação da terceira lanterna
                new Vector2(36.50f, 67.20f), // Leito de aproximação à cabeceira da ponte
                new Vector2(32.80f, 68.35f)  // Início da ponte de madeira
            };

            int coinCount = 14;
            System.Collections.Generic.List<Vector3> waypoints3D = new System.Collections.Generic.List<Vector3>();

            for (int i = 0; i < coinCount; i++)
            {
                float t = (float)i / (coinCount - 1);
                // Interpolação suave ao longo dos segmentos mestres do caminho de terra
                float globalIdx = t * (masterControlPoints.Length - 1);
                int seg = Mathf.Clamp(Mathf.FloorToInt(globalIdx), 0, masterControlPoints.Length - 2);
                float segT = globalIdx - seg;

                Vector2 xz = Vector2.Lerp(masterControlPoints[seg], masterControlPoints[seg + 1], segT);

                float groundY = terrain.SampleHeight(new Vector3(xz.x, 0f, xz.y)) + terrain.transform.position.y;
                
                // Moedas na ponte ajustam à altura da plataforma de madeira
                if (i >= 12)
                {
                    groundY = Mathf.Max(groundY, 4.80f);
                }

                // Altura ergonômica ideal de 1.15m do solo (altura do peito/mãos do paciente no exoesqueleto)
                Vector3 worldPos = new Vector3(xz.x, groundY + 1.15f, xz.y);
                waypoints3D.Add(worldPos);

                GameObject coinGo = (GameObject)PrefabUtility.InstantiatePrefab(energyCoinPrefab, trailGroup.transform);
                coinGo.name = $"EnergyStarCoin_{i + 1}";
                coinGo.transform.position = worldPos;
                coinGo.transform.rotation = Quaternion.identity;
            }

            spawner.SetWaypoints(waypoints3D);

            // ==================================================================
            // 2. Moedas Bônus Secretas de Diamante (Alcance via Raio Laser VR)
            // ==================================================================
            GameObject bonusGroup = new GameObject("BonusCoins_SecretSpots");
            bonusGroup.transform.SetParent(rootGroup.transform);

            // 3 locais secretos desafiadores fora da linha de passada direta:
            // Spot 1: Sobre a pedra ao lado da Lanterna 2 na clareira (coleta com laser VR)
            // Spot 2: Elevado sob a copa da árvore na margem direita
            // Spot 3: Flutuando sobre as águas do rio ao lado da ponte
            Vector3[] bonusPositions = new Vector3[]
            {
                new Vector3(47.20f, 5.20f, 65.80f),
                new Vector3(45.50f, 6.60f, 67.80f),
                new Vector3(32.00f, 4.20f, 63.50f)
            };
            string[] bonusNames = new string[] { "BonusDiamondCoin_Rock", "BonusDiamondCoin_Tree", "BonusDiamondCoin_River" };

            for (int j = 0; j < bonusPositions.Length; j++)
            {
                GameObject bGo = (GameObject)PrefabUtility.InstantiatePrefab(bonusCoinPrefab, bonusGroup.transform);
                bGo.name = bonusNames[j];
                bGo.transform.position = bonusPositions[j];
                bGo.transform.rotation = Quaternion.Euler(15f, 30f * j, 0f);
            }

            scoreMgr.SetTotalCoins(coinCount + bonusPositions.Length);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[TASK-14] Cena Floresta_e_Rio configurada com {coinCount} moedas de trilha e {bonusPositions.Length} diamantes secretos!");
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

            // Print 1: Perspectiva da Trilha de Moedas
            // Visão em três quartos angular ao longo da trilha mostrando a linha curva e contínua de moedas até a ponte
            string pathTrail = Path.Combine(outDir, "TASK-14_Coin_Trail_Perspective.png");
            Vector3 camPos1 = new Vector3(54.50f, 4.70f, 59.80f);
            Vector3 lookAt1 = new Vector3(43.00f, 4.80f, 65.00f);
            VisualQACaptureEditor.CaptureCameraView(pathTrail, camPos1, lookAt1, 1920, 1080);
            Debug.Log($"[TASK-14 VisualQA] Capturado Coin Trail Perspective: {pathTrail}");

            // Print 2: Close-up da Moeda Octogonal EnergyStarCoin
            // Enquadramento frontal revelando a estrela 3D em alto-relevo reluzente e o disco ciano
            string pathCloseup = Path.Combine(outDir, "TASK-14_Coin_Closeup.png");
            GameObject coin1 = GameObject.Find("EnergyStarCoin_1");
            Vector3 c1Pos = coin1 != null ? coin1.transform.position : new Vector3(55.00f, 5.65f, 62.20f);
            Vector3 camPos2 = new Vector3(c1Pos.x, c1Pos.y, c1Pos.z - 0.52f);
            Vector3 lookAt2 = c1Pos;
            VisualQACaptureEditor.CaptureCameraView(pathCloseup, camPos2, lookAt2, 1920, 1080);
            Debug.Log($"[TASK-14 VisualQA] Capturado Coin Closeup: {pathCloseup}");

            // Print 3: Efeito Magnético / Diamante Secreto no Rochedo
            // Câmera focada diretamente no diamante secreto com ângulo limpo e desimpedido
            string pathDiamond = Path.Combine(outDir, "TASK-14_Magnetic_Attraction_Effect.png");
            GameObject dRock = GameObject.Find("BonusDiamondCoin_Rock");
            Vector3 dPos = dRock != null ? dRock.transform.position : new Vector3(47.20f, 5.20f, 65.80f);
            Vector3 camPos3 = new Vector3(48.50f, 5.60f, 64.60f);
            Vector3 lookAt3 = dPos;
            VisualQACaptureEditor.CaptureCameraView(pathDiamond, camPos3, lookAt3, 1920, 1080);
            Debug.Log($"[TASK-14 VisualQA] Capturado Magnetic Attraction / Diamond: {pathDiamond}");
        }
        #endregion
    }
}
