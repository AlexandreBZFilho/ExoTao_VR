using System;
using System.IO;
using System.Collections.Generic;
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
    /// Builder e validador automatizado para a TASK-06 (Rádio Deck com 4 Fitas Cassete Magnéticas na Mesa 1).
    /// Executa:
    /// 1. Criação e validação de materiais VR-safe com GPU Instancing ativo;
    /// 2. Construção dos Prefabs:
    ///    - RadioCassetteDeck (FBX, BoxCollider, SocketTriggerZone com RadioCassetteSocket, Status_LED_Light, CassetteEjectButton);
    ///    - CassetteOrganizerRack (FBX, BoxCollider, CassetteOrganizerRack com 4 slots em degraus inclinados a 18°);
    ///    - 4 Prefabs de Fitas Cassete (Cyber Horizon, Luna Cantina, Neon Sunset, Emerald Zen) com MusicCassetteTape (LaserSwingThrowInteractable);
    /// 3. Higienização profunda da cena Menu_Principal (Lei 1: Expurgo de lixo no (0,0,0) e duplicatas);
    /// 4. Posicionamento métrico na Mesa 1 (full_desk_3 (1)) com tampo a Y = 0.952m (Lei 2 e Lei 3):
    ///    - RadioCassetteDeck ao lado do rádio vintage em (0.66, 0.952, 0.14);
    ///    - CassetteOrganizerRack em (0.70, 0.952, -0.08);
    ///    - 4 Fitas: Fita 1 no Deck, Fitas 2, 3 e 4 nos slots 1, 2, 3 do Rack;
    ///    - Validação de ausência de sobreposição (!boundsA.Intersects(boundsB)) e folga métrica >= 0.08m;
    /// 5. Capturas HD de Visual QA (1920x1080):
    ///    - TASK-06_View1_Front.png
    ///    - TASK-06_View2_Side_Grounding.png
    ///    - TASK-06_View3_Closeup.png
    ///    - TASK-06_Eject_Sequence.png
    ///    - TASK-06_Radio_Deck_Overview.png
    ///    - TASK-06_Cassette_Tapes_Closeup.png
    ///    - TASK-06_Tape_Inserted_Slot.png
    /// 6. Auditoria Espacial formal em TASK-06_SpatialAudit.json via SpatialAuditUtility;
    /// 7. Gravação de log de resultado em TASK-06_BuilderResult.txt.
    /// </summary>
    [InitializeOnLoad]
    public static class Task06_SceneAndPrefabBuilder
    {
        private const string TriggerFile = ".run_builder_task06";
        private const string ResultFile = ".agents/visual_qa/TASK-06_BuilderResult.txt";
        private const string MenuScenePath = "Assets/Scenes/Menu_Principal.unity";
        private const string ModelsDir = "Assets/Models/Generated/Task06";
        private const string PrefabsDir = "Assets/Prefabs/Interactive";
        private const string OutVisualQADir = ".agents/visual_qa";

        // Modelos FBX Low-Poly VR gerados no Blender 5.1 (TASK-06.B)
        private const string CassetteModelPath = "Assets/Models/Generated/Task06/RetroCassetteTape.fbx";
        private const string DeckModelPath = "Assets/Models/Generated/Task06/CassetteDeckSlot.fbx";
        private const string RackModelPath = "Assets/Models/Generated/Task06/CassetteOrganizerRack.fbx";

        // Áudios VR-Safe Calibrados (Pico <= -8.5 dBFS)
        private const string SfxInsertPath = "Assets/Audio/Generated/Task06/cassette_insert_click.wav";
        private const string SfxEjectPath = "Assets/Audio/Generated/Task06/cassette_eject_snap.wav";
        private const string MusicSciFiPath = "Assets/Resources/Audio/Music_SciFi_Menu.mp3";
        private const string MusicLunaCantinaPath = "Assets/FloatGrids/Demo/Music/Construyendo en el Espacio - Luna Cantina.mp3";
        private const string MusicNeonSunsetPath = "Assets/Audio/Generated/Task06/lofi_sunset_groove.wav";
        private const string MusicEmeraldZenPath = "Assets/Audio/Generated/Task06/ambient_zen_flow.wav";

        private static bool isExecuting = false;

        static Task06_SceneAndPrefabBuilder()
        {
            EditorApplication.delayCall += OnEditorReady;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorReady()
        {
            CheckTriggerFile();
        }

        private static void OnEditorUpdate()
        {
            CheckTriggerFile();
        }

        private static void CheckTriggerFile()
        {
            if (isExecuting) return;

            string triggerPath = Path.Combine(Directory.GetCurrentDirectory(), TriggerFile);
            if (File.Exists(triggerPath))
            {
                try
                {
                    File.Delete(triggerPath);
                    Debug.Log($"[Task06_SceneAndPrefabBuilder] Gatilho detectado ({TriggerFile}). Iniciando execução...");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Task06_SceneAndPrefabBuilder] Erro ao deletar trigger: {ex.Message}");
                }

                BuildAndVerify();
            }
        }

        [MenuItem("VRRehab/AutoBuild/Build and Verify TASK-06")]
        public static void BuildAndVerifyMenu()
        {
            string summary = BuildAndVerify();
            Debug.Log("[TASK-06] Execução concluída:\n" + summary);
        }

        public static string BuildAndVerify()
        {
            if (isExecuting) return "Já em execução";
            isExecuting = true;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== RELATÓRIO DE EXECUÇÃO E AUDITORIA ESPACIAL (TASK-06) ===");
            sb.AppendLine($"Data/Hora: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n");

            string originalScenePath = EditorSceneManager.GetActiveScene().path;

            try
            {
                // Força importação síncrona dos FBXs
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(CassetteModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(DeckModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(RackModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                // 1. Materiais com GPU Instancing ativo
                sb.AppendLine("--- 1. MATERIAIS & RECURSOS VR-SAFE ---");
                EnsureMaterials(sb);

                // 2. Construção de Prefabs limpos
                sb.AppendLine("--- 2. CONSTRUÇÃO DE PREFABS INTERATIVOS ---");
                DeleteOldPrefabs(sb);
                GameObject prefabDeck = BuildDeckPrefab(sb);
                GameObject prefabRack = BuildRackPrefab(sb);

                GameObject prefabTapeCyan = BuildCassettePrefab(
                    "CassetteTape_CyberHorizon",
                    "tape_cyan_01",
                    "Cyber Horizon",
                    "Sci-Fi Synthwave",
                    new Color(0.0f, 0.85f, 1.0f),
                    $"{ModelsDir}/M_Cassette_Shell_Cyan.mat",
                    $"{ModelsDir}/M_Cassette_Label_Cyan.mat",
                    MusicSciFiPath,
                    110f,
                    sb
                );

                GameObject prefabTapeGold = BuildCassettePrefab(
                    "CassetteTape_LunaCantina",
                    "tape_gold_02",
                    "Luna Cantina",
                    "Cantina Latina Retro",
                    new Color(0.95f, 0.78f, 0.22f),
                    $"{ModelsDir}/M_Cassette_Shell_Gold.mat",
                    $"{ModelsDir}/M_Cassette_Label_Gold.mat",
                    MusicLunaCantinaPath,
                    105f,
                    sb
                );

                GameObject prefabTapeMagenta = BuildCassettePrefab(
                    "CassetteTape_NeonSunset",
                    "tape_magenta_03",
                    "Neon Sunset",
                    "Lo-Fi Chill Rehab",
                    new Color(0.95f, 0.15f, 0.65f),
                    $"{ModelsDir}/M_Cassette_Shell_Magenta.mat",
                    $"{ModelsDir}/M_Cassette_Label_Magenta.mat",
                    MusicNeonSunsetPath,
                    80f,
                    sb
                );

                GameObject prefabTapeEmerald = BuildCassettePrefab(
                    "CassetteTape_EmeraldZen",
                    "tape_emerald_04",
                    "Emerald Zen",
                    "Nature Zen Acoustic",
                    new Color(0.15f, 0.85f, 0.45f),
                    $"{ModelsDir}/M_Cassette_Shell_Emerald.mat",
                    $"{ModelsDir}/M_Cassette_Label_Emerald.mat",
                    MusicEmeraldZenPath,
                    65f,
                    sb
                );

                // 3. Montagem na cena Menu_Principal (Lei 1, Lei 2 e Lei 3)
                sb.AppendLine("--- 3. CENA: Menu_Principal (EXPURGO (0,0,0) & MONTAGEM MESA 1) ---");
                int menuCleanedOrigin = 0;
                int menuCleanedDuplicates = 0;
                SetupMenuPrincipalScene(
                    prefabDeck,
                    prefabRack,
                    prefabTapeCyan,
                    prefabTapeGold,
                    prefabTapeMagenta,
                    prefabTapeEmerald,
                    out menuCleanedOrigin,
                    out menuCleanedDuplicates,
                    sb
                );

                // 4-7. (Re-run [D] pós-auditoria) Layout validado da Mesa 1: o posicionamento fixo acima é apenas o ponto
                // de partida. Task06_RelayoutPass realoca o que não é TASK-06, reposiciona Deck/Rack com folga ≥ 0.08 m
                // contra todos os objetos da mesa, roda o expurgo antes/depois, gera pairwiseClearances e as capturas
                // offscreen (incluindo a sequência de Eject simulada com Rigidbody real).
                sb.AppendLine("--- 4. RELAYOUT VALIDADO + AUDITORIA + CAPTURAS (Task06_RelayoutPass) ---");
                sb.AppendLine(Task06_RelayoutPass.Run());
            }
            catch (Exception ex)
            {
                sb.AppendLine($"\n[ERRO CRÍTICO]: {ex.Message}\n{ex.StackTrace}");
                Debug.LogError($"[Task06_SceneAndPrefabBuilder] Erro: {ex.Message}");
            }
            finally
            {
                string resultDir = Path.GetDirectoryName(ResultFile);
                if (!Directory.Exists(resultDir)) Directory.CreateDirectory(resultDir);
                File.WriteAllText(ResultFile, sb.ToString());
                Debug.Log(sb.ToString());

                if (!string.IsNullOrEmpty(originalScenePath) && EditorSceneManager.GetActiveScene().path != originalScenePath)
                {
                    EditorSceneManager.SaveOpenScenes();
                    EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
                }

                isExecuting = false;
            }

            return sb.ToString();
        }

        private static void DeleteOldPrefabs(System.Text.StringBuilder log)
        {
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
                    log.AppendLine($"  - Prefab legado removido para recriação fresca: {p}");
                }
            }
        }

        public static void EnsureMaterials(System.Text.StringBuilder log = null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Models"))
                AssetDatabase.CreateFolder("Assets", "Models");
            if (!AssetDatabase.IsValidFolder("Assets/Models/Generated"))
                AssetDatabase.CreateFolder("Assets/Models", "Generated");
            if (!AssetDatabase.IsValidFolder(ModelsDir))
                AssetDatabase.CreateFolder("Assets/Models/Generated", "Task06");

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

            // Garante enableInstancing = true em todos os materiais de Task06
            string[] allMatGuids = AssetDatabase.FindAssets("t:Material", new string[] { ModelsDir });
            foreach (var guid in allMatGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m != null && !m.enableInstancing)
                {
                    m.enableInstancing = true;
                    EditorUtility.SetDirty(m);
                }
            }

            AssetDatabase.SaveAssets();
            if (log != null) log.AppendLine("  - Materiais de Task06 validados com GPU Instancing ativado.");
        }

        private static Material EnsureMaterial(string path, string name, Func<Material> createFunc)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = createFunc();
                mat.enableInstancing = true;
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                MaterialShaderHelper.EnsureValidShader(mat);
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
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
            string audioClipPath,
            float trackBpm,
            System.Text.StringBuilder log = null)
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
            tape.trackBpm = trackBpm;

            Transform sLeft = visual.transform.Find("Spool_Left");
            Transform sRight = visual.transform.Find("Spool_Right");
            tape.spoolLeft = sLeft;
            tape.spoolRight = sRight;

            string savePath = $"{PrefabsDir}/{prefabName}.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
            UnityEngine.Object.DestroyImmediate(root);

            if (log != null) log.AppendLine($"  - Prefab {prefabName} criado e salvo em {savePath}");
            return savedPrefab;
        }

        private static GameObject BuildDeckPrefab(System.Text.StringBuilder log = null)
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

            // Laje + bordas laterais: o corredor do slot fica livre para a fita ejetar (ver Task06_RelayoutPass.ApplyDeckSlotColliders)
            Task06_RelayoutPass.ApplyDeckSlotColliders(root);

            // Ponto de Ancoragem (Attach Point) da fita no slot do deck
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
            ledLight.shadows = LightShadows.None;
            socket.statusLED = ledLight;

            Transform ledMeshTrans = visual.transform.Find("Status_LED");
            if (ledMeshTrans != null)
            {
                socket.ledMeshRenderer = ledMeshTrans.GetComponent<MeshRenderer>();
            }

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

            if (log != null) log.AppendLine($"  - Prefab RadioCassetteDeck criado e salvo em {savePath}");
            return savedPrefab;
        }

        private static GameObject BuildRackPrefab(System.Text.StringBuilder log = null)
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

            CassetteOrganizerRack rackComp = root.AddComponent<CassetteOrganizerRack>();
            rackComp.slots = new Transform[4];

            float[] slotOffsetsZ = new float[] { -0.035f, -0.009f, 0.017f, 0.043f };
            float[] slotHeightsY = new float[] { 0.042f,  0.050f,  0.058f, 0.066f };
            for (int i = 0; i < 4; i++)
            {
                GameObject slotGo = new GameObject($"Slot_{i}");
                slotGo.transform.SetParent(root.transform, false);
                slotGo.transform.localPosition = new Vector3(0f, slotHeightsY[i], slotOffsetsZ[i]);
                slotGo.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
                rackComp.slots[i] = slotGo.transform;
            }

            string savePath = $"{PrefabsDir}/CassetteOrganizerRack.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
            UnityEngine.Object.DestroyImmediate(root);

            if (log != null) log.AppendLine($"  - Prefab CassetteOrganizerRack criado e salvo em {savePath}");
            return savedPrefab;
        }

        public static void SetupMenuPrincipalScene(
            GameObject prefabDeck,
            GameObject prefabRack,
            GameObject prefabTapeCyan,
            GameObject prefabTapeGold,
            GameObject prefabTapeMagenta,
            GameObject prefabTapeEmerald,
            out int cleanedOrigin,
            out int cleanedDuplicates,
            System.Text.StringBuilder log = null)
        {
            cleanedOrigin = 0;
            cleanedDuplicates = 0;

            if (EditorSceneManager.GetActiveScene().path != MenuScenePath)
            {
                EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            }

            var activeScene = EditorSceneManager.GetActiveScene();
            var rootObjects = activeScene.GetRootGameObjects();

            // Lei 1: Expurgo de lixo na origem (0,0,0)
            foreach (var root in rootObjects)
            {
                if (root == null || SpatialAuditUtility.IsWhitelisted(root)) continue;

                float dist = Vector3.Distance(root.transform.position, Vector3.zero);
                if (dist < 0.80f)
                {
                    if (log != null) log.AppendLine($"  - [Expurgo (0,0,0)] Deletando root órfão na origem: '{root.name}'");
                    Undo.DestroyObjectImmediate(root);
                    cleanedOrigin++;
                }
            }

            // Lei 1: Limpeza de duplicatas e instâncias antigas de TASK-06
            string[] cleanupNames = new string[] {
                "RadioCassetteDeck_Instance",
                "CassetteOrganizerRack_Instance",
                "Tape_CyberHorizon_Active",
                "Tape_LunaCantina_Rack",
                "Tape_NeonSunset_Rack",
                "Tape_EmeraldZen_Rack"
            };

            foreach (var root in activeScene.GetRootGameObjects())
            {
                if (root == null) continue;
                foreach (string cName in cleanupNames)
                {
                    if (root.name.StartsWith(cName, StringComparison.OrdinalIgnoreCase))
                    {
                        Undo.DestroyObjectImmediate(root);
                        cleanedDuplicates++;
                    }
                }
            }

            // Localiza o rádio vintage original
            GameObject radioObj = GameObject.Find("[IC] Radio_Menu_3D");
            if (radioObj == null)
            {
                var radioComp = UnityEngine.Object.FindFirstObjectByType<RadioMenu3D>();
                if (radioComp != null) radioObj = radioComp.gameObject;
            }

            // Desobstrução ergonômica da Mesa 1
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

            // Lei 2: Posicionamento métrico na Mesa 1 (full_desk_3 (1)) com tampo a Y = 0.952m
            // 1. RadioCassetteDeck ao lado do rádio vintage
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

            // 2. CassetteOrganizerRack na Mesa 1
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

            // 3. Instanciação e Acomodação das 4 Fitas Cassete
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
            if (socket != null)
            {
                socket.startingSelectedInteractable = tapeCyan;
            }
            Rigidbody rbCyan = tapeCyanInst.GetComponent<Rigidbody>();
            if (rbCyan != null) rbCyan.isKinematic = true;

            // Tape 2 (Luna Cantina): Slot 1 do Rack
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

            // Tape 3 (Neon Sunset): Slot 2 do Rack
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

            // Tape 4 (Emerald Zen): Slot 3 do Rack
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

            // Registro das fitas no CassetteOrganizerRack
            CassetteOrganizerRack rackComp = rackInstance.GetComponent<CassetteOrganizerRack>();
            if (rackComp != null)
            {
                rackComp.AssignTapeToSlot(0, tapeCyan);
                rackComp.AssignTapeToSlot(1, tapeGoldInst.GetComponent<MusicCassetteTape>());
                rackComp.AssignTapeToSlot(2, tapeMagentaInst.GetComponent<MusicCassetteTape>());
                rackComp.AssignTapeToSlot(3, tapeEmeraldInst.GetComponent<MusicCassetteTape>());
            }

            // Lei 3: Aterramento Absoluto & Validação de Bounds
            Bounds deckBounds = GetCombinedBounds(deckInstance);
            Bounds rackBounds = GetCombinedBounds(rackInstance);
            bool deckRackOverlap = deckBounds.Intersects(rackBounds);
            float clearance = Vector3.Distance(deckBounds.center, rackBounds.center);

            if (log != null)
            {
                log.AppendLine($"  - [Layout Mesa 1] RadioCassetteDeck em {deckPos}");
                log.AppendLine($"  - [Layout Mesa 1] CassetteOrganizerRack em {rackPos}");
                log.AppendLine($"  - [Layout Mesa 1] Folga Deck vs Rack: {clearance:F3}m (Overlap: {deckRackOverlap})");
                log.AppendLine($"  - [Aterramento] Y do tampo: 0.952m | Deck Base Y: {deckBounds.min.y:F3}m | Rack Base Y: {rackBounds.min.y:F3}m");
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveOpenScenes();
            if (log != null) log.AppendLine("  - Cena Menu_Principal salva com sucesso!");
        }

        private static Bounds GetCombinedBounds(GameObject go)
        {
            Renderer[] rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        public static string CaptureVisualQA()
        {
            var log = new System.Text.StringBuilder();
            string outDir = Path.GetFullPath(OutVisualQADir);
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            // 1. TASK-06_View1_Front.png (Visão frontal do paciente visualizando a Mesa 1, rádio vintage, deck e rack)
            string pathView1 = Path.Combine(outDir, "TASK-06_View1_Front.png");
            Vector3 camPos1 = new Vector3(1.75f, 1.35f, 0.10f);
            Vector3 lookAt1 = new Vector3(0.66f, 0.98f, 0.14f);
            VisualQACaptureEditor.CaptureCameraView(pathView1, camPos1, lookAt1, 1920, 1080);
            log.AppendLine($"  - View 1 Frontal gerada: {pathView1}");

            // 2. TASK-06_View2_Side_Grounding.png (Visão lateral demonstrando aterramento perfeito sobre o tampo)
            string pathView2 = Path.Combine(outDir, "TASK-06_View2_Side_Grounding.png");
            Vector3 camPos2 = new Vector3(0.68f, 1.04f, -0.85f);
            Vector3 lookAt2 = new Vector3(0.68f, 0.97f, 0.14f);
            VisualQACaptureEditor.CaptureCameraView(pathView2, camPos2, lookAt2, 1920, 1080);
            log.AppendLine($"  - View 2 Side Grounding gerada: {pathView2}");

            // 3. TASK-06_View3_Closeup.png (Close-up macro no Deck com fita inserida, carretéis e botão Eject)
            string pathView3 = Path.Combine(outDir, "TASK-06_View3_Closeup.png");
            Vector3 camPos3 = new Vector3(0.95f, 1.15f, 0.14f);
            Vector3 lookAt3 = new Vector3(0.66f, 1.01f, 0.14f);
            VisualQACaptureEditor.CaptureCameraView(pathView3, camPos3, lookAt3, 1920, 1080);
            log.AppendLine($"  - View 3 Closeup Macro gerada: {pathView3}");

            // 4. TASK-06_Radio_Deck_Overview.png
            string pathOverview = Path.Combine(outDir, "TASK-06_Radio_Deck_Overview.png");
            Vector3 camPosOver = new Vector3(1.80f, 1.45f, 0.25f);
            Vector3 lookAtOver = new Vector3(0.66f, 0.98f, 0.14f);
            VisualQACaptureEditor.CaptureCameraView(pathOverview, camPosOver, lookAtOver, 1920, 1080);
            log.AppendLine($"  - Overview Deck gerado: {pathOverview}");

            // 5. TASK-06_Cassette_Tapes_Closeup.png
            string pathRackClose = Path.Combine(outDir, "TASK-06_Cassette_Tapes_Closeup.png");
            Vector3 camPosRack = new Vector3(0.98f, 1.15f, -0.08f);
            Vector3 lookAtRack = new Vector3(0.70f, 1.01f, -0.08f);
            VisualQACaptureEditor.CaptureCameraView(pathRackClose, camPosRack, lookAtRack, 1920, 1080);
            log.AppendLine($"  - Closeup Rack gerado: {pathRackClose}");

            // 6. TASK-06_Tape_Inserted_Slot.png
            string pathSlot = Path.Combine(outDir, "TASK-06_Tape_Inserted_Slot.png");
            Vector3 camPosSlot = new Vector3(0.92f, 1.12f, 0.15f);
            Vector3 lookAtSlot = new Vector3(0.66f, 1.02f, 0.14f);
            VisualQACaptureEditor.CaptureCameraView(pathSlot, camPosSlot, lookAtSlot, 1920, 1080);
            log.AppendLine($"  - Closeup Slot gerado: {pathSlot}");

            // 7. TASK-06_Eject_Sequence.png (Captura da fita ejetada repousando sobre a mesa)
            GameObject tapeCyan = GameObject.Find("Tape_CyberHorizon_Active");
            GameObject deckInst = GameObject.Find("RadioCassetteDeck_Instance");
            if (tapeCyan != null && deckInst != null)
            {
                Vector3 origPos = tapeCyan.transform.position;
                Quaternion origRot = tapeCyan.transform.rotation;
                var tapeComp = tapeCyan.GetComponent<MusicCassetteTape>();
                if (tapeComp != null) tapeComp.SetPlaying(false);

                // Posiciona a fita na frente do deck repousando flat na mesa (Y = 0.952m + meia espessura)
                Vector3 ejectedPos = deckInst.transform.position + deckInst.transform.forward * 0.16f;
                ejectedPos.y = 0.952f + 0.006f;
                Quaternion ejectedRot = Quaternion.Euler(90f, 75f, 0f);

                tapeCyan.transform.position = ejectedPos;
                tapeCyan.transform.rotation = ejectedRot;

                string pathEject = Path.Combine(outDir, "TASK-06_Eject_Sequence.png");
                Vector3 camPosEject = new Vector3(1.10f, 1.25f, 0.20f);
                Vector3 lookAtEject = new Vector3(0.70f, 0.98f, 0.16f);
                VisualQACaptureEditor.CaptureCameraView(pathEject, camPosEject, lookAtEject, 1920, 1080);
                log.AppendLine($"  - Eject Sequence gerada: {pathEject}");

                // Restaura o estado inicial da fita no socket
                tapeCyan.transform.position = origPos;
                tapeCyan.transform.rotation = origRot;
                if (tapeComp != null) tapeComp.SetPlaying(true);
            }

            return log.ToString();
        }
    }
}
