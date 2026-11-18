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
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;
using VRRehab.Core.Testing;
using VRRehab.Ergonomics;
using VRRehab.Interactive.Task11;
using VRRehab.Interactive.Task12;
using VRRehab.Interactive.Task14;
using VRRehab.Interactive.Task15;
using Object = UnityEngine.Object;

namespace VRRehab.EditorTools
{
    /// <summary>
    /// Testes em PLAY MODE de verdade da Floresta_e_Rio (Tasks 11, 12, 14 e 15): Awake/Start, corrotinas, física e
    /// eventos do XRI rodando como no jogo. Uma "mão de teste" (ScriptedTestInteractor) pega, balança e solta os objetos.
    /// Nada é salvo: tudo que acontece no Play Mode é descartado ao sair.
    /// Batch: unity run . -- -executeMethod VRRehab.EditorTools.ForestPlayModeTests.Run
    /// Relatório: .agents/visual_qa/FOREST_PlayModeTest.txt
    /// </summary>
    [InitializeOnLoad]
    public static class ForestPlayModeTests
    {
        private const string StateKey = "ForestPlayModeTests.state"; // 1 = rodar ao entrar no Play, 2 = terminou
        private const string FailKey = "ForestPlayModeTests.fail";
        private const string ReportPath = ".agents/visual_qa/FOREST_PlayModeTest.txt";
        private const float TimeoutSeconds = 300f;

        // Mini executor de corrotinas no EditorApplication.update:
        //   yield return null        -> espera o PRÓXIMO QUADRO do jogo (o update do editor roda várias vezes por quadro)
        //   yield return <float>     -> espera N segundos de jogo
        //   yield return IEnumerator -> roda a sub-rotina até o fim (pilha)
        private static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        private static IEnumerator routine;
        private static float waitUntil;
        private static int waitFrame = -1;
        private static double startRealTime;
        private static StringBuilder log;
        private static int pass, fail;

        static ForestPlayModeTests()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("ExoTao IC/Floresta/Testes em Play Mode (Tasks 11-15)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) return;
            EditorSceneManager.OpenScene(ForestAuditTool.ForestScene, OpenSceneMode.Single);
            SessionState.SetInt(StateKey, 1);
            routine = null;
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            if (SessionState.GetInt(StateKey, 0) != 2) return;
            SessionState.EraseInt(StateKey);
            int failures = SessionState.GetInt(FailKey, 1);
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static void Tick()
        {
            if (SessionState.GetInt(StateKey, 0) != 1 || !EditorApplication.isPlaying) return;

            if (routine == null)
            {
                log = new StringBuilder();
                pass = fail = 0;
                startRealTime = EditorApplication.timeSinceStartup;
                waitUntil = 0f;
                waitFrame = -1;
                routine = Tests();
                stack.Clear();
                stack.Push(routine);
            }

            if (EditorApplication.timeSinceStartup - startRealTime > TimeoutSeconds)
            {
                Fail("TEMPO ESGOTADO", $"o teste passou de {TimeoutSeconds} s");
                Finish();
                return;
            }
            if (Time.time < waitUntil) return;
            if (Time.frameCount <= waitFrame) return;

            try
            {
                // Avança até a rotina pedir uma espera (quadro ou segundos)
                while (stack.Count > 0)
                {
                    IEnumerator top = stack.Peek();
                    if (!top.MoveNext()) { stack.Pop(); continue; }
                    object cur = top.Current;
                    if (cur is IEnumerator sub) { stack.Push(sub); continue; }
                    if (cur is float f) { waitUntil = Time.time + f; waitFrame = -1; }
                    else { waitUntil = 0f; waitFrame = Time.frameCount; }
                    return;
                }
                Finish();
            }
            catch (Exception e)
            {
                Fail("EXCEÇÃO no teste", e.ToString());
                Finish();
            }
        }

        private static void Finish()
        {
            log.AppendLine();
            log.AppendLine($"--- RESULTADO: {pass} passaram, {fail} falharam ---");
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, log.ToString());
            Debug.Log("[ForestPlayModeTests]\n" + log);
            SessionState.SetInt(FailKey, fail);
            SessionState.SetInt(StateKey, 2);
            routine = null;
            stack.Clear();
            EditorApplication.ExitPlaymode();
        }

