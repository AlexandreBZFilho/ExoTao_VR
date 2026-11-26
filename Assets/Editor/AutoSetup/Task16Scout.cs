using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core.Testing;
using VRRehab.Interactive.Task16;
using Object = UnityEngine.Object;

namespace VRRehab.EditorTools
{
    /// <summary>
    /// TASK-16 [A]: scout em Play Mode real (SOMENTE LEITURA, nada e salvo). Relatorio .agents/visual_qa/TASK-16_Before.txt
    /// Rodar: Unity.exe -batchmode (SEM -quit) -executeMethod VRRehab.EditorTools.Task16Scout.Run
    /// </summary>
    [InitializeOnLoad]
    public static class Task16Scout
    {
        private const string StateKey = "Task16Scout.state";
        private const string ReportPath = ".agents/visual_qa/TASK-16_Before.txt";
        private const string QaDir = ".agents/visual_qa";
        private const string MenuScenePath = "Assets/Scenes/Menu_Principal.unity";
        private static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        private static IEnumerator routine;
        private static float waitUntil; private static int waitFrame = -1;
        private static double startReal; private static StringBuilder log;
        private static readonly Regex FurnRx = new Regex("(counter|desk|drawer|shelf|shelves|table|cabinet|bookcase)", RegexOptions.IgnoreCase);

        static Task16Scout()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += c =>
            {
                if (c == PlayModeStateChange.EnteredEditMode && SessionState.GetInt(StateKey, 0) == 2)
                { SessionState.EraseInt(StateKey); if (Application.isBatchMode) EditorApplication.Exit(0); }
            };
        }

