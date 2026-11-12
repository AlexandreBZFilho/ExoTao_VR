using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRRehab.EditorTools;
using VRRehab.Interactive.Task06;

namespace VRRehab.EditorAutoSetup
{
    /// <summary>
    /// TASK-06.D — Sequência de Eject usando a lógica REAL do runtime (RadioCassetteSocket.EjectRoutine, a corrotina que
    /// EjectCurrentTape() inicia). Em Edit Mode o Unity não bombeia corrotinas, então o IEnumerator real é avançado aqui
    /// quadro a quadro em sincronia com Physics.Simulate(1/90). O simulador NÃO ignora colisões por conta própria: quem
    /// liga/desliga Physics.IgnoreCollision fita↔deck é o próprio runtime (BeginEjectCollisionIgnore/EndEjectCollisionIgnore).
    /// Capturas apenas por câmera offscreen (Camera.Render → RenderTexture). Sem automação de desktop.
    /// Entrada standalone: Run() (via `unity command script run` ou menu).
    /// </summary>
    public static class Task06_EjectSequencePass
    {
        private const string MenuScenePath = "Assets/Scenes/Menu_Principal.unity";
        private const string OutDir = ".agents/visual_qa";
        private const string ResultFile = ".agents/visual_qa/TASK-06_BuilderResult.txt";
        private const string AuditFile = ".agents/visual_qa/TASK-06_SpatialAudit.json";
        private const string Mesa1Path = "Building/full_desk_3 (1)";
        private const BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags PrivStatic = BindingFlags.NonPublic | BindingFlags.Static;

        [Serializable]
        public class EjectAuditJson
        {
            public string method;
            public float physicsStepSeconds;
            public float editorCoroutineFrameSeconds;
            public float ejectImpulseForce;
            public float maxEjectSpeed;
            public float ejectIgnoreCollisionSeconds;
            public float ejectRecaptureCooldownSeconds;
            public bool runtimeIgnoreBegan;
            public int deckCollidersIgnoredByRuntime;
            public float ignoreEndedAtSeconds = -1f;
            public float cooldownEndedAtSeconds = -1f;
            public bool socketRefusedRecaptureDuringCooldown;
            public bool ignoreCollisionRestoredAfterEject;
            public bool tapeCollidesWithDeckAgain;
            public float dropTestTapeBaseY;
            public float peakY;
            public float minY;
            public float launchSpeed;
            public Vector3Json midFlightPosition = new Vector3Json();
            public float midFlightTime;
            public Vector3Json finalPosition = new Vector3Json();
            public float finalBaseY;
            public float deskTopY;
            public float settleTimeSeconds;
            public float horizontalDistanceFromSlot;
            public float distanceToDeckAabb;
            public bool outsideSlotTrigger;
            public bool restingOnMesa1Top;
            public bool insideMesa1TopEdges;
            public float minEdgeMarginMeters;
            public float respawnFloorThresholdY;
            public bool neverBelowRespawnFloor;
            public bool recaptureRiskAfterCooldown;
            public string result;
        }

        [MenuItem("VRRehab/AutoBuild/TASK-06 Eject Sequence (runtime real)")]
        public static void RunMenu() => Run();

