using System.Collections;
using UnityEngine;
using TMPro;
using VRRehab.Ergonomics;

namespace VRRehab.Interactive.Task08
{
    /// <summary>
    /// Controlador do Minigame de Basquete Arcade.
    /// 1. Duplo sensor de passagem: a bola precisa ENTRAR pelo sensor de cima DESCENDO e depois sair pelo sensor
    ///    de baixo, ainda descendo e dentro do aro. Bola subindo pela rede ou passando por fora não pontua.
    /// 2. Cesta limpa (sem tocar o aro) = SWISH +3; com toque no aro = +2.
    /// 3. Placar: PONTOS, SEQUÊNCIA e RECORDE (TextMeshPro) + faixa de mensagem temporária.
    /// 4. Efeitos: rede elástica, pulso de emissão no aro (MaterialPropertyBlock), partículas e sons suaves.
    /// 5. IAimAssistTarget para o ThrowAimAssist.
    /// Sem Update: tudo acontece nos eventos dos sensores.
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public class BasketballHoopScorer : MonoBehaviour, IAimAssistTarget
    {
        private const string HighScoreKey = "VRRehab_Basketball_HighScore";
        private const string IdleNotice = "ARREMESSE!";
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        /// <summary>Cesta ativa na cena (as bolas avisam aqui quando um arremesso erra).</summary>
        public static BasketballHoopScorer ActiveInstance { get; private set; }

        /// <summary>Relógio do minigame. Substituível em testes de editor (onde Time.time não avança).</summary>
        public static System.Func<float> ClockOverride;
        public static float Now => ClockOverride != null ? ClockOverride() : Time.time;

        [Header("Sensores de Cesta")]
        [Tooltip("Trigger logo acima da boca do aro.")]
        [SerializeField] private Collider topTriggerCollider;

        [Tooltip("Trigger na saída da rede, abaixo do aro.")]
        [SerializeField] private Collider bottomTriggerCollider;

        [Tooltip("Collider físico sólido do aro metálico onde ocorrem colisões.")]
        [SerializeField] private Collider rimSolidCollider;

        [Header("Geometria do Aro (espaço local da cesta)")]
        [Tooltip("Centro do aro no espaço local desta cesta.")]
        [SerializeField] private Vector3 rimCenterLocal = new Vector3(0f, -0.13f, -0.3425f);
        [Tooltip("Raio do aro (m).")]
        [SerializeField] private float rimRadius = 0.19f;

        [Header("Pontuação & Configuração")]
        [SerializeField] private int pointsRegular = 2;
        [SerializeField] private int pointsSwish = 3;
        [Tooltip("Tempo máximo (s) entre passar pelo sensor de cima e sair pelo de baixo.")]
        [SerializeField] private float maxTransitSeconds = 1.2f;

        [Header("Placar (TextMeshPro)")]
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text streakText;
        [SerializeField] private TMP_Text highScoreText;
        [SerializeField] private TMP_Text statusNoticeText;

        [Header("Efeitos Visuais e Feedback")]
        [SerializeField] private Transform netTransform;
        [SerializeField] private Renderer rimRenderer;
        [SerializeField] private ParticleSystem celebrationParticles;
        [SerializeField] private Light rimGlowLight;

        [Header("Efeitos Sonoros (volumes confortáveis)")]
        [SerializeField] private AudioSource hoopAudioSource;
        [SerializeField] private AudioClip sfxSwish;
        [SerializeField] private AudioClip sfxRimClang;
        [SerializeField] private AudioClip sfxBuzzer;
        [Range(0.05f, 0.6f)] [SerializeField] private float sfxVolume = 0.4f;

        [Header("Configuração de Aim Assist")]
        [SerializeField] private Vector3 aimAssistOffset = new Vector3(0f, 0.05f, 0.34f);
        [SerializeField] private float aimAssistRadius = 0.32f;
        [SerializeField] private bool aimAssistEnabled = true;

        // Estado de Jogo
        private int currentScore = 0;
        private int currentStreak = 0;
        private int highScore = 0;
        private bool initialized;
        private Coroutine netAnimationCoroutine;
        private Coroutine rimPulseCoroutine;
        private Coroutine noticeCoroutine;
        private Vector3 originalNetScale = Vector3.one;
        private MaterialPropertyBlock rimBlock;

        #region IAimAssistTarget Implementation
        public Vector3 AimTargetPosition => transform.TransformPoint(aimAssistOffset);
        public float AimTargetRadius => aimAssistRadius;
        public bool IsAimTargetActive => aimAssistEnabled && gameObject.activeInHierarchy;
        public Transform TargetTransform => transform;
        #endregion

        public int CurrentScore => currentScore;
        public int CurrentStreak => currentStreak;
        public int HighScore => highScore;
        public Vector3 RimCenterWorld => transform.TransformPoint(rimCenterLocal);
        public float RimRadius => rimRadius;
        public string NoticeText => statusNoticeText != null ? statusNoticeText.text : string.Empty;

        /// <summary>Corrotinas de efeito só rodam em jogo (no editor o estado é aplicado direto).</summary>
        private bool CanAnimate => isActiveAndEnabled && Application.isPlaying;

        private void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>Inicialização idempotente (Awake em jogo; chamada direta nos testes de editor).</summary>
        public void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;

            if (hoopAudioSource == null)
            {
                hoopAudioSource = GetComponent<AudioSource>();
                if (hoopAudioSource == null) hoopAudioSource = gameObject.AddComponent<AudioSource>();
            }
            hoopAudioSource.playOnAwake = false;
            hoopAudioSource.loop = false;
            hoopAudioSource.spatialBlend = 1.0f;
            hoopAudioSource.dopplerLevel = 0f;
            hoopAudioSource.minDistance = 1.0f;
            hoopAudioSource.maxDistance = 12.0f;

            ActiveInstance = this;
            if (netTransform != null) originalNetScale = netTransform.localScale;
            if (rimGlowLight != null) rimGlowLight.shadows = LightShadows.None;
            rimBlock = new MaterialPropertyBlock();

            highScore = PlayerPrefs.GetInt(HighScoreKey, 0);
            if (statusNoticeText != null) statusNoticeText.text = IdleNotice;
            UpdateScoreboardUI();
        }