        [MenuItem("ExoTao IC/Menu/Scout TASK-16 (Play Mode)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) return;
            EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            SessionState.SetInt(StateKey, 1); routine = null;
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if (SessionState.GetInt(StateKey, 0) != 1 || !EditorApplication.isPlaying) return;
            if (routine == null)
            {
                log = new StringBuilder(); startReal = EditorApplication.timeSinceStartup; waitUntil = 0; waitFrame = -1;
                routine = Main(); stack.Clear(); stack.Push(routine);
            }
            if (EditorApplication.timeSinceStartup - startReal > 560) { L("TEMPO ESGOTADO"); Finish(); return; }
            if (Time.time < waitUntil || Time.frameCount <= waitFrame) return;
            try
            {
                while (stack.Count > 0)
                {
                    var top = stack.Peek();
                    if (!top.MoveNext()) { stack.Pop(); continue; }
                    object cur = top.Current;
                    if (cur is IEnumerator sub) { stack.Push(sub); continue; }
                    if (cur is float f) { waitUntil = Time.time + f; waitFrame = -1; } else { waitUntil = 0; waitFrame = Time.frameCount; }
                    return;
                }
                Finish();
            }
            catch (Exception e) { L("EXCECAO: " + e); Finish(); }
        }

        private static void Finish()
        {
            Directory.CreateDirectory(QaDir);
            File.WriteAllText(ReportPath, log.ToString());
            SessionState.SetInt(StateKey, 2); routine = null; stack.Clear();
            EditorApplication.ExitPlaymode();
        }

        private static void L(string s) => log.AppendLine(s);
        private static void H(string s) { log.AppendLine(); log.AppendLine("=== " + s + " ==="); }
        private static string V(Vector3 v) => $"({v.x:0.000}, {v.y:0.000}, {v.z:0.000})";
        private static string P(Transform t) { var s = t.name; while (t.parent != null) { t = t.parent; s = t.name + "/" + s; } return s; }
        private static XRInteractionManager Mgr() => Object.FindFirstObjectByType<XRInteractionManager>();

        private static void Shot(string file, Vector3 pos, Vector3 lookAt, float fov)
        {
            var go = new GameObject("__T16Cam"); var cam = go.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation((lookAt - pos).normalized, Vector3.up));
            cam.fieldOfView = fov; cam.nearClipPlane = 0.02f; cam.farClipPlane = 60f; cam.clearFlags = CameraClearFlags.Skybox; cam.enabled = false;
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt; cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(QaDir, file), tex.EncodeToPNG());
            Object.Destroy(tex); cam.targetTexture = null; rt.Release(); Object.Destroy(rt); Object.Destroy(go);
        }

        private static Bounds? RB(GameObject go)
        {
            Bounds? r = null;
            foreach (var x in go.GetComponentsInChildren<Renderer>(false))
            {
                if (x is ParticleSystemRenderer || x is TrailRenderer || x is LineRenderer || x.GetComponent<TMP_Text>() != null) continue;
                if (r == null) r = x.bounds; else { var b = r.Value; b.Encapsulate(x.bounds); r = b; }
            }
            return r;
        }

        private static Dictionary<Transform, Vector3> Snap(Transform root)
        {
            var d = new Dictionary<Transform, Vector3>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) d[t] = t.position;
            return d;
        }

        private static IEnumerator Main()
        {
            L($"=== TASK-16 [A] BEFORE - Play Mode real, Menu_Principal ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");
            Application.targetFrameRate = 90; QualitySettings.vSyncCount = 0;
            yield return 2.5f;

            var stand = GameObject.Find("TrophyDisplayStand_Instance");
            if (stand == null) { L("ERRO: TrophyDisplayStand_Instance nao encontrado"); yield break; }
            var mgr = stand.GetComponent<TrophyShelfManager>();
            var cam = Camera.main;
            var xro = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();

            H("0) contagens");
            L($"TrophyShelfManager: {Object.FindObjectsByType<TrophyShelfManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length}");
            L($"DroneCrownWearable: {Object.FindObjectsByType<DroneCrownWearable>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length}");
            L($"RehabProgressionSaveSystem: {Object.FindObjectsByType<VRRehab.Persistence.RehabProgressionSaveSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length}");
            L($"Raizes 'TrophyDisplayStand*': {Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(t => t.name.StartsWith("TrophyDisplayStand"))}");
            L($"Camera.main: {(cam != null ? V(cam.transform.position) : "null")} XROrigin: {(xro != null ? V(xro.transform.position) + " rotY=" + xro.transform.eulerAngles.y.ToString("0.0") : "null")}");

            H("1) estante: pose, filhos, base");
            L($"Raiz '{P(stand.transform)}' pos={V(stand.transform.position)} rotEuler={V(stand.transform.eulerAngles)} scale={V(stand.transform.lossyScale)} forward(+Z)={V(stand.transform.forward)}");
            foreach (var c in stand.GetComponents<Component>()) L($"  comp raiz: {c.GetType().Name}");
            var rootBox = stand.GetComponent<BoxCollider>();
            if (rootBox != null) L($"  BoxCollider raiz: center={V(rootBox.center)} size={V(rootBox.size)} isTrigger={rootBox.isTrigger}");
            var rb0 = RB(stand);
            if (rb0.HasValue) L($"  AABB renderers da estante: min={V(rb0.Value.min)} max={V(rb0.Value.max)} size={V(rb0.Value.size)}");
            foreach (var t in stand.GetComponentsInChildren<Transform>(true))
                L($"  {new string(' ', Depth(t, stand.transform) * 2)}{t.name} local={V(t.localPosition)} world={V(t.position)} active={t.gameObject.activeSelf}");

            H("2) manager e slots");
            if (mgr != null)
            {
                L($"autoRespawnDelay={mgr.autoRespawnDelay}");
                foreach (var s in mgr.slots)
                    L($"  slot {s.trophyId}: title='{s.title}' trophy={(s.trophyObject != null ? s.trophyObject.name : "null")} anchor={(s.slotAnchor != null ? s.slotAnchor.name + " " + V(s.slotAnchor.position) : "null")} plaque={(s.plaqueText != null ? s.plaqueText.name : "null")} particles={(s.unlockParticles != null ? "sim" : "nao")}");
                var asrc = stand.GetComponent<AudioSource>();
                L($"  AudioSource na raiz: {(asrc != null)} (o AudioSource do manager vive na raiz)");
            }

            H("3) trofeus: colisores, pivos, rigidbody, ergonomic grab");
            var trophies = new List<(string id, GameObject go)>();
            if (mgr != null) foreach (var s in mgr.slots) if (s.trophyObject != null) trophies.Add((s.trophyId, s.trophyObject));
            foreach (var (id, go) in trophies)
            {
                var rb = go.GetComponent<Rigidbody>();
                var grab = go.GetComponent<VRRehab.Ergonomics.ErgonomicGrabInteractable>();
                L($"Trofeu {id} '{P(go.transform)}' pos={V(go.transform.position)} local={V(go.transform.localPosition)} rotEuler={V(go.transform.eulerAngles)}");
                if (rb != null) L($"  Rigidbody: mass={rb.mass} kinematic={rb.isKinematic} gravity={rb.useGravity} interp={rb.interpolation} cdm={rb.collisionDetectionMode} sleeping={rb.IsSleeping()} drag={rb.linearDamping} angDrag={rb.angularDamping}");
                if (grab != null) L($"  Grab: movementType={grab.movementType} throwOnDetach={grab.throwOnDetach} layers={grab.interactionLayers.value} colliders={grab.colliders.Count}");
                var b = RB(go);
                if (b.HasValue) L($"  render AABB size={V(b.Value.size)} center={V(b.Value.center)} minY-pivotY={(b.Value.min.y - go.transform.position.y):0.000}");
                foreach (var c in go.GetComponentsInChildren<Collider>(true))
                {
                    string d = c is BoxCollider bc ? $"Box center={V(bc.center)} size={V(bc.size)}" : c is SphereCollider sc ? $"Sphere center={V(sc.center)} r={sc.radius:0.000}" : c is CapsuleCollider cc ? $"Capsule center={V(cc.center)} r={cc.radius:0.000} h={cc.height:0.000} dir={cc.direction}" : c.GetType().Name;
                    L($"  Collider '{c.name}' {d} trigger={c.isTrigger} worldAABB size={V(c.bounds.size)} center={V(c.bounds.center)} material={(c.sharedMaterial != null ? c.sharedMaterial.name : "none")}");
                }
            }
            L("-- sobreposicao entre pares de colisores (solidos) no repouso (ComputePenetration):");
            var cols = stand.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger && c.enabled).ToArray();
            int pairs = 0;
            for (int i = 0; i < cols.Length; i++)
                for (int j = i + 1; j < cols.Length; j++)
                {
                    bool ov = Physics.ComputePenetration(cols[i], cols[i].transform.position, cols[i].transform.rotation, cols[j], cols[j].transform.position, cols[j].transform.rotation, out var dir, out float dist);
                    if (ov) { pairs++; L($"  SOBREPOE: {P(cols[i].transform)} x {P(cols[j].transform)} profundidade={dist * 100:0.0} cm dir={V(dir)}"); }
                }
            L($"  total de pares sobrepostos: {pairs} (de {cols.Length} colisores solidos)");
            L("-- posicao x local de cada trofeu: " + string.Join(", ", trophies.Select(t => t.id + "=" + stand.transform.InverseTransformPoint(t.go.transform.position).x.ToString("0.000"))));
            foreach (var (id, go) in trophies)
            {
                var cc = go.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger).ToArray(); if (cc.Length == 0) continue;
                var bb = cc[0].bounds; foreach (var c in cc) bb.Encapsulate(c.bounds);
                L($"   {id}: AABB mundo dos colisores size={V(bb.size)}");
            }

            H("4) textos Label_*");
            foreach (var tmp in stand.GetComponentsInChildren<TMP_Text>(true))
            {
                var r = tmp.GetComponent<Renderer>();
                L($"{P(tmp.transform)} text='{tmp.text}' font='{(tmp.font != null ? tmp.font.name : "null")}' fontSize={tmp.fontSize} autoSize={tmp.enableAutoSizing} min/max={tmp.fontSizeMin}/{tmp.fontSizeMax} overflow={tmp.overflowMode} wrap={tmp.enableWordWrapping} align={tmp.alignment} color={tmp.color}");
                L($"   rect={tmp.rectTransform.rect.size} pivot={tmp.rectTransform.pivot} parent='{tmp.transform.parent.name}' world={V(tmp.transform.position)} scale={V(tmp.transform.lossyScale)} forward={V(tmp.transform.forward)}");
                L($"   textBounds size={V(tmp.textBounds.size)}  preferred={tmp.preferredWidth:0.00}x{tmp.preferredHeight:0.00} rendererAABB size={(r != null ? V(r.bounds.size) : "-")} -> {(r != null && r.bounds.size.magnitude > 0.5f ? "FORA DO LUGAR (gigante)" : "tamanho ok")}");
            }

            H("5) tris, materiais e shaders (assets)");
            foreach (var name in new[] { "TrophyDisplayStand", "Trophy_GoldenApple", "Trophy_LuminousBonsai", "Trophy_StarCup", "Trophy_DroneCrownHat" })
            {
                var g = AssetDatabase.FindAssets(name + " t:Prefab");
                string p = g.Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(x => Path.GetFileNameWithoutExtension(x) == name);
                if (p == null) { L($"{name}: prefab nao encontrado"); continue; }
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                int tris = 0; var mats = new HashSet<string>();
                foreach (var mf in pf.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
                foreach (var r in pf.GetComponentsInChildren<Renderer>(true)) foreach (var m in r.sharedMaterials) if (m != null) mats.Add($"{m.name}[{m.shader.name}, inst={m.enableInstancing}]");
                L($"{p}: tris={tris} materiais: {string.Join("; ", mats)}");
            }

            H("6) moveis (counter|desk|drawer|shelf|shelves|table|cabinet|bookcase) - AABB por renderers");
            var furn = new List<GameObject>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!FurnRx.IsMatch(t.name) || t.IsChildOf(stand.transform)) continue;
                if (t.parent != null && FurnRx.IsMatch(t.parent.name)) continue;
                furn.Add(t.gameObject);
            }
            foreach (var f in furn.OrderBy(f => f.name))
            {
                var b = RB(f);
                L($"  {P(f.transform)} pos={V(f.transform.position)} rotY={f.transform.eulerAngles.y:0} AABB min={(b.HasValue ? V(b.Value.min) : "-")} max={(b.HasValue ? V(b.Value.max) : "-")} size={(b.HasValue ? V(b.Value.size) : "-")}{(b.HasValue && (b.Value.size.x > 2.4f || b.Value.size.z > 2.4f) ? "  [AABB > 2.4 m: IGNORAR]" : "")}");
            }

            var shelf = GameObject.Find("modern_shelves_3 (1)");
            H("7) modern_shelves_3 (1): superficies internas e vizinhanca");
            if (shelf != null)
            {
                foreach (var r in shelf.GetComponentsInChildren<Renderer>(true))
                    L($"  renderer '{r.name}' min={V(r.bounds.min)} max={V(r.bounds.max)} size={V(r.bounds.size)}");
                foreach (var c in shelf.GetComponentsInChildren<Collider>(true))
                    L($"  collider '{c.name}' {c.GetType().Name} min={V(c.bounds.min)} max={V(c.bounds.max)} trigger={c.isTrigger}");
                var sb = RB(shelf);
                if (sb.HasValue)
                {
                    L($"  AABB movel: min={V(sb.Value.min)} max={V(sb.Value.max)}");
                    L("  Vizinhos (renderers de outros objetos a < 0.9 m horizontais, y entre 0.8 e 2.4):");
                    foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    {
                        if (r.transform.IsChildOf(shelf.transform) || r.transform.IsChildOf(stand.transform) || r is ParticleSystemRenderer || r.GetComponent<TMP_Text>() != null) continue;
                        var b = r.bounds; if (b.size.x > 4 || b.size.z > 4) continue;
                        float gx = Mathf.Max(0, Mathf.Max(sb.Value.min.x - b.max.x, b.min.x - sb.Value.max.x));
                        float gz = Mathf.Max(0, Mathf.Max(sb.Value.min.z - b.max.z, b.min.z - sb.Value.max.z));
                        float g = Mathf.Sqrt(gx * gx + gz * gz);
                        if (g < 0.9f && b.max.y > 0.8f && b.min.y < 2.4f) L($"    {P(r.transform)} gapHoriz={g:0.000} y={b.min.y:0.00}..{b.max.y:0.00} size={V(b.size)}");
                    }
                }
            }
            else L("  modern_shelves_3 (1) NAO ENCONTRADO");

            H("8) Encaixe de base 0.86x0.22 e 0.72x0.22 (folga horizontal maxima obtivel, altura util 0.30 m) em superficies candidatas");
            var standCols = new HashSet<Collider>(stand.GetComponentsInChildren<Collider>(true));
            foreach (var f in furn.OrderBy(f => f.name))
            {
                var fb = RB(f); if (!fb.HasValue || fb.Value.size.x > 2.4f || fb.Value.size.z > 2.4f) continue;
                var levels = new List<Bounds>();
                foreach (var r in f.GetComponentsInChildren<Renderer>(false))
                {
                    var b = r.bounds;
                    if (b.size.y < 0.10f && b.size.x * b.size.z > 0.04f && b.size.x > 0.25f && b.size.z > 0.15f) levels.Add(b);
                }
                if (levels.Count == 0) levels.Add(fb.Value);
                foreach (var lv in levels.GroupBy(l => Mathf.Round(l.max.y / 0.03f)).Select(g => { var b = g.First(); foreach (var x in g) b.Encapsulate(x); return b; }).OrderBy(b => b.max.y))
                {
                    float topY = lv.max.y; if (topY < 0.5f || topY > 1.8f) continue;
                    foreach (float w in new[] { 0.86f, 0.72f })
                    {
                        float bestClear = -1; Vector3 bestPos = default; bool bestAlongX = true;
                        foreach (bool alongX in new[] { true, false })
                        {
                            float sx = alongX ? w : 0.22f, sz = alongX ? 0.22f : w;
                            for (float x = lv.min.x + sx / 2; x <= lv.max.x - sx / 2 + 1e-4f; x += 0.04f)
                                for (float z = lv.min.z + sz / 2; z <= lv.max.z - sz / 2 + 1e-4f; z += 0.04f)
                                {
                                    float clear = -1;
                                    foreach (float inf in new[] { 0f, 0.04f, 0.08f, 0.12f, 0.16f, 0.2f, 0.25f })
                                    {
                                        var half = new Vector3(sx / 2 + inf, 0.15f, sz / 2 + inf);
                                        var center = new Vector3(x, topY + 0.02f + 0.15f, z);
                                        var hits = Physics.OverlapBox(center, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                                        bool blocked = hits.Any(h => !standCols.Contains(h) && !h.transform.IsChildOf(f.transform) && (xro == null || !h.transform.IsChildOf(xro.transform)));
                                        if (blocked) break;
                                        clear = inf;
                                    }
                                    if (clear > bestClear) { bestClear = clear; bestPos = new Vector3(x, topY, z); bestAlongX = alongX; }
                                }
                        }
                        L($"  {f.name} nivel y={topY:0.000} [x {lv.min.x:0.00}..{lv.max.x:0.00}, z {lv.min.z:0.00}..{lv.max.z:0.00}] peca {w:0.00}x0.22: folga max={(bestClear < 0 ? "NAO CABE" : bestClear.ToString("0.00") + " m")}{(bestClear >= 0 ? " em " + V(bestPos) + (bestAlongX ? " (eixo X)" : " (eixo Z)") : "")}");
                    }
                }
            }

            H("9) BUG do teleporte: pose da raiz antes/depois de pegar um trofeu");
            Vector3 headPos = cam != null ? cam.transform.position : stand.transform.position + stand.transform.forward * 2f + Vector3.up * 1.5f;
            Vector3 flatDir = headPos - stand.transform.position; flatDir.y = 0; float distHead = flatDir.magnitude; flatDir = flatDir.normalized;
            float standFrontDot = Vector3.Dot(stand.transform.forward, flatDir);
            L($"Paciente (camera) em {V(headPos)}; distancia horizontal ate a estante={distHead:0.00} m; dot(frente +Z da estante, direcao ao paciente)={standFrontDot:0.00} (1 = de frente, -1 = de costas)");
            Vector3 viewPos = stand.transform.position + flatDir * Mathf.Min(Mathf.Max(distHead, 1.8f), 2.4f); viewPos.y = 1.5f;
            Vector3 lookAt = stand.transform.position + Vector3.up * 0.15f;
            Shot("TASK-16_Before_1.png", viewPos, lookAt, 55f);
            Vector3 rootBefore = stand.transform.position;
            var snapBefore = Snap(stand.transform);
            L($"ANTES: raiz={V(rootBefore)} rotY={stand.transform.eulerAngles.y:0.0}");

            var hand = new GameObject("~T16Hand"); var hc = hand.AddComponent<SphereCollider>(); hc.radius = 0.03f; hc.isTrigger = true;
            var ti = hand.AddComponent<ScriptedTestInteractor>();
            yield return null;
            bool firstShot = true;
            foreach (var (id, go) in trophies)
            {
                var grab = go.GetComponent<XRGrabInteractable>(); if (grab == null) { L($"  {id}: sem XRGrabInteractable"); continue; }
                var rootPre = stand.transform.position; var trophyPre = go.transform.position;
                hand.transform.position = go.transform.position;
                yield return null;
                Mgr().SelectEnter((IXRSelectInteractor)ti, grab);
                yield return null;
                var f1 = stand.transform.position;
                L($"  [{id}] pego: trofeu_antes={V(trophyPre)}; raiz 1 frame depois={V(f1)} deslocamento_da_raiz={Vector3.Distance(rootPre, f1) * 100:0.0} cm");
                yield return 1.0f;
                var f2 = stand.transform.position;
                L($"      raiz 1 s depois={V(f2)} deslocamento_da_raiz={Vector3.Distance(rootPre, f2) * 100:0.0} cm; trofeu agora={V(go.transform.position)}");
                if (firstShot) { Shot("TASK-16_Before_2.png", viewPos, lookAt, 55f); firstShot = false; L("      captura TASK-16_Before_2.png tirada da mesma camera do Before_1 (viewPos=" + V(viewPos) + ")"); }
                Vector3 p0 = hand.transform.position;
                for (int i = 1; i <= 20; i++) { hand.transform.position = p0 + new Vector3(0, 0.3f, 0) + Vector3.right * 1.5f * (i / 20f); yield return null; }
                if (grab.isSelected) Mgr().SelectExit((IXRSelectInteractor)ti, grab);
                yield return 6.0f;
                var f3 = stand.transform.position;
                var slot = mgr.slots.First(s => s.trophyId == id);
                L($"      solto longe, +6 s: raiz={V(f3)} deslocamento_acumulado={Vector3.Distance(rootBefore, f3) * 100:0.0} cm; trofeu em {V(go.transform.position)}, distancia ao slot={Vector3.Distance(go.transform.position, slot.slotAnchor.position) * 100:0.0} cm");
            }
            Object.Destroy(hand);
            yield return 3.0f;
            L($"DEPOIS (tudo): raiz={V(stand.transform.position)} rotY={stand.transform.eulerAngles.y:0.0} deslocamento_total_raiz={Vector3.Distance(rootBefore, stand.transform.position) * 100:0.0} cm");
            float maxMove = 0; string who = "";
            foreach (var kv in snapBefore) if (kv.Key != null && !trophies.Any(t => kv.Key.IsChildOf(t.go.transform))) { float d = Vector3.Distance(kv.Value, kv.Key.position); if (d > maxMove) { maxMove = d; who = kv.Key.name; } }
            L($"Maior deslocamento de nao-trofeu (ancoras, base, rotulos): {maxMove * 100:0.0} cm ({who})");
            L(maxMove > 0.01f ? "CONCLUSAO: BUG CONFIRMADO - a raiz da estante (e tudo que e filho dela) e movida ao pegar um trofeu (PlayPickupSound escreve audioSource.transform.position na raiz)." : "CONCLUSAO: bug NAO reproduzido (raiz parada).");
        }

        private static int Depth(Transform t, Transform root) { int d = 0; while (t != root && t.parent != null) { t = t.parent; d++; } return d; }
    }
}
