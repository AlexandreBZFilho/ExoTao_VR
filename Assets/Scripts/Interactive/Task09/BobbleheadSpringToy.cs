using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;

namespace VRRehab.Interactive.Task09
{
    /// <summary>
    /// Mascote Bobblehead (TASK-09, refeito para balançar de verdade).
    ///
    /// Física: oscilador amortecido em GRAUS de deflexão (pitch = frente/trás, roll = lados) + torção (yaw) com
    /// frequência um pouco diferente e leve acoplamento (o "rodopio" do pitch/roll alimenta o yaw). Uma pancada de
    /// amplitude A vira velocidade inicial A·ω, então a cabeça realmente sai ~A graus do centro.
    /// Limite elástico: acima de <see cref="softLimitAngle"/> a mola endurece; ao bater em <see cref="tockAngle"/>
    /// toca o "tock" e devolve ~25% da velocidade (quique no ombro). Nunca passa de <see cref="maxDeflectionAngle"/>.
    ///
    /// Interações: toque da mão (velocidade da mão no contato), gatilho do Vive = peteleco direcional (empurra para
    /// longe do ponto apontado), segurar o gatilho = puxar a cabeça com o laser/mão e soltar, hover do laser = tremidinha,
    /// objetos arremessados = pancada proporcional à velocidade relativa.
    /// Movimento secundário: mola do pescoço dobra/estica, antenas e braços seguem com atraso.
    ///
    /// Performance: substeps fixos de 1/90 s, zero alocação, e o componente se DESLIGA (enabled = false) quando a
    /// energia some por 0,5 s; qualquer pancada religa.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class BobbleheadSpringToy : MonoBehaviour
    {
        [Header("Hierarquia do Bobblehead")]
        [SerializeField] private Transform headTransform;
        [SerializeField] private Transform neckSpring;
        [SerializeField] private Transform[] antennas = new Transform[0];
        [SerializeField] private Transform[] arms = new Transform[0];
        [SerializeField] private Renderer visorRenderer;
        [Tooltip("Índice do material do visor no renderer da cabeça.")]
        [SerializeField] private int visorMaterialIndex = 1;
        [SerializeField] private Light visorGlowLight;

        [Header("Mola da cabeça (pitch/roll)")]
        [Tooltip("Frequência natural (rad/s). 15-17 = ~2,5 Hz, cara de bobblehead.")]
        [SerializeField] private float naturalFrequency = 16f;
        [Tooltip("Amortecimento. 0,05-0,07 = balança 3-4 s.")]
        [SerializeField] private float dampingRatio = 0.055f;
        [Tooltip("Ângulo onde a mola começa a endurecer (graus).")]
        [SerializeField] private float softLimitAngle = 38f;
        [Tooltip("Quanto a mola endurece além do ângulo suave (multiplica ω²).")]
        [SerializeField] private float limitStiffness = 4f;
        [Tooltip("Ângulo da 'batida' no ombro (toca o tock e quica).")]
        [SerializeField] private float tockAngle = 41f;
        [Tooltip("Deflexão máxima absoluta (graus).")]
        [SerializeField] private float maxDeflectionAngle = 50f;
        [Tooltip("Fração da velocidade devolvida no quique do limite.")]
        [SerializeField, Range(0f, 1f)] private float limitRestitution = 0.25f;

        [Header("Torção (yaw)")]
        [SerializeField] private float twistFrequencyRatio = 0.82f;
        [SerializeField] private float twistDamping = 0.07f;
        [Tooltip("Quanto o rodopio do pitch/roll alimenta a torção.")]
        [SerializeField] private float twistCoupling = 0.0025f;
        [Tooltip("Fração da pancada acima de 18° que vira torção.")]
        [SerializeField] private float twistFromStrongKick = 0.35f;
        [SerializeField] private float maxTwistAngle = 30f;

        [Header("Interações (amplitudes em graus)")]
        [SerializeField] private Vector2 handTouchRange = new Vector2(10f, 35f);
        [Tooltip("Velocidade da mão (m/s) que dá a amplitude máxima.")]
        [SerializeField] private float handSpeedForMax = 1.4f;
        [SerializeField] private Vector2 flickRange = new Vector2(28f, 35f);
        [SerializeField] private Vector2 hoverRange = new Vector2(3f, 5f);
        [SerializeField] private float hoverCooldown = 0.5f;
        [Tooltip("Segurar o gatilho por este tempo entra no modo 'puxar a cabeça'.")]
        [SerializeField] private float holdToPullDelay = 0.25f;
        [SerializeField] private float maxPullAngle = 40f;
        [SerializeField] private float pullLeverArm = 0.07f;
        [SerializeField] private float impactDegreesPerMps = 12f;
        [SerializeField] private float maxImpactDegrees = 32f;

        [Header("Movimento secundário")]
        [SerializeField] private float antennaFrequency = 27f;
        [SerializeField] private float antennaDamping = 0.10f;
        [SerializeField] private float antennaGain = 0.55f;
        [SerializeField] private float antennaMaxAngle = 35f;
        [SerializeField] private float armFrequency = 19f;
        [SerializeField] private float armDamping = 0.14f;
        [SerializeField] private float armGain = 0.18f;
        [SerializeField] private float armMaxAngle = 14f;
        [Tooltip("Esticada máxima da mola do pescoço (escala Y).")]
        [SerializeField] private float springMaxStretch = 0.12f;

        [Header("Visor")]
        [SerializeField] private Color visorExcitedEmission = new Color(1.2f, 0.35f, 2.2f, 1f);
        [SerializeField] private Vector2 visorLightRange = new Vector2(0.8f, 2.0f);

        [Header("Sons (3D, volume <= 0,5)")]
        [SerializeField] private AudioClip sfxBoing;
        [SerializeField] private AudioClip sfxSoft;
        [SerializeField] private AudioClip sfxTock;
        [SerializeField] private AudioSource audioSource;
        [SerializeField, Range(0f, 0.5f)] private float boingVolume = 0.45f;
        [SerializeField, Range(0f, 0.5f)] private float softVolume = 0.22f;
        [SerializeField, Range(0f, 0.5f)] private float tockVolume = 0.35f;
        [Tooltip("Pancadas menores que isto (graus) não tocam o boing.")]
        [SerializeField] private float minKickForSound = 8f;
        [SerializeField] private float softSoundMinInterval = 0.18f;

        [Header("Compatibilidade (campo antigo)")]
        [SerializeField] private float minImpulseForSound = 8f;

        // ------------------------------------------------------------------ constantes / caches
        private const float Step = 1f / 90f;
        private const int MaxSubsteps = 8;
        private const float SleepEnergy = 0.02f;   // graus² (~0,14°)
        private const float SleepDelay = 0.5f;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        // ------------------------------------------------------------------ estado
        private Vector2 angle;          // x = pitch, y = roll (graus)
        private Vector2 angVel;         // graus/s
        private float twist, twistVel;
        private Vector2 prevAngVel;
        private Vector2[] antennaAngle, antennaVel, armAngle, armVel;
        private Quaternion headBase = Quaternion.identity, springBase = Quaternion.identity;
        private Vector3 headBasePos, springBaseScale = Vector3.one;
        private Quaternion[] antennaBase, armBase;
        private float accumulator;
        private float lowEnergyTime;
        private float lastSoftTime = -1f, lastHoverTime = -1f, lastHandTime = -1f;
        private Vector2 lastSign;

        private XRSimpleInteractable xrInteractable;
        private IXRSelectInteractor holder;
        private float holdStartTime;
        private bool pulling;
        private Vector3 grabPointWorld;
        private float grabDistance;

        private Transform pendingHand;
        private Vector3 pendingHandPos;
        private float pendingHandTime;
        private int pendingHandFrames;

        private MaterialPropertyBlock mpb;
        private Color visorBaseEmission = Color.black;
        private Transform cachedTransform;

        // ------------------------------------------------------------------ telemetria / testes
        public Vector2 AngularDisplacement => angle;
        public float DeflectionDegrees => angle.magnitude;
        public float TwistDegrees => twist;
        public float PeakSinceKick { get; private set; }
        public int BoingCount { get; private set; }
        public int SoftCount { get; private set; }
        public int TockCount { get; private set; }
        public bool IsSleeping => !enabled;
        public bool IsPulling => pulling;
        public Transform HeadTransform => headTransform;
        public float NaturalFrequency => naturalFrequency;
        /// <summary>Direção horizontal (mundo) para onde o topo da cabeça foi empurrado na última pancada.</summary>
        public Vector3 LastKickDirectionWorld { get; private set; }
        public float Energy => angle.sqrMagnitude + (angVel / naturalFrequency).sqrMagnitude
                               + twist * twist + Sqr(twistVel / (naturalFrequency * twistFrequencyRatio));

        private static float Sqr(float v) => v * v;

        // ------------------------------------------------------------------ ciclo de vida
        private void Awake()
        {
            cachedTransform = transform;
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.loop = false;
                audioSource.spatialBlend = 1f;
            }
            if (minKickForSound <= 0f) minKickForSound = minImpulseForSound;

            if (headTransform != null)
            {
                headBase = headTransform.localRotation;
                headBasePos = headTransform.localPosition;
            }
            if (neckSpring != null)
            {
                springBase = neckSpring.localRotation;
                springBaseScale = neckSpring.localScale;
            }
            antennaAngle = new Vector2[antennas.Length];
            antennaVel = new Vector2[antennas.Length];
            antennaBase = new Quaternion[antennas.Length];
            for (int i = 0; i < antennas.Length; i++) antennaBase[i] = antennas[i] != null ? antennas[i].localRotation : Quaternion.identity;
            armAngle = new Vector2[arms.Length];
            armVel = new Vector2[arms.Length];
            armBase = new Quaternion[arms.Length];
            for (int i = 0; i < arms.Length; i++) armBase[i] = arms[i] != null ? arms[i].localRotation : Quaternion.identity;

            mpb = new MaterialPropertyBlock();
            if (visorRenderer != null)
            {
                Material[] mats = visorRenderer.sharedMaterials;
                int idx = Mathf.Clamp(visorMaterialIndex, 0, mats.Length - 1);
                if (mats.Length > 0 && mats[idx] != null && mats[idx].HasProperty(EmissionColorId))
                    visorBaseEmission = mats[idx].GetColor(EmissionColorId);
            }
            if (visorGlowLight != null) visorGlowLight.shadows = LightShadows.None;

            xrInteractable = GetComponent<XRSimpleInteractable>();
            if (xrInteractable == null) xrInteractable = gameObject.AddComponent<XRSimpleInteractable>();
            xrInteractable.selectEntered.AddListener(OnSelectEntered);
            xrInteractable.selectExited.AddListener(OnSelectExited);
            xrInteractable.hoverEntered.AddListener(OnHoverEntered);
        }

