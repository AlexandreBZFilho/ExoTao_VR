using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;
using VRRehab.Interactive.Task07;
using VRRehab.EditorTools;

namespace VRRehab.EditorAutoSetup
{
    /// <summary>
    /// Builder e validador automatizado para a TASK-07 (Pistola/Dispenser de Balões Antigravidade).
    /// Constrói materiais, Prefabs da Pistola e do Balão Flutuante,
    /// configura a cena Menu_Principal e executa o ciclo Visual QA (1920x1080).
    /// </summary>
    public static class Task07_SceneAndPrefabBuilder
    {
        private const string ModelsDir = "Assets/Models/Generated/Task07";
        private const string PrefabsDir = "Assets/Prefabs/Interactive";
        private const string MenuScenePath = "Assets/Scenes/Menu_Principal.unity";
        private const string OutVisualQADir = ".agents/visual_qa";

        // Modelos FBX gerados pelo Blender 5.1
        private const string GunModelPath = "Assets/Models/Generated/Task07/BalloonGunDispenser.fbx";
        private const string BalloonModelPath = "Assets/Models/Generated/Task07/InflatableBalloon.fbx";

        // Áudios procedurais gerados via Python
        private const string SfxStretchPath = "Assets/Audio/Generated/Task07/balloon_inflate_stretch.wav";
        private const string SfxWhooshPath = "Assets/Audio/Generated/Task07/balloon_release_whoosh.wav";
        private const string SfxPopPath = "Assets/Audio/Generated/Task07/balloon_pop_loud.wav";

        [MenuItem("VRRehab/AutoBuild/Build and Verify TASK-07")]
        public static void BuildAndVerify()
        {
            Debug.Log("[TASK-07] Iniciando construção completa da Pistola de Balões e Balões Antigravidade...");

            // 1. Reimporta síncrono os FBX e áudios
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(GunModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(BalloonModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxStretchPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxWhooshPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxPopPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            // 2. Remove prefabs antigos para recriação limpa
            string[] prefabsToDelete = new string[] {
                $"{PrefabsDir}/InflatableBalloon.prefab",
                $"{PrefabsDir}/BalloonGunDispenser.prefab"
            };
            foreach (var p in prefabsToDelete)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(p) != null)
                {
                    AssetDatabase.DeleteAsset(p);
                }
            }

            // 3. Garante Materiais
            EnsureMaterials();

            // 4. Cria Prefab do Balão
            GameObject prefabBalloon = BuildBalloonPrefab();

            // 5. Cria Prefab da Pistola
            GameObject prefabGun = BuildGunPrefab(prefabBalloon);

            // 6. Configura cena Menu_Principal
            SetupMenuPrincipalScene(prefabGun, prefabBalloon);

            // 7. Visual QA Captures
            CaptureVisualQA();

            Debug.Log("[TASK-07] Conclusão com sucesso de todos os estágios!");
        }

