using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRRehab.Interactive.Task08;

namespace VRRehab.EditorTools
{
    public static partial class BasketballArcadeFixer
    {
        // Geometria do aro no espaço local da cesta (mesmos valores do modelo/builder)
        private static readonly Vector3 RimCenter = new Vector3(0f, -0.13f, -0.3425f);
        private const float RimRadius = 0.19f;

        // Painel do placar: cobre o visor pequeno do modelo (46 x 13 cm), que não comportava título + 3 colunas
        private const float PanelW = 0.74f, PanelH = 0.32f;
        private static readonly Vector3 PanelCenter = new Vector3(0f, 0.445f, -0.105f);

        [MenuItem("ExoTao IC/Basquete/Corrigir Cesta e Placar")]
        public static void ApplyFromMenu() => EditorUtility.DisplayDialog("Basquete", Apply(), "OK");

        /// <summary>Corrige o prefab da cesta (sensores + placar) e a instância da cena do Menu.</summary>
        public static string Apply()
        {
            var log = new StringBuilder();
            log.AppendLine($"=== Correção da cesta ({System.DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");

            // ---- 1. Prefab ----
            GameObject root = PrefabUtility.LoadPrefabContents(HoopPrefabPath);
            try
            {
                ConfigureHoopHierarchy(root, log);
                PrefabUtility.SaveAsPrefabAsset(root, HoopPrefabPath);
                log.AppendLine("prefab salvo");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            // ---- 2. Instância na cena do Menu ----
            Scene scene = SceneManager.GetSceneByPath(MenuScene);
            bool wasOpen = scene.IsValid() && scene.isLoaded;
            bool wasDirty = wasOpen && scene.isDirty;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);

            BasketballHoopScorer hoop = FindHoop(scene);
            if (hoop == null) { log.AppendLine("ERRO: cesta não encontrada na cena do Menu"); return log.ToString(); }

            GameObject instRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(hoop.gameObject);
            if (instRoot != null)
            {
                PropertyModification[] mods = PrefabUtility.GetPropertyModifications(instRoot);
                int nonDefault = 0;
                if (mods != null)
                    foreach (PropertyModification m in mods)
                    {
                        if (m.target == null || PrefabUtility.IsDefaultOverride(m)) continue;
                        nonDefault++;
                        if (nonDefault <= 12) log.AppendLine($"  override revertido: {m.target.name}.{m.propertyPath} = {m.value}");
                    }
                // Mantém posição/rotação/nome da instância (overrides padrão); o resto volta a seguir o prefab corrigido
                PrefabUtility.RevertPrefabInstance(instRoot, InteractionMode.AutomatedAction);
                log.AppendLine($"instância '{instRoot.name}' sincronizada com o prefab ({nonDefault} overrides revertidos)");
            }
            else
            {
                ConfigureHoopHierarchy(hoop.gameObject, log);
                log.AppendLine("cesta da cena não é instância de prefab: corrigida diretamente");
            }

            var missing = new List<string>();
            int missingCount = ForestAuditTool.CountMissingScripts(scene, missing);
            log.AppendLine($"Missing Script na cena do Menu depois da correção: {missingCount}");
            foreach (string m in missing) log.AppendLine("  " + m);

            hoop = FindHoop(scene);
            foreach (HoopSensorForwarder f in hoop.GetComponentsInChildren<HoopSensorForwarder>(true))
            {
                BoxCollider bc = f.GetComponent<BoxCollider>();
                log.AppendLine($"sensor {f.name}: {f.sensorType}, scorer ligado={(f.scorer == hoop)}, trigger={bc.isTrigger}, tamanho {ForestAuditTool.V(bc.size)}, pos local {ForestAuditTool.V(f.transform.localPosition)}");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!wasOpen || !wasDirty) { EditorSceneManager.SaveScene(scene); log.AppendLine("cena do Menu salva"); }
            else log.AppendLine("cena aberta com alterações suas pendentes: SALVE com Ctrl+S");

            string report = log.ToString();
            File.WriteAllText(".agents/visual_qa/BASKET_Apply.txt", report);
            return report;
        }

