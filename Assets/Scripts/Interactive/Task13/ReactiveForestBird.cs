using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;

namespace VRRehab.Interactive.Task13
{
    /// <summary>
    /// Pássaro Guia da Floresta Interativo.
    /// Pousado em mourões ou postes de trilha. Ao receber foco do laser VR,
    /// toque ou detecção de aproximação do paciente, gorjeia alegremente,
    /// bate as asas proceduralmente e voa em curva Bezier suave até o próximo
    /// marco do percurso, guiando o paciente ao longo da caminhada.
    ///
    /// HOTFIX DO PLAYTEST (pássaro "gritando sem parar"): no último poleiro o pio era tocado a CADA FRAME enquanto
    /// o paciente estava perto, em volume 1.0. Agora todo som passa por TryChirp(): um único pio curto e suave por
    /// encontro, com cooldown rígido de 15 s, volume baixo, sem loop, audível só até 10 m.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class ReactiveForestBird : MonoBehaviour
    {
        public enum BirdState
        {
            Perched,
            Alerted,
            Flying,
            Landing
        }

        [Header("Hierarquia do Pássaro")]
        [SerializeField] private Transform wingLeft;
        [SerializeField] private Transform wingRight;
        [SerializeField] private Transform birdBody;

        [Header("Áudio")]
        [SerializeField] private AudioClip chirpSfx;
        [SerializeField] private AudioClip flutterSfx;
        [SerializeField] private AudioSource audioSource;
        [Tooltip("Volume máximo do pio (o AudioSource inteiro fica limitado a isso).")]
        [Range(0.05f, 0.18f)] [SerializeField] private float maxChirpVolume = 0.16f;
        [Tooltip("Volume do bater de asas, relativo ao volume máximo (0 = voo silencioso).")]
        [Range(0f, 1f)] [SerializeField] private float flutterRelativeVolume = 0.6f;
        [Tooltip("Tempo mínimo (s) entre dois pios, aconteça o que acontecer.")]
        [SerializeField] private float chirpCooldown = 15.0f;

        [Header("Waypoints de Voo (Poleiros)")]
        [SerializeField] private Transform[] perchTransforms;
        [SerializeField] private Vector3[] perchPositions;
        [SerializeField] private Quaternion[] perchRotations;
        [SerializeField] private int currentWaypointIndex = 0;

        [Header("Parâmetros de Voo Procedural")]
        [SerializeField] private float flightSpeed = 3.6f;
        [SerializeField] private float flightArcHeight = 1.4f;
        [SerializeField] private float flapFrequency = 24.0f;
        [SerializeField] private float maxFlapAngle = 42.0f;

        [Header("Interatividade & Proximidade")]
        [SerializeField] private bool triggerOnProximity = true;
        [SerializeField] private float proximityTriggerDist = 3.2f;
        [Tooltip("Além desta distância (m) do paciente o pássaro não roda lógica nenhuma por frame.")]
        [SerializeField] private float activeRadius = 15.0f;

        [Header("Estado do Encontro (somente leitura)")]
        [Tooltip("Já reagiu a esta aproximação do paciente; só rearma quando ele se afasta.")]
        [SerializeField] private bool hasChirpedThisEncounter = false;

        private XRSimpleInteractable simpleInteractable;
        private BirdState currentState = BirdState.Perched;
        private Quaternion initialWingLRot = Quaternion.identity;
        private Quaternion initialWingRRot = Quaternion.identity;
        private Quaternion initialBodyRot = Quaternion.identity;

        private Transform playerCameraTransform;
        private Coroutine flightCoroutine;
        private float nextIdleActionTime = 0f;
        private float wingFlapPhase = 0f;
        private float lastChirpTime = -1000f;
        private float now;
        private float nextCameraSearchTime;

        /// <summary>Quantos pios já foram tocados (diagnóstico / testes).</summary>
        public int ChirpCount { get; private set; }
        public bool HasChirpedThisEncounter => hasChirpedThisEncounter;

        public BirdState State => currentState;
        public int CurrentWaypointIndex => currentWaypointIndex;

        private void Awake()
        {
            if (wingLeft == null) wingLeft = transform.Find("Wing_L");
            if (wingRight == null) wingRight = transform.Find("Wing_R");
            if (birdBody == null) birdBody = transform.Find("Bird_Body");

            if (wingLeft != null) initialWingLRot = wingLeft.localRotation;
            if (wingRight != null) initialWingRRot = wingRight.localRotation;
            if (birdBody != null) initialBodyRot = birdBody.localRotation;

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            }

            ConfigureAudioSource();

            simpleInteractable = GetComponent<XRSimpleInteractable>();
            if (simpleInteractable == null)
            {
                simpleInteractable = gameObject.AddComponent<XRSimpleInteractable>();
            }

            simpleInteractable.hoverEntered.AddListener(OnXRIHoverEntered);
            simpleInteractable.selectEntered.AddListener(OnXRISelectEntered);

            nextIdleActionTime = Time.time + Random.Range(1.5f, 3.5f);
        }

