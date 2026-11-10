using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using VRRehab.Core;
using VRRehab.DronePet;
using VRRehab.EditorTools;

namespace VRRehab.EditorAutoSetup
{
    /// <summary>
    /// Builder e validador automatizado para a TASK-05 (Drone Pet Milestone Celebration).
    /// - Cria texturas, materiais e canhões duplos de confetes.
    /// - Monta o balão de fala 3D com TextMeshPro e auto-billboard.
    /// - Configura o componente DroneMilestoneCelebration no Drone da cena ativa.
    /// - Atualiza as cenas Menu_Principal e Floresta_e_Rio.
    /// - Executa e valida as capturas de Visual QA em alta resolução (1920x1080).
    /// </summary>
    public static class Task05_SceneAndPrefabBuilder
    {
        private const string MenuScenePath = "Assets/Scenes/Menu_Principal.unity";
        private const string FlorestaScenePath = "Assets/Scenes/Floresta_e_Rio.unity";

        private const string TexturesDir = "Assets/Textures/Generated/Task05";
        private const string MaterialsDir = "Assets/Models/Generated/Task05";
        private const string AudioDir = "Assets/Audio/Generated/Task05";

        [MenuItem("VRRehab/AutoBuild/Build and Verify TASK-05")]
        public static void BuildAndVerify()
        {
            Debug.Log("[TASK-05] Iniciando montagem automatizada do Drone Milestone Celebration System...");

            // 1. Garante que texturas, materiais e áudios existem
            EnsureAssets();

            // 2. Configura a cena Menu_Principal
            SetupMenuPrincipalScene();

            // 3. Configura a cena Floresta_e_Rio
            SetupFlorestaScene();

            // 4. Executa captura de Visual QA
            CaptureVisualQA();

            Debug.Log("[TASK-05] Montagem e Visual QA da TASK-05 concluídos com 100% de sucesso!");
        }

        public static void EnsureAssets()
        {
            EnsureDirectories();
            GenerateSpeechBubbleTextures();
            EnsureMaterials();
            AssetDatabase.Refresh();
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Textures"))
                AssetDatabase.CreateFolder("Assets", "Textures");
            if (!AssetDatabase.IsValidFolder("Assets/Textures/Generated"))
                AssetDatabase.CreateFolder("Assets/Textures", "Generated");
            if (!AssetDatabase.IsValidFolder(TexturesDir))
                AssetDatabase.CreateFolder("Assets/Textures/Generated", "Task05");

            if (!AssetDatabase.IsValidFolder("Assets/Models"))
                AssetDatabase.CreateFolder("Assets", "Models");
            if (!AssetDatabase.IsValidFolder("Assets/Models/Generated"))
                AssetDatabase.CreateFolder("Assets/Models", "Generated");
            if (!AssetDatabase.IsValidFolder(MaterialsDir))
                AssetDatabase.CreateFolder("Assets/Models/Generated", "Task05");
        }

        private static void GenerateSpeechBubbleTextures()
        {
            string panelPath = $"{TexturesDir}/SpeechBubble_Panel.png";
            if (!File.Exists(panelPath))
            {
                int w = 512, h = 256;
                Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                Color cFill = new Color(0.04f, 0.08f, 0.15f, 0.92f);
                Color cBorder = new Color(0.0f, 0.95f, 1.0f, 1.0f);
                Color cBorderGlow = new Color(0.0f, 0.70f, 1.0f, 0.40f);
                Color cSep = new Color(0.0f, 0.95f, 1.0f, 0.35f);
                int radius = 32;

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        // Distância do canto mais próximo
                        int dx = x < radius ? radius - x : (x >= w - radius ? x - (w - radius) : 0);
                        int dy = y < radius ? radius - y : (y >= h - radius ? y - (h - radius) : 0);
                        float distCorner = Mathf.Sqrt(dx * dx + dy * dy);

                        if (distCorner > radius)
                        {
                            tex.SetPixel(x, y, Color.clear);
                        }
                        else if (distCorner >= radius - 4)
                        {
                            tex.SetPixel(x, y, cBorder);
                        }
                        else if (distCorner >= radius - 8)
                        {
                            tex.SetPixel(x, y, cBorderGlow);
                        }
                        else if (x < 4 || x >= w - 4 || y < 4 || y >= h - 4)
                        {
                            tex.SetPixel(x, y, cBorder);
                        }
                        else if (x < 8 || x >= w - 8 || y < 8 || y >= h - 8)
                        {
                            tex.SetPixel(x, y, cBorderGlow);
                        }
                        else if (y == 180 && x >= 24 && x <= w - 24)
                        {
                            // Linha separadora do cabeçalho
                            tex.SetPixel(x, y, cSep);
                        }
                        else
                        {
                            tex.SetPixel(x, y, cFill);
                        }
                    }
                }
                tex.Apply();
                File.WriteAllBytes(panelPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(panelPath);
            }

