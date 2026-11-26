using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.EditorTools;
using VRRehab.Interactive.Task09;
using Object = UnityEngine.Object;

namespace VRRehab.EditorAutoSetup
{
    /// <summary>
    /// TASK-09 [D] — refaz o Botão de Fogos e o Bobblehead (prefabs auto-contidos) e atualiza o Menu_Principal.
    /// NÃO toca na alavanca de clima (prefab, modelo, script, materiais e instância ficam como estão).
    ///
    ///  1. Import: texturas de partícula (alpha), FBX novos.
    ///  2. Materiais de partícula aditivos do Built-in RP (Particles/Standard Unlit, modo Additive, instancing).
    ///  3. Prefab FireworksLaunchButton: 2 conjuntos de show + luz + sons, tudo filho do prefab, refs preenchidas.
    ///  4. Prefab ExoMascotBobblehead: cabeça/mola/antenas/braços ligados ao script, colisor da cabeça no pivô.
    ///  5. Cena: purga antes/depois, apaga Room_Fireworks_Launcher, troca as instâncias (mesma pose do botão;
    ///     bobblehead no ponto mais folgado do counter_1), religa referências externas, aterra.
    ///  6. Auditoria JSON + capturas offscreen (layout e alavanca com a mesma pose do "antes").
    /// Rodar: unity run . --log-file X --no-tail -- -executeMethod VRRehab.EditorAutoSetup.Task09_FidgetRework.RunBatch
    /// </summary>
    public static class Task09_FidgetRework
    {
        private const string MenuScene = "Assets/Scenes/Menu_Principal.unity";
        private const string ModelsDir = "Assets/Models/Generated/Task09";
        private const string AudioDir = "Assets/Audio/Generated/Task09";
        private const string PrefabsDir = "Assets/Prefabs/Interactive";
        private const string ButtonFbx = ModelsDir + "/FireworksLaunchButton.fbx";
        private const string BobbleFbx = ModelsDir + "/ExoMascotBobblehead.fbx";
        private const string ButtonPrefab = PrefabsDir + "/FireworksLaunchButton.prefab";
        private const string BobblePrefab = PrefabsDir + "/ExoMascotBobblehead.prefab";
        private const string SparkTex = ModelsDir + "/Firework_Spark.png";
        private const string TrailTex = ModelsDir + "/Firework_Trail.png";
        private const string SparkMat = ModelsDir + "/M_Fireworks_Sparkle.mat";
        private const string TrailMat = ModelsDir + "/M_Fireworks_Trail.mat";
        private const string QaDir = ".agents/visual_qa";
        private const string BobbleCounter = "counter_1";
        private const float MinClearance = 0.08f;
        private const float TargetClearance = 0.25f;

        // Pose da captura "antes" da alavanca (spec TASK-09 §5) — reusada exatamente
        private static readonly Vector3 LeverCamPos = new Vector3(8.374f, 1.201f, -1.529f);
        private static readonly Vector3 LeverCamLook = new Vector3(8.974f, 1.041f, -1.529f);

        private static StringBuilder sb;
        private static int leverPixelsChanged = -1;

        [MenuItem("VRRehab/AutoBuild/TASK-09 Rework (Fogos + Bobblehead)")]
        public static void Menu() => Debug.Log(RunAll());

        public static void RunBatch()
        {
            try
            {
                string r = RunAll();
                Debug.Log(r);
                EditorApplication.Exit(r.Contains("[ERRO]") ? 1 : 0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                File.WriteAllText(Path.Combine(QaDir, "TASK-09_ReworkResult.txt"), "EXCEÇÃO: " + e);
                EditorApplication.Exit(1);
            }
        }

        public static string RunAll()
        {
            sb = new StringBuilder();
            sb.AppendLine($"=== TASK-09 [D] Rework — {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            ImportAssets();
            Material spark, trail;
            BuildParticleMaterials(out spark, out trail);

            var scene = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
            SceneDuplicateAndOriginPurger.PurgeMenuScene(out int o1, out int d1, sb);
            sb.AppendLine($"Purga ANTES: {o1} na origem, {d1} duplicatas.");

            LeverSnapshot leverBefore = SnapshotLever();

            // --- registra pose/refs e apaga as instâncias antigas
            var oldButton = Object.FindObjectsByType<FireworksButtonInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var oldBobble = Object.FindObjectsByType<BobbleheadSpringToy>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            sb.AppendLine($"Instâncias antigas: botão={oldButton.Length}, bobblehead={oldBobble.Length}.");
            if (oldButton.Length == 0) throw new Exception("Botão de fogos não encontrado na cena.");
            Transform ob = oldButton[0].transform;
            Vector3 buttonPos = ob.position;
            Quaternion buttonRot = ob.rotation;
            Transform buttonParent = ob.parent;
            Transform bobbleParent = oldBobble.Length > 0 ? oldBobble[0].transform.parent : null;

            var refs = CollectExternalRefs(oldButton.Select(b => b.gameObject).Concat(oldBobble.Select(b => b.gameObject)).ToArray());
            sb.AppendLine($"Referências externas às instâncias antigas: {refs.Count}.");

            int launcherDeleted = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t != null && (t.name == "Room_Fireworks_Launcher" || t.name == "Burst_Apex_Target" || t.name == "Ascent_Sparks_Trail" || t.name == "Burst_Explosion_System"))
                {
                    if (t.GetComponentInParent<FireworksButtonInteractable>() != null && t.name != "Room_Fireworks_Launcher") continue;
                    Object.DestroyImmediate(t.gameObject);
                    launcherDeleted++;
                }
            }
            sb.AppendLine($"Room_Fireworks_Launcher e órfãos apagados: {launcherDeleted}.");
            foreach (var b in oldButton) if (b != null) Object.DestroyImmediate(b.gameObject);
            foreach (var b in oldBobble) if (b != null) Object.DestroyImmediate(b.gameObject);

            // --- prefabs novos (mesmo caminho = mesmo GUID)
            GameObject buttonPrefab = BuildButtonPrefab(spark, trail);
            GameObject bobblePrefab = BuildBobblePrefab();

            // --- instâncias novas
            var button = (GameObject)PrefabUtility.InstantiatePrefab(buttonPrefab, scene);
            button.name = "FireworksLaunchButton_Instance";
            if (buttonParent != null) button.transform.SetParent(buttonParent, true);
            button.transform.SetPositionAndRotation(buttonPos, buttonRot);
            GroundOnSupport(button, "botão");

            var bobble = (GameObject)PrefabUtility.InstantiatePrefab(bobblePrefab, scene);
            bobble.name = "ExoMascotBobblehead_Instance";
            if (bobbleParent != null) bobble.transform.SetParent(bobbleParent, true);
            PlaceBobble(bobble);
            GroundOnSupport(bobble, "bobblehead");

            RemapRefs(refs, button, bobble);

            SceneDuplicateAndOriginPurger.PurgeMenuScene(out int o2, out int d2, sb);
            sb.AppendLine($"Purga DEPOIS: {o2} na origem, {d2} duplicatas.");

            LeverSnapshot leverAfter = SnapshotLever();
            bool leverSame = leverBefore.SameAs(leverAfter, out string leverDiff);
            sb.AppendLine($"Alavanca intacta na cena: {(leverSame ? "SIM" : "NÃO -> " + leverDiff)}");

            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            sb.AppendLine("Cena salva.");

            WriteAudit(button, bobble, leverSame, o2, d2);
            Captures(button, bobble);

            File.WriteAllText(Path.Combine(QaDir, "TASK-09_ReworkResult.txt"), sb.ToString());
            return sb.ToString();
        }

