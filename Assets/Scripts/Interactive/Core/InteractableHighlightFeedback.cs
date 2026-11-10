using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace VRRehab.Core
{
    /// <summary>
    /// Componente de feedback multissensorial acoplável a qualquer XRBaseInteractable.
    /// Realiza scale punch elástico, highlight emissivo via MaterialPropertyBlock (zero alocação de material)
    /// e emite áudio 3D + hápticos via VRFeedbackManager.
    /// </summary>
    [RequireComponent(typeof(XRBaseInteractable))]
    [DisallowMultipleComponent]
    public class InteractableHighlightFeedback : MonoBehaviour
    {
        [Header("Configuração Visual")]
        [SerializeField] private bool enableScalePunch = true;
        [SerializeField] private float hoverScaleMultiplier = 1.06f;
        [SerializeField] private float scaleTransitionSpeed = 14.0f;

        [SerializeField] private bool enableEmissionHighlight = true;
        [ColorUsage(true, true)]
        [SerializeField] private Color highlightEmissionColor = new Color(0.2f, 0.6f, 1.0f, 1.0f) * 1.5f;

        [Header("Configuração Sensorial (Áudio & Haptics)")]
        [SerializeField] private bool playAudioOnHover = true;
        [SerializeField] private bool playAudioOnSelect = true;
        [SerializeField] private bool triggerHaptics = true;

        [Header("Renderers Alvo")]
        [Tooltip("Se vazio, detecta automaticamente os Renderers nos filhos.")]
        [SerializeField] private List<Renderer> targetRenderers = new List<Renderer>();

        private XRBaseInteractable interactable;
        private Vector3 initialScale;
        private Vector3 targetScale;
        private Coroutine scaleCoroutine;

        private MaterialPropertyBlock propBlock;
        private static readonly int EmissionColorPropId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorPropId = Shader.PropertyToID("_Color");
        private static readonly int URPBaseColorPropId = Shader.PropertyToID("_BaseColor");

        private bool isHovered = false;

        private void Awake()
        {
            interactable = GetComponent<XRBaseInteractable>();
            initialScale = transform.localScale;
            targetScale = initialScale;

            if (targetRenderers == null || targetRenderers.Count == 0)
            {
                targetRenderers = new List<Renderer>(GetComponentsInChildren<Renderer>(true));
            }

            propBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (interactable != null)
            {
                interactable.firstHoverEntered.AddListener(OnFirstHoverEntered);
                interactable.lastHoverExited.AddListener(OnLastHoverExited);
                interactable.selectEntered.AddListener(OnSelectEntered);
                interactable.selectExited.AddListener(OnSelectExited);
            }
        }

        private void OnDisable()
        {
            if (interactable != null)
            {
                interactable.firstHoverEntered.RemoveListener(OnFirstHoverEntered);
                interactable.lastHoverExited.RemoveListener(OnLastHoverExited);
                interactable.selectEntered.RemoveListener(OnSelectEntered);
                interactable.selectExited.RemoveListener(OnSelectExited);
            }

            ResetVisualState();
        }

        private void ResetVisualState()
        {
            isHovered = false;
            transform.localScale = initialScale;
            targetScale = initialScale;

            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
                scaleCoroutine = null;
            }

            ClearEmissionHighlight();
        }

        private void OnFirstHoverEntered(HoverEnterEventArgs args)
        {
            isHovered = true;

            if (enableScalePunch)
            {
                targetScale = initialScale * hoverScaleMultiplier;
                StartScaleAnimation();
            }

            if (enableEmissionHighlight)
            {
                ApplyEmissionHighlight();
            }

            if (playAudioOnHover || triggerHaptics)
            {
                IXRInteractor interactor = triggerHaptics ? args.interactorObject : null;
                VRFeedbackManager.Instance.PlayHover(transform.position, interactor);
            }
        }

        private void OnLastHoverExited(HoverExitEventArgs args)
        {
            isHovered = false;

            if (enableScalePunch)
            {
                targetScale = initialScale;
                StartScaleAnimation();
            }

            if (enableEmissionHighlight)
            {
                ClearEmissionHighlight();
            }
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (playAudioOnSelect || triggerHaptics)
            {
                IXRInteractor interactor = triggerHaptics ? args.interactorObject : null;
                VRFeedbackManager.Instance.PlayGrab(transform.position, interactor);
            }
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            if (playAudioOnSelect)
            {
                VRFeedbackManager.Instance.PlayRelease(transform.position);
            }
        }

        private void StartScaleAnimation()
        {
            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
            }
            if (gameObject.activeInHierarchy)
            {
                scaleCoroutine = StartCoroutine(AnimateScaleRoutine());
            }
        }

        private IEnumerator AnimateScaleRoutine()
        {
            while (Vector3.Distance(transform.localScale, targetScale) > 0.001f)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleTransitionSpeed);
                yield return null;
            }
            transform.localScale = targetScale;
            scaleCoroutine = null;
        }

        private void ApplyEmissionHighlight()
        {
            foreach (var rend in targetRenderers)
            {
                if (rend == null) continue;

                rend.GetPropertyBlock(propBlock);
                propBlock.SetColor(EmissionColorPropId, highlightEmissionColor);

                // Garante que o keyword _EMISSION esteja ativo no material compartilhado
                foreach (var mat in rend.sharedMaterials)
                {
                    if (mat != null && !mat.IsKeywordEnabled("_EMISSION"))
                    {
                        mat.EnableKeyword("_EMISSION");
                    }
                }

                rend.SetPropertyBlock(propBlock);
            }
        }

        private void ClearEmissionHighlight()
        {
            foreach (var rend in targetRenderers)
            {
                if (rend == null) continue;

                rend.GetPropertyBlock(propBlock);
                propBlock.Clear();
                rend.SetPropertyBlock(propBlock);
            }
        }
    }
}
