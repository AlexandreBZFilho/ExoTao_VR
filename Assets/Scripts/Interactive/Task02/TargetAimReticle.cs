using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;

namespace VRRehab.Ergonomics
{
    /// <summary>
    /// Retícula Holográfica 3D no Chão / Superfície da Mesa.
    /// Projeta o anel cibernético (ReticleTargetRing.fbx) abaixo do objeto interativo quando
    /// o raio laser do XRRayInteractor aponta para ele, fornecendo percepção de profundidade
    /// e confirmação visual cristalina para pacientes com exoesqueleto.
    /// </summary>
    [DisallowMultipleComponent]
    public class TargetAimReticle : MonoBehaviour
    {
        [Header("Prefab e Estrutura")]
        [Tooltip("Prefab ou instância do anel ReticleTargetRing.")]
        [SerializeField] private GameObject reticlePrefab;
        [SerializeField] private Transform reticleInstance;

        [Header("Alinhamento e Superfície")]
        [Tooltip("Camadas consideradas superfície/chão/mesa para projeção da retícula.")]
        [SerializeField] private LayerMask groundLayers = ~0; // Padrão: tudo exceto triggers
        [SerializeField] private float raycastDownDistance = 6.0f;
        [SerializeField] private float surfaceOffset = 0.003f; // Previne Z-Fighting

        [Header("Animação")]
        [SerializeField] private float rotationSpeed = 35.0f; // Graus por segundo
        [SerializeField] private float scaleTransitionDuration = 0.22f;
        [SerializeField] private Vector3 targetRingScale = Vector3.one;

        private XRBaseInteractable cachedInteractable;
        private Coroutine activeScaleRoutine;
        private bool isVisible = false;
        private Renderer[] reticleRenderers;

        private void Awake()
        {
            cachedInteractable = GetComponent<XRBaseInteractable>();

            // Instancia ou localiza a retícula
            if (reticleInstance == null)
            {
                if (reticlePrefab != null)
                {
                    GameObject go = Instantiate(reticlePrefab, transform.position, Quaternion.identity);
                    go.name = $"{gameObject.name}_ReticleTargetRing";
                    reticleInstance = go.transform;
                }
                else
                {
                    // Tenta encontrar em filhos
                    Transform child = transform.Find("ReticleTargetRing");
                    if (child != null)
                    {
                        reticleInstance = child;
                    }
                }
            }

            if (reticleInstance != null)
            {
                reticleRenderers = reticleInstance.GetComponentsInChildren<Renderer>();
                reticleInstance.localScale = Vector3.zero;
                reticleInstance.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (cachedInteractable != null)
            {
                cachedInteractable.firstHoverEntered.AddListener(OnFirstHoverEntered);
                cachedInteractable.lastHoverExited.AddListener(OnLastHoverExited);
                cachedInteractable.selectEntered.AddListener(OnSelectEntered);
                cachedInteractable.selectExited.AddListener(OnSelectExited);
            }
        }

        private void OnDisable()
        {
            if (cachedInteractable != null)
            {
                cachedInteractable.firstHoverEntered.RemoveListener(OnFirstHoverEntered);
                cachedInteractable.lastHoverExited.RemoveListener(OnLastHoverExited);
                cachedInteractable.selectEntered.RemoveListener(OnSelectEntered);
                cachedInteractable.selectExited.RemoveListener(OnSelectExited);
            }

            if (reticleInstance != null)
            {
                reticleInstance.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (!isVisible || reticleInstance == null) return;

            // Rotação contínua da retícula cibernética
            reticleInstance.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);

            // Projeção vertical sobre a superfície mais próxima
            UpdateReticlePosition();
        }

        private void UpdateReticlePosition()
        {
            Vector3 origin = transform.position;
            // Raycast para baixo ignorando o próprio objeto
            Ray ray = new Ray(origin, Vector3.down);
            RaycastHit hit;

            // Mascara colisões com o próprio objeto para não projetar na própria malha
            Collider myCol = GetComponent<Collider>();
            bool prevColState = false;
            if (myCol != null)
            {
                prevColState = myCol.enabled;
            }

            if (Physics.Raycast(ray, out hit, raycastDownDistance, groundLayers, QueryTriggerInteraction.Ignore))
            {
                reticleInstance.position = hit.point + hit.normal * surfaceOffset;
                reticleInstance.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            }
            else
            {
                // Fallback caso não atinja nenhuma geometria (ex: flutuando no vazio)
                reticleInstance.position = origin - Vector3.up * 0.45f;
                reticleInstance.rotation = Quaternion.identity;
            }
        }

        #region Eventos XRI
        private void OnFirstHoverEntered(HoverEnterEventArgs args)
        {
            SetReticleVisible(true);
        }

        private void OnLastHoverExited(HoverExitEventArgs args)
        {
            // Se o objeto estiver selecionado na mão, mantém invisível
            if (cachedInteractable != null && cachedInteractable.isSelected) return;
            SetReticleVisible(false);
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            // Enquanto o jogador segura o objeto, esconde a retícula do chão para despoluir a visão
            SetReticleVisible(false);
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            // Se o laser ainda estiver apontando ao soltar, reativa
            if (cachedInteractable != null && cachedInteractable.isHovered)
            {
                SetReticleVisible(true);
            }
        }
        #endregion

        public void SetReticleVisible(bool visible)
        {
            if (reticleInstance == null) return;

            isVisible = visible;
            if (activeScaleRoutine != null)
            {
                StopCoroutine(activeScaleRoutine);
            }

            if (visible)
            {
                reticleInstance.gameObject.SetActive(true);
                UpdateReticlePosition();
                activeScaleRoutine = StartCoroutine(AnimateScale(reticleInstance, targetRingScale, scaleTransitionDuration));
            }
            else
            {
                activeScaleRoutine = StartCoroutine(AnimateScale(reticleInstance, Vector3.zero, scaleTransitionDuration, () =>
                {
                    if (reticleInstance != null)
                    {
                        reticleInstance.gameObject.SetActive(false);
                    }
                }));
            }
        }

        private IEnumerator AnimateScale(Transform target, Vector3 toScale, float duration, System.Action onComplete = null)
        {
            Vector3 fromScale = target.localScale;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Curva suave ease-out
                float smoothT = Mathf.Sin(t * Mathf.PI * 0.5f);
                target.localScale = Vector3.LerpUnclamped(fromScale, toScale, smoothT);
                yield return null;
            }

            target.localScale = toScale;
            onComplete?.Invoke();
        }

        /// <summary>
        /// Permite atribuir programaticamente o transform da retícula (ex: no script de Editor AutoSetup).
        /// </summary>
        public void ConfigureReticleInstance(Transform instance, GameObject prefab = null)
        {
            reticleInstance = instance;
            reticlePrefab = prefab;
            if (reticleInstance != null)
            {
                reticleRenderers = reticleInstance.GetComponentsInChildren<Renderer>();
            }
        }
    }
}
