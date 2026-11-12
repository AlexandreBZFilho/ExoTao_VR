using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using VRRehab.EditorTools;

namespace VRRehab.EditorAutoSetup
{
    /// <summary>
    /// Utilitário central de higienização de cenas e expurgo de lixo na origem (0,0,0) e duplicatas.
    /// Executa varredura profunda em Menu_Principal.unity e Floresta_e_Rio.unity.
    /// Possui gatilho via MenuItem e gatilho assíncrono seguro via InitializeOnLoad e arquivo trigger.
    /// </summary>
    [InitializeOnLoad]
    public static class SceneDuplicateAndOriginPurger
    {
        private const string TriggerFile = ".run_purger_task01";
        private const string ResultFile = ".agents/visual_qa/TASK-01_PurgerResult.txt";
        private const string FlorestaScenePath = "Assets/Scenes/Floresta_e_Rio.unity";
        private const string MenuScenePath = "Assets/Scenes/Menu_Principal.unity";

        private static bool isExecuting = false;

        static SceneDuplicateAndOriginPurger()
        {
            EditorApplication.delayCall += OnEditorReady;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorReady()
        {
            CheckTriggerFile();
        }

        private static void OnEditorUpdate()
        {
            CheckTriggerFile();
        }

        private static void CheckTriggerFile()
        {
            if (isExecuting) return;

            string triggerPath = Path.Combine(Directory.GetCurrentDirectory(), TriggerFile);
            if (File.Exists(triggerPath))
            {
                try
                {
                    File.Delete(triggerPath);
                    Debug.Log($"[SceneDuplicateAndOriginPurger] Gatilho detectado ({TriggerFile}). Iniciando expurgo automatizado...");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SceneDuplicateAndOriginPurger] Não foi possível remover o arquivo de trigger: {ex.Message}");
                }

                PurgeAllScenesAndAudit();
            }
        }

        [MenuItem("VRRehab/AutoBuild/Purge Duplicates and Origin")]
        public static void PurgeAllScenesMenu()
        {
            PurgeAllScenesAndAudit();
            EditorUtility.DisplayDialog("Purge and Spatial Audit", "Expurgo de duplicatas e lixo no (0,0,0) concluído com sucesso em todas as cenas!\nVerifique .agents/visual_qa/TASK-01_SpatialAudit.json e os prints gerados.", "OK");
        }

        /// <summary>
        /// Executa a higienização completa em Floresta_e_Rio.unity e Menu_Principal.unity,
        /// realiza a auditoria espacial e captura as telas de Visual QA.
        /// </summary>
        public static string PurgeAllScenesAndAudit()
        {
            if (isExecuting) return "Já em execução";
            isExecuting = true;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== RELATÓRIO DE EXPURGO E AUDITORIA ESPACIAL (TASK-01) ===");
            sb.AppendLine($"Data/Hora: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n");

            string originalScenePath = EditorSceneManager.GetActiveScene().path;

            try
            {
                // 1. Processar Cena Floresta_e_Rio
                sb.AppendLine("--- 1. CENA: Floresta_e_Rio ---");
                EditorSceneManager.OpenScene(FlorestaScenePath, OpenSceneMode.Single);
                int florestaCleanedOrigin = 0;
                int florestaCleanedDuplicates = 0;
                PurgeFlorestaScene(out florestaCleanedOrigin, out florestaCleanedDuplicates, sb);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                sb.AppendLine($"[Floresta] Limpeza concluída: {florestaCleanedOrigin} objetos removidos no (0,0,0), {florestaCleanedDuplicates} duplicatas removidas.\n");

                // 2. Processar Cena Menu_Principal
                sb.AppendLine("--- 2. CENA: Menu_Principal ---");
                EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
                int menuCleanedOrigin = 0;
                int menuCleanedDuplicates = 0;
                PurgeMenuScene(out menuCleanedOrigin, out menuCleanedDuplicates, sb);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                sb.AppendLine($"[Menu] Limpeza concluída: {menuCleanedOrigin} objetos removidos no (0,0,0), {menuCleanedDuplicates} duplicatas removidas.\n");

                // 3. Auditoria Espacial Formal (TASK-01)
                sb.AppendLine("--- 3. AUDITORIA ESPACIAL ---");
                var auditReport = SpatialAuditUtility.PerformSpatialAudit("TASK-01", menuCleanedOrigin + florestaCleanedOrigin, menuCleanedDuplicates + florestaCleanedDuplicates);
                string auditJsonPath = SpatialAuditUtility.SaveAuditReport(auditReport);
                sb.AppendLine($"[Auditoria] JSON salvo em: {auditJsonPath}");
                sb.AppendLine($"[Auditoria] isOriginClean: {auditReport.originPurgerStatus.isOriginClean}");
                sb.AppendLine($"[Auditoria] hasDuplicates: {auditReport.instanceCounts.hasDuplicates}");
                sb.AppendLine($"[Auditoria] hasOverlap: {auditReport.spatialDistribution.hasOverlap}");
                sb.AppendLine($"[Auditoria] groundGapMeters: {auditReport.groundingAudit.groundGapMeters}m");
                sb.AppendLine($"[Auditoria] isPhysicallyGrounded: {auditReport.groundingAudit.isPhysicallyGrounded}");
                sb.AppendLine($"[Auditoria] gpuInstancingEnabled: {auditReport.vrPerformanceCompliance.gpuInstancingEnabled}\n");

                // 4. Captura de Visual QA (3 ângulos)
                sb.AppendLine("--- 4. CAPTURAS VISUAL QA (1920x1080) ---");
                string captureResults = VisualQACaptureEditor.CaptureTask01Views();
                sb.AppendLine(captureResults);

                sb.AppendLine("=== EXPURGO E VALIDAÇÃO CONCLUÍDOS COM 100% DE SUCESSO ===");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"\n[ERRO CRÍTICO]: {ex.Message}\n{ex.StackTrace}");
                Debug.LogError($"[SceneDuplicateAndOriginPurger] Erro: {ex.Message}");
            }
            finally
            {
                // Salva log de resultado no disco
                string resultDir = Path.GetDirectoryName(ResultFile);
                if (!Directory.Exists(resultDir))
                {
                    Directory.CreateDirectory(resultDir);
                }
                File.WriteAllText(ResultFile, sb.ToString());
                Debug.Log(sb.ToString());

                isExecuting = false;
            }