        private void Start()
        {
            // Começa dormindo: nada roda até a primeira pancada.
            ApplyPose(0f);
            enabled = false;
        }

        private void OnDestroy()
        {
            if (xrInteractable == null) return;
            xrInteractable.selectEntered.RemoveListener(OnSelectEntered);
            xrInteractable.selectExited.RemoveListener(OnSelectExited);
            xrInteractable.hoverEntered.RemoveListener(OnHoverEntered);
        }

        // ------------------------------------------------------------------ API pública
        /// <summary>Pancada em graus: a cabeça sai ~amplitudeDeg do centro (x = pitch/frente, y = roll/lado).</summary>
        public void ApplyKick(Vector2 amplitudeDeg)
        {
            float amp = amplitudeDeg.magnitude;
            if (amp <= 0.0001f) return;
            angVel += amplitudeDeg * naturalFrequency;
            // Pancada forte também torce a cabeça (para um lado que depende da direção do golpe)
            if (amp > 18f)
            {
                float sign = (amplitudeDeg.x * amplitudeDeg.y) >= 0f ? 1f : -1f;
                twistVel += sign * (amp - 18f) * twistFromStrongKick * naturalFrequency * twistFrequencyRatio;
            }
            PeakSinceKick = 0f;
            Wake();
            if (amp >= minKickForSound) PlayBoing(amp);
        }

