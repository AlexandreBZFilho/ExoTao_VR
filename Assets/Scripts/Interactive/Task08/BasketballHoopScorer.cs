using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using VRRehab.Ergonomics;
using VRRehab.Core;

namespace VRRehab.Interactive.Task08
{
    /// <summary>
    /// Controlador principal do Minigame de Basquete Arcade.
    /// Gerencia:
    /// 1. Duplo sensor de passagem (TriggerTop + TriggerBottom) para pontuação de cima para baixo;
    /// 2. Detecção de cesta limpa (Swish! +3 pontos) vs com aro (+2 pontos);
    /// 3. Placar digital 3D luminoso via TextMeshPro com Pontos, Sequência (Streak) e Recorde (HighScore);
    /// 4. Efeitos procedurais: tremor elástico da rede, pulso de emissão neon no aro, partículas e buzina arcade;
    /// 5. Integração com IAimAssistTarget para arremessos assistidos por ThrowAimAssist.
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public class BasketballHoopScorer : MonoBehaviour, IAimAssistTarget
    {
        private const string HighScoreKey = "VRRehab_Basketball_HighScore";

        [Header("Sensores de Cesta")]
        [Tooltip("Trigger no plano superior da boca do aro.")]
        [SerializeField] private Collider topTriggerCollider;

        [Tooltip("Trigger no plano inferior da rede do aro.")]
        [SerializeField] private Collider bottomTriggerCollider;

        [Tooltip("Collider físico sólido do aro metálico onde ocorrem colisões.")]
        [SerializeField] private Collider rimSolidCollider;

        [Header("Pontuação & Configuração")]
        [SerializeField] private int pointsRegular = 2;
        [SerializeField] private int pointsSwish = 3;
        [SerializeField] private float maxTransitSeconds = 0.85f;

        [Header("Placar Digital 3D (TextMeshPro)")]
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text streakText;
        [SerializeField] private TMP_Text highScoreText;
        [SerializeField] private TMP_Text statusNoticeText;

        [Header("Efeitos Visuais e Feedback")]
        [SerializeField] private Transform netTransform;
        [SerializeField] private Renderer rimRenderer;
        [SerializeField] private ParticleSystem celebrationParticles;
        [SerializeField] private Light rimGlowLight;

        [Header("Efeitos Sonoros (SFX)")]
        [SerializeField] private AudioSource hoopAudioSource;
        [SerializeField] private AudioClip sfxSwish;
        [SerializeField] private AudioClip sfxRimClang;
        [SerializeField] private AudioClip sfxBuzzer;

        [Header("Configuração de Aim Assist")]
        [SerializeField] private Vector3 aimAssistOffset = new Vector3(0f, 0.05f, 0.34f);
        [SerializeField] private float aimAssistRadius = 0.32f;
        [SerializeField] private bool aimAssistEnabled = true;

        // Estado de Jogo
        private int currentScore = 0;
        private int currentStreak = 0;
        private int highScore = 0;
        private Coroutine netAnimationCoroutine;
        private Coroutine rimPulseCoroutine;
        private Coroutine noticeCoroutine;
        private Vector3 originalNetScale = Vector3.one;

        #region IAimAssistTarget Implementation
        public Vector3 AimTargetPosition => transform.TransformPoint(aimAssistOffset);
        public float AimTargetRadius => aimAssistRadius;
        public bool IsAimTargetActive => aimAssistEnabled && gameObject.activeInHierarchy;
        public Transform TargetTransform => transform;
        #endregion

        public int CurrentScore => currentScore;
        public int CurrentStreak => currentStreak;
        public int HighScore => highScore;

        private void Awake()
        {
            if (hoopAudioSource == null)
            {
                hoopAudioSource = GetComponent<AudioSource>();
                if (hoopAudioSource == null)
                {
                    hoopAudioSource = gameObject.AddComponent<AudioSource>();
                    hoopAudioSource.spatialBlend = 1.0f;
                    hoopAudioSource.minDistance = 1.0f;
                    hoopAudioSource.maxDistance = 20.0f;
                    hoopAudioSource.playOnAwake = false;
                }
            }

            if (netTransform != null)
            {
                originalNetScale = netTransform.localScale;
            }

            highScore = PlayerPrefs.GetInt(HighScoreKey, 0);
            UpdateScoreboardUI();
        }

        private void OnEnable()
        {
            AimAssistTargetRegistry.Register(this);
        }

        private void OnDisable()
        {
            AimAssistTargetRegistry.Unregister(this);
        }

        #region Triggers de Passagem
        /// <summary>
        /// Chamado pelo trigger superior quando a bola entra.
        /// </summary>
        public void OnTriggerTopEnter(Collider other)
        {
            BasketballBallAudio ball = other.GetComponentInParent<BasketballBallAudio>();
            if (ball != null)
            {
                ball.topTriggerEnterTimestamp = Time.time;
            }
        }

        /// <summary>
        /// Chamado pelo trigger inferior quando a bola sai da rede.
        /// </summary>
        public void OnTriggerBottomEnter(Collider other)
        {
            BasketballBallAudio ball = other.GetComponentInParent<BasketballBallAudio>();
            if (ball == null) return;

            // Valida se a bola veio do TriggerTop de cima para baixo dentro do tempo hábil
            float dt = Time.time - ball.topTriggerEnterTimestamp;
            if (ball.topTriggerEnterTimestamp > 0f && dt > 0.02f && dt <= maxTransitSeconds)
            {
                bool isSwish = !ball.hasTouchedRimThisShot;
                RegisterBasket(isSwish, ball);
            }

            // Reseta rastreamento da bola
            ball.ResetShotTracking();
        }
        #endregion

        #region Lógica de Pontuação e Celebração
        private void RegisterBasket(bool isSwish, BasketballBallAudio ball)
        {
            int pointsAwarded = isSwish ? pointsSwish : pointsRegular;
            currentScore += pointsAwarded;
            currentStreak++;

            if (currentScore > highScore)
            {
                highScore = currentScore;
                PlayerPrefs.SetInt(HighScoreKey, highScore);
                PlayerPrefs.Save();
            }

            UpdateScoreboardUI();

            // Áudio
            if (hoopAudioSource != null)
            {
                if (isSwish)
                {
                    if (sfxSwish != null) hoopAudioSource.PlayOneShot(sfxSwish, 1.0f);
                    if (sfxBuzzer != null) hoopAudioSource.PlayOneShot(sfxBuzzer, 0.75f);
                }
                else
                {
                    if (sfxRimClang != null) hoopAudioSource.PlayOneShot(sfxRimClang, 0.65f);
                    if (sfxSwish != null) hoopAudioSource.PlayOneShot(sfxSwish, 0.90f);
                    if (sfxBuzzer != null) hoopAudioSource.PlayOneShot(sfxBuzzer, 0.70f);
                }
            }

            // Animação procedural da rede
            if (netTransform != null)
            {
                if (netAnimationCoroutine != null) StopCoroutine(netAnimationCoroutine);
                netAnimationCoroutine = StartCoroutine(NetRippleAnimationRoutine());
            }

            // Pulso de emissão do aro
            Color pulseColor = isSwish ? new Color(0.0f, 1.0f, 0.65f) : new Color(1.0f, 0.55f, 0.05f);
            if (rimPulseCoroutine != null) StopCoroutine(rimPulseCoroutine);
            rimPulseCoroutine = StartCoroutine(RimPulseRoutine(pulseColor));

            // Partículas de confete
            if (celebrationParticles != null)
            {
                celebrationParticles.Play();
            }

            // Notificação temporária no visor do placar
            string notice = isSwish ? "★ SWISH! +3 ★" : "CESTA! +2";
            if (currentStreak > 1) notice += $" (STREAK x{currentStreak})";
            ShowStatusNotice(notice, isSwish ? Color.yellow : Color.cyan);

            Debug.Log($"[BASKETBALL] Cesta pontuada! {(isSwish ? "SWISH (+3)" : "Regular (+2)")} | Total: {currentScore} | Streak: {currentStreak}");
        }

        private void UpdateScoreboardUI()
        {
            if (scoreText != null)
            {
                scoreText.text = currentScore.ToString("D2");
            }
            if (streakText != null)
            {
                streakText.text = $"x{currentStreak}";
            }
            if (highScoreText != null)
            {
                highScoreText.text = highScore.ToString("D2");
            }
        }

        private void ShowStatusNotice(string msg, Color col)
        {
            if (statusNoticeText == null) return;
            if (noticeCoroutine != null) StopCoroutine(noticeCoroutine);
            noticeCoroutine = StartCoroutine(NoticeRoutine(msg, col));
        }

        private IEnumerator NoticeRoutine(string msg, Color col)
        {
            statusNoticeText.text = msg;
            statusNoticeText.color = col;
            statusNoticeText.gameObject.SetActive(true);

            yield return new WaitForSeconds(2.0f);

            statusNoticeText.text = "ARCADE HOOP";
            statusNoticeText.color = Color.white;
        }

        private IEnumerator NetRippleAnimationRoutine()
        {
            float duration = 0.65f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Estica para baixo no impacto e oscila senoidalmente
                float stretchY = 1.0f + 0.35f * Mathf.Exp(-5.0f * t) * Mathf.Cos(t * Mathf.PI * 8.0f);
                float waveXZ = 1.0f + 0.12f * Mathf.Exp(-4.0f * t) * Mathf.Sin(t * Mathf.PI * 10.0f);

                netTransform.localScale = new Vector3(
                    originalNetScale.x * waveXZ,
                    originalNetScale.y * stretchY,
                    originalNetScale.z * waveXZ
                );

                yield return null;
            }

            netTransform.localScale = originalNetScale;
        }

