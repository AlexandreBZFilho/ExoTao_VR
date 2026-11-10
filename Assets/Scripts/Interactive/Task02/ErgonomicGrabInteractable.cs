using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;

namespace VRRehab.Ergonomics
{
    /// <summary>
    /// Componente de Agarre Ergonômico de Alta Acessibilidade para Pacientes em Exoesqueleto.
    /// Herda de XRGrabInteractable do XRI 2.6.5 garantindo:
    /// 1. Force Grab habilitado por padrão com atração suave (sem solavancos ou teleporte brusco);
    /// 2. Auto-Respawn Inteligente que recupera objetos que caem no chão ou se distanciam da mesa;
    /// 3. Integração com ThrowAimAssist para correção de velocidade e trajetória balística;
    /// 4. Integração nativa com VRFeedbackManager (áudio 3D espacializado e hápticos em controllers).
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public class ErgonomicGrabInteractable : XRGrabInteractable
    {
        [Header("Ergonomia de Força & Atração Suave")]
        [Tooltip("Tempo de aproximação suave durante o Force Grab (segundos).")]
        [SerializeField] private float ergonomicAttachEaseInTime = 0.30f;

        [Header("Recuperação Automática (Auto-Respawn)")]
        [Tooltip("Ativa a recuperação automática quando o objeto cai no chão ou é abandonado longe da mesa.")]
        [SerializeField] private bool enableAutoRespawn = true;

        [Tooltip("Tempo de espera (em segundos) após ser solto antes de checar as condições de retorno.")]
        [SerializeField] private float respawnDelaySeconds = 2.5f;

        [Tooltip("Altitude mínima em Y (chão). Se cair abaixo desta cota, o retorno é acionado.")]
        [SerializeField] private float respawnFloorThresholdY = 0.35f;

        [Tooltip("Distância máxima permitida da zona inicial (em metros).")]
        [SerializeField] private float respawnMaxDistance = 5.0f;

        [Tooltip("Duração do voo suave de retorno para o ponto de origem (segundos).")]
        [SerializeField] private float returnGlideDuration = 0.85f;

        [Tooltip("Ponto de ancoragem inicial (definido automaticamente na inicialização se nulo).")]
        [SerializeField] private Transform customHomeAnchor;

        [Header("Eventos e Feedback")]
        [SerializeField] private bool playAudioHapticFeedback = true;

        private Vector3 homePosition;
        private Quaternion homeRotation;
        private bool isReturningHome = false;
        private float lastDropTimestamp = -1f;
        private bool isCurrentlyHeld = false;
        private Rigidbody cachedRb;
        private bool originalWasKinematic;
        private Coroutine respawnMonitorCoroutine;

        public Vector3 HomePosition => customHomeAnchor != null ? customHomeAnchor.position : homePosition;
        public Quaternion HomeRotation => customHomeAnchor != null ? customHomeAnchor.rotation : homeRotation;
        public bool IsReturningHome => isReturningHome;

        protected override void Awake()
        {
            base.Awake();

            cachedRb = GetComponent<Rigidbody>();
            if (cachedRb != null)
            {
                originalWasKinematic = cachedRb.isKinematic;
            }

            // Captura posição de repouso inicial
            SetHomeTransform(transform.position, transform.rotation);

            // Configuração ergonômica padrão do XRI
            attachEaseInTime = ergonomicAttachEaseInTime;
            movementType = MovementType.VelocityTracking;
            smoothPosition = true;
            smoothPositionAmount = 10f;
            smoothRotation = true;
            smoothRotationAmount = 10f;
            throwOnDetach = true;
        }

        protected override void Reset()
        {
            base.Reset();

            attachEaseInTime = 0.30f;
            movementType = MovementType.VelocityTracking;
            smoothPosition = true;
            smoothPositionAmount = 10f;
            smoothRotation = true;
            smoothRotationAmount = 10f;
            throwOnDetach = true;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (enableAutoRespawn && respawnMonitorCoroutine == null)
            {
                respawnMonitorCoroutine = StartCoroutine(AutoRespawnMonitorRoutine());
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (respawnMonitorCoroutine != null)
            {
                StopCoroutine(respawnMonitorCoroutine);
                respawnMonitorCoroutine = null;
            }
        }

        /// <summary>
        /// Atualiza o ponto de descanso / doca deste objeto.
        /// </summary>
        public void SetHomeTransform(Vector3 position, Quaternion rotation)
        {
            homePosition = position;
            homeRotation = rotation;
        }

        #region XRI Overrides
        protected override void OnHoverEntered(HoverEnterEventArgs args)
        {
            base.OnHoverEntered(args);

            if (playAudioHapticFeedback && VRFeedbackManager.Instance != null)
            {
                VRFeedbackManager.Instance.PlayHover(transform.position, args.interactorObject as IXRInteractor);
            }
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);

            isCurrentlyHeld = true;
            lastDropTimestamp = -1f;

            // Se estava no meio do voo de retorno, interrompe
            if (isReturningHome)
            {
                StopAllCoroutines();
                isReturningHome = false;
                if (cachedRb != null)
                {
                    cachedRb.isKinematic = originalWasKinematic;
                }
                if (enableAutoRespawn)
                {
                    respawnMonitorCoroutine = StartCoroutine(AutoRespawnMonitorRoutine());
                }
            }

            if (playAudioHapticFeedback && VRFeedbackManager.Instance != null)
            {
                VRFeedbackManager.Instance.PlayGrab(transform.position, args.interactorObject as IXRInteractor);
            }
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);

            isCurrentlyHeld = false;
            lastDropTimestamp = Time.time;

            if (playAudioHapticFeedback && VRFeedbackManager.Instance != null)
            {
                VRFeedbackManager.Instance.PlayRelease(transform.position);
            }
        }