        /// <summary>
        /// Deixa a hierarquia de uma cesta (raiz com BasketballHoopScorer) correta: sensores com HoopSensorForwarder,
        /// volumes robustos e placar novo. Idempotente; usada também pelo builder da TASK-08.
        /// </summary>
        public static void ConfigureHoopHierarchy(GameObject root, StringBuilder log = null)
        {
            BasketballHoopScorer scorer = root.GetComponent<BasketballHoopScorer>();
            if (scorer == null) throw new System.InvalidOperationException("raiz sem BasketballHoopScorer");

            int removed = 0;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            log?.AppendLine($"{removed} componentes Missing Script removidos");

            // ---- Sensores ----
            // Cima: caixa logo acima do plano do aro. Baixo: caixa na saída da rede.
            // Espessura + diâmetro da bola (0.18) >> deslocamento por passo de física de uma bola a 10 m/s (0.20).
            BoxCollider top = SetupSensor(root.transform, "Sensor_TriggerTop", HoopSensorForwarder.SensorType.Top, scorer,
                new Vector3(RimCenter.x, RimCenter.y + 0.08f, RimCenter.z), new Vector3(0.34f, 0.12f, 0.34f));
            BoxCollider bottom = SetupSensor(root.transform, "Sensor_TriggerBottom", HoopSensorForwarder.SensorType.Bottom, scorer,
                new Vector3(RimCenter.x, RimCenter.y - 0.22f, RimCenter.z), new Vector3(0.24f, 0.14f, 0.24f));

            foreach (Light l in root.GetComponentsInChildren<Light>(true)) l.shadows = LightShadows.None;

            // ---- Aro: cápsulas em vez de caixas ----
            // O topo plano das caixas deixava a bola parada equilibrada em cima do aro (jogada travada).
            // Cápsula = tubo redondo como um aro de verdade: a bola sempre rola para dentro ou para fora.
            int rimCapsules = 0;
            foreach (Transform seg in root.GetComponentsInChildren<Transform>(true))
            {
                if (seg.name != "Hoop_Rim_Segment") continue;
                foreach (BoxCollider b in seg.GetComponents<BoxCollider>()) Object.DestroyImmediate(b);
                CapsuleCollider cap = seg.GetComponent<CapsuleCollider>();
                if (cap == null) cap = seg.gameObject.AddComponent<CapsuleCollider>();
                cap.isTrigger = false;
                // O X local de cada segmento aponta para FORA do centro (raio de roda: a ponta interna invadia
                // ~7 cm a boca do aro). O Z local é a tangente da circunferência: o tubo segue o anel.
                cap.direction = 2;
                cap.radius = 0.0175f;
                cap.height = 0.165f;        // corda de 45° = 0.145 m + sobreposição: anel contínuo, sem frestas
                cap.center = Vector3.zero;
                rimCapsules++;
            }
            log?.AppendLine($"aro: {rimCapsules} segmentos com CapsuleCollider (antes BoxCollider de topo plano)");

            // ---- Placar ----
            Transform old = root.transform.Find("Scoreboard_Canvas_3D");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Transform oldPanel = root.transform.Find("Scoreboard_Panel");
            if (oldPanel != null) Object.DestroyImmediate(oldPanel.gameObject);

            MeshRenderer visual = root.transform.Find("Visual_Model") != null ? root.transform.Find("Visual_Model").GetComponent<MeshRenderer>() : null;
            Material matPlate = FindMaterial(visual, "DisplayGlass") ?? FindMaterial(visual, "Backboard");
            Material matNeon = FindMaterial(visual, "NeonTrim");

            var panel = new GameObject("Scoreboard_Panel");
            panel.transform.SetParent(root.transform, false);
            panel.transform.localPosition = PanelCenter;

            // Moldura neon (atrás) + placa escura
            MakeQuad(panel.transform, "Panel_NeonFrame", new Vector3(0f, 0f, 0.002f), new Vector2(PanelW + 0.024f, PanelH + 0.024f), matNeon);
            MakeQuad(panel.transform, "Panel_Backplate", Vector3.zero, new Vector2(PanelW, PanelH), matPlate);

            const float zLine = -0.002f, zText = -0.004f;
            // Linhas divisórias: título | valores | mensagem, e entre as 3 colunas
            MakeQuad(panel.transform, "Divider_Title", new Vector3(0f, 0.078f, zLine), new Vector2(PanelW - 0.06f, 0.004f), matNeon);
            MakeQuad(panel.transform, "Divider_Status", new Vector3(0f, -0.100f, zLine), new Vector2(PanelW - 0.06f, 0.004f), matNeon);
            MakeQuad(panel.transform, "Divider_Col_L", new Vector3(-0.1225f, -0.011f, zLine), new Vector2(0.004f, 0.150f), matNeon);
            MakeQuad(panel.transform, "Divider_Col_R", new Vector3(0.1225f, -0.011f, zLine), new Vector2(0.004f, 0.150f), matNeon);

            // Faixa do título
            MakeText(panel.transform, "Text_Title", "ARCADE HOOP", new Vector3(0f, 0.118f, zText), new Vector2(PanelW - 0.06f, 0.06f), 0.52f,
                new Color(0.0f, 0.90f, 1.0f), FontStyles.Bold);

            // 3 colunas: SEQUÊNCIA | PONTOS | RECORDE
            const float colX = 0.245f, valueY = 0.012f, labelY = -0.072f;
            var valueRect = new Vector2(0.215f, 0.12f);
            var labelRect = new Vector2(0.225f, 0.04f);
            TextMeshPro streak = MakeText(panel.transform, "Text_Streak_Value", "x0", new Vector3(-colX, valueY, zText), valueRect, 1.25f, new Color(0.25f, 1.0f, 0.5f), FontStyles.Bold);
            TextMeshPro score = MakeText(panel.transform, "Text_Score_Value", "00", new Vector3(0f, valueY, zText), valueRect, 1.35f, new Color(1.0f, 0.90f, 0.15f), FontStyles.Bold);
            TextMeshPro high = MakeText(panel.transform, "Text_HighScore_Value", "00", new Vector3(colX, valueY, zText), valueRect, 1.25f, new Color(1.0f, 0.55f, 0.15f), FontStyles.Bold);
            Color labelColor = new Color(0.82f, 0.86f, 0.92f);
            MakeText(panel.transform, "Text_Streak_Label", "SEQUÊNCIA", new Vector3(-colX, labelY, zText), labelRect, 0.32f, labelColor, FontStyles.Normal);
            MakeText(panel.transform, "Text_Score_Label", "PONTOS", new Vector3(0f, labelY, zText), labelRect, 0.32f, labelColor, FontStyles.Normal);
            MakeText(panel.transform, "Text_High_Label", "RECORDE", new Vector3(colX, labelY, zText), labelRect, 0.32f, labelColor, FontStyles.Normal);

            // Faixa de mensagem (não cobre os números)
            TextMeshPro notice = MakeText(panel.transform, "Text_Status_Notice", "ARREMESSE!", new Vector3(0f, -0.130f, zText), new Vector2(PanelW - 0.06f, 0.05f), 0.44f, Color.white, FontStyles.Bold);

            // ---- Referências e ajustes do scorer ----
            var so = new SerializedObject(scorer);
            so.FindProperty("topTriggerCollider").objectReferenceValue = top;
            so.FindProperty("bottomTriggerCollider").objectReferenceValue = bottom;
            so.FindProperty("scoreText").objectReferenceValue = score;
            so.FindProperty("streakText").objectReferenceValue = streak;
            so.FindProperty("highScoreText").objectReferenceValue = high;
            so.FindProperty("statusNoticeText").objectReferenceValue = notice;
            so.FindProperty("rimCenterLocal").vector3Value = RimCenter;
            so.FindProperty("rimRadius").floatValue = RimRadius;
            so.FindProperty("maxTransitSeconds").floatValue = 2.0f;
            so.FindProperty("sfxVolume").floatValue = 0.4f;
            so.ApplyModifiedPropertiesWithoutUndo();
            log?.AppendLine("sensores, placar e referências do scorer configurados");
        }

