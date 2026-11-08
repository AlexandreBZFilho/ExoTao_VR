using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Configura automaticamente Canvases World Space para Realidade Virtual (VR) no Unity 6 URP.
/// - Remove interatores indevidos (XRRayInteractor não deve ficar no Canvas).
/// - Garante o TrackedDeviceGraphicRaycaster para permitir cliques com laser VR.
/// - Ativa vertexColorAlwaysGammaSpace para correção de gradientes no linear color space do URP.
/// - Conecta a Event Camera à Main Camera do XR Origin automaticamente.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Canvas))]
[DisallowMultipleComponent]
public class AutoConfigurarCanvasVR : MonoBehaviour
{
    private Canvas m_Canvas;

    private void Awake()
    {
        ConfigurarCanvas();
    }

    private void OnEnable()
    {
        ConfigurarCanvas();
    }

    private void Start()
    {
        ConfigurarCanvas();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ConfigurarCanvas();
    }
#endif

    public void ConfigurarCanvas()
    {
        if (m_Canvas == null)
            m_Canvas = GetComponent<Canvas>();

        if (m_Canvas == null) return;

        // 1. Em World Space, ativar Gamma Space no Unity 6 URP
        if (!m_Canvas.vertexColorAlwaysGammaSpace)
        {
            m_Canvas.vertexColorAlwaysGammaSpace = true;
        }

        // 2. Se for World Space e não tiver câmera associada, buscar a Main Camera
        if (m_Canvas.renderMode == RenderMode.WorldSpace && m_Canvas.worldCamera == null)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                cam = Object.FindFirstObjectByType<Camera>();
            }

            if (cam != null)
            {
                m_Canvas.worldCamera = cam;
            }
        }

        // 3. Remover XRRayInteractor se acidentalmente colocado no Canvas (pertence apenas aos controles das mãos)
        XRRayInteractor rayInteractor = GetComponent<XRRayInteractor>();
        if (rayInteractor != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null && rayInteractor != null)
                    {
                        DestroyImmediate(rayInteractor, true);
                        Debug.Log($"[ExoTao IC] Componente indevido XRRayInteractor removido de '{gameObject.name}'.");
                    }
                };
            }
            else
            {
                Destroy(rayInteractor);
            }
#else
            Destroy(rayInteractor);
#endif
        }

        // 4. Garantir que o TrackedDeviceGraphicRaycaster está presente para receber raios VR
        if (m_Canvas.renderMode == RenderMode.WorldSpace)
        {
            TrackedDeviceGraphicRaycaster vrRaycaster = GetComponent<TrackedDeviceGraphicRaycaster>();
            if (vrRaycaster == null)
            {
                gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            }
        }
    }
}
