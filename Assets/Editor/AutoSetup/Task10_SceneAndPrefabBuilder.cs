using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;
using VRRehab.Interactive.Task10;
using VRRehab.EditorTools;

namespace VRRehab.EditorAutoSetup
{
    /// <summary>
    /// Builder e validador automatizado para a TASK-10 (Seletor de Mapas por Esferas de Portal 3D com Mini-Dioramas e Pedestal).
    /// Constrói os prefabs PortalOrbDiorama_Forest e PortalPedestalSocket, configura materiais e shaders compatíveis,
    /// instancia no Menu Principal ao alcance ergonômico do paciente e captura os prints de Visual QA.
    /// </summary>
    public static class Task10_SceneAndPrefabBuilder
    {
        private const string ModelsDir = "Assets/Models/Generated/Task10";
        private const string PrefabsDir = "Assets/Prefabs/Interactive";
        private const string AudioDir = "Assets/Audio/Generated/Task10";
        private const string MenuScenePath = "Assets/Scenes/Menu_Principal.unity";
        private const string OutVisualQADir = ".agents/visual_qa";

        private const string PedestalModelPath = "Assets/Models/Generated/Task10/PortalPedestalSocket.fbx";
        private const string OrbModelPath = "Assets/Models/Generated/Task10/PortalOrbDiorama_Forest.fbx";

        private const string SfxOrbHumPath = "Assets/Audio/Generated/Task10/portal_orb_hum.wav";
        private const string SfxSocketSnapPath = "Assets/Audio/Generated/Task10/portal_socket_snap.wav";
        private const string SfxTeleportWhooshPath = "Assets/Audio/Generated/Task10/portal_teleport_whoosh.wav";
        private const string SfxHeadsetProximityPath = "Assets/Audio/Generated/Task10/portal_headset_proximity.wav";

        [MenuItem("VRRehab/AutoBuild/Build and Verify TASK-10")]
        public static void BuildAndVerify()
        {
            Debug.Log("[TASK-10] Iniciando construção completa do Seletor de Mapas por Esferas de Portal 3D...");

            // 1. Reimporta modelos e áudios
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(PedestalModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(OrbModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxOrbHumPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxSocketSnapPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxTeleportWhooshPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxHeadsetProximityPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            // 2. Limpa prefabs antigos para recriação limpa
            string[] prefabsToDelete = new string[] {
                $"{PrefabsDir}/PortalOrbDiorama_Forest.prefab",
                $"{PrefabsDir}/PortalPedestalSocket.prefab"
            };
            foreach (var p in prefabsToDelete)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(p) != null)
                {
                    AssetDatabase.DeleteAsset(p);
                }
            }

            // 3. Garante materiais perfeitos
            EnsureMaterials();

            // 4. Constrói prefabs
            GameObject prefabOrb = BuildOrbPrefab();
            GameObject prefabPedestal = BuildPedestalPrefab();

            // 5. Configura e posiciona na cena Menu_Principal
            SetupMenuPrincipalScene(prefabPedestal, prefabOrb);

            // 6. Executa captura de Visual QA
            CaptureVisualQA();

            Debug.Log("[TASK-10] Conclusão com sucesso de todos os estágios do Seletor de Mapas por Esferas de Portal!");
        }

