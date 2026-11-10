using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using VRRehab.Core;
using VRRehab.DronePet;
using VRRehab.EditorTools;
using VRRehab.Ergonomics;
using VRRehab.Interactive.Task16;
using VRRehab.Persistence;

namespace VRRehab.EditorAutoSetup
{
    /// <summary>
    /// Builder e validador automatizado para a TASK-16:
    /// Persistência de Progresso + Estante de Troféus Físicos Desbloqueáveis no Menu + Coroa Interativa do Drone Pet.
    /// Constrói materiais, Prefabs de troféus, estante de exibição na mesa e executa o ciclo de Visual QA.
    /// </summary>
    public static class Task16_SceneAndPrefabBuilder
    {
        private const string ModelsDir = "Assets/Models/Generated/Task16";
        private const string PrefabsDir = "Assets/Prefabs/Interactive";
        private const string AudioDir = "Assets/Audio/Generated/Task16";
        private const string MenuScenePath = "Assets/Scenes/Menu_Principal.unity";
        private const string OutVisualQADir = ".agents/visual_qa";

        private const string ModelApplePath = "Assets/Models/Generated/Task16/Trophy_GoldenApple.fbx";
        private const string ModelBonsaiPath = "Assets/Models/Generated/Task16/Trophy_LuminousBonsai.fbx";
        private const string ModelStarCupPath = "Assets/Models/Generated/Task16/Trophy_StarCup.fbx";
        private const string ModelCrownPath = "Assets/Models/Generated/Task16/Trophy_DroneCrownHat.fbx";
        private const string ModelStandPath = "Assets/Models/Generated/Task16/Trophy_DisplayStand.fbx";

        private const string SfxFanfarePath = "Assets/Audio/Generated/Task16/trophy_unlock_fanfare.wav";
        private const string SfxPickupPath = "Assets/Audio/Generated/Task16/trophy_pickup_shimmer.wav";
        private const string SfxCrownEquipPath = "Assets/Audio/Generated/Task16/crown_equip_royal.wav";

