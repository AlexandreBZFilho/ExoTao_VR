using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace VRRehab.Interactive.Task08
{
    /// <summary>
    /// Componente da MiniBasketball: som de quique e rastreamento do arremesso para o placar.
    /// - Marca se a bola tocou o aro (para diferenciar SWISH +3 de cesta com aro +2).
    /// - Um arremesso começa quando o jogador solta a bola e termina na cesta ou no primeiro impacto
    ///   fora da cesta (chão, mesa, parede) -> erro: zera a sequência e LIMPA a flag do aro para o próximo.
    /// - Pegar a bola de novo também limpa todo o rastreamento.
    /// Tudo é dirigido por eventos (colisão / select): não há Update.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class BasketballBallAudio : MonoBehaviour
    {
        /// <summary>Um toque no aro mais antigo que isso (s) não conta mais para a cesta seguinte.</summary>
        private const float RimTouchMemorySeconds = 4.0f;

        [Header("Áudio de Quique")]
        [SerializeField] private AudioClip bounceClip;
        [SerializeField] private float minBounceVelocity = 0.45f;
        [SerializeField] private float maxBounceVelocity = 4.5f;
        [Range(0.05f, 0.6f)] [SerializeField] private float maxBounceVolume = 0.45f;

        [Header("Rastreamento de Cesta (somente leitura)")]
        public bool hasTouchedRimThisShot = false;
        public float topTriggerEnterTimestamp = -1f;
        [SerializeField] private bool shotInFlight = false;
        [SerializeField] private bool scoredThisShot = false;

        private float rimTouchTimestamp = -1f;
        private AudioSource audioSource;
        private Rigidbody rb;
        private XRBaseInteractable interactable;
        private float lastBounceTime = -1f;

        public Rigidbody AttachedRigidbody => rb != null ? rb : (rb = GetComponent<Rigidbody>());
        public bool ShotInFlight => shotInFlight;

        /// <summary>
        /// Este arremesso já virou cesta. Uma bola só pontua UMA vez por arremesso: sem isso, uma bola rápida que
        /// quica no chão, volta a subir pelo aro e cai de novo contava 2 ou 3 cestas.
        /// </summary>
        public bool HasScoredThisShot => scoredThisShot;

        /// <summary>Tocou o aro neste arremesso (toques antigos expiram).</summary>
        public bool TouchedRimRecently =>
            hasTouchedRimThisShot && BasketballHoopScorer.Now - rimTouchTimestamp <= RimTouchMemorySeconds;

        public Vector3 Velocity
        {
            get
            {
                Rigidbody body = AttachedRigidbody;
                return body != null ? body.linearVelocity : Vector3.zero;
            }
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            interactable = GetComponent<XRBaseInteractable>();
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1.0f;
                audioSource.minDistance = 0.5f;
                audioSource.maxDistance = 15.0f;
                audioSource.playOnAwake = false;
            }
        }

        private void OnEnable()
        {
            ResetShotTracking();
            scoredThisShot = false;
            if (interactable != null)
            {
                interactable.selectEntered.AddListener(OnGrabbed);
                interactable.selectExited.AddListener(OnReleased);
            }
        }

        private void OnDisable()
        {
            if (interactable != null)
            {
                interactable.selectEntered.RemoveListener(OnGrabbed);
                interactable.selectExited.RemoveListener(OnReleased);
            }
        }

        public void SetBounceClip(AudioClip clip)
        {
            bounceClip = clip;
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            // Pega de volta antes de bater em qualquer coisa (ou depois de sumir do mapa e reaparecer): conta como erro
            if (shotInFlight) NotifyMiss();
            ResetShotTracking();
            scoredThisShot = false;
        }

        private void OnReleased(SelectExitEventArgs args) => BeginShot();

        /// <summary>Começa um arremesso (chamado ao soltar a bola).</summary>
        public void BeginShot()
        {
            ResetShotTracking();
            scoredThisShot = false;
            shotInFlight = true;
        }

        /// <summary>Limpa todo o rastreamento do arremesso (toque no aro, passagem pelo sensor de cima).</summary>
        public void ResetShotTracking()
        {
            hasTouchedRimThisShot = false;
            rimTouchTimestamp = -1f;
            topTriggerEnterTimestamp = -1f;
            shotInFlight = false;
        }

        /// <summary>Chamado pelo placar quando este arremesso virou cesta.</summary>
        public void NotifyScored()
        {
            ResetShotTracking();
            scoredThisShot = true;
        }

        private static void NotifyMiss()
        {
            BasketballHoopScorer active = BasketballHoopScorer.ActiveInstance;
            if (active != null) active.RegisterMiss();
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleImpact(collision.collider, collision.relativeVelocity.magnitude);
        }

        /// <summary>Trata um impacto da bola (separado do OnCollisionEnter para poder ser testado).</summary>
        public void HandleImpact(Collider other, float impactSpeed)
        {
            if (other != null)
            {
                BasketballHoopScorer hoop = other.GetComponentInParent<BasketballHoopScorer>();
                if (hoop != null)
                {
                    // Parte da cesta: só o aro tira o SWISH (a tabela não)
                    if (other.name.Contains("Rim"))
                    {
                        hasTouchedRimThisShot = true;
                        rimTouchTimestamp = BasketballHoopScorer.Now;
                    }
                }
                else if (shotInFlight)
                {
                    // Primeiro impacto fora da cesta depois de soltar = arremesso errado
                    ResetShotTracking();
                    NotifyMiss();
                }
            }

            PlayBounce(impactSpeed);
        }

        private void PlayBounce(float impactSpeed)
        {
            if (bounceClip == null || audioSource == null) return;
            if (impactSpeed < minBounceVelocity) return;

            float now = BasketballHoopScorer.Now;
            if (now - lastBounceTime < 0.08f) return; // evita metralhadora de áudio
            lastBounceTime = now;

            float t = Mathf.Clamp01((impactSpeed - minBounceVelocity) / (maxBounceVelocity - minBounceVelocity));
            audioSource.pitch = Random.Range(0.92f, 1.08f);
            audioSource.PlayOneShot(bounceClip, Mathf.Lerp(0.12f, maxBounceVolume, t));
        }
    }
}
