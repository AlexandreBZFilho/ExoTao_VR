using System.IO;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRRehab.Interactive.Task15;

namespace VRRehab.EditorTools
{
    /// <summary>
    /// Recria o painel "Objetivos da Sessão" (missões da TASK-15) no holograma do drone da Floresta_e_Rio.
    /// O painel sumiu quando o drone foi reconstruído (commit 79802f7) e o SessionQuestManager ficou sem UI.
    /// Usa o próprio construtor da TASK-15 (mesmo layout aprovado antes) e só mexe no Canvas_Holograma_Clinico do drone.
    /// Batch: unity run . -- -executeMethod VRRehab.EditorTools.ForestQuestPanelRestore.ApplyBatch
    /// </summary>
    public static class ForestQuestPanelRestore
    {
        private const string DroneName = "[IC] Drone_Guia_IA_ExoBot";
        private const string Report = ".agents/visual_qa/FOREST_QuestPanel_Restore.txt";

        [MenuItem("ExoTao IC/Floresta/Recriar painel de missões no drone")]
        public static void ApplyFromMenu() => Debug.Log(Apply());

        public static void ApplyBatch()
        {
            int code = 1;
            try
            {
                string r = Apply();
                Debug.Log(r);
                code = r.Contains("ERRO") ? 1 : 0;
            }
            catch (System.Exception e) { Debug.LogException(e); }
            EditorApplication.Exit(code);
        }

        public static string Apply()
        {
            var sb = new StringBuilder();
            Scene scene = EditorSceneManager.OpenScene(ForestAuditTool.ForestScene, OpenSceneMode.Single);
            GameObject drone = GameObject.Find(DroneName);
            if (drone == null) return "ERRO: drone não encontrado";
            var quest = drone.GetComponent<SessionQuestManager>();
            if (quest == null) return "ERRO: SessionQuestManager não está no drone";
            Transform canvas = drone.transform.Find("Canvas_Holograma_Clinico");
            if (canvas == null) return "ERRO: Canvas_Holograma_Clinico não encontrado no drone";

            // As texturas do painel estavam importadas como "Multiple": LoadAssetAtPath<Sprite> devolvia null, a barra
            // "Filled" ficava sem sprite e aparecia sempre cheia (ignorando fillAmount). Passa para sprite único.
            foreach (string tex in new[] { "Assets/Textures/Generated/Task15/UI_ProgressBar_White.png", "Assets/Textures/Generated/Task15/UI_QuestPanel_Bg.png" })
            {
                if (AssetImporter.GetAtPath(tex) is TextureImporter ti && (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single))
                {
                    ti.textureType = TextureImporterType.Sprite;
                    ti.spriteImportMode = SpriteImportMode.Single;
                    ti.SaveAndReimport();
                    sb.AppendLine($"Textura {Path.GetFileName(tex)} reimportada como sprite único");
                }
                sb.AppendLine($"  sprite {Path.GetFileName(tex)}: {(AssetDatabase.LoadAssetAtPath<Sprite>(tex) != null ? "ok" : "FALTANDO")}");
            }

            var builder = System.Type.GetType("VRRehab.EditorAutoSetup.Task15_SceneAndPrefabBuilder, Assembly-CSharp-Editor");
            MethodInfo setup = builder?.GetMethod("SetupDroneQuestHologramPanel", BindingFlags.NonPublic | BindingFlags.Static);
            if (setup == null) return "ERRO: SetupDroneQuestHologramPanel não encontrado no construtor da TASK-15";

            setup.Invoke(null, new object[] { drone.transform, quest });
            Transform panel = canvas.Find("Painel_Missoes_Floresta");
            if (panel == null) return "ERRO: painel não foi criado";

            // Holograma do drone é só leitura (sem raycasters): nenhum gráfico do painel precisa receber raio
            int graphics = 0;
            foreach (Graphic g in panel.GetComponentsInChildren<Graphic>(true)) { g.raycastTarget = false; graphics++; }

            // Valores iniciais coerentes (o construtor deixa 0.35 de preview na barra)
            foreach (Image img in panel.GetComponentsInChildren<Image>(true))
                if (img.type == Image.Type.Filled) img.fillAmount = 0f;
            quest.RefreshAllUI();

            EditorUtility.SetDirty(quest);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var rt = (RectTransform)panel;
            Vector3[] c = new Vector3[4];
            rt.GetWorldCorners(c);
            sb.AppendLine($"Painel recriado: {ForestAuditTool.PathOf(panel)} ({graphics} gráficos, raycastTarget desligado)");
            sb.AppendLine($"Tamanho no mundo: {(c[3] - c[0]).magnitude:0.00} m x {(c[1] - c[0]).magnitude:0.00} m");
            foreach (string row in new[] { "Row_Quest_1", "Row_Quest_2", "Row_Quest_3" })
            {
                Transform r = panel.Find(row);
                sb.AppendLine($"  {row}: {r?.Find("Txt_Title")?.GetComponent<TMP_Text>()?.text} | {r?.Find("Txt_Status")?.GetComponent<TMP_Text>()?.text}");
            }

            // Foto offscreen: holograma inteiro do drone (painel principal + aba de missões), de frente
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text t in canvas.GetComponentsInChildren<TMP_Text>(true)) t.ForceMeshUpdate();
            var canvasRt = (RectTransform)canvas;
            canvasRt.GetWorldCorners(c);
            Vector3 min = c[0], max = c[2];
            rt.GetWorldCorners(c);
            Bounds all = new Bounds((min + max) * 0.5f, Vector3.zero);
            all.Encapsulate(min); all.Encapsulate(max); all.Encapsulate(c[0]); all.Encapsulate(c[2]);
            float span = all.size.magnitude;
            Vector3 camPos = all.center - canvas.forward * span * 1.15f;
            sb.AppendLine("Foto: " + ForestAuditTool.RenderTo(".agents/visual_qa/FOREST_quest_panel_drone.png", camPos, all.center, 45f, 1600, 900));

            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, sb.ToString());
            return sb.ToString();
        }
    }
}