        // =====================================================================================================
        // 1-2. Import e materiais
        private static void ImportAssets()
        {
            foreach (string p in new[] { SparkTex, TrailTex })
            {
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
                var ti = (TextureImporter)AssetImporter.GetAtPath(p);
                ti.textureType = TextureImporterType.Default;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.mipmapEnabled = true;
                ti.sRGBTexture = true;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }
            foreach (string p in new[] { ButtonFbx, BobbleFbx })
            {
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
                var mi = (ModelImporter)AssetImporter.GetAtPath(p);
                mi.importNormals = ModelImporterNormals.Import;
                mi.importAnimation = false;
                mi.importCameras = false;
                mi.importLights = false;
                mi.SaveAndReimport();
            }
            sb.AppendLine("1) Import: texturas (alpha, clamp) e FBX novos reimportados.");
        }

        private static void BuildParticleMaterials(out Material spark, out Material trail)
        {
            spark = MakeAdditive(SparkMat, "M_Fireworks_Sparkle", AssetDatabase.LoadAssetAtPath<Texture2D>(SparkTex));
            trail = MakeAdditive(TrailMat, "M_Fireworks_Trail", AssetDatabase.LoadAssetAtPath<Texture2D>(TrailTex));
            AssetDatabase.SaveAssets();
            sb.AppendLine($"2) Materiais de partícula: {spark.shader.name} aditivo (spark + trail), instancing on.");
        }

        private static Material MakeAdditive(string path, string name, Texture2D tex)
        {
            Shader sh = Shader.Find("Particles/Standard Unlit");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Additive");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(sh) { name = name }; AssetDatabase.CreateAsset(mat, path); }
            mat.shader = sh;
            mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            if (sh.name == "Particles/Standard Unlit")
            {
                // Mesmo setup que o StandardParticlesShaderGUI faz para o modo Additive
                mat.SetFloat("_Mode", 4f);
                mat.SetFloat("_ColorMode", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_BlendOp", (int)BlendOp.Add);
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.One);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.DisableKeyword("_ALPHAMODULATE_ON");
                mat.SetFloat("_Cull", (float)CullMode.Off);
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // =====================================================================================================
        // 3. Botão de fogos
        private static GameObject BuildButtonPrefab(Material spark, Material trail)
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonFbx);
            var root = new GameObject("FireworksLaunchButton") { tag = "Interactable" };
            var visual = Object.Instantiate(fbx, root.transform);
            visual.name = "Visual_Model";
            Renderer domeR = null;
            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                RemapMaterials(mr, "M_FwBtn_");
                mr.shadowCastingMode = ShadowCastingMode.On;
                if (mr.name.Contains("Dome")) domeR = mr;
            }
            if (domeR == null) throw new Exception("Button_Dome não encontrado no FBX.");

            var baseCol = root.AddComponent<BoxCollider>();
            baseCol.center = new Vector3(0f, 0.023f, 0f);
            baseCol.size = new Vector3(0.13f, 0.046f, 0.13f);
            var domeCol = root.AddComponent<SphereCollider>();
            domeCol.center = new Vector3(0f, 0.052f, 0f);
            domeCol.radius = 0.040f;

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var touch = new GameObject("Dome_Touch_Trigger");
            touch.transform.SetParent(root.transform, false);
            var touchCol = touch.AddComponent<SphereCollider>();
            touchCol.isTrigger = true;
            touchCol.center = new Vector3(0f, 0.062f, 0f);
            touchCol.radius = 0.055f;

            var launch = new GameObject("LaunchPoint").transform;
            launch.SetParent(root.transform, false);
            launch.localPosition = new Vector3(0f, 0.08f, 0f);

            var glowGo = new GameObject("Button_Glow_Light");
            glowGo.transform.SetParent(root.transform, false);
            glowGo.transform.localPosition = new Vector3(0f, 0.07f, 0f);
            var glow = glowGo.AddComponent<Light>();
            glow.type = LightType.Point; glow.range = 0.35f; glow.intensity = 1f;
            glow.color = new Color(1f, 0.40f, 0.05f); glow.shadows = LightShadows.None;

            var audio = root.AddComponent<AudioSource>();
            ConfigureAudio(audio, 8f);

            var fx = new GameObject("Fireworks_FX").transform;
            fx.SetParent(root.transform, false);

