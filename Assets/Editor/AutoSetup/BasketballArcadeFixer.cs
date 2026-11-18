using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRRehab.Interactive.Task08;

namespace VRRehab.EditorTools
{
    /// <summary>
    /// Ferramentas do Mini Basquete Arcade (TASK-08): inspeção, correção dos sensores/placar no prefab e na cena,
    /// teste de pontuação por simulação de física e capturas offscreen do placar.
    /// </summary>
    public static partial class BasketballArcadeFixer
    {
        public const string MenuScene = "Assets/Scenes/Menu_Principal.unity";
        public const string HoopPrefabPath = "Assets/Prefabs/Interactive/ArcadeBasketballHoop.prefab";
        public const string BallPrefabPath = "Assets/Prefabs/Interactive/MiniBasketball.prefab";

        public static BasketballHoopScorer FindHoop(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                BasketballHoopScorer s = root.GetComponentInChildren<BasketballHoopScorer>(true);
                if (s != null) return s;
            }
            return null;
        }

        /// <summary>Relatório somente leitura do estado atual da cesta (prefab + instância da cena).</summary>
        public static string Inspect()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Inspeção da cesta ===");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HoopPrefabPath);
            sb.AppendLine($"Prefab: {(prefab != null ? "ok" : "NÃO ENCONTRADO")}");
            if (prefab != null) DumpHierarchy(prefab.transform, 0, sb);

            Scene scene = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
            BasketballHoopScorer hoop = FindHoop(scene);
            if (hoop == null) { sb.AppendLine("Cesta NÃO encontrada na cena do Menu"); return sb.ToString(); }

            Transform t = hoop.transform;
            sb.AppendLine($"Instância: {ForestAuditTool.PathOf(t)} pos {ForestAuditTool.V(t.position)} rot {t.eulerAngles} escala {t.lossyScale}");
            sb.AppendLine($"  é instância de prefab: {PrefabUtility.IsPartOfPrefabInstance(hoop.gameObject)} ({PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(hoop.gameObject)})");
            DumpHierarchy(t, 1, sb);

            MeshFilter mf = t.Find("Visual_Model") != null ? t.Find("Visual_Model").GetComponent<MeshFilter>() : null;
            if (mf != null && mf.sharedMesh != null)
            {
                Mesh m = mf.sharedMesh;
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                sb.AppendLine($"Malha {m.name}: bounds centro {ForestAuditTool.V(m.bounds.center)} tamanho {ForestAuditTool.V(m.bounds.size)} tris {m.triangles.Length / 3}; Visual_Model localPos {ForestAuditTool.V(mf.transform.localPosition)} rot {mf.transform.localEulerAngles} escala {mf.transform.localScale}");
                for (int i = 0; i < m.subMeshCount; i++)
                {
                    Bounds b = m.GetSubMesh(i).bounds;
                    string mat = mr != null && i < mr.sharedMaterials.Length && mr.sharedMaterials[i] != null ? mr.sharedMaterials[i].name : "?";
                    sb.AppendLine($"  submesh {i} [{mat}]: centro {ForestAuditTool.V(b.center)} tamanho {ForestAuditTool.V(b.size)}");
                }
            }

            foreach (TMP_Text txt in t.GetComponentsInChildren<TMP_Text>(true))
            {
                txt.ForceMeshUpdate();
                sb.AppendLine($"  TMP '{txt.name}': \"{txt.text}\" font {txt.fontSize} localPos {ForestAuditTool.V(txt.transform.localPosition)} rect {txt.rectTransform.sizeDelta} bounds {txt.bounds.size} cor {txt.color}");
            }

            foreach (BasketballBallAudio ball in Object.FindObjectsByType<BasketballBallAudio>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine($"Bola: {ForestAuditTool.PathOf(ball.transform)} pos {ForestAuditTool.V(ball.transform.position)}");
            foreach (ArcadeBallSpawner sp in Object.FindObjectsByType<ArcadeBallSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine($"Spawner: {ForestAuditTool.PathOf(sp.transform)} pos {ForestAuditTool.V(sp.transform.position)}");

            var missing = new List<string>();
            sb.AppendLine($"Missing Script na cena do Menu: {ForestAuditTool.CountMissingScripts(scene, missing)}");
            foreach (string m in missing) sb.AppendLine("  " + m);

            // Capturas do estado atual
            Vector3 board = t.position;
            Vector3 front = -t.forward;
            sb.AppendLine(ForestAuditTool.RenderTo(".agents/visual_qa/BASKET_before_front.png", board + front * 2.0f + Vector3.up * 0.05f, board + Vector3.up * 0.1f, 40f, 1600, 900));
            sb.AppendLine(ForestAuditTool.RenderTo(".agents/visual_qa/BASKET_before_scoreboard.png", board + front * 2.0f + Vector3.up * 0.35f, board + Vector3.up * 0.35f, 14f, 1600, 900));
            return sb.ToString();
        }

        private static void DumpHierarchy(Transform t, int depth, StringBuilder sb)
        {
            var comps = new List<string>();
            foreach (Component c in t.GetComponents<Component>())
            {
                if (c == null) { comps.Add("<MISSING SCRIPT>"); continue; }
                if (c is Transform) continue;
                string d = c.GetType().Name;
                if (c is BoxCollider bc) d += $"(c {ForestAuditTool.V(bc.center)} s {ForestAuditTool.V(bc.size)}{(bc.isTrigger ? " trig" : "")})";
                if (c is HoopSensorForwarder f) d += $"({f.sensorType}, scorer={(f.scorer != null)})";
                comps.Add(d);
            }
            sb.AppendLine($"{new string(' ', depth * 2 + 2)}{t.name} lp {ForestAuditTool.V(t.localPosition)} [{string.Join(", ", comps)}]");
            foreach (Transform ch in t) DumpHierarchy(ch, depth + 1, sb);
        }
    }

    /// <summary>Entradas de batchmode (Unity CLI com o editor fechado).</summary>
    public static class PlaytestBatch
    {
        /// <summary>Passo 1: inspeção da cesta + auditoria inicial da Floresta + inspeção do pássaro. Não altera nada.</summary>
        public static void InspectAll()
        {
            int code = 0;
            try
            {
                File.WriteAllText(".agents/visual_qa/BASKET_Inspect.txt", BasketballArcadeFixer.Inspect());
                ForestAuditTool.Audit(ForestAuditTool.ForestScene, ".agents/visual_qa/FOREST_Audit_Before.txt", out _);
                File.WriteAllText(".agents/visual_qa/FOREST_BirdInspect.txt", ForestBirdTools.InspectBird());
            }
            catch (System.Exception e) { Debug.LogException(e); code = 2; }
            EditorApplication.Exit(code);
        }
    }
}
