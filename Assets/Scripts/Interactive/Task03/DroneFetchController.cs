using System;
using System.Collections;
using UnityEngine;
using VRRehab.Core;

namespace VRRehab.DronePet
{
    public enum DroneFetchState
    {
        Idle,
        Alert,
        FlyingToTarget,
        TractorBeamEngage,
        CarryingBack,
        PresentingAtChestHeight,
        Release
    }

    /// <summary>
    /// Controlador autônomo da máquina de estados de busca e feixe trator do Drone Pet ("FetchBot").
    /// Acoplado ao Drone Guia, detecta quando objetos são arremessados longe, voa até eles,
    /// ativa o feixe trator cônico luminoso e entrega o item de volta na altura ergonômica
    /// ideal do exoesqueleto (1.15m acima do solo).
    /// </summary>
    [DisallowMultipleComponent]
    public class DroneFetchController : MonoBehaviour
    {
        public static DroneFetchController Instance { get; private set; }

        [Header("Estado Atual do FetchBot")]
        [SerializeField] private DroneFetchState currentState = DroneFetchState.Idle;
        public DroneFetchState CurrentState => currentState;

        [Header("Pontos de Ancoragem e Fuselagem")]
        [Tooltip("Transform do módulo emissor na base inferior do drone (onde o feixe trator se origina)")]
        public Transform tractorEmitterAnchor;

        [Tooltip("Transform do paciente / headset (se nulo, busca automaticamente a MainCamera)")]
        public Transform patientAnchor;

        [Header("Parâmetros de Voo e Cinemática")]
        [Tooltip("Velocidade de cruzeiro do drone durante a busca")]
        [SerializeField] private float flightSpeed = 3.2f;

        [Tooltip("Suavidade de rotação ao se alinhar com alvos")]
        [SerializeField] private float rotationSpeed = 6.0f;

        [Tooltip("Altura em que o drone paira diretamente acima do objeto para disparar o feixe trator")]
        [SerializeField] private float hoverHeightAboveTarget = 0.65f;

        [Tooltip("Altura ergonômica em relação ao solo para entrega no peito do paciente (ex: 1.15m)")]
        [SerializeField] private float presentationHeight = 1.15f;

        [Tooltip("Distância à frente do peito do paciente para apresentar o item")]
        [SerializeField] private float presentationDistanceFront = 0.65f;

        [Header("Recursos de Áudio")]
        [Tooltip("Clip de áudio de alerta/chirp alegre do drone ao identificar o objeto")]
        public AudioClip sfxFetchAlert;

        [Tooltip("Clip de zumbido eletromagnético contínuo do feixe trator ativo")]
        public AudioClip sfxTractorBeamHum;

        // Subcomponentes e Visual do Feixe Trator
        private LineRenderer beamLineRenderer;
        private Light beamLight;
        private AudioSource audioSourceAlert;
        private AudioSource audioSourceHum;
        private DroneGuiaIA droneGuia;
        private DroneFetchTarget currentTarget;
        private Coroutine fetchRoutine;

        public DroneFetchTarget CurrentTarget => currentTarget;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            droneGuia = GetComponent<DroneGuiaIA>();
            if (droneGuia == null)
            {
                droneGuia = GetComponentInParent<DroneGuiaIA>();
            }

            SetupTractorBeamHardware();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            EnsureAudioSources();
            EnsurePatientAnchor();
        }

        public bool CanAcceptFetch()
        {
            return currentState == DroneFetchState.Idle;
        }

        public void EnsurePatientAnchor()
        {
            if (patientAnchor == null)
            {
                if (Camera.main != null)
                {
                    patientAnchor = Camera.main.transform;
                }
                else
                {
                    CharacterController cc = FindFirstObjectByType<CharacterController>();
                    if (cc != null) patientAnchor = cc.transform;
                }
            }
        }

