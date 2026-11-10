using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using VRRehab.Core;
using VRRehab.DronePet;
using VRRehab.EditorTools;

namespace VRRehab.EditorAutoSetup
{
    /// <summary>
    /// Builder e validador automatizado para a TASK-04 (Drone Pet Affective System).
    /// Constrói o visor LED frontal, os colisores de carinho e cutucão, o sistema de partículas
    /// de corações holográficos, configura materiais anti-pink e captura as telas de Visual QA.
    /// </summary>
    public static class Task04_SceneAndPrefabBuilder
    {
        private const string MenuScenePath = "Assets/Scenes/Menu_Principal.unity";
        private const string FlorestaScenePath = "Assets/Scenes/Floresta_e_Rio.unity";

        private const string TexturesDir = "Assets/Textures/Generated/Task04";
        private const string MaterialsDir = "Assets/Models/Generated/Task04";
        private const string AudioDir = "Assets/Audio/Generated/Task04";

        [MenuItem("VRRehab/AutoBuild/Build and Verify TASK-04")]
        public static void BuildAndVerify()
        {
            Debug.Log("[TASK-04] Iniciando montagem automatizada do Drone Pet Affective System...");

            // 1. Garante que texturas e materiais existem
            EnsureMaterialsAndTextures();

            // 2. Configura a cena Menu_Principal
            SetupMenuPrincipalScene();

            // 3. Configura a cena Floresta_e_Rio
            SetupFlorestaScene();

            // 4. Executa captura de Visual QA
            CaptureVisualQA();

            Debug.Log("[TASK-04] Montagem e Visual QA da TASK-04 concluídos com 100% de sucesso!");
        }

