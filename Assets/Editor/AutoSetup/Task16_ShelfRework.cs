using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using VRRehab.EditorTools;
using VRRehab.Ergonomics;
using VRRehab.Interactive.Task16;
using VRRehab.Persistence;
using Object = UnityEngine.Object;

namespace VRRehab.EditorAutoSetup
{
    /// <summary>
    /// TASK-16 (retoque) [D] — reconstrói a estante de troféus e troca a instância do Menu_Principal.
    ///  1. Import: base nova (Trophy_DisplayStand_v2.fbx), texturas, sons.
    ///  2. Materiais: metal escuro, latão, anel emissivo (cor por MPB), silhueta de bloqueado, halo aditivo,
    ///     fundo do cartão, cadeado, material físico dos troféus.
    ///  3. Prefab TrophyDisplayStand (mesmo caminho/GUID): base, 4 SlotAnchors nos pedestais, 4 troféus com
    ///     ShelfTrophyGrabInteractable (customHomeAnchor = SlotAnchor via SerializedObject), rótulos TMP como filhos
    ///     das placas inclinadas, halos, cadeados, partículas de desbloqueio, cartão de informação, TrophyAudio.
    ///  4. Cena: purga antes/depois, remove a estante antiga (e rótulos/troféus soltos), instancia 1 estante aterrada
    ///     no móvel escolhido, frente para o paciente, folga medida.
    ///  5. Auditoria JSON + capturas estáticas (as de interação ficam no teste em Play Mode).
    /// Rodar: unity run . --log-file X --no-tail -- -executeMethod VRRehab.EditorAutoSetup.Task16_ShelfRework.RunBatch
    /// </summary>
    public static class Task16_ShelfRework
    {
        // ------------------------------------------------------------------ configuração
        public static float StandWidth = 0.70f;      // 0,86 m só caberia removendo decoração (spec [A]); alternativa do plano
        public static float SlotSpacing = 0.17f;
        public const float StandDepth = 0.22f;
        public const float PlinthH = 0.045f;
        public const float PedestalH = 0.022f;
        public const float PedestalBackOffset = 0.010f;     // Blender +Y (para trás) -> Unity -Z
        public static string TargetFurniture = "modern_shelves_3 (1)";

        private const string MenuScene = "Assets/Scenes/Menu_Principal.unity";
        private const string Dir = "Assets/Models/Generated/Task16";
        private const string AudioDir = "Assets/Audio/Generated/Task16";
        private const string PrefabDir = "Assets/Prefabs/Interactive";
        private const string StandFbx = Dir + "/Trophy_DisplayStand_v2.fbx";
        private const string StandPrefab = PrefabDir + "/TrophyDisplayStand.prefab";
        private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string FontOutlineMat = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat";
        private const string QaDir = ".agents/visual_qa";

        private static readonly string[] Ids =
        {
            RehabProgressionSaveSystem.TrophyIdGoldenApple, RehabProgressionSaveSystem.TrophyIdLuminousBonsai,
            RehabProgressionSaveSystem.TrophyIdStarCup, RehabProgressionSaveSystem.TrophyIdDroneCrown
        };
        private static readonly string[] Prefabs = { "Trophy_GoldenApple", "Trophy_LuminousBonsai", "Trophy_StarCup", "Trophy_DroneCrownHat" };
        private static readonly string[] Titles = { "Maçã Dourada", "Bonsai Radiante", "Taça Estelar", "Coroa do Drone" };
        private static readonly string[] Descriptions =
        {
            "Colheita de Ouro — maçãs colhidas na floresta com destreza e calma.",
            "Guardião da Floresta — sementes mágicas plantadas ao longo da trilha.",
            "Campeão do Arcade — cestas no mini basquete e moedas de energia.",
            "Parceria Real — carinho e companhia do Drone durante as sessões."
        };
        private static readonly TrophyShelfManager.ProgressKind[] Progress =
        {
            TrophyShelfManager.ProgressKind.Apples, TrophyShelfManager.ProgressKind.Pinecones,
            TrophyShelfManager.ProgressKind.BasketsAndCoins, TrophyShelfManager.ProgressKind.Steps
        };
        private static readonly Color[] Accents =
        {
            new Color(1.00f, 0.78f, 0.25f), new Color(0.35f, 1.00f, 0.55f), new Color(0.40f, 0.65f, 1.00f), new Color(0.78f, 0.45f, 1.00f)
        };

        private static StringBuilder sb;