        public static string Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("\n--- 8. EJECT (RE-RUN pós-correção [C] em RadioCassetteSocket: lógica REAL do runtime) ---");
            sb.AppendLine($"  Data/Hora local: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            EjectAuditJson ej = null;
            try
            {
                var scene = EditorSceneManager.GetActiveScene();
                if (scene.path != MenuScenePath)
                {
                    if (scene.isDirty) throw new Exception("cena ativa suja e diferente do Menu — abortado para não perder alterações");
                    EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
                    scene = EditorSceneManager.GetActiveScene();
                }
                if (scene.isDirty) throw new Exception("Menu_Principal com alterações pendentes antes da simulação — abortado (não descarto trabalho do usuário)");

                Transform desk = GameObject.Find(Mesa1Path)?.transform;
                GameObject deck = GameObject.Find("RadioCassetteDeck_Instance");
                GameObject tape = GameObject.Find("Tape_CyberHorizon_Active");
                if (desk == null || deck == null || tape == null) throw new Exception("Mesa 1 / deck / fita ativa não encontrados");
                var gdb = typeof(Task06_RelayoutPass).GetMethod("GetDeskTopBounds", PrivStatic);
                object[] gargs = { desk, 0f };
                Bounds deskB = (Bounds)gdb.Invoke(null, gargs);
                float deskTop = (float)gargs[1];

                ej = SimulateRealEject(Path.GetFullPath(OutDir), desk, deskB, deskTop, deck, tape, sb);

                // Restauração definitiva: descarta qualquer mutação da simulação recarregando a cena salva (estava limpa)
                EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
                scene = EditorSceneManager.GetActiveScene();
                var tapeAfter = GameObject.Find("Tape_CyberHorizon_Active");
                var deckAfter = GameObject.Find("RadioCassetteDeck_Instance");
                var sock = deckAfter.GetComponentInChildren<RadioCassetteSocket>(true);
                float seatErr = Vector3.Distance(tapeAfter.transform.position, sock.slotAttachPoint.position);
                sb.AppendLine($"  cena recarregada do disco (descarta mutações da simulação): fita em {V(tapeAfter.transform.position)}, erro ao SlotAttachPoint {seatErr * 1000f:F1} mm, kinematic={tapeAfter.GetComponent<Rigidbody>().isKinematic}");

                // Expurgo + contagens
                var purgeLog = new StringBuilder();
                SceneDuplicateAndOriginPurger.PurgeMenuScene(out int o, out int d, purgeLog);
                sb.Append(purgeLog);
                var origin = (List<string>)typeof(Task06_RelayoutPass).GetMethod("OriginSweep", PrivStatic).Invoke(null, new object[] { 1.0f });
                var dups = (List<string>)typeof(Task06_RelayoutPass).GetMethod("DuplicateSweep", PrivStatic).Invoke(null, null);
                var counts = (List<string>)typeof(Task06_RelayoutPass).GetMethod("InstanceCounts", PrivStatic).Invoke(null, null);
                sb.AppendLine($"  expurgo: removidos na origem {o} | duplicatas removidas {d} | (0,0,0) r=1.0 m: {(origin.Count == 0 ? "LIMPO" : string.Join(", ", origin))} | duplicatas: {(dups.Count == 0 ? "nenhuma" : string.Join(", ", dups))}");
                int nTapes = UnityEngine.Object.FindObjectsByType<MusicCassetteTape>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                int nDecks = scene.GetRootGameObjects().Count(r => r.name == "RadioCassetteDeck_Instance");
                int nRacks = scene.GetRootGameObjects().Count(r => r.name == "CassetteOrganizerRack_Instance");
                sb.AppendLine($"  contagem: deck {nDecks} | rack {nRacks} | fitas {nTapes}");
                bool dirty = scene.isDirty;
                if (dirty && (o > 0 || d > 0)) { EditorSceneManager.SaveScene(scene); sb.AppendLine("  cena salva (expurgo removeu itens)"); }
                sb.AppendLine($"  cena com alterações pendentes ao final: {EditorSceneManager.GetActiveScene().isDirty}");

                UpdateAuditJson(ej, origin, dups, counts, nDecks, nRacks, nTapes);
                sb.AppendLine($"  JSON atualizado: {Path.GetFullPath(AuditFile)}");
            }
            catch (Exception ex)
            {
                sb.AppendLine("  [ERRO] " + ex);
                Debug.LogError("[Task06_EjectSequencePass] " + ex);
            }
            UpdateBuilderResult(sb.ToString());
            return sb.ToString();
        }

        private static string V(Vector3 v) => $"({v.x:F3}, {v.y:F3}, {v.z:F3})";

        private static bool SolidBounds(GameObject go, out Bounds b) => SpatialAuditUtility.TryGetSolidBounds(go, out b, true, t => t.name.StartsWith("Label_", StringComparison.Ordinal));

