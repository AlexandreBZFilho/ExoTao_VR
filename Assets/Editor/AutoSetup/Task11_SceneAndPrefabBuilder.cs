using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using VRRehab.Core;
using VRRehab.EditorTools;
using VRRehab.Interactive.Task11;
using VRRehab.Ergonomics;

namespace VRRehab.EditorAutoSetup
{
    /// <summary>
    /// Builder e validador automatizado para a TASK-11:
    /// Sistema de Maçãs Interativas (Árvores Sacudíveis + Cestas de Pontuação + Splash no Rio)
    /// </summary>
    public static class Task11_SceneAndPrefabBuilder
    {
        private const string ModelsDir = "Assets/Models/Generated/Task11";
        private const string PrefabsDir = "Assets/Prefabs/Interactive";
        private const string AudioDir = "Assets/Audio/Generated/Task11";
        private const string TexturesDir = "Assets/Textures/Generated/Task11";
        private const string ForestScenePath = "Assets/Scenes/Floresta_e_Rio.unity";
        private const string OutVisualQADir = ".agents/visual_qa";

        private const string RedAppleModelPath = "Assets/Models/Generated/Task11/InteractiveRedApple.fbx";
        private const string GoldenAppleModelPath = "Assets/Models/Generated/Task11/GoldenBonusApple.fbx";
        private const string BasketModelPath = "Assets/Models/Generated/Task11/WickerCollectionBasket.fbx";

        private const string FruitTreeOriginalPrefab = "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Fruit_Tree_01_green.prefab";

        private const string SfxRustlePath = "Assets/Audio/Generated/Task11/tree_leaves_rustle.wav";
        private const string SfxDropPath = "Assets/Audio/Generated/Task11/apple_drop_thud.wav";
        private const string SfxBellPath = "Assets/Audio/Generated/Task11/basket_score_bell.wav";
        private const string SfxSplashPath = "Assets/Audio/Generated/Task11/water_splash.wav";
        private const string SfxSparklePath = "Assets/Audio/Generated/Task11/golden_apple_sparkle.wav";
        private const string LeafTexturePath = "Assets/Textures/Generated/Task11/Leaf_Particle.png";

