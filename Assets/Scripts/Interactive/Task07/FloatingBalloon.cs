using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;

namespace VRRehab.Interactive.Task07
{
    /// <summary>
    /// Objeto marcador: qualquer collider com este componente (no próprio objeto ou num pai) estoura o balão ao tocar.
    /// </summary>
    [DisallowMultipleComponent]
    public class BalloonSharpObject : MonoBehaviour { }

    /// <summary>
    /// Balão antigravidade da TASK-07 (estilo The Lab), pensado para reabilitação:
    /// - Mão encostando devagar: EMPURRA o balão (joguinho de manter no ar), sem estourar.
    /// - Gatilho (Select) com o laser ou com a mão, ou Activate mirando: ESTOURA com háptico e partículas.
    /// - Bater em mesa/parede/chão/outro balão: QUICA suave. Só estoura com impacto forte (> popImpactSpeed) ou em algo afiado.
    /// - Perto do teto fica balançando, sem travar e sem atravessar.
    /// - Depois de lifetimeSeconds murcha devagar e volta para o pool (a sala nunca enche).
    /// Zero Instantiate/Destroy em runtime: o balão vem do BalloonPool da pistola e volta para ele.
    /// Balões de demonstração (homeAnchored) ficam presos ao ponto de origem por uma mola suave e renascem depois de estourar.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    [SelectionBase]
    public class FloatingBalloon : XRSimpleInteractable
    {
        private enum State { Idle, Inflating, Free, Popping, Deflating, WaitingRespawn }

        [Header("Flutuação")]
        [Tooltip("Aceleração vertical de empuxo (m/s²) para um balão de escala 1.")]
        [SerializeField] private float buoyancyAcceleration = 0.30f;
        [Tooltip("Velocidade máxima de subida (m/s).")]
        [SerializeField] private float maxAscentSpeed = 0.42f;
        [Tooltip("Altura (Y mundial) do teto da sala. O topo do balão desacelera antes dele.")]
        [SerializeField] private float ceilingHeightLimit = 2.85f;
        [Tooltip("Intensidade da brisa lateral (m/s²).")]
        [SerializeField] private float ambientWindForce = 0.07f;
        [Tooltip("Resistência do ar base; cresce com a escala do balão.")]
        [SerializeField] private float baseLinearDamping = 0.9f;
        [Tooltip("Rigidez do torque que mantém o balão de pé (o nó para baixo).")]
        [SerializeField] private float uprightTorque = 2.2f;
        [Tooltip("Oscilação de rotação (graus/s² de ruído).")]
        [SerializeField] private float wobbleTorque = 0.35f;

        [Header("Cordinha (escala 1)")]
        [Tooltip("Comprimento da cordinha para balão de escala 1 (metros). Escala junto com o balão.")]
        [SerializeField] private float stringLength = 0.34f;
        [SerializeField] private float stringStartWidth = 0.0030f;
        [SerializeField] private float stringEndWidth = 0.0018f;
        [SerializeField] private int stringSegments = 6;
        [Tooltip("Acima desta distância do jogador a cordinha não simula (fica pendurada reta).")]
        [SerializeField] private float stringSimulationRadius = 8f;
        [SerializeField] private LineRenderer stringRenderer;
        [SerializeField] private Transform stringAnchor;

        [Header("Toque, impacto e estouro")]
        [Tooltip("Velocidade relativa de impacto que estoura o balão (m/s).")]
        [SerializeField] private float popImpactSpeed = 2.5f;
        [Tooltip("Impulso mínimo e máximo de um tapinha da mão (m/s).")]
        [SerializeField] private Vector2 bopSpeedRange = new Vector2(0.55f, 2.2f);
        [Tooltip("Imunidade logo após sair do bico (s): ignora a pistola e a mão que segurava.")]
        [SerializeField] private float launchGracePeriod = 0.4f;
        [SerializeField] private ParticleSystem burstParticleSystem;
        [SerializeField] private AudioClip popAudioClip;
        [SerializeField] private AudioSource audioSource;
        [Range(0f, 1f)] [SerializeField] private float popVolume = 0.55f;

        [Header("Vida útil")]
        [Tooltip("Tempo (s) até o balão murchar sozinho e voltar para o pool.")]
        [SerializeField] private float lifetimeSeconds = 60f;
        [SerializeField] private float deflateSeconds = 2.5f;

