using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using VRRehab.EditorTools;
using VRRehab.Interactive.Task06;

namespace VRRehab.EditorAutoSetup
{
    /// <summary>
    /// TASK-06.D (re-run após FAIL do auditor [E]):
    ///  1. Expurgo (0,0,0)/duplicatas ANTES (SceneDuplicateAndOriginPurger.PurgeMenuScene + varredura r = 1.0 m).
    ///  2. Esvazia a Mesa 1 (full_desk_3 (1)) de tudo o que não é TASK-06, realocando cada item para outro móvel existente
    ///     (busca em grade + validação por raycast do tampo, altura livre e folga AABB).
    ///  3. Reposiciona o Deck (mesma rotação do rádio) fora do volume do rádio e o Rack com folga ≥ 0.10 m; fitas seguem slots.
    ///  4. Expurgo DEPOIS + auditoria com pairwiseClearances de todos os objetos sobre a Mesa 1.
    ///  5. Capturas offscreen 1920x1080 (Camera.Render → RenderTexture), incluindo a sequência de Eject simulada com
    ///     Rigidbody real (Physics.Simulate em modo Script) e o impulso serializado no socket.
    /// Gatilho: arquivo ".run_task06_relayout" na raiz, ou menu VRRehab/AutoBuild/TASK-06 Relayout Mesa 1.
    /// Nunca usa automação de desktop.
    /// </summary>
    [InitializeOnLoad]
    public static class Task06_RelayoutPass
    {
        private const string TriggerFile = ".run_task06_relayout";
        private const string MenuScenePath = "Assets/Scenes/Menu_Principal.unity";
        private const string OutDir = ".agents/visual_qa";
        private const string ResultFile = ".agents/visual_qa/TASK-06_BuilderResult.txt";
        private const string Mesa1Path = "Building/full_desk_3 (1)";

        private const float RequiredClearance = 0.08f;   // critério do auditor
        private const float PlacementClearance = 0.10f;  // margem de construção
        private const float ItemSpacingSameFurniture = 0.25f; // Lei 2
        private const float DecorClearance = 0.06f;
        private const float EdgeMargin = 0.02f;

        private static bool running;

