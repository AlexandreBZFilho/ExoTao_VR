using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;
using VRRehab.Interactive.Task06;
using VRRehab.EditorTools;

namespace VRRehab.EditorAutoSetup
{
    /// <summary>
    /// Builder e validador automatizado para a TASK-06 (Rádio Deck com Fitas Cassete Magnéticas).
    /// Constrói materiais, Prefabs das 4 Fitas Cassete, Deck com Socket e Botão de Eject,
    /// Organizador de mesa em degraus, configura a cena Menu_Principal e executa o ciclo Visual QA (1920x1080).
    /// </summary>
    public static class Task06_SceneAndPrefabBuilder
    {
        private const string ModelsDir = "Assets/Models/Generated/Task06";
        private const string PrefabsDir = "Assets/Prefabs/Interactive";
        private const string MenuScenePath = "Assets/Scenes/Menu_Principal.unity";
        private const string OutVisualQADir = ".agents/visual_qa";

        // Modelos FBX gerados pelo Blender 5.1
        private const string CassetteModelPath = "Assets/Models/Generated/Task06/RetroCassetteTape.fbx";
        private const string DeckModelPath = "Assets/Models/Generated/Task06/CassetteDeckSlot.fbx";
        private const string RackModelPath = "Assets/Models/Generated/Task06/CassetteOrganizerRack.fbx";

        // Áudios
        private const string SfxInsertPath = "Assets/Audio/Generated/Task06/cassette_insert_click.wav";
        private const string SfxEjectPath = "Assets/Audio/Generated/Task06/cassette_eject_snap.wav";
        private const string MusicSciFiPath = "Assets/Resources/Audio/Music_SciFi_Menu.mp3";
        private const string MusicLunaCantinaPath = "Assets/FloatGrids/Demo/Music/Construyendo en el Espacio - Luna Cantina.mp3";
        private const string MusicNeonSunsetPath = "Assets/Audio/Generated/Task06/lofi_sunset_groove.wav";
        private const string MusicEmeraldZenPath = "Assets/Audio/Generated/Task06/ambient_zen_flow.wav";

