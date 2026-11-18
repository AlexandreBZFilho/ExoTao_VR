using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace VRRehab.EditorTools
{
    /// <summary>
    /// Auditoria SOMENTE LEITURA de triângulos dos modelos gerados (Assets/Models/Generated/Task*) contra o orçamento VR
    /// do plano: itens de mão 300-600 tris, itens médios/estáticos 800-1800 tris.
    /// Também conta MeshCollider em prefabs com Rigidbody (proibido em objetos móveis).
    /// Batch: unity run . -- -executeMethod VRRehab.EditorTools.ModelPolycountAudit.RunBatch
    /// </summary>
    public static class ModelPolycountAudit
    {
        private const string Report = ".agents/visual_qa/MODELS_PolycountAudit.txt";

        // Itens pequenos de mão/coleta (orçamento 600). O resto é "médio/estático" (1800).
        private static readonly string[] SmallKeywords =
        {
            "apple", "cassette", "tape", "coin", "pinecone", "seed", "ball", "balloon", "reticle", "orb", "mushroomcap"
        };

        // Malhas de cenário grandes que não são "itens" (fora do orçamento por item)
        private static readonly string[] SceneryKeywords = { "terrain", "skydome", "diorama" };

        [MenuItem("ExoTao IC/Auditoria/Polígonos dos modelos gerados")]
        public static void RunFromMenu() => Debug.Log(Run());

        public static void RunBatch()
        {
            int code = 1;
            try { Debug.Log(Run()); code = 0; }
            catch (System.Exception e) { Debug.LogException(e); }
            EditorApplication.Exit(code);
        }

        public static string Run()
        {
            var rows = new List<(string path, string name, int tris, int budget, string kind)>();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Models/Generated" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                int tris = 0;
                foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
                foreach (SkinnedMeshRenderer sm in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (sm.sharedMesh != null) tris += sm.sharedMesh.triangles.Length / 3;

                string lower = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                string kind; int budget;
                if (SceneryKeywords.Any(k => lower.Contains(k))) { kind = "cenário"; budget = 6000; }
                else if (lower.StartsWith("trophy_")) { kind = "troféu"; budget = 800; }               // TASK-16: < 800 cada
                else if (lower.Contains("hoop") || lower.Contains("gun") || lower.Contains("cluster")) { kind = "médio"; budget = 1800; }
                else if (SmallKeywords.Any(k => lower.Contains(k))) { kind = "mão"; budget = 600; }
                else { kind = "médio"; budget = 1800; }
                if (lower.Contains("basketball") && !lower.Contains("hoop")) { kind = "mão (bola com costuras em geometria)"; budget = 1100; }
                rows.Add((path, Path.GetFileName(path), tris, budget, kind));
            }

            // MeshCollider em prefabs móveis
            var meshColliderOnRb = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                foreach (MeshCollider mc in go.GetComponentsInChildren<MeshCollider>(true))
                {
                    Rigidbody rb = mc.attachedRigidbody != null ? mc.attachedRigidbody : mc.GetComponentInParent<Rigidbody>();
                    if (rb != null && !rb.isKinematic) meshColliderOnRb.Add($"{path} -> {mc.name}");
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"=== Auditoria de polígonos dos modelos gerados ({System.DateTime.Now:yyyy-MM-dd HH:mm}) ===");
            sb.AppendLine("Orçamento VR do plano: itens de mão <= 600 tris, itens médios/estáticos <= 1800 tris.");
            var over = rows.Where(r => r.tris > r.budget).OrderByDescending(r => r.tris - r.budget).ToList();
            sb.AppendLine($"Modelos: {rows.Count} | acima do orçamento: {over.Count} | triângulos totais: {rows.Sum(r => r.tris)}");
            sb.AppendLine();
            sb.AppendLine("ACIMA DO ORÇAMENTO:");
            foreach (var r in over) sb.AppendLine($"  {r.tris,6} tris (limite {r.budget}, {r.kind})  {r.path}");
            sb.AppendLine();
            sb.AppendLine($"MeshCollider em prefab com Rigidbody dinâmico (proibido): {meshColliderOnRb.Count}");
            foreach (string s in meshColliderOnRb) sb.AppendLine("  " + s);
            sb.AppendLine();
            sb.AppendLine("Todos (por pasta):");
            foreach (var r in rows.OrderBy(r => r.path))
                sb.AppendLine($"  {(r.tris > r.budget ? "!!" : "ok")} {r.tris,6} tris  [{r.kind}]  {r.path}");

            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, sb.ToString());
            return sb.ToString();
        }
    }
}
