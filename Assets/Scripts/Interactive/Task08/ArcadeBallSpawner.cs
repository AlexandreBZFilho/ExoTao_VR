using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;
using VRRehab.Ergonomics;

namespace VRRehab.Interactive.Task08
{
    /// <summary>
    /// Botão Spawner Arcade de Mesa para Invocação de Bolas de Basquete.
    /// Acoplado à mesa no Menu Principal.
    /// Responde a:
    /// 1. Interação por Raio Laser VR (Select/Click);
    /// 2. Toque Físico Direto da Mão VR (Trigger Collider);
    /// 3. Efeitos: Animação mecânica do domo afundando, SFX microswitch arcade, vibração háptica
    ///    e geração de MiniBasketball com limite de instâncias (Object Pooling ergonômico).
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public class ArcadeBallSpawner : XRSimpleInteractable
    {
        [Header("Spawn Configuration")]
        [Tooltip("Prefab da MiniBasketball que será instanciada.")]
        [SerializeField] private GameObject miniBasketballPrefab;

        [Tooltip("Ponto de desova da bola (sobre a mesa em frente ao paciente).")]
        [SerializeField] private Transform ballSpawnPoint;

        [Tooltip("Número máximo de bolas simultâneas na cena.")]
        [SerializeField] private int maxSimultaneousBalls = 4;

        [Header("Animação Mecânica do Botão")]
        [Tooltip("Transform do domo que afunda ao ser pressionado.")]
        [SerializeField] private Transform buttonDomeTransform;

        [Tooltip("Deslocamento local ao ser pressionado.")]
        [SerializeField] private Vector3 pressedLocalOffset = new Vector3(0f, 0f, -0.012f);

        [Tooltip("Duração da animação de retorno (segundos).")]
        [SerializeField] private float returnDuration = 0.18f;

        [Header("Áudio e Feedback")]
        [SerializeField] private AudioSource buttonAudioSource;
        [SerializeField] private AudioClip sfxButtonPress;
        [SerializeField] private Light buttonGlowLight;
        [SerializeField] private Renderer buttonDomeRenderer;

        // Estado Interno
        private Vector3 initialDomeLocalPos;
        private Coroutine buttonPressCoroutine;
        private List<GameObject> activeBalls = new List<GameObject>();
        private bool isButtonLocked = false;

        protected override void Awake()
        {
            base.Awake();

            if (buttonDomeTransform != null)
            {
                initialDomeLocalPos = buttonDomeTransform.localPosition;
            }

            if (buttonAudioSource == null)
            {
                buttonAudioSource = GetComponent<AudioSource>();
                if (buttonAudioSource == null)
                {
                    buttonAudioSource = gameObject.AddComponent<AudioSource>();
                    buttonAudioSource.spatialBlend = 1.0f;
                    buttonAudioSource.minDistance = 0.5f;
                    buttonAudioSource.maxDistance = 10.0f;
                    buttonAudioSource.playOnAwake = false;
                }
            }
        }

        #region XRI Overrides
        protected override void OnHoverEntered(HoverEnterEventArgs args)
        {
            base.OnHoverEntered(args);

            if (buttonGlowLight != null)
            {
                buttonGlowLight.intensity = 2.0f;
            }

            if (VRFeedbackManager.Instance != null && args.interactorObject is IXRInteractor interactor)
            {
                VRFeedbackManager.Instance.PlayHover(transform.position, interactor);
            }
        }

        protected override void OnHoverExited(HoverExitEventArgs args)
        {
            base.OnHoverExited(args);

            if (buttonGlowLight != null)
            {
                buttonGlowLight.intensity = 0.8f;
            }
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);

            PressButton(args.interactorObject as IXRInteractor);
        }
        #endregion

        #region Físico (Toque com a mão)
        private void OnTriggerEnter(Collider other)
        {
            // Detecta colisão física com mão ou dedo do jogador
            if (other.name.ToLower().Contains("hand") || other.name.ToLower().Contains("controller") || other.CompareTag("Player"))
            {
                PressButton(null);
            }
        }
        #endregion