        /// <summary>Compatibilidade com o código antigo: soma velocidade angular direta (graus/s).</summary>
        public void ApplyImpulse(Vector2 impulseDegPerSecond)
        {
            ApplyKick(impulseDegPerSecond / Mathf.Max(0.01f, naturalFrequency));
        }

        /// <summary>Empurra o topo da cabeça na direção horizontal (mundo) indicada, com a amplitude em graus.</summary>
        public void KickTowards(Vector3 worldDirection, float amplitudeDeg)
        {
            Vector3 d = worldDirection;
            d.y = 0f;
            if (d.sqrMagnitude < 1e-6f) d = cachedTransform.forward;
            d.Normalize();
            LastKickDirectionWorld = d;
            ApplyKick(WorldDirToDeflection(d) * amplitudeDeg);
        }

        /// <summary>Peteleco: empurra para longe do ponto atingido (mundo).</summary>
        public void Flick(Vector3 worldHitPoint, float amplitudeDeg)
        {
            KickTowards(HeadCenter() - worldHitPoint, amplitudeDeg);
        }

        [ContextMenu("Boing Forte")] public void BoingStrong() => KickTowards(cachedTransform.right, 34f);
        [ContextMenu("Boing Médio")] public void BoingMedium() => KickTowards(cachedTransform.right, 22f);
        [ContextMenu("Boing Fraco")] public void BoingWeak() => KickTowards(cachedTransform.right, 10f);
        [ContextMenu("Boing Test")] public void BoingTest() => BoingMedium();

