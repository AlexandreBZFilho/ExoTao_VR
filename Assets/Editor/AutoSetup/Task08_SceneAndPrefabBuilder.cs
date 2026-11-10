using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;
using VRRehab.Core;
using VRRehab.Ergonomics;
using VRRehab.Interactive.Task08;
using VRRehab.EditorTools;

namespace VRRehab.EditorAutoSetup
{
    /// <summary>
    /// Builder e validador automatizado para a TASK-08 (Mini Basquete Arcade de Mesa com Spawner e Duplo Sensor).
    /// Constrói materiais, Prefabs da Tabela/Aro, Bola e Botão Spawner,
    /// monta a cena Menu_Principal e executa o ciclo Visual QA (1920x1080).
    /// </summary>
    public static class Task08_SceneAndPrefabBuilder
    {
        private const string ModelsDir = "Assets/Models/Generated/Task08";
        private const string PrefabsDir = "Assets/Prefabs/Interactive";
        private const string MenuScenePath = "Assets/Scenes/Menu_Principal.unity";
        private const string OutVisualQADir = ".agents/visual_qa";

        // Modelos FBX gerados pelo Blender 5.1
        private const string HoopModelPath = "Assets/Models/Generated/Task08/ArcadeBasketballHoop.fbx";
        private const string BallModelPath = "Assets/Models/Generated/Task08/MiniBasketball.fbx";
        private const string ButtonModelPath = "Assets/Models/Generated/Task08/ArcadeSpawnButton.fbx";

        // Áudios procedurais gerados via Python
        private const string SfxBouncePath = "Assets/Audio/Generated/Task08/basketball_bounce.wav";
        private const string SfxSwishPath = "Assets/Audio/Generated/Task08/hoop_swish.wav";
        private const string SfxClangPath = "Assets/Audio/Generated/Task08/hoop_rim_clang.wav";
        private const string SfxBuzzerPath = "Assets/Audio/Generated/Task08/arcade_buzzer.wav";
        private const string SfxButtonPressPath = "Assets/Audio/Generated/Task08/arcade_button_press.wav";