        [MenuItem("VRRehab/AutoBuild/Build and Verify TASK-06")]
        public static void BuildAndVerify()
        {
            Debug.Log("[TASK-06] Iniciando construção completa do Rádio Deck e Fitas Cassete...");

            // Força a Unity a reimportar de forma síncrona os arquivos FBX gerados no Blender
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(CassetteModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(DeckModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(RackModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            // Remove prefabs antigos para recriação fresca garantida
            string[] prefabsToDelete = new string[] {
                $"{PrefabsDir}/RadioCassetteDeck.prefab",
                $"{PrefabsDir}/CassetteOrganizerRack.prefab",
                $"{PrefabsDir}/CassetteTape_CyberHorizon.prefab",
                $"{PrefabsDir}/CassetteTape_LunaCantina.prefab",
                $"{PrefabsDir}/CassetteTape_NeonSunset.prefab",
                $"{PrefabsDir}/CassetteTape_EmeraldZen.prefab"
            };
            foreach (var p in prefabsToDelete)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(p) != null)
                {
                    AssetDatabase.DeleteAsset(p);
                }
            }

            // 1. Materiais
            EnsureMaterials();

            // 2. Prefabs
            GameObject prefabDeck = BuildDeckPrefab();
            GameObject prefabRack = BuildRackPrefab();

            GameObject prefabTapeCyan = BuildCassettePrefab(
                "CassetteTape_CyberHorizon",
                "tape_cyan_01",
                "Cyber Horizon",
                "Sci-Fi Synthwave",
                new Color(0.0f, 0.85f, 1.0f),
                $"{ModelsDir}/M_Cassette_Shell_Cyan.mat",
                $"{ModelsDir}/M_Cassette_Label_Cyan.mat",
                MusicSciFiPath
            );

            GameObject prefabTapeGold = BuildCassettePrefab(
                "CassetteTape_LunaCantina",
                "tape_gold_02",
                "Luna Cantina",
                "Cantina Latina Retro",
                new Color(0.95f, 0.78f, 0.22f),
                $"{ModelsDir}/M_Cassette_Shell_Gold.mat",
                $"{ModelsDir}/M_Cassette_Label_Gold.mat",
                MusicLunaCantinaPath
            );

            GameObject prefabTapeMagenta = BuildCassettePrefab(
                "CassetteTape_NeonSunset",
                "tape_magenta_03",
                "Neon Sunset",
                "Lo-Fi Chill Rehab",
                new Color(0.95f, 0.15f, 0.65f),
                $"{ModelsDir}/M_Cassette_Shell_Magenta.mat",
                $"{ModelsDir}/M_Cassette_Label_Magenta.mat",
                MusicNeonSunsetPath
            );

            GameObject prefabTapeEmerald = BuildCassettePrefab(
                "CassetteTape_EmeraldZen",
                "tape_emerald_04",
                "Emerald Zen",
                "Nature Zen Acoustic",
                new Color(0.15f, 0.85f, 0.45f),
                $"{ModelsDir}/M_Cassette_Shell_Emerald.mat",
                $"{ModelsDir}/M_Cassette_Label_Emerald.mat",
                MusicEmeraldZenPath
            );

            // 3. Montagem na cena Menu_Principal
            SetupMenuPrincipalScene(prefabDeck, prefabRack, prefabTapeCyan, prefabTapeGold, prefabTapeMagenta, prefabTapeEmerald);

            // 4. Captura Visual QA
            CaptureVisualQA();

            Debug.Log("[TASK-06] Conclusão com sucesso de todos os estágios!");
        }

        public static void EnsureMaterials()
        {
            if (!AssetDatabase.IsValidFolder(ModelsDir))
            {
                AssetDatabase.CreateFolder("Assets/Models/Generated", "Task06");
            }

            // Deck
            EnsureMaterial($"{ModelsDir}/M_Deck_Chassis.mat", "M_Deck_Chassis", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Deck_Chassis", new Color(0.24f, 0.14f, 0.09f), 0.55f, 0.10f));

            EnsureMaterial($"{ModelsDir}/M_Deck_Metal.mat", "M_Deck_Metal", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Deck_Metal", new Color(0.88f, 0.74f, 0.38f), 0.85f, 0.90f));

            EnsureMaterial($"{ModelsDir}/M_Deck_Cavity.mat", "M_Deck_Cavity", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Deck_Cavity", new Color(0.06f, 0.07f, 0.08f), 0.25f, 0.20f));

            EnsureMaterial($"{ModelsDir}/M_Eject_Button.mat", "M_Eject_Button", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Eject_Button", new Color(0.90f, 0.15f, 0.12f), 0.50f, 0.15f));

            EnsureMaterial($"{ModelsDir}/M_Deck_StatusLED.mat", "M_Deck_StatusLED", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_Deck_StatusLED", new Color(0.0f, 0.95f, 1.0f), new Color(0.0f, 0.95f, 1.0f), 3.0f));

            // Rack
            EnsureMaterial($"{ModelsDir}/M_Rack_Wood.mat", "M_Rack_Wood", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Rack_Wood", new Color(0.24f, 0.14f, 0.09f), 0.55f, 0.10f));

            EnsureMaterial($"{ModelsDir}/M_Rack_Trim.mat", "M_Rack_Trim", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Rack_Trim", new Color(0.88f, 0.74f, 0.38f), 0.80f, 0.85f));

            // Shared Cassette Parts
            EnsureMaterial($"{ModelsDir}/M_Cassette_Window.mat", "M_Cassette_Window", () =>
                MaterialShaderHelper.CreateTransparentMaterial("M_Cassette_Window", new Color(0.85f, 0.92f, 0.98f), 0.45f));

            EnsureMaterial($"{ModelsDir}/M_Cassette_Spool.mat", "M_Cassette_Spool", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Cassette_Spool", new Color(0.92f, 0.90f, 0.85f), 0.45f, 0.05f));

            EnsureMaterial($"{ModelsDir}/M_Cassette_TapeRibbon.mat", "M_Cassette_TapeRibbon", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Cassette_TapeRibbon", new Color(0.12f, 0.09f, 0.07f), 0.30f, 0.05f));

            // Tape 1: Cyan / Cyber Horizon
            EnsureMaterial($"{ModelsDir}/M_Cassette_Shell_Cyan.mat", "M_Cassette_Shell_Cyan", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Cassette_Shell_Cyan", new Color(0.12f, 0.14f, 0.16f), 0.40f, 0.10f));

            EnsureMaterial($"{ModelsDir}/M_Cassette_Label_Cyan.mat", "M_Cassette_Label_Cyan", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_Cassette_Label_Cyan", new Color(0.02f, 0.65f, 0.80f), new Color(0.0f, 0.85f, 1.0f), 1.2f));

            // Tape 2: Gold / Luna Cantina
            EnsureMaterial($"{ModelsDir}/M_Cassette_Shell_Gold.mat", "M_Cassette_Shell_Gold", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Cassette_Shell_Gold", new Color(0.26f, 0.18f, 0.11f), 0.45f, 0.20f));

            EnsureMaterial($"{ModelsDir}/M_Cassette_Label_Gold.mat", "M_Cassette_Label_Gold", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Cassette_Label_Gold", new Color(0.92f, 0.75f, 0.25f), 0.60f, 0.40f));

            // Tape 3: Magenta / Neon Sunset
            EnsureMaterial($"{ModelsDir}/M_Cassette_Shell_Magenta.mat", "M_Cassette_Shell_Magenta", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Cassette_Shell_Magenta", new Color(0.16f, 0.10f, 0.22f), 0.40f, 0.10f));

            EnsureMaterial($"{ModelsDir}/M_Cassette_Label_Magenta.mat", "M_Cassette_Label_Magenta", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_Cassette_Label_Magenta", new Color(0.85f, 0.12f, 0.60f), new Color(0.95f, 0.15f, 0.65f), 1.2f));

            // Tape 4: Emerald / Emerald Zen
            EnsureMaterial($"{ModelsDir}/M_Cassette_Shell_Emerald.mat", "M_Cassette_Shell_Emerald", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Cassette_Shell_Emerald", new Color(0.10f, 0.18f, 0.12f), 0.40f, 0.10f));

            EnsureMaterial($"{ModelsDir}/M_Cassette_Label_Emerald.mat", "M_Cassette_Label_Emerald", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Cassette_Label_Emerald", new Color(0.15f, 0.80f, 0.40f), 0.50f, 0.20f));

            AssetDatabase.SaveAssets();
        }

        private static Material EnsureMaterial(string path, string name, Func<Material> createFunc)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = createFunc();
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                MaterialShaderHelper.EnsureValidShader(mat);
            }
            return mat;
        }

        private static GameObject BuildCassettePrefab(
            string prefabName,
            string tapeId,
            string title,
            string genre,
            Color themeColor,
            string shellMatPath,
            string labelMatPath,
            string audioClipPath)
        {
            if (!AssetDatabase.IsValidFolder(PrefabsDir))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "Interactive");
            }

            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CassetteModelPath);
            if (fbx == null)
            {
                Debug.LogError($"[TASK-06] Modelo FBX da fita não encontrado em {CassetteModelPath}");
                return null;
            }

            GameObject root = new GameObject(prefabName);
            root.layer = LayerMask.NameToLayer("Default");

            GameObject visual = UnityEngine.Object.Instantiate(fbx, root.transform);
            visual.name = "Visual_Model";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            foreach (var col in visual.GetComponentsInChildren<Collider>())
            {
                UnityEngine.Object.DestroyImmediate(col);
            }

            Material matShell = AssetDatabase.LoadAssetAtPath<Material>(shellMatPath);
            Material matLabel = AssetDatabase.LoadAssetAtPath<Material>(labelMatPath);
            Material matWindow = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Cassette_Window.mat");
            Material matSpool = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Cassette_Spool.mat");
            Material matRibbon = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Cassette_TapeRibbon.mat");

            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>())
            {
                string objName = mr.gameObject.name;
                if (objName.Contains("Body_Shell")) mr.sharedMaterial = matShell;
                else if (objName.Contains("Label")) mr.sharedMaterial = matLabel;
                else if (objName.Contains("Window")) mr.sharedMaterial = matWindow;
                else if (objName.Contains("Spool")) mr.sharedMaterial = matSpool;
                else if (objName.Contains("Ribbon")) mr.sharedMaterial = matRibbon;
            }

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(0.100f, 0.064f, 0.012f);
            box.center = Vector3.zero;

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.12f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            MusicCassetteTape tape = root.AddComponent<MusicCassetteTape>();
            tape.tapeId = tapeId;
            tape.tapeTitle = title;
            tape.tapeGenre = genre;
            tape.themeColor = themeColor;
            tape.musicClip = AssetDatabase.LoadAssetAtPath<AudioClip>(audioClipPath);

            Transform sLeft = visual.transform.Find("Spool_Left");
            Transform sRight = visual.transform.Find("Spool_Right");
            tape.spoolLeft = sLeft;
            tape.spoolRight = sRight;

            string savePath = $"{PrefabsDir}/{prefabName}.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log($"[TASK-06] Prefab de fita salvo em {savePath}");
            return savedPrefab;
        }

        private static GameObject BuildDeckPrefab()
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(DeckModelPath);
            if (fbx == null)
            {
                Debug.LogError($"[TASK-06] Modelo FBX do Deck não encontrado em {DeckModelPath}");
                return null;
            }

            GameObject root = new GameObject("RadioCassetteDeck");
            root.layer = LayerMask.NameToLayer("Default");

            GameObject visual = UnityEngine.Object.Instantiate(fbx, root.transform);
            visual.name = "Visual_Model";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            foreach (var col in visual.GetComponentsInChildren<Collider>())
            {
                UnityEngine.Object.DestroyImmediate(col);
            }

            Material matChassis = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Deck_Chassis.mat");
            Material matMetal = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Deck_Metal.mat");
            Material matCavity = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Deck_Cavity.mat");
            Material matEject = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Eject_Button.mat");
            Material matLed = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Deck_StatusLED.mat");

            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>())
            {
                string n = mr.gameObject.name;
                if (n.Contains("Chassis") || n.Contains("Housing")) mr.sharedMaterial = matChassis;
                else if (n.Contains("Faceplate") || n.Contains("Bezel") || n.Contains("Ring")) mr.sharedMaterial = matMetal;
                else if (n.Contains("Slot") || n.Contains("Cavity")) mr.sharedMaterial = matCavity;
                else if (n.Contains("Eject_Button")) mr.sharedMaterial = matEject;
                else if (n.Contains("Status_LED")) mr.sharedMaterial = matLed;
            }

            BoxCollider deckCol = root.AddComponent<BoxCollider>();
            deckCol.center = new Vector3(0f, 0.0375f, 0f);
            deckCol.size = new Vector3(0.20f, 0.075f, 0.15f);

            // Ponto de Ancoragem (Attach Point) da fita no slot do deck:
            // Elevação Y = 0.082m para que metade da fita fique projetada para fora,
            // permitindo visualizar claramente a janela acrílica, carretéis e a etiqueta colorida!
            GameObject attachGo = new GameObject("SlotAttachPoint");
            attachGo.transform.SetParent(root.transform, false);
            attachGo.transform.localPosition = new Vector3(0f, 0.082f, 0.010f);
            attachGo.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);

            // Gatilho de encaixe magnético (SocketTriggerZone)
            GameObject socketTriggerGo = new GameObject("SocketTriggerZone");
            socketTriggerGo.transform.SetParent(root.transform, false);
            socketTriggerGo.transform.localPosition = new Vector3(0f, 0.082f, 0.010f);
            socketTriggerGo.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);

            BoxCollider socketCol = socketTriggerGo.AddComponent<BoxCollider>();
            socketCol.isTrigger = true;
            socketCol.size = new Vector3(0.12f, 0.09f, 0.05f);

            RadioCassetteSocket socket = socketTriggerGo.AddComponent<RadioCassetteSocket>();
            socket.slotAttachPoint = attachGo.transform;
            socket.attachTransform = attachGo.transform;
            socket.sfxInsert = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxInsertPath);
            socket.sfxEject = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxEjectPath);

            AudioSource audio = socketTriggerGo.AddComponent<AudioSource>();
            audio.spatialBlend = 1.0f;
            audio.minDistance = 0.5f;
            audio.maxDistance = 6.0f;
            socket.socketAudioSource = audio;

            GameObject ledGo = new GameObject("Status_LED_Light");
            ledGo.transform.SetParent(root.transform, false);
            ledGo.transform.localPosition = new Vector3(-0.035f, 0.082f, -0.040f);
            Light ledLight = ledGo.AddComponent<Light>();
            ledLight.type = LightType.Point;
            ledLight.range = 0.35f;
            ledLight.intensity = 1.0f;
            ledLight.color = new Color(0.0f, 0.95f, 1.0f);
            socket.statusLED = ledLight;

            // Configuração do Botão Físico EJECT na placa superior
            Transform btnTransform = visual.transform.Find("Eject_Button");
            if (btnTransform != null)
            {
                BoxCollider btnCol = btnTransform.gameObject.AddComponent<BoxCollider>();
                btnCol.isTrigger = false;
                btnCol.size = new Vector3(0.030f, 0.020f, 0.015f);

                CassetteEjectButton ejectBtn = btnTransform.gameObject.AddComponent<CassetteEjectButton>();
                ejectBtn.deckSocket = socket;
                ejectBtn.buttonMeshTransform = btnTransform;
                ejectBtn.pressedLocalOffset = new Vector3(0f, 0f, -0.006f);
                socket.ejectButton = ejectBtn;
            }

            string savePath = $"{PrefabsDir}/RadioCassetteDeck.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log($"[TASK-06] Prefab do Rádio Deck salvo em {savePath}");
            return savedPrefab;
        }

        private static GameObject BuildRackPrefab()
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(RackModelPath);
            if (fbx == null)
            {
                Debug.LogError($"[TASK-06] Modelo FBX do Rack não encontrado em {RackModelPath}");
                return null;
            }

            GameObject root = new GameObject("CassetteOrganizerRack");
            root.layer = LayerMask.NameToLayer("Default");

            GameObject visual = UnityEngine.Object.Instantiate(fbx, root.transform);
            visual.name = "Visual_Model";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            foreach (var col in visual.GetComponentsInChildren<Collider>())
            {
                UnityEngine.Object.DestroyImmediate(col);
            }

            Material matWood = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Rack_Wood.mat");
            Material matTrim = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Rack_Trim.mat");

            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>())
            {
                if (mr.gameObject.name.Contains("Base")) mr.sharedMaterial = matWood;
                else mr.sharedMaterial = matTrim;
            }

            BoxCollider baseCol = root.AddComponent<BoxCollider>();
            baseCol.center = new Vector3(0f, 0.0175f, 0f);
            baseCol.size = new Vector3(0.14f, 0.035f, 0.14f);

            // 4 Slots de Ancoragem escalonados em degraus com inclinação de 18° para trás
            float[] slotOffsetsZ = new float[] { -0.035f, -0.009f, 0.017f, 0.043f };
            float[] slotHeightsY = new float[] { 0.042f,  0.050f,  0.058f, 0.066f };
            for (int i = 0; i < 4; i++)
            {
                GameObject slotGo = new GameObject($"Slot_{i}");
                slotGo.transform.SetParent(root.transform, false);
                slotGo.transform.localPosition = new Vector3(0f, slotHeightsY[i], slotOffsetsZ[i]);
                slotGo.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            }

            string savePath = $"{PrefabsDir}/CassetteOrganizerRack.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log($"[TASK-06] Prefab do Organizador de Fitas salvo em {savePath}");
            return savedPrefab;
        }

        public static void SetupMenuPrincipalScene(
            GameObject prefabDeck,
            GameObject prefabRack,
            GameObject prefabTapeCyan,
            GameObject prefabTapeGold,
            GameObject prefabTapeMagenta,
            GameObject prefabTapeEmerald)
        {
            if (EditorSceneManager.GetActiveScene().path != MenuScenePath)
            {
                EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            }

            GameObject radioObj = GameObject.Find("[IC] Radio_Menu_3D");
            if (radioObj == null)
            {
                var radioComp = UnityEngine.Object.FindFirstObjectByType<RadioMenu3D>();
                if (radioComp != null) radioObj = radioComp.gameObject;
            }

            // Reposiciona o brinquedo interativo e o bastão para a lateral esquerda da mesa,
            // liberando todo o centro e frente do rádio para a estação de fitas
            GameObject toyDemoObj = GameObject.Find("[IC] Ergonomic_Toy_Demo");
            if (toyDemoObj != null)
            {
                toyDemoObj.transform.position = new Vector3(0.58f, 1.025f, -0.28f);
            }

            GameObject batonObj = GameObject.Find("[IC] Cyber_Fetch_Baton");
            if (batonObj != null)
            {
                batonObj.transform.position = new Vector3(0.58f, 0.965f, -0.42f);
                batonObj.transform.rotation = Quaternion.Euler(0f, 45f, 90f);
            }

            // Remove instâncias antigas para montagem limpa idempotente
            string[] cleanupNames = new string[] {
                "RadioCassetteDeck_Instance",
                "CassetteOrganizerRack_Instance",
                "Tape_CyberHorizon_Active",
                "Tape_LunaCantina_Rack",
                "Tape_NeonSunset_Rack",
                "Tape_EmeraldZen_Rack"
            };
            foreach (string cName in cleanupNames)
            {
                GameObject existing = GameObject.Find(cName);
                if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
            }

            // 1. Instancia o RadioCassetteDeck ao lado do rádio
            Vector3 deckPos = new Vector3(0.66f, 0.952f, 0.14f);
            Quaternion deckRot = Quaternion.Euler(0f, 75f, 0f);

            GameObject deckInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefabDeck);
            deckInstance.name = "RadioCassetteDeck_Instance";
            deckInstance.transform.position = deckPos;
            deckInstance.transform.rotation = deckRot;

            RadioCassetteSocket socket = deckInstance.GetComponentInChildren<RadioCassetteSocket>();
            if (socket != null && radioObj != null)
            {
                socket.radioTarget = radioObj.GetComponent<RadioMenu3D>();
            }

            // 2. Instancia o CassetteOrganizerRack na mesa
            Vector3 rackPos = new Vector3(0.70f, 0.952f, -0.08f);
            Quaternion rackRot = Quaternion.Euler(0f, 75f, 0f);

            GameObject rackInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefabRack);
            rackInstance.name = "CassetteOrganizerRack_Instance";
            rackInstance.transform.position = rackPos;
            rackInstance.transform.rotation = rackRot;

            Transform slot0 = rackInstance.transform.Find("Slot_0");
            Transform slot1 = rackInstance.transform.Find("Slot_1");
            Transform slot2 = rackInstance.transform.Find("Slot_2");
            Transform slot3 = rackInstance.transform.Find("Slot_3");

            // 3. Instancia as 4 Fitas Cassete
            // Tape 1 (Cyber Horizon): Inserida no Slot do Deck
            GameObject tapeCyanInst = (GameObject)PrefabUtility.InstantiatePrefab(prefabTapeCyan);
            tapeCyanInst.name = "Tape_CyberHorizon_Active";
            if (socket != null && socket.slotAttachPoint != null)
            {
                tapeCyanInst.transform.position = socket.slotAttachPoint.position;
                tapeCyanInst.transform.rotation = socket.slotAttachPoint.rotation;
            }
            MusicCassetteTape tapeCyan = tapeCyanInst.GetComponent<MusicCassetteTape>();
            if (tapeCyan != null)
            {
                tapeCyan.SetPlaying(true);
                tapeCyan.SetAssignedRackSlot(slot0);
            }
            Rigidbody rbCyan = tapeCyanInst.GetComponent<Rigidbody>();
            if (rbCyan != null) rbCyan.isKinematic = true;

            // Tape 2 (Luna Cantina): Slot 1 do Rack (Dourada)
            GameObject tapeGoldInst = (GameObject)PrefabUtility.InstantiatePrefab(prefabTapeGold);
            tapeGoldInst.name = "Tape_LunaCantina_Rack";
            if (slot1 != null)
            {
                tapeGoldInst.transform.position = slot1.position;
                tapeGoldInst.transform.rotation = slot1.rotation;
                tapeGoldInst.GetComponent<MusicCassetteTape>()?.SetAssignedRackSlot(slot1);
            }
            Rigidbody rbGold = tapeGoldInst.GetComponent<Rigidbody>();
            if (rbGold != null) rbGold.isKinematic = true;

            // Tape 3 (Neon Sunset): Slot 2 do Rack (Magenta)
            GameObject tapeMagentaInst = (GameObject)PrefabUtility.InstantiatePrefab(prefabTapeMagenta);
            tapeMagentaInst.name = "Tape_NeonSunset_Rack";
            if (slot2 != null)
            {
                tapeMagentaInst.transform.position = slot2.position;
                tapeMagentaInst.transform.rotation = slot2.rotation;
                tapeMagentaInst.GetComponent<MusicCassetteTape>()?.SetAssignedRackSlot(slot2);
            }
            Rigidbody rbMagenta = tapeMagentaInst.GetComponent<Rigidbody>();
            if (rbMagenta != null) rbMagenta.isKinematic = true;

            // Tape 4 (Emerald Zen): Slot 3 do Rack (Esmeralda)
            GameObject tapeEmeraldInst = (GameObject)PrefabUtility.InstantiatePrefab(prefabTapeEmerald);
            tapeEmeraldInst.name = "Tape_EmeraldZen_Rack";
            if (slot3 != null)
            {
                tapeEmeraldInst.transform.position = slot3.position;
                tapeEmeraldInst.transform.rotation = slot3.rotation;
                tapeEmeraldInst.GetComponent<MusicCassetteTape>()?.SetAssignedRackSlot(slot3);
            }
            Rigidbody rbEmerald = tapeEmeraldInst.GetComponent<Rigidbody>();
            if (rbEmerald != null) rbEmerald.isKinematic = true;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("[TASK-06] Cena Menu_Principal configurada com Deck, Rack e as 4 fitas!");
        }

        public static void CaptureVisualQA()
        {
            string outDir = Path.GetFullPath(OutVisualQADir);
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            // 1. TASK-06_Radio_Deck_Overview.png
            string pathOverview = Path.Combine(outDir, "TASK-06_Radio_Deck_Overview.png");
            Vector3 camPos1 = new Vector3(1.70f, 1.40f, 0.22f);
            Vector3 lookAt1 = new Vector3(0.66f, 0.98f, 0.14f);
            VisualQACaptureEditor.CaptureCameraView(pathOverview, camPos1, lookAt1, 1920, 1080);
            Debug.Log($"[TASK-06] Visual QA 1 capturado: {pathOverview}");

            // 2. TASK-06_Cassette_Tapes_Closeup.png
            string pathCloseup = Path.Combine(outDir, "TASK-06_Cassette_Tapes_Closeup.png");
            Vector3 camPos2 = new Vector3(1.10f, 1.25f, -0.08f);
            Vector3 lookAt2 = new Vector3(0.70f, 1.01f, -0.08f);
            VisualQACaptureEditor.CaptureCameraView(pathCloseup, camPos2, lookAt2, 1920, 1080);
            Debug.Log($"[TASK-06] Visual QA 2 capturado: {pathCloseup}");

            // 3. TASK-06_Tape_Inserted_Slot.png
            string pathSlot = Path.Combine(outDir, "TASK-06_Tape_Inserted_Slot.png");
            Vector3 camPos3 = new Vector3(1.05f, 1.25f, 0.14f);
            Vector3 lookAt3 = new Vector3(0.66f, 1.01f, 0.14f);
            VisualQACaptureEditor.CaptureCameraView(pathSlot, camPos3, lookAt3, 1920, 1080);
            Debug.Log($"[TASK-06] Visual QA 3 capturado: {pathSlot}");
        }
    }
}
