using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using VRRehab.Core;
using VRRehab.EditorTools;
using VRRehab.Interactive.Task12;
using VRRehab.Ergonomics;

namespace VRRehab.EditorAutoSetup
{
    /// <summary>
    /// Builder e validador automatizado para a TASK-12:
    /// Sementes Mágicas e Pinhões Arremessáveis (Brotação Procedural de Flores e Cogumelos)
    /// </summary>
    public static class Task12_SceneAndPrefabBuilder
    {
        private const string ModelsDir = "Assets/Models/Generated/Task12";
        private const string PrefabsDir = "Assets/Prefabs/Interactive";
        private const string AudioDir = "Assets/Audio/Generated/Task12";
        private const string ForestScenePath = "Assets/Scenes/Floresta_e_Rio.unity";
        private const string OutVisualQADir = ".agents/visual_qa";

        private const string PineconeModelPath = "Assets/Models/Generated/Task12/GlowingPineconeSeed.fbx";
        private const string FlowerPatchModelPath = "Assets/Models/Generated/Task12/SproutingFlowerPatch.fbx";
        private const string MushroomClusterModelPath = "Assets/Models/Generated/Task12/BouncyMushroomCluster.fbx";
        private const string PedestalModelPath = "Assets/Models/Generated/Task12/SeedNestPedestal.fbx";

        private const string SfxPickupPath = "Assets/Audio/Generated/Task12/seed_pickup_chime.wav";
        private const string SfxBurstPath = "Assets/Audio/Generated/Task12/seed_impact_burst.wav";
        private const string SfxSproutPath = "Assets/Audio/Generated/Task12/plant_sprout_elastic.wav";
        private const string SfxBouncePath = "Assets/Audio/Generated/Task12/mushroom_bounce_boing.wav";

        [MenuItem("VRRehab/AutoBuild/Build and Verify TASK-12")]
        public static void BuildAndVerify()
        {
            Debug.Log("[TASK-12] Iniciando construção completa de Sementes Mágicas e Brotação Procedural...");

            // 1. Reimporta modelos e áudios
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(PineconeModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(FlowerPatchModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(MushroomClusterModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(PedestalModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            AssetDatabase.ImportAsset(SfxPickupPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxBurstPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxSproutPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxBouncePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            // 2. Garante pastas e materiais
            EnsureDirectories();
            EnsureMaterials();

            // 3. Constrói Prefabs
            GameObject flowerPatchPrefab = BuildFlowerPatchPrefab();
            GameObject mushroomClusterPrefab = BuildMushroomClusterPrefab();
            GameObject pineconePrefab = BuildPineconePrefab(flowerPatchPrefab, mushroomClusterPrefab);
            GameObject pedestalPrefab = BuildPedestalPrefab();

            // 4. Configura cena da Floresta
            SetupForestScene(pineconePrefab, pedestalPrefab, flowerPatchPrefab, mushroomClusterPrefab);

            // 5. Captura Visual QA
            CaptureVisualQA();

            Debug.Log("[TASK-12] Conclusão com sucesso de todos os estágios da TASK-12!");
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Models/Generated/Task12"))
            {
                AssetDatabase.CreateFolder("Assets/Models/Generated", "Task12");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Interactive"))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "Interactive");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Audio/Generated/Task12"))
            {
                AssetDatabase.CreateFolder("Assets/Audio/Generated", "Task12");
            }
            if (!Directory.Exists(OutVisualQADir))
            {
                Directory.CreateDirectory(OutVisualQADir);
            }
        }

        public static void EnsureMaterials()
        {
            // 1. Materiais do Pinhão
            EnsureMaterial($"{ModelsDir}/M_Pinecone_Bark.mat", "M_Pinecone_Bark", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Pinecone_Bark", new Color(0.32f, 0.18f, 0.08f), 0.35f, 0.05f);
            });

            EnsureMaterial($"{ModelsDir}/M_Pinecone_Glow.mat", "M_Pinecone_Glow", () =>
            {
                Material m = MaterialShaderHelper.CreateEmissiveMaterial("M_Pinecone_Glow", new Color(0.12f, 0.88f, 0.98f), new Color(0.15f, 0.90f, 1.0f), 2.8f);
                MaterialShaderHelper.SetSmoothness(m, 0.85f);
                MaterialShaderHelper.SetMetallic(m, 0.20f);
                return m;
            });

            // 2. Materiais das Flores
            EnsureMaterial($"{ModelsDir}/M_Flower_BaseMound.mat", "M_Flower_BaseMound", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Flower_BaseMound", new Color(0.24f, 0.16f, 0.09f), 0.15f, 0.02f);
            });

            EnsureMaterial($"{ModelsDir}/M_Flower_Foliage.mat", "M_Flower_Foliage", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Flower_Foliage", new Color(0.16f, 0.60f, 0.14f), 0.35f, 0.02f);
            });

            EnsureMaterial($"{ModelsDir}/M_Flower_Center.mat", "M_Flower_Center", () =>
            {
                return MaterialShaderHelper.CreateEmissiveMaterial("M_Flower_Center", new Color(1.0f, 0.72f, 0.05f), new Color(1.0f, 0.65f, 0.0f), 1.6f);
            });

            EnsureMaterial($"{ModelsDir}/M_Petals_Yellow.mat", "M_Petals_Yellow", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Petals_Yellow", new Color(1.0f, 0.88f, 0.10f), 0.50f, 0.05f);
            });

            EnsureMaterial($"{ModelsDir}/M_Petals_Magenta.mat", "M_Petals_Magenta", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Petals_Magenta", new Color(0.92f, 0.15f, 0.55f), 0.50f, 0.05f);
            });

