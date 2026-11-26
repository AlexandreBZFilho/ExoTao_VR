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
using VRRehab.Interactive.Task06;
using Object = UnityEngine.Object;

namespace VRRehab.EditorTools
{
    /// <summary>
    /// Testes em PLAY MODE real da TASK-06 (retoque das músicas): cada fita encaixada no deck troca a música do rádio
    /// para a dela (clip certo, tocando, volume de volta ao normal após o crossfade), a fita seguinte troca de novo,
    /// e ejetar volta para a música padrão do rádio (Music_SciFi_Menu). Nada é salvo.
    /// Rodar: Unity.exe -batchmode (SEM -quit) -executeMethod VRRehab.EditorTools.Task06MusicPlayModeTests.Run
    /// Relatório: .agents/visual_qa/TASK-06_MusicPlayModeTest.txt
    /// </summary>
    [InitializeOnLoad]
    public static class Task06MusicPlayModeTests
    {
        private const string StateKey = "Task06MusicPlayModeTests.state";
        private const string FailKey = "Task06MusicPlayModeTests.fail";
        private const string ReportPath = ".agents/visual_qa/TASK-06_MusicPlayModeTest.txt";
        private const string MenuScenePath = "Assets/Scenes/Menu_Principal.unity";
        private const float TimeoutSeconds = 300f;

        private static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        private static IEnumerator routine;
        private static float waitUntil;
        private static int waitFrame = -1;
        private static double startRealTime;
        private static StringBuilder log;
        private static int pass, fail;

        static Task06MusicPlayModeTests()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("ExoTao IC/Menu/Testes em Play Mode (TASK-06 Músicas das Fitas)")]
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
            log.AppendLine();
            log.AppendLine($"--- RESULTADO: {pass} passaram, {fail} falharam ---");
            File.WriteAllText(ReportPath, log.ToString());
            Debug.Log("[Task06MusicPlayModeTests]\n" + log);
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

        private static XRInteractionManager Manager() => Object.FindFirstObjectByType<XRInteractionManager>();

        private static float ExpectedVolume(RadioMenu3D radio)
        {
            float master = GerenciadorAudioRehab.Instancia != null ? GerenciadorAudioRehab.Instancia.MusicVol : 0.78f;
            return radio.ligado ? radio.volumeBase * master : 0f;
        }

        private static string ClipName(AudioSource a) => a.clip != null ? a.clip.name : "(nenhum)";

        private static void Release(MusicCassetteTape tape)
        {
            var m = Manager();
            foreach (var i in tape.interactorsSelecting.ToList()) m.SelectExit(i, (IXRSelectInteractable)tape);
        }

        private static readonly Dictionary<MusicCassetteTape, Pose> restPose = new Dictionary<MusicCassetteTape, Pose>();

        /// <summary>
        /// Simula a mão tirando a fita do deck: solta do socket e leva a fita de volta para onde ela estava descansando
        /// (se ficasse parada dentro do slot, o socket a recapturaria — a mão do paciente não deixa isso acontecer).
        /// </summary>
        private static IEnumerator TakeOut(RadioCassetteSocket socket, MusicCassetteTape tape)
        {
            // Enquanto a mão segura a fita o socket não pode pegá-la; aqui isso vira socketActive = false por alguns
            // passos de física (o trigger do socket só "esquece" a fita no próximo FixedUpdate).
            socket.socketActive = false;
            if (tape.isSelected && tape.interactorsSelecting.Contains(socket)) Manager().SelectExit((IXRSelectInteractor)socket, (IXRSelectInteractable)tape);
            if (restPose.TryGetValue(tape, out Pose p))
            {
                var rb = tape.GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                tape.transform.SetPositionAndRotation(p.position, p.rotation);
                if (rb != null) { rb.position = p.position; rb.rotation = p.rotation; }
                Physics.SyncTransforms();
            }
            yield return 0.25f;
            socket.socketActive = true;
        }