        // ------------------------------------------------------------------ helpers

        private static void Check(string name, bool ok, string detail)
        {
            if (ok) pass++; else fail++;
            log.AppendLine($"[{(ok ? "PASS" : "FAIL")}] {name}: {detail}");
        }

        private static void Fail(string name, string detail) => Check(name, false, detail);
        private static void Info(string text) => log.AppendLine("      " + text);

        private static object GetField(object obj, string name)
        {
            for (Type t = obj.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null) return f.GetValue(obj);
            }
            return null;
        }

        private static float GroundY(Vector3 p, GameObject self)
        {
            Physics.SyncTransforms();
            return ForestAuditTool.TryGroundHeight(self, p, out float g, out _) ? g : float.NaN;
        }

        /// <summary>Distância da base do objeto até o chão (negativo = enterrado).</summary>
        private static float GroundGap(GameObject go)
        {
            Bounds b = ForestAuditTool.RendererBounds(go, out bool has);
            float baseY = has ? b.min.y : go.transform.position.y;
            return baseY - GroundY(go.transform.position, go);
        }

        private static ScriptedTestInteractor MakeHand(Vector3 pos)
        {
            var go = new GameObject("~TestHand");
            go.transform.position = pos;
            return go.AddComponent<ScriptedTestInteractor>();
        }

        private static XRInteractionManager Manager(ScriptedTestInteractor hand)
        {
            return hand.interactionManager != null ? hand.interactionManager : Object.FindFirstObjectByType<XRInteractionManager>();
        }

        private static void Grab(ScriptedTestInteractor hand, IXRSelectInteractable target)
        {
            Manager(hand).SelectEnter((IXRSelectInteractor)hand, target);
        }

        private static void Release(ScriptedTestInteractor hand, IXRSelectInteractable target)
        {
            Manager(hand).SelectExit((IXRSelectInteractor)hand, target);
        }

        /// <summary>Move a mão em linha reta até 'to' com velocidade constante, um passo por quadro.</summary>
        private static IEnumerator MoveHand(Transform hand, Vector3 to, float speed)
        {
            while ((hand.position - to).sqrMagnitude > 0.0001f)
            {
                hand.position = Vector3.MoveTowards(hand.position, to, speed * Mathf.Max(Time.deltaTime, 0.005f));
                yield return null;
            }
        }

        private static Vector3 Vel(Rigidbody rb) => rb.linearVelocity;

        private static string V(Vector3 v) => ForestAuditTool.V(v);

        private static QuestSnapshot Snap()
        {
            var q = SessionQuestManager.Instance;
            var c = CoinScoreManager.Instance;
            return new QuestSnapshot
            {
                gait = q != null ? q.QuestGait.currentCount : -1,
                friend = q != null ? q.QuestForestFriend.currentCount : -1,
                friendDone = q != null && q.QuestForestFriend.isCompleted,
                botany = q != null ? q.QuestBotany.currentCount : -1,
                botanyDone = q != null && q.QuestBotany.isCompleted,
                coins = c != null ? c.CoinsCollected : -1,
                score = c != null ? c.CurrentScore : -1,
            };
        }

        private struct QuestSnapshot
        {
            public int gait, friend, botany, coins, score;
            public bool friendDone, botanyDone;
            public override string ToString() =>
                $"marcha {gait}, amigo {friend}{(friendDone ? " (ok)" : "")}, botânica {botany}{(botanyDone ? " (ok)" : "")}, moedas {coins}, placar {score}";
        }

        private static List<GameObject> FallenApples() =>
            Object.FindObjectsByType<InteractiveApple>(FindObjectsSortMode.None)
                  .Where(a => a.name.StartsWith("InteractiveRedApple_") || a.name.StartsWith("GoldenBonusApple_"))
                  .Select(a => a.gameObject).ToList();

        // ------------------------------------------------------------------ testes