        public static void EnsureMaterials()
        {
            if (!AssetDatabase.IsValidFolder(ModelsDir))
            {
                AssetDatabase.CreateFolder("Assets/Models/Generated", "Task10");
            }

            // Redoma de vidro translúcido / transparente
            EnsureMaterial($"{ModelsDir}/M_Orb_Glass.mat", "M_Orb_Glass", () =>
            {
                Material mat = MaterialShaderHelper.CreateTransparentMaterial("M_Orb_Glass", new Color(0.85f, 0.95f, 1.0f), 0.28f);
                MaterialShaderHelper.SetSmoothness(mat, 0.95f);
                MaterialShaderHelper.SetMetallic(mat, 0.05f);
                return mat;
            });

            // Materiais do diorama interno
            EnsureMaterial($"{ModelsDir}/M_Orb_Terrain.mat", "M_Orb_Terrain", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Orb_Terrain", new Color(0.22f, 0.58f, 0.24f), 0.25f, 0.0f));

            EnsureMaterial($"{ModelsDir}/M_Orb_River.mat", "M_Orb_River", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_Orb_River", new Color(0.05f, 0.65f, 0.95f), new Color(0.02f, 0.35f, 0.60f), 1.2f));

            EnsureMaterial($"{ModelsDir}/M_Orb_PineTrunk.mat", "M_Orb_PineTrunk", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Orb_PineTrunk", new Color(0.38f, 0.24f, 0.14f), 0.15f, 0.0f));

            EnsureMaterial($"{ModelsDir}/M_Orb_PineFoliage.mat", "M_Orb_PineFoliage", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Orb_PineFoliage", new Color(0.12f, 0.42f, 0.18f), 0.20f, 0.0f));

            EnsureMaterial($"{ModelsDir}/M_Orb_Apple.mat", "M_Orb_Apple", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Orb_Apple", new Color(0.92f, 0.12f, 0.14f), 0.85f, 0.10f));

            // Materiais do Pedestal
            EnsureMaterial($"{ModelsDir}/M_Pedestal_Chassis.mat", "M_Pedestal_Chassis", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Pedestal_Chassis", new Color(0.14f, 0.16f, 0.19f), 0.65f, 0.70f));

            EnsureMaterial($"{ModelsDir}/M_Pedestal_Trim.mat", "M_Pedestal_Trim", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Pedestal_Trim", new Color(0.82f, 0.85f, 0.88f), 0.90f, 0.85f));

            EnsureMaterial($"{ModelsDir}/M_Pedestal_RingEmissive.mat", "M_Pedestal_RingEmissive", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_Pedestal_RingEmissive", new Color(0.0f, 0.90f, 1.0f), new Color(0.0f, 0.90f, 1.0f), 2.8f));

            EnsureMaterial($"{ModelsDir}/M_Pedestal_CradlePad.mat", "M_Pedestal_CradlePad", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Pedestal_CradlePad", new Color(0.08f, 0.08f, 0.10f), 0.15f, 0.05f));

            EnsureMaterial($"{ModelsDir}/M_Pedestal_Beam.mat", "M_Pedestal_Beam", () =>
                MaterialShaderHelper.CreateBeamGlowMaterial("M_Pedestal_Beam", new Color(0.1f, 0.9f, 1.0f, 0.8f)));
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

        private static GameObject BuildOrbPrefab()
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(OrbModelPath);
            if (fbx == null)
            {
                Debug.LogError($"[TASK-10] Modelo FBX da Esfera de Portal não encontrado em {OrbModelPath}");
                return null;
            }

            GameObject root = new GameObject("PortalOrbDiorama_Forest");
            root.layer = LayerMask.NameToLayer("Default");
            root.tag = "Interactable";

            GameObject visual = UnityEngine.Object.Instantiate(fbx, root.transform);
            visual.name = "Visual_Model";

            Material matGlass = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Orb_Glass.mat");
            Material matTerrain = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Orb_Terrain.mat");
            Material matRiver = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Orb_River.mat");
            Material matTrunk = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Orb_PineTrunk.mat");
            Material matFoliage = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Orb_PineFoliage.mat");
            Material matApple = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Orb_Apple.mat");

            Transform innerDioramaTform = null;

            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = mr.gameObject.name;
                if (n.Contains("Dome") || n.Contains("Glass"))
                {
                    mr.sharedMaterial = matGlass;
                }
                else if (n.Contains("Diorama"))
                {
                    innerDioramaTform = mr.transform;
                    mr.sharedMaterials = new Material[] { matTerrain, matRiver, matTrunk, matFoliage, matApple };
                }
            }

            // Colisor esférico principal
            SphereCollider col = root.AddComponent<SphereCollider>();
            col.radius = 0.090f;
            col.center = Vector3.zero;

            // Física com massa leve e contínua
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.45f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Fontes de áudio
            AudioSource audioAmbient = root.AddComponent<AudioSource>();
            audioAmbient.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxOrbHumPath);
            audioAmbient.playOnAwake = false;
            audioAmbient.loop = true;
            audioAmbient.volume = 0.45f;
            audioAmbient.spatialBlend = 1.0f;
            audioAmbient.minDistance = 0.4f;
            audioAmbient.maxDistance = 4.0f;

            AudioSource audioProximity = root.AddComponent<AudioSource>();
            audioProximity.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxHeadsetProximityPath);
            audioProximity.playOnAwake = false;
            audioProximity.loop = true;
            audioProximity.volume = 0.0f;
            audioProximity.spatialBlend = 1.0f;
            audioProximity.minDistance = 0.15f;
            audioProximity.maxDistance = 2.5f;

            // Componente interativo principal
            PortalWorldSphere orbComp = root.AddComponent<PortalWorldSphere>();
            SerializedObject soOrb = new SerializedObject(orbComp);
            soOrb.FindProperty("targetSceneName").stringValue = "Floresta_e_Rio";
            soOrb.FindProperty("innerDioramaTransform").objectReferenceValue = innerDioramaTform;
            soOrb.FindProperty("dioramaRotationSpeed").floatValue = 20.0f;
            soOrb.FindProperty("proximityNoticeDistance").floatValue = 0.50f;
            soOrb.FindProperty("proximityTriggerDistance").floatValue = 0.22f;
            soOrb.FindProperty("ambientAudioSource").objectReferenceValue = audioAmbient;
            soOrb.FindProperty("proximityAudioSource").objectReferenceValue = audioProximity;
            soOrb.FindProperty("sfxTeleportWhoosh").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxTeleportWhooshPath);
            soOrb.FindProperty("enableAutoRespawn").boolValue = true;
            soOrb.FindProperty("respawnFloorThresholdY").floatValue = 0.25f;
            soOrb.FindProperty("respawnMaxDistance").floatValue = 4.5f;
            soOrb.FindProperty("returnGlideDuration").floatValue = 0.85f;
            soOrb.ApplyModifiedPropertiesWithoutUndo();

            // Feedback tátil e visual ao passar o raio laser
            root.AddComponent<InteractableHighlightFeedback>();

            if (!AssetDatabase.IsValidFolder(PrefabsDir))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
                AssetDatabase.CreateFolder("Assets/Prefabs", "Interactive");
            }

            string savePath = $"{PrefabsDir}/PortalOrbDiorama_Forest.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log($"[TASK-10] Prefab PortalOrbDiorama_Forest salvo com sucesso em {savePath}");
            return savedPrefab;
        }

        private static GameObject BuildPedestalPrefab()
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(PedestalModelPath);
            if (fbx == null)
            {
                Debug.LogError($"[TASK-10] Modelo FBX do Pedestal não encontrado em {PedestalModelPath}");
                return null;
            }

            GameObject root = new GameObject("PortalPedestalSocket");
            root.layer = LayerMask.NameToLayer("Default");

            GameObject visual = UnityEngine.Object.Instantiate(fbx, root.transform);
            visual.name = "Visual_Model";

            Material matChassis = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Pedestal_Chassis.mat");
            Material matTrim = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Pedestal_Trim.mat");
            Material matRing = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Pedestal_RingEmissive.mat");
            Material matCradle = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Pedestal_CradlePad.mat");
            Material matBeam = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Pedestal_Beam.mat");

            Renderer ringRenderer = null;
            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.gameObject.name.Contains("Ring"))
                {
                    mr.sharedMaterial = matRing;
                    ringRenderer = mr;
                }
                else
                {
                    mr.sharedMaterials = new Material[] { matChassis, matTrim, matCradle };
                }
            }

            // Colisores sólidos da coluna pedestal
            BoxCollider baseCol = root.AddComponent<BoxCollider>();
            baseCol.center = new Vector3(0f, 0.07f, 0f);
            baseCol.size = new Vector3(0.48f, 0.14f, 0.48f);

            CapsuleCollider shaftCol = root.AddComponent<CapsuleCollider>();
            shaftCol.center = new Vector3(0f, 0.44f, 0f);
            shaftCol.radius = 0.12f;
            shaftCol.height = 0.62f;

            BoxCollider headCol = root.AddComponent<BoxCollider>();
            headCol.center = new Vector3(0f, 0.81f, 0f);
            headCol.size = new Vector3(0.33f, 0.14f, 0.33f);

            // Ancoragem de encaixe magnético (Attach Point)
            GameObject attachGo = new GameObject("Socket_Attach_Point");
            attachGo.transform.SetParent(root.transform, false);
            attachGo.transform.localPosition = new Vector3(0f, 0.93f, 0f);

            // Zona de disparo do trigger do socket
            SphereCollider socketTriggerCol = root.AddComponent<SphereCollider>();
            socketTriggerCol.isTrigger = true;
            socketTriggerCol.center = new Vector3(0f, 0.90f, 0f);
            socketTriggerCol.radius = 0.14f;

            // Luz suave do berço
            GameObject lightGo = new GameObject("Socket_Glow_Light");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.93f, 0f);
            Light glowLight = lightGo.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.range = 2.2f;
            glowLight.intensity = 0.8f;
            glowLight.color = new Color(0.0f, 0.85f, 1.0f);

            // Sistema de partículas de feixe vertical ascendente
            GameObject beamGo = new GameObject("Vertical_Beam_Particles");
            beamGo.transform.SetParent(root.transform, false);
            beamGo.transform.localPosition = new Vector3(0f, 0.88f, 0f);
            beamGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // Aponta para cima (+Y)
            ParticleSystem psBeam = beamGo.AddComponent<ParticleSystem>();

            var main = psBeam.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1.0f;
            main.startLifetime = 1.0f;
            main.startSpeed = 1.6f;
            main.startSize = 0.05f;
            main.gravityModifier = -0.05f;
            main.startColor = new Color(0.1f, 0.9f, 1.0f, 0.85f);

            var em = psBeam.emission;
            em.rateOverTime = 35;

            var shape = psBeam.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.11f;

            var rendBeam = beamGo.GetComponent<ParticleSystemRenderer>();
            if (rendBeam != null && matBeam != null)
            {
                rendBeam.sharedMaterial = matBeam;
            }

            // Áudio do socket
            AudioSource audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1.0f;
            audio.minDistance = 0.5f;
            audio.maxDistance = 5.0f;

            // Componente PortalPedestalSocket
            PortalPedestalSocket socketComp = root.AddComponent<PortalPedestalSocket>();
            socketComp.attachTransform = attachGo.transform;
            socketComp.showInteractableHoverMeshes = false;

            SerializedObject soSocket = new SerializedObject(socketComp);
            var pSocketAttach = soSocket.FindProperty("socketAttachPoint");
            if (pSocketAttach != null) pSocketAttach.objectReferenceValue = attachGo.transform;
            var pRing = soSocket.FindProperty("emissiveRingRenderer");
            if (pRing != null) pRing.objectReferenceValue = ringRenderer;
            var pLight = soSocket.FindProperty("socketGlowLight");
            if (pLight != null) pLight.objectReferenceValue = glowLight;
            var pBeam = soSocket.FindProperty("verticalBeamParticles");
            if (pBeam != null) pBeam.objectReferenceValue = psBeam;
            var pAudio = soSocket.FindProperty("socketAudioSource");
            if (pAudio != null) pAudio.objectReferenceValue = audio;
            var pSnap = soSocket.FindProperty("sfxSocketSnap");
            if (pSnap != null) pSnap.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxSocketSnapPath);
            var pDuration = soSocket.FindProperty("celebrationDuration");
            if (pDuration != null) pDuration.floatValue = 1.2f;
            soSocket.ApplyModifiedPropertiesWithoutUndo();

