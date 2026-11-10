using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;

namespace VRRehab.Interactive.Task09
{
    /// <summary>
    /// Botão Arcade Fidget de Lançamento de Fogos de Artifício.
    /// Ao ser pressionado (laser ou toque da mão VR), o domo afunda elasticamente,
    /// emite feedback mecânico/háptico e dispara um foguete com cauda e explosão festiva no céu da sala.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class FireworksButtonInteractable : MonoBehaviour
    {
        [Header("Hierarquia do Botão")]
        [SerializeField] private Transform buttonDomeTransform;
        [SerializeField] private Vector3 pressedLocalOffset = new Vector3(0f, 0f, -0.012f);
        [SerializeField] private Light buttonGlowLight;

        [Header("Sistemas de Efeitos Pirotécnicos")]
        [SerializeField] private ParticleSystem fireworksAscentParticles;
        [SerializeField] private ParticleSystem fireworksBurstParticles;
        [SerializeField] private Light fireworksBurstLight;
        [SerializeField] private Transform burstLocation;
        [SerializeField] private float rocketTravelTime = 0.60f;

        [Header("Efeitos Sonoros")]
        [SerializeField] private AudioClip sfxButtonPress;
        [SerializeField] private AudioClip sfxLaunchWhistle;
        [SerializeField] private AudioClip sfxBurstExplosion;
        [SerializeField] private AudioSource audioSource;

        private Vector3 domeOriginalLocalPos;
        private Coroutine activePressCoroutine;
        private Coroutine activeFireworksCoroutine;
        private XRSimpleInteractable xrInteractable;
        private bool isPressing = false;

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (buttonDomeTransform != null)
            {
                domeOriginalLocalPos = buttonDomeTransform.localPosition;
            }

            SetupXRInteraction();
        }

        private void SetupXRInteraction()
        {
            xrInteractable = GetComponent<XRSimpleInteractable>();
            if (xrInteractable == null)
            {
                xrInteractable = gameObject.AddComponent<XRSimpleInteractable>();
            }

            xrInteractable.selectEntered.RemoveListener(OnSelectEntered);
            xrInteractable.selectEntered.AddListener(OnSelectEntered);
        }

