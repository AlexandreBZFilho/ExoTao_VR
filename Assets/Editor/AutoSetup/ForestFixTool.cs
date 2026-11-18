using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRRehab.Interactive.Task13;

namespace VRRehab.EditorTools
{
    /// <summary>
    /// Aterra os objetos das Tasks 11-15 da Floresta_e_Rio no chão REAL.
    /// Causa do "tudo flutuando": a cena tem 9 blocos de Terrain e os builders usaram Terrain.activeTerrain.SampleHeight,
    /// que consultou o bloco errado; os itens ficaram ~1,5 m acima do terreno onde o paciente anda.
    /// A correção desloca cada item pela diferença (altura real - altura do bloco errado) no seu XZ, preservando as
    /// alturas relativas planejadas (moedas na altura do peito, pássaro no poleiro, pinhão no pedestal).
    /// Menu: "ExoTao IC/Floresta/Aterrar Itens das Tasks 11-15".
    /// </summary>
    public static class ForestFixTool
    {
        private static readonly string[] GroundedTypes =
        {
            "AppleCollectionBasket", "ShakeableFruitTree", "PathLanternInteractable", "MusicalMushroom",
            "InteractiveCheckpointTotem", "SproutingPlantLifecycle"
        };

        [MenuItem("ExoTao IC/Floresta/Aterrar Itens das Tasks 11-15")]
        public static void ApplyFromMenu() => Debug.Log(Apply());

        /// <summary>Altura do terreno real (bloco que contém o ponto). NaN se o ponto estiver fora de todos os blocos.</summary>
        public static float RealGround(Vector3 p)
        {
            float best = float.NaN;
            foreach (Terrain t in Terrain.activeTerrains)
            {
                Vector3 tp = t.transform.position;
                Vector3 size = t.terrainData.size;
                if (p.x < tp.x || p.x > tp.x + size.x || p.z < tp.z || p.z > tp.z + size.z) continue;
                float h = t.SampleHeight(p) + tp.y;
                if (float.IsNaN(best) || h > best) best = h;
            }
            return best;
        }

        public static string Apply()
        {
            Scene scene = SceneManager.GetSceneByPath(ForestAuditTool.ForestScene);
            bool wasOpen = scene.IsValid() && scene.isLoaded;
            bool wasDirty = wasOpen && scene.isDirty;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(ForestAuditTool.ForestScene, OpenSceneMode.Single);

            var log = new StringBuilder();
            log.AppendLine($"=== Aterramento da Floresta ({System.DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");

            // ---- Unidades a mover: filhos diretos dos grupos [TASK-1x] (descendo por contêineres na origem) ----
            var units = new List<Transform>();
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name.StartsWith("[TASK-1")) CollectUnits(root.transform, units);
            log.AppendLine($"unidades das Tasks 11-15: {units.Count}");

            // ---- Qual bloco de terreno os builders consultaram? ----
            var refs = new List<Transform>();
            foreach (Transform u in units)
                if (HasType(u, "PathLanternInteractable") || HasType(u, "MusicalMushroom") || HasType(u, "InteractiveCheckpointTotem")) refs.Add(u);

            Terrain wrong = null;
            float bestErr = float.MaxValue;
            foreach (Terrain t in Terrain.activeTerrains)
            {
                float err = 0f;
                foreach (Transform r in refs) err += Mathf.Abs(t.SampleHeight(r.position) + t.transform.position.y - r.position.y);
                err = refs.Count > 0 ? err / refs.Count : float.MaxValue;
                log.AppendLine($"  bloco '{t.name}': erro médio {err:0.000} m contra a altura dos {refs.Count} itens de referência");
                if (err < bestErr) { bestErr = err; wrong = t; }
            }
            bool tileMethod = wrong != null && bestErr < 0.08f;
            log.AppendLine(tileMethod
                ? $"builders usaram o bloco '{wrong.name}' (erro {bestErr:0.000} m): deslocamento = altura real - altura desse bloco"
                : "nenhum bloco explica as alturas atuais: usando o deslocamento do item de referência mais próximo");

