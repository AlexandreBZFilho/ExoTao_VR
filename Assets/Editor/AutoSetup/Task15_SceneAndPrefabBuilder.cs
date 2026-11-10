using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;
using VRRehab.Core;
using VRRehab.EditorTools;
using VRRehab.Interactive.Task15;

namespace VRRehab.EditorAutoSetup
{
    /// <summary>
    /// Builder e validador automatizado para a TASK-15:
    /// Sistema de Missões no Visor do Drone + Totens de Checkpoint com Alavanca e Baú (SessionQuestAndCheckpointManager)
    /// </summary>
    public static class Task15_SceneAndPrefabBuilder
    {
        private const string ModelsDir = "Assets/Models/Generated/Task15";
        private const string PrefabsDir = "Assets/Prefabs/Interactive";
        private const string AudioDir = "Assets/Audio/Generated/Task15";
        private const string TexturesDir = "Assets/Textures/Generated/Task15";
        private const string ForestScenePath = "Assets/Scenes/Floresta_e_Rio.unity";
        private const string OutVisualQADir = ".agents/visual_qa";

        private const string TotemModelPath = "Assets/Models/Generated/Task15/CheckpointRewardTotem.fbx";
        private const string SfxLeverPullPath = "Assets/Audio/Generated/Task15/totem_lever_pull.wav";
        private const string SfxChestCreakPath = "Assets/Audio/Generated/Task15/chest_open_creak.wav";
        private const string SfxQuestJinglePath = "Assets/Audio/Generated/Task15/quest_complete_jingle.wav";
        private const string SfxCheckpointHumPath = "Assets/Audio/Generated/Task15/checkpoint_activate_hum.wav";

        [MenuItem("VRRehab/AutoBuild/Build and Verify TASK-15")]
        public static void BuildAndVerify()
        {
            Debug.Log("[TASK-15] Iniciando automacao completa do Sistema de Missoes e Totem de Checkpoint...");

            // 1. Reimporta modelos e áudios
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(TotemModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxLeverPullPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxChestCreakPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxQuestJinglePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxCheckpointHumPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            // 2. Garante pastas, texturas e materiais
            EnsureDirectories();
            GenerateUITextures();
            EnsureMaterials();

            // 3. Constrói Prefab do Totem
            GameObject totemPrefab = BuildCheckpointTotemPrefab();

            // 4. Configura cena da Floresta com Totem e HUD do Drone
            SetupForestScene(totemPrefab);

            // 5. Captura Visual QA
            CaptureVisualQA();

            Debug.Log("[TASK-15] Conclusao com sucesso de todos os estagios da TASK-15!");
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Models/Generated/Task15"))
            {
                AssetDatabase.CreateFolder("Assets/Models/Generated", "Task15");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Interactive"))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "Interactive");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Audio/Generated/Task15"))
            {
                AssetDatabase.CreateFolder("Assets/Audio/Generated", "Task15");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Textures/Generated"))
            {
                AssetDatabase.CreateFolder("Assets/Textures", "Generated");
            }
            if (!AssetDatabase.IsValidFolder(TexturesDir))
            {
                AssetDatabase.CreateFolder("Assets/Textures/Generated", "Task15");
            }
            if (!Directory.Exists(OutVisualQADir))
            {
                Directory.CreateDirectory(OutVisualQADir);
            }
        }

        private static void GenerateUITextures()
        {
            string barTexPath = $"{TexturesDir}/UI_ProgressBar_White.png";
            if (!File.Exists(barTexPath))
            {
                Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                Color[] colors = new Color[32 * 32];
                for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
                tex.SetPixels(colors);
                tex.Apply();
                File.WriteAllBytes(barTexPath, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(barTexPath, ImportAssetOptions.ForceSynchronousImport);

                TextureImporter importer = AssetImporter.GetAtPath(barTexPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.SaveAndReimport();
                }
            }

            string panelTexPath = $"{TexturesDir}/UI_QuestPanel_Bg.png";
            if (!File.Exists(panelTexPath))
            {
                int w = 256, h = 256;
                Texture2D texPanel = new Texture2D(w, h, TextureFormat.RGBA32, false);
                Color cBg = new Color(0.04f, 0.09f, 0.16f, 0.92f);
                Color cBorder = new Color(0.0f, 0.85f, 0.95f, 0.95f);
                int borderThick = 4;

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (x < borderThick || x >= w - borderThick || y < borderThick || y >= h - borderThick)
                            texPanel.SetPixel(x, y, cBorder);
                        else
                            texPanel.SetPixel(x, y, cBg);
                    }
                }
                texPanel.Apply();
                File.WriteAllBytes(panelTexPath, texPanel.EncodeToPNG());
                AssetDatabase.ImportAsset(panelTexPath, ImportAssetOptions.ForceSynchronousImport);

                TextureImporter importer = AssetImporter.GetAtPath(panelTexPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.SaveAndReimport();
                }
            }
        }

