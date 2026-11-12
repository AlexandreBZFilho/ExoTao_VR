using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;

namespace VRRehab.DronePet
{
    public enum CompanionFollowState
    {
        /// <summary>
        /// Estado 1: Estacionário a 1.6m na frente do paciente (Y ~ 1.45m), com micro-flutuação senoidal suave,
        /// permitindo leitura tranquila das instruções clínicas e métricas de telemetria sem perseguição.
        /// </summary>
        WaitingInitialRead,

        /// <summary>
        /// Estado 2: Sendo manipulado pelo jogador através do raio laser (XRGrabInteractable + allowAnchorControl).
        /// O jogador pode aproximar/afastar no analógico/scroll entre 1.2m e 4.5m, mantendo o visor virado para si.
        /// </summary>
        BeingManipulatedByPlayer,

        /// <summary>
        /// Estado 3: Acompanhamento dinâmico mantendo o vetor de deslocamento relativo memorizado no espaço Yaw
        /// do paciente. Move-se suavemente via Vector3.SmoothDamp (zero alocação de memória GC).
        /// </summary>
        FollowingPlayerAtCustomOffset
    }

    /// <summary>
    /// Controlador de Acompanhamento Ergonômico e Manipulação Espacial por Raio Laser do Drone Pet (TASK-03).
    /// Herda de XRGrabInteractable para permitir puxar, empurrar e reposicionar o Drone com o laser em tempo real,
    /// sem nunca colidir com o paciente (Rigidbody cinemático + Physics.IgnoreCollision) e sem buscar maçãs.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    [DisallowMultipleComponent]
    [SelectionBase]
    public class DroneCompanionFollowAndGrab : XRGrabInteractable
    {
        [Header("Estado Atual do Companheiro")]
        [SerializeField] private CompanionFollowState currentState = CompanionFollowState.WaitingInitialRead;
        public CompanionFollowState CurrentState => currentState;

        [Header("Parâmetros de Posicionamento Ergonômico")]
        [Tooltip("Distância horizontal inicial à frente do paciente no início da sessão (m)")]
        [SerializeField] private float initialDistance = 1.60f;

        [Tooltip("Altura de repouso inicial em relação ao solo (m)")]
        [SerializeField] private float initialHeight = 1.45f;

        [Tooltip("Distância mínima de aproximação do paciente para evitar obstrução visual")]
        [SerializeField] private float minDistance = 1.20f;

        [Tooltip("Distância máxima permitida para manter o HUD legível")]
        [SerializeField] private float maxDistance = 4.50f;

        [Tooltip("Tempo de suavização do Vector3.SmoothDamp durante a marcha")]
        [SerializeField] private float smoothTime = 0.35f;

        [Header("Offset Memorizado (Espaço Local de Rotação Yaw do Paciente)")]
        [Tooltip("Vetor de deslocamento relativo memorizado no momento em que o laser solta o Drone")]
        [SerializeField] private Vector3 offsetLocal = new Vector3(0f, 0f, 1.60f);
        public Vector3 OffsetLocal => offsetLocal;

        [Header("Recursos de Áudio e Feedback Multissensorial")]
        [Tooltip("Clip de áudio suave tocado na confirmação de ancoragem ao soltar o Drone")]
        public AudioClip sfxAnchorLock;

        [Tooltip("Volume máximo do áudio de ancoragem (confortável e não estridente)")]
        [Range(0.05f, 0.40f)]
        [SerializeField] private float anchorLockVolume = 0.18f;

        [Header("Referências")]
        [Tooltip("Transform do paciente / headset (se nulo, busca automaticamente a MainCamera / Player)")]
        public Transform patientTransform;

        // Componentes e Estado Interno
        private Rigidbody droneRb;
        private BoxCollider droneCollider;
        private AudioSource audioSourceAnchor;
        private DroneGuiaIA droneGuia;

        private Vector3 initialStationaryAnchor;
        private Vector3 initialPlayerPosition;
        private Vector3 currentSmoothVelocity = Vector3.zero;
        private bool hasInitializedPose = false;

        protected override void Awake()
        {
            base.Awake();

            // Configuração do XRGrabInteractable para manipulação VR confortável
            movementType = MovementType.Instantaneous;
            trackRotation = false; // Preserva rotação autônoma voltada para o paciente (painel legível)
            trackPosition = true;
            throwOnDetach = false; // Drone não é jogado como uma bola ao soltar
            attachEaseInTime = 0.15f;
            interactionLayers = ~0; // Aceita qualquer interação de raio laser

            ConfigurePhysics();
            EnsureAudioSource();
        }