            string savePath = $"{PrefabsDir}/PortalPedestalSocket.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log($"[TASK-10] Prefab PortalPedestalSocket salvo com sucesso em {savePath}");
            return savedPrefab;
        }

        public static void SetupMenuPrincipalScene(GameObject prefabPedestal, GameObject prefabOrb)
        {
            if (EditorSceneManager.GetActiveScene().path != MenuScenePath)
            {
                EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            }

            // Remove instâncias antigas para remontagem limpa
            string[] oldInstances = new string[] {
                "PortalPedestalSocket_Instance",
                "PortalOrbDiorama_Forest_Instance"
            };
            foreach (var n in oldInstances)
            {
                GameObject old = GameObject.Find(n);
                if (old != null) UnityEngine.Object.DestroyImmediate(old);
            }

            // Posição ergonômica: à frente e à direita do paciente (4.50, 0, 2.31),
            // assentado sobre o piso em Y=0.00 e alinhado com o painel de Mapas Exploráveis
            Vector3 pedestalPos = new Vector3(5.20f, 0.00f, 2.90f);
            Quaternion pedestalRot = Quaternion.identity;

            GameObject pedInst = (GameObject)PrefabUtility.InstantiatePrefab(prefabPedestal);
            pedInst.name = "PortalPedestalSocket_Instance";
            pedInst.transform.position = pedestalPos;
            pedInst.transform.rotation = pedestalRot;

            // Esfera assentada no berço superior do pedestal
            Vector3 orbPos = new Vector3(5.20f, 0.93f, 2.90f);
            Quaternion orbRot = Quaternion.identity;

            GameObject orbInst = (GameObject)PrefabUtility.InstantiatePrefab(prefabOrb);
            orbInst.name = "PortalOrbDiorama_Forest_Instance";
            orbInst.transform.position = orbPos;
            orbInst.transform.rotation = orbRot;

            // Define âncora de Home para o Auto-Respawn da esfera
            PortalWorldSphere orbComp = orbInst.GetComponent<PortalWorldSphere>();
            if (orbComp != null)
            {
                orbComp.SetHomeTransform(orbPos, orbRot);
                orbComp.SetSocketedState(true);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[TASK-10] Pedestal e Esfera instanciados e salvos com sucesso na cena Menu_Principal!");
        }

        public static void CaptureVisualQA()
        {
            string outDir = Path.GetFullPath(OutVisualQADir);
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            if (EditorSceneManager.GetActiveScene().path != MenuScenePath)
            {
                EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            }

            GameObject pedGo = GameObject.Find("PortalPedestalSocket_Instance");
            GameObject orbGo = GameObject.Find("PortalOrbDiorama_Forest_Instance");

            // 1. TASK-10_Pedestal_Overview.png: Visão geral a partir da perspectiva do jogador na cadeira VR
            string pathOverview = Path.Combine(outDir, "TASK-10_Pedestal_Overview.png");
            Vector3 playerEyePos = new Vector3(4.50f, 1.67f, 2.31f);
            Vector3 overviewLookAt = new Vector3(5.20f, 0.70f, 2.90f);
            VisualQACaptureEditor.CaptureCameraView(pathOverview, playerEyePos, overviewLookAt, 1920, 1080);
            Debug.Log($"[TASK-10] Visual QA 1 capturado: {pathOverview}");

            // 2. TASK-10_Orb_Diorama_Closeup.png: Macro close-up em alta definição focado na transparência da redoma e detalhes da floresta
            string pathCloseup = Path.Combine(outDir, "TASK-10_Orb_Diorama_Closeup.png");
            Vector3 closeupCamPos = new Vector3(5.20f, 1.15f, 2.62f);
            Vector3 orbCenter = new Vector3(5.20f, 0.93f, 2.90f);
            VisualQACaptureEditor.CaptureCameraView(pathCloseup, closeupCamPos, orbCenter, 1920, 1080);
            Debug.Log($"[TASK-10] Visual QA 2 capturado: {pathCloseup}");

            // 3. TASK-10_Orb_Socket_Docked.png: Vista isométrica/45 graus com anel emissivo aceso e encaixe perfeito no berço
            PortalPedestalSocket socketComp = pedGo != null ? pedGo.GetComponent<PortalPedestalSocket>() : null;
            if (socketComp != null)
            {
                socketComp.ApplyRingVisuals(new Color(0.0f, 0.90f, 1.0f), 3.5f, 1.8f);
            }

            string pathDocked = Path.Combine(outDir, "TASK-10_Orb_Socket_Docked.png");
            Vector3 dockedCamPos = new Vector3(4.75f, 1.25f, 2.45f);
            Vector3 socketTarget = new Vector3(5.20f, 0.82f, 2.90f);
            VisualQACaptureEditor.CaptureCameraView(pathDocked, dockedCamPos, socketTarget, 1920, 1080);
            Debug.Log($"[TASK-10] Visual QA 3 capturado: {pathDocked}");

            if (socketComp != null)
            {
                socketComp.ApplyRingVisuals(new Color(0.0f, 0.70f, 0.95f), 1.8f, 0.6f);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }
    }
}