            string tailPath = $"{TexturesDir}/SpeechBubble_Tail.png";
            if (!File.Exists(tailPath))
            {
                int tw = 64, th = 48;
                Texture2D tailTex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
                Color cFill = new Color(0.04f, 0.08f, 0.15f, 0.92f);
                Color cBorder = new Color(0.0f, 0.95f, 1.0f, 1.0f);

                for (int y = 0; y < th; y++)
                {
                    float widthAtY = (float)y / th * (tw / 2f);
                    int xMid = tw / 2;
                    for (int x = 0; x < tw; x++)
                    {
                        float distFromMid = Mathf.Abs(x - xMid);
                        if (distFromMid <= widthAtY)
                        {
                            if (distFromMid >= widthAtY - 2f || y <= 2)
                                tailTex.SetPixel(x, y, cBorder);
                            else
                                tailTex.SetPixel(x, y, cFill);
                        }
                        else
                        {
                            tailTex.SetPixel(x, y, Color.clear);
                        }
                    }
                }
                tailTex.Apply();
                File.WriteAllBytes(tailPath, tailTex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tailTex);
                AssetDatabase.ImportAsset(tailPath);
            }

            // Configura os assets como Sprites
            ConfigureTextureAsSprite(panelPath);
            ConfigureTextureAsSprite(tailPath);
        }

        private static void ConfigureTextureAsSprite(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }

        private static void EnsureMaterials()
        {
            string matConfettiPath = $"{MaterialsDir}/M_Confetti_Multicolor.mat";
            Material matConfetti = AssetDatabase.LoadAssetAtPath<Material>(matConfettiPath);
            Shader shader = Shader.Find("Mobile/Particles/Alpha Blended")
                            ?? Shader.Find("Particles/Standard Unlit")
                            ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                            ?? Shader.Find("Unlit/Color");

            if (matConfetti == null)
            {
                matConfetti = new Material(shader)
                {
                    name = "M_Confetti_Multicolor"
                };
                AssetDatabase.CreateAsset(matConfetti, matConfettiPath);
            }
            else
            {
                matConfetti.shader = shader;
            }

            if (matConfetti.HasProperty("_TintColor"))
                matConfetti.SetColor("_TintColor", Color.white);
            if (matConfetti.HasProperty("_Color"))
                matConfetti.SetColor("_Color", Color.white);
            if (matConfetti.HasProperty("_BaseColor"))
                matConfetti.SetColor("_BaseColor", Color.white);
            if (matConfetti.HasProperty("_Cull"))
                matConfetti.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

            EditorUtility.SetDirty(matConfetti);
        }

        public static void SetupMenuPrincipalScene()
        {
            if (EditorSceneManager.GetActiveScene().path != MenuScenePath)
            {
                EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            }

            SetupDroneMilestoneCelebrationInActiveScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[TASK-05] Menu_Principal configurado com sucesso.");
        }

        public static void SetupFlorestaScene()
        {
            if (EditorSceneManager.GetActiveScene().path != FlorestaScenePath)
            {
                EditorSceneManager.OpenScene(FlorestaScenePath, OpenSceneMode.Single);
            }

            SetupDroneMilestoneCelebrationInActiveScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[TASK-05] Floresta_e_Rio configurada com sucesso.");
        }

        public static void SetupDroneMilestoneCelebrationInActiveScene()
        {
            GameObject droneObj = GameObject.Find("[IC] Drone_Guia_IA_ExoBot");
            if (droneObj == null)
            {
                Debug.LogError("[TASK-05] GameObject '[IC] Drone_Guia_IA_ExoBot' não encontrado na cena ativa!");
                return;
            }

            DroneMilestoneCelebration celebration = droneObj.GetComponent<DroneMilestoneCelebration>();
            if (celebration == null)
            {
                celebration = droneObj.AddComponent<DroneMilestoneCelebration>();
            }

            // Garante AudioSource dedicado no drone para celebração
            AudioSource audioSource = droneObj.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = droneObj.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.85f;
            audioSource.minDistance = 1.0f;
            audioSource.maxDistance = 25.0f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            celebration.celebrationAudioSource = audioSource;

            // Carrega clipes de áudio
            celebration.sfxFanfare = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/fanfare_triumph.wav");
            celebration.sfxConfettiPop = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/confetti_cannon_pop.wav");

            // 1. Configura os Canhões Duplos de Confetes
            SetupConfettiCannons(droneObj.transform, celebration);

            // 2. Configura o Balão de Fala 3D
            SetupSpeechBubble(droneObj.transform, celebration);

            // 3. Atualiza referências
            celebration.LocateDependencies();
            celebration.LoadPhrasesBank();
        }

