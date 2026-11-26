using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;
using VRRehab.EditorTools;
using VRRehab.Interactive.Task07;
using Object = UnityEngine.Object;

namespace VRRehab.EditorAutoSetup
{
    /// <summary>
    /// TASK-07 (rodada de correção 2026-11-25): corrige e reconfigura a Pistola de Balões SEM apagar os prefabs
    /// (os GUIDs continuam os mesmos, então as instâncias da cena não quebram).
    ///  1. Materiais dos balões com mais brilho + GPU Instancing; material de partícula e PhysicsMaterial "quicante".
    ///  2. Prefab do balão: collider, física, cordinha, partículas e campos do FloatingBalloon (pool, estouro suave).
    ///  3. Prefab da pistola: Visual_Model girado 180° (o FBX tem o cano em -Z), Muzzle_Socket com +Z = cano,
    ///     Grip_Attach na empunhadura ligado ao attachTransform, colliders refeitos a partir das malhas.
    ///  4. Cena Menu_Principal: pistola deitada no tampo de counter_3_a (1) com o cano para a sala, folga >= 0,25 m,
    ///     3 balões demo "amarrados" (mola) acima dela, expurgo de duplicatas/(0,0,0) antes e depois.
    ///  5. Auditoria JSON + capturas offscreen 1920x1080 (Camera.Render em RenderTexture, nada de captura de tela).
    /// Batch: unity run . -- -executeMethod VRRehab.EditorAutoSetup.Task07_SceneAndPrefabBuilder.RunBatch
    /// </summary>
    public static class Task07_SceneAndPrefabBuilder
    {
        private const string ModelsDir = "Assets/Models/Generated/Task07";
        private const string PrefabsDir = "Assets/Prefabs/Interactive";
        private const string MenuScenePath = "Assets/Scenes/Menu_Principal.unity";
        private const string QA = ".agents/visual_qa";

        private const string GunModelPath = ModelsDir + "/BalloonGunDispenser.fbx";
        private const string BalloonModelPath = ModelsDir + "/InflatableBalloon.fbx";
        private const string GunPrefabPath = PrefabsDir + "/BalloonGunDispenser.prefab";
        private const string BalloonPrefabPath = PrefabsDir + "/InflatableBalloon.prefab";
        private const string BurstMatPath = ModelsDir + "/M_Balloon_Burst.mat";
        private const string PhysMatPath = ModelsDir + "/PM_Balloon_Bouncy.asset";

        private const string SfxStretchPath = "Assets/Audio/Generated/Task07/balloon_inflate_stretch.wav";
        private const string SfxWhooshPath = "Assets/Audio/Generated/Task07/balloon_release_whoosh.wav";
        private const string SfxPopPath = "Assets/Audio/Generated/Task07/balloon_pop_soft.wav";

        public const string GunInstanceName = "BalloonGunDispenser_Instance";
        public const string CounterName = "counter_3_a (1)";
        /// <summary>Bancadas candidatas, na ordem de preferência (a do TASK-06 primeiro; a vizinha vazia se faltar folga).</summary>
        public static readonly string[] CounterCandidates = { "counter_3_a (1)", "counter_3_c (2)" };
        public static readonly string[] DemoNames = { "Demo_Balloon_Red", "Demo_Balloon_Cyan", "Demo_Balloon_Yellow" };
        private static readonly string[] DemoMats = { "M_Balloon_RubyRed", "M_Balloon_CyberCyan", "M_Balloon_SunnyYellow" };
        private static readonly float[] DemoScales = { 1.25f, 1.35f, 1.30f };

        private static readonly string[] BalloonMatNames =
        {
            "M_Balloon_RubyRed", "M_Balloon_CyberCyan", "M_Balloon_SunnyYellow", "M_Balloon_NeonMagenta",
            "M_Balloon_LimeGreen", "M_Balloon_RoyalPurple", "M_Balloon_Tangerine"
        };

        /// <summary>Se definido, a pistola vai sempre para esta bancada (decisão do usuário em 2026-11-25).</summary>
        public const string ForcedCounter = "counter_3_a (1)";
        public const float GripPitchDeg = 10f;      // attach inclinado 10° para baixo -> cano 10° acima do "apontar" do controle
        public const float MinClearance = 0.25f;
        private const float StringLengthScale1 = 0.34f;

        [MenuItem("VRRehab/AutoBuild/Build and Verify TASK-07")]
        public static void BuildAndVerify() => Debug.Log(RunAll());

        public static void RunBatch()
        {
            int code = 1;
            try { Debug.Log(RunAll()); code = 0; }
            catch (Exception e) { Debug.LogException(e); }
            EditorApplication.Exit(code);
        }

        public static string RunAll()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== TASK-07 Builder ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            // Normais suaves calculadas na importação (evita a "faixa" de luz no equador do balão)
            if (AssetImporter.GetAtPath(BalloonModelPath) is ModelImporter mi &&
                (mi.importNormals != ModelImporterNormals.Calculate || Mathf.Abs(mi.normalSmoothingAngle - 85f) > 0.1f))
            {
                mi.importNormals = ModelImporterNormals.Calculate;
                mi.normalSmoothingAngle = 85f;
                mi.normalCalculationMode = ModelImporterNormalCalculationMode.AreaAndAngleWeighted;
                mi.SaveAndReimport();
            }
            foreach (string p in new[] { GunModelPath, BalloonModelPath, SfxStretchPath, SfxWhooshPath, SfxPopPath })
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            Scene scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            float ceilingY = MeasureCeiling(sb);

            UpgradeMaterials(sb);
            BuildBalloonPrefab(sb, ceilingY - 0.05f);
            GunValidation gv = BuildGunPrefab(sb);

            scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            SceneResult sr = SetupScene(sb, ceilingY, gv);
            CaptureAll(sb, sr);

            File.WriteAllText(Path.Combine(QA, "TASK-07_BuilderResult.txt"), sb.ToString());
            return sb.ToString();
        }