        private static BoxCollider SetupSensor(Transform root, string name, HoopSensorForwarder.SensorType type, BasketballHoopScorer scorer, Vector3 localPos, Vector3 size)
        {
            Transform t = root.Find(name);
            if (t == null)
            {
                t = new GameObject(name).transform;
                t.SetParent(root, false);
            }
            t.localPosition = localPos;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            BoxCollider bc = t.GetComponent<BoxCollider>();
            if (bc == null) bc = t.gameObject.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.center = Vector3.zero;
            bc.size = size;

            HoopSensorForwarder f = t.GetComponent<HoopSensorForwarder>();
            if (f == null) f = t.gameObject.AddComponent<HoopSensorForwarder>();
            f.sensorType = type;
            f.scorer = scorer;
            EditorUtility.SetDirty(f);
            return bc;
        }

        private static Material FindMaterial(MeshRenderer r, string nameContains)
        {
            if (r == null) return null;
            foreach (Material m in r.sharedMaterials)
                if (m != null && m.name.Contains(nameContains)) return m;
            return null;
        }

        private static void MakeQuad(Transform parent, string name, Vector3 localPos, Vector2 size, Material mat)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.localPosition = localPos;
            q.transform.localScale = new Vector3(size.x, size.y, 1f);
            MeshRenderer mr = q.GetComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private static TextMeshPro MakeText(Transform parent, string name, string text, Vector3 localPos, Vector2 rect, float fontSize, Color color, FontStyles style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            TextMeshPro t = go.AddComponent<TextMeshPro>();
            t.rectTransform.sizeDelta = rect;
            t.text = text;
            t.alignment = TextAlignmentOptions.Center;
            t.color = color;
            t.fontStyle = style;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            // Auto-size: o texto encolhe para caber no retângulo da sua célula (nunca invade a vizinha)
            t.enableAutoSizing = true;
            t.fontSizeMax = fontSize;
            t.fontSizeMin = fontSize * 0.35f;
            t.fontSize = fontSize;
            t.raycastTarget = false;
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return t;
        }