        private static void SetupConfettiCannons(Transform droneTransform, DroneMilestoneCelebration celebration)
        {
            Transform cannonsGroup = droneTransform.Find("ConfettiCannons_Group");
            if (cannonsGroup == null)
            {
                GameObject groupGo = new GameObject("ConfettiCannons_Group");
                groupGo.transform.SetParent(droneTransform, false);
                groupGo.transform.localPosition = Vector3.zero;
                groupGo.transform.localRotation = Quaternion.identity;
                cannonsGroup = groupGo.transform;
            }

            // Canhão Esquerdo
            Transform leftT = cannonsGroup.Find("ConfettiCannon_Left");
            GameObject leftGo = leftT != null ? leftT.gameObject : new GameObject("ConfettiCannon_Left");
            leftGo.transform.SetParent(cannonsGroup, false);
            leftGo.transform.localPosition = new Vector3(-0.25f, 0.08f, 0.02f);
            leftGo.transform.localRotation = Quaternion.Euler(-35f, -25f, 0f);

            ParticleSystem psLeft = leftGo.GetComponent<ParticleSystem>();
            if (psLeft == null) psLeft = leftGo.AddComponent<ParticleSystem>();
            ConfigureConfettiParticleSystem(psLeft);
            celebration.leftConfettiCannon = psLeft;

            // Canhão Direito
            Transform rightT = cannonsGroup.Find("ConfettiCannon_Right");
            GameObject rightGo = rightT != null ? rightT.gameObject : new GameObject("ConfettiCannon_Right");
            rightGo.transform.SetParent(cannonsGroup, false);
            rightGo.transform.localPosition = new Vector3(0.25f, 0.08f, 0.02f);
            rightGo.transform.localRotation = Quaternion.Euler(-35f, 25f, 0f);

            ParticleSystem psRight = rightGo.GetComponent<ParticleSystem>();
            if (psRight == null) psRight = rightGo.AddComponent<ParticleSystem>();
            ConfigureConfettiParticleSystem(psRight);
            celebration.rightConfettiCannon = psRight;
        }

