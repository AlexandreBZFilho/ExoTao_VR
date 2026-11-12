using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;

namespace VRRehab.Ergonomics
{
    /// <summary>
    /// Componente de Arremesso por Balanço de Raio Laser de Alta Precisão para VR (TASK-02).
    /// Herda de ErgonomicGrabInteractable e substitui a física tradicional de descarte do XRI por um
    /// rastreador cinemático de alavanca tangencial real em espaço de mundo (World Space).
    /// 
    /// Destaques de Engenharia e VR 90 FPS:
    /// 1. Ring Buffer Circular de 12 amostras com ZERO alocações de memória (GC Alloc = 0 em runtime);
    /// 2. Cálculo cinemático de velocidade tangencial ponderada no tempo (janela 0.03s a 0.12s);
    /// 3. Multiplicador de impulso ergonômico clínico (1.65x) com clamp de segurança (18.0 m/s);
    /// 4. Cálculo de velocidade angular a partir de rotações quaternárias (ToAngleAxis clampada a 25.0 rad/s);
    /// 5. Integração com ThrowAimAssist (cone sutil de 18° e blend de 50% com parábola ideal);
    /// 6. Otimização PhysX dinâmica: ContinuousDynamic durante grab/voo -> Discrete + Sleep após 3.0s de repouso;
    /// 7. Proteção OutOfBoundsSafetyGuard contra queda debaixo do terreno ou fora da sala.
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public class LaserSwingThrowInteractable : ErgonomicGrabInteractable
    {
        [System.Serializable]
        public struct PoseSample
        {
            public Vector3 position;
            public Quaternion rotation;
            public float timestamp;
        }

        [Header("Cinemática de Balanço do Laser (Laser Swing)")]
        [Tooltip("Multiplicador ergonômico clínico de impulso quando solto via raio laser.")]
        [Range(1.0f, 2.5f)]
        [SerializeField] private float throwBoostMultiplier = 1.65f;

        [Tooltip("Multiplicador de impulso quando solto por agarre direto da mão.")]
        [Range(1.0f, 2.0f)]
        [SerializeField] private float directThrowBoostMultiplier = 1.25f;

        [Tooltip("Velocidade linear máxima rígida de segurança para VR (m/s) para evitar tunelamento físico.")]
        [SerializeField] private float maxThrowSpeed = 18.0f;

        [Tooltip("Velocidade angular máxima permitida em radianos/s.")]
        [SerializeField] private float maxAngularSpeed = 25.0f;

        [Header("Filtro Temporal de Amostragem")]
        [Tooltip("Janela temporal mínima antes do frame de soltura (ignora ruído mecânico do botão).")]
        [SerializeField] private float minWindowOffsetSeconds = 0.020f;

        [Tooltip("Janela temporal máxima no passado para amostragem do pico de chicote.")]
        [SerializeField] private float maxWindowOffsetSeconds = 0.125f;

        [Header("Feedback de Áudio & Háptico")]
        [SerializeField] private AudioClip laserThrowWooshClip;

        // Ring Buffer fixo de 12 amostras: ~0.13s a 90 FPS (zero alocações)
        private const int BUFFER_SIZE = 12;
        private readonly PoseSample[] poseRingBuffer = new PoseSample[BUFFER_SIZE];
        private int bufferHead = 0;
        private int sampleCount = 0;

        private bool isHeldByRay = false;
        private IXRSelectInteractor lastSelectInteractor;
        private Coroutine physXRestCoroutine;

        public float ThrowBoostMultiplier
        {
            get => throwBoostMultiplier;
            set => throwBoostMultiplier = Mathf.Clamp(value, 1.0f, 2.5f);
        }

        public float MaxThrowSpeed
        {
            get => maxThrowSpeed;
            set => maxThrowSpeed = Mathf.Max(1.0f, value);
        }

        public bool IsHeldByRay => isHeldByRay;

        protected override void Awake()
        {
            base.Awake();

            // Desativa o descarte inercial padrão do XRI para assumirmos controle total da cinemática
            throwOnDetach = false;
            movementType = MovementType.VelocityTracking;

            AutoLoadThrowClipIfMissing();
        }

        protected override void Reset()
        {
            base.Reset();

            throwOnDetach = false;
            movementType = MovementType.VelocityTracking;
            throwBoostMultiplier = 1.65f;
            maxThrowSpeed = 18.0f;
            maxAngularSpeed = 25.0f;

            AutoLoadThrowClipIfMissing();
        }

        private void AutoLoadThrowClipIfMissing()
        {
#if UNITY_EDITOR
            if (laserThrowWooshClip == null)
            {
                laserThrowWooshClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Generated/Task02/laser_throw_woosh.wav");
                if (laserThrowWooshClip == null)
                {
                    laserThrowWooshClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Generated/Core/release_woosh.wav");
                }
            }
#endif
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);

            // Interrompe monitoramento de repouso anterior se houver
            if (physXRestCoroutine != null)
            {
                StopCoroutine(physXRestCoroutine);
                physXRestCoroutine = null;
            }

            // Identifica se o agarre foi realizado via raio laser
            lastSelectInteractor = args.interactorObject;
            isHeldByRay = (args.interactorObject is XRRayInteractor);

            // Otimização PhysX: eleva para ContinuousDynamic enquanto na mão para evitar penetração
            if (cachedRb != null)
            {
                cachedRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            // Reinicializa o buffer circular de amostragem
            bufferHead = 0;
            sampleCount = 0;
            RecordSample();
        }

        protected virtual void Update()
        {
            // Amostra a posição e rotação global do objeto a cada frame enquanto segurado
            if (isSelected)
            {
                RecordSample();
            }
        }

        protected virtual void FixedUpdate()
        {
            // Proteção em tempo real contra tunelamento físico durante o voo
            if (!isSelected && !isReturningHome && cachedRb != null && !cachedRb.isKinematic)
            {
                CheckActiveFlightBounds();
            }
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            // Executa o descarte base do XRI (libera transform e parent)
            base.OnSelectExited(args);

            if (cachedRb == null || cachedRb.isKinematic)
            {
                return;
            }

            // Garante que a gravidade e o movimento físico estejam ativos
            cachedRb.useGravity = true;

            // Calcula a velocidade linear tangencial real a partir do histórico de poses
            Vector3 calculatedVelocity = ComputeTangentialVelocity(out PoseSample oldestWindowSample, out PoseSample newestWindowSample);

            // Aplica multiplicador clínico ergonômico
            float multiplier = isHeldByRay ? throwBoostMultiplier : directThrowBoostMultiplier;
            Vector3 boostedVelocity = calculatedVelocity * multiplier;

            // Integração com ThrowAimAssist se presente no objeto
            if (TryGetComponent<ThrowAimAssist>(out var aimAssist))
            {
                boostedVelocity = aimAssist.CalculateAssistedVelocity(transform.position, boostedVelocity);
            }

            // Clamp estrito de segurança VR para evitar tunelamento
            float finalSpeed = boostedVelocity.magnitude;
            if (finalSpeed > maxThrowSpeed)
            {
                boostedVelocity = (boostedVelocity / finalSpeed) * maxThrowSpeed;
            }

            // Aplica a velocidade linear tangencial calculada ao Rigidbody
            SetRigidbodyVelocity(cachedRb, boostedVelocity);

            // Calcula a velocidade angular a partir da rotação no buffer para giro natural
            Vector3 angularVel = ComputeAngularVelocity(oldestWindowSample, newestWindowSample);
            SetRigidbodyAngularVelocity(cachedRb, angularVel);

            // Feedback sonoro e háptico de arremesso no controle
            if (boostedVelocity.sqrMagnitude > 0.25f)
            {
                PlayThrowFeedback();
            }

            // Inicia ciclo dinâmico de PhysX: mantém ContinuousDynamic até repousar
            cachedRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            if (gameObject.activeInHierarchy)
            {
                physXRestCoroutine = StartCoroutine(PhysXRestMonitorRoutine());
            }
        }

        #region Cinemática & Ring Buffer (Zero GC)
        /// <summary>
        /// Grava uma amostra no buffer circular fixo. Zero alocações de GC.
        /// </summary>
        private void RecordSample()
        {
            poseRingBuffer[bufferHead] = new PoseSample
            {
                position = transform.position,
                rotation = transform.rotation,
                timestamp = Time.time
            };

            bufferHead = (bufferHead + 1) % BUFFER_SIZE;
            if (sampleCount < BUFFER_SIZE)
            {
                sampleCount++;
            }
        }

        /// <summary>
        /// Calcula a velocidade linear tangencial ponderada no tempo a partir do histórico de poses.
        /// Janela de amostragem clínica: [minWindowOffsetSeconds, maxWindowOffsetSeconds].
        /// </summary>
        private Vector3 ComputeTangentialVelocity(out PoseSample oldestSample, out PoseSample newestSample)
        {
            oldestSample = default;
            newestSample = default;

            if (sampleCount < 2)
            {
                return Vector3.zero;
            }

            float releaseTime = Time.time;
            Vector3 weightedVelSum = Vector3.zero;
            float totalWeight = 0f;
            int validSegments = 0;

            bool foundNewest = false;

            // Percorre o buffer em ordem cronológica decrescente (do mais recente para o mais antigo)
            for (int i = 0; i < sampleCount - 1; i++)
            {
                int currIdx = (bufferHead - 1 - i + BUFFER_SIZE) % BUFFER_SIZE;
                int prevIdx = (bufferHead - 2 - i + BUFFER_SIZE) % BUFFER_SIZE;

                PoseSample curr = poseRingBuffer[currIdx];
                PoseSample prev = poseRingBuffer[prevIdx];

                float dt = curr.timestamp - prev.timestamp;
                if (dt <= 0.0001f) continue;

                float timeBeforeRelease = releaseTime - curr.timestamp;

                // Avalia se o segmento está dentro da janela cinemática útil
                bool insideWindow = timeBeforeRelease >= minWindowOffsetSeconds && timeBeforeRelease <= maxWindowOffsetSeconds;

                // Se houver poucos frames (ex: soltura muito rápida), aceita segmentos adjacentes
                if (insideWindow || (validSegments == 0 && timeBeforeRelease <= maxWindowOffsetSeconds * 1.5f))
                {
                    if (!foundNewest)
                    {
                        newestSample = curr;
                        foundNewest = true;
                    }
                    oldestSample = prev;

                    Vector3 segVel = (curr.position - prev.position) / dt;

                    // Ponderação temporal: amostras mais recentes recebem peso progressivamente maior
                    float weight = 1.0f + (1.0f / (1.0f + timeBeforeRelease * 10f));
                    weightedVelSum += segVel * weight;
                    totalWeight += weight;
                    validSegments++;
                }
            }

            if (totalWeight > 0.001f)
            {
                return weightedVelSum / totalWeight;
            }

            // Fallback se nenhum segmento couber estritamente na janela
            int newestIdx = (bufferHead - 1 + BUFFER_SIZE) % BUFFER_SIZE;
            int oldestIdx = (bufferHead - sampleCount + BUFFER_SIZE) % BUFFER_SIZE;
            PoseSample fallbackNew = poseRingBuffer[newestIdx];
            PoseSample fallbackOld = poseRingBuffer[oldestIdx];
            float fallbackDt = fallbackNew.timestamp - fallbackOld.timestamp;

            oldestSample = fallbackOld;
            newestSample = fallbackNew;

            if (fallbackDt > 0.001f)
            {
                return (fallbackNew.position - fallbackOld.position) / fallbackDt;
            }

            return Vector3.zero;
        }

        /// <summary>
        /// Calcula a velocidade angular a partir da rotação acumulada na janela de amostragem.
        /// </summary>
        private Vector3 ComputeAngularVelocity(PoseSample oldest, PoseSample newest)
        {
            float dt = newest.timestamp - oldest.timestamp;
            if (dt <= 0.001f)
            {
                return Vector3.zero;
            }

            Quaternion qDelta = newest.rotation * Quaternion.Inverse(oldest.rotation);
            qDelta.ToAngleAxis(out float angleDeg, out Vector3 axis);

            if (angleDeg > 180f)
            {
                angleDeg -= 360f;
            }

            if (axis.sqrMagnitude < 0.001f)
            {
                return Vector3.zero;
            }

            float omegaRad = (angleDeg * Mathf.Deg2Rad) / dt;
            omegaRad = Mathf.Clamp(omegaRad, -maxAngularSpeed, maxAngularSpeed);

            return axis.normalized * omegaRad;
        }
        #endregion

        #region Otimização PhysX VR (Ciclo de Vida Dinâmico)
        /// <summary>
        /// Coroutine de monitoramento de repouso estável.
        /// Após 3.0s de repouso (< 0.2 m/s), rebaixa para Discrete e força Sleep() para economizar CPU em VR.
        /// </summary>
        private IEnumerator PhysXRestMonitorRoutine()
        {
            const float REQUIRED_REST_TIME = 3.0f;
            const float REST_SPEED_SQR_THRESHOLD = 0.04f; // 0.2 m/s

            float accumulatedRestTime = 0f;
            WaitForSeconds waitInterval = new WaitForSeconds(0.25f);

            while (!isSelected && cachedRb != null && !cachedRb.isKinematic)
            {
                yield return waitInterval;

                float speedSqr = GetRigidbodyVelocity(cachedRb).sqrMagnitude;

                if (speedSqr < REST_SPEED_SQR_THRESHOLD)
                {
                    accumulatedRestTime += 0.25f;
                    if (accumulatedRestTime >= REQUIRED_REST_TIME)
                    {
                        // Objeto repousou com estabilidade: otimiza colisão para Discrete e entra em Sleep
                        cachedRb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                        cachedRb.Sleep();
                        break;
                    }
                }
                else
                {
                    accumulatedRestTime = 0f;
                }
            }

            physXRestCoroutine = null;
        }
        #endregion

        #region Proteção Anti-Queda Fora do Mapa (OutOfBoundsSafetyGuard)
        /// <summary>
        /// Validação imediata durante o voo físico balístico para impedir que o objeto penetre abaixo do chão.
        /// </summary>
        private void CheckActiveFlightBounds()
        {
            Vector3 pos = transform.position;

            // Se estiver na Floresta com terreno ativo
            if (Terrain.activeTerrain != null)
            {
                float terrainY = Terrain.activeTerrain.SampleHeight(pos) + Terrain.activeTerrain.transform.position.y;
                if (pos.y < terrainY - 1.2f)
                {
                    RecoverFromOutOfBounds();
                    return;
                }
            }

            // Se caiu abaixo do limiar crítico do piso da sala ou do mundo
            if (pos.y < -1.5f || (Terrain.activeTerrain == null && pos.y < respawnFloorThresholdY - 0.2f))
            {
                RecoverFromOutOfBounds();
            }
        }

        private void RecoverFromOutOfBounds()
        {
            if (cachedRb != null)
            {
                SetRigidbodyVelocity(cachedRb, Vector3.zero);
                SetRigidbodyAngularVelocity(cachedRb, Vector3.zero);
            }

            TriggerManualRespawn();
        }
        #endregion

        #region Feedback Multissensorial
        private void PlayThrowFeedback()
        {
            if (VRFeedbackManager.Instance != null)
            {
                AudioClip clipToPlay = (laserThrowWooshClip != null) ? laserThrowWooshClip : VRFeedbackManager.Instance.clipReleaseWoosh;
                VRFeedbackManager.Instance.PlaySound(clipToPlay, transform.position, 0.75f, 0.96f, 1.04f);

                if (lastSelectInteractor is IXRInteractor interactor)
                {
                    VRFeedbackManager.Instance.TriggerHaptic(interactor, 0.40f, 0.08f);
                }
            }
        }
        #endregion
    }
}