        static Task06_RelayoutPass()
        {
            EditorApplication.update += () =>
            {
                if (running || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
                string p = Path.Combine(Directory.GetCurrentDirectory(), TriggerFile);
                if (!File.Exists(p)) return;
                try { File.Delete(p); } catch { }
                Run();
            };
        }

        // ---------------------------------------------------------------------------------------------
        // Modelo de dados
        // ---------------------------------------------------------------------------------------------
        private class MoveSpec
        {
            public string rootName;
            public string task;
            public string[] furnitureCandidates;
            public string[] followers = new string[0];       // roots que acompanham (offset preservado)
            public string groupPartner;                       // item do mesmo grupo (mesmo móvel, espaçamento extra)
            public float partnerMinGap;
            public bool fixedYaw;                             // usa yawOverride (relativo à frente do móvel)
            public float yawRelativeToFacing;                 // rotY final = facing + este valor
            public float[] shelfRayStartY;                    // para estantes: alturas de partida do raio
        }

        private static string V(Vector3 v) => $"({v.x:F3}, {v.y:F3}, {v.z:F3})";

        private static Transform FindPath(string path)
        {
            var go = GameObject.Find(path);
            return go != null ? go.transform : null;
        }

        private static GameObject FindRoot(string name)
        {
            if (name.Contains("/")) return GameObject.Find(name);
            foreach (var r in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (r.name == name) return r;
            return null;
        }

        /// <summary>Posições originais sobre a Mesa 1 registradas pelo scout (antes de qualquer realocação desta rodada).</summary>
        private static readonly Dictionary<string, Vector3> OriginalOnMesa1 = new Dictionary<string, Vector3>
        {
            { "TrophyDisplayStand_Instance", new Vector3(0.680f, 0.952f, -0.380f) },
            { "EnvironmentControlLever_Instance", new Vector3(0.660f, 0.955f, 0.620f) },
            { "FireworksLaunchButton_Instance", new Vector3(0.790f, 0.955f, 0.600f) },
            { "ExoMascotBobblehead_Instance", new Vector3(0.730f, 0.955f, 0.720f) },
            { "BalloonGunDispenser_Instance", new Vector3(0.780f, 0.976f, 0.480f) },
            { "ArcadeBallSpawner_Instance", new Vector3(0.800f, 0.955f, 0.150f) },
            { "Demo_Basketball_Table", new Vector3(0.640f, 1.045f, -0.020f) },
            { "[IC] Ergonomic_Toy_Demo", new Vector3(0.580f, 1.025f, -0.280f) },
            { "Building/full_desk_3 (1)/Table Lamp", new Vector3(0.747f, 0.950f, -0.557f) },
            { "Building/full_desk_3 (1)/Deco Variant 3", new Vector3(0.560f, 0.954f, 0.600f) },
        };

        private static bool RendererBounds(GameObject go, out Bounds b) => SpatialAuditUtility.TryGetSolidBounds(go, out b, false, IsLabel);

        private static bool IsLabel(Transform t) => t.name.StartsWith("Label_", StringComparison.Ordinal);

        private static bool SolidBounds(GameObject go, out Bounds b) => SpatialAuditUtility.TryGetSolidBounds(go, out b, true, IsLabel);

        // ---------------------------------------------------------------------------------------------
        [MenuItem("VRRehab/AutoBuild/TASK-06 Relayout Mesa 1 + Audit + Captures")]
        public static void RunMenu() => Run();

        public static string Run()
        {
            if (running) return "running";
            running = true;
            var sb = new StringBuilder();
            sb.AppendLine("=== TASK-06 [D] RE-RUN — RELAYOUT DA MESA 1, REALOCAÇÃO, EXPURGO, AUDITORIA E CAPTURAS ===");
            sb.AppendLine($"Data/Hora local: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | Execução: script de Editor na instância aberta (gatilho por arquivo), capturas offscreen via Camera.Render.");
            var report = new ExtendedSpatialAuditReport
            {
                taskId = "TASK-06",
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                requiredClearanceMeters = RequiredClearance,
                originSweepRadiusMeters = 1.0f
            };

            try
            {
                if (EditorSceneManager.GetActiveScene().path != MenuScenePath)
                {
                    EditorSceneManager.SaveOpenScenes();
                    EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
                }
                var scene = EditorSceneManager.GetActiveScene();
                report.sceneName = scene.name;
                Physics.SyncTransforms();

                // ---------- 1. Expurgo ANTES ----------
                sb.AppendLine("\n--- 1. EXPURGO ANTES (SceneDuplicateAndOriginPurger.PurgeMenuScene + varredura r=1.0 m) ---");
                var purgeLog = new StringBuilder();
                SceneDuplicateAndOriginPurger.PurgeMenuScene(out int o1, out int d1, purgeLog);
                sb.Append(purgeLog);
                sb.AppendLine($"  removidos na origem: {o1} | duplicatas removidas: {d1}");
                var preOrigin = OriginSweep(1.0f);
                var preDup = DuplicateSweep();
                sb.AppendLine($"  varredura r=1.0m: {(preOrigin.Count == 0 ? "limpa" : string.Join(", ", preOrigin))}");
                sb.AppendLine($"  duplicatas: {(preDup.Count == 0 ? "nenhuma" : string.Join(", ", preDup))}");

                // ---------- 2. Mesa 1 ----------
                Transform desk = FindPath(Mesa1Path);
                if (desk == null) throw new Exception("Mesa 1 (" + Mesa1Path + ") não encontrada");
                GameObject radio = FindRoot("[IC] Radio_Menu_3D");
                GameObject deck = FindRoot("RadioCassetteDeck_Instance");
                GameObject rack = FindRoot("CassetteOrganizerRack_Instance");
                GameObject tapeDeck = FindRoot("Tape_CyberHorizon_Active");
                GameObject[] rackTapes = { FindRoot("Tape_LunaCantina_Rack"), FindRoot("Tape_NeonSunset_Rack"), FindRoot("Tape_EmeraldZen_Rack") };
                if (radio == null || deck == null || rack == null || tapeDeck == null || rackTapes.Any(t => t == null))
                    throw new Exception("Objetos da TASK-06 ausentes na cena (rádio/deck/rack/fitas)");

                Bounds deskB = GetDeskTopBounds(desk, out float deskTop);
                report.furnitureAudited = "full_desk_3 (1)";
                report.furnitureTopY = (float)Math.Round(deskTop, 4);
                sb.AppendLine($"\n--- 2. MESA 1 '{Mesa1Path}' tampo Y={deskTop:F3} (raycast) | XZ min={V(deskB.min)} max={V(deskB.max)} ---");

                var keepOnDesk = new HashSet<GameObject> { radio, deck, rack, tapeDeck, rackTapes[0], rackTapes[1], rackTapes[2] };

                // ---------- 3. Realocação de tudo o que não é TASK-06 ----------
                sb.AppendLine("\n--- 3. REALOCAÇÃO (Mesa 1 reservada a Rádio + Deck + Rack + 4 Fitas) ---");
                var specs = BuildMoveSpecs();
                var placedThisRun = new List<GameObject>();
                // Retira temporariamente todos os itens a realocar (e seus seguidores) para que posições de rodadas
                // anteriores não bloqueiem a busca; cada item volta à sua pose antes de ser (re)posicionado.
                var stash = new Dictionary<GameObject, (Vector3 p, Quaternion r)>();
                int si = 0;
                foreach (var spec in specs)
                {
                    foreach (var n in new[] { spec.rootName }.Concat(spec.followers))
                    {
                        var g = FindRoot(n);
                        if (g == null || stash.ContainsKey(g)) continue;
                        stash[g] = (g.transform.position, g.transform.rotation);
                        g.transform.position = new Vector3(20f + 3f * si++, -50f, 20f);
                    }
                }
                Physics.SyncTransforms();
                foreach (var spec in specs)
                {
                    GameObject go = FindRoot(spec.rootName);
                    if (go == null) { sb.AppendLine($"  [skip] '{spec.rootName}' não existe na cena"); continue; }
                    foreach (var n in new[] { spec.rootName }.Concat(spec.followers))
                    {
                        var g = FindRoot(n);
                        if (g != null && stash.TryGetValue(g, out var pose)) g.transform.SetPositionAndRotation(pose.p, pose.r);
                    }
                    Physics.SyncTransforms();
                    // Sempre (re)valida: itens já fora da Mesa 1 numa rodada anterior são reprocessados para garantir
                    // aterramento pela malha visível e espaçamento; a origem registrada é a posição original na Mesa 1.
                    var rel = RelocateItem(go, spec, placedThisRun, keepOnDesk, sb);
                    if (rel != null && OriginalOnMesa1.TryGetValue(spec.rootName, out var orig)) rel.oldPosition = new Vector3Json(orig);
                    if (rel != null) { report.relocations.Add(rel); placedThisRun.Add(go); }
                }
                // Balões flutuantes demonstrativos: acompanham a pistola
                MoveFloatingBalloons(report, sb);

                // Varredura: qualquer outro root restante sobre a Mesa 1 que não seja TASK-06
                foreach (var r in scene.GetRootGameObjects())
                {
                    if (keepOnDesk.Contains(r) || r.transform == desk.root) continue;
                    if (!r.activeInHierarchy) continue;
                    if (IsOnDesk(r, deskB, deskTop))
                        sb.AppendLine($"  [ATENÇÃO] root '{r.name}' ainda sobre a Mesa 1 em {V(r.transform.position)} (sem spec de realocação)");
                }

                // ---------- 4. Deck + Rack + Fitas ----------
                sb.AppendLine("\n--- 4. DECK / RACK / FITAS NA MESA 1 ---");
                FixDeckPrefabColliders(sb);
                LayoutDeckAndRack(desk, deskB, deskTop, radio, deck, rack, tapeDeck, rackTapes, sb);

                // ---------- 5. Expurgo DEPOIS ----------
                sb.AppendLine("\n--- 5. EXPURGO DEPOIS ---");
                purgeLog.Clear();
                SceneDuplicateAndOriginPurger.PurgeMenuScene(out int o2, out int d2, purgeLog);
                sb.Append(purgeLog);
                sb.AppendLine($"  removidos na origem: {o2} | duplicatas removidas: {d2}");
                report.objectsAtOriginZero = OriginSweep(1.0f);
                report.duplicateObjectsFound = DuplicateSweep();
                report.isOriginClean = report.objectsAtOriginZero.Count == 0;
                report.instanceCounts = InstanceCounts();
                sb.AppendLine($"  (0,0,0) r=1.0m: {(report.isOriginClean ? "LIMPO" : string.Join(", ", report.objectsAtOriginZero))}");
                sb.AppendLine($"  duplicatas: {(report.duplicateObjectsFound.Count == 0 ? "nenhuma" : string.Join(", ", report.duplicateObjectsFound))}");
                foreach (var c in report.instanceCounts) sb.AppendLine("  contagem: " + c);

                // ---------- 6. Auditoria pairwise ----------
                sb.AppendLine("\n--- 6. AUDITORIA PAIRWISE (todos os objetos sobre a Mesa 1) ---");
                AuditDesk(desk, deskB, deskTop, radio, deck, rack, tapeDeck, rackTapes, report, sb);

                EditorSceneManager.MarkSceneDirty(scene);
                bool saved = EditorSceneManager.SaveScene(scene);
                sb.AppendLine($"\n  Cena salva: {saved} ({scene.path})");

                // ---------- 7. Capturas ----------
                sb.AppendLine("\n--- 7. CAPTURAS OFFSCREEN 1920x1080 ---");
                CaptureAll(desk, deskTop, radio, deck, rack, tapeDeck, report, sb);

                string jsonPath = SpatialAuditUtility.SaveExtendedReport(report);
                sb.AppendLine($"\n  JSON: {jsonPath}");
                sb.AppendLine($"\n=== RESUMO: hasOverlap={report.hasOverlap} | minPairClearance={report.minPairClearanceMeters:F3} m ({report.minPairClearancePair}) | groundGap={report.groundGapMeters:F4} m | isOriginClean={report.isOriginClean} | duplicatas={report.duplicateObjectsFound.Count} ===");
            }
            catch (Exception ex)
            {
                sb.AppendLine("\n[ERRO] " + ex);
                Debug.LogError("[Task06_RelayoutPass] " + ex);
            }
            finally
            {
                Directory.CreateDirectory(OutDir);
                File.WriteAllText(ResultFile, sb.ToString());
                Debug.Log("[Task06_RelayoutPass] concluído → " + ResultFile);
                running = false;
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------------------------------------
        // Especificação das realocações (móveis candidatos em ordem de preferência)
        // ---------------------------------------------------------------------------------------------
        private static List<MoveSpec> BuildMoveSpecs()
        {
            return new List<MoveSpec>
            {
                new MoveSpec { rootName = "TrophyDisplayStand_Instance", task = "TASK-16",
                    furnitureCandidates = new[] { "Building/modern_shelves_3 (1)" },
                    shelfRayStartY = new[] { 1.50f, 1.13f, 0.78f },
                    fixedYaw = true, yawRelativeToFacing = 0f },
                new MoveSpec { rootName = "EnvironmentControlLever_Instance", task = "TASK-09",
                    furnitureCandidates = new[] { "Building/counter_2_b", "Building/drawer_simple (1)", "Building/counter_1" },
                    fixedYaw = true, yawRelativeToFacing = 0f },
                new MoveSpec { rootName = "FireworksLaunchButton_Instance", task = "TASK-09",
                    furnitureCandidates = new[] { "Building/counter_2_b", "Building/drawer_simple (1)", "Building/counter_1" },
                    followers = new[] { "Room_Fireworks_Launcher" },
                    groupPartner = "EnvironmentControlLever_Instance", partnerMinGap = 0.32f,
                    fixedYaw = true, yawRelativeToFacing = 0f },
                new MoveSpec { rootName = "ExoMascotBobblehead_Instance", task = "TASK-09",
                    furnitureCandidates = new[] { "Building/drawer_simple", "Building/drawer_simple (1)", "Building/counter_3_a" },
                    fixedYaw = true, yawRelativeToFacing = 125f },
                new MoveSpec { rootName = "BalloonGunDispenser_Instance", task = "TASK-07",
                    furnitureCandidates = new[] { "Building/counter_3_c (3)", "Building/counter_3_a (1)", "Building/counter_3_a" },
                    fixedYaw = true, yawRelativeToFacing = -35f },
                new MoveSpec { rootName = "ArcadeBallSpawner_Instance", task = "TASK-08",
                    furnitureCandidates = new[] { "Building/counter_1", "Building/drawer_simple (1)", "Building/drawer_simple" },
                    fixedYaw = true, yawRelativeToFacing = 0f },
                new MoveSpec { rootName = "Demo_Basketball_Table", task = "TASK-08",
                    furnitureCandidates = new[] { "Building/counter_1", "Building/drawer_simple (1)", "Building/drawer_simple", "Building/counter_3_c (3)", "Building/counter_3_a" },
                    groupPartner = "ArcadeBallSpawner_Instance", partnerMinGap = 0.10f,
                    fixedYaw = false },
                new MoveSpec { rootName = "[IC] Ergonomic_Toy_Demo", task = "TASK-02",
                    furnitureCandidates = new[] { "Building/drawer_simple (1)", "Building/drawer_simple", "Building/counter_3_a", "Building/counter_3_c (3)", "Building/counter_1", "Building/counter_3_c (1)", "Building/counter_3_a (1)" },
                    fixedYaw = false },
                // Decoração original do móvel que ocupava a única área de trás livre da Mesa 1 (o alcance real do Eject
                // é ~0.22 m, então o deck precisa ficar no fundo do tampo) e o vaso que já interpenetrava o rádio.
                new MoveSpec { rootName = "Building/full_desk_3 (1)/Table Lamp", task = "decoração do móvel (não-TASK)",
                    furnitureCandidates = new[] { "Building/drawer_simple (1)", "Building/counter_3_c (3)", "Building/counter_3_a (1)", "Building/counter_2_b",
                        "Building/counter_3_a", "Building/counter_3_c (1)", "Building/counter_3_c", "Building/drawer_simple", "Building/counter_1", "Building/coffee_table",
                        "Building/modern_shelves_3 (1)" },
                    shelfRayStartY = new[] { 2.80f },
                    fixedYaw = false },
                new MoveSpec { rootName = "Building/full_desk_3 (1)/Deco Variant 3", task = "decoração do móvel (não-TASK)",
                    furnitureCandidates = new[] { "Building/counter_3_c (2)", "Building/counter_3_a (1)", "Building/drawer_simple" },
                    fixedYaw = false },
            };
        }

        private static bool IsOnDesk(GameObject go, Bounds deskB, float deskTop)
        {
            if (!SolidBounds(go, out var b)) b = new Bounds(go.transform.position, Vector3.zero);
            var c = b.center;
            bool insideXZ = c.x >= deskB.min.x && c.x <= deskB.max.x && c.z >= deskB.min.z && c.z <= deskB.max.z;
            return insideXZ && b.min.y > deskTop - 0.06f && b.min.y < deskTop + 0.30f;
        }

        // ---------------------------------------------------------------------------------------------
        // Tampo da Mesa 1: bounds XZ do renderer/collider do próprio móvel + Y via raycast
        // ---------------------------------------------------------------------------------------------
        private static Bounds GetDeskTopBounds(Transform desk, out float topY)
        {
            Bounds b;
            var col = desk.GetComponent<Collider>();
            var rend = desk.GetComponent<Renderer>();
            if (col != null) b = col.bounds; else if (rend != null) b = rend.bounds; else SolidBounds(desk.gameObject, out b);
            topY = SurfaceTopAt(desk, new Vector3(b.center.x, b.max.y + 0.6f, b.center.z), out _);
            if (float.IsNaN(topY)) topY = 0.949f;
            return b;
        }

        /// <summary>Primeiro impacto de um raio vertical descendente que pertence ao móvel (ignora triggers). NaN se o 1º impacto não for o móvel.</summary>
        private static float SurfaceTopAt(Transform furniture, Vector3 origin, out Collider firstHit, GameObject ignore = null, float maxDist = 3f)
        {
            firstHit = null;
            var hits = Physics.RaycastAll(origin, Vector3.down, maxDist, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, c) => a.distance.CompareTo(c.distance));
            foreach (var h in hits)
            {
                if (ignore != null && h.collider.transform.IsChildOf(ignore.transform)) continue;
                firstHit = h.collider;
                return h.collider.transform.IsChildOf(furniture) || h.collider.transform == furniture ? h.point.y : float.NaN;
            }
            return float.NaN;
        }

        private static float FreeHeightAbove(Vector3 p, GameObject ignore)
        {
            var hits = Physics.RaycastAll(p + Vector3.up * 0.005f, Vector3.up, 3f, ~0, QueryTriggerInteraction.Ignore);
            float best = 3f;
            foreach (var h in hits)
            {
                if (ignore != null && h.collider.transform.IsChildOf(ignore.transform)) continue;
                best = Mathf.Min(best, h.distance);
            }
            return best;
        }

        private static float FacingYaw(Bounds furnitureB)
        {
            Vector3 roomCenter = new Vector3(5.0f, 0f, 2.0f);
            Vector3 d = roomCenter - furnitureB.center; d.y = 0;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            return Mathf.Round(yaw / 90f) * 90f;
        }

        private static GameObject[] InteractiveAvoidSet(IEnumerable<GameObject> extra)
        {
            var names = new[] { "TrophyDisplayStand_Instance", "EnvironmentControlLever_Instance", "FireworksLaunchButton_Instance",
                "ExoMascotBobblehead_Instance", "BalloonGunDispenser_Instance", "ArcadeBallSpawner_Instance", "Demo_Basketball_Table",
                "[IC] Ergonomic_Toy_Demo", "[IC] Radio_Menu_3D", "RadioCassetteDeck_Instance", "CassetteOrganizerRack_Instance",
                "Tape_CyberHorizon_Active", "Tape_LunaCantina_Rack", "Tape_NeonSunset_Rack", "Tape_EmeraldZen_Rack",
                "ArcadeBasketballHoop_Instance", "[IC] AimAssist_Target_Demo" };
            var list = new List<GameObject>();
            foreach (var n in names) { var g = FindRoot(n); if (g != null) list.Add(g); }
            if (extra != null) list.AddRange(extra);
            return list.Distinct().ToArray();
        }

        /// <summary>Renderers (não-TMP) da cena que são obstáculos potenciais, agrupados por "unidade" (root ou filho direto de Building).</summary>
        private static List<(GameObject unit, Bounds b)> CollectObstacleUnits()
        {
            var result = new List<(GameObject, Bounds)>();
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root.activeInHierarchy) continue;
                if (root.name == "Building")
                {
                    foreach (Transform child in root.transform)
                    {
                        if (!child.gameObject.activeInHierarchy) continue;
                        if (child.name == "Structure")
                        {
                            foreach (Transform s in child) if (s.gameObject.activeInHierarchy && SolidBounds(s.gameObject, out var sb2)) result.Add((s.gameObject, sb2));
                            continue;
                        }
                        // móveis: o corpo do móvel e cada filho com renderer viram unidades separadas
                        var bodyRend = child.GetComponent<Renderer>();
                        if (bodyRend != null && bodyRend.enabled) result.Add((child.gameObject, bodyRend.bounds));
                        foreach (Transform sub in child)
                            if (sub.gameObject.activeInHierarchy && SolidBounds(sub.gameObject, out var b3)) result.Add((sub.gameObject, b3));
                        if (bodyRend == null && child.childCount == 0 && SolidBounds(child.gameObject, out var b4)) result.Add((child.gameObject, b4));
                    }
                    continue;
                }
                if (SolidBounds(root, out var b)) result.Add((root, b));
            }
            return result;
        }

        // ---------------------------------------------------------------------------------------------
        // Realocação de um item: busca em grade sobre os móveis candidatos
        // ---------------------------------------------------------------------------------------------
        private static RelocationJson RelocateItem(GameObject go, MoveSpec spec, List<GameObject> placedThisRun, HashSet<GameObject> keepOnDesk, StringBuilder sb)
        {
            Vector3 oldPos = go.transform.position;
            Quaternion oldRot = go.transform.rotation;
            float oldYaw = go.transform.eulerAngles.y;

            // offsets de seguidores
            var followerOffsets = new List<(GameObject f, Vector3 localOff, Quaternion localRot)>();
            foreach (var fn in spec.followers)
            {
                var f = FindRoot(fn);
                if (f != null) followerOffsets.Add((f, Quaternion.Inverse(oldRot) * (f.transform.position - oldPos), Quaternion.Inverse(oldRot) * f.transform.rotation));
            }

            // offset pivô → base (ignora placas TMP)
            SolidBounds(go, out var ob);
            float pivotAboveBase = oldPos.y - ob.min.y;

            var avoid = InteractiveAvoidSet(placedThisRun);
            GameObject partner = string.IsNullOrEmpty(spec.groupPartner) ? null : FindRoot(spec.groupPartner);

            // itens sem orientação fixa experimentam 4 giros (o AABB muda com o yaw; útil em tampos estreitos)
            var combos = spec.furnitureCandidates.SelectMany(fp => (spec.fixedYaw ? new[] { 0f } : new[] { 0f, 45f, 90f, -45f }).Select(d => (fp, d))).ToList();
            foreach (var (furnPath, yawDelta) in combos)
            {
                Transform furn = FindPath(furnPath);
                if (furn == null) { sb.AppendLine($"    móvel '{furnPath}' não encontrado"); continue; }
                var fCol = furn.GetComponent<Collider>();
                Bounds fb = fCol != null ? fCol.bounds : (furn.GetComponent<Renderer>() != null ? furn.GetComponent<Renderer>().bounds : new Bounds());
                float facing = FacingYaw(fb);
                float yaw = spec.fixedYaw ? facing + spec.yawRelativeToFacing : oldYaw + yawDelta;
                Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
                float[] starts = spec.shelfRayStartY ?? new[] { fb.max.y + 0.6f };

                // unidades obstáculo (recalculadas por móvel; exclui o próprio item e o corpo do móvel)
                go.transform.SetPositionAndRotation(new Vector3(0, -50, 0), rot); // tira do caminho durante a busca
                foreach (var fo in followerOffsets) fo.f.transform.position = new Vector3(0, -50, 0);
                Physics.SyncTransforms();
                SolidBounds(go, out var itemB0);
                if (!RendererBounds(go, out var rendB0)) rendB0 = itemB0;
                Vector3 itemOff = itemB0.center - go.transform.position;   // AABB sólida (render+collider) relativa ao pivô
                Vector3 ext = itemB0.extents;
                float itemH = rendB0.size.y;
                float baseOff = go.transform.position.y - rendB0.min.y;      // aterramento pela malha VISÍVEL
                var units = CollectObstacleUnits().Where(u => u.unit != go && !u.unit.transform.IsChildOf(go.transform) && u.unit.transform != furn).ToList();

                Vector3 frontPt = new Vector3(Mathf.Clamp(5.0f, fb.min.x, fb.max.x), 0, Mathf.Clamp(2.0f, fb.min.z, fb.max.z));
                float bestScore = float.MaxValue; Vector3 bestPos = Vector3.zero; float bestTop = 0; float bestClear = 0;

                foreach (float startY in starts)
                {
                    for (float x = fb.min.x + ext.x + EdgeMargin; x <= fb.max.x - ext.x - EdgeMargin + 1e-4f; x += 0.01f)
                    for (float z = fb.min.z + ext.z + EdgeMargin; z <= fb.max.z - ext.z - EdgeMargin + 1e-4f; z += 0.01f)
                    {
                        // centro do AABB do item em (x,z)
                        float top = SurfaceTopAt(furn, new Vector3(x, startY, z), out _, go);
                        if (float.IsNaN(top)) continue;
                        // planicidade sob os 4 cantos
                        bool flat = true;
                        for (int k = 0; k < 4 && flat; k++)
                        {
                            float cx = x + ((k & 1) == 0 ? -1 : 1) * (ext.x - 0.008f);
                            float cz = z + ((k & 2) == 0 ? -1 : 1) * (ext.z - 0.008f);
                            float t2 = SurfaceTopAt(furn, new Vector3(cx, startY, cz), out _, go);
                            if (float.IsNaN(t2) || Mathf.Abs(t2 - top) > 0.012f) flat = false;
                        }
                        if (!flat) continue;
                        if (FreeHeightAbove(new Vector3(x, top, z), go) < itemH + 0.01f) continue;

                        float pivotY = top + 0.002f + baseOff;
                        var cand = new Bounds(new Vector3(x, pivotY + itemOff.y, z), itemB0.size);
                        float minClear = float.MaxValue; bool ok = true;
                        foreach (var u in units)
                        {
                            // ignora o que está abaixo do tampo (gavetas/corpo de outros móveis sob o nível)
                            if (u.b.max.y <= top + 0.004f) continue;
                            float need = DecorClearance;
                            bool isStructure = u.unit.name.ToLowerInvariant().Contains("wall") || u.b.size.x > 1.5f || u.b.size.z > 1.5f || u.b.size.y > 2.0f;
                            if (isStructure) need = EdgeMargin;
                            if (avoid.Contains(u.unit)) need = ItemSpacingSameFurniture;
                            if (partner != null && u.unit == partner) need = spec.partnerMinGap; // o parceiro do mesmo grupo usa o espaçamento do grupo
                            float c = SpatialAuditUtility.AabbClearance(cand, u.b);
                            if (c < need) { ok = false; break; }
                            if (!isStructure) minClear = Mathf.Min(minClear, c);
                        }
                        if (!ok) continue;

                        float score = new Vector2(x - frontPt.x, z - frontPt.z).magnitude;
                        if (partner != null)
                        {
                            float pd = new Vector2(x - partner.transform.position.x, z - partner.transform.position.z).magnitude;
                            score += Mathf.Abs(pd - 0.50f) * 2f;
                        }
                        if (score < bestScore) { bestScore = score; bestPos = new Vector3(x, top, z); bestTop = top; bestClear = minClear; }
                    }
                    if (bestScore < float.MaxValue) break; // primeira prateleira válida
                }

                if (bestScore < float.MaxValue)
                {
                    Vector3 pivot = new Vector3(bestPos.x - itemOff.x, bestTop + 0.002f + baseOff, bestPos.z - itemOff.z);
                    Undo.RecordObject(go.transform, "TASK-06 relocate");
                    go.transform.SetPositionAndRotation(pivot, rot);
                    foreach (var fo in followerOffsets)
                    {
                        Undo.RecordObject(fo.f.transform, "TASK-06 relocate follower");
                        fo.f.transform.SetPositionAndRotation(pivot + rot * fo.localOff, rot * fo.localRot);
                    }
                    Physics.SyncTransforms();
                    RendererBounds(go, out var nb);
                    SolidBounds(go, out var nsb);
                    if (nsb.min.y < bestTop - 0.005f)
                        sb.AppendLine($"      nota: collider do prefab de '{go.name}' desce {(bestTop - nsb.min.y):F3} m abaixo da malha visível (geometria do prefab, fora do escopo de layout)");
                    var rel = new RelocationJson
                    {
                        objectName = go.name, task = spec.task,
                        oldPosition = new Vector3Json(oldPos), newPosition = new Vector3Json(pivot),
                        oldRotY = (float)Math.Round(oldYaw, 1), newRotY = (float)Math.Round(go.transform.eulerAngles.y, 1),
                        fromFurniture = "full_desk_3 (1)", toFurniture = furnPath.Replace("Building/", ""),
                        surfaceTopY = (float)Math.Round(bestTop, 4),
                        groundGapMeters = (float)Math.Round(nb.min.y - bestTop, 4),
                        minClearanceToNeighboursMeters = (float)Math.Round(bestClear == float.MaxValue ? 9.99f : bestClear, 3)
                    };
                    sb.AppendLine($"  [MOVIDO] '{go.name}' ({spec.task}) {V(oldPos)} rotY {oldYaw:F0} → {V(pivot)} rotY {go.transform.eulerAngles.y:F0} | móvel: {rel.toFurniture} (tampo {bestTop:F3}) | gap chão {rel.groundGapMeters:F4} m | folga mín. vizinhos {rel.minClearanceToNeighboursMeters:F3} m");
                    foreach (var fo in followerOffsets) sb.AppendLine($"      seguidor '{fo.f.name}' → {V(fo.f.transform.position)}");
                    return rel;
                }
                if (spec.fixedYaw || yawDelta == -45f) sb.AppendLine($"    '{go.name}': sem posição válida em '{furnPath}'");
            }

            // falhou: restaura
            go.transform.SetPositionAndRotation(oldPos, oldRot);
            foreach (var fo in followerOffsets) fo.f.transform.SetPositionAndRotation(oldPos + oldRot * fo.localOff, oldRot * fo.localRot);
            Physics.SyncTransforms();
            sb.AppendLine($"  [FALHA] '{go.name}' não pôde ser realocado — mantido em {V(oldPos)}");
            return null;
        }

        private static void MoveFloatingBalloons(ExtendedSpatialAuditReport report, StringBuilder sb)
        {
            var gun = FindRoot("BalloonGunDispenser_Instance");
            if (gun == null) return;
            string[] names = { "Demo_Balloon_Red", "Demo_Balloon_Cyan", "Demo_Balloon_Yellow" };
            // flutuam acima e à frente da pistola (sentido do centro da sala), em alturas 1.45–1.85 m
            Vector3 toRoom = new Vector3(5.0f, 0, 2.0f) - gun.transform.position; toRoom.y = 0; toRoom.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, toRoom);
            Vector3[] offs = { toRoom * 0.45f + side * 0.25f, toRoom * 0.60f - side * 0.20f, toRoom * 0.35f - side * 0.45f };
            float[] heights = { 1.55f, 1.75f, 1.90f };
            for (int i = 0; i < names.Length; i++)
            {
                var b = FindRoot(names[i]);
                if (b == null) continue;
                Vector3 old = b.transform.position;
                Vector3 np = gun.transform.position + offs[i]; np.y = heights[i];
                Undo.RecordObject(b.transform, "TASK-06 balloon");
                b.transform.position = np;
                report.relocations.Add(new RelocationJson { objectName = names[i], task = "TASK-07 (balão demo flutuante)", oldPosition = new Vector3Json(old), newPosition = new Vector3Json(np), oldRotY = b.transform.eulerAngles.y, newRotY = b.transform.eulerAngles.y, fromFurniture = "ar acima da Mesa 1", toFurniture = "ar acima da pistola de balões", surfaceTopY = 0, groundGapMeters = 0, minClearanceToNeighboursMeters = 0 });
                sb.AppendLine($"  [MOVIDO] '{names[i]}' (balão flutuante) {V(old)} → {V(np)}");
            }
            Physics.SyncTransforms();
        }