        public static void EnsureMaterials()
        {
            if (!AssetDatabase.IsValidFolder(ModelsDir))
            {
                AssetDatabase.CreateFolder("Assets/Models/Generated", "Task07");
            }

            // Gun Materials
            EnsureMaterial($"{ModelsDir}/M_BalloonGun_Body.mat", "M_BalloonGun_Body", () =>
                MaterialShaderHelper.CreateLitMaterial("M_BalloonGun_Body", new Color(0.92f, 0.94f, 0.96f), 0.35f, 0.08f));

            EnsureMaterial($"{ModelsDir}/M_BalloonGun_Grip.mat", "M_BalloonGun_Grip", () =>
                MaterialShaderHelper.CreateLitMaterial("M_BalloonGun_Grip", new Color(0.12f, 0.13f, 0.15f), 0.20f, 0.02f));

            EnsureMaterial($"{ModelsDir}/M_BalloonGun_Canister.mat", "M_BalloonGun_Canister", () =>
                MaterialShaderHelper.CreateLitMaterial("M_BalloonGun_Canister", new Color(0.00f, 0.46f, 0.72f), 0.88f, 0.85f));

            EnsureMaterial($"{ModelsDir}/M_BalloonGun_Accent.mat", "M_BalloonGun_Accent", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_BalloonGun_Accent", new Color(0.00f, 0.71f, 0.85f), new Color(0.00f, 0.85f, 1.0f), 2.2f));

            EnsureMaterial($"{ModelsDir}/M_BalloonGun_Nozzle.mat", "M_BalloonGun_Nozzle", () =>
                MaterialShaderHelper.CreateLitMaterial("M_BalloonGun_Nozzle", new Color(0.20f, 0.22f, 0.25f), 0.50f, 0.65f));

            EnsureMaterial($"{ModelsDir}/M_BalloonGun_Gauge.mat", "M_BalloonGun_Gauge", () =>
                MaterialShaderHelper.CreateLitMaterial("M_BalloonGun_Gauge", new Color(0.96f, 0.97f, 0.98f), 0.30f, 0.05f));

            EnsureMaterial($"{ModelsDir}/M_BalloonGun_Brass.mat", "M_BalloonGun_Brass", () =>
                MaterialShaderHelper.CreateLitMaterial("M_BalloonGun_Brass", new Color(0.85f, 0.68f, 0.22f), 0.75f, 0.80f));

            // Balloon Rubber Materials (Glossy finish)
            EnsureMaterial($"{ModelsDir}/M_Balloon_RubyRed.mat", "M_Balloon_RubyRed", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Balloon_RubyRed", new Color(1.00f, 0.12f, 0.25f), 0.86f, 0.04f));

            EnsureMaterial($"{ModelsDir}/M_Balloon_CyberCyan.mat", "M_Balloon_CyberCyan", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Balloon_CyberCyan", new Color(0.00f, 0.90f, 1.00f), 0.86f, 0.04f));

            EnsureMaterial($"{ModelsDir}/M_Balloon_SunnyYellow.mat", "M_Balloon_SunnyYellow", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Balloon_SunnyYellow", new Color(1.00f, 0.84f, 0.00f), 0.86f, 0.04f));

            EnsureMaterial($"{ModelsDir}/M_Balloon_NeonMagenta.mat", "M_Balloon_NeonMagenta", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Balloon_NeonMagenta", new Color(1.00f, 0.00f, 0.50f), 0.86f, 0.04f));

            EnsureMaterial($"{ModelsDir}/M_Balloon_LimeGreen.mat", "M_Balloon_LimeGreen", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Balloon_LimeGreen", new Color(0.46f, 1.00f, 0.01f), 0.86f, 0.04f));

            EnsureMaterial($"{ModelsDir}/M_Balloon_RoyalPurple.mat", "M_Balloon_RoyalPurple", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Balloon_RoyalPurple", new Color(0.62f, 0.00f, 1.00f), 0.86f, 0.04f));

            EnsureMaterial($"{ModelsDir}/M_Balloon_Tangerine.mat", "M_Balloon_Tangerine", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Balloon_Tangerine", new Color(1.00f, 0.43f, 0.00f), 0.86f, 0.04f));

            EnsureMaterial($"{ModelsDir}/M_Balloon_String.mat", "M_Balloon_String", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Balloon_String", new Color(0.88f, 0.88f, 0.88f), 0.20f, 0.0f));

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

        private static GameObject BuildBalloonPrefab()
        {
            if (!AssetDatabase.IsValidFolder(PrefabsDir))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "Interactive");
            }

            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(BalloonModelPath);
            if (fbx == null)
            {
                Debug.LogError($"[TASK-07] Modelo FBX do balão não encontrado em {BalloonModelPath}");
                return null;
            }

            GameObject root = new GameObject("InflatableBalloon");
            root.layer = LayerMask.NameToLayer("Default");

            GameObject visual = UnityEngine.Object.Instantiate(fbx, root.transform);
            visual.name = "Visual_Model";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            // Remove colisores importados do FBX se houver
            foreach (var col in visual.GetComponentsInChildren<Collider>())
            {
                UnityEngine.Object.DestroyImmediate(col);
            }

            Material matRed = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Balloon_RubyRed.mat");
            Material matString = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Balloon_String.mat");

            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>())
            {
                mr.sharedMaterial = matRed;
            }

            // Collider esférico cobrindo o corpo do balão
            SphereCollider sphere = root.AddComponent<SphereCollider>();
            sphere.center = new Vector3(0f, 0.18f, 0f);
            sphere.radius = 0.13f;

            // Rigidbody aerodinâmico
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.012f;
            rb.linearDamping = 1.35f;
            rb.angularDamping = 1.60f;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // LineRenderer para a cordinha pendurada
            LineRenderer lr = root.AddComponent<LineRenderer>();
            lr.sharedMaterial = matString;
            lr.startWidth = 0.0035f;
            lr.endWidth = 0.0020f;
            lr.useWorldSpace = true;
            lr.positionCount = 6;

            // AudioSource 3D espacializado
            AudioSource audioSrc = root.AddComponent<AudioSource>();
            audioSrc.spatialBlend = 1.0f;
            audioSrc.playOnAwake = false;
            audioSrc.rolloffMode = AudioRolloffMode.Linear;
            audioSrc.minDistance = 0.5f;
            audioSrc.maxDistance = 15.0f;

            // ParticleSystem para o estouro
            GameObject psObj = new GameObject("Burst_Particles");
            psObj.transform.SetParent(root.transform);
            psObj.transform.localPosition = new Vector3(0f, 0.18f, 0f);

            ParticleSystem ps = psObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = 0.6f;
            main.startSpeed = 3.2f;
            main.startSize = 0.035f;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None;
            main.startColor = new Color(1.0f, 0.12f, 0.25f);

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 32) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;

            ParticleSystemRenderer psr = psObj.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                psr.sharedMaterial = matRed;
            }

            // Script FloatingBalloon
            FloatingBalloon fb = root.AddComponent<FloatingBalloon>();
            fb.interactionLayers = InteractionLayerMask.GetMask("Default");

            // Configura via SerializedObject para garantir atribuição robusta dos campos
            SerializedObject so = new SerializedObject(fb);
            so.FindProperty("stringRenderer").objectReferenceValue = lr;
            Transform stringAnchor = visual.transform.Find("String_Anchor");
            if (stringAnchor == null) stringAnchor = root.transform;
            so.FindProperty("stringAnchor").objectReferenceValue = stringAnchor;
            so.FindProperty("burstParticleSystem").objectReferenceValue = ps;
            so.FindProperty("popAudioClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxPopPath);
            so.FindProperty("audioSource").objectReferenceValue = audioSrc;
            so.ApplyModifiedPropertiesWithoutUndo();

            string savePath = $"{PrefabsDir}/InflatableBalloon.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log($"[TASK-07] Prefab do Balão salvo em {savePath}");
            return savedPrefab;
        }

        private static GameObject BuildGunPrefab(GameObject balloonPrefab)
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(GunModelPath);
            if (fbx == null)
            {
                Debug.LogError($"[TASK-07] Modelo FBX da Pistola não encontrado em {GunModelPath}");
                return null;
            }

            GameObject root = new GameObject("BalloonGunDispenser");
            root.layer = LayerMask.NameToLayer("Default");

            GameObject visual = UnityEngine.Object.Instantiate(fbx, root.transform);
            visual.name = "Visual_Model";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            // Remove colisores importados do FBX se houver
            foreach (var col in visual.GetComponentsInChildren<Collider>())
            {
                UnityEngine.Object.DestroyImmediate(col);
            }

            // Mapeia Materiais
            Material matBody = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_BalloonGun_Body.mat");
            Material matGrip = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_BalloonGun_Grip.mat");
            Material matCanister = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_BalloonGun_Canister.mat");
            Material matAccent = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_BalloonGun_Accent.mat");
            Material matNozzle = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_BalloonGun_Nozzle.mat");
            Material matGauge = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_BalloonGun_Gauge.mat");
            Material matBrass = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_BalloonGun_Brass.mat");

            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>())
            {
                string oName = mr.gameObject.name;
                if (oName.Contains("Grip_Pad") || oName.Contains("Trigger") || oName.Contains("Fin") || oName.Contains("Needle"))
                    mr.sharedMaterial = matAccent;
                else if (oName.Contains("Grip"))
                    mr.sharedMaterial = matGrip;
                else if (oName.Contains("Canister_Band") || oName.Contains("Trigger_Guard") || oName.Contains("Gun_Nozzle"))
                    mr.sharedMaterial = matNozzle;
                else if (oName.Contains("Canister"))
                    mr.sharedMaterial = matCanister;
                else if (oName.Contains("Rim") || oName.Contains("Pipe") || oName.Contains("Bezel") || oName.Contains("Brass"))
                    mr.sharedMaterial = matBrass;
                else if (oName.Contains("Face") || oName.Contains("Gauge"))
                    mr.sharedMaterial = matGauge;
                else
                    mr.sharedMaterial = matBody;
            }

            // BoxCollider ergonômico para apoio na mesa
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(0.09f, 0.16f, 0.24f);
            box.center = new Vector3(0.01f, 0.01f, 0.04f);

            // Rigidbody
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.65f;
            rb.linearDamping = 1.0f;
            rb.angularDamping = 1.0f;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // AudioSource
            AudioSource audioSrc = root.AddComponent<AudioSource>();
            audioSrc.spatialBlend = 1.0f;
            audioSrc.playOnAwake = false;
            audioSrc.rolloffMode = AudioRolloffMode.Linear;
            audioSrc.minDistance = 0.3f;
            audioSrc.maxDistance = 10.0f;

            // BalloonInflaterGun (Herda de ErgonomicGrabInteractable)
            BalloonInflaterGun gun = root.AddComponent<BalloonInflaterGun>();
            gun.interactionLayers = InteractionLayerMask.GetMask("Default");

            Transform muzzle = visual.transform.Find("Muzzle_Socket");
            if (muzzle == null) muzzle = root.transform;

            Material[] balloonMats = new Material[] {
                AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Balloon_RubyRed.mat"),
                AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Balloon_CyberCyan.mat"),
                AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Balloon_SunnyYellow.mat"),
                AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Balloon_NeonMagenta.mat"),
                AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Balloon_LimeGreen.mat"),
                AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Balloon_RoyalPurple.mat"),
                AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Balloon_Tangerine.mat")
            };

            SerializedObject so = new SerializedObject(gun);
            so.FindProperty("muzzleSocket").objectReferenceValue = muzzle;
            so.FindProperty("balloonPrefab").objectReferenceValue = balloonPrefab;
            so.FindProperty("inflateStretchAudioClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxStretchPath);
            so.FindProperty("releaseWhooshAudioClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxWhooshPath);
            so.FindProperty("gunAudioSource").objectReferenceValue = audioSrc;

            SerializedProperty matsProp = so.FindProperty("balloonMaterials");
            matsProp.arraySize = balloonMats.Length;
            for (int i = 0; i < balloonMats.Length; i++)
            {
                matsProp.GetArrayElementAtIndex(i).objectReferenceValue = balloonMats[i];
            }

            // Auto-respawn na mesa
            so.FindProperty("enableAutoRespawn").boolValue = true;
            so.FindProperty("respawnDelaySeconds").floatValue = 3.0f;
            so.FindProperty("respawnFloorThresholdY").floatValue = 0.40f;
            so.ApplyModifiedPropertiesWithoutUndo();

            string savePath = $"{PrefabsDir}/BalloonGunDispenser.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log($"[TASK-07] Prefab da Pistola salvo em {savePath}");
            return savedPrefab;
        }

        public static void SetupMenuPrincipalScene(GameObject prefabGun, GameObject prefabBalloon)
        {
            if (EditorSceneManager.GetActiveScene().path != MenuScenePath)
            {
                EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            }

            // Remove instâncias antigas da Task07
            string[] toClean = new string[] {
                "BalloonGunDispenser_Instance",
                "Demo_Balloon_Red",
                "Demo_Balloon_Cyan",
                "Demo_Balloon_Yellow",
                "InflatingBalloon_Preview"
            };
            foreach (var cName in toClean)
            {
                GameObject existing = GameObject.Find(cName);
                if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
            }

            // 1. Instancia a Pistola sobre a mesa do menu em repouso ergonômico
            // Mesa está em y ~ 0.952m. Rádio em (0.66, 0.952, 0.36)
            // Posiciona a pistola na frente/direita do rádio, em repouso plano sobre o tampo
            Vector3 gunPos = new Vector3(0.78f, 0.976f, 0.48f);
            Quaternion gunRot = Quaternion.Euler(0f, 40f, -90f);

            GameObject gunInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefabGun);
            gunInstance.name = "BalloonGunDispenser_Instance";
            gunInstance.transform.position = gunPos;
            gunInstance.transform.rotation = gunRot;

            Rigidbody gunRb = gunInstance.GetComponent<Rigidbody>();
            if (gunRb != null) gunRb.isKinematic = true;

            // 2. Instancia 3 Balões Flutuantes de demonstração na sala em alturas variadas (orientação vertical ereta)
            Material matRed = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Balloon_RubyRed.mat");
            Material matCyan = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Balloon_CyberCyan.mat");
            Material matYellow = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Balloon_SunnyYellow.mat");

            // Balão 1: Vermelho Ruby, flutuando à frente da mesa
            GameObject b1 = (GameObject)PrefabUtility.InstantiatePrefab(prefabBalloon);
            b1.name = "Demo_Balloon_Red";
            b1.transform.position = new Vector3(1.10f, 1.48f, 0.65f);
            b1.transform.rotation = Quaternion.identity;
            b1.transform.localScale = Vector3.one * 0.27f;
            b1.GetComponent<FloatingBalloon>()?.ApplyColorMaterial(matRed, new Color(1.00f, 0.12f, 0.25f));
            Rigidbody rb1 = b1.GetComponent<Rigidbody>();
            if (rb1 != null) rb1.isKinematic = true;

            // Balão 2: Ciano Cyber, flutuando mais alto e central
            GameObject b2 = (GameObject)PrefabUtility.InstantiatePrefab(prefabBalloon);
            b2.name = "Demo_Balloon_Cyan";
            b2.transform.position = new Vector3(1.42f, 1.75f, 0.10f);
            b2.transform.rotation = Quaternion.identity;
            b2.transform.localScale = Vector3.one * 0.29f;
            b2.GetComponent<FloatingBalloon>()?.ApplyColorMaterial(matCyan, new Color(0.00f, 0.90f, 1.00f));
            Rigidbody rb2 = b2.GetComponent<Rigidbody>();
            if (rb2 != null) rb2.isKinematic = true;

            // Balão 3: Amarelo Solar, flutuando à direita
            GameObject b3 = (GameObject)PrefabUtility.InstantiatePrefab(prefabBalloon);
            b3.name = "Demo_Balloon_Yellow";
            b3.transform.position = new Vector3(0.95f, 1.95f, -0.30f);
            b3.transform.rotation = Quaternion.identity;
            b3.transform.localScale = Vector3.one * 0.28f;
            b3.GetComponent<FloatingBalloon>()?.ApplyColorMaterial(matYellow, new Color(1.00f, 0.84f, 0.00f));
            Rigidbody rb3 = b3.GetComponent<Rigidbody>();
            if (rb3 != null) rb3.isKinematic = true;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("[TASK-07] Cena Menu_Principal configurada com Pistola e Balões Flutuantes!");
        }

        public static void CaptureVisualQA()
        {
            string outDir = Path.GetFullPath(OutVisualQADir);
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            // Garante que a cena Menu_Principal está aberta
            if (EditorSceneManager.GetActiveScene().path != MenuScenePath)
            {
                EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            }

            GameObject gunObj = GameObject.Find("BalloonGunDispenser_Instance");
            BalloonInflaterGun gun = gunObj != null ? gunObj.GetComponent<BalloonInflaterGun>() : null;

            // 1. TASK-07_BalloonGun_Desk.png: Pistola repousando na mesa
            string pathDesk = Path.Combine(outDir, "TASK-07_BalloonGun_Desk.png");
            Vector3 camPos1 = new Vector3(1.22f, 1.25f, 0.62f);
            Vector3 lookAt1 = new Vector3(0.78f, 0.98f, 0.48f);
            VisualQACaptureEditor.CaptureCameraView(pathDesk, camPos1, lookAt1, 1920, 1080);
            Debug.Log($"[TASK-07] Visual QA 1 capturado: {pathDesk}");

            // 2. TASK-07_Balloon_Inflating.png: Close-up com balão sendo inflado no bico
            string pathInflating = Path.Combine(outDir, "TASK-07_Balloon_Inflating.png");
            GameObject inflatingPreview = null;
            if (gun != null)
            {
                Material matCyan = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Balloon_CyberCyan.mat");
                inflatingPreview = gun.ForceInflateOnMuzzle(0.25f, matCyan);
            }
            Vector3 camPos2 = new Vector3(1.10f, 1.15f, 0.36f);
            Vector3 lookAt2 = new Vector3(0.78f, 1.02f, 0.48f);
            VisualQACaptureEditor.CaptureCameraView(pathInflating, camPos2, lookAt2, 1920, 1080);
            Debug.Log($"[TASK-07] Visual QA 2 capturado: {pathInflating}");

            if (inflatingPreview != null)
            {
                UnityEngine.Object.DestroyImmediate(inflatingPreview);
            }

            // 3. TASK-07_Balloons_Floating_Room.png: Visão panorâmica dos balões flutuando na sala
            string pathRoom = Path.Combine(outDir, "TASK-07_Balloons_Floating_Room.png");
            Vector3 camPos3 = new Vector3(2.35f, 1.45f, 0.40f);
            Vector3 lookAt3 = new Vector3(1.05f, 1.62f, 0.15f);
            VisualQACaptureEditor.CaptureCameraView(pathRoom, camPos3, lookAt3, 1920, 1080);
            Debug.Log($"[TASK-07] Visual QA 3 capturado: {pathRoom}");

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }
    }
}