        // ------------------------------------------------------------------ eventos XRI
        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            IXRSelectInteractor who = args.interactorObject;
            Vector3 hitPoint;
            float flick = Random.Range(flickRange.x, flickRange.y);
            if (who is XRRayInteractor ray && ray.TryGetCurrent3DRaycastHit(out RaycastHit hit))
            {
                hitPoint = hit.point;
                grabDistance = hit.distance;
            }
            else
            {
                hitPoint = who != null ? who.transform.position : HeadCenter() - cachedTransform.forward;
                grabDistance = 0f;
            }
            Flick(hitPoint, flick);
            grabPointWorld = hitPoint;
            holder = who;
            holdStartTime = Time.time;
            pulling = false;
            Haptic(who, flick);
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            if (args.interactorObject != holder) return;
            holder = null;
            if (pulling)
            {
                pulling = false;
                // Solta com a velocidade que tinha; a deflexão acumulada vira um balanço grande.
                PeakSinceKick = 0f;
                if (angle.magnitude >= minKickForSound) PlayBoing(angle.magnitude);
                Wake();
            }
        }

        private void OnHoverEntered(HoverEnterEventArgs args)
        {
            if (args.interactorObject is XRRayInteractor)
            {
                if (Time.time < lastHoverTime + hoverCooldown) return;
                lastHoverTime = Time.time;
                Vector2 dir = Random.insideUnitCircle.normalized;
                ApplyKick(dir * Random.Range(hoverRange.x, hoverRange.y));
                Haptic(args.interactorObject, 5f);
            }
            else if (!(args.interactorObject is XRSocketInteractor))
            {
                BeginHandTouch(args.interactorObject.transform);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;
            XRBaseInteractor hand = other.GetComponentInParent<XRBaseInteractor>();
            if (hand == null || hand is XRRayInteractor || hand is XRSocketInteractor) return;
            BeginHandTouch(hand.transform);
        }

        private void BeginHandTouch(Transform hand)
        {
            if (hand == null || Time.time < lastHandTime + 0.15f) return;
            lastHandTime = Time.time;
            pendingHand = hand;
            pendingHandPos = hand.position;
            pendingHandTime = Time.time;
            pendingHandFrames = 2;
            Wake();
        }

        private void OnCollisionEnter(Collision collision)
        {
            float speed = collision.relativeVelocity.magnitude;
            if (speed < 0.2f) return;
            // relativeVelocity aponta do outro objeto para nós: a cabeça é empurrada nesse sentido
            Vector3 push = -collision.relativeVelocity;
            if (collision.contactCount > 0)
            {
                Vector3 fromContact = HeadCenter() - collision.GetContact(0).point;
                fromContact.y = 0f;
                if (fromContact.sqrMagnitude > 1e-6f) push = fromContact.normalized * speed + push * 0.5f;
            }
            KickTowards(push, Mathf.Min(maxImpactDegrees, speed * impactDegreesPerMps));
        }

        // ------------------------------------------------------------------ simulação
        private void Update()
        {
            if (headTransform == null) { enabled = false; return; }

            ProcessPendingHand();

            accumulator += Mathf.Min(Time.deltaTime, Step * MaxSubsteps);
            int steps = 0;
            while (accumulator >= Step && steps < MaxSubsteps)
            {
                Substep(Step);
                accumulator -= Step;
                steps++;
            }

            float energy = Energy;
            ApplyPose(energy);

            if (holder == null && pendingHandFrames <= 0 && energy < SleepEnergy)
            {
                lowEnergyTime += Time.deltaTime;
                if (lowEnergyTime >= SleepDelay) Sleep();
            }
            else lowEnergyTime = 0f;
        }

        private void Substep(float dt)
        {
            float w = naturalFrequency;
            prevAngVel = angVel;

            if (holder != null && Time.time >= holdStartTime + holdToPullDelay) pulling = true;

            if (pulling && TryGetPullTarget(out Vector2 target))
            {
                // Segue o ponto apontado com suavização; a velocidade resultante é a do "soltar".
                Vector2 next = Vector2.Lerp(angle, target, 1f - Mathf.Exp(-14f * dt));
                angVel = (next - angle) / dt;
                angle = next;
            }
            else
            {
                Vector2 acc = -2f * dampingRatio * w * angVel - w * w * angle;
                float mag = angle.magnitude;
                if (mag > softLimitAngle)
                {
                    // Limite elástico: a mola endurece além do ângulo suave (nada de clamp seco)
                    Vector2 radial = angle / mag;
                    acc -= radial * (mag - softLimitAngle) * (w * w * limitStiffness);
                }
                angVel += acc * dt;
                angle += angVel * dt;

                mag = angle.magnitude;
                if (mag > tockAngle)
                {
                    Vector2 radial = angle / mag;
                    float vr = Vector2.Dot(angVel, radial);
                    if (vr > 0f)
                    {
                        // Bate no ombro: quica devolvendo uma fração da velocidade radial
                        angVel -= radial * vr * (1f + limitRestitution);
                        PlayTock(vr);
                    }
                    if (mag > maxDeflectionAngle) angle = radial * maxDeflectionAngle;
                }
            }

            // Torção com acoplamento ao "rodopio" (momento angular do pêndulo 2D)
            float wy = w * twistFrequencyRatio;
            float swirl = angle.x * angVel.y - angle.y * angVel.x;
            float twistAcc = -2f * twistDamping * wy * twistVel - wy * wy * twist + swirl * twistCoupling * w;
            twistVel += twistAcc * dt;
            twist = Mathf.Clamp(twist + twistVel * dt, -maxTwistAngle, maxTwistAngle);

            // Antenas e braços: osciladores rápidos dirigidos pela aceleração da cabeça (ficam "para trás")
            Vector2 headAcc = (angVel - prevAngVel) / dt;
            StepSecondary(antennaAngle, antennaVel, antennaFrequency, antennaDamping, antennaGain, antennaMaxAngle, headAcc, angle, dt);
            StepSecondary(armAngle, armVel, armFrequency, armDamping, armGain, armMaxAngle, headAcc, angle * 0.25f, dt);

            // Som suave quando a cabeça cruza o centro com energia boa
            Vector2 sign = new Vector2(Mathf.Sign(angle.x), Mathf.Sign(angle.y));
            if (!pulling && (sign.x != lastSign.x || sign.y != lastSign.y) && angVel.magnitude > 9f * w
                && Time.time >= lastSoftTime + softSoundMinInterval)
            {
                lastSoftTime = Time.time;
                PlaySoft(angVel.magnitude / w);
            }
            lastSign = sign;

            float m2 = angle.magnitude;
            if (m2 > PeakSinceKick) PeakSinceKick = m2;
        }

        private static void StepSecondary(Vector2[] a, Vector2[] v, float w, float zeta, float gain, float maxA,
                                          Vector2 headAcc, Vector2 follow, float dt)
        {
            for (int i = 0; i < a.Length; i++)
            {
                Vector2 acc = -2f * zeta * w * v[i] - w * w * (a[i] - follow * 0.15f) - headAcc * gain;
                v[i] += acc * dt;
                a[i] = Vector2.ClampMagnitude(a[i] + v[i] * dt, maxA);
            }
        }

        private bool TryGetPullTarget(out Vector2 target)
        {
            target = angle;
            if (holder == null) return false;
            Vector3 point;
            if (holder is XRRayInteractor ray && grabDistance > 0f)
            {
                Transform o = ray.rayOriginTransform != null ? ray.rayOriginTransform : ray.transform;
                point = o.position + o.forward * grabDistance;
            }
            else point = holder.transform.position;

            Vector3 offset = point - grabPointWorld;
            offset.y = 0f;
            float dist = offset.magnitude;
            if (dist < 1e-4f) { target = Vector2.zero; return true; }
            float deg = Mathf.Min(maxPullAngle, Mathf.Atan2(dist, pullLeverArm) * Mathf.Rad2Deg);
            target = WorldDirToDeflection(offset / dist) * deg;
            return true;
        }

        private void ProcessPendingHand()
        {
            if (pendingHandFrames <= 0 || pendingHand == null) return;
            pendingHandFrames--;
            if (pendingHandFrames > 0) return;
            float dt = Mathf.Max(Time.time - pendingHandTime, 0.008f);
            Vector3 v = (pendingHand.position - pendingHandPos) / dt;
            v.y = 0f;
            float speed = v.magnitude;
            Vector3 dir = speed > 0.1f ? v : HeadCenter() - pendingHand.position;
            float amp = Mathf.Lerp(handTouchRange.x, handTouchRange.y, Mathf.Clamp01(speed / handSpeedForMax));
            KickTowards(dir, amp);
            Haptic(pendingHand.GetComponent<IXRInteractor>(), amp);
            pendingHand = null;
        }

        private void ApplyPose(float energy)
        {
            if (headTransform == null) return;
            Quaternion swing = Quaternion.Euler(angle.x, twist, angle.y);
            float stretch = springMaxStretch * Mathf.Clamp01(angVel.magnitude / (naturalFrequency * 30f));

            if (neckSpring != null)
            {
                neckSpring.localRotation = springBase * Quaternion.Euler(angle.x * 0.5f, twist * 0.5f, angle.y * 0.5f);
                Vector3 s = springBaseScale;
                s.y *= 1f + stretch;
                neckSpring.localScale = s;
            }
            headTransform.localRotation = headBase * swing;
            // A cabeça sobe junto com a mola esticada (0,026 m de mola x stretch)
            headTransform.localPosition = headBasePos + Vector3.up * (0.026f * stretch);

            for (int i = 0; i < antennas.Length; i++)
                if (antennas[i] != null) antennas[i].localRotation = antennaBase[i] * Quaternion.Euler(antennaAngle[i].x, 0f, antennaAngle[i].y);
            for (int i = 0; i < arms.Length; i++)
                if (arms[i] != null) arms[i].localRotation = armBase[i] * Quaternion.Euler(armAngle[i].x, 0f, armAngle[i].y * (i % 2 == 0 ? 1f : -1f));

            float excite = Mathf.Clamp01(Mathf.Sqrt(energy) / 30f);
            if (visorRenderer != null)
            {
                visorRenderer.GetPropertyBlock(mpb, visorMaterialIndex);
                mpb.SetColor(EmissionColorId, Color.Lerp(visorBaseEmission, visorExcitedEmission, excite));
                visorRenderer.SetPropertyBlock(mpb, visorMaterialIndex);
            }
            if (visorGlowLight != null) visorGlowLight.intensity = Mathf.Lerp(visorLightRange.x, visorLightRange.y, excite);
        }

        private void Wake()
        {
            lowEnergyTime = 0f;
            if (!enabled) { accumulator = 0f; enabled = true; }
        }

        private void Sleep()
        {
            angle = angVel = Vector2.zero;
            twist = twistVel = 0f;
            for (int i = 0; i < antennaAngle.Length; i++) antennaAngle[i] = antennaVel[i] = Vector2.zero;
            for (int i = 0; i < armAngle.Length; i++) armAngle[i] = armVel[i] = Vector2.zero;
            accumulator = 0f;
            lowEnergyTime = 0f;
            ApplyPose(0f);
            enabled = false;
        }

        // ------------------------------------------------------------------ utilidades
        /// <summary>Converte direção horizontal do mundo em (pitch, roll) unitário no espaço do pai da cabeça.</summary>
        private Vector2 WorldDirToDeflection(Vector3 worldDir)
        {
            Transform frame = headTransform != null && headTransform.parent != null ? headTransform.parent : cachedTransform;
            Vector3 local = Quaternion.Inverse(frame.rotation * headBase) * worldDir;
            // Rotação +X leva o topo (+Y) para +Z; rotação +Z leva o topo para -X.
            Vector2 d = new Vector2(local.z, -local.x);
            float m = d.magnitude;
            return m > 1e-5f ? d / m : new Vector2(1f, 0f);
        }

        private Vector3 HeadCenter() =>
            headTransform != null ? headTransform.position + headTransform.up * 0.045f : cachedTransform.position + Vector3.up * 0.14f;

        private void PlayBoing(float amp)
        {
            if (audioSource == null || sfxBoing == null) return;
            BoingCount++;
            audioSource.pitch = Mathf.Clamp(0.92f + amp / 120f + Random.Range(-0.04f, 0.04f), 0.85f, 1.25f);
            audioSource.PlayOneShot(sfxBoing, Mathf.Clamp01(amp / 35f) * boingVolume);
        }

        private void PlaySoft(float amp)
        {
            if (audioSource == null || sfxSoft == null) return;
            SoftCount++;
            audioSource.pitch = Random.Range(0.94f, 1.08f);
            audioSource.PlayOneShot(sfxSoft, Mathf.Clamp01(amp / 30f) * softVolume);
        }

        private void PlayTock(float radialSpeed)
        {
            TockCount++;
            if (audioSource == null || sfxTock == null) return;
            audioSource.pitch = Random.Range(0.95f, 1.06f);
            audioSource.PlayOneShot(sfxTock, Mathf.Clamp01(radialSpeed / (naturalFrequency * 25f)) * tockVolume + 0.1f);
        }

        private static void Haptic(IXRInteractor who, float amplitudeDeg)
        {
            if (who == null) return;
            VRFeedbackManager.Instance?.TriggerHaptic(who, Mathf.Clamp(amplitudeDeg / 35f, 0.1f, 0.8f), 0.08f);
        }
    }
}