        [Header("Balão de demonstração")]
        [Tooltip("Fica preso ao ponto inicial por uma mola suave (decoração da sala) e renasce depois de estourar.")]
        [SerializeField] private bool homeAnchored = false;
        [SerializeField] private float homeSpring = 1.4f;
        [SerializeField] private float demoRespawnSeconds = 25f;

        private const float SoftPopSeconds = 0.35f;
        private const float PopHideSeconds = 0.9f;
        private const float BodyHeightAtScale1 = 0.30f;

        private Rigidbody rb;
        private MeshRenderer[] meshRenderers;
        private Collider balloonCollider;
        private Vector3[] stringPos;
        private Vector3[] stringPrev;
        private BalloonPool ownerPool;

        private State state = State.Idle;
        private float stateTime;
        private float releaseTime;
        private float scale = 1f;
        private float targetScale = 1f;
        private float scaleAtStateStart = 1f;
        private float perlinSeed;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private float homeScale = 1f;
        private float currentDeflateSeconds = 2.5f;

        // Imunidade inicial
        private Collider[] graceIgnored;
        private int graceIgnoredCount;
        private Transform graceInteractorRoot;

        // Tapinha da mão: amostra a posição da mão entre dois passos de física
        private Transform bopHand;
        private Vector3 bopHandPrevPos;
        private int bopSamples;
        private float lastBopTime = -10f;

        private static Transform viewer;

        public bool IsPopped => state == State.Popping || state == State.WaitingRespawn || state == State.Idle;
        public bool IsFree => state == State.Free;
        public bool IsInflating => state == State.Inflating;
        public bool IsDeflatingOrPopping => state == State.Popping || state == State.Deflating;
        public float CurrentScale => scale;
        public float BodyHeightMeters => BodyHeightAtScale1 * scale;
        public float StringLengthMeters => stringLength * scale;
        public float ReleaseTime => releaseTime;
        public Collider BalloonCollider => balloonCollider;
        /// <summary>Quantas vezes o balão foi empurrado pela mão desde que foi solto (telemetria / testes).</summary>
        public int BopCount { get; private set; }
        /// <summary>Motivo do último estouro: "select", "activate", "impact", "sharp", "pool", "lifetime" (telemetria / testes).</summary>
        public string LastPopReason { get; private set; } = "";

        protected override void Awake()
        {
            base.Awake();
            rb = GetComponent<Rigidbody>();
            meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
            balloonCollider = GetComponent<Collider>();
            graceIgnored = new Collider[8];

            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.loop = false;
                audioSource.spatialBlend = 1f;
            }

            rb.useGravity = false;
            rb.mass = 0.012f;
            rb.angularDamping = 1.6f;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

            if (stringAnchor == null)
            {
                Transform found = transform.Find("Visual_Model/String_Anchor");
                if (found == null) found = transform.Find("String_Anchor");
                stringAnchor = found != null ? found : transform;
            }
            if (stringRenderer == null) stringRenderer = GetComponent<LineRenderer>();
            stringSegments = Mathf.Max(3, stringSegments);
            stringPos = new Vector3[stringSegments];
            stringPrev = new Vector3[stringSegments];
            if (stringRenderer != null)
            {
                stringRenderer.positionCount = stringSegments;
                stringRenderer.useWorldSpace = true;
            }
            if (burstParticleSystem == null) burstParticleSystem = GetComponentInChildren<ParticleSystem>(true);
            if (burstParticleSystem != null)
            {
                var main = burstParticleSystem.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World; // partículas continuam no lugar enquanto o balão some
            }

            perlinSeed = Random.Range(0f, 1000f);
            homePosition = transform.position;
            homeRotation = transform.rotation;
            homeScale = transform.localScale.x;
            scale = targetScale = homeScale;

            if (homeAnchored)
            {
                rb.isKinematic = false;
                SetState(State.Free);
                releaseTime = float.MaxValue; // demo não murcha
                ResetString();
            }
        }

        #region API usada pela pistola / pool
        internal void BindPool(BalloonPool pool) => ownerPool = pool;