        public static void EnsureMaterials()
        {
            EnsureMaterial($"{ModelsDir}/M_Totem_Stone.mat", "M_Totem_Stone", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Totem_Stone", new Color(0.36f, 0.36f, 0.38f), 0.05f, 0.88f);
            });

            EnsureMaterial($"{ModelsDir}/M_Totem_Wood.mat", "M_Totem_Wood", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Totem_Wood", new Color(0.30f, 0.17f, 0.09f), 0.0f, 0.72f);
            });

            EnsureMaterial($"{ModelsDir}/M_Totem_Iron.mat", "M_Totem_Iron", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Totem_Iron", new Color(0.16f, 0.17f, 0.19f), 0.88f, 0.38f);
            });

            EnsureMaterial($"{ModelsDir}/M_Totem_Rune_Cyan.mat", "M_Totem_Rune_Cyan", () =>
            {
                return MaterialShaderHelper.CreateEmissiveMaterial("M_Totem_Rune_Cyan", new Color(0.02f, 0.92f, 0.98f), new Color(0.02f, 0.92f, 0.98f), 3.5f);
            });

            EnsureMaterial($"{ModelsDir}/M_Totem_Gold_Treasure.mat", "M_Totem_Gold_Treasure", () =>
            {
                return MaterialShaderHelper.CreateEmissiveMaterial("M_Totem_Gold_Treasure", new Color(1.0f, 0.82f, 0.14f), new Color(0.65f, 0.48f, 0.10f), 2.2f);
            });

            EnsureMaterial($"{ModelsDir}/M_Totem_Lever_Knob.mat", "M_Totem_Lever_Knob", () =>
            {
                return MaterialShaderHelper.CreateLitMaterial("M_Totem_Lever_Knob", new Color(0.95f, 0.22f, 0.12f), 0.2f, 0.30f);
            });
        }

        private static void EnsureMaterial(string path, string name, Func<Material> creator)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = creator();
                AssetDatabase.CreateAsset(mat, path);
                Debug.Log($"[TASK-15] Material criado: {path}");
            }
        }

        #region Prefab Building
        public static GameObject BuildCheckpointTotemPrefab()
        {
            string prefabPath = $"{PrefabsDir}/CheckpointRewardTotem.prefab";
            if (File.Exists(prefabPath))
            {
                AssetDatabase.DeleteAsset(prefabPath);
            }

            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TotemModelPath);
            if (fbxAsset == null)
            {
                Debug.LogError($"[TASK-15] FBX CheckpointRewardTotem não encontrado em {TotemModelPath}");
                return null;
            }

            GameObject root = new GameObject("CheckpointRewardTotem");

            GameObject visualMesh = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset, root.transform);
            visualMesh.name = "Totem_VisualModel";
            visualMesh.transform.localPosition = Vector3.zero;
            visualMesh.transform.localRotation = Quaternion.identity;

            // Atribui materiais a todas as partes
            AssignMaterial(visualMesh, "Totem_StoneBase", $"{ModelsDir}/M_Totem_Stone.mat");
            AssignMaterial(visualMesh, "Totem_WoodPillar", $"{ModelsDir}/M_Totem_Wood.mat");
            AssignMaterial(visualMesh, "Totem_RuneInlays", $"{ModelsDir}/M_Totem_Rune_Cyan.mat");
            AssignMaterial(visualMesh, "Totem_LeverMount", $"{ModelsDir}/M_Totem_Iron.mat");
            AssignMaterial(visualMesh, "Totem_LeverHandle", $"{ModelsDir}/M_Totem_Lever_Knob.mat");
            AssignMaterial(visualMesh, "Totem_ChestBase", $"{ModelsDir}/M_Totem_Wood.mat");
            AssignMaterial(visualMesh, "Totem_ChestInteriorGold", $"{ModelsDir}/M_Totem_Gold_Treasure.mat");
            AssignMaterial(visualMesh, "Totem_ChestLid", $"{ModelsDir}/M_Totem_Wood.mat");

            // Colisor sólido do totem na base e tronco
            BoxCollider colBase = root.AddComponent<BoxCollider>();
            colBase.center = new Vector3(0f, 0.65f, 0f);
            colBase.size = new Vector3(0.55f, 1.30f, 0.55f);

            // Localiza os Pivôs no modelo instanciado
            Transform leverPivot = FindDeepChild(visualMesh.transform, "Totem_LeverPivot");
            Transform lidPivot = FindDeepChild(visualMesh.transform, "Totem_ChestLidPivot");
            Transform handleTransform = FindDeepChild(visualMesh.transform, "Totem_LeverHandle");

            // Colisor de interação e XRSimpleInteractable na alavanca
            if (handleTransform != null)
            {
                SphereCollider knobCol = handleTransform.gameObject.AddComponent<SphereCollider>();
                knobCol.center = new Vector3(0f, 0.093f, 0.20f);
                knobCol.radius = 0.09f;
                knobCol.isTrigger = false;

                XRSimpleInteractable simpleInteractable = handleTransform.gameObject.AddComponent<XRSimpleInteractable>();
            }

            // Luz de Beacon ascendente no topo do baú
            GameObject beaconGo = new GameObject("Totem_BeaconLight");
            beaconGo.transform.SetParent(root.transform, false);
            beaconGo.transform.localPosition = new Vector3(0f, 1.25f, 0f);

            Light beaconLight = beaconGo.AddComponent<Light>();
            beaconLight.type = LightType.Point;
            beaconLight.color = new Color(1.0f, 0.85f, 0.40f);
            beaconLight.range = 6.0f;
            beaconLight.intensity = 0f; // Começa apagado
            beaconLight.shadows = LightShadows.None;

            // Partículas de celebração / confetes
            GameObject psGo = new GameObject("ChestCelebration_FX");
            psGo.transform.SetParent(root.transform, false);
            psGo.transform.localPosition = new Vector3(0f, 1.22f, 0f);
            ParticleSystem ps = psGo.AddComponent<ParticleSystem>();
            ConfigureTotemCelebrationParticles(ps);

            // Script InteractiveCheckpointTotem
            AudioClip sfxLever = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxLeverPullPath);
            AudioClip sfxChest = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxChestCreakPath);
            AudioClip sfxHum = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxCheckpointHumPath);

            InteractiveCheckpointTotem totemScript = root.AddComponent<InteractiveCheckpointTotem>();
            totemScript.Configure(leverPivot, lidPivot, beaconLight, ps, sfxLever, sfxChest, sfxHum);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log($"[TASK-15] Prefab CheckpointRewardTotem salvo em {prefabPath}");
            return prefab;
        }

        private static void AssignMaterial(GameObject root, string childName, string materialPath)
        {
            Transform child = FindDeepChild(root.transform, childName);
            if (child != null)
            {
                Renderer r = child.GetComponent<Renderer>();
                if (r != null)
                {
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (mat != null)
                    {
                        r.sharedMaterial = mat;
                    }
                }
            }
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeepChild(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static void ConfigureTotemCelebrationParticles(ParticleSystem ps)
        {
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 2.0f;
            main.startLifetime = 1.6f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 3.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            main.gravityModifier = 0.45f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 35, 50) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.15f;

            var colOverLifetime = ps.colorOverLifetime;
            colOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0f), new GradientColorKey(new Color(0.2f, 0.95f, 1f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colOverLifetime.color = grad;
        }
        #endregion

        #region Scene Setup
        public static void SetupForestScene(GameObject totemPrefab)
        {
            EditorSceneManager.OpenScene(ForestScenePath, OpenSceneMode.Single);

            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                Debug.LogError("[TASK-15] Terrain não encontrado na cena da Floresta!");
                return;
            }

            // ==================================================================
            // 1. Instanciação e Posicionamento do Totem na Margem da Trilha
            // ==================================================================
            string oldTotemTag = "[TASK-15] CheckpointRewardTotem";
            var oldObj = GameObject.Find(oldTotemTag);
            if (oldObj != null) UnityEngine.Object.DestroyImmediate(oldObj);

            // Coordenadas na margem da trilha (gramado limpo e plano próximo à árvore e ponte):
            Vector3 totemPosXZ = new Vector3(43.50f, 0f, 66.20f);
            float groundY = terrain.SampleHeight(totemPosXZ) + terrain.transform.position.y;
            // Ancoragem sólida da base de pedra no solo
            Vector3 totemWorldPos = new Vector3(totemPosXZ.x, groundY - 0.05f, totemPosXZ.z);

            GameObject totemInst = (GameObject)PrefabUtility.InstantiatePrefab(totemPrefab);
            totemInst.name = oldTotemTag;
            totemInst.transform.position = totemWorldPos;
            // Rotação para facear diagonalmente o paciente que avança pela trilha
            totemInst.transform.rotation = Quaternion.Euler(0f, 135f, 0f);

            // ==================================================================
            // 2. Acoplamento da Interface de Missões no Drone Pet
            // ==================================================================
            GameObject droneObj = GameObject.Find("[IC] Drone_Guia_IA_ExoBot");
            if (droneObj == null)
            {
                Debug.LogError("[TASK-15] Drone Pet '[IC] Drone_Guia_IA_ExoBot' não encontrado na cena!");
                return;
            }

            // Adiciona ou obtém SessionQuestManager no Drone
            SessionQuestManager questMgr = droneObj.GetComponent<SessionQuestManager>();
            if (questMgr == null)
            {
                questMgr = droneObj.AddComponent<SessionQuestManager>();
            }

            // Monta ou atualiza o painel holográfico de missões no Canvas do Drone
            SetupDroneQuestHologramPanel(droneObj.transform, questMgr);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[TASK-15] Cena Floresta_e_Rio configurada com Totem de Checkpoint e Painel de Missoes no Drone!");
        }

        private static void SetupDroneQuestHologramPanel(Transform droneTransform, SessionQuestManager questMgr)
        {
            Transform canvasTransform = droneTransform.Find("Canvas_Holograma_Clinico");
            if (canvasTransform == null)
            {
                Debug.LogWarning("[TASK-15] Canvas_Holograma_Clinico não encontrado no Drone; criando novo...");
                DroneGuiaIA droneIA = droneTransform.GetComponent<DroneGuiaIA>();
                if (droneIA != null)
                {
                    droneIA.ConstruirHUDHolografico(true);
                }
                canvasTransform = droneTransform.Find("Canvas_Holograma_Clinico");
            }

            if (canvasTransform == null) return;

            // Remove painel antigo se já existir
            Transform oldPanel = canvasTransform.Find("Painel_Missoes_Floresta");
            if (oldPanel != null) UnityEngine.Object.DestroyImmediate(oldPanel.gameObject);

            // Carrega sprites criadas
            Sprite sprPanelBg = AssetDatabase.LoadAssetAtPath<Sprite>($"{TexturesDir}/UI_QuestPanel_Bg.png");
            Sprite sprBarFill = AssetDatabase.LoadAssetAtPath<Sprite>($"{TexturesDir}/UI_ProgressBar_White.png");

            // Cria o Painel de Missões como uma aba lateral adjacente direita (asa holográfica)
            GameObject goPanel = new GameObject("Painel_Missoes_Floresta");
            goPanel.transform.SetParent(canvasTransform, false);

            RectTransform rtPanel = goPanel.AddComponent<RectTransform>();
            rtPanel.anchorMin = new Vector2(1.0f, 0.5f);
            rtPanel.anchorMax = new Vector2(1.0f, 0.5f);
            rtPanel.pivot = new Vector2(0.0f, 0.5f);
            rtPanel.sizeDelta = new Vector2(230f, 195f);
            rtPanel.anchoredPosition = new Vector2(12f, 0f);

            Image imgBg = goPanel.AddComponent<Image>();
            if (sprPanelBg != null) imgBg.sprite = sprPanelBg;
            imgBg.color = new Color(0.04f, 0.10f, 0.18f, 0.94f);

            // Cabeçalho do Painel: "MISSOES DA FLORESTA"
            GameObject goHeader = new GameObject("Txt_Header");
            goHeader.transform.SetParent(goPanel.transform, false);
            RectTransform rtHeader = goHeader.AddComponent<RectTransform>();
            rtHeader.anchorMin = new Vector2(0.05f, 0.82f);
            rtHeader.anchorMax = new Vector2(0.95f, 0.96f);
            rtHeader.offsetMin = Vector2.zero;
            rtHeader.offsetMax = Vector2.zero;

            TextMeshProUGUI txtHeader = goHeader.AddComponent<TextMeshProUGUI>();
            txtHeader.text = "OBJETIVOS DA SESSAO";
            txtHeader.fontSize = 13.5f;
            txtHeader.fontStyle = FontStyles.Bold;
            txtHeader.alignment = TextAlignmentOptions.Center;
            txtHeader.color = new Color(0.0f, 0.95f, 1.0f, 1.0f);

            // Cria as 3 linhas de missões
            SessionQuestManager.QuestUIElements gaitUI = CreateQuestUIRow(goPanel.transform, "Row_Quest_1", 0.58f, "Marcha Ativa", sprBarFill);
            SessionQuestManager.QuestUIElements friendUI = CreateQuestUIRow(goPanel.transform, "Row_Quest_2", 0.32f, "Amigo Floresta", sprBarFill);
            SessionQuestManager.QuestUIElements botanyUI = CreateQuestUIRow(goPanel.transform, "Row_Quest_3", 0.06f, "Magia Botanica", sprBarFill);

            // Vincula ao SessionQuestManager
            AudioClip jingleClip = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxQuestJinglePath);
            questMgr.BindUIElements(gaitUI, friendUI, botanyUI, jingleClip);
        }

        private static SessionQuestManager.QuestUIElements CreateQuestUIRow(Transform parent, string rowName, float anchorY, string defaultTitle, Sprite barSprite)
        {
            GameObject goRow = new GameObject(rowName);
            goRow.transform.SetParent(parent, false);

            RectTransform rtRow = goRow.AddComponent<RectTransform>();
            rtRow.anchorMin = new Vector2(0.06f, anchorY);
            rtRow.anchorMax = new Vector2(0.94f, anchorY + 0.22f);
            rtRow.offsetMin = Vector2.zero;
            rtRow.offsetMax = Vector2.zero;

            // 1. Título da Missão (Topo esquerdo)
            GameObject goTitle = new GameObject("Txt_Title");
            goTitle.transform.SetParent(goRow.transform, false);
            RectTransform rtTitle = goTitle.AddComponent<RectTransform>();
            rtTitle.anchorMin = new Vector2(0f, 0.50f);
            rtTitle.anchorMax = new Vector2(0.68f, 1.0f);
            rtTitle.offsetMin = Vector2.zero;
            rtTitle.offsetMax = Vector2.zero;

            TextMeshProUGUI txtTitle = goTitle.AddComponent<TextMeshProUGUI>();
            txtTitle.text = defaultTitle;
            txtTitle.fontSize = 11.5f;
            txtTitle.fontStyle = FontStyles.Bold;
            txtTitle.alignment = TextAlignmentOptions.Left;
            txtTitle.color = Color.white;

            // 2. Contador / Status (Topo direito: "0/15" ou "[✓]")
            GameObject goStatus = new GameObject("Txt_Status");
            goStatus.transform.SetParent(goRow.transform, false);
            RectTransform rtStatus = goStatus.AddComponent<RectTransform>();
            rtStatus.anchorMin = new Vector2(0.65f, 0.50f);
            rtStatus.anchorMax = new Vector2(1.0f, 1.0f);
            rtStatus.offsetMin = Vector2.zero;
            rtStatus.offsetMax = Vector2.zero;

            TextMeshProUGUI txtStatus = goStatus.AddComponent<TextMeshProUGUI>();
            txtStatus.text = "0/15";
            txtStatus.fontSize = 10.5f;
            txtStatus.alignment = TextAlignmentOptions.Right;
            txtStatus.color = new Color(0.0f, 0.95f, 1.0f, 1.0f);

            // 3. Fundo da Barra de Progresso
            GameObject goBarBg = new GameObject("Img_BarBg");
            goBarBg.transform.SetParent(goRow.transform, false);
            RectTransform rtBarBg = goBarBg.AddComponent<RectTransform>();
            rtBarBg.anchorMin = new Vector2(0f, 0.08f);
            rtBarBg.anchorMax = new Vector2(1f, 0.42f);
            rtBarBg.offsetMin = Vector2.zero;
            rtBarBg.offsetMax = Vector2.zero;

            Image imgBarBg = goBarBg.AddComponent<Image>();
            if (barSprite != null) imgBarBg.sprite = barSprite;
            imgBarBg.color = new Color(0.12f, 0.18f, 0.25f, 0.85f);

            // 4. Fill da Barra de Progresso
            GameObject goBarFill = new GameObject("Img_BarFill");
            goBarFill.transform.SetParent(goBarBg.transform, false);
            RectTransform rtBarFill = goBarFill.AddComponent<RectTransform>();
            rtBarFill.anchorMin = Vector2.zero;
            rtBarFill.anchorMax = Vector2.one;
            rtBarFill.offsetMin = Vector2.zero;
            rtBarFill.offsetMax = Vector2.zero;

            Image imgBarFill = goBarFill.AddComponent<Image>();
            if (barSprite != null) imgBarFill.sprite = barSprite;
            imgBarFill.type = Image.Type.Filled;
            imgBarFill.fillMethod = Image.FillMethod.Horizontal;
            imgBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            imgBarFill.fillAmount = 0.35f; // Valor de preview
            imgBarFill.color = new Color(0.0f, 0.95f, 1.0f, 1.0f);

            var elements = new SessionQuestManager.QuestUIElements
            {
                txtTitle = txtTitle,
                txtStatus = txtStatus,
                imgFillBar = imgBarFill,
                imgCheckBadge = null
            };

            return elements;
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

            // Print 1: Painel de Missões no Visor do Drone Pet
            // Close-up frontal ortogonal nítido focado na aba holográfica de objetivos do Drone
            string pathDroneQuest = Path.Combine(outDir, "TASK-15_Drone_Quest_Panel.png");
            GameObject droneObj = GameObject.Find("[IC] Drone_Guia_IA_ExoBot");
            Transform panelT = null;
            if (droneObj != null)
            {
                panelT = FindDeepChild(droneObj.transform, "Painel_Missoes_Floresta");
            }

            Vector3 pPos = panelT != null ? panelT.position : (droneObj != null ? droneObj.transform.position + new Vector3(0.5f, 0.5f, 0f) : Vector3.zero);
            // Em Canvas WorldSpace no Unity, a face frontal do Canvas fica na direção de -transform.forward
            Vector3 panelNormal = panelT != null ? -panelT.forward : (droneObj != null ? droneObj.transform.forward : Vector3.back);
            Vector3 camPos1 = pPos + panelNormal * 0.70f;
            Vector3 lookAt1 = pPos;
            VisualQACaptureEditor.CaptureCameraView(pathDroneQuest, camPos1, lookAt1, 1920, 1080);
            Debug.Log($"[TASK-15 VisualQA] Capturado Drone Quest Panel: {pathDroneQuest}");

            // Print 2: Totem de Checkpoint na Margem da Trilha (100% em pé na vertical e ancorado no solo)
            // Perspectiva em três quartos mostrando o Totem firmemente ancorado no solo, com alavanca e baú no topo
            string pathTotemTrail = Path.Combine(outDir, "TASK-15_Checkpoint_Totem_Trail.png");
            GameObject totemObj = GameObject.Find("[TASK-15] CheckpointRewardTotem");
            Vector3 tPos = totemObj != null ? totemObj.transform.position : new Vector3(43.50f, 4.80f, 66.20f);
            Vector3 camPos2 = tPos + new Vector3(-2.10f, 1.15f, -1.60f);
            Vector3 lookAt2 = tPos + new Vector3(0f, 0.70f, 0f);
            VisualQACaptureEditor.CaptureCameraView(pathTotemTrail, camPos2, lookAt2, 1920, 1080);
            Debug.Log($"[TASK-15 VisualQA] Capturado Checkpoint Totem Trail: {pathTotemTrail}");

            // Print 3: Baú Aberto no Topo do Totem com Interior Dourado e Tampa Inclinada
            // Close-up frontal a 45 graus olhando para dentro da cavidade dourada com a tampa erguida ao fundo
            string pathChestOpen = Path.Combine(outDir, "TASK-15_Checkpoint_Chest_Open.png");
            InteractiveCheckpointTotem totemScript = totemObj != null ? totemObj.GetComponent<InteractiveCheckpointTotem>() : null;
            if (totemScript != null)
            {
                totemScript.SetOpenImmediate();
            }

            // Câmera frontal aproximada a 75cm da boca do baú, olhando para dentro com ângulo de 25 graus
            Vector3 camPos3 = tPos + new Vector3(-0.70f, 1.55f, -0.60f);
            Vector3 lookAt3 = tPos + new Vector3(0f, 1.20f, 0f);
            VisualQACaptureEditor.CaptureCameraView(pathChestOpen, camPos3, lookAt3, 1920, 1080);
            Debug.Log($"[TASK-15 VisualQA] Capturado Checkpoint Chest Open: {pathChestOpen}");
        }
        #endregion
    }
}