        // =====================================================================
        // TESTE DE PONTUAÇÃO (simulação de física no editor, sem salvar nada)
        // =====================================================================

        private class Sim
        {
            public BasketballHoopScorer hoop;
            public HoopSensorForwarder[] sensors;
            public bool[] inside;
            public float time;
            public bool sawRim, sawOther;
            public readonly HashSet<Collider> touching = new HashSet<Collider>();
            public readonly List<Collider> scratch = new List<Collider>();
        }

        private static readonly MethodInfo ForwarderTriggerEnter =
            typeof(HoopSensorForwarder).GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// Roda os cenários de arremesso com Physics.Simulate e grava .agents/visual_qa/BASKET_ScoreTest.txt.
        /// No editor a Unity não entrega OnTriggerEnter/OnCollisionEnter a scripts comuns, então o teste detecta a
        /// sobreposição bola x sensor e os contatos a cada passo e chama os MESMOS métodos que o jogo chama
        /// (HoopSensorForwarder.OnTriggerEnter e BasketballBallAudio.HandleImpact), usando as referências salvas na cena.
        /// </summary>
        public static string TestScoring()
        {
            Scene scene = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
            BasketballHoopScorer hoop = FindHoop(scene);
            var log = new StringBuilder();
            log.AppendLine($"=== Teste de pontuação da cesta ({System.DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");
            log.AppendLine("Método: Physics.Simulate (passo 0.02 s) na cena Menu_Principal, gatilhos/contatos emulados por sobreposição a cada passo.");
            if (hoop == null) { log.AppendLine("ERRO: cesta não encontrada"); return log.ToString(); }

            const string key = "VRRehab_Basketball_HighScore";
            bool hadKey = PlayerPrefs.HasKey(key);
            int savedHigh = PlayerPrefs.GetInt(key, 0);

            GameObject ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BallPrefabPath);
            SimulationMode prevMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            var sim = new Sim { hoop = hoop, sensors = hoop.GetComponentsInChildren<HoopSensorForwarder>(true) };
            sim.inside = new bool[sim.sensors.Length];
            BasketballHoopScorer.ClockOverride = () => sim.time;
            int pass = 0, fail = 0;

