using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRRehab.Core;
using VRRehab.DronePet;
using VRRehab.Interactive.Task11;
using VRRehab.Interactive.Task12;
using VRRehab.Interactive.Task13;
using VRRehab.Interactive.Task14;

namespace VRRehab.Interactive.Task15
{
    /// <summary>
    /// Gerenciador de Missões Clínicas e Objetivos de Sessão no Visor do Drone Pet (TASK-15).
    /// Coordena as 3 metas terapêuticas da Floresta:
    /// 1. Marcha Ativa (15 passos ou 10m de caminhada);
    /// 2. Amigo da Floresta (2 maçãs na cesta ou 2 lanternas rúnicas acesas);
    /// 3. Magia Botânica (8 moedas de energia coletadas ou canteiro brotado).
    /// Atualiza barras de progresso, emite jingles festivos e aciona a comemoração do Drone.
    /// </summary>
    [DisallowMultipleComponent]
    public class SessionQuestManager : MonoBehaviour
    {
        public static SessionQuestManager Instance { get; private set; }

        [System.Serializable]
        public class QuestData
        {
            public string id;
            public string title;
            public string description;
            public int targetCount;
            public int currentCount;
            public bool isCompleted;
            public int bonusPoints = 50;

            public float Progress => targetCount > 0 ? Mathf.Clamp01((float)currentCount / targetCount) : 0f;
        }

        [System.Serializable]
        public class QuestUIElements
        {
            public TextMeshProUGUI txtTitle;
            public TextMeshProUGUI txtStatus;
            public Image imgFillBar;
            public Image imgCheckBadge;
        }

        [Header("Missões Clínicas")]
        [SerializeField] private QuestData questGait = new QuestData
        {
            id = "quest_gait",
            title = "Marcha Ativa",
            description = "Dar 15 passos ou avançar 10m com simetria",
            targetCount = 15,
            currentCount = 0
        };

        [SerializeField] private QuestData questForestFriend = new QuestData
        {
            id = "quest_forest_friend",
            title = "Amigo da Floresta",
            description = "Colher 2 macas na cesta ou acender 2 lanternas",
            targetCount = 2,
            currentCount = 0
        };

        [SerializeField] private QuestData questBotany = new QuestData
        {
            id = "quest_botany",
            title = "Magia Botanica",
            description = "Coletar 8 moedas ou brotar canteiro magico",
            targetCount = 8,
            currentCount = 0
        };

        [Header("Elementos de UI no Visor do Drone")]
        [SerializeField] private QuestUIElements uiQuestGait;
        [SerializeField] private QuestUIElements uiQuestForestFriend;
        [SerializeField] private QuestUIElements uiQuestBotany;

        [Header("Áudio e Celebração")]
        [SerializeField] private AudioClip sfxQuestComplete;
        [SerializeField] private AudioSource audioSource;

        [Header("Cores Temáticas")]
        [SerializeField] private Color colorInProgress = new Color(0.0f, 0.95f, 1.0f, 1.0f);
        [SerializeField] private Color colorCompleted = new Color(0.25f, 1.0f, 0.45f, 1.0f);

        // Controle interno de telemetria
        private Vector3 initialPlayerPosition;
        private bool playerPositionInitialized = false;
        private Transform playerTransform;
        private DroneMilestoneCelebration droneCelebration;

        public static event Action<QuestData> OnQuestCompletedEvent;

