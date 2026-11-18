using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRRehab.Interactive.Task13;
using VRRehab.Interactive.Task14;

namespace VRRehab.EditorTools
{
    /// <summary>
    /// Acabamento da Floresta_e_Rio depois do aterramento:
    /// - moedas da trilha numa altura uniforme acima do chão (o aterramento por bloco de terreno deixou algumas
    ///   rente ao chão e uma moeda bônus enterrada);
    /// - limpa o capim alto do terreno em volta das estações de cogumelos musicais (estavam escondidas no capim);
    /// - capturas offscreen: trilha, estações de cogumelos e o pássaro isolado (pernas).
    /// Menu: "ExoTao IC/Floresta/Acabamento (moedas, capim dos cogumelos)".
    /// </summary>
    public static class ForestFinishTool
    {
        private const float CoinGap = 1.03f;       // altura da base da moeda acima do chão (peito do paciente sentado no exo)
        private const float RiverCoinGap = 0.6f;   // moeda bônus do rio: visível acima da margem
        private const float GrassClearMargin = 0.9f;

        [MenuItem("ExoTao IC/Floresta/Acabamento (moedas, capim dos cogumelos)")]
        public static void ApplyFromMenu() => Debug.Log(Apply());

        public static string Apply()
        {
            Scene scene = SceneManager.GetSceneByPath(ForestAuditTool.ForestScene);
            bool wasOpen = scene.IsValid() && scene.isLoaded;
            bool wasDirty = wasOpen && scene.isDirty;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(ForestAuditTool.ForestScene, OpenSceneMode.Single);

            var log = new StringBuilder();
            log.AppendLine($"=== Acabamento da Floresta ({System.DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");
            log.Append(NormalizeCoins());
            log.Append(ClearGrassAroundMushrooms());

            EditorSceneManager.MarkSceneDirty(scene);
            if (!wasOpen || !wasDirty) { EditorSceneManager.SaveScene(scene); log.AppendLine("cena Floresta_e_Rio salva"); }
            else log.AppendLine("cena aberta com alterações suas pendentes: SALVE com Ctrl+S");
            AssetDatabase.SaveAssets();

            string report = log.ToString();
            File.WriteAllText(".agents/visual_qa/FOREST_Finish.txt", report);
            return report;
        }