            try
            {
                hoop.EnsureInitialized();
                hoop.ResetGame();
                RefreshTexts(hoop);
                Capture(hoop, ".agents/visual_qa/BASKET_scoreboard_1_inicial.png", log);

                Vector3 rim = hoop.RimCenterWorld;
                Vector3 front = -hoop.transform.forward;            // lado de quem arremessa
                Vector3 side = hoop.transform.right;

                // (a) cesta limpa
                var r = Shoot(sim, ballPrefab, rim + Vector3.up * 0.5f, Vector3.zero, 3f);
                Check(log, ref pass, ref fail, "a) bola caindo limpa pelo aro", r.scoreDelta == 3 && hoop.CurrentStreak == 1 && !r.sawRim,
                    $"pontos +{r.scoreDelta} (esperado +3), sequência {hoop.CurrentStreak}, tocou aro={r.sawRim}, mensagem \"{hoop.NoticeText}\"");
                RefreshTexts(hoop);
                Capture(hoop, ".agents/visual_qa/BASKET_scoreboard_2_swish.png", log);

                // (b) bate no aro e entra: procura um deslocamento lateral que toque o aro e ainda caia dentro
                bool foundRimIn = false;
                string rimInfo = "nenhum deslocamento testado tocou o aro e entrou";
                foreach (float off in new[] { 0.095f, 0.10f, 0.105f, 0.11f, 0.115f, 0.12f, 0.09f, 0.125f, 0.13f })
                {
                    int before = hoop.CurrentScore;
                    var rb = Shoot(sim, ballPrefab, rim + Vector3.up * 0.45f + front * off, Vector3.zero, 3f);
                    if (rb.sawRim && rb.scoreDelta > 0)
                    {
                        foundRimIn = rb.scoreDelta == 2;
                        rimInfo = $"deslocamento {off:0.000} m: tocou o aro e entrou, pontos +{rb.scoreDelta} (esperado +2), mensagem \"{hoop.NoticeText}\"";
                        break;
                    }
                }
                Check(log, ref pass, ref fail, "b) bate no aro e entra", foundRimIn, rimInfo);

                // (c) erra: bate no aro e cai fora -> sem pontos, sequência zera, flag limpa
                int streakBefore = hoop.CurrentStreak;
                var miss = Shoot(sim, ballPrefab, rim + Vector3.up * 0.45f + front * 0.25f, Vector3.zero, 4f);
                Check(log, ref pass, ref fail, "c1) bate no aro e erra", miss.scoreDelta == 0 && miss.sawRim && hoop.CurrentStreak == 0 && !miss.flagAfter && !miss.inFlightAfter,
                    $"pontos +{miss.scoreDelta} (esperado 0), tocou aro={miss.sawRim}, sequência {streakBefore} -> {hoop.CurrentStreak} (esperado 0), flag do aro depois={miss.flagAfter} (esperado False); tocou em [{miss.touched}], parou em {ForestAuditTool.V(miss.finalPos)}");
                var afterMiss = Shoot(sim, ballPrefab, rim + Vector3.up * 0.5f, Vector3.zero, 3f, reuseLast: true);
                Check(log, ref pass, ref fail, "c2) mesma bola, próxima cesta limpa depois do erro", afterMiss.scoreDelta == 3,
                    $"pontos +{afterMiss.scoreDelta} (esperado +3: a flag do aro do arremesso anterior não vaza)");
                var farMiss = Shoot(sim, ballPrefab, rim + Vector3.up * 0.4f + front * 0.8f, Vector3.zero, 3f);
                Check(log, ref pass, ref fail, "c3) arremesso longe da cesta", farMiss.scoreDelta == 0 && hoop.CurrentStreak == 0,
                    $"pontos +{farMiss.scoreDelta} (esperado 0), sequência {hoop.CurrentStreak} (esperado 0)");

                // (d) arremesso rápido
                var fast = Shoot(sim, ballPrefab, rim + Vector3.up * 1.0f, Vector3.down * 8f, 3f);
                Check(log, ref pass, ref fail, "d1) bola rápida, 8 m/s na vertical (quica no chão e volta pelo aro: só 1 cesta por arremesso)", fast.scoreDelta == 3, $"pontos +{fast.scoreDelta} (esperado +3), velocidade máx {fast.maxSpeed:0.0} m/s");
                var fast2 = Shoot(sim, ballPrefab, rim + Vector3.up * 1.2f, Vector3.down * 11f, 3f);
                Check(log, ref pass, ref fail, "d2) bola muito rápida, 11 m/s", fast2.scoreDelta == 3, $"pontos +{fast2.scoreDelta} (esperado +3), velocidade máx {fast2.maxSpeed:0.0} m/s");
                // arremesso em arco vindo da frente (parábola que cai no aro)
                Vector3 start = rim + front * 2.0f + Vector3.down * 0.3f;
                float ballDrag = ballPrefab.GetComponent<Rigidbody>().linearDamping;
                var arc = Shoot(sim, ballPrefab, start, BallisticVelocity(start, rim + Vector3.up * 0.05f, 0.75f, ballDrag), 4f);
                Check(log, ref pass, ref fail, "d3) arremesso em arco a 2 m de distância", arc.scoreDelta > 0, $"pontos +{arc.scoreDelta} (esperado +3 ou +2), tocou aro={arc.sawRim}, velocidade máx {arc.maxSpeed:0.0} m/s");

                // (e) bola subindo pela rede não pontua (sai de baixo, sobe pelo aro e cai fora)
                int scoreBeforeUp = hoop.CurrentScore;
                var up = Shoot(sim, ballPrefab, rim + Vector3.down * 0.6f - side * 0.03f, Vector3.up * 6f + side * 0.6f, 3.5f);
                Check(log, ref pass, ref fail, "e1) bola subindo pela rede", up.scoreDelta == 0, $"pontos +{up.scoreDelta} (esperado 0), altura máx acima do aro {up.maxHeightAboveRim:0.00} m");
                var sideways = Shoot(sim, ballPrefab, rim + Vector3.down * 0.18f - side * 0.6f, side * 3.5f + Vector3.up * 0.4f, 2.5f);
                Check(log, ref pass, ref fail, "e2) bola cruzando a rede de lado, por baixo do aro", sideways.scoreDelta == 0, $"pontos +{sideways.scoreDelta} (esperado 0)");

                // (f) duas cestas seguidas
                hoop.ResetGame();
                var f1 = Shoot(sim, ballPrefab, rim + Vector3.up * 0.5f, Vector3.zero, 3f);
                var f2 = Shoot(sim, ballPrefab, rim + Vector3.up * 0.6f, Vector3.zero, 3f);
                var f3 = Shoot(sim, ballPrefab, rim + Vector3.up * 0.55f, Vector3.zero, 3f);
                Check(log, ref pass, ref fail, "f) três cestas seguidas somam e a sequência sobe", hoop.CurrentScore == 9 && hoop.CurrentStreak == 3 && hoop.NoticeText.Contains("x3"),
                    $"pontos {hoop.CurrentScore} (esperado 09), sequência {hoop.CurrentStreak} (esperado 3), mensagem \"{hoop.NoticeText}\"");
                RefreshTexts(hoop);
                Capture(hoop, ".agents/visual_qa/BASKET_scoreboard_3_sequencia.png", log);

                // ResetGame mantém o recorde
                int highBefore = hoop.HighScore;
                hoop.ResetGame();
                Check(log, ref pass, ref fail, "g) ResetGame zera pontos e sequência e mantém o recorde", hoop.CurrentScore == 0 && hoop.CurrentStreak == 0 && hoop.HighScore == highBefore,
                    $"pontos {hoop.CurrentScore}, sequência {hoop.CurrentStreak}, recorde {hoop.HighScore}");

                // Sobreposição dos textos do placar (bounds de cada TMP no plano do painel)
                log.AppendLine(CheckTextOverlap(hoop, ref pass, ref fail));
            }
            finally
            {
                BasketballHoopScorer.ClockOverride = null;
                Physics.simulationMode = prevMode;
                if (hadKey) PlayerPrefs.SetInt(key, savedHigh); else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }

            log.AppendLine($"--- RESULTADO: {pass} passaram, {fail} falharam");
            string report = log.ToString();
            File.WriteAllText(".agents/visual_qa/BASKET_ScoreTest.txt", report);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); // descarta a simulação
            return report;
        }

        private struct ShotResult
        {
            public int scoreDelta;
            public bool sawRim, flagAfter, inFlightAfter;
            public float maxSpeed, maxHeightAboveRim;
            public string touched;
            public Vector3 finalPos;
        }

        private static GameObject lastBall;

        private static ShotResult Shoot(Sim sim, GameObject ballPrefab, Vector3 pos, Vector3 velocity, float seconds, bool reuseLast = false)
        {
            GameObject ballGo = reuseLast && lastBall != null ? lastBall : (GameObject)PrefabUtility.InstantiatePrefab(ballPrefab, sim.hoop.gameObject.scene);
            if (!reuseLast && lastBall != null) Object.DestroyImmediate(lastBall);
            lastBall = ballGo;

            BasketballBallAudio ball = ballGo.GetComponent<BasketballBallAudio>();
            Rigidbody rb = ballGo.GetComponent<Rigidbody>();
            SphereCollider sphere = ballGo.GetComponent<SphereCollider>();
            ballGo.transform.SetPositionAndRotation(pos, Quaternion.identity);
            rb.isKinematic = false;
            rb.useGravity = true;
            Physics.SyncTransforms();
            rb.linearVelocity = velocity;
            rb.angularVelocity = Vector3.zero;

            ball.BeginShot(); // equivale a soltar a bola (selectExited)
            sim.touching.Clear();
            sim.sawRim = false;
            for (int i = 0; i < sim.inside.Length; i++) sim.inside[i] = false;

            int scoreBefore = sim.hoop.CurrentScore;
            var res = new ShotResult();
            var touchedNames = new List<string>();
            float radius = sphere.radius * ballGo.transform.lossyScale.x;
            Vector3 rim = sim.hoop.RimCenterWorld;

            const float step = 0.02f;
            for (float t = 0f; t < seconds; t += step)
            {
                Physics.Simulate(step);
                sim.time += step;
                Vector3 c = ballGo.transform.TransformPoint(sphere.center);
                res.maxSpeed = Mathf.Max(res.maxSpeed, rb.linearVelocity.magnitude);
                res.maxHeightAboveRim = Mathf.Max(res.maxHeightAboveRim, c.y - rim.y);

                // contatos (equivalente ao OnCollisionEnter)
                sim.scratch.Clear();
                foreach (Collider col in Physics.OverlapSphere(c, radius + 0.012f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (col.transform == ballGo.transform || col.transform.IsChildOf(ballGo.transform)) continue;
                    sim.scratch.Add(col);
                    if (sim.touching.Add(col))
                    {
                        if (!touchedNames.Contains(col.name) && touchedNames.Count < 6) touchedNames.Add(col.name);
                        if (col.GetComponentInParent<BasketballHoopScorer>() != null && col.name.Contains("Rim")) { sim.sawRim = true; res.sawRim = true; }
                        ball.HandleImpact(col, rb.linearVelocity.magnitude);
                    }
                }
                sim.touching.RemoveWhere(x => !sim.scratch.Contains(x));

                // sensores (equivalente ao OnTriggerEnter)
                for (int i = 0; i < sim.sensors.Length; i++)
                {
                    BoxCollider box = sim.sensors[i].GetComponent<BoxCollider>();
                    Vector3 local = box.transform.InverseTransformPoint(c) - box.center;
                    Vector3 half = box.size * 0.5f;
                    Vector3 clamped = new Vector3(Mathf.Clamp(local.x, -half.x, half.x), Mathf.Clamp(local.y, -half.y, half.y), Mathf.Clamp(local.z, -half.z, half.z));
                    bool overlap = (local - clamped).sqrMagnitude <= radius * radius;
                    if (overlap && !sim.inside[i]) ForwarderTriggerEnter.Invoke(sim.sensors[i], new object[] { sphere });
                    sim.inside[i] = overlap;
                }
            }

            res.scoreDelta = sim.hoop.CurrentScore - scoreBefore;
            res.touched = string.Join(", ", touchedNames);
            res.finalPos = ballGo.transform.position;
            res.flagAfter = ball.hasTouchedRimThisShot;
            res.inFlightAfter = ball.ShotInFlight;
            return res;
        }

        /// <summary>Velocidade inicial para sair de 'from' e chegar em 'to' em 'time' segundos sob gravidade.</summary>
        private static Vector3 BallisticVelocity(Vector3 from, Vector3 to, float time, float drag = 0f)
        {
            Vector3 d = to - from;
            float g = -Physics.gravity.y;
            if (drag < 1e-4f)
                return new Vector3(d.x / time, d.y / time + 0.5f * g * time, d.z / time);

            // Arrasto linear (Rigidbody.linearDamping): x(t) = v0/k (1 - e^-kt);  y(t) = (v0y + g/k)/k (1 - e^-kt) - g t / k
            float k = drag, f = (1f - Mathf.Exp(-k * time)) / k;
            return new Vector3(d.x / f, (d.y + g * time / k) / f - g / k, d.z / f);
        }

        private static void Check(StringBuilder log, ref int pass, ref int fail, string name, bool ok, string detail)
        {
            if (ok) pass++; else fail++;
            log.AppendLine($"[{(ok ? "PASSOU" : "FALHOU")}] {name}: {detail}");
        }

        private static void RefreshTexts(BasketballHoopScorer hoop)
        {
            foreach (TMP_Text t in hoop.GetComponentsInChildren<TMP_Text>(true)) t.ForceMeshUpdate(true, true);
        }

        private static void Capture(BasketballHoopScorer hoop, string path, StringBuilder log)
        {
            Transform t = hoop.transform;
            Vector3 panel = t.TransformPoint(PanelCenter);
            Vector3 front = -t.forward;
            log.AppendLine("captura: " + ForestAuditTool.RenderTo(path, panel + front * 2.0f, panel, 26f, 1600, 900));
        }

        /// <summary>Confere que nenhum texto do placar invade o retângulo de outro nem sai da placa.</summary>
        private static string CheckTextOverlap(BasketballHoopScorer hoop, ref int pass, ref int fail)
        {
            Transform panel = hoop.transform.Find("Scoreboard_Panel");
            var sb = new StringBuilder();
            if (panel == null) { fail++; return "[FALHOU] placar: Scoreboard_Panel não existe"; }

            // Pior caso de largura: números e mensagem longos
            var samples = new Dictionary<string, string>
            {
                { "Text_Score_Value", "188" }, { "Text_Streak_Value", "x24" }, { "Text_HighScore_Value", "999" },
                { "Text_Status_Notice", "SWISH! +3  x24" }
            };
            var rects = new List<(string name, Rect r, float size)>();
            foreach (TMP_Text t in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                string original = t.text;
                if (samples.TryGetValue(t.name, out string s)) t.text = s;
                t.ForceMeshUpdate(true, true);
                Bounds b = t.bounds; // local ao texto
                Vector3 c = panel.InverseTransformPoint(t.transform.TransformPoint(b.center));
                rects.Add((t.name, new Rect(c.x - b.extents.x, c.y - b.extents.y, b.size.x, b.size.y), t.fontSize));
                sb.AppendLine($"   {t.name} \"{t.text}\": {b.size.x * 100f:0.0} x {b.size.y * 100f:0.0} cm, fonte {t.fontSize:0.00}");
                t.text = original;
                t.ForceMeshUpdate(true, true);
            }

            int overlaps = 0, outside = 0;
            var plate = new Rect(-PanelW * 0.5f, -PanelH * 0.5f, PanelW, PanelH);
            for (int i = 0; i < rects.Count; i++)
            {
                Rect a = rects[i].r;
                if (a.xMin < plate.xMin || a.xMax > plate.xMax || a.yMin < plate.yMin || a.yMax > plate.yMax) { outside++; sb.AppendLine($"   FORA DA PLACA: {rects[i].name}"); }
                for (int j = i + 1; j < rects.Count; j++)
                    if (a.Overlaps(rects[j].r)) { overlaps++; sb.AppendLine($"   SOBREPOSIÇÃO: {rects[i].name} x {rects[j].name}"); }
            }
            bool ok = overlaps == 0 && outside == 0;
            if (ok) pass++; else fail++;
            return $"[{(ok ? "PASSOU" : "FALHOU")}] placar sem sobreposição com textos no pior caso: {overlaps} sobreposições, {outside} fora da placa\n{sb}";
        }
    }
}
