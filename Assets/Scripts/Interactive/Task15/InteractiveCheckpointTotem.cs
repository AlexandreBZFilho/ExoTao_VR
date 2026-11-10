using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;
using VRRehab.DronePet;
using VRRehab.Interactive.Task14;

namespace VRRehab.Interactive.Task15
{
    /// <summary>
    /// Totem de Checkpoint Interativo da Trilha na Floresta (TASK-15).
    /// Possui alavanca mecânica acionável via Raio Laser VR ou toque direto,
    /// baú animado no topo que abre suavemente revelando interior dourado,
    /// coluna de luz ascendente, confetes e recompensa de 100 pontos de sessão.
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public class InteractiveCheckpointTotem : MonoBehaviour
    {
        [Header("Hierarquia Mecânica do Modelo")]
        [SerializeField] private Transform leverPivot;
        [SerializeField] private Transform chestLidPivot;
        [SerializeField] private Renderer goldInteriorRenderer;

        [Header("Feedback Audiovisual")]
        [SerializeField] private Light beaconLight;
        [SerializeField] private ParticleSystem activateParticles;
        [SerializeField] private AudioSource audioSource;

        [Header("Clipes de Áudio")]
        [SerializeField] private AudioClip sfxLeverPull;
        [SerializeField] private AudioClip sfxChestCreak;
        [SerializeField] private AudioClip sfxCheckpointHum;

        [Header("Configuração de Animação")]
        [SerializeField] private float leverTravelAngle = 55.0f;
        [SerializeField] private float leverDuration = 0.45f;
        [SerializeField] private float chestOpenAngle = -80.0f;
        [SerializeField] private float chestDuration = 0.85f;

        [Header("Recompensa Clínica")]
        [SerializeField] private int bonusScoreValue = 100;

        // Estado
        [SerializeField] private bool isActivated = false;
        private Quaternion initialLeverRotation = Quaternion.identity;
        private Quaternion initialChestRotation = Quaternion.identity;
        private Coroutine activationRoutine;
        private XRSimpleInteractable simpleInteractable;

        public bool IsActivated => isActivated;
        public static event Action<InteractiveCheckpointTotem> OnCheckpointActivatedEvent;

        private void Awake()
        {
            LocateComponents();

            if (leverPivot != null)
                initialLeverRotation = leverPivot.localRotation;
            if (chestLidPivot != null)
                initialChestRotation = chestLidPivot.localRotation;

            if (beaconLight != null)
                beaconLight.intensity = 0f;
        }

        private void Start()
        {
            SetupXRInteraction();
        }

        public void LocateComponents()
        {
            if (leverPivot == null)
            {
                leverPivot = FindDeepChild(transform, "Totem_LeverPivot");
            }

            if (chestLidPivot == null)
            {
                chestLidPivot = FindDeepChild(transform, "Totem_ChestLidPivot");
            }

            if (goldInteriorRenderer == null)
            {
                Transform t = FindDeepChild(transform, "Totem_ChestInteriorGold");
                if (t != null) goldInteriorRenderer = t.GetComponent<Renderer>();
            }

            if (beaconLight == null)
            {
                beaconLight = GetComponentInChildren<Light>();
            }

            if (activateParticles == null)
            {
                activateParticles = GetComponentInChildren<ParticleSystem>();
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1.0f;
                audioSource.minDistance = 1.0f;
                audioSource.maxDistance = 25.0f;
                audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            }
        }

        public static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeepChild(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        public void Configure(Transform lever, Transform lid, Light beacon, ParticleSystem ps,
                              AudioClip leverClip, AudioClip creakClip, AudioClip humClip)
        {
            leverPivot = lever;
            chestLidPivot = lid;
            beaconLight = beacon;
            activateParticles = ps;
            sfxLeverPull = leverClip;
            sfxChestCreak = creakClip;
            sfxCheckpointHum = humClip;

            if (leverPivot != null) initialLeverRotation = leverPivot.localRotation;
            if (chestLidPivot != null) initialChestRotation = chestLidPivot.localRotation;
        }

        private void SetupXRInteraction()
        {
            simpleInteractable = GetComponent<XRSimpleInteractable>();
            if (simpleInteractable == null)
            {
                Transform targetForInteractable = leverPivot != null ? leverPivot : transform;
                simpleInteractable = targetForInteractable.GetComponent<XRSimpleInteractable>();
                if (simpleInteractable == null)
                {
                    simpleInteractable = targetForInteractable.gameObject.AddComponent<XRSimpleInteractable>();
                }
            }

            if (simpleInteractable != null)
            {
                simpleInteractable.selectEntered.AddListener(OnXRISelectEntered);
                simpleInteractable.hoverEntered.AddListener(OnXRIHoverEntered);
            }
        }

        private void OnDestroy()
        {
            if (simpleInteractable != null)
            {
                simpleInteractable.selectEntered.RemoveListener(OnXRISelectEntered);
                simpleInteractable.hoverEntered.RemoveListener(OnXRIHoverEntered);
            }
        }

        private void OnXRISelectEntered(SelectEnterEventArgs args)
        {
            if (!isActivated)
            {
                ActivateCheckpoint();
            }
        }

        private void OnXRIHoverEntered(HoverEnterEventArgs args)
        {
            if (!isActivated)
            {
                VRFeedbackManager.Instance?.TriggerHaptic(args.interactorObject, 0.25f, 0.08f);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isActivated) return;

            string objName = other.name.ToLower();
            if (objName.Contains("hand") || objName.Contains("controller") || other.CompareTag("Player"))
            {
                ActivateCheckpoint();
            }
        }

        /// <summary>
        /// Aciona o Totem de Checkpoint, abrindo a alavanca, destravando o baú e celebrando.
        /// </summary>
        [ContextMenu("Activate Checkpoint")]
        public void ActivateCheckpoint()
        {
            if (isActivated) return;
            isActivated = true;

            if (activationRoutine != null)
                StopCoroutine(activationRoutine);
            activationRoutine = StartCoroutine(CheckpointSequenceRoutine());

            // Dispara evento estático
            OnCheckpointActivatedEvent?.Invoke(this);

            // Recompensa de pontos no CoinScoreManager da floresta
            if (CoinScoreManager.Instance != null)
            {
                CoinScoreManager.Instance.RegisterCoinCollected(
                    CollectibleOrbCoin.CoinType.BonusDiamond, bonusScoreValue, 1);
            }

            // Notifica o Drone Pet para celebrar
            var droneMilestone = FindFirstObjectByType<DroneMilestoneCelebration>();
            if (droneMilestone != null)
            {
                droneMilestone.TriggerCelebration(CelebrationCategory.Geral, "Checkpoint 1 Salvo! Baú de Tesouro Desbloqueado!");
            }

            Debug.Log("[InteractiveCheckpointTotem] Checkpoint ativado com sucesso! Progresso da sessão salvo.");
        }

        private IEnumerator CheckpointSequenceRoutine()
        {
            // 1. Áudio da alavanca mecânica descendo
            if (audioSource != null && sfxLeverPull != null)
            {
                audioSource.pitch = UnityEngine.Random.Range(0.97f, 1.03f);
                audioSource.PlayOneShot(sfxLeverPull, 0.95f);
            }

            // Animação suave da alavanca descendo
            float elapsedLever = 0f;
            Quaternion targetLeverRot = initialLeverRotation * Quaternion.Euler(leverTravelAngle, 0f, 0f);

            while (elapsedLever < leverDuration)
            {
                elapsedLever += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedLever / leverDuration);
                float easeOut = Mathf.Sin(t * Mathf.PI * 0.5f);
                if (leverPivot != null)
                {
                    leverPivot.localRotation = Quaternion.Slerp(initialLeverRotation, targetLeverRot, easeOut);
                }
                yield return null;
            }

            yield return new WaitForSeconds(0.12f);

            // 2. Áudio do baú rangendo ao abrir
            if (audioSource != null && sfxChestCreak != null)
            {
                audioSource.pitch = UnityEngine.Random.Range(0.96f, 1.02f);
                audioSource.PlayOneShot(sfxChestCreak, 1.0f);
            }

            // 3. Animação da tampa do baú se abrindo
            float elapsedChest = 0f;
            Quaternion targetChestRot = initialChestRotation * Quaternion.Euler(chestOpenAngle, 0f, 0f);

            while (elapsedChest < chestDuration)
            {
                elapsedChest += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedChest / chestDuration);
                float smoothT = t * t * (3f - 2f * t);
                if (chestLidPivot != null)
                {
                    chestLidPivot.localRotation = Quaternion.Slerp(initialChestRotation, targetChestRot, smoothT);
                }

                if (beaconLight != null)
                {
                    beaconLight.intensity = Mathf.Lerp(0f, 2.5f, smoothT);
                }
                yield return null;
            }

            // 4. Áudio etéreo de marco atingido
            if (audioSource != null && sfxCheckpointHum != null)
            {
                audioSource.PlayOneShot(sfxCheckpointHum, 0.90f);
            }

            // 5. Ativa sistema de partículas / confetes cintilantes
            if (activateParticles != null)
            {
                activateParticles.Play();
            }

            // 6. Pulso contínuo de luz sutil no baú aberto
            float pulseTimer = 0f;
            while (true)
            {
                pulseTimer += Time.deltaTime * 2.5f;
                if (beaconLight != null)
                {
                    beaconLight.intensity = 2.2f + Mathf.Sin(pulseTimer) * 0.4f;
                }
                yield return null;
            }
        }

        /// <summary>
        /// Força o estado aberto imediato (para inspeção e captura de Visual QA).
        /// </summary>
        public void SetOpenImmediate()
        {
            isActivated = true;
            LocateComponents();

            if (leverPivot != null)
            {
                leverPivot.localRotation = Quaternion.Euler(leverTravelAngle, 0f, 0f);
            }
            if (chestLidPivot != null)
            {
                // Abertura de -80 graus na dobradiça para trás e para cima
                chestLidPivot.localRotation = Quaternion.Euler(-80f, 0f, 0f);
            }
            if (beaconLight != null)
            {
                beaconLight.intensity = 2.8f;
                beaconLight.enabled = true;
            }
            if (activateParticles != null)
            {
                activateParticles.Play();
            }
        }
    }
}