        private static string NormalizeCoins()
        {
            Physics.SyncTransforms();
            var log = new StringBuilder();
            int moved = 0;
            var byIndex = new Dictionary<int, float>();
            foreach (CollectibleOrbCoin coin in Object.FindObjectsByType<CollectibleOrbCoin>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string n = coin.name;
                float gap;
                if (n.StartsWith("EnergyStarCoin_")) gap = CoinGap;
                else if (n.Contains("River")) gap = RiverCoinGap;
                else continue; // moedas bônus da pedra e da árvore ficam onde o builder escondeu

                // Chão onde o paciente anda: terreno OU superfícies sólidas como a ponte sobre o rio
                if (!ForestAuditTool.TryGroundHeight(coin.gameObject, coin.transform.position, out float ground, out _)) continue;
                Bounds b = ForestAuditTool.RendererBounds(coin.gameObject, out bool has);
                float baseY = has ? b.min.y : coin.transform.position.y;
                float dy = ground + gap - baseY;
                if (Mathf.Abs(dy) > 0.02f)
                {
                    coin.transform.position += Vector3.up * dy;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(coin.transform);
                    moved++;
                    log.AppendLine($"  {n}: {dy:+0.00;-0.00} m (base agora {gap:0.00} m acima do chão)");
                }
                if (n.StartsWith("EnergyStarCoin_") && int.TryParse(n.Substring("EnergyStarCoin_".Length), out int idx))
                    byIndex[idx - 1] = coin.transform.position.y;
            }

            // A trilha guardada no spawner acompanha as moedas (moeda N = ponto N-1)
            CoinTrailSpawner spawner = Object.FindFirstObjectByType<CoinTrailSpawner>(FindObjectsInactive.Include);
            if (spawner != null)
            {
                var so = new SerializedObject(spawner);
                SerializedProperty arr = so.FindProperty("trailWaypoints");
                int synced = 0;
                for (int i = 0; arr != null && i < arr.arraySize; i++)
                {
                    if (!byIndex.TryGetValue(i, out float y)) continue;
                    SerializedProperty e = arr.GetArrayElementAtIndex(i);
                    Vector3 v = e.vector3Value;
                    if (Mathf.Abs(v.y - y) > 0.01f) { e.vector3Value = new Vector3(v.x, y, v.z); synced++; }
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                log.AppendLine($"  trilha do CoinTrailSpawner: {synced} pontos sincronizados com as moedas");
            }
            log.AppendLine($"moedas ajustadas: {moved}");
            return log.ToString();
        }

        /// <summary>Remove os detalhes do terreno (capim) num círculo em volta de cada estação de cogumelos.</summary>
        private static string ClearGrassAroundMushrooms()
        {
            var log = new StringBuilder();
            var stations = new Dictionary<Transform, List<Vector3>>();
            foreach (MusicalMushroom m in Object.FindObjectsByType<MusicalMushroom>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Transform glade = m.transform.parent != null ? m.transform.parent : m.transform;
                if (!stations.TryGetValue(glade, out var list)) stations[glade] = list = new List<Vector3>();
                list.Add(m.transform.position);
            }

            var usage = new Dictionary<TerrainData, int>();
            foreach (Terrain t in Terrain.activeTerrains)
                if (t.terrainData != null) usage[t.terrainData] = usage.TryGetValue(t.terrainData, out int c) ? c + 1 : 1;

            int totalCleared = 0;
            foreach (var kv in stations)
            {
                Vector3 c = Vector3.zero;
                foreach (Vector3 p in kv.Value) c += p;
                c /= kv.Value.Count;
                float r = 0f;
                foreach (Vector3 p in kv.Value) r = Mathf.Max(r, new Vector2(p.x - c.x, p.z - c.z).magnitude);
                r += GrassClearMargin;

                int cleared = 0;
                foreach (Terrain t in Terrain.activeTerrains)
                {
                    TerrainData td = t.terrainData;
                    if (td == null || td.detailPrototypes.Length == 0) continue;
                    if (usage[td] > 1) { log.AppendLine($"  ! {t.name}: TerrainData compartilhado por {usage[td]} blocos, capim NÃO alterado"); continue; }

                    Vector3 o = t.transform.position, size = td.size;
                    int res = td.detailResolution;
                    float cellX = size.x / res, cellZ = size.z / res;
                    int x0 = Mathf.Clamp(Mathf.FloorToInt((c.x - r - o.x) / cellX), 0, res - 1);
                    int x1 = Mathf.Clamp(Mathf.CeilToInt((c.x + r - o.x) / cellX), 0, res - 1);
                    int z0 = Mathf.Clamp(Mathf.FloorToInt((c.z - r - o.z) / cellZ), 0, res - 1);
                    int z1 = Mathf.Clamp(Mathf.CeilToInt((c.z + r - o.z) / cellZ), 0, res - 1);
                    if (c.x + r < o.x || c.x - r > o.x + size.x || c.z + r < o.z || c.z - r > o.z + size.z) continue;
                    int w = x1 - x0 + 1, h = z1 - z0 + 1;
                    if (w <= 0 || h <= 0) continue;

                    Undo.RegisterCompleteObjectUndo(td, "Limpar capim dos cogumelos");
                    for (int layer = 0; layer < td.detailPrototypes.Length; layer++)
                    {
                        int[,] map = td.GetDetailLayer(x0, z0, w, h, layer); // [z, x]
                        bool changed = false;
                        for (int zi = 0; zi < h; zi++)
                            for (int xi = 0; xi < w; xi++)
                            {
                                if (map[zi, xi] == 0) continue;
                                float wx = o.x + (x0 + xi + 0.5f) * cellX, wz = o.z + (z0 + zi + 0.5f) * cellZ;
                                if ((wx - c.x) * (wx - c.x) + (wz - c.z) * (wz - c.z) > r * r) continue;
                                map[zi, xi] = 0;
                                cleared++;
                                changed = true;
                            }
                        if (changed) td.SetDetailLayer(x0, z0, layer, map);
                    }
                    EditorUtility.SetDirty(td);
                }
                totalCleared += cleared;
                log.AppendLine($"  estação '{kv.Key.name}' em {ForestAuditTool.V(c)}: raio {r:0.0} m, {cleared} células de capim removidas");
            }
            log.AppendLine($"capim removido em volta de {stations.Count} estações de cogumelos ({totalCleared} células)");
            return log.ToString();
        }

        public static string Capture()
        {
            var sb = new StringBuilder();
            Scene scene = SceneManager.GetSceneByPath(ForestAuditTool.ForestScene);
            if (!scene.IsValid() || !scene.isLoaded) EditorSceneManager.OpenScene(ForestAuditTool.ForestScene, OpenSceneMode.Single);

            sb.Append(ForestFixTool.CaptureTrail()); // trilha (frente e lateral rasante) + capturas antigas

            // Uma captura por estação de cogumelos, de cima e de lado
            var glades = new Dictionary<Transform, List<Vector3>>();
            foreach (MusicalMushroom m in Object.FindObjectsByType<MusicalMushroom>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                Transform g = m.transform.parent != null ? m.transform.parent : m.transform;
                if (!glades.TryGetValue(g, out var l)) glades[g] = l = new List<Vector3>();
                l.Add(m.transform.position);
            }
            int k = 1;
            foreach (var kv in glades)
            {
                Vector3 c = Vector3.zero;
                foreach (Vector3 p in kv.Value) c += p;
                c /= kv.Value.Count;
                // Câmera do lado da trilha (aberta), mirando a estação: a trilha é onde estão as moedas
                Vector3 toTrail = Vector3.forward;
                float best = float.MaxValue;
                foreach (CollectibleOrbCoin coin in Object.FindObjectsByType<CollectibleOrbCoin>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (!coin.name.StartsWith("EnergyStarCoin_")) continue;
                    Vector3 d = coin.transform.position - c; d.y = 0f;
                    if (d.sqrMagnitude < best && d.sqrMagnitude > 0.25f) { best = d.sqrMagnitude; toTrail = d.normalized; }
                }
                Vector3 cam = c + toTrail * 1.5f;
                cam.y = (float.IsNaN(ForestFixTool.RealGround(cam)) ? c.y : ForestFixTool.RealGround(cam)) + 0.75f;
                sb.AppendLine(ForestAuditTool.RenderTo($".agents/visual_qa/FOREST_view3_closeup_mushrooms_{k}.png", cam, c + Vector3.up * 0.12f, 55f));
                k++;
            }
            if (File.Exists(".agents/visual_qa/FOREST_view3_closeup_mushrooms.png")) File.Delete(".agents/visual_qa/FOREST_view3_closeup_mushrooms.png");

            // Pássaro isolado numa camada temporária (a folhagem da cena cobria a câmera)
            ReactiveForestBird bird = ForestBirdTools.FindBird();
            if (bird != null)
            {
                const int layer = 31;
                var saved = new List<(GameObject go, int layer)>();
                foreach (Transform t in bird.GetComponentsInChildren<Transform>(true)) { saved.Add((t.gameObject, t.gameObject.layer)); t.gameObject.layer = layer; }
                try
                {
                    Transform bt = bird.transform;
                    Vector3 p = bt.position;
                    Color bg = new Color(0.55f, 0.72f, 0.86f);
                    sb.AppendLine(ForestAuditTool.RenderTo(".agents/visual_qa/FOREST_bird_legs_front.png", p + bt.forward * 0.42f + bt.right * 0.18f + Vector3.up * 0.07f, p + Vector3.up * 0.085f, 30f, 1600, 900, 1 << layer, bg));
                    sb.AppendLine(ForestAuditTool.RenderTo(".agents/visual_qa/FOREST_bird_legs_side.png", p + bt.right * 0.45f + Vector3.up * 0.04f, p + Vector3.up * 0.08f, 30f, 1600, 900, 1 << layer, bg));
                }
                finally { foreach (var s in saved) s.go.layer = s.layer; }
            }
            return sb.ToString();
        }
    }