        private IEnumerator RimPulseRoutine(Color pulseColor)
        {
            if (rimRenderer == null) yield break;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            rimRenderer.GetPropertyBlock(block);

            if (rimGlowLight != null)
            {
                rimGlowLight.color = pulseColor;
                rimGlowLight.intensity = 3.0f;
            }

            float duration = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float intensity = Mathf.Lerp(4.0f, 1.0f, t);

                block.SetColor("_EmissionColor", pulseColor * intensity);
                rimRenderer.SetPropertyBlock(block);

                if (rimGlowLight != null)
                {
                    rimGlowLight.intensity = Mathf.Lerp(3.0f, 0.4f, t);
                }

                yield return null;
            }

            block.SetColor("_EmissionColor", new Color(1.0f, 0.35f, 0.02f) * 1.5f);
            rimRenderer.SetPropertyBlock(block);

            if (rimGlowLight != null)
            {
                rimGlowLight.color = new Color(1.0f, 0.4f, 0.1f);
                rimGlowLight.intensity = 0.6f;
            }
        }
        #endregion

        /// <summary>
        /// Reseta os pontos da rodada atual mantendo o recorde.
        /// </summary>
        public void ResetGame()
        {
            currentScore = 0;
            currentStreak = 0;
            UpdateScoreboardUI();
            ShowStatusNotice("READY - SHOOT!", Color.green);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0.8f, 0.5f);
            Gizmos.DrawWireSphere(AimTargetPosition, aimAssistRadius);
            Gizmos.color = new Color(0f, 1f, 0.8f, 1f);
            Gizmos.DrawSphere(AimTargetPosition, 0.04f);
        }
    }

    /// <summary>
    /// Componente auxiliar para encaminhar eventos de trigger para o HoopScorer principal.
    /// </summary>
    public class HoopSensorForwarder : MonoBehaviour
    {
        public enum SensorType { Top, Bottom }
        public SensorType sensorType;
        public BasketballHoopScorer scorer;

        private void OnTriggerEnter(Collider other)
        {
            if (scorer == null) return;
            if (sensorType == SensorType.Top)
            {
                scorer.OnTriggerTopEnter(other);
            }
            else
            {
                scorer.OnTriggerBottomEnter(other);
            }
        }
    }
}
