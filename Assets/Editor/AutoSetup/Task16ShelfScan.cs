using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VRRehab.EditorTools
{
    /// <summary>TASK-16 [A]: varredura (edit mode, somente leitura) dos niveis da estante modern_shelves_3 (1). Saida: .agents/visual_qa/TASK-16_ShelfScan.txt</summary>
    public static class Task16ShelfScan
    {
        private static StringBuilder sb;
        private static string V(Vector3 v) => $"({v.x:0.000}, {v.y:0.000}, {v.z:0.000})";
        private static void L(string s) => sb.AppendLine(s);

        public static void Run()
        {
            int code = 1; sb = new StringBuilder();
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/Menu_Principal.unity", OpenSceneMode.Single);
                Physics.SyncTransforms();
                Execute(); code = 0;
            }
            catch (Exception e) { L("EXCECAO " + e); }
            File.WriteAllText(".agents/visual_qa/TASK-16_ShelfScan.txt", sb.ToString());
            EditorApplication.Exit(code);
        }

        private static void Execute()
        {
            var shelf = GameObject.Find("modern_shelves_3 (1)");
            var stand = GameObject.Find("TrophyDisplayStand_Instance");
            var sc = shelf.GetComponent<Collider>();
            L($"shelf pos={V(shelf.transform.position)} rotY={shelf.transform.eulerAngles.y:0} bounds min={V(sc.bounds.min)} max={V(sc.bounds.max)}");
            var standCols = new HashSet<Collider>(stand.GetComponentsInChildren<Collider>(true));

            // Niveis de prateleira: raycast para baixo
            var levels = new SortedDictionary<int, List<Vector3>>();
            for (float x = sc.bounds.min.x + 0.01f; x < sc.bounds.max.x; x += 0.02f)
                for (float z = sc.bounds.min.z + 0.01f; z < sc.bounds.max.z; z += 0.02f)
                {
                    foreach (var h in Physics.RaycastAll(new Vector3(x, 2.5f, z), Vector3.down, 2.5f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (h.collider != sc || h.normal.y < 0.9f) continue;
                        int key = Mathf.RoundToInt(h.point.y / 0.02f);
                        if (!levels.TryGetValue(key, out var l)) levels[key] = l = new List<Vector3>();
                        l.Add(h.point);
                    }
                }
            L("Niveis horizontais (superficies com normal para cima) no colisor da estante:");
            foreach (var kv in levels)
            {
                var pts = kv.Value; if (pts.Count < 20) continue;
                float y = pts.Average(p => p.y);
                L($"  y={y:0.000} amostras={pts.Count} x {pts.Min(p => p.x):0.000}..{pts.Max(p => p.x):0.000}  z {pts.Min(p => p.z):0.000}..{pts.Max(p => p.z):0.000}");
            }


            // Niveis a partir da malha visual (triangulos com normal para cima, em coordenadas de mundo)
            var meshLevels = new List<float>(); var mc = sc as MeshCollider;
            L($"MeshCollider convex={(mc != null && mc.convex)} sharedMesh={(mc != null && mc.sharedMesh != null ? mc.sharedMesh.name + " readable=" + mc.sharedMesh.isReadable : "null")}");
            var mf = shelf.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                var m = mf.sharedMesh; L($"MeshFilter mesh={m.name} readable={m.isReadable} verts={m.vertexCount} tris={m.triangles.Length / 3}");
                if (true)
                {
                    var vs = m.vertices; var downs = new SortedDictionary<int, float>(); var ts = m.triangles; var ups = new SortedDictionary<int, (float minx, float maxx, float minz, float maxz, float area)>();
                    for (int i = 0; i < ts.Length; i += 3)
                    {
                        Vector3 a = shelf.transform.TransformPoint(vs[ts[i]]), b2 = shelf.transform.TransformPoint(vs[ts[i + 1]]), c = shelf.transform.TransformPoint(vs[ts[i + 2]]);
                        Vector3 n = Vector3.Cross(b2 - a, c - a); float area = n.magnitude * 0.5f; if (area < 1e-6f) continue; n.Normalize();
                        if (n.y < -0.95f) { int kd = Mathf.RoundToInt(((a.y + b2.y + c.y) / 3f) / 0.01f); downs[kd] = 1f; continue; } if (n.y < 0.95f) continue;
                        int k = Mathf.RoundToInt(((a.y + b2.y + c.y) / 3f) / 0.01f);
                        float mnx = Mathf.Min(a.x, b2.x, c.x), mxx = Mathf.Max(a.x, b2.x, c.x), mnz = Mathf.Min(a.z, b2.z, c.z), mxz = Mathf.Max(a.z, b2.z, c.z);
                        if (ups.TryGetValue(k, out var e)) ups[k] = (Mathf.Min(e.minx, mnx), Mathf.Max(e.maxx, mxx), Mathf.Min(e.minz, mnz), Mathf.Max(e.maxz, mxz), e.area + area); else ups[k] = (mnx, mxx, mnz, mxz, area);
                    }
                    foreach (var kd in downs.Keys) L($"  face voltada p/ baixo (fundo de tabua) y={kd * 0.01f:0.00}"); foreach (var kv in ups) if (kv.Value.area > 0.02f) meshLevels.Add(kv.Key * 0.01f); foreach (var kv in ups) if (kv.Value.area > 0.02f) L($"  face voltada p/ cima y={kv.Key * 0.01f:0.00} area={kv.Value.area:0.000} x {kv.Value.minx:0.000}..{kv.Value.maxx:0.000} z {kv.Value.minz:0.000}..{kv.Value.maxz:0.000}");
                }
            }

            // Headroom acima de cada nivel: raycast para cima no centro
            L("Altura livre acima de cada nivel (raycast para cima no centro x, varios z):");
            foreach (var kv in levels)
            {
                var pts = kv.Value; if (pts.Count < 20) continue;
                float y = pts.Average(p => p.y);
                float cx = (pts.Min(p => p.x) + pts.Max(p => p.x)) * 0.5f;
                float minH = 99;
                for (float z = -0.5f; z <= 0.55f; z += 0.1f)
                    foreach (var h in Physics.RaycastAll(new Vector3(cx, y + 0.01f, z), Vector3.up, 1.0f, ~0, QueryTriggerInteraction.Ignore))
                        if (h.collider == sc) { minH = Mathf.Min(minH, h.distance + 0.01f); break; }
                L($"  nivel y={y:0.000}: folga ate o fundo da prateleira de cima = {(minH > 90 ? ">1.0" : minH.ToString("0.000"))} m");
            }

            // Objetos que ocupam a vizinhanca de cada nivel
            L("Objetos (renderers) sobre/ao lado da estante (x dentro do movel +-0.05) entre y=0.8 e 1.9:");
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r.transform.IsChildOf(shelf.transform) || r.transform.IsChildOf(stand.transform) || r is ParticleSystemRenderer) continue;
                var b = r.bounds;
                if (b.max.x < sc.bounds.min.x - 0.05f || b.min.x > sc.bounds.max.x + 0.05f) continue;
                if (b.max.z < sc.bounds.min.z - 0.05f || b.min.z > sc.bounds.max.z + 0.05f) continue;
                if (b.max.y < 0.8f || b.min.y > 1.9f) continue;
                if (b.size.x > 3 || b.size.z > 3 || b.size.y > 2.5f) continue;
                L($"  {r.transform.name} (pai {(r.transform.parent != null ? r.transform.parent.name : "-")}) x {b.min.x:0.000}..{b.max.x:0.000} y {b.min.y:0.000}..{b.max.y:0.000} z {b.min.z:0.000}..{b.max.z:0.000}");
            }

            L($"Estante atual: pos={V(stand.transform.position)} rotY={stand.transform.eulerAngles.y:0}");
            var allStand = stand.GetComponentsInChildren<Renderer>(true).Where(r => r.GetComponent("TMP_Text") == null).ToArray();
            Bounds sbn = allStand[0].bounds; foreach (var r in allStand) sbn.Encapsulate(r.bounds);
            L($"  AABB renderers (sem texto): x {sbn.min.x:0.000}..{sbn.max.x:0.000} y {sbn.min.y:0.000}..{sbn.max.y:0.000} z {sbn.min.z:0.000}..{sbn.max.z:0.000}");

            // Teste de encaixe por nivel (niveis da malha visual)
            L($"Colisor da estante sob o nivel 1.19? OverlapBox em (9.611,1.20,-0.05) {{0.2,0.01,0.2}}: {Physics.OverlapBox(new Vector3(9.611f, 1.20f, -0.05f), new Vector3(0.1f, 0.01f, 0.1f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore).Select(h => h.name).Distinct().Aggregate("", (a, b2) => a + b2 + ";")}");
            foreach (bool ignoreBooks in new[] { false, true })
            {
                L(ignoreBooks ? "--- ENCAIXE ignorando book_pile* (livros sao decoracao realocavel) ---" : "--- ENCAIXE com tudo como obstaculo ---");
                foreach (float lvY in meshLevels)
                {
                    float y = lvY + 0.005f; if (y < 0.7f || y > 1.8f) continue;
                    float x0 = 9.473f, x1 = 9.749f, z0 = -0.595f, z1 = 0.649f;
                    foreach (float w in new[] { 0.86f, 0.72f })
                    {
                        float best = -1; Vector3 bp = default; float sx = 0.22f, sz = w;
                        for (float x = x0 + sx / 2; x <= x1 - sx / 2 + 1e-4f; x += 0.02f)
                            for (float z = z0 + sz / 2; z <= z1 - sz / 2 + 1e-4f; z += 0.02f)
                            {
                                float clear = -1;
                                foreach (float inf in new[] { 0f, 0.04f, 0.08f, 0.12f, 0.16f, 0.2f, 0.25f })
                                {
                                    var half = new Vector3(sx / 2, 0.15f, sz / 2 + inf);
                                    var center = new Vector3(x, y + 0.02f + 0.15f, z);
                                    var hits = Physics.OverlapBox(center, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                                    bool blocked = hits.Any(h => !standCols.Contains(h) && h != sc && !(ignoreBooks && h.transform.name.StartsWith("book_pile")));
                                    if (blocked) break; clear = inf;
                                }
                                if (clear > best) { best = clear; bp = new Vector3(x, y, z); }
                            }
                        L($"  nivel y={y:0.000} peca {w:0.00}x0.22: folga lateral max (eixo Z) = {(best < 0 ? "NAO CABE" : best.ToString("0.00") + " m")}{(best >= 0 ? " em " + V(bp) : "")}");
                    }
                }
            }
        }
    }
}