        [MenuItem("VRRehab/AutoBuild/Build and Verify TASK-16")]
        public static void BuildAndVerify()
        {
            Debug.Log("[TASK-16] Iniciando automacao completa de Persistencia, Trofeus e Coroa do Drone...");

            // 1. Reimporta modelos e áudios
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(ModelApplePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(ModelBonsaiPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(ModelStarCupPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(ModelCrownPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(ModelStandPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxFanfarePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxPickupPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxCrownEquipPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            // 2. Garante pastas e materiais
            EnsureDirectories();
            EnsureMaterials();

            // 3. Constrói Prefabs dos Troféus e da Estante
            GameObject prefabApple = BuildGoldenApplePrefab();
            GameObject prefabBonsai = BuildLuminousBonsaiPrefab();
            GameObject prefabStarCup = BuildStarCupPrefab();
            GameObject prefabCrown = BuildDroneCrownPrefab();
            GameObject prefabStand = BuildDisplayStandPrefab(prefabApple, prefabBonsai, prefabStarCup, prefabCrown);

            // 4. Implanta na cena Menu_Principal
            SetupMenuPrincipalScene(prefabStand, prefabCrown);

            // 5. Visual QA & Captura de Screenshots
            CaptureVisualQA();

            Debug.Log("[TASK-16] Automacao concluida com 100% de sucesso!");
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(PrefabsDir)) Directory.CreateDirectory(PrefabsDir);
            if (!Directory.Exists(OutVisualQADir)) Directory.CreateDirectory(OutVisualQADir);
        }

        private static void EnsureMaterials()
        {
            // Madeira nobre polida
            EnsureMaterial($"{ModelsDir}/M_Trophy_WoodDark.mat", "M_Trophy_WoodDark", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Trophy_WoodDark", new Color(0.18f, 0.10f, 0.05f), 0.05f, 0.65f));

            // Ouro polido reluzente
            EnsureMaterial($"{ModelsDir}/M_Trophy_GoldPolished.mat", "M_Trophy_GoldPolished", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_Trophy_GoldPolished", new Color(1.0f, 0.82f, 0.16f), new Color(0.75f, 0.58f, 0.10f), 0.8f));

            // Plaqueta de latão gravado
            EnsureMaterial($"{ModelsDir}/M_Trophy_PlaqueBrass.mat", "M_Trophy_PlaqueBrass", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Trophy_PlaqueBrass", new Color(0.88f, 0.72f, 0.28f), 0.85f, 0.75f));

            // Vaso de cerâmica jade
            EnsureMaterial($"{ModelsDir}/M_Trophy_JadePot.mat", "M_Trophy_JadePot", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Trophy_JadePot", new Color(0.12f, 0.48f, 0.38f), 0.15f, 0.80f));

            // Tronco antigo do bonsai
            EnsureMaterial($"{ModelsDir}/M_Trophy_BonsaiTrunk.mat", "M_Trophy_BonsaiTrunk", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Trophy_BonsaiTrunk", new Color(0.28f, 0.16f, 0.08f), 0.05f, 0.35f));

            // Cristal bioluminescente menta/ciano
            EnsureMaterial($"{ModelsDir}/M_Trophy_CrystalBiolum.mat", "M_Trophy_CrystalBiolum", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_Trophy_CrystalBiolum", new Color(0.15f, 0.95f, 0.85f), new Color(0.10f, 0.95f, 0.82f), 3.5f));

            // Base de mármore negro
            EnsureMaterial($"{ModelsDir}/M_Trophy_MarbleBase.mat", "M_Trophy_MarbleBase", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Trophy_MarbleBase", new Color(0.10f, 0.10f, 0.12f), 0.20f, 0.70f));

            // Emblema estrela cintilante
            EnsureMaterial($"{ModelsDir}/M_Trophy_StarEmblem.mat", "M_Trophy_StarEmblem", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_Trophy_StarEmblem", new Color(1.0f, 0.95f, 0.40f), new Color(1.0f, 0.90f, 0.35f), 2.2f));

            // Veludo vermelho real da coroa
            EnsureMaterial($"{ModelsDir}/M_Trophy_RoyalVelvet.mat", "M_Trophy_RoyalVelvet", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Trophy_RoyalVelvet", new Color(0.75f, 0.05f, 0.12f), 0.08f, 0.20f));

            // Gema Rubi cintilante
            EnsureMaterial($"{ModelsDir}/M_Trophy_GemRuby.mat", "M_Trophy_GemRuby", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_Trophy_GemRuby", new Color(0.95f, 0.08f, 0.22f), new Color(0.90f, 0.05f, 0.20f), 2.5f));

            // Gema Safira imperial
            EnsureMaterial($"{ModelsDir}/M_Trophy_GemSapphire.mat", "M_Trophy_GemSapphire", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_Trophy_GemSapphire", new Color(0.08f, 0.35f, 0.95f), new Color(0.06f, 0.30f, 0.92f), 2.5f));

            // Veludo azul marinho dos berços
            EnsureMaterial($"{ModelsDir}/M_Trophy_DisplayVelvet.mat", "M_Trophy_DisplayVelvet", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Trophy_DisplayVelvet", new Color(0.05f, 0.12f, 0.28f), 0.05f, 0.25f));

            AssetDatabase.SaveAssets();
        }

        private static void EnsureMaterial(string path, string name, Func<Material> creator)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = creator();
                AssetDatabase.CreateAsset(mat, path);
            }
        }

        private static GameObject BuildGoldenApplePrefab()
        {
            string prefabPath = $"{PrefabsDir}/Trophy_GoldenApple.prefab";
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelApplePath);
            if (fbx == null) throw new Exception($"FBX nao encontrado: {ModelApplePath}");

            GameObject root = new GameObject("Trophy_GoldenApple");
            GameObject visual = UnityEngine.Object.Instantiate(fbx, root.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            ApplyMaterialsToHierarchy(visual, new Dictionary<string, string> {
                { "Pedestal", $"{ModelsDir}/M_Trophy_WoodDark.mat" },
                { "Plaque", $"{ModelsDir}/M_Trophy_PlaqueBrass.mat" },
                { "GoldenApple", $"{ModelsDir}/M_Trophy_GoldPolished.mat" }
            });

            // Colisor & Rigidbody ergonômico
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.085f, 0f);
            col.size = new Vector3(0.10f, 0.17f, 0.10f);

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.8f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // XRI Grab
            var grab = root.AddComponent<ErgonomicGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwVelocityScale = 1.35f;

            var highlight = root.AddComponent<InteractableHighlightFeedback>();

            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefabAsset;
        }

        private static GameObject BuildLuminousBonsaiPrefab()
        {
            string prefabPath = $"{PrefabsDir}/Trophy_LuminousBonsai.prefab";
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelBonsaiPath);
            if (fbx == null) throw new Exception($"FBX nao encontrado: {ModelBonsaiPath}");

            GameObject root = new GameObject("Trophy_LuminousBonsai");
            GameObject visual = UnityEngine.Object.Instantiate(fbx, root.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            ApplyMaterialsToHierarchy(visual, new Dictionary<string, string> {
                { "JadePot", $"{ModelsDir}/M_Trophy_JadePot.mat" },
                { "BonsaiTrunk", $"{ModelsDir}/M_Trophy_BonsaiTrunk.mat" },
                { "CrystalLeaves", $"{ModelsDir}/M_Trophy_CrystalBiolum.mat" }
            });

            BoxCollider col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.095f, 0f);
            col.size = new Vector3(0.12f, 0.19f, 0.12f);

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.9f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var grab = root.AddComponent<ErgonomicGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwVelocityScale = 1.35f;

            var highlight = root.AddComponent<InteractableHighlightFeedback>();

            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefabAsset;
        }

        private static GameObject BuildStarCupPrefab()
        {
            string prefabPath = $"{PrefabsDir}/Trophy_StarCup.prefab";
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelStarCupPath);
            if (fbx == null) throw new Exception($"FBX nao encontrado: {ModelStarCupPath}");

            GameObject root = new GameObject("Trophy_StarCup");
            GameObject visual = UnityEngine.Object.Instantiate(fbx, root.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            ApplyMaterialsToHierarchy(visual, new Dictionary<string, string> {
                { "MarbleBase", $"{ModelsDir}/M_Trophy_MarbleBase.mat" },
                { "GoldenCup", $"{ModelsDir}/M_Trophy_GoldPolished.mat" },
                { "StarEmblem", $"{ModelsDir}/M_Trophy_StarEmblem.mat" }
            });

            BoxCollider col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.105f, 0f);
            col.size = new Vector3(0.14f, 0.21f, 0.11f);

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = 1.1f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var grab = root.AddComponent<ErgonomicGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwVelocityScale = 1.35f;

            var highlight = root.AddComponent<InteractableHighlightFeedback>();

            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefabAsset;
        }

        private static GameObject BuildDroneCrownPrefab()
        {
            string prefabPath = $"{PrefabsDir}/Trophy_DroneCrownHat.prefab";
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelCrownPath);
            if (fbx == null) throw new Exception($"FBX nao encontrado: {ModelCrownPath}");

            GameObject root = new GameObject("Trophy_DroneCrownHat");
            GameObject visual = UnityEngine.Object.Instantiate(fbx, root.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            ApplyMaterialsToHierarchy(visual, new Dictionary<string, string> {
                { "VelvetDome", $"{ModelsDir}/M_Trophy_RoyalVelvet.mat" },
                { "CrownGoldBand", $"{ModelsDir}/M_Trophy_GoldPolished.mat" },
                { "CrownRubies", $"{ModelsDir}/M_Trophy_GemRuby.mat" },
                { "CrownSapphires", $"{ModelsDir}/M_Trophy_GemSapphire.mat" }
            });

            // Colisor da coroa (tamanho para cobrir o aro e veludo)
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.05f, 0f);
            col.size = new Vector3(0.16f, 0.10f, 0.16f);

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var grab = root.AddComponent<ErgonomicGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwVelocityScale = 1.25f;

            var highlight = root.AddComponent<InteractableHighlightFeedback>();

            // Script vestível da coroa
            var crownWearable = root.AddComponent<DroneCrownWearable>();
            crownWearable.sfxCrownEquip = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxCrownEquipPath);

            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefabAsset;
        }

        private static GameObject BuildDisplayStandPrefab(GameObject pApple, GameObject pBonsai, GameObject pCup, GameObject pCrown)
        {
            string prefabPath = $"{PrefabsDir}/TrophyDisplayStand.prefab";
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelStandPath);
            if (fbx == null) throw new Exception($"FBX nao encontrado: {ModelStandPath}");

            GameObject root = new GameObject("TrophyDisplayStand");
            GameObject visual = UnityEngine.Object.Instantiate(fbx, root.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            ApplyMaterialsToHierarchy(visual, new Dictionary<string, string> {
                { "DisplayPlinth", $"{ModelsDir}/M_Trophy_WoodDark.mat" },
                { "VelvetPads", $"{ModelsDir}/M_Trophy_DisplayVelvet.mat" },
                { "DisplayPlaques", $"{ModelsDir}/M_Trophy_PlaqueBrass.mat" }
            });

            // Colisor da base
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.0225f, 0f);
            col.size = new Vector3(0.52f, 0.045f, 0.14f);

            // TrophyShelfManager
            TrophyShelfManager shelf = root.AddComponent<TrophyShelfManager>();
            shelf.sfxUnlockFanfare = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxFanfarePath);
            shelf.sfxPickupShimmer = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxPickupPath);

            // Cria os 4 slots com textos 3D e instâncias filhas
            float[] slotXs = new float[] { -0.18f, -0.06f, 0.06f, 0.18f };
            string[] trophyIds = new string[] {
                RehabProgressionSaveSystem.TrophyIdGoldenApple,
                RehabProgressionSaveSystem.TrophyIdLuminousBonsai,
                RehabProgressionSaveSystem.TrophyIdStarCup,
                RehabProgressionSaveSystem.TrophyIdDroneCrown
            };
            string[] titles = new string[] {
                "Maca Dourada",
                "Bonsai Radiante",
                "Taca Estelar",
                "Coroa do Drone"
            };
            string[] descs = new string[] {
                "Colheita de Ouro - Macas colhidas na floresta com destreza motora.",
                "Guardiao da Floresta - Plantio de sementes magicas e cogumelos.",
                "Campeao Arcade - Cestas convertidas e moedas de energia recolhidas.",
                "Parceria Real - Afeto, carinho e cumplicidade com o Drone Pet."
            };
            GameObject[] prefabs = new GameObject[] { pApple, pBonsai, pCup, pCrown };

            for (int i = 0; i < 4; i++)
            {
                float sx = slotXs[i];
                GameObject anchor = new GameObject($"SlotAnchor_{i}_{trophyIds[i]}");
                anchor.transform.SetParent(root.transform, false);
                anchor.transform.localPosition = new Vector3(sx, 0.045f, 0.008f);

                // Instancia o troféu no slot
                GameObject trophyInst = UnityEngine.Object.Instantiate(prefabs[i], anchor.transform);
                trophyInst.name = $"TrophyItem_{trophyIds[i]}";
                trophyInst.transform.localPosition = Vector3.zero;
                trophyInst.transform.localRotation = Quaternion.identity;

                // Texto 3D na plaqueta
                GameObject txtObj = new GameObject($"Label_{trophyIds[i]}");
                txtObj.transform.SetParent(root.transform, false);
                txtObj.transform.localPosition = new Vector3(sx, 0.024f, -0.071f);
                txtObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                TextMeshPro tmp = txtObj.AddComponent<TextMeshPro>();
                tmp.text = titles[i];
                tmp.fontSize = 1.0f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = new Color(0.95f, 0.85f, 0.35f);
                tmp.rectTransform.sizeDelta = new Vector2(0.12f, 0.04f);

                var slotEntry = new TrophyShelfManager.TrophySlot
                {
                    trophyId = trophyIds[i],
                    title = titles[i],
                    description = descs[i],
                    trophyObject = trophyInst,
                    slotAnchor = anchor.transform,
                    plaqueText = tmp
                };
                shelf.slots.Add(slotEntry);
            }

            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefabAsset;
        }

        private static void ApplyMaterialsToHierarchy(GameObject target, Dictionary<string, string> matMap)
        {
            foreach (var kvp in matMap)
            {
                Transform t = target.transform.Find(kvp.Key);
                if (t != null)
                {
                    Renderer mr = t.GetComponent<Renderer>();
                    if (mr != null)
                    {
                        Material mat = AssetDatabase.LoadAssetAtPath<Material>(kvp.Value);
                        if (mat != null) mr.sharedMaterial = mat;
                    }
                }
            }
        }

        private static void SetupMenuPrincipalScene(GameObject prefabStand, GameObject prefabCrown)
        {
            if (EditorSceneManager.GetActiveScene().path != MenuScenePath)
            {
                EditorSceneManager.SaveOpenScenes();
                EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            }

            // 1. Garante RehabProgressionSaveSystem na cena
            RehabProgressionSaveSystem saveSys = UnityEngine.Object.FindFirstObjectByType<RehabProgressionSaveSystem>();
            if (saveSys == null)
            {
                GameObject saveGo = new GameObject("[VRRehab] ProgressionSaveSystem");
                saveSys = saveGo.AddComponent<RehabProgressionSaveSystem>();
                saveSys.unlockAllForDemo = true;
            }

            // 2. Limpa instâncias anteriores da estante
            string[] cleanups = new string[] {
                "TrophyDisplayStand_Instance",
                "TrophyDisplayStand(Clone)"
            };
            foreach (string name in cleanups)
            {
                GameObject old = GameObject.Find(name);
                if (old != null) UnityEngine.Object.DestroyImmediate(old);
            }

            // 3. Instancia a Estante de Troféus sobre a mesa principal
            // Mesa está em x: 0.63, y: 0.0, z: -0.02. Tampo da mesa em y = 0.952m.
            // Posicionamento no lado livre da mesa (z = -0.38f)
            Vector3 standPos = new Vector3(0.68f, 0.952f, -0.38f);
            Quaternion standRot = Quaternion.Euler(0f, 75f, 0f);

            GameObject standInst = (GameObject)PrefabUtility.InstantiatePrefab(prefabStand);
            standInst.name = "TrophyDisplayStand_Instance";
            standInst.transform.position = standPos;
            standInst.transform.rotation = standRot;
            PrefabUtility.UnpackPrefabInstance(standInst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            // 4. Configura o Drone Pet com DroneHeadSocket
            GameObject droneObj = GameObject.Find("[IC] Drone_Guia_IA_ExoBot");
            if (droneObj != null)
            {
                Transform socketTr = droneObj.transform.Find("DroneHeadSocket");
                if (socketTr == null)
                {
                    GameObject socketGo = new GameObject("DroneHeadSocket");
                    socketGo.transform.SetParent(droneObj.transform, false);
                    socketGo.transform.localPosition = new Vector3(0f, 0.225f, 0f);
                    socketGo.transform.localRotation = Quaternion.identity;
                    socketTr = socketGo.transform;

                    // Trigger collider do socket
                    SphereCollider sCol = socketGo.AddComponent<SphereCollider>();
                    sCol.isTrigger = true;
                    sCol.radius = 0.16f;

                    // Interactor XRI
                    var socketInteractor = socketGo.AddComponent<XRSocketInteractor>();
                    socketInteractor.socketActive = true;
                }

                // Partículas de coroação
                Transform psTr = socketTr.Find("ParticleSystem_Coronation");
                if (psTr == null)
                {
                    GameObject psGo = new GameObject("ParticleSystem_Coronation");
                    psGo.transform.SetParent(socketTr, false);
                    psGo.transform.localPosition = Vector3.zero;
                    ParticleSystem ps = psGo.AddComponent<ParticleSystem>();
                    ConfigureCoronationParticles(ps);
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[TASK-16] Cena Menu_Principal configurada e salva com sucesso!");
        }

        private static void ConfigureCoronationParticles(ParticleSystem ps)
        {
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1.6f;
            main.startLifetime = 1.2f;
            main.startSpeed = 1.2f;
            main.startSize = 0.05f;
            main.startColor = new Color(1.0f, 0.88f, 0.25f, 0.95f);

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.burstCount = 1;
            emission.SetBurst(0, new ParticleSystem.Burst(0.0f, 35));

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;
        }

        private static void CaptureVisualQA()
        {
            string baseDir = Path.GetFullPath(OutVisualQADir);
            if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);

            // 1. CAPTURA 1: TASK-16_Trophy_Shelf_Closeup.png
            // Foco fechado na estante de troféus na mesa (ângulo limpo sem obstrução do braço da luminária)
            string pathShelf = Path.Combine(baseDir, "TASK-16_Trophy_Shelf_Closeup.png");
            Vector3 shelfCamPos = new Vector3(1.08f, 1.15f, -0.56f);
            Vector3 shelfLookTarget = new Vector3(0.68f, 1.02f, -0.38f);
            VisualQACaptureEditor.CaptureCameraView(pathShelf, shelfCamPos, shelfLookTarget, 1920, 1080);
            Debug.Log($"[TASK-16 Visual QA] Imagem 1 capturada: {pathShelf}");

            // 2. CAPTURA 2: TASK-16_Drone_Crown_Equipped.png
            // Enquadramento focado no Drone Pet vestindo a coroa imperial com visão perfeita da coroa
            GameObject droneObj = GameObject.Find("[IC] Drone_Guia_IA_ExoBot");
            Transform droneSocket = droneObj != null ? droneObj.transform.Find("DroneHeadSocket") : null;

            GameObject prefabCrown = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsDir}/Trophy_DroneCrownHat.prefab");
            GameObject crownPreview = null;
            if (prefabCrown != null && droneObj != null)
            {
                crownPreview = UnityEngine.Object.Instantiate(prefabCrown, droneSocket != null ? droneSocket : droneObj.transform);
                crownPreview.name = "Crown_Preview_Wearable";
                crownPreview.transform.localPosition = (droneSocket != null) ? new Vector3(0f, -0.015f, 0f) : new Vector3(0f, 0.21f, 0f);
                crownPreview.transform.localRotation = Quaternion.identity;
                crownPreview.transform.localScale = Vector3.one;

                Rigidbody cRb = crownPreview.GetComponent<Rigidbody>();
                if (cRb != null) UnityEngine.Object.DestroyImmediate(cRb);
                Collider col = crownPreview.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);
            }

            // Ativa expressão alegre no visor do Drone
            DroneAffectiveSystem affective = droneObj?.GetComponent<DroneAffectiveSystem>();
            if (affective != null)
            {
                affective.SetExpression(DroneLEDExpression.Happy);
            }

            Vector3 dronePos = droneObj != null ? droneObj.transform.position : new Vector3(2.20f, 1.45f, 1.20f);
            Vector3 droneForward = droneObj != null ? droneObj.transform.forward : Vector3.back;
            // Câmera posicionada para enquadrar perfeitamente a coroa real reluzente no topo, visor LED alegre e propulsores
            Vector3 crownCamPos = dronePos + (droneForward * 0.88f) + (Vector3.up * 0.20f);
            Vector3 crownLookTarget = dronePos + (Vector3.up * 0.14f);

            string pathCrown = Path.Combine(baseDir, "TASK-16_Drone_Crown_Equipped.png");
            VisualQACaptureEditor.CaptureCameraView(pathCrown, crownCamPos, crownLookTarget, 1920, 1080);
            Debug.Log($"[TASK-16 Visual QA] Imagem 2 capturada: {pathCrown}");

            if (crownPreview != null)
            {
                UnityEngine.Object.DestroyImmediate(crownPreview);
            }

            // 3. CAPTURA 3: TASK-16_Menu_Hub_Master_Panorama.png
            // Panorama geral de toda a sala mostrando todas as mecânicas em harmonia
            string pathPanorama = Path.Combine(baseDir, "TASK-16_Menu_Hub_Master_Panorama.png");
            Vector3 panoCamPos = new Vector3(3.90f, 1.68f, 2.05f);
            Vector3 panoLookTarget = new Vector3(0.95f, 1.15f, 0.25f);
            VisualQACaptureEditor.CaptureCameraView(pathPanorama, panoCamPos, panoLookTarget, 1920, 1080);
            Debug.Log($"[TASK-16 Visual QA] Imagem 3 capturada: {pathPanorama}");

            if (affective != null)
            {
                affective.SetExpression(DroneLEDExpression.Normal);
            }

            EditorSceneManager.SaveOpenScenes();
        }
    }
}