        private void OnEnable()
        {
            ActiveInstance = this;
            AimAssistTargetRegistry.Register(this);
        }

        private void OnDisable()
        {
            if (ActiveInstance == this) ActiveInstance = null;
            AimAssistTargetRegistry.Unregister(this);
        }

        #region Sensores de Passagem
        /// <summary>Distância horizontal (no plano do aro) do centro da bola ao eixo do aro.</summary>
        private float HorizontalDistanceToRimAxis(Vector3 worldPos)
        {
            Vector3 local = transform.InverseTransformPoint(worldPos) - rimCenterLocal;
            local.y = 0f;
            return Vector3.Scale(local, transform.lossyScale).magnitude;
        }

        /// <summary>Sensor de cima: a bola precisa estar DESCENDO e por cima da boca do aro.</summary>
        public void OnTriggerTopEnter(Collider other)
        {
            BasketballBallAudio ball = other.GetComponentInParent<BasketballBallAudio>();
            if (ball == null) return;

            bool descending = ball.Velocity.y < 0.05f;
            bool overMouth = HorizontalDistanceToRimAxis(ball.transform.position) <= rimRadius + 0.06f;
            ball.topTriggerEnterTimestamp = descending && overMouth ? Now : -1f;
        }

        /// <summary>Sensor de baixo: valida a passagem de cima para baixo por dentro do aro e pontua.</summary>
        public void OnTriggerBottomEnter(Collider other)
        {
            BasketballBallAudio ball = other.GetComponentInParent<BasketballBallAudio>();
            if (ball == null) return;
            if (ball.HasScoredThisShot) { ball.topTriggerEnterTimestamp = -1f; return; } // uma cesta por arremesso

            float topTime = ball.topTriggerEnterTimestamp;
            ball.topTriggerEnterTimestamp = -1f;
            if (topTime < 0f) return; // não passou pela boca do aro descendo (ex.: bola subindo pela rede)

            bool inTime = Now - topTime <= maxTransitSeconds;
            bool descending = ball.Velocity.y < 0f;
            // Arremesso em arco sai pela rede deslocado para trás (não há colisor de rede): tolera até o raio do aro
            bool insideNet = HorizontalDistanceToRimAxis(ball.transform.position) <= rimRadius + 0.02f;
            if (!inTime || !descending || !insideNet) return;

            bool isSwish = !ball.TouchedRimRecently;
            ball.NotifyScored();

            // A rede "segura" a bola: corta a velocidade horizontal para ela cair embaixo da cesta, não atravessar a sala
            Rigidbody body = ball.AttachedRigidbody;
            if (body != null && !body.isKinematic)
            {
                Vector3 v = body.linearVelocity;
                body.linearVelocity = new Vector3(v.x * 0.25f, v.y, v.z * 0.25f);
            }
            RegisterBasket(isSwish);
        }
        #endregion