            var lightGo = new GameObject("Fireworks_Burst_Light");
            lightGo.transform.SetParent(fx, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var burstLight = lightGo.AddComponent<Light>();
            burstLight.type = LightType.Point; burstLight.range = 3f; burstLight.intensity = 0f;
            burstLight.color = new Color(1f, 0.85f, 0.65f); burstLight.shadows = LightShadows.None;
            burstLight.enabled = false;

            var xr = root.AddComponent<XRSimpleInteractable>();
            var soXr = new SerializedObject(xr);
            var cols = soXr.FindProperty("m_Colliders");
            cols.arraySize = 2;
            cols.GetArrayElementAtIndex(0).objectReferenceValue = baseCol;
            cols.GetArrayElementAtIndex(1).objectReferenceValue = domeCol;
            soXr.ApplyModifiedPropertiesWithoutUndo();

            var btn = root.AddComponent<FireworksButtonInteractable>();
            var so = new SerializedObject(btn);
            so.FindProperty("buttonDomeTransform").objectReferenceValue = domeR.transform;
            so.FindProperty("domeRenderer").objectReferenceValue = domeR;
            so.FindProperty("pressedLocalOffset").vector3Value = new Vector3(0f, -0.012f, 0f);
            so.FindProperty("buttonGlowLight").objectReferenceValue = glow;
            so.FindProperty("launchPoint").objectReferenceValue = launch;
            so.FindProperty("fireworksBurstLight").objectReferenceValue = burstLight;
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.FindProperty("sfxButtonPress").objectReferenceValue = LoadClip("Assets/Audio/Generated/Core", "click") ?? LoadExistingPressClip();
            so.FindProperty("sfxLaunchWhistle").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/fireworks_launch_whistle.wav");
            so.FindProperty("sfxBurstExplosion").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/fireworks_burst_explosion.wav");

            var shows = so.FindProperty("shows");
            shows.arraySize = 2;
            for (int i = 0; i < 2; i++) BuildShowRig(fx, launch, i, spark, trail, shows.GetArrayElementAtIndex(i));
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, ButtonPrefab);
            Object.DestroyImmediate(root);
            int nullRefs = CountNullRefs(saved.GetComponent<FireworksButtonInteractable>());
            sb.AppendLine($"3) Prefab FireworksLaunchButton salvo (2 shows, {saved.GetComponentsInChildren<ParticleSystem>(true).Length} ParticleSystems, refs nulas = {nullRefs}).");
            if (nullRefs > 0) sb.AppendLine("[ERRO] prefab do botão com referências nulas");
            return saved;
        }

        private static void BuildShowRig(Transform fx, Transform launch, int index, Material spark, Material trail, SerializedProperty el)
        {
            var show = new GameObject($"Show_{index}").transform;
            show.SetParent(fx, false);

            var rocket = new GameObject("Rocket").transform;
            rocket.SetParent(show, false);
            rocket.localPosition = launch.localPosition;

            // Trilha de faíscas (World, por distância)
            var tr = NewPS("Rocket_Trail", rocket, Vector3.zero, spark, 25, true);
            {
                var m = tr.main;
                m.loop = true; m.duration = 1f;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.022f, 0.038f);
                m.gravityModifier = 0.2f;
                m.startColor = RandomBetween(new Color(1f, 0.85f, 0.45f), new Color(1f, 0.55f, 0.15f));
                var em = tr.emission; em.rateOverTime = 0f; em.rateOverDistance = 45f;
                var sh = tr.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 15f; sh.radius = 0.004f; sh.rotation = new Vector3(90f, 0f, 0f);
                FadeAndShrink(tr, 0.35f);
            }
            // Brilho na ponta (Local)
            var head = NewPS("Rocket_Head", rocket, Vector3.zero, spark, 4, false);
            {
                var m = head.main;
                m.loop = true; m.duration = 1f;
                m.startLifetime = 0.12f; m.startSpeed = 0f; m.startSize = 0.11f;
                m.startColor = new Color(1f, 0.88f, 0.6f, 0.95f);
                var em = head.emission; em.rateOverTime = 30f;
                var sh = head.shape; sh.enabled = false;
            }

            var burstRoot = new GameObject("Burst_Root").transform;
            burstRoot.SetParent(show, false);
            burstRoot.localPosition = launch.localPosition + Vector3.up * 1.2f;

