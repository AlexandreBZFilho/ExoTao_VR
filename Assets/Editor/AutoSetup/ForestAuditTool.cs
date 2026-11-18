using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRRehab.EditorTools
{
    /// <summary>
    /// Auditoria SOMENTE LEITURA dos objetos interativos da Floresta_e_Rio (Tasks 11-15):
    /// Missing Script, duplicatas, objetos na origem, fora do terreno, abaixo do chão ou flutuando.
    /// Menu: "ExoTao IC/Floresta/Auditar Floresta (somente leitura)".
    /// </summary>
    public static class ForestAuditTool
    {
        public const string ForestScene = "Assets/Scenes/Floresta_e_Rio.unity";

        public class Item
        {
            public GameObject go;
            public string type;
            public string path;
            public Vector3 pos;
            public float groundY;
            public float gap;          // base do objeto - chão
            public bool hasGround;
            public bool insideTerrain;
            public bool nearOrigin;
            public bool duplicate;
        }

        [MenuItem("ExoTao IC/Floresta/Auditar Floresta (somente leitura)")]
        public static void AuditFromMenu()
        {
            string r = Audit(ForestScene, ".agents/visual_qa/FOREST_Audit_Manual.txt", out _);
            Debug.Log(r);
        }

        /// <summary>Tipos de gameplay auditados: tudo em VRRehab.Interactive.Task11..Task15.</summary>
        public static bool IsForestGameplayType(System.Type t)
        {
            string ns = t.Namespace ?? "";
            return ns.StartsWith("VRRehab.Interactive.Task11") || ns.StartsWith("VRRehab.Interactive.Task12") ||
                   ns.StartsWith("VRRehab.Interactive.Task13") || ns.StartsWith("VRRehab.Interactive.Task14") ||
                   ns.StartsWith("VRRehab.Interactive.Task15");
        }

        public static List<Item> Collect(Scene scene)
        {
            Physics.SyncTransforms();
            var items = new List<Item>();
            var seen = new HashSet<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null || !IsForestGameplayType(mb.GetType())) continue;
                    if (!seen.Add(mb.gameObject)) continue;
                    items.Add(Measure(mb.gameObject, mb.GetType().Name));
                }
            }

            // Duplicatas: mesmo tipo praticamente no mesmo lugar
            for (int i = 0; i < items.Count; i++)
                for (int j = i + 1; j < items.Count; j++)
                    if (items[i].type == items[j].type && (items[i].pos - items[j].pos).sqrMagnitude < 0.05f * 0.05f)
                        items[j].duplicate = true;
            return items;
        }

        public static Item Measure(GameObject go, string type)
        {
            var it = new Item { go = go, type = type, path = PathOf(go.transform), pos = go.transform.position };
            it.nearOrigin = it.pos.magnitude < 1.0f;

            Bounds b = RendererBounds(go, out bool hasRenderer);
            float baseY = hasRenderer ? b.min.y : it.pos.y;

            it.insideTerrain = false;
            it.hasGround = TryGroundHeight(go, it.pos, out it.groundY, out it.insideTerrain);
            it.gap = it.hasGround ? baseY - it.groundY : float.NaN;
            return it;
        }

        /// <summary>Altura do chão em XZ: terreno (se houver) ou o colisor sólido mais alto abaixo do ponto que não seja do próprio objeto.</summary>
        public static bool TryGroundHeight(GameObject self, Vector3 p, out float groundY, out bool insideTerrain)
        {
            groundY = 0f;
            insideTerrain = false;
            bool found = false;

            foreach (Terrain t in Terrain.activeTerrains)
            {
                Vector3 tp = t.transform.position;
                Vector3 size = t.terrainData.size;
                if (p.x < tp.x || p.x > tp.x + size.x || p.z < tp.z || p.z > tp.z + size.z) continue;
                insideTerrain = true;
                float h = t.SampleHeight(p) + tp.y;
                if (!found || h > groundY) { groundY = h; found = true; }
            }

            // Chão por colisores (malhas de cenário, pontes, deck): o impacto mais alto ABAIXO do topo do objeto
            Vector3 origin = new Vector3(p.x, p.y + 30f, p.z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            foreach (RaycastHit h in hits)
            {
                if (self != null && (h.collider.transform == self.transform || h.collider.transform.IsChildOf(self.transform))) continue;
                if (h.collider.attachedRigidbody != null && !h.collider.attachedRigidbody.isKinematic) continue; // ignora objetos soltos
                if (h.point.y > p.y + 0.5f) continue; // copas de árvore / tetos acima do objeto
                if (h.point.y > best) best = h.point.y;
            }
            if (!float.IsNegativeInfinity(best))
            {
                if (Terrain.activeTerrains.Length == 0) insideTerrain = true;
                if (!found || best > groundY) { groundY = best; found = true; }
            }
            return found;
        }

        public static Bounds RendererBounds(GameObject go, out bool has)
        {
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            has = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        public static int CountMissingScripts(Scene scene, List<string> where)
        {
            int n = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    int c = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                    if (c > 0) { n += c; where?.Add($"{PathOf(t)} ({c})"); }
                }
            return n;
        }

        public static string Audit(string scenePath, string reportPath, out List<Item> items)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var sb = new StringBuilder();
            sb.AppendLine($"=== Auditoria da {scene.name} ({System.DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");

            Terrain[] terrains = Terrain.activeTerrains;
            sb.AppendLine($"Terrenos ativos: {terrains.Length}");
            foreach (Terrain t in terrains)
                sb.AppendLine($"  {t.name}: origem {V(t.transform.position)} tamanho {V(t.terrainData.size)}");

            var missing = new List<string>();
            int missingCount = CountMissingScripts(scene, missing);
            sb.AppendLine($"Missing Script: {missingCount}");
            foreach (string m in missing.Take(30)) sb.AppendLine("  " + m);

            items = Collect(scene);
            sb.AppendLine($"Objetos de gameplay (Tasks 11-15): {items.Count}");
            foreach (var g in items.GroupBy(i => i.type).OrderBy(g => g.Key))
                sb.AppendLine($"  {g.Key}: {g.Count()}");

            int dup = items.Count(i => i.duplicate), origin = items.Count(i => i.nearOrigin && !IsManagerOnGroup(i));
            int outside = items.Count(i => !i.insideTerrain && !IsManagerOnGroup(i)), below = items.Count(i => i.hasGround && i.gap < -0.10f && !IsManagerOnGroup(i));
            int floating = items.Count(i => i.hasGround && i.gap > 0.15f && !ElevatedByDesign(i));
            int elevated = items.Count(i => i.hasGround && i.gap > 0.15f && ElevatedByDesign(i));
            sb.AppendLine($"Duplicatas: {dup} | perto de (0,0,0): {origin} | fora do mapa/sem chão: {outside} | abaixo do chão: {below} | FLUTUANDO (>0.15 m, deveria estar no chão): {floating} | no alto por design (moedas, pássaro, pinhão no pedestal, maçã na cesta, drone): {elevated}");
            sb.AppendLine();
            sb.AppendLine("tipo | caminho | posição | chão | gap | flags");
            foreach (Item i in items.OrderBy(i => i.type).ThenBy(i => i.path))
            {
                var flags = new List<string>();
                if (i.duplicate) flags.Add("DUPLICATA");
                if (IsManagerOnGroup(i)) flags.Add("gerenciador no grupo (sem corpo)");
                else
                {
                    if (i.nearOrigin) flags.Add("ORIGEM");
                    if (!i.insideTerrain) flags.Add("FORA_DO_MAPA");
                    if (i.hasGround && i.gap < -0.10f) flags.Add("ABAIXO_DO_CHAO");
                }
                if (i.hasGround && i.gap > 0.15f) flags.Add(ElevatedByDesign(i) ? "no alto por design" : "FLUTUANDO");
                if (!i.go.activeInHierarchy) flags.Add("inativo");
                sb.AppendLine($"{i.type} | {i.path} | {V(i.pos)} | {(i.hasGround ? i.groundY.ToString("0.00") : "-")} | {(i.hasGround ? i.gap.ToString("+0.00;-0.00") : "-")} | {string.Join(",", flags)}");
            }

            // Raízes da cena (para achar lixo e grupos de tasks)
            sb.AppendLine();
            sb.AppendLine("Raízes da cena:");
            foreach (GameObject root in scene.GetRootGameObjects())
                sb.AppendLine($"  {root.name} {V(root.transform.position)} filhos={root.transform.childCount}{(root.activeSelf ? "" : " (inativo)")}");

            // Luzes com sombra em objetos de gameplay
            var shadowLights = new List<string>();
            foreach (Item i in items)
                foreach (Light l in i.go.GetComponentsInChildren<Light>(true))
                    if (l.shadows != LightShadows.None) shadowLights.Add(PathOf(l.transform));
            sb.AppendLine();
            sb.AppendLine($"Luzes de gameplay com sombra: {shadowLights.Count}");
            foreach (string s in shadowLights) sb.AppendLine("  " + s);

            string report = sb.ToString();
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, report);
            return report;
        }

        /// <summary>Script de gerenciamento pendurado num grupo/contêiner na origem (não é um objeto físico).</summary>
        public static bool IsManagerOnGroup(Item i)
        {
            return i.go.transform.position.sqrMagnitude < 0.01f && i.go.transform.childCount > 0 && i.go.GetComponent<Renderer>() == null;
        }

        /// <summary>Itens que ficam acima do chão de propósito.</summary>
        public static bool ElevatedByDesign(Item i)
        {
            return i.type == "CollectibleOrbCoin" || i.type == "ReactiveForestBird" || i.type == "SessionQuestManager" ||
                   i.type == "CoinScoreManager" || i.type == "MagicSeedPinecone" || i.go.name == "Apple_In_Basket";
        }

        public static string V(Vector3 v) => $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";

        public static string PathOf(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        /// <summary>Renderiza uma câmera temporária offscreen para PNG (sem mexer em janela/mouse).</summary>
        public static string RenderTo(string path, Vector3 camPos, Vector3 lookAt, float fov, int w = 1920, int h = 1080, int cullingMask = ~0, Color? solidBackground = null)
        {
            var camGo = new GameObject("~QACam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 600f;
            cam.cullingMask = cullingMask;
            cam.clearFlags = solidBackground.HasValue ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
            if (solidBackground.HasValue) cam.backgroundColor = solidBackground.Value;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            RenderTexture prev = RenderTexture.active;
            try
            {
                camGo.transform.position = camPos;
                camGo.transform.LookAt(lookAt);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = prev;
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(camGo);
            }
            return Path.GetFullPath(path);
        }
    }
}