            EnsureMaterial($"{ModelsDir}/M_Petals_Cyan.mat", "M_Petals_Cyan", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Petals_Cyan", new Color(0.10f, 0.78f, 0.95f), 0.50f, 0.05f);
            });

            // 3. Materiais dos Cogumelos
            EnsureMaterial($"{ModelsDir}/M_Mushroom_Base.mat", "M_Mushroom_Base", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Mushroom_Base", new Color(0.20f, 0.38f, 0.14f), 0.15f, 0.02f);
            });

            EnsureMaterial($"{ModelsDir}/M_Mushroom_Stem.mat", "M_Mushroom_Stem", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Mushroom_Stem", new Color(0.92f, 0.88f, 0.78f), 0.40f, 0.05f);
            });

            EnsureMaterial($"{ModelsDir}/M_Mushroom_Cap_Red.mat", "M_Mushroom_Cap_Red", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Mushroom_Cap_Red", new Color(0.88f, 0.12f, 0.14f), 0.65f, 0.08f);
            });

            EnsureMaterial($"{ModelsDir}/M_Mushroom_Spots.mat", "M_Mushroom_Spots", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Mushroom_Spots", new Color(0.98f, 0.98f, 0.95f), 0.45f, 0.05f);
            });

            EnsureMaterial($"{ModelsDir}/M_Mushroom_Gills.mat", "M_Mushroom_Gills", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Mushroom_Gills", new Color(0.85f, 0.78f, 0.65f), 0.25f, 0.02f);
            });

            // 4. Materiais do Pedestal
            EnsureMaterial($"{ModelsDir}/M_Pedestal_Bark.mat", "M_Pedestal_Bark", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Pedestal_Bark", new Color(0.35f, 0.22f, 0.12f), 0.20f, 0.05f);
            });

            EnsureMaterial($"{ModelsDir}/M_Pedestal_Moss.mat", "M_Pedestal_Moss", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Pedestal_Moss", new Color(0.22f, 0.52f, 0.15f), 0.25f, 0.02f);
            });

            EnsureMaterial($"{ModelsDir}/M_Pedestal_RuneGlow.mat", "M_Pedestal_RuneGlow", () =>
            {
                Material m = MaterialShaderHelper.CreateEmissiveMaterial("M_Pedestal_RuneGlow", new Color(0.15f, 0.85f, 0.95f), new Color(0.15f, 0.85f, 0.95f), 2.2f);
                MaterialShaderHelper.SetSmoothness(m, 0.80f);
                return m;
            });

            // 5. Partículas
            EnsureMaterial($"{ModelsDir}/M_Pollen_Sparkle.mat", "M_Pollen_Sparkle", () =>
            {
                return MaterialShaderHelper.CreateTransparentMaterial("M_Pollen_Sparkle", new Color(1.0f, 0.88f, 0.35f, 0.85f), 0.9f);
            });

            EnsureMaterial($"{ModelsDir}/M_Magic_Burst.mat", "M_Magic_Burst", () =>
            {
                return MaterialShaderHelper.CreateTransparentMaterial("M_Magic_Burst", new Color(0.25f, 0.95f, 1.0f, 0.90f), 0.95f);
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

        #region Prefab Builders
        private static GameObject BuildPineconePrefab(GameObject flowerPatchPrefab, GameObject mushroomClusterPrefab)
        {
            string prefabPath = $"{PrefabsDir}/GlowingPineconeSeed.prefab";
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(PineconeModelPath);
            if (fbx == null)
            {
                Debug.LogError($"[TASK-12] FBX não encontrado em {PineconeModelPath}");
                return null;
            }

            GameObject root = new GameObject("GlowingPineconeSeed");
            root.layer = LayerMask.NameToLayer("Default");

            GameObject visual = UnityEngine.Object.Instantiate(fbx, root.transform);
            visual.name = "Visual_Model";

            Material matBark = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Pinecone_Bark.mat");
            Material matGlow = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Pinecone_Glow.mat");

            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = mr.gameObject.name;
                if (n.Contains("Crown") || (n.Contains("Scale_") && mr.sharedMaterial != null && mr.sharedMaterial.name.Contains("Glow")))
                {
                    mr.sharedMaterial = matGlow;
                }
                else
                {
                    mr.sharedMaterial = matBark;
                }
            }

            visual.transform.localScale = Vector3.one * 1.35f;

            // Rigidbody ergonômico
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.25f;
            rb.linearDamping = 0.15f;
            rb.angularDamping = 0.25f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // Colisor de cápsula arredondada (~0.14m altura, 0.08m diâmetro)
            CapsuleCollider col = root.AddComponent<CapsuleCollider>();
            col.radius = 0.045f;
            col.height = 0.14f;
            col.center = new Vector3(0f, 0.07f, 0f);

            // Aura de brilho místico (partículas cintilantes)
            GameObject auraGo = new GameObject("AuraGlowFx");
            auraGo.transform.SetParent(root.transform, false);
            auraGo.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            ParticleSystem auraPs = auraGo.AddComponent<ParticleSystem>();
            ConfigureAuraParticles(auraPs);

            // Partículas de explosão de pólen (emitidas no impacto)
            GameObject burstGo = new GameObject("ImpactBurstFx");
            burstGo.transform.SetParent(root.transform, false);
            burstGo.transform.localPosition = Vector3.zero;
            ParticleSystem burstPs = burstGo.AddComponent<ParticleSystem>();
            ConfigureImpactBurstParticles(burstPs);

            // Áudios
            AudioClip sfxPickup = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxPickupPath);
            AudioClip sfxBurst = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxBurstPath);
            AudioClip sfxSprout = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxSproutPath);

            // Script de Agarre Ergonômico e Brotação
            MagicSeedPinecone seedComp = root.AddComponent<MagicSeedPinecone>();
            seedComp.Configure(flowerPatchPrefab, mushroomClusterPrefab, sfxPickup, sfxBurst, sfxSprout, auraPs, burstPs);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log($"[TASK-12] Prefab salvo: {prefabPath}");
            return savedPrefab;
        }

        private static GameObject BuildFlowerPatchPrefab()
        {
            string prefabPath = $"{PrefabsDir}/SproutingFlowerPatch.prefab";
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FlowerPatchModelPath);
            if (fbx == null)
            {
                Debug.LogError($"[TASK-12] FBX não encontrado em {FlowerPatchModelPath}");
                return null;
            }

            GameObject root = new GameObject("SproutingFlowerPatch");
            root.layer = LayerMask.NameToLayer("Default");

            GameObject visual = UnityEngine.Object.Instantiate(fbx, root.transform);
            visual.name = "Visual_Model";

            Material matMound = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Flower_BaseMound.mat");
            Material matFoliage = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Flower_Foliage.mat");
            Material matCenter = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Flower_Center.mat");
            Material matYellow = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Petals_Yellow.mat");
            Material matMagenta = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Petals_Magenta.mat");
            Material matCyan = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Petals_Cyan.mat");

            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = mr.gameObject.name;
                if (n.Contains("Mound"))
                {
                    mr.sharedMaterial = matMound;
                }
                else if (n.Contains("Stem") || n.Contains("Leaf"))
                {
                    mr.sharedMaterial = matFoliage;
                }
                else if (n.Contains("Center"))
                {
                    mr.sharedMaterial = matCenter;
                }
                else if (n.Contains("Petals"))
                {
                    // Distribui as cores conforme o número da flor
                    if (n.Contains("_1") || n.Contains("_5")) mr.sharedMaterial = matYellow;
                    else if (n.Contains("_2") || n.Contains("_6")) mr.sharedMaterial = matMagenta;
                    else mr.sharedMaterial = matCyan;
                }
            }

            // Colisor de base (~0.55m diâmetro)
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.04f, 0f);
            col.size = new Vector3(0.55f, 0.08f, 0.55f);

            // Partículas de pólen flutuante
            GameObject pollenGo = new GameObject("PollenDriftFx");
            pollenGo.transform.SetParent(root.transform, false);
            pollenGo.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            ParticleSystem pollenPs = pollenGo.AddComponent<ParticleSystem>();
            ConfigurePollenParticles(pollenPs);

            // Script de ciclo de vida
            SproutingPlantLifecycle plantComp = root.AddComponent<SproutingPlantLifecycle>();
            plantComp.Configure(isMushroom: false, sfxBounce: null, pollenPs: pollenPs);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log($"[TASK-12] Prefab salvo: {prefabPath}");
            return savedPrefab;
        }

        private static GameObject BuildMushroomClusterPrefab()
        {
            string prefabPath = $"{PrefabsDir}/BouncyMushroomCluster.prefab";
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(MushroomClusterModelPath);
            if (fbx == null)
            {
                Debug.LogError($"[TASK-12] FBX não encontrado em {MushroomClusterModelPath}");
                return null;
            }

            GameObject root = new GameObject("BouncyMushroomCluster");
            root.layer = LayerMask.NameToLayer("Default");

            GameObject visual = UnityEngine.Object.Instantiate(fbx, root.transform);
            visual.name = "Visual_Model";

            Material matBase = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Mushroom_Base.mat");
            Material matStem = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Mushroom_Stem.mat");
            Material matCap = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Mushroom_Cap_Red.mat");
            Material matSpots = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Mushroom_Spots.mat");
            Material matGills = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Mushroom_Gills.mat");

            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = mr.gameObject.name;
                if (n.Contains("BaseMound")) mr.sharedMaterial = matBase;
                else if (n.Contains("Stem")) mr.sharedMaterial = matStem;
                else if (n.Contains("Cap_Mesh") || n.EndsWith("_Cap")) mr.sharedMaterial = matCap;
                else if (n.Contains("Spots")) mr.sharedMaterial = matSpots;
                else if (n.Contains("Gills")) mr.sharedMaterial = matGills;
            }

            // Colisor de base e trigger nos chapéus
            BoxCollider floorCol = root.AddComponent<BoxCollider>();
            floorCol.center = new Vector3(0f, 0.03f, 0f);
            floorCol.size = new Vector3(0.42f, 0.06f, 0.42f);

            SphereCollider capCol = root.AddComponent<SphereCollider>();
            capCol.center = new Vector3(0.02f, 0.22f, 0.01f);
            capCol.radius = 0.14f;

            // Partículas de esporos mágicos
            GameObject sporeGo = new GameObject("SporeDriftFx");
            sporeGo.transform.SetParent(root.transform, false);
            sporeGo.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            ParticleSystem sporePs = sporeGo.AddComponent<ParticleSystem>();
            ConfigurePollenParticles(sporePs);

            // Script de ciclo de vida e interação elástica
            AudioClip sfxBounce = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxBouncePath);
            SproutingPlantLifecycle plantComp = root.AddComponent<SproutingPlantLifecycle>();
            plantComp.Configure(isMushroom: true, sfxBounce: sfxBounce, pollenPs: sporePs);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log($"[TASK-12] Prefab salvo: {prefabPath}");
            return savedPrefab;
        }

        private static GameObject BuildPedestalPrefab()
        {
            string prefabPath = $"{PrefabsDir}/SeedNestPedestal.prefab";
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(PedestalModelPath);
            if (fbx == null)
            {
                Debug.LogError($"[TASK-12] FBX não encontrado em {PedestalModelPath}");
                return null;
            }

            GameObject root = new GameObject("SeedNestPedestal");
            root.layer = LayerMask.NameToLayer("Default");

            GameObject visual = UnityEngine.Object.Instantiate(fbx, root.transform);
            visual.name = "Visual_Model";

            Material matBark = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Pedestal_Bark.mat");
            Material matMoss = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Pedestal_Moss.mat");
            Material matRune = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Pedestal_RuneGlow.mat");

            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = mr.gameObject.name;
                if (n.Contains("Trunk")) mr.sharedMaterial = matBark;
                else if (n.Contains("Moss")) mr.sharedMaterial = matMoss;
                else if (n.Contains("Rune")) mr.sharedMaterial = matRune;
            }

            // Colisor cilíndrico de suporte sólido
            CapsuleCollider col = root.AddComponent<CapsuleCollider>();
            col.radius = 0.17f;
            col.height = 0.65f;
            col.center = new Vector3(0f, 0.325f, 0f);

            // Luz suave destacando o ninho
            GameObject lightGo = new GameObject("NestGlowLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.70f, 0f);
            Light nestLight = lightGo.AddComponent<Light>();
            nestLight.type = LightType.Point;
            nestLight.color = new Color(0.18f, 0.90f, 1.0f);
            nestLight.range = 1.4f;
            nestLight.intensity = 0.95f;

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log($"[TASK-12] Prefab salvo: {prefabPath}");
            return savedPrefab;
        }
        #endregion

        #region Particle System Configurations
        private static void ConfigureAuraParticles(ParticleSystem ps)
        {
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 1.0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.028f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.20f, 0.92f, 1.0f, 0.85f);

            var emission = ps.emission;
            emission.rateOverTime = 14f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.045f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Pollen_Sparkle.mat");
        }

        private static void ConfigureImpactBurstParticles(ParticleSystem ps)
        {
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
            main.gravityModifier = 0.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.20f, 0.92f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(1.0f, 0.85f, 0.20f), 0.5f),
                    new GradientColorKey(new Color(0.95f, 0.30f, 0.80f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0.0f),
                    new GradientAlphaKey(0.9f, 0.7f),
                    new GradientAlphaKey(0f, 1.0f)
                }
            );
            main.startColor = grad;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 30, 50) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = 0.06f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Magic_Burst.mat");
        }

        private static void ConfigurePollenParticles(ParticleSystem ps)
        {
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 2.0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
            main.gravityModifier = -0.05f; // Flutua lentamente para cima
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(1.0f, 0.88f, 0.30f, 0.75f);

            var emission = ps.emission;
            emission.rateOverTime = 8f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.40f, 0.10f, 0.40f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Pollen_Sparkle.mat");
        }
        #endregion

        #region Scene Setup
        private static void SetupForestScene(GameObject pineconePrefab, GameObject pedestalPrefab, GameObject flowerPatchPrefab, GameObject mushroomClusterPrefab)
        {
            EditorSceneManager.OpenScene(ForestScenePath, OpenSceneMode.Single);

            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                Debug.LogError("[TASK-12] Terrain não encontrado na cena da Floresta!");
                return;
            }

            // Remove instâncias antigas de Task12
            string oldGroupTag = "[TASK-12] MagicSeeds_Group";
            var oldObj = GameObject.Find(oldGroupTag);
            if (oldObj != null) UnityEngine.Object.DestroyImmediate(oldObj);

            GameObject rootGroup = new GameObject(oldGroupTag);

            // 1. Estações de Sementes Mágicas ao longo da trilha
            Vector3[] stationPositions = new Vector3[] {
                new Vector3(56.20f, 0f, 62.40f), // Estação 1: Início da trilha (visão imediata do paciente)
                new Vector3(48.50f, 0f, 63.20f), // Estação 2: Meio da trilha (margem esquerda)
                new Vector3(39.50f, 0f, 65.50f)  // Estação 3: Próximo à ponte de madeira
            };

            for (int i = 0; i < stationPositions.Length; i++)
            {
                Vector3 basePos = stationPositions[i];
                float y = terrain.SampleHeight(basePos) + terrain.transform.position.y;
                Vector3 pedestalWorldPos = new Vector3(basePos.x, y, basePos.z);

                // Instancia o Pedestal
                GameObject pedestalGo = (GameObject)PrefabUtility.InstantiatePrefab(pedestalPrefab, rootGroup.transform);
                pedestalGo.name = $"SeedNestPedestal_{i + 1}";
                pedestalGo.transform.position = pedestalWorldPos;
                pedestalGo.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

                // Instancia o Pinhão Mágico repousando no ninho do pedestal
                Vector3 seedWorldPos = pedestalWorldPos + new Vector3(0f, 0.65f, 0f);
                GameObject pineconeGo = (GameObject)PrefabUtility.InstantiatePrefab(pineconePrefab, rootGroup.transform);
                pineconeGo.name = $"GlowingPineconeSeed_{i + 1}";
                pineconeGo.transform.position = seedWorldPos;
                pineconeGo.transform.rotation = Quaternion.Euler(UnityEngine.Random.Range(-5f, 5f), UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(-5f, 5f));

                // Configura ancoragem de repouso (HomePosition)
                MagicSeedPinecone seedComp = pineconeGo.GetComponent<MagicSeedPinecone>();
                if (seedComp != null)
                {
                    seedComp.SetHomeTransform(seedWorldPos, pineconeGo.transform.rotation);
                }
            }

            // 2. Demonstração de Canteiros Brotados na margem ensolarada da trilha
            // Canteiro de Flores na campina aberta (lado esquerdo da trilha, sem cercas ou cestos)
            Vector3 flowerPos = new Vector3(54.20f, 0f, 60.40f);
            float flowerY = terrain.SampleHeight(flowerPos) + terrain.transform.position.y;
            Vector3 flowerWorldPos = new Vector3(flowerPos.x, flowerY, flowerPos.z);
            GameObject flowerGo = (GameObject)PrefabUtility.InstantiatePrefab(flowerPatchPrefab, rootGroup.transform);
            flowerGo.name = "Demo_Sprouted_FlowerPatch";
            flowerGo.transform.position = flowerWorldPos;
            flowerGo.transform.rotation = Quaternion.Euler(0f, 35f, 0f);

            // Canteiro de Cogumelos Saltitantes na campina aberta
            Vector3 mushPos = new Vector3(53.10f, 0f, 61.10f);
            float mushY = terrain.SampleHeight(mushPos) + terrain.transform.position.y;
            Vector3 mushWorldPos = new Vector3(mushPos.x, mushY, mushPos.z);
            GameObject mushGo = (GameObject)PrefabUtility.InstantiatePrefab(mushroomClusterPrefab, rootGroup.transform);
            mushGo.name = "Demo_Sprouted_MushroomCluster";
            mushGo.transform.position = mushWorldPos;
            mushGo.transform.rotation = Quaternion.Euler(0f, -20f, 0f);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[TASK-12] Cena da Floresta configurada e salva com sucesso!");
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

            // Print 1: Visão da margem da trilha mostrando o suporte com o pinhão mágico iluminado pronto para ser puxado
            string pathPinecone = Path.Combine(outDir, "TASK-12_Magic_Pinecone_Trail.png");
            GameObject p1 = GameObject.Find("SeedNestPedestal_1");
            Vector3 p1Pos = p1 != null ? p1.transform.position : new Vector3(56.20f, 4.55f, 62.40f);
            Vector3 camPos1 = p1Pos + new Vector3(0.85f, 0.85f, -0.70f);
            Vector3 lookAt1 = p1Pos + new Vector3(0f, 0.66f, 0f);
            VisualQACaptureEditor.CaptureCameraView(pathPinecone, camPos1, lookAt1, 1920, 1080);
            Debug.Log($"[TASK-12 VisualQA] Capturado Pinecone Trail: {pathPinecone}");

            // Print 2: Close-up do canteiro de flores brotado perfeitamente assentado no chão com suas cores vivas
            string pathFlower = Path.Combine(outDir, "TASK-12_Flower_Patch_Sprouted.png");
            GameObject flowerGo = GameObject.Find("Demo_Sprouted_FlowerPatch");
            Vector3 flowerPos = flowerGo != null ? flowerGo.transform.position : new Vector3(54.20f, 4.55f, 60.40f);
            Vector3 camPos2 = flowerPos + new Vector3(0.52f, 0.42f, -0.52f);
            Vector3 lookAt2 = flowerPos + new Vector3(0f, 0.18f, 0f);
            VisualQACaptureEditor.CaptureCameraView(pathFlower, camPos2, lookAt2, 1920, 1080);
            Debug.Log($"[TASK-12 VisualQA] Capturado Flower Patch: {pathFlower}");

            // Print 3: Close-up do canteiro de cogumelos saltitantes brotado na margem da trilha
            string pathMushroom = Path.Combine(outDir, "TASK-12_Mushroom_Cluster_Sprouted.png");
            GameObject mushGo = GameObject.Find("Demo_Sprouted_MushroomCluster");
            Vector3 mushPos = mushGo != null ? mushGo.transform.position : new Vector3(53.10f, 4.55f, 61.10f);
            Vector3 camPos3 = mushPos + new Vector3(0.48f, 0.38f, -0.45f);
            Vector3 lookAt3 = mushPos + new Vector3(0f, 0.15f, 0f);
            VisualQACaptureEditor.CaptureCameraView(pathMushroom, camPos3, lookAt3, 1920, 1080);
            Debug.Log($"[TASK-12 VisualQA] Capturado Mushroom Cluster: {pathMushroom}");
        }
        #endregion
    }
}
