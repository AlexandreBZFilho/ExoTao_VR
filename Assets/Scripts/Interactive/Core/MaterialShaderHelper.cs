using UnityEngine;
using UnityEngine.Rendering;

namespace VRRehab.Core
{
    /// <summary>
    /// Helper estático para detecção dinâmica de Render Pipeline (Built-in vs URP/HDRP)
    /// e fornecimento de shaders e materiais compatíveis, prevenindo shaders magenta/quebrados.
    /// </summary>
    public static class MaterialShaderHelper
    {
        private const string ShaderBuiltinStandard = "Standard";
        private const string ShaderBuiltinUnlit = "Unlit/Color";
        private const string ShaderURPLit = "Universal Render Pipeline/Lit";
        private const string ShaderURPUnlit = "Universal Render Pipeline/Unlit";

        /// <summary>
        /// Retorna verdadeiro se um Render Pipeline baseado em SRP/URP estiver ativo.
        /// </summary>
        public static bool IsURPActive
        {
            get
            {
                RenderPipelineAsset current = GraphicsSettings.currentRenderPipeline;
                if (current == null)
                {
                    current = QualitySettings.renderPipeline;
                }

                if (current != null)
                {
                    string typeName = current.GetType().Name.ToLowerInvariant();
                    return typeName.Contains("universal") || typeName.Contains("urp");
                }

                return false;
            }
        }

        /// <summary>
        /// Retorna o melhor Shader Lit compatível com a configuração atual.
        /// </summary>
        public static Shader GetStandardLitShader()
        {
            if (IsURPActive)
            {
                Shader urpShader = Shader.Find(ShaderURPLit);
                if (urpShader != null) return urpShader;
            }

            Shader builtinStandard = Shader.Find(ShaderBuiltinStandard);
            if (builtinStandard != null) return builtinStandard;

            // Fallback resiliente
            Shader mobileDiffuse = Shader.Find("Mobile/Diffuse");
            if (mobileDiffuse != null) return mobileDiffuse;

            return Shader.Find("Diffuse");
        }

        /// <summary>
        /// Retorna o melhor Shader Unlit compatível com a configuração atual.
        /// </summary>
        public static Shader GetUnlitShader()
        {
            if (IsURPActive)
            {
                Shader urpUnlit = Shader.Find(ShaderURPUnlit);
                if (urpUnlit != null) return urpUnlit;
            }

            Shader unlitColor = Shader.Find(ShaderBuiltinUnlit);
            if (unlitColor != null) return unlitColor;

            return Shader.Find("Unlit/Texture");
        }

        /// <summary>
        /// Cria um novo Material Lit pronto com a cor e acabamento especificados.
        /// </summary>
        public static Material CreateLitMaterial(string name, Color baseColor, float smoothness = 0.5f, float metallic = 0.0f)
        {
            Shader shader = GetStandardLitShader();
            Material mat = new Material(shader)
            {
                name = name
            };

            SetBaseColor(mat, baseColor);
            SetSmoothness(mat, smoothness);
            SetMetallic(mat, metallic);

            return mat;
        }

        /// <summary>
        /// Cria um novo Material Emissivo com brilho e cor configurados.
        /// </summary>
        public static Material CreateEmissiveMaterial(string name, Color baseColor, Color emissionColor, float intensity = 1.0f)
        {
            Material mat = CreateLitMaterial(name, baseColor, 0.4f, 0.0f);
            SetEmission(mat, emissionColor, intensity);
            return mat;
        }

        /// <summary>
        /// Cria um material transparente ajustando os blend modes corretamente.
        /// </summary>
        public static Material CreateTransparentMaterial(string name, Color baseColor, float alpha)
        {
            Material mat = CreateLitMaterial(name, new Color(baseColor.r, baseColor.g, baseColor.b, alpha));

            if (IsURPActive)
            {
                mat.SetFloat("_Surface", 1); // Transparent
                mat.SetFloat("_Blend", 0);   // Alpha
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                // Standard shader transparente no Built-in
                mat.SetFloat("_Mode", 3); // Transparent
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }

            return mat;
        }

        /// <summary>
        /// Cria um material luminoso aditivo para raios, lasers e feixes tratores holográficos.
        /// </summary>
        public static Material CreateBeamGlowMaterial(string name, Color color)
        {
            Shader shader = null;
            if (IsURPActive)
            {
                shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit");
            }
            else
            {
                shader = Shader.Find("Mobile/Particles/Additive") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Legacy Shaders/Particles/Additive");
            }

            if (shader == null) shader = GetUnlitShader();

            Material mat = new Material(shader)
            {
                name = name
            };

            SetBaseColor(mat, color);
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", color);
            SetEmission(mat, color, 2.5f);

            return mat;
        }

        /// <summary>
        /// Atribui a cor base nos slots corretos (_BaseColor para URP ou _Color para Standard).
        /// </summary>
        public static void SetBaseColor(Material mat, Color color)
        {
            if (mat == null) return;

            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }
            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", color);
            }
        }

        /// <summary>
        /// Configura a cor e intensidade da emissão, ativando a keyword necessária.
        /// </summary>
        public static void SetEmission(Material mat, Color emissionColor, float intensity = 1.0f)
        {
            if (mat == null) return;

            Color finalEmission = emissionColor * Mathf.LinearToGammaSpace(intensity);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            if (mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", finalEmission);
            }
        }

        public static void SetSmoothness(Material mat, float smoothness)
        {
            if (mat == null) return;

            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", smoothness);
            }
            if (mat.HasProperty("_Glossiness"))
            {
                mat.SetFloat("_Glossiness", smoothness);
            }
        }

        public static void SetMetallic(Material mat, float metallic)
        {
            if (mat == null) return;

            if (mat.HasProperty("_Metallic"))
            {
                mat.SetFloat("_Metallic", metallic);
            }
        }

        /// <summary>
        /// Verifica se o shader do material está quebrado, nulo ou magenta (InternalErrorShader),
        /// recuperando-o com o shader padrão compatível.
        /// </summary>
        public static bool EnsureValidShader(Material mat)
        {
            if (mat == null) return false;

            bool isBroken = mat.shader == null ||
                            mat.shader.name == "Hidden/InternalErrorShader" ||
                            mat.shader.name == "";

            if (isBroken)
            {
                mat.shader = GetStandardLitShader();
                return true;
            }

            // Se URP estiver ativo mas o material estiver usando Standard, ou vice-versa:
            if (!IsURPActive && mat.shader.name.StartsWith("Universal Render Pipeline/"))
            {
                mat.shader = GetStandardLitShader();
                return true;
            }

            return false;
        }
    }
}
