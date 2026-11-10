using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace VRRehab.EditorTools
{
    /// <summary>
    /// Utilitário de automação para Captura Visual de Alta Fidelidade (Visual QA).
    /// Renderiza imagens de alta resolução (1920x1080) com enquadramento preciso de Renderer.bounds,
    /// inspeção de cenas e salvamento em .agents/visual_qa/ para auditoria automatizada.
    /// </summary>
    public static class VisualQACaptureEditor
    {
        private const string OutputDirectory = ".agents/visual_qa";

        [MenuItem("VRRehab/VisualQA/Capture All Test Views")]
        public static void CaptureAllTestViewsMenu()
        {
            string results = CaptureAllTestViews();
            Debug.Log($"[VisualQA] Captura concluída:\n{results}");
            EditorUtility.DisplayDialog("Visual QA Capture", "Capturas concluídas com sucesso!\n" + results, "OK");
        }

        /// <summary>
        /// Executa o lote completo de capturas de teste do Menu e da Floresta.
        /// Retorna resumo em texto dos arquivos gerados.
        /// </summary>
        public static string CaptureAllTestViews()
        {
            string baseDir = Path.GetFullPath(OutputDirectory);
            if (!Directory.Exists(baseDir))
            {
                Directory.CreateDirectory(baseDir);
            }

            var log = new System.Text.StringBuilder();
            string originalScenePath = EditorSceneManager.GetActiveScene().path;

            try
            {
                // 1. Garante que a cena do Menu Principal está aberta
                if (!originalScenePath.EndsWith("Menu_Principal.unity"))
                {
                    EditorSceneManager.OpenScene("Assets/Scenes/Menu_Principal.unity", OpenSceneMode.Single);
                }

                // Captura 1: Menu Overview (perspectiva ampla da sala do menu a partir da posição do jogador VR)
                string pathMenuOverview = Path.Combine(baseDir, "TASK-01_Menu_Overview.png");
                Vector3 menuCamPos = new Vector3(4.50f, 1.65f, 1.50f);
                Vector3 menuCamLookAt = new Vector3(4.50f, 1.40f, 5.50f);
                CaptureCameraView(pathMenuOverview, menuCamPos, menuCamLookAt, 1920, 1080);
                log.AppendLine($"Capturado: {pathMenuOverview}");

                // Captura 2: Menu Desk Zone (focado na mesa do rádio e workstation interativa)
                string pathDeskZone = Path.Combine(baseDir, "TASK-01_Menu_DeskZone.png");
                Vector3 deskCamPos = new Vector3(2.20f, 1.45f, 0.40f);
                Vector3 deskCamLookAt = new Vector3(0.66f, 0.95f, 0.36f);
                CaptureCameraView(pathDeskZone, deskCamPos, deskCamLookAt, 1920, 1080);
                log.AppendLine($"Capturado: {pathDeskZone}");

                // 2. Salva e abre a cena da Floresta_e_Rio para captura
                EditorSceneManager.SaveOpenScenes();
                EditorSceneManager.OpenScene("Assets/Scenes/Floresta_e_Rio.unity", OpenSceneMode.Single);

                // Captura 3: Floresta Overview (enquadrando trilha e rio)
                string pathFloresta = Path.Combine(baseDir, "TASK-01_Floresta_Overview.png");
                Camera florestaCam = Camera.main;
                Vector3 florestaPos = florestaCam != null ? florestaCam.transform.position : new Vector3(0f, 2.5f, -5f);
                Vector3 florestaLookAt = florestaCam != null ? (florestaPos + florestaCam.transform.forward * 10f) : new Vector3(0f, 1.5f, 5f);

                CaptureCameraView(pathFloresta, florestaPos, florestaLookAt, 1920, 1080);
                log.AppendLine($"Capturado: {pathFloresta}");
            }
            finally
            {
                // Retorna à cena original
                if (!string.IsNullOrEmpty(originalScenePath) && EditorSceneManager.GetActiveScene().path != originalScenePath)
                {
                    EditorSceneManager.SaveOpenScenes();
                    EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
                }
            }

            return log.ToString();
        }

        public static string CaptureFlorestaOverviewOnly()
        {
            string baseDir = Path.GetFullPath(OutputDirectory);
            if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);

            string originalScene = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.SaveOpenScenes();
            EditorSceneManager.OpenScene("Assets/Scenes/Floresta_e_Rio.unity", OpenSceneMode.Single);

            string pathFloresta = Path.Combine(baseDir, "TASK-01_Floresta_Overview.png");
            Camera florestaCam = Camera.main;
            Vector3 florestaPos = florestaCam != null ? florestaCam.transform.position : new Vector3(0f, 2.5f, -5f);
            Vector3 florestaLookAt = florestaCam != null ? (florestaPos + florestaCam.transform.forward * 10f) : new Vector3(0f, 1.5f, 5f);

            CaptureCameraView(pathFloresta, florestaPos, florestaLookAt, 1920, 1080);

            if (!string.IsNullOrEmpty(originalScene))
            {
                EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
            }

            return pathFloresta;
        }

        /// <summary>
        /// Captura uma vista posicional precisa usando uma câmera temporária isolada.
        /// </summary>
        public static bool CaptureCameraView(string outputPath, Vector3 cameraPos, Vector3 lookAtTarget, int width = 1920, int height = 1080)
        {
            GameObject tempCamGo = null;
            GameObject tempLightGo = null;
            RenderTexture rt = null;

            try
            {
                tempCamGo = new GameObject("__VisualQA_Temp_Camera__");
                Camera cam = tempCamGo.AddComponent<Camera>();
                cam.transform.position = cameraPos;
                cam.transform.LookAt(lookAtTarget);
                cam.fieldOfView = 60f;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 1000f;
                cam.clearFlags = CameraClearFlags.Skybox;

                // Câmera adicional de iluminação para áreas de sombra / interiores
                tempLightGo = new GameObject("__VisualQA_Temp_Light__");
                Light fillLight = tempLightGo.AddComponent<Light>();
                fillLight.type = LightType.Directional;
                fillLight.intensity = 0.5f;
                fillLight.color = Color.white;
                tempLightGo.transform.position = cameraPos;
                tempLightGo.transform.rotation = cam.transform.rotation;

                rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 8,
                    useMipMap = false
                };

                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                Texture2D screenShot = new Texture2D(width, height, TextureFormat.RGB24, false);
                screenShot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                screenShot.Apply();
                RenderTexture.active = null;

                byte[] bytes = screenShot.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(screenShot);

                string dir = Path.GetDirectoryName(outputPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllBytes(outputPath, bytes);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VisualQA] Erro ao capturar view em {outputPath}: {ex.Message}");
                return false;
            }
            finally
            {
                if (rt != null)
                {
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);
                }
                if (tempCamGo != null)
                {
                    UnityEngine.Object.DestroyImmediate(tempCamGo);
                }
                if (tempLightGo != null)
                {
                    UnityEngine.Object.DestroyImmediate(tempLightGo);
                }
            }
        }

        /// <summary>
        /// Captura automática de um GameObject enquadrando exatamente seus bounds (Renderer.bounds).
        /// </summary>
        public static bool CaptureObjectBounds(string outputPath, GameObject targetObject, Vector3 directionOffset, float padding = 1.25f, int width = 1920, int height = 1080)
        {
            if (targetObject == null) return false;

            Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Vector3 fallbackPos = targetObject.transform.position + directionOffset.normalized * 2.0f;
                return CaptureCameraView(outputPath, fallbackPos, targetObject.transform.position, width, height);
            }

            Bounds combinedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combinedBounds.Encapsulate(renderers[i].bounds);
            }

            float maxExtent = combinedBounds.extents.magnitude;
            float fov = 60f;
            float distance = (maxExtent / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad)) * padding;
            Vector3 camPos = combinedBounds.center + directionOffset.normalized * distance;

            return CaptureCameraView(outputPath, camPos, combinedBounds.center, width, height);
        }
    }
}