        /// <summary>
        /// Monta e configura a estrutura física do feixe trator na base do Drone.
        /// </summary>
        public void SetupTractorBeamHardware()
        {
            if (tractorEmitterAnchor == null)
            {
                Transform found = transform.Find("TractorBeamEmitter_Anchor");
                if (found == null)
                {
                    GameObject goAnchor = new GameObject("TractorBeamEmitter_Anchor");
                    goAnchor.transform.SetParent(transform, false);
                    // Posição central na base inferior da fuselagem
                    goAnchor.transform.localPosition = new Vector3(0f, -0.06f, 0f);
                    goAnchor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Apontando para baixo (-Y mundo)
                    tractorEmitterAnchor = goAnchor.transform;
                }
                else
                {
                    tractorEmitterAnchor = found;
                }
            }

            // Garante o LineRenderer cônico do feixe trator
            beamLineRenderer = tractorEmitterAnchor.GetComponent<LineRenderer>();
            if (beamLineRenderer == null)
            {
                beamLineRenderer = tractorEmitterAnchor.gameObject.AddComponent<LineRenderer>();
            }

            beamLineRenderer.useWorldSpace = true;
            beamLineRenderer.positionCount = 2;
            beamLineRenderer.startWidth = 0.035f;
            beamLineRenderer.endWidth = 0.095f;
            beamLineRenderer.startColor = new Color(0.2f, 0.98f, 1.0f, 0.85f);
            beamLineRenderer.endColor = new Color(0.0f, 0.85f, 1.0f, 0.25f);
            beamLineRenderer.enabled = false;

            // Material emissivo ciano holográfico aditivo sem shader rosa
            Material matBeam = MaterialShaderHelper.CreateBeamGlowMaterial(
                "M_TractorBeam_ConeGlow",
                new Color(0.0f, 0.94f, 1.0f, 0.45f)
            );
            beamLineRenderer.material = matBeam;

            // Luz pontual concentrada do feixe
            beamLight = tractorEmitterAnchor.GetComponent<Light>();
            if (beamLight == null)
            {
                beamLight = tractorEmitterAnchor.gameObject.AddComponent<Light>();
            }
            beamLight.type = LightType.Spot;
            beamLight.spotAngle = 48f;
            beamLight.range = 3.5f;
            beamLight.color = new Color(0.0f, 0.96f, 1.0f);
            beamLight.intensity = 2.5f;
            beamLight.enabled = false;
        }

        private void EnsureAudioSources()
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            if (sources.Length >= 2)
            {
                audioSourceAlert = sources[0];
                audioSourceHum = sources[1];
            }
            else
            {
                if (audioSourceAlert == null) audioSourceAlert = gameObject.AddComponent<AudioSource>();
                if (audioSourceHum == null) audioSourceHum = gameObject.AddComponent<AudioSource>();
            }

            audioSourceAlert.playOnAwake = false;
            audioSourceAlert.spatialBlend = 0.7f;
            audioSourceAlert.volume = 0.85f;

            audioSourceHum.playOnAwake = false;
            audioSourceHum.loop = true;
            audioSourceHum.spatialBlend = 0.8f;
            audioSourceHum.volume = 0.55f;

            if (sfxFetchAlert == null)
            {
                sfxFetchAlert = Resources.Load<AudioClip>("Audio/Generated/Task03/drone_fetch_alert");
#if UNITY_EDITOR
                if (sfxFetchAlert == null)
                {
                    sfxFetchAlert = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Generated/Task03/drone_fetch_alert.wav");
                }
#endif
            }

