using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;

namespace VRRehab.Interactive.Task14
{
    /// <summary>
    /// Moeda/Orbe Flutuante Coletável com Atração Magnética para o Exoesqueleto.
    /// Flutua suavemente a 1.15m do chão, detecta a aproximação do paciente,
    /// acelera suavemente em direção ao tronco com feedback de áudio, combo progressivo
    /// e suporte à mira com o raio laser VR.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class CollectibleOrbCoin : MonoBehaviour
    {
        public enum CoinType
        {
            EnergyStar,   // Moeda octogonal ao longo da trilha principal (10 pts)
            BonusDiamond  // Diamante com anéis orbitais em locais secretos (50 pts, alcance com laser)
        }

        [Header("Tipo e Pontuação")]
        [SerializeField] private CoinType coinType = CoinType.EnergyStar;
        [SerializeField] private int basePointValue = 10;

        [Header("Dinâmica de Flutuação e Rotação")]
        [SerializeField] private float rotationSpeed = 90.0f;
        [SerializeField] private float bobAmplitude = 0.06f;
        [SerializeField] private float bobFrequency = 2.2f;
        [SerializeField] private Transform orbitRingTransform;
        [SerializeField] private float orbitRingRotationSpeed = 140.0f;

        [Header("Parâmetros Magnéticos (Exoesqueleto)")]
        [SerializeField] private float magneticAttractionRadius = 2.2f;
        [SerializeField] private float collectionDistanceThreshold = 0.35f;
        [SerializeField] private float minAttractionSpeed = 2.0f;
        [SerializeField] private float maxAttractionSpeed = 9.5f;
        [SerializeField] private float chestHeightOffset = 1.15f;

        [Header("Áudio Procedural")]
        [SerializeField] private AudioClip pickupChimeClip;
        [SerializeField] private AudioClip magnetWhooshClip;
        [SerializeField] private AudioClip secretFanfareClip;
        [SerializeField] private AudioSource localAudioSource;

        [Header("Efeitos Visuais")]
        [SerializeField] private ParticleSystem collectSparklesPs;
        [SerializeField] private Renderer mainRenderer;
        [SerializeField] private Color floatingTextColor = new Color(1.0f, 0.88f, 0.20f);

        // Sistema Estático de Combo Streak Compartilhado
        private static int s_currentComboStreak = 0;
        private static float s_lastCollectTime = -10.0f;
        private const float ComboTimeoutWindow = 2.5f;
        private const int MaxComboMultiplier = 8;

        // Estado Interno de Movimento e Atração
        private Vector3 initialBasePosition;
        private float randomTimeOffset;
        private bool isAttracting = false;
        private bool isCollected = false;
        private float attractionDuration = 0f;
        private Transform playerTarget;
        private Collider coinCollider;
        private XRSimpleInteractable xrInteractable;

        public CoinType Type => coinType;
        public bool IsCollected => isCollected;

        private void Awake()
        {
            initialBasePosition = transform.position;
            randomTimeOffset = Random.Range(0f, 10f);
            coinCollider = GetComponent<Collider>();

            if (coinCollider != null)
            {
                coinCollider.isTrigger = true;
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
            localAudioSource.spatialBlend = 1.0f;
            localAudioSource.minDistance = 0.5f;
            localAudioSource.maxDistance = 20.0f;
            localAudioSource.rolloffMode = AudioRolloffMode.Linear;

            if (mainRenderer == null)
            {
                mainRenderer = GetComponentInChildren<Renderer>();
            }

            if (orbitRingTransform == null)
            {
                Transform ring = transform.Find("Diamond_Orbit_Ring");
                if (ring != null) orbitRingTransform = ring;
            }

            // Configura interação com raio laser VR
            xrInteractable = GetComponent<XRSimpleInteractable>();
            if (xrInteractable == null)
            {
                xrInteractable = gameObject.AddComponent<XRSimpleInteractable>();
            }

            xrInteractable.hoverEntered.AddListener(OnLaserHoverEntered);
            xrInteractable.selectEntered.AddListener(OnLaserSelectEntered);
        }

        private void Start()
        {
            FindPlayerTarget();
        }

        private void OnDestroy()
        {
            if (xrInteractable != null)
            {
                xrInteractable.hoverEntered.RemoveListener(OnLaserHoverEntered);
                xrInteractable.selectEntered.RemoveListener(OnLaserSelectEntered);
            }
        }

        public void Configure(
            CoinType type,
            int points,
            AudioClip chime,
            AudioClip whoosh,
            AudioClip fanfare,
            ParticleSystem particles)
        {
            coinType = type;
            basePointValue = points;
            pickupChimeClip = chime;
            magnetWhooshClip = whoosh;
            secretFanfareClip = fanfare;
            collectSparklesPs = particles;
            initialBasePosition = transform.position;

            if (coinType == CoinType.BonusDiamond)
            {
                floatingTextColor = new Color(0.25f, 0.90f, 1.0f);
            }
        }

        private void FindPlayerTarget()
        {
            // 1. Procura primeiro Pessoa_Virtual_sem3D (jogador com exoesqueleto)
            GameObject playerGo = GameObject.Find("Pessoa_Virtual_sem3D");
            if (playerGo != null)
            {
                playerTarget = playerGo.transform;
                return;
            }

            // 2. Fallback para Main Camera (XR Origin Head)
            if (Camera.main != null)
            {
                playerTarget = Camera.main.transform;
                return;
            }

            // 3. Fallback para qualquer CharacterController ou XR Origin
            var cc = FindFirstObjectByType<CharacterController>();
            if (cc != null)
            {
                playerTarget = cc.transform;
            }
        }

        private void Update()
        {
            if (isCollected) return;

            // 1. Rotação do corpo principal no eixo vertical do mundo
            transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.World);

            // 2. Rotação do anel orbital em velocidade e eixo distintos para a moeda bônus
            if (orbitRingTransform != null)
            {
                orbitRingTransform.Rotate(Vector3.up, orbitRingRotationSpeed * Time.deltaTime, Space.Self);
            }

            // 3. Atualiza alvo do jogador se ainda não inicializado
            if (playerTarget == null)
            {
                FindPlayerTarget();
            }

            // Se o alvo não estiver disponível, apenas executa o bobbing no lugar
            if (playerTarget == null)
            {
                ApplyBobbing();
                return;
            }

            // Posição magnética no peito do paciente
            Vector3 targetChestPos = playerTarget.position + (Vector3.up * chestHeightOffset);
            float distToTarget = Vector3.Distance(transform.position, targetChestPos);

            // 4. Detecção e Atração Magnética
            if (!isAttracting)
            {
                // Verifica se entrou no raio de atração do paciente
                if (distToTarget <= magneticAttractionRadius)
                {
                    StartMagneticAttraction();
                }
                else
                {
                    ApplyBobbing();
                }
            }
            else
            {
                // Executa movimento magnético suave acelerado até o peito
                attractionDuration += Time.deltaTime;
                float progress = Mathf.Clamp01(attractionDuration * 1.8f);
                float speed = Mathf.Lerp(minAttractionSpeed, maxAttractionSpeed, progress * progress);

                // Curva de deslocamento com ligeira atração em arco
                Vector3 newPos = Vector3.MoveTowards(transform.position, targetChestPos, speed * Time.deltaTime);
                transform.position = newPos;

                // Checa distância de coleta ao colidir/alcançar o tronco
                if (distToTarget <= collectionDistanceThreshold)
                {
                    Collect(isLaserCollect: false);
                }
            }
        }

        private void ApplyBobbing()
        {
            float yOffset = Mathf.Sin((Time.time + randomTimeOffset) * bobFrequency) * bobAmplitude;
            transform.position = new Vector3(initialBasePosition.x, initialBasePosition.y + yOffset, initialBasePosition.z);
        }

        private void StartMagneticAttraction()
        {
            isAttracting = true;
            attractionDuration = 0f;

            // Reproduz o áudio suave de vácuo magnético
            if (magnetWhooshClip != null && localAudioSource != null)
            {
                localAudioSource.PlayOneShot(magnetWhooshClip, 0.70f);
            }

            // Pulso háptico suave avisando o início da atração
            if (VRFeedbackManager.Instance != null && xrInteractable != null)
            {
                VRFeedbackManager.Instance.TriggerHaptic(xrInteractable, 0.20f, 0.08f);
            }
        }

        #region Laser Interaction
        private void OnLaserHoverEntered(HoverEnterEventArgs args)
        {
            if (isCollected) return;

            // Efeito sutil ao mirar com o laser (pulso leve)
            if (VRFeedbackManager.Instance != null && xrInteractable != null)
            {
                VRFeedbackManager.Instance.TriggerHaptic(xrInteractable, 0.15f, 0.05f);
            }
        }

        private void OnLaserSelectEntered(SelectEnterEventArgs args)
        {
            if (isCollected) return;

            // Coleta ativada via clique de gatilho do laser VR
            Collect(isLaserCollect: true);
        }
        #endregion

        private void OnTriggerEnter(Collider other)
        {
            if (isCollected) return;

            // Coleta instantânea caso colida diretamente com as mãos VR ou o corpo do paciente
            if (other.CompareTag("Player") ||
                other.name.Contains("Pessoa") ||
                other.name.Contains("Hand") ||
                other.name.Contains("Direct") ||
                other.name.Contains("XR"))
            {
                Collect(isLaserCollect: false);
            }
        }

        public void Collect(bool isLaserCollect)
        {
            if (isCollected) return;
            isCollected = true;
            isAttracting = false;

            // 1. Atualização do Combo Streak Estático
            float timeSinceLast = Time.time - s_lastCollectTime;
            if (timeSinceLast <= ComboTimeoutWindow)
            {
                s_currentComboStreak = Mathf.Min(MaxComboMultiplier, s_currentComboStreak + 1);
            }
            else
            {
                s_currentComboStreak = 1;
            }
            s_lastCollectTime = Time.time;

            int earnedScore = basePointValue * s_currentComboStreak;

            // 2. Notifica Gerenciador de Pontuação
            if (CoinScoreManager.Instance != null)
            {
                CoinScoreManager.Instance.RegisterCoinCollected(coinType, earnedScore, s_currentComboStreak);
            }

            // 3. Reprodução de Áudio com Pitch Escalonado de Combo
            float comboPitch = Mathf.Clamp(1.0f + ((s_currentComboStreak - 1) * 0.08f), 1.0f, 1.60f);

            if (coinType == CoinType.BonusDiamond && isLaserCollect && secretFanfareClip != null)
            {
                PlayDetachedAudio(secretFanfareClip, 1.0f, 1.0f);
            }
            else if (pickupChimeClip != null)
            {
                PlayDetachedAudio(pickupChimeClip, comboPitch, 0.90f);
            }

            // 4. Feedback Háptico nos Controladores VR
            if (VRFeedbackManager.Instance != null && xrInteractable != null)
            {
                float hapticIntensity = Mathf.Clamp01(0.35f + (s_currentComboStreak * 0.08f));
                float hapticDuration = isLaserCollect ? 0.22f : 0.12f;
                VRFeedbackManager.Instance.TriggerHaptic(xrInteractable, hapticIntensity, hapticDuration);
            }

            // 5. Partículas de Explosão Estelar / Cristalina
            if (collectSparklesPs != null)
            {
                collectSparklesPs.transform.SetParent(null);
                collectSparklesPs.Play();
                Destroy(collectSparklesPs.gameObject, 2.5f);
            }

            // 6. Texto Flutuante 3D de Pontuação e Combo
            SpawnFloatingScore(earnedScore, s_currentComboStreak);

            // 7. Oculta e Desativa visual e colisor imediatamente
            if (coinCollider != null) coinCollider.enabled = false;
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers) r.enabled = false;

            // Destrói o objeto após tempo para o som concluir
            Destroy(gameObject, 1.5f);
        }

