using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;

namespace VRRehab.Interactive.Task13
{
    /// <summary>
    /// Cogumelo Xilofone Pentatônico interativo.
    /// Reage a hover do raio laser VR, clique de gatilho ou toque físico.
    /// Toca a nota musical afinada correspondente, anima efeito elástico de Squash & Stretch,
    /// pulsa a bioluminescência do chapéu e gera partículas cintilantes.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class MusicalMushroom : MonoBehaviour
    {
        [Header("Afinação & Nota Musical")]
        [Tooltip("Índice da nota (0=C4, 1=D4, 2=E4, 3=G4, 4=A4)")]
        [SerializeField] private int noteIndex = 0;
        [SerializeField] private string noteName = "C4";
        [SerializeField] private float frequencyHz = 261.63f;
        [SerializeField] private AudioClip noteClip;

        [Header("Identidade Visual & Bioluminescência")]
        [SerializeField] private Renderer capRenderer;
        [SerializeField] private Renderer glowDotsRenderer;
        [ColorUsage(true, true)] [SerializeField] private Color basalGlowColor = new Color(0.0f, 0.95f, 1.0f, 1.0f);
        [ColorUsage(true, true)] [SerializeField] private Color flashGlowColor = new Color(0.70f, 1.0f, 1.0f, 1.0f);
        [SerializeField] private float flashIntensityMultiplier = 3.5f;

        [Header("Dinâmica Elástica (Squash & Stretch)")]
        [SerializeField] private Transform capTransform;
        [SerializeField] private float springStiffness = 380f;
        [SerializeField] private float springDamping = 16f;
        [SerializeField] private float initialImpactImpulse = -0.42f;

        [Header("Partículas de Música")]
        [SerializeField] private ParticleSystem noteSparklesPs;

        [Header("Configuração de Áudio")]
        [SerializeField] private AudioSource localAudioSource;

        private XRSimpleInteractable simpleInteractable;
        private MaterialPropertyBlock mpb;
        private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");

        private Vector3 initialCapScale = Vector3.one;
        private float scaleDisplacement = 0f;
        private float scaleVelocity = 0f;
        private bool isSpringActive = false;

        private Coroutine glowFlashCoroutine;
        private float lastTriggerTime = -10f;
        private const float TriggerCooldown = 0.08f; // Permite arpeggios rápidos fluídos sem spam de colisão dupla

        public int NoteIndex => noteIndex;
        public string NoteName => noteName;
        public float FrequencyHz => frequencyHz;

        private void Awake()
        {
            if (capTransform == null)
            {
                Transform capChild = transform.Find("Mushroom_Cap");
                capTransform = capChild != null ? capChild : transform;
            }
            initialCapScale = capTransform.localScale;

            if (capRenderer == null)
            {
                Transform capChild = transform.Find("Mushroom_Cap");
                if (capChild != null) capRenderer = capChild.GetComponent<Renderer>();
            }

            if (glowDotsRenderer == null)
            {
                Transform dotsChild = transform.Find("Mushroom_Glow_Spots");
                if (dotsChild != null) glowDotsRenderer = dotsChild.GetComponent<Renderer>();
            }

            if (localAudioSource == null)
            {
                localAudioSource = GetComponent<AudioSource>();
                if (localAudioSource == null)
                {
                    localAudioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            localAudioSource.playOnAwake = false;
            localAudioSource.spatialBlend = 1.0f; // 3D total
            localAudioSource.minDistance = 0.8f;
            localAudioSource.maxDistance = 25.0f;
            localAudioSource.rolloffMode = AudioRolloffMode.Linear;
            localAudioSource.dopplerLevel = 0f;

            mpb = new MaterialPropertyBlock();

            // Configura XRSimpleInteractable
            simpleInteractable = GetComponent<XRSimpleInteractable>();
            if (simpleInteractable == null)
            {
                simpleInteractable = gameObject.AddComponent<XRSimpleInteractable>();
            }

            simpleInteractable.hoverEntered.AddListener(OnXRIHoverEntered);
            simpleInteractable.selectEntered.AddListener(OnXRISelectEntered);

            // Garante estado inicial do emissivo
            ApplyGlowColor(basalGlowColor);
        }

        private void OnDestroy()
        {
            if (simpleInteractable != null)
            {
                simpleInteractable.hoverEntered.RemoveListener(OnXRIHoverEntered);
                simpleInteractable.selectEntered.RemoveListener(OnXRISelectEntered);
            }
        }

        public void Configure(int index, string name, float freq, AudioClip clip, Color baseGlow, Color flashGlow, Renderer capRend, Renderer dotsRend, ParticleSystem sparkles)
        {
            noteIndex = index;
            noteName = name;
            frequencyHz = freq;
            noteClip = clip;
            basalGlowColor = baseGlow;
            flashGlowColor = flashGlow;
            capRenderer = capRend;
            glowDotsRenderer = dotsRend;
            noteSparklesPs = sparkles;

            ApplyGlowColor(basalGlowColor);
        }

        private void OnXRIHoverEntered(HoverEnterEventArgs args)
        {
            TriggerNote(args.interactorObject);
        }

        private void OnXRISelectEntered(SelectEnterEventArgs args)
        {
            TriggerNote(args.interactorObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.name.ToLower().Contains("hand") || other.name.ToLower().Contains("controller") || other.CompareTag("Player"))
            {
                TriggerNote(null);
            }
        }

        /// <summary>
        /// Aciona a execução da nota musical do cogumelo com todos os feedbacks.
        /// </summary>
        public void TriggerNote(IXRInteractor interactor = null)
        {
            if (Time.time - lastTriggerTime < TriggerCooldown) return;
            lastTriggerTime = Time.time;

            // 1. Reprodução Sonora Afinada
            if (noteClip != null && localAudioSource != null)
            {
                localAudioSource.pitch = Random.Range(0.992f, 1.008f); // Nuance acústica sutil
                localAudioSource.PlayOneShot(noteClip, 1.0f);
            }

            // 2. Animação Mecânica Elástica (Squash & Stretch)
            scaleDisplacement = initialImpactImpulse;
            scaleVelocity = 0f;
            isSpringActive = true;

            // 3. Flash Bioluminescente
            if (glowFlashCoroutine != null) StopCoroutine(glowFlashCoroutine);
            glowFlashCoroutine = StartCoroutine(GlowFlashRoutine());

            // 4. Partículas Cintilantes
            if (noteSparklesPs != null)
            {
                noteSparklesPs.Play();
            }

            // 5. Háptico no controle VR
            if (interactor is XRBaseControllerInteractor controllerInteractor)
            {
                controllerInteractor.SendHapticImpulse(0.35f, 0.08f);
            }
        }

        private void Update()
        {
            if (isSpringActive && capTransform != null)
            {
                // Sistema físico oscilador harmônico amortecido (Spring-Damper)
                float springForce = -springStiffness * scaleDisplacement;
                float dampingForce = -springDamping * scaleVelocity;
                float acceleration = springForce + dampingForce;

                scaleVelocity += acceleration * Time.deltaTime;
                scaleDisplacement += scaleVelocity * Time.deltaTime;

                // Efeito elástico de conservação de volume: quando Y achata, X e Z expandem
                float yFactor = 1.0f + scaleDisplacement;
                float xzFactor = 1.0f - (scaleDisplacement * 0.45f);

                capTransform.localScale = new Vector3(
                    initialCapScale.x * xzFactor,
                    initialCapScale.y * Mathf.Max(0.2f, yFactor),
                    initialCapScale.z * xzFactor
                );

                if (Mathf.Abs(scaleDisplacement) < 0.005f && Mathf.Abs(scaleVelocity) < 0.02f)
                {
                    scaleDisplacement = 0f;
                    scaleVelocity = 0f;
                    isSpringActive = false;
                    capTransform.localScale = initialCapScale;
                }
            }
        }

        private IEnumerator GlowFlashRoutine()
        {
            float duration = 0.38f;
            float elapsed = 0f;

            Color targetFlash = flashGlowColor * flashIntensityMultiplier;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // Decaimento exponencial rápido do flash para retorno suave à cor basal
                float decay = Mathf.Exp(-4.5f * t);
                Color currentColor = Color.Lerp(basalGlowColor, targetFlash, decay);
                ApplyGlowColor(currentColor);
                yield return null;
            }

            ApplyGlowColor(basalGlowColor);
            glowFlashCoroutine = null;
        }

        private void ApplyGlowColor(Color color)
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();

            if (glowDotsRenderer != null)
            {
                glowDotsRenderer.GetPropertyBlock(mpb);
                mpb.SetColor(EmissionColorProp, color);
                glowDotsRenderer.SetPropertyBlock(mpb);
            }

            if (capRenderer != null)
            {
                capRenderer.GetPropertyBlock(mpb);
                // O chapéu recebe um brilho secundário suave correspondente
                mpb.SetColor(EmissionColorProp, color * 0.40f);
                capRenderer.SetPropertyBlock(mpb);
            }
        }
    }
}