            float Delta(Vector3 p)
            {
                float real = RealGround(p);
                if (float.IsNaN(real)) return float.NaN;
                if (tileMethod) return real - (wrong.SampleHeight(p) + wrong.transform.position.y);
                Transform nearest = null;
                float bestD = float.MaxValue;
                foreach (Transform r in refs)
                {
                    float d = (new Vector2(r.position.x - p.x, r.position.z - p.z)).sqrMagnitude;
                    if (d < bestD) { bestD = d; nearest = r; }
                }
                return nearest != null ? RealGround(nearest.position) - nearest.position.y : 0f;
            }

            // ---- 1. Deslocamento vertical de cada unidade ----
            int moved = 0, skipped = 0;
            float minDelta = float.MaxValue, maxDelta = float.MinValue;
            foreach (Transform u in units)
            {
                float d = Delta(u.position);
                if (float.IsNaN(d)) { skipped++; log.AppendLine($"  ! fora do terreno, não movido: {ForestAuditTool.PathOf(u)}"); continue; }
                if (Mathf.Abs(d) < 0.02f) continue;
                Vector3 p = u.position;
                u.position = new Vector3(p.x, p.y + d, p.z);
                PrefabUtility.RecordPrefabInstancePropertyModifications(u);
                moved++;
                minDelta = Mathf.Min(minDelta, d);
                maxDelta = Mathf.Max(maxDelta, d);
            }
            log.AppendLine($"1) {moved} unidades deslocadas na vertical (de {minDelta:+0.00;-0.00} a {maxDelta:+0.00;-0.00} m); {skipped} fora do terreno");

            // ---- 2. Posições de mundo guardadas em scripts (poleiros do pássaro, trilha de moedas) ----
            int shiftedPoints = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    string field = mb is ReactiveForestBird ? "perchPositions" : mb.GetType().Name == "CoinTrailSpawner" ? "trailWaypoints" : null;
                    if (field == null) continue;
                    var so = new SerializedObject(mb);
                    SerializedProperty arr = so.FindProperty(field);
                    if (arr == null || !arr.isArray) continue;
                    for (int i = 0; i < arr.arraySize; i++)
                    {
                        SerializedProperty e = arr.GetArrayElementAtIndex(i);
                        Vector3 v = e.vector3Value;
                        float d = Delta(v);
                        if (float.IsNaN(d) || Mathf.Abs(d) < 0.02f) continue;
                        e.vector3Value = new Vector3(v.x, v.y + d, v.z);
                        shiftedPoints++;
                        log.AppendLine($"  {mb.GetType().Name}.{field}[{i}]: y {v.y:0.00} -> {v.y + d:0.00}");
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            log.AppendLine($"2) {shiftedPoints} posições de mundo em scripts ajustadas");

            // ---- 3. Encosto fino no chão dos itens que ficam apoiados no solo ----
            Physics.SyncTransforms();
            int snapped = 0;
            foreach (ForestAuditTool.Item it in ForestAuditTool.Collect(scene))
            {
                bool grounded = System.Array.IndexOf(GroundedTypes, it.type) >= 0;
                bool looseApple = it.type == "InteractiveApple" && it.go.name != "Apple_In_Basket";
                if (!grounded && !looseApple) continue;

                float ground = RealGround(it.pos);
                if (float.IsNaN(ground)) continue;
                Bounds b = ForestAuditTool.RendererBounds(it.go, out bool has);
                if (!has) continue;

                // Árvores e lanternas entram 3 cm no solo (sem fresta em terreno inclinado); maçãs ficam 1 cm acima
                float target = looseApple ? ground + 0.01f : ground - (it.type == "ShakeableFruitTree" || it.type == "PathLanternInteractable" ? 0.03f : 0.01f);
                float dy = target - b.min.y;
                if (Mathf.Abs(dy) < 0.03f) continue;
                it.go.transform.position += Vector3.up * dy;
                PrefabUtility.RecordPrefabInstancePropertyModifications(it.go.transform);
                snapped++;
                log.AppendLine($"  encostado no chão: {it.path} ({dy:+0.00;-0.00} m)");
            }
            log.AppendLine($"3) {snapped} itens encostados no chão");

            // ---- 4. Estações de cogumelos musicais (uma existente + cópias ao longo da trilha) ----
            log.Append(EnsureMushroomStations(scene, units));

            // ---- 5. Pássaro e luzes ----
            log.Append(ForestBirdTools.ApplySceneFix());
            int lights = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name.StartsWith("[TASK-1"))
                    foreach (Light l in root.GetComponentsInChildren<Light>(true))
                        if (l.shadows != LightShadows.None) { l.shadows = LightShadows.None; lights++; }
            log.AppendLine($"5) {lights} luzes de gameplay tiveram a sombra desligada");