        /// <summary>Prepara o balão preso ao bico, kinematic, sem collider (o laser não o atinge enquanto infla).</summary>
        public void BeginInflation(Transform muzzle, float startScale, Material mat, Color burstColor)
        {
            CancelGrace();
            gameObject.SetActive(true);
            transform.SetParent(muzzle, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // +Y do balão (nó -> topo) = +Z do bico
            ApplyColorMaterial(mat, burstColor);
            rb.isKinematic = true;
            if (balloonCollider != null) balloonCollider.enabled = false;
            if (stringRenderer != null) stringRenderer.enabled = false;
            SetRenderersVisible(true);
            BopCount = 0;
            LastPopReason = "";
            SetScaleImmediate(startScale);
            SetState(State.Inflating);
        }

        /// <summary>Atualiza a forma durante a inflação (escala + squash/stretch elástico).</summary>
        public void SetInflationShape(float uniformScale, float stretch, Vector3 jitterLocal)
        {
            scale = targetScale = uniformScale;
            float side = 1f / Mathf.Sqrt(Mathf.Max(0.5f, stretch));
            transform.localScale = new Vector3(uniformScale * side, uniformScale * stretch, uniformScale * side);
            transform.localPosition = jitterLocal;
        }

        /// <summary>Solta o balão no mundo com velocidade inicial. Ignora a pistola e a mão que segurava por launchGracePeriod.</summary>
        public void Launch(Vector3 velocity, float finalScale, Collider[] ignoreColliders, Transform holdingInteractorRoot)
        {
            transform.SetParent(ownerPool != null ? ownerPool.Root : null, true);
            SetScaleImmediate(scale);
            targetScale = Mathf.Max(scale, finalScale);
            scaleAtStateStart = scale;
            rb.isKinematic = false;
            rb.linearVelocity = velocity;
            rb.angularVelocity = Vector3.zero;
            if (balloonCollider != null) balloonCollider.enabled = true;
            if (stringRenderer != null) stringRenderer.enabled = true;

            graceIgnoredCount = 0;
            if (ignoreColliders != null && balloonCollider != null)
            {
                for (int i = 0; i < ignoreColliders.Length && graceIgnoredCount < graceIgnored.Length; i++)
                {
                    Collider c = ignoreColliders[i];
                    if (c == null || !c.enabled) continue;
                    Physics.IgnoreCollision(balloonCollider, c, true);
                    graceIgnored[graceIgnoredCount++] = c;
                }
            }
            graceInteractorRoot = holdingInteractorRoot;
            releaseTime = Time.time;
            SetState(State.Free);
            ResetString();
        }

        /// <summary>Volta para o pool sem efeitos (cancelamento de inflação muito curta).</summary>
        public void CancelSilently()
        {
            CancelGrace();
            FinishAndRecycle();
        }

        /// <summary>Estouro suave usado quando o pool precisa do balão mais antigo: encolhe rápido e solta poucas partículas.</summary>
        public void SoftPop()
        {
            if (state != State.Free) return;
            LastPopReason = "pool";
            scaleAtStateStart = scale;
            if (balloonCollider != null) balloonCollider.enabled = false;
            EmitBurst(10);
            PlayPop(popVolume * 0.45f, 1.15f);
            currentDeflateSeconds = SoftPopSeconds; // murcha rápido, mas contínuo (sem salto de escala)
            SetState(State.Deflating);
        }
        #endregion

        #region Interação XRI
        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            Pop(args.interactorObject, "select");
        }

        protected override void OnActivated(ActivateEventArgs args)
        {
            base.OnActivated(args);
            Pop(args.interactorObject, "activate");
        }
        #endregion

