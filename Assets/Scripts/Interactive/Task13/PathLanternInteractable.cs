using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;

namespace VRRehab.Interactive.Task13
{
    /// <summary>
    /// Lanterna Rúnica Rústica de Trilha.
    /// Reage ao raio laser VR, toque ou aproximação do paciente.
    /// Ao ser acesa, projeta luz pontual dourada suave, chamas com oscilação orgânica,
    /// faíscas ascendentes e ilumina as runas mágicas esculpidas no poste.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class PathLanternInteractable : MonoBehaviour
    {
        [Header("Componentes Visuais da Lanterna")]
        [SerializeField] private GameObject flameObject;
        [SerializeField] private Light lanternLight;
        [SerializeField] private Renderer runesRenderer;
        [SerializeField] private ParticleSystem flameSparksPs;

        [Header("Parâmetros de Iluminação e Chama")]
        [SerializeField] private bool startLit = false;
        [SerializeField] private float targetLightIntensity = 1.8f;
        [SerializeField] private float lightRange = 4.5f;
        [ColorUsage(true, true)] [SerializeField] private Color flameColor = new Color(1.0f, 0.68f, 0.18f, 1.0f);
        [ColorUsage(true, true)] [SerializeField] private Color unlitRuneColor = new Color(0.18f, 0.12f, 0.04f, 1.0f);
        [ColorUsage(true, true)] [SerializeField] private Color litRuneColor = new Color(1.0f, 0.72f, 0.15f, 1.0f);

        [Header("Áudio")]
        [SerializeField] private AudioClip igniteSfx;
        [SerializeField] private AudioSource audioSource;

        [Header("Detecção de Aproximação")]
        [SerializeField] private bool autoIgniteOnProximity = true;
        [SerializeField] private float proximityDistance = 2.8f;

        private XRSimpleInteractable simpleInteractable;
        private MaterialPropertyBlock mpb;
        private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");

        private bool isLit = false;
        private Coroutine lightRoutine;
        private Transform playerCameraTransform;
        private Vector3 initialFlameScale = Vector3.one;

        public bool IsLit => isLit;

        private void Awake()
        {
            if (flameObject == null)
            {
                Transform flameChild = transform.Find("Lantern_Flame");
                if (flameChild != null) flameObject = flameChild.gameObject;
            }
            if (flameObject != null)
            {
                initialFlameScale = flameObject.transform.localScale;
            }

            if (lanternLight == null)
            {
                lanternLight = GetComponentInChildren<Light>();
            }

            if (runesRenderer == null)
            {
                Transform runesChild = transform.Find("Lantern_Runes");
                if (runesChild != null) runesRenderer = runesChild.GetComponent<Renderer>();
            }

            if (flameSparksPs == null)
            {
                flameSparksPs = GetComponentInChildren<ParticleSystem>();
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1.0f;
            audioSource.minDistance = 1.0f;
            audioSource.maxDistance = 20.0f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;

            mpb = new MaterialPropertyBlock();

            // Configura XRSimpleInteractable
            simpleInteractable = GetComponent<XRSimpleInteractable>();
            if (simpleInteractable == null)
            {
                simpleInteractable = gameObject.AddComponent<XRSimpleInteractable>();
            }

            simpleInteractable.hoverEntered.AddListener(OnXRIHoverEntered);
            simpleInteractable.selectEntered.AddListener(OnXRISelectEntered);

            // Estado inicial
            if (startLit)
            {
                SetLitInstant(true);
            }
            else
            {
                SetLitInstant(false);
            }
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

        public void Configure(GameObject flame, Light lightComp, Renderer runes, ParticleSystem sparks, AudioClip sfx, bool initiallyLit)
        {
            flameObject = flame;
            lanternLight = lightComp;
            runesRenderer = runes;
            flameSparksPs = sparks;
            igniteSfx = sfx;
            startLit = initiallyLit;

            if (flameObject != null) initialFlameScale = flameObject.transform.localScale;
            SetLitInstant(initiallyLit);
        }

        private void OnXRIHoverEntered(HoverEnterEventArgs args)
        {
            if (!isLit) Ignite(args.interactorObject);
        }

        private void OnXRISelectEntered(SelectEnterEventArgs args)
        {
            if (!isLit) Ignite(args.interactorObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isLit && (other.name.ToLower().Contains("hand") || other.name.ToLower().Contains("controller") || other.CompareTag("Player")))
            {
                Ignite(null);
            }
        }

        private void Update()
        {
            // Auto ignição por proximidade quando o paciente caminha pela trilha
            if (!isLit && autoIgniteOnProximity && playerCameraTransform != null)
            {
                float distSq = (playerCameraTransform.position - transform.position).sqrMagnitude;
                if (distSq <= proximityDistance * proximityDistance)
                {
                    Ignite(null);
                }
            }

            // Animação procedural viva de oscilação da chama quando acesa
            if (isLit && flameObject != null)
            {
                float flickerNoise = Mathf.PerlinNoise(Time.time * 4.5f, transform.position.x);
                float sinPulse = Mathf.Sin(Time.time * 6.0f) * 0.08f;
                float currentScaleFactor = 0.92f + (flickerNoise * 0.20f) + sinPulse;

                flameObject.transform.localScale = new Vector3(
                    initialFlameScale.x * (2.0f - currentScaleFactor),
                    initialFlameScale.y * currentScaleFactor,
                    initialFlameScale.z * (2.0f - currentScaleFactor)
                );

                flameObject.transform.localRotation = Quaternion.Euler(
                    Mathf.Sin(Time.time * 3.5f) * 6.0f,
                    Time.time * 25.0f,
                    Mathf.Cos(Time.time * 4.0f) * 6.0f
                );

                if (lanternLight != null)
                {
                    lanternLight.intensity = targetLightIntensity * (0.88f + flickerNoise * 0.24f);
                }
            }
        }

        /// <summary>
        /// Acende a lanterna rúnica com transição suave, áudio e iluminação.
        /// </summary>
        public void Ignite(IXRInteractor interactor = null)
        {
            if (isLit) return;
            isLit = true;

            if (igniteSfx != null && audioSource != null)
            {
                audioSource.pitch = Random.Range(0.96f, 1.04f);
                audioSource.PlayOneShot(igniteSfx, 0.95f);
            }

            if (lightRoutine != null) StopCoroutine(lightRoutine);
            lightRoutine = StartCoroutine(IgnitionTransitionRoutine());

            if (flameSparksPs != null)
            {
                flameSparksPs.Play();
            }

            if (interactor is XRBaseControllerInteractor ctrl)
            {
                ctrl.SendHapticImpulse(0.5f, 0.12f);
            }
        }

        private IEnumerator IgnitionTransitionRoutine()
        {
            if (flameObject != null) flameObject.SetActive(true);
            if (lanternLight != null)
            {
                lanternLight.enabled = true;
                lanternLight.color = flameColor;
                lanternLight.range = lightRange;
            }

            float duration = 0.60f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

                if (lanternLight != null)
                {
                    // Overshoot suave de intensidade no momento da ignição (chama estufa e se assenta)
                    float flare = Mathf.Sin(t * Mathf.PI) * 0.5f;
                    lanternLight.intensity = (targetLightIntensity * t) + flare;
                }

                if (runesRenderer != null)
                {
                    Color runeCol = Color.Lerp(unlitRuneColor, litRuneColor, t);
                    ApplyRuneEmission(runeCol);
                }

                yield return null;
            }

            if (runesRenderer != null) ApplyRuneEmission(litRuneColor);
            lightRoutine = null;
        }

        private void SetLitInstant(bool lit)
        {
            isLit = lit;
            if (flameObject != null) flameObject.SetActive(lit);

            if (lanternLight != null)
            {
                lanternLight.enabled = lit;
                lanternLight.color = flameColor;
                lanternLight.range = lightRange;
                lanternLight.intensity = lit ? targetLightIntensity : 0f;
            }

            if (runesRenderer != null)
            {
                ApplyRuneEmission(lit ? litRuneColor : unlitRuneColor);
            }

            if (flameSparksPs != null)
            {
                if (lit) flameSparksPs.Play();
                else flameSparksPs.Stop();
            }
        }

        private void ApplyRuneEmission(Color color)
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();

            if (runesRenderer != null)
            {
                runesRenderer.GetPropertyBlock(mpb);
                mpb.SetColor(EmissionColorProp, color);
                runesRenderer.SetPropertyBlock(mpb);
            }
        }
    }
}