        #region Pontuação e Celebração
        private void RegisterBasket(bool isSwish)
        {
            EnsureInitialized();

            currentScore += isSwish ? pointsSwish : pointsRegular;
            currentStreak++;

            if (currentScore > highScore)
            {
                highScore = currentScore;
                PlayerPrefs.SetInt(HighScoreKey, highScore);
                PlayerPrefs.Save();
            }

            UpdateScoreboardUI();

            if (hoopAudioSource != null)
            {
                if (!isSwish && sfxRimClang != null) hoopAudioSource.PlayOneShot(sfxRimClang, sfxVolume * 0.6f);
                if (sfxSwish != null) hoopAudioSource.PlayOneShot(sfxSwish, sfxVolume);
                if (sfxBuzzer != null) hoopAudioSource.PlayOneShot(sfxBuzzer, sfxVolume * 0.6f);
            }

            if (netTransform != null && CanAnimate)
            {
                if (netAnimationCoroutine != null) StopCoroutine(netAnimationCoroutine);
                netAnimationCoroutine = StartCoroutine(NetRippleAnimationRoutine());
            }

            if (CanAnimate)
            {
                Color pulseColor = isSwish ? new Color(0.0f, 1.0f, 0.65f) : new Color(1.0f, 0.55f, 0.05f);
                if (rimPulseCoroutine != null) StopCoroutine(rimPulseCoroutine);
                rimPulseCoroutine = StartCoroutine(RimPulseRoutine(pulseColor));
            }

            if (celebrationParticles != null) celebrationParticles.Play();

            string notice = isSwish ? $"SWISH! +{pointsSwish}" : $"CESTA! +{pointsRegular}";
            if (currentStreak > 1) notice += $"  x{currentStreak}";
            ShowStatusNotice(notice, isSwish ? new Color(1f, 0.92f, 0.2f) : new Color(0.2f, 0.95f, 1f));
        }

        /// <summary>Arremesso errado (avisado pela bola no primeiro impacto fora da cesta): zera a sequência.</summary>
        public void RegisterMiss()
        {
            if (currentStreak == 0) return;
            currentStreak = 0;
            UpdateScoreboardUI();
        }

        private void UpdateScoreboardUI()
        {
            if (scoreText != null) scoreText.text = currentScore.ToString("D2");
            if (streakText != null) streakText.text = "x" + currentStreak;
            if (highScoreText != null) highScoreText.text = highScore.ToString("D2");
        }

        private void ShowStatusNotice(string msg, Color col)
        {
            if (statusNoticeText == null) return;
            statusNoticeText.text = msg;
            statusNoticeText.color = col;
            if (!CanAnimate) return;
            if (noticeCoroutine != null) StopCoroutine(noticeCoroutine);
            noticeCoroutine = StartCoroutine(NoticeRoutine());
        }

        private IEnumerator NoticeRoutine()
        {
            yield return new WaitForSeconds(2.2f);
            statusNoticeText.text = IdleNotice;
            statusNoticeText.color = Color.white;
            noticeCoroutine = null;
        }

        private IEnumerator NetRippleAnimationRoutine()
        {
            const float duration = 0.65f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Estica para baixo no impacto e oscila
                float stretchY = 1.0f + 0.35f * Mathf.Exp(-5.0f * t) * Mathf.Cos(t * Mathf.PI * 8.0f);
                float waveXZ = 1.0f + 0.12f * Mathf.Exp(-4.0f * t) * Mathf.Sin(t * Mathf.PI * 10.0f);
                netTransform.localScale = new Vector3(originalNetScale.x * waveXZ, originalNetScale.y * stretchY, originalNetScale.z * waveXZ);
                yield return null;
            }

            netTransform.localScale = originalNetScale;
            netAnimationCoroutine = null;
        }

        private IEnumerator RimPulseRoutine(Color pulseColor)
        {
            if (rimRenderer == null) yield break;
            if (rimBlock == null) rimBlock = new MaterialPropertyBlock();
            rimRenderer.GetPropertyBlock(rimBlock);

            const float duration = 0.5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                rimBlock.SetColor(EmissionColorId, pulseColor * Mathf.Lerp(4.0f, 1.0f, t));
                rimRenderer.SetPropertyBlock(rimBlock);

                if (rimGlowLight != null)
                {
                    rimGlowLight.color = pulseColor;
                    rimGlowLight.intensity = Mathf.Lerp(3.0f, 0.4f, t);
                }
                yield return null;
            }

            rimBlock.SetColor(EmissionColorId, new Color(1.0f, 0.35f, 0.02f) * 1.5f);
            rimRenderer.SetPropertyBlock(rimBlock);

            if (rimGlowLight != null)
            {
                rimGlowLight.color = new Color(1.0f, 0.4f, 0.1f);
                rimGlowLight.intensity = 0.6f;
            }
            rimPulseCoroutine = null;
        }
        #endregion

        /// <summary>Zera os pontos e a sequência da rodada, mantendo o recorde.</summary>
        public void ResetGame()
        {
            EnsureInitialized();
            currentScore = 0;
            currentStreak = 0;
            UpdateScoreboardUI();
            ShowStatusNotice("VALENDO!", new Color(0.3f, 1f, 0.45f));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0.8f, 0.5f);
            Gizmos.DrawWireSphere(AimTargetPosition, aimAssistRadius);
            Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
            Gizmos.DrawWireSphere(RimCenterWorld, rimRadius);
        }
    }
}