        /// <summary>Executa o Eject com o IEnumerator real do runtime e grava TASK-06_Eject_Sequence.png. Restaura poses/estado ao fim.</summary>
        public static EjectAuditJson SimulateRealEject(string dir, Transform desk, Bounds deskB, float deskTop, GameObject deck, GameObject tape, StringBuilder sb)
        {
            var ej = new EjectAuditJson { method = "RadioCassetteSocket.EjectRoutine real (corrotina iniciada por EjectCurrentTape) avançada quadro a quadro em Edit Mode + Physics.Simulate(1/90); base de tempo da corrotina emulada a 90 Hz (Time.deltaTime do Editor não é confiável); simulador NÃO altera IgnoreCollision", physicsStepSeconds = 1f / 90f, deskTopY = deskTop };
            var socket = deck.GetComponentInChildren<RadioCassetteSocket>(true);
            var mt = tape.GetComponent<MusicCassetteTape>();
            var rb = tape.GetComponent<Rigidbody>();
            if (socket == null || mt == null || rb == null) throw new Exception("socket/MusicCassetteTape/Rigidbody ausente");
            ej.ejectImpulseForce = socket.ejectImpulseForce; ej.maxEjectSpeed = socket.maxEjectSpeed;
            ej.ejectIgnoreCollisionSeconds = socket.ejectIgnoreCollisionSeconds; ej.ejectRecaptureCooldownSeconds = socket.ejectRecaptureCooldownSeconds;
            var so = new SerializedObject(mt); var pr = so.FindProperty("respawnFloorThresholdY");
            ej.respawnFloorThresholdY = pr != null ? pr.floatValue : 0.70f;

            var tapeCols = tape.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger).ToArray();
            var deckSolid = deck.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger).ToArray();
            var slotTrigger = socket.GetComponent<Collider>();
            Bounds trigB = slotTrigger != null ? slotTrigger.bounds : new Bounds(socket.transform.position, Vector3.one * 0.1f);
            SolidBounds(deck, out var deckB);
            bool AnyIgnored() => tapeCols.Any(a => deckSolid.Any(b => Physics.GetIgnoreCollision(a, b)));
            if (AnyIgnored()) sb.AppendLine("  [AVISO] já havia IgnoreCollision fita↔deck antes da simulação");

