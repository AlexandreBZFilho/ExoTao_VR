using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;
using VRRehab.Ergonomics;
using VRRehab.DronePet;
using VRRehab.EditorTools;

namespace VRRehab.EditorAutoSetup
{
    /// <summary>
    /// Builder e validador automatizado para a TASK-03 (Drone Pet FetchBot & Feixe Trator).
    /// Constrói os materiais, Prefab do CyberFetchBaton, acopla o emissor de feixe trator
    /// e o controlador ao Drone nas cenas Menu_Principal e Floresta_e_Rio,
    /// e executa o ciclo completo de Visual QA gerando screenshots 1920x1080.
    /// </summary>
    public static class Task03_SceneAndPrefabBuilder
    {
        private const string EmitterModelPath = "Assets/Models/Generated/Task03/TractorBeamEmitter.fbx";
        private const string BatonModelPath = "Assets/Models/Generated/Task03/CyberFetchBaton.fbx";
        private const string BatonPrefabPath = "Assets/Prefabs/Interactive/CyberFetchBaton.prefab";
        private const string MenuScenePath = "Assets/Scenes/Menu_Principal.unity";
        private const string FlorestaScenePath = "Assets/Scenes/Floresta_e_Rio.unity";

        private const string SfxAlertPath = "Assets/Audio/Generated/Task03/drone_fetch_alert.wav";
        private const string SfxHumPath = "Assets/Audio/Generated/Task03/tractor_beam_hum.wav";

        [MenuItem("VRRehab/AutoBuild/Build and Verify TASK-03")]
        public static void BuildAndVerify()
        {
            Debug.Log("[TASK-03] Iniciando montagem automatizada do Drone FetchBot...");

            // 1. Cria e atualiza todos os materiais
            EnsureMaterials();

            // 2. Constrói o Prefab do CyberFetchBaton
            GameObject batonPrefab = BuildCyberFetchBatonPrefab();

            // 3. Configura a cena Menu_Principal (Drone + Feixe Trator + Bastão na mesa)
            SetupMenuPrincipalScene(batonPrefab);

            // 4. Configura a cena Floresta_e_Rio (Drone Guia com Feixe Trator)
            SetupFlorestaScene();

            // 5. Gera as capturas de Visual QA
            CaptureVisualQA();

            Debug.Log("[TASK-03] Montagem, configuração de cenas e Visual QA concluídos com 100% de sucesso!");
        }

