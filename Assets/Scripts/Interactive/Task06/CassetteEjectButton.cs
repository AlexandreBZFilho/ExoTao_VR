using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;

namespace VRRehab.Interactive.Task06
{
    /// <summary>
    /// Botão físico de ejeção mecânica de fita cassete (TASK-06).
    /// Responde a interações do XRI (raio laser XRRayInteractor, toque direto com as mãos) e cliques de mouse,
    /// animando o recuo do botão com mola amortecida de alta frequência e comandando a ejeção no RadioCassetteSocket.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class CassetteEjectButton : XRBaseInteractable, IPointerClickHandler, IPointerEnterHandler
    {
        [Header("Referências")]
        [Tooltip("Socket do deck ao qual este botão está acoplado.")]
        public RadioCassetteSocket deckSocket;

        [Tooltip("Transform da malha do botão que se move ao ser pressionado.")]
        public Transform buttonMeshTransform;

        [Header("Cinemática do Botão (Mola Amortecida)")]
        [Tooltip("Deslocamento local para dentro ao ser pressionado (em metros).")]
        public Vector3 pressedLocalOffset = new Vector3(0f, 0f, -0.006f);

        [Tooltip("Rigidez do retorno elástico.")]
        public float springStiffness = 30f;

        [Tooltip("Tempo de retenção totalmente pressionado (segundos).")]
        public float pressHoldDuration = 0.08f;

        private Vector3 initialLocalPos;
        private Vector3 targetLocalPos;
        private float holdTimer = 0f;
        private bool isSettled = true;

        protected override void Awake()
        {
            base.Awake();

            if (buttonMeshTransform == null)
            {
                Transform t = transform.Find("Eject_Button") ?? transform.Find("Visual_Model/Eject_Button");
                buttonMeshTransform = t != null ? t : transform;
            }

            initialLocalPos = buttonMeshTransform.localPosition;
            targetLocalPos = initialLocalPos;

            if (deckSocket == null)
            {
                deckSocket = GetComponentInParent<RadioCassetteSocket>();
            }
        }

        private void Update()
        {
            // Saída antecipada (custo ~zero): botão em repouso e já convergido
            if (isSettled) return;

            if (holdTimer > 0f)
            {
                holdTimer -= Time.deltaTime;
                if (holdTimer <= 0f)
                {
                    targetLocalPos = initialLocalPos;
                }
            }

            if (buttonMeshTransform == null)
            {
                isSettled = holdTimer <= 0f;
                return;
            }

            Vector3 currentLocalPos = buttonMeshTransform.localPosition;
            if (holdTimer <= 0f && (targetLocalPos - currentLocalPos).sqrMagnitude < 1e-8f)
            {
                buttonMeshTransform.localPosition = targetLocalPos; // encaixe final exato
                isSettled = true;
                return;
            }

            // Interpolação elástica zero-GC da malha do botão
            buttonMeshTransform.localPosition = Vector3.Lerp(
                currentLocalPos,
                targetLocalPos,
                Time.deltaTime * springStiffness
            );
        }

        public void PressButton(IXRInteractor interactor = null)
        {
            // Animação de afundamento do botão mecânico
            targetLocalPos = initialLocalPos + pressedLocalOffset;
            holdTimer = pressHoldDuration;
            isSettled = false;

            // Feedback háptico tátil no controle
            if (interactor is XRBaseControllerInteractor controllerInteractor)
            {
                controllerInteractor.SendHapticImpulse(0.70f, 0.15f);
            }
            else if (interactor != null && VRFeedbackManager.Instance != null)
            {
                VRFeedbackManager.Instance.TriggerHaptic(interactor, 0.70f, 0.15f);
            }

            // Feedback de som de clique mecânico
            if (VRFeedbackManager.Instance != null)
            {
                VRFeedbackManager.Instance.PlayButtonClick(transform.position);
            }

            // Dispara a ejeção mecânica no socket
            if (deckSocket != null)
            {
                deckSocket.EjectCurrentTape();
            }
        }

        #region XRI Overrides
        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            PressButton(args.interactorObject as IXRInteractor);
        }

        protected override void OnActivated(ActivateEventArgs args)
        {
            base.OnActivated(args);
            PressButton(args.interactorObject as IXRInteractor);
        }
        #endregion

        #region UI & Mouse Fallback
        public void OnPointerClick(PointerEventData eventData)
        {
            PressButton();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (VRFeedbackManager.Instance != null)
            {
                VRFeedbackManager.Instance.PlayHover(transform.position);
            }
        }
        #endregion
    }
}