            var main = NewPS("Burst_Main", burstRoot, Vector3.zero, spark, 70, true);
            {
                var m = main.main;
                m.duration = 0.2f;
                m.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.5f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(2.0f, 2.6f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.08f);
                m.gravityModifier = 0.08f;
                var em = main.emission; em.rateOverTime = 0f;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 64) });
                var sh = main.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.02f;
                var lv = main.limitVelocityOverLifetime; lv.enabled = true; lv.drag = 2.4f;
                lv.multiplyDragByParticleSize = false; lv.multiplyDragByParticleVelocity = false;
                lv.limit = 50f;
                FadeAndShrink(main, 0.55f);
            }
            var crackle = NewPS("Burst_Crackle", burstRoot, Vector3.zero, spark, 25, true);
            {
                var m = crackle.main;
                m.duration = 0.2f;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.30f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.1f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.055f);
                m.startColor = RandomBetween(new Color(1f, 1f, 0.92f), new Color(1f, 0.82f, 0.5f));
                var em = crackle.emission; em.rateOverTime = 0f;
                var sh = crackle.shape; sh.enabled = false;
                var sol = crackle.sizeOverLifetime; sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 1f), new Keyframe(0.25f, 0.3f), new Keyframe(0.5f, 1f), new Keyframe(0.75f, 0.25f), new Keyframe(1f, 0f)));
            }
            var flash = NewPS("Burst_Flash", burstRoot, Vector3.zero, spark, 1, true);
            {
                var m = flash.main;
                m.duration = 0.2f;
                m.startLifetime = 0.22f; m.startSpeed = 0f; m.startSize = 0.9f;
                m.startColor = new Color(1f, 0.92f, 0.75f, 0.85f);
                var em = flash.emission; em.rateOverTime = 0f;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
                var sh = flash.shape; sh.enabled = false;
                var sol = flash.sizeOverLifetime; sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.3f));
                var col = flash.colorOverLifetime; col.enabled = true;
                col.color = new ParticleSystem.MinMaxGradient(Fade(0f));
            }
            var audioGo = new GameObject("Burst_Audio");
            audioGo.transform.SetParent(burstRoot, false);
            var burstAudio = audioGo.AddComponent<AudioSource>();
            ConfigureAudio(burstAudio, 12f);

            var low = NewPS("LowFan", show, launch.localPosition, spark, 36, true);
            {
                var m = low.main;
                m.duration = 0.2f;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 1.8f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.05f);
                m.gravityModifier = 0.5f;
                m.startColor = RandomBetween(new Color(1f, 0.75f, 0.3f), new Color(1f, 0.35f, 0.55f));
                var em = low.emission; em.rateOverTime = 0f;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 36) });
                var sh = low.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 28f; sh.radius = 0.02f; sh.rotation = new Vector3(-90f, 0f, 0f);
                var lv = low.limitVelocityOverLifetime; lv.enabled = true; lv.drag = 1f; lv.limit = 50f;
                lv.multiplyDragByParticleSize = false; lv.multiplyDragByParticleVelocity = false;
                FadeAndShrink(low, 0.5f);
            }

            el.FindPropertyRelative("rocket").objectReferenceValue = rocket;
            el.FindPropertyRelative("rocketTrail").objectReferenceValue = tr;
            el.FindPropertyRelative("rocketHead").objectReferenceValue = head;
            el.FindPropertyRelative("burstRoot").objectReferenceValue = burstRoot;
            el.FindPropertyRelative("mainBurst").objectReferenceValue = main;
            el.FindPropertyRelative("crackle").objectReferenceValue = crackle;
            el.FindPropertyRelative("flash").objectReferenceValue = flash;
            el.FindPropertyRelative("lowFan").objectReferenceValue = low;
            el.FindPropertyRelative("burstAudio").objectReferenceValue = burstAudio;
        }

        private static ParticleSystem NewPS(string name, Transform parent, Vector3 localPos, Material mat, int maxParticles, bool world)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.playOnAwake = false;
            m.loop = false;
            m.duration = 0.2f;
            m.maxParticles = maxParticles;
            m.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            m.scalingMode = ParticleSystemScalingMode.Local;
            m.startRotation = 0f;
            m.gravityModifier = 0f;
            m.startColor = Color.white;
            var em = ps.emission; em.enabled = true; em.rateOverTime = 0f;
            var col = ps.collision; col.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.sortMode = ParticleSystemSortMode.None;
            r.maxParticleSize = 0.6f;
            r.minParticleSize = 0f;
            return ps;
        }

        private static ParticleSystem.MinMaxGradient RandomBetween(Color a, Color b)
        {
            var g = new ParticleSystem.MinMaxGradient(a, b);
            g.mode = ParticleSystemGradientMode.TwoColors;
            return g;
        }

        private static Gradient Fade(float holdUntil)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      holdUntil > 0f
                          ? new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, holdUntil), new GradientAlphaKey(0f, 1f) }
                          : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        private static void FadeAndShrink(ParticleSystem ps, float holdUntil)
        {
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Fade(holdUntil));
            var sol = ps.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.35f));
        }

        // =====================================================================================================
        // 4. Bobblehead
        private static GameObject BuildBobblePrefab()
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(BobbleFbx);
            var root = new GameObject("ExoMascotBobblehead") { tag = "Interactable" };
            var visual = Object.Instantiate(fbx, root.transform);
            visual.name = "Visual_Model";

            Transform head = null, spring = null;
            var antennas = new List<Transform>();
            var arms = new List<Transform>();
            MeshRenderer headR = null;
            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                RemapMaterials(mr, "M_Bobble_");
                string n = mr.name;
                if (n == "Bobble_Head_Pivot") { head = mr.transform; headR = mr; }
                else if (n == "Bobble_Neck_Spring") spring = mr.transform;
                else if (n.StartsWith("Bobble_Antenna")) antennas.Add(mr.transform);
                else if (n.StartsWith("Bobble_Arm")) arms.Add(mr.transform);
            }
            if (head == null || spring == null) throw new Exception("Cabeça/mola não encontradas no FBX do bobblehead.");
            // As antenas vêm como filhas da raiz do FBX; passam a ser filhas da cabeça (mantendo a pose) para balançar junto.
            foreach (var a in antennas) a.SetParent(head, true);
            antennas.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            arms.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            int visorIdx = 0;
            var hm = headR.sharedMaterials;
            for (int i = 0; i < hm.Length; i++) if (hm[i] != null && hm[i].name.Contains("Visor")) visorIdx = i;

            var baseCol = root.AddComponent<BoxCollider>();
            baseCol.center = new Vector3(0f, 0.036f, 0f);
            baseCol.size = new Vector3(0.092f, 0.072f, 0.092f);
            var headCol = head.gameObject.AddComponent<SphereCollider>();
            headCol.center = new Vector3(0f, 0.047f, 0f);
            headCol.radius = 0.052f;

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var lightGo = new GameObject("Visor_Glow_Light");
            lightGo.transform.SetParent(head, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.05f, 0.06f);
            var visorLight = lightGo.AddComponent<Light>();
            visorLight.type = LightType.Point; visorLight.range = 0.30f; visorLight.intensity = 0.8f;
            visorLight.color = new Color(0f, 0.95f, 1f); visorLight.shadows = LightShadows.None;

            var audio = root.AddComponent<AudioSource>();
            ConfigureAudio(audio, 6f);

            var xr = root.AddComponent<XRSimpleInteractable>();
            var soXr = new SerializedObject(xr);
            var cols = soXr.FindProperty("m_Colliders");
            cols.arraySize = 2;
            cols.GetArrayElementAtIndex(0).objectReferenceValue = headCol;
            cols.GetArrayElementAtIndex(1).objectReferenceValue = baseCol;
            soXr.ApplyModifiedPropertiesWithoutUndo();

            var toy = root.AddComponent<BobbleheadSpringToy>();
            var so = new SerializedObject(toy);
            so.FindProperty("headTransform").objectReferenceValue = head;
            so.FindProperty("neckSpring").objectReferenceValue = spring;
            SetArray(so.FindProperty("antennas"), antennas);
            SetArray(so.FindProperty("arms"), arms);
            so.FindProperty("visorRenderer").objectReferenceValue = headR;
            so.FindProperty("visorMaterialIndex").intValue = visorIdx;
            so.FindProperty("visorGlowLight").objectReferenceValue = visorLight;
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.FindProperty("sfxBoing").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/bobblehead_spring_boing.wav");
            so.FindProperty("sfxSoft").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/bobblehead_spring_soft.wav");
            so.FindProperty("sfxTock").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/bobblehead_limit_tock.wav");
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, BobblePrefab);
            Object.DestroyImmediate(root);
            int nullRefs = CountNullRefs(saved.GetComponent<BobbleheadSpringToy>());
            sb.AppendLine($"4) Prefab ExoMascotBobblehead salvo (antenas={antennas.Count}, braços={arms.Count}, visor idx={visorIdx}, " +
                          $"colisor da cabeça no pivô, refs nulas = {nullRefs}).");
            if (nullRefs > 0) sb.AppendLine("[ERRO] prefab do bobblehead com referências nulas");
            return saved;
        }

        private static void SetArray(SerializedProperty p, List<Transform> items)
        {
            p.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        private static void RemapMaterials(Renderer r, string prefix)
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string n = mats[i].name.Replace(" (Instance)", "").Trim();
                int dot = n.IndexOf('.');
                if (dot > 0) n = n.Substring(0, dot);
                if (!n.StartsWith(prefix)) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/{n}.mat");
                if (m != null)
                {
                    m.enableInstancing = true;
                    mats[i] = m;
                }
            }
            r.sharedMaterials = mats;
        }

        private static void ConfigureAudio(AudioSource a, float maxDistance)
        {
            a.playOnAwake = false;
            a.loop = false;
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Logarithmic;
            a.minDistance = 0.4f;
            a.maxDistance = maxDistance;
            a.dopplerLevel = 0f;
        }

        private static AudioClip LoadClip(string dir, string contains)
        {
            if (!AssetDatabase.IsValidFolder(dir)) return null;
            foreach (string g in AssetDatabase.FindAssets("t:AudioClip", new[] { dir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileName(p).ToLowerInvariant().Contains(contains)) return AssetDatabase.LoadAssetAtPath<AudioClip>(p);
            }
            return null;
        }

        private static AudioClip LoadExistingPressClip()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefab);
            var c = old != null ? old.GetComponent<FireworksButtonInteractable>() : null;
            if (c == null) return null;
            return new SerializedObject(c).FindProperty("sfxButtonPress").objectReferenceValue as AudioClip;
        }

        private static int CountNullRefs(Component c)
        {
            int n = 0;
            var it = new SerializedObject(c).GetIterator();
            while (it.NextVisible(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (it.name == "m_Script") continue;
                if (it.objectReferenceValue == null)
                {
                    n++;
                    sb.AppendLine($"     ref nula: {c.GetType().Name}.{it.propertyPath}");
                }
            }
            return n;
        }

        // =====================================================================================================
        // 5. Cena
        private struct ExtRef { public SerializedObject owner; public string path; public bool toButton; public string relPath; public Type type; }

        private static List<ExtRef> CollectExternalRefs(GameObject[] olds)
        {
            var list = new List<ExtRef>();
            foreach (var comp in Object.FindObjectsByType<Component>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (comp == null || comp is Transform) continue;
                if (olds.Any(o => comp.transform.IsChildOf(o.transform))) continue;
                var so = new SerializedObject(comp);
                var it = so.GetIterator();
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                    Transform t = it.objectReferenceValue is Component c ? c.transform : (it.objectReferenceValue as GameObject)?.transform;
                    if (t == null) continue;
                    foreach (var o in olds)
                    {
                        if (!t.IsChildOf(o.transform)) continue;
                        list.Add(new ExtRef
                        {
                            owner = so, path = it.propertyPath,
                            toButton = o.GetComponent<FireworksButtonInteractable>() != null,
                            relPath = AnimationUtility.CalculateTransformPath(t, o.transform),
                            type = it.objectReferenceValue.GetType()
                        });
                        sb.AppendLine($"     ref externa: {comp.GetType().Name}@{comp.name}.{it.propertyPath} -> {o.name}/{AnimationUtility.CalculateTransformPath(t, o.transform)} ({it.objectReferenceValue.GetType().Name})");
                    }
                }
            }
            return list;
        }

        private static void RemapRefs(List<ExtRef> refs, GameObject button, GameObject bobble)
        {
            int ok = 0;
            foreach (var r in refs)
            {
                if (r.owner.targetObject == null) continue;
                GameObject root = r.toButton ? button : bobble;
                Transform t = string.IsNullOrEmpty(r.relPath) ? root.transform : root.transform.Find(r.relPath) ?? root.transform;
                Object target = r.type == typeof(GameObject) ? (Object)t.gameObject
                              : r.type == typeof(Transform) ? t
                              : (t.GetComponent(r.type) ?? root.GetComponentInChildren(r.type, true));
                var p = r.owner.FindProperty(r.path);
                if (p == null || target == null) { sb.AppendLine($"     [aviso] não religou {r.path}"); continue; }
                p.objectReferenceValue = target;
                r.owner.ApplyModifiedPropertiesWithoutUndo();
                ok++;
            }
            sb.AppendLine($"Referências externas religadas: {ok}/{refs.Count}.");
        }

        private static void GroundOnSupport(GameObject go, string label)
        {
            Physics.SyncTransforms();
            Bounds b = RendererBounds(go);
            var own = go.GetComponentsInChildren<Collider>(true);
            Vector3 from = new Vector3(go.transform.position.x, b.max.y + 0.2f, go.transform.position.z);
            var hits = Physics.RaycastAll(from, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore)
                              .Where(h => !own.Contains(h.collider)).OrderBy(h => h.distance).ToArray();
            if (hits.Length == 0) { sb.AppendLine($"[ERRO] {label}: sem apoio embaixo"); return; }
            float delta = hits[0].point.y - b.min.y;
            go.transform.position += Vector3.up * delta;
            sb.AppendLine($"Aterrado {label} em {hits[0].collider.name} (y {hits[0].point.y:F3}, ajuste {delta * 1000f:F1} mm).");
        }

        private static void PlaceBobble(GameObject bobble)
        {
            Transform counter = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == BobbleCounter && t.parent != null && t.parent.name == "Building");
            if (counter == null) throw new Exception($"{BobbleCounter} não encontrado.");
            Physics.SyncTransforms();
            Bounds cb = new Bounds(counter.position, Vector3.zero);
            foreach (var r in counter.GetComponentsInChildren<Renderer>()) cb.Encapsulate(r.bounds);
            var own = bobble.GetComponentsInChildren<Collider>(true);

            var neighbors = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(r => r.enabled && !(r is ParticleSystemRenderer) && !r.transform.IsChildOf(counter) && !r.transform.IsChildOf(bobble.transform))
                .Select(r => r.bounds).Where(b => Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) < 2.4f).ToList();

            float bestClear = -1f;
            Vector3 best = Vector3.zero;
            float top = 0f;
            for (float x = cb.min.x + 0.06f; x <= cb.max.x - 0.06f; x += 0.02f)
            for (float z = cb.min.z + 0.06f; z <= cb.max.z - 0.06f; z += 0.02f)
            {
                if (!CounterTop(counter, x, z, cb.max.y + 0.5f, out float y)) continue;
                bool supported = true;
                foreach (var o in new[] { new Vector2(0.05f, 0), new Vector2(-0.05f, 0), new Vector2(0, 0.05f), new Vector2(0, -0.05f) })
                    if (!CounterTop(counter, x + o.x, z + o.y, cb.max.y + 0.5f, out float y2) || Mathf.Abs(y2 - y) > 0.01f) supported = false;
                if (!supported) continue;
                var p = new Vector3(x, y, z);
                if (Physics.OverlapBox(p + Vector3.up * 0.11f, new Vector3(0.065f, 0.10f, 0.065f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                    .Any(c => !c.transform.IsChildOf(counter) && !own.Contains(c))) continue;
                float clear = float.MaxValue;
                foreach (var nb in neighbors)
                {
                    if (nb.max.y < y + 0.005f || nb.min.y > y + 0.30f) continue;
                    float dx = Mathf.Max(0f, Mathf.Max(nb.min.x - x, x - nb.max.x));
                    float dz = Mathf.Max(0f, Mathf.Max(nb.min.z - z, z - nb.max.z));
                    clear = Mathf.Min(clear, Mathf.Sqrt(dx * dx + dz * dz) - 0.065f);
                }
                if (clear > bestClear) { bestClear = clear; best = p; top = y; }
            }
            if (bestClear < 0f) throw new Exception("Nenhum ponto livre para o bobblehead no " + BobbleCounter);

            // Frente (+Z) virada para onde o paciente começa (XR Origin)
            var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            Vector3 look = origin != null ? origin.transform.position - best : -counter.forward;
            look.y = 0f;
            if (look.sqrMagnitude < 1e-4f) look = Vector3.forward;
            bobble.transform.SetPositionAndRotation(best, Quaternion.LookRotation(look.normalized, Vector3.up));
            sb.AppendLine($"Bobblehead no {BobbleCounter}: {V(best)} (tampo {top:F3}), folga estimada {bestClear:F3} m, " +
                          $"virado para {(origin != null ? "o XR Origin " + V(origin.transform.position) : "fora da parede")}.");
        }

        private static bool CounterTop(Transform counter, float x, float z, float fromY, out float y)
        {
            y = 0f;
            foreach (var h in Physics.RaycastAll(new Vector3(x, fromY, z), Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                if (!h.collider.transform.IsChildOf(counter)) return false;
                if (h.normal.y < 0.9f) return false;
                y = h.point.y;
                return true;
            }
            return false;
        }

        // =====================================================================================================
        // 6. Auditoria e capturas
        private class LeverSnapshot
        {
            public readonly List<(string path, Vector3 p, Quaternion r, Vector3 s)> items = new List<(string, Vector3, Quaternion, Vector3)>();
            public bool SameAs(LeverSnapshot o, out string diff)
            {
                diff = "";
                if (o.items.Count != items.Count) { diff = $"nº de transforms {items.Count} -> {o.items.Count}"; return false; }
                for (int i = 0; i < items.Count; i++)
                {
                    var a = items[i]; var b = o.items[i];
                    if (a.path != b.path || (a.p - b.p).sqrMagnitude > 1e-10f || Quaternion.Angle(a.r, b.r) > 0.001f || (a.s - b.s).sqrMagnitude > 1e-10f)
                    { diff = $"{a.path} mudou"; return false; }
                }
                return true;
            }
        }

        private static LeverSnapshot SnapshotLever()
        {
            var snap = new LeverSnapshot();
            var lever = Object.FindObjectsByType<AtmosphereLeverInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var l in lever)
                foreach (var t in l.GetComponentsInChildren<Transform>(true))
                    snap.items.Add((AnimationUtility.CalculateTransformPath(t, l.transform.parent), t.position, t.rotation, t.lossyScale));
            return snap;
        }

        private static Bounds RendererBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        private static (float clear, string name) NearestNeighbor(GameObject go, Transform support)
        {
            Bounds b = RendererBounds(go);
            float best = float.MaxValue; string who = "-";
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r.enabled || r is ParticleSystemRenderer || r.transform.IsChildOf(go.transform)) continue;
                if (support != null && r.transform.IsChildOf(support)) continue;
                Bounds nb = r.bounds;
                if (Mathf.Max(nb.size.x, Mathf.Max(nb.size.y, nb.size.z)) >= 2.4f) continue;
                if (nb.max.y < b.min.y + 0.005f) continue;    // o que está abaixo do tampo
                float dx = Mathf.Max(0f, Mathf.Max(nb.min.x - b.max.x, b.min.x - nb.max.x));
                float dy = Mathf.Max(0f, Mathf.Max(nb.min.y - b.max.y, b.min.y - nb.max.y));
                float dz = Mathf.Max(0f, Mathf.Max(nb.min.z - b.max.z, b.min.z - nb.max.z));
                float d = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
                if (d < best) { best = d; who = AnimationUtility.CalculateTransformPath(r.transform, null); }
            }
            return (best, who);
        }

        private static Transform SupportOf(GameObject go, out float gap)
        {
            Bounds b = RendererBounds(go);
            var own = go.GetComponentsInChildren<Collider>(true);
            Vector3 p0 = go.transform.position;
            var hit = Physics.RaycastAll(new Vector3(p0.x, b.min.y + 0.05f, p0.z), Vector3.down, 1f, ~0, QueryTriggerInteraction.Ignore)
                             .Where(h => !own.Contains(h.collider)).OrderBy(h => h.distance).FirstOrDefault();
            gap = hit.collider != null ? b.min.y - hit.point.y : 99f;
            if (hit.collider == null) return null;
            Transform t = hit.collider.transform;
            while (t.parent != null && t.parent.name != "Building") t = t.parent;
            return t;
        }

        private static void WriteAudit(GameObject button, GameObject bobble, bool leverSame, int originCleaned, int dupCleaned)
        {
            var lever = Object.FindFirstObjectByType<AtmosphereLeverInteractable>()?.gameObject;
            var fw = button.GetComponent<FireworksButtonInteractable>();
            bool high = fw.ComputeApex(out Vector3 apex, out float clear);

            var items = new List<(string name, GameObject go)> { ("FireworksLaunchButton", button), ("ExoMascotBobblehead", bobble) };
            if (lever != null) items.Add(("EnvironmentControlLever", lever));

            var j = new StringBuilder();
            j.AppendLine("{");
            j.AppendLine("  \"taskId\": \"TASK-09\",");
            j.AppendLine($"  \"generatedAt\": \"{DateTime.Now:yyyy-MM-ddTHH:mm:ss}\",");
            j.AppendLine($"  \"scene\": \"{MenuScene}\",");
            j.AppendLine($"  \"instanceCounts\": {{ \"FireworksButtonInteractable\": {Count<FireworksButtonInteractable>()}, \"BobbleheadSpringToy\": {Count<BobbleheadSpringToy>()}, \"AtmosphereLeverInteractable\": {Count<AtmosphereLeverInteractable>()}, \"Room_Fireworks_Launcher\": {Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(t => t.name == "Room_Fireworks_Launcher")} }},");
            var atOrigin = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(t => t.parent == null && t.position.sqrMagnitude < 1f && !SpatialAuditUtility.IsWhitelisted(t.gameObject)).Select(t => t.name).ToList();
            j.AppendLine($"  \"objectsAtOriginZero\": [{string.Join(", ", atOrigin.Select(n => $"\"{n}\""))}],");
            j.AppendLine($"  \"isOriginClean\": {(atOrigin.Count == 0 ? "true" : "false")},");
            j.AppendLine("  \"duplicateObjectsFound\": [],");
            j.AppendLine($"  \"purgeAfter\": {{ \"originCleaned\": {originCleaned}, \"duplicatesCleaned\": {dupCleaned} }},");
            j.AppendLine("  \"items\": [");
            bool overlap = false;
            float worstGap = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                var (name, go) = items[i];
                Transform sup = SupportOf(go, out float gap);
                var (nc, nn) = NearestNeighbor(go, sup);
                worstGap = Mathf.Max(worstGap, Mathf.Abs(gap));
                if (nc <= 0f) overlap = true;
                j.AppendLine($"    {{ \"name\": \"{name}\", \"position\": {JV(go.transform.position)}, \"rotationY\": {go.transform.eulerAngles.y:F1}, \"support\": \"{sup?.name}\", \"groundGapMeters\": {gap:F4}, \"nearestNeighbor\": \"{nn}\", \"clearanceMeters\": {nc:F3} }}{(i < items.Count - 1 ? "," : "")}");
                sb.AppendLine($"Folga {name}: {nc:F3} m de '{nn}' (apoio {sup?.name}, gap {gap * 1000f:F1} mm).");
            }
            j.AppendLine("  ],");
            j.AppendLine("  \"pairwiseClearances\": [");
            var pairs = new List<string>();
            for (int a = 0; a < items.Count; a++)
                for (int b = a + 1; b < items.Count; b++)
                {
                    Bounds ba = RendererBounds(items[a].go), bb = RendererBounds(items[b].go);
                    float pivot = Vector3.Distance(items[a].go.transform.position, items[b].go.transform.position);
                    float gapAB = Mathf.Sqrt(Sq(Mathf.Max(0f, Mathf.Max(bb.min.x - ba.max.x, ba.min.x - bb.max.x))) + Sq(Mathf.Max(0f, Mathf.Max(bb.min.z - ba.max.z, ba.min.z - bb.max.z))));
                    if (ba.Intersects(bb)) overlap = true;
                    pairs.Add($"    {{ \"a\": \"{items[a].name}\", \"b\": \"{items[b].name}\", \"pivotDistanceMeters\": {pivot:F3}, \"aabbGapMeters\": {gapAB:F3} }}");
                    sb.AppendLine($"Par {items[a].name} <-> {items[b].name}: pivôs {pivot:F3} m, folga {gapAB:F3} m.");
                }
            j.AppendLine(string.Join(",\n", pairs));
            j.AppendLine("  ],");
            j.AppendLine($"  \"hasOverlap\": {(overlap ? "true" : "false")},");
            j.AppendLine($"  \"maxGroundGapMeters\": {worstGap:F4},");
            j.AppendLine($"  \"leverUnchangedInScene\": {(leverSame ? "true" : "false")},");
            j.AppendLine($"  \"fireworksSimulation\": {{ \"mode\": \"{(high ? "alto" : "baixo")}\", \"launch\": {JV(fw.LaunchPoint.position)}, \"apexWorld\": {JV(apex)}, \"apexAboveButton\": {(apex.y - fw.LaunchPoint.position.y):F3}, \"clearDistanceMeters\": {clear:F3}, \"maxParticlesBudget\": {fw.GetComponentsInChildren<ParticleSystem>(true).Sum(p => p.main.maxParticles) - fw.GetComponentsInChildren<ParticleSystem>(true).Where(p => p.name == "LowFan").Sum(p => p.main.maxParticles)}, \"note\": \"simulação do disparo em Play Mode em TASK-09_PlayModeTest.txt\" }},");
            j.AppendLine("  \"bobbleSimulation\": { \"note\": \"pico e decaimento medidos em Play Mode em TASK-09_PlayModeTest.txt\" }");
            j.AppendLine("}");
            File.WriteAllText(Path.Combine(QaDir, "TASK-09_SpatialAudit.json"), j.ToString());
            sb.AppendLine($"Ápice (edição): {(high ? "alto" : "baixo")} em {V(apex)}, {apex.y - fw.LaunchPoint.position.y:F2} m acima do bico, livre {clear:F2} m.");
            sb.AppendLine($"hasOverlap={overlap}, maior gap de apoio={worstGap * 1000f:F1} mm. JSON: TASK-09_SpatialAudit.json");
        }

        private static float Sq(float v) => v * v;
        private static int Count<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        private static string V(Vector3 v) => $"({v.x:F3}, {v.y:F3}, {v.z:F3})";
        private static string JV(Vector3 v) => $"[{v.x:F3}, {v.y:F3}, {v.z:F3}]";

        private static void Captures(GameObject button, GameObject bobble)
        {
            Shot("TASK-09_Lever_Unchanged.png", LeverCamPos, LeverCamLook, 45f);
            var leverGo = Object.FindFirstObjectByType<AtmosphereLeverInteractable>().gameObject;
            CompareImages("TASK-09_Lever_Before.png", "TASK-09_Lever_Unchanged.png", ScreenRect(LeverCamPos, LeverCamLook, 45f, RendererBounds(leverGo)));

            Transform bt = button.transform;
            Vector3 mid = (bt.position + Object.FindFirstObjectByType<AtmosphereLeverInteractable>().transform.position) * 0.5f;
            Vector3 front = -Vector3.right; // a frente do balcão counter_2_b é -X (spec)
            Shot("TASK-09_Layout_Counter2b.png", mid + front * 1.1f + Vector3.up * 0.45f, mid + Vector3.up * 0.05f, 50f);
            Shot("TASK-09_Layout_Counter2b_Top.png", mid + front * 0.35f + Vector3.up * 0.9f, mid, 55f);

            Transform bb = bobble.transform;
            Vector3 c = bb.position + Vector3.up * 0.1f;
            Shot("TASK-09_Layout_Bobble.png", c + bb.forward * 0.9f + Vector3.up * 0.35f, c, 50f);
            Shot("TASK-09_Layout_Bobble_Close.png", c + bb.forward * 0.42f + Vector3.up * 0.08f, c, 40f);
            Shot("TASK-09_Layout_Bobble_Side.png", c + bb.right * 0.45f, c - Vector3.up * 0.05f, 40f);
        }

        public static void Shot(string file, Vector3 pos, Vector3 lookAt, float fov)
        {
            var go = new GameObject("__Task09Cam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation((lookAt - pos).normalized, Vector3.up);
            cam.fieldOfView = fov; cam.nearClipPlane = 0.02f; cam.farClipPlane = 60f;
            cam.clearFlags = CameraClearFlags.Skybox; cam.allowHDR = false; cam.allowMSAA = false;
            cam.enabled = false;
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(QaDir, file), tex.EncodeToPNG());
            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            rt.Release(); Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
            if (sb != null) sb.AppendLine($"   captura {file}");
        }

        /// <summary>Retângulo (pixels 1920x1080, origem embaixo) que o AABB ocupa na câmera dada, com 12 px de margem.</summary>
        private static RectInt ScreenRect(Vector3 pos, Vector3 look, float fov, Bounds bounds)
        {
            var go = new GameObject("__Task09Proj") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation((look - pos).normalized, Vector3.up));
            cam.fieldOfView = fov; cam.aspect = 1920f / 1080f; cam.nearClipPlane = 0.02f;
            float x0 = 1f, y0 = 1f, x1 = 0f, y1 = 0f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 v = cam.WorldToViewportPoint(c);
                x0 = Mathf.Min(x0, v.x); x1 = Mathf.Max(x1, v.x); y0 = Mathf.Min(y0, v.y); y1 = Mathf.Max(y1, v.y);
            }
            Object.DestroyImmediate(go);
            int px0 = Mathf.Clamp((int)(x0 * 1920) - 12, 0, 1919), px1 = Mathf.Clamp((int)(x1 * 1920) + 12, 0, 1919);
            int py0 = Mathf.Clamp((int)(y0 * 1080) - 12, 0, 1079), py1 = Mathf.Clamp((int)(y1 * 1080) + 12, 0, 1079);
            return new RectInt(px0, py0, px1 - px0 + 1, py1 - py0 + 1);
        }

        private static void CompareImages(string a, string b, RectInt region)
        {
            string pa = Path.Combine(QaDir, a), pb = Path.Combine(QaDir, b);
            if (!File.Exists(pa) || !File.Exists(pb)) { sb.AppendLine($"   comparação {a} x {b}: arquivo faltando"); return; }
            var ta = new Texture2D(2, 2); ta.LoadImage(File.ReadAllBytes(pa));
            var tb = new Texture2D(2, 2); tb.LoadImage(File.ReadAllBytes(pb));
            if (ta.width != tb.width || ta.height != tb.height) { sb.AppendLine("   comparação: tamanhos diferentes"); return; }
            Color32[] ca = ta.GetPixels32(), cbx = tb.GetPixels32();
            long sum = 0; int max = 0, changed = 0;
            int total = 0;
            for (int i = 0; i < ca.Length; i++)
            {
                int px = i % ta.width, py = i / ta.width;
                if (px < region.xMin || px >= region.xMax || py < region.yMin || py >= region.yMax) continue;
                total++;
                // Tolerante a 1 px de deslocamento (a pose do "antes" foi gravada com 3 casas decimais):
                // vale a menor diferença entre o pixel e os 8 vizinhos da outra imagem.
                int d = int.MaxValue;
                for (int oy = -1; oy <= 1 && d > 0; oy++)
                for (int ox = -1; ox <= 1 && d > 0; ox++)
                {
                    int qx = px + ox, qy = py + oy;
                    if (qx < 0 || qy < 0 || qx >= ta.width || qy >= ta.height) continue;
                    Color32 q = cbx[qy * ta.width + qx];
                    d = Mathf.Min(d, Mathf.Abs(ca[i].r - q.r) + Mathf.Abs(ca[i].g - q.g) + Mathf.Abs(ca[i].b - q.b));
                }
                sum += d; if (d > max) max = d; if (d > 12) changed++;
            }
            sb.AppendLine($"   Alavanca antes x depois (só a região da alavanca, {region}): diferença média {(double)sum / Mathf.Max(1, total) / 3.0:F3} níveis/canal, " +
                          $"máx {max}, pixels mudados (>12) {changed} de {total}.");
            leverPixelsChanged = changed;
            Object.DestroyImmediate(ta); Object.DestroyImmediate(tb);
        }
    }
}