    public static partial class PlaytestBatchApply
    {
        /// <summary>Só as capturas da Floresta (batchmode).</summary>
        public static void ForestCapturesOnly()
        {
            int code = 0;
            try { File.WriteAllText(".agents/visual_qa/FOREST_Captures.txt", ForestFinishTool.Capture()); }
            catch (System.Exception e) { Debug.LogException(e); code = 2; }
            EditorApplication.Exit(code);
        }

        /// <summary>Só o teste de pontuação da cesta (batchmode).</summary>
        public static void BasketTestOnly()
        {
            int code = 0;
            try { BasketballArcadeFixer.TestScoring(); }
            catch (System.Exception e) { Debug.LogException(e); code = 2; }
            EditorApplication.Exit(code);
        }

        /// <summary>Passo 3 (batchmode): reteste da cesta, acabamento da Floresta, auditoria final, capturas e teste do pássaro.</summary>
        public static void FinishAll()
        {
            int failures = 0;
            void Step(string name, System.Action a)
            {
                try { a(); Debug.Log($"[PlaytestBatch] {name}: ok"); }
                catch (System.Exception e) { failures++; Debug.LogError($"[PlaytestBatch] {name}: FALHOU"); Debug.LogException(e); File.AppendAllText(".agents/visual_qa/PLAYTEST_BatchErrors.txt", $"{name}: {e}\n\n"); }
            }

            if (File.Exists(".agents/visual_qa/PLAYTEST_BatchErrors.txt")) File.Delete(".agents/visual_qa/PLAYTEST_BatchErrors.txt");
            Step("cesta: aplicar", () => BasketballArcadeFixer.Apply());
            Step("cesta: testar", () => BasketballArcadeFixer.TestScoring());
            Step("floresta: acabamento", () => ForestFinishTool.Apply());
            Step("floresta: auditoria depois", () => ForestAuditTool.Audit(ForestAuditTool.ForestScene, ".agents/visual_qa/FOREST_Audit_After.txt", out _));
            Step("floresta: capturas", () => File.WriteAllText(".agents/visual_qa/FOREST_Captures.txt", ForestFinishTool.Capture()));
            Step("pássaro: teste de áudio", () => File.WriteAllText(".agents/visual_qa/FOREST_BirdAudioTest.txt", ForestBirdTools.TestAudio()));
            EditorApplication.Exit(failures);
        }
    }
}