        /// <summary>
        /// Intercepta o momento de soltura da física para aplicar ThrowAimAssist balístico.
        /// </summary>
        protected override void SetupRigidbodyDrop(Rigidbody rb)
        {
            base.SetupRigidbodyDrop(rb);

            if (rb == null) return;

            // Se houver componente ThrowAimAssist, aplica velocidade assistida
            if (TryGetComponent<ThrowAimAssist>(out var aimAssist))
            {
                Vector3 currentVel = GetRigidbodyVelocity(rb);
                Vector3 assistedVel = aimAssist.CalculateAssistedVelocity(transform.position, currentVel);
                SetRigidbodyVelocity(rb, assistedVel);
            }
        }
        #endregion

        #region Auto-Respawn Logic
        /// <summary>
        /// Coroutine de monitoramento contínuo de posição quando o objeto não estiver na mão do paciente.
        /// </summary>
        private IEnumerator AutoRespawnMonitorRoutine()
        {
            WaitForSeconds waitInterval = new WaitForSeconds(0.4f);

            while (true)
            {
                yield return waitInterval;

                if (!enableAutoRespawn || isCurrentlyHeld || isReturningHome)
                {
                    continue;
                }

                // Só avalia após o período de graça pós-soltura
                if (lastDropTimestamp > 0f && (Time.time - lastDropTimestamp) >= respawnDelaySeconds)
                {
                    Vector3 currentPos = transform.position;
                    Vector3 homePos = HomePosition;

                    bool fallenOnFloor = currentPos.y < respawnFloorThresholdY;
                    bool tooFarFromHome = Vector3.Distance(currentPos, homePos) > respawnMaxDistance;

                    if (fallenOnFloor || tooFarFromHome)
                    {
                        StartCoroutine(ReturnHomeRoutine());
                    }
                }
            }
        }

        /// <summary>
        /// Executa o retorno suave elástico ao ponto de descanso.
        /// </summary>
        public void TriggerManualRespawn()
        {
            if (!isReturningHome && !isCurrentlyHeld)
            {
                StartCoroutine(ReturnHomeRoutine());
            }
        }

        private IEnumerator ReturnHomeRoutine()
        {
            isReturningHome = true;

            if (cachedRb != null)
            {
                cachedRb.isKinematic = true;
                SetRigidbodyVelocity(cachedRb, Vector3.zero);
                SetRigidbodyAngularVelocity(cachedRb, Vector3.zero);
            }

            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            Vector3 targetPos = HomePosition;
            Quaternion targetRot = HomeRotation;

            // Eleva suavemente para evitar colisão com quinas de mesas no caminho
            float arcHeight = Mathf.Clamp(Vector3.Distance(startPos, targetPos) * 0.15f, 0.10f, 0.60f);

            float elapsed = 0f;
            while (elapsed < returnGlideDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / returnGlideDuration);

                // Curva SmoothStep para aceleração e desaceleração orgânica
                float smoothT = t * t * (3f - 2f * t);

                // Posição com arco parabólico ascendente
                Vector3 currentTarget = Vector3.Lerp(startPos, targetPos, smoothT);
                float arc = Mathf.Sin(t * Mathf.PI) * arcHeight;
                currentTarget.y += arc;

                transform.position = currentTarget;
                transform.rotation = Quaternion.Slerp(startRot, targetRot, smoothT);

                yield return null;
            }

            transform.position = targetPos;
            transform.rotation = targetRot;

            if (cachedRb != null)
            {
                cachedRb.isKinematic = originalWasKinematic;
                SetRigidbodyVelocity(cachedRb, Vector3.zero);
                SetRigidbodyAngularVelocity(cachedRb, Vector3.zero);
            }

            isReturningHome = false;
            lastDropTimestamp = -1f;

            if (playAudioHapticFeedback && VRFeedbackManager.Instance != null)
            {
                VRFeedbackManager.Instance.PlaySuccess(targetPos);
            }
        }
        #endregion

        #region Helpers de Compatibilidade de Física (Unity 6 / LinearVelocity)
        private static Vector3 GetRigidbodyVelocity(Rigidbody rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        private static void SetRigidbodyVelocity(Rigidbody rb, Vector3 velocity)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = velocity;
#else
            rb.velocity = velocity;
#endif
        }

        private static void SetRigidbodyAngularVelocity(Rigidbody rb, Vector3 angularVelocity)
        {
#if UNITY_6000_0_OR_NEWER
            rb.angularVelocity = angularVelocity;
#else
            rb.angularVelocity = angularVelocity;
#endif
        }
        #endregion
    }
}
