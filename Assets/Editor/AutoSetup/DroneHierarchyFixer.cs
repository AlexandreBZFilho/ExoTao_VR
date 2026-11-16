using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;
using VRRehab.DronePet;

namespace VRRehab.EditorTools
{
    /// <summary>
    /// Corrige e padroniza o "[IC] Drone_Guia_IA_ExoBot" e TODOS os filhos em todas as cenas de Assets/Scenes.
    /// O drone do Menu_Principal é o modelo de referência (tem visor LED, corações, confetes e balão de fala):
    /// cenas de mapa que não têm essas peças recebem cópias idênticas, com as referências internas remapeadas.
    ///
    /// Execução: menu "ExoTao IC/Drone/Corrigir Drone (Todas as Cenas)", arquivo-gatilho ".run_drone_fix"
    /// na raiz do projeto, ou ponte de CLI: exec VRRehab.EditorTools.DroneHierarchyFixer.Run
    /// Cenas fechadas são abertas, corrigidas, salvas e fechadas. Cenas abertas só são salvas se estavam sem
    /// alterações pendentes antes da correção (para nunca salvar trabalho seu por acidente).
    /// </summary>
    [InitializeOnLoad]
    public static class DroneHierarchyFixer
    {
        private const string TriggerFile = ".run_drone_fix";
        private const string ResultFile = ".agents/visual_qa/DRONE_HierarchyFix_Result.txt";
        private const string TemplateScenePath = "Assets/Scenes/Menu_Principal.unity";
        private const string DroneName = "[IC] Drone_Guia_IA_ExoBot";
        private const string ModelName = "Modelo_3D_ExoBot";

        /// <summary>Filhos que o drone deve ter (copiados do drone do Menu quando faltarem).</summary>
        private static readonly string[] FeatureChildren =
        {
            ModelName + "/Visor_LED_Frontal",
            "ParticleSystem_Hearts",
            "ConfettiCannons_Group",
            "SpeechBubble_Anchor",
        };