        private void Start()
        {
            FindPlayerCamera();
        }

        /// <summary>Áudio confortável: sem loop, volume baixo, 3D, inaudível além de 10 m.</summary>
        public void ConfigureAudioSource()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null) return;
            audioSource.loop = false;
            audioSource.playOnAwake = false;
            audioSource.clip = null;
            audioSource.volume = Mathf.Min(maxChirpVolume, 0.18f);
            audioSource.spatialBlend = 1.0f;
            audioSource.dopplerLevel = 0f;
            audioSource.minDistance = 1.0f;
            audioSource.maxDistance = 10.0f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        }

        private void FindPlayerCamera()
        {
            Camera mainCam = Camera.main;
            if (mainCam != null) playerCameraTransform = mainCam.transform;
        }

        /// <summary>
        /// ÚNICO lugar que toca o pio. Respeita o cooldown rígido e nunca toca durante o voo.
        /// </summary>
        private bool TryChirp()
        {
            if (now < lastChirpTime + chirpCooldown || currentState == BirdState.Flying || currentState == BirdState.Landing) return false;
            if (chirpSfx == null || audioSource == null) return false;

            lastChirpTime = now;
            ChirpCount++;
            audioSource.pitch = Random.Range(0.98f, 1.04f);
            audioSource.PlayOneShot(chirpSfx, 1.0f); // o teto é o volume do AudioSource (maxChirpVolume)
            return true;
        }

        private void OnDestroy()
        {
            if (simpleInteractable != null)
            {
                simpleInteractable.hoverEntered.RemoveListener(OnXRIHoverEntered);
                simpleInteractable.selectEntered.RemoveListener(OnXRISelectEntered);
            }
        }

        public void Configure(Transform wL, Transform wR, Transform bBody, AudioClip chirp, AudioClip flutter, Vector3[] waypoints, Quaternion[] rotations)
        {
            wingLeft = wL;
            wingRight = wR;
            birdBody = bBody;
            chirpSfx = chirp;
            flutterSfx = flutter;
            perchPositions = waypoints;
            perchRotations = rotations;

            if (wingLeft != null) initialWingLRot = wingLeft.localRotation;
            if (wingRight != null) initialWingRRot = wingRight.localRotation;
            if (birdBody != null) initialBodyRot = birdBody.localRotation;

            if (perchPositions != null && perchPositions.Length > 0)
            {
                transform.position = perchPositions[0];
                if (perchRotations != null && perchRotations.Length > 0)
                {
                    transform.rotation = perchRotations[0];
                }
            }
        }

        private void OnXRIHoverEntered(HoverEnterEventArgs args)
        {
            ReactToPatient(args.interactorObject);
        }

        private void OnXRISelectEntered(SelectEnterEventArgs args)
        {
            ReactToPatient(args.interactorObject);
        }

        /// <summary>Reação única a uma aproximação/laser: no máximo um pio (se o cooldown permitir) e voa ao próximo poleiro.</summary>
        private void ReactToPatient(IXRInteractor interactor)
        {
            // Um encontro = uma reação. Laser/toque repetidos sem o paciente se afastar não disparam nada de novo.
            if (currentState != BirdState.Perched || hasChirpedThisEncounter) return;
            hasChirpedThisEncounter = true;
            TryFlyToNextWaypoint(interactor);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasChirpedThisEncounter) return;
            string n = other.name;
            if (n.IndexOf("hand", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("controller", System.StringComparison.OrdinalIgnoreCase) >= 0 || other.CompareTag("Player"))
            {
                ReactToPatient(null);
            }
        }

        private void Update()
        {
            now = Time.time;

            if (currentState == BirdState.Flying)
            {
                AnimateWingsFlapping();
                return;
            }
            if (currentState != BirdState.Perched) return;

            if (playerCameraTransform == null)
            {
                if (now < nextCameraSearchTime) return;
                nextCameraSearchTime = now + 2f;
                FindPlayerCamera();
                if (playerCameraTransform == null) return;
            }

            StepPerched(now, playerCameraTransform.position);
        }

        /// <summary>
        /// Lógica do pássaro pousado para um instante e uma posição do paciente (chamada pelo Update;
        /// exposta para testes de editor, onde o tempo é simulado).
        /// </summary>
        public void StepPerched(float time, Vector3 playerPosition)
        {
            now = time;
            if (currentState != BirdState.Perched) return;

            float distSq = (playerPosition - transform.position).sqrMagnitude;
            if (distSq > activeRadius * activeRadius) return; // longe: nenhuma lógica por frame

            HandlePerchedIdle();
            if (!triggerOnProximity) return;

            float rearm = proximityTriggerDist * 2f;
            if (distSq > rearm * rearm)
            {
                hasChirpedThisEncounter = false; // o paciente se afastou: o próximo encontro pode reagir de novo
                return;
            }

            if (!hasChirpedThisEncounter && distSq <= proximityTriggerDist * proximityTriggerDist)
            {
                ReactToPatient(null);
            }
        }

        private void HandlePerchedIdle()
        {
            // Respiração sutil do peito
            float breath = Mathf.Sin(now * 2.5f) * 0.02f;
            if (birdBody != null)
            {
                birdBody.localScale = new Vector3(1.0f + breath, 1.0f + breath, 1.0f - breath);
            }

            // Ações aleatórias de curiosidade (olhar para os lados, mexer a cabeça)
            if (now >= nextIdleActionTime)
            {
                nextIdleActionTime = now + Random.Range(2.0f, 4.5f);
                float randomYaw = Random.Range(-35f, 35f);
                float randomPitch = Random.Range(-10f, 15f);
                if (birdBody != null)
                {
                    birdBody.localRotation = initialBodyRot * Quaternion.Euler(randomPitch, randomYaw, 0f);
                }
            }
        }

        private void AnimateWingsFlapping()
        {
            wingFlapPhase += Time.deltaTime * flapFrequency;
            float flapAngle = Mathf.Sin(wingFlapPhase) * maxFlapAngle;

            if (wingLeft != null)
            {
                // Bate a asa esquerda para cima e para baixo rotacionando no ombro
                wingLeft.localRotation = initialWingLRot * Quaternion.Euler(flapAngle, 0f, flapAngle * 0.6f);
            }

            if (wingRight != null)
            {
                // Bate a asa direita espelhada
                wingRight.localRotation = initialWingRRot * Quaternion.Euler(-flapAngle, 0f, -flapAngle * 0.6f);
            }
        }

        private void ResetWingsToFolded()
        {
            if (wingLeft != null) wingLeft.localRotation = initialWingLRot;
            if (wingRight != null) wingRight.localRotation = initialWingRRot;
            if (birdBody != null)
            {
                birdBody.localRotation = initialBodyRot;
                birdBody.localScale = Vector3.one;
            }
        }

        /// <summary>
        /// Aciona a transição de voo para o próximo poleiro na trilha.
        /// </summary>
        public void TryFlyToNextWaypoint(IXRInteractor interactor = null)
        {
            if (currentState != BirdState.Perched) return;

            int targetIndex = currentWaypointIndex + 1;
            int totalWaypoints = perchPositions != null ? perchPositions.Length : (perchTransforms != null ? perchTransforms.Length : 0);

            if (targetIndex >= totalWaypoints)
            {
                // Já no poleiro final: no máximo UM pio suave (sujeito ao cooldown). Nunca repetido por frame.
                TryChirp();
                return;
            }

            Vector3 startPos = transform.position;
            Vector3 endPos = perchPositions != null && perchPositions.Length > targetIndex ? perchPositions[targetIndex] : perchTransforms[targetIndex].position;
            Quaternion endRot = perchRotations != null && perchRotations.Length > targetIndex ? perchRotations[targetIndex] : (perchTransforms != null && perchTransforms[targetIndex] != null ? perchTransforms[targetIndex].rotation : transform.rotation);

            if (flightCoroutine != null) StopCoroutine(flightCoroutine);
            flightCoroutine = StartCoroutine(FlightRoutine(startPos, endPos, endRot, targetIndex));

            if (interactor is XRBaseControllerInteractor ctrl)
            {
                ctrl.SendHapticImpulse(0.3f, 0.10f);
            }
        }

        private IEnumerator FlightRoutine(Vector3 startPos, Vector3 endPos, Quaternion endRot, int nextIndex)
        {
            currentState = BirdState.Alerted;

            // 1. Um único pio curto antes da decolagem (se o cooldown permitir)
            TryChirp();

            yield return new WaitForSeconds(0.12f);

            // 2. Som de bater de asas
            if (flutterSfx != null && audioSource != null && flutterRelativeVolume > 0.001f)
            {
                audioSource.pitch = Random.Range(0.95f, 1.05f);
                audioSource.PlayOneShot(flutterSfx, flutterRelativeVolume);
            }

            currentState = BirdState.Flying;

            float distance = Vector3.Distance(startPos, endPos);
            float duration = Mathf.Clamp(distance / flightSpeed, 1.8f, 3.6f);
            float elapsed = 0f;

            // Pontos de controle Bezier cúbica com arco elevado e curvatura natural
            Vector3 flightDir = (endPos - startPos).normalized;
            Vector3 p0 = startPos;
            Vector3 p1 = startPos + Vector3.up * flightArcHeight + flightDir * (distance * 0.25f);
            Vector3 p2 = endPos + Vector3.up * (flightArcHeight * 0.85f) - flightDir * (distance * 0.25f);
            Vector3 p3 = endPos;

            Vector3 lastPos = startPos;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Curva Bezier cúbica
                float u = 1f - t;
                float tt = t * t;
                float uu = u * u;
                float uuu = uu * u;
                float ttt = tt * t;

                Vector3 currentPos = (uuu * p0) + (3f * uu * t * p1) + (3f * u * tt * p2) + (ttt * p3);
                transform.position = currentPos;

                // Alinhamento suave da rotação na direção do movimento com banking
                Vector3 moveDelta = currentPos - lastPos;
                if (moveDelta.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetFlightRot = Quaternion.LookRotation(moveDelta.normalized, Vector3.up);
                    // Suave rotação para a curva
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetFlightRot, Time.deltaTime * 10f);
                }
                lastPos = currentPos;

                yield return null;
            }

            // 3. Aterrissagem suave no novo poleiro
            currentState = BirdState.Landing;
            transform.position = endPos;

            float landingTime = 0.35f;
            float landingElapsed = 0f;
            Quaternion currentLandingRot = transform.rotation;

            while (landingElapsed < landingTime)
            {
                landingElapsed += Time.deltaTime;
                float lt = landingElapsed / landingTime;
                transform.rotation = Quaternion.Slerp(currentLandingRot, endRot, lt);
                yield return null;
            }

            transform.rotation = endRot;
            currentWaypointIndex = nextIndex;
            ResetWingsToFolded();

            // Pousa em silêncio (sem pio de confirmação)
            currentState = BirdState.Perched;
            flightCoroutine = null;
        }
    }
}
