using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core.Testing;
using VRRehab.Interactive.Task16;
using VRRehab.Persistence;
using Object = UnityEngine.Object;

namespace VRRehab.EditorTools
{
    /// <summary>
    /// Testes em PLAY MODE real da estante de troféus (TASK-16, retoque) no Menu_Principal, com capturas offscreen.
    /// Uma "mão de teste" (ScriptedTestInteractor) pega cada troféu pelo XRInteractionManager, leva para longe, solta,
    /// coloca perto do pedestal, deixa cair no chão, encaixa a coroa no Drone etc. Nada é salvo; o save JSON do
    /// paciente é copiado antes e restaurado no fim.
    /// Rodar: Unity.exe -batchmode (SEM -quit) -executeMethod VRRehab.EditorTools.Task16PlayModeTests.Run
    /// Relatório: .agents/visual_qa/TASK-16_PlayModeTest.txt
    /// </summary>
    [InitializeOnLoad]
    public static class Task16PlayModeTests
    {
        private const string StateKey = "Task16PlayModeTests.state";
        private const string FailKey = "Task16PlayModeTests.fail";
        private const string ReportPath = ".agents/visual_qa/TASK-16_PlayModeTest.txt";
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
        private static string savePath;
        private static byte[] saveBackup;

        static Task16PlayModeTests()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("ExoTao IC/Menu/Testes em Play Mode (TASK-16 Estante de Troféus)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) return;
            savePath = Path.Combine(Application.persistentDataPath, "vr_rehab_save.json");
            SessionState.SetString("Task16PlayModeTests.savePath", savePath);
            SessionState.SetString("Task16PlayModeTests.saveBackup", File.Exists(savePath) ? Convert.ToBase64String(File.ReadAllBytes(savePath)) : "");
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
            // Restaura o save do paciente exatamente como estava antes do teste
            string p = SessionState.GetString("Task16PlayModeTests.savePath", "");
            string b = SessionState.GetString("Task16PlayModeTests.saveBackup", "");
            if (!string.IsNullOrEmpty(p))
            {
                if (b.Length > 0) File.WriteAllBytes(p, Convert.FromBase64String(b));
                else if (File.Exists(p)) File.Delete(p);
            }
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
                waitUntil = 0f; waitFrame = -1;
                routine = Tests();
                stack.Clear();
                stack.Push(routine);
            }
            if (EditorApplication.timeSinceStartup - startRealTime > TimeoutSeconds)
            {
                Check("TEMPO ESGOTADO", false, $"o teste passou de {TimeoutSeconds} s");
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
                Check("EXCEÇÃO no teste", false, e.ToString());
                Finish();
            }
        }

        private static void Finish()
        {
            Time.captureDeltaTime = 0f;
            log.AppendLine();
            log.AppendLine($"--- RESULTADO: {pass} passaram, {fail} falharam ---");
            File.WriteAllText(ReportPath, log.ToString());
            Debug.Log("[Task16PlayModeTests]\n" + log);
            SessionState.SetInt(FailKey, fail);
            SessionState.SetInt(StateKey, 2);
            routine = null;
            stack.Clear();
            EditorApplication.ExitPlaymode();
        }

        private static void Check(string name, bool ok, string detail)
        {
            if (ok) pass++; else fail++;
            log.AppendLine($"[{(ok ? "PASS" : "FAIL")}] {name}: {detail}");
        }
        private static void Info(string s) => log.AppendLine("      " + s);
        private static string V(Vector3 v) => $"({v.x:0.000}, {v.y:0.000}, {v.z:0.000})";
        private static XRInteractionManager Manager() => Object.FindFirstObjectByType<XRInteractionManager>();