        // ---------------------------------------------------------------------------------------------
        // Colisores do deck: o BoxCollider sólido original (0.20×0.075×0.15) englobava a cavidade do slot, então a fita
        // assentada (base local y≈0.0515) ficava 2.4 cm DENTRO dele e, ao ejetar, o PhysX a despenetrava para cima do
        // deck (verificado na simulação). Corpo vira laje até y=0.048 + duas bordas laterais na altura total, deixando o
        // corredor do slot livre para a fita sair para a frente. Mesmo layout aplicado no builder (BuildDeckPrefab).
        // ---------------------------------------------------------------------------------------------
        public const string DeckPrefabPath = "Assets/Prefabs/Interactive/RadioCassetteDeck.prefab";

        public static void ApplyDeckSlotColliders(GameObject deckRoot)
        {
            var box = deckRoot.GetComponent<BoxCollider>();
            if (box == null) box = deckRoot.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.024f, 0f);
            box.size = new Vector3(0.20f, 0.048f, 0.15f);
            foreach (var side in new[] { ("Collider_SideRim_L", -0.0875f), ("Collider_SideRim_R", 0.0875f) })
            {
                Transform t = deckRoot.transform.Find(side.Item1);
                if (t == null)
                {
                    t = new GameObject(side.Item1).transform;
                    t.SetParent(deckRoot.transform, false);
                }
                t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
                var c = t.GetComponent<BoxCollider>();
                if (c == null) c = t.gameObject.AddComponent<BoxCollider>();
                c.isTrigger = false;
                c.center = new Vector3(side.Item2, 0.0375f, 0f);
                c.size = new Vector3(0.025f, 0.075f, 0.15f);
            }
        }