        private static IEnumerator Tests()
        {
            log.AppendLine($"=== Testes em Play Mode da Floresta_e_Rio ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");
            log.AppendLine("Cada teste roda no Play Mode real (Awake/Start, corrotinas, física, eventos do XRI). Uma 'mão de teste' do XRI pega e solta os objetos.");
            log.AppendLine();

            // Ritmo de quadros de óculos VR (sem isso o batchmode roda a milhares de FPS)
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 90;
            log.AppendLine($"Física: passo fixo {Time.fixedDeltaTime:0.0000} s, alvo {Application.targetFrameRate} FPS");

            yield return 2.5f; // deixa Start/OnEnable rodarem e a física assentar

            // ---------------- 0. Estado inicial ----------------
            log.AppendLine("## 0. Estado inicial (2,5 s depois do Play)");
            var quest = SessionQuestManager.Instance;
            Check("0a) SessionQuestManager ativo", quest != null, quest != null ? ForestAuditTool.PathOf(quest.transform) : "não encontrado");
            QuestSnapshot s0 = Snap();
            Info("missões: " + s0);
            Check("0b) 'Magia Botânica' NÃO completa sozinha ao dar Play", !s0.botanyDone && s0.botany == 0,
                $"botânica {s0.botany}, completa={s0.botanyDone} (antes os 2 canteiros de demonstração completavam a missão no Start)");
            Check("0c) 'Amigo da Floresta' começa em 0", s0.friend == 0 && !s0.friendDone, $"amigo {s0.friend}");

            var baskets = Object.FindObjectsByType<AppleCollectionBasket>(FindObjectsSortMode.None).OrderBy(b => b.name).ToArray();
            int scoredAtStart = baskets.Sum(b => b.ApplesScoredCount);
            GameObject decoApple = baskets.Select(b => b.transform.Find("Apple_In_Basket")).Where(t => t != null).Select(t => t.gameObject).FirstOrDefault();
            yield return 3.5f; // a cesta antiga encolhia e apagava a maçã decorativa 2,5 s depois de "pontuar"
            Check("0d) maçã decorativa dentro da cesta não pontua nem some sozinha",
                scoredAtStart == 0 && baskets.Sum(b => b.ApplesScoredCount) == 0 && decoApple != null,
                $"cestas pontuadas no início={scoredAtStart}, depois de 6 s={baskets.Sum(b => b.ApplesScoredCount)}, Apple_In_Basket existe={decoApple != null}");

            // Painel de missões no drone
            object uiGait = quest != null ? GetField(quest, "uiQuestGait") : null;
            object uiFriend = quest != null ? GetField(quest, "uiQuestForestFriend") : null;
            object uiBotany = quest != null ? GetField(quest, "uiQuestBotany") : null;
            var texts = new List<string>();
            bool uiOk = true;
            foreach (object ui in new[] { uiGait, uiFriend, uiBotany })
            {
                var title = ui != null ? GetField(ui, "txtTitle") as TMPro.TMP_Text : null;
                var status = ui != null ? GetField(ui, "txtStatus") as TMPro.TMP_Text : null;
                var bar = ui != null ? GetField(ui, "imgFillBar") as UnityEngine.UI.Image : null;
                uiOk &= title != null && status != null && bar != null && title.gameObject.activeInHierarchy;
                texts.Add(title != null ? $"'{title.text}' {status?.text} barra={bar?.fillAmount:0.00}" : "(sem UI)");
            }
            Check("0e) painel de missões do drone ligado e visível", uiOk, string.Join(" | ", texts));
            CaptureQuestPanel(uiGait, ".agents/visual_qa/FOREST_quest_panel_inicio.png");

            // ---------------- 1. Sacudir árvore ----------------
            log.AppendLine();
            log.AppendLine("## 1. Árvore sacudível (gatilho do laser na copa)");
            var trees = Object.FindObjectsByType<ShakeableFruitTree>(FindObjectsSortMode.None).OrderBy(t => t.name).ToArray();
            Check("1a) 3 árvores sacudíveis na cena", trees.Length == 3, $"{trees.Length} árvores");
            foreach (ShakeableFruitTree t in trees)
                foreach (Collider c in t.GetComponents<Collider>())
                    Info($"{t.name}: colisor {c.GetType().Name} trigger={c.isTrigger} bounds={V(c.bounds.center)} ext={V(c.bounds.extents)}");

            ShakeableFruitTree tree = trees.FirstOrDefault();
            if (tree != null)
            {
                var treeInteractable = tree.GetComponent<XRSimpleInteractable>();
                ScriptedTestInteractor hand = MakeHand(tree.transform.position + new Vector3(0f, 1.2f, -3f));
                yield return null;
                int before = FallenApples().Count;
                Grab(hand, treeInteractable);
                yield return 0.2f;
                Release(hand, treeInteractable);
                bool cooldownBlocks = !tree.TryShakeTree();
                yield return 4.0f;
                List<GameObject> apples = FallenApples();
                int spawned = apples.Count - before;
                Check("1b) gatilho na árvore derruba 2 a 4 maçãs", spawned >= 2 && spawned <= 4, $"{spawned} maçãs");
                Check("1c) cooldown impede sacudir de novo na hora", cooldownBlocks, $"segunda sacudida imediata bloqueada={cooldownBlocks}");
                var gaps = apples.Select(a => GroundGap(a)).ToList();
                var speeds = apples.Select(a => Vel(a.GetComponent<Rigidbody>()).magnitude).ToList();
                bool allGrounded = apples.Count > 0 && gaps.All(g => g > -0.06f && g < 0.12f);
                Check("1d) maçãs caídas param no chão (nem presas na copa, nem enterradas)", allGrounded,
                    string.Join(", ", apples.Select((a, i) => $"{a.name} gap {gaps[i]:+0.00;-0.00} m vel {speeds[i]:0.00}")));
                bool insideMap = apples.All(a => TerrainGroundUtil.TryGetHeight(a.transform.position, out _));
                float maxDist = apples.Count > 0 ? apples.Max(a => Vector3.ProjectOnPlane(a.transform.position - tree.transform.position, Vector3.up).magnitude) : 0f;
                Check("1e) maçãs ficam dentro do mapa, perto da árvore", insideMap && maxDist < 4.5f, $"dentro do mapa={insideMap}, mais longe a {maxDist:0.00} m do tronco");
                var homes = apples.Select(a => a.GetComponent<ErgonomicGrabInteractable>()).Where(e => e != null).Select(e => e.HomePosition.y - GroundY(e.HomePosition, e.gameObject)).ToList();
                Check("1f) 'casa' da maçã derrubada fica no chão (não no meio da copa)", homes.Count > 0 && homes.All(h => h < 0.2f),
                    "altura da casa acima do chão: " + string.Join(", ", homes.Select(h => h.ToString("0.00"))));
                Object.Destroy(hand.gameObject);

                // Pool de 12 maçãs
                for (int round = 0; round < 4; round++)
                {
                    foreach (ShakeableFruitTree t in trees) t.TryShakeTree();
                    yield return 3.7f;
                }
                yield return 1.0f;
                int live = FallenApples().Count;
                Check("1g) limite de 12 maçãs derrubadas na cena (4 rodadas x 3 árvores)", live <= ShakeableFruitTree.MaxFallenApplesInScene && live >= 10, $"{live} maçãs vivas");
            }

            // ---------------- 2. Pegar e arremessar maçã ----------------
            log.AppendLine();
            log.AppendLine("## 2. Pegar a maçã e arremessar balançando");
            GameObject throwApple = FallenApples().OrderBy(a => a.name).FirstOrDefault();
            if (throwApple != null && tree != null)
            {
                var grab = throwApple.GetComponent<InteractiveApple>();
                var rb = throwApple.GetComponent<Rigidbody>();
                Vector3 start = throwApple.transform.position + Vector3.up * 1.0f;
                // Joga para longe do tronco, no plano
                Vector3 dir = Vector3.ProjectOnPlane(throwApple.transform.position - tree.transform.position, Vector3.up);
                if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
                dir = (dir.normalized + Vector3.up * 0.35f).normalized;

                ScriptedTestInteractor hand = MakeHand(throwApple.transform.position);
                yield return null;
                Grab(hand, grab);
                yield return null;
                Check("2a) laser/mão pega a maçã", grab.isSelected, $"selecionada={grab.isSelected}, rb cinemático={rb.isKinematic}");
                yield return MoveHand(hand.transform, start, 1.5f);
                yield return 0.6f;
                float followErr = (throwApple.transform.position - hand.transform.position).magnitude;
                Check("2b) maçã acompanha a mão enquanto segurada", followErr < 0.15f, $"distância maçã-mão {followErr:0.000} m");

                const float swingSpeed = 3.0f;
                Vector3 swingEnd = start + dir * 0.75f;
                var trace = new StringBuilder();
                while ((hand.transform.position - swingEnd).sqrMagnitude > 0.0001f)
                {
                    hand.transform.position = Vector3.MoveTowards(hand.transform.position, swingEnd, swingSpeed * Mathf.Max(Time.deltaTime, 0.005f));
                    trace.AppendLine($"        f{Time.frameCount} t={Time.time:0.000} mão {V(hand.transform.position)} maçã {V(throwApple.transform.position)} vel {V(Vel(rb))} kin={rb.isKinematic} sel={grab.isSelected}");
                    yield return null;
                }
                Vector3 releasePos = throwApple.transform.position;
                Release(hand, grab);
                trace.AppendLine($"        SOLTOU f{Time.frameCount} vel imediata {V(Vel(rb))} kin={rb.isKinematic} sel={grab.isSelected}");
                Vector3 v0 = Vel(rb);
                for (int k = 0; k < 4; k++)
                {
                    yield return null;
                    trace.AppendLine($"        +{k + 1} f{Time.frameCount} vel {V(Vel(rb))} pos {V(throwApple.transform.position)} kin={rb.isKinematic}");
                    if (k == 0 && v0.sqrMagnitude < 0.0001f) v0 = Vel(rb);
                }
                Info("rastro do balanço:\n" + trace);
                float cos = v0.sqrMagnitude > 0.0001f ? Vector3.Dot(v0.normalized, dir) : 0f;
                Check("2c) ao soltar, sai voando na direção do balanço", cos > 0.85f && v0.magnitude > 1.5f && v0.magnitude < 9f,
                    $"velocidade {V(v0)} = {v0.magnitude:0.00} m/s (mão a {swingSpeed} m/s, multiplicador do soltar), alinhamento cos={cos:0.00}");
                Check("2d) física normal depois de soltar (sem flutuar)", !rb.isKinematic && rb.useGravity, $"cinemático={rb.isKinematic}, gravidade={rb.useGravity}");
                yield return 4.5f;
                float travelled = Vector3.ProjectOnPlane(throwApple.transform.position - releasePos, Vector3.up).magnitude;
                float gap = GroundGap(throwApple);
                Check("2e) maçã voa, cai e para no chão", travelled > 1.0f && gap > -0.06f && gap < 0.15f,
                    $"andou {travelled:0.00} m na horizontal, parou com gap {gap:+0.00;-0.00} m em {V(throwApple.transform.position)}");
                Object.Destroy(hand.gameObject);
            }
            else Fail("2) arremesso", "nenhuma maçã derrubada para testar");

            // ---------------- 3. Cesta ----------------
            log.AppendLine();
            log.AppendLine("## 3. Cesta de vime");
            AppleCollectionBasket basket = baskets.FirstOrDefault();
            GameObject basketApple = FallenApples().OrderBy(a => a.name).Skip(1).FirstOrDefault();
            if (basket != null && basketApple != null)
            {
                var grab = basketApple.GetComponent<InteractiveApple>();
                QuestSnapshot before = Snap();
                int basketBefore = basket.ApplesScoredCount;
                ScriptedTestInteractor hand = MakeHand(basketApple.transform.position);
                yield return null;
                Grab(hand, grab);
                yield return MoveHand(hand.transform, basket.AimTargetPosition + Vector3.up * 0.8f, 3f);
                yield return 0.5f;
                yield return MoveHand(hand.transform, basket.AimTargetPosition + Vector3.up * 0.05f, 1.0f);
                yield return 0.8f;
                int whileHeld = basket.ApplesScoredCount - basketBefore;
                Check("3a) maçã AINDA SEGURA dentro da boca da cesta não pontua", whileHeld == 0 && grab.isSelected, $"pontuou {whileHeld} vez(es) segurada");
                Info($"antes de soltar: maçã {V(basketApple.transform.position)}, boca {V(basket.AimTargetPosition)} raio {basket.AimTargetRadius:0.00}");
                Release(hand, grab);
                var rbB = basketApple.GetComponent<Rigidbody>();
                for (int k = 0; k < 6; k++)
                {
                    yield return 0.1f;
                    if (basketApple == null) break;
                    Info($"+{(k + 1) * 0.1f:0.0}s maçã {V(basketApple.transform.position)} vel {V(Vel(rbB))} kin={rbB.isKinematic} sel={grab.isSelected} dist boca {(basketApple.transform.position - basket.AimTargetPosition).magnitude:0.00}");
                }
                yield return 0.6f;
                QuestSnapshot after = Snap();
                int scored = basket.ApplesScoredCount - basketBefore;
                Check("3b) solta dentro da cesta: pontua 1 vez", scored == 1, $"cesta {basket.name}: +{scored}, placar da cesta {basket.TotalScore}");
                Check("3c) missão 'Amigo da Floresta' sobe 1", after.friend == before.friend + 1, $"{before.friend} -> {after.friend}");
                yield return 3.5f;
                Check("3d) maçã pontuada some da cesta depois (libera espaço)", basketApple == null, $"ainda existe={basketApple != null}");
                Object.Destroy(hand.gameObject);
            }
            else Fail("3) cesta", "sem cesta ou maçã para testar");

            // ---------------- 4. Anti-queda ----------------
            log.AppendLine();
            log.AppendLine("## 4. Proteção contra cair para baixo do mapa");
            GameObject lost = FallenApples().OrderBy(a => a.name).LastOrDefault();
            if (lost != null)
            {
                var ergo = lost.GetComponent<ErgonomicGrabInteractable>();
                var rb = lost.GetComponent<Rigidbody>();
                Vector3 home = ergo.HomePosition;
                float groundHere = GroundY(lost.transform.position, lost);
                rb.position = new Vector3(lost.transform.position.x, groundHere - 4f, lost.transform.position.z);
                lost.transform.position = rb.position;
                rb.linearVelocity = Vector3.down * 2f;
                yield return 2.0f;
                float gap = GroundGap(lost);
                bool inMap = TerrainGroundUtil.TryGetHeight(lost.transform.position, out _);
                Check("4a) maçã enfiada 4 m abaixo do terreno não fica perdida embaixo do mapa", gap > -0.10f && inMap,
                    $"depois de 2 s: gap {gap:+0.00;-0.00} m, dentro do mapa={inMap}, a {(lost.transform.position - home).magnitude:0.00} m da casa");

                // Fora de todos os blocos do mapa (voou por cima da borda): tem que voltar para a casa
                Vector3 outside = new Vector3(-130f, 8f, 60f);
                rb.position = outside;
                lost.transform.position = outside;
                rb.linearVelocity = Vector3.zero;
                yield return 2.0f;
                float toHome = (lost.transform.position - home).magnitude;
                float gap2 = GroundGap(lost);
                Check("4b) maçã que sai pela borda do mapa volta para a casa no chão", toHome < 0.6f && gap2 > -0.10f && gap2 < 0.15f,
                    $"depois de 2 s: a {toHome:0.00} m da casa {V(home)}, gap {gap2:+0.00;-0.00} m");
            }

            // ---------------- 5. Moeda conta, bônus não ----------------
            log.AppendLine();
            log.AppendLine("## 5. Moedas x bônus (missão 'Magia Botânica')");
            if (CoinScoreManager.Instance != null)
            {
                QuestSnapshot b5 = Snap();
                CoinScoreManager.Instance.RegisterCoinCollected(CollectibleOrbCoin.CoinType.EnergyStar, 10, 1);
                yield return null;
                QuestSnapshot a5 = Snap();
                Check("5a) moeda coletada conta 1 na missão", a5.botany == b5.botany + 1 && a5.coins == b5.coins + 1, $"{b5} -> {a5}");
            }

            // ---------------- 6. Totem ----------------
            log.AppendLine();
            log.AppendLine("## 6. Totem de checkpoint");
            var totem = Object.FindFirstObjectByType<InteractiveCheckpointTotem>();
            if (totem != null)
            {
                QuestSnapshot b6 = Snap();
                var simple = totem.GetComponentInChildren<XRSimpleInteractable>();
                Check("6a) totem tem alvo para o gatilho do laser", simple != null, simple != null ? ForestAuditTool.PathOf(simple.transform) : "sem XRSimpleInteractable");
                Transform lid = GetField(totem, "chestLidPivot") as Transform;
                Quaternion lid0 = lid != null ? lid.localRotation : Quaternion.identity;
                if (simple != null)
                {
                    ScriptedTestInteractor hand = MakeHand(totem.transform.position + new Vector3(0f, 1.2f, -2f));
                    yield return null;
                    Grab(hand, simple);
                    yield return 0.2f;
                    Release(hand, simple);
                    Object.Destroy(hand.gameObject);
                }
                yield return 2.0f;
                QuestSnapshot a6 = Snap();
                float lidAngle = lid != null ? Quaternion.Angle(lid0, lid.localRotation) : 0f;
                Light beacon = GetField(totem, "beaconLight") as Light;
                Check("6b) gatilho ativa o totem e abre o baú", totem.IsActivated && lidAngle > 60f, $"ativado={totem.IsActivated}, tampa girou {lidAngle:0}°");
                Check("6c) luz do totem acende sem sombra", beacon != null && beacon.intensity > 1f && beacon.shadows == LightShadows.None,
                    beacon != null ? $"intensidade {beacon.intensity:0.0}, sombras {beacon.shadows}" : "sem luz");
                Check("6d) bônus do totem soma 100 pontos e NÃO conta como moeda", a6.score - b6.score >= 100 && a6.coins == b6.coins && a6.botany == b6.botany,
                    $"{b6} -> {a6}");
                yield return 7.0f;
                Check("6e) pulso da luz termina (nada rodando para sempre)", GetField(totem, "activationRoutine") == null, "corrotina encerrada");
            }
            else Fail("6) totem", "não encontrado");

            // ---------------- 7. Pinhão mágico ----------------
            log.AppendLine();
            log.AppendLine("## 7. Pinhão mágico (arremessar e brotar)");
            var pinecones = Object.FindObjectsByType<MagicSeedPinecone>(FindObjectsSortMode.None).OrderBy(p => p.name).ToArray();
            Check("7a) 3 pinhões nos pedestais", pinecones.Length == 3, $"{pinecones.Length}");
            MagicSeedPinecone seed = pinecones.FirstOrDefault();
            if (seed != null)
            {
                QuestSnapshot b7 = Snap();
                var rb = seed.GetComponent<Rigidbody>();
                Vector3 home = seed.HomePosition;
                int sproutsBefore = Object.FindObjectsByType<SproutingPlantLifecycle>(FindObjectsSortMode.None).Length;
                ScriptedTestInteractor hand = MakeHand(seed.transform.position);
                yield return null;
                Grab(hand, seed);
                yield return null;
                Check("7b) pinhão é pego", seed.isSelected, $"selecionado={seed.isSelected}");
                Vector3 up = seed.transform.position + Vector3.up * 0.8f;
                yield return MoveHand(hand.transform, up, 1.5f);
                yield return 0.6f;
                // balança para frente e para baixo, longe do pedestal
                Vector3 away = Vector3.ProjectOnPlane(seed.transform.position - tree.transform.position, Vector3.up);
                if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
                away.Normalize();
                yield return MoveHand(hand.transform, up + away * 0.6f + Vector3.up * 0.15f, 3.0f);
                Release(hand, seed);
                Object.Destroy(hand.gameObject);

                SproutingPlantLifecycle sprout = null;
                float t0 = Time.time;
                while (sprout == null && Time.time - t0 < 4f)
                {
                    sprout = Object.FindObjectsByType<SproutingPlantLifecycle>(FindObjectsSortMode.None).FirstOrDefault(p => p.name.StartsWith("Sprouted_"));
                    yield return null;
                }
                Check("7c) arremessado, brota um canteiro ao bater no chão", sprout != null, sprout != null ? $"{sprout.name} em {V(sprout.transform.position)}" : "nada brotou em 4 s");
                if (sprout != null)
                {
                    float gy = GroundY(sprout.transform.position + Vector3.up * 0.3f, sprout.gameObject);
                    float off = sprout.transform.position.y - gy;
                    Check("7d) canteiro brota no chão e dentro do mapa", Mathf.Abs(off) < 0.12f && TerrainGroundUtil.TryGetHeight(sprout.transform.position, out _),
                        $"pivô {off:+0.00;-0.00} m do chão");
                }
                yield return null;
                QuestSnapshot a7 = Snap();
                Check("7e) brotar completa a missão 'Magia Botânica'", a7.botanyDone, $"{b7} -> {a7}");
                yield return 3.6f;
                float back = (seed.transform.position - home).magnitude;
                bool visible = seed.GetComponentsInChildren<Renderer>().Any(r => r.enabled && r.gameObject.activeInHierarchy);
                float aboveGround = TerrainGroundUtil.TryGetHeight(seed.transform.position, out float terrainY) ? seed.transform.position.y - terrainY : -1f;
                Check("7f) pinhão reaparece no pedestal depois de 3 s", back < 0.15f && visible && aboveGround > 0.3f,
                    $"a {back:0.000} m da casa, visível={visible}, {aboveGround:0.00} m acima do chão (no pedestal), cinemático={rb.isKinematic}");

                // Pool de 10 canteiros
                MagicSeedPinecone poolSeed = pinecones.Last();
                Vector3 basePos = poolSeed.transform.position;
                for (int i = 0; i < 13; i++)
                {
                    Vector3 p = basePos + new Vector3(1.5f + (i % 4) * 0.6f, 0f, (i / 4) * 0.6f);
                    float gy = GroundY(p + Vector3.up, null);
                    poolSeed.ExecuteSprout(new Vector3(p.x, gy, p.z), Vector3.up, i % 2 == 0);
                    yield return null;
                }
                yield return 0.5f;
                int liveSprouts = Object.FindObjectsByType<SproutingPlantLifecycle>(FindObjectsSortMode.None).Count(p => p.name.StartsWith("Sprouted_"));
                Check("7g) limite de 10 canteiros brotados (14 brotações)", liveSprouts == MagicSeedPinecone.MaxSproutsInScene, $"{liveSprouts} canteiros vivos");
            }

            // ---------------- 8. Painel final ----------------
            log.AppendLine();
            log.AppendLine("## 8. Painel de missões depois dos testes");
            var finalTexts = new List<string>();
            bool matches = quest != null;
            var datas = quest != null ? new[] { quest.QuestGait, quest.QuestForestFriend, quest.QuestBotany } : new SessionQuestManager.QuestData[0];
            object[] uis = { uiGait, uiFriend, uiBotany };
            for (int i = 0; i < datas.Length; i++)
            {
                var status = uis[i] != null ? GetField(uis[i], "txtStatus") as TMPro.TMP_Text : null;
                var bar = uis[i] != null ? GetField(uis[i], "imgFillBar") as UnityEngine.UI.Image : null;
                string expected = datas[i].isCompleted ? "[v] CONCLUIDA" : $"{datas[i].currentCount}/{datas[i].targetCount}";
                matches &= status != null && status.text == expected && bar != null && Mathf.Abs(bar.fillAmount - datas[i].Progress) < 0.01f;
                finalTexts.Add($"'{datas[i].title}' painel='{status?.text}' (esperado '{expected}') barra={bar?.fillAmount:0.00}");
            }
            QuestSnapshot sEnd = Snap();
            Check("8a) painel do drone mostra exatamente o progresso das missões", matches && quest.QuestBotany.isCompleted,
                string.Join(" | ", finalTexts) + " || " + sEnd);
            CaptureQuestPanel(uiGait, ".agents/visual_qa/FOREST_quest_panel_depois.png");
        }

        /// <summary>Foto offscreen do painel de missões do drone (de frente).</summary>
        private static void CaptureQuestPanel(object uiRow, string path)
        {
            var title = uiRow != null ? GetField(uiRow, "txtTitle") as TMPro.TMP_Text : null;
            if (title == null) { Info("sem painel para fotografar"); return; }
            Canvas canvas = title.canvas != null ? title.canvas.rootCanvas : null;
            Transform panel = canvas != null ? canvas.transform : title.transform.parent;
            Vector3 center = panel.position;
            var rt = panel as RectTransform;
            float size = 0.5f;
            if (rt != null)
            {
                Vector3[] corners = new Vector3[4];
                rt.GetWorldCorners(corners);
                center = (corners[0] + corners[2]) * 0.5f;
                size = Mathf.Max((corners[2] - corners[0]).magnitude, 0.2f);
            }
            // Canvas em espaço de mundo é lido olhando no mesmo sentido do forward dele
            Vector3 camPos = center - panel.forward * size * 1.3f;
            string full = ForestAuditTool.RenderTo(path, camPos, center, 40f, 1280, 720);
            Info($"foto do painel: {full}");
        }
    }
}