            if (sfxTractorBeamHum == null)
            {
                sfxTractorBeamHum = Resources.Load<AudioClip>("Audio/Generated/Task03/tractor_beam_hum");
#if UNITY_EDITOR
                if (sfxTractorBeamHum == null)
                {
                    sfxTractorBeamHum = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Generated/Task03/tractor_beam_hum.wav");
                }
#endif
            }
        }

        /// <summary>
        /// Inicia a missão de busca para o alvo indicado.
        /// </summary>
        public void RequestFetch(DroneFetchTarget target)
        {
            if (target == null || currentState != DroneFetchState.Idle) return;

            currentTarget = target;
            if (fetchRoutine != null) StopCoroutine(fetchRoutine);
            fetchRoutine = StartCoroutine(FetchRoutine(target));
        }

        private IEnumerator FetchRoutine(DroneFetchTarget target)
        {
            EnsurePatientAnchor();

            // 1. ESTADO: ALERT
            currentState = DroneFetchState.Alert;
            if (droneGuia != null)
            {
                droneGuia.sobControleFetch = true;
                droneGuia.DefinirMensagemPersonalizada("OBJETO DETECTADO!\nVou buscar com o Feixe Trator!", new Color(0.0f, 0.95f, 1.0f));
            }

            if (audioSourceAlert != null && sfxFetchAlert != null)
            {
                audioSourceAlert.PlayOneShot(sfxFetchAlert);
            }

            // Gira suavemente na direção do alvo
            Vector3 dirAlvo = (target.transform.position - transform.position).normalized;
            dirAlvo.y = 0;
            if (dirAlvo.sqrMagnitude > 0.01f)
            {
                Quaternion rotAlvo = Quaternion.LookRotation(dirAlvo, Vector3.up);
                float alertTimer = 0f;
                while (alertTimer < 0.45f)
                {
                    alertTimer += Time.deltaTime;
                    transform.rotation = Quaternion.Slerp(transform.rotation, rotAlvo, Time.deltaTime * rotationSpeed);
                    yield return null;
                }
            }

            // 2. ESTADO: FLYING TO TARGET
            currentState = DroneFetchState.FlyingToTarget;
            while (target != null && target.IsBeingFetched)
            {
                Vector3 posHoverAlvo = target.transform.position + Vector3.up * hoverHeightAboveTarget;
                float distHover = Vector3.Distance(transform.position, posHoverAlvo);

                if (distHover < 0.15f) break;

                // Move suavemente até a posição acima do objeto
                transform.position = Vector3.MoveTowards(transform.position, posHoverAlvo, flightSpeed * Time.deltaTime);

                Vector3 dirLook = (target.transform.position - transform.position).normalized;
                dirLook.y = 0;
                if (dirLook.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dirLook, Vector3.up), Time.deltaTime * rotationSpeed);
                }

                yield return null;
            }

            if (target == null)
            {
                FinalizeFetch();
                yield break;
            }

            // 3. ESTADO: TRACTOR BEAM ENGAGE
            currentState = DroneFetchState.TractorBeamEngage;
            if (droneGuia != null)
            {
                droneGuia.DefinirMensagemPersonalizada("FEIXE TRATOR ACOPLADO!\nLevantando objeto...", new Color(0.1f, 0.95f, 0.7f));
            }

            SetBeamActive(true, target.transform.position);

            if (audioSourceHum != null && sfxTractorBeamHum != null)
            {
                audioSourceHum.clip = sfxTractorBeamHum;
                audioSourceHum.Play();
            }

            // Conecta o feixe e levita o objeto em direção à base inferior do drone
            target.NotifyCapturedByFetch(tractorEmitterAnchor);

            Vector3 dockTargetPos = tractorEmitterAnchor.position + target.DockingLocalOffset;
            Quaternion dockTargetRot = Quaternion.Euler(target.DockingLocalEuler);

            float liftTimer = 0f;
            float liftDuration = 0.90f;
            Vector3 startObjPos = target.transform.position;
            Quaternion startObjRot = target.transform.rotation;

            while (liftTimer < liftDuration)
            {
                liftTimer += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, liftTimer / liftDuration);

                dockTargetPos = tractorEmitterAnchor.position + target.DockingLocalOffset;
                target.transform.position = Vector3.Lerp(startObjPos, dockTargetPos, t);
                target.transform.rotation = Quaternion.Slerp(startObjRot, dockTargetRot, t);

                UpdateBeamEndpoints(target.transform.position);
                yield return null;
            }

            // Prende como filho relativo temporário para o voo de volta
            target.transform.position = tractorEmitterAnchor.position + target.DockingLocalOffset;
            target.transform.rotation = dockTargetRot;

            // 4. ESTADO: CARRYING BACK
            currentState = DroneFetchState.CarryingBack;
            if (droneGuia != null)
            {
                droneGuia.DefinirMensagemPersonalizada("TRAZENDO DE VOLTA!\nPrepare suas mãos!", new Color(0.0f, 0.95f, 1.0f));
            }

            while (target != null && target.IsBeingFetched)
            {
                EnsurePatientAnchor();
                Vector3 playerPos = patientAnchor != null ? patientAnchor.position : Vector3.zero;
                Vector3 playerForward = patientAnchor != null ? patientAnchor.forward : Vector3.forward;
                playerForward.y = 0;
                playerForward.Normalize();

                // Destino ergonômico: à frente do peito do paciente a 1.15m de altura do solo
                float groundY = GetGroundAltitude(playerPos);
                Vector3 deliveryPos = playerPos + playerForward * presentationDistanceFront;
                deliveryPos.y = groundY + presentationHeight;

                float distDelivery = Vector3.Distance(transform.position, deliveryPos);
                if (distDelivery < 0.12f) break;

                transform.position = Vector3.MoveTowards(transform.position, deliveryPos, flightSpeed * Time.deltaTime);

                // Mantém o item posicionado exatamente abaixo do feixe durante o voo
                target.transform.position = tractorEmitterAnchor.position + target.DockingLocalOffset;
                target.transform.rotation = dockTargetRot;
                UpdateBeamEndpoints(target.transform.position);

                // Drone vira de frente para o paciente
                Vector3 dirToPlayer = (playerPos - transform.position).normalized;
                dirToPlayer.y = 0;
                if (dirToPlayer.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dirToPlayer, Vector3.up), Time.deltaTime * rotationSpeed);
                }

                yield return null;
            }

            // 5. ESTADO: PRESENTING AT CHEST HEIGHT
            currentState = DroneFetchState.PresentingAtChestHeight;
            if (droneGuia != null)
            {
                droneGuia.DefinirMensagemPersonalizada("AQUI ESTÁ SEU BASTÃO!\nPode pegar ou arremessar de novo!", new Color(0.3f, 1.0f, 0.4f));
            }

            float presentTimer = 0f;
            float maxPresentDuration = 15.0f; // Se o jogador não pegar em 15s, solta suavemente na mesa/chão

            while (presentTimer < maxPresentDuration && target != null && target.IsBeingFetched)
            {
                presentTimer += Time.deltaTime;

                // Suave flutuação harmônica (bobbing)
                float bob = Mathf.Sin(Time.time * 2.5f) * 0.025f;

                EnsurePatientAnchor();
                Vector3 playerPos = patientAnchor != null ? patientAnchor.position : Vector3.zero;
                Vector3 playerForward = patientAnchor != null ? patientAnchor.forward : Vector3.forward;
                playerForward.y = 0;
                playerForward.Normalize();

                float groundY = GetGroundAltitude(playerPos);
                Vector3 hoverChestPos = playerPos + playerForward * presentationDistanceFront;
                hoverChestPos.y = groundY + presentationHeight + bob;

                transform.position = Vector3.Lerp(transform.position, hoverChestPos, Time.deltaTime * 3.5f);

                target.transform.position = tractorEmitterAnchor.position + target.DockingLocalOffset;
                target.transform.rotation = dockTargetRot;
                UpdateBeamEndpoints(target.transform.position);

                yield return null;
            }

            // Se o tempo expirou sem o jogador pegar, solta no estado Release
            if (currentState == DroneFetchState.PresentingAtChestHeight)
            {
                ReleaseTargetGracefully();
            }
        }

        /// <summary>
        /// Chamado quando o jogador agarra o item sendo apresentado.
        /// </summary>
        public void OnTargetGrabbedByPlayer()
        {
            if (currentState != DroneFetchState.Idle)
            {
                StartCoroutine(ReleaseRoutine());
            }
        }

        public void ReleaseTargetGracefully()
        {
            StartCoroutine(ReleaseRoutine());
        }

        private IEnumerator ReleaseRoutine()
        {
            currentState = DroneFetchState.Release;

            SetBeamActive(false, Vector3.zero);

            if (audioSourceHum != null && audioSourceHum.isPlaying)
            {
                audioSourceHum.Stop();
            }

            if (currentTarget != null)
            {
                currentTarget.NotifyReleasedFromFetch();
                currentTarget = null;
            }

            if (droneGuia != null)
            {
                droneGuia.DefinirMensagemPersonalizada("EXCELENTE ARREMESSO!\nContinue a marcha quando quiser.", new Color(0.0f, 0.96f, 0.83f));
            }

            yield return new WaitForSeconds(1.2f);

            FinalizeFetch();
        }

        private void FinalizeFetch()
        {
            currentState = DroneFetchState.Idle;
            SetBeamActive(false, Vector3.zero);

            if (audioSourceHum != null && audioSourceHum.isPlaying)
            {
                audioSourceHum.Stop();
            }

            if (droneGuia != null)
            {
                droneGuia.sobControleFetch = false;
                droneGuia.RestaurarMensagemEstado();
            }

            currentTarget = null;
            fetchRoutine = null;
        }

        private void SetBeamActive(bool active, Vector3 targetPoint)
        {
            if (beamLineRenderer != null)
            {
                beamLineRenderer.enabled = active;
                if (active)
                {
                    beamLineRenderer.SetPosition(0, tractorEmitterAnchor.position);
                    beamLineRenderer.SetPosition(1, targetPoint);
                }
            }

            if (beamLight != null)
            {
                beamLight.enabled = active;
            }
        }

        private void UpdateBeamEndpoints(Vector3 targetPoint)
        {
            if (beamLineRenderer != null && beamLineRenderer.enabled && tractorEmitterAnchor != null)
            {
                beamLineRenderer.SetPosition(0, tractorEmitterAnchor.position);
                beamLineRenderer.SetPosition(1, targetPoint);
            }
        }

        private float GetGroundAltitude(Vector3 pos)
        {
            Vector3 origin = new Vector3(pos.x, pos.y + 2.0f, pos.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }
            return pos.y;
        }

        #if UNITY_EDITOR
        [ContextMenu("Debug: Iniciar Busca do Bastão")]
        public void DebugFetchBaton()
        {
            DroneFetchTarget batonTarget = FindFirstObjectByType<DroneFetchTarget>();
            if (batonTarget != null)
            {
                RequestFetch(batonTarget);
            }
            else
            {
                Debug.LogWarning("[DroneFetchController] Nenhum DroneFetchTarget encontrado na cena para teste.");
            }
        }
        #endif
    }
}