        private static void FixDeckPrefabColliders(StringBuilder sb)
        {
            var root = PrefabUtility.LoadPrefabContents(DeckPrefabPath);
            try
            {
                var before = root.GetComponent<BoxCollider>();
                string b4 = before != null ? $"center {before.center} size {before.size}" : "nenhum";
                ApplyDeckSlotColliders(root);
                PrefabUtility.SaveAsPrefabAsset(root, DeckPrefabPath);
                sb.AppendLine($"  [PREFAB] {DeckPrefabPath}: BoxCollider raiz {b4} → center (0,0.024,0) size (0.20,0.048,0.15) + Collider_SideRim_L/R (0.025×0.075×0.15). Slot livre para a fita.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            // instância da cena: garante que nenhum override local reponha o collider antigo
            var inst = FindRoot("RadioCassetteDeck_Instance");
            if (inst != null)
            {
                var ib = inst.GetComponent<BoxCollider>();
                if (ib != null && (Mathf.Abs(ib.size.y - 0.048f) > 1e-4f))
                {
                    PrefabUtility.RevertObjectOverride(ib, InteractionMode.AutomatedAction);
                    sb.AppendLine("  [PREFAB] override de BoxCollider revertido na instância da cena");
                }
                if (inst.transform.Find("Collider_SideRim_L") == null) ApplyDeckSlotColliders(inst);
            }
            Physics.SyncTransforms();
        }

        // ---------------------------------------------------------------------------------------------
        // Deck + Rack
        // ---------------------------------------------------------------------------------------------
        private static Bounds AssemblyBounds(GameObject a, IEnumerable<GameObject> extra)
        {
            SolidBounds(a, out var b);
            foreach (var e in extra) if (SolidBounds(e, out var eb)) b.Encapsulate(eb);
            return b;
        }

        private static void SeatTapes(GameObject deck, GameObject rack, GameObject tapeDeck, GameObject[] rackTapes)
        {
            var socket = deck.GetComponentInChildren<RadioCassetteSocket>(true);
            Transform attach = socket != null && socket.slotAttachPoint != null ? socket.slotAttachPoint : deck.transform.Find("SlotAttachPoint");
            tapeDeck.transform.SetPositionAndRotation(attach.position, attach.rotation);
            for (int i = 0; i < 3; i++)
            {
                var slot = rack.transform.Find("Slot_" + (i + 1));
                rackTapes[i].transform.SetPositionAndRotation(slot.position, slot.rotation);
            }
            Physics.SyncTransforms();
        }

        private static List<(GameObject unit, Bounds b)> DeskDecor(Transform desk, float deskTop)
        {
            var list = new List<(GameObject, Bounds)>();
            var col = desk.GetComponent<Collider>();
            Bounds db = col != null ? col.bounds : new Bounds(desk.position, Vector3.one * 2f);
            foreach (Transform c in desk)
                if (c.gameObject.activeInHierarchy && SolidBounds(c.gameObject, out var b) && b.max.y > deskTop + 0.01f && b.min.y > deskTop - 0.05f && b.min.y < deskTop + 0.3f
                    && b.center.x >= db.min.x && b.center.x <= db.max.x && b.center.z >= db.min.z && b.center.z <= db.max.z)
                    list.Add((c.gameObject, b));
            return list;
        }

        private static bool InsideTop(Bounds b, Bounds deskB, float margin) =>
            b.min.x >= deskB.min.x + margin && b.max.x <= deskB.max.x - margin && b.min.z >= deskB.min.z + margin && b.max.z <= deskB.max.z - margin;

        private static Bounds LandingZone(GameObject deck, float deskTop)
        {
            // Alcance balístico do Eject com 0.15 N·s / 0.12 kg (|v|≈1.25 m/s, 18°+0.4 up, saída a ~8.5 cm do tampo) ≈ 0.22 m
            // à frente do slot, + deslize. Zona reservada: de 0.08 a 0.40 m à frente do pivô do deck, largura 0.16 m.
            Vector3 f = deck.transform.forward; f.y = 0; f.Normalize();
            Vector3 c = deck.transform.position + f * 0.24f; c.y = deskTop + 0.05f;
            var b = new Bounds(c, Vector3.zero);
            Vector3 r = Vector3.Cross(Vector3.up, f);
            foreach (float a in new[] { -0.16f, 0.16f }) foreach (float w in new[] { -0.08f, 0.08f }) foreach (float h in new[] { -0.045f, 0.045f })
                b.Encapsulate(c + f * a + r * w + Vector3.up * h);
            return b;
        }

        private static void LayoutDeckAndRack(Transform desk, Bounds deskB, float deskTop, GameObject radio, GameObject deck, GameObject rack, GameObject tapeDeck, GameObject[] rackTapes, StringBuilder sb)
        {
            Vector3 oldDeck = deck.transform.position, oldRack = rack.transform.position;
            Quaternion rot = radio.transform.rotation; // mesma rotação do rádio
            rot = Quaternion.Euler(0f, rot.eulerAngles.y, 0f);
            SolidBounds(radio, out var radioB);
            var decor = DeskDecor(desk, deskTop);
            sb.AppendLine($"  rádio AABB min={V(radioB.min)} max={V(radioB.max)} | decoração fixa do móvel: {string.Join(", ", decor.Select(d => d.unit.name + " " + V(d.b.min) + "-" + V(d.b.max)))}");

            // tira o rack do caminho
            rack.transform.position = new Vector3(0, -60, 0);
            foreach (var t in rackTapes) t.transform.position = new Vector3(0, -60, 0);
            Physics.SyncTransforms();

            // Bounds de referência (rotação fixa → só translação durante a busca)
            Vector3 p0 = new Vector3(0f, deskTop + 0.002f, 0f);
            deck.transform.SetPositionAndRotation(new Vector3(0, -40, 0), rot);
            rack.transform.SetPositionAndRotation(new Vector3(3, -40, 0), rot);
            SeatTapes(deck, rack, tapeDeck, rackTapes);
            Bounds deckRef = AssemblyBounds(deck, new[] { tapeDeck }); deckRef.center += p0 - new Vector3(0, -40, 0);
            Bounds rackRef = AssemblyBounds(rack, rackTapes); rackRef.center += p0 - new Vector3(3, -40, 0);
            deck.transform.position = p0; Physics.SyncTransforms();
            Bounds landRef = LandingZone(deck, deskTop);
            deck.transform.position = new Vector3(0, -40, 0); Physics.SyncTransforms();
            Bounds At(Bounds r, Vector3 p) { var b = r; b.center += p - p0; return b; }
            bool Supported(Vector3 p, GameObject ign) { float t = SurfaceTopAt(desk, new Vector3(p.x, deskTop + 0.6f, p.z), out _, ign); return !float.IsNaN(t) && Mathf.Abs(t - deskTop) <= 0.01f; }

            // ---- Deck: candidatos em grade (mesma rotação do rádio), ordenados por proximidade ao rádio ----
            var deckCands = new List<(Vector3 p, float score)>();
            for (float gx = deskB.min.x + 0.06f; gx <= deskB.max.x - 0.06f; gx += 0.01f)
            for (float gz = deskB.min.z + 0.06f; gz <= deskB.max.z - 0.06f; gz += 0.01f)
            {
                Vector3 p = new Vector3(gx, deskTop + 0.002f, gz);
                var ab = At(deckRef, p);
                if (!InsideTop(ab, deskB, EdgeMargin)) continue;
                float cr = SpatialAuditUtility.AabbClearance(ab, radioB);
                if (cr < PlacementClearance) continue;
                if (decor.Any(dc => SpatialAuditUtility.AabbClearance(ab, dc.b) < PlacementClearance)) continue;
                var lz = At(landRef, p);
                if (!InsideTop(lz, deskB, 0.0f)) continue;
                if (SpatialAuditUtility.AabbClearance(lz, radioB) < 0.02f || decor.Any(dc => SpatialAuditUtility.AabbClearance(lz, dc.b) < 0.02f)) continue;
                deckCands.Add((p, cr));
            }
            deckCands.Sort((a, b) => a.score.CompareTo(b.score));
            sb.AppendLine($"  candidatos válidos para o deck: {deckCands.Count}");

            Vector3 bestPos = oldDeck, bestRack = oldRack; bool found = false; int tried = 0;
            foreach (var dc0 in deckCands)
            {
                if (tried > 400) break;
                if (!Supported(dc0.p, deck)) continue;
                tried++;
                var deckAsmC = At(deckRef, dc0.p); var landC = At(landRef, dc0.p);
                float bestR = float.MaxValue; Vector3 rp = Vector3.zero;
                for (float x = deskB.min.x + 0.05f; x <= deskB.max.x - 0.05f; x += 0.01f)
                for (float z = deskB.min.z + 0.05f; z <= deskB.max.z - 0.05f; z += 0.01f)
                {
                    Vector3 p = new Vector3(x, deskTop + 0.002f, z);
                    var rb = At(rackRef, p);
                    if (!InsideTop(rb, deskB, EdgeMargin)) continue;
                    if (SpatialAuditUtility.AabbClearance(rb, radioB) < PlacementClearance) continue;
                    if (SpatialAuditUtility.AabbClearance(rb, deckAsmC) < PlacementClearance) continue;
                    if (SpatialAuditUtility.AabbClearance(rb, landC) < 0.03f) continue;
                    if (decor.Any(d2 => SpatialAuditUtility.AabbClearance(rb, d2.b) < PlacementClearance)) continue;
                    float s = Vector3.Distance(p, dc0.p);
                    if (s < bestR && Supported(p, rack)) { bestR = s; rp = p; }
                }
                if (bestR < float.MaxValue) { bestPos = dc0.p; bestRack = rp; found = true; break; }
            }
            if (!found) sb.AppendLine("  [FALHA] nenhuma combinação deck+rack válida — mantidos nas posições anteriores");

            deck.transform.SetPositionAndRotation(bestPos, rot);
            rack.transform.SetPositionAndRotation(bestRack, rot);
            SeatTapes(deck, rack, tapeDeck, rackTapes);
            var deckAsm = AssemblyBounds(deck, new[] { tapeDeck });
            var landing = LandingZone(deck, deskTop);
            sb.AppendLine($"  [DECK] {V(oldDeck)} → {V(bestPos)} rotY {rot.eulerAngles.y:F1} | folga AABB deck+fita ↔ rádio = {SpatialAuditUtility.AabbClearance(deckAsm, radioB):F3} m | zona de pouso do eject reservada min={V(landing.min)} max={V(landing.max)}");
            var rackAsm = AssemblyBounds(rack, rackTapes);
            sb.AppendLine($"  [RACK] {V(oldRack)} → {V(bestRack)} rotY {rot.eulerAngles.y:F1} | folga rack+fitas ↔ deck+fita = {SpatialAuditUtility.AabbClearance(rackAsm, deckAsm):F3} m | ↔ rádio = {SpatialAuditUtility.AabbClearance(rackAsm, radioB):F3} m");
            sb.AppendLine($"  [FITAS] deck: '{tapeDeck.name}' em {V(tapeDeck.transform.position)} (SlotAttachPoint) | rack: " + string.Join(", ", rackTapes.Select(t => $"'{t.name}' {V(t.transform.position)}")));

            // integridade das referências
            var socket = deck.GetComponentInChildren<RadioCassetteSocket>(true);
            var rackComp = rack.GetComponent<CassetteOrganizerRack>();
            var so = socket != null ? new SerializedObject(socket) : null;
            var start = so?.FindProperty("m_StartingSelectedInteractable");
            sb.AppendLine($"  [REFS] socket.startingSelectedInteractable = {(socket != null && socket.startingSelectedInteractable != null ? ((Component)socket.startingSelectedInteractable).name : "NULL")} | radioTarget = {(socket != null && socket.radioTarget != null ? socket.radioTarget.name : "NULL")} | ejectImpulseForce(cena) = {(socket != null ? socket.ejectImpulseForce.ToString("F2") : "?")} | maxEjectSpeed = {(socket != null ? socket.maxEjectSpeed.ToString("F2") : "?")}");
            if (rackComp != null)
            {
                var rso = new SerializedObject(rackComp);
                var it = rso.GetIterator(); var names = new List<string>();
                while (it.NextVisible(true))
                    if (it.propertyType == SerializedPropertyType.ObjectReference && it.propertyPath.Contains("Tape") && it.objectReferenceValue != null) names.Add(it.propertyPath + "=" + it.objectReferenceValue.name);
                sb.AppendLine($"  [REFS] rack: {string.Join(" | ", names)}");
            }
            EditorUtility.SetDirty(deck.transform); EditorUtility.SetDirty(rack.transform);
        }

        // ---------------------------------------------------------------------------------------------
        // Auditoria
        // ---------------------------------------------------------------------------------------------
        private static void AuditDesk(Transform desk, Bounds deskB, float deskTop, GameObject radio, GameObject deck, GameObject rack, GameObject tapeDeck, GameObject[] rackTapes, ExtendedSpatialAuditReport report, StringBuilder sb)
        {
            Physics.SyncTransforms();
            var items = new List<(string name, GameObject go, string group)>();
            items.Add(("[IC] Radio_Menu_3D", radio, "radio"));
            items.Add(("RadioCassetteDeck_Instance", deck, "deck"));
            items.Add(("Tape_CyberHorizon_Active (no slot do deck)", tapeDeck, "deck"));
            items.Add(("CassetteOrganizerRack_Instance", rack, "rack"));
            foreach (var t in rackTapes) items.Add((t.name + " (no slot do rack)", t, "rack"));
            foreach (var d in DeskDecor(desk, deskTop)) items.Add(("full_desk_3 (1)/" + d.unit.name + " (decoração fixa do móvel)", d.unit, "decor:" + d.unit.name));
            var known = new HashSet<GameObject>(items.Select(i => i.go));
            foreach (var r in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (!known.Contains(r) && r.name != "Building" && r.activeInHierarchy && IsOnDesk(r, deskB, deskTop))
                    items.Add((r.name + " (RESTANTE NÃO-TASK-06)", r, "other:" + r.name));

            var bounds = new Dictionary<GameObject, Bounds>();
            foreach (var it in items) { SolidBounds(it.go, out var b); bounds[it.go] = b; }

            float maxGap = 0f; bool allInside = true;
            foreach (var it in items)
            {
                var b = bounds[it.go];
                float gap = (it.group.StartsWith("decor") || it.go == rackTapes[0] || it.go == rackTapes[1] || it.go == rackTapes[2] || it.go == tapeDeck) ? float.NaN : b.min.y - deskTop;
                report.objectsOnFurniture.Add(new ObjectOnFurnitureJson { name = it.name, boundsMin = new Vector3Json(b.min), boundsMax = new Vector3Json(b.max), groundGapMeters = float.IsNaN(gap) ? -1f : (float)Math.Round(gap, 4) });
                if (!float.IsNaN(gap)) { maxGap = Mathf.Max(maxGap, Mathf.Abs(gap)); }
                if (!it.group.StartsWith("decor") && !InsideTop(b, deskB, 0f)) { allInside = false; sb.AppendLine($"  [BORDA] '{it.name}' sai do tampo: {V(b.min)}-{V(b.max)}"); }
            }

            bool overlap = false; float minC = float.MaxValue; string minPair = "";
            for (int i = 0; i < items.Count; i++)
            for (int j = i + 1; j < items.Count; j++)
            {
                var a = items[i]; var c = items[j];
                float cl = SpatialAuditUtility.AabbClearance(bounds[a.go], bounds[c.go]);
                bool sameAsm = a.group == c.group && (a.group == "deck" || a.group == "rack");
                bool bothDecor = a.group.StartsWith("decor") && c.group.StartsWith("decor");
                var pj = new PairwiseClearanceJson
                {
                    objectA = a.name, objectB = c.name, clearanceMeters = (float)Math.Round(cl, 4),
                    excludedFromOverlapCheck = sameAsm || bothDecor,
                    note = sameAsm ? "mesmo conjunto: fita assentada no slot do próprio suporte (contato intencional socket/slot)" : bothDecor ? "decoração original do próprio móvel (fora do escopo TASK-06)" : ""
                };
                pj.passes = pj.excludedFromOverlapCheck || cl >= RequiredClearance;
                report.pairwiseClearances.Add(pj);
                if (!pj.excludedFromOverlapCheck)
                {
                    if (cl < RequiredClearance) { overlap = true; sb.AppendLine($"  [FALHA PAR] {a.name} × {c.name}: {cl:F3} m"); }
                    if (cl < minC) { minC = cl; minPair = a.name + " × " + c.name; }
                }
            }
            report.hasOverlap = overlap;
            report.minPairClearanceMeters = (float)Math.Round(minC, 4);
            report.minPairClearancePair = minPair;
            report.groundGapMeters = (float)Math.Round(maxGap, 4);
            report.isPhysicallyGrounded = maxGap < 0.01f;
            report.isInsideFurnitureTop = allInside;
            report.notes.Add("Folga = distância euclidiana entre AABBs mundiais (renderers não-TMP + colliders sólidos). Para objetos girados (rotY≈75°) a AABB é maior que o objeto real, então o valor é conservador.");
            report.notes.Add("Pares deck↔fita do slot e rack↔fitas do rack (e fitas do rack entre si) estão listados mas excluídos do critério de sobreposição: o contato é o encaixe funcional (XRSocketInteractor / slots).");
            report.notes.Add("groundGapMeters = |base AABB − tampo (raycast no collider da mesa)| para rádio, deck e rack.");
            sb.AppendLine($"  objetos auditados sobre a Mesa 1: {items.Count} | pares: {report.pairwiseClearances.Count} | pares avaliados: {report.pairwiseClearances.Count(p => !p.excludedFromOverlapCheck)}");
            sb.AppendLine($"  menor folga entre pares avaliados: {minC:F3} m ({minPair}) | hasOverlap={overlap} | groundGap máx = {maxGap:F4} m | dentro do tampo = {allInside}");
            foreach (var p in report.pairwiseClearances.Where(p => !p.excludedFromOverlapCheck).OrderBy(p => p.clearanceMeters).Take(8))
                sb.AppendLine($"    {p.clearanceMeters,7:F3} m  {p.objectA} × {p.objectB}");
        }

        private static List<string> OriginSweep(float radius)
        {
            var list = new List<string>();
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var r in scene.GetRootGameObjects())
            {
                if (r.transform.position.magnitude >= radius) continue;
                bool hasGeometry = SolidBounds(r, out var b) && b.size.magnitude < 3f; // contêineres vazios (gerenciadores) e o 'Building' não são lixo
                bool interactive = r.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.XRBaseInteractable>(true) != null;
                if ((hasGeometry || interactive) && !SpatialAuditUtility.IsWhitelisted(r)) list.Add($"{r.name} @ {V(r.transform.position)}");
            }
            foreach (var x in UnityEngine.Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.XRBaseInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (x.transform.position.magnitude < radius && x.transform.root.name != "Building") list.Add($"interactable {x.name} @ {V(x.transform.position)}");
            return list.Distinct().ToList();
        }

        private static readonly string[] TrackedNames = {
            "[IC] Radio_Menu_3D", "RadioCassetteDeck_Instance", "CassetteOrganizerRack_Instance", "Tape_CyberHorizon_Active", "Tape_LunaCantina_Rack",
            "Tape_NeonSunset_Rack", "Tape_EmeraldZen_Rack", "TrophyDisplayStand_Instance", "EnvironmentControlLever_Instance",
            "FireworksLaunchButton_Instance", "ExoMascotBobblehead_Instance", "BalloonGunDispenser_Instance", "ArcadeBallSpawner_Instance",
            "Demo_Basketball_Table", "ArcadeBasketballHoop_Instance", "[IC] Ergonomic_Toy_Demo", "[IC] Drone_Guia_IA_ExoBot", "Room_Fireworks_Launcher" };

        private static List<string> DuplicateSweep()
        {
            var list = new List<string>();
            var roots = EditorSceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var g in roots.GroupBy(r => r.name).Where(g => g.Count() > 1)) list.Add($"{g.Key} x{g.Count()}");
            foreach (var r in roots) if (r.name.EndsWith("(Clone)")) list.Add(r.name);
            // fitas: exatamente 1 instância de cada MusicCassetteTape
            var tapes = UnityEngine.Object.FindObjectsByType<MusicCassetteTape>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var g in tapes.GroupBy(t => t.tapeId).Where(g => g.Count() > 1)) list.Add($"MusicCassetteTape '{g.Key}' x{g.Count()}");
            int decks = UnityEngine.Object.FindObjectsByType<RadioCassetteSocket>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            if (decks > 1) list.Add($"RadioCassetteSocket x{decks}");
            return list.Distinct().ToList();
        }

        private static List<string> InstanceCounts()
        {
            var roots = EditorSceneManager.GetActiveScene().GetRootGameObjects();
            var res = TrackedNames.Select(n => $"{n}: {roots.Count(r => r.name == n)}").ToList();
            res.Add($"MusicCassetteTape (total): {UnityEngine.Object.FindObjectsByType<MusicCassetteTape>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length}");
            res.Add($"RadioCassetteSocket (total): {UnityEngine.Object.FindObjectsByType<RadioCassetteSocket>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length}");
            return res;
        }

        // ---------------------------------------------------------------------------------------------
        // Capturas (offscreen)
        // ---------------------------------------------------------------------------------------------
        private static bool Visible(Vector3 cam, Vector3 target, GameObject[] allowed)
        {
            Vector3 d = target - cam;
            var hits = Physics.RaycastAll(cam, d.normalized, d.magnitude - 0.01f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                bool ok = allowed.Any(a => h.collider.transform.IsChildOf(a.transform));
                if (!ok) return false;
            }
            return true;
        }

        /// <summary>Escolhe, entre candidatos, a primeira câmera com linha de visão livre até todos os alvos.</summary>
        private static Vector3 PickCamera(IEnumerable<Vector3> candidates, Vector3[] targets, GameObject[] allowed, StringBuilder sb, string label)
        {
            Vector3 first = Vector3.zero; bool hasFirst = false;
            foreach (var c in candidates)
            {
                if (!hasFirst) { first = c; hasFirst = true; }
                if (targets.All(t => Visible(c, t, allowed))) return c;
            }
            sb.AppendLine($"  [aviso] {label}: nenhum candidato com visão 100% livre; usando o primeiro");
            return first;
        }

        private static IEnumerable<Vector3> Orbit(Vector3 center, Vector3 baseDir, float dist, float height, float[] yawOffsets)
        {
            baseDir.y = 0; baseDir.Normalize();
            foreach (float yo in yawOffsets)
            {
                Vector3 d = Quaternion.Euler(0, yo, 0) * baseDir;
                yield return center + d * dist + Vector3.up * height;
            }
        }

        private static void CaptureAll(Transform desk, float deskTop, GameObject radio, GameObject deck, GameObject rack, GameObject tapeDeck, ExtendedSpatialAuditReport report, StringBuilder sb)
        {
            string dir = Path.GetFullPath(OutDir);
            Directory.CreateDirectory(dir);
            Physics.SyncTransforms();
            SolidBounds(deck, out var deckB); SolidBounds(rack, out var rackB); SolidBounds(radio, out var radioB);
            Vector3 deckFwd = deck.transform.forward; deckFwd.y = 0; deckFwd.Normalize();
            Vector3 deskCenterTop = new Vector3((deckB.center.x + rackB.center.x + radioB.center.x) / 3f, deskTop + 0.05f, (deckB.center.z + rackB.center.z + radioB.center.z) / 3f);
            var allowedAll = new[] { radio, deck, rack, tapeDeck, FindRoot("Tape_LunaCantina_Rack"), FindRoot("Tape_NeonSunset_Rack"), FindRoot("Tape_EmeraldZen_Rack") }.Where(g => g != null).ToArray();
            var sockets = deck.GetComponentInChildren<RadioCassetteSocket>(true);
            Vector3 slotPt = sockets != null && sockets.slotAttachPoint != null ? sockets.slotAttachPoint.position : deckB.center;
            float[] yaws = { 0, 15, -15, 30, -30, 45, -45 };

            // View 1 — frente da mesa (lado da sala)
            Vector3 c1 = PickCamera(Orbit(deskCenterTop, Vector3.right, 1.25f, 0.50f, yaws), new[] { deckB.center, rackB.center, radioB.center }, allowedAll, sb, "View1");
            Cap(Path.Combine(dir, "TASK-06_View1_Front.png"), c1, deskCenterTop + Vector3.up * 0.08f, 55f, sb);

            // View 2 — lateral rasante na altura do tampo (Y≈0.97), deck e rack inteiros
            Vector3 mid = (deckB.center + rackB.center) * 0.5f; mid.y = deskTop + 0.03f;
            Vector3 along = rack.transform.position - deck.transform.position; along.y = 0;
            Vector3 sideDir = along.sqrMagnitude > 1e-4f ? along.normalized : -radio.transform.right;
            var sideCands = new List<Vector3>();
            foreach (float yo in new float[] { 30, 40, 20, 50, 10, 60, -30, -40 })
            {
                Vector3 d = Quaternion.Euler(0, yo, 0) * sideDir;
                foreach (float dist in new float[] { 0.70f, 0.85f, 1.0f, 0.6f })
                    sideCands.Add(new Vector3(mid.x + d.x * dist, deskTop + 0.022f, mid.z + d.z * dist));
            }
            Vector3 c2 = PickCamera(sideCands, new[] { new Vector3(deckB.center.x, deskTop + 0.02f, deckB.center.z), new Vector3(rackB.center.x, deskTop + 0.02f, rackB.center.z) }, allowedAll, sb, "View2");
            Cap(Path.Combine(dir, "TASK-06_View2_Side_Grounding.png"), c2, mid, 50f, sb);

            // View 3 / Tape_Inserted_Slot — close frontal do deck
            Vector3 c3 = slotPt + deckFwd * 0.30f + Vector3.up * 0.10f;
            Cap(Path.Combine(dir, "TASK-06_View3_Closeup.png"), c3, slotPt + Vector3.down * 0.025f, 50f, sb);
            Vector3 c3b = slotPt + deckFwd * 0.24f + Vector3.up * 0.15f + Vector3.Cross(Vector3.up, deckFwd) * 0.05f;
            Cap(Path.Combine(dir, "TASK-06_Tape_Inserted_Slot.png"), c3b, slotPt + Vector3.down * 0.03f, 45f, sb);

            // Rack + 3 fitas
            Vector3 rackFwd = rack.transform.forward; rackFwd.y = 0; rackFwd.Normalize();
            Vector3 rackTop = rackB.center + Vector3.up * 0.02f;
            Vector3 c5 = PickCamera(Orbit(rackTop, rackFwd, 0.32f, 0.16f, yaws), new[] { rackB.center }, allowedAll, sb, "RackCloseup");
            Cap(Path.Combine(dir, "TASK-06_Cassette_Tapes_Closeup.png"), c5, rackTop, 50f, sb);

            // Overview rádio + deck + rack (alta)
            Vector3 c4 = PickCamera(Orbit(deskCenterTop, Vector3.right, 1.0f, 0.75f, new float[] { 20, 0, 35, -20 }), new[] { deckB.center, rackB.center }, allowedAll, sb, "Overview");
            Cap(Path.Combine(dir, "TASK-06_Radio_Deck_Overview.png"), c4, deskCenterTop + Vector3.up * 0.12f, 55f, sb);

            // Eject — lógica REAL do runtime (RadioCassetteSocket.EjectRoutine) + Physics.Simulate; ver Task06_EjectSequencePass
            var deskB = GetDeskTopBounds(desk, out _);
            var ej = Task06_EjectSequencePass.SimulateRealEject(dir, desk, deskB, deskTop, deck, tapeDeck, sb);
            report.notes.Add($"Eject REAL (runtime): {ej.result} — {ej.horizontalDistanceFromSlot:F3} m do slot, base {ej.finalBaseY:F3}, IgnoreCollision restaurado = {ej.ignoreCollisionRestoredAfterEject}.");
        }

        private static void Cap(string path, Vector3 cam, Vector3 look, float fov, StringBuilder sb)
        {
            bool ok = VisualQACaptureEditor.CaptureCameraView(path, cam, look, 1920, 1080, fov);
            sb.AppendLine($"  {(ok ? "OK" : "ERRO")} {Path.GetFileName(path)} | câmera {V(cam)} → alvo {V(look)} fov {fov}");
        }
    }
}