        [MenuItem("VRRehab/AutoBuild/Build and Verify TASK-11")]
        public static void BuildAndVerify()
        {
            Debug.Log("[TASK-11] Iniciando construção completa do Sistema de Maçãs Interativas...");

            // 1. Reimporta modelos, áudios e texturas
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(RedAppleModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(GoldenAppleModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(BasketModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            AssetDatabase.ImportAsset(SfxRustlePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxDropPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxBellPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxSplashPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxSparklePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            if (File.Exists(LeafTexturePath))
            {
                AssetDatabase.ImportAsset(LeafTexturePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                TextureImporter ti = AssetImporter.GetAtPath(LeafTexturePath) as TextureImporter;
                if (ti != null)
                {
                    ti.textureType = TextureImporterType.Default;
                    ti.alphaIsTransparency = true;
                    ti.SaveAndReimport();
                }
            }

            // 2. Garante pastas e materiais
            EnsureDirectories();
            EnsureMaterials();

            // 3. Constrói Prefabs
            GameObject redApplePrefab = BuildApplePrefab(isGolden: false);
            GameObject goldenApplePrefab = BuildApplePrefab(isGolden: true);
            GameObject basketPrefab = BuildBasketPrefab();

            // 4. Configura cena da Floresta
            SetupForestScene(redApplePrefab, goldenApplePrefab, basketPrefab);

            // 5. Captura Visual QA
            CaptureVisualQA();

            Debug.Log("[TASK-11] Conclusão com sucesso de todos os estágios da TASK-11!");
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Models/Generated/Task11"))
            {
                AssetDatabase.CreateFolder("Assets/Models/Generated", "Task11");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Interactive"))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "Interactive");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Textures/Generated/Task11"))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Textures")) AssetDatabase.CreateFolder("Assets", "Textures");
                if (!AssetDatabase.IsValidFolder("Assets/Textures/Generated")) AssetDatabase.CreateFolder("Assets/Textures", "Generated");
                AssetDatabase.CreateFolder("Assets/Textures/Generated", "Task11");
            }
            if (!Directory.Exists(OutVisualQADir))
            {
                Directory.CreateDirectory(OutVisualQADir);
            }
        }

        public static void EnsureMaterials()
        {
            EnsureMaterial($"{ModelsDir}/M_Apple_Red.mat", "M_Apple_Red", () =>
            {
                Material m = MaterialShaderHelper.CreateLitMaterial("M_Apple_Red", new Color(0.85f, 0.08f, 0.10f), 0.65f, 0.05f);
                return m;
            });

            EnsureMaterial($"{ModelsDir}/M_Apple_Stem.mat", "M_Apple_Stem", () =>
            {
                Material m = MaterialShaderHelper.CreateLitMaterial("M_Apple_Stem", new Color(0.35f, 0.20f, 0.10f), 0.20f, 0.05f);
                return m;
            });

            EnsureMaterial($"{ModelsDir}/M_Apple_Leaf.mat", "M_Apple_Leaf", () =>
            {
                Material m = MaterialShaderHelper.CreateLitMaterial("M_Apple_Leaf", new Color(0.20f, 0.65f, 0.15f), 0.40f, 0.02f);
                return m;
            });

            EnsureMaterial($"{ModelsDir}/M_Apple_Gold.mat", "M_Apple_Gold", () =>
            {
                Material m = MaterialShaderHelper.CreateEmissiveMaterial("M_Apple_Gold", new Color(1.0f, 0.82f, 0.18f), new Color(0.60f, 0.45f, 0.05f), 1.5f);
                MaterialShaderHelper.SetSmoothness(m, 0.90f);
                MaterialShaderHelper.SetMetallic(m, 0.85f);
                return m;
            });

            EnsureMaterial($"{ModelsDir}/M_Wicker_Basket.mat", "M_Wicker_Basket", () =>
            {
                Material m = MaterialShaderHelper.CreateLitMaterial("M_Wicker_Basket", new Color(0.68f, 0.50f, 0.28f), 0.15f, 0.02f);
                return m;
            });

            EnsureMaterial($"{ModelsDir}/M_Basket_SignPost.mat", "M_Basket_SignPost", () =>
            {
                Material m = MaterialShaderHelper.CreateLitMaterial("M_Basket_SignPost", new Color(0.48f, 0.32f, 0.18f), 0.20f, 0.05f);
                return m;
            });

            EnsureMaterial($"{ModelsDir}/M_Basket_ScorePlate.mat", "M_Basket_ScorePlate", () =>
            {
                Material m = MaterialShaderHelper.CreateLitMaterial("M_Basket_ScorePlate", new Color(0.18f, 0.12f, 0.08f), 0.40f, 0.15f);
                return m;
            });

            EnsureMaterial($"{ModelsDir}/M_Basket_GoldTrim.mat", "M_Basket_GoldTrim", () =>
            {
                Material m = MaterialShaderHelper.CreateEmissiveMaterial("M_Basket_GoldTrim", new Color(1.0f, 0.82f, 0.15f), new Color(1.0f, 0.82f, 0.15f), 1.5f);
                MaterialShaderHelper.SetSmoothness(m, 0.85f);
                MaterialShaderHelper.SetMetallic(m, 0.80f);
                return m;
            });

            EnsureMaterial($"{ModelsDir}/M_Falling_Leaves.mat", "M_Falling_Leaves", () =>
            {
                Material m = MaterialShaderHelper.CreateTransparentMaterial("M_Falling_Leaves", Color.white, 0.95f);
                return m;
            });

            // Atribui a textura de folha ao material
            Material mLeaves = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Falling_Leaves.mat");
            if (mLeaves != null)
            {
                Texture2D leafTex = AssetDatabase.LoadAssetAtPath<Texture2D>(LeafTexturePath);
                if (leafTex != null)
                {
                    mLeaves.mainTexture = leafTex;
                    mLeaves.color = Color.white;
                    if (mLeaves.HasProperty("_BaseColor")) mLeaves.SetColor("_BaseColor", Color.white);
                }
            }

            EnsureMaterial($"{ModelsDir}/M_Water_Splash.mat", "M_Water_Splash", () =>
            {
                Material m = MaterialShaderHelper.CreateTransparentMaterial("M_Water_Splash", new Color(0.80f, 0.95f, 1.0f), 0.65f);
                return m;
            });
        }

        private static void EnsureMaterial(string path, string name, Func<Material> creator)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = creator();
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                MaterialShaderHelper.EnsureValidShader(mat);
            }
        }

        private static GameObject BuildApplePrefab(bool isGolden)
        {
            string prefabPath = $"{PrefabsDir}/{(isGolden ? "GoldenBonusApple" : "InteractiveRedApple")}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                AssetDatabase.DeleteAsset(prefabPath);
            }

            string fbxPath = isGolden ? GoldenAppleModelPath : RedAppleModelPath;
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null)
            {
                Debug.LogError($"[TASK-11] FBX da maçã não encontrado em {fbxPath}");
                return null;
            }

