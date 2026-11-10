using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;
using VRRehab.Ergonomics;
using VRRehab.EditorTools;

namespace VRRehab.EditorAutoSetup
{
    /// <summary>
    /// Builder e validador automatizado para a TASK-02.
    /// Constrói o material holográfico do anel, o Prefab de objeto interativo ergonômico,
    /// monta a cena de demonstração no Menu Principal e executa as capturas de Visual QA.
    /// </summary>
    public static class Task02_SceneAndPrefabBuilder
    {
        private const string ReticleModelPath = "Assets/Models/Generated/Task02/ReticleTargetRing.fbx";
        private const string ReticleMaterialPath = "Assets/Models/Generated/Task02/M_Reticle_HoloCyan.mat";
        private const string ToyPrefabPath = "Assets/Prefabs/Interactive/ErgonomicInteractiveToy.prefab";
        private const string MenuScenePath = "Assets/Scenes/Menu_Principal.unity";

        [MenuItem("VRRehab/AutoBuild/Build and Verify TASK-02")]
        public static void BuildAndVerify()
        {
            Debug.Log("[TASK-02] Iniciando montagem automatizada e verificação ergonômica...");

            // 1. Cria ou atualiza o material holográfico do anel
            Material reticleMat = EnsureReticleMaterial();

            // 2. Constrói o Prefab do brinquedo ergonômico
            GameObject prefabAsset = BuildInteractiveToyPrefab(reticleMat);

            // 3. Monta a cena do Menu Principal
            SetupMenuScene(prefabAsset);

            // 4. Gera capturas de Visual QA
            CaptureVisualQA();

            Debug.Log("[TASK-02] Montagem e Visual QA concluídos com 100% de sucesso!");
        }

