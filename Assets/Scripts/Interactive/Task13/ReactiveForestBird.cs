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

        private XRSimpleInteractable simpleInteractable;
        private BirdState currentState = BirdState.Perched;
        private Quaternion initialWingLRot = Quaternion.identity;
        private Quaternion initialWingRRot = Quaternion.identity;
        private Quaternion initialBodyRot = Quaternion.identity;

        private Transform playerCameraTransform;
        private Coroutine flightCoroutine;
        private float nextIdleActionTime = 0f;
        private float wingFlapPhase = 0f;

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

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1.0f;
            audioSource.minDistance = 0.5f;
            audioSource.maxDistance = 25.0f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;

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
            Camera mainCam = Camera.main;
            if (mainCam != null) playerCameraTransform = mainCam.transform;
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
            TryFlyToNextWaypoint(args.interactorObject);
        }

        private void OnXRISelectEntered(SelectEnterEventArgs args)
        {
            TryFlyToNextWaypoint(args.interactorObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.name.ToLower().Contains("hand") || other.name.ToLower().Contains("controller") || other.CompareTag("Player"))
            {
                TryFlyToNextWaypoint(null);
            }
        }

        private void Update()
        {
            switch (currentState)
            {
                case BirdState.Perched:
                    HandlePerchedIdle();
                    CheckProximity();
                    break;

                case BirdState.Flying:
                    AnimateWingsFlapping();
                    break;
            }
        }

        private void CheckProximity()
        {
            if (!triggerOnProximity || playerCameraTransform == null) return;

            float distSq = (playerCameraTransform.position - transform.position).sqrMagnitude;
            if (distSq <= proximityTriggerDist * proximityTriggerDist)
            {
                TryFlyToNextWaypoint(null);
            }
        }

        private void HandlePerchedIdle()
        {
            // Respiração sutil do peito
            float breath = Mathf.Sin(Time.time * 2.5f) * 0.02f;
            if (birdBody != null)
            {
                birdBody.localScale = new Vector3(1.0f + breath, 1.0f + breath, 1.0f - breath);
            }

            // Ações aleatórias de curiosidade (olhar para os lados, mexer a cabeça)
            if (Time.time >= nextIdleActionTime)
            {
                nextIdleActionTime = Time.time + Random.Range(2.0f, 4.5f);
                float randomYaw = Random.Range(-35f, 35f);
                float randomPitch = Random.Range(-10f, 15f);
                if (birdBody != null)
                {
                    birdBody.localRotation = initialBodyRot * Quaternion.Euler(randomPitch, randomYaw, 0f);
                }

                // Chance de emitir um pequeno gorjeio suave
                if (Random.value < 0.25f && chirpSfx != null && audioSource != null)
                {
                    audioSource.pitch = Random.Range(1.05f, 1.25f);
                    audioSource.PlayOneShot(chirpSfx, 0.45f);
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
                // Já no poleiro final (ponte): gorjeia comemorando a chegada
                if (chirpSfx != null && audioSource != null)
                {
                    audioSource.pitch = Random.Range(0.98f, 1.05f);
                    audioSource.PlayOneShot(chirpSfx, 1.0f);
                }
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

            // 1. Gorjeio alegre antes da decolagem
            if (chirpSfx != null && audioSource != null)
            {
                audioSource.pitch = Random.Range(0.98f, 1.04f);
                audioSource.PlayOneShot(chirpSfx, 0.95f);
            }

            yield return new WaitForSeconds(0.12f);

            // 2. Som de bater de asas
            if (flutterSfx != null && audioSource != null)
            {
                audioSource.pitch = Random.Range(0.95f, 1.05f);
                audioSource.PlayOneShot(flutterSfx, 0.85f);
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

            // Gorjeio de confirmação após pousar com sucesso
            if (chirpSfx != null && audioSource != null)
            {
                audioSource.pitch = Random.Range(1.05f, 1.15f);
                audioSource.PlayOneShot(chirpSfx, 0.80f);
            }

            currentState = BirdState.Perched;
            flightCoroutine = null;
        }
    }
}