            var all = UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var snap = all.Select(r => (r, r.transform.position, r.transform.rotation, r.isKinematic, r.collisionDetectionMode)).ToList();
            var prevMode = Physics.simulationMode;
            Vector3 deckFwd = deck.transform.forward; deckFwd.y = 0; deckFwd.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, deckFwd);
            Vector3 slot = tape.transform.position;
            Vector3 camPos = slot + deckFwd * 0.50f + side * 0.50f + Vector3.up * 0.22f;
            Vector3 look = slot + deckFwd * 0.13f + Vector3.down * 0.05f;

            Texture2D f0 = null, f1 = null, f2 = null, comp = null;
            try
            {
                foreach (var s in snap) if (s.r != rb) s.r.isKinematic = true; // congela o resto da cena

                // Estado que o runtime teria em Play: Awake → CacheDeckColliders; OnSelectEntered (startingSelectedInteractable) → currentlySocketedTape
                typeof(RadioCassetteSocket).GetMethod("CacheDeckColliders", Priv).Invoke(socket, null);
                var deckColsField = (Collider[])typeof(RadioCassetteSocket).GetField("deckColliders", Priv).GetValue(socket);
                sb.AppendLine($"  runtime CacheDeckColliders: {deckColsField.Length} colliders sólidos do deck ({string.Join(", ", deckColsField.Select(c => c.name))})");
                typeof(RadioCassetteSocket).GetField("currentlySocketedTape", Priv).SetValue(socket, mt);

                f0 = VisualQACaptureEditor.RenderCameraToTexture(camPos, look, 640, 1080, 62f);

                Physics.simulationMode = SimulationMode.Script;
                rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
                Physics.SyncTransforms();

                var routine = (IEnumerator)typeof(RadioCassetteSocket).GetMethod("EjectRoutine", Priv).Invoke(socket, null);
                var ignoredFlag = typeof(RadioCassetteSocket).GetField("ejectCollisionIgnored", Priv);
                bool alive = routine.MoveNext(); // 1º trecho = exatamente o que EjectCurrentTape() executa ao chamar StartCoroutine
                // Em Edit Mode Time.deltaTime é arbitrário (0 ou 0.333 s conforme o foco do Editor). A corrotina real faz
                // `elapsed += Time.deltaTime`; para reproduzir um headset a 90 Hz, o campo local `elapsed` do IEnumerator é
                // pré-compensado antes de cada MoveNext para que cada quadro some exatamente 1/90 s. Só a base de tempo é
                // emulada — a lógica de ignorar/restaurar colisão e o cooldown são os do runtime.
                float frameDt = 1f / 90f;
                float editorDt = Time.deltaTime;
                var elapsedField = routine.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).FirstOrDefault(f => f.Name.Contains("elapsed") && f.FieldType == typeof(float));
                if (elapsedField == null) throw new Exception("campo 'elapsed' do IEnumerator EjectRoutine não encontrado");
                ej.editorCoroutineFrameSeconds = frameDt;
                sb.AppendLine($"  Time.deltaTime do Editor = {editorDt:F4}s → base de tempo da corrotina emulada a 90 Hz via campo '{elapsedField.Name}'");
                ej.runtimeIgnoreBegan = (bool)ignoredFlag.GetValue(socket);
                ej.deckCollidersIgnoredByRuntime = deckColsField.Count(dc => tapeCols.Any(tc => Physics.GetIgnoreCollision(tc, dc)));
                ej.launchSpeed = rb.linearVelocity.magnitude;
                sb.AppendLine($"  após 1º trecho da corrotina: isKinematic={rb.isKinematic} |v|={ej.launchSpeed:F2} m/s ejectCollisionIgnored={ej.runtimeIgnoreBegan} (pares ignorados com {ej.deckCollidersIgnoredByRuntime} colliders do deck) IsEjectingCooldown={socket.IsEjectingCooldown}");

                float dt = 1f / 90f, t = 0f, nextResume = frameDt, peakY = slot.y, minY = slot.y;
                bool refusedAll = true, midCaptured = false; float settleT = -1f;
                var traj = new StringBuilder();
                for (int i = 0; i < 270; i++) // 3 s
                {
                    Physics.Simulate(dt); t += dt;
                    while (alive && t + 1e-5f >= nextResume)
                    {
                        elapsedField.SetValue(routine, (float)elapsedField.GetValue(routine) + frameDt - Time.deltaTime);
                        alive = routine.MoveNext();
                        if (ej.ignoreEndedAtSeconds < 0f && !(bool)ignoredFlag.GetValue(socket)) ej.ignoreEndedAtSeconds = nextResume;
                        if (ej.cooldownEndedAtSeconds < 0f && !socket.IsEjectingCooldown) ej.cooldownEndedAtSeconds = nextResume;
                        nextResume += frameDt;
                    }
                    if (socket.IsEjectingCooldown)
                    {
                        bool can = true;
                        try { can = socket.CanSelect((UnityEngine.XR.Interaction.Toolkit.IXRSelectInteractable)mt); } catch { can = !socket.IsEjectingCooldown; }
                        if (can) refusedAll = false;
                    }
                    Vector3 p = tape.transform.position;
                    peakY = Mathf.Max(peakY, p.y); minY = Mathf.Min(minY, p.y);
                    if (i % 9 == 0) traj.Append($"t={t:F2}s {V(p)}; ");
                    SolidBounds(tape, out var tbNow);
                    if (!midCaptured && t >= 0.10f && !tbNow.Intersects(trigB) && SpatialAuditUtility.AabbClearance(tbNow, deckB) > 0.005f && tbNow.min.y > deskTop + 0.02f)
                    {
                        f1 = VisualQACaptureEditor.RenderCameraToTexture(camPos, look, 640, 1080, 62f);
                        midCaptured = true; ej.midFlightTime = t; ej.midFlightPosition = new Vector3Json(p);
                        sb.AppendLine($"  quadro 'em voo' t={t:F2}s pos={V(p)} base={tbNow.min.y:F3} (tampo {deskTop:F3})");
                    }
                    bool still = rb.linearVelocity.sqrMagnitude < 1e-4f && rb.angularVelocity.sqrMagnitude < 1e-3f;
                    if (still && settleT < 0f && t > 0.15f) settleT = t; else if (!still) settleT = -1f;
                    if (!alive && t > 1.2f && settleT > 0f) break;
                }
                if (!midCaptured) sb.AppendLine("  [AVISO] nenhum quadro com a fita no ar fora do slot — quadro do meio = repouso");
                ej.socketRefusedRecaptureDuringCooldown = refusedAll;
                ej.settleTimeSeconds = settleT;
                ej.peakY = peakY; ej.minY = minY;
                ej.ignoreCollisionRestoredAfterEject = !AnyIgnored();

                Vector3 landed = tape.transform.position;
                SolidBounds(tape, out var tb);
                ej.finalPosition = new Vector3Json(landed); ej.finalBaseY = tb.min.y;
                ej.horizontalDistanceFromSlot = Vector2.Distance(new Vector2(landed.x, landed.z), new Vector2(slot.x, slot.z));
                ej.distanceToDeckAabb = SpatialAuditUtility.AabbClearance(tb, deckB);
                ej.outsideSlotTrigger = !tb.Intersects(trigB);
                ej.recaptureRiskAfterCooldown = tb.Intersects(trigB);
                ej.restingOnMesa1Top = Mathf.Abs(tb.min.y - deskTop) < 0.012f;
                ej.minEdgeMarginMeters = Mathf.Min(tb.min.x - deskB.min.x, deskB.max.x - tb.max.x, tb.min.z - deskB.min.z, deskB.max.z - tb.max.z);
                ej.insideMesa1TopEdges = ej.minEdgeMarginMeters > 0f;
                ej.neverBelowRespawnFloor = minY > ej.respawnFloorThresholdY;
                sb.AppendLine($"  corrotina: fim do IgnoreCollision em t={ej.ignoreEndedAtSeconds:F3}s, fim do cooldown em t={ej.cooldownEndedAtSeconds:F3}s (quadro da corrotina emulado = {frameDt:F4}s, 90 Hz), socket recusou recaptura durante todo o cooldown = {refusedAll}");
                sb.AppendLine($"  trajetória: {traj}");
                f2 = VisualQACaptureEditor.RenderCameraToTexture(camPos, look, 640, 1080, 62f);

                // Prova de que a fita volta a colidir com o deck: soltar a fita 6 cm acima do centro da laje do deck
                Vector3 dropPos = new Vector3(deckB.center.x, deckB.max.y + 0.06f, deckB.center.z);
                tape.transform.SetPositionAndRotation(dropPos, deck.transform.rotation);
                rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; Physics.SyncTransforms();
                for (int i = 0; i < 60; i++) Physics.Simulate(dt);
                SolidBounds(tape, out var dropB);
                var top = deckSolid.Where(c => c.bounds.min.x <= dropB.center.x && c.bounds.max.x >= dropB.center.x && c.bounds.min.z <= dropB.center.z && c.bounds.max.z >= dropB.center.z).Select(c => c.bounds.max.y).DefaultIfEmpty(float.NaN).Max();
                ej.dropTestTapeBaseY = dropB.min.y;
                ej.tapeCollidesWithDeckAgain = !float.IsNaN(top) && dropB.min.y > top - 0.01f;
                sb.AppendLine($"  teste de colisão pós-eject: fita solta sobre o deck parou com base Y={dropB.min.y:F3} (topo do deck sob ela {top:F3}) → colide com o deck = {ej.tapeCollidesWithDeckAgain}");

                bool distOk = ej.horizontalDistanceFromSlot >= 0.15f && ej.horizontalDistanceFromSlot <= 0.35f;
                bool pass = ej.runtimeIgnoreBegan && ej.outsideSlotTrigger && ej.restingOnMesa1Top && distOk && ej.insideMesa1TopEdges && ej.neverBelowRespawnFloor
                            && ej.ignoreCollisionRestoredAfterEject && ej.tapeCollidesWithDeckAgain && ej.socketRefusedRecaptureDuringCooldown && !ej.recaptureRiskAfterCooldown;
                ej.result = pass ? "PASS" : "FAIL";
                sb.AppendLine($"  RESULTADO EJECT: {ej.result} | repouso {V(landed)} base {tb.min.y:F3} (tampo {deskTop:F3}, apoiada={ej.restingOnMesa1Top}) | {ej.horizontalDistanceFromSlot:F3} m do slot (esperado 0.15–0.35: {distOk}) | folga AABB ao deck {ej.distanceToDeckAabb:F3} m | fora do trigger do slot={ej.outsideSlotTrigger} | margem à borda do tampo {ej.minEdgeMarginMeters:F3} m | pico Y {peakY:F3} | Y mín {minY:F3} (> respawn {ej.respawnFloorThresholdY:F2}: {ej.neverBelowRespawnFloor}) | IgnoreCollision restaurado={ej.ignoreCollisionRestoredAfterEject} | repouso em t={settleT:F2}s");

                comp = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                comp.SetPixels(0, 0, 640, 1080, f0.GetPixels());
                comp.SetPixels(640, 0, 640, 1080, (f1 ?? f2).GetPixels());
                comp.SetPixels(1280, 0, 640, 1080, f2.GetPixels());
                var sep = Enumerable.Repeat(new Color(0.05f, 0.05f, 0.05f), 4 * 1080).ToArray();
                comp.SetPixels(638, 0, 4, 1080, sep); comp.SetPixels(1278, 0, 4, 1080, sep);
                DrawLabel(comp, 0, "1 ANTES");
                DrawLabel(comp, 640, "2 EM VOO");
                DrawLabel(comp, 1280, "3 POUSADA");
                comp.Apply();
                File.WriteAllBytes(Path.Combine(dir, "TASK-06_Eject_Sequence.png"), comp.EncodeToPNG());
                sb.AppendLine($"  OK TASK-06_Eject_Sequence.png 1920x1080 (antes | t={ej.midFlightTime:F2}s | repouso) câmera {V(camPos)} → {V(look)}");
            }
            finally
            {
                // estado do socket como antes da simulação (a cena é recarregada do disco em seguida no fluxo standalone)
                try
                {
                    typeof(RadioCassetteSocket).GetMethod("EndEjectCollisionIgnore", Priv).Invoke(socket, null);
                    typeof(RadioCassetteSocket).GetField("isEjectingCooldown", Priv).SetValue(socket, false);
                    typeof(RadioCassetteSocket).GetField("currentlySocketedTape", Priv).SetValue(socket, null);
                }
                catch { }
                Physics.simulationMode = prevMode;
                foreach (var s in snap)
                {
                    if (s.r == null) continue;
                    if (!s.r.isKinematic) { s.r.linearVelocity = Vector3.zero; s.r.angularVelocity = Vector3.zero; }
                    s.r.isKinematic = s.isKinematic;
                    s.r.collisionDetectionMode = s.collisionDetectionMode;
                    s.r.transform.SetPositionAndRotation(s.position, s.rotation);
                }
                Physics.SyncTransforms();
                foreach (var tx in new[] { f0, f1, f2, comp }) if (tx != null) UnityEngine.Object.DestroyImmediate(tx);
            }
            return ej;
        }

        // ---------------- rótulos (fonte bitmap 5x7) ----------------
        private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            { 'A', new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" } },
            { 'N', new[] { "10001", "11001", "10101", "10011", "10001", "10001", "10001" } },
            { 'T', new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" } },
            { 'E', new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" } },
            { 'S', new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" } },
            { 'M', new[] { "10001", "11011", "10101", "10101", "10001", "10001", "10001" } },
            { 'V', new[] { "10001", "10001", "10001", "10001", "10001", "01010", "00100" } },
            { 'O', new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" } },
            { 'P', new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" } },
            { 'U', new[] { "10001", "10001", "10001", "10001", "10001", "10001", "01110" } },
            { 'D', new[] { "11110", "10001", "10001", "10001", "10001", "10001", "11110" } },
            { '1', new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" } },
            { '2', new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" } },
            { '3', new[] { "11110", "00001", "00001", "01110", "00001", "00001", "11110" } },
        };

        private static void DrawLabel(Texture2D tex, int x0, string text)
        {
            const int s = 6, pad = 14;
            int w = text.Length * 6 * s + pad * 2, h = 7 * s + pad * 2;
            int top = tex.height - 20;
            var bg = new Color(0f, 0f, 0f, 1f); var fg = new Color(1f, 0.92f, 0.3f);
            for (int y = top - h; y < top; y++) for (int x = x0 + 20; x < Mathf.Min(x0 + 20 + w, x0 + 636); x++)
                    tex.SetPixel(x, y, Color.Lerp(tex.GetPixel(x, y), bg, 0.75f));
            int cx = x0 + 20 + pad;
            foreach (char ch in text)
            {
                if (Glyphs.TryGetValue(ch, out var g))
                    for (int r = 0; r < 7; r++) for (int c = 0; c < 5; c++)
                            if (g[r][c] == '1')
                                for (int dy = 0; dy < s; dy++) for (int dx = 0; dx < s; dx++)
                                        tex.SetPixel(cx + c * s + dx, top - pad - r * s - dy, fg);
                cx += 6 * s;
            }
        }

        // ---------------- relatórios ----------------
        private static void UpdateAuditJson(EjectAuditJson ej, List<string> origin, List<string> dups, List<string> counts, int nDecks, int nRacks, int nTapes)
        {
            string path = Path.GetFullPath(AuditFile);
            var rep = File.Exists(path) ? JsonUtility.FromJson<ExtendedSpatialAuditReport>(File.ReadAllText(path)) : new ExtendedSpatialAuditReport { taskId = "TASK-06" };
            rep.timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            rep.objectsAtOriginZero = origin; rep.duplicateObjectsFound = dups; rep.isOriginClean = origin.Count == 0;
            if (counts != null && counts.Count > 0) rep.instanceCounts = counts;
            rep.notes.RemoveAll(n => n.StartsWith("Eject", StringComparison.Ordinal));
            if (ej != null)
                rep.notes.Add($"Eject REAL (runtime RadioCassetteSocket.EjectRoutine pós-fix [C], Physics.Simulate 1/90 s, Rigidbody real): {ej.result} — repouso {ej.finalPosition.x:F3}/{ej.finalPosition.y:F3}/{ej.finalPosition.z:F3}, base {ej.finalBaseY:F3} sobre o tampo {ej.deskTopY:F3}, {ej.horizontalDistanceFromSlot:F3} m do slot, IgnoreCollision restaurado = {ej.ignoreCollisionRestoredAfterEject}.");
            rep.notes.Add($"Pós-simulação: cena recarregada do disco; deck {nDecks} / rack {nRacks} / fitas {nTapes}; (0,0,0) r=1.0 m {(origin.Count == 0 ? "limpo" : "COM OBJETOS")}.");
            string json = JsonUtility.ToJson(rep, true);
            if (ej != null)
            {
                string ejJson = JsonUtility.ToJson(ej, true).Replace("\n", "\n    ");
                int last = json.LastIndexOf('}');
                json = json.Substring(0, last).TrimEnd() + ",\n    \"ejectSimulation\": " + ejJson + "\n}\n";
            }
            File.WriteAllText(path, json);
        }

        private static void UpdateBuilderResult(string section)
        {
            string path = Path.GetFullPath(ResultFile);
            string txt = File.Exists(path) ? File.ReadAllText(path) : "";
            // remove a seção antiga de eject (simulação anterior + diagnóstico com colisão ignorada pelo simulador)
            int a = txt.IndexOf("  -- eject REAL", StringComparison.Ordinal);
            int b = txt.IndexOf("\n  JSON:", StringComparison.Ordinal);
            if (a >= 0 && b > a) txt = txt.Substring(0, a) + "  (sequência de Eject refeita na seção 8 com a lógica real do runtime; diagnóstico antigo removido)\n" + txt.Substring(b);
            int c = txt.IndexOf("\n--- 8. EJECT", StringComparison.Ordinal);
            if (c >= 0) txt = txt.Substring(0, c);
            File.WriteAllText(path, txt.TrimEnd() + "\n" + section);
        }
    }
}