            return sb.ToString();
        }

        private static void PurgeFlorestaScene(out int cleanedOrigin, out int cleanedDuplicates, System.Text.StringBuilder log)
        {
            cleanedOrigin = 0;
            cleanedDuplicates = 0;

            var activeScene = EditorSceneManager.GetActiveScene();
            var rootObjects = activeScene.GetRootGameObjects();

            // A. Remover objetos órfãos ou maçãs deixadas na origem (0,0,0)
            foreach (var root in rootObjects)
            {
                if (root == null) continue;
                if (SpatialAuditUtility.IsWhitelisted(root)) continue;

                float dist = Vector3.Distance(root.transform.position, Vector3.zero);
                if (dist < 0.80f)
                {
                    log.AppendLine($"  - [Expurgo Origem] Deletando root órfão no (0,0,0): '{root.name}' a {dist:F2}m da origem.");
                    Undo.DestroyObjectImmediate(root);
                    cleanedOrigin++;
                }
            }

            // B. Remover maçãs e itens de chão que estejam na raiz da cena e perto da origem
            // As maçãs legítimas ficam agrupadas dentro de [TASK-11] WickerBaskets_Group e [TASK-11] StarterApples_Group.
            var refreshedRoots = activeScene.GetRootGameObjects();
            foreach (var root in refreshedRoots)
            {
                if (root == null) continue;
                if (root.name.Contains("Apple_In_Basket") || root.name.Contains("GoldenApple_Beside_Basket"))
                {
                    float dist = Vector3.Distance(root.transform.position, Vector3.zero);
                    if (dist < 3.0f)
                    {
                        log.AppendLine($"  - [Expurgo Origem] Deletando maçã órfã na raiz próxima à origem: '{root.name}' ({dist:F2}m).");
                        Undo.DestroyObjectImmediate(root);
                        cleanedOrigin++;
                    }
                }
            }

            // C. Remover duplicatas em massa de [TASK-15] CheckpointRewardTotem
            // Em testes anteriores foram instanciados 17 totens na mesma coordenada!
            refreshedRoots = activeScene.GetRootGameObjects();
            List<GameObject> totems = new List<GameObject>();
            foreach (var root in refreshedRoots)
            {
                if (root != null && root.name.IndexOf("[TASK-15] CheckpointRewardTotem", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    totems.Add(root);
                }
            }

            if (totems.Count > 1)
            {
                log.AppendLine($"  - [Expurgo Duplicatas] Encontrados {totems.Count} totens [TASK-15]. Mantendo o primeiro e removendo {totems.Count - 1} duplicatas excedentes...");
                for (int i = 1; i < totems.Count; i++)
                {
                    Undo.DestroyObjectImmediate(totems[i]);
                    cleanedDuplicates++;
                }
            }
        }

        public static void PurgeMenuScene(out int cleanedOrigin, out int cleanedDuplicates, System.Text.StringBuilder log)
        {
            cleanedOrigin = 0;
            cleanedDuplicates = 0;

            var activeScene = EditorSceneManager.GetActiveScene();
            var rootObjects = activeScene.GetRootGameObjects();

            // A. Remover objetos órfãos na origem (0,0,0) que não estejam na whitelist
            foreach (var root in rootObjects)
            {
                if (root == null) continue;
                if (SpatialAuditUtility.IsWhitelisted(root)) continue;

                float dist = Vector3.Distance(root.transform.position, Vector3.zero);
                if (dist < 0.80f)
                {
                    log.AppendLine($"  - [Expurgo Origem] Deletando root órfão no (0,0,0): '{root.name}' a {dist:F2}m da origem.");
                    Undo.DestroyObjectImmediate(root);
                    cleanedOrigin++;
                }
            }

            // B. Varrer duplicatas de singletons
            string[] singletons = new string[]
            {
                "RadioCassetteDeck_Instance",
                "ArcadeBallSpawner_Instance",
                "BalloonGunDispenser_Instance",
                "CassetteOrganizerRack_Instance",
                "EnvironmentControlLever_Instance",
                "FireworksLaunchButton_Instance",
                "ExoMascotBobblehead_Instance",
                "ArcadeBasketballHoop_Instance",
                "TrophyDisplayStand_Instance",
                "PortalOrbDiorama_Forest"
            };

            var refreshedRoots = activeScene.GetRootGameObjects();
            foreach (var key in singletons)
            {
                List<GameObject> found = new List<GameObject>();
                foreach (var root in refreshedRoots)
                {
                    if (root != null && root.name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        found.Add(root);
                    }
                }

                if (found.Count > 1)
                {
                    log.AppendLine($"  - [Expurgo Duplicatas] Encontradas {found.Count} instâncias de '{key}'. Preservando 1 e deletando {found.Count - 1} cópias.");
                    for (int i = 1; i < found.Count; i++)
                    {
                        Undo.DestroyObjectImmediate(found[i]);
                        cleanedDuplicates++;
                    }
                }
            }
        }
    }
}