        static DroneHierarchyFixer()
        {
            EditorApplication.delayCall += () =>
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(), TriggerFile);
                if (!File.Exists(path)) return;
                try { File.Delete(path); } catch { }
                Run();
            };
        }

        [MenuItem("ExoTao IC/Drone/Corrigir Drone (Todas as Cenas)")]
        public static void RunFromMenu()
        {
            string report = Run();
            EditorUtility.DisplayDialog("Drone", report.Length > 1800 ? report.Substring(0, 1800) + "\n..." : report, "OK");
        }

        /// <summary>
        /// Entrada para a Unity em batchmode (Unity CLI com o editor fechado):
        /// Unity.exe -batchmode -projectPath . -executeMethod VRRehab.EditorTools.DroneHierarchyFixer.RunBatch
        /// Corrige todas as cenas, abre o Menu e salva capturas do drone em .agents/visual_qa/DRONE_view_*.png.
        /// </summary>
        public static void RunBatch()
        {
            int code = 0;
            try
            {
                string report = Run();
                if (report.StartsWith("FAIL") || report.Contains("ERRO")) code = 1;
                EditorSceneManager.OpenScene(TemplateScenePath, OpenSceneMode.Single);
                Debug.Log("[DroneHierarchyFixer] Capturas:\n" + Capture());
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                code = 2;
            }
            EditorApplication.Exit(code);
        }

        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "FAIL: saia do Play Mode antes de corrigir o drone.";

            var log = new StringBuilder();
            log.AppendLine($"=== Correção do Drone ({System.DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");

            // ---- Modelo de referência (drone do Menu) ----
            Scene templateScene = SceneManager.GetSceneByPath(TemplateScenePath);
            bool templateWasOpen = templateScene.IsValid() && templateScene.isLoaded;
            bool templateWasDirty = templateWasOpen && templateScene.isDirty;
            if (!templateWasOpen) templateScene = EditorSceneManager.OpenScene(TemplateScenePath, OpenSceneMode.Additive);
            DroneGuiaIA template = FindDrone(templateScene);
            if (template == null) log.AppendLine("AVISO: drone do Menu_Principal não encontrado; peças faltantes não serão copiadas.");

            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
                {
                    string scenePath = AssetDatabase.GUIDToAssetPath(guid);
                    Scene scene = SceneManager.GetSceneByPath(scenePath);
                    bool wasOpen = scene.IsValid() && scene.isLoaded;
                    bool wasDirty = wasOpen && scene.isDirty;
                    if (scenePath == TemplateScenePath) { wasOpen = templateWasOpen; wasDirty = templateWasDirty; }
                    if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

                    var sceneLog = new StringBuilder();
                    int changes = 0;
                    try
                    {
                        foreach (GameObject root in scene.GetRootGameObjects())
                            foreach (DroneGuiaIA drone in root.GetComponentsInChildren<DroneGuiaIA>(true))
                                changes += FixDrone(drone, template, sceneLog);
                    }
                    catch (System.Exception e)
                    {
                        log.AppendLine($"[{scene.name}] ERRO — cena NÃO salva: {e.Message}");
                        Debug.LogException(e);
                        if (!wasOpen && scenePath != TemplateScenePath) EditorSceneManager.CloseScene(scene, true);
                        continue;
                    }

                    if (changes > 0)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        bool save = !wasOpen || !wasDirty;
                        if (save) EditorSceneManager.SaveScene(scene);
                        log.AppendLine($"[{scene.name}] {changes} ajustes {(save ? "(salva)" : "(cena aberta com alterações suas pendentes: SALVE com Ctrl+S)")}");
                        log.Append(sceneLog);
                    }
                    else if (sceneLog.Length > 0 || FindDrone(scene) != null) log.AppendLine($"[{scene.name}] já estava correto");

                    if (!wasOpen && scenePath != TemplateScenePath) EditorSceneManager.CloseScene(scene, true);
                }
            }
            finally
            {
                if (!templateWasOpen && templateScene.IsValid()) EditorSceneManager.CloseScene(templateScene, true);
            }

            string report = log.ToString();
            Directory.CreateDirectory(Path.GetDirectoryName(ResultFile));
            File.WriteAllText(ResultFile, report);
            Debug.Log(report);
            return report;
        }

        private static DroneGuiaIA FindDrone(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                DroneGuiaIA d = root.GetComponentInChildren<DroneGuiaIA>(true);
                if (d != null) return d;
            }
            return null;
        }

        // =====================================================================
        // CORREÇÃO DE UM DRONE
        // =====================================================================

        private static int FixDrone(DroneGuiaIA drone, DroneGuiaIA template, StringBuilder log)
        {
            int n = 0;
            GameObject go = drone.gameObject;
            Transform t = go.transform;
            bool isTemplate = template != null && drone == template;
            void Note(string what) { n++; log.AppendLine($"  - {what}"); }

            if (go.name != DroneName) { Note($"renomeado '{go.name}' -> '{DroneName}'"); go.name = DroneName; }

            // ---- 1. Restos de versões antigas ----
            int missing = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            foreach (Transform child in t.GetComponentsInChildren<Transform>(true))
                missing += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
            if (missing > 0) Note($"{missing} scripts ausentes (Missing Script) removidos");

            foreach (string legacy in new[] { "HeadPettingTrigger", "FacePokeTrigger", "Canvas_Holograma_Clinico/Zona_Arraste_Corpo_Drone", "Canvas_Holograma_Clinico/Barra_Controle_Espacial" })
            {
                Transform c = t.Find(legacy);
                if (c != null) { Object.DestroyImmediate(c.gameObject); Note($"objeto legado removido: {legacy}"); }
            }

            Transform model = t.Find(ModelName);
            if (model == null) { log.AppendLine($"  ! '{ModelName}' não existe — o DroneGuiaIA o recria em runtime"); }

            // ---- 2. Peças que faltam (copiadas do drone do Menu) ----
            if (template != null && !isTemplate)
            {
                foreach (string rel in FeatureChildren)
                {
                    if (t.Find(rel) != null) continue;
                    Transform src = template.transform.Find(rel);
                    int slash = rel.LastIndexOf('/');
                    Transform dstParent = slash < 0 ? t : t.Find(rel.Substring(0, slash));
                    if (src == null || dstParent == null) continue;
                    GameObject copy = Object.Instantiate(src.gameObject, dstParent);
                    copy.name = src.name;
                    copy.transform.localPosition = src.localPosition;
                    copy.transform.localRotation = src.localRotation;
                    copy.transform.localScale = src.localScale;
                    foreach (Canvas cv in copy.GetComponentsInChildren<Canvas>(true)) cv.worldCamera = null;
                    Note($"peça copiada do drone do Menu: {rel}");
                }

                foreach (System.Type type in new[] { typeof(AudioSource), typeof(DroneAffectiveSystem), typeof(DroneMilestoneCelebration) })
                {
                    Component src = template.GetComponent(type);
                    if (src == null || go.GetComponent(type) != null) continue;
                    Component dst = go.AddComponent(type);
                    CopyComponentValues(src, dst, template.transform, t);
                    Note($"componente adicionado (cópia do Menu): {type.Name}");
                }
            }

            // ---- 3. Corpo: colisor sólido (o laser ignora triggers) ajustado à malha ----
            BoxCollider box = go.GetComponent<BoxCollider>();
            if (box == null) { box = go.AddComponent<BoxCollider>(); Note("BoxCollider adicionado"); }
            if (box.isTrigger) { box.isTrigger = false; Note("BoxCollider do corpo agora é sólido"); }
            if (!box.enabled) { box.enabled = true; Note("BoxCollider do corpo reativado"); }
            if (model != null && TryModelBounds(t, model, out Vector3 center, out Vector3 size))
            {
                if ((box.center - center).sqrMagnitude > 1e-6f || (box.size - size).sqrMagnitude > 1e-6f)
                {
                    box.center = center;
                    box.size = size;
                    Note($"BoxCollider ajustado à malha: centro {center:F3}, tamanho {size:F3}");
                }
            }

            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb == null) { rb = go.AddComponent<Rigidbody>(); Note("Rigidbody adicionado"); }
            if (!rb.isKinematic || rb.useGravity || rb.interpolation != RigidbodyInterpolation.None || rb.collisionDetectionMode != CollisionDetectionMode.Discrete)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.None;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                Note("Rigidbody: cinemático, sem gravidade, sem interpolação");
            }

            // ---- 4. Locomoção/arraste: valores salvos antigos -> padrões atuais do script ----
            var companion = go.GetComponent<DroneCompanionFollowAndGrab>();
            if (companion == null) { companion = go.AddComponent<DroneCompanionFollowAndGrab>(); Note("DroneCompanionFollowAndGrab adicionado"); }
            n += ResetCompanionTuning(companion, box, drone, log);

            // ---- 5. Áudio da comemoração (na raiz) ----
            AudioSource rootAudio = go.GetComponent<AudioSource>();
            if (rootAudio != null)
                n += Set(log, "AudioSource da raiz (comemoração)", rootAudio, a =>
                {
                    a.playOnAwake = false; a.loop = false; a.spatialBlend = 1f; a.dopplerLevel = 0f;
                    a.minDistance = 1f; a.maxDistance = 12f; a.rolloffMode = AudioRolloffMode.Logarithmic;
                }, a => !a.playOnAwake && !a.loop && a.spatialBlend == 1f && a.dopplerLevel == 0f && a.maxDistance == 12f && a.rolloffMode == AudioRolloffMode.Logarithmic);

            // ---- 6. Modelo 3D, visor, luz e áudios do modelo ----
            if (model != null)
            {
                MeshRenderer mr = model.GetComponent<MeshRenderer>();
                if (mr != null)
                    n += Set(log, "malha do drone sem sombra projetada (VR)", mr, r =>
                    {
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        r.receiveShadows = true;
                    }, r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off);

                Transform visor = model.Find("Visor_LED_Frontal");
                MeshRenderer vr = visor != null ? visor.GetComponent<MeshRenderer>() : null;
                if (vr != null)
                    n += Set(log, "visor LED sem sombras/probes", vr, r =>
                    {
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        r.receiveShadows = false;
                        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                    }, r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off && !r.receiveShadows &&
                            r.lightProbeUsage == UnityEngine.Rendering.LightProbeUsage.Off);

                foreach (Light l in model.GetComponentsInChildren<Light>(true))
                    n += Set(log, $"luz '{l.name}' (sem sombra, alcance 2.5 m)", l, li =>
                    {
                        li.type = LightType.Point; li.shadows = LightShadows.None; li.range = 2.5f; li.intensity = 1.2f;
                    }, li => li.shadows == LightShadows.None && li.range == 2.5f && li.intensity == 1.2f);

                AudioSource[] modelAudio = model.GetComponents<AudioSource>();
                if (modelAudio.Length > 2)
                {
                    for (int i = modelAudio.Length - 1; i >= 2; i--) Object.DestroyImmediate(modelAudio[i]);
                    Note($"{modelAudio.Length - 2} AudioSource(s) duplicados removidos do modelo");
                    modelAudio = model.GetComponents<AudioSource>();
                }
                if (modelAudio.Length > 0)
                    n += Set(log, "áudio de hover (loop suave 3D)", modelAudio[0], a =>
                    {
                        a.loop = true; a.playOnAwake = true; a.spatialBlend = 1f; a.dopplerLevel = 0f;
                        a.minDistance = 0.8f; a.maxDistance = 8f; a.volume = 0.12f;
                    }, a => a.loop && a.spatialBlend == 1f && a.dopplerLevel == 0f && a.volume == 0.12f && a.maxDistance == 8f);
                if (modelAudio.Length > 1)
                    n += Set(log, "áudio de voz do drone (3D, sem loop)", modelAudio[1], a =>
                    {
                        a.loop = false; a.playOnAwake = false; a.spatialBlend = 1f; a.dopplerLevel = 0f;
                        a.minDistance = 1f; a.maxDistance = 10f; a.volume = 0.5f;
                    }, a => !a.loop && !a.playOnAwake && a.spatialBlend == 1f && a.dopplerLevel == 0f && a.volume == 0.5f);
            }

            // ---- 7. Painéis (HUD e balão): só leitura, nada clicável -> sem raycasters ----
            foreach (string canvasPath in new[] { "Canvas_Holograma_Clinico", "SpeechBubble_Anchor/SpeechBubble_Canvas" })
            {
                Transform ct = t.Find(canvasPath);
                if (ct == null) continue;
                n += StripRaycasting(ct.gameObject, canvasPath, drone, log);
            }

            // ---- 8. Soquete da coroa: só trigger (o XRSocketInteractor aceitava QUALQUER objeto) ----
            Transform socket = t.Find("DroneHeadSocket");
            if (socket != null)
            {
                foreach (XRSocketInteractor s in socket.GetComponents<XRSocketInteractor>())
                {
                    Object.DestroyImmediate(s);
                    Note("DroneHeadSocket: XRSocketInteractor removido (prendia fitas/bolas/maçãs na cabeça do drone)");
                }
                SphereCollider sc = socket.GetComponent<SphereCollider>();
                if (sc != null && !sc.isTrigger) { sc.isTrigger = true; Note("DroneHeadSocket: colisor virou trigger"); }
            }

            // ---- 9. Partículas: nada de sombra/colisão ----
            foreach (ParticleSystemRenderer pr in t.GetComponentsInChildren<ParticleSystemRenderer>(true))
                n += Set(log, $"partículas '{pr.name}' sem sombra", pr, r =>
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                }, r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off && !r.receiveShadows);

            // ---- 10. Referências do cérebro e do sistema afetivo ----
            var affective = go.GetComponent<DroneAffectiveSystem>();
            if (affective != null)
            {
                var so = new SerializedObject(affective);
                n += FixRef(so, "droneVisualRoot", model, log);
                Transform visorT = t.Find(ModelName + "/Visor_LED_Frontal");
                n += FixRef(so, "visorRenderer", visorT != null ? visorT.GetComponent<MeshRenderer>() : null, log);
                Transform hearts = t.Find("ParticleSystem_Hearts");
                n += FixRef(so, "heartParticles", hearts != null ? hearts.GetComponent<ParticleSystem>() : null, log);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var celebration = go.GetComponent<DroneMilestoneCelebration>();
            if (celebration != null)
            {
                var so = new SerializedObject(celebration);
                n += FixRef(so, "droneGuia", drone, log);
                n += FixRef(so, "affectiveSystem", affective, log);
                n += FixRef(so, "visualModelTransform", model, log);
                n += FixRef(so, "celebrationAudioSource", rootAudio, log);
                Transform anchor = t.Find("SpeechBubble_Anchor");
                n += FixRef(so, "speechBubbleAnchor", anchor, log);
                Transform cl = t.Find("ConfettiCannons_Group/ConfettiCannon_Left");
                Transform cr = t.Find("ConfettiCannons_Group/ConfettiCannon_Right");
                n += FixRef(so, "leftConfettiCannon", cl != null ? cl.GetComponent<ParticleSystem>() : null, log);
                n += FixRef(so, "rightConfettiCannon", cr != null ? cr.GetComponent<ParticleSystem>() : null, log);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return n;
        }

        // =====================================================================
        // AUXILIARES
        // =====================================================================

        /// <summary>Copia para o drone da cena os valores padrão ATUAIS do script (a cena guardava valores da versão antiga).</summary>
        private static int ResetCompanionTuning(DroneCompanionFollowAndGrab companion, BoxCollider box, DroneGuiaIA drone, StringBuilder log)
        {
            int n = 0;
            var temp = new GameObject("~DroneDefaults") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var defaults = temp.AddComponent<DroneCompanionFollowAndGrab>();
                var src = new SerializedObject(defaults);
                var dst = new SerializedObject(companion);
                SerializedProperty p = src.GetIterator();
                var changed = new List<string>();
                bool enter = true;
                while (p.NextVisible(enter))
                {
                    enter = false;
                    if (p.name.StartsWith("m_") || p.propertyType == SerializedPropertyType.ObjectReference) continue;
                    SerializedProperty d = dst.FindProperty(p.propertyPath);
                    if (d == null || SerializedProperty.DataEquals(p, d)) continue;
                    dst.CopyFromSerializedProperty(p);
                    changed.Add(p.name);
                }

                // Único colisor de interação = o corpo (triggers filhos não roubam o laser)
                SerializedProperty cols = dst.FindProperty("m_Colliders");
                if (cols != null && (cols.arraySize != 1 || cols.GetArrayElementAtIndex(0).objectReferenceValue != box))
                {
                    cols.arraySize = 1;
                    cols.GetArrayElementAtIndex(0).objectReferenceValue = box;
                    changed.Add("colliders=[BoxCollider do corpo]");
                }

                SerializedProperty sfx = dst.FindProperty("sfxAnchorLock");
                if (sfx != null && sfx.objectReferenceValue == null)
                {
                    sfx.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Generated/Task03/drone_anchor_lock.wav");
                    if (sfx.objectReferenceValue != null) changed.Add("sfxAnchorLock");
                }

                SerializedProperty head = dst.FindProperty("headTransform");
                if (head != null && head.objectReferenceValue == null && drone.cameraPaciente != null)
                {
                    head.objectReferenceValue = drone.cameraPaciente;
                    changed.Add("headTransform");
                }

                if (changed.Count > 0)
                {
                    dst.ApplyModifiedPropertiesWithoutUndo();
                    n++;
                    log.AppendLine($"  - DroneCompanionFollowAndGrab atualizado: {string.Join(", ", changed)}");
                }
            }
            finally { Object.DestroyImmediate(temp); }
            return n;
        }

        private static int StripRaycasting(GameObject canvasGo, string label, DroneGuiaIA drone, StringBuilder log)
        {
            int n = 0;
            foreach (TrackedDeviceGraphicRaycaster r in canvasGo.GetComponents<TrackedDeviceGraphicRaycaster>()) { Object.DestroyImmediate(r); n++; }
            foreach (GraphicRaycaster r in canvasGo.GetComponents<GraphicRaycaster>()) { Object.DestroyImmediate(r); n++; }
            foreach (EventTrigger et in canvasGo.GetComponentsInChildren<EventTrigger>(true)) { Object.DestroyImmediate(et); n++; }
            foreach (Graphic g in canvasGo.GetComponentsInChildren<Graphic>(true)) if (g.raycastTarget) { g.raycastTarget = false; n++; }
            foreach (CanvasGroup cg in canvasGo.GetComponentsInChildren<CanvasGroup>(true))
                if (cg.blocksRaycasts || cg.interactable) { cg.blocksRaycasts = false; cg.interactable = false; n++; }

            Canvas cv = canvasGo.GetComponent<Canvas>();
            if (cv != null && cv.worldCamera == null && drone.cameraPaciente != null && drone.cameraPaciente.TryGetComponent(out Camera cam))
            {
                cv.worldCamera = cam;
                n++;
            }
            if (n > 0) log.AppendLine($"  - {label}: painel só de leitura (raycasters/alvos de clique removidos)");
            return n > 0 ? 1 : 0;
        }

        private static int FixRef(SerializedObject so, string field, Object value, StringBuilder log)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null || value == null || p.objectReferenceValue == value) return 0;
            p.objectReferenceValue = value;
            log.AppendLine($"  - {so.targetObject.GetType().Name}.{field} -> {value.name}");
            return 1;
        }

        private static int Set<T>(StringBuilder log, string what, T obj, System.Action<T> apply, System.Func<T, bool> isOk) where T : Object
        {
            if (isOk(obj)) return 0;
            apply(obj);
            EditorUtility.SetDirty(obj);
            log.AppendLine($"  - {what}");
            return 1;
        }

        /// <summary>Bounds da malha do modelo no espaço local da raiz do drone (para o BoxCollider do corpo).</summary>
        public static bool TryModelBounds(Transform root, Transform model, out Vector3 center, out Vector3 size)
        {
            center = size = Vector3.zero;
            MeshFilter mf = model.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return false;
            Bounds mb = mf.sharedMesh.bounds;
            Matrix4x4 toRoot = root.worldToLocalMatrix * model.localToWorldMatrix;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = toRoot.MultiplyPoint3x4(c);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            center = Round((min + max) * 0.5f);
            size = Round((max - min) * 0.95f);
            return true;
        }

        private static Vector3 Round(Vector3 v) => new Vector3(Mathf.Round(v.x * 1000f) / 1000f, Mathf.Round(v.y * 1000f) / 1000f, Mathf.Round(v.z * 1000f) / 1000f);

        /// <summary>
        /// Copia os valores serializados de um componente para outro, campo a campo, e troca referências que apontam
        /// para o drone-modelo (outra cena) pelos equivalentes no drone de destino.
        /// NÃO usar EditorUtility.CopySerialized entre GameObjects: ele copia também o m_GameObject (dono do componente),
        /// deixando o componente "pendurado" no objeto da outra cena — isso derrubou a Unity na primeira versão.
        /// </summary>
        private static void CopyComponentValues(Component src, Component dst, Transform srcRoot, Transform dstRoot)
        {
            var from = new SerializedObject(src);
            var to = new SerializedObject(dst);
            SerializedProperty p = from.GetIterator();
            bool enter = true;
            while (p.NextVisible(enter))
            {
                enter = false;
                if (p.name == "m_Script" || p.name == "m_GameObject" || p.name == "m_ObjectHideFlags" ||
                    p.name == "m_CorrespondingSourceObject" || p.name == "m_PrefabInstance" || p.name == "m_PrefabAsset") continue;
                to.CopyFromSerializedProperty(p);
            }
            to.ApplyModifiedPropertiesWithoutUndo();

            to.Update();
            SerializedProperty q = to.GetIterator();
            while (q.Next(true))
            {
                if (q.propertyType != SerializedPropertyType.ObjectReference || q.name == "m_GameObject" || q.name == "m_Script") continue;
                Object o = q.objectReferenceValue;
                if (o == null || EditorUtility.IsPersistent(o)) continue;
                q.objectReferenceValue = MapObject(o, srcRoot, dstRoot);
            }
            to.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Object MapObject(Object o, Transform srcRoot, Transform dstRoot)
        {
            Transform st = o is Component c ? c.transform : (o as GameObject)?.transform;
            if (st == null || (st != srcRoot && !st.IsChildOf(srcRoot))) return null; // referência a outra coisa da cena do Menu: detectada em runtime
            string rel = RelativePath(srcRoot, st);
            Transform dt = rel.Length == 0 ? dstRoot : dstRoot.Find(rel);
            if (dt == null) return null;
            if (o is GameObject) return dt.gameObject;
            if (o is Transform) return dt;
            Component sc = (Component)o;
            Component[] same = st.GetComponents(sc.GetType());
            int idx = System.Array.IndexOf(same, sc);
            Component[] cand = dt.GetComponents(sc.GetType());
            return idx >= 0 && idx < cand.Length ? cand[idx] : null;
        }

        private static string RelativePath(Transform root, Transform t)
        {
            if (t == root) return "";
            string p = t.name;
            while (t.parent != null && t.parent != root) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        // =====================================================================
        // CAPTURA OFFSCREEN (QA visual sem mexer no mouse/teclado)
        // =====================================================================

        /// <summary>Renderiza o drone da cena ativa (frente, lado, 3/4 de cima) em .agents/visual_qa/DRONE_view_*.png.</summary>
        public static string Capture()
        {
            DroneGuiaIA drone = Object.FindFirstObjectByType<DroneGuiaIA>(FindObjectsInactive.Exclude);
            if (drone == null) return "FAIL: nenhum drone na cena aberta";
            Transform t = drone.transform;
            Vector3 focus = t.position + Vector3.up * 0.15f;

            var views = new (string name, Vector3 pos)[]
            {
                ("front", t.position + t.forward * 1.9f + Vector3.up * 0.25f),
                ("side", t.position + t.right * 1.9f + Vector3.up * 0.2f),
                ("top34", t.position + (t.forward - t.right).normalized * 1.6f + Vector3.up * 1.1f),
                ("close_visor", t.position + t.forward * 0.85f + Vector3.up * 0.05f),
            };

            var camGo = new GameObject("~DroneQACam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.Skybox;
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            var sb = new StringBuilder();
            RenderTexture prev = RenderTexture.active;
            try
            {
                cam.targetTexture = rt;
                Directory.CreateDirectory(".agents/visual_qa");
                foreach (var v in views)
                {
                    camGo.transform.position = v.pos;
                    camGo.transform.LookAt(v.name == "close_visor" ? t.position : focus);
                    cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    tex.Apply();
                    string path = $".agents/visual_qa/DRONE_view_{v.name}.png";
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    sb.AppendLine(Path.GetFullPath(path));
                }
            }
            finally
            {
                RenderTexture.active = prev;
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(camGo);
            }
            return sb.ToString();
        }
    }
}