        protected virtual void Start()
        {
            EnsurePatientReference();
            IgnorePlayerCollisions();

            if (Application.isPlaying && !hasInitializedPose)
            {
                SetupInitialStationaryPose();
            }
        }

        private void ConfigurePhysics()
        {
            droneRb = GetComponent<Rigidbody>();
            if (droneRb == null) droneRb = gameObject.AddComponent<Rigidbody>();
            droneRb.isKinematic = true;
            droneRb.useGravity = false;
            droneRb.interpolation = RigidbodyInterpolation.Interpolate;

            droneCollider = GetComponent<BoxCollider>();
            if (droneCollider == null) droneCollider = gameObject.AddComponent<BoxCollider>();
            droneCollider.isTrigger = true;
            droneCollider.center = new Vector3(0f, 0.25f, 0f);
            droneCollider.size = new Vector3(1.10f, 0.95f, 0.45f);

            // Garante registro do colisor no interator
            if (!colliders.Contains(droneCollider))
            {
                colliders.Add(droneCollider);
            }

            droneGuia = GetComponent<DroneGuiaIA>();
            if (droneGuia == null) droneGuia = GetComponentInParent<DroneGuiaIA>();
        }

        public void EnsurePatientReference()
        {
            if (patientTransform == null)
            {
                if (droneGuia != null && droneGuia.corpoPaciente != null)
                {
                    patientTransform = droneGuia.corpoPaciente;
                }
                else if (Camera.main != null)
                {
                    patientTransform = Camera.main.transform;
                }
                else
                {
                    CharacterController cc = FindFirstObjectByType<CharacterController>();
                    if (cc != null) patientTransform = cc.transform;
                }
            }
        }

        public void IgnorePlayerCollisions()
        {
            Collider[] myColliders = GetComponentsInChildren<Collider>(true);

            // Ignora CharacterControllers na cena
            CharacterController[] ccs = FindObjectsByType<CharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cc in ccs)
            {
                if (cc == null) continue;
                foreach (var col in myColliders)
                {
                    if (col != null) Physics.IgnoreCollision(col, cc, true);
                }
            }

            // Ignora colisores do corpo/mãos do paciente
            if (patientTransform != null)
            {
                foreach (var pCol in patientTransform.GetComponentsInChildren<Collider>(true))
                {
                    if (pCol == null) continue;
                    foreach (var myCol in myColliders)
                    {
                        if (myCol != null) Physics.IgnoreCollision(myCol, pCol, true);
                    }
                }
            }

            // Ignora colisores da câmera/cabeça
            if (Camera.main != null && Camera.main.transform != patientTransform)
            {
                foreach (var camCol in Camera.main.GetComponentsInChildren<Collider>(true))
                {
                    if (camCol == null) continue;
                    foreach (var myCol in myColliders)
                    {
                        if (myCol != null) Physics.IgnoreCollision(myCol, camCol, true);
                    }
                }
            }
        }

        private void EnsureAudioSource()
        {
            if (audioSourceAnchor == null)
            {
                audioSourceAnchor = gameObject.AddComponent<AudioSource>();
                audioSourceAnchor.playOnAwake = false;
                audioSourceAnchor.loop = false;
                audioSourceAnchor.spatialBlend = 1.0f; // 3D Audio
                audioSourceAnchor.minDistance = 0.8f;
                audioSourceAnchor.maxDistance = 8.0f;
                audioSourceAnchor.volume = anchorLockVolume;
            }

            if (sfxAnchorLock == null)
            {
                sfxAnchorLock = Resources.Load<AudioClip>("Audio/Generated/Task03/drone_anchor_lock");
#if UNITY_EDITOR
                if (sfxAnchorLock == null)
                {
                    sfxAnchorLock = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Generated/Task03/drone_anchor_lock.wav");
                }
#endif
            }
        }

