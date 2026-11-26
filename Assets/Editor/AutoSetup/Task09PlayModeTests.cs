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
using VRRehab.Core.Testing;
using VRRehab.Interactive.Task09;
using Object = UnityEngine.Object;

namespace VRRehab.EditorTools
{
    /// <summary>
    /// Testes em PLAY MODE real da TASK-09 (Botão de Fogos refeito + Bobblehead) no Menu_Principal, com capturas
    /// offscreen (Camera.Render -> RenderTexture -> PNG). Nada é salvo (o Play Mode é descartado).
    /// Rodar: Unity.exe -batchmode (SEM -quit) -executeMethod VRRehab.EditorTools.Task09PlayModeTests.Run
    /// Relatório: .agents/visual_qa/TASK-09_PlayModeTest.txt
    /// </summary>
    [InitializeOnLoad]
    public static class Task09PlayModeTests
    {
        private const string StateKey = "Task09PlayModeTests.state";
        private const string FailKey = "Task09PlayModeTests.fail";
        private const string ReportPath = ".agents/visual_qa/TASK-09_PlayModeTest.txt";
        private const string QaDir = ".agents/visual_qa";
        private const string MenuScenePath = "Assets/Scenes/Menu_Principal.unity";
        private const float TimeoutSeconds = 500f;

        private static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        private static IEnumerator routine;
        private static float waitUntil;
        private static int waitFrame = -1;
        private static double startRealTime;
        private static StringBuilder log;
        private static int pass, fail;

        static Task09PlayModeTests()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("ExoTao IC/Menu/Testes em Play Mode (TASK-09 Fogos e Bobblehead)")]
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
            Directory.CreateDirectory(QaDir);
            File.WriteAllText(ReportPath, log.ToString());
            Debug.Log("[Task09PlayModeTests]\n" + log);
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
        private static string V(Vector3 v) => $"({v.x:0.000}, {v.y:0.000}, {v.z:0.000})";
        private static XRInteractionManager Manager() => Object.FindFirstObjectByType<XRInteractionManager>();

