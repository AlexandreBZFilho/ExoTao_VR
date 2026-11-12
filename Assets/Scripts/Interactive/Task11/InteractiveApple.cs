using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;
using VRRehab.DronePet;
using VRRehab.Ergonomics;

namespace VRRehab.Interactive.Task11
{
    public enum AppleType
    {
        NormalRed,
        GoldenBonus
    }

    /// <summary>
    /// Maçã Interativa de Alta Fidelidade para o mapa da Floresta (TASK-11).
    /// Herda de LaserSwingThrowInteractable (Física de Chicote por Raio Laser, Force Grab, Auto-Respawn, Aim Assist),
    /// possui física hidrodinâmica com splash no rio e som de impacto na terra.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    [DisallowMultipleComponent]
    public class InteractiveApple : LaserSwingThrowInteractable, IAimAssistTarget
    {
        [Header("Tipo de Maçã & Pontuação")]
        [SerializeField] private AppleType appleType = AppleType.NormalRed;
        [SerializeField] private int scoreValue = 10;

        [Header("Efeitos Sonoros")]
        [SerializeField] private AudioClip sfxGroundImpact;
        [SerializeField] private AudioClip sfxWaterSplash;
        [SerializeField] private AudioClip sfxGoldenSparkle;

        [Header("Física Hidrodinâmica no Rio")]
        [Tooltip("Cota de altitude Y da superfície da água do rio")]
        [SerializeField] private float waterLevelY = 2.50f;
        [Tooltip("Força de empuxo da água")]
        [SerializeField] private float buoyancyStrength = 14.0f;
        [Tooltip("Amortecimento linear na água")]
        [SerializeField] private float waterLinearDrag = 3.5f;
        [Tooltip("Amortecimento angular na água")]
        [SerializeField] private float waterAngularDrag = 2.5f;

        [Header("Partículas")]
        [SerializeField] private ParticleSystem waterSplashFx;
        [SerializeField] private ParticleSystem goldenSparkleFx;

        // IAimAssistTarget implementation
        public Vector3 AimTargetPosition => transform.position;
        public float AimTargetRadius => 0.085f;
        public bool IsAimTargetActive => !isFloatingInWater && !isSelected;
        public Transform TargetTransform => transform;

        public AppleType Type => appleType;
        public int ScoreValue => scoreValue;
        public bool IsFloatingInWater => isFloatingInWater;
        public bool IsGrabbed => isSelected;

        private Rigidbody appleRb;
        private SphereCollider appleCollider;
        private bool isFloatingInWater = false;
        private bool hasSplashed = false;
        private float originalLinearDrag = 0.05f;
        private float originalAngularDrag = 0.05f;
        private float lastImpactSoundTime = -1f;

        protected override void Awake()
        {
            base.Awake();

            appleRb = GetComponent<Rigidbody>();
            appleCollider = GetComponent<SphereCollider>();

            if (appleRb != null)
            {
                originalLinearDrag = appleRb.linearDamping;
                originalAngularDrag = appleRb.angularDamping;
                appleRb.mass = 0.18f;
                appleRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            if (appleType == AppleType.GoldenBonus)
            {
                scoreValue = 50;
                if (goldenSparkleFx != null && !goldenSparkleFx.isPlaying)
                {
                    goldenSparkleFx.Play();
                }
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            AimAssistTargetRegistry.Register(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            AimAssistTargetRegistry.Unregister(this);
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);

            // Se for maçã dourada, toca áudio cintilante mágico ao ser apanhada
            if (appleType == AppleType.GoldenBonus && sfxGoldenSparkle != null)
            {
                VRFeedbackManager.Instance?.PlaySound(sfxGoldenSparkle, transform.position, 0.75f, 0.98f, 1.02f);
            }

            // Se estava na água e foi apanhada, restaura física normal
            if (isFloatingInWater)
            {
                ExitWater();
            }
        }

        private void FixedUpdate()
        {
            // Checagem de imersão no rio
            if (!isSelected && appleRb != null && !appleRb.isKinematic)
            {
                CheckWaterInteraction();
            }
        }

        private void CheckWaterInteraction()
        {
            float currentY = transform.position.y;
            float submergedDepth = waterLevelY - currentY;

            if (submergedDepth > -0.05f) // Em contato com o plano d'água
            {
                if (!isFloatingInWater)
                {
                    EnterWater();
                }

                // Aplica empuxo hidrodinâmico e bobbing suave
                float depthClamped = Mathf.Clamp(submergedDepth + 0.05f, 0f, 0.25f);
                float waveOscillation = Mathf.Sin(Time.time * 2.5f + transform.position.x * 2.0f) * 0.015f;
                Vector3 buoyantForce = Vector3.up * ((depthClamped + waveOscillation) * buoyancyStrength * appleRb.mass * 9.81f);

                appleRb.AddForce(buoyantForce, ForceMode.Force);

                // Arrasto na água
                appleRb.linearDamping = waterLinearDrag;
                appleRb.angularDamping = waterAngularDrag;
            }
            else if (isFloatingInWater)
            {
                ExitWater();
            }
        }

        private void EnterWater()
        {
            isFloatingInWater = true;

            if (!hasSplashed)
            {
                hasSplashed = true;

                // Toca som de splash no rio
                if (sfxWaterSplash != null)
                {
                    VRFeedbackManager.Instance?.PlaySound(sfxWaterSplash, transform.position, 0.90f, 0.95f, 1.05f);
                }

                // Emite partículas de splash aquático
                if (waterSplashFx != null)
                {
                    waterSplashFx.transform.position = new Vector3(transform.position.x, waterLevelY + 0.02f, transform.position.z);
                    waterSplashFx.Play();
                }
            }
        }

        private void ExitWater()
        {
            isFloatingInWater = false;
            hasSplashed = false;

            if (appleRb != null)
            {
                appleRb.linearDamping = originalLinearDrag;
                appleRb.angularDamping = originalAngularDrag;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Evita som se acabou de tocar em outro trigger ou se foi em velocidade desprezível
            if (collision.relativeVelocity.magnitude < 0.6f) return;
            if (Time.time - lastImpactSoundTime < 0.20f) return;

            // Ignora se estiver na água
            if (isFloatingInWater) return;

            // Impacto orgânico de queda na terra/pedra
            lastImpactSoundTime = Time.time;
            if (sfxGroundImpact != null)
            {
                float vol = Mathf.Clamp01(collision.relativeVelocity.magnitude / 5.0f) * 0.85f;
                VRFeedbackManager.Instance?.PlaySound(sfxGroundImpact, collision.contacts[0].point, vol, 0.92f, 1.08f);
            }
        }

        /// <summary>
        /// Configuração via script do builder
        /// </summary>
        public void Configure(AppleType type, AudioClip groundSfx, AudioClip waterSfx, AudioClip goldenSfx, ParticleSystem splashFx, ParticleSystem sparkleFx)
        {
            appleType = type;
            scoreValue = (type == AppleType.GoldenBonus) ? 50 : 10;
            sfxGroundImpact = groundSfx;
            sfxWaterSplash = waterSfx;
            sfxGoldenSparkle = goldenSfx;
            waterSplashFx = splashFx;
            goldenSparkleFx = sparkleFx;
        }
    }
}
