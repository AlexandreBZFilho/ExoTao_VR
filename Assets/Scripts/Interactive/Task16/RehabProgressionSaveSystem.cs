using System;
using System.IO;
using UnityEngine;

namespace VRRehab.Persistence
{
    /// <summary>
    /// Dados persistidos do progresso clínico e gamificação do paciente no VR Rehab.
    /// </summary>
    [Serializable]
    public class RehabSaveData
    {
        public int totalCoinsCollected = 150;
        public int totalStepsTaken = 520;
        public int totalBasketsScored = 16;
        public int totalApplesHarvested = 24;
        public int totalPineconesPlanted = 14;
        public int totalCheckpointsCleared = 8;

        public bool trophyGoldenAppleUnlocked = true;
        public bool trophyLuminousBonsaiUnlocked = true;
        public bool trophyStarCupUnlocked = true;
        public bool trophyDroneCrownUnlocked = true;

        public bool isDroneCrowned = false;
        public string lastSaveTimestamp = "";
        public string playerNotes = "VR Rehab - Protocolo Clínico IC";
    }

    /// <summary>
    /// Gerenciador central de persistência JSON em disco para o projeto VR Rehab.
    /// Salva dados em Application.persistentDataPath/vr_rehab_save.json.
    /// Suporta modo demonstração clínica imediata através de unlockAllForDemo.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class RehabProgressionSaveSystem : MonoBehaviour
    {
        public static RehabProgressionSaveSystem Instance { get; private set; }

        [Header("Modo Demonstração Clínica")]
        [Tooltip("Quando ativado, desbloqueia todos os troféus imediatamente para validação do terapeuta e testes visuais.")]
        public bool unlockAllForDemo = true;

        [Header("Configuração de Arquivo")]
        [Tooltip("Nome do arquivo JSON salvo em persistentDataPath")]
        public string saveFileName = "vr_rehab_save.json";

        [Header("Estado Atual (Somente Leitura)")]
        [SerializeField]
        private RehabSaveData currentData;
        public RehabSaveData Data => currentData;

        public event Action<RehabSaveData> OnSaveDataUpdated;
        public event Action<string> OnTrophyUnlocked;

        public const string TrophyIdGoldenApple = "GoldenApple";
        public const string TrophyIdLuminousBonsai = "LuminousBonsai";
        public const string TrophyIdStarCup = "StarCup";
        public const string TrophyIdDroneCrown = "DroneCrown";

        private string SaveFilePath => Path.Combine(Application.persistentDataPath, saveFileName);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            Load();
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                Save();
            }
        }

        /// <summary>
        /// Carrega os dados salvos em disco ou inicializa novos dados padrão.
        /// </summary>
        public void Load()
        {
            try
            {
                string path = SaveFilePath;
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    currentData = JsonUtility.FromJson<RehabSaveData>(json);
                    if (currentData == null) currentData = new RehabSaveData();
                    Debug.Log($"[RehabSaveSystem] Dados carregados com sucesso de: {path}");
                }
                else
                {
                    currentData = new RehabSaveData();
                    Debug.Log($"[RehabSaveSystem] Nenhum arquivo anterior em {path}. Criando novo save padrão.");
                    Save();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RehabSaveSystem] Falha ao ler save: {ex.Message}. Recriando dados padrão.");
                currentData = new RehabSaveData();
            }

            if (unlockAllForDemo)
            {
                currentData.trophyGoldenAppleUnlocked = true;
                currentData.trophyLuminousBonsaiUnlocked = true;
                currentData.trophyStarCupUnlocked = true;
                currentData.trophyDroneCrownUnlocked = true;
            }

            OnSaveDataUpdated?.Invoke(currentData);
        }

        /// <summary>
        /// Serializa e grava os dados atuais em disco JSON.
        /// </summary>
        public void Save()
        {
            if (currentData == null) currentData = new RehabSaveData();

            try
            {
                currentData.lastSaveTimestamp = DateTime.UtcNow.ToString("o");
                string json = JsonUtility.ToJson(currentData, true);
                string path = SaveFilePath;

                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(path, json);
                Debug.Log($"[RehabSaveSystem] Save persistido ({json.Length} bytes) em: {path}");
                OnSaveDataUpdated?.Invoke(currentData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RehabSaveSystem] Erro ao gravar save em disco: {ex.Message}");
            }
        }

        /// <summary>
        /// Reseta o save aos valores iniciais.
        /// </summary>
        public void ResetSave()
        {
            currentData = new RehabSaveData();
            if (!unlockAllForDemo)
            {
                currentData.trophyGoldenAppleUnlocked = false;
                currentData.trophyLuminousBonsaiUnlocked = false;
                currentData.trophyStarCupUnlocked = false;
                currentData.trophyDroneCrownUnlocked = false;
                currentData.isDroneCrowned = false;
            }
            Save();
        }

        #region Mutadores de Progresso Clínico

        public void AddCoins(int amount)
        {
            if (amount <= 0) return;
            currentData.totalCoinsCollected += amount;
            CheckTrophyUnlocks();
            Save();
        }

        public void AddSteps(int amount)
        {
            if (amount <= 0) return;
            currentData.totalStepsTaken += amount;
            CheckTrophyUnlocks();
            Save();
        }

        public void AddBasket()
        {
            currentData.totalBasketsScored++;
            CheckTrophyUnlocks();
            Save();
        }

        public void AddApple()
        {
            currentData.totalApplesHarvested++;
            CheckTrophyUnlocks();
            Save();
        }

        public void AddPinecone()
        {
            currentData.totalPineconesPlanted++;
            CheckTrophyUnlocks();
            Save();
        }

        public void AddCheckpoint()
        {
            currentData.totalCheckpointsCleared++;
            CheckTrophyUnlocks();
            Save();
        }

        public void SetDroneCrowned(bool crowned)
        {
            if (currentData.isDroneCrowned != crowned)
            {
                currentData.isDroneCrowned = crowned;
                Save();
            }
        }

        public bool IsTrophyUnlocked(string trophyId)
        {
            if (unlockAllForDemo) return true;
            if (currentData == null) return false;

            return trophyId switch
            {
                TrophyIdGoldenApple => currentData.trophyGoldenAppleUnlocked,
                TrophyIdLuminousBonsai => currentData.trophyLuminousBonsaiUnlocked,
                TrophyIdStarCup => currentData.trophyStarCupUnlocked,
                TrophyIdDroneCrown => currentData.trophyDroneCrownUnlocked,
                _ => false
            };
        }

        public void UnlockTrophy(string trophyId)
        {
            if (currentData == null) currentData = new RehabSaveData();

            bool justUnlocked = false;
            switch (trophyId)
            {
                case TrophyIdGoldenApple:
                    if (!currentData.trophyGoldenAppleUnlocked) { currentData.trophyGoldenAppleUnlocked = true; justUnlocked = true; }
                    break;
                case TrophyIdLuminousBonsai:
                    if (!currentData.trophyLuminousBonsaiUnlocked) { currentData.trophyLuminousBonsaiUnlocked = true; justUnlocked = true; }
                    break;
                case TrophyIdStarCup:
                    if (!currentData.trophyStarCupUnlocked) { currentData.trophyStarCupUnlocked = true; justUnlocked = true; }
                    break;
                case TrophyIdDroneCrown:
                    if (!currentData.trophyDroneCrownUnlocked) { currentData.trophyDroneCrownUnlocked = true; justUnlocked = true; }
                    break;
            }

            if (justUnlocked)
            {
                Debug.Log($"[RehabSaveSystem] NOVO TROFEU DESBLOQUEADO: {trophyId}!");
                OnTrophyUnlocked?.Invoke(trophyId);
                Save();
            }
        }

        private void CheckTrophyUnlocks()
        {
            if (currentData.totalApplesHarvested >= 5) UnlockTrophy(TrophyIdGoldenApple);
            if (currentData.totalPineconesPlanted >= 4) UnlockTrophy(TrophyIdLuminousBonsai);
            if (currentData.totalBasketsScored >= 5 || currentData.totalCoinsCollected >= 80) UnlockTrophy(TrophyIdStarCup);
            if (currentData.totalCheckpointsCleared >= 2) UnlockTrophy(TrophyIdDroneCrown);
        }

        #endregion
    }
}