        public QuestData QuestGait => questGait;
        public QuestData QuestForestFriend => questForestFriend;
        public QuestData QuestBotany => questBotany;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0.5f;
            }
        }

        private void Start()
        {
            LocatePlayer();
            droneCelebration = FindFirstObjectByType<DroneMilestoneCelebration>();

            // Atualiza UI inicial
            RefreshAllUI();
        }

        private void OnEnable()
        {
            // 1. Assina eventos de Moedas (Task 14)
            CoinScoreManager.OnCoinCollectedEvent += HandleCoinCollected;

            // 2. Assina eventos de Cesta de Maçãs (Task 11)
            AppleCollectionBasket.OnAppleScoredEvent += HandleAppleScored;

            // 3. Assina eventos de Lanternas Rúnicas (Task 13)
            PathLanternInteractable.OnLanternIgnitedEvent += HandleLanternIgnited;

            // 4. Assina eventos de Sementes/Plantas brotando (Task 12)
            SproutingPlantLifecycle.OnPlantSproutedEvent += HandlePlantSprouted;
        }

        private void OnDisable()
        {
            CoinScoreManager.OnCoinCollectedEvent -= HandleCoinCollected;
            AppleCollectionBasket.OnAppleScoredEvent -= HandleAppleScored;
            PathLanternInteractable.OnLanternIgnitedEvent -= HandleLanternIgnited;
            SproutingPlantLifecycle.OnPlantSproutedEvent -= HandlePlantSprouted;
        }

        private void LocatePlayer()
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                playerTransform = mainCam.transform;
                initialPlayerPosition = mainCam.transform.position;
                playerPositionInitialized = true;
            }
        }

        private void Update()
        {
            // Monitora a Missão de Marcha Ativa
            if (!questGait.isCompleted)
            {
                UpdateGaitProgress();
            }
        }

        private void UpdateGaitProgress()
        {
            int currentStepCount = 0;

            // 1. Tenta obter passos da Telemetria Clínica oficial
            if (TelemetriaClinica.Instancia != null)
            {
                currentStepCount = TelemetriaClinica.Instancia.totalPassos;

                // Se passos ainda não foram detectados pelo receptor, usa distância
                if (currentStepCount == 0 && TelemetriaClinica.Instancia.distanciaPercorridaMetros > 1.0f)
                {
                    currentStepCount = Mathf.FloorToInt(TelemetriaClinica.Instancia.distanciaPercorridaMetros * 1.5f);
                }
            }

            // 2. Fallback por movimentação do HMD no espaço da Floresta
            if (currentStepCount == 0 && playerTransform != null && playerPositionInitialized)
            {
                float distTraveled = Vector3.Distance(
                    new Vector3(playerTransform.position.x, 0f, playerTransform.position.z),
                    new Vector3(initialPlayerPosition.x, 0f, initialPlayerPosition.z)
                );
                currentStepCount = Mathf.FloorToInt(distTraveled * 1.4f);
            }

            if (currentStepCount > questGait.currentCount)
            {
                questGait.currentCount = Mathf.Min(currentStepCount, questGait.targetCount);
                UpdateQuestUI(questGait, uiQuestGait);

                if (questGait.currentCount >= questGait.targetCount)
                {
                    CompleteQuest(questGait, "Excelente cadencia de marcha! Meta de passos alcancada!");
                }
            }
        }

        private void HandleCoinCollected(int delta, int total, int combo)
        {
            if (questBotany.isCompleted) return;

            questBotany.currentCount++;
            UpdateQuestUI(questBotany, uiQuestBotany);

            if (questBotany.currentCount >= questBotany.targetCount)
            {
                CompleteQuest(questBotany, "Coleta de Energia Completa! Magia Botanica ativada!");
            }
        }

        private void HandleAppleScored(int points, int count)
        {
            if (questForestFriend.isCompleted) return;

            questForestFriend.currentCount++;
            UpdateQuestUI(questForestFriend, uiQuestForestFriend);

            if (questForestFriend.currentCount >= questForestFriend.targetCount)
            {
                CompleteQuest(questForestFriend, "Pontaria exemplar com as macas! Amigo da Floresta cumprido!");
            }
        }

        private void HandleLanternIgnited(PathLanternInteractable lantern)
        {
            if (questForestFriend.isCompleted) return;

            questForestFriend.currentCount++;
            UpdateQuestUI(questForestFriend, uiQuestForestFriend);

            if (questForestFriend.currentCount >= questForestFriend.targetCount)
            {
                CompleteQuest(questForestFriend, "Caminho iluminado com maestria! Amigo da Floresta cumprido!");
            }
        }

        private void HandlePlantSprouted(SproutingPlantLifecycle plant)
        {
            if (questBotany.isCompleted) return;

            questBotany.currentCount = questBotany.targetCount;
            UpdateQuestUI(questBotany, uiQuestBotany);
            CompleteQuest(questBotany, "Broto magico floresceu com energia viva! Magia Botanica completa!");
        }

        private void CompleteQuest(QuestData quest, string celebrationMessage)
        {
            if (quest.isCompleted) return;
            quest.isCompleted = true;
            quest.currentCount = quest.targetCount;

            // 1. Áudio alegre de conclusão de missão
            if (audioSource != null && sfxQuestComplete != null)
            {
                audioSource.pitch = UnityEngine.Random.Range(0.98f, 1.02f);
                audioSource.PlayOneShot(sfxQuestComplete, 1.0f);
            }

            // 2. Concede pontos no CoinScoreManager
            if (CoinScoreManager.Instance != null)
            {
                CoinScoreManager.Instance.RegisterCoinCollected(
                    CollectibleOrbCoin.CoinType.BonusDiamond, quest.bonusPoints, 1);
            }

            // 3. Notifica o Drone Pet para celebrar
            if (droneCelebration != null)
            {
                droneCelebration.TriggerCelebration(CelebrationCategory.Geral, celebrationMessage);
            }

            // 4. Dispara evento
            OnQuestCompletedEvent?.Invoke(quest);

            // 5. Atualiza UI correspondente
            RefreshAllUI();

            Debug.Log($"[SessionQuestManager] Missao CONCLUIDA: {quest.title} (+{quest.bonusPoints} pts)");
        }

        public void BindUIElements(QuestUIElements gaitUI, QuestUIElements friendUI, QuestUIElements botanyUI, AudioClip jingleClip)
        {
            uiQuestGait = gaitUI;
            uiQuestForestFriend = friendUI;
            uiQuestBotany = botanyUI;
            sfxQuestComplete = jingleClip;

            RefreshAllUI();
        }

        public void RefreshAllUI()
        {
            UpdateQuestUI(questGait, uiQuestGait);
            UpdateQuestUI(questForestFriend, uiQuestForestFriend);
            UpdateQuestUI(questBotany, uiQuestBotany);
        }

        private void UpdateQuestUI(QuestData quest, QuestUIElements ui)
        {
            if (ui == null) return;

            // Título
            if (ui.txtTitle != null)
            {
                ui.txtTitle.text = quest.title;
                ui.txtTitle.color = quest.isCompleted ? colorCompleted : Color.white;
            }

            // Status e Contador: "8/15" ou "[✓] CONCLUIDA"
            if (ui.txtStatus != null)
            {
                if (quest.isCompleted)
                {
                    ui.txtStatus.text = "[v] CONCLUIDA";
                    ui.txtStatus.color = colorCompleted;
                }
                else
                {
                    ui.txtStatus.text = $"{quest.currentCount}/{quest.targetCount}";
                    ui.txtStatus.color = colorInProgress;
                }
            }

            // Barra de Progresso
            if (ui.imgFillBar != null)
            {
                ui.imgFillBar.fillAmount = quest.Progress;
                ui.imgFillBar.color = quest.isCompleted ? colorCompleted : colorInProgress;
            }

            // Indicador / Badge
            if (ui.imgCheckBadge != null)
            {
                ui.imgCheckBadge.color = quest.isCompleted ? colorCompleted : new Color(0.2f, 0.35f, 0.45f, 0.8f);
            }
        }
    }
}