        private void PlayDetachedAudio(AudioClip clip, float pitch, float volume)
        {
            GameObject audioObj = new GameObject("DetachedCoinAudio_" + gameObject.name);
            audioObj.transform.position = transform.position;
            AudioSource src = audioObj.AddComponent<AudioSource>();
            src.clip = clip;
            src.pitch = pitch;
            src.volume = volume;
            src.spatialBlend = 1.0f;
            src.minDistance = 0.8f;
            src.maxDistance = 25.0f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.Play();
            Destroy(audioObj, clip.length + 0.2f);
        }

        private void SpawnFloatingScore(int points, int combo)
        {
            GameObject textObj = new GameObject("FloatingScore_" + points);
            textObj.transform.position = transform.position + (Vector3.up * 0.15f);

            TextMesh tm = textObj.AddComponent<TextMesh>();
            string comboTag = combo > 1 ? $"\n<size=16>x{combo} COMBO!</size>" : "";
            tm.text = $"+{points}{comboTag}";
            tm.fontSize = 26;
            tm.characterSize = 0.045f;
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = floatingTextColor;

            StartCoroutine(AnimateFloatingScore(textObj));
        }

        private IEnumerator AnimateFloatingScore(GameObject textObj)
        {
            float duration = 1.1f;
            float elapsed = 0f;
            Vector3 startPos = textObj.transform.position;
            TextMesh tm = textObj.GetComponent<TextMesh>();
            Color initialColor = tm.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Flutua para cima suavemente
                textObj.transform.position = startPos + new Vector3(0f, Mathf.Sin(t * Mathf.PI * 0.5f) * 0.65f, 0f);

                // Olha para a câmera/jogador
                if (Camera.main != null)
                {
                    textObj.transform.rotation = Quaternion.LookRotation(textObj.transform.position - Camera.main.transform.position);
                }

                // Fade out na segunda metade
                if (t > 0.5f)
                {
                    float alpha = 1.0f - ((t - 0.5f) / 0.5f);
                    tm.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);
                }

                yield return null;
            }

            Destroy(textObj);
        }
    }
}
