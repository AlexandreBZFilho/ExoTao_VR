using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRRehab.Core;

namespace VRRehab.DronePet
{
    /// <summary>
    /// Categorias de celebração clínica para o Drone Pet (TASK-05).
    /// Permite sortear frases motivadoras contextuais do arquivo JSON.
    /// </summary>
    public enum CelebrationCategory
    {
        Passos,
        Simetria,
        Distancia,
        Geral
    }

    /// <summary>
    /// Estrutura serializável para o carregamento do banco de frases motivacionais em JSON.
    /// Localizado em Assets/Resources/DroneEncouragementPhrases.json
    /// </summary>
    [Serializable]
    public class EncouragementPhrasesData
    {
        public List<string> passos = new List<string>();
        public List<string> simetria = new List<string>();
        public List<string> distancia = new List<string>();
        public List<string> geral = new List<string>();

        public string GetRandomPhrase(CelebrationCategory category)
        {
            List<string> list = category switch
            {
                CelebrationCategory.Passos => passos,
                CelebrationCategory.Simetria => simetria,
                CelebrationCategory.Distancia => distancia,
                _ => geral
            };

            if (list == null || list.Count == 0)
            {
                list = geral;
            }

            if (list != null && list.Count > 0)
            {
                int idx = UnityEngine.Random.Range(0, list.Count);
                return list[idx];
            }

            return "Parabéns pelo seu esforço e dedicação! Continue firme!";
        }
    }

