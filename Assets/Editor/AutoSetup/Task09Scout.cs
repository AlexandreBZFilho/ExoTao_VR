using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRRehab.Interactive.Task09;

namespace VRRehab.EditorTools
{
    /// <summary>
    /// TASK-09 [A]: scout SOMENTE LEITURA (nao salva a cena). Gera .agents/visual_qa/TASK-09_ScoutReport.txt e capturas "antes" offscreen.
    /// Batch: unity run . --log-file ... --no-tail -- -executeMethod VRRehab.EditorTools.Task09Scout.Run
    /// </summary>
    public static class Task09Scout
    {
        private const string ScenePath = "Assets/Scenes/Menu_Principal.unity";
        private const string OutDir = ".agents/visual_qa";
        private static readonly Regex FurnRx = new Regex("(counter|desk|drawer|shelf|table|cabinet|bookcase)", RegexOptions.IgnoreCase);

        private static StringBuilder sb;

        public static void Run()
        {
            int code = 1;
            sb = new StringBuilder();
            try
            {
                Directory.CreateDirectory(OutDir);
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Physics.SyncTransforms();
                Execute();
                code = 0;
            }
            catch (Exception e)
            {
                sb.AppendLine("EXCECAO: " + e);
                Debug.LogException(e);
            }
            File.WriteAllText(Path.Combine(OutDir, "TASK-09_ScoutReport.txt"), sb.ToString());
            EditorApplication.Exit(code);
        }

        private static void H(string t) { sb.AppendLine(); sb.AppendLine("=== " + t + " ==="); }
        private static void L(string t) { sb.AppendLine(t); }
        private static string V(Vector3 v) => $"({v.x:0.000}, {v.y:0.000}, {v.z:0.000})";
        private static string Path_(Transform t)
        {
            var s = t.name; while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }

        private static Bounds? RBounds(GameObject go, bool includeInactive = false)
        {
            Bounds? res = null;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(includeInactive))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (r.GetComponent("TMP_Text") != null) continue;
                if (res == null) res = r.bounds; else { var b = res.Value; b.Encapsulate(r.bounds); res = b; }
            }
            return res;
        }

        private static List<Renderer> MeshRenderers()
        {
            return UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r => r.enabled && !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer) && r.GetComponent("TMP_Text") == null).ToList();
        }

        private static float AabbClear(Bounds a, Bounds b) => SpatialAuditUtility.AabbClearance(a, b);

        private static void Execute()
        {
            var buttons = UnityEngine.Object.FindObjectsByType<FireworksButtonInteractable>(FindObjectsSortMode.None);
            var bobbles = UnityEngine.Object.FindObjectsByType<BobbleheadSpringToy>(FindObjectsSortMode.None);
            var levers = UnityEngine.Object.FindObjectsByType<AtmosphereLeverInteractable>(FindObjectsSortMode.None);

            H("g) DUPLICATAS (contagens)");
            L($"FireworksButtonInteractable: {buttons.Length}");
            foreach (var b in buttons) L($"   {Path_(b.transform)} pos={V(b.transform.position)}");
            L($"BobbleheadSpringToy: {bobbles.Length}");
            foreach (var b in bobbles) L($"   {Path_(b.transform)} pos={V(b.transform.position)}");
            L($"AtmosphereLeverInteractable: {levers.Length}");
            foreach (var b in levers) L($"   {Path_(b.transform)} pos={V(b.transform.position)}");
            foreach (string n in new[] { "Room_Fireworks_Launcher", "Burst_Apex_Target", "Ascent_Sparks_Trail", "Burst_Explosion_System", "Burst_Flash_Light" })
            {
                var all = AllTransforms().Where(t => t.name == n).ToList();
                L($"'{n}': {all.Count} instancia(s)");
                foreach (var t in all) L($"   {Path_(t)} world={V(t.position)}");
            }
            OriginScan();

            FireworksButtonInteractable fw = buttons.FirstOrDefault();
            BobbleheadSpringToy bb = bobbles.FirstOrDefault();
            AtmosphereLeverInteractable lv = levers.FirstOrDefault();

            if (fw != null) SectionFireworks(fw);
            if (fw != null) SectionFreeSpace(fw);
            if (bb != null) SectionBobble(bb);
            SectionNeighbors(fw, bb, lv);
            SectionSurfaces(bb, fw, lv);
            SectionTris();
            SectionLeverNote(lv);
            Captures(fw, bb, lv);
        }

        private static IEnumerable<Transform> AllTransforms()
        {
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) yield return t;
        }

        private static void OriginScan()
        {
            L("Objetos topo-de-hierarquia a < 1 m de (0,0,0) com Renderer/Collider/script:");
            int n = 0;
            foreach (var t in AllTransforms())
            {
                if (Vector3.Distance(t.position, Vector3.zero) >= 1f) continue;
                if (t.parent != null && Vector3.Distance(t.parent.position, Vector3.zero) < 1f) continue;
                bool has = t.GetComponentInChildren<Renderer>(true) != null || t.GetComponentInChildren<Collider>(true) != null || t.GetComponentInChildren<MonoBehaviour>(true) != null;
                if (!has) continue;
                var bnd = RBounds(t.gameObject, true);
                L($"   {Path_(t)} pos={V(t.position)} size={(bnd.HasValue ? V(bnd.Value.size) : "-")}");
                if (++n >= 60) { L("   ... (truncado)"); break; }
            }
            if (n == 0) L("   (nenhum)");
        }

        // ---------------------------------------------------------------- a) FOGOS
        private static void DumpSerialized(UnityEngine.Object o, string indent)
        {
            var so = new SerializedObject(o);
            var it = so.GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = false;
                if (it.name == "m_Script") continue;
                L($"{indent}{it.propertyPath} = {PropVal(it)}");
            }
        }

        private static string PropVal(SerializedProperty p)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Float: return p.floatValue.ToString("0.####");
                case SerializedPropertyType.Integer: return p.intValue.ToString();
                case SerializedPropertyType.Boolean: return p.boolValue.ToString();
                case SerializedPropertyType.String: return "\"" + p.stringValue + "\"";
                case SerializedPropertyType.Vector3: return V(p.vector3Value);
                case SerializedPropertyType.Vector2: return p.vector2Value.ToString();
                case SerializedPropertyType.Color: return p.colorValue.ToString();
                case SerializedPropertyType.Enum: return p.enumValueIndex >= 0 && p.enumValueIndex < p.enumDisplayNames.Length ? p.enumDisplayNames[p.enumValueIndex] : p.enumValueIndex.ToString();
                case SerializedPropertyType.ObjectReference:
                    if (p.objectReferenceValue == null) return "null";
                    var oo = p.objectReferenceValue;
                    string extra = oo is Component c ? " @" + Path_(c.transform) : (AssetDatabase.GetAssetPath(oo) is string ap && ap.Length > 0 ? " [" + ap + "]" : "");
                    return oo.name + " (" + oo.GetType().Name + ")" + extra;
                default: return "<" + p.propertyType + ">";
            }
        }

        private static void DumpPs(ParticleSystem ps)
        {
            var m = ps.main; var e = ps.emission; var sh = ps.shape; var r = ps.GetComponent<ParticleSystemRenderer>();
            L($"  PS '{Path_(ps.transform)}' world={V(ps.transform.position)} rotEuler={V(ps.transform.eulerAngles)} forward={V(ps.transform.forward)} activeInHierarchy={ps.gameObject.activeInHierarchy}");
            L($"     emission.enabled={e.enabled} rateOverTime={e.rateOverTime.constant} bursts={e.burstCount} " + string.Join(" ", Enumerable.Range(0, e.burstCount).Select(i => { var b = e.GetBurst(i); return $"[t={b.time} count={b.count.constantMin}-{b.count.constantMax} cycles={b.cycleCount}]"; })));
            L($"     main: duration={m.duration} loop={m.loop} playOnAwake={m.playOnAwake} startSpeed={m.startSpeed.constant} startLifetime={m.startLifetime.constant} startSize={m.startSize.constant} simulationSpace={m.simulationSpace} maxParticles={m.maxParticles} gravityModifier={m.gravityModifier.constant} startColorMode={m.startColor.mode}");
            L($"     shape: enabled={sh.enabled} type={sh.shapeType} angle={sh.angle} radius={sh.radius}");
            L($"     collision.enabled={ps.collision.enabled} trails.enabled={ps.trails.enabled} lights.enabled={ps.lights.enabled}");
            if (r != null)
            {
                var mat = r.sharedMaterial;
                L($"     renderer: material={(mat != null ? mat.name : "null")} shader={(mat != null ? mat.shader.name : "null")} renderMode={r.renderMode} shadowCasting={r.shadowCastingMode} receiveShadows={r.receiveShadows} enableGPUInstancing={(mat != null && mat.enableInstancing)} mainTex={(mat != null && mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") != null ? mat.GetTexture("_MainTex").name : "none")}");
                if (mat != null) L($"     matBlend: renderQueue={mat.renderQueue} _Mode={(mat.HasProperty("_Mode") ? mat.GetFloat("_Mode").ToString() : "-")} _Color={(mat.HasProperty("_Color") ? mat.GetColor("_Color").ToString() : "-")} emission={(mat.IsKeywordEnabled("_EMISSION"))}");
            }
        }

        private static void SectionFireworks(FireworksButtonInteractable fw)
        {
            H("a) FOGOS - posicoes e sistemas");
            Transform bt = fw.transform;
            L($"Botao '{Path_(bt)}' world={V(bt.position)} rotEuler={V(bt.eulerAngles)} forward={V(bt.forward)} up={V(bt.up)} scale={V(bt.lossyScale)}");
            var so = new SerializedObject(fw);
            Transform apex = so.FindProperty("burstLocation").objectReferenceValue as Transform;
            ParticleSystem asc = so.FindProperty("fireworksAscentParticles").objectReferenceValue as ParticleSystem;
            ParticleSystem bur = so.FindProperty("fireworksBurstParticles").objectReferenceValue as ParticleSystem;
            Light bl = so.FindProperty("fireworksBurstLight").objectReferenceValue as Light;
            float travel = so.FindProperty("rocketTravelTime").floatValue;

            if (apex != null)
            {
                Vector3 d = apex.position - bt.position;
                L($"Burst_Apex_Target world={V(apex.position)} localNoPai={V(apex.localPosition)} pai='{(apex.parent != null ? apex.parent.name : "-")}' delta_vs_botao={V(d)} distHoriz={new Vector2(d.x, d.z).magnitude:0.000} alturaAcima={d.y:0.000}");
                Physics.SyncTransforms();
                var inside = Physics.OverlapSphere(apex.position, 0.15f, ~0, QueryTriggerInteraction.Ignore);
                L($"   OverlapSphere(0.15) no apex: {inside.Length} colisores: " + string.Join(", ", inside.Take(8).Select(c => c.name)));
                var rin = MeshRenderers().Where(r => r.bounds.Contains(apex.position) && r.bounds.size.x < 6f && r.bounds.size.z < 6f && r.bounds.size.y < 6f).Take(10).ToList();
                L($"   Renderers cujo AABB contem o apex (<6 m): {rin.Count}: " + string.Join(", ", rin.Select(r => r.name + " size" + V(r.bounds.size))));
                Vector3 from = asc != null ? asc.transform.position : bt.position;
                bool blocked = Physics.Linecast(from, apex.position, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore);
                L($"   Linecast(saida da trilha -> apex) bloqueado={blocked}" + (blocked ? $" por '{hit.collider.name}' a {hit.distance:0.000} m" : ""));
                if (asc != null)
                {
                    Vector3 toApex = (apex.position - asc.transform.position);
                    float ang = Vector3.Angle(asc.transform.forward, toApex);
                    L($"   Ascent forward vs direcao ate o apex: {ang:0.0} graus; alcance = startSpeed*lifetime = {asc.main.startSpeed.constant * asc.main.startLifetime.constant:0.00} m; distancia ate o apex = {toApex.magnitude:0.00} m; angulo do forward com vertical = {Vector3.Angle(asc.transform.forward, Vector3.up):0.0}");
                }
                L($"   rocketTravelTime(WaitForSeconds fixo)={travel}");
            }
            else L("burstLocation = NULL na cena");

            L("-- Campos serializados do script NA CENA:");
            DumpSerialized(fw, "   ");
            L("-- Hierarquia do botao (cena) com transforms locais:");
            DumpHierarchy(bt, "   ");

            var launcher = AllTransforms().FirstOrDefault(t => t.name == "Room_Fireworks_Launcher");
            if (launcher != null)
            {
                L($"-- Room_Fireworks_Launcher world={V(launcher.position)} rot={V(launcher.eulerAngles)} filhos:");
                DumpHierarchy(launcher, "   ");
            }
            else L("-- Room_Fireworks_Launcher NAO existe na cena");

            L("-- ParticleSystems relacionados:");
            var set = new HashSet<ParticleSystem>();
            if (asc != null) set.Add(asc);
            if (bur != null) set.Add(bur);
            if (launcher != null) foreach (var p in launcher.GetComponentsInChildren<ParticleSystem>(true)) set.Add(p);
            foreach (var p in bt.GetComponentsInChildren<ParticleSystem>(true)) set.Add(p);
            foreach (var p in set) DumpPs(p);
            if (bl != null) L($"  Luz de explosao '{Path_(bl.transform)}' type={bl.type} range={bl.range} intensity={bl.intensity} shadows={bl.shadows} enabled={bl.enabled}");

            L("-- Prefab FireworksLaunchButton.prefab:");
            var prefabs = AssetDatabase.FindAssets("FireworksLaunchButton t:Prefab").Select(AssetDatabase.GUIDToAssetPath).ToList();
            foreach (string pp in prefabs)
            {
                L($" {pp}");
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(pp);
                var comp = go.GetComponentInChildren<FireworksButtonInteractable>(true);
                if (comp != null) DumpSerialized(comp, "     ");
                L("     ParticleSystems no prefab: " + go.GetComponentsInChildren<ParticleSystem>(true).Length);
                L("     Colliders no prefab: " + string.Join(", ", go.GetComponentsInChildren<Collider>(true).Select(c => $"{c.name}:{c.GetType().Name}(trigger={c.isTrigger})")));
                DumpHierarchy(go.transform, "     ");
            }

            L("-- Material M_Fireworks_Sparkle:");
            var sm = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Generated/Task09/M_Fireworks_Sparkle.mat");
            if (sm != null) L($"   shader={sm.shader.name} renderQueue={sm.renderQueue} _Mode={(sm.HasProperty("_Mode") ? sm.GetFloat("_Mode").ToString() : "-")} emission={sm.IsKeywordEnabled("_EMISSION")} instancing={sm.enableInstancing} mainTex={(sm.HasProperty("_MainTex") && sm.GetTexture("_MainTex") != null ? sm.GetTexture("_MainTex").name : "none")}");
            else L("   NAO ENCONTRADO");
            L("-- Texturas Firework_*.png existentes: " + string.Join(", ", AssetDatabase.FindAssets("Firework t:Texture").Select(AssetDatabase.GUIDToAssetPath)));
            L("-- Audios fireworks/bobble: " + string.Join(", ", AssetDatabase.FindAssets("t:AudioClip").Select(AssetDatabase.GUIDToAssetPath).Where(p => p.ToLower().Contains("firework") || p.ToLower().Contains("bobble"))));
        }

        private static void DumpHierarchy(Transform t, string indent, int depth = 0)
        {
            var comps = t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name);
            L($"{indent}{new string(' ', depth * 2)}{t.name} lp={V(t.localPosition)} lr={V(t.localEulerAngles)} ls={V(t.localScale)} wp={V(t.position)} [{string.Join(",", comps)}] active={t.gameObject.activeSelf}");
            foreach (Transform c in t) DumpHierarchy(c, indent, depth + 1);
        }

        // ---------------------------------------------------------------- b) ESPACO LIVRE
        private static RaycastHit? First(Vector3 o, Vector3 d, float max, Transform ignore)
        {
            foreach (var h in Physics.RaycastAll(o, d, max, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                if (ignore != null && h.collider.transform.IsChildOf(ignore)) continue;
                return h;
            }
            return null;
        }

        private static Vector3 best_free_dir;
        private static float best_free_dist;

        private static void SectionFreeSpace(FireworksButtonInteractable fw)
        {
            H("b) ESPACO LIVRE em volta do botao de fogos");
            Physics.SyncTransforms();
            Transform bt = fw.transform;
            Bounds bb = RBounds(bt.gameObject).Value;
            float top = bb.max.y, floorOfBtn = bb.min.y;
            L($"AABB do botao: min={V(bb.min)} max={V(bb.max)}  topo={top:0.000} base(tampo)={floorOfBtn:0.000}");
            // movel de apoio
            var down = First(new Vector3(bt.position.x, floorOfBtn + 0.05f, bt.position.z), Vector3.down, 1f, bt);
            L($"Raycast para baixo: {(down.HasValue ? down.Value.collider.name + " (pai: " + (down.Value.collider.transform.parent != null ? down.Value.collider.transform.parent.name : "-") + ") y=" + down.Value.point.y.ToString("0.000") : "nada")}");

            Vector3 c = new Vector3(bt.position.x, top + 0.01f, bt.position.z);
            L("Raycast para cima (centro e 4 offsets de 0,15 m) a partir do topo:");
            float minUp = 99f;
            foreach (var off in new[] { Vector3.zero, new Vector3(0.15f, 0, 0), new Vector3(-0.15f, 0, 0), new Vector3(0, 0, 0.15f), new Vector3(0, 0, -0.15f) })
            {
                var h = First(c + off, Vector3.up, 6f, bt);
                if (h.HasValue) { L($"   off {V(off)} -> '{h.Value.collider.name}' a {h.Value.distance:0.000} m (y={h.Value.point.y:0.000})"); minUp = Mathf.Min(minUp, h.Value.distance); }
                else L($"   off {V(off)} -> nada em 6 m");
            }
            L($"Distancia livre acima do botao (minimo dos raios) = {minUp:0.000} m");

            L("Raycasts horizontais (8 direcoes) a partir do eixo do botao:");
            best_free_dist = 0f; best_free_dir = Vector3.forward;
            foreach (float h in new[] { 0.1f, 1.0f })
            {
                L($"  altura = tampo + {h:0.0} m (y={floorOfBtn + h:0.000})");
                for (int i = 0; i < 8; i++)
                {
                    Vector3 d = Quaternion.Euler(0, i * 45f, 0) * Vector3.forward;
                    var hit = First(new Vector3(bt.position.x, floorOfBtn + h, bt.position.z), d, 8f, bt);
                    float dist = hit.HasValue ? hit.Value.distance : 8f;
                    L($"     yaw {i * 45,3}: {(hit.HasValue ? hit.Value.collider.name : "livre >8 m")} a {dist:0.000} m");
                    if (h > 0.5f && dist > best_free_dist) { best_free_dist = dist; best_free_dir = d; }
                }
            }
            L($"Direcao horizontal mais livre a 1,0 m: {V(best_free_dir)} ({best_free_dist:0.00} m)");

            L("Renderers (AABB) numa coluna de 0,6 m de raio acima do botao ate +3 m (independente de colisor):");
            int n = 0;
            foreach (var r in MeshRenderers())
            {
                if (r.transform.IsChildOf(bt)) continue;
                Bounds b = r.bounds;
                if (b.max.y <= top + 0.01f || b.min.y > top + 3f) continue;
                Vector2 cp = new Vector2(Mathf.Clamp(bt.position.x, b.min.x, b.max.x) - bt.position.x, Mathf.Clamp(bt.position.z, b.min.z, b.max.z) - bt.position.z);
                if (cp.magnitude > 0.6f) continue;
                bool big = b.size.x > 4f || b.size.z > 4f || b.size.y > 4f;
                L($"   {Path_(r.transform)} dXZ={cp.magnitude:0.000} y[{b.min.y:0.000},{b.max.y:0.000}] size={V(b.size)} collider={(r.GetComponentInParent<Collider>() != null)}{(big ? " (ESTRUTURAL grande)" : "")}");
                if (++n >= 60) { L("   ... truncado"); break; }
            }
            // teto
            float ceil = float.NaN;
            foreach (var r in MeshRenderers())
            {
                Bounds b = r.bounds;
                if (b.size.x < 2f || b.size.z < 2f || b.min.y < top) continue;
                if (bt.position.x < b.min.x || bt.position.x > b.max.x || bt.position.z < b.min.z || bt.position.z > b.max.z) continue;
                if (float.IsNaN(ceil) || b.min.y < ceil) ceil = b.min.y;
            }
            L($"Teto (menor min.y de renderer grande >=2x2 m acima do botao) = {ceil:0.000} m  -> pe-direito acima do tampo = {ceil - floorOfBtn:0.000} m");
        }

        // ---------------------------------------------------------------- c) BOBBLEHEAD
        private static void SectionBobble(BobbleheadSpringToy bb)
        {
            H("c) BOBBLEHEAD");
            Transform t = bb.transform;
            Physics.SyncTransforms();
            Bounds? b = RBounds(t.gameObject);
            L($"Bobblehead '{Path_(t)}' world={V(t.position)} rotEuler={V(t.eulerAngles)} forward={V(t.forward)} scale={V(t.lossyScale)}");
            if (b.HasValue) L($"AABB min={V(b.Value.min)} max={V(b.Value.max)} size={V(b.Value.size)}");
            float baseY = b.HasValue ? b.Value.min.y : t.position.y;
            var down = First(new Vector3(t.position.x, baseY + 0.05f, t.position.z), Vector3.down, 1f, t);
            L($"Movel de apoio (raycast para baixo): {(down.HasValue ? Path_(down.Value.collider.transform) + " y=" + down.Value.point.y.ToString("0.000") + " gap=" + (baseY - down.Value.point.y).ToString("0.000") : "nada")}");
            L("Hierarquia completa:");
            DumpHierarchy(t, "   ");
            L("Colliders:");
            foreach (var c in t.GetComponentsInChildren<Collider>(true))
            {
                string d = c is SphereCollider sc ? $"Sphere center={V(sc.center)} radius={sc.radius:0.000}" : c is BoxCollider bc ? $"Box center={V(bc.center)} size={V(bc.size)}" : c is CapsuleCollider cc ? $"Capsule center={V(cc.center)} r={cc.radius:0.000} h={cc.height:0.000}" : c.GetType().Name;
                L($"   {Path_(c.transform)}: {d} isTrigger={c.isTrigger} worldCenter={V(c.bounds.center)}");
            }
            foreach (var rb in t.GetComponentsInChildren<Rigidbody>(true)) L($"Rigidbody em '{Path_(rb.transform)}': isKinematic={rb.isKinematic} useGravity={rb.useGravity} mass={rb.mass}");
            L("Renderers (nome, tris, material/shader):");
            foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
            {
                var r = mf.GetComponent<Renderer>();
                L($"   {mf.name} tris={Tris(mf.sharedMesh)} mat={string.Join("|", r.sharedMaterials.Select(m => m != null ? m.name + "/" + m.shader.name : "null"))}");
            }
            L("Lights: " + string.Join("; ", t.GetComponentsInChildren<Light>(true).Select(l => $"{l.name} type={l.type} range={l.range} int={l.intensity} shadows={l.shadows}")));
            L("-- Campos serializados do script NA CENA:");
            DumpSerialized(bb, "   ");
            var prefabs = AssetDatabase.FindAssets("ExoMascotBobblehead t:Prefab").Select(AssetDatabase.GUIDToAssetPath).ToList();
            foreach (var pp in prefabs)
            {
                L($"-- Prefab {pp}: hierarquia");
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(pp);
                DumpHierarchy(go.transform, "   ");
                var comp = go.GetComponentInChildren<BobbleheadSpringToy>(true);
                if (comp != null) DumpSerialized(comp, "   script: ");
            }
        }

        // ---------------------------------------------------------------- d) VIZINHOS
        private static GameObject SupportFurniture(GameObject fidget)
        {
            Bounds? b = RBounds(fidget);
            if (!b.HasValue) return null;
            var h = First(new Vector3(b.Value.center.x, b.Value.min.y + 0.05f, b.Value.center.z), Vector3.down, 0.5f, fidget.transform);
            if (!h.HasValue) return null;
            Transform tr = h.Value.collider.transform;
            Transform pick = tr;
            for (Transform p = tr; p != null; p = p.parent) if (FurnRx.IsMatch(p.name) && !p.name.ToLower().Contains("lamp")) { pick = p; }
            // mantem o ancestral mais externo que case, a menos que seja gigantesco (grupo da sala)
            Bounds? fb = RBounds(pick.gameObject);
            if (fb.HasValue && (fb.Value.size.x > 2.4f || fb.Value.size.z > 2.4f)) pick = tr;
            return pick.gameObject;
        }

        private static void SectionNeighbors(FireworksButtonInteractable fw, BobbleheadSpringToy bb, AtmosphereLeverInteractable lv)
        {
            H("d) ALAVANCA / BOTAO / BOBBLEHEAD - distancias e folgas");
            if (lv != null) L($"Alavanca '{Path_(lv.transform)}' world={V(lv.transform.position)} rotEuler={V(lv.transform.eulerAngles)} forward={V(lv.transform.forward)}");
            if (lv != null && fw != null)
            {
                var bl = RBounds(lv.gameObject); var bf = RBounds(fw.gameObject);
                L($"Distancia pivo alavanca <-> botao de fogos = {Vector3.Distance(lv.transform.position, fw.transform.position):0.000} m; folga entre AABBs = {(bl.HasValue && bf.HasValue ? AabbClear(bl.Value, bf.Value) : float.NaN):0.000} m");
            }
            if (bb != null && fw != null) L($"Distancia bobblehead <-> botao = {Vector3.Distance(bb.transform.position, fw.transform.position):0.000} m");
            if (bb != null && lv != null) L($"Distancia bobblehead <-> alavanca = {Vector3.Distance(bb.transform.position, lv.transform.position):0.000} m");

            var fidgets = new List<(string n, GameObject go)>();
            if (lv != null) fidgets.Add(("alavanca", lv.gameObject));
            if (fw != null) fidgets.Add(("botao_fogos", fw.gameObject));
            if (bb != null) fidgets.Add(("bobblehead", bb.gameObject));
            foreach (var f in fidgets)
            {
                var fb = RBounds(f.go);
                if (!fb.HasValue) continue;
                var furn = SupportFurniture(f.go);
                L($"[{f.n}] movel de apoio = {(furn != null ? Path_(furn.transform) : "?")}; tampo~{fb.Value.min.y:0.000}");
                var list = new List<(string who, float clr)>();
                foreach (var r in MeshRenderers())
                {
                    if (r.transform.IsChildOf(f.go.transform)) continue;
                    if (furn != null && r.transform.IsChildOf(furn.transform)) continue;
                    Bounds b = r.bounds;
                    if (b.size.x > 2.4f || b.size.y > 2.4f || b.size.z > 2.4f) continue; // paredes/piso/teto/grupos
                    if (b.max.y <= fb.Value.min.y + 0.002f) continue; // abaixo do tampo
                    list.Add((Path_(r.transform) + " size" + V(b.size), AabbClear(fb.Value, b)));
                }
                foreach (var x in list.OrderBy(x => x.clr).Take(4)) L($"     vizinho: folga {x.clr:0.000} m  {x.who}");
                // paredes/estruturais grandes tambem
                var walls = new List<(string who, float clr)>();
                foreach (var r in MeshRenderers())
                {
                    if (r.transform.IsChildOf(f.go.transform)) continue;
                    if (furn != null && r.transform.IsChildOf(furn.transform)) continue;
                    Bounds b = r.bounds;
                    if (!(b.size.x > 2.4f || b.size.z > 2.4f) || b.size.y > 4f || b.max.y <= fb.Value.min.y + 0.002f || b.min.y > fb.Value.max.y) continue;
                    walls.Add((Path_(r.transform) + " size" + V(b.size), AabbClear(fb.Value, b)));
                }
                foreach (var x in walls.OrderBy(x => x.clr).Take(2)) L($"     grande (parede/grupo): folga {x.clr:0.000} m  {x.who}");
            }
        }

        // ---------------------------------------------------------------- e) SUPERFICIES
        private static void SectionSurfaces(BobbleheadSpringToy bb, FireworksButtonInteractable fw, AtmosphereLeverInteractable lv)
        {
            H("e) SUPERFICIES CANDIDATAS para o bobblehead (item ~0,12 m de diametro, grade 2 cm)");
            Physics.SyncTransforms();
            var skipRoots = new List<Transform>();
            if (bb != null) skipRoots.Add(bb.transform);
            var cand = new List<GameObject>();
            foreach (var t in AllTransforms())
            {
                if (!FurnRx.IsMatch(t.name) || t.name.ToLower().Contains("lamp")) continue;
                if (t.parent != null && FurnRx.IsMatch(t.parent.name)) continue;
                var b = RBounds(t.gameObject);
                if (!b.HasValue) continue;
                if (b.Value.size.x > 2.4f || b.Value.size.y > 2.4f || b.Value.size.z > 2.4f) { L($"   (ignorado, AABB > 2,4 m) {Path_(t)} size={V(b.Value.size)}"); continue; }
                if (b.Value.max.y < 0.4f || b.Value.max.y > 2.0f) continue;
                cand.Add(t.gameObject);
            }
            var allR = MeshRenderers();
            var results = new List<(string name, float top, float best, Vector3 where, string who, Bounds fb)>();
            foreach (var f in cand)
            {
                Bounds fb = RBounds(f).Value;
                // altura do tampo: mediana de raios para baixo que atingem o movel
                var ys = new List<float>();
                for (int i = 0; i < 9; i++)
                {
                    Vector3 o = new Vector3(fb.center.x + (i % 3 - 1) * 0.25f * fb.size.x, fb.max.y + 0.4f, fb.center.z + (i / 3 - 1) * 0.25f * fb.size.z);
                    var h = First(o, Vector3.down, 2.5f, null);
                    if (h.HasValue && h.Value.collider.transform.IsChildOf(f.transform)) ys.Add(h.Value.point.y);
                }
                float top = fb.max.y;
                if (ys.Count >= 3) { ys.Sort(); top = ys[ys.Count / 2]; }
                // vizinhos relevantes
                var nb = new List<(Bounds b, string n)>();
                Bounds area = fb; area.Expand(new Vector3(1f, 2f, 1f));
                foreach (var r in allR)
                {
                    if (r.transform.IsChildOf(f.transform)) continue;
                    if (bb != null && r.transform.IsChildOf(bb.transform)) continue;
                    Bounds b = r.bounds;
                    if (b.size.x > 2.4f || b.size.y > 2.4f || b.size.z > 2.4f) continue;
                    if (b.max.y <= top + 0.002f || b.min.y > top + 0.6f) continue;
                    if (!area.Intersects(b)) continue;
                    // peca de movel contigua (mesmo tampo) nao e obstaculo
                    if (r.name.StartsWith("counter_") || (r.transform.parent != null && r.transform.parent.name.StartsWith("counter_"))) { if (b.max.y < top + 0.03f) continue; }
                    nb.Add((b, Path_(r.transform)));
                }
                float best = -1f; Vector3 bestAt = Vector3.zero; string bestWho = "";
                const float rad = 0.06f;
                for (float x = fb.min.x + rad; x <= fb.max.x - rad; x += 0.02f)
                    for (float z = fb.min.z + rad; z <= fb.max.z - rad; z += 0.02f)
                    {
                        // apoio: tampo logo abaixo
                        var hh = First(new Vector3(x, top + 0.05f, z), Vector3.down, 0.12f, null);
                        if (!hh.HasValue || !hh.Value.collider.transform.IsChildOf(f.transform)) continue;
                        Vector3 p = new Vector3(x, top + 0.06f, z);
                        float clr = 0.5f; string who = "";
                        foreach (var n in nb)
                        {
                            float d = Vector3.Distance(n.b.ClosestPoint(p), p) - rad;
                            if (n.b.Contains(p)) d = -rad;
                            if (d < clr) { clr = d; who = n.n; }
                        }
                        if (clr > best) { best = clr; bestAt = new Vector3(x, top, z); bestWho = who; }
                    }
                results.Add((Path_(f.transform), top, best, bestAt, bestWho, fb));
            }
            foreach (var r in results.OrderByDescending(r => r.best))
                L($"   {r.name}: tampo y={r.top:0.000}  melhorFolga={(r.best < 0 ? "sem apoio valido" : r.best.ToString("0.000") + " m")}  em {V(r.where)}  vizinhoMaisProximo='{r.who}' AABBmovel size={V(r.fb.size)}");
        }

        // ---------------------------------------------------------------- f) TRIS
        private static int Tris(Mesh m)
        {
            if (m == null) return 0;
            int t = 0; for (int i = 0; i < m.subMeshCount; i++) t += (int)(m.GetIndexCount(i) / 3);
            return t;
        }

        private static void SectionTris()
        {
            H("f) TRIANGULOS dos FBX");
            foreach (string n in new[] { "FireworksLaunchButton", "ExoMascotBobblehead", "EnvironmentControlLever" })
            {
                string p = $"Assets/Models/Generated/Task09/{n}.fbx";
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go == null) { L($"{n}: nao carregou"); continue; }
                int total = 0; var sbp = new StringBuilder();
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                {
                    int t = Tris(mf.sharedMesh); total += t;
                    var parts = Enumerable.Range(0, mf.sharedMesh.subMeshCount).Select(i => mf.sharedMesh.GetIndexCount(i) / 3).ToArray();
                    sbp.AppendLine($"      {mf.name}: {t} tris (submeshes: {string.Join("+", parts)}) lp={V(mf.transform.localPosition)} boundsLocal={V(mf.sharedMesh.bounds.size)}");
                }
                L($"{n}.fbx TOTAL = {total} tris");
                L(sbp.ToString().TrimEnd());
            }
        }

        private static void SectionLeverNote(AtmosphereLeverInteractable lv)
        {
            H("Alavanca - nota (somente registro)");
            string p = "Assets/Scripts/Interactive/Task09/AtmosphereLeverInteractable.cs";
            var lines = File.ReadAllLines(p);
            for (int i = 0; i < lines.Length; i++) if (lines[i].Contains("ledRenderer.material")) L($"{p}:{i + 1}: {lines[i].Trim()}");
        }

        // ---------------------------------------------------------------- h) CAPTURAS
        private static void Shot(string file, Vector3 pos, Vector3 lookAt, float fov, Action beforeRender = null)
        {
            var go = new GameObject("__Task09ScoutCam");
            go.hideFlags = HideFlags.HideAndDontSave;
            var cam = go.AddComponent<Camera>();
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation((lookAt - pos).normalized, Vector3.up);
            cam.fieldOfView = fov; cam.nearClipPlane = 0.02f; cam.farClipPlane = 60f;
            cam.clearFlags = CameraClearFlags.Skybox; cam.allowHDR = false; cam.allowMSAA = false;
            cam.enabled = false;
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            beforeRender?.Invoke();
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(OutDir, file), tex.EncodeToPNG());
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(go);
            L($"   {file}: camera pos={V(pos)} lookAt={V(lookAt)} fov={fov}");
        }

        private static void Captures(FireworksButtonInteractable fw, BobbleheadSpringToy bb, AtmosphereLeverInteractable lv)
        {
            H("h) CAPTURAS 'antes' (offscreen)");
            if (fw != null)
            {
                var so = new SerializedObject(fw);
                Transform apex = so.FindProperty("burstLocation").objectReferenceValue as Transform;
                var asc = so.FindProperty("fireworksAscentParticles").objectReferenceValue as ParticleSystem;
                var bur = so.FindProperty("fireworksBurstParticles").objectReferenceValue as ParticleSystem;
                float travel = so.FindProperty("rocketTravelTime").floatValue;
                Vector3 btn = fw.transform.position;
                Vector3 target = apex != null ? (btn + apex.position) * 0.5f : btn + Vector3.up;
                Vector3 dir = best_free_dir; float free = Mathf.Max(0.5f, best_free_dist);
                Vector3 fwdFlat = fw.transform.forward; fwdFlat.y = 0f; fwdFlat.Normalize();
                var fh = First(new Vector3(btn.x, 1.5f, btn.z), fwdFlat, 8f, fw.transform);
                float fdist = fh.HasValue ? fh.Value.distance : 8f;
                L($"Espaco livre na frente do botao (forward {V(fwdFlat)}) a y=1,5: {fdist:0.00} m");
                if (fdist >= 1.6f) { dir = fwdFlat; free = fdist; }
                Vector3 cam = btn + dir * Mathf.Min(2.0f, free * 0.85f);
                cam.y = 1.5f;
                L($"Camera dos fogos: 2 m (ou 85% do espaco livre) na frente do botao (ou direcao mais livre) {V(dir)}; alvo = ponto medio botao-apex {V(target)}");
                foreach (var t in new[] { 0.2f, 0.5f, 0.9f, 1.4f })
                {
                    string name = $"TASK-09_Before_Fireworks_t{Mathf.RoundToInt(t * 10f):00}.png";
                    Shot(name, cam, target, 70f, () =>
                    {
                        // simula como o runtime: trilha desde t=0; estouro comeca em t=rocketTravelTime
                        if (asc != null) asc.Simulate(t, true, true);
                        if (bur != null) bur.Simulate(Mathf.Max(0f, t - travel), true, true);
                    });
                    int a = asc != null ? asc.particleCount : -1, b = bur != null ? bur.particleCount : -1;
                    L($"      t={t:0.0}s: particulas vivas ascent={a} burst={b} (burst simulado a {Mathf.Max(0f, t - travel):0.0}s)");
                }
                if (asc != null) asc.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (bur != null) bur.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (bb != null) CloseUp(bb.gameObject, "TASK-09_Before_Bobble.png");
            if (lv != null) CloseUp(lv.gameObject, "TASK-09_Lever_Before.png");
        }

        private static void CloseUp(GameObject go, string file)
        {
            Bounds b = RBounds(go).Value;
            Vector3 f = go.transform.forward; f.y = 0f;
            string how = "forward do transform achatado";
            if (f.magnitude < 0.5f) { f = best_free_dir; how = "direcao mais livre (forward vertical)"; }
            f.Normalize();
            Vector3 pos = go.transform.position + f * 0.6f + Vector3.up * 0.25f;
            // posicao deterministica: 0,6 m a frente do pivo do objeto, 0,25 m acima do pivo
            L($"{file}: {how}; forward={V(go.transform.forward)}");
            Shot(file, pos, b.center, 45f);
        }
    }
}