        [MenuItem("VRRehab/AutoBuild/TASK-16 Estante de Troféus (retoque)")]
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
                File.WriteAllText(Path.Combine(QaDir, "TASK-16_ShelfResult.txt"), "EXCEÇÃO: " + e);
                EditorApplication.Exit(1);
            }
        }

        public static string RunAll()
        {
            sb = new StringBuilder();
            sb.AppendLine($"=== TASK-16 retoque [D] — {DateTime.Now:yyyy-MM-dd HH:mm:ss} (largura {StandWidth} m, espaçamento {SlotSpacing} m) ===");
            ImportAssets();
            var mats = BuildMaterials();
            GameObject prefab = BuildStandPrefab(mats);

            var scene = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
            SceneDuplicateAndOriginPurger.PurgeMenuScene(out int o1, out int d1, sb);
            sb.AppendLine($"Purga ANTES: {o1} na origem, {d1} duplicatas.");
            GameObject stand = ReplaceInScene(prefab);
            SceneDuplicateAndOriginPurger.PurgeMenuScene(out int o2, out int d2, sb);
            sb.AppendLine($"Purga DEPOIS: {o2} na origem, {d2} duplicatas.");
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            sb.AppendLine("Cena salva.");

            WriteAudit(stand, o2, d2);
            Captures(stand);
            File.WriteAllText(Path.Combine(QaDir, "TASK-16_ShelfResult.txt"), sb.ToString());
            return sb.ToString();
        }

        // =====================================================================================================
        // 1. Import
        private static void ImportAssets()
        {
            AssetDatabase.ImportAsset(StandFbx, ImportAssetOptions.ForceUpdate);
            var mi = (ModelImporter)AssetImporter.GetAtPath(StandFbx);
            if (mi == null) throw new Exception("FBX da base não encontrado: " + StandFbx);
            mi.importNormals = ModelImporterNormals.Import;
            mi.importAnimation = false; mi.importCameras = false; mi.importLights = false;
            mi.SaveAndReimport();

            foreach (string t in new[] { "Trophy_LockIcon", "Trophy_HaloRadial", "Trophy_CardBg" })
            {
                string p = $"{Dir}/{t}.png";
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
                var ti = (TextureImporter)AssetImporter.GetAtPath(p);
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.mipmapEnabled = true;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }
            foreach (string a in new[] { "trophy_pickup_shimmer", "trophy_unlock_fanfare", "crown_equip_royal", "trophy_place_click" })
                AssetDatabase.ImportAsset($"{AudioDir}/{a}.wav", ImportAssetOptions.ForceUpdate);
            sb.AppendLine("1) Import: base v2, 3 texturas, 4 sons.");
        }

        // =====================================================================================================
        // 2. Materiais
        private class Mats
        {
            public Material dark, brass, ring, locked, halo, card, lockIcon, particles;
            public PhysicsMaterial phys;
        }

        private static Mats BuildMaterials()
        {
            var m = new Mats
            {
                dark = Std("M_Stand_DarkMetal", new Color(0.07f, 0.075f, 0.09f), 0.75f, 0.62f, Color.black),
                brass = Std("M_Stand_Brass", new Color(0.86f, 0.66f, 0.30f), 0.9f, 0.72f, Color.black),
                ring = Std("M_Stand_Ring", new Color(0.05f, 0.05f, 0.05f), 0f, 0.7f, Color.white),
                locked = Std("M_Trophy_LockedSilhouette", new Color(0.035f, 0.035f, 0.05f), 0.25f, 0.55f, new Color(0.10f, 0.08f, 0.18f)),
                halo = Particle("M_Trophy_Halo", $"{Dir}/Trophy_HaloRadial.png", true, new Color(1f, 0.85f, 0.5f, 0.5f)),
                lockIcon = Particle("M_Trophy_LockIcon", $"{Dir}/Trophy_LockIcon.png", false, new Color(0.75f, 0.77f, 0.82f, 1f)),
                particles = Particle("M_Trophy_UnlockSpark", $"{Dir}/Trophy_HaloRadial.png", true, Color.white),
            };
            m.card = AssetDatabase.LoadAssetAtPath<Material>($"{Dir}/M_Trophy_InfoCard.mat");
            if (m.card == null) { m.card = new Material(Shader.Find("Unlit/Transparent")); AssetDatabase.CreateAsset(m.card, $"{Dir}/M_Trophy_InfoCard.mat"); }
            m.card.shader = Shader.Find("Unlit/Transparent");
            m.card.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}/Trophy_CardBg.png");
            m.card.enableInstancing = true;
            EditorUtility.SetDirty(m.card);

            string pmPath = $"{Dir}/PM_Trophy.asset";
            m.phys = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(pmPath);
            if (m.phys == null) { m.phys = new PhysicsMaterial("PM_Trophy"); AssetDatabase.CreateAsset(m.phys, pmPath); }
            m.phys.dynamicFriction = 0.6f; m.phys.staticFriction = 0.7f; m.phys.bounciness = 0.05f;
            m.phys.frictionCombine = PhysicsMaterialCombine.Average; m.phys.bounceCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(m.phys);
            AssetDatabase.SaveAssets();
            sb.AppendLine("2) Materiais: metal escuro, latão, anel emissivo, silhueta, halo, cadeado, cartão, faíscas, PM_Trophy (atrito 0,6 / quique 0,05).");
            return m;
        }

        private static Material Std(string name, Color albedo, float metallic, float smooth, Color emission)
        {
            string p = $"{Dir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (mat == null) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, p); }
            mat.shader = Shader.Find("Standard");
            mat.SetColor("_Color", albedo);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Glossiness", smooth);
            if (emission.maxColorComponent > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material Particle(string name, string tex, bool additive, Color color)
        {
            string p = $"{Dir}/{name}.mat";
            Shader sh = Shader.Find("Particles/Standard Unlit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (mat == null) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, p); }
            mat.shader = sh;
            mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
            mat.SetColor("_Color", color);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_Mode", additive ? 4f : 2f);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", additive ? (int)BlendMode.One : (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // =====================================================================================================
        // 3. Prefab da estante
        private static GameObject BuildStandPrefab(Mats m)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var fontMat = AssetDatabase.LoadAssetAtPath<Material>(FontOutlineMat);
            string missing = new string("Maçã Taça Guardião Campeão · ç ã é í ó ú â ê ô".Where(c => c != ' ' && !font.HasCharacter(c)).ToArray());
            sb.AppendLine($"   Fonte {font.name}: glifos PT-BR {(missing.Length == 0 ? "todos presentes" : "FALTAM: " + missing)}.");

            var root = new GameObject("TrophyDisplayStand");
            var visual = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(StandFbx), root.transform);
            visual.name = "Visual";
            var byName = new Dictionary<string, Transform>();
            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                byName[mr.name] = mr.transform;
                mr.sharedMaterial = mr.name.StartsWith("Stand_Ring") ? m.ring
                                  : mr.name.Contains("Trim") || mr.name.Contains("Cap") || mr.name.Contains("Plaque") ? m.brass
                                  : m.dark;
                mr.shadowCastingMode = mr.name.StartsWith("Stand_Ring") || mr.name.StartsWith("Stand_Plaque") ? ShadowCastingMode.Off : ShadowCastingMode.On;
            }

            // Colisores estáticos da base (primitivos): plinto, painel e um bloco baixo por pedestal
            AddBox(root.transform, "Col_Plinth", new Vector3(0f, PlinthH / 2f, 0f), new Vector3(StandWidth, PlinthH, StandDepth));
            AddBox(root.transform, "Col_BackPanel", new Vector3(0f, PlinthH + 0.06f, -(StandDepth / 2f - 0.008f)), new Vector3(StandWidth, 0.12f, 0.016f));

            var shelf = root.AddComponent<TrophyShelfManager>();
            shelf.sfxPickupShimmer = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/trophy_pickup_shimmer.wav");
            shelf.sfxUnlockFanfare = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/trophy_unlock_fanfare.wav");
            shelf.sfxPlaceClick = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/trophy_place_click.wav");
            shelf.lockedSilhouetteMaterial = m.locked;
            shelf.autoRespawnDelay = 1.5f;

            var audioGo = new GameObject("TrophyAudio");
            audioGo.transform.SetParent(root.transform, false);
            audioGo.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            var audio = audioGo.AddComponent<AudioSource>();
            audio.playOnAwake = false; audio.loop = false; audio.spatialBlend = 1f; audio.minDistance = 0.5f; audio.maxDistance = 8f; audio.dopplerLevel = 0f;
            shelf.trophyAudio = audio;

            BuildInfoCard(root.transform, shelf, m, font, fontMat);

            float topY = PlinthH + PedestalH;
            var crownPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Trophy_DroneCrownHat.prefab");
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * SlotSpacing;
                AddBox(root.transform, $"Col_Pedestal_{i}", new Vector3(x, PlinthH + PedestalH / 2f, -PedestalBackOffset), new Vector3(0.10f, PedestalH, 0.10f));

                var anchor = new GameObject($"SlotAnchor_{i}_{Ids[i]}").transform;
                anchor.SetParent(root.transform, false);
                anchor.localPosition = new Vector3(x, topY, -PedestalBackOffset);

                GameObject trophy = BuildTrophy(Prefabs[i], anchor, m);
                trophy.name = $"TrophyItem_{Ids[i]}";

                // Halo atrás do troféu
                var halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(halo.GetComponent<Collider>());
                halo.name = "Halo";
                halo.transform.SetParent(anchor, false);
                halo.transform.localPosition = new Vector3(0f, 0.10f, -0.07f);
                halo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                halo.transform.localScale = new Vector3(0.20f, 0.20f, 1f);
                var haloR = halo.GetComponent<MeshRenderer>();
                haloR.sharedMaterial = m.halo; haloR.shadowCastingMode = ShadowCastingMode.Off; haloR.receiveShadows = false;
                haloR.lightProbeUsage = LightProbeUsage.Off; haloR.reflectionProbeUsage = ReflectionProbeUsage.Off;

                // Rótulo e cadeado: filhos da placa inclinada
                // O import do FBX espelha o X do Blender: liga placa/anel ao slot pela POSIÇÃO, não pelo índice
                Transform plaque = Nearest(byName, "Stand_Plaque_", x);
                var (label, lockR) = BuildPlaqueLabel(plaque, root.transform, Titles[i], font, fontMat, m);

                var ps = BuildUnlockParticles(anchor, m.particles, Accents[i]);

                shelf.slots.Add(new TrophyShelfManager.TrophySlot
                {
                    trophyId = Ids[i], title = Titles[i], description = Descriptions[i],
                    trophyObject = trophy, slotAnchor = anchor, plaqueText = label, plaqueLockIcon = lockR,
                    ringRenderer = Nearest(byName, "Stand_Ring_", x).GetComponent<Renderer>(), haloRenderer = haloR,
                    unlockParticles = ps, accentColor = Accents[i], progress = Progress[i]
                });
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, StandPrefab);
            Object.DestroyImmediate(root);
            int tris = saved.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name.StartsWith("Stand_")).Sum(f => f.sharedMesh.triangles.Length / 3);
            sb.AppendLine($"3) Prefab TrophyDisplayStand salvo: base {tris} tris, 4 troféus com ShelfTrophyGrabInteractable, rótulos nas placas, cartão, TrophyAudio.");
            foreach (var slot in saved.GetComponent<TrophyShelfManager>().slots)
            {
                int tt = slot.trophyObject.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh != null ? f.sharedMesh.triangles.Length / 3 : 0);
                var bc = slot.trophyObject.GetComponent<BoxCollider>();
                sb.AppendLine($"   {slot.trophyId}: {tt} tris, colisor {(bc != null ? $"{bc.size.x:0.000}x{bc.size.y:0.000}x{bc.size.z:0.000} centro y {bc.center.y:0.000}" : "?")}");
                if (tt > 800) sb.AppendLine($"[ERRO] {slot.trophyId} passa de 800 tris");
            }
            return saved;
        }

        private static Transform Nearest(Dictionary<string, Transform> byName, string prefix, float localX)
        {
            Transform best = null; float bd = float.MaxValue;
            foreach (var kv in byName)
            {
                if (!kv.Key.StartsWith(prefix)) continue;
                float d = Mathf.Abs(kv.Value.GetComponent<Renderer>().bounds.center.x - localX);   // prefab na origem
                if (d < bd) { bd = d; best = kv.Value; }
            }
            return best;
        }

        private static void AddBox(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var bc = go.AddComponent<BoxCollider>();
            bc.center = center;
            bc.size = size;
        }

        private static GameObject BuildTrophy(string prefabName, Transform anchor, Mats m)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{prefabName}.prefab");
            GameObject t = Object.Instantiate(src, anchor);        // cópia solta (o prefab do troféu não muda)
            t.transform.localPosition = Vector3.zero;
            t.transform.localRotation = Quaternion.identity;

            // Troca a ErgonomicGrabInteractable pela especialização da estante (o highlight exige um interactable)
            var hl = t.GetComponent<VRRehab.Core.InteractableHighlightFeedback>();
            if (hl != null) Object.DestroyImmediate(hl);
            var old = t.GetComponent<ErgonomicGrabInteractable>();
            float throwScale = old != null ? old.throwVelocityScale : 1.35f;
            if (old != null) Object.DestroyImmediate(old);
            var grab = t.AddComponent<ShelfTrophyGrabInteractable>();
            grab.throwVelocityScale = throwScale;
            var so = new SerializedObject(grab);
            so.FindProperty("customHomeAnchor").objectReferenceValue = anchor;
            so.FindProperty("respawnFloorThresholdY").floatValue = 0.35f;
            so.ApplyModifiedPropertiesWithoutUndo();
            t.AddComponent<VRRehab.Core.InteractableHighlightFeedback>();

            var rb = t.GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            foreach (var mc in t.GetComponentsInChildren<MeshCollider>(true)) Object.DestroyImmediate(mc);
            foreach (var c in t.GetComponentsInChildren<Collider>(true)) if (!c.isTrigger) c.sharedMaterial = m.phys;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                foreach (var mat in r.sharedMaterials) if (mat != null && !mat.enableInstancing) { mat.enableInstancing = true; EditorUtility.SetDirty(mat); }
            foreach (var l in t.GetComponentsInChildren<Light>(true)) l.shadows = LightShadows.None;
            return t;
        }

        private static (TextMeshPro, Renderer) BuildPlaqueLabel(Transform plaque, Transform root, string text, TMP_FontAsset font, Material fontMat, Mats m)
        {
            // Placa reclinada 30°: normal da face da frente (espaço da estante) = (0, sin30, cos30)
            Vector3 n = new Vector3(0f, 0.5f, 0.8660254f);
            Vector3 alongUp = new Vector3(0f, 0.8660254f, -0.5f);
            Bounds pb = plaque.GetComponent<MeshRenderer>().bounds;     // prefab na origem: espaço da estante = mundo
            Vector3 center = root.InverseTransformPoint(pb.center);
            Vector3 face = center + n * 0.0038f;

            var go = new GameObject("Label");
            go.transform.SetParent(plaque, true);
            go.transform.position = root.TransformPoint(face + Vector3.right * 0.008f);
            go.transform.rotation = root.rotation * Quaternion.LookRotation(-n, alongUp);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = font;
            if (fontMat != null) tmp.fontSharedMaterial = fontMat;
            tmp.text = text;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.08f;
            tmp.fontSizeMax = 0.13f;         // teto comum: os 4 nomes saem do mesmo tamanho (~1 cm de letra)
            tmp.fontSize = 0.13f;
            tmp.rectTransform.sizeDelta = new Vector2(0.115f, 0.032f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.characterSpacing = 2f;
            tmp.richText = true;
            tmp.color = new Color(1f, 0.86f, 0.45f);
            tmp.margin = Vector4.zero;
            var mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            mr.sortingOrder = 2;

            var lockGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(lockGo.GetComponent<Collider>());
            lockGo.name = "LockIcon";
            lockGo.transform.SetParent(plaque, true);
            lockGo.transform.position = root.TransformPoint(face + n * 0.0004f + Vector3.left * 0.058f);
            lockGo.transform.rotation = root.rotation * Quaternion.LookRotation(-n, alongUp);
            lockGo.transform.localScale = Vector3.one * 0.022f;
            var lr = lockGo.GetComponent<MeshRenderer>();
            lr.sharedMaterial = m.lockIcon; lr.shadowCastingMode = ShadowCastingMode.Off; lr.receiveShadows = false;
            lr.enabled = false;
            return (tmp, lr);
        }

        private static void BuildInfoCard(Transform root, TrophyShelfManager shelf, Mats m, TMP_FontAsset font, Material fontMat)
        {
            var card = new GameObject("InfoCard").transform;
            card.SetParent(root, false);
            card.localPosition = new Vector3(0f, 0.35f, 0.05f);

            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(bg.GetComponent<Collider>());
            bg.name = "Background";
            bg.transform.SetParent(card, false);
            bg.transform.localScale = new Vector3(0.30f, 0.15f, 1f);
            var bgR = bg.GetComponent<MeshRenderer>();
            bgR.sharedMaterial = m.card; bgR.shadowCastingMode = ShadowCastingMode.Off; bgR.receiveShadows = false;

            TextMeshPro Make(string name, Vector3 pos, Vector2 size, float min, float max, bool bold, bool wrap, Color c)
            {
                var go = new GameObject(name);
                go.transform.SetParent(card, false);
                go.transform.localPosition = pos + new Vector3(0f, 0f, -0.002f);
                var t = go.AddComponent<TextMeshPro>();
                t.font = font;
                if (fontMat != null) t.fontSharedMaterial = fontMat;
                t.enableAutoSizing = true; t.fontSizeMin = min; t.fontSizeMax = max; t.fontSize = max;
                t.rectTransform.sizeDelta = size;
                t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
                t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                t.overflowMode = TextOverflowModes.Ellipsis;
                t.alignment = bold ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;
                t.richText = true;
                t.color = c;
                var r = go.GetComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.sortingOrder = 5;
                return t;
            }
            shelf.infoTitle = Make("Title", new Vector3(0f, 0.045f, 0f), new Vector2(0.27f, 0.035f), 0.10f, 0.30f, true, false, new Color(1f, 0.86f, 0.45f));
            shelf.infoBody = Make("Body", new Vector3(0f, -0.018f, 0f), new Vector2(0.265f, 0.085f), 0.08f, 0.18f, false, true, new Color(0.92f, 0.94f, 0.98f));
            shelf.infoTitle.text = "Troféu";
            shelf.infoBody.text = "Descrição";
            shelf.infoCard = card;
            card.gameObject.SetActive(false);
        }

        private static ParticleSystem BuildUnlockParticles(Transform anchor, Material mat, Color accent)
        {
            var go = new GameObject("UnlockParticles");
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false; main.loop = false; main.duration = 0.5f; main.maxParticles = 36;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.03f);
            main.startColor = new ParticleSystem.MinMaxGradient(accent, Color.white);
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 32) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.05f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var coll = ps.collision; coll.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return ps;
        }

        // =====================================================================================================
        // 4. Cena
        private static GameObject ReplaceInScene(GameObject prefab)
        {
            var olds = Object.FindObjectsByType<TrophyShelfManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            sb.AppendLine($"Estantes antigas na cena: {olds.Length}.");
            Transform oldParent = olds.Length > 0 ? olds[0].transform.parent : null;
            foreach (var o in olds) Object.DestroyImmediate(o.gameObject);
            // Restos soltos de rodadas antigas (rótulos/troféus fora da estante)
            int stray = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null) continue;
                bool trophyLike = t.name.StartsWith("Label_GoldenApple") || t.name.StartsWith("Label_LuminousBonsai") || t.name.StartsWith("Label_StarCup") || t.name.StartsWith("Label_DroneCrown")
                                  || t.name.StartsWith("TrophyItem_");
                if (trophyLike && t.GetComponentInParent<TrophyShelfManager>() == null) { Object.DestroyImmediate(t.gameObject); stray++; }
            }
            sb.AppendLine($"Restos soltos removidos: {stray}.");

            var stand = (GameObject)PrefabUtility.InstantiatePrefab(prefab, EditorSceneManager.GetActiveScene());
            stand.name = "TrophyDisplayStand_Instance";
            if (oldParent != null) stand.transform.SetParent(oldParent, true);
            PlaceStand(stand);
            return stand;
        }

        /// <summary>
        /// Procura no móvel-alvo o ponto com mais folga para a pegada W x D (girada para a frente ficar voltada ao
        /// paciente), exige apoio em toda a pegada e nenhum colisor no volume da estante; aterra pelo raycast.
        /// </summary>
        private static void PlaceStand(GameObject stand)
        {
            Transform furniture = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == TargetFurniture);
            if (furniture == null) throw new Exception("Móvel não encontrado: " + TargetFurniture);
            Physics.SyncTransforms();
            Bounds fb = new Bounds(furniture.position, Vector3.zero);
            foreach (var r in furniture.GetComponentsInChildren<Renderer>()) fb.Encapsulate(r.bounds);
            var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            Vector3 viewer = origin != null ? origin.transform.position : fb.center + Vector3.forward * 2f;

            // Frente: o eixo horizontal do móvel (±X/±Z local) que mais aponta para o paciente
            Vector3[] axes = { furniture.forward, -furniture.forward, furniture.right, -furniture.right };
            Vector3 toViewer = viewer - fb.center; toViewer.y = 0f; toViewer.Normalize();
            Vector3 front = axes.OrderByDescending(a => Vector3.Dot(new Vector3(a.x, 0, a.z).normalized, toViewer)).First();
            front.y = 0f; front.Normalize();
            Quaternion rot = Quaternion.LookRotation(front, Vector3.up);
            Vector3 right = rot * Vector3.right;

            var own = stand.GetComponentsInChildren<Collider>(true);
            var neighbors = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(r => r.enabled && !(r is ParticleSystemRenderer) && !r.transform.IsChildOf(furniture) && !r.transform.IsChildOf(stand.transform))
                .Select(r => r.bounds).Where(b => Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) < 2.4f).ToList();

            float best = -1f; Vector3 bestP = Vector3.zero;
            int levels = 0;
            for (float u = fb.min.x; u <= fb.max.x; u += 0.02f)
            for (float w = fb.min.z; w <= fb.max.z; w += 0.02f)
            {
                // Cada tábua do móvel nesse (u, w). O móvel é UM MeshCollider côncavo e o RaycastAll só devolve um
                // ponto por colisor, então lançamos raios curtos começando em várias alturas.
                float lastY = -1f;
                for (float sy = 1.47f; sy >= 1.0f; sy -= 0.04f)
                {
                    if (!Physics.Raycast(new Vector3(u, sy, w), Vector3.down, out RaycastHit h, 0.05f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (!h.collider.transform.IsChildOf(furniture) || h.normal.y < 0.9f) continue;
                    float y = h.point.y;
                    if (Mathf.Abs(y - lastY) < 0.005f) continue;
                    lastY = y;
                    if (y < 1.0f || y > 1.45f) continue;                 // altura do peito (tábua de 1,19 m)
                    Vector3 p = new Vector3(u, y, w);
                    if (!Supported(furniture, p, rot)) continue;
                    // Volume livre: base + troféus (até 0,27 m de altura; há 0,30 m até a tábua de cima)
                    Vector3 c = p + Vector3.up * 0.145f;
                    var hitCols = Physics.OverlapBox(c, new Vector3(StandWidth / 2f + 0.005f, 0.13f, StandDepth / 2f + 0.005f), rot, ~0, QueryTriggerInteraction.Ignore);
                    if (hitCols.Any(col => !own.Contains(col))) continue;
                    levels++;
                    float clear = Clearance(p, rot, neighbors, y);
                    float frontBias = Vector3.Dot(p - fb.center, front) * 0.05f;     // prefere a borda da frente (alcance do laser)
                    if (clear + frontBias > best) { best = clear + frontBias; bestP = p; }
                }
            }
            if (best < 0f) throw new Exception($"Nenhum ponto livre para a estante de {StandWidth} m no {TargetFurniture}");
            stand.transform.SetPositionAndRotation(bestP, rot);
            Physics.SyncTransforms();
            sb.AppendLine($"Estante no {TargetFurniture}: {V(bestP)}, rotY {rot.eulerAngles.y:0.0}, frente para o XR Origin {V(viewer)} ({levels} pontos válidos avaliados).");
        }

        private static bool Supported(Transform furniture, Vector3 p, Quaternion rot)
        {
            float hx = StandWidth / 2f - 0.02f, hz = StandDepth / 2f - 0.02f;
            for (int i = 0; i < 9; i++)
            {
                float a = (i % 3 - 1) * hx, b = (i / 3 - 1) * hz;
                Vector3 q = p + rot * new Vector3(a, 0f, b);
                if (!Physics.Raycast(q + Vector3.up * 0.05f, Vector3.down, out RaycastHit h, 0.12f, ~0, QueryTriggerInteraction.Ignore)) return false;
                if (!h.collider.transform.IsChildOf(furniture) || Mathf.Abs(h.point.y - p.y) > 0.01f) return false;
            }
            return true;
        }

        private static float Clearance(Vector3 p, Quaternion rot, List<Bounds> neighbors, float top)
        {
            // AABB aproximado da estante girada
            Vector3 ext = new Vector3(Mathf.Abs((rot * Vector3.right).x) * StandWidth / 2f + Mathf.Abs((rot * Vector3.forward).x) * StandDepth / 2f, 0.16f,
                                      Mathf.Abs((rot * Vector3.right).z) * StandWidth / 2f + Mathf.Abs((rot * Vector3.forward).z) * StandDepth / 2f);
            Bounds sb2 = new Bounds(p + Vector3.up * 0.16f, ext * 2f);
            float best = 10f;
            foreach (var nb in neighbors)
            {
                if (nb.max.y < top + 0.005f || nb.min.y > top + 0.29f) continue;   // o que está na tábua de cima não encosta
                float dx = Mathf.Max(0f, Mathf.Max(nb.min.x - sb2.max.x, sb2.min.x - nb.max.x));
                float dz = Mathf.Max(0f, Mathf.Max(nb.min.z - sb2.max.z, sb2.min.z - nb.max.z));
                best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
            }
            return best;
        }

        // =====================================================================================================
        // 5. Auditoria e capturas estáticas
        private static void WriteAudit(GameObject stand, int originCleaned, int dupCleaned)
        {
            var shelf = stand.GetComponent<TrophyShelfManager>();
            Bounds b = RendererBounds(stand, true);
            var own = stand.GetComponentsInChildren<Collider>(true);
            Vector3 p0 = stand.transform.position;
            RaycastHit hit = Physics.RaycastAll(p0 + Vector3.up * 0.03f, Vector3.down, 0.5f, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => !own.Contains(h.collider)).OrderBy(h => h.distance).FirstOrDefault();
            float gap = hit.collider != null ? p0.y - hit.point.y : 99f;
            string support = hit.collider != null ? hit.collider.name : "-";

            float nearest = 10f; string nearestName = "-";
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r.enabled || r is ParticleSystemRenderer || r.transform.IsChildOf(stand.transform)) continue;
                if (hit.collider != null && r.transform.IsChildOf(TopFurniture(hit.collider.transform))) continue;
                Bounds nb = r.bounds;
                if (Mathf.Max(nb.size.x, Mathf.Max(nb.size.y, nb.size.z)) >= 2.4f || nb.max.y < b.min.y + 0.005f) continue;
                float dx = Mathf.Max(0f, Mathf.Max(nb.min.x - b.max.x, b.min.x - nb.max.x));
                float dy = Mathf.Max(0f, Mathf.Max(nb.min.y - b.max.y, b.min.y - nb.max.y));
                float dz = Mathf.Max(0f, Mathf.Max(nb.min.z - b.max.z, b.min.z - nb.max.z));
                float d = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
                if (d < nearest) { nearest = d; nearestName = AnimationUtility.CalculateTransformPath(r.transform, null); }
            }

            // Pares de colisores dos troféus em repouso
            var pairs = new List<string>();
            float maxPen = 0f;
            var trophyCols = shelf.slots.Select(s => s.trophyObject.GetComponent<Collider>()).ToArray();
            var baseCols = stand.GetComponentsInChildren<BoxCollider>(true).Where(c => c.name.StartsWith("Col_")).ToArray();
            for (int i = 0; i < trophyCols.Length; i++)
            {
                for (int j = i + 1; j < trophyCols.Length; j++) maxPen = Mathf.Max(maxPen, Pen(trophyCols[i], trophyCols[j]));
                foreach (var bc in baseCols) maxPen = Mathf.Max(maxPen, Pen(trophyCols[i], bc));
            }
            for (int i = 0; i < 4; i++)
                for (int j = i + 1; j < 4; j++)
                {
                    Bounds a = trophyCols[i].bounds, c = trophyCols[j].bounds;
                    float g = Mathf.Max(0f, Mathf.Max(c.min.x - a.max.x, a.min.x - c.max.x)) + Mathf.Max(0f, Mathf.Max(c.min.z - a.max.z, a.min.z - c.max.z));
                    pairs.Add($"    {{ \"a\": \"{shelf.slots[i].trophyId}\", \"b\": \"{shelf.slots[j].trophyId}\", \"centerDistance\": {Vector3.Distance(trophyCols[i].transform.position, trophyCols[j].transform.position):F3}, \"colliderGap\": {g:F3} }}");
                }

            var atOrigin = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(t => t.parent == null && t.position.sqrMagnitude < 1f && !SpatialAuditUtility.IsWhitelisted(t.gameObject)).Select(t => t.name).ToList();
            int stands = Object.FindObjectsByType<TrophyShelfManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            int trophies = Object.FindObjectsByType<ShelfTrophyGrabInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            int labels = Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(t => t.fontSize >= 0.99f && t.GetComponentInParent<TrophyShelfManager>() != null);

            var j2 = new StringBuilder();
            j2.AppendLine("{");
            j2.AppendLine("  \"taskId\": \"TASK-16 (retoque da estante)\",");
            j2.AppendLine($"  \"generatedAt\": \"{DateTime.Now:yyyy-MM-ddTHH:mm:ss}\",");
            j2.AppendLine($"  \"instanceCounts\": {{ \"TrophyShelfManager\": {stands}, \"ShelfTrophyGrabInteractable\": {trophies} }},");
            j2.AppendLine($"  \"objectsAtOriginZero\": [{string.Join(", ", atOrigin.Select(n => $"\"{n}\""))}],");
            j2.AppendLine($"  \"isOriginClean\": {(atOrigin.Count == 0 ? "true" : "false")},");
            j2.AppendLine("  \"duplicateObjectsFound\": [],");
            j2.AppendLine($"  \"purgeAfter\": {{ \"originCleaned\": {originCleaned}, \"duplicatesCleaned\": {dupCleaned} }},");
            j2.AppendLine($"  \"stand\": {{ \"position\": {JV(p0)}, \"rotationY\": {stand.transform.eulerAngles.y:F1}, \"widthMeters\": {StandWidth}, \"slotSpacingMeters\": {SlotSpacing}, \"support\": \"{support}\", \"groundGapMeters\": {gap:F4}, \"nearestNeighbor\": \"{nearestName}\", \"clearanceMeters\": {nearest:F3} }},");
            j2.AppendLine("  \"pairwiseClearances\": [");
            j2.AppendLine(string.Join(",\n", pairs));
            j2.AppendLine("  ],");
            j2.AppendLine($"  \"hasOverlap\": {(maxPen > 0.0005f || nearest <= 0f ? "true" : "false")},");
            j2.AppendLine($"  \"labelsWithFontSize1\": {labels},");
            j2.AppendLine($"  \"shelfStability\": {{ \"restColliderMaxPenetrationMeters\": {maxPen:F4}, \"note\": \"deslocamento da raiz e deriva dos troféus medidos em Play Mode: TASK-16_PlayModeTest.txt\" }}");
            j2.AppendLine("}");
            File.WriteAllText(Path.Combine(QaDir, "TASK-16_SpatialAudit.json"), j2.ToString());
            sb.AppendLine($"Auditoria: apoio '{support}', gap {gap * 1000f:F1} mm, folga {nearest:F3} m de '{nearestName}', penetração máx. em repouso {maxPen * 1000f:F2} mm, estantes {stands}, troféus {trophies}.");
            if (gap > 0.01f || gap < -0.005f) sb.AppendLine("[ERRO] estante não aterrada");
            if (stands != 1 || trophies != 4) sb.AppendLine("[ERRO] contagem de estante/troféus");
        }

        private static Transform TopFurniture(Transform t)
        {
            while (t.parent != null && t.parent.name != "Building") t = t.parent;
            return t;
        }

        private static float Pen(Collider a, Collider b)
        {
            return Physics.ComputePenetration(a, a.transform.position, a.transform.rotation, b, b.transform.position, b.transform.rotation, out _, out float d) ? d : 0f;
        }

        private static Bounds RendererBounds(GameObject go, bool skipCard)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer) && r.enabled && (!skipCard || !r.transform.IsChildOfName("InfoCard"))).ToArray();
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        private static bool IsChildOfName(this Transform t, string name)
        {
            for (Transform p = t; p != null; p = p.parent) if (p.name == name) return true;
            return false;
        }

        private static string V(Vector3 v) => $"({v.x:F3}, {v.y:F3}, {v.z:F3})";
        private static string JV(Vector3 v) => $"[{v.x:F3}, {v.y:F3}, {v.z:F3}]";

        private static void Captures(GameObject stand)
        {
            Transform s = stand.transform;
            Vector3 c = s.position + Vector3.up * 0.14f;
            Shot("TASK-16_Shelf_Front.png", c + s.forward * 1.25f + Vector3.up * 0.25f, c, 50f);
            Shot("TASK-16_Shelf_Side_Grounding.png", s.position + s.right * 0.75f + s.forward * 0.25f + Vector3.up * 0.06f, s.position + Vector3.up * 0.04f, 45f);
            Shot("TASK-16_Shelf_Closeup_Plaques.png", s.position + s.forward * 0.42f + Vector3.up * 0.14f, s.position + s.forward * 0.08f + Vector3.up * 0.06f, 50f);
            // Panorama: de onde o paciente para diante do móvel (2,2 m à frente, altura dos olhos), mostrando a sala em volta
            Vector3 eye = new Vector3(s.position.x, 1.55f, s.position.z) + s.forward * 2.2f;
            Shot("TASK-16_Shelf_Panorama_Room.png", eye, c, 75f);
        }

        public static void Shot(string file, Vector3 pos, Vector3 lookAt, float fov)
        {
            var go = new GameObject("__Task16Cam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation((lookAt - pos).normalized, Vector3.up));
            cam.fieldOfView = fov; cam.nearClipPlane = 0.02f; cam.farClipPlane = 60f;
            cam.clearFlags = CameraClearFlags.Skybox; cam.allowHDR = false; cam.allowMSAA = false; cam.enabled = false;
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
            Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
            if (sb != null) sb.AppendLine($"   captura {file}");
        }
    }
}