        private static void Shot(string file, Vector3 pos, Vector3 lookAt, float fov)
        {
            var go = new GameObject("__Task16TestCam");
            var cam = go.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation((lookAt - pos).normalized, Vector3.up));
            cam.fieldOfView = fov; cam.nearClipPlane = 0.02f; cam.farClipPlane = 60f;
            cam.clearFlags = CameraClearFlags.Skybox; cam.allowHDR = false; cam.allowMSAA = false; cam.enabled = false;
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(QaDir, file), tex.EncodeToPNG());
            cam.targetTexture = null;
            Object.Destroy(tex); rt.Release(); Object.Destroy(rt); Object.Destroy(go);
        }

        // Poses fixas da estante (raiz, âncoras, base, placas, rótulos) para provar que nada se move
        private static List<(Transform t, Vector3 p, Quaternion r)> Snapshot(TrophyShelfManager shelf)
        {
            var list = new List<(Transform, Vector3, Quaternion)> { (shelf.transform, shelf.transform.position, shelf.transform.rotation) };
            foreach (var t in shelf.GetComponentsInChildren<Transform>(true))
            {
                if (t == shelf.transform || t.GetComponentInParent<ShelfTrophyGrabInteractable>() != null) continue;
                if (t.name == "InfoCard" || t.IsChildOf(shelf.infoCard) || t.name == "TrophyAudio") continue;
                list.Add((t, t.position, t.rotation));
            }
            return list;
        }

        private static float MaxDrift(List<(Transform t, Vector3 p, Quaternion r)> snap, out string who)
        {
            float max = 0f; who = "-";
            foreach (var (t, p, r) in snap)
            {
                if (t == null) continue;
                float d = Vector3.Distance(t.position, p) + Quaternion.Angle(t.rotation, r) * 0.001f;
                if (d > max) { max = d; who = t.name; }
            }
            return max;
        }

        private static IEnumerator Grab(ScriptedTestInteractor hand, ShelfTrophyGrabInteractable t)
        {
            hand.transform.SetPositionAndRotation(t.transform.position + Vector3.up * 0.05f, t.transform.rotation);
            yield return null;
            Manager().SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)t);
            yield return 0.35f;                         // ease-in da pegada
        }

        private static IEnumerator MoveHand(ScriptedTestInteractor hand, Vector3 to, float seconds)
        {
            Vector3 from = hand.transform.position;
            float t0 = Time.time;
            while (Time.time < t0 + seconds)
            {
                hand.transform.position = Vector3.Lerp(from, to, (Time.time - t0) / seconds);
                yield return null;
            }
            hand.transform.position = to;
            yield return 0.2f;
        }

        private static void Release(ScriptedTestInteractor hand, ShelfTrophyGrabInteractable t)
        {
            if (t.isSelected) Manager().SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)t);
        }

        /// <summary>Pega a coroa da cabeça do Drone, leva para longe, solta e confere que ela volta ao pedestal.</summary>
        private static IEnumerator TakeCrownFromDrone(TrophyShelfManager shelf, TrophyShelfManager.TrophySlot cs, DroneCrownWearable crown, ScriptedTestInteractor hand, Transform st)
        {
            hand.transform.position = cs.trophyObject.transform.position;
            yield return null;
            Info($"antes de pegar: equipada={crown.isEquippedOnDrone}, selecionada por [{string.Join(",", cs.grab.interactorsSelecting.Select(i => i.transform.name))}], " +
                 $"enabled={cs.grab.enabled}, registrada={Manager().IsRegistered((IXRInteractable)cs.grab)}, camadas {cs.grab.interactionLayers.value}, " +
                 $"mão camadas {hand.interactionLayers.value}");
            Manager().SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)cs.grab);
            yield return 0.4f;
            Info($"depois de pegar: equipada={crown.isEquippedOnDrone}, selecionada por [{string.Join(",", cs.grab.interactorsSelecting.Select(i => i.transform.name))}], pai {cs.trophyObject.transform.parent?.name}");
            Check("6d) Pegar a coroa da cabeça do Drone desencaixa", !crown.isEquippedOnDrone && cs.grab.isSelected, "");
            yield return MoveHand(hand, hand.transform.position + Vector3.up * 0.3f + (st.position - hand.transform.position).normalized * 0.6f, 0.4f);
            Release(hand, cs.grab);
            float t0 = Time.time;
            while (!cs.grab.IsDocked && Time.time < t0 + 4f) yield return null;
            yield return 0.1f;
            Check("6e) Solta longe: volta ao pedestal, de volta na estante", cs.grab.IsDocked && cs.trophyObject.transform.IsChildOf(shelf.transform)
                && Vector3.Distance(cs.trophyObject.transform.position, cs.grab.HomePosition) < 0.005f && cs.plaqueText.text == cs.title,
                $"em {Time.time - t0:0.00} s, placa '{cs.plaqueText.text}'");
        }

        // ------------------------------------------------------------------ testes
        private static IEnumerator Tests()
        {
            log.AppendLine($"=== Testes em Play Mode — TASK-16 Estante de Troféus (retoque) ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");
            log.AppendLine("Mão de teste do XRI (ScriptedTestInteractor) pegando e soltando pelo XRInteractionManager. Passo de jogo fixo de 1/90 s.");
            log.AppendLine();
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 90;
            Time.captureDeltaTime = 1f / 90f;
            yield return 1.5f;

            var shelves = Object.FindObjectsByType<TrophyShelfManager>(FindObjectsSortMode.None);
            var trophies = Object.FindObjectsByType<ShelfTrophyGrabInteractable>(FindObjectsSortMode.None);
            Check("0a) 1 estante e 4 troféus", shelves.Length == 1 && trophies.Length == 4, $"estantes {shelves.Length}, troféus {trophies.Length}");
            if (shelves.Length != 1) yield break;
            var shelf = shelves[0];
            var crownSlot = shelf.GetSlot(RehabProgressionSaveSystem.TrophyIdDroneCrown);
            bool crownStartsOnDrone = crownSlot.onDrone;
            Info($"Coroa começa {(crownStartsOnDrone ? "NA CABEÇA DO DRONE (save diz coroado)" : "na estante")}.");
            Check("0b) Troféus da estante começam encaixados, cinemáticos e parados no pedestal", shelf.slots.Where(s => !s.onDrone).All(s => s.grab != null && s.grab.IsDocked
                && s.grab.GetComponent<Rigidbody>().isKinematic && Vector3.Distance(s.grab.transform.position, s.grab.HomePosition) < 0.005f), "");
            if (crownStartsOnDrone)
                Check("0b2) Coroa no Drone: placa 'Coroa no Drone' no pedestal vazio", crownSlot.plaqueText.text == "Coroa no Drone", $"'{crownSlot.plaqueText.text}'");
            var labels = shelf.slots.Select(s => s.plaqueText).ToList();
            string texts = string.Join(" | ", labels.Select(l => l.text));
            bool labelsOk = labels.All(l => l != null && l.fontSize < 0.5f && l.transform.parent != null && l.transform.parent.name.StartsWith("Stand_Plaque"));
            foreach (var l in labels)
            {
                l.ForceMeshUpdate();
                Bounds tb = l.textBounds;
                if (tb.size.x > l.rectTransform.sizeDelta.x + 0.002f || tb.size.y > l.rectTransform.sizeDelta.y + 0.004f) labelsOk = false;
            }
            Check("0c) Rótulos nas placas, com acento, dentro da placa (fontSize real, nada de 1)", labelsOk && texts.Contains("Maçã Dourada") && texts.Contains("Taça Estelar"),
                texts + " | tamanhos " + string.Join(", ", labels.Select(l => $"{l.fontSize:0.000}")));
            Check("0d) Luzes da estante sem sombra", shelf.GetComponentsInChildren<Light>(true).All(l => l.shadows == LightShadows.None), "");
            Check("0e) Partículas de desbloqueio <= 40", shelf.slots.All(s => s.unlockParticles == null || s.unlockParticles.main.maxParticles <= 40), "");

            Transform st = shelf.transform;
            Vector3 camFront = st.position + st.forward * 1.15f + Vector3.up * 0.35f;
            Vector3 camLook = st.position + Vector3.up * 0.14f;

            var handGo = new GameObject("~TestHand");
            var hand = handGo.AddComponent<ScriptedTestInteractor>();
            var snap = Snapshot(shelf);

            // 1. Pegar, levar 1,5 m, soltar longe -> volta sozinho; a estante não se move
            log.AppendLine();
            log.AppendLine("## 1. Pegar, levar 1,5 m e soltar longe");
            bool firstShots = true;
            foreach (var s in shelf.slots)
            {
                if (s.onDrone) { Info($"{s.title} está no Drone: testada na seção 6."); continue; }
                var t = s.grab;
                yield return Grab(hand, t);
                Check($"1a) {s.title}: pego", t.isSelected && !t.IsDocked, "");
                if (firstShots)
                {
                    var hoverTarget = (IXRHoverInteractable)t;
                    Manager().HoverEnter((IXRHoverInteractor)hand, hoverTarget);
                    yield return null;
                }
                Vector3 away = t.transform.position + st.forward * 1.2f + Vector3.up * 0.15f + st.right * 0.9f;
                yield return MoveHand(hand, away, 0.6f);
                if (firstShots)
                {
                    Shot("TASK-16_Interaction_1.png", camFront + st.forward * 0.6f + st.right * 0.4f, (camLook + hand.transform.position) * 0.5f, 62f);
                    Check("1b) Cartão de informação aparece segurando/apontando", shelf.InfoCardVisible && shelf.InfoCardTitle == s.title, $"visível={shelf.InfoCardVisible}, título '{shelf.InfoCardTitle}'");
                }
                float drift = MaxDrift(snap, out string who);
                Check($"1c) {s.title}: estante parada enquanto o troféu está na mão", drift < 0.001f, $"maior deslocamento {drift * 1000f:0.000} mm ({who})");
                Vector3 releasedAt = t.transform.position;
                Release(hand, t);
                if (firstShots)
                {
                    var hoverTarget = (IXRHoverInteractable)t;
                    if (t.isHovered) Manager().HoverExit((IXRHoverInteractor)hand, hoverTarget);
                }
                float tr = Time.time;
                bool shot2 = false, shot3 = false;
                float returnStart = -1f;
                while (!t.IsDocked && Time.time < tr + 4f)
                {
                    if (firstShots && !shot2 && Time.time > tr + 0.3f) { Shot("TASK-16_Interaction_2.png", camFront + st.forward * 0.8f + st.right * 0.6f, (camLook + releasedAt) * 0.5f, 65f); shot2 = true; }
                    if (t.IsReturningHome && returnStart < 0f) returnStart = Time.time;
                    if (firstShots && !shot3 && returnStart > 0f && Time.time > returnStart + 0.3f)
                    {
                        Shot("TASK-16_Interaction_3.png", camFront + st.forward * 0.6f + st.right * 0.3f, (camLook + t.transform.position) * 0.5f, 65f);   // voltando em arco
                        shot3 = true;
                    }
                    yield return null;
                }
                float back = Time.time - tr;
                yield return null;
                var rb = t.GetComponent<Rigidbody>();
                float err = Vector3.Distance(t.transform.position, t.HomePosition);
                Check($"1d) {s.title}: volta ao pedestal sozinho em <= 2,5 s e fica parado", t.IsDocked && back <= 2.5f && err < 0.005f && rb.isKinematic,
                    $"voltou em {back:0.00} s, erro {err * 1000f:0.0} mm, cinemático={rb.isKinematic}");
                if (firstShots)
                {
                    Check("1e) Cartão some depois que o laser sai e o troféu é solto", !shelf.InfoCardVisible, $"visível={shelf.InfoCardVisible}");
                    yield return 0.08f;
                    Shot("TASK-16_Interaction_4.png", camFront, camLook, 50f);       // de volta, anel brilhando
                }
                drift = MaxDrift(snap, out who);
                Check($"1f) {s.title}: raiz/âncoras/base/placas não se moveram (nem 1 mm)", drift < 0.001f, $"{drift * 1000f:0.000} mm ({who})");
                firstShots = false;
                yield return 0.3f;
            }

            // 2. Soltar perto do pedestal e devagar -> encaixa na hora
            log.AppendLine();
            log.AppendLine("## 2. Encaixe imediato (solto a ~0,12 m do pedestal, devagar)");
            {
                var t = shelf.slots[1].grab;
                yield return Grab(hand, t);
                yield return MoveHand(hand, t.HomePosition + st.forward * 0.12f + Vector3.up * 0.06f, 0.5f);
                yield return 0.3f;
                int docks = t.DockCount;
                Release(hand, t);
                float t0 = Time.time;
                while (t.DockCount == docks && Time.time < t0 + 2f) yield return null;
                float dt = Time.time - t0;
                Check("2a) Encaixa em <= 0,4 s", t.IsDocked && dt <= 0.4f, $"{dt:0.00} s");
                yield return 0.3f;
            }

            // 3. Largar no chão -> volta logo
            log.AppendLine();
            log.AppendLine("## 3. Largar no chão");
            {
                var t = shelf.slots[0].grab;
                yield return Grab(hand, t);
                Vector3 floorSpot = st.position + st.forward * 0.7f;
                floorSpot.y = 0.55f;
                yield return MoveHand(hand, floorSpot, 0.6f);
                Release(hand, t);
                float t0 = Time.time, crossed = -1f, minY = 99f;
                while (!t.IsDocked && Time.time < t0 + 4f)
                {
                    minY = Mathf.Min(minY, t.transform.position.y);
                    if (crossed < 0f && t.transform.position.y < t.RespawnFloorY) crossed = Time.time;
                    yield return null;
                }
                float total = Time.time - t0;
                float afterCross = crossed > 0f ? Time.time - crossed : total;
                Check("3a) Caiu no chão e voltou em <= 1,5 s (a partir de passar da cota do chão)", t.IsDocked && afterCross <= 1.5f,
                    $"total {total:0.00} s, após passar de y {t.RespawnFloorY:0.00}: {afterCross:0.00} s, menor y {minY:0.00}");
                yield return 0.3f;
            }

            // 4. Repouso
            log.AppendLine();
            log.AppendLine("## 4. Repouso (3 s)");
            {
                var resting = shelf.slots.Where(s => !s.onDrone).ToList();
                var pos0 = resting.Select(s => s.grab.transform.position).ToArray();
                yield return 3f;
                float drift = resting.Select((s, i) => Vector3.Distance(s.grab.transform.position, pos0[i])).Max();
                var cols = shelf.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger && c.enabled).ToArray();
                float maxPen = 0f; string pair = "-";
                for (int i = 0; i < cols.Length; i++)
                    for (int j = i + 1; j < cols.Length; j++)
                    {
                        bool sameBody = cols[i].attachedRigidbody != null && cols[i].attachedRigidbody == cols[j].attachedRigidbody;
                        if (sameBody) continue;
                        if (cols[i].attachedRigidbody == null && cols[j].attachedRigidbody == null) continue;   // base com base (estático)
                        if (Physics.ComputePenetration(cols[i], cols[i].transform.position, cols[i].transform.rotation,
                                                       cols[j], cols[j].transform.position, cols[j].transform.rotation, out _, out float d) && d > maxPen)
                        { maxPen = d; pair = cols[i].name + " x " + cols[j].name; }
                    }
                Check("4a) Nenhum troféu se mexe em repouso (<= 1 mm em 3 s, girando no lugar)", drift <= 0.001f, $"{drift * 1000f:0.000} mm");
                Check("4b) Nenhum colisor se sobrepondo em repouso", maxPen < 0.0005f, $"penetração máx. {maxPen * 1000f:0.00} mm ({pair})");
            }

            // 5. Bloqueado (silhueta, não pegável, placa com cadeado)
            log.AppendLine();
            log.AppendLine("## 5. Troféu bloqueado");
            var save = RehabProgressionSaveSystem.Instance;
            if (save != null)
            {
                bool demo = save.unlockAllForDemo;
                bool cup = save.Data.trophyStarCupUnlocked;
                save.unlockAllForDemo = false;
                save.Data.trophyStarCupUnlocked = false;
                shelf.RefreshShelfState(save.Data);
                yield return 0.2f;
                var s = shelf.GetSlot(RehabProgressionSaveSystem.TrophyIdStarCup);
                var r = s.trophyObject.GetComponentInChildren<MeshRenderer>();
                Check("5a) Bloqueado: silhueta, não pegável, placa 'Bloqueado' com cadeado", !s.grab.enabled && r.sharedMaterial == shelf.lockedSilhouetteMaterial
                    && s.plaqueText.text == "Bloqueado" && s.plaqueLockIcon != null && s.plaqueLockIcon.enabled,
                    $"pegável={s.grab.enabled}, material {r.sharedMaterial?.name}, placa '{s.plaqueText.text}'");
                Shot("TASK-16_Locked_Silhouette.png", st.position + st.forward * 0.75f + Vector3.up * 0.25f, s.trophyObject.transform.position + Vector3.up * 0.03f, 45f);
                save.Data.trophyStarCupUnlocked = cup;
                save.unlockAllForDemo = demo;
                shelf.RefreshShelfState(save.Data);
                yield return 0.2f;
                Check("5b) Desbloqueado de novo: material normal e pegável", s.grab.enabled && r.sharedMaterial != shelf.lockedSilhouetteMaterial && s.plaqueText.text == s.title, "");
            }
            else Check("5) Save system presente", false, "RehabProgressionSaveSystem não encontrado");

            // 6. Coroa no Drone
            log.AppendLine();
            log.AppendLine("## 6. Coroa do Drone");
            {
                var cs = shelf.GetSlot(RehabProgressionSaveSystem.TrophyIdDroneCrown);
                var crown = cs.trophyObject.GetComponent<DroneCrownWearable>();
                crown.AutoLocateDroneReferences();
                Transform head = crown.droneHeadSocketTransform;
                if (head == null) { Check("6) Socket da cabeça do Drone", false, "DroneHeadSocket não encontrado"); }
                else
                {
                    if (crown.isEquippedOnDrone)
                    {
                        // Começou no Drone: primeiro tira e devolve para a estante, depois testa o encaixe
                        yield return TakeCrownFromDrone(shelf, cs, crown, hand, st);
                        yield return 0.4f;
                    }
                    yield return Grab(hand, cs.grab);
                    float t0 = Time.time;
                    Info($"levando a coroa até a cabeça do Drone ({Vector3.Distance(hand.transform.position, head.position):0.0} m)");
                    while (!crown.isEquippedOnDrone && Time.time < t0 + 8f)
                    {
                        hand.transform.position = Vector3.MoveTowards(hand.transform.position, head.position, 2.5f * Time.deltaTime);
                        yield return null;
                    }
                    Info($"coroa a {Vector3.Distance(cs.trophyObject.transform.position, head.position):0.00} m da cabeça após {Time.time - t0:0.0} s");
                    yield return 0.3f;
                    Check("6a) Encostar na cabeça do Drone encaixa a coroa", crown.isEquippedOnDrone && cs.trophyObject.transform.IsChildOf(head),
                        $"equipada={crown.isEquippedOnDrone}, pai {cs.trophyObject.transform.parent?.name}");
                    Check("6b) Placa 'Coroa no Drone' e anel apagado", cs.plaqueText.text == "Coroa no Drone", $"placa '{cs.plaqueText.text}'");
                    Shot("TASK-16_Crown_On_Drone.png", st.position + st.forward * 0.8f + Vector3.up * 0.3f, cs.slotAnchor.position + Vector3.up * 0.04f, 50f);
                    yield return 1.6f;
                    Check("6c) Coroa no Drone não é puxada de volta para a estante", crown.isEquippedOnDrone, "");

                    if (!crownStartsOnDrone) yield return TakeCrownFromDrone(shelf, cs, crown, hand, st);
                    else Info("Coroa deixada no Drone, como no início (o save do paciente é restaurado no fim).");
                }
            }
            float driftEnd = MaxDrift(snap, out string whoEnd);
            Check("6f) Depois de tudo: a estante continua exatamente no lugar", driftEnd < 0.001f, $"{driftEnd * 1000f:0.000} mm ({whoEnd})");

            // 7. Cartão no hover (laser) e some 0,3 s depois
            log.AppendLine();
            log.AppendLine("## 7. Cartão de informação e desempenho");
            {
                var s = shelf.slots[2];
                hand.transform.position = s.grab.transform.position + st.forward * 0.6f;
                Manager().HoverEnter((IXRHoverInteractor)hand, (IXRHoverInteractable)s.grab);
                yield return 0.1f;
                bool shown = shelf.InfoCardVisible && shelf.InfoCardTitle == s.title;
                Shot("TASK-16_Interaction_5.png", st.position + st.forward * 0.85f + Vector3.up * 0.3f, s.trophyObject.transform.position + st.forward * 0.15f + Vector3.up * 0.1f, 55f);
                Manager().HoverExit((IXRHoverInteractor)hand, (IXRHoverInteractable)s.grab);
                yield return 0.15f;
                bool stillAt015 = shelf.InfoCardVisible;
                yield return 0.3f;
                Check("7a) Hover mostra o cartão; some ~0,3 s depois de sair", shown && stillAt015 && !shelf.InfoCardVisible, $"mostrou={shown}, aos 0,15 s ainda visível={stillAt015}, depois={shelf.InfoCardVisible}");
            }
            {
                var upd = (Action)Delegate.CreateDelegate(typeof(Action), shelf, "Update");
                upd();
                long a0 = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 300; i++) upd();
                long a1 = GC.GetAllocatedBytesForCurrentThread();
                Check("7b) 300 frames de idle (giro + halos + cartão) com 0 bytes alocados", a1 - a0 == 0, $"{a1 - a0} bytes");
            }
            {
                var offenders = new List<string>();
                foreach (var f in Directory.GetFiles("Assets/Scripts/Interactive/Task16", "*.cs"))
                {
                    string code = File.ReadAllText(f);
                    if (code.Contains("SceneManager." + "LoadScene")) offenders.Add(Path.GetFileName(f) + ": LoadScene");
                    if (code.Contains(".material ") || code.Contains(".material;") || code.Contains(".material.") || code.Contains(".materials")) offenders.Add(Path.GetFileName(f) + ": .material");
                    if (code.Contains("// " + "TODO")) offenders.Add(Path.GetFileName(f) + ": TODO");
                }
                Check("7c) Sem LoadScene, renderer.material ou TODO nos scripts da Task16", offenders.Count == 0, offenders.Count == 0 ? "ok" : string.Join("; ", offenders));
                Check("7d) Nenhum MeshCollider em objeto com Rigidbody da estante", !shelf.GetComponentsInChildren<MeshCollider>(true).Any(c => c.attachedRigidbody != null), "");
            }
            Object.Destroy(handGo);
        }
    }
}
