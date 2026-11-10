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
    /// - Integração com passos e simetria de DroneGuiaIA e TelemetriaClinica.
    /// - Animação procedural de pirueta acrobática 360° com ondulação vertical (swoop).
    /// - Canhões duplos de confetes coloridos nos propulsores laterais.
    /// - Fanfarra musical triunfante (fanfare_triumph.wav) e estouro de confetes (confetti_cannon_pop.wav).
    /// - Balão de fala 3D com auto-billboard voltado para o paciente, exibindo mensagens motivacionais em português.
    /// - Sincronização expressiva com o DroneAffectiveSystem (visor LED Happy).
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
        [Tooltip("Altura do balão em relação ao centro do Drone")]
        public float bubbleElevationOffset = 0.58f;

        [Header("Canhões Duplos de Confetes")]
        public ParticleSystem leftConfettiCannon;
        public ParticleSystem rightConfettiCannon;

        [Header("Áudio e Fanfarra 3D")]
        public AudioSource celebrationAudioSource;
        public AudioClip sfxFanfare;
        public AudioClip sfxConfettiPop;

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

            if (celebrationAudioSource == null)
            {
                celebrationAudioSource = GetComponent<AudioSource>();
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
            UpdateSpeechBubbleBillboard();
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
        /// Dispara a celebração completa com coreografia, confetes, fanfarra e fala motivadora.
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

            // 2. Dispara os canhões duplos de confetes
            FireConfettiCannons();

            // 3. Toca Fanfarra Triunfante e som do canhão
            PlayCelebrationAudio();

            // 4. Ativa expressão facial alegre no visor LED
            if (affectiveSystem != null)
            {
                affectiveSystem.SetExpression(DroneLEDExpression.Happy);
            }

            Debug.Log($"[TASK-05] Drone Pet celebrando {category}: \"{phrase}\"");
        }

        private void FireConfettiCannons()
        {
            if (leftConfettiCannon != null)
            {
                leftConfettiCannon.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                leftConfettiCannon.Play();
            }

            if (rightConfettiCannon != null)
            {
                rightConfettiCannon.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                rightConfettiCannon.Play();
            }
        }

        private void PlayCelebrationAudio()
        {
            if (celebrationAudioSource == null) return;

            // Dispara o som de estouro dos canhões
            if (sfxConfettiPop != null)
            {
                celebrationAudioSource.PlayOneShot(sfxConfettiPop, 0.90f);
            }

            // Dispara a fanfarra triunfante
            if (sfxFanfare != null)
            {
                celebrationAudioSource.PlayOneShot(sfxFanfare, 1.0f);
            }
            else if (VRFeedbackManager.Instance != null && VRFeedbackManager.Instance.clipSuccessChime != null)
            {
                VRFeedbackManager.Instance.PlaySound(VRFeedbackManager.Instance.clipSuccessChime, transform.position, 1.0f);
            }
        }

        private void UpdateCelebrationSequence()
        {
            celebrationTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(celebrationTimer / celebrationDuration);

            // Fase 1: Pirueta Acrobática 360° com Swoop Vertical (primeiros 2.0s)
            float pirouetteDuration = 2.0f;
            if (celebrationTimer <= pirouetteDuration)
            {
                float tNorm = celebrationTimer / pirouetteDuration;
                // Curva suave de interpolação: rápida no meio, suave no início e término
                float smoothT = Mathf.SmoothStep(0f, 1f, tNorm);

                pirouetteYawOffset = smoothT * 360.0f;
                // Swoop vertical harmonico (arco para cima de +0.14m)
                pirouetteVerticalOffset = Mathf.Sin(tNorm * Mathf.PI) * pirouetteVerticalSwoop;
                // Leve inclinação dinâmica de roll e pitch para dar acrobacia realista
                pirouetteRollOffset = Mathf.Sin(tNorm * Mathf.PI * 2.0f) * 14.0f;
                pirouettePitchOffset = -Mathf.Sin(tNorm * Mathf.PI) * 10.0f;
            }
            else
            {
                // Drone acomoda-se de volta suavemente
                pirouetteYawOffset = Mathf.Lerp(pirouetteYawOffset, 0f, Time.deltaTime * 6f);
                pirouetteVerticalOffset = Mathf.Lerp(pirouetteVerticalOffset, 0f, Time.deltaTime * 6f);
                pirouetteRollOffset = Mathf.Lerp(pirouetteRollOffset, 0f, Time.deltaTime * 8f);
                pirouettePitchOffset = Mathf.Lerp(pirouettePitchOffset, 0f, Time.deltaTime * 8f);
            }

            // Aplica os offsets ao transform do Drone se não estiver sendo arrastado manualmente
            if (droneGuia != null && !droneGuia.estaArrastandoDrone)
            {
                transform.rotation = transform.rotation * Quaternion.Euler(pirouettePitchOffset * Time.deltaTime * 4f,
                                                                          (celebrationTimer <= pirouetteDuration ? (360f / pirouetteDuration) : 0f) * Time.deltaTime,
                                                                          pirouetteRollOffset * Time.deltaTime * 4f);
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

            // Restaura o HUD padrão de telemetria
            Transform hudT = transform.Find("Canvas_Holograma_Clinico") ?? transform.Find("HUD_Holograma_Drone");
            if (hudT != null) hudT.gameObject.SetActive(true);

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

            // Oculta temporariamente o HUD padrão para dar destaque limpo ao balão
            Transform hudT = transform.Find("Canvas_Holograma_Clinico") ?? transform.Find("HUD_Holograma_Drone");
            if (hudT != null) hudT.gameObject.SetActive(false);

            if (txtMotivationalSpeech != null)
            {
                txtMotivationalSpeech.text = text;
            }

            if (txtCategoryBadge != null)
            {
                txtCategoryBadge.text = !string.IsNullOrEmpty(badge) ? badge : "[CELEBRAÇÃO CLÍNICA]";
            }

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

            Transform hudT = transform.Find("Canvas_Holograma_Clinico") ?? transform.Find("HUD_Holograma_Drone");
            if (hudT != null) hudT.gameObject.SetActive(true);
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
                Transform hudT = transform.Find("Canvas_Holograma_Clinico") ?? transform.Find("HUD_Holograma_Drone");
                if (hudT != null) hudT.gameObject.SetActive(true);
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

            // Mantém o balão posicionado acima do drone com offset vertical suave
            Vector3 worldAnchorPos = transform.position + Vector3.up * (bubbleElevationOffset + pirouetteVerticalOffset);
            speechBubbleAnchor.position = worldAnchorPos;

            // Encontra a câmera alvo
            Transform camTarget = cameraPaciente;
            if (camTarget == null && Camera.main != null)
                camTarget = Camera.main.transform;

            if (camTarget != null)
            {
                Vector3 dirBalaoParaCam = camTarget.position - speechBubbleAnchor.position;
                dirBalaoParaCam.y *= 0.25f;

                if (dirBalaoParaCam.sqrMagnitude > 0.001f)
                {
                    // Como a face frontal do Canvas em Unity UI olha para o -Z local do Canvas,
                    // LookRotation(dirBalaoParaCam) aponta +Z para a câmera, logo precisamos de 180° em Y
                    // para que a face frontal com o texto fique orientada perfeitamente para a câmera!
                    speechBubbleAnchor.rotation = Quaternion.LookRotation(dirBalaoParaCam.normalized, Vector3.up) * Quaternion.Euler(0f, 180f, 0f);
                }
            }
        }

        // =========================================================================
        // MÉTODOS DE TESTE E VISUAL QA AUTOMATIZADOS
        // =========================================================================

        /// <summary>
        /// Configura a pose exata para a captura do print TASK-05_Celebration_Pirouette.png:
        /// Drone inclinado na pirueta acrobática com confetes no ar.
        /// </summary>
        public void SetVisualQAPirouettePose()
        {
            LocateDependencies();
            LoadPhrasesBank();

            isCelebrating = true;
            celebrationTimer = 0.95f; // Meio da pirueta (pico do swoop)
            pirouetteVerticalOffset = pirouetteVerticalSwoop;

            Transform hudT = transform.Find("Canvas_Holograma_Clinico") ?? transform.Find("HUD_Holograma_Drone");
            if (hudT != null) hudT.gameObject.SetActive(false);

            transform.localRotation = Quaternion.Euler(-6.0f, 65.0f, 16.0f);

            if (speechBubbleAnchor != null)
            {
                speechBubbleAnchor.gameObject.SetActive(true);
                speechBubbleAnchor.localScale = Vector3.one;
                speechBubbleAnchor.localPosition = new Vector3(0f, bubbleElevationOffset + pirouetteVerticalSwoop, 0f);
                // O balão fica voltado para o jogador (+Z do mundo)
                speechBubbleAnchor.rotation = Quaternion.Euler(0f, 180f, 0f);

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
        /// Close-up focado no balão de fala 3D com texto nítido em português.
        /// </summary>
        public void SetVisualQASpeechBubblePose(string message = null, string tag = null)
        {
            LocateDependencies();
            LoadPhrasesBank();

            isCelebrating = true;
            celebrationTimer = 1.8f;
            pirouetteVerticalOffset = 0.0f;
            transform.localRotation = Quaternion.identity;

            Transform hudT = transform.Find("Canvas_Holograma_Clinico") ?? transform.Find("HUD_Holograma_Drone");
            if (hudT != null) hudT.gameObject.SetActive(false);

            if (speechBubbleAnchor != null)
            {
                speechBubbleAnchor.gameObject.SetActive(true);
                speechBubbleAnchor.localScale = Vector3.one;
                speechBubbleAnchor.localPosition = new Vector3(0f, bubbleElevationOffset, 0f);
                // Canvas orientado de frente para a câmera no +Z
                speechBubbleAnchor.rotation = Quaternion.Euler(0f, 180f, 0f);

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

            Transform hudT = transform.Find("Canvas_Holograma_Clinico") ?? transform.Find("HUD_Holograma_Drone");
            if (hudT != null) hudT.gameObject.SetActive(false);

            transform.localRotation = Quaternion.identity;

            if (speechBubbleAnchor != null)
            {
                speechBubbleAnchor.gameObject.SetActive(true);
                speechBubbleAnchor.localScale = Vector3.one;
                speechBubbleAnchor.localPosition = new Vector3(0f, bubbleElevationOffset, 0f);
                speechBubbleAnchor.rotation = Quaternion.Euler(0f, 180f, 0f);

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
                leftConfettiCannon.Clear();
                leftConfettiCannon.Play();
                leftConfettiCannon.Simulate(simulationTime, true, true);
            }

            if (rightConfettiCannon != null)
            {
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
    }
}
