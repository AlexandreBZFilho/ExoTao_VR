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
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core.Testing;
using VRRehab.EditorAutoSetup;
using VRRehab.Interactive.Task07;
using Object = UnityEngine.Object;

namespace VRRehab.EditorTools
{
    /// <summary>
    /// Testes em PLAY MODE real da TASK-07 (pistola de balões) no Menu_Principal: uma "mão de teste" do XRI pega a pistola,
    /// aperta o botão lateral (Activate), solta, bate nos balões e os estoura. Nada é salvo (o Play Mode é descartado).
    /// Rodar com Unity.exe -batchmode SEM -quit: -executeMethod VRRehab.EditorTools.Task07PlayModeTests.Run
    /// Relatório: .agents/visual_qa/TASK-07_PlayModeTest.txt
    /// </summary>
    [InitializeOnLoad]
    public static class Task07PlayModeTests
    {
        private const string StateKey = "Task07PlayModeTests.state";
        private const string FailKey = "Task07PlayModeTests.fail";
        private const string ReportPath = ".agents/visual_qa/TASK-07_PlayModeTest.txt";
        private const string MenuScenePath = "Assets/Scenes/Menu_Principal.unity";
        private const float TimeoutSeconds = 400f;

        private static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        private static IEnumerator routine;
        private static float waitUntil;
        private static int waitFrame = -1;
        private static double startRealTime;
        private static StringBuilder log;
        private static int pass, fail;

        static Task07PlayModeTests()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("ExoTao IC/Menu/Testes em Play Mode (TASK-07 Balões)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) return;
            EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
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
            Debug.Log("[Task07PlayModeTests]\n" + log);
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
        private static string V(Vector3 v) => ForestAuditTool.V(v);

        private static void SetField(object obj, string name, object value)
        {
            for (Type t = obj.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null) { f.SetValue(obj, value); return; }
            }
            throw new Exception($"campo {name} não encontrado em {obj.GetType().Name}");
        }

        private static XRInteractionManager Manager() => Object.FindFirstObjectByType<XRInteractionManager>();

        private static void Activate(BalloonInflaterGun gun) =>
            ((IXRActivateInteractable)gun).OnActivated(new ActivateEventArgs { interactorObject = null, interactableObject = gun });

        private static void Deactivate(BalloonInflaterGun gun) =>
            ((IXRActivateInteractable)gun).OnDeactivated(new DeactivateEventArgs { interactorObject = null, interactableObject = gun });

        /// <summary>Altura real do balão ao longo do próprio eixo (nó -> topo), independente da inclinação no ar.</summary>
        private static float RendererHeight(FloatingBalloon b)
        {
            Vector3 axis = b.transform.up;
            float min = float.MaxValue, max = float.MinValue;
            foreach (var mf in b.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    float d = Vector3.Dot(mf.transform.TransformPoint(c), axis);
                    min = Mathf.Min(min, d); max = Mathf.Max(max, d);
                }
            }
            return max > min ? max - min : 0f;
        }

        private static IEnumerator Inflate(BalloonInflaterGun gun, float seconds)
        {
            Activate(gun);
            yield return seconds;
            if (gun.IsInflating) Deactivate(gun);
            else Deactivate(gun); // solta o botão de qualquer jeito (o auto-release já pode ter ocorrido)
            yield return null;
        }

        private static List<FloatingBalloon> FreeBalloons(BalloonInflaterGun gun) =>
            gun.Pool.Root.GetComponentsInChildren<FloatingBalloon>(false).Where(b => b.IsFree).ToList();