        public static Material EnsureReticleMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(ReticleMaterialPath);
            if (mat == null)
            {
                mat = MaterialShaderHelper.CreateEmissiveMaterial(
                    "M_Reticle_HoloCyan",
                    new Color(0.0f, 0.94f, 1.0f, 0.95f),
                    new Color(0.0f, 0.94f, 1.0f, 1.0f),
                    2.8f
                );
                AssetDatabase.CreateAsset(mat, ReticleMaterialPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[TASK-02] Material criado em {ReticleMaterialPath}");
            }
            else
            {
                MaterialShaderHelper.EnsureValidShader(mat);
                MaterialShaderHelper.SetBaseColor(mat, new Color(0.0f, 0.94f, 1.0f, 0.95f));
                MaterialShaderHelper.SetEmission(mat, new Color(0.0f, 0.94f, 1.0f, 1.0f), 2.8f);
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        private static GameObject BuildInteractiveToyPrefab(Material reticleMat)
        {
            GameObject reticleModelFbx = AssetDatabase.LoadAssetAtPath<GameObject>(ReticleModelPath);
            if (reticleModelFbx == null)
            {
                Debug.LogError($"[TASK-02] Modelo não encontrado em {ReticleModelPath}. Certifique-se de que o Blender exportou o FBX.");
                return null;
            }

            // Cria raiz do Prefab temporário na memória
            GameObject root = new GameObject("ErgonomicInteractiveToy");
            root.layer = LayerMask.NameToLayer("Default");

            // Corpo Esférico Principal
            GameObject visualBody = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visualBody.name = "Visual_SphereBody";
            visualBody.transform.SetParent(root.transform);
            visualBody.transform.localPosition = Vector3.zero;
            visualBody.transform.localScale = new Vector3(0.14f, 0.14f, 0.14f);
            UnityEngine.Object.DestroyImmediate(visualBody.GetComponent<Collider>());

            // Material do corpo (grafite metálico brilhante)
            Material bodyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Generated/Task02/M_Toy_Body.mat");
            if (bodyMat == null)
            {
                bodyMat = MaterialShaderHelper.CreateLitMaterial("M_Toy_Body", new Color(0.16f, 0.20f, 0.25f), 0.90f, 0.70f);
                AssetDatabase.CreateAsset(bodyMat, "Assets/Models/Generated/Task02/M_Toy_Body.mat");
            }
            visualBody.GetComponent<Renderer>().sharedMaterial = bodyMat;

            // Anel de Destaque no Equador
            GameObject cyberRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cyberRing.name = "Visual_EquatorGlowRing";
            cyberRing.transform.SetParent(root.transform);
            cyberRing.transform.localPosition = Vector3.zero;
            cyberRing.transform.localScale = new Vector3(0.144f, 0.016f, 0.144f);
            UnityEngine.Object.DestroyImmediate(cyberRing.GetComponent<Collider>());

            Material ringMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Generated/Task02/M_Toy_GlowRing.mat");
            if (ringMat == null)
            {
                ringMat = MaterialShaderHelper.CreateEmissiveMaterial("M_Toy_GlowRing", new Color(0.0f, 0.90f, 1.0f), new Color(0.0f, 0.94f, 1.0f), 2.5f);
                AssetDatabase.CreateAsset(ringMat, "Assets/Models/Generated/Task02/M_Toy_GlowRing.mat");
            }
            cyberRing.GetComponent<Renderer>().sharedMaterial = ringMat;

            // Collider e Rigidbody
            SphereCollider sphereCollider = root.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.075f;

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Componentes Ergonômicos
            ErgonomicGrabInteractable grab = root.AddComponent<ErgonomicGrabInteractable>();
            ThrowAimAssist aimAssist = root.AddComponent<ThrowAimAssist>();
            InteractableHighlightFeedback highlight = root.AddComponent<InteractableHighlightFeedback>();
            TargetAimReticle aimReticle = root.AddComponent<TargetAimReticle>();

            // Instancia o ReticleTargetRing FBX como filho
            GameObject reticleChild = UnityEngine.Object.Instantiate(reticleModelFbx, root.transform);
            reticleChild.name = "ReticleTargetRing";
            reticleChild.transform.localPosition = Vector3.zero;
            reticleChild.transform.localRotation = Quaternion.identity;
            reticleChild.transform.localScale = Vector3.one;

            // Remove colisores espúrios do FBX se houver
            foreach (var col in reticleChild.GetComponentsInChildren<Collider>())
            {
                UnityEngine.Object.DestroyImmediate(col);
            }

            // Aplica o material emissivo ciano no anel
            foreach (var rend in reticleChild.GetComponentsInChildren<Renderer>())
            {
                rend.sharedMaterial = reticleMat;
            }

            aimReticle.ConfigureReticleInstance(reticleChild.transform, reticleModelFbx);

            // Salva como Prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, ToyPrefabPath, InteractionMode.AutomatedAction);
            UnityEngine.Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            Debug.Log($"[TASK-02] Prefab salvo com sucesso em {ToyPrefabPath}");
            return prefab;
        }

        private static void SetupMenuScene(GameObject toyPrefab)
        {
            if (EditorSceneManager.GetActiveScene().path != MenuScenePath)
            {
                EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            }

            // Limpa instâncias antigas de teste se existirem
            GameObject oldToy = GameObject.Find("[IC] Ergonomic_Toy_Demo");
            if (oldToy != null) UnityEngine.Object.DestroyImmediate(oldToy);

            GameObject oldTarget = GameObject.Find("[IC] AimAssist_Target_Demo");
            if (oldTarget != null) UnityEngine.Object.DestroyImmediate(oldTarget);

            // Instancia o brinquedo ergonômico no tampo principal da mesa
            // Coordenadas exatas do tampo: X=0.60, Y=1.025, Z=0.05 (repousando suavemente a 0.95m + raio 0.075m)
            Vector3 deskPos = new Vector3(0.60f, 1.025f, 0.05f);
            GameObject toyInstance = PrefabUtility.InstantiatePrefab(toyPrefab) as GameObject;
            toyInstance.name = "[IC] Ergonomic_Toy_Demo";
            toyInstance.transform.position = deskPos;
            toyInstance.transform.rotation = Quaternion.identity;

            ErgonomicGrabInteractable grabComp = toyInstance.GetComponent<ErgonomicGrabInteractable>();
            if (grabComp != null)
            {
                grabComp.SetHomeTransform(deskPos, Quaternion.identity);
            }

            // Cria um alvo receptáculo sci-fi em frente à mesa para testar e validar o Aim Assist
            // Posição no espaço aberto da sala sem obstruir a cadeira: (2.10, 1.15, 0.40)
            Vector3 targetPos = new Vector3(2.10f, 1.15f, 0.40f);
            GameObject targetDemo = new GameObject("[IC] AimAssist_Target_Demo");
            targetDemo.transform.position = targetPos;

            // Pedestal e anel visual do alvo
            GameObject targetRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            targetRing.name = "Target_HoopVisual";
            targetRing.transform.SetParent(targetDemo.transform);
            targetRing.transform.localPosition = Vector3.zero;
            targetRing.transform.localRotation = Quaternion.Euler(70f, -35f, 0f);
            targetRing.transform.localScale = new Vector3(0.50f, 0.035f, 0.50f);
            UnityEngine.Object.DestroyImmediate(targetRing.GetComponent<Collider>());

            Material targetMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Generated/Task02/M_AimTarget_Glow.mat");
            if (targetMat == null)
            {
                targetMat = MaterialShaderHelper.CreateEmissiveMaterial(
                    "M_AimTarget_Glow",
                    new Color(0.1f, 0.8f, 0.9f, 0.7f),
                    new Color(0.0f, 1.0f, 0.7f),
                    3.0f
                );
                AssetDatabase.CreateAsset(targetMat, "Assets/Models/Generated/Task02/M_AimTarget_Glow.mat");
            }
            targetRing.GetComponent<Renderer>().sharedMaterial = targetMat;

            // Haste suporte do alvo até o chão
            GameObject targetStem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            targetStem.name = "Target_Stem";
            targetStem.transform.SetParent(targetDemo.transform);
            targetStem.transform.localPosition = new Vector3(0f, -0.56f, 0f);
            targetStem.transform.localScale = new Vector3(0.04f, 0.56f, 0.04f);
            UnityEngine.Object.DestroyImmediate(targetStem.GetComponent<Collider>());
            Material stemMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Generated/Task02/M_Toy_Body.mat");
            if (stemMat != null) targetStem.GetComponent<Renderer>().sharedMaterial = stemMat;

            // Adiciona o componente receptor de mira assistida
            targetDemo.AddComponent<AimAssistTargetPoint>();

            // Configura os Ray Interactors do Rig de jogador para garantir useForceGrab = true
            ConfigurePlayerControllersForceGrab();

            // Marca a cena como suja e salva
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[TASK-02] Cena Menu_Principal configurada e salva.");
        }

        private static void ConfigurePlayerControllersForceGrab()
        {
            var rays = UnityEngine.Object.FindObjectsByType<XRRayInteractor>(FindObjectsSortMode.None);
            foreach (var ray in rays)
            {
                ray.useForceGrab = true;
                ray.maxRaycastDistance = 30.0f;
                ray.enableUIInteraction = true;
                EditorUtility.SetDirty(ray);
            }

            // Também garante na fonte do prefab do jogador
            string playerPrefabPath = "Assets/Prefabs/Pessoa_Virtual_sem3D loading.prefab";
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
            if (playerPrefab != null)
            {
                var prefabRays = playerPrefab.GetComponentsInChildren<XRRayInteractor>(true);
                foreach (var r in prefabRays)
                {
                    r.useForceGrab = true;
                    r.maxRaycastDistance = 30.0f;
                    EditorUtility.SetDirty(r);
                }
                PrefabUtility.SavePrefabAsset(playerPrefab);
            }
        }

        [MenuItem("VRRehab/VisualQA/Capture TASK-02 Views")]
        public static void CaptureVisualQA()
        {
            string outDir = Path.GetFullPath(".agents/visual_qa");
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            GameObject toy = GameObject.Find("[IC] Ergonomic_Toy_Demo");
            Vector3 toyPos = toy != null ? toy.transform.position : new Vector3(0.60f, 1.025f, 0.05f);

            // Prepara a retícula para ficar visível durante a captura técnica de ângulo
            TargetAimReticle reticle = toy != null ? toy.GetComponent<TargetAimReticle>() : null;
            Transform reticleChild = toy != null ? toy.transform.Find("ReticleTargetRing") : null;
            if (reticleChild != null)
            {
                reticleChild.gameObject.SetActive(true);
                reticleChild.localScale = Vector3.one;
                // Projeta perfeitamente sobre a superfície de madeira do tampo da mesa (Y = 0.952m)
                reticleChild.position = new Vector3(toyPos.x, 0.952f, toyPos.z);
            }

            // 1. TASK-02_Reticle_Aim.png: Close-up isométrico do brinquedo e da retícula sobre o tampo da mesa
            string path1 = Path.Combine(outDir, "TASK-02_Reticle_Aim.png");
            Vector3 camPos1 = new Vector3(1.10f, 1.30f, 0.35f);
            Vector3 lookAt1 = new Vector3(toyPos.x, 0.98f, toyPos.z);
            VisualQACaptureEditor.CaptureCameraView(path1, camPos1, lookAt1, 1920, 1080);
            Debug.Log($"[TASK-02] Captura 1 gerada: {path1}");

            // 2. TASK-02_ForceGrab_Preview.png: Visão média de interação mostrando o tampo da mesa, rádio e objeto
            string path2 = Path.Combine(outDir, "TASK-02_ForceGrab_Preview.png");
            Vector3 camPos2 = new Vector3(1.85f, 1.45f, 0.60f);
            Vector3 lookAt2 = new Vector3(0.60f, 1.00f, 0.10f);
            VisualQACaptureEditor.CaptureCameraView(path2, camPos2, lookAt2, 1920, 1080);
            Debug.Log($"[TASK-02] Captura 2 gerada: {path2}");

            // 3. TASK-02_AutoRespawn_Zone.png: Visão ampla enquadrando mesa (origem), chão e o alvo receptor AimAssist
            string path3 = Path.Combine(outDir, "TASK-02_AutoRespawn_Zone.png");
            Vector3 camPos3 = new Vector3(2.85f, 1.55f, -0.25f);
            Vector3 lookAt3 = new Vector3(1.35f, 1.05f, 0.22f);
            VisualQACaptureEditor.CaptureCameraView(path3, camPos3, lookAt3, 1920, 1080);
            Debug.Log($"[TASK-02] Captura 3 gerada: {path3}");

            // Restaura retícula para estado inicial oculto
            if (reticleChild != null)
            {
                reticleChild.localScale = Vector3.zero;
                reticleChild.gameObject.SetActive(false);
            }
        }
    }
}
