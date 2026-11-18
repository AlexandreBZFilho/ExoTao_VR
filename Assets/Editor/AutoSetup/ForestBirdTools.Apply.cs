using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Interactive.Task13;

namespace VRRehab.EditorTools
{
    public static partial class ForestBirdTools
    {
        public const string BirdPrefabPath = "Assets/Prefabs/Interactive/LowPolyForestBird.prefab";
        private const float LegHeight = 0.035f;

        /// <summary>Corrige o prefab do pássaro: duas pernas visíveis apoiadas em Y = 0 e AudioSource suave.</summary>
        public static string ApplyPrefabFix()
        {
            var log = new StringBuilder();
            GameObject root = PrefabUtility.LoadPrefabContents(BirdPrefabPath);
            try
            {
                EnsureLegs(root.transform, log);

                ReactiveForestBird bird = root.GetComponent<ReactiveForestBird>();
                AudioSource a = root.GetComponent<AudioSource>();
                if (a == null) a = root.AddComponent<AudioSource>();
                var so = new SerializedObject(bird);
                so.FindProperty("audioSource").objectReferenceValue = a;
                so.ApplyModifiedPropertiesWithoutUndo();
                bird.ConfigureAudioSource();
                EditorUtility.SetDirty(a);
                log.AppendLine($"AudioSource do prefab: loop={a.loop} playOnAwake={a.playOnAwake} volume={a.volume} blend={a.spatialBlend} maxDistance={a.maxDistance} rolloff={a.rolloffMode}");

                int tris = 0;
                foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
                log.AppendLine($"triângulos do pássaro com pernas: {tris} (limite 1800)");

                PrefabUtility.SaveAsPrefabAsset(root, BirdPrefabPath);
                log.AppendLine("prefab do pássaro salvo");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return log.ToString();
        }

        private static void EnsureLegs(Transform root, StringBuilder log)
        {
            if (root.Find("Leg_Left") != null && root.Find("Leg_Right") != null) { log.AppendLine("pernas já existem"); return; }

            Renderer body = root.Find("Bird_Body") != null ? root.Find("Bird_Body").GetComponent<Renderer>() : null;
            Renderer beak = root.Find("Bird_Beak") != null ? root.Find("Bird_Beak").GetComponent<Renderer>() : null;
            if (body == null) { log.AppendLine("ERRO: Bird_Body não encontrado; pernas não criadas"); return; }

            // Frente do pássaro = do centro do corpo para o bico (no plano horizontal)
            Vector3 bodyC = root.InverseTransformPoint(body.bounds.center);
            Vector3 fwd = Vector3.forward;
            if (beak != null)
            {
                Vector3 d = root.InverseTransformPoint(beak.bounds.center) - bodyC;
                d.y = 0f;
                if (d.sqrMagnitude > 1e-6f) fwd = d.normalized;
            }
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Material legMat = beak != null ? beak.sharedMaterial : body.sharedMaterial;

            // Sobe o corpo para as pernas aparecerem (antes a barriga encostava direto no poleiro)
            foreach (Transform ch in root)
                ch.localPosition += Vector3.up * LegHeight;
            SphereCollider sc = root.GetComponent<SphereCollider>();
            if (sc != null) sc.center += Vector3.up * LegHeight;

            float halfSpread = Mathf.Clamp(body.bounds.size.x * 0.16f, 0.014f, 0.03f);
            // Ligeiramente atrás do centro do corpo, como num pássaro pousado
            Vector3 hip = new Vector3(bodyC.x, 0f, bodyC.z) - fwd * 0.004f;
            BuildLeg(root, "Leg_Left", hip + right * halfSpread, fwd, legMat);   // Wing_L fica em +X
            BuildLeg(root, "Leg_Right", hip - right * halfSpread, fwd, legMat);
            log.AppendLine($"pernas criadas (Leg_Left / Leg_Right), corpo elevado {LegHeight * 100f:0.0} cm, pés em Y = 0");
        }

        private static void BuildLeg(Transform root, string name, Vector3 localPos, Vector3 fwd, Material mat)
        {
            var leg = new GameObject(name);
            leg.transform.SetParent(root, false);
            leg.transform.localPosition = localPos;
            leg.transform.localRotation = Quaternion.LookRotation(fwd, Vector3.up);

            // Canela: do pé (Y = 0) até dentro da barriga
            const float shankTop = 0.075f;
            AddPart(leg.transform, PrimitiveType.Cylinder, "Shank", new Vector3(0f, shankTop * 0.5f, 0f), Quaternion.identity,
                new Vector3(0.0075f, shankTop * 0.5f, 0.0075f), mat);

            // Pé: três dedos para a frente e um para trás, apoiados no poleiro
            float[] yaw = { -28f, 0f, 28f };
            foreach (float y in yaw)
            {
                Quaternion r = Quaternion.Euler(0f, y, 0f);
                AddPart(leg.transform, PrimitiveType.Cube, "Toe_Front", r * new Vector3(0f, 0.0022f, 0.013f), r, new Vector3(0.0048f, 0.0044f, 0.026f), mat);
            }
            AddPart(leg.transform, PrimitiveType.Cube, "Toe_Back", new Vector3(0f, 0.0022f, -0.008f), Quaternion.identity, new Vector3(0.0048f, 0.0044f, 0.016f), mat);
        }

        private static void AddPart(Transform parent, PrimitiveType type, string name, Vector3 lp, Quaternion lr, Vector3 scale, Material mat)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            g.name = name;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = lp;
            g.transform.localRotation = lr;
            g.transform.localScale = scale;
            MeshRenderer mr = g.GetComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Ajusta a instância do pássaro na cena aberta (AudioSource suave). Devolve o log.</summary>
        public static string ApplySceneFix()
        {
            var log = new StringBuilder();
            foreach (ReactiveForestBird bird in Object.FindObjectsByType<ReactiveForestBird>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                bird.ConfigureAudioSource();
                AudioSource a = bird.GetComponent<AudioSource>();
                if (a != null) { EditorUtility.SetDirty(a); PrefabUtility.RecordPrefabInstancePropertyModifications(a); }
                log.AppendLine($"pássaro '{bird.name}': AudioSource loop={a != null && a.loop} volume={(a != null ? a.volume : -1f)} maxDistance={(a != null ? a.maxDistance : -1f)}; pernas={(bird.transform.Find("Leg_Left") != null && bird.transform.Find("Leg_Right") != null)}");
            }
            return log.ToString();
        }

        /// <summary>
        /// Teste de áudio (tempo simulado, 90 passos por segundo): conta os pios tocados com o paciente perto do pássaro.
        /// Roda na cena aberta e não salva nada.
        /// </summary>
        public static string TestAudio()
        {
            var log = new StringBuilder();
            log.AppendLine($"=== Teste de áudio do pássaro ({System.DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");
            ReactiveForestBird bird = FindBird();
            if (bird == null) { log.AppendLine("ERRO: pássaro não encontrado"); return log.ToString(); }

            bird.ConfigureAudioSource();
            var so = new SerializedObject(bird);
            int perches = so.FindProperty("perchPositions").arraySize;
            float trigger = so.FindProperty("proximityTriggerDist").floatValue;
            float cooldown = so.FindProperty("chirpCooldown").floatValue;
            log.AppendLine($"poleiros: {perches}; distância de reação: {trigger} m; cooldown do pio: {cooldown} s");

            int pass = 0, fail = 0;
            const float dt = 1f / 90f;
            Vector3 near = bird.transform.position + Vector3.right * 1.5f;
            Vector3 far = bird.transform.position + Vector3.right * (trigger * 2f + 2f);
            float t = 100f;

            // --- Cenário 1: último poleiro (onde gritava a cada frame), paciente parado perto por 60 s ---
            so.FindProperty("currentWaypointIndex").intValue = perches - 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            int frames = 0;
            for (float e = 0f; e < 60f; e += dt, t += dt, frames++) bird.StepPerched(t, near);
            Report(log, ref pass, ref fail, "1) último poleiro, paciente parado a 1,5 m por 60 s", bird.ChirpCount == 1,
                $"{bird.ChirpCount} pio(s) em {frames} frames (esperado 1; a versão antiga tocava 1 por frame = {frames})");

            // --- Cenário 2: laser passando por cima 200 vezes em seguida ---
            MethodInfo hover = typeof(ReactiveForestBird).GetMethod("OnXRIHoverEntered", BindingFlags.Instance | BindingFlags.NonPublic);
            int before = bird.ChirpCount;
            for (int i = 0; i < 200; i++, t += dt) { hover.Invoke(bird, new object[] { new HoverEnterEventArgs() }); bird.StepPerched(t, near); }
            Report(log, ref pass, ref fail, "2) 200 hovers do laser seguidos (mesmo encontro, depois do cooldown)", bird.ChirpCount == before, $"{bird.ChirpCount - before} pio(s) extras (esperado 0: um pio por encontro)");

            // --- Cenário 3: paciente se afasta e volta depois do cooldown -> um novo pio ---
            before = bird.ChirpCount;
            for (float e = 0f; e < 5f; e += dt, t += dt) bird.StepPerched(t, far);
            bool rearmed = !bird.HasChirpedThisEncounter;
            for (float e = 0f; e < 2f; e += dt, t += dt) bird.StepPerched(t, near);
            Report(log, ref pass, ref fail, "3) afasta (rearma) e volta depois do cooldown, fica 2 s", bird.ChirpCount - before == 1 && rearmed,
                $"{bird.ChirpCount - before} pio(s) (esperado 1), rearmou ao se afastar={rearmed}");

            // --- Cenário 4: vai e volta rápido várias vezes dentro do cooldown -> nenhum pio ---
            before = bird.ChirpCount;
            for (int k = 0; k < 4; k++)
            {
                for (float e = 0f; e < 1f; e += dt, t += dt) bird.StepPerched(t, far);
                for (float e = 0f; e < 1f; e += dt, t += dt) bird.StepPerched(t, near);
            }
            Report(log, ref pass, ref fail, "4) 4 idas e voltas em 8 s (dentro do cooldown)", bird.ChirpCount == before, $"{bird.ChirpCount - before} pio(s) (esperado 0)");

            // --- Cenário 5: intervalo mínimo entre pios em 5 minutos de vai e vem ---
            before = bird.ChirpCount;
            float start = t, lastChange = -1f, minGap = float.MaxValue;
            int count = before;
            while (t - start < 300f)
            {
                Vector3 p = Mathf.Repeat(t - start, 6f) < 3f ? near : far;
                bird.StepPerched(t, p);
                if (bird.ChirpCount != count)
                {
                    if (lastChange >= 0f) minGap = Mathf.Min(minGap, t - lastChange);
                    lastChange = t;
                    count = bird.ChirpCount;
                }
                t += dt;
            }
            int total = bird.ChirpCount - before;
            Report(log, ref pass, ref fail, "5) 5 minutos de vai e vem a cada 3 s", total <= 21 && (minGap == float.MaxValue || minGap >= cooldown - 0.02f),
                $"{total} pios em 300 s (máximo possível com cooldown de {cooldown} s: 20), menor intervalo entre pios {(minGap == float.MaxValue ? 0f : minGap):0.0} s");

            // --- Cenário 6: poleiro inicial -> reage uma vez (pio + decolagem) e não repete ---
            try
            {
                for (float e = 0f; e < 20f; e += dt, t += dt) bird.StepPerched(t, far);
                so.Update();
                so.FindProperty("currentWaypointIndex").intValue = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
                before = bird.ChirpCount;
                for (float e = 0f; e < 10f; e += dt, t += dt) bird.StepPerched(t, near);
                Report(log, ref pass, ref fail, "6) poleiro inicial, paciente chega perto e fica 10 s", bird.ChirpCount - before == 1 && bird.State != ReactiveForestBird.BirdState.Perched,
                    $"{bird.ChirpCount - before} pio(s) (esperado 1), estado depois = {bird.State} (decolou; o voo em si só roda em Play)");
            }
            catch (System.Exception ex)
            {
                fail++;
                log.AppendLine("[FALHOU] 6) exceção: " + ex.Message);
            }

            AudioSource a = bird.GetComponent<AudioSource>();
            if (a != null)
                Report(log, ref pass, ref fail, "7) AudioSource", !a.loop && !a.playOnAwake && a.volume <= 0.18f && a.spatialBlend == 1f && a.maxDistance <= 10f,
                    $"loop={a.loop} playOnAwake={a.playOnAwake} volume={a.volume} spatialBlend={a.spatialBlend} maxDistance={a.maxDistance}");

            log.AppendLine($"--- RESULTADO: {pass} passaram, {fail} falharam");
            return log.ToString();
        }

        private static void Report(StringBuilder log, ref int pass, ref int fail, string name, bool ok, string detail)
        {
            if (ok) pass++; else fail++;
            log.AppendLine($"[{(ok ? "PASSOU" : "FALHOU")}] {name}: {detail}");
        }
    }
}
