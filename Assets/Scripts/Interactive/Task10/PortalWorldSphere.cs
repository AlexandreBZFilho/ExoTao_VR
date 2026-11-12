using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;
using VRRehab.Ergonomics;

namespace VRRehab.Interactive.Task10
{
    /// <summary>
    /// Esfera de Mini-Diorama 3D Interativa para o Menu Principal (TASK-10).
    /// Herda de ErgonomicGrabInteractable para Force Grab assistido e Auto-Respawn.
    /// HOTFIX PLAYTEST #1: Funciona PURAMENTE como uma peça de diorama 3D decorativa e tátil.
    /// O paciente pode pegar, puxar com o laser, inspecionar a mini-floresta girando dentro e
    /// encaixar no pedestal tecnológico para emitir iluminação suave e partículas.
    /// TERMINANTEMENTE PROIBIDO qualquer carregamento de cena (SceneManager.LoadScene) ou
    /// teleporte automático por proximidade do headset. A transição de cena é responsabilidade
    /// exclusiva do Canvas 2D da clínica (Canvas Menu Principal).
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public class PortalWorldSphere : ErgonomicGrabInteractable
    {
        [Header("Configuração de Exibição")]
        [Tooltip("Identificador do mapa representado neste diorama (apenas informativo para a UI).")]
        [SerializeField] private string targetSceneName = "Floresta_e_Rio";

        [Tooltip("Tempo de exibição / celebração visual.")]
        [SerializeField] private float transitionDelay = 0.8f;

        [Header("Diorama Interno")]
        [Tooltip("Transform do diorama interno para rotação independente da redoma externa.")]
        [SerializeField] private Transform innerDioramaTransform;

        [Tooltip("Velocidade de rotação contínua no eixo Y (graus por segundo).")]
        [SerializeField] private float dioramaRotationSpeed = 20.0f;

        [Header("Parâmetros Suaves de Interação")]
        [SerializeField] private float proximityNoticeDistance = 0.50f;
        [SerializeField] private float proximityTriggerDistance = 0.22f;

        [Header("Áudio Espacializado Suave")]
        [Tooltip("Fonte de áudio em loop para o zumbido místico sutil do diorama.")]
        [SerializeField] private AudioSource ambientAudioSource;

        [Tooltip("Fonte de áudio opcional de ressonância.")]
        [SerializeField] private AudioSource proximityAudioSource;

        [Tooltip("Efeito sonoro suave de ressonância.")]
        [SerializeField] private AudioClip sfxTeleportWhoosh;

        [Header("Estado do Diorama")]
        [SerializeField] private bool isSocketed = false;

        public string TargetSceneName => targetSceneName;
        public bool IsSocketed => isSocketed;
        public bool IsTeleporting => false; // Sempre falso (zero teleporte automático)

        protected override void Awake()
        {
            base.Awake();

            if (innerDioramaTransform == null)
            {
                innerDioramaTransform = transform.Find("Orb_Inner_Diorama") ??
                                        transform.Find("Visual_Model/Orb_Inner_Diorama") ??
                                        transform.Find("Visual_Model/PortalOrbDiorama_Forest/Orb_Inner_Diorama");
            }

            EnsureAudioSources();
        }

        private void Start()
        {
            if (ambientAudioSource != null && !ambientAudioSource.isPlaying)
            {
                ambientAudioSource.Play();
            }
        }

        private void Update()
        {
            // Rotação contínua suave e relaxante do mini-diorama interno no eixo Y
            if (innerDioramaTransform != null)
            {
                innerDioramaTransform.Rotate(Vector3.up, dioramaRotationSpeed * Time.deltaTime, Space.Self);
            }
        }

        /// <summary>
        /// Mantido para compatibilidade com chamadas existentes, mas com TELEPORTE REMOVIDO.
        /// A navegação ocorre estritamente via Canvas 2D original do projeto.
        /// </summary>
        public void InitiateTeleportSequence(string sourceTrigger)
        {
            // HOTFIX #1: Zero teleporte ou carregamento de cena.
            Debug.Log($"[TASK-10 Diorama] Esfera interagida via '{sourceTrigger}'. Teleporte desativado para garantir controle clínico pelo Canvas 2D.");
            
            if (sfxTeleportWhoosh != null && ambientAudioSource != null)
            {
                ambientAudioSource.PlayOneShot(sfxTeleportWhoosh, 0.35f);
            }
        }

        public void SetSocketedState(bool socketed)
        {
            isSocketed = socketed;
            if (isSocketed && proximityAudioSource != null)
            {
                proximityAudioSource.volume = 0f;
            }
        }

        private void EnsureAudioSources()
        {
            if (ambientAudioSource == null)
            {
                AudioSource[] srcs = GetComponents<AudioSource>();
                if (srcs.Length > 0) ambientAudioSource = srcs[0];
                if (srcs.Length > 1) proximityAudioSource = srcs[1];

                if (ambientAudioSource == null)
                {
                    ambientAudioSource = gameObject.AddComponent<AudioSource>();
                }
                if (proximityAudioSource == null)
                {
                    proximityAudioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            // Calibração de conforto VR: sem estridência, doppler zerado, volume suave
            ambientAudioSource.spatialBlend = 1.0f;
            ambientAudioSource.loop = true;
            ambientAudioSource.playOnAwake = false;
            ambientAudioSource.volume = 0.15f;
            ambientAudioSource.minDistance = 0.3f;
            ambientAudioSource.maxDistance = 3.0f;
            ambientAudioSource.dopplerLevel = 0.0f;

            proximityAudioSource.spatialBlend = 1.0f;
            proximityAudioSource.loop = false;
            proximityAudioSource.playOnAwake = false;
            proximityAudioSource.volume = 0.0f;
            proximityAudioSource.minDistance = 0.2f;
            proximityAudioSource.maxDistance = 2.0f;
            proximityAudioSource.dopplerLevel = 0.0f;
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            SetSocketedState(false);
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            if (proximityAudioSource != null)
            {
                proximityAudioSource.volume = 0f;
            }
        }
    }
}