        private void OnDestroy()
        {
            if (xrInteractable != null)
            {
                xrInteractable.selectEntered.RemoveListener(OnSelectEntered);
            }
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            TriggerFireworks();
            TriggerHaptic(args.interactorObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            // Ativa se mão VR ou objeto físico colidir com o domo
            if (other.CompareTag("Player") || other.name.ToLower().Contains("hand") || other.name.ToLower().Contains("controller"))
            {
                TriggerFireworks();
            }
        }

        private void TriggerHaptic(IXRInteractor interactor)
        {
            if (interactor != null)
            {
                VRFeedbackManager.Instance?.TriggerHaptic(interactor, 0.70f, 0.15f);
            }
        }

        /// <summary>
        /// Dispara a rotina de compressão do botão e lançamento pirotécnico.
        /// </summary>
        [ContextMenu("Trigger Fireworks")]
        public void TriggerFireworks()
        {
            if (isPressing) return;

            // Animação mecânica do botão
            if (activePressCoroutine != null) StopCoroutine(activePressCoroutine);
            activePressCoroutine = StartCoroutine(AnimateButtonPressRoutine());

            // Sequência de fogos
            if (activeFireworksCoroutine != null) StopCoroutine(activeFireworksCoroutine);
            activeFireworksCoroutine = StartCoroutine(LaunchFireworksSequenceRoutine());
        }

        private IEnumerator AnimateButtonPressRoutine()
        {
            isPressing = true;

            // Som de clique mecânico do botão
            if (audioSource != null && sfxButtonPress != null)
            {
                audioSource.pitch = UnityEngine.Random.Range(0.96f, 1.04f);
                audioSource.PlayOneShot(sfxButtonPress, 0.90f);
            }

            // Realce luminoso do botão
            if (buttonGlowLight != null)
            {
                buttonGlowLight.intensity = 2.2f;
            }

            // Afunda o domo
            if (buttonDomeTransform != null)
            {
                float downTime = 0.05f;
                float elapsed = 0f;
                Vector3 startPos = buttonDomeTransform.localPosition;
                Vector3 targetPos = domeOriginalLocalPos + pressedLocalOffset;

                while (elapsed < downTime)
                {
                    elapsed += Time.deltaTime;
                    buttonDomeTransform.localPosition = Vector3.Lerp(startPos, targetPos, elapsed / downTime);
                    yield return null;
                }
                buttonDomeTransform.localPosition = targetPos;

                yield return new WaitForSeconds(0.06f);

                // Retorno elástico amortecido
                float upTime = 0.16f;
                elapsed = 0f;
                while (elapsed < upTime)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / upTime;
                    // Overshoot elástico suave
                    float bounce = Mathf.Sin(t * Mathf.PI * 1.5f);
                    buttonDomeTransform.localPosition = Vector3.Lerp(targetPos, domeOriginalLocalPos, bounce);
                    yield return null;
                }
                buttonDomeTransform.localPosition = domeOriginalLocalPos;
            }

            // Fade out do brilho do botão
            if (buttonGlowLight != null)
            {
                float fadeTime = 0.25f;
                float elapsed = 0f;
                float initialIntensity = buttonGlowLight.intensity;
                while (elapsed < fadeTime)
                {
                    elapsed += Time.deltaTime;
                    buttonGlowLight.intensity = Mathf.Lerp(initialIntensity, 1.0f, elapsed / fadeTime);
                    yield return null;
                }
                buttonGlowLight.intensity = 1.0f;
            }

            isPressing = false;
        }

        private IEnumerator LaunchFireworksSequenceRoutine()
        {
            // 1. Assobio de foguete ascendente
            if (audioSource != null && sfxLaunchWhistle != null)
            {
                audioSource.pitch = UnityEngine.Random.Range(0.97f, 1.05f);
                audioSource.PlayOneShot(sfxLaunchWhistle, 0.85f);
            }

            // 2. Dispara rastro ascendente
            if (fireworksAscentParticles != null)
            {
                fireworksAscentParticles.Play();
            }

            // Aguarda o foguete atingir o ápice no teto
            yield return new WaitForSeconds(rocketTravelTime);

            // 3. Estouro da detonação
            if (audioSource != null && sfxBurstExplosion != null)
            {
                audioSource.pitch = UnityEngine.Random.Range(0.92f, 1.06f);
                audioSource.PlayOneShot(sfxBurstExplosion, 1.0f);
            }

            // Dispara explosão esférica multicolorida
            if (fireworksBurstParticles != null)
            {
                if (burstLocation != null)
                {
                    fireworksBurstParticles.transform.position = burstLocation.position;
                }
                fireworksBurstParticles.Play();
            }

            // Flash luminoso do estouro
            if (fireworksBurstLight != null)
            {
                if (burstLocation != null)
                {
                    fireworksBurstLight.transform.position = burstLocation.position;
                }
                StartCoroutine(FlashBurstLightRoutine());
            }
        }

        private IEnumerator FlashBurstLightRoutine()
        {
            if (fireworksBurstLight == null) yield break;

            fireworksBurstLight.enabled = true;
            fireworksBurstLight.intensity = 4.5f;

            float flashDuration = 0.35f;
            float elapsed = 0f;
            while (elapsed < flashDuration)
            {
                elapsed += Time.deltaTime;
                fireworksBurstLight.intensity = Mathf.Lerp(4.5f, 0f, elapsed / flashDuration);
                yield return null;
            }

            fireworksBurstLight.intensity = 0f;
            fireworksBurstLight.enabled = false;
        }
    }
}