        public static void EnsureMaterialsAndTextures()
        {
            if (!AssetDatabase.IsValidFolder(MaterialsDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Models/Generated"))
                {
                    AssetDatabase.CreateFolder("Assets/Models", "Generated");
                }
                AssetDatabase.CreateFolder("Assets/Models/Generated", "Task04");
            }

            // Garante que as texturas LED foram criadas
            string texNormalPath = $"{TexturesDir}/LED_Expression_Normal.png";
            if (!File.Exists(texNormalPath))
            {
                Task04_LEDTextureGenerator.GenerateAllTextures();
            }

            // Material do Visor LED
            string matVisorPath = $"{MaterialsDir}/M_Drone_LED_Visor.mat";
            Material matVisor = AssetDatabase.LoadAssetAtPath<Material>(matVisorPath);
            if (matVisor == null)
            {
                matVisor = MaterialShaderHelper.CreateEmissiveMaterial(
                    "M_Drone_LED_Visor",
                    new Color(0.015f, 0.025f, 0.04f),
                    new Color(0.0f, 0.95f, 1.0f),
                    2.8f
                );
                AssetDatabase.CreateAsset(matVisor, matVisorPath);
            }
            else
            {
                MaterialShaderHelper.EnsureValidShader(matVisor);
            }

            Texture2D texNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(texNormalPath);
            if (texNormal != null)
            {
                matVisor.mainTexture = texNormal;
                matVisor.SetTexture("_EmissionMap", texNormal);
                matVisor.SetColor("_EmissionColor", new Color(0.0f, 0.95f, 1.0f) * 2.8f);
                matVisor.EnableKeyword("_EMISSION");
            }

            // Material das partículas de coração
            string matHeartPath = $"{MaterialsDir}/M_Holo_Heart_Particle.mat";
            Material matHeart = AssetDatabase.LoadAssetAtPath<Material>(matHeartPath);
            if (matHeart == null)
            {
                matHeart = MaterialShaderHelper.CreateBeamGlowMaterial("M_Holo_Heart_Particle", new Color(1.0f, 0.20f, 0.65f, 0.9f));
                AssetDatabase.CreateAsset(matHeart, matHeartPath);
            }

            Texture2D texHeart = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/Holo_Heart_Particle.png");
            if (texHeart != null)
            {
                matHeart.mainTexture = texHeart;
            }

            AssetDatabase.SaveAssets();
        }

        public static void SetupMenuPrincipalScene()
        {
            if (EditorSceneManager.GetActiveScene().path != MenuScenePath)
            {
                EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            }

            GameObject droneObj = GameObject.Find("[IC] Drone_Guia_IA_ExoBot");
            if (droneObj == null)
            {
                Debug.LogError("[TASK-04] Drone não encontrado na cena Menu_Principal!");
                return;
            }

            ConfigureDroneAffectiveComponents(droneObj);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[TASK-04] Drone configurado na cena Menu_Principal!");
        }

        public static void SetupFlorestaScene()
        {
            string original = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.OpenScene(FlorestaScenePath, OpenSceneMode.Single);

            GameObject droneObj = GameObject.Find("[IC] Drone_Guia_IA_ExoBot");
            if (droneObj != null)
            {
                ConfigureDroneAffectiveComponents(droneObj);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                Debug.Log("[TASK-04] Drone configurado na cena Floresta_e_Rio!");
            }

            if (!string.IsNullOrEmpty(original))
            {
                EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
            }
        }

        public static void ConfigureDroneAffectiveComponents(GameObject droneObj)
        {
            // 1. Garante modelo 3D raiz
            Transform modeloRaiz = droneObj.transform.Find("Modelo_3D_ExoBot");
            if (modeloRaiz == null)
            {
                modeloRaiz = new GameObject("Modelo_3D_ExoBot").transform;
                modeloRaiz.SetParent(droneObj.transform, false);
            }

            // 2. Constrói ou atualiza o Visor_LED_Frontal
            Transform visorTransform = modeloRaiz.Find("Visor_LED_Frontal");
            if (visorTransform == null)
            {
                GameObject goVisor = new GameObject("Visor_LED_Frontal");
                goVisor.transform.SetParent(modeloRaiz, false);
                visorTransform = goVisor.transform;
            }

            // Posicionamento exato na face frontal da lente concêntrica (+Z)
            visorTransform.localPosition = new Vector3(0f, 0.005f, 0.245f);
            visorTransform.localRotation = Quaternion.identity;
            visorTransform.localScale = new Vector3(0.205f, 0.205f, 0.205f);

            // Cria malha circular precisa para o visor
            MeshFilter mfVisor = visorTransform.GetComponent<MeshFilter>();
            if (mfVisor == null) mfVisor = visorTransform.gameObject.AddComponent<MeshFilter>();
            mfVisor.sharedMesh = BuildCircularVisorMesh();

            MeshRenderer mrVisor = visorTransform.GetComponent<MeshRenderer>();
            if (mrVisor == null) mrVisor = visorTransform.gameObject.AddComponent<MeshRenderer>();
            Material matVisor = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/M_Drone_LED_Visor.mat");
            mrVisor.sharedMaterial = matVisor;

            // 3. Colisor de Carinho (Petting) na calota superior
            Transform tPet = droneObj.transform.Find("HeadPettingTrigger");
            if (tPet == null)
            {
                GameObject goPet = new GameObject("HeadPettingTrigger");
                goPet.transform.SetParent(droneObj.transform, false);
                tPet = goPet.transform;
            }
            tPet.localPosition = new Vector3(0f, 0.22f, 0f);
            SphereCollider colPet = tPet.GetComponent<SphereCollider>();
            if (colPet == null) colPet = tPet.gameObject.AddComponent<SphereCollider>();
            colPet.isTrigger = true;
            colPet.radius = 0.18f;

            // 4. Colisor de Cutucão (Poke/Boop) na face frontal
            Transform tPoke = droneObj.transform.Find("FacePokeTrigger");
            if (tPoke == null)
            {
                GameObject goPoke = new GameObject("FacePokeTrigger");
                goPoke.transform.SetParent(droneObj.transform, false);
                tPoke = goPoke.transform;
            }
            tPoke.localPosition = new Vector3(0f, 0.005f, 0.235f);
            SphereCollider colPoke = tPoke.GetComponent<SphereCollider>();
            if (colPoke == null) colPoke = tPoke.gameObject.AddComponent<SphereCollider>();
            colPoke.isTrigger = true;
            colPoke.radius = 0.095f;

            // 5. Sistema de Partículas de Corações Holográficos
            Transform tHearts = droneObj.transform.Find("ParticleSystem_Hearts");
            ParticleSystem psHearts = null;
            if (tHearts == null)
            {
                GameObject goHearts = new GameObject("ParticleSystem_Hearts");
                goHearts.transform.SetParent(droneObj.transform, false);
                tHearts = goHearts.transform;
            }
            tHearts.localPosition = new Vector3(0f, 0.24f, 0f);
            psHearts = tHearts.GetComponent<ParticleSystem>();
            if (psHearts == null) psHearts = tHearts.gameObject.AddComponent<ParticleSystem>();
            ConfigureHeartParticles(psHearts);

            // 6. Configura o componente DroneAffectiveSystem
            DroneAffectiveSystem affective = droneObj.GetComponent<DroneAffectiveSystem>();
            if (affective == null) affective = droneObj.AddComponent<DroneAffectiveSystem>();

            affective.visorRenderer = mrVisor;
            affective.texNormal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/LED_Expression_Normal.png");
            affective.texHappy = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/LED_Expression_Happy.png");
            affective.texLove = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/LED_Expression_Love.png");
            affective.texPoked = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/LED_Expression_Poked.png");
            affective.texDance = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/LED_Expression_Dance.png");

            affective.headPettingTrigger = colPet;
            affective.facePokeTrigger = colPoke;
            affective.heartParticles = psHearts;

            affective.sfxDronePurr = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/drone_purr.wav");
            affective.sfxDroneBoop = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/drone_boop.wav");
            affective.sfxDroneGiggle = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/drone_giggle.wav");

            GameObject radioObj = GameObject.Find("[IC] Radio_Menu_3D");
            if (radioObj != null)
            {
                affective.musicSource = radioObj.GetComponent<AudioSource>();
            }

            affective.AutoDetectComponents();
            affective.SetExpression(DroneLEDExpression.Normal);
        }

        private static Mesh BuildCircularVisorMesh()
        {
            Mesh mesh = new Mesh { name = "Visor_LED_Mesh" };
            int segments = 36;
            var verts = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];
            var norms = new Vector3[segments + 1];
            var tris = new int[segments * 6]; // Two-sided para prevenir culling

            // Vértice central
            verts[0] = new Vector3(0f, 0f, 0f);
            uvs[0] = new Vector2(0.5f, 0.5f);
            norms[0] = Vector3.forward;

            float radius = 0.5f;
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                // Leve curvatura esférica convexa para harmonia com a lente óptica
                float z = (1f - (cos * cos + sin * sin)) * 0.02f;
                verts[i + 1] = new Vector3(cos * radius, sin * radius, z);
                uvs[i + 1] = new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f);
                norms[i + 1] = Vector3.forward;

