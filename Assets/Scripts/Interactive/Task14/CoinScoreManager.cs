using System;
using UnityEngine;
using VRRehab.Core;
using VRRehab.DronePet;

namespace VRRehab.Interactive.Task14
{
    /// <summary>
    /// Gerenciador de Pontuação, Telemetria Clínica e Progresso da Trilha de Moedas na Floresta.
    /// Acompanha o total de moedas coletadas, streak de combo e comunica marcos clínicos ao Drone Pet.
    /// </summary>
    [DisallowMultipleComponent]
    public class CoinScoreManager : MonoBehaviour
    {
        public static CoinScoreManager Instance { get; private set; }

        [Header("Telemetria da Trilha")]
        [SerializeField] private int totalCoinsInLevel = 0;
        [SerializeField] private int coinsCollected = 0;
        [SerializeField] private int bonusCoinsCollected = 0;
        [SerializeField] private int currentScore = 0;
        [SerializeField] private int currentComboStreak = 0;
        [SerializeField] private int highestComboStreak = 0;

        [Header("Feedback de Marcos")]
        [SerializeField] private bool milestone50PercentTriggered = false;
        [SerializeField] private bool milestone100PercentTriggered = false;

        public static event Action<int, int, int> OnCoinCollectedEvent; // scoreDelta, totalScore, comboStreak
        public static event Action<float> OnProgressChangedEvent;       // progress 0.0f a 1.0f

        public int TotalCoinsInLevel => totalCoinsInLevel;
        public int CoinsCollected => coinsCollected;
        public int BonusCoinsCollected => bonusCoinsCollected;
        public int CurrentScore => currentScore;
        public int CurrentComboStreak => currentComboStreak;
        public int HighestComboStreak => highestComboStreak;
        public float CompletionProgress => totalCoinsInLevel > 0 ? (float)coinsCollected / totalCoinsInLevel : 0f;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
            }
        }

        private void Start()
        {
            // Auto-detecta total de moedas se não estiver fixado
            if (totalCoinsInLevel == 0)
            {
                CollectibleOrbCoin[] coins = FindObjectsByType<CollectibleOrbCoin>(FindObjectsSortMode.None);
                totalCoinsInLevel = coins.Length;
            }
        }

        public void RegisterCoinSpawned()
        {
            totalCoinsInLevel++;
        }

        public void SetTotalCoins(int count)
        {
            totalCoinsInLevel = count;
        }

        public void RegisterCoinCollected(CollectibleOrbCoin.CoinType type, int scoreDelta, int combo)
        {
            coinsCollected++;
            if (type == CollectibleOrbCoin.CoinType.BonusDiamond)
            {
                bonusCoinsCollected++;
            }

            currentScore += scoreDelta;
            currentComboStreak = combo;
            if (combo > highestComboStreak)
            {
                highestComboStreak = combo;
            }

            OnCoinCollectedEvent?.Invoke(scoreDelta, currentScore, currentComboStreak);
            OnProgressChangedEvent?.Invoke(CompletionProgress);

            CheckMilestones();
        }

        private void CheckMilestones()
        {
            float progress = CompletionProgress;

            if (!milestone50PercentTriggered && progress >= 0.5f)
            {
                milestone50PercentTriggered = true;
                Debug.Log($"[CoinScoreManager] Marco clínico atingido: 50% das moedas coletadas! Score: {currentScore}");
                NotifyDroneCelebration("Excelente ritmo de marcha! Metade do caminho concluído!");
            }

            if (!milestone100PercentTriggered && progress >= 1.0f)
            {
                milestone100PercentTriggered = true;
                Debug.Log($"[CoinScoreManager] Trilha 100% concluída! Score final: {currentScore}, Maior Combo: x{highestComboStreak}");
                NotifyDroneCelebration("Sensacional! Trilha completa com maestria!");
            }
        }

        private void NotifyDroneCelebration(string customMessage)
        {
            // Se o DroneMilestoneCelebration existir na cena (Task 05), dispara comemoração
            var droneMilestone = FindFirstObjectByType<DroneMilestoneCelebration>();
            if (droneMilestone != null)
            {
                droneMilestone.TriggerCelebration(CelebrationCategory.Geral, customMessage);
            }
        }

        public void ResetSession()
        {
            coinsCollected = 0;
            bonusCoinsCollected = 0;
            currentScore = 0;
            currentComboStreak = 0;
            milestone50PercentTriggered = false;
            milestone100PercentTriggered = false;
        }
    }
}
