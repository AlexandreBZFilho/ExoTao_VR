using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;
using VRRehab.Ergonomics;

namespace VRRehab.DronePet
{
    /// <summary>
    /// Componente acoplado a itens interativos e arremessáveis (como CyberFetchBaton, brinquedos ou maçãs).
    /// Detecta quando o objeto é arremessado ou cai além do raio de alcance seguro do paciente (> 2.5m)
    /// e emite um alerta para o Drone Guia / FetchBot vir resgatá-lo com o feixe trator.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DroneFetchTarget : MonoBehaviour
    {
        [Header("Configurações de Detecção")]
        [Tooltip("Distância mínima do jogador para considerar o objeto apto para busca pelo Drone")]
        [SerializeField] private float minFetchDistance = 2.5f;

        [Tooltip("Tempo de espera (em segundos) após soltar o objeto antes de acionar a busca")]
        [SerializeField] private float autoFetchDelay = 1.2f;

        [Tooltip("Se ativado, mesmo com velocidade baixa, distâncias maiores que minFetchDistance acionam o drone (DESATIVADO PARA TASK-03)")]
        [SerializeField] private bool allowAutoFetch = false;

        [Header("Parâmetros de Acoplamento ao Feixe Trator")]
        [Tooltip("Offset local em relação ao ponto de ancoragem do feixe trator (mantém item afastado da fuselagem)")]
        [SerializeField] private Vector3 dockingLocalOffset = new Vector3(0f, -0.28f, 0f);

        [Tooltip("Rotação local sugerida ao ser suspenso pelo feixe trator")]
        [SerializeField] private Vector3 dockingLocalEuler = new Vector3(0f, 90f, 0f);

        [Header("Eventos e Callbacks")]
        public Action onFetchRequested;
        public Action onFetchCaptured;
        public Action onFetchReleased;

        // Referências e Estado Interno
        private Rigidbody rb;
        private XRGrabInteractable grabInteractable;
        private ErgonomicGrabInteractable ergonomicGrab;
        private bool isHeld = false;
        private bool isBeingFetched = false;
        private float timeSinceRelease = 0f;
        private Vector3 lastReleasePlayerPos = Vector3.zero;

        public bool IsBeingFetched => isBeingFetched;
        public bool IsHeld => isHeld;
        public Vector3 DockingLocalOffset => dockingLocalOffset;
        public Vector3 DockingLocalEuler => dockingLocalEuler;
        public Rigidbody TargetRigidbody => rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            grabInteractable = GetComponent<XRGrabInteractable>();
            ergonomicGrab = GetComponent<ErgonomicGrabInteractable>();
        }

        private void OnEnable()
        {
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.AddListener(OnGrabbed);
                grabInteractable.selectExited.AddListener(OnReleased);
            }
        }

        private void OnDisable()
        {
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.RemoveListener(OnGrabbed);
                grabInteractable.selectExited.RemoveListener(OnReleased);
            }
        }

        private void Update()
        {
            // Desativado: o Drone nunca busca maçãs nem se afasta do paciente para pegar objetos
            return;
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            isHeld = true;
            timeSinceRelease = 0f;

            if (isBeingFetched)
            {
                // Paciente pegou o item das mãos do Drone!
                NotifyReleasedFromFetch();
                if (DroneFetchController.Instance != null && DroneFetchController.Instance.CurrentTarget == this)
                {
                    DroneFetchController.Instance.OnTargetGrabbedByPlayer();
                }
            }
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            isHeld = false;
            timeSinceRelease = 0f;

            Transform playerTransform = GetPlayerTransform();
            if (playerTransform != null)
            {
                lastReleasePlayerPos = playerTransform.position;
            }
            else
            {
                lastReleasePlayerPos = transform.position;
            }
        }

        private void CheckFetchConditions()
        {
            Transform playerTransform = GetPlayerTransform();
            Vector3 originPos = playerTransform != null ? playerTransform.position : lastReleasePlayerPos;

            float distance = Vector3.Distance(transform.position, originPos);

            // Se o item está além do alcance seguro (> 2.5m) e com velocidade baixa (já parou de rolar/quicar)
            if (distance >= minFetchDistance && GetRigidbodyVelocity(rb).sqrMagnitude < 0.35f)
            {
                RequestFetch();
            }
        }

        private static Vector3 GetRigidbodyVelocity(Rigidbody r)
        {
#if UNITY_6000_0_OR_NEWER
            return r.linearVelocity;
#else
            return r.velocity;
#endif
        }

        /// <summary>
        /// Solicita explicitamente que o Drone Guia venha buscar este objeto (Neutralizado para TASK-03).
        /// </summary>
        public void RequestFetch()
        {
            // Neutralizado: o Drone é companheiro de acompanhamento e nunca se afasta do paciente para buscar itens.
            return;
        }

        /// <summary>
        /// Chamado pelo DroneFetchController quando o feixe trator prende o objeto.
        /// </summary>
        public void NotifyCapturedByFetch(Transform tractorAnchor)
        {
            isBeingFetched = true;
            if (rb != null)
            {
                rb.isKinematic = true;
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = Vector3.zero;
#else
                rb.velocity = Vector3.zero;
#endif
                rb.angularVelocity = Vector3.zero;
            }

            // Desativa temporariamente o auto-respawn do ErgonomicGrab para não interferir na busca do drone
            if (ergonomicGrab != null)
            {
                ergonomicGrab.enabled = false;
            }

            onFetchCaptured?.Invoke();
        }

        /// <summary>
        /// Chamado quando o drone solta o objeto ou o jogador o agarra.
        /// </summary>
        public void NotifyReleasedFromFetch()
        {
            isBeingFetched = false;
            timeSinceRelease = 0f;

            if (rb != null)
            {
                rb.isKinematic = false;
            }

            if (ergonomicGrab != null)
            {
                ergonomicGrab.enabled = true;
            }

            onFetchReleased?.Invoke();
        }

        private Transform GetPlayerTransform()
        {
            if (Camera.main != null) return Camera.main.transform;
            CharacterController cc = FindFirstObjectByType<CharacterController>();
            if (cc != null) return cc.transform;
            return null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.0f, 0.95f, 1.0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, minFetchDistance);
        }
    }
}
