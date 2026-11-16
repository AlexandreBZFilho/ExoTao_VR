using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace VRRehab.EditorTools
{
    /// <summary>
    /// Garante física normal (cair, quicar, rolar) em TODOS os objetos pegáveis de uma cena:
    /// Rigidbody não cinemático com gravidade e sem travas, "Force Gravity On Detach" ligado,
    /// colisores sólidos (MeshCollider côncavo em corpo dinâmico não simula -> vira convexo).
    /// Relatório: .agents/visual_qa/GRAB_Physics_&lt;cena&gt;.txt
    ///
    /// Menu: "ExoTao IC/Pegáveis/Física Normal nos Pegáveis (Menu Principal)"
    /// CLI (editor aberto): unity command script eval 'return VRRehab.EditorTools.GrabbablePhysicsFixer.Fix("Assets/Scenes/Menu_Principal.unity");'
    /// CLI (editor fechado): unity run . -- -executeMethod VRRehab.EditorTools.GrabbablePhysicsFixer.RunBatchMenu
    /// </summary>
    public static class GrabbablePhysicsFixer
    {
        private const string MenuScene = "Assets/Scenes/Menu_Principal.unity";

        [MenuItem("ExoTao IC/Pegáveis/Física Normal nos Pegáveis (Menu Principal)")]
        public static void FixMenuFromMenu()
        {
            string r = Fix(MenuScene);
            EditorUtility.DisplayDialog("Pegáveis", r.Length > 1800 ? r.Substring(0, 1800) + "\n..." : r, "OK");
        }

        public static void RunBatchMenu()
        {
            int code = 0;
            try { Debug.Log(Fix(MenuScene)); }
            catch (System.Exception e) { Debug.LogException(e); code = 2; }
            EditorApplication.Exit(code);
        }

        /// <summary>Corrige a cena e devolve o relatório (antes -> depois). Cenas abertas com alterações pendentes não são salvas.</summary>
        public static string Fix(string scenePath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "FAIL: saia do Play Mode.";

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool wasOpen = scene.IsValid() && scene.isLoaded;
            bool wasDirty = wasOpen && scene.isDirty;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            var log = new StringBuilder();
            log.AppendLine($"=== Física dos pegáveis em {scene.name} ({System.DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");
            int total = 0, fixedCount = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (XRGrabInteractable grab in root.GetComponentsInChildren<XRGrabInteractable>(true))
                {
                    total++;
                    var changes = new StringBuilder();
                    FixOne(grab, changes);
                    string path = PathOf(grab.transform);
                    string type = grab.GetType().Name;
                    if (changes.Length > 0)
                    {
                        fixedCount++;
                        log.AppendLine($"* {path} [{type}]{changes}");
                    }
                    else log.AppendLine($"  {path} [{type}] ok");
                }
            }

            log.AppendLine($"--- {total} pegáveis, {fixedCount} corrigidos");

            if (fixedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!wasOpen || !wasDirty)
                {
                    EditorSceneManager.SaveScene(scene);
                    log.AppendLine("cena salva");
                }
                else log.AppendLine("cena aberta com alterações suas pendentes: SALVE com Ctrl+S");
            }
            string sceneName = scene.name;
            if (!wasOpen) EditorSceneManager.CloseScene(scene, true);

            string report = log.ToString();
            Directory.CreateDirectory(".agents/visual_qa");
            File.WriteAllText($".agents/visual_qa/GRAB_Physics_{sceneName}.txt", report);
            return report;
        }

        public static void RunBatchSettleMenu()
        {
            int code = 0;
            try { Debug.Log(SimulateSettle(MenuScene, 3f)); }
            catch (System.Exception e) { Debug.LogException(e); code = 2; }
            EditorApplication.Exit(code);
        }

        /// <summary>
        /// Teste sem salvar: simula a física da cena por N segundos (como no início do Play) e mede o quanto
        /// cada pegável se moveu. Itens que deslizam/caem das mesas e estantes aparecem com deslocamento grande.
        /// (Scripts não rodam aqui: uma fita presa por socket aparece caindo, o que não acontece no jogo.)
        /// </summary>
        public static string SimulateSettle(string scenePath, float seconds)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var grabs = new System.Collections.Generic.List<XRGrabInteractable>();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (XRGrabInteractable g in root.GetComponentsInChildren<XRGrabInteractable>(false))
                    grabs.Add(g);

            var start = new Vector3[grabs.Count];
            for (int i = 0; i < grabs.Count; i++) start[i] = grabs[i].transform.position;

            SimulationMode prevMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            const float step = 0.02f;
            for (float t = 0f; t < seconds; t += step) Physics.Simulate(step);
            Physics.simulationMode = prevMode;

            var log = new StringBuilder();
            log.AppendLine($"=== Teste de assentamento ({seconds:0.#} s de física, sem salvar) em {scene.name} ===");
            for (int i = 0; i < grabs.Count; i++)
            {
                Vector3 d = grabs[i].transform.position - start[i];
                Rigidbody rb = grabs[i].GetComponent<Rigidbody>();
                string flag = d.magnitude > 0.05f ? "  <-- MOVEU" : "";
                log.AppendLine($"{PathOf(grabs[i].transform)}: deslocamento {d.magnitude:0.000} m (dy {d.y:+0.000;-0.000}), kinematic={rb != null && rb.isKinematic}{flag}");
            }

            string report = log.ToString();
            Directory.CreateDirectory(".agents/visual_qa");
            File.WriteAllText($".agents/visual_qa/GRAB_Settle_{scene.name}.txt", report);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); // descarta a simulação
            return report;
        }

        private static void FixOne(XRGrabInteractable grab, StringBuilder changes)
        {
            GameObject go = grab.gameObject;
            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = go.AddComponent<Rigidbody>();
                changes.Append(" | +Rigidbody");
            }

            if (rb.isKinematic) { rb.isKinematic = false; changes.Append(" | cinemático->dinâmico"); }
            if (!rb.useGravity) { rb.useGravity = true; changes.Append(" | gravidade ligada"); }
            if (rb.constraints != RigidbodyConstraints.None) { changes.Append($" | travas {rb.constraints}->None"); rb.constraints = RigidbodyConstraints.None; }
            if (rb.linearDamping > 1f) { changes.Append($" | arrasto {rb.linearDamping:0.##}->0.05"); rb.linearDamping = 0.05f; }
            if (rb.mass <= 0.0001f) { rb.mass = 0.2f; changes.Append(" | massa 0->0.2"); }

            var so = new SerializedObject(grab);
            SerializedProperty fg = so.FindProperty("m_ForceGravityOnDetach");
            if (fg != null && !fg.boolValue) { fg.boolValue = true; changes.Append(" | forceGravityOnDetach"); }
            SerializedProperty dyn = so.FindProperty("alwaysDynamicAfterRelease");
            if (dyn != null && !dyn.boolValue) { dyn.boolValue = true; changes.Append(" | alwaysDynamicAfterRelease"); }
            so.ApplyModifiedPropertiesWithoutUndo();

            int solid = 0;
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
            {
                if (c.attachedRigidbody != rb && c.GetComponentInParent<Rigidbody>() != rb) continue;
                if (c is MeshCollider mc && !mc.convex)
                {
                    mc.convex = true;
                    changes.Append($" | MeshCollider '{c.name}' convexo");
                }
                if (!c.isTrigger && c.enabled) solid++;
            }
            if (solid == 0)
            {
                Bounds b = RendererBounds(go);
                var box = go.AddComponent<BoxCollider>();
                box.center = go.transform.InverseTransformPoint(b.center);
                Vector3 s = go.transform.lossyScale;
                box.size = new Vector3(b.size.x / Mathf.Max(0.0001f, Mathf.Abs(s.x)), b.size.y / Mathf.Max(0.0001f, Mathf.Abs(s.y)), b.size.z / Mathf.Max(0.0001f, Mathf.Abs(s.z)));
                changes.Append(" | +BoxCollider sólido (não tinha colisor sólido)");
            }

            EditorUtility.SetDirty(rb);
        }

        private static Bounds RendererBounds(GameObject go)
        {
            Bounds b = new Bounds(go.transform.position, Vector3.one * 0.1f);
            bool has = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        private static string PathOf(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }
    }
}
