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

        [MenuItem("VRRehab/VisualQA/Capture TASK-01 Views")]
        public static void CaptureTask01ViewsMenu()
        {
            string results = CaptureTask01Views();
            Debug.Log($"[VisualQA] Capturas da TASK-01 concluídas:\n{results}");
            EditorUtility.DisplayDialog("Visual QA Capture - TASK-01", "Capturas da TASK-01 concluídas!\n" + results, "OK");
        }

        /// <summary>
        /// Captura os 3 ângulos canônicos da TASK-01 conforme especificado em TASK-01_Spec.md:
        /// View 1: Frontal Ampla
        /// View 2: Lateral Grounding & Zero Lixo na Origem
        /// View 3: Closeup Macro dos Interactables
        /// </summary>
        public static string CaptureTask01Views()
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
                if (!originalScenePath.EndsWith("Menu_Principal.unity"))
                {
                    EditorSceneManager.OpenScene("Assets/Scenes/Menu_Principal.unity", OpenSceneMode.Single);
                }

                // 1. View 1: Frontal Ampla da Estação Limpa
                string pathView1 = Path.Combine(baseDir, "TASK-01_View1_Front.png");
                Vector3 view1CamPos = new Vector3(2.30f, 1.50f, 0.40f);
                Vector3 view1CamLookAt = new Vector3(0.66f, 0.95f, 0.05f);
                CaptureCameraView(pathView1, view1CamPos, view1CamLookAt, 1920, 1080);
                log.AppendLine($"Capturado View 1: {pathView1}");

                // 2. View 2: Lateral Rente ao Solo/Mesa (Zero Flutuação & Zero Lixo no (0,0,0))
                string pathView2 = Path.Combine(baseDir, "TASK-01_View2_Side_Grounding.png");
                Vector3 view2CamPos = new Vector3(0.68f, 1.05f, -1.05f);
                Vector3 view2CamLookAt = new Vector3(0.66f, 0.95f, 0.25f);
                CaptureCameraView(pathView2, view2CamPos, view2CamLookAt, 1920, 1080);
                log.AppendLine($"Capturado View 2: {pathView2}");

                // 3. View 3: Closeup Macro dos Interactables e Affordances (Shader Instanciado)
                string pathView3 = Path.Combine(baseDir, "TASK-01_View3_Closeup.png");
                Vector3 view3CamPos = new Vector3(1.10f, 1.15f, 0.15f);
                Vector3 view3CamLookAt = new Vector3(0.67f, 0.96f, 0.14f);
                CaptureCameraView(pathView3, view3CamPos, view3CamLookAt, 1920, 1080);
                log.AppendLine($"Capturado View 3: {pathView3}");
            }
            finally
            {
                if (!string.IsNullOrEmpty(originalScenePath) && EditorSceneManager.GetActiveScene().path != originalScenePath)
                {
                    EditorSceneManager.SaveOpenScenes();
                    EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
                }
            }

            return log.ToString();
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
        /// Renderiza offscreen (Camera.Render → RenderTexture) e devolve a imagem como Texture2D (o chamador destrói).
        /// Não usa a janela do Editor nem captura de desktop.
        /// </summary>
        public static Texture2D RenderCameraToTexture(Vector3 cameraPos, Vector3 lookAtTarget, int width, int height, float fov = 60f, float nearClip = 0.02f)
        {
            GameObject camGo = null, lightGo = null;
            RenderTexture rt = null;
            try
            {
                camGo = new GameObject("__VisualQA_Temp_Camera__");
                var cam = camGo.AddComponent<Camera>();
                cam.transform.position = cameraPos;
                cam.transform.LookAt(lookAtTarget);
                cam.fieldOfView = fov;
                cam.nearClipPlane = nearClip;
                cam.farClipPlane = 200f;
                cam.clearFlags = CameraClearFlags.Skybox;

                lightGo = new GameObject("__VisualQA_Temp_Light__");
                var fill = lightGo.AddComponent<Light>();
                fill.type = LightType.Directional;
                fill.intensity = 0.5f;
                fill.shadows = LightShadows.None;
                lightGo.transform.rotation = cam.transform.rotation;

                rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8, useMipMap = false };
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                return tex;
            }
            finally
            {
                if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                if (camGo != null) UnityEngine.Object.DestroyImmediate(camGo);
                if (lightGo != null) UnityEngine.Object.DestroyImmediate(lightGo);
            }
        }

        /// <summary>Captura offscreen com FOV configurável.</summary>
        public static bool CaptureCameraView(string outputPath, Vector3 cameraPos, Vector3 lookAtTarget, int width, int height, float fov)
        {
            Texture2D tex = null;
            try
            {
                tex = RenderCameraToTexture(cameraPos, lookAtTarget, width, height, fov);
                string dir = Path.GetDirectoryName(outputPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(outputPath, tex.EncodeToPNG());
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VisualQA] Erro ao capturar view em {outputPath}: {ex.Message}");
                return false;
            }
            finally
            {
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        /// <summary>
        /// Captura uma vista posicional precisa usando uma câmera temporária isolada.
        /// </summary>
        public static bool CaptureCameraView(string outputPath, Vector3 cameraPos, Vector3 lookAtTarget, int width = 1920, int height = 1080)
        {
            GameObject tempCamGo = null;
            GameObject tempLightGo = null;
            Camera cam = null;
            RenderTexture rt = null;

            try
            {
                tempCamGo = new GameObject("__VisualQA_Temp_Camera__");
                cam = tempCamGo.AddComponent<Camera>();
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
                if (cam != null)
                {
                    cam.targetTexture = null;
                }
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
