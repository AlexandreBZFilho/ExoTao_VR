using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace VRRehab.EditorAutoSetup
{
    /// <summary>
    /// Gerador procedural de texturas faciais cibernéticas em estilo display LED
    /// de alta fidelidade (512x512) para o visor frontal do Drone Pet (TASK-04).
    /// </summary>
    public static class Task04_LEDTextureGenerator
    {
        private const int TexSize = 512;
        private const string OutputFolder = "Assets/Textures/Generated/Task04";

        public static readonly Color ColorBg = new Color(0.015f, 0.025f, 0.04f, 1.0f);
        public static readonly Color ColorGrid = new Color(0.035f, 0.065f, 0.095f, 1.0f);
        public static readonly Color ColorCyan = new Color(0.0f, 0.96f, 1.0f, 1.0f);
        public static readonly Color ColorCyanGlow = new Color(0.0f, 0.75f, 1.0f, 0.35f);
        public static readonly Color ColorPink = new Color(1.0f, 0.18f, 0.65f, 1.0f);
        public static readonly Color ColorPinkGlow = new Color(1.0f, 0.15f, 0.55f, 0.35f);
        public static readonly Color ColorGold = new Color(1.0f, 0.85f, 0.20f, 1.0f);

        [MenuItem("VRRehab/AutoBuild/Generate Task04 LED Textures")]
        public static void GenerateAllTextures()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Textures"))
            {
                AssetDatabase.CreateFolder("Assets", "Textures");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Textures/Generated"))
            {
                AssetDatabase.CreateFolder("Assets/Textures", "Generated");
            }
            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                AssetDatabase.CreateFolder("Assets/Textures/Generated", "Task04");
            }

            GenerateTexture("LED_Expression_Normal.png", DrawExpressionNormal);
            GenerateTexture("LED_Expression_Happy.png", DrawExpressionHappy);
            GenerateTexture("LED_Expression_Love.png", DrawExpressionLove);
            GenerateTexture("LED_Expression_Poked.png", DrawExpressionPoked);
            GenerateTexture("LED_Expression_Dance.png", DrawExpressionDance);
            GenerateTexture("Holo_Heart_Particle.png", DrawHoloHeartParticle, transparentBg: true);

            AssetDatabase.Refresh();
            Debug.Log("[TASK-04] Todas as 6 texturas LED e partículas foram geradas com sucesso!");
        }

        private static void GenerateTexture(string fileName, Action<Color[,]> drawAction, bool transparentBg = false)
        {
            string fullPath = Path.Combine(OutputFolder, fileName);
            Color[,] pixels = new Color[TexSize, TexSize];

            // 1. Inicializa background com grid de matriz LED
            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    if (transparentBg)
                    {
                        pixels[x, y] = new Color(0f, 0f, 0f, 0f);
                    }
                    else
                    {
                        // Moldura circular e matriz de micropixels LED
                        float dx = (x - TexSize * 0.5f) / (TexSize * 0.5f);
                        float dy = (y - TexSize * 0.5f) / (TexSize * 0.5f);
                        float distSq = dx * dx + dy * dy;

                        if (distSq > 0.98f)
                        {
                            // Moldura externa escura metálica
                            pixels[x, y] = new Color(0.01f, 0.012f, 0.018f, 1.0f);
                        }
                        else
                        {
                            // Microgrid LED (passo de 8 pixels)
                            bool isLedCell = (x % 8 <= 5) && (y % 8 <= 5);
                            pixels[x, y] = isLedCell ? ColorBg : ColorGrid;
                        }
                    }
                }
            }

            // 2. Executa desenho procedural da expressão
            drawAction(pixels);

            // 3. Converte para Texture2D e salva PNG
            Texture2D tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    tex.SetPixel(x, y, pixels[x, y]);
                }
            }
            tex.Apply();

            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(fullPath, bytes);
            UnityEngine.Object.DestroyImmediate(tex);

            // Configura TextureImporter
            AssetDatabase.ImportAsset(fullPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(fullPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
        }

        #region Desenhos Procedurais das Expressões

        // 1. Normal (•‿•)
        private static void DrawExpressionNormal(Color[,] pixels)
        {
            // Olho esquerdo (X: 175, Y: 295, R: 38)
            DrawGlowingCircle(pixels, 175, 295, 36, ColorCyan, ColorCyanGlow);
            DrawCircle(pixels, 175, 295, 20, Color.white);
            DrawCircle(pixels, 185, 305, 9, Color.white); // Brilho especular

            // Olho direito (X: 337, Y: 295, R: 38)
            DrawGlowingCircle(pixels, 337, 295, 36, ColorCyan, ColorCyanGlow);
            DrawCircle(pixels, 337, 295, 20, Color.white);
            DrawCircle(pixels, 347, 305, 9, Color.white);

            // Arcos superiores sutis (sobrancelhas robóticas)
            DrawThickArc(pixels, 175, 338, 44, 25, 155, 6, ColorCyan, ColorCyanGlow);
            DrawThickArc(pixels, 337, 338, 44, 25, 155, 6, ColorCyan, ColorCyanGlow);

            // Sorriso suave arqueado (X: 256, Y: 215, R: 52)
            DrawThickArc(pixels, 256, 215, 52, 205, 335, 7, ColorCyan, ColorCyanGlow);

            // Bochechas de biofeedback ciano suave
            DrawGlowingCircle(pixels, 125, 235, 18, ColorCyanGlow, new Color(0f, 0.4f, 0.8f, 0.15f));
            DrawGlowingCircle(pixels, 387, 235, 18, ColorCyanGlow, new Color(0f, 0.4f, 0.8f, 0.15f));
        }

        // 2. Happy (^‿^)
        private static void DrawExpressionHappy(Color[,] pixels)
        {
            // Olho esquerdo em arco feliz ^
            DrawThickArc(pixels, 175, 280, 44, 30, 150, 9, ColorCyan, ColorCyanGlow);
            // Olho direito em arco feliz ^
            DrawThickArc(pixels, 337, 280, 44, 30, 150, 9, ColorCyan, ColorCyanGlow);

            // Sorriso aberto largo e alegre
            DrawThickArc(pixels, 256, 225, 65, 200, 340, 9, ColorCyan, ColorCyanGlow);

            // Bochechas coradas em magenta/rosa suave
            DrawGlowingCircle(pixels, 120, 235, 22, ColorPink, ColorPinkGlow);
            DrawGlowingCircle(pixels, 392, 235, 22, ColorPink, ColorPinkGlow);
        }

        // 3. Love (♥‿♥)
        private static void DrawExpressionLove(Color[,] pixels)
        {
            // Coração olho esquerdo
            DrawHeart(pixels, 175, 295, 38, ColorPink, ColorPinkGlow);
            // Coração olho direito
            DrawHeart(pixels, 337, 295, 38, ColorPink, ColorPinkGlow);

            // Sorriso afetuoso radiante com toque rosa/ciano
            DrawThickArc(pixels, 256, 210, 58, 205, 335, 8, ColorCyan, ColorCyanGlow);

            // Bochechas brilhantes
            DrawGlowingCircle(pixels, 115, 235, 24, ColorPink, ColorPinkGlow);
            DrawGlowingCircle(pixels, 397, 235, 24, ColorPink, ColorPinkGlow);
        }

        // 4. Poked (@_@)
        private static void DrawExpressionPoked(Color[,] pixels)
        {
            // Olho esquerdo em espiral concêntrica
            DrawThickArc(pixels, 175, 295, 42, 0, 360, 5, ColorCyan, ColorCyanGlow);
            DrawThickArc(pixels, 175, 295, 26, 0, 360, 5, ColorPink, ColorPinkGlow);
            DrawCircle(pixels, 175, 295, 10, ColorCyan);

            // Olho direito em espiral concêntrica
            DrawThickArc(pixels, 337, 295, 42, 0, 360, 5, ColorCyan, ColorCyanGlow);
            DrawThickArc(pixels, 337, 295, 26, 0, 360, 5, ColorPink, ColorPinkGlow);
            DrawCircle(pixels, 337, 295, 10, ColorCyan);

            // Boca ondulada de surpresa/tontura
            for (int x = 205; x <= 307; x++)
            {
                float t = (x - 205f) / 102f;
                float y = 195f + Mathf.Sin(t * Mathf.PI * 4f) * 12f;
                DrawBrush(pixels, x, (int)y, 5, ColorGold, new Color(1f, 0.7f, 0f, 0.3f));
            }

            // Pequenas estrelas/tonturas orbitando
            DrawStar(pixels, 130, 365, 14, ColorGold);
            DrawStar(pixels, 382, 365, 14, ColorGold);
        }

        // 5. Dance (♪‿♪)
        private static void DrawExpressionDance(Color[,] pixels)
        {
            // Olho esquerdo: Nota musical ♪
            DrawMusicalNote(pixels, 175, 295, 40, ColorCyan, ColorCyanGlow);

            // Olho direito: Nota musical ♪
            DrawMusicalNote(pixels, 337, 295, 40, ColorCyan, ColorCyanGlow);

            // Sorriso dançante aberto
            DrawThickArc(pixels, 256, 215, 62, 195, 345, 9, ColorCyan, ColorCyanGlow);

            // Notas musicais flutuantes pequenas nas laterais
            DrawMusicalNote(pixels, 115, 355, 22, ColorPink, ColorPinkGlow);
            DrawMusicalNote(pixels, 397, 355, 22, ColorGold, new Color(1f, 0.8f, 0.2f, 0.3f));
        }

        // 6. Holo Heart Particle (para ParticleSystem de carinho)
        private static void DrawHoloHeartParticle(Color[,] pixels)
        {
            DrawHeart(pixels, 256, 256, 110, ColorPink, ColorPinkGlow);
            DrawHeart(pixels, 256, 256, 85, Color.white, ColorPink);
        }

        #endregion

        #region Primitivas Gráficas com Glow

        private static void DrawCircle(Color[,] pixels, int cx, int cy, int radius, Color color)
        {
            int rSq = radius * radius;
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                if (y < 0 || y >= TexSize) continue;
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    if (x < 0 || x >= TexSize) continue;
                    int dSq = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                    if (dSq <= rSq)
                    {
                        pixels[x, y] = BlendColors(pixels[x, y], color);
                    }
                }
            }
        }

        private static void DrawGlowingCircle(Color[,] pixels, int cx, int cy, int radius, Color coreColor, Color glowColor)
        {
            int outerR = radius + 18;
            int outerSq = outerR * outerR;
            int coreSq = radius * radius;

            for (int y = cy - outerR; y <= cy + outerR; y++)
            {
                if (y < 0 || y >= TexSize) continue;
                for (int x = cx - outerR; x <= cx + outerR; x++)
                {
                    if (x < 0 || x >= TexSize) continue;
                    int dSq = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                    if (dSq <= coreSq)
                    {
                        pixels[x, y] = BlendColors(pixels[x, y], coreColor);
                    }
                    else if (dSq <= outerSq)
                    {
                        float factor = 1.0f - Mathf.Sqrt((float)(dSq - coreSq)) / 18.0f;
                        Color c = new Color(glowColor.r, glowColor.g, glowColor.b, glowColor.a * factor);
                        pixels[x, y] = BlendColors(pixels[x, y], c);
                    }
                }
            }
        }

        private static void DrawThickArc(Color[,] pixels, int cx, int cy, int radius, float angleStartDeg, float angleEndDeg, int thickness, Color coreColor, Color glowColor)
        {
            int maxR = radius + thickness + 12;
            int minR = Mathf.Max(0, radius - thickness - 12);

            for (int y = cy - maxR; y <= cy + maxR; y++)
            {
                if (y < 0 || y >= TexSize) continue;
                for (int x = cx - maxR; x <= cx + maxR; x++)
                {
                    if (x < 0 || x >= TexSize) continue;
                    float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float deltaR = Mathf.Abs(dist - radius);
                    if (deltaR > thickness + 12) continue;

                    float angle = Mathf.Atan2(y - cy, x - cx) * Mathf.Rad2Deg;
                    if (angle < 0) angle += 360f;

                    bool inAngle = false;
                    if (angleStartDeg <= angleEndDeg)
                        inAngle = (angle >= angleStartDeg && angle <= angleEndDeg);
                    else
                        inAngle = (angle >= angleStartDeg || angle <= angleEndDeg);

                    if (inAngle)
                    {
                        if (deltaR <= thickness)
                        {
                            pixels[x, y] = BlendColors(pixels[x, y], coreColor);
                        }
                        else
                        {
                            float factor = 1.0f - (deltaR - thickness) / 12.0f;
                            Color c = new Color(glowColor.r, glowColor.g, glowColor.b, glowColor.a * factor);
                            pixels[x, y] = BlendColors(pixels[x, y], c);
                        }
                    }
                }
            }
        }

        private static void DrawHeart(Color[,] pixels, int cx, int cy, int size, Color coreColor, Color glowColor)
        {
            int bound = size + 16;
            for (int y = cy - bound; y <= cy + bound; y++)
            {
                if (y < 0 || y >= TexSize) continue;
                for (int x = cx - bound; x <= cx + bound; x++)
                {
                    if (x < 0 || x >= TexSize) continue;
                    float nx = (x - cx) / (float)size;
                    float ny = (y - cy) / (float)size;

                    // Equação implícita padrão do coração: (x^2 + y^2 - 1)^3 - x^2 * y^3 <= 0
                    // Ajuste de escala e orientação
                    float hx = nx * 1.25f;
                    float hy = (ny + 0.15f) * 1.25f;
                    float f = (hx * hx + hy * hy - 1f);
                    float heartVal = f * f * f - hx * hx * (hy * hy * hy);

                    if (heartVal <= 0f)
                    {
                        pixels[x, y] = BlendColors(pixels[x, y], coreColor);
                    }
                    else if (heartVal < 0.28f)
                    {
                        float factor = 1.0f - (heartVal / 0.28f);
                        Color c = new Color(glowColor.r, glowColor.g, glowColor.b, glowColor.a * factor);
                        pixels[x, y] = BlendColors(pixels[x, y], c);
                    }
                }
            }
        }

        private static void DrawMusicalNote(Color[,] pixels, int cx, int cy, int size, Color coreColor, Color glowColor)
        {
            // Cabeça da nota musical (elipse inclinada na base inferior esquerda)
            int noteHeadX = cx - (int)(size * 0.25f);
            int noteHeadY = cy - (int)(size * 0.35f);
            int noteHeadR = (int)(size * 0.30f);
            DrawGlowingCircle(pixels, noteHeadX, noteHeadY, noteHeadR, coreColor, glowColor);

            // Haste vertical ascendente
            int stemX = noteHeadX + (int)(noteHeadR * 0.7f);
            int stemY0 = noteHeadY;
            int stemY1 = cy + (int)(size * 0.45f);
            int stemW = Mathf.Max(3, (int)(size * 0.14f));
            for (int y = stemY0; y <= stemY1; y++)
            {
                for (int x = stemX; x < stemX + stemW; x++)
                {
                    if (x >= 0 && x < TexSize && y >= 0 && y < TexSize)
                    {
                        pixels[x, y] = BlendColors(pixels[x, y], coreColor);
                    }
                }
            }

            // Colchete / Bandeira superior para a direita
            DrawThickArc(pixels, stemX + (int)(size * 0.18f), stemY1 - (int)(size * 0.15f), (int)(size * 0.32f), 30, 110, stemW, coreColor, glowColor);
        }

        private static void DrawStar(Color[,] pixels, int cx, int cy, int size, Color color)
        {
            for (int y = cy - size; y <= cy + size; y++)
            {
                if (y < 0 || y >= TexSize) continue;
                for (int x = cx - size; x <= cx + size; x++)
                {
                    if (x < 0 || x >= TexSize) continue;
                    int dx = Mathf.Abs(x - cx);
                    int dy = Mathf.Abs(y - cy);
                    if (dx + dy <= size)
                    {
                        pixels[x, y] = BlendColors(pixels[x, y], color);
                    }
                }
            }
        }

        private static void DrawBrush(Color[,] pixels, int cx, int cy, int radius, Color coreColor, Color glowColor)
        {
            DrawGlowingCircle(pixels, cx, cy, radius, coreColor, glowColor);
        }

        private static Color BlendColors(Color bg, Color fg)
        {
            float a = fg.a;
            float invA = 1.0f - a;
            return new Color(
                bg.r * invA + fg.r * a,
                bg.g * invA + fg.g * a,
                bg.b * invA + fg.b * a,
                Mathf.Clamp01(bg.a + fg.a)
            );
        }

        #endregion
    }
}