        /// <summary>
        /// Aciona a compressão física do botão e o nascimento da bola de basquete.
        /// </summary>
        public void PressButton(IXRInteractor interactor = null)
        {
            if (isButtonLocked) return;

            // Áudio
            if (buttonAudioSource != null && sfxButtonPress != null)
            {
                buttonAudioSource.pitch = UnityEngine.Random.Range(0.97f, 1.03f);
                buttonAudioSource.PlayOneShot(sfxButtonPress, 1.0f);
            }

            // Háptico
            if (VRFeedbackManager.Instance != null && interactor != null)
            {
                VRFeedbackManager.Instance.TriggerHaptic(interactor, 0.65f, 0.12f);
            }

            // Animação mecânica do domo
            if (buttonDomeTransform != null)
            {
                if (buttonPressCoroutine != null) StopCoroutine(buttonPressCoroutine);
                buttonPressCoroutine = StartCoroutine(AnimateButtonPressRoutine());
            }

            // Spawn da Bola
            SpawnBasketball();
        }

        private IEnumerator AnimateButtonPressRoutine()
        {
            isButtonLocked = true;

            // Afunda instantaneamente
            buttonDomeTransform.localPosition = initialDomeLocalPos + pressedLocalOffset;

            if (buttonGlowLight != null)
            {
                buttonGlowLight.intensity = 3.5f;
            }

            yield return new WaitForSeconds(0.06f);

            // Retorna suavemente
            float elapsed = 0f;
            Vector3 startPos = buttonDomeTransform.localPosition;

            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / returnDuration;
                buttonDomeTransform.localPosition = Vector3.Lerp(startPos, initialDomeLocalPos, t);
                yield return null;
            }

            buttonDomeTransform.localPosition = initialDomeLocalPos;

            if (buttonGlowLight != null)
            {
                buttonGlowLight.intensity = isHovered ? 2.0f : 0.8f;
            }

            isButtonLocked = false;
        }

        private void SpawnBasketball()
        {
            if (miniBasketballPrefab == null)
            {
                Debug.LogWarning("[ArcadeBallSpawner] Prefab da MiniBasketball não atribuído!");
                return;
            }

            // Limpa referências nulas da lista
            activeBalls.RemoveAll(b => b == null);

            // Pool Management: Se atingiu o limite, descarta a bola mais antiga não segurada
            if (activeBalls.Count >= maxSimultaneousBalls)
            {
                GameObject oldestFreeBall = null;
                for (int i = 0; i < activeBalls.Count; i++)
                {
                    GameObject candidate = activeBalls[i];
                    if (candidate != null)
                    {
                        var grab = candidate.GetComponent<XRGrabInteractable>();
                        if (grab == null || !grab.isSelected)
                        {
                            oldestFreeBall = candidate;
                            break;
                        }
                    }
                }

                if (oldestFreeBall != null)
                {
                    activeBalls.Remove(oldestFreeBall);
                    Destroy(oldestFreeBall);
                }
            }

            // Posição de spawn
            Vector3 spawnPos = ballSpawnPoint != null ? ballSpawnPoint.position : transform.position + Vector3.up * 0.15f;
            Quaternion spawnRot = Quaternion.Euler(UnityEngine.Random.Range(0, 360), UnityEngine.Random.Range(0, 360), 0);

            GameObject newBall = Instantiate(miniBasketballPrefab, spawnPos, spawnRot);
            newBall.name = $"MiniBasketball_{Time.frameCount}";

            // Configura Auto-Respawn para voltar à mesa se cair
            var ergoGrab = newBall.GetComponent<ErgonomicGrabInteractable>();
            if (ergoGrab != null)
            {
                ergoGrab.SetHomeTransform(spawnPos, spawnRot);
            }

            // Adiciona pequeno pop/impulso suave para cima
            Rigidbody rb = newBall.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.up * 0.45f;
                rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 2.0f;
            }

            activeBalls.Add(newBall);

            Debug.Log($"[ArcadeBallSpawner] Bola invocada em {spawnPos} (Ativas: {activeBalls.Count}/{maxSimultaneousBalls})");
        }
    }
}