        [MenuItem("VRRehab/AutoBuild/Build and Verify TASK-08")]
        public static void BuildAndVerify()
        {
            Debug.Log("[TASK-08] Iniciando construção completa do Mini Basquete Arcade...");

            // 1. Reimporta síncrono FBX e áudios
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(HoopModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(BallModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(ButtonModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxBouncePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxSwishPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxClangPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxBuzzerPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxButtonPressPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            // 2. Remove prefabs antigos para recriação limpa
            string[] prefabsToDelete = new string[] {
                $"{PrefabsDir}/MiniBasketball.prefab",
                $"{PrefabsDir}/ArcadeBallSpawnerButton.prefab",
                $"{PrefabsDir}/ArcadeBasketballHoop.prefab"
            };
            foreach (var p in prefabsToDelete)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(p) != null)
                {
                    AssetDatabase.DeleteAsset(p);
                }
            }

            // 3. Garante Materiais e PhysicMaterial
            EnsureMaterials();
            PhysicsMaterial pmBouncy = EnsurePhysicMaterial();

            // 4. Cria Prefab da MiniBasketball
            GameObject prefabBall = BuildBasketballPrefab(pmBouncy);

            // 5. Cria Prefab da Tabela e Aro Arcade
            GameObject prefabHoop = BuildHoopPrefab();

            // 6. Cria Prefab do Botão Spawner
            GameObject prefabButton = BuildButtonPrefab(prefabBall);

            // 7. Configura a cena Menu_Principal
            SetupMenuPrincipalScene(prefabHoop, prefabButton, prefabBall);

            // 8. Visual QA Captures
            CaptureVisualQA();

            Debug.Log("[TASK-08] Conclusão com sucesso de todos os estágios do Mini Basquete Arcade!");
        }

        public static void EnsureMaterials()
        {
            if (!AssetDatabase.IsValidFolder(ModelsDir))
            {
                AssetDatabase.CreateFolder("Assets/Models/Generated", "Task08");
            }

            // Hoop Materials
            EnsureMaterial($"{ModelsDir}/M_Hoop_Backboard.mat", "M_Hoop_Backboard", () =>
                MaterialShaderHelper.CreateTransparentMaterial("M_Hoop_Backboard", new Color(0.12f, 0.16f, 0.22f), 0.70f));

            EnsureMaterial($"{ModelsDir}/M_Hoop_FrameMetal.mat", "M_Hoop_FrameMetal", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Hoop_FrameMetal", new Color(0.18f, 0.20f, 0.24f), 0.65f, 0.85f));

            EnsureMaterial($"{ModelsDir}/M_Hoop_NeonTrim.mat", "M_Hoop_NeonTrim", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_Hoop_NeonTrim", new Color(0.0f, 0.85f, 1.0f), new Color(0.0f, 0.85f, 1.0f), 2.5f));

            EnsureMaterial($"{ModelsDir}/M_Hoop_TargetWhite.mat", "M_Hoop_TargetWhite", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_Hoop_TargetWhite", new Color(0.95f, 0.95f, 0.95f), new Color(0.95f, 0.95f, 0.95f), 1.2f));

            EnsureMaterial($"{ModelsDir}/M_Hoop_RimOrange.mat", "M_Hoop_RimOrange", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_Hoop_RimOrange", new Color(1.0f, 0.35f, 0.02f), new Color(1.0f, 0.35f, 0.02f), 1.5f));

            EnsureMaterial($"{ModelsDir}/M_Hoop_NetWhite.mat", "M_Hoop_NetWhite", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Hoop_NetWhite", new Color(0.95f, 0.95f, 0.92f), 0.25f, 0.0f));

            EnsureMaterial($"{ModelsDir}/M_Hoop_DisplayGlass.mat", "M_Hoop_DisplayGlass", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Hoop_DisplayGlass", new Color(0.05f, 0.06f, 0.08f), 0.85f, 0.30f));

            // Ball Materials
            EnsureMaterial($"{ModelsDir}/M_Basketball_Rubber.mat", "M_Basketball_Rubber", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Basketball_Rubber", new Color(0.88f, 0.38f, 0.06f), 0.25f, 0.05f));

            EnsureMaterial($"{ModelsDir}/M_Basketball_Seams.mat", "M_Basketball_Seams", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Basketball_Seams", new Color(0.08f, 0.08f, 0.09f), 0.20f, 0.10f));

            // Button Materials
            EnsureMaterial($"{ModelsDir}/M_ArcadeBtn_Chassis.mat", "M_ArcadeBtn_Chassis", () =>
                MaterialShaderHelper.CreateLitMaterial("M_ArcadeBtn_Chassis", new Color(0.12f, 0.13f, 0.16f), 0.40f, 0.70f));

            EnsureMaterial($"{ModelsDir}/M_ArcadeBtn_Chrome.mat", "M_ArcadeBtn_Chrome", () =>
                MaterialShaderHelper.CreateLitMaterial("M_ArcadeBtn_Chrome", new Color(0.85f, 0.88f, 0.92f), 0.90f, 0.95f));

            EnsureMaterial($"{ModelsDir}/M_ArcadeBtn_Bezel.mat", "M_ArcadeBtn_Bezel", () =>
                MaterialShaderHelper.CreateLitMaterial("M_ArcadeBtn_Bezel", new Color(0.05f, 0.05f, 0.06f), 0.30f, 0.20f));

            EnsureMaterial($"{ModelsDir}/M_ArcadeBtn_Dome.mat", "M_ArcadeBtn_Dome", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_ArcadeBtn_Dome", new Color(0.95f, 0.15f, 0.12f), new Color(0.95f, 0.15f, 0.12f), 2.0f));

            EnsureMaterial($"{ModelsDir}/M_ArcadeBtn_Label.mat", "M_ArcadeBtn_Label", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_ArcadeBtn_Label", new Color(0.0f, 0.85f, 1.0f), new Color(0.0f, 0.85f, 1.0f), 2.0f));
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

        public static PhysicsMaterial EnsurePhysicMaterial()
        {
            string pmPath = $"{ModelsDir}/PM_Basketball_Bouncy.physicMaterial";
            PhysicsMaterial pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(pmPath);
            if (pm == null)
            {
                pm = new PhysicsMaterial("PM_Basketball_Bouncy")
                {
                    bounciness = 0.76f,
                    bounceCombine = PhysicsMaterialCombine.Maximum,
                    dynamicFriction = 0.45f,
                    staticFriction = 0.50f,
                    frictionCombine = PhysicsMaterialCombine.Average
                };
                AssetDatabase.CreateAsset(pm, pmPath);
            }
            return pm;
        }

        private static GameObject BuildBasketballPrefab(PhysicsMaterial pmBouncy)
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(BallModelPath);
            if (fbx == null)
            {
                Debug.LogError($"[TASK-08] Modelo FBX da MiniBasketball não encontrado em {BallModelPath}");
                return null;
            }

            GameObject root = new GameObject("MiniBasketball");
            root.layer = LayerMask.NameToLayer("Default");
            root.tag = "Interactable";

            GameObject visual = UnityEngine.Object.Instantiate(fbx, root.transform);
            visual.name = "Visual_Model";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            // Remove colisores importados
            foreach (var col in visual.GetComponentsInChildren<Collider>())
            {
                UnityEngine.Object.DestroyImmediate(col);
            }

            // Aplica materiais de borracha e sulcos
            Material matRubber = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Basketball_Rubber.mat");
            Material matSeams = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Basketball_Seams.mat");

            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>())
            {
                Material[] mats = mr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && mats[i].name.Contains("Seams")) mats[i] = matSeams;
                    else mats[i] = matRubber;
                }
                mr.sharedMaterials = mats;
            }

            // 1. SphereCollider Físico
            SphereCollider sphereCol = root.AddComponent<SphereCollider>();
            sphereCol.radius = 0.09f; // Diâmetro de 0.18m
            sphereCol.material = pmBouncy;

            // 2. Rigidbody com física realista de basquete
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.45f; // 450 gramas
            rb.linearDamping = 0.08f;
            rb.angularDamping = 0.15f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // 3. ErgonomicGrabInteractable (XRI + Force Grab + Auto-Respawn)
            ErgonomicGrabInteractable grab = root.AddComponent<ErgonomicGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.smoothPosition = true;
            grab.smoothPositionAmount = 12f;
            grab.smoothRotation = true;
            grab.smoothRotationAmount = 12f;
            grab.throwOnDetach = true;

            // 4. ThrowAimAssist (Compensação ergonômica de arremesso para pacientes)
            ThrowAimAssist aimAssist = root.AddComponent<ThrowAimAssist>();
            SerializedObject soAim = new SerializedObject(aimAssist);
            soAim.FindProperty("throwVelocityMultiplier").floatValue = 1.35f;
            soAim.FindProperty("aimAssistStrength").floatValue = 0.65f;
            soAim.FindProperty("maxConeAngleDegrees").floatValue = 42.0f;
            soAim.FindProperty("maxTargetDistance").floatValue = 15.0f;
            soAim.ApplyModifiedPropertiesWithoutUndo();

            // 5. BasketballBallAudio (Quiques e rastreamento de impacto com aro)
            BasketballBallAudio ballAudio = root.AddComponent<BasketballBallAudio>();
            ballAudio.SetBounceClip(AssetDatabase.LoadAssetAtPath<AudioClip>(SfxBouncePath));

            string savePath = $"{PrefabsDir}/MiniBasketball.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log($"[TASK-08] Prefab MiniBasketball salvo em {savePath}");
            return savedPrefab;
        }

        private static GameObject BuildHoopPrefab()
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(HoopModelPath);
            if (fbx == null)
            {
                Debug.LogError($"[TASK-08] Modelo FBX da Tabela de Basquete não encontrado em {HoopModelPath}");
                return null;
            }

            GameObject root = new GameObject("ArcadeBasketballHoop");
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

            // Atribuição de Materiais
            Material matBackboard = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Hoop_Backboard.mat");
            Material matMetal = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Hoop_FrameMetal.mat");
            Material matNeon = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Hoop_NeonTrim.mat");
            Material matTarget = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Hoop_TargetWhite.mat");
            Material matRim = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Hoop_RimOrange.mat");
            Material matNet = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Hoop_NetWhite.mat");
            Material matGlass = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Hoop_DisplayGlass.mat");

            Renderer hoopRenderer = visual.GetComponentInChildren<Renderer>();
            if (hoopRenderer != null)
            {
                Material[] mats = hoopRenderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string mName = mats[i].name;
                    if (mName.Contains("Backboard")) mats[i] = matBackboard;
                    else if (mName.Contains("NeonTrim")) mats[i] = matNeon;
                    else if (mName.Contains("TargetWhite")) mats[i] = matTarget;
                    else if (mName.Contains("RimOrange")) mats[i] = matRim;
                    else if (mName.Contains("NetWhite")) mats[i] = matNet;
                    else if (mName.Contains("DisplayGlass")) mats[i] = matGlass;
                    else mats[i] = matMetal;
                }
                hoopRenderer.sharedMaterials = mats;
            }

            // 1. Colisor Sólido da Tabela (Backboard)
            BoxCollider backboardCol = root.AddComponent<BoxCollider>();
            backboardCol.center = new Vector3(0f, 0f, 0.0125f);
            backboardCol.size = new Vector3(0.72f, 0.52f, 0.035f);

            // 2. Colisor Sólido do Aro (Anel de colisores em torno do diâmetro de 0.38m)
            // No espaço local Unity:
            // Y é vertical (altura), Z é profundidade (negativo é para a frente da tabela onde fica o aro)
            float rimCenterY = -0.13f;
            float rimCenterZ = -0.3425f;
            float rimRadius = 0.19f;

            GameObject rimColHolder = new GameObject("Hoop_Rim_Solid_Colliders");
            rimColHolder.transform.SetParent(root.transform, false);
            rimColHolder.tag = "Untagged";

            // 8 BoxColliders segmentando o aro toroidal horizontal (plano XZ) para quiques realistas
            int rimSegments = 8;
            for (int i = 0; i < rimSegments; i++)
            {
                float angle = (float)i / rimSegments * Mathf.PI * 2f;
                float segX = Mathf.Cos(angle) * rimRadius;
                float segZ = rimCenterZ + Mathf.Sin(angle) * rimRadius;

                GameObject segGo = new GameObject($"Rim_Col_Segment_{i}");
                segGo.name = "Hoop_Rim_Segment";
                segGo.transform.SetParent(rimColHolder.transform, false);
                segGo.transform.localPosition = new Vector3(segX, rimCenterY, segZ);
                segGo.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);

                BoxCollider segBox = segGo.AddComponent<BoxCollider>();
                segBox.size = new Vector3(0.14f, 0.035f, 0.035f);
            }

            // 3. Duplo Sensor de Passagem (TriggerTop e TriggerBottom)
            // TriggerTop: Na boca superior do aro (Y = rimCenterY + 0.04m, plano XZ)
            GameObject topTriggerGo = new GameObject("Sensor_TriggerTop");
            topTriggerGo.transform.SetParent(root.transform, false);
            topTriggerGo.transform.localPosition = new Vector3(0f, rimCenterY + 0.04f, rimCenterZ);
            BoxCollider topCol = topTriggerGo.AddComponent<BoxCollider>();
            topCol.isTrigger = true;
            topCol.size = new Vector3(0.36f, 0.04f, 0.36f);

            // TriggerBottom: Abaixo da rede (Y = rimCenterY - 0.28m, plano XZ)
            GameObject botTriggerGo = new GameObject("Sensor_TriggerBottom");
            botTriggerGo.transform.SetParent(root.transform, false);
            botTriggerGo.transform.localPosition = new Vector3(0f, rimCenterY - 0.28f, rimCenterZ);
            BoxCollider botCol = botTriggerGo.AddComponent<BoxCollider>();
            botCol.isTrigger = true;
            botCol.size = new Vector3(0.26f, 0.06f, 0.26f);

            // 4. Luz de realce emissiva do aro
            GameObject rimLightGo = new GameObject("Rim_Glow_Light");
            rimLightGo.transform.SetParent(root.transform, false);
            rimLightGo.transform.localPosition = new Vector3(0f, rimCenterY, rimCenterZ);
            Light rimLight = rimLightGo.AddComponent<Light>();
            rimLight.type = LightType.Point;
            rimLight.range = 0.85f;
            rimLight.intensity = 0.8f;
            rimLight.color = new Color(1.0f, 0.40f, 0.05f);

            // 5. Partículas de Celebração de Cesta
            GameObject particlesGo = new GameObject("Score_Celebration_Particles");
            particlesGo.transform.SetParent(root.transform, false);
            particlesGo.transform.localPosition = new Vector3(0f, rimCenterY, rimCenterZ);
            ParticleSystem ps = particlesGo.AddComponent<ParticleSystem>();
            var mainPs = ps.main;
            mainPs.playOnAwake = false;
            mainPs.loop = false;
            mainPs.duration = 0.5f;
            mainPs.startLifetime = 0.7f;
            mainPs.startSpeed = 1.8f;
            mainPs.startSize = 0.04f;
            mainPs.startColor = new Color(1.0f, 0.85f, 0.2f);
            var emissionPs = ps.emission;
            emissionPs.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 25) });
            var shapePs = ps.shape;
            shapePs.shapeType = ParticleSystemShapeType.Circle;
            shapePs.radius = 0.18f;

            // 6. Placar Digital 3D (TextMeshPro)
            GameObject canvasGo = new GameObject("Scoreboard_Canvas_3D");
            canvasGo.transform.SetParent(root.transform, false);
            // Placar fica em Y = 0.35m (acima da tabela), Z = -0.092f (em frente à tela de vidro, voltado para a frente em -Z)
            canvasGo.transform.localPosition = new Vector3(0f, 0.35f, -0.092f);
            canvasGo.transform.localRotation = Quaternion.identity;

            // Texto de Notificação Superior / Título: "ARCADE HOOP"
            GameObject noticeGo = new GameObject("Text_Status_Notice");
            noticeGo.transform.SetParent(canvasGo.transform, false);
            noticeGo.transform.localPosition = new Vector3(0f, 0.040f, 0f);
            TextMeshPro textNotice = noticeGo.AddComponent<TextMeshPro>();
            textNotice.text = "ARCADE HOOP";
            textNotice.fontSize = 0.45f;
            textNotice.alignment = TextAlignmentOptions.Center;
            textNotice.color = new Color(0.0f, 0.90f, 1.0f);
            textNotice.textWrappingMode = TextWrappingModes.NoWrap;

            // Texto: Pontuação Atual (Centro, Dourado)
            GameObject scoreTextGo = new GameObject("Text_Score_Value");
            scoreTextGo.transform.SetParent(canvasGo.transform, false);
            scoreTextGo.transform.localPosition = new Vector3(0f, 0.005f, 0f);
            TextMeshPro textScore = scoreTextGo.AddComponent<TextMeshPro>();
            textScore.text = "00";
            textScore.fontSize = 0.85f;
            textScore.alignment = TextAlignmentOptions.Center;
            textScore.color = new Color(1.0f, 0.90f, 0.15f);
            textScore.textWrappingMode = TextWrappingModes.NoWrap;

            // Texto: Rótulo "PONTOS"
            GameObject labelScoreGo = new GameObject("Text_Score_Label");
            labelScoreGo.transform.SetParent(canvasGo.transform, false);
            labelScoreGo.transform.localPosition = new Vector3(0f, -0.035f, 0f);
            TextMeshPro textLabelScore = labelScoreGo.AddComponent<TextMeshPro>();
            textLabelScore.text = "PONTOS";
            textLabelScore.fontSize = 0.28f;
            textLabelScore.alignment = TextAlignmentOptions.Center;
            textLabelScore.color = new Color(0.85f, 0.85f, 0.90f);
            textLabelScore.textWrappingMode = TextWrappingModes.NoWrap;

            // Texto: Sequência (Streak) à esquerda
            GameObject streakGo = new GameObject("Text_Streak_Value");
            streakGo.transform.SetParent(canvasGo.transform, false);
            streakGo.transform.localPosition = new Vector3(-0.145f, 0.005f, 0f);
            TextMeshPro textStreak = streakGo.AddComponent<TextMeshPro>();
            textStreak.text = "x0";
            textStreak.fontSize = 0.75f;
            textStreak.alignment = TextAlignmentOptions.Center;
            textStreak.color = new Color(0.20f, 1.0f, 0.50f);
            textStreak.textWrappingMode = TextWrappingModes.NoWrap;

            GameObject labelStreakGo = new GameObject("Text_Streak_Label");
            labelStreakGo.transform.SetParent(canvasGo.transform, false);
            labelStreakGo.transform.localPosition = new Vector3(-0.145f, -0.035f, 0f);
            TextMeshPro textLabelStreak = labelStreakGo.AddComponent<TextMeshPro>();
            textLabelStreak.text = "STREAK";
            textLabelStreak.fontSize = 0.28f;
            textLabelStreak.alignment = TextAlignmentOptions.Center;
            textLabelStreak.color = new Color(0.65f, 0.65f, 0.75f);
            textLabelStreak.textWrappingMode = TextWrappingModes.NoWrap;

            // Texto: Recorde (High Score) à direita
            GameObject highGo = new GameObject("Text_HighScore_Value");
            highGo.transform.SetParent(canvasGo.transform, false);
            highGo.transform.localPosition = new Vector3(0.145f, 0.005f, 0f);
            TextMeshPro textHigh = highGo.AddComponent<TextMeshPro>();
            textHigh.text = "00";
            textHigh.fontSize = 0.75f;
            textHigh.alignment = TextAlignmentOptions.Center;
            textHigh.color = new Color(1.0f, 0.45f, 0.15f);
            textHigh.textWrappingMode = TextWrappingModes.NoWrap;

            GameObject labelHighGo = new GameObject("Text_High_Label");
            labelHighGo.transform.SetParent(canvasGo.transform, false);
            labelHighGo.transform.localPosition = new Vector3(0.145f, -0.035f, 0f);
            TextMeshPro textLabelHigh = labelHighGo.AddComponent<TextMeshPro>();
            textLabelHigh.text = "RECORDE";
            textLabelHigh.fontSize = 0.28f;
            textLabelHigh.alignment = TextAlignmentOptions.Center;
            textLabelHigh.color = new Color(0.65f, 0.65f, 0.75f);
            textLabelHigh.textWrappingMode = TextWrappingModes.NoWrap;

            // 7. Componente BasketballHoopScorer
            BasketballHoopScorer scorer = root.AddComponent<BasketballHoopScorer>();
            SerializedObject soScorer = new SerializedObject(scorer);
            soScorer.FindProperty("topTriggerCollider").objectReferenceValue = topCol;
            soScorer.FindProperty("bottomTriggerCollider").objectReferenceValue = botCol;
            soScorer.FindProperty("rimSolidCollider").objectReferenceValue = rimColHolder.GetComponentInChildren<Collider>();
            soScorer.FindProperty("scoreText").objectReferenceValue = textScore;
            soScorer.FindProperty("streakText").objectReferenceValue = textStreak;
            soScorer.FindProperty("highScoreText").objectReferenceValue = textHigh;
            soScorer.FindProperty("statusNoticeText").objectReferenceValue = textNotice;
            soScorer.FindProperty("rimRenderer").objectReferenceValue = hoopRenderer;
            soScorer.FindProperty("celebrationParticles").objectReferenceValue = ps;
            soScorer.FindProperty("rimGlowLight").objectReferenceValue = rimLight;
            soScorer.FindProperty("netTransform").objectReferenceValue = visual.transform;
            soScorer.FindProperty("sfxSwish").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxSwishPath);
            soScorer.FindProperty("sfxRimClang").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxClangPath);
            soScorer.FindProperty("sfxBuzzer").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxBuzzerPath);
            soScorer.FindProperty("aimAssistOffset").vector3Value = new Vector3(0f, rimCenterY, rimCenterZ);
            soScorer.FindProperty("aimAssistRadius").floatValue = 0.35f;
            soScorer.ApplyModifiedPropertiesWithoutUndo();

