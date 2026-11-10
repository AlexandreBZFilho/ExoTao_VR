using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;

namespace VRRehab.Interactive.Task07
{
    /// <summary>
    /// Balão flutuante antigravidade com dinâmica de empuxo suave, oscilação de vento,
    /// cordinha pendurada física (LineRenderer) e detecção de estouro multissensorial
    /// (toque físico das mãos VR, disparo com laser raycast ou colisão ambiental).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    [SelectionBase]
    public class FloatingBalloon : XRSimpleInteractable
    {
        [Header("Dinâmica de Flutuação Antigravidade")]
        [Tooltip("Aceleração vertical de empuxo (m/s²).")]
        [SerializeField] private float buoyancyAcceleration = 0.22f;

        [Tooltip("Velocidade máxima de subida (m/s).")]
        [SerializeField] private float maxAscentSpeed = 0.45f;

        [Tooltip("Teto máximo da sala antes de estabilizar (metros).")]
        [SerializeField] private float ceilingHeightLimit = 2.85f;

        [Tooltip("Intensidade da brisa/turbulência ambiente suave.")]
        [SerializeField] private float ambientWindForce = 0.08f;

        [Header("Cordinha Pendurada (String)")]
        [Tooltip("Comprimento da cordinha pendurada (metros).")]
        [SerializeField] private float stringLength = 0.40f;

        [Tooltip("Quantidade de nós/segmentos na cordinha.")]
        [SerializeField] private int stringSegments = 6;

        [SerializeField] private LineRenderer stringRenderer;
        [SerializeField] private Transform stringAnchor;

        [Header("Efeitos de Estouro (Pop VFX & Audio)")]
        [SerializeField] private ParticleSystem burstParticleSystem;
        [SerializeField] private AudioClip popAudioClip;
        [SerializeField] private AudioSource audioSource;

        [Header("Estado & Proteção")]
        [Tooltip("Tempo de imunidade logo após sair do bico da pistola (segundos).")]
        [SerializeField] private float launchGracePeriod = 0.25f;

        private Rigidbody rb;
        private MeshRenderer meshRenderer;
        private Collider balloonCollider;
        private bool isPopped = false;
        private float spawnTime;
        private float perlinSeedX;
        private float perlinSeedZ;
        private Vector3[] stringPositions;

        public bool IsPopped => isPopped;

        protected override void Awake()
        {
            base.Awake();

            rb = GetComponent<Rigidbody>();
            meshRenderer = GetComponentInChildren<MeshRenderer>();
            balloonCollider = GetComponent<Collider>();

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.spatialBlend = 1.0f;
                    audioSource.playOnAwake = false;
                    audioSource.rolloffMode = AudioRolloffMode.Linear;
                    audioSource.minDistance = 0.5f;
                    audioSource.maxDistance = 15.0f;
                }
            }

            // Configuração física de baixa inércia e arrasto aerodinâmico
            if (rb != null)
            {
                rb.mass = 0.012f;
                rb.linearDamping = 1.35f;
                rb.angularDamping = 1.60f;
                rb.useGravity = false;
            }

            perlinSeedX = Random.Range(0f, 1000f);
            perlinSeedZ = Random.Range(0f, 1000f);