        #region Física
        private void FixedUpdate()
        {
            if (state != State.Free || rb.isKinematic) return;

            float now = Time.time;
            if (graceIgnoredCount > 0 && now - releaseTime > launchGracePeriod) CancelGrace();

            ProcessBop();

            Vector3 vel = rb.linearVelocity;
            float topY = transform.position.y + BodyHeightMeters;
            float viewerSqr = ViewerSqrDistance();
            bool near = viewerSqr < stringSimulationRadius * stringSimulationRadius;

            // Longe do jogador e já parado (no teto ou no ponto de origem do demo): dorme sem aplicar forças
            if (!near && vel.sqrMagnitude < 0.0004f && rb.angularVelocity.sqrMagnitude < 0.0004f)
            {
                bool resting = homeAnchored ? (homePosition - transform.position).sqrMagnitude < 0.0025f
                                            : ceilingHeightLimit - topY < 0.12f;
                if (resting) { rb.Sleep(); return; }
            }

            if (homeAnchored)
            {
                // Mola suave até o ponto de origem (decoração "amarrada"), com amortecimento crítico aproximado
                Vector3 toHome = homePosition - transform.position;
                rb.AddForce(toHome * homeSpring - vel * (2f * Mathf.Sqrt(homeSpring)), ForceMode.Acceleration);
            }
            else
            {
                // Empuxo: some suavemente nos últimos 0,35 m antes do teto; acima dele empurra de volta para baixo
                float room = ceilingHeightLimit - topY;
                float buoy = buoyancyAcceleration * Mathf.Clamp01(room / 0.35f);
                if (room < 0f) buoy = room * 1.5f;
                if (vel.y < maxAscentSpeed || buoy < 0f) rb.AddForce(Vector3.up * buoy, ForceMode.Acceleration);
            }

            // Resistência do ar proporcional ao tamanho (balão grande freia mais)
            rb.linearDamping = baseLinearDamping * (0.75f + 0.35f * scale);

            if (near)
            {
                // Brisa e balanço só perto do jogador (longe, o balão pode dormir)
                float t = now * 0.55f;
                float windX = (Mathf.PerlinNoise(perlinSeed + t, 0.3f) - 0.5f) * 2f * ambientWindForce;
                float windZ = (Mathf.PerlinNoise(0.7f, perlinSeed + t) - 0.5f) * 2f * ambientWindForce;
                rb.AddForce(new Vector3(windX, 0f, windZ), ForceMode.Acceleration);

                Vector3 wobble = new Vector3(
                    Mathf.PerlinNoise(perlinSeed + t * 1.3f, 5.1f) - 0.5f,
                    0f,
                    Mathf.PerlinNoise(9.7f, perlinSeed + t * 1.3f) - 0.5f) * wobbleTorque;
                rb.AddTorque(wobble, ForceMode.Acceleration);
            }

            // Mantém o nó para baixo com torque (sem brigar com a física via transform.rotation)
            Vector3 up = transform.up;
            Vector3 axis = Vector3.Cross(up, Vector3.up);
            rb.AddTorque(axis * uprightTorque - rb.angularVelocity * 0.6f, ForceMode.Acceleration);

        }

        private void ProcessBop()
        {
            if (bopHand == null) return;
            if (bopSamples == 0)
            {
                bopHandPrevPos = bopHand.position;
                bopSamples = 1;
                return;
            }
            Vector3 handPos = bopHand.position;
            Vector3 handVel = (handPos - bopHandPrevPos) / Mathf.Max(Time.fixedDeltaTime, 0.001f);
            Vector3 away = transform.position + Vector3.up * (BodyHeightMeters * 0.6f) - handPos;
            if (away.sqrMagnitude < 1e-6f) away = Vector3.up;
            away.Normalize();
            float speed = Mathf.Clamp(handVel.magnitude * 0.9f, bopSpeedRange.x, bopSpeedRange.y);
            Vector3 dir = (away + (handVel.sqrMagnitude > 0.01f ? handVel.normalized : away)).normalized;
            rb.WakeUp();
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, dir * speed, 0.85f);
            rb.AddTorque(Vector3.Cross(Vector3.up, dir) * speed * 2f, ForceMode.VelocityChange);
            BopCount++;
            lastBopTime = Time.time;
            bopHand = null;
            bopSamples = 0;
            if (VRFeedbackManager.Instance != null && VRFeedbackManager.Instance.clipUIHover != null)
                VRFeedbackManager.Instance.PlaySound(VRFeedbackManager.Instance.clipUIHover, transform.position, 0.15f, 0.75f, 0.85f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (state != State.Free) return;
            if (IsSharp(other)) { Pop(null, "sharp"); return; }

            // Mão/controle (XRDirectInteractor usa collider trigger): empurra, não estoura
            Transform hand = FindInteractorRoot(other);
            if (hand == null) return;
            if (graceInteractorRoot != null && hand == graceInteractorRoot && Time.time - releaseTime < launchGracePeriod) return;
            if (Time.time - lastBopTime < 0.15f) return;
            bopHand = other.transform;
            bopSamples = 0;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (state != State.Free) return;
            if (IsSharp(collision.collider)) { Pop(null, "sharp"); return; }
            if (collision.relativeVelocity.sqrMagnitude > popImpactSpeed * popImpactSpeed) Pop(null, "impact");
            // Senão: só quica (PhysicsMaterial do collider: bounciness ~0.5, atrito baixo)
        }