    /// <summary>
    /// Sistema de Celebração de Metas Clínicas para o Drone Pet (TASK-05):
    /// - ISOLAMENTO FÍSICO ESTRITO DA PIROUETA (TASK-03):
    ///   A pirueta acrobática 360° (yaw, swoop +0.14m, roll 14°, pitch -10°) é aplicada EXCLUSIVAMENTE
    ///   sobre as coordenadas locais do nó filho visual 'Modelo_3D_ExoBot'.
    ///   O nó raiz '[IC] Drone_Guia_IA_ExoBot' permanece 100% estático em relação ao paciente,
    ///   sem deriva no 'offsetLocal' e sem avançar ou empurrar o jogador.
    /// - PRESERVAÇÃO TOTAL DO HUD CLÍNICO & ELEVAÇÃO DO BALÃO:
    ///   O HUD Holográfico com dados de marcha NUNCA é ocultado durante a celebração.
    ///   O balão holográfico de fala situa-se a Y = 0.96m, flutuando harmoniosamente acima do HUD.
    /// - OTIMIZAÇÃO RIGOROSA VR-SAFE (90 FPS):
    ///   Máximo 40 partículas nos canhões de confetes (20 por canhão), colisão desativada, sombras desativadas.
    /// - ÁUDIO CALIBRADO E SINCRONIA AFETIVA:
    ///   Fanfarra suave (<= -8.5 dBFS, vol 0.22f), estouro suave (vol 0.20f), expressão Happy no visor LED.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class DroneMilestoneCelebration : MonoBehaviour
    {
        public static DroneMilestoneCelebration Instance { get; private set; }

        [Header("Referências Centrais")]
        [Tooltip("Referência ao DroneGuiaIA")]
        public DroneGuiaIA droneGuia;
        [Tooltip("Referência ao módulo de telemetria clínica")]
        public TelemetriaClinica telemetria;
        [Tooltip("Referência ao sistema afetivo (expressões faciais LED)")]
        public DroneAffectiveSystem affectiveSystem;
        [Tooltip("Câmera do paciente (para orientação do balão de fala)")]
        public Transform cameraPaciente;

        [Header("Nó Visual Filho (Isolamento Físico Estrito)")]
        [Tooltip("Transform do modelo 3D visual 'Modelo_3D_ExoBot'. A pirueta acrobática 360° atua EXCLUSIVAMENTE aqui")]
        public Transform visualModelTransform;

        [Header("Balão de Fala 3D")]
        [Tooltip("Objeto âncora do balão de fala sobre o Drone")]
        public Transform speechBubbleAnchor;
        [Tooltip("Canvas em WorldSpace do balão de fala")]
        public Canvas speechBubbleCanvas;
        [Tooltip("CanvasGroup para animação de fade/escala")]
        public CanvasGroup bubbleCanvasGroup;
        [Tooltip("Texto principal da mensagem motivadora")]
        public TextMeshProUGUI txtMotivationalSpeech;
        [Tooltip("Badge indicativo da categoria da meta")]
        public TextMeshProUGUI txtCategoryBadge;
        [Tooltip("Fundo/Painel visual do balão")]
        public Image imgBubbleBackground;
        [Tooltip("Altura do balão em relação ao centro do Drone (Y = 0.96m para posicionar acima do HUD clínico)")]
        public float bubbleElevationOffset = 0.96f;

        [Header("Canhões Duplos de Confetes (VR-Safe: Máx 40 Partículas)")]
        public ParticleSystem leftConfettiCannon;
        public ParticleSystem rightConfettiCannon;

        [Header("Áudio e Fanfarra 3D Calibrados")]
        public AudioSource celebrationAudioSource;
        public AudioClip sfxFanfare;
        public AudioClip sfxConfettiPop;
        [Range(0.05f, 0.40f)] public float fanfareVolume = 0.22f;
        [Range(0.05f, 0.40f)] public float confettiPopVolume = 0.20f;

        [Header("Parâmetros da Celebração")]
        [Tooltip("Duração total da celebração em segundos")]
        public float celebrationDuration = 3.8f;
        [Tooltip("A cada quantos passos celebrar")]
        public int stepsMilestoneInterval = 15;
        [Tooltip("Limiar de simetria excelente para celebração")]
        public float excellentSymmetryThreshold = 80.0f;
        [Tooltip("Distância entre marcos em metros")]
        public float distanceMilestoneInterval = 25.0f;
        [Tooltip("Altura máxima do swoop acrobático da pirueta em metros")]
        public float pirouetteVerticalSwoop = 0.14f;

        [Header("Estado Atual")]
        [SerializeField] private bool isCelebrating = false;
        public bool IsCelebrating => isCelebrating;
        [SerializeField] private float celebrationTimer = 0f;
        [SerializeField] private CelebrationCategory lastCelebratedCategory = CelebrationCategory.Geral;

        // Pose de repouso do nó visual filho
        private Vector3 visualRestPos = Vector3.zero;
        private Quaternion visualRestRot = Quaternion.identity;
        private bool hasCachedVisualRestPose = false;

        // Controle de marcos atingidos
        private int lastStepMilestone = 0;
        private float lastDistanceMilestone = 0f;
        private float lastSymmetryCelebrationTime = -60f;
        private bool wasSymmetryPoor = false;

        // Animação procedural
        private float pirouetteYawOffset = 0f;
        private float pirouettePitchOffset = 0f;
        private float pirouetteRollOffset = 0f;
        private float pirouetteVerticalOffset = 0f;
        private float bubbleScaleAnim = 0f;
        private float bubbleScaleVelocity = 0f;

        // Banco de Frases JSON
        private EncouragementPhrasesData phrasesData;
        private const string JsonResourcePath = "DroneEncouragementPhrases";

        void Awake()
        {
            if (Application.isPlaying)
            {
                if (Instance != null && Instance != this)
                {
                    Destroy(this);
                    return;
                }
                Instance = this;
            }

            LocateDependencies();
            LoadPhrasesBank();
        }

        void OnEnable()
        {
            LocateDependencies();
            LoadPhrasesBank();
        }

        void OnDisable()
        {
            if (visualModelTransform != null && hasCachedVisualRestPose)
            {
                visualModelTransform.localPosition = visualRestPos;
                visualModelTransform.localRotation = visualRestRot;
            }
        }

        void Start()
        {
            LocateDependencies();
            LoadPhrasesBank();

            if (Application.isPlaying)
            {
                HideSpeechBubbleInstant();
            }
        }

        public void LocateDependencies()
        {
            if (droneGuia == null)
                droneGuia = GetComponent<DroneGuiaIA>() ?? FindFirstObjectByType<DroneGuiaIA>();

            if (telemetria == null)
                telemetria = FindFirstObjectByType<TelemetriaClinica>();

            if (affectiveSystem == null)
                affectiveSystem = GetComponent<DroneAffectiveSystem>() ?? FindFirstObjectByType<DroneAffectiveSystem>();

            if (cameraPaciente == null)
            {
                if (Camera.main != null)
                    cameraPaciente = Camera.main.transform;
                else if (droneGuia != null && droneGuia.cameraPaciente != null)
                    cameraPaciente = droneGuia.cameraPaciente;
            }

            CacheVisualModelTransform();

            if (celebrationAudioSource == null)
            {
                celebrationAudioSource = GetComponent<AudioSource>();
            }

            ConfigureAudioSourceProperties();
            ConfigureConfettiVRSafe(leftConfettiCannon);
            ConfigureConfettiVRSafe(rightConfettiCannon);
        }

        public void CacheVisualModelTransform()
        {
            if (visualModelTransform == null)
            {
                visualModelTransform = transform.Find("Modelo_3D_ExoBot");
                if (visualModelTransform == null && transform.childCount > 0)
                {
                    visualModelTransform = transform.GetChild(0);
                }
            }

            if (visualModelTransform != null && !hasCachedVisualRestPose)
            {
                visualRestPos = visualModelTransform.localPosition;
                visualRestRot = visualModelTransform.localRotation;
                hasCachedVisualRestPose = true;
            }
        }

        private void ConfigureAudioSourceProperties()
        {
            if (celebrationAudioSource == null) return;
            celebrationAudioSource.playOnAwake = false;
            celebrationAudioSource.spatialBlend = 1.0f; // 3D Espacial estrito
            celebrationAudioSource.dopplerLevel = 0f; // Sem distorção de pitch em VR
            celebrationAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            celebrationAudioSource.minDistance = 1.0f;
            celebrationAudioSource.maxDistance = 12.0f;

            if (sfxFanfare == null)
            {
                sfxFanfare = Resources.Load<AudioClip>("Audio/drone_milestone_fanfare")
                             ?? Resources.Load<AudioClip>("drone_milestone_fanfare")
                             ?? Resources.Load<AudioClip>("fanfare_triumph");
            }
            if (sfxConfettiPop == null)
            {
                sfxConfettiPop = Resources.Load<AudioClip>("Audio/confetti_cannon_pop")
                                 ?? Resources.Load<AudioClip>("confetti_cannon_pop");
            }
        }

        public static void ConfigureConfettiVRSafe(ParticleSystem ps)
        {
            if (ps == null) return;
            if (ps.isPlaying)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            var main = ps.main;
            main.maxParticles = 40; // Teto estrito de 40 partículas
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // Desativa colisão física PhysX (zero CPU cost)
            var col = ps.collision;
            col.enabled = false;

            // Desativa sombras dinâmicas no ParticleSystemRenderer (zero passes extras por olho)
            var rend = ps.GetComponent<ParticleSystemRenderer>();
            if (rend != null)
            {
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
                rend.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                rend.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
        }

        public void LoadPhrasesBank()
        {
            if (phrasesData != null && phrasesData.passos.Count > 0) return;

            TextAsset jsonAsset = Resources.Load<TextAsset>(JsonResourcePath);
            if (jsonAsset != null && !string.IsNullOrEmpty(jsonAsset.text))
            {
                try
                {
                    phrasesData = JsonUtility.FromJson<EncouragementPhrasesData>(jsonAsset.text);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[TASK-05] Erro ao analisar {JsonResourcePath}.json: {ex.Message}. Utilizando banco padrão.");
                }
            }

            if (phrasesData == null || phrasesData.passos.Count == 0)
            {
                phrasesData = CreateFallbackPhrases();
            }
        }

        private EncouragementPhrasesData CreateFallbackPhrases()
        {
            var data = new EncouragementPhrasesData();
            data.passos.Add("Meta de passos superada! Cada passo fortalece sua independência motora!");
            data.passos.Add("Excelente cadência! Seus membros inferiores estão respondendo perfeitamente!");
            data.passos.Add("Mais um marco alcançado! Sua persistência é o combustível da recuperação!");
            data.passos.Add("Sensacional! Você atingiu a meta de passos com vigor e constância!");

            data.simetria.Add("Simetria perfeita! Ambas as pernas estão trabalhando com equilíbrio exemplar!");
            data.simetria.Add("Equilíbrio biomecânico impecável! Sua transferência de peso foi harmoniosa!");
            data.simetria.Add("Simetria excelente acima de 80%! Seus circuitos motores estão afinadíssimos!");

            data.distancia.Add("Nova marca de distância conquistada! Você percorreu um longo trajeto com bravura!");
            data.distancia.Add("Distância impressionante! Sua capacidade aeróbica e muscular só cresce!");

            data.geral.Add("Parabéns pelo empenho de hoje! É um privilégio ser seu companheiro robótico!");
            data.geral.Add("Sua dedicação é contagiante! Continue acreditando no seu potencial!");
            return data;
        }

        void Update()
        {
            if (!Application.isPlaying) return;

            EvaluateAutomaticMilestones();

            if (isCelebrating)
            {
                UpdateCelebrationSequence();
            }

            UpdateSpeechBubbleAnimation();
        }

        void LateUpdate()
        {
            if (Application.isPlaying)
            {
                UpdateSpeechBubbleBillboard();
            }
        }

        // =========================================================================
        // AVALIAÇÃO DE MARCOS CLÍNICOS AUTOMÁTICOS
        // =========================================================================

        private void EvaluateAutomaticMilestones()
        {
            if (isCelebrating) return;

            // 1. Sincronia com EstadoDrone.CelebrandoMeta em DroneGuiaIA
            if (droneGuia != null && droneGuia.estadoAtual == DroneGuiaIA.EstadoDrone.CelebrandoMeta)
            {
                TriggerCelebration(CelebrationCategory.Passos);
                return;
            }

            if (telemetria == null) return;

            // 2. Marco de Passos
            if (telemetria.totalPassos >= lastStepMilestone + stepsMilestoneInterval)
            {
                lastStepMilestone = (telemetria.totalPassos / stepsMilestoneInterval) * stepsMilestoneInterval;
                TriggerCelebration(CelebrationCategory.Passos);
                return;
            }

            // 3. Marco de Simetria Articular Excelente (> 80%)
            if (telemetria.totalPassos >= 8)
            {
                if (telemetria.indiceSimetriaMarcha < 75.0f)
                {
                    wasSymmetryPoor = true;
                }
                else if (wasSymmetryPoor && telemetria.indiceSimetriaMarcha >= excellentSymmetryThreshold)
                {
                    if (Time.time - lastSymmetryCelebrationTime > 25.0f)
                    {
                        wasSymmetryPoor = false;
                        lastSymmetryCelebrationTime = Time.time;
                        TriggerCelebration(CelebrationCategory.Simetria);
                        return;
                    }
                }
            }

            // 4. Marco de Distância Percorrida
            if (telemetria.distanciaPercorridaMetros >= lastDistanceMilestone + distanceMilestoneInterval)
            {
                lastDistanceMilestone = Mathf.Floor(telemetria.distanciaPercorridaMetros / distanceMilestoneInterval) * distanceMilestoneInterval;
                TriggerCelebration(CelebrationCategory.Distancia);
                return;
            }
        }

        // =========================================================================
        // DISPARO E SEQUENCIAMENTO DA CELEBRAÇÃO
        // =========================================================================

        /// <summary>
        /// Dispara a celebração completa com coreografia acrobática, confetes, fanfarra suave e fala motivadora.
        /// </summary>
        public void TriggerCelebration(CelebrationCategory category, string overrideMessage = null)
        {
            isCelebrating = true;
            celebrationTimer = 0f;
            lastCelebratedCategory = category;

            // 1. Seleciona a frase motivacional do JSON
            LoadPhrasesBank();
            string phrase = !string.IsNullOrEmpty(overrideMessage)
                ? overrideMessage
                : (phrasesData != null ? phrasesData.GetRandomPhrase(category) : "Parabéns pelo progresso!");

            string badgeTag = category switch
            {
                CelebrationCategory.Passos => "[META DE PASSOS]",
                CelebrationCategory.Simetria => "[SIMETRIA EXCELENTE]",
                CelebrationCategory.Distancia => "[DISTÂNCIA CONQUISTADA]",
                _ => "[CONQUISTA CLÍNICA]"
            };

            ShowSpeechBubble(phrase, badgeTag);

            // 2. Dispara os canhões duplos de confetes VR-Safe
            FireConfettiCannons();

            // 3. Toca Fanfarra Triunfante suave e som do canhão
            PlayCelebrationAudio();

            // 4. Ativa expressão facial alegre no visor LED durante a celebração
            if (affectiveSystem != null)
            {
                affectiveSystem.SetExpression(DroneLEDExpression.Happy, celebrationDuration);
            }

            Debug.Log($"[TASK-05] Drone Pet celebrando {category}: \"{phrase}\"");
        }

        private void FireConfettiCannons()
        {
            if (leftConfettiCannon != null)
            {
                ConfigureConfettiVRSafe(leftConfettiCannon);
                leftConfettiCannon.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                leftConfettiCannon.Play();
            }

            if (rightConfettiCannon != null)
            {
                ConfigureConfettiVRSafe(rightConfettiCannon);
                rightConfettiCannon.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                rightConfettiCannon.Play();
            }
        }

        private void PlayCelebrationAudio()
        {
            if (celebrationAudioSource == null) return;
            ConfigureAudioSourceProperties();

            // Dispara o som de estouro dos canhões calibrado em volume confortável
            if (sfxConfettiPop != null)
            {
                celebrationAudioSource.PlayOneShot(sfxConfettiPop, confettiPopVolume);
            }

            // Dispara a fanfarra triunfante suave calibrada
            if (sfxFanfare != null)
            {
                celebrationAudioSource.PlayOneShot(sfxFanfare, fanfareVolume);
            }
            else if (VRFeedbackManager.Instance != null && VRFeedbackManager.Instance.clipSuccessChime != null)
            {
                VRFeedbackManager.Instance.PlaySound(VRFeedbackManager.Instance.clipSuccessChime, transform.position, fanfareVolume);
            }
        }

        private void UpdateCelebrationSequence()
        {
            celebrationTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(celebrationTimer / celebrationDuration);

            CacheVisualModelTransform();
            if (visualModelTransform == null) return;

            // Fase 1: Pirueta Acrobática 360° com Swoop Vertical (primeiros 2.0s)
            // Aplicada EXCLUSIVAMENTE nas coordenadas locais de visualModelTransform (Modelo_3D_ExoBot).
            // O nó raiz [IC] Drone_Guia_IA_ExoBot NUNCA é movido ou rotacionado por esta lógica.
            float pirouetteDuration = 2.0f;
            if (celebrationTimer <= pirouetteDuration)
            {
                float tNorm = celebrationTimer / pirouetteDuration;
                // Curva suave de interpolação SmoothStep
                float smoothT = Mathf.SmoothStep(0f, 1f, tNorm);

                pirouetteYawOffset = smoothT * 360.0f;
                // Swoop vertical harmônico (arco para cima de +0.14m)
                pirouetteVerticalOffset = Mathf.Sin(tNorm * Mathf.PI) * pirouetteVerticalSwoop;
                // Inclinação dinâmica de roll e pitch para dar acrobacia realista
                pirouetteRollOffset = Mathf.Sin(tNorm * Mathf.PI * 2.0f) * 14.0f;
                pirouettePitchOffset = -Mathf.Sin(tNorm * Mathf.PI) * 10.0f;

                Quaternion rotAcrobat = Quaternion.Euler(pirouettePitchOffset, pirouetteYawOffset, pirouetteRollOffset);
                visualModelTransform.localPosition = visualRestPos + new Vector3(0f, pirouetteVerticalOffset, 0f);
                visualModelTransform.localRotation = visualRestRot * rotAcrobat;
            }
            else
            {
                // Fase 2: O nó visual acomoda-se suavemente de volta à pose de repouso
                float dampFactor = Time.deltaTime * 6.0f;
                visualModelTransform.localPosition = Vector3.Lerp(visualModelTransform.localPosition, visualRestPos, dampFactor);
                visualModelTransform.localRotation = Quaternion.Slerp(visualModelTransform.localRotation, visualRestRot, dampFactor);
            }

            // Fim da celebração
            if (celebrationTimer >= celebrationDuration)
            {
                ConcludeCelebration();
            }
        }

        private void ConcludeCelebration()
        {
            isCelebrating = false;
            pirouetteYawOffset = 0f;
            pirouettePitchOffset = 0f;
            pirouetteRollOffset = 0f;
            pirouetteVerticalOffset = 0f;

            if (visualModelTransform != null && hasCachedVisualRestPose)
            {
                visualModelTransform.localPosition = visualRestPos;
                visualModelTransform.localRotation = visualRestRot;
            }

            // O HUD clínico de telemetria permanece continuamente ativo!

            // Esconde suavemente o balão de fala
            HideSpeechBubbleSmooth();

            // Retorna a expressão do LED para o estado normal (ou dança se música ativa)
            if (affectiveSystem != null)
            {
                affectiveSystem.SetExpression(DroneLEDExpression.Normal);
            }
        }

        // =========================================================================
        // BALÃO DE FALA 3D E AUTO-BILLBOARD
        // =========================================================================

        public void ShowSpeechBubble(string text, string badge = null)
        {
            if (speechBubbleAnchor == null) return;

            // O HUD clínico permanece sempre ATIVO. O balão é posicionado a Y = 0.96m acima do HUD.

            if (txtMotivationalSpeech != null)
            {
                txtMotivationalSpeech.text = text;
            }

            if (txtCategoryBadge != null)
            {
                txtCategoryBadge.text = !string.IsNullOrEmpty(badge) ? badge : "[CELEBRAÇÃO CLÍNICA]";
            }

            speechBubbleAnchor.localPosition = new Vector3(0f, bubbleElevationOffset, 0f);

            if (!speechBubbleAnchor.gameObject.activeSelf)
            {
                speechBubbleAnchor.gameObject.SetActive(true);
            }

            // Inicia escala de 0 com impulso elástico para 1
            bubbleScaleAnim = 0.05f;
            bubbleScaleVelocity = 4.5f;

            if (bubbleCanvasGroup != null)
            {
                bubbleCanvasGroup.alpha = 1.0f;
            }
        }

        public void HideSpeechBubbleInstant()
        {
            if (speechBubbleAnchor != null)
            {
                speechBubbleAnchor.localScale = Vector3.zero;
                speechBubbleAnchor.gameObject.SetActive(false);
            }
            bubbleScaleAnim = 0f;
        }

        public void HideSpeechBubbleSmooth()
        {
            // Ocultação é tratada pela animação de escala decrescente
            bubbleScaleVelocity = -3.5f;
        }

        private void UpdateSpeechBubbleAnimation()
        {
            if (speechBubbleAnchor == null || !speechBubbleAnchor.gameObject.activeSelf) return;

            if (!isCelebrating && bubbleScaleAnim <= 0.01f && bubbleScaleVelocity < 0f)
            {
                speechBubbleAnchor.gameObject.SetActive(false);
                return;
            }

            float targetScale = isCelebrating ? 1.0f : 0.0f;
            // Efeito elástico suave
            bubbleScaleAnim = Mathf.SmoothDamp(bubbleScaleAnim, targetScale, ref bubbleScaleVelocity, 0.18f);
            bubbleScaleAnim = Mathf.Clamp(bubbleScaleAnim, 0f, 1.15f);

            speechBubbleAnchor.localScale = Vector3.one * bubbleScaleAnim;
            if (bubbleCanvasGroup != null)
            {
                bubbleCanvasGroup.alpha = Mathf.Clamp01(bubbleScaleAnim * 1.5f);
            }
        }

        private void UpdateSpeechBubbleBillboard()
        {
            if (speechBubbleAnchor == null || !speechBubbleAnchor.gameObject.activeSelf) return;

            // Mantém o balão ancorado a Y = 0.96m acima do drone
            speechBubbleAnchor.localPosition = new Vector3(0f, bubbleElevationOffset, 0f);

            // Encontra a câmera alvo
            Transform camTarget = cameraPaciente;
            if (camTarget == null && Camera.main != null)
                camTarget = Camera.main.transform;

            if (camTarget != null)
            {
                // Para Canvas UI WorldSpace na Unity, a rotação canônica perfeita de billboard
                // que NUNCA espelha texto é alinhar a rotação do Canvas com a rotação da câmera do jogador:
                // speechBubbleAnchor.rotation = camTarget.rotation;
                // NUNCA multiplicar por Quaternion.Euler(0, 180, 0)!
                speechBubbleAnchor.rotation = camTarget.rotation;
            }
            else
            {
                speechBubbleAnchor.localRotation = Quaternion.identity;
            }
        }

        // =========================================================================
        // MÉTODOS DE TESTE E VISUAL QA AUTOMATIZADOS (ISOLAMENTO DO ROOT GARANTIDO)
        // =========================================================================

        /// <summary>
        /// Restaura o drone e o modelo à pose de repouso limpa.
        /// </summary>
        public void ResetToRestPose()
        {
            LocateDependencies();
            isCelebrating = false;
            celebrationTimer = 0f;
            pirouetteVerticalOffset = 0f;

            if (visualModelTransform != null && hasCachedVisualRestPose)
            {
                visualModelTransform.localPosition = visualRestPos;
                visualModelTransform.localRotation = visualRestRot;
            }

            transform.localRotation = Quaternion.identity;
            HideSpeechBubbleInstant();

            if (affectiveSystem != null)
            {
                affectiveSystem.SetExpression(DroneLEDExpression.Normal);
            }
        }

        /// <summary>
        /// Configura a pose exata para a captura do print TASK-05_Celebration_Pirouette.png:
        /// Nó visual inclinado na pirueta acrobática com confetes no ar.
        /// O transform raiz permanece 100% INTACTO. O HUD clínico permanece ATIVO.
        /// </summary>
        public void SetVisualQAPirouettePose()
        {
            LocateDependencies();
            LoadPhrasesBank();

            isCelebrating = true;
            celebrationTimer = 0.95f; // Meio da pirueta (pico do swoop)
            pirouetteVerticalOffset = pirouetteVerticalSwoop;

            // Raiz estática
            transform.localRotation = Quaternion.identity;

            // Pirueta exclusivamente no nó visual
            if (visualModelTransform != null)
            {
                visualModelTransform.localPosition = visualRestPos + new Vector3(0f, pirouetteVerticalSwoop, 0f);
                visualModelTransform.localRotation = visualRestRot * Quaternion.Euler(-6.0f, 65.0f, 16.0f);
            }

            // Balão acima do HUD (Y = 0.96m)
            if (speechBubbleAnchor != null)
            {
                speechBubbleAnchor.gameObject.SetActive(true);
                speechBubbleAnchor.localScale = Vector3.one;
                speechBubbleAnchor.localPosition = new Vector3(0f, bubbleElevationOffset, 0f);
                speechBubbleAnchor.localRotation = Quaternion.identity;

                if (bubbleCanvasGroup != null) bubbleCanvasGroup.alpha = 1.0f;
                if (txtMotivationalSpeech != null)
                    txtMotivationalSpeech.text = "Sensacional! Você atingiu a meta de passos com vigor e constância!";
                if (txtCategoryBadge != null)
                    txtCategoryBadge.text = "[META DE PASSOS]";
            }

            if (affectiveSystem != null)
            {
                affectiveSystem.SetExpression(DroneLEDExpression.Happy);
            }

            SimulateConfettiParticles(0.55f);
        }

        /// <summary>
        /// Configura a pose exata para a captura do print TASK-05_Celebration_SpeechBubble.png:
        /// Close-up focado no balão de fala 3D com texto nítido em português acima do HUD clínico.
        /// </summary>
        public void SetVisualQASpeechBubblePose(string message = null, string tag = null)
        {
            LocateDependencies();
            LoadPhrasesBank();

            isCelebrating = true;
            celebrationTimer = 1.8f;
            pirouetteVerticalOffset = 0.0f;

            // Raiz estática
            transform.localRotation = Quaternion.identity;

            // Modelo visual em repouso
            if (visualModelTransform != null)
            {
                visualModelTransform.localPosition = visualRestPos;
                visualModelTransform.localRotation = visualRestRot;
            }

            // Balão acima do HUD (Y = 0.96m)
            if (speechBubbleAnchor != null)
            {
                speechBubbleAnchor.gameObject.SetActive(true);
                speechBubbleAnchor.localScale = Vector3.one;
                speechBubbleAnchor.localPosition = new Vector3(0f, bubbleElevationOffset, 0f);
                speechBubbleAnchor.localRotation = Quaternion.identity;

                if (bubbleCanvasGroup != null) bubbleCanvasGroup.alpha = 1.0f;
                if (txtMotivationalSpeech != null)
                {
                    txtMotivationalSpeech.text = !string.IsNullOrEmpty(message)
                        ? message
                        : "Simetria perfeita! Ambas as pernas estão trabalhando com equilíbrio exemplar!";
                }
                if (txtCategoryBadge != null)
                {
                    txtCategoryBadge.text = !string.IsNullOrEmpty(tag) ? tag : "[SIMETRIA EXCELENTE]";
                }
            }

            if (affectiveSystem != null)
            {
                affectiveSystem.SetExpression(DroneLEDExpression.Happy);
            }
        }

        /// <summary>
        /// Configura a pose para a captura do print TASK-05_Confetti_Explosion.png:
        /// Visão frontal panorâmica com confetes multicolores flutuando ao redor do drone.
        /// </summary>
        public void SetVisualQAConfettiExplosionPose()
        {
            LocateDependencies();
            LoadPhrasesBank();

            isCelebrating = true;
            celebrationTimer = 0.65f;
            pirouetteVerticalOffset = 0.06f;

            // Raiz estática
            transform.localRotation = Quaternion.identity;

            // Leve elevação no nó visual
            if (visualModelTransform != null)
            {
                visualModelTransform.localPosition = visualRestPos + new Vector3(0f, 0.06f, 0f);
                visualModelTransform.localRotation = visualRestRot;
            }

            // Balão visível acima do HUD
            if (speechBubbleAnchor != null)
            {
                speechBubbleAnchor.gameObject.SetActive(true);
                speechBubbleAnchor.localScale = Vector3.one;
                speechBubbleAnchor.localPosition = new Vector3(0f, bubbleElevationOffset, 0f);
                speechBubbleAnchor.localRotation = Quaternion.identity;

                if (bubbleCanvasGroup != null) bubbleCanvasGroup.alpha = 1.0f;
                if (txtMotivationalSpeech != null)
                    txtMotivationalSpeech.text = "Nova marca de distância conquistada! Você percorreu um longo trajeto com bravura!";
                if (txtCategoryBadge != null)
                    txtCategoryBadge.text = "[DISTÂNCIA CONQUISTADA]";
            }

            if (affectiveSystem != null)
            {
                affectiveSystem.SetExpression(DroneLEDExpression.Happy);
            }

            SimulateConfettiParticles(0.35f);
        }

        public void SimulateConfettiParticles(float simulationTime)
        {
            if (leftConfettiCannon != null)
            {
                ConfigureConfettiVRSafe(leftConfettiCannon);
                leftConfettiCannon.Clear();
                leftConfettiCannon.Play();
                leftConfettiCannon.Simulate(simulationTime, true, true);
            }

            if (rightConfettiCannon != null)
            {
                ConfigureConfettiVRSafe(rightConfettiCannon);
                rightConfettiCannon.Clear();
                rightConfettiCannon.Play();
                rightConfettiCannon.Simulate(simulationTime, true, true);
            }
        }

        [ContextMenu("Test Celebration - Passos")]
        public void TestPassosCelebration() => TriggerCelebration(CelebrationCategory.Passos);

        [ContextMenu("Test Celebration - Simetria")]
        public void TestSimetriaCelebration() => TriggerCelebration(CelebrationCategory.Simetria);

        [ContextMenu("Test Celebration - Distancia")]
        public void TestDistanciaCelebration() => TriggerCelebration(CelebrationCategory.Distancia);

        [ContextMenu("Test Celebration - Geral")]
        public void TestGeralCelebration() => TriggerCelebration(CelebrationCategory.Geral);

        [ContextMenu("Reset to Rest Pose")]
        public void TestResetPose() => ResetToRestPose();
    }
}