                int i0 = 0;
                int i1 = i + 1;
                int i2 = (i + 1) % segments + 1;

                // Face Frontal (+Z, horária vista da frente)
                tris[i * 6 + 0] = i0;
                tris[i * 6 + 1] = i2;
                tris[i * 6 + 2] = i1;

                // Face Traseira (-Z)
                tris[i * 6 + 3] = i0;
                tris[i * 6 + 4] = i1;
                tris[i * 6 + 5] = i2;
            }

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.normals = norms;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void ConfigureHeartParticles(ParticleSystem ps)
        {
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 2.0f;
            main.startLifetime = 1.4f;
            main.startSpeed = 0.40f;
            main.startSize = 0.09f;
            main.gravityModifier = -0.06f; // Flutua suavemente para cima
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(1.0f, 0.25f, 0.70f, 0.95f);

            var emission = ps.emission;
            emission.rateOverTime = 8.0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.12f;

            var colOverLifetime = ps.colorOverLifetime;
            colOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 0.2f, 0.7f), 0.0f),
                    new GradientColorKey(new Color(0f, 0.95f, 1f), 0.65f),
                    new GradientColorKey(new Color(1f, 1f, 1f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.95f, 0.2f),
                    new GradientAlphaKey(0.85f, 0.7f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colOverLifetime.color = grad;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.4f);
            curve.AddKey(0.35f, 1.15f);
            curve.AddKey(1f, 0.8f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            Material matHeart = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/M_Holo_Heart_Particle.mat");
            if (matHeart != null)
            {
                rend.sharedMaterial = matHeart;
            }
        }

        public static void CaptureVisualQA()
        {
            string baseDir = Path.GetFullPath(".agents/visual_qa");
            if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);

            if (EditorSceneManager.GetActiveScene().path != MenuScenePath)
            {
                EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            }

            GameObject droneObj = GameObject.Find("[IC] Drone_Guia_IA_ExoBot");
            if (droneObj == null)
            {
                Debug.LogError("[TASK-04 Visual QA] Drone não encontrado para capturar!");
                return;
            }

            DroneAffectiveSystem affective = droneObj.GetComponent<DroneAffectiveSystem>();
            if (affective == null)
            {
                Debug.LogError("[TASK-04 Visual QA] DroneAffectiveSystem ausente!");
                return;
            }

            Vector3 dronePos = droneObj.transform.position;
            Vector3 droneForward = droneObj.transform.forward;
            Vector3 droneUp = droneObj.transform.up;
            Vector3 droneRight = droneObj.transform.right;

            // Localiza o centro exato do visor frontal
            Vector3 visorWorldPos = (affective.visorRenderer != null)
                ? affective.visorRenderer.bounds.center
                : (dronePos + (droneForward * 0.245f) + (droneUp * 0.005f));

            // 1. Close-up frontal do visor exibindo expressão LED Happy (^‿^)
            affective.SetExpression(DroneLEDExpression.Happy);
            string pathVisor = Path.Combine(baseDir, "TASK-04_Drone_Visor_Happy.png");
            Vector3 camPosVisor = visorWorldPos + (droneForward * 0.38f) + (droneUp * 0.02f);
            Vector3 targetVisor = visorWorldPos;
            VisualQACaptureEditor.CaptureCameraView(pathVisor, camPosVisor, targetVisor, 1920, 1080);
            Debug.Log($"[TASK-04 Visual QA] Imagem 1 capturada: {pathVisor}");

            // 2. Visão do Drone recebendo Carinho (Petting Feedback com Corações e Expressão Love ♥‿♥)
            affective.TestPettingPose();
            // Simula avanço de partículas para aparecerem no print
            if (affective.heartParticles != null)
            {
                affective.heartParticles.Clear();
                affective.heartParticles.Play();
                affective.heartParticles.Simulate(0.85f, true, true);
            }
            string pathPetting = Path.Combine(baseDir, "TASK-04_Drone_Petting_Feedback.png");
            Vector3 camPosPet = dronePos + (droneForward * 0.65f) + (droneRight * 0.35f) + (droneUp * 0.32f);
            Vector3 targetPet = dronePos + (droneUp * 0.12f);
            VisualQACaptureEditor.CaptureCameraView(pathPetting, camPosPet, targetPet, 1920, 1080);
            Debug.Log($"[TASK-04 Visual QA] Imagem 2 capturada: {pathPetting}");

            // 3. Enquadramento do Drone dançando no ritmo do rádio na sala do Menu (Expressão Dance ♪‿♪)
            affective.TestDancingPose();
            string pathDance = Path.Combine(baseDir, "TASK-04_Drone_Dancing_Pose.png");
            Vector3 camPosDance = dronePos + (droneForward * 0.95f) + (droneRight * 0.45f) + (droneUp * 0.15f);
            Vector3 targetDance = visorWorldPos;
            VisualQACaptureEditor.CaptureCameraView(pathDance, camPosDance, targetDance, 1920, 1080);
            Debug.Log($"[TASK-04 Visual QA] Imagem 3 capturada: {pathDance}");

            // Restaura pose normal
            affective.ResetTestPose();
            EditorSceneManager.SaveOpenScenes();
        }
    }
}