            // Encaminhadores de Trigger
            HoopSensorForwarder topForwarder = topTriggerGo.AddComponent<HoopSensorForwarder>();
            topForwarder.sensorType = HoopSensorForwarder.SensorType.Top;
            topForwarder.scorer = scorer;

            HoopSensorForwarder botForwarder = botTriggerGo.AddComponent<HoopSensorForwarder>();
            botForwarder.sensorType = HoopSensorForwarder.SensorType.Bottom;
            botForwarder.scorer = scorer;

            string savePath = $"{PrefabsDir}/ArcadeBasketballHoop.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log($"[TASK-08] Prefab ArcadeBasketballHoop salvo em {savePath}");
            return savedPrefab;
        }

        private static GameObject BuildButtonPrefab(GameObject prefabBall)
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonModelPath);
            if (fbx == null)
            {
                Debug.LogError($"[TASK-08] Modelo FBX do Botão Spawner não encontrado em {ButtonModelPath}");
                return null;
            }

            GameObject root = new GameObject("ArcadeBallSpawnerButton");
            root.layer = LayerMask.NameToLayer("Default");
            root.tag = "Interactable";

            GameObject visual = UnityEngine.Object.Instantiate(fbx, root.transform);
            visual.name = "Visual_Model";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            foreach (var col in visual.GetComponentsInChildren<Collider>())
            {
                UnityEngine.Object.DestroyImmediate(col);
            }

            // Atribuição de materiais
            Material matChassis = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_ArcadeBtn_Chassis.mat");
            Material matChrome = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_ArcadeBtn_Chrome.mat");
            Material matBezel = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_ArcadeBtn_Bezel.mat");
            Material matDome = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_ArcadeBtn_Dome.mat");
            Material matLabel = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_ArcadeBtn_Label.mat");

            Transform domeTransform = null;

            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>())
            {
                if (mr.gameObject.name.Contains("Dome"))
                {
                    domeTransform = mr.transform;
                    mr.sharedMaterial = matDome;
                }
                else if (mr.gameObject.name.Contains("Bolt")) mr.sharedMaterial = matChrome;
                else if (mr.gameObject.name.Contains("Bezel")) mr.sharedMaterial = matBezel;
                else if (mr.gameObject.name.Contains("Label")) mr.sharedMaterial = matLabel;
                else mr.sharedMaterial = matChassis;
            }

            // 1. BoxCollider Físico da Base
            BoxCollider baseCol = root.AddComponent<BoxCollider>();
            baseCol.center = new Vector3(0f, 0f, 0.025f);
            baseCol.size = new Vector3(0.12f, 0.12f, 0.05f);

            // 2. SphereCollider de Toque / Laser no Domo
            SphereCollider domeCol = root.AddComponent<SphereCollider>();
            domeCol.center = new Vector3(0f, 0f, 0.055f);
            domeCol.radius = 0.045f;
            domeCol.isTrigger = false;

            // 3. Ponto de Spawn da Bola (desova ligeiramente acima e à frente do botão)
            GameObject spawnPointGo = new GameObject("Ball_Spawn_Point");
            spawnPointGo.transform.SetParent(root.transform, false);
            spawnPointGo.transform.localPosition = new Vector3(0f, 0.16f, 0.06f);

            // 4. Luz de realce emissiva
            GameObject lightGo = new GameObject("Button_Glow_Light");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0f, 0.08f);
            Light btnLight = lightGo.AddComponent<Light>();
            btnLight.type = LightType.Point;
            btnLight.range = 0.35f;
            btnLight.intensity = 1.0f;
            btnLight.color = new Color(0.95f, 0.15f, 0.12f);

            // 5. ArcadeBallSpawner Component
            ArcadeBallSpawner spawner = root.AddComponent<ArcadeBallSpawner>();
            SerializedObject soBtn = new SerializedObject(spawner);
            soBtn.FindProperty("miniBasketballPrefab").objectReferenceValue = prefabBall;
            soBtn.FindProperty("ballSpawnPoint").objectReferenceValue = spawnPointGo.transform;
            soBtn.FindProperty("buttonDomeTransform").objectReferenceValue = domeTransform;
            soBtn.FindProperty("pressedLocalOffset").vector3Value = new Vector3(0f, 0f, -0.012f);
            soBtn.FindProperty("sfxButtonPress").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxButtonPressPath);
            soBtn.FindProperty("buttonGlowLight").objectReferenceValue = btnLight;
            soBtn.ApplyModifiedPropertiesWithoutUndo();

            string savePath = $"{PrefabsDir}/ArcadeBallSpawnerButton.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log($"[TASK-08] Prefab ArcadeBallSpawnerButton salvo em {savePath}");
            return savedPrefab;
        }

        public static void SetupMenuPrincipalScene(GameObject prefabHoop, GameObject prefabButton, GameObject prefabBall)
        {
            if (EditorSceneManager.GetActiveScene().path != MenuScenePath)
            {
                EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            }

            // Remove instâncias antigas da Task08
            string[] toClean = new string[] {
                "ArcadeBasketballHoop_Instance",
                "ArcadeBallSpawner_Instance",
                "Demo_Basketball_Table"
            };
            foreach (var cName in toClean)
            {
                GameObject existing = GameObject.Find(cName);
                if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
            }

            // 1. Instancia a Tabela de Basquete na parede lateral
            // Coordenadas: X = 0.12m, Y = 1.85m, Z = 2.70m, Rotação (0, -90, 0) para apontar para o interior da sala (+X)
            Vector3 hoopPos = new Vector3(0.12f, 1.85f, 2.70f);
            Quaternion hoopRot = Quaternion.Euler(0f, -90f, 0f);

            GameObject hoopInst = (GameObject)PrefabUtility.InstantiatePrefab(prefabHoop);
            hoopInst.name = "ArcadeBasketballHoop_Instance";
            hoopInst.transform.position = hoopPos;
            hoopInst.transform.rotation = hoopRot;

            // 2. Instancia o Botão Spawner na mesa ao alcance do paciente
            // Coordenadas: X = 0.80m, Y = 0.955m, Z = 0.15m, Rotação (0, 75, 0)
            Vector3 btnPos = new Vector3(0.80f, 0.955f, 0.15f);
            Quaternion btnRot = Quaternion.Euler(0f, 75f, 0f);

            GameObject btnInst = (GameObject)PrefabUtility.InstantiatePrefab(prefabButton);
            btnInst.name = "ArcadeBallSpawner_Instance";
            btnInst.transform.position = btnPos;
            btnInst.transform.rotation = btnRot;

            // 3. Instancia uma MiniBasketball de demonstração em repouso sobre o tampo da mesa
            Vector3 ballPos = new Vector3(0.64f, 1.045f, -0.02f);
            Quaternion ballRot = Quaternion.Euler(15f, 30f, 0f);

            GameObject ballInst = (GameObject)PrefabUtility.InstantiatePrefab(prefabBall);
            ballInst.name = "Demo_Basketball_Table";
            ballInst.transform.position = ballPos;
            ballInst.transform.rotation = ballRot;

            Rigidbody ballRb = ballInst.GetComponent<Rigidbody>();
            if (ballRb != null)
            {
                ballRb.isKinematic = true; // Mantém estática e perfeitamente assentada no preview
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("[TASK-08] Cena Menu_Principal configurada com Tabela de Basquete, Botão Spawner e Minibola!");
        }

        public static void CaptureVisualQA()
        {
            string outDir = Path.GetFullPath(OutVisualQADir);
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            if (EditorSceneManager.GetActiveScene().path != MenuScenePath)
            {
                EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            }

            // 1. TASK-08_Basketball_Hoop_Wall.png: Visão da câmera do jogador sentado olhando para a tabela de basquete na parede
            string pathWall = Path.Combine(outDir, "TASK-08_Basketball_Hoop_Wall.png");
            Vector3 playerCamPos = new Vector3(4.50f, 1.67f, 2.31f);
            Vector3 hoopTarget = new Vector3(0.12f, 1.85f, 2.70f);
            VisualQACaptureEditor.CaptureCameraView(pathWall, playerCamPos, hoopTarget, 1920, 1080);
            Debug.Log($"[TASK-08] Visual QA 1 capturado: {pathWall}");

            // 2. TASK-08_Scoreboard_Closeup.png: Close-up nítido da tabela, aro, rede e placar digital
            string pathScoreboard = Path.Combine(outDir, "TASK-08_Scoreboard_Closeup.png");
            Vector3 closeCamPos = new Vector3(1.45f, 1.90f, 2.70f);
            Vector3 closeTarget = new Vector3(0.12f, 1.90f, 2.70f);
            VisualQACaptureEditor.CaptureCameraView(pathScoreboard, closeCamPos, closeTarget, 1920, 1080);
            Debug.Log($"[TASK-08] Visual QA 2 capturado: {pathScoreboard}");

            // 3. TASK-08_Ball_Spawn_Button.png: Visão da mesa com o botão arcade spawner e a minibola recém-invocada
            string pathButton = Path.Combine(outDir, "TASK-08_Ball_Spawn_Button.png");
            Vector3 deskCamPos = new Vector3(1.22f, 1.25f, 0.28f);
            Vector3 deskTarget = new Vector3(0.70f, 0.98f, 0.08f);
            VisualQACaptureEditor.CaptureCameraView(pathButton, deskCamPos, deskTarget, 1920, 1080);
            Debug.Log($"[TASK-08] Visual QA 3 capturado: {pathButton}");

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }
    }
}