        // =====================================================================================================
        // 1. Materiais
        // =====================================================================================================
        private static void UpgradeMaterials(StringBuilder sb)
        {
            foreach (string n in BalloonMatNames)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/{n}.mat");
                if (mat == null) { sb.AppendLine($"  [!] material {n} não encontrado"); continue; }
                MaterialShaderHelper.EnsureValidShader(mat);
                Color c = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.color;
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.93f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.93f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.0f);
                if (mat.HasProperty("_GlossyReflections")) mat.SetFloat("_GlossyReflections", 1f);
                // Leve brilho próprio: cores vivas mesmo em cantos escuros da sala (sem custo de luz extra)
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", c * 0.14f);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
            }
            var str = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Balloon_String.mat");
            if (str != null) { str.enableInstancing = true; EditorUtility.SetDirty(str); }

            var burst = AssetDatabase.LoadAssetAtPath<Material>(BurstMatPath);
            if (burst == null)
            {
                Shader sh = Shader.Find("Particles/Standard Unlit") ?? MaterialShaderHelper.GetUnlitShader();
                burst = new Material(sh) { name = "M_Balloon_Burst" };
                AssetDatabase.CreateAsset(burst, BurstMatPath);
            }
            if (burst.HasProperty("_Color")) burst.SetColor("_Color", Color.white);
            burst.enableInstancing = true;
            EditorUtility.SetDirty(burst);

            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PhysMatPath);
            if (pm == null)
            {
                pm = new PhysicsMaterial("PM_Balloon_Bouncy");
                AssetDatabase.CreateAsset(pm, PhysMatPath);
            }
            pm.bounciness = 0.5f;
            pm.dynamicFriction = 0.08f;
            pm.staticFriction = 0.08f;
            pm.bounceCombine = PhysicsMaterialCombine.Maximum;
            pm.frictionCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(pm);
            AssetDatabase.SaveAssets();
            sb.AppendLine("1) Materiais: 7 balões (smoothness 0.93, metallic 0, emissão 14% da cor, instancing), partícula e PhysicsMaterial quicante (0.5).");
        }

        // =====================================================================================================
        // 2. Prefab do balão
        // =====================================================================================================
        private static void BuildBalloonPrefab(StringBuilder sb, float ceilingLimit)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BalloonPrefabPath);
            try
            {
                Transform visual = root.transform.Find("Visual_Model");
                Material red = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Balloon_RubyRed.mat");
                foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>(true))
                {
                    mr.sharedMaterial = red;
                    mr.shadowCastingMode = ShadowCastingMode.Off; // até 11 balões: sem sombra dinâmica (VR)
                    mr.receiveShadows = false;
                }

                foreach (var c in root.GetComponents<Collider>()) if (!(c is SphereCollider)) Object.DestroyImmediate(c);
                var sc = root.GetComponent<SphereCollider>() ?? root.AddComponent<SphereCollider>();
                sc.center = new Vector3(0f, 0.18f, 0f); // ventre do balão (largura máxima em y = 0,18)
                sc.radius = 0.118f;
                sc.isTrigger = false;
                sc.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PhysMatPath);

                var rb = root.GetComponent<Rigidbody>();
                rb.mass = 0.012f;
                rb.linearDamping = 0.9f;
                rb.angularDamping = 1.6f;
                rb.useGravity = false;
                rb.isKinematic = false;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                var lr = root.GetComponent<LineRenderer>();
                lr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Balloon_String.mat");
                lr.positionCount = 6;
                lr.useWorldSpace = true;
                lr.startWidth = 0.0030f;
                lr.endWidth = 0.0018f;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.numCapVertices = 0;
                lr.numCornerVertices = 0;

                var src = root.GetComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 0.5f;
                src.maxDistance = 10f;
                src.volume = 1f;

                Transform psT = root.transform.Find("Burst_Particles");
                var ps = psT.GetComponent<ParticleSystem>();
                var main = ps.main;
                main.duration = 0.5f;
                main.loop = false;
                main.playOnAwake = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.65f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
                main.gravityModifier = 0.5f;
                main.maxParticles = 40;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var em = ps.emission;
                em.enabled = false; // o script usa Emit(n) no estouro
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.1f;
                var col = ps.collision;
                col.enabled = false;
                var psr = psT.GetComponent<ParticleSystemRenderer>();
                psr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(BurstMatPath);
                psr.shadowCastingMode = ShadowCastingMode.Off;
                psr.receiveShadows = false;

                var fb = root.GetComponent<FloatingBalloon>();
                fb.interactionLayers = InteractionLayerMask.GetMask("Default");
                var so = new SerializedObject(fb);
                Set(so, "stringRenderer", lr);
                Transform anchor = visual.Find("String_Anchor");
                if (anchor == null) anchor = FindDeep(visual, "String_Anchor");
                Set(so, "stringAnchor", anchor != null ? anchor : root.transform);
                Set(so, "burstParticleSystem", ps);
                Set(so, "popAudioClip", AssetDatabase.LoadAssetAtPath<AudioClip>(SfxPopPath));
                Set(so, "audioSource", src);
                so.FindProperty("stringLength").floatValue = StringLengthScale1;
                so.FindProperty("ceilingHeightLimit").floatValue = ceilingLimit;
                so.FindProperty("homeAnchored").boolValue = false;
                so.FindProperty("popVolume").floatValue = 0.55f;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, BalloonPrefabPath);
                sb.AppendLine($"2) Prefab do balão atualizado (mesmo GUID): SphereCollider r=0,115 quicante, Discrete, cordinha 0,34 m x escala, teto {ceilingLimit:0.00} m, estouro suave.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // =====================================================================================================
        // 3. Prefab da pistola (correção dos 180°)
        // =====================================================================================================
        public struct GunValidation
        {
            public float dotMuzzleVsAttach;
            public float muzzleAheadOfGripMeters;
            public Vector3 muzzleLocal;
            public Vector3 gripAttachLocal;
            public int triangles;
            public bool ok;
        }

        private static GunValidation BuildGunPrefab(StringBuilder sb)
        {
            var gv = new GunValidation();
            GameObject root = PrefabUtility.LoadPrefabContents(GunPrefabPath);
            try
            {
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Transform visual = root.transform.Find("Visual_Model");
                visual.localPosition = Vector3.zero;
                visual.localRotation = Quaternion.Euler(0f, 180f, 0f); // FBX: cano em -Z -> agora +Z da raiz
                visual.localScale = Vector3.one;

                Transform muzzle = FindDeep(visual, "Muzzle_Socket");
                if (muzzle == null) throw new Exception("Muzzle_Socket não encontrado no FBX da pistola");
                muzzle.rotation = root.transform.rotation; // forward = +Z da raiz (direção do cano)

                Transform grip = FindDeep(visual, "Gun_Grip");
                Bounds gripB = LocalBounds(root.transform, grip.GetComponent<Renderer>());
                Transform attach = root.transform.Find("Grip_Attach");
                if (attach == null)
                {
                    attach = new GameObject("Grip_Attach").transform;
                    attach.SetParent(root.transform, false);
                }
                attach.localPosition = gripB.center;
                attach.localRotation = Quaternion.Euler(GripPitchDeg, 0f, 0f);

                // Colliders refeitos a partir das malhas (empunhadura + corpo/cano/cilindro)
                foreach (var c in root.GetComponents<Collider>()) Object.DestroyImmediate(c);
                var gripBox = root.AddComponent<BoxCollider>();
                gripBox.center = gripB.center;
                gripBox.size = gripB.size * 0.95f;
                Bounds bodyB = new Bounds();
                bool has = false;
                int tris = 0;
                foreach (var r in visual.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
                    if (r.transform == grip || r.name.Contains("Grip") || r.name.Contains("Trigger")) continue;
                    Bounds lb = LocalBounds(root.transform, r);
                    if (!has) { bodyB = lb; has = true; } else bodyB.Encapsulate(lb);
                }
                var bodyBox = root.AddComponent<BoxCollider>();
                bodyBox.center = bodyB.center;
                bodyBox.size = bodyB.size;

                var rb = root.GetComponent<Rigidbody>();
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.useGravity = true;

                var gun = root.GetComponent<BalloonInflaterGun>();
                gun.attachTransform = attach;
                gun.useDynamicAttach = false;
                gun.matchAttachPosition = true;
                gun.matchAttachRotation = true;
                gun.interactionLayers = InteractionLayerMask.GetMask("Default");

                var src = root.GetComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.spatialBlend = 1f;
                src.volume = 1f;

                var so = new SerializedObject(gun);
                Set(so, "muzzleSocket", muzzle);
                Set(so, "gaugeNeedle", FindDeep(visual, "Pressure_Gauge_Needle"));
                Set(so, "balloonPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(BalloonPrefabPath));
                Set(so, "inflateStretchAudioClip", AssetDatabase.LoadAssetAtPath<AudioClip>(SfxStretchPath));
                Set(so, "releaseWhooshAudioClip", AssetDatabase.LoadAssetAtPath<AudioClip>(SfxWhooshPath));
                Set(so, "gunAudioSource", src);
                SerializedProperty mats = so.FindProperty("balloonMaterials");
                mats.arraySize = BalloonMatNames.Length;
                for (int i = 0; i < BalloonMatNames.Length; i++)
                    mats.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/{BalloonMatNames[i]}.mat");
                so.FindProperty("maxLiveBalloons").intValue = 8;
                so.FindProperty("minScale").floatValue = 0.15f;
                so.FindProperty("maxScale").floatValue = 1.36f;
                so.FindProperty("minReleaseScale").floatValue = 0.7f;
                so.FindProperty("sizeVariation").floatValue = 0.08f;
                so.FindProperty("inflateDuration").floatValue = 1.6f;
                so.FindProperty("enableAutoRespawn").boolValue = true;
                so.FindProperty("alwaysDynamicAfterRelease").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();

                gv.muzzleLocal = root.transform.InverseTransformPoint(muzzle.position);
                gv.gripAttachLocal = attach.localPosition;
                gv.dotMuzzleVsAttach = Vector3.Dot(muzzle.forward, attach.forward);
                gv.muzzleAheadOfGripMeters = gv.muzzleLocal.z - attach.localPosition.z;
                gv.triangles = tris;
                gv.ok = gv.dotMuzzleVsAttach > 0.95f && gv.muzzleAheadOfGripMeters > 0.05f;

                PrefabUtility.SaveAsPrefabAsset(root, GunPrefabPath);
                sb.AppendLine("3) Prefab da pistola: Visual_Model girado 180° em Y, Muzzle_Socket com forward = +Z, Grip_Attach na empunhadura (pitch 10°).");
                sb.AppendLine($"   Muzzle (espaço da raiz) {V(gv.muzzleLocal)} | Grip_Attach {V(gv.gripAttachLocal)} | dot(cano, attach) = {gv.dotMuzzleVsAttach:0.000} | bico {gv.muzzleAheadOfGripMeters:0.000} m à frente da pegada -> {(gv.ok ? "OK" : "FALHOU")}");
                sb.AppendLine($"   Colliders: empunhadura {V(gripBox.center)} {V(gripBox.size)}; corpo {V(bodyBox.center)} {V(bodyBox.size)} | {tris} triângulos");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return gv;
        }

        // =====================================================================================================
        // 4. Cena
        // =====================================================================================================
        public class SceneResult
        {
            public Vector3 gunPos;
            public Quaternion gunRot;
            public Vector3 roomDir;
            public Vector3 side;
            public float topY;
            public float ceilingY;
            public Vector3 viewerPos;
        }

        [Serializable] public class Clearance { public string a; public string b; public float clearanceMeters; }
        [Serializable] public class BalloonInfo { public string name; public Vector3 position; public float scale; public float topY; public bool belowCeiling; public bool overlapsScene; }
        [Serializable]
        public class Audit
        {
            public string taskId = "TASK-07";
            public string scene = MenuScenePath;
            public string timestamp;
            public string furniture = CounterName;
            public Vector3 gunPosition;
            public Vector3 gunEuler;
            public Vector3 gunBarrelDirection;
            public float counterTopY;
            public float ceilingY;
            public float groundGapMeters;
            public float colliderBelowTopMeters;
            public bool hasOverlap;
            public float minClearanceMeters;
            public List<Clearance> pairwiseClearances = new List<Clearance>();
            public List<BalloonInfo> demoBalloons = new List<BalloonInfo>();
            public List<string> objectsAtOriginZero = new List<string>();
            public List<string> duplicateObjectsFound = new List<string>();
            public int gunCount;
            public int demoBalloonCount;
            public int floatingBalloonComponentsInScene;
            public float gunDotMuzzleVsAttach;
            public float muzzleAheadOfGripMeters;
            public int purgeBeforeOrigin, purgeBeforeDuplicates, purgeAfterOrigin, purgeAfterDuplicates;
        }

        private static SceneResult SetupScene(StringBuilder sb, float ceilingY, GunValidation gv)
        {
            var audit = new Audit { timestamp = DateTime.Now.ToString("s"), ceilingY = ceilingY, gunDotMuzzleVsAttach = gv.dotMuzzleVsAttach, muzzleAheadOfGripMeters = gv.muzzleAheadOfGripMeters };
            var log = new StringBuilder();
            SceneDuplicateAndOriginPurger.PurgeMenuScene(out audit.purgeBeforeOrigin, out audit.purgeBeforeDuplicates, log);
            sb.AppendLine($"4) Expurgo ANTES: origem {audit.purgeBeforeOrigin}, duplicatas {audit.purgeBeforeDuplicates}");
            if (log.Length > 0) sb.Append(log);

            // Pistola (uma só)
            var guns = Object.FindObjectsByType<BalloonInflaterGun>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList();
            GameObject gunGo = guns.Select(g => g.gameObject).FirstOrDefault(g => g.name == GunInstanceName) ?? guns.FirstOrDefault()?.gameObject;
            if (gunGo == null)
            {
                gunGo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GunPrefabPath));
                gunGo.name = GunInstanceName;
                sb.AppendLine("   [!] pistola não estava na cena: recriada a partir do prefab");
            }
            foreach (var g in guns) if (g.gameObject != gunGo) { audit.duplicateObjectsFound.Add(ForestAuditTool.PathOf(g.transform)); Object.DestroyImmediate(g.gameObject); }

            Vector3 viewer = ViewerStart();
            var demoGos = new List<GameObject>();
            foreach (string n in DemoNames)
            {
                var all = Object.FindObjectsByType<FloatingBalloon>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(f => f.name == n).ToList();
                for (int i = 1; i < all.Count; i++) { audit.duplicateObjectsFound.Add(n); Object.DestroyImmediate(all[i].gameObject); }
                GameObject go = all.Count > 0 ? all[0].gameObject : null;
                if (go == null)
                {
                    go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BalloonPrefabPath));
                    go.name = n;
                    sb.AppendLine($"   [!] {n} não estava na cena: recriado");
                }
                demoGos.Add(go);
            }
            // Qualquer outro FloatingBalloon solto na cena (cópias antigas) é removido: só ficam os 3 demos
            foreach (var f in Object.FindObjectsByType<FloatingBalloon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!demoGos.Contains(f.gameObject)) { audit.duplicateObjectsFound.Add(ForestAuditTool.PathOf(f.transform)); Object.DestroyImmediate(f.gameObject); }

            var ignore = new HashSet<GameObject>(demoGos) { gunGo };

            // Avalia as bancadas candidatas (a do TASK-06 primeiro); usa a primeira que der folga >= 0,25 m
            GameObject counter = null; Bounds counterB = default; float topY = 0f; Vector3 roomDir = Vector3.forward, side = Vector3.right;
            Quaternion gunRot = Quaternion.identity; Vector3 best = Vector3.zero; float bestClear = float.MinValue;
            List<(Renderer r, Bounds b)> neighbors = null;
            var candidates = new List<GameObject>();
            foreach (string cname in CounterCandidates)
            {
                GameObject c = GameObject.Find(cname);
                if (c == null) sb.AppendLine($"   bancada '{cname}' não encontrada"); else candidates.Add(c);
            }
            Vector3 preferredCenter = candidates.Count > 0 ? candidates[0].transform.position : Vector3.zero;
            if (string.IsNullOrEmpty(ForcedCounter))
                candidates.AddRange(SurveyFurniture(candidates).OrderBy(c => (c.transform.position - preferredCenter).sqrMagnitude));
            else
            {
                // Decisão do usuário (2026-11-25): a pistola fica na mesa onde já estava, aceitando a folga possível ali
                candidates = candidates.Where(c => c.name == ForcedCounter).ToList();
                sb.AppendLine($"   Bancada fixada pelo usuário: '{ForcedCounter}' (folga < {MinClearance} m aceita)");
            }
            sb.AppendLine($"   Superfícies avaliadas: {candidates.Count} (2 preferidas + móveis da sala com tampo entre 0,6 e 1,2 m)");
            foreach (GameObject c in candidates)
            {
                string cname = c.name;
                SpatialAuditUtility.TryGetSolidBounds(c, out Bounds cb, false);
                if (Mathf.Max(cb.size.x, cb.size.z) > 2.4f) { sb.AppendLine($"   '{cname}' ignorado: bounds {V(cb.size)} (agrupa vários móveis)"); continue; }
                float ty = CounterTopY(c, cb);
                Vector3 dir = RoomFacing(c, cb, ty);
                Vector3 sd = Vector3.Cross(Vector3.up, dir);
                Quaternion rot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, 0f, 90f); // deitada de lado, cano para a sala
                var nb = CollectNeighbors(c, ignore, cb.center, 2.0f, ty);
                Vector3 bp = Vector3.zero; float bs = float.MinValue, bc = float.MinValue; string bw = "";
                float maxC = float.MinValue; Vector3 maxP = Vector3.zero; string maxW = "";
                float inset = 0.10f;
                for (float x = cb.min.x + inset; x <= cb.max.x - inset + 1e-4f; x += 0.04f)
                for (float z = cb.min.z + inset; z <= cb.max.z - inset + 1e-4f; z += 0.04f)
                {
                    Vector3 p = new Vector3(x, ty, z);
                    Bounds gb = PlaceGun(gunGo, p, rot, ty);
                    if (!InsideXZ(gb, cb, 0.03f) || !SupportedBy(gunGo, gb, c)) continue;
                    float clear = MinClear(gb, nb, out string who);
                    // distância até a borda voltada para a sala (pistola perto da borda = fácil de alcançar)
                    float frontEdge = Vector3.Dot(cb.center, dir) + Vector3.Dot(cb.extents, Abs(dir));
                    float frontDist = frontEdge - Vector3.Dot(gb.center, dir);
                    float score = Mathf.Min(clear, 0.40f) - 0.25f * Mathf.Max(0f, frontDist - 0.15f);
                    if (score > bs) { bs = score; bp = p; bc = clear; bw = who; }
                    if (clear > maxC) { maxC = clear; maxP = p; maxW = who; }
                }
                sb.AppendLine($"   bancada '{cname}': tampo y={ty:0.000}, frente para {V(dir)}, melhor folga {bc:0.000} m em {V(bp)} (vizinho mais perto: {bw}) | AABB {V(cb.min)}..{V(cb.max)} | maior folga possível {maxC:0.000} m em {V(maxP)} ({maxW})");
                foreach (var n in nb.Where(n => n.b.max.y > ty + 0.002f).OrderBy(n => SpatialAuditUtility.AabbClearance(n.b, cb)).Take(10))
                    sb.AppendLine($"      obstáculo {ForestAuditTool.PathOf(n.r.transform)} AABB {V(n.b.min)}..{V(n.b.max)}");
                if (bc == float.MinValue) continue;
                if (counter == null || (bestClear < MinClearance && bc > bestClear))
                {
                    counter = c; counterB = cb; topY = ty; roomDir = dir; side = sd; gunRot = rot; best = bp; bestClear = bc; neighbors = nb;
                }
                if (bestClear >= MinClearance) break;
            }
            if (bestClear < MinClearance && string.IsNullOrEmpty(ForcedCounter)) sb.AppendLine($"   [!] nenhuma superfície com folga >= {MinClearance} m; usada a de maior folga ({bestClear:0.000} m)");
            if (counter == null) throw new Exception("nenhuma bancada candidata encontrada");
            audit.furniture = counter.name;
            audit.counterTopY = topY;
            Bounds gunB = PlaceGun(gunGo, best, gunRot, topY);
            var gunRb = gunGo.GetComponent<Rigidbody>();
            if (gunRb != null) gunRb.isKinematic = true; // repousa parada até a primeira pegada (depois fica dinâmica)
            EditorUtility.SetDirty(gunGo);

            audit.gunPosition = gunGo.transform.position;
            audit.gunEuler = gunGo.transform.eulerAngles;
            audit.gunBarrelDirection = gunGo.transform.forward;
            SpatialAuditUtility.TryGetSolidBounds(gunGo, out Bounds gunRenderB, false);
            audit.groundGapMeters = gunRenderB.min.y - topY;
            audit.colliderBelowTopMeters = Mathf.Max(0f, topY - ColliderMinY(gunGo));
            audit.minClearanceMeters = MinClear(gunB, neighbors, out _);
            foreach (var n in neighbors)
            {
                float c = SpatialAuditUtility.AabbClearance(gunB, n.b);
                if (c < 0.6f) audit.pairwiseClearances.Add(new Clearance { a = GunInstanceName, b = ForestAuditTool.PathOf(n.r.transform), clearanceMeters = c });
            }
            audit.pairwiseClearances = audit.pairwiseClearances.OrderBy(c => c.clearanceMeters).ToList();
            sb.AppendLine($"   Pistola em {V(audit.gunPosition)} rot {V(audit.gunEuler)} | cano para {V(roomDir)} | tampo y={topY:0.000} | gap da malha {audit.groundGapMeters:0.0000} m (collider {audit.colliderBelowTopMeters:0.0000} m abaixo do tampo) | folga mínima {audit.minClearanceMeters:0.000} m");

            // Balões demo "amarrados" acima e ao lado da pistola, com a ponta da cordinha perto do tampo
            Material[] demoMats = DemoMats.Select(m => AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/{m}.mat")).ToArray();
            Vector3[] offsets =
            {
                -side * 0.34f + roomDir * 0.02f, side * 0.34f + roomDir * 0.02f, roomDir * 0.32f,
                -side * 0.55f, side * 0.55f, -side * 0.30f + roomDir * 0.32f, side * 0.30f + roomDir * 0.32f,
                -side * 0.75f + roomDir * 0.15f, side * 0.75f + roomDir * 0.15f
            };
            var placed = new List<Bounds>();
            for (int i = 0; i < demoGos.Count; i++)
            {
                GameObject go = demoGos[i];
                float s = DemoScales[i];
                float baseY = topY + StringLengthScale1 * s + 0.04f;
                Bounds bb = default; bool ok = false; Vector3 pos = Vector3.zero;
                foreach (Vector3 off in offsets)
                {
                    pos = new Vector3(gunB.center.x, baseY, gunB.center.z) + off;
                    go.transform.SetPositionAndRotation(pos, Quaternion.identity);
                    go.transform.localScale = Vector3.one * s;
                    SpatialAuditUtility.TryGetSolidBounds(go, out bb, false);
                    bool hits = placed.Any(p => SpatialAuditUtility.AabbClearance(p, bb) < 0.05f) ||
                                MinClear(bb, neighbors, out _) < 0.03f || bb.max.y > ceilingY - 0.15f || HitsSceneColliders(bb, go);
                    if (!hits) { ok = true; break; }
                }
                placed.Add(bb);
                var fb = go.GetComponent<FloatingBalloon>();
                fb.ApplyColorMaterial(demoMats[i], demoMats[i].color);
                var so = new SerializedObject(fb);
                so.FindProperty("homeAnchored").boolValue = true;
                so.FindProperty("ceilingHeightLimit").floatValue = ceilingY - 0.05f;
                so.ApplyModifiedPropertiesWithoutUndo();
                var brb = go.GetComponent<Rigidbody>();
                brb.isKinematic = false; // a mola do homeAnchored segura o balão no lugar
                EditorUtility.SetDirty(go);
                audit.demoBalloons.Add(new BalloonInfo { name = go.name, position = pos, scale = s, topY = bb.max.y, belowCeiling = bb.max.y < ceilingY - 0.1f, overlapsScene = !ok });
                audit.pairwiseClearances.Add(new Clearance { a = go.name, b = GunInstanceName, clearanceMeters = SpatialAuditUtility.AabbClearance(bb, gunB) });
                sb.AppendLine($"   {go.name}: {V(pos)} escala {s:0.00} topo {bb.max.y:0.00} m {(ok ? "" : "[!] sem posição livre ideal")}");
            }

            EditorSceneManager.MarkSceneDirty(gunGo.scene);
            var log2 = new StringBuilder();
            SceneDuplicateAndOriginPurger.PurgeMenuScene(out audit.purgeAfterOrigin, out audit.purgeAfterDuplicates, log2);
            sb.AppendLine($"   Expurgo DEPOIS: origem {audit.purgeAfterOrigin}, duplicatas {audit.purgeAfterDuplicates}");

            foreach (GameObject r in gunGo.scene.GetRootGameObjects())
                if (r.transform.position.magnitude < 1.0f && !SpatialAuditUtility.IsWhitelisted(r)) audit.objectsAtOriginZero.Add(r.name);
            audit.gunCount = Object.FindObjectsByType<BalloonInflaterGun>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            audit.floatingBalloonComponentsInScene = Object.FindObjectsByType<FloatingBalloon>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            audit.demoBalloonCount = audit.demoBalloons.Count;
            audit.hasOverlap = audit.minClearanceMeters < 0f || audit.demoBalloons.Any(b => b.overlapsScene);

            EditorSceneManager.SaveScene(gunGo.scene);
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(QA, "TASK-07_SpatialAudit.json"), JsonUtility.ToJson(audit, true));
            sb.AppendLine($"   Auditoria: pistolas {audit.gunCount}, FloatingBalloon na cena {audit.floatingBalloonComponentsInScene}, (0,0,0) {audit.objectsAtOriginZero.Count}, duplicatas removidas {audit.duplicateObjectsFound.Count}, hasOverlap {audit.hasOverlap}");
            sb.AppendLine("   Vizinhos mais próximos da pistola:");
            foreach (var c in audit.pairwiseClearances.Take(8)) sb.AppendLine($"     {c.clearanceMeters,6:0.000} m  {c.a} <-> {c.b}");

            return new SceneResult { gunPos = gunGo.transform.position, gunRot = gunGo.transform.rotation, roomDir = roomDir, side = side, topY = topY, ceilingY = ceilingY, viewerPos = viewer };
        }

        /// <summary>Põe a pistola no tampo encostando a MALHA visível (não o collider) no tampo. Retorna a AABB sólida.</summary>
        private static Bounds PlaceGun(GameObject gun, Vector3 p, Quaternion rot, float topY)
        {
            gun.transform.SetPositionAndRotation(p, rot);
            Physics.SyncTransforms();
            SpatialAuditUtility.TryGetSolidBounds(gun, out Bounds rb, false);
            gun.transform.position += Vector3.up * (topY + 0.001f - rb.min.y);
            Physics.SyncTransforms();
            SpatialAuditUtility.TryGetSolidBounds(gun, out Bounds b, true);
            return b;
        }

        private static float ColliderMinY(GameObject go)
        {
            float m = float.MaxValue;
            foreach (var c in go.GetComponentsInChildren<Collider>()) if (c.enabled && !c.isTrigger) m = Mathf.Min(m, c.bounds.min.y);
            return m;
        }

        private static List<(Renderer r, Bounds b)> CollectNeighbors(GameObject counter, HashSet<GameObject> ignore, Vector3 around, float radius, float topY)
        {
            var list = new List<(Renderer r, Bounds b)>();
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                if (r.GetComponent("TMP_Text") != null) continue;
                if (r.transform.IsChildOf(counter.transform)) continue;
                if (ignore.Any(g => r.transform.IsChildOf(g.transform))) continue;
                Bounds b = r.bounds;
                if (b.size.x > 2.2f || b.size.z > 2.2f || b.size.y > 2.2f) continue; // paredes, piso, teto
                if ((b.ClosestPoint(around) - around).sqrMagnitude > radius * radius) continue;
                // Móveis vizinhos contíguos (mesmo tampo) e tudo que fica abaixo do tampo não obstruem a pistola
                if (r.name.StartsWith("counter_") || r.transform.parent != null && r.transform.parent.name.StartsWith("counter_")) continue;
                if (b.max.y <= topY + 0.002f) continue;
                list.Add((r, b));
            }
            return list;
        }

        private static float MinClear(Bounds b, List<(Renderer r, Bounds b)> neighbors, out string who)
        {
            float min = float.MaxValue; who = "";
            foreach (var n in neighbors)
            {
                float c = SpatialAuditUtility.AabbClearance(b, n.b);
                if (c < min) { min = c; who = n.r.name; }
            }
            return min == float.MaxValue ? 9f : min;
        }

        private static bool InsideXZ(Bounds inner, Bounds outer, float margin) =>
            inner.min.x >= outer.min.x + margin && inner.max.x <= outer.max.x - margin &&
            inner.min.z >= outer.min.z + margin && inner.max.z <= outer.max.z - margin;

        /// <summary>O meio da pistola tem o tampo do próprio móvel logo abaixo (nada de pistola flutuando).</summary>
        private static bool SupportedBy(GameObject gun, Bounds gb, GameObject furniture)
        {
            foreach (RaycastHit h in Physics.RaycastAll(new Vector3(gb.center.x, gb.min.y + 0.05f, gb.center.z), Vector3.down, 0.12f).OrderBy(h => h.distance))
            {
                if (h.collider.isTrigger || h.collider.transform.IsChildOf(gun.transform)) continue;
                return h.collider.transform.IsChildOf(furniture.transform);
            }
            return false;
        }

        /// <summary>Móveis da sala com tampo horizontal utilizável (mesas, bancadas, cômodas, estantes baixas).</summary>
        private static List<GameObject> SurveyFurniture(List<GameObject> exclude)
        {
            var res = new List<GameObject>();
            var rx = new System.Text.RegularExpressions.Regex("(counter|desk|table|drawer|cabinet|shelf|bench|dresser|sideboard|nightstand)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                GameObject go = r.gameObject;
                if (!rx.IsMatch(go.name) || go.name.ToLowerInvariant().Contains("lamp")) continue;
                if (exclude.Contains(go) || res.Contains(go)) continue;
                if (go.transform.parent != null && rx.IsMatch(go.transform.parent.name)) continue; // peça interna de outro móvel
                Bounds b = r.bounds;
                if (b.max.y < 0.6f || b.max.y > 1.2f) continue;
                float sx = b.size.x, sz = b.size.z;
                if (Mathf.Min(sx, sz) < 0.35f || Mathf.Max(sx, sz) > 2.4f) continue;
                res.Add(go);
            }
            return res;
        }

        /// <summary>Lado do móvel voltado para a sala: o eixo horizontal (±forward/±right do móvel) com mais espaço livre.</summary>
        private static Vector3 RoomFacing(GameObject counter, Bounds b, float topY)
        {
            Physics.SyncTransforms();
            // Móvel encostado na parede: a parede é a direção com MENOR espaço livre; a frente é o lado oposto.
            Vector3 wall = Vector3.forward; float wallDist = float.MaxValue;
            // Só o eixo de PROFUNDIDADE do tampo (o lado curto) importa: a bancada encosta na parede por trás
            Vector3 depth = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
            foreach (Vector3 d in new[] { depth, -depth })
            {
                Vector3 o = new Vector3(b.center.x, topY + 0.35f, b.center.z);
                float dist = 8f;
                foreach (RaycastHit h in Physics.RaycastAll(o, d, 8f).OrderBy(h => h.distance))
                {
                    if (h.collider.isTrigger || h.collider.transform.IsChildOf(counter.transform) || h.collider.attachedRigidbody != null) continue;
                    dist = h.distance; break;
                }
                if (dist < wallDist) { wallDist = dist; wall = d; }
            }
            Vector3 best = -wall;
            // arredonda para o eixo do mundo mais próximo quando o móvel está alinhado (rotY ~ 0/90/180/270)
            if (Mathf.Abs(best.x) > 0.98f) best = new Vector3(Mathf.Sign(best.x), 0f, 0f);
            else if (Mathf.Abs(best.z) > 0.98f) best = new Vector3(0f, 0f, Mathf.Sign(best.z));
            return best;
        }

        /// <summary>true se a AABB encosta em algum collider sólido da cena (paredes, luminárias, móveis) que não seja do próprio objeto.</summary>
        private static bool HitsSceneColliders(Bounds b, GameObject self)
        {
            Physics.SyncTransforms();
            foreach (Collider c in Physics.OverlapBox(b.center, b.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                if (!c.transform.IsChildOf(self.transform)) return true;
            return false;
        }

        private static float CounterTopY(GameObject counter, Bounds b)
        {
            // Raios de cima para baixo no meio do tampo; vale o primeiro collider que pertence ao móvel
            Physics.SyncTransforms();
            var ys = new List<float>();
            for (int i = 0; i < 9; i++)
            {
                float fx = (i % 3 - 1) * 0.25f, fz = (i / 3 - 1) * 0.25f;
                Vector3 o = new Vector3(b.center.x + fx * b.size.x, b.max.y + 0.5f, b.center.z + fz * b.size.z);
                foreach (RaycastHit h in Physics.RaycastAll(o, Vector3.down, 2f).OrderBy(h => h.distance))
                {
                    if (h.collider.isTrigger) continue;
                    if (h.collider.transform.IsChildOf(counter.transform)) ys.Add(h.point.y);
                    break;
                }
            }
            if (ys.Count >= 3) { ys.Sort(); return ys[ys.Count / 2]; }
            return b.max.y;
        }

        private static float MeasureCeiling(StringBuilder sb)
        {
            GameObject counter = GameObject.Find(CounterName);
            Vector3 o = counter != null ? counter.GetComponentInChildren<Renderer>().bounds.center + Vector3.up * 0.8f : new Vector3(0f, 1.5f, 0f);
            Physics.SyncTransforms();
            float y = float.NaN;
            foreach (RaycastHit h in Physics.RaycastAll(o, Vector3.up, 6f).OrderBy(h => h.distance))
            {
                if (h.collider.isTrigger || h.collider.attachedRigidbody != null) continue;
                y = h.point.y; break;
            }
            if (float.IsNaN(y))
            {
                // Sem collider no teto: usa o renderer mais baixo acima do ponto que cobre a coluna (forro/laje)
                float best = float.MaxValue;
                foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    Bounds b = r.bounds;
                    if (b.size.x < 2f || b.size.z < 2f || b.min.y < o.y) continue;
                    if (o.x < b.min.x || o.x > b.max.x || o.z < b.min.z || o.z > b.max.z) continue;
                    best = Mathf.Min(best, b.min.y);
                }
                y = best < float.MaxValue ? best : 2.85f;
                sb.AppendLine($"0) Teto (sem collider, por malha) em y = {y:0.000} m");
            }
            else sb.AppendLine($"0) Teto (raycast para cima a partir de {V(o)}) em y = {y:0.000} m");
            return y;
        }

        private static Vector3 ViewerStart()
        {
            var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (origin != null) return origin.transform.position + Vector3.up * 1.6f;
            var cam = Camera.main;
            return cam != null ? cam.transform.position : new Vector3(0f, 1.6f, 0f);
        }

        // =====================================================================================================
        // 5. Capturas offscreen
        // =====================================================================================================
        private static void CaptureAll(StringBuilder sb, SceneResult sr)
        {
            Vector3 g = sr.gunPos, d = sr.roomDir, s = sr.side, up = Vector3.up;
            var shots = new List<string>();
            void Shot(string name, Vector3 cam, Vector3 look, float fov)
            {
                string p = Path.Combine(QA, name);
                VisualQACaptureEditor.CaptureCameraView(p, cam, look, 1920, 1080, fov);
                shots.Add(name);
            }
            Shot("TASK-07_View1_Front.png", g + d * 1.35f + up * 0.55f + s * 0.15f, g + up * 0.35f, 55f);
            Shot("TASK-07_View2_Side_Grounding.png", g + s * 0.55f + d * 0.10f + up * 0.05f, g + up * 0.02f, 35f);
            Shot("TASK-07_View3_Closeup.png", g + d * 0.42f + s * 0.18f + up * 0.22f, g, 45f);
            Shot("TASK-07_Balloons_Floating_Room.png", g + d * 2.2f + up * 0.75f - s * 0.6f, g + up * 0.55f, 60f);

            // Cena vazia temporária (não salva) para a pistola "na mão", a inflação e a comparação de tamanho
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var lightGo = new GameObject("Key");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.56f, 0.6f);
            var bg = new Color(0.17f, 0.19f, 0.23f);

            // "Controle" de referência: corpo + eixos (vermelho X, verde Y, azul Z = para onde o controle aponta)
            var hand = new GameObject("HandProxy").transform;
            hand.SetPositionAndRotation(new Vector3(0f, 1.2f, 0f), Quaternion.Euler(-5f, 0f, 0f));
            MakeBar(hand, Vector3.forward * 0.20f, new Vector3(0.006f, 0.006f, 0.40f), new Color(0.15f, 0.35f, 1f));
            MakeBar(hand, Vector3.right * 0.06f, new Vector3(0.12f, 0.005f, 0.005f), new Color(1f, 0.2f, 0.2f));
            MakeBar(hand, Vector3.up * 0.06f, new Vector3(0.005f, 0.12f, 0.005f), new Color(0.2f, 1f, 0.3f));

            var gunPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GunPrefabPath);
            var gun = (GameObject)PrefabUtility.InstantiatePrefab(gunPrefab);
            Transform attach = gun.transform.Find("Grip_Attach");
            // O XRI alinha o attach do objeto ao attach da mão: rotação = mão * inverso(attach local)
            Quaternion rot = hand.rotation * Quaternion.Inverse(attach.localRotation);
            gun.transform.SetPositionAndRotation(hand.position - rot * attach.localPosition, rot);

            string P(string n) { shots.Add(n); return Path.Combine(QA, n); }
            ForestAuditTool.RenderTo(P("TASK-07_Gun_In_Hand.png"), hand.position + new Vector3(0.75f, 0.12f, 0.05f), hand.position + Vector3.forward * 0.08f, 35f, 1920, 1080, ~0, bg);
            ForestAuditTool.RenderTo(P("TASK-07_Gun_In_Hand_Top.png"), hand.position + new Vector3(0.0f, 0.85f, 0.08f) + Vector3.right * 0.001f, hand.position + Vector3.forward * 0.08f, 35f, 1920, 1080, ~0, bg);

            // Balão enchendo no bico (~80%)
            var balloonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BalloonPrefabPath);
            BalloonInflaterGun gunC = gun.GetComponent<BalloonInflaterGun>();
            Transform muzzle = gunC.MuzzleSocket != null ? gunC.MuzzleSocket : FindDeep(gun.transform, "Muzzle_Socket");
            var inflating = (GameObject)PrefabUtility.InstantiatePrefab(balloonPrefab);
            inflating.transform.SetParent(muzzle, false);
            inflating.transform.localPosition = Vector3.zero;
            inflating.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            inflating.transform.localScale = new Vector3(1.05f, 1.14f, 1.05f);
            inflating.GetComponent<FloatingBalloon>().ApplyColorMaterial(AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Balloon_CyberCyan.mat"), Color.cyan);
            inflating.GetComponent<LineRenderer>().enabled = false;
            ForestAuditTool.RenderTo(P("TASK-07_Balloon_Inflating.png"), hand.position + new Vector3(0.85f, 0.35f, 0.45f), hand.position + Vector3.forward * 0.3f + Vector3.up * 0.05f, 40f, 1920, 1080, ~0, bg);
            Object.DestroyImmediate(inflating);
            Object.DestroyImmediate(hand.gameObject);

            // Comparação de tamanho: antes (escala 0,30 = 9 cm) x depois (escala 1,38 = 41 cm) + régua de 0,30 m + pistola
            gun.transform.SetPositionAndRotation(new Vector3(0.42f, 0.0f, 0f), Quaternion.Euler(0f, -90f, 90f));
            SpatialAuditUtility.TryGetSolidBounds(gun, out Bounds gb, true);
            gun.transform.position += Vector3.up * (-gb.min.y);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0.1f, -0.01f, 0f);
            floor.transform.localScale = new Vector3(2f, 0.02f, 1f);
            SetColor(floor, new Color(0.32f, 0.33f, 0.36f));
            var ruler = new GameObject("Ruler").transform;
            ruler.position = new Vector3(-0.42f, 0f, 0f);
            MakeBar(ruler, Vector3.up * 0.15f, new Vector3(0.012f, 0.30f, 0.012f), new Color(0.95f, 0.95f, 0.95f));
            for (int i = 0; i <= 6; i++)
                MakeBar(ruler, new Vector3(0.012f, i * 0.05f, 0f), new Vector3(i % 2 == 0 ? 0.035f : 0.02f, 0.004f, 0.004f), new Color(0.95f, 0.25f, 0.25f));
            var oldB = (GameObject)PrefabUtility.InstantiatePrefab(balloonPrefab);
            oldB.transform.position = new Vector3(-0.27f, 0f, 0f);
            oldB.transform.localScale = Vector3.one * 0.30f;
            oldB.GetComponent<FloatingBalloon>().ApplyColorMaterial(AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Balloon_RubyRed.mat"), Color.red);
            var newB = (GameObject)PrefabUtility.InstantiatePrefab(balloonPrefab);
            newB.transform.position = new Vector3(0.02f, 0f, 0f);
            newB.transform.localScale = Vector3.one * 1.38f;
            newB.GetComponent<FloatingBalloon>().ApplyColorMaterial(AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Balloon_RubyRed.mat"), Color.red);
            foreach (var lr in Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None)) lr.enabled = false;
            ForestAuditTool.RenderTo(P("TASK-07_Balloon_Size_Compare.png"), new Vector3(0.05f, 0.30f, -1.75f), new Vector3(0.05f, 0.20f, 0f), 32f, 1920, 1080, ~0, bg);

            sb.AppendLine("5) Capturas offscreen 1920x1080: " + string.Join(", ", shots));
            sb.AppendLine("   Comparação: vermelho pequeno = tamanho antigo (escala 0,30 -> 0,09 m); grande = novo (1,38 -> 0,41 m); régua branca = 0,30 m (marcas a cada 5 cm).");
        }

        // =====================================================================================================
        // Utilitários
        // =====================================================================================================
        private static void MakeBar(Transform parent, Vector3 localPos, Vector3 size, Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            SetColor(go, c);
        }

        private static void SetColor(GameObject go, Color c)
        {
            var mat = new Material(MaterialShaderHelper.GetUnlitShader());
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static Bounds LocalBounds(Transform root, Renderer r)
        {
            var mf = r.GetComponent<MeshFilter>();
            Bounds mb = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.zero);
            Matrix4x4 m = root.worldToLocalMatrix * r.transform.localToWorldMatrix;
            Vector3 c = mb.center, e = mb.extents;
            Bounds res = new Bounds(m.MultiplyPoint3x4(c), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                res.Encapsulate(m.MultiplyPoint3x4(corner));
            }
            return res;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        private static void Set(SerializedObject so, string prop, Object value)
        {
            SerializedProperty p = so.FindProperty(prop);
            if (p == null) throw new Exception($"campo '{prop}' não existe em {so.targetObject.GetType().Name}");
            p.objectReferenceValue = value;
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        private static string V(Vector3 v) => ForestAuditTool.V(v);
    }
}