            EditorSceneManager.MarkSceneDirty(scene);
            if (!wasOpen || !wasDirty) { EditorSceneManager.SaveScene(scene); log.AppendLine("cena Floresta_e_Rio salva"); }
            else log.AppendLine("cena aberta com alterações suas pendentes: SALVE com Ctrl+S");

            string report = log.ToString();
            Directory.CreateDirectory(".agents/visual_qa");
            File.WriteAllText(".agents/visual_qa/FOREST_Fix.txt", report);
            return report;
        }

        private static void CollectUnits(Transform t, List<Transform> units)
        {
            bool container = t.position.sqrMagnitude < 0.01f && t.childCount > 0 && t.GetComponent<Renderer>() == null;
            if (!container) { units.Add(t); return; }
            foreach (Transform ch in t) CollectUnits(ch, units);
        }

        private static bool HasType(Transform t, string typeName)
        {
            foreach (MonoBehaviour mb in t.GetComponents<MonoBehaviour>())
                if (mb != null && mb.GetType().Name == typeName) return true;
            return false;
        }

        /// <summary>Garante 3 estações de 5 cogumelos musicais bem espaçadas ao longo da trilha (a original + 2 cópias).</summary>
        private static string EnsureMushroomStations(Scene scene, List<Transform> units)
        {
            var log = new StringBuilder();
            Transform source = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == "XylophoneMushrooms_Glade") source = t;
            if (source == null) { log.AppendLine("4) estação de cogumelos original não encontrada"); return log.ToString(); }

            var srcMushrooms = source.GetComponentsInChildren<MusicalMushroom>(true);
            Vector3 centroid = Vector3.zero;
            foreach (MusicalMushroom m in srcMushrooms) centroid += m.transform.position;
            centroid /= Mathf.Max(1, srcMushrooms.Length);
            float srcGround = RealGround(centroid);

            // Pontos ocupados: todas as unidades das tasks (inclui moedas = eixo da trilha, que deve ficar livre)
            var occupied = new List<Vector3>();
            foreach (Transform u in units) if (u != null && !u.IsChildOf(source)) occupied.Add(u.position);
            foreach (MusicalMushroom m in srcMushrooms) occupied.Add(m.transform.position);

            Vector2[] candidates =
            {
                new Vector2(42.0f, 62.8f), new Vector2(39.5f, 68.6f), new Vector2(46.0f, 61.4f),
                new Vector2(41.0f, 68.6f), new Vector2(48.6f, 67.2f), new Vector2(38.6f, 62.6f)
            };