            GameObject root = new GameObject(isGolden ? "GoldenBonusApple" : "InteractiveRedApple");
            root.layer = LayerMask.NameToLayer("Default");

            GameObject visual = UnityEngine.Object.Instantiate(fbx, root.transform);
            visual.name = "Visual_Model";

            Material matBody = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/{(isGolden ? "M_Apple_Gold" : "M_Apple_Red")}.mat");
            Material matStem = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Apple_Stem.mat");
            Material matLeaf = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Apple_Leaf.mat");

            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                mr.sharedMaterials = new Material[] { matBody, matStem, matLeaf };
            }

            // Colisor esférico
            SphereCollider col = root.AddComponent<SphereCollider>();
            col.radius = isGolden ? 0.046f : 0.043f;
            col.center = Vector3.zero;

            // Rigidbody
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.18f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Partículas de Splash
            GameObject splashGo = new GameObject("WaterSplashFx");
            splashGo.transform.SetParent(root.transform, false);
            ParticleSystem splashPs = splashGo.AddComponent<ParticleSystem>();
            ConfigureSplashParticleSystem(splashPs);

            // Partículas de Sparkle (se dourada)
            ParticleSystem sparklePs = null;
            if (isGolden)
            {
                GameObject sparkleGo = new GameObject("GoldenSparkleFx");
                sparkleGo.transform.SetParent(root.transform, false);
                sparklePs = sparkleGo.AddComponent<ParticleSystem>();
                ConfigureSparkleParticleSystem(sparklePs);
            }