        private static IEnumerator Tests()
        {
            log.AppendLine($"=== Testes em Play Mode — TASK-06 Músicas das fitas ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");
            log.AppendLine("Encaixe/retirada pela XRInteractionManager (como o XRI faz) e Eject pelo método do botão físico.");
            log.AppendLine($"Áudio: {(AudioSettings.GetConfiguration().dspBufferSize > 0 ? "ativo" : "?")}, taxa {AudioSettings.outputSampleRate} Hz.");
            log.AppendLine();
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 90;
            yield return 2.0f;

            var socket = Object.FindFirstObjectByType<RadioCassetteSocket>();
            var tapes = Object.FindObjectsByType<MusicCassetteTape>(FindObjectsSortMode.None).OrderBy(t => t.tapeId).ToList();
            Check("0a) Deck e 4 fitas na cena", socket != null && tapes.Count == 4, $"deck={(socket != null)}, fitas={tapes.Count}");
            if (socket == null || tapes.Count != 4) yield break;
            var radio = socket.radioTarget != null ? socket.radioTarget : RadioMenu3D.Instancia;
            AudioSource src = radio.audioFonte;
            var defaultClip = (AudioClip)typeof(RadioCassetteSocket).GetField("defaultRadioClip", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(socket);
            Check("0b) Música padrão do rádio = Music_SciFi_Menu", defaultClip != null && defaultClip.name == "Music_SciFi_Menu", defaultClip != null ? defaultClip.name : "nula");
            Check("0c) As 4 fitas têm músicas distintas e diferentes da padrão", tapes.Select(t => t.musicClip).Distinct().Count() == 4 && tapes.All(t => t.musicClip != null && t.musicClip != defaultClip),
                string.Join(", ", tapes.Select(t => $"{t.tapeTitle}={t.musicClip?.name}")));
            float wait = socket.crossfadeTime + 0.35f;

            // Esvazia o deck se a cena começar com fita encaixada
            if (socket.CurrentlySocketedTape != null)
            {
                log.AppendLine($"      deck começou com '{socket.CurrentlySocketedTape.tapeTitle}' -> Eject");
                socket.EjectCurrentTape();
                yield return wait + socket.ejectIgnoreCollisionSeconds + socket.ejectRecaptureCooldownSeconds + 0.3f;
            }
            // Onde a "mão" devolve cada fita: o slot do rack dela (é onde o paciente guarda a fita). Se a fita não tiver
            // slot e estiver perto do deck (a que começou encaixada e foi ejetada), usa um slot livre do rack.
            restPose.Clear();
            var rack = Object.FindFirstObjectByType<CassetteOrganizerRack>();
            Vector3 deckPos = socket.transform.position;
            foreach (var t in tapes)
            {
                Transform slot = t.AssignedRackSlot;
                if (slot == null && rack != null && Vector3.Distance(t.transform.position, deckPos) < 0.35f)
                {
                    for (int i = 0; i < rack.SlotCount && slot == null; i++)
                    {
                        Transform cand = rack.GetSlot(i);
                        if (cand != null && tapes.All(o => o == t || Vector3.Distance(o.transform.position, cand.position) > 0.05f)) slot = cand;
                    }
                }
                Pose rest = slot != null ? new Pose(slot.position, slot.rotation) : new Pose(t.transform.position, t.transform.rotation);
                if (slot == null && Vector3.Distance(rest.position, deckPos) < 0.30f)
                {
                    // Continua a fileira das outras fitas do rack (espaçamento entre elas), longe do deck
                    var others = tapes.Where(o => o != t).Select(o => o.transform).OrderBy(o => Vector3.Distance(o.position, deckPos)).ToList();
                    Vector3 step = others[others.Count - 1].position - others[others.Count - 2].position;
                    var last = others[others.Count - 1];
                    rest = new Pose(last.position + step, last.rotation);
                }
                restPose[t] = rest;
                log.AppendLine($"      '{t.tapeTitle}' é devolvida a {Vector3.Distance(restPose[t].position, deckPos):0.00} m do deck");
            }
            Check("0d) Sem fita: rádio toca a música padrão", src.clip == defaultClip && src.isPlaying, $"{ClipName(src)}, tocando={src.isPlaying}");

            AudioClip previous = src.clip;
            foreach (var tape in tapes)
            {
                Release(tape);
                yield return null;
                Manager().SelectEnter((IXRSelectInteractor)socket, (IXRSelectInteractable)tape);
                yield return wait;
                bool ok = src.clip == tape.musicClip && src.isPlaying && Mathf.Abs(src.volume - ExpectedVolume(radio)) < 0.02f && src.clip != previous;
                Check($"1) Encaixar '{tape.tapeTitle}' troca a música do rádio", ok,
                    $"tocando '{ClipName(src)}' (esperado '{tape.musicClip.name}'), isPlaying={src.isPlaying}, volume {src.volume:0.00} (esperado {ExpectedVolume(radio):0.00}), " +
                    $"tempo {src.time:0.0} s de {tape.musicClip.length:0.0} s, {tape.trackBpm} BPM, \"{tape.tapeGenre}\"");
                previous = src.clip;
                yield return 1.0f;
                Check($"1b) '{tape.tapeTitle}' continua tocando (streaming avança)", src.isPlaying && src.time > 0.5f, $"t={src.time:0.00} s");

                // Tira a fita do deck sem eject (mão puxando): volta a música padrão
                yield return TakeOut(socket, tape);
                yield return wait;
                Check($"1c) Tirar '{tape.tapeTitle}' do deck volta a música padrão", src.clip == defaultClip && src.isPlaying,
                    $"{ClipName(src)} | deck: {(socket.CurrentlySocketedTape != null ? socket.CurrentlySocketedTape.tapeTitle : "vazio")}, fita a {Vector3.Distance(tape.transform.position, socket.transform.position):0.00} m, " +
                    $"selecionada por: {string.Join(",", tape.interactorsSelecting.Select(i => i.transform.name))}");
                previous = src.clip;
                yield return 0.3f;
            }

            // Troca direta fita -> fita (sem passar pela padrão no meio) e Eject físico
            var a = tapes[0]; var b = tapes[2];
            Release(a); yield return null;
            Manager().SelectEnter((IXRSelectInteractor)socket, (IXRSelectInteractable)a);
            yield return wait;
            AudioClip clipA = src.clip;
            yield return TakeOut(socket, a);
            Release(b); yield return null;
            Manager().SelectEnter((IXRSelectInteractor)socket, (IXRSelectInteractable)b);
            yield return wait;
            Check("2) Trocar uma fita pela outra muda a música", clipA == a.musicClip && src.clip == b.musicClip && src.isPlaying, $"{a.musicClip.name} -> {ClipName(src)}");

            socket.EjectCurrentTape();
            yield return wait + 0.2f;
            Check("3) Eject (botão físico) volta a Music_SciFi_Menu", src.clip == defaultClip && src.isPlaying && socket.CurrentlySocketedTape == null,
                $"{ClipName(src)}, deck vazio={socket.CurrentlySocketedTape == null}{(socket.CurrentlySocketedTape != null ? " (no deck: " + socket.CurrentlySocketedTape.tapeTitle + ")" : "")}");
            yield return socket.ejectIgnoreCollisionSeconds + socket.ejectRecaptureCooldownSeconds + 0.3f;

            var all = Object.FindObjectsByType<MusicCassetteTape>(FindObjectsSortMode.None);
            Check("4) Continuam exatamente 4 fitas (nada duplicado)", all.Length == 4, $"{all.Length}");
            string code = File.ReadAllText("Assets/Editor/AutoSetup/Task06_TapeMusicAssign.cs") + File.ReadAllText("Assets/Editor/AutoSetup/Task06MusicPlayModeTests.cs");
            Check("5) Nenhum LoadScene nos scripts novos", !code.Contains("SceneManager." + "LoadScene"), "");
        }
    }
}