            int stations = 1;
            for (int n = 2; n <= 3; n++)
            {
                string name = $"XylophoneMushrooms_Glade_{n}";
                Transform existing = source.parent != null ? source.parent.Find(name) : null;
                if (existing != null) { stations++; foreach (MusicalMushroom m in existing.GetComponentsInChildren<MusicalMushroom>(true)) occupied.Add(m.transform.position); continue; }

                foreach (Vector2 c in candidates)
                {
                    Vector3 offset = new Vector3(c.x - centroid.x, 0f, c.y - centroid.z);
                    bool ok = true;
                    foreach (MusicalMushroom m in srcMushrooms)
                    {
                        Vector3 p = m.transform.position + offset;
                        float g = RealGround(p);
                        if (float.IsNaN(g) || Mathf.Abs(g - srcGround) > 0.25f) { ok = false; break; }
                        foreach (Vector3 o in occupied)
                            if ((new Vector2(o.x - p.x, o.z - p.z)).sqrMagnitude < 1.3f * 1.3f) { ok = false; break; }
                        if (!ok) break;
                    }
                    if (!ok) continue;

                    GameObject copy = Object.Instantiate(source.gameObject, source.parent);
                    copy.name = name;
                    copy.transform.position = source.position + offset;
                    foreach (MusicalMushroom m in copy.GetComponentsInChildren<MusicalMushroom>(true))
                    {
                        Bounds b = ForestAuditTool.RendererBounds(m.gameObject, out bool has);
                        float g = RealGround(m.transform.position);
                        if (has && !float.IsNaN(g)) m.transform.position += Vector3.up * (g - 0.01f - b.min.y);
                        occupied.Add(m.transform.position);
                    }
                    stations++;
                    log.AppendLine($"  estação {n} criada em ({c.x:0.0}, {c.y:0.0}), {copy.GetComponentsInChildren<MusicalMushroom>(true).Length} cogumelos (cópia da original, mesmos sons)");
                    break;
                }
            }
            log.AppendLine($"4) estações de cogumelos musicais: {stations}");
            return log.ToString();
        }

        /// <summary>Capturas offscreen da trilha depois da correção.</summary>
        public static string CaptureTrail()
        {
            var sb = new StringBuilder();
            Scene scene = SceneManager.GetSceneByPath(ForestAuditTool.ForestScene);
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(ForestAuditTool.ForestScene, OpenSceneMode.Single);

            float g(Vector3 p) { float h = RealGround(p); return float.IsNaN(h) ? 3f : h; }
            Vector3 eye = new Vector3(58.0f, 0f, 60.7f); eye.y = g(eye) + 1.6f;
            Vector3 look = new Vector3(48.0f, 0f, 64.0f); look.y = g(look) + 0.8f;
            sb.AppendLine(ForestAuditTool.RenderTo(".agents/visual_qa/FOREST_view1_front.png", eye, look, 62f));

            Vector3 low = new Vector3(55.6f, 0f, 60.6f); low.y = g(low) + 0.28f;
            Vector3 lowLook = new Vector3(52.6f, 0f, 64.6f); lowLook.y = g(lowLook) + 0.35f;
            sb.AppendLine(ForestAuditTool.RenderTo(".agents/visual_qa/FOREST_view2_side_grounding.png", low, lowLook, 55f));

            MusicalMushroom[] mush = Object.FindObjectsByType<MusicalMushroom>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (mush.Length > 0)
            {
                Transform glade = mush[0].transform.parent;
                Vector3 c = Vector3.zero; int n = 0;
                foreach (MusicalMushroom m in mush) if (m.transform.parent == glade) { c += m.transform.position; n++; }
                c /= Mathf.Max(1, n);
                sb.AppendLine(ForestAuditTool.RenderTo(".agents/visual_qa/FOREST_view3_closeup_mushrooms.png", c + new Vector3(2.2f, 0.9f, -1.6f), c + Vector3.up * 0.2f, 50f));
            }

            ReactiveForestBird bird = ForestBirdTools.FindBird();
            if (bird != null)
            {
                Transform t = bird.transform;
                Vector3 p = t.position;
                sb.AppendLine(ForestAuditTool.RenderTo(".agents/visual_qa/FOREST_bird_legs_front.png", p + t.forward * 0.42f + t.right * 0.16f + Vector3.up * 0.06f, p + Vector3.up * 0.075f, 32f));
                sb.AppendLine(ForestAuditTool.RenderTo(".agents/visual_qa/FOREST_bird_legs_side.png", p + t.right * 0.45f + Vector3.up * 0.03f, p + Vector3.up * 0.07f, 32f));
            }
            return sb.ToString();
        }
    }

    public static partial class PlaytestBatchApply
    {
        /// <summary>Passo 2 (batchmode): aplica cesta + pássaro + Floresta, testa e captura. Cada etapa é independente.</summary>
        public static void ApplyAll()
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
            Step("pássaro: prefab", () => File.WriteAllText(".agents/visual_qa/FOREST_BirdFix.txt", ForestBirdTools.ApplyPrefabFix()));
            Step("floresta: aterrar", () => ForestFixTool.Apply());
            Step("floresta: auditoria depois", () => ForestAuditTool.Audit(ForestAuditTool.ForestScene, ".agents/visual_qa/FOREST_Audit_After.txt", out _));
            Step("floresta: capturas", () => File.WriteAllText(".agents/visual_qa/FOREST_Captures.txt", ForestFixTool.CaptureTrail()));
            Step("pássaro: teste de áudio", () => File.WriteAllText(".agents/visual_qa/FOREST_BirdAudioTest.txt", ForestBirdTools.TestAudio()));
            EditorApplication.Exit(failures);
        }
    }
}