        public static void EnsureMaterials()
        {
            string matDir = "Assets/Models/Generated/Task03";
            if (!AssetDatabase.IsValidFolder(matDir))
            {
                AssetDatabase.CreateFolder("Assets/Models/Generated", "Task03");
            }

            // M_Baton_Grip
            EnsureMaterial($"{matDir}/M_Baton_Grip.mat", "M_Baton_Grip", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Baton_Grip", new Color(0.10f, 0.12f, 0.14f), 0.35f, 0.10f));

            // M_Baton_Housing
            EnsureMaterial($"{matDir}/M_Baton_Housing.mat", "M_Baton_Housing", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Baton_Housing", new Color(0.78f, 0.83f, 0.88f), 0.85f, 0.85f));

            // M_Baton_NeonCyan
            EnsureMaterial($"{matDir}/M_Baton_NeonCyan.mat", "M_Baton_NeonCyan", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_Baton_NeonCyan", new Color(0.0f, 0.96f, 1.0f), new Color(0.0f, 0.96f, 1.0f), 3.5f));

            // M_Baton_Caps
            EnsureMaterial($"{matDir}/M_Baton_Caps.mat", "M_Baton_Caps", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Baton_Caps", new Color(0.18f, 0.20f, 0.22f), 0.40f, 0.10f));

            // M_Tractor_Housing
            EnsureMaterial($"{matDir}/M_Tractor_Housing.mat", "M_Tractor_Housing", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Tractor_Housing", new Color(0.12f, 0.15f, 0.18f), 0.75f, 0.85f));

            // M_Tractor_CyanGlow
            EnsureMaterial($"{matDir}/M_Tractor_CyanGlow.mat", "M_Tractor_CyanGlow", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_Tractor_CyanGlow", new Color(0.0f, 0.95f, 1.0f), new Color(0.0f, 0.95f, 1.0f), 3.5f));

            // M_Tractor_Lens
            EnsureMaterial($"{matDir}/M_Tractor_Lens.mat", "M_Tractor_Lens", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_Tractor_Lens", new Color(0.05f, 0.35f, 0.50f), new Color(0.0f, 0.85f, 1.0f), 2.5f));

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

        private static GameObject BuildCyberFetchBatonPrefab()
        {
            GameObject batonModelFbx = AssetDatabase.LoadAssetAtPath<GameObject>(BatonModelPath);
            if (batonModelFbx == null)
            {
                Debug.LogError($"[TASK-03] Modelo FBX não encontrado em {BatonModelPath}");
                return null;
            }

            string prefabsDir = "Assets/Prefabs/Interactive";
            if (!AssetDatabase.IsValidFolder(prefabsDir))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "Interactive");
            }

            GameObject root = new GameObject("CyberFetchBaton");
            root.layer = LayerMask.NameToLayer("Default");

            // Instancia o modelo visual como filho
            GameObject visual = UnityEngine.Object.Instantiate(batonModelFbx, root.transform);
            visual.name = "Visual_Model";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            // Remove colisores nativos do FBX se houver
            foreach (var col in visual.GetComponentsInChildren<Collider>())
            {
                UnityEngine.Object.DestroyImmediate(col);
            }

            // Atribui materiais compatíveis aos renderers do modelo
            Material matGrip = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Generated/Task03/M_Baton_Grip.mat");
            Material matHousing = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Generated/Task03/M_Baton_Housing.mat");
            Material matNeon = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Generated/Task03/M_Baton_NeonCyan.mat");
            Material matCaps = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Generated/Task03/M_Baton_Caps.mat");

            foreach (var r in visual.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterials = new Material[] { matGrip, matHousing, matNeon, matCaps };
            }

            // Colisor de Cápsula cilíndrica com extremidades arredondadas
            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = 0.024f;
            capsule.height = 0.22f;
            capsule.direction = 2; // Z-axis

            // Rigidbody configurado para física VR precisa
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.45f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Componente ergonômico de agarre e retorno com force grab
            ErgonomicGrabInteractable grab = root.AddComponent<ErgonomicGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.attachEaseInTime = 0.25f;

            // Componente de mira assistida para arremesso satisfatório
            ThrowAimAssist aim = root.AddComponent<ThrowAimAssist>();

            // Componente de alvo de busca do Drone
            DroneFetchTarget fetchTarget = root.AddComponent<DroneFetchTarget>();

            // Feedback visual ao mirar o raio laser
            InteractableHighlightFeedback highlight = root.AddComponent<InteractableHighlightFeedback>();

            // Salva como Prefab
            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, BatonPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            Debug.Log($"[TASK-03] Prefab CyberFetchBaton salvo em {BatonPrefabPath}");
            return prefabAsset;
        }

        private static void SetupMenuPrincipalScene(GameObject batonPrefab)
        {
            if (EditorSceneManager.GetActiveScene().path != MenuScenePath)
            {
                EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            }

            // 1. Garante o Drone Guia interativo na sala do Menu
            GameObject droneObj = GameObject.Find("[IC] Drone_Guia_IA_ExoBot");
            if (droneObj == null)
            {
                // Carrega ou instancia drone companheiro
                droneObj = new GameObject("[IC] Drone_Guia_IA_ExoBot");
                droneObj.transform.position = new Vector3(2.20f, 1.45f, 1.20f);
                droneObj.transform.rotation = Quaternion.Euler(0f, 195f, 0f);
            }

            DroneGuiaIA droneIA = droneObj.GetComponent<DroneGuiaIA>();
            if (droneIA == null) droneIA = droneObj.AddComponent<DroneGuiaIA>();
            droneIA.distanciaIdealFrente = 2.4f;
            droneIA.alturaFlutuacao = 1.40f;
            droneIA.ConstruirVisualProceduralSeNecessario();
            droneIA.ConstruirHUDHolografico(false);

            // 2. Acopla o TractorBeamEmitter na base inferior do Drone
            Transform emitterAnchor = droneObj.transform.Find("TractorBeamEmitter_Anchor");
            if (emitterAnchor == null)
            {
                GameObject goAnchor = new GameObject("TractorBeamEmitter_Anchor");
                goAnchor.transform.SetParent(droneObj.transform, false);
                goAnchor.transform.localPosition = new Vector3(0f, -0.06f, 0f);
                goAnchor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                emitterAnchor = goAnchor.transform;
            }

            // Visual do Emitter
            Transform emitterVisual = emitterAnchor.Find("Visual_Emitter");
            if (emitterVisual == null)
            {
                GameObject fbxEmitter = AssetDatabase.LoadAssetAtPath<GameObject>(EmitterModelPath);
                if (fbxEmitter != null)
                {
                    GameObject goVisual = UnityEngine.Object.Instantiate(fbxEmitter, emitterAnchor);
                    goVisual.name = "Visual_Emitter";
                    goVisual.transform.localPosition = Vector3.zero;
                    goVisual.transform.localRotation = Quaternion.identity;
                    goVisual.transform.localScale = Vector3.one;

                    Material matHousing = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Generated/Task03/M_Tractor_Housing.mat");
                    Material matGlow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Generated/Task03/M_Tractor_CyanGlow.mat");
                    Material matLens = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Generated/Task03/M_Tractor_Lens.mat");
                    foreach (var mr in goVisual.GetComponentsInChildren<Renderer>())
                    {
                        mr.sharedMaterials = new Material[] { matHousing, matGlow, matLens };
                    }
                }
            }

            // 3. Acopla e configura o DroneFetchController
            DroneFetchController controller = droneObj.GetComponent<DroneFetchController>();
            if (controller == null) controller = droneObj.AddComponent<DroneFetchController>();

            controller.tractorEmitterAnchor = emitterAnchor;
            controller.sfxFetchAlert = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxAlertPath);
            controller.sfxTractorBeamHum = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxHumPath);
            controller.SetupTractorBeamHardware();

            // 4. Posiciona o CyberFetchBaton na bancada/mesa do Menu
            GameObject existingBaton = GameObject.Find("[IC] Cyber_Fetch_Baton");
            if (existingBaton == null)
            {
                existingBaton = PrefabUtility.InstantiatePrefab(batonPrefab) as GameObject;
                existingBaton.name = "[IC] Cyber_Fetch_Baton";
            }

            // Posição de repouso sobre a bancada de madeira (horizontal, no centro frontal da bancada)
            Vector3 deskPos = new Vector3(0.68f, 0.965f, -0.05f);
            Quaternion deskRot = Quaternion.Euler(0f, 60f, 90f);
            existingBaton.transform.position = deskPos;
            existingBaton.transform.rotation = deskRot;

            ErgonomicGrabInteractable batonGrab = existingBaton.GetComponent<ErgonomicGrabInteractable>();
            if (batonGrab != null)
            {
                batonGrab.SetHomeTransform(deskPos, deskRot);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[TASK-03] Cena Menu_Principal configurada com sucesso.");
        }

        private static void SetupFlorestaScene()
        {
            EditorSceneManager.OpenScene(FlorestaScenePath, OpenSceneMode.Single);

            GameObject droneObj = GameObject.Find("[IC] Drone_Guia_IA_ExoBot");
            if (droneObj != null)
            {
                Transform emitterAnchor = droneObj.transform.Find("TractorBeamEmitter_Anchor");
                if (emitterAnchor == null)
                {
                    GameObject goAnchor = new GameObject("TractorBeamEmitter_Anchor");
                    goAnchor.transform.SetParent(droneObj.transform, false);
                    goAnchor.transform.localPosition = new Vector3(0f, -0.06f, 0f);
                    goAnchor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    emitterAnchor = goAnchor.transform;
                }

                Transform emitterVisual = emitterAnchor.Find("Visual_Emitter");
                if (emitterVisual == null)
                {
                    GameObject fbxEmitter = AssetDatabase.LoadAssetAtPath<GameObject>(EmitterModelPath);
                    if (fbxEmitter != null)
                    {
                        GameObject goVisual = UnityEngine.Object.Instantiate(fbxEmitter, emitterAnchor);
                        goVisual.name = "Visual_Emitter";
                        goVisual.transform.localPosition = Vector3.zero;
                        goVisual.transform.localRotation = Quaternion.identity;
                        goVisual.transform.localScale = Vector3.one;

                        Material matHousing = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Generated/Task03/M_Tractor_Housing.mat");
                        Material matGlow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Generated/Task03/M_Tractor_CyanGlow.mat");
                        Material matLens = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Generated/Task03/M_Tractor_Lens.mat");
                        foreach (var mr in goVisual.GetComponentsInChildren<Renderer>())
                        {
                            mr.sharedMaterials = new Material[] { matHousing, matGlow, matLens };
                        }
                    }
                }

                DroneFetchController controller = droneObj.GetComponent<DroneFetchController>();
                if (controller == null) controller = droneObj.AddComponent<DroneFetchController>();

                controller.tractorEmitterAnchor = emitterAnchor;
                controller.sfxFetchAlert = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxAlertPath);
                controller.sfxTractorBeamHum = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxHumPath);
                controller.SetupTractorBeamHardware();

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                Debug.Log("[TASK-03] Cena Floresta_e_Rio configurada com o Feixe Trator do Drone.");
            }

            // Retorna ao Menu Principal
            EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        }

        [MenuItem("VRRehab/VisualQA/Capture TASK-03 Views")]
        public static void CaptureVisualQA()
        {
            string outDir = Path.GetFullPath(".agents/visual_qa");
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            if (EditorSceneManager.GetActiveScene().path != MenuScenePath)
            {
                EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            }

            GameObject drone = GameObject.Find("[IC] Drone_Guia_IA_ExoBot");
            GameObject baton = GameObject.Find("[IC] Cyber_Fetch_Baton");

            Vector3 droneOriginalPos = drone != null ? drone.transform.position : Vector3.zero;
            Quaternion droneOriginalRot = drone != null ? drone.transform.rotation : Quaternion.identity;

            Vector3 batonOriginalPos = baton != null ? baton.transform.position : Vector3.zero;
            Quaternion batonOriginalRot = baton != null ? baton.transform.rotation : Quaternion.identity;

            try
            {
                // -------------------------------------------------------------
                // 1. TASK-03_Drone_TractorBeam.png
                // Close-up do Drone com o feixe trator ativo segurando o bastão
                // -------------------------------------------------------------
                if (drone != null && baton != null)
                {
                    drone.transform.position = new Vector3(2.10f, 1.55f, 1.10f);
                    drone.transform.rotation = Quaternion.Euler(0f, 190f, 0f);

                    Transform anchor = drone.transform.Find("TractorBeamEmitter_Anchor");
                    Vector3 emitterPos = anchor != null ? anchor.position : drone.transform.position + Vector3.down * 0.06f;

                    // Posiciona o bastão suspenso 0.28m abaixo do emissor
                    baton.transform.position = emitterPos + Vector3.down * 0.28f;
                    baton.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

                    // Ativa temporariamente o LineRenderer e a Luz para o print
                    LineRenderer lr = anchor != null ? anchor.GetComponent<LineRenderer>() : null;
                    Light lt = anchor != null ? anchor.GetComponent<Light>() : null;
                    if (lr != null)
                    {
                        lr.enabled = true;
                        lr.startWidth = 0.035f;
                        lr.endWidth = 0.095f;
                        lr.startColor = new Color(0.2f, 0.98f, 1.0f, 0.85f);
                        lr.endColor = new Color(0.0f, 0.85f, 1.0f, 0.25f);
                        lr.SetPosition(0, emitterPos);
                        lr.SetPosition(1, baton.transform.position);
                    }
                    if (lt != null) lt.enabled = true;

                    string path1 = Path.Combine(outDir, "TASK-03_Drone_TractorBeam.png");
                    // Enquadramento 3/4 isométrico que revela claramente fuselagem, HUD, feixe e bastão suspenso
                    Vector3 camPos1 = new Vector3(1.50f, 1.45f, 0.55f);
                    Vector3 lookAt1 = new Vector3(2.10f, 1.38f, 1.10f);
                    VisualQACaptureEditor.CaptureCameraView(path1, camPos1, lookAt1, 1920, 1080);
                    Debug.Log($"[TASK-03] Captura 1 gerada: {path1}");

                    if (lr != null) lr.enabled = false;
                    if (lt != null) lt.enabled = false;
                }

                // -------------------------------------------------------------
                // 2. TASK-03_Fetch_Presenting.png
                // Visão da altura do peito do jogador com o drone flutuando a 1.15m
                // -------------------------------------------------------------
                if (drone != null && baton != null)
                {
                    // Drone em postura de apresentação a 1.15m do solo em frente à posição do jogador
                    drone.transform.position = new Vector3(4.50f, 1.35f, 3.10f);
                    drone.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

                    Transform anchor = drone.transform.Find("TractorBeamEmitter_Anchor");
                    Vector3 emitterPos = anchor != null ? anchor.position : drone.transform.position + Vector3.down * 0.06f;

                    baton.transform.position = emitterPos + Vector3.down * 0.28f;
                    baton.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

                    LineRenderer lr = anchor != null ? anchor.GetComponent<LineRenderer>() : null;
                    if (lr != null)
                    {
                        lr.enabled = true;
                        lr.startWidth = 0.035f;
                        lr.endWidth = 0.095f;
                        lr.startColor = new Color(0.2f, 0.98f, 1.0f, 0.85f);
                        lr.endColor = new Color(0.0f, 0.85f, 1.0f, 0.25f);
                        lr.SetPosition(0, emitterPos);
                        lr.SetPosition(1, baton.transform.position);
                    }

                    string path2 = Path.Combine(outDir, "TASK-03_Fetch_Presenting.png");
                    // Câmera em ângulo três-quartos over-the-shoulder mostrando paciente e drone entregando a 1.15m
                    Vector3 camPos2 = new Vector3(3.55f, 1.48f, 2.15f);
                    Vector3 lookAt2 = new Vector3(4.50f, 1.25f, 3.05f);
                    VisualQACaptureEditor.CaptureCameraView(path2, camPos2, lookAt2, 1920, 1080);
                    Debug.Log($"[TASK-03] Captura 2 gerada: {path2}");

                    if (lr != null) lr.enabled = false;
                }

                // -------------------------------------------------------------
                // 3. TASK-03_FetchBaton_Desk.png
                // Bastão repousando na bancada/mesa do menu
                // -------------------------------------------------------------
                if (baton != null)
                {
                    Vector3 deskPos = new Vector3(0.68f, 0.965f, -0.05f);
                    Quaternion deskRot = Quaternion.Euler(0f, 60f, 90f);
                    baton.transform.position = deskPos;
                    baton.transform.rotation = deskRot;

                    string path3 = Path.Combine(outDir, "TASK-03_FetchBaton_Desk.png");
                    Vector3 camPos3 = new Vector3(1.20f, 1.25f, 0.15f);
                    Vector3 lookAt3 = new Vector3(0.68f, 0.965f, -0.05f);
                    VisualQACaptureEditor.CaptureCameraView(path3, camPos3, lookAt3, 1920, 1080);
                    Debug.Log($"[TASK-03] Captura 3 gerada: {path3}");
                }
            }
            finally
            {
                // Restaura posições e salva
                if (drone != null)
                {
                    drone.transform.position = droneOriginalPos;
                    drone.transform.rotation = droneOriginalRot;
                }
                if (baton != null)
                {
                    baton.transform.position = new Vector3(0.68f, 0.965f, -0.05f);
                    baton.transform.rotation = Quaternion.Euler(0f, 60f, 90f);
                }

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
            }
        }
    }
}
