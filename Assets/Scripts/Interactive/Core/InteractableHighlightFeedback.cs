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
    /// Cores calibradas: Hover Cyan (0.20, 0.60, 1.0) e Sucesso Gold (1.0, 0.78, 0.20).
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
        [SerializeField] private Color highlightEmissionColor = new Color(0.20f, 0.60f, 1.0f, 1.0f) * 1.5f;

        [ColorUsage(true, true)]
        [SerializeField] private Color successEmissionColor = new Color(1.0f, 0.78f, 0.20f, 1.0f) * 1.8f;

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
        private Coroutine flashCoroutine;

        private MaterialPropertyBlock propBlock;
        private static readonly int EmissionColorPropId = Shader.PropertyToID("_EmissionColor");

        private bool isHovered = false;

        public Color HighlightEmissionColor
        {
            get => highlightEmissionColor;
            set => highlightEmissionColor = value;
        }

        public Color SuccessEmissionColor
        {
            get => successEmissionColor;
            set => successEmissionColor = value;
        }

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

            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
                flashCoroutine = null;
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

            if (enableEmissionHighlight && flashCoroutine == null)
            {
                ApplyEmissionHighlight(highlightEmissionColor);
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

            if (enableEmissionHighlight && flashCoroutine == null)
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

        /// <summary>
        /// Aciona um flash dourado de sucesso (Meta Clínica / Pontuação) via MaterialPropertyBlock.
        /// </summary>
        public void TriggerSuccessHighlight(float duration = 0.6f)
        {
            if (!gameObject.activeInHierarchy) return;

            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
            }
            flashCoroutine = StartCoroutine(FlashSuccessRoutine(duration));
        }

        private IEnumerator FlashSuccessRoutine(float duration)
        {
            ApplyEmissionHighlight(successEmissionColor);
            yield return new WaitForSeconds(duration);

            if (isHovered)
            {
                ApplyEmissionHighlight(highlightEmissionColor);
            }
            else
            {
                ClearEmissionHighlight();
            }

            flashCoroutine = null;
        }

        private void ApplyEmissionHighlight(Color color)
        {
            if (propBlock == null) propBlock = new MaterialPropertyBlock();

            for (int i = 0; i < targetRenderers.Count; i++)
            {
                Renderer rend = targetRenderers[i];
                if (rend == null) continue;

                rend.GetPropertyBlock(propBlock);
                propBlock.SetColor(EmissionColorPropId, color);

                // Garante que o keyword _EMISSION esteja ativo no material compartilhado
                // (usando sharedMaterials para NUNCA instanciar material duplicado em runtime)
                Material[] sharedMats = rend.sharedMaterials;
                for (int m = 0; m < sharedMats.Length; m++)
                {
                    Material mat = sharedMats[m];
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
            if (propBlock == null) propBlock = new MaterialPropertyBlock();

            for (int i = 0; i < targetRenderers.Count; i++)
            {
                Renderer rend = targetRenderers[i];
                if (rend == null) continue;

                rend.GetPropertyBlock(propBlock);
                propBlock.Clear();
                rend.SetPropertyBlock(propBlock);
            }
        }
    }
}