        private static bool IsSharp(Collider c)
        {
            return c != null && c.GetComponentInParent<BalloonSharpObject>() != null;
        }

        private static Transform FindInteractorRoot(Collider c)
        {
            // Evento raro (enter): GetComponentInParent é aceitável aqui, nunca em Update
            var hand = c.GetComponentInParent<XRBaseInteractor>();
            if (hand != null && !(hand is XRRayInteractor) && !(hand is XRSocketInteractor)) return hand.transform;
            if (c.CompareTag("Player")) return c.transform;
            return null;
        }
        #endregion

        #region Ciclo de vida (Update: escala, estouro, murchar, cordinha)
        private void Update()
        {
            float now = Time.time;
            switch (state)
            {
                case State.Free:
                    if (!Mathf.Approximately(scale, targetScale))
                        SetScaleImmediate(Mathf.MoveTowards(scale, targetScale, Time.deltaTime * 3f));
                    if (!homeAnchored && now - releaseTime > lifetimeSeconds) BeginDeflate();
                    break;

                case State.Deflating:
                {
                    float k = Mathf.Clamp01((now - stateTime) / Mathf.Max(0.05f, currentDeflateSeconds));
                    float s = Mathf.Lerp(scaleAtStateStart, scaleAtStateStart * 0.12f, k * k);
                    float wob = 1f + Mathf.Sin(now * 9f) * 0.06f * (1f - k);
                    transform.localScale = new Vector3(s * wob, s / wob, s * wob);
                    scale = s;
                    if (!rb.isKinematic) rb.AddForce(Vector3.down * 0.15f * k, ForceMode.Acceleration);
                    if (k >= 1f) FinishAndRecycle();
                    break;
                }

                case State.Popping:
                    if (now - stateTime > PopHideSeconds) FinishAndRecycle();
                    break;

                case State.WaitingRespawn:
                    if (now - stateTime > demoRespawnSeconds) RespawnDemo();
                    break;
            }
        }

        private void LateUpdate()
        {
            if ((state != State.Free && state != State.Deflating) || stringRenderer == null || !stringRenderer.enabled) return;
            UpdateString();
        }

        private void BeginDeflate()
        {
            LastPopReason = "lifetime";
            scaleAtStateStart = scale;
            currentDeflateSeconds = deflateSeconds;
            SetState(State.Deflating);
        }

        /// <summary>Estouro principal (laser, gatilho, impacto forte ou objeto afiado).</summary>
        public void Pop(IXRInteractor interactor, string reason = "select")
        {
            if (state != State.Free && state != State.Deflating) return;
            LastPopReason = reason;
            CancelGrace();
            SetRenderersVisible(false);
            if (balloonCollider != null) balloonCollider.enabled = false;
            if (stringRenderer != null) stringRenderer.enabled = false;
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            EmitBurst(24);
            PlayPop(popVolume, 1f);
            if (interactor != null && VRFeedbackManager.Instance != null)
                VRFeedbackManager.Instance.TriggerHaptic(interactor, 0.7f, 0.1f);
            SetState(State.Popping);
        }

        private void FinishAndRecycle()
        {
            CancelGrace();
            if (homeAnchored)
            {
                SetRenderersVisible(false);
                if (balloonCollider != null) balloonCollider.enabled = false;
                if (stringRenderer != null) stringRenderer.enabled = false;
                rb.isKinematic = true;
                SetState(State.WaitingRespawn);
                return;
            }
            SetState(State.Idle);
            if (ownerPool != null) ownerPool.Return(this);
            else gameObject.SetActive(false);
        }

        private void RespawnDemo()
        {
            transform.SetPositionAndRotation(homePosition, homeRotation);
            SetScaleImmediate(homeScale * 0.2f);
            targetScale = homeScale;
            SetRenderersVisible(true);
            if (balloonCollider != null) balloonCollider.enabled = true;
            if (stringRenderer != null) stringRenderer.enabled = true;
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            releaseTime = float.MaxValue;
            SetState(State.Free);
            ResetString();
        }
        #endregion