        private static T Get<T>(object obj, string field)
        {
            for (Type t = obj.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null) return (T)f.GetValue(obj);
            }
            throw new Exception($"campo {field} não encontrado em {obj.GetType().Name}");
        }

        private static void Shot(string file, Vector3 pos, Vector3 lookAt, float fov, int w = 1920, int h = 1080, Texture2D into = null, int dx = 0, int dy = 0)
        {
            var go = new GameObject("__Task09TestCam");
            var cam = go.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation((lookAt - pos).normalized, Vector3.up));
            cam.fieldOfView = fov; cam.nearClipPlane = 0.02f; cam.farClipPlane = 60f;
            cam.clearFlags = CameraClearFlags.Skybox; cam.allowHDR = false; cam.allowMSAA = false;
            cam.enabled = false;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D tex = into ?? new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), dx, dy);
            tex.Apply();
            RenderTexture.active = prev;
            if (into == null)
            {
                File.WriteAllBytes(Path.Combine(QaDir, file), tex.EncodeToPNG());
                Object.Destroy(tex);
            }
            cam.targetTexture = null;
            rt.Release(); Object.Destroy(rt);
            Object.Destroy(go);
        }

        private static bool SolidBetween(Vector3 a, Vector3 b, Transform ignoreRoot, out string what)
        {
            what = "-";
            Vector3 d = b - a;
            foreach (var h in Physics.SphereCastAll(a, 0.05f, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.transform.IsChildOf(ignoreRoot)) continue;
                var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
                if (origin != null && h.collider.transform.IsChildOf(origin.transform)) continue;
                if (h.rigidbody != null && !h.rigidbody.isKinematic) continue;
                what = h.collider.name;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ testes
        private static IEnumerator Tests()
        {
            log.AppendLine($"=== Testes em Play Mode da TASK-09 — Fogos de Artifício + Bobblehead ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");
            log.AppendLine("Play Mode real (Awake/Start, física, corrotinas, eventos do XRI) com 'mão de teste' (ScriptedTestInteractor).");
            log.AppendLine();
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 90;
            yield return 2.0f;

            var buttons = Object.FindObjectsByType<FireworksButtonInteractable>(FindObjectsSortMode.None);
            var bobbles = Object.FindObjectsByType<BobbleheadSpringToy>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var levers = Object.FindObjectsByType<AtmosphereLeverInteractable>(FindObjectsSortMode.None);
            log.AppendLine("## 0. Cena");
            Check("0a) Exatamente 1 botão, 1 bobblehead e 1 alavanca", buttons.Length == 1 && bobbles.Length == 1 && levers.Length == 1,
                $"botão {buttons.Length}, bobblehead {bobbles.Length}, alavanca {levers.Length}");
            Check("0b) Room_Fireworks_Launcher antigo removido", GameObject.Find("Room_Fireworks_Launcher") == null, "");
            if (buttons.Length == 0 || bobbles.Length == 0) yield break;

            yield return FireworksTests(buttons[0]);
            yield return BobbleTests(bobbles[0]);
            GlobalChecks(buttons[0], bobbles[0]);
        }

        // ================================================================== FOGOS
        private static IEnumerator FireworksTests(FireworksButtonInteractable fw)
        {
            log.AppendLine();
            log.AppendLine("## 1. Botão de fogos");
            var shows = fw.Shows;
            bool refsOk = shows.Length == 2 && shows.All(s => s.rocket && s.rocketTrail && s.rocketHead && s.burstRoot && s.mainBurst && s.crackle && s.flash && s.lowFan && s.burstAudio)
                          && fw.LaunchPoint != null && fw.BurstLight != null;
            Check("1a) Show auto-contido: 2 conjuntos com todas as referências preenchidas (filhos do botão)", refsOk && shows.All(s => s.rocket.IsChildOf(fw.transform)), $"{shows.Length} conjuntos");
            bool emissionOn = shows.All(s => s.mainBurst.emission.enabled && s.flash.emission.enabled && s.rocketTrail.emission.enabled);
            Check("1b) EmissionModule da explosão LIGADO", emissionOn, "");
            var allPs = fw.GetComponentsInChildren<ParticleSystem>(true);
            int maxSum = shows.Sum(s => s.rocketTrail.main.maxParticles + s.rocketHead.main.maxParticles + s.mainBurst.main.maxParticles + s.crackle.main.maxParticles + s.flash.main.maxParticles);
            Check("1c) Teto de partículas do show alto (2 conjuntos) <= 250", maxSum <= 250, $"soma dos maxParticles = {maxSum}");
            bool worldSpace = shows.All(s => s.mainBurst.main.simulationSpace == ParticleSystemSimulationSpace.World && s.rocketTrail.main.simulationSpace == ParticleSystemSimulationSpace.World);
            bool noCollision = allPs.All(p => !p.collision.enabled);
            var psr = allPs.Select(p => p.GetComponent<ParticleSystemRenderer>()).ToList();
            bool noShadow = psr.All(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off && !r.receiveShadows);
            Check("1d) World space, sem colisão de partícula, sem sombra", worldSpace && noCollision && noShadow, $"world={worldSpace}, sem colisão={noCollision}, sem sombra={noShadow}");
            Material m = psr[0].sharedMaterial;
            bool additive = m != null && m.shader.name == "Particles/Standard Unlit" && m.GetInt("_DstBlend") == (int)UnityEngine.Rendering.BlendMode.One && m.mainTexture != null && m.enableInstancing;
            Check("1e) Material de partícula aditivo com textura (faísca, não esfera sólida)", additive, m == null ? "sem material" : $"{m.name}: {m.shader.name}, DstBlend={m.GetInt("_DstBlend")}, tex={m.mainTexture?.name}, instancing={m.enableInstancing}");
            var lights = fw.GetComponentsInChildren<Light>(true);
            Check("1f) Todas as luzes do botão sem sombra; flash <= 2,5 e alcance <= 3 m", lights.All(l => l.shadows == LightShadows.None)
                && Get<float>(fw, "flashIntensity") <= 2.5f && Get<float>(fw, "flashRange") <= 3f, $"{lights.Length} luzes");

            // --- Disparo 1 com captura da sequência (vista do paciente)
            Vector3 launch = fw.LaunchPoint.position;
            Vector3 roomDir = -Vector3.right; // frente do counter_2_b (spec)
            Vector3 eye = launch + roomDir * 1.6f + Vector3.up * 0.55f;
            Vector3 aim = launch + Vector3.up * 0.75f;
            Vector3 sideEye = launch + Vector3.forward * 2.2f + roomDir * 0.4f + Vector3.up * 0.3f;

            fw.ForceNextVariant(FireworksButtonInteractable.BurstVariant.Peony);
            float fireTime = Time.time;
            bool fired = fw.TriggerFireworks();
            Check("1g) Disparo aceito", fired, $"ShowsFired={fw.ShowsFired}");
            yield return null;
            yield return null;
            Shot("TASK-09_Fireworks_Seq_1.png", eye, aim, 62f);   // botão afundado, foguete saindo
            var rig = shows.FirstOrDefault(s => s.inFlight) ?? shows[0];
            int burstDuringFlight = 0; bool seq2 = false;
            while (fw.LastRocketArrivalTime < fireTime && Time.time < fireTime + 2f)
            {
                burstDuringFlight = Mathf.Max(burstDuringFlight, rig.mainBurst.particleCount + rig.flash.particleCount);
                if (!seq2 && Time.time > fireTime + 0.28f) { Shot("TASK-09_Fireworks_Seq_2.png", eye, aim, 62f); seq2 = true; }
                yield return null;
            }
            float flight = fw.LastRocketArrivalTime - fireTime;
            Vector3 apex = fw.LastApex;
            Check("1h) Show alto: ápice >= 0,6 m acima do bico", !fw.LastWasLowShow && apex.y - launch.y >= 0.6f, $"ápice {V(apex)}, {apex.y - launch.y:0.00} m acima, livre {fw.LastClearDistance:0.00} m");
            Check("1i) Ápice quase vertical (inclinação <= 13°)", Vector3.Angle(Vector3.up, apex - launch) <= 13f, $"{Vector3.Angle(Vector3.up, apex - launch):0.0}°");
            bool blocked = SolidBetween(launch + Vector3.up * 0.03f, apex, fw.transform, out string blocker);
            Check("1j) Nenhum colisor sólido entre o botão e o ápice", !blocked, blocked ? blocker : "caminho livre");
            Check("1k) Foguete voa 0,45-0,8 s e a explosão NÃO aparece antes de ele chegar", flight >= 0.44f && flight <= 0.85f && burstDuringFlight == 0,
                $"voo {flight:0.000} s, partículas de explosão durante o voo = {burstDuringFlight}");
            yield return null;
            yield return null;
            int burstNow = rig.mainBurst.particleCount;
            Check("1l) Explosão acontece quando o foguete chega", burstNow > 30, $"{burstNow} partículas na explosão, variante {fw.LastVariant}");
            yield return 0.12f;
            Shot("TASK-09_Fireworks_Seq_3.png", eye, aim, 62f);
            Shot("TASK-09_Fireworks_Side.png", sideEye, (launch + apex) * 0.5f, 60f);

            // Cooldown
            bool second = fw.TriggerFireworks();
            Check("1m) Cooldown: segundo disparo antes de 1,2 s é ignorado (sem fila)", !second && fw.RejectedByCooldown >= 1, $"aceito={second}, rejeitados={fw.RejectedByCooldown}");
            yield return 0.55f;
            Shot("TASK-09_Fireworks_Seq_4.png", eye, aim, 62f);   // faíscas caindo

            // Dois shows ao mesmo tempo + teto de partículas
            while (fw.IsOnCooldown) yield return null;
            fw.ForceNextVariant(FireworksButtonInteractable.BurstVariant.Glitter);
            bool third = fw.TriggerFireworks();
            Check("1n) Depois do cooldown dispara de novo", third, $"ShowsFired={fw.ShowsFired}");
            int maxLive = 0, maxShows = 0;
            float t0 = Time.time;
            bool lightShadowOk = true;
            while (Time.time < t0 + 3.2f)
            {
                maxLive = Mathf.Max(maxLive, fw.LiveParticleCount());
                maxShows = Mathf.Max(maxShows, fw.ActiveShows);
                if (fw.BurstLight.enabled && fw.BurstLight.shadows != LightShadows.None) lightShadowOk = false;
                yield return null;
            }
            Check("1o) Nunca mais de 250 partículas vivas (2 shows sobrepostos)", maxLive <= 250, $"máximo {maxLive}, shows simultâneos {maxShows}");
            Check("1p) Luz do flash nunca projeta sombra", lightShadowOk, "");
            yield return 2.0f;
            Shot("TASK-09_Fireworks_Seq_5.png", eye, aim, 62f);
            Check("1q) Depois do show tudo para sozinho", fw.LiveParticleCount() == 0 && !fw.BurstLight.enabled && fw.ActiveShows == 0,
                $"partículas {fw.LiveParticleCount()}, luz {(fw.BurstLight.enabled ? "acesa" : "apagada")}, shows {fw.ActiveShows}");

            // Variantes (mosaico 2x2)
            var mosaic = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            var seen = new List<string>();
            var order = new[] { FireworksButtonInteractable.BurstVariant.Peony, FireworksButtonInteractable.BurstVariant.Ring, FireworksButtonInteractable.BurstVariant.Willow, FireworksButtonInteractable.BurstVariant.Glitter };
            for (int i = 0; i < 4; i++)
            {
                while (fw.IsOnCooldown || fw.ActiveShows > 0) yield return null;
                fw.ForceNextVariant(order[i]);
                float ft = Time.time;
                fw.TriggerFireworks();
                while (fw.LastRocketArrivalTime < ft) yield return null;
                yield return order[i] == FireworksButtonInteractable.BurstVariant.Willow ? 0.75f : 0.35f;
                seen.Add($"{fw.LastVariant}:{fw.LiveParticleCount()}");
                Shot(null, eye, aim, 62f, 960, 540, mosaic, (i % 2) * 960, (i < 2 ? 540 : 0));
                yield return 2.6f;
            }
            File.WriteAllBytes(Path.Combine(QaDir, "TASK-09_Fireworks_Variants.png"), mosaic.EncodeToPNG());
            Object.Destroy(mosaic);
            Check("1r) 4 variantes de explosão disparam (peônia, anel, salgueiro, purpurina)", seen.Count == 4, string.Join(", ", seen));

            // Robustez: girar o botão no lugar não estraga o show
            while (fw.IsOnCooldown || fw.ActiveShows > 0) yield return null;
            Quaternion rot0 = fw.transform.rotation;
            fw.transform.rotation = Quaternion.Euler(0f, 137f, 0f) * rot0;
            Physics.SyncTransforms();
            float ft2 = Time.time;
            fw.TriggerFireworks();
            while (fw.LastRocketArrivalTime < ft2) yield return null;
            Vector3 apexR = fw.LastApex;
            Check("1s) Botão girado 137°: ápice continua acima (efeito segue o botão)", !fw.LastWasLowShow && apexR.y - fw.LaunchPoint.position.y >= 0.6f && Vector3.Angle(Vector3.up, apexR - fw.LaunchPoint.position) <= 13f,
                $"ápice {V(apexR)}, {apexR.y - fw.LaunchPoint.position.y:0.00} m acima");
            yield return 2.8f;
            fw.transform.rotation = rot0;
            Physics.SyncTransforms();

            // Show baixo: 'móvel' logo acima do botão
            while (fw.IsOnCooldown || fw.ActiveShows > 0) yield return null;
            var shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shelf.name = "~TestShelfAbove";
            shelf.transform.position = fw.LaunchPoint.position + Vector3.up * 0.55f;
            shelf.transform.localScale = new Vector3(1.2f, 0.04f, 1.2f);
            Physics.SyncTransforms();
            float ft3 = Time.time;
            fw.TriggerFireworks();
            yield return 0.15f;
            Vector3 apexLow = fw.LastApex;
            Check("1t) Com móvel 0,55 m acima: 'show baixo' (nada explode dentro do móvel)", fw.LastWasLowShow && apexLow.y < shelf.transform.position.y - 0.05f
                && shows.Sum(s => s.mainBurst.particleCount) == 0 && shows.Sum(s => s.lowFan.particleCount) > 0,
                $"baixo={fw.LastWasLowShow}, ponto {V(apexLow)}, leque {shows.Sum(s => s.lowFan.particleCount)} faíscas");
            Object.Destroy(shelf);
            yield return 1.5f;

            // Toque da mão no domo (interactor de mão) e objeto qualquer chamado "hand" (não pode disparar)
            while (fw.IsOnCooldown || fw.ActiveShows > 0) yield return null;
            int before = fw.ShowsFired;
            var fake = new GameObject("LeftHand Controller (sem interactor)");
            var fc = fake.AddComponent<SphereCollider>(); fc.radius = 0.03f; fc.isTrigger = true;
            fake.transform.position = fw.LaunchPoint.position + Vector3.up * 0.4f;
            yield return null;
            fake.transform.position = fw.LaunchPoint.position - Vector3.up * 0.02f;
            yield return null; yield return null; yield return null;
            Check("1u) Objeto chamado 'hand/controller' SEM interactor não dispara", fw.ShowsFired == before, $"ShowsFired {before} -> {fw.ShowsFired}");
            Object.Destroy(fake);
            yield return null;

            var handGo = new GameObject("~TestHand");
            var hc = handGo.AddComponent<SphereCollider>(); hc.radius = 0.03f; hc.isTrigger = true;
            handGo.transform.position = fw.LaunchPoint.position + Vector3.up * 0.4f;
            var hand = handGo.AddComponent<ScriptedTestInteractor>();
            yield return null;
            handGo.transform.position = fw.LaunchPoint.position - Vector3.up * 0.02f;
            yield return null; yield return null; yield return null;
            Check("1v) Toque da mão (XR interactor) no domo dispara", fw.ShowsFired == before + 1, $"ShowsFired {before} -> {fw.ShowsFired}");
            handGo.transform.position = fw.LaunchPoint.position + Vector3.up * 0.4f;
            yield return 1.4f;
            while (fw.IsOnCooldown || fw.ActiveShows > 0) yield return null;

            // Gatilho (Select)
            int beforeSel = fw.ShowsFired;
            var xr = fw.GetComponent<XRSimpleInteractable>();
            Manager().SelectEnter((IXRSelectInteractor)hand, xr);
            yield return null;
            if (xr.isSelected) Manager().SelectExit((IXRSelectInteractor)hand, xr);
            Check("1w) Gatilho do Vive (Select) dispara", fw.ShowsFired == beforeSel + 1, $"ShowsFired {beforeSel} -> {fw.ShowsFired}");
            Object.Destroy(handGo);
            yield return 3.0f;

            // Zero GC por frame: o show roda em corrotinas sem alocar
            long a0 = GC.GetAllocatedBytesForCurrentThread();
            int lp = 0;
            for (int i = 0; i < 300; i++) lp += fw.LiveParticleCount();
            long a1 = GC.GetAllocatedBytesForCurrentThread();
            Check("1x) Consultas por frame do botão sem alocação", a1 - a0 == 0, $"{a1 - a0} bytes em 300 chamadas");
        }

        // ================================================================== BOBBLEHEAD
        private static IEnumerator BobbleTests(BobbleheadSpringToy bb)
        {
            log.AppendLine();
            log.AppendLine("## 2. Bobblehead");
            Transform head = bb.HeadTransform;
            Check("2a) Começa dormindo (Update desligado em repouso)", bb.IsSleeping, $"enabled={bb.enabled}");
            var headCol = head.GetComponent<SphereCollider>();
            Check("2b) Colisor da cabeça é filho do pivô da cabeça; só colisores primitivos", headCol != null && !bb.GetComponentsInChildren<MeshCollider>(true).Any(),
                headCol == null ? "sem colisor na cabeça" : $"SphereCollider r={headCol.radius:0.000} em {head.name}");
            Check("2c) Luzes do bobblehead sem sombra", bb.GetComponentsInChildren<Light>(true).All(l => l.shadows == LightShadows.None), "");
            var antennas = Get<Transform[]>(bb, "antennas");
            var spring = Get<Transform>(bb, "neckSpring");
            Check("2d) Mola do pescoço e 2 antenas ligadas (movimento secundário)", spring != null && antennas.Length == 2 && antennas.All(a => a.IsChildOf(head)), $"antenas {antennas.Length}");

            Vector3 c = bb.transform.position + Vector3.up * 0.12f;
            Vector3 camFront = c + bb.transform.forward * 0.55f + Vector3.up * 0.06f;
            Shot("TASK-09_Bobble_Seq_1.png", camFront, c, 40f);
            // Passo de jogo fixo de 1/90 s (como no óculos): no batchmode um frame real pode avançar ~0,1 s e as capturas
            // da pancada sairiam todas no mesmo instante.
            Time.captureDeltaTime = 1f / 90f;

            // Pancada média de 25° para o lado
            Quaternion baseRot = head.rotation;
            bb.KickTowards(bb.transform.right, 25f);
            float tk = Time.time;
            yield return null;
            Shot("TASK-09_Bobble_Seq_2.png", camFront, c, 40f);
            int seq2Frame = Time.frameCount;
            float peak = 0f, peakT = 0f, minSide = 0f, maxSide = 0f;
            bool seq3 = false, seq4 = false;
            Vector3 antennaStart = antennas[0].localEulerAngles;
            float antennaMax = 0f, springMax = 0f;
            while (Time.time < tk + 1.2f)
            {
                float d = bb.DeflectionDegrees;
                if (d > peak) { peak = d; peakT = Time.time - tk; }
                float side = Vector3.Dot(head.up, bb.transform.right);
                minSide = Mathf.Min(minSide, side); maxSide = Mathf.Max(maxSide, side);
                antennaMax = Mathf.Max(antennaMax, Quaternion.Angle(antennas[0].localRotation, Quaternion.Euler(antennaStart)));
                springMax = Mathf.Max(springMax, spring.localScale.y);
                if (!seq3 && side > 0.30f && Time.frameCount > seq2Frame + 1) { Shot("TASK-09_Bobble_Seq_3.png", camFront, c, 40f); seq3 = true; }
                if (seq3 && !seq4 && side < -0.25f) { Shot("TASK-09_Bobble_Seq_4.png", camFront, c, 40f); seq4 = true; }
                yield return null;
            }
            Time.captureDeltaTime = 0f;
            Check("2e) Pancada de 25° -> pico >= 18°", peak >= 18f, $"pico {peak:0.0}° em {peakT:0.00} s");
            Check("2f) Topo da cabeça vai para o lado empurrado e volta para o outro", maxSide > 0.3f && minSide < -0.25f, $"lado + {Mathf.Asin(Mathf.Clamp(maxSide, -1, 1)) * Mathf.Rad2Deg:0}° / lado − {Mathf.Asin(Mathf.Clamp(-minSide, -1, 1)) * Mathf.Rad2Deg:0}°");
            Check("2g) Antenas e mola acompanham (movimento secundário)", antennaMax > 3f && springMax > 1.005f, $"antena até {antennaMax:0.0}°, mola esticou até {springMax:0.000}x");
            float env = 0f;
            while (Time.time < tk + 1.75f) { if (Time.time >= tk + 1.4f) env = Mathf.Max(env, bb.DeflectionDegrees); yield return null; }
            Check("2h) Ainda balança >= 3° depois de 1,5 s", env >= 3f, $"{env:0.0}° entre 1,4 e 1,75 s");
            float env3 = 0f;
            while (Time.time < tk + 3.3f) { if (Time.time >= tk + 2.9f) env3 = Mathf.Max(env3, bb.DeflectionDegrees); yield return null; }
            Info($"amplitude em ~3 s: {env3:0.0}°");
            yield return 0.7f;
            Shot("TASK-09_Bobble_Seq_6.png", camFront, c, 40f);
            while (Time.time < tk + 8.2f) yield return null;
            Check("2i) Depois de ~8 s: parado (<= 0,2°) e Update desligado", bb.DeflectionDegrees <= 0.2f && bb.IsSleeping,
                $"{bb.DeflectionDegrees:0.000}°, dormindo={bb.IsSleeping}, cabeça voltou {Quaternion.Angle(head.rotation, baseRot):0.00}°");

            // Limite (tock)
            int tocks = bb.TockCount;
            bb.KickTowards(-bb.transform.right, 48f);
            float tl = Time.time, maxDef = 0f;
            bool seq5 = false;
            while (Time.time < tl + 0.6f)
            {
                maxDef = Mathf.Max(maxDef, bb.DeflectionDegrees);
                if (!seq5 && bb.TockCount > tocks) { Shot("TASK-09_Bobble_Seq_5.png", camFront, c, 40f); seq5 = true; }
                yield return null;
            }
            Check("2j) Pancada forte bate no limite (tock) e quica, sem passar de 50°", bb.TockCount > tocks && maxDef <= 50.01f, $"tocks +{bb.TockCount - tocks}, máx {maxDef:0.0}°, torção {bb.TwistDegrees:0.0}°");
            while (!bb.IsSleeping) yield return null;

            // Peteleco direcional (ponto atingido à direita -> cabeça vai para a esquerda)
            Vector3 hitPoint = head.position + head.up * 0.045f + bb.transform.right * 0.06f;
            bb.Flick(hitPoint, 30f);
            yield return 0.09f;
            float sideFlick = Vector3.Dot(head.up, bb.transform.right);
            Check("2k) Peteleco empurra para longe do ponto apontado", sideFlick < -0.25f, $"inclinação lateral {Mathf.Asin(Mathf.Clamp(sideFlick, -1, 1)) * Mathf.Rad2Deg:0}° (negativo = para longe)");
            while (!bb.IsSleeping) yield return null;

            // Gatilho com a mão (Select) do lado direito, segurar e puxar
            var handGo = new GameObject("~TestHandBobble");
            handGo.transform.position = head.position + bb.transform.right * 0.08f + Vector3.up * 0.04f;
            var hand = handGo.AddComponent<ScriptedTestInteractor>();
            yield return null;
            var xr = bb.GetComponent<XRSimpleInteractable>();
            Manager().SelectEnter((IXRSelectInteractor)hand, xr);
            yield return 0.09f;
            float selSide = Vector3.Dot(head.up, bb.transform.right);
            Check("2l) Gatilho (Select) com a mão = peteleco para longe da mão", selSide < -0.2f, $"inclinação {Mathf.Asin(Mathf.Clamp(selSide, -1, 1)) * Mathf.Rad2Deg:0}°");
            yield return 0.3f;
            Vector3 grab = handGo.transform.position;
            for (int i = 0; i < 20; i++) { handGo.transform.position = grab + bb.transform.forward * (0.005f * (i + 1)); yield return null; }
            yield return 0.45f;
            float pullFwd = Vector3.Dot(head.up, bb.transform.forward);
            Check("2m) Segurar e puxar: a cabeça segue a mão (>= 25° para a frente)", bb.IsPulling && pullFwd > 0.42f, $"puxando={bb.IsPulling}, inclinação {Mathf.Asin(Mathf.Clamp(pullFwd, -1, 1)) * Mathf.Rad2Deg:0}°");
            if (xr.isSelected) Manager().SelectExit((IXRSelectInteractor)hand, xr);
            float tr = Time.time, back = 0f;
            while (Time.time < tr + 0.35f) { back = Mathf.Min(back, Vector3.Dot(head.up, bb.transform.forward)); yield return null; }
            Check("2n) Ao soltar, balança para o outro lado", !bb.IsPulling && back < -0.25f, $"foi até {Mathf.Asin(Mathf.Clamp(-back, -1, 1)) * Mathf.Rad2Deg:0}° para trás");
            Object.Destroy(handGo);
            while (!bb.IsSleeping) yield return null;

            // Toque da mão em movimento (hover de interactor de mão)
            var h2 = new GameObject("~TestHandTouch");
            var touch = h2.AddComponent<ScriptedTestInteractor>();
            Vector3 dir = bb.transform.forward;     // mão vindo da frente para trás
            Vector3 start = head.position + Vector3.up * 0.05f + dir * 0.25f;
            h2.transform.position = start;
            yield return null;
            float speed = 1.0f;
            for (int i = 0; i < 6; i++) { h2.transform.position -= dir * speed * Time.deltaTime; yield return null; }
            Manager().HoverEnter((IXRHoverInteractor)touch, xr);
            for (int i = 0; i < 4; i++) { h2.transform.position -= dir * speed * Time.deltaTime; yield return null; }
            float touchPeak = 0f, touchDir = 0f;
            float th = Time.time;
            while (Time.time < th + 0.3f)
            {
                touchPeak = Mathf.Max(touchPeak, bb.DeflectionDegrees);
                touchDir = Mathf.Min(touchDir, Vector3.Dot(head.up, bb.transform.forward));
                yield return null;
            }
            if (xr.isHovered) Manager().HoverExit((IXRHoverInteractor)touch, xr);
            Check("2o) Toque da mão em movimento empurra no sentido da mão (10°-35°)", touchPeak >= 10f && touchPeak <= 40f && touchDir < -0.15f,
                $"pico {touchPeak:0.0}°, foi para trás {Mathf.Asin(Mathf.Clamp(-touchDir, -1, 1)) * Mathf.Rad2Deg:0}°");
            Object.Destroy(h2);
            while (!bb.IsSleeping) yield return null;

            // Objeto arremessado batendo na cabeça
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "~TestBall";
            ball.transform.localScale = Vector3.one * 0.06f;
            ball.transform.position = head.position + Vector3.up * 0.05f + bb.transform.forward * 0.25f;
            var rb = ball.AddComponent<Rigidbody>();
            rb.mass = 0.1f; rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = -bb.transform.forward * 2.0f;
            float tb = Time.time, ballPeak = 0f;
            while (Time.time < tb + 0.6f) { ballPeak = Mathf.Max(ballPeak, bb.DeflectionDegrees); yield return null; }
            Check("2p) Bola arremessada (2 m/s) balança a cabeça proporcionalmente", ballPeak >= 10f && ballPeak <= 35f, $"pico {ballPeak:0.0}°");
            Object.Destroy(ball);
            while (!bb.IsSleeping) yield return null;

            // Sons
            Check("2q) Sons tocaram: boing, soft (cruzando o centro) e tock (limite)", bb.BoingCount > 0 && bb.SoftCount > 0 && bb.TockCount > 0,
                $"boing {bb.BoingCount}, soft {bb.SoftCount}, tock {bb.TockCount}");

            // GC: 300 Updates sem alocar
            bb.KickTowards(bb.transform.right, 20f);
            var upd = (Action)Delegate.CreateDelegate(typeof(Action), bb, "Update");
            upd();
            long a0 = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 300; i++) upd();
            long a1 = GC.GetAllocatedBytesForCurrentThread();
            Check("2r) 300 Updates do bobblehead com 0 bytes alocados", a1 - a0 == 0, $"{a1 - a0} bytes");
        }

        private static void GlobalChecks(FireworksButtonInteractable fw, BobbleheadSpringToy bb)
        {
            log.AppendLine();
            log.AppendLine("## 3. Regras gerais");
            string dir = "Assets/Scripts/Interactive/Task09";
            var offenders = new List<string>();
            foreach (string f in new[] { "FireworksButtonInteractable.cs", "BobbleheadSpringToy.cs" })
            {
                string txt = File.ReadAllText(Path.Combine(dir, f));
                if (txt.Contains("LoadScene")) offenders.Add(f + ": LoadScene");
                if (txt.Contains(".material ") || txt.Contains(".material;") || txt.Contains(".material.") || txt.Contains(".materials")) offenders.Add(f + ": renderer.material");
                if (txt.Contains("// TODO")) offenders.Add(f + ": TODO");
            }
            Check("3a) Sem LoadScene, sem renderer.material/.materials, sem TODO nos scripts refeitos", offenders.Count == 0, offenders.Count == 0 ? "ok" : string.Join("; ", offenders));
            Check("3b) Nenhum MeshCollider em objeto com Rigidbody (botão e bobblehead)",
                !fw.GetComponentsInChildren<MeshCollider>(true).Any() && !bb.GetComponentsInChildren<MeshCollider>(true).Any(), "");
            var srcs = fw.GetComponentsInChildren<AudioSource>(true).Concat(bb.GetComponentsInChildren<AudioSource>(true)).ToList();
            Check("3c) Áudio 3D sem loop", srcs.All(s => !s.loop && s.spatialBlend >= 0.99f), $"{srcs.Count} fontes");
        }
    }
}