        // ------------------------------------------------------------------ testes
        private static IEnumerator Tests()
        {
            log.AppendLine($"=== Testes em Play Mode da TASK-07 — Pistola de Balões ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");
            log.AppendLine("Play Mode real (Awake/Start, física, eventos do XRI). A 'mão de teste' segura a pistola via XRInteractionManager.");
            log.AppendLine("Activate (botão lateral do Vive) é disparado pela interface IXRActivateInteractable, como o XRI faz.");
            log.AppendLine();
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 90;
            yield return 2.0f;

            // 0. Estado inicial
            log.AppendLine("## 0. Estado inicial");
            var guns = Object.FindObjectsByType<BalloonInflaterGun>(FindObjectsSortMode.None);
            Check("0a) Exatamente 1 pistola na cena", guns.Length == 1, $"{guns.Length}");
            BalloonInflaterGun gun = guns.FirstOrDefault();
            if (gun == null) yield break;
            Check("0b) Pool criado no Start com 8 + 1 reserva", gun.Pool != null && gun.Pool.TotalInstances == 9 && gun.Pool.FreeCount == 9,
                gun.Pool == null ? "sem pool" : $"instâncias {gun.Pool.TotalInstances}, livres {gun.Pool.FreeCount}, máx no ar {gun.Pool.MaxLive}");
            int balloonObjectsAtStart = Resources.FindObjectsOfTypeAll<FloatingBalloon>().Count(b => b.gameObject.scene.IsValid());
            Info($"FloatingBalloon na cena (incluindo inativos): {balloonObjectsAtStart}");
            var demos = Task07_SceneAndPrefabBuilder.DemoNames.Select(n => GameObject.Find(n)).Where(g => g != null).Select(g => g.GetComponent<FloatingBalloon>()).ToList();
            Check("0c) 3 balões de demonstração presentes", demos.Count == 3, string.Join(", ", demos.Select(d => d.name)));
            var demoStart = demos.Select(d => d.transform.position).ToList();

            // 1. Pegar a pistola: cano para a frente
            log.AppendLine("## 1. Pegar a pistola (gatilho = Select)");
            var handGo = new GameObject("~TestHand");
            Vector3 handPos = gun.transform.position + Vector3.up * 0.45f - gun.transform.right * 0.0f;
            Vector3 roomDir = Vector3.ProjectOnPlane(gun.transform.forward, Vector3.up).normalized;
            handGo.transform.SetPositionAndRotation(handPos, Quaternion.LookRotation(roomDir, Vector3.up));
            var hand = handGo.AddComponent<ScriptedTestInteractor>();
            yield return null;
            Manager().SelectEnter((IXRSelectInteractor)hand, gun);
            yield return 0.8f; // tempo do "ease-in" ergonômico da pegada
            Transform muzzle = gun.MuzzleSocket;
            float dot = Vector3.Dot(muzzle.forward, hand.transform.forward);
            float pitch = Vector3.SignedAngle(hand.transform.forward, muzzle.forward, hand.transform.right);
            Check("1a) Pistola segurada", gun.isSelected, $"isSelected={gun.isSelected}");
            Check("1b) Cano aponta para a frente da mão (dot > 0,95)", dot > 0.95f, $"dot={dot:0.000}, cano {(pitch < 0 ? "acima" : "abaixo")} do apontar em {Mathf.Abs(pitch):0.0}°");
            Vector3 muzzleInHand = hand.transform.InverseTransformPoint(muzzle.position);
            Check("1c) Bico à frente da mão", muzzleInHand.z > 0.05f, $"bico no espaço da mão {V(muzzleInHand)}");

            // 2. Inflar 1,5 s e soltar o botão
            log.AppendLine("## 2. Inflar 1,5 s e soltar");
            Activate(gun);
            yield return 0.5f;
            FloatingBalloon b1 = gun.CurrentBalloon;
            Check("2a) Balão enchendo no bico", b1 != null && b1.IsInflating && b1.transform.parent == muzzle, b1 == null ? "nenhum" : $"pai {b1.transform.parent?.name}");
            Check("2b) Collider do balão desligado durante a inflação (laser não estoura na mão)", b1 != null && !b1.BalloonCollider.enabled, "");
            yield return 1.0f;
            Deactivate(gun);
            Vector3 p0 = b1.transform.position;
            yield return 0.9f;
            Vector3 moved = b1.transform.position - p0;
            float along = Vector3.Dot(moved, muzzle.forward);
            float h = RendererHeight(b1);
            Check("2c) Balão solto e livre", b1.IsFree && b1.transform.parent == gun.Pool.Root, $"livre={b1.IsFree}");
            Check("2d) Saiu na direção do cano", along > 0.12f, $"andou {along:0.00} m ao longo do cano (deslocamento {V(moved)})");
            Check("2e) Altura do balão entre 0,35 e 0,45 m", h >= 0.35f && h <= 0.45f, $"{h:0.000} m (escala {b1.CurrentScale:0.00})");
            Check("2f) Cordinha proporcional", b1.StringLengthMeters > 0.38f && b1.StringLengthMeters < 0.55f, $"{b1.StringLengthMeters:0.00} m");

            // 3. Solta sozinho no máximo
            log.AppendLine("## 3. Segurar o botão além do máximo");
            Activate(gun);
            FloatingBalloon b2 = gun.CurrentBalloon;
            yield return 2.2f;
            Check("3a) Soltou sozinho ao encher (não estoura na mão)", b2 != null && b2.IsFree && !gun.IsInflating, $"livre={b2?.IsFree}, inflando={gun.IsInflating}");
            Activate(gun); // ainda com o botão apertado: não deve começar outro
            yield return null;
            Check("3b) Novo balão só depois de soltar e apertar de novo", gun.CurrentBalloon == null, "");
            Deactivate(gun);
            yield return null;

            // 4. Clique acidental muito curto cancela
            Activate(gun);
            yield return null;
            Deactivate(gun);
            yield return null;
            Check("4) Clique de 1 quadro não gera balão vazio", gun.CurrentBalloon == null && FreeBalloons(gun).Count == 2, $"balões no ar: {FreeBalloons(gun).Count}");

            // 5. Pool de 8
            log.AppendLine("## 5. Pool: inflar 9 balões");
            for (int i = 0; i < 9; i++) yield return Inflate(gun, 0.45f);
            yield return 0.6f;
            int live = FreeBalloons(gun).Count;
            int balloonObjectsNow = Resources.FindObjectsOfTypeAll<FloatingBalloon>().Count(b => b.gameObject.scene.IsValid());
            Check("5a) No máximo 8 balões no ar", live <= 8 && live >= 7, $"{live} no ar, {gun.Pool.RecycledOldest} reciclados (estouro suave do mais antigo)");
            Check("5b) O mais antigo estourou suave para abrir espaço", gun.Pool.RecycledOldest >= 1, $"recicla {gun.Pool.RecycledOldest}");
            Check("5c) Zero Instantiate/Destroy por inflada", balloonObjectsNow == balloonObjectsAtStart && gun.Pool.TotalInstances == 9,
                $"FloatingBalloon na cena: início {balloonObjectsAtStart}, agora {balloonObjectsNow}");

            // 6. Mão bate: empurra, não estoura
            log.AppendLine("## 6. Tapinha com a mão");
            FloatingBalloon target = FreeBalloons(gun).OrderByDescending(b => b.ReleaseTime).First();
            var bopGo = new GameObject("~TestBopHand");
            var bopHand = bopGo.AddComponent<ScriptedTestInteractor>();
            var sc = bopGo.AddComponent<SphereCollider>(); sc.radius = 0.05f; sc.isTrigger = true;
            var brb = bopGo.AddComponent<Rigidbody>(); brb.isKinematic = true; brb.useGravity = false;
            Vector3 center = target.transform.position + Vector3.up * target.BodyHeightMeters * 0.5f;
            bopGo.transform.position = center + Vector3.down * 0.45f;
            yield return null;
            Vector3 vBefore = target.GetComponent<Rigidbody>().linearVelocity;
            for (int i = 0; i < 160 && target.BopCount == 0; i++)
            {
                center = target.transform.position + Vector3.up * target.BodyHeightMeters * 0.5f;
                bopGo.transform.position = Vector3.MoveTowards(bopGo.transform.position, center, 1.4f * Mathf.Max(Time.deltaTime, 0.008f));
                yield return null;
            }
            yield return 0.1f;
            Vector3 vAfter = target.GetComponent<Rigidbody>().linearVelocity;
            Check("6a) Tapinha empurrou o balão", target.BopCount >= 1 && vAfter.y > vBefore.y + 0.2f, $"toques {target.BopCount}, vel {V(vBefore)} -> {V(vAfter)}");
            Check("6b) Tapinha NÃO estourou", target.IsFree, $"livre={target.IsFree}, motivo={target.LastPopReason}");
            sc.enabled = false; // daqui em diante a mão-de-tapinha só serve de "laser" (Select)

            // 7. Laser / gatilho estoura
            log.AppendLine("## 7. Gatilho no balão (laser ou mão)");
            Manager().SelectEnter((IXRSelectInteractor)bopHand, target);
            yield return null;
            if (target.isSelected) Manager().SelectExit((IXRSelectInteractor)bopHand, target);
            Check("7a) Gatilho estoura", !target.IsFree && target.LastPopReason == "select", $"motivo={target.LastPopReason}");
            yield return 1.2f;
            Check("7b) Balão estourado voltou ao pool", !target.gameObject.activeSelf, $"ativo={target.gameObject.activeSelf}");

            // 8. Colisão suave quica, impacto forte estoura
            log.AppendLine("## 8. Colisões");
            FloatingBalloon soft = FreeBalloons(gun).OrderByDescending(b => b.ReleaseTime).First();
            var softRb = soft.GetComponent<Rigidbody>();
            var floorGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floorGo.name = "~TestBoard";
            floorGo.transform.localScale = new Vector3(1.2f, 0.10f, 1.2f);
            floorGo.transform.position = soft.transform.position + Vector3.down * 0.35f;
            yield return null;
            softRb.linearVelocity = Vector3.down * 1.4f;
            yield return 0.7f;
            Check("8a) Batida suave (1,4 m/s) só quica", soft.IsFree, $"livre={soft.IsFree}, vel atual {V(softRb.linearVelocity)}");
            FloatingBalloon hard = FreeBalloons(gun).Where(b => b != soft).OrderByDescending(b => b.ReleaseTime).First();
            var hardRb = hard.GetComponent<Rigidbody>();
            floorGo.transform.position = hard.transform.position + Vector3.down * 0.30f;
            yield return null;
            hardRb.linearVelocity = Vector3.down * 6f;
            yield return 0.4f;
            Check("8b) Impacto forte (6 m/s) estoura", !hard.IsFree && hard.LastPopReason == "impact", $"motivo={hard.LastPopReason}");
            Object.Destroy(floorGo);

            // 9. Teto: fica balançando perto do teto, sem atravessar
            log.AppendLine("## 9. Teto");
            var ceilField = typeof(FloatingBalloon).GetField("ceilingHeightLimit", BindingFlags.Instance | BindingFlags.NonPublic);
            float ceiling = (float)ceilField.GetValue(soft);
            yield return 9.0f;
            var still = FreeBalloons(gun);
            float maxTop = still.Count > 0 ? still.Max(b => b.transform.position.y + b.BodyHeightMeters) : 0f;
            float maxY0 = still.Count > 0 ? still.Max(b => b.transform.position.y) : 0f;
            yield return 2.0f;
            float drift = still.Count > 0 ? still.Max(b => Mathf.Abs(b.transform.position.y - maxY0)) : 0f;
            Check("9a) Nenhum balão passa do teto", maxTop <= ceiling + 0.08f, $"topo mais alto {maxTop:0.00} m, limite {ceiling:0.00} m, {still.Count} balões");
            Check("9b) Balões perto do teto seguem livres (não travam)", still.All(b => b.IsFree), $"{still.Count(b => b.IsFree)}/{still.Count} livres");

            // 10. Vida útil: murcha e volta ao pool
            log.AppendLine("## 10. Vida útil");
            FloatingBalloon old = still.FirstOrDefault();
            if (old != null)
            {
                SetField(old, "lifetimeSeconds", 0.5f);
                yield return 0.7f;
                Check("10a) Começa a murchar depois da vida útil", old.IsDeflatingOrPopping, $"motivo={old.LastPopReason}");
                yield return 3.0f;
                Check("10b) Murchou e voltou ao pool", !old.gameObject.activeSelf, $"ativo={old.gameObject.activeSelf}");
            }

            // 11. Balões demo
            log.AppendLine("## 11. Balões de demonstração (amarrados)");
            float demoDrift = demos.Select((d, i) => Vector3.Distance(d.transform.position, demoStart[i])).Max();
            Check("11a) Demos ficam perto do lugar", demoDrift < 0.2f, $"maior deslocamento {demoDrift:0.000} m em ~{Time.time:0} s");
            FloatingBalloon demo = demos[0];
            SetField(demo, "demoRespawnSeconds", 1.0f);
            Manager().SelectEnter((IXRSelectInteractor)bopHand, demo);
            yield return null;
            if (demo.isSelected) Manager().SelectExit((IXRSelectInteractor)bopHand, demo);
            Check("11b) Demo estoura com o gatilho", demo.LastPopReason == "select" && !demo.IsFree, $"motivo={demo.LastPopReason}");
            yield return 2.5f;
            Check("11c) Demo renasce no lugar", demo.IsFree && demo.gameObject.activeSelf && Vector3.Distance(demo.transform.position, demoStart[0]) < 0.25f,
                $"livre={demo.IsFree}, pos {V(demo.transform.position)}");

            // 12. Soltar a pistola: cai com física normal; sem troca de cena
            log.AppendLine("## 12. Soltar a pistola");
            Manager().SelectExit((IXRSelectInteractor)hand, gun);
            yield return 0.3f;
            var grb = gun.GetComponent<Rigidbody>();
            Check("12a) Pistola solta fica dinâmica (não flutua)", !grb.isKinematic && grb.useGravity, $"kinematic={grb.isKinematic}, gravidade={grb.useGravity}");
            Check("12b) Continua no Menu_Principal (nenhum LoadScene)", SceneManager.GetActiveScene().path == MenuScenePath, SceneManager.GetActiveScene().path);
            Object.Destroy(handGo);
            Object.Destroy(bopGo);
        }
    }
}