        #region Cordinha (Verlet com 3 iterações de restrição de distância)
        private void ResetString()
        {
            if (stringRenderer == null) return;
            Vector3 a = stringAnchor.position;
            float seg = StringLengthMeters / (stringSegments - 1);
            for (int i = 0; i < stringSegments; i++)
            {
                stringPos[i] = a + Vector3.down * (seg * i);
                stringPrev[i] = stringPos[i];
            }
            stringRenderer.startWidth = stringStartWidth * Mathf.Max(0.6f, scale);
            stringRenderer.endWidth = stringEndWidth * Mathf.Max(0.6f, scale);
            stringRenderer.SetPositions(stringPos);
        }

        private void UpdateString()
        {
            Vector3 a = stringAnchor.position;
            float seg = StringLengthMeters / (stringSegments - 1);
            stringRenderer.startWidth = stringStartWidth * Mathf.Max(0.6f, scale);
            stringRenderer.endWidth = stringEndWidth * Mathf.Max(0.6f, scale);

            if (ViewerSqrDistance() > stringSimulationRadius * stringSimulationRadius)
            {
                if (rb.IsSleeping() && state == State.Free) return;
                for (int i = 0; i < stringSegments; i++) stringPos[i] = stringPrev[i] = a + Vector3.down * (seg * i);
                stringRenderer.SetPositions(stringPos);
                return;
            }

            float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.033f);
            Vector3 g = Vector3.down * (3.5f * dt * dt); // gravidade leve (cordinha de fita)
            stringPos[0] = stringPrev[0] = a;
            for (int i = 1; i < stringSegments; i++)
            {
                Vector3 cur = stringPos[i];
                stringPos[i] = cur + (cur - stringPrev[i]) * 0.92f + g;
                stringPrev[i] = cur;
            }
            for (int it = 0; it < 3; it++)
            {
                stringPos[0] = a;
                for (int i = 1; i < stringSegments; i++)
                {
                    Vector3 d = stringPos[i] - stringPos[i - 1];
                    float len = d.magnitude;
                    if (len < 1e-5f) continue;
                    Vector3 corr = d * ((len - seg) / len);
                    if (i == 1) stringPos[i] -= corr;
                    else { stringPos[i] -= corr * 0.5f; stringPos[i - 1] += corr * 0.5f; }
                }
            }
            stringPos[0] = a;
            stringRenderer.SetPositions(stringPos);
        }
        #endregion

        #region Utilitários
        public void ApplyColorMaterial(Material material, Color burstColor)
        {
            if (meshRenderers == null) meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
            if (material != null)
                for (int i = 0; i < meshRenderers.Length; i++) meshRenderers[i].sharedMaterial = material; // shared: mantém batching/instancing
            if (burstParticleSystem == null) burstParticleSystem = GetComponentInChildren<ParticleSystem>(true);
            if (burstParticleSystem != null)
            {
                var main = burstParticleSystem.main;
                main.startColor = new ParticleSystem.MinMaxGradient(burstColor);
            }
        }

        /// <summary>Escala uniforme do balão; a cordinha acompanha (comprimento e espessura).</summary>
        public void SetScaleImmediate(float s)
        {
            scale = s;
            transform.localScale = Vector3.one * s;
        }

        private void SetRenderersVisible(bool visible)
        {
            for (int i = 0; i < meshRenderers.Length; i++) meshRenderers[i].enabled = visible;
        }

        private void EmitBurst(int count)
        {
            if (burstParticleSystem == null) return;
            burstParticleSystem.transform.position = transform.position + transform.up * (BodyHeightMeters * 0.6f);
            burstParticleSystem.Emit(count);
        }

        private void PlayPop(float volume, float pitch)
        {
            if (popAudioClip == null || audioSource == null) return;
            audioSource.pitch = pitch * Random.Range(0.94f, 1.06f);
            audioSource.PlayOneShot(popAudioClip, volume);
        }

        private void CancelGrace()
        {
            if (balloonCollider != null)
                for (int i = 0; i < graceIgnoredCount; i++)
                    if (graceIgnored[i] != null) Physics.IgnoreCollision(balloonCollider, graceIgnored[i], false);
            graceIgnoredCount = 0;
            graceInteractorRoot = null;
        }

        private void SetState(State s)
        {
            state = s;
            stateTime = Time.time;
        }

        private float ViewerSqrDistance()
        {
            if (viewer == null)
            {
                Camera cam = Camera.main;
                if (cam == null) return 0f;
                viewer = cam.transform;
            }
            return (viewer.position - transform.position).sqrMagnitude;
        }
        #endregion
    }
}