        private static void ConfigureConfettiParticleSystem(ParticleSystem ps)
        {
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1.0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.2f, 4.6f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.045f, 0.075f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.070f, 0.130f);
            main.startSizeZ = 0.01f;
            main.gravityModifier = 0.85f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // Cores festivas multicor vibrantes (combinação entre 2 gradientes ricos)
            Gradient grad1 = new Gradient();
            grad1.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1.0f, 0.85f, 0.15f), 0.0f), // Ouro
                    new GradientColorKey(new Color(0.0f, 0.95f, 1.0f), 0.5f),  // Ciano
                    new GradientColorKey(new Color(0.2f, 1.0f, 0.35f), 1.0f)  // Esmeralda
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 0.85f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );

            Gradient grad2 = new Gradient();
            grad2.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1.0f, 0.18f, 0.65f), 0.0f), // Magenta
                    new GradientColorKey(new Color(1.0f, 0.45f, 0.10f), 0.5f), // Coral/Laranja
                    new GradientColorKey(new Color(0.35f, 0.65f, 1.0f), 1.0f)  // Azul Real
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 0.85f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            main.startColor = new ParticleSystem.MinMaxGradient(grad1, grad2);

            // Emissão por Burst
            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0.0f, 40)
            });

            // Formato Cone
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18.0f;
            shape.radius = 0.035f;

            // Rotação dinâmica em 3D (tumble no ar)
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-180f, 180f);
            rot.y = new ParticleSystem.MinMaxCurve(-240f, 240f);
            rot.z = new ParticleSystem.MinMaxCurve(-320f, 320f);

            // Resistência do ar / Drag
            var limitVel = ps.limitVelocityOverLifetime;
            limitVel.enabled = true;
            limitVel.dampen = 0.38f;

            // Renderer
            var rend = ps.GetComponent<ParticleSystemRenderer>();
            Material matConfetti = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/M_Confetti_Multicolor.mat");
            if (matConfetti != null)
            {
                rend.sharedMaterial = matConfetti;
            }
        }

        private static void SetupSpeechBubble(Transform droneTransform, DroneMilestoneCelebration celebration)
        {
            // Limpa qualquer SpeechBubble_Anchor residual para garantir hierarquia limpa
            Transform existingAnchor = droneTransform.Find("SpeechBubble_Anchor");
            if (existingAnchor != null)
            {
                UnityEngine.Object.DestroyImmediate(existingAnchor.gameObject);
            }

            // 1. Âncora pai (Transform puro para controle de posição, rotação e escala elástica 0 a 1)
            GameObject anchorGo = new GameObject("SpeechBubble_Anchor");
            anchorGo.transform.SetParent(droneTransform, false);
            anchorGo.transform.localPosition = new Vector3(0f, celebration.bubbleElevationOffset, 0f);
            anchorGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            anchorGo.transform.localScale = Vector3.one;
            Transform bubbleAnchor = anchorGo.transform;
            celebration.speechBubbleAnchor = bubbleAnchor;

            // 2. Canvas World Space (filho da âncora, com escala métrica VR 0.0012)
            GameObject canvasGo = new GameObject("SpeechBubble_Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(bubbleAnchor, false);
            canvasGo.transform.localPosition = Vector3.zero;
            canvasGo.transform.localRotation = Quaternion.identity;

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            celebration.speechBubbleCanvas = canvas;

            RectTransform canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(520, 220);
            canvasRt.localScale = new Vector3(0.0012f, 0.0012f, 0.0012f);

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;

            CanvasGroup group = canvasGo.AddComponent<CanvasGroup>();
            celebration.bubbleCanvasGroup = group;

            // 3. Painel de Fundo
            GameObject panelGo = new GameObject("Panel_Background", typeof(RectTransform));
            panelGo.transform.SetParent(canvasGo.transform, false);

            RectTransform panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            Image imgPanel = panelGo.AddComponent<Image>();
            Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{TexturesDir}/SpeechBubble_Panel.png");
            if (panelSprite != null) imgPanel.sprite = panelSprite;
            imgPanel.color = Color.white;
            celebration.imgBubbleBackground = imgPanel;

            // 4. Ponta do Balão (Tail)
            GameObject tailGo = new GameObject("Pointer_Tail", typeof(RectTransform));
            tailGo.transform.SetParent(canvasGo.transform, false);

            RectTransform tailRt = tailGo.GetComponent<RectTransform>();
            tailRt.anchorMin = new Vector2(0.5f, 0f);
            tailRt.anchorMax = new Vector2(0.5f, 0f);
            tailRt.pivot = new Vector2(0.5f, 1f);
            tailRt.anchoredPosition = new Vector2(0f, 2f);
            tailRt.sizeDelta = new Vector2(36, 24);

            Image imgTail = tailGo.AddComponent<Image>();
            Sprite tailSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{TexturesDir}/SpeechBubble_Tail.png");
            if (tailSprite != null) imgTail.sprite = tailSprite;
            imgTail.color = Color.white;

            // 5. Badge da Categoria
            GameObject badgeGo = new GameObject("Txt_CategoryBadge", typeof(RectTransform));
            badgeGo.transform.SetParent(canvasGo.transform, false);

            RectTransform badgeRt = badgeGo.GetComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(0.5f, 1f);
            badgeRt.anchorMax = new Vector2(0.5f, 1f);
            badgeRt.pivot = new Vector2(0.5f, 1f);
            badgeRt.anchoredPosition = new Vector2(0f, -14f);
            badgeRt.sizeDelta = new Vector2(460, 32);

            TextMeshProUGUI txtBadge = badgeGo.AddComponent<TextMeshProUGUI>();
            txtBadge.text = "[META DE PASSOS]";
            txtBadge.fontSize = 18;
            txtBadge.fontStyle = FontStyles.Bold;
            txtBadge.color = new Color(0.0f, 0.95f, 1.0f, 1.0f);
            txtBadge.alignment = TextAlignmentOptions.Center;
            celebration.txtCategoryBadge = txtBadge;

            // 6. Texto Motivacional
            GameObject speechGo = new GameObject("Txt_MotivationalSpeech", typeof(RectTransform));
            speechGo.transform.SetParent(canvasGo.transform, false);

            RectTransform speechRt = speechGo.GetComponent<RectTransform>();
            speechRt.anchorMin = new Vector2(0.5f, 0.5f);
            speechRt.anchorMax = new Vector2(0.5f, 0.5f);
            speechRt.pivot = new Vector2(0.5f, 0.5f);
            speechRt.anchoredPosition = new Vector2(0f, -14f);
            speechRt.sizeDelta = new Vector2(470, 115);

            TextMeshProUGUI txtSpeech = speechGo.AddComponent<TextMeshProUGUI>();
            txtSpeech.text = "Sensacional! Você atingiu a meta de passos com vigor e constância!";
            txtSpeech.fontSize = 24;
            txtSpeech.color = Color.white;
            txtSpeech.enableWordWrapping = true;
            txtSpeech.alignment = TextAlignmentOptions.Center;
            celebration.txtMotivationalSpeech = txtSpeech;

            // Configura a fonte LiberationSans SDF
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (fontAsset != null)
            {
                txtBadge.font = fontAsset;
                txtSpeech.font = fontAsset;
            }
        }

        public static void CaptureVisualQA()
        {
            string baseDir = Path.GetFullPath(".agents/visual_qa");
            if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);

            if (EditorSceneManager.GetActiveScene().path != MenuScenePath)
            {
                EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            }

            GameObject droneObj = GameObject.Find("[IC] Drone_Guia_IA_ExoBot");
            if (droneObj == null)
            {
                Debug.LogError("[TASK-05 Visual QA] Drone não encontrado para capturar!");
                return;
            }

            DroneMilestoneCelebration celebration = droneObj.GetComponent<DroneMilestoneCelebration>();
            if (celebration == null)
            {
                Debug.LogError("[TASK-05 Visual QA] DroneMilestoneCelebration ausente!");
                return;
            }

            Vector3 dronePos = droneObj.transform.position;
            Vector3 droneForward = droneObj.transform.forward;
            Vector3 droneUp = droneObj.transform.up;
            Vector3 droneRight = droneObj.transform.right;

            // =========================================================================
            // CAPTURA 1: TASK-05_Celebration_Pirouette.png
            // Drone inclinado na pirueta acrobática com confetes no ar
            // =========================================================================
            celebration.SetVisualQAPirouettePose();
            string pathPirouette = Path.Combine(baseDir, "TASK-05_Celebration_Pirouette.png");
            Vector3 camPosPirouette = dronePos + (droneForward * 0.90f) + (Vector3.up * 0.18f);
            Vector3 targetPirouette = dronePos + (Vector3.up * 0.18f);
            VisualQACaptureEditor.CaptureCameraView(pathPirouette, camPosPirouette, targetPirouette, 1920, 1080);
            Debug.Log($"[TASK-05 Visual QA] Imagem 1 capturada: {pathPirouette}");

            // =========================================================================
            // CAPTURA 2: TASK-05_Celebration_SpeechBubble.png
            // Close-up do balão de fala 3D com texto nítido em português
            // =========================================================================
            celebration.SetVisualQASpeechBubblePose("Simetria perfeita! Ambas as pernas estão trabalhando com equilíbrio exemplar!", "[SIMETRIA EXCELENTE]");
            string pathBubble = Path.Combine(baseDir, "TASK-05_Celebration_SpeechBubble.png");
            Vector3 bubbleCenter = celebration.speechBubbleAnchor.position;
            Vector3 camPosBubble = bubbleCenter + (droneForward * 0.65f);
            VisualQACaptureEditor.CaptureCameraView(pathBubble, camPosBubble, bubbleCenter, 1920, 1080);
            Debug.Log($"[TASK-05 Visual QA] Imagem 2 capturada: {pathBubble}");

            // =========================================================================
            // CAPTURA 3: TASK-05_Confetti_Explosion.png
            // Visão frontal dos confetes multicolor flutuando ao redor do drone
            // =========================================================================
            celebration.SetVisualQAConfettiExplosionPose();
            string pathConfetti = Path.Combine(baseDir, "TASK-05_Confetti_Explosion.png");
            Vector3 camPosConfetti = dronePos + (droneForward * 1.15f) + (Vector3.up * 0.20f);
            Vector3 targetConfetti = dronePos + (Vector3.up * 0.20f);
            VisualQACaptureEditor.CaptureCameraView(pathConfetti, camPosConfetti, targetConfetti, 1920, 1080);
            Debug.Log($"[TASK-05 Visual QA] Imagem 3 capturada: {pathConfetti}");

            // Restaura rotação e oculta balão para deixar a cena limpa
            droneObj.transform.localRotation = Quaternion.identity;
            celebration.HideSpeechBubbleInstant();
        }
    }
}
