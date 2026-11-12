using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace VRRehab.EditorAutoSetup
{
    /// <summary>
    /// TASK-06.D (re-run): Scout de móveis/superfícies da sala do Menu_Principal.
    /// Lista roots, móveis (mesas/bancadas/estantes) com bounds e os itens apoiados sobre cada um.
    /// Gatilho: arquivo ".run_task06_scout" na raiz do projeto, ou menu VRRehab/AutoBuild/TASK-06 Furniture Scout.
    /// Saída: .agents/visual_qa/TASK-06_FurnitureScout.txt
    /// </summary>
    [InitializeOnLoad]
    public static class Task06_FurnitureScout
    {
        private const string TriggerFile = ".run_task06_scout";
        private const string OutFile = ".agents/visual_qa/TASK-06_FurnitureScout.txt";
        private const string MenuScenePath = "Assets/Scenes/Menu_Principal.unity";
        private static bool running;

        static Task06_FurnitureScout()
        {
            EditorApplication.update += () =>
            {
                if (running) return;
                string p = Path.Combine(Directory.GetCurrentDirectory(), TriggerFile);
                if (!File.Exists(p)) return;
                try { File.Delete(p); } catch { }
                Run();
            };
        }

        private static readonly string[] FurnitureKeys = { "desk", "table", "shelf", "shelves", "counter", "bench", "bancada", "estante", "mesa", "drawer", "cabinet", "stand", "rack" };

        public static bool IsFurnitureName(string n)
        {
            string l = n.ToLowerInvariant();
            foreach (var k in FurnitureKeys) if (l.Contains(k)) return true;
            return false;
        }

        public static bool TryBounds(GameObject go, out Bounds b)
        {
            b = new Bounds();
            bool has = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            return has;
        }

        private static string V(Vector3 v) => $"({v.x:F3}, {v.y:F3}, {v.z:F3})";

        private static string Path_(Transform t)
        {
            var s = t.name;
            while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }

        [MenuItem("VRRehab/AutoBuild/TASK-06 Furniture Scout")]
        public static void Run()
        {
            running = true;
            var sb = new StringBuilder();
            try
            {
                if (EditorSceneManager.GetActiveScene().path != MenuScenePath)
                {
                    EditorSceneManager.SaveOpenScenes();
                    EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
                }
                var scene = EditorSceneManager.GetActiveScene();
                sb.AppendLine($"=== TASK-06 FURNITURE SCOUT === {DateTime.Now:yyyy-MM-dd HH:mm:ss} scene={scene.path} playing={EditorApplication.isPlaying}");

                sb.AppendLine("\n--- ROOTS ---");
                foreach (var root in scene.GetRootGameObjects())
                {
                    string bs = TryBounds(root, out var b) ? $"bounds c={V(b.center)} size={V(b.size)} min.y={b.min.y:F3}" : "no-renderer";
                    sb.AppendLine($"[{(root.activeInHierarchy ? "on" : "off")}] '{root.name}' pos={V(root.transform.position)} rotY={root.transform.eulerAngles.y:F1} scale={V(root.transform.lossyScale)} children={root.transform.childCount} {bs}");
                }

                sb.AppendLine("\n--- FURNITURE (any depth) ---");
                var furniture = new List<(Transform t, Bounds b)>();
                foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (!IsFurnitureName(t.name)) continue;
                    if (t.parent != null && IsFurnitureName(t.parent.name)) continue; // only top-most furniture node
                    if (!TryBounds(t.gameObject, out var b)) continue;
                    if (b.size.x > 6f || b.size.z > 6f) continue;
                    furniture.Add((t, b));
                }
                furniture.Sort((a, c) => string.CompareOrdinal(Path_(a.t), Path_(c.t)));
                foreach (var f in furniture)
                {
                    // Topo via raycast vertical em 5 pontos
                    var tops = new List<string>();
                    Vector3[] pts = { f.b.center, f.b.center + new Vector3(f.b.extents.x * 0.6f, 0, 0), f.b.center - new Vector3(f.b.extents.x * 0.6f, 0, 0), f.b.center + new Vector3(0, 0, f.b.extents.z * 0.6f), f.b.center - new Vector3(0, 0, f.b.extents.z * 0.6f) };
                    foreach (var p in pts)
                    {
                        var origin = new Vector3(p.x, f.b.max.y + 0.5f, p.z);
                        var hits = Physics.RaycastAll(origin, Vector3.down, f.b.size.y + 1f, ~0, QueryTriggerInteraction.Ignore);
                        Array.Sort(hits, (h1, h2) => h1.distance.CompareTo(h2.distance));
                        var hs = new List<string>();
                        foreach (var h in hits) hs.Add($"{h.point.y:F3}:{h.collider.name}");
                        tops.Add("[" + string.Join(" | ", hs) + "]");
                    }
                    sb.AppendLine($"FURN '{Path_(f.t)}' pos={V(f.t.position)} rotY={f.t.eulerAngles.y:F1} bounds min={V(f.b.min)} max={V(f.b.max)}");
                    sb.AppendLine($"     rayHits(center,+x,-x,+z,-z)={string.Join(" ", tops)}");
                }

                sb.AppendLine("\n--- ITEMS ABOVE FURNITURE (non-furniture renderers' top-most non-furniture ancestor, inside furniture XZ footprint) ---");
                var seen = new HashSet<Transform>();
                foreach (var f in furniture)
                {
                    sb.AppendLine($"ON '{Path_(f.t)}':");
                    foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    {
                        if (r.transform.IsChildOf(f.t)) continue;
                        var c = r.bounds.center;
                        if (c.x < f.b.min.x || c.x > f.b.max.x || c.z < f.b.min.z || c.z > f.b.max.z) continue;
                        if (r.bounds.min.y < f.b.min.y - 0.05f || r.bounds.min.y > f.b.max.y + 0.6f) continue;
                        // ancestor: the root-level node (or direct child of a big container root)
                        Transform a = r.transform;
                        while (a.parent != null && !IsFurnitureName(a.parent.name) && a.parent.parent != null) a = a.parent;
                        if (a.parent != null && a.parent.parent == null && !IsFurnitureName(a.parent.name))
                        {
                            // a is child of a root; prefer the root unless the root is huge
                            if (TryBounds(a.parent.gameObject, out var rb) && rb.size.magnitude < 2.5f) a = a.parent;
                        }
                        var key = a;
                        string tag = f.t.GetInstanceID() + ":" + key.GetInstanceID();
                        if (!seen.Add(key) && false) continue;
                        if (sbContains(sb, tag)) continue;
                        TryBounds(key.gameObject, out var kb);
                        sb.AppendLine($"   #{tag} '{Path_(key)}' pos={V(key.position)} rotY={key.eulerAngles.y:F1} bounds min={V(kb.min)} max={V(kb.max)}");
                    }
                }

                sb.AppendLine("\n--- KEY ITEMS (by name) ---");
                string[] keys = { "Radio", "Cassette", "Tape_", "Basket", "Firework", "Trophy", "Crown", "Balloon", "Diorama", "Portal", "Bobble", "Lever", "Toy", "Baton", "Hoop", "Spawner", "Drone", "Pedestal", "Mascot", "Atmosphere" };
                foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    bool m = false;
                    foreach (var k in keys) if (t.name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) { m = true; break; }
                    if (!m) continue;
                    if (t.parent != null)
                    {
                        bool parentMatches = false;
                        foreach (var k in keys) if (t.parent.name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) { parentMatches = true; break; }
                        if (parentMatches) continue;
                    }
                    string bs = TryBounds(t.gameObject, out var b) ? $"min={V(b.min)} max={V(b.max)}" : "no-renderer";
                    var cols = t.GetComponentsInChildren<Collider>(true);
                    sb.AppendLine($"[{(t.gameObject.activeInHierarchy ? "on" : "off")}] '{Path_(t)}' pos={V(t.position)} rotY={t.eulerAngles.y:F1} lossy={V(t.lossyScale)} {bs} colliders={cols.Length} prefab={PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)}");
                }

                sb.AppendLine("\n--- ORIGIN (r<1.0m) roots/interactables ---");
                foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (t.position.magnitude < 1.0f && (t.parent == null || t.GetComponent<UnityEngine.XR.Interaction.Toolkit.XRBaseInteractable>() != null))
                        sb.AppendLine($"  '{Path_(t)}' pos={V(t.position)}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("[ERRO] " + ex);
            }
            finally
            {
                Directory.CreateDirectory(Path.GetDirectoryName(OutFile));
                File.WriteAllText(OutFile, sb.ToString());
                Debug.Log("[Task06_FurnitureScout] escrito em " + OutFile);
                running = false;
            }
        }

        private static bool sbContains(StringBuilder sb, string tag) => sb.ToString().Contains("#" + tag + " ");
    }
}
