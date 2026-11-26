using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Ergonomics;
using Object = UnityEngine.Object;

namespace VRRehab.EditorTools
{
    /// <summary>
    /// Laser escondido enquanto segura um objeto (HideRayLineWhileHolding).
    /// 1) RunBatch: coloca o componente em todo GameObject com XRRayInteractor + XRInteractorLineVisual
    ///    dos prefabs do jogador (Pessoa_Virtual*). As cenas herdam pelo prefab.
    /// 2) RunMenuTest / RunForestTest: Play Mode real; seleciona objetos pegáveis com o RAIO de verdade do jogador
    ///    e confere que o laser some ao pegar e volta ao soltar; teleporte e hover não escondem o laser.
    /// Relatório: .agents/visual_qa/RAYLINE_PlayModeTest_{Menu|Floresta}.txt
    /// </summary>
    [InitializeOnLoad]
    public static class RayLineHideSetup
    {
        private static readonly string[] PlayerPrefabs =
        {
            "Assets/Prefabs/Pessoa_Virtual_sem3D loading.prefab",
            "Assets/Prefabs/Pessoa_Virtual_sem3D.prefab",
            "Assets/Prefabs/Pessoa_Virtual.prefab",
        };

        // ------------------------------------------------------------------ prefabs
        public static void RunBatch()
        {
            int code = 0;
            var sb = new StringBuilder();
            try
            {
                foreach (string path in PlayerPrefabs)
                {
                    GameObject root = PrefabUtility.LoadPrefabContents(path);
                    int added = 0, rays = 0;
                    foreach (var lv in root.GetComponentsInChildren<XRInteractorLineVisual>(true))
                    {
                        if (lv.GetComponent<XRRayInteractor>() == null) continue;
                        rays++;
                        if (lv.GetComponent<HideRayLineWhileHolding>() != null) continue;
                        lv.gameObject.AddComponent<HideRayLineWhileHolding>();
                        added++;
                    }
                    if (added > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                    PrefabUtility.UnloadPrefabContents(root);
                    sb.AppendLine($"{path}: {rays} raios com laser, componente adicionado em {added}");
                }
                AssetDatabase.SaveAssets();
            }
            catch (Exception e) { sb.AppendLine("ERRO: " + e); code = 1; }
            File.WriteAllText(".agents/visual_qa/RAYLINE_Setup.txt", sb.ToString());
            Debug.Log("[RayLineHideSetup]\n" + sb);
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        // ------------------------------------------------------------------ Play Mode
        private const string StateKey = "RayLineHideSetup.state";
        private const string FailKey = "RayLineHideSetup.fail";
        private const string SceneKey = "RayLineHideSetup.scene";
        private const float TimeoutSeconds = 240f;

        private static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        private static IEnumerator routine;
        private static float waitUntil;
        private static int waitFrame = -1;
        private static double startRealTime;
        private static StringBuilder log;
        private static int pass, fail;

        static RayLineHideSetup()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static void RunMenuTest() => StartTest("Menu");
        public static void RunForestTest() => StartTest("Floresta");

        private static void StartTest(string which)
        {
            if (EditorApplication.isPlaying) return;
            string scene = which == "Menu" ? "Assets/Scenes/Menu_Principal.unity" : "Assets/Scenes/Floresta_e_Rio.unity";
            EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            SessionState.SetString(SceneKey, which);
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
                Time.captureDeltaTime = 1f / 90f;
                routine = Tests();
                stack.Clear();
                stack.Push(routine);
            }
            if (EditorApplication.timeSinceStartup - startRealTime > TimeoutSeconds)
            {
                Check("TEMPO ESGOTADO", false, $"passou de {TimeoutSeconds} s");
                Finish();
                return;
            }
            if (Time.time < waitUntil || Time.frameCount <= waitFrame) return;
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
            string which = SessionState.GetString(SceneKey, "Menu");
            File.WriteAllText($".agents/visual_qa/RAYLINE_PlayModeTest_{which}.txt", log.ToString());
            Debug.Log("[RayLineHideSetup]\n" + log);
            Time.captureDeltaTime = 0f;
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

        private static string PathOf(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        private static System.Reflection.MethodInfo updateLineMethod;

        /// <summary>Chama XRInteractorLineVisual.UpdateLineVisual (interno) — o que o XRI faz a cada onBeforeRender.</summary>
        private static bool ForceLineUpdate(XRInteractorLineVisual lv)
        {
            if (lv == null || !lv.isActiveAndEnabled) return false;
            if (updateLineMethod == null)
                updateLineMethod = typeof(XRInteractorLineVisual).GetMethod("UpdateLineVisual",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (updateLineMethod == null) return false;
            updateLineMethod.Invoke(lv, null);
            return true;
        }

        private static IEnumerator Tests()
        {
            log.AppendLine($"Laser escondido ao segurar — cena {SessionState.GetString(SceneKey, "?")} — {DateTime.Now:yyyy-MM-dd HH:mm}");
            log.AppendLine();
            yield return 1.5f;

            var manager = Object.FindFirstObjectByType<XRInteractionManager>();
            Check("XRInteractionManager na cena", manager != null, manager != null ? manager.name : "não achado");
            if (manager == null) yield break;

            var rays = Object.FindObjectsByType<XRRayInteractor>(FindObjectsSortMode.None)
                .Where(r => r.isActiveAndEnabled && r.GetComponent<XRInteractorLineVisual>() != null)
                .OrderBy(r => r.name).ToList();
            Check("Raios com laser ativos", rays.Count >= 1, $"{rays.Count}: " + string.Join(", ", rays.Select(r => PathOf(r.transform))));
            foreach (var r in rays)
                Check($"Componente no raio '{r.name}'", r.GetComponent<HideRayLineWhileHolding>() != null, PathOf(r.transform));
            if (rays.Count == 0) yield break;

            // Até 4 objetos pegáveis diferentes (inclui o Drone se houver), cada um testado com o primeiro raio.
            var grabs = Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None)
                .Where(g => g.isActiveAndEnabled && !g.isSelected)
                .OrderBy(g => g.name.Contains("Drone") ? 0 : 1)
                .ThenBy(g => (g.transform.position - rays[0].transform.position).sqrMagnitude)
                .Take(4).ToList();
            Check("Objetos pegáveis para testar", grabs.Count >= 1, string.Join(", ", grabs.Select(g => g.name)));

            for (int i = 0; i < grabs.Count; i++)
            {
                var ray = rays[i % rays.Count];
                var lv = ray.GetComponent<XRInteractorLineVisual>();
                var lr = ray.GetComponent<LineRenderer>();
                var hide = ray.GetComponent<HideRayLineWhileHolding>();
                var g = grabs[i];
                bool baselineDrawn = ForceLineUpdate(lv) && lr != null && lr.enabled && lr.positionCount >= 2;
                if (i == 0) Check("Laser é desenhado normalmente antes de pegar (referência)", baselineDrawn,
                    $"lineRenderer.enabled={(lr != null ? lr.enabled.ToString() : "-")}, pontos={(lr != null ? lr.positionCount : 0)}");

                // Hover não esconde.
                manager.HoverEnter((IXRHoverInteractor)ray, (IXRHoverInteractable)g);
                Check($"[{g.name}] apontar (hover) mantém o laser", lv.enabled, $"lineVisual.enabled={lv.enabled}");
                manager.HoverExit((IXRHoverInteractor)ray, (IXRHoverInteractable)g);

                manager.SelectEnter((IXRSelectInteractor)ray, (IXRSelectInteractable)g);
                bool sel = ray.hasSelection && g.isSelected;
                Check($"[{g.name}] pego pelo raio '{ray.name}'", sel, $"hasSelection={ray.hasSelection}");
                Check($"[{g.name}] laser escondido ao pegar", !lv.enabled && (lr == null || !lr.enabled) && hide.IsLineHidden,
                    $"lineVisual.enabled={lv.enabled}, lineRenderer.enabled={(lr != null ? lr.enabled.ToString() : "-")}");
                yield return null;
                // Ainda segurando depois de um frame? (o raio sem botão pode soltar sozinho; só conferimos se ainda está pego)
                if (ray.hasSelection)
                    Check($"[{g.name}] laser continua escondido no frame seguinte", !lv.enabled && (lr == null || !lr.enabled),
                        $"lineVisual.enabled={lv.enabled}");

                if (ray.hasSelection) manager.SelectExit((IXRSelectInteractor)ray, (IXRSelectInteractable)g);
                Check($"[{g.name}] laser volta ao soltar", lv.enabled && !hide.IsLineHidden, $"lineVisual.enabled={lv.enabled}");
                yield return null;
                yield return null;
                // Em batchmode não há renderização, então o onBeforeRender (onde o XRI desenha o laser) não roda:
                // chamamos a mesma atualização à mão e conferimos que o laser volta a ser desenhado igual ao de antes.
                bool drawnAfter = ForceLineUpdate(lv) && lr != null && lr.enabled && lr.positionCount >= 2;
                Check($"[{g.name}] laser desenhando de novo depois de soltar", lv.enabled && drawnAfter == baselineDrawn,
                    $"antes de pegar={baselineDrawn}, depois de soltar={drawnAfter} (pontos={(lr != null ? lr.positionCount : 0)})");
                yield return 0.2f;
            }

            // Drone (XRBaseInteractable arrastado pelo laser) também esconde o laser.
            var drone = Object.FindObjectsByType<VRRehab.DronePet.DroneCompanionFollowAndGrab>(FindObjectsSortMode.None).FirstOrDefault(d => d.isActiveAndEnabled);
            if (drone != null)
            {
                var ray = rays[0];
                var lv = ray.GetComponent<XRInteractorLineVisual>();
                manager.SelectEnter((IXRSelectInteractor)ray, (IXRSelectInteractable)drone);
                Check($"Segurar o Drone '{drone.name}' esconde o laser", ray.hasSelection && !lv.enabled, $"hasSelection={ray.hasSelection}, lineVisual.enabled={lv.enabled}");
                if (ray.hasSelection) manager.SelectExit((IXRSelectInteractor)ray, (IXRSelectInteractable)drone);
                Check("Soltar o Drone mostra o laser de novo", lv.enabled, $"lineVisual.enabled={lv.enabled}");
                yield return 0.3f;
            }
            else log.AppendLine("(sem Drone ativo nesta cena — teste do Drone pulado)");

            // Botão de toque rápido (não é objeto segurado) não esconde, para o laser não piscar a cada clique.
            var button = Object.FindObjectsByType<XRBaseInteractable>(FindObjectsSortMode.None)
                .FirstOrDefault(b => b.isActiveAndEnabled && !(b is XRGrabInteractable) && !(b is VRRehab.DronePet.DroneCompanionFollowAndGrab)
                                     && !(b is BaseTeleportationInteractable) && !b.isSelected);
            if (button != null)
            {
                var ray = rays[0];
                var lv = ray.GetComponent<XRInteractorLineVisual>();
                manager.SelectEnter((IXRSelectInteractor)ray, (IXRSelectInteractable)button);
                Check($"Clicar no botão/interativo '{button.name}' ({button.GetType().Name}) NÃO esconde o laser", lv.enabled, $"lineVisual.enabled={lv.enabled}");
                if (ray.hasSelection && button.isSelected) manager.SelectExit((IXRSelectInteractor)ray, (IXRSelectInteractable)button);
                yield return 0.3f;
            }

            // Teleporte (se houver) não esconde o laser.
            var tp = Object.FindObjectsByType<BaseTeleportationInteractable>(FindObjectsSortMode.None).FirstOrDefault(t => t.isActiveAndEnabled);
            if (tp != null)
            {
                var ray = rays[0];
                var lv = ray.GetComponent<XRInteractorLineVisual>();
                manager.SelectEnter((IXRSelectInteractor)ray, (IXRSelectInteractable)tp);
                Check($"Selecionar teleporte '{tp.name}' NÃO esconde o laser", lv.enabled, $"lineVisual.enabled={lv.enabled}");
                if (ray.hasSelection) manager.SelectExit((IXRSelectInteractor)ray, (IXRSelectInteractable)tp);
            }
            else log.AppendLine("(sem área de teleporte ativa nesta cena — teste de teleporte pulado)");

            // Instalador de reserva não duplica nada.
            int extra = HideRayLineWhileHoldingInstaller.InstallAll();
            Check("Instalador de reserva não precisou adicionar nada (prefabs já têm)", extra == 0, $"adicionados={extra}");
            int dup = rays.Count(r => r.GetComponents<HideRayLineWhileHolding>().Length > 1);
            Check("Sem componente duplicado", dup == 0, $"raios com duplicata={dup}");
        }
    }
}