            InitializeString();
            spawnTime = Time.time;
        }

        private void InitializeString()
        {
            if (stringAnchor == null)
            {
                Transform foundAnchor = transform.Find("String_Anchor");
                stringAnchor = foundAnchor != null ? foundAnchor : transform;
            }

            if (stringRenderer == null)
            {
                stringRenderer = GetComponent<LineRenderer>();
            }

            if (stringRenderer != null)
            {
                stringRenderer.positionCount = stringSegments;
                stringRenderer.startWidth = 0.0035f;
                stringRenderer.endWidth = 0.0020f;
                stringRenderer.useWorldSpace = true;
                stringPositions = new Vector3[stringSegments];
            }
        }

        private void FixedUpdate()
        {
            if (isPopped || rb == null || rb.isKinematic) return;

            // 1. Empuxo vertical antigravidade suave
            Vector3 currentVel = rb.linearVelocity;
            float currentY = transform.position.y;

            if (currentY < ceilingHeightLimit)
            {
                // Aplica empuxo até atingir maxAscentSpeed
                if (currentVel.y < maxAscentSpeed)
                {
                    rb.AddForce(Vector3.up * buoyancyAcceleration, ForceMode.Acceleration);
                }
            }
            else
            {
                // Amortecimento suave próximo ao teto da sala
                if (currentVel.y > 0f)
                {
                    rb.linearVelocity = new Vector3(currentVel.x, currentVel.y * 0.85f, currentVel.z);
                }
            }

            // 2. Turbulência orgânica de brisa indoor (Perlin noise)
            float t = Time.time * 0.6f;
            float windX = (Mathf.PerlinNoise(perlinSeedX + t, 0f) - 0.5f) * 2f * ambientWindForce;
            float windZ = (Mathf.PerlinNoise(0f, perlinSeedZ + t) - 0.5f) * 2f * ambientWindForce;
            rb.AddForce(new Vector3(windX, 0f, windZ), ForceMode.Acceleration);

            // 3. Torque sutil de alinhamento vertical
            Quaternion targetUp = Quaternion.FromToRotation(transform.up, Vector3.up) * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetUp, Time.fixedDeltaTime * 1.5f);
        }

        private void LateUpdate()
        {
            if (isPopped || stringRenderer == null || !stringRenderer.enabled) return;

            UpdateHangingString();
        }

        private void UpdateHangingString()
        {
            Vector3 anchorPos = stringAnchor != null ? stringAnchor.position : transform.position;
            stringPositions[0] = anchorPos;

            // Simula física pendular simples para a cordinha
            Vector3 vel = rb != null ? rb.linearVelocity : Vector3.zero;
            Vector3 dragLag = -vel * 0.12f;

            float segmentLength = stringLength / (stringSegments - 1);

            for (int i = 1; i < stringSegments; i++)
            {
                float frac = (float)i / (stringSegments - 1);
                // Curvatura catenária com leve oscilação senoidal
                float wave = Mathf.Sin(Time.time * 3.0f + i * 0.8f) * 0.012f * frac;
                Vector3 offset = new Vector3(wave, -segmentLength * i, 0f) + dragLag * frac;
                stringPositions[i] = anchorPos + offset;
            }

            stringRenderer.SetPositions(stringPositions);
        }

        #region Interação XRI & Disparo com Laser Raycast
        protected override void OnActivated(ActivateEventArgs args)
        {
            base.OnActivated(args);
            // Ao mirar o raio laser no balão e puxar o gatilho, estoura!
            Pop(args.interactorObject as IXRInteractor);
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            // Ao agarrar ou pinçar com a mão VR, estoura!
            Pop(args.interactorObject as IXRInteractor);
        }
        #endregion

        #region Detecção de Colisão Física
        private void OnTriggerEnter(Collider other)
        {
            CheckCollisionAndPop(other.gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            CheckCollisionAndPop(collision.gameObject);
        }

        private void CheckCollisionAndPop(GameObject hitObj)
        {
            if (isPopped) return;

            // Ignora colisões durante o período de graça pós-lançamento da arma
            if (Time.time - spawnTime < launchGracePeriod)
            {
                if (hitObj.GetComponentInParent<BalloonInflaterGun>() != null)
                {
                    return;
                }
            }

            // Estoura ao encostar em mãos VR, controles, raios ou projéteis
            string hitName = hitObj.name.ToLowerInvariant();
            bool isHandOrController = hitName.Contains("hand") || hitName.Contains("controller") ||
                                      hitName.Contains("direct") || hitName.Contains("ray") ||
                                      hitName.Contains("interactor") || hitObj.CompareTag("Player");

            if (isHandOrController)
            {
                Pop(null);
            }
            else if (Time.time - spawnTime >= launchGracePeriod)
            {
                // Depois do período de graça, objetos sólidos ou chão também estouram o balão
                Pop(null);
            }
        }
        #endregion

        /// <summary>
        /// Executa o estouro multissensorial do balão (áudio POP, partículas de borracha e haptics).
        /// </summary>
        public void Pop(IXRInteractor triggeringInteractor = null)
        {
            if (isPopped) return;
            isPopped = true;

            // 1. Áudio de estouro de alta fidelidade
            if (popAudioClip != null && audioSource != null)
            {
                audioSource.pitch = Random.Range(0.92f, 1.08f);
                audioSource.PlayOneShot(popAudioClip, 1.0f);
            }
            else if (VRFeedbackManager.Instance != null)
            {
                VRFeedbackManager.Instance.PlayRelease(transform.position);
            }

            // 2. Feedback Háptico no controle do paciente
            if (triggeringInteractor != null && VRFeedbackManager.Instance != null)
            {
                VRFeedbackManager.Instance.TriggerHaptic(triggeringInteractor, 0.85f, 0.12f);
            }

            // 3. Efeito de Partículas de borracha / confetes
            if (burstParticleSystem != null)
            {
                burstParticleSystem.transform.SetParent(null);
                burstParticleSystem.transform.position = transform.position;
                burstParticleSystem.Play();
                Destroy(burstParticleSystem.gameObject, 2.0f);
            }

            // 4. Desabilita visuais e colisores instantaneamente
            if (meshRenderer != null) meshRenderer.enabled = false;
            if (balloonCollider != null) balloonCollider.enabled = false;
            if (stringRenderer != null) stringRenderer.enabled = false;

            if (rb != null)
            {
                rb.isKinematic = true;
            }

            // 5. Destrói o GameObject após a reprodução do som
            Destroy(gameObject, 0.8f);
        }

        /// <summary>
        /// Aplica material colorido e propaga a cor para o sistema de partículas de estouro.
        /// </summary>
        public void ApplyColorMaterial(Material material, Color burstColor)
        {
            if (meshRenderer == null) meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (meshRenderer != null && material != null)
            {
                meshRenderer.sharedMaterial = material;
            }

            if (burstParticleSystem == null) burstParticleSystem = GetComponentInChildren<ParticleSystem>();
            if (burstParticleSystem != null)
            {
                var main = burstParticleSystem.main;
                main.startColor = new ParticleSystem.MinMaxGradient(burstColor);
            }
        }

        /// <summary>
        /// Lança o balão com velocidade inicial a partir do bico da pistola.
        /// </summary>
        public void Launch(Vector3 velocity)
        {
            spawnTime = Time.time;
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.linearVelocity = velocity;
            }
        }
    }
}