        /// <summary>
        /// Inicializa o Drone no Estado 1: Calmo e estacionário a 1.6m na frente do paciente.
        /// </summary>
        public void SetupInitialStationaryPose()
        {
            EnsurePatientReference();
            if (patientTransform == null) return;

            Vector3 forward = GetPatientHorizontalForward();
            Vector3 startPos = patientTransform.position + forward * initialDistance;
            float groundY = GetGroundAltitude(startPos);
            startPos.y = groundY + initialHeight;

            transform.position = startPos;
            initialStationaryAnchor = startPos;
            initialPlayerPosition = patientTransform.position;
            hasInitializedPose = true;

            offsetLocal = new Vector3(0f, 0f, initialDistance);
            currentState = CompanionFollowState.WaitingInitialRead;

            OrientTowardsPatient(true);
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);

            currentState = CompanionFollowState.BeingManipulatedByPlayer;

            if (droneGuia != null)
            {
                droneGuia.estaArrastandoDrone = true;
            }

            // Pulso háptico suave na pegada
            if (args.interactorObject is XRBaseControllerInteractor ctrl)
            {
                ctrl.SendHapticImpulse(0.15f, 0.08f);
            }
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);

            EnsurePatientReference();

            if (patientTransform != null)
            {
                // Calcula e memoriza o vetor de deslocamento relativo no espaço local de rotação yaw do paciente:
                // offsetLocal = Quaternion.Inverse(Quaternion.Euler(0, playerTransform.eulerAngles.y, 0)) * (transform.position - playerTransform.position);
                Vector3 deltaGlobal = transform.position - patientTransform.position;
                Quaternion playerYawRot = Quaternion.Euler(0f, patientTransform.eulerAngles.y, 0f);
                offsetLocal = Quaternion.Inverse(playerYawRot) * deltaGlobal;

                // Clampa distância horizontal no plano XZ entre 1.2m e 4.5m
                Vector2 xz = new Vector2(offsetLocal.x, offsetLocal.z);
                float dist = xz.magnitude;
                if (dist < minDistance)
                {
                    xz = (dist < 0.01f ? Vector2.up : xz.normalized) * minDistance;
                    offsetLocal.x = xz.x;
                    offsetLocal.z = xz.y;
                }
                else if (dist > maxDistance)
                {
                    xz = xz.normalized * maxDistance;
                    offsetLocal.x = xz.x;
                    offsetLocal.z = xz.y;
                }

                // Clampa altura relativa
                offsetLocal.y = Mathf.Clamp(offsetLocal.y, -0.65f, 1.60f);
            }

            currentState = CompanionFollowState.FollowingPlayerAtCustomOffset;

            if (droneGuia != null)
            {
                droneGuia.estaArrastandoDrone = false;
                droneGuia.offsetLocalPaciente = offsetLocal;
                droneGuia.posicaoPersonalizadaPeloJogador = true;
            }

            // Toca áudio suave de confirmação de ancoragem (drone_anchor_lock.wav)
            PlayAnchorLockAudio();

            // Emite pulso háptico suave na soltura
            if (args.interactorObject is XRBaseControllerInteractor ctrl)
            {
                ctrl.SendHapticImpulse(0.18f, 0.12f);
            }
        }

        private void PlayAnchorLockAudio()
        {
            if (sfxAnchorLock != null && audioSourceAnchor != null)
            {
                audioSourceAnchor.pitch = UnityEngine.Random.Range(0.98f, 1.02f);
                audioSourceAnchor.PlayOneShot(sfxAnchorLock, anchorLockVolume);
            }
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            if (patientTransform == null)
            {
                EnsurePatientReference();
                if (patientTransform == null) return;
            }

            switch (currentState)
            {
                case CompanionFollowState.WaitingInitialRead:
                    UpdateWaitingInitialRead();
                    break;

                case CompanionFollowState.BeingManipulatedByPlayer:
                    UpdateBeingManipulated();
                    break;

                case CompanionFollowState.FollowingPlayerAtCustomOffset:
                    UpdateFollowingCustomOffset();
                    break;
            }
        }

        private void UpdateWaitingInitialRead()
        {
            // Transita para acompanhamento dinâmico se o paciente iniciar caminhada
            bool isWalking = CheckPatientWalking();
            float distMoved = Vector3.Distance(
                new Vector3(patientTransform.position.x, 0f, patientTransform.position.z),
                new Vector3(initialPlayerPosition.x, 0f, initialPlayerPosition.z)
            );

            if (isWalking || distMoved > 0.45f)
            {
                offsetLocal = new Vector3(0f, 0f, initialDistance);
                currentState = CompanionFollowState.FollowingPlayerAtCustomOffset;
                return;
            }

            // Flutuação senoidal suave mantendo posição inicial calma
            float bob = Mathf.Sin(Time.time * 1.8f) * 0.025f;
            transform.position = initialStationaryAnchor + Vector3.up * bob;

            OrientTowardsPatient(false);
        }

        private void UpdateBeingManipulated()
        {
            // Suporte ergonômico a scroll do mouse no Editor para testes rápidos
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                Vector3 dir = (transform.position - patientTransform.position).normalized;
                transform.position += dir * (scroll * 0.35f);
            }

            // Clampa distância em relação ao paciente entre min e max
            Vector3 delta = transform.position - patientTransform.position;
            Vector3 deltaHoriz = new Vector3(delta.x, 0f, delta.z);
            float dist = deltaHoriz.magnitude;
            if (dist < minDistance && dist > 0.001f)
            {
                deltaHoriz = deltaHoriz.normalized * minDistance;
                transform.position = new Vector3(patientTransform.position.x + deltaHoriz.x, transform.position.y, patientTransform.position.z + deltaHoriz.z);
            }
            else if (dist > maxDistance)
            {
                deltaHoriz = deltaHoriz.normalized * maxDistance;
                transform.position = new Vector3(patientTransform.position.x + deltaHoriz.x, transform.position.y, patientTransform.position.z + deltaHoriz.z);
            }

            // Clampa altitude em relação ao solo para não atravessar o chão
            float groundY = GetGroundAltitude(transform.position);
            float clampedY = Mathf.Clamp(transform.position.y, groundY + 0.65f, groundY + 2.80f);
            if (Mathf.Abs(transform.position.y - clampedY) > 0.01f)
            {
                transform.position = new Vector3(transform.position.x, clampedY, transform.position.z);
            }

            // Drone mantém orientação voltada para o paciente (trackRotation = false)
            OrientTowardsPatient(false);
        }

        private void UpdateFollowingCustomOffset()
        {
            // Calcula a posição alvo no mundo a partir da rotação Yaw do paciente e do offset memorizado
            Quaternion playerYaw = Quaternion.Euler(0f, patientTransform.eulerAngles.y, 0f);
            Vector3 targetPos = patientTransform.position + playerYaw * offsetLocal;

            // Respeita o solo e aplica suave micro-bobbing
            float groundY = GetGroundAltitude(targetPos);
            float bob = Mathf.Sin(Time.time * 2.0f) * 0.025f;
            float targetY = Mathf.Max(groundY + 0.65f, targetPos.y + bob);
            targetPos.y = targetY;

            // Movimento suave via Vector3.SmoothDamp (zero GC Alloc)
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref currentSmoothVelocity, smoothTime);

            // Rotação suave olhando para o paciente
            OrientTowardsPatient(false);
        }

        private void OrientTowardsPatient(bool instant)
        {
            if (patientTransform == null) return;

            Vector3 dir = patientTransform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = instant ? targetRot : Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 6.0f);
            }
        }

        private bool CheckPatientWalking()
        {
            if (droneGuia != null)
            {
                if (droneGuia.receptorExo != null && droneGuia.receptorExo.PacienteCaminhando)
                    return true;
                if (droneGuia.telemetria != null && droneGuia.telemetria.velocidadeAtualMs > 0.12f)
                    return true;
            }
            return false;
        }

        private Vector3 GetPatientHorizontalForward()
        {
            Vector3 fwd = patientTransform != null ? patientTransform.forward : Vector3.forward;
            fwd.y = 0f;
            return fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;
        }

        private float GetGroundAltitude(Vector3 pos)
        {
            float refY = patientTransform != null ? patientTransform.position.y : pos.y;
            Vector3 origin = new Vector3(pos.x, refY + 2.5f, pos.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }
            return refY;
        }

        /// <summary>
        /// Permite redefinir programaticamente a distância do drone (ex: via atalho de acessibilidade).
        /// </summary>
        public void SetCustomDistance(float distanceMeters)
        {
            float d = Mathf.Clamp(distanceMeters, minDistance, maxDistance);
            Vector2 xz = new Vector2(offsetLocal.x, offsetLocal.z);
            if (xz.sqrMagnitude < 0.01f) xz = Vector2.up;
            xz = xz.normalized * d;
            offsetLocal = new Vector3(xz.x, offsetLocal.y, xz.y);

            if (droneGuia != null)
            {
                droneGuia.offsetLocalPaciente = offsetLocal;
                droneGuia.posicaoPersonalizadaPeloJogador = true;
            }
        }
    }
}