            // Componente InteractiveApple
            InteractiveApple appleComp = root.AddComponent<InteractiveApple>();
            AudioClip sfxImpact = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxDropPath);
            AudioClip sfxSplash = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxSplashPath);
            AudioClip sfxSparkle = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxSparklePath);

            appleComp.Configure(
                isGolden ? AppleType.GoldenBonus : AppleType.NormalRed,
                sfxImpact,
                sfxSplash,
                sfxSparkle,
                splashPs,
                sparklePs
            );

            // Feedback de destaque (Highlight)
            InteractableHighlightFeedback highlight = root.AddComponent<InteractableHighlightFeedback>();

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log($"[TASK-11] Prefab salvo: {prefabPath}");
            return savedPrefab;
        }

        private static GameObject BuildBasketPrefab()
        {
            string prefabPath = $"{PrefabsDir}/WickerCollectionBasket.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                AssetDatabase.DeleteAsset(prefabPath);
            }

            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(BasketModelPath);
            if (fbx == null)
            {
                Debug.LogError($"[TASK-11] FBX da cesta não encontrado em {BasketModelPath}");
                return null;
            }

            GameObject root = new GameObject("WickerCollectionBasket");
            root.layer = LayerMask.NameToLayer("Default");

            GameObject visual = UnityEngine.Object.Instantiate(fbx, root.transform);
            visual.name = "Visual_Model";

            Material matWicker = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Wicker_Basket.mat");
            Material matWood = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Basket_SignPost.mat");
            Material matPlate = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Basket_ScorePlate.mat");
            Material matTrim = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Basket_GoldTrim.mat");

            Transform plateTransform = null;

            // Atribuição de materiais específica e precisa por componente
            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = mr.gameObject.name;
                if (n.Contains("Plate"))
                {
                    mr.sharedMaterial = matPlate;
                    plateTransform = mr.transform;
                }
                else if (n.Contains("Trim"))
                {
                    mr.sharedMaterial = matTrim;
                }
                else if (n.Contains("Signboard") || n.Contains("Board"))
                {
                    mr.sharedMaterial = matWood;
                }
                else if (n.Contains("Signpost") || n.Contains("Post"))
                {
                    mr.sharedMaterial = matWood;
                }
                else
                {
                    mr.sharedMaterial = matWicker;
                }
            }

            // Colisor de base e paredes sólidas da cesta
            CylinderOrBoxCollidersForBasket(root);

            // Placar 3D na placa de madeira voltado para a frente (+Z local)
            TextMeshPro tmp = null;
            if (plateTransform != null)
            {
                GameObject tmpGo = new GameObject("ScoreboardText_TMP");
                tmpGo.transform.SetParent(plateTransform, false);
                tmpGo.transform.localPosition = new Vector3(0f, 0f, 0.015f);
                tmpGo.transform.localRotation = Quaternion.identity;
                tmpGo.transform.localScale = new Vector3(-1f, 1f, 1f);

                tmp = tmpGo.AddComponent<TextMeshPro>();
                tmp.text = "<b>CESTA DE MAÇÃS</b>\n<color=#FFD700>0 PTS</color>\n<size=70%>Colhidas: 0</size>";
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 0.08f;
                tmp.fontSizeMax = 0.22f;
                tmp.fontSize = 0.18f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                RectTransform rt = tmp.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = new Vector2(0.38f, 0.15f);
            }

            // Confetes festivos na cesta
            GameObject confettiGo = new GameObject("BasketConfettiFx");
            confettiGo.transform.SetParent(root.transform, false);
            confettiGo.transform.localPosition = new Vector3(0f, 0.38f, 0f);
            ParticleSystem confettiPs = confettiGo.AddComponent<ParticleSystem>();
            ConfigureBasketConfetti(confettiPs);

            // Script da Cesta
            AppleCollectionBasket basketComp = root.AddComponent<AppleCollectionBasket>();
            AudioClip sfxBell = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxBellPath);
            basketComp.Configure(sfxBell, confettiPs, tmp);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log($"[TASK-11] Prefab salvo: {prefabPath}");
            return savedPrefab;
        }

        private static void CylinderOrBoxCollidersForBasket(GameObject root)
        {
            // Colisor de chão do cesto
            BoxCollider floorCol = root.AddComponent<BoxCollider>();
            floorCol.center = new Vector3(0f, 0.02f, 0f);
            floorCol.size = new Vector3(0.40f, 0.04f, 0.40f);

            // Colisor de poste
            BoxCollider postCol = root.AddComponent<BoxCollider>();
            postCol.center = new Vector3(0f, 0.36f, -0.28f);
            postCol.size = new Vector3(0.06f, 0.72f, 0.06f);

            // Colisor de placa
            BoxCollider boardCol = root.AddComponent<BoxCollider>();
            boardCol.center = new Vector3(0f, 0.64f, -0.27f);
            boardCol.size = new Vector3(0.42f, 0.18f, 0.04f);
        }

        private static void ConfigureSplashParticleSystem(ParticleSystem ps)
        {
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.gravityModifier = 1.0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.85f, 0.95f, 1.0f, 0.85f);

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 25, 40) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.06f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Water_Splash.mat");
        }

        private static void ConfigureSparkleParticleSystem(ParticleSystem ps)
        {
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 1.0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(1.0f, 0.85f, 0.20f, 0.95f);

            var emission = ps.emission;
            emission.rateOverTime = 16f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.055f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Apple_Gold.mat");
        }

        private static void ConfigureBasketConfetti(ParticleSystem ps)
        {
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            main.gravityModifier = 0.75f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 0.85f, 0.15f), 0.0f),
                    new GradientColorKey(new Color(0.2f, 1f, 0.35f), 0.5f),
                    new GradientColorKey(new Color(1f, 0.15f, 0.35f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0.0f),
                    new GradientAlphaKey(1f, 0.8f),
                    new GradientAlphaKey(0f, 1.0f)
                }
            );
            main.startColor = grad;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 35, 60) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.20f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Basket_GoldTrim.mat");
        }

        private static void SetupForestScene(GameObject redPrefab, GameObject goldenPrefab, GameObject basketPrefab)
        {
            EditorSceneManager.OpenScene(ForestScenePath, OpenSceneMode.Single);

            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                Debug.LogError("[TASK-11] Terrain não encontrado na cena da Floresta!");
                return;
            }

            // Remove instâncias antigas de Task11 para montagem limpa
            string[] cleanupTags = new string[] { "[TASK-11] FruitTrees_Group", "[TASK-11] WickerBaskets_Group", "[TASK-11] StarterApples_Group" };
            foreach (var tag in cleanupTags)
            {
                var oldObj = GameObject.Find(tag);
                if (oldObj != null) UnityEngine.Object.DestroyImmediate(oldObj);
            }

            // 1. Grupo de Árvores Frutíferas Sacudíveis
            GameObject treesGroup = new GameObject("[TASK-11] FruitTrees_Group");
            GameObject treeOriginal = AssetDatabase.LoadAssetAtPath<GameObject>(FruitTreeOriginalPrefab);
            AudioClip sfxRustle = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxRustlePath);
            Material matLeaves = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Falling_Leaves.mat");

            Vector3[] treePositions = new Vector3[] {
                new Vector3(53.50f, 0f, 58.50f), // Árvore 1: Início da trilha à direita
                new Vector3(45.50f, 0f, 67.50f), // Árvore 2: Meio da trilha à esquerda
                new Vector3(37.50f, 0f, 63.50f)  // Árvore 3: Próximo à ponte
            };

            for (int i = 0; i < treePositions.Length; i++)
            {
                Vector3 basePos = treePositions[i];
                float y = terrain.SampleHeight(basePos) + terrain.transform.position.y;
                Vector3 treeWorldPos = new Vector3(basePos.x, y, basePos.z);

                GameObject treeGo = null;
                if (treeOriginal != null)
                {
                    treeGo = (GameObject)PrefabUtility.InstantiatePrefab(treeOriginal, treesGroup.transform);
                    treeGo.transform.position = treeWorldPos;
                    treeGo.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                }
                else
                {
                    treeGo = new GameObject($"FruitTree_{i + 1}");
                    treeGo.transform.SetParent(treesGroup.transform, false);
                    treeGo.transform.position = treeWorldPos;
                }
                treeGo.name = $"ShakeableFruitTree_{i + 1}";

                // Partículas de folhas cadentes
                GameObject leavesGo = new GameObject("FallingLeavesFx");
                leavesGo.transform.SetParent(treeGo.transform, false);
                leavesGo.transform.localPosition = new Vector3(0f, 3.8f, 0f);
                ParticleSystem leavesPs = leavesGo.AddComponent<ParticleSystem>();
                ConfigureLeavesParticleSystem(leavesPs, matLeaves);

                // Configura ShakeableFruitTree
                ShakeableFruitTree shakeComp = treeGo.GetComponent<ShakeableFruitTree>();
                if (shakeComp == null) shakeComp = treeGo.AddComponent<ShakeableFruitTree>();

                shakeComp.Configure(redPrefab, goldenPrefab, sfxRustle, leavesPs, treeGo.transform, new Vector3(0f, 3.8f, 0f));
            }

            // 2. Grupo de Cestas de Vime
            GameObject basketsGroup = new GameObject("[TASK-11] WickerBaskets_Group");
            Vector3[] basketPositions = new Vector3[] {
                new Vector3(53.80f, 0f, 63.80f), // Cesta 1: Fácil (prado ensolarado à direita da trilha)
                new Vector3(44.20f, 0f, 66.20f), // Cesta 2: Médio (beira da água)
                new Vector3(35.50f, 0f, 66.00f)  // Cesta 3: Desafio longo (próximo à ponte)
            };

            // Pontos centrais da trilha aos quais cada cesta deve se voltar
            Vector3[] trailFacingPoints = new Vector3[] {
                new Vector3(55.20f, 4.60f, 61.80f),
                new Vector3(46.00f, 4.50f, 64.50f),
                new Vector3(37.50f, 4.50f, 66.80f)
            };

            for (int i = 0; i < basketPositions.Length; i++)
            {
                Vector3 basePos = basketPositions[i];
                float y = terrain.SampleHeight(basePos) + terrain.transform.position.y - 0.12f;
                Vector3 basketWorldPos = new Vector3(basePos.x, y, basePos.z);

                GameObject basketGo = (GameObject)PrefabUtility.InstantiatePrefab(basketPrefab, basketsGroup.transform);
                basketGo.name = $"WickerCollectionBasket_{i + 1}";
                basketGo.transform.position = basketWorldPos;

                // Rotaciona para que o cesto e a frente do placar olhem diretamente para a trilha
                Vector3 dirToTrail = (trailFacingPoints[i] - basketWorldPos);
                dirToTrail.y = 0f;
                basketGo.transform.rotation = Quaternion.LookRotation(dirToTrail.normalized);

                // Pre-coloca 1 maçã dentro da cesta 1 e 1 maçã dourada ao lado na frente
                if (i == 0)
                {
                    GameObject appleInBasket = (GameObject)PrefabUtility.InstantiatePrefab(redPrefab, basketGo.transform);
                    appleInBasket.name = "Apple_In_Basket";
                    appleInBasket.transform.localPosition = new Vector3(0.04f, 0.30f, 0.05f);
                    appleInBasket.transform.localRotation = Quaternion.Euler(15f, 35f, -10f);
                    Rigidbody rbIn = appleInBasket.GetComponent<Rigidbody>();
                    if (rbIn != null) rbIn.isKinematic = true;

                    GameObject appleBeside = (GameObject)PrefabUtility.InstantiatePrefab(goldenPrefab, basketGo.transform);
                    appleBeside.name = "GoldenApple_Beside_Basket";
                    appleBeside.transform.localPosition = new Vector3(0.32f, 0.08f, 0.24f);
                    appleBeside.transform.localRotation = Quaternion.Euler(-10f, 60f, 20f);
                    Rigidbody rbBeside = appleBeside.GetComponent<Rigidbody>();
                    if (rbBeside != null) rbBeside.isKinematic = true;
                }
            }

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[TASK-11] Cena da Floresta_e_Rio configurada e salva com sucesso!");
        }

        private static void ConfigureLeavesParticleSystem(ParticleSystem ps, Material matLeaves)
        {
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.0f, 3.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            main.gravityModifier = 0.35f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = Color.white;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 40, 70) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.4f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = matLeaves;
        }

        public static void CaptureVisualQA()
        {
            string outDir = Path.GetFullPath(OutVisualQADir);
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            // Garante que a cena da Floresta está aberta
            if (!EditorSceneManager.GetActiveScene().path.EndsWith("Floresta_e_Rio.unity"))
            {
                EditorSceneManager.OpenScene(ForestScenePath, OpenSceneMode.Single);
            }

            // Print 1: Overview da Trilha da Floresta a partir de uma perspectiva do paciente na largada
            // Print 1: Overview da Trilha da Floresta mostrando a trilha, as árvores frutíferas e a cesta
            string pathOverview = Path.Combine(outDir, "TASK-11_Forest_Trail_Overview.png");
            Vector3 camPos1 = new Vector3(56.20f, 4.90f, 59.50f);
            Vector3 lookAt1 = new Vector3(43.50f, 4.00f, 64.50f);
            VisualQACaptureEditor.CaptureCameraView(pathOverview, camPos1, lookAt1, 1920, 1080);
            Debug.Log($"[TASK-11 VisualQA] Capturado Overview: {pathOverview}");

            // Print 2: Close-up da Cesta 1 mostrando a frente, o cesto, as maçãs e a placa com pontuação
            string pathCloseup = Path.Combine(outDir, "TASK-11_Apple_Basket_Closeup.png");
            GameObject b1 = GameObject.Find("WickerCollectionBasket_1");
            Vector3 b1Pos = b1 != null ? b1.transform.position : new Vector3(53.80f, 4.34f, 63.80f);
            Vector3 b1Fwd = b1 != null ? b1.transform.forward : new Vector3(0.58f, 0f, -0.81f);
            Vector3 camPos2 = b1Pos + b1Fwd * 1.25f + Vector3.up * 0.40f;
            Vector3 lookAt2 = b1Pos + Vector3.up * 0.45f;
            VisualQACaptureEditor.CaptureCameraView(pathCloseup, camPos2, lookAt2, 1920, 1080);
            Debug.Log($"[TASK-11 VisualQA] Capturado Closeup: {pathCloseup}");

            // Print 3: Evento de sacudir a árvore com maçãs no ar e folhas cadentes
            string pathShakeEvent = Path.Combine(outDir, "TASK-11_Tree_Shake_Event.png");
            GameObject tree1 = GameObject.Find("ShakeableFruitTree_1");
            Vector3 treePos = tree1 != null ? tree1.transform.position : new Vector3(53.50f, 4.80f, 58.50f);

            GameObject tempApple1 = null;
            GameObject tempApple2 = null;
            GameObject tempApple3 = null;

            try
            {
                GameObject redPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsDir}/InteractiveRedApple.prefab");
                GameObject goldenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsDir}/GoldenBonusApple.prefab");

                // Posiciona 3 maçãs caindo em destaque na frente da copa
                if (redPrefab != null)
                {
                    tempApple1 = UnityEngine.Object.Instantiate(redPrefab, treePos + new Vector3(0.55f, 2.70f, 1.10f), Quaternion.Euler(25f, 45f, 0f));
                    tempApple1.transform.localScale = Vector3.one * 1.35f;
                    Rigidbody rb1 = tempApple1.GetComponent<Rigidbody>();
                    if (rb1 != null) rb1.isKinematic = true;

                    tempApple3 = UnityEngine.Object.Instantiate(redPrefab, treePos + new Vector3(1.10f, 2.10f, 0.85f), Quaternion.Euler(60f, -20f, 15f));
                    tempApple3.transform.localScale = Vector3.one * 1.35f;
                    Rigidbody rb3 = tempApple3.GetComponent<Rigidbody>();
                    if (rb3 != null) rb3.isKinematic = true;
                }
                if (goldenPrefab != null)
                {
                    tempApple2 = UnityEngine.Object.Instantiate(goldenPrefab, treePos + new Vector3(0.15f, 2.30f, 1.35f), Quaternion.Euler(-15f, 30f, 45f));
                    tempApple2.transform.localScale = Vector3.one * 1.35f;
                    Rigidbody rb2 = tempApple2.GetComponent<Rigidbody>();
                    if (rb2 != null) rb2.isKinematic = true;
                }

                // Dispara partículas de folhas temporariamente para o print
                Transform leavesT = tree1 != null ? tree1.transform.Find("FallingLeavesFx") : null;
                if (leavesT != null)
                {
                    ParticleSystem ps = leavesT.GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        ps.Simulate(0.50f, true, true);
                    }
                }

                Vector3 camPos3 = treePos + new Vector3(2.60f, 1.40f, 2.40f);
                Vector3 lookAt3 = treePos + new Vector3(0.20f, 2.60f, 0.80f);
                VisualQACaptureEditor.CaptureCameraView(pathShakeEvent, camPos3, lookAt3, 1920, 1080);
                Debug.Log($"[TASK-11 VisualQA] Capturado Shake Event: {pathShakeEvent}");
            }
            finally
            {
                if (tempApple1 != null) UnityEngine.Object.DestroyImmediate(tempApple1);
                if (tempApple2 != null) UnityEngine.Object.DestroyImmediate(tempApple2);
                if (tempApple3 != null) UnityEngine.Object.DestroyImmediate(tempApple3);
            }
        }
    }
}
