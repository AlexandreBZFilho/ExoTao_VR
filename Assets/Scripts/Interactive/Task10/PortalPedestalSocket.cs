using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;

namespace VRRehab.Interactive.Task10
{
    /// <summary>
    /// Pedestal Tecnológico com Berço Magnético (XRSocketInteractor) para a Esfera de Portal (TASK-10).
    /// Detecta o encaixe da PortalWorldSphere, aciona estalo magnético com haptics,
    /// projeta feixe vertical de partículas luminosas ascendentes e inicia a transição dimensional para o mapa.
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public class PortalPedestalSocket : XRSocketInteractor
    {
        [Header("Ponto de Encaixe")]
        [Tooltip("Transform de ancoragem para o encaixe perfeito da esfera no berço côncavo.")]
        [SerializeField] private Transform socketAttachPoint;

        [Header("Iluminação & Anel Emissivo")]
        [Tooltip("Renderer do anel emissivo no topo do pedestal.")]
        [SerializeField] private Renderer emissiveRingRenderer;

        [Tooltip("Luz pontual centralizada no berço do pedestal.")]
        [SerializeField] private Light socketGlowLight;

        [Tooltip("Cor do anel em estado de espera (standby).")]
        [SerializeField] private Color standbyColor = new Color(0.0f, 0.70f, 0.95f);

        [Tooltip("Cor do anel no momento do encaixe ativo e celebração.")]
        [SerializeField] private Color activeFlareColor = new Color(0.20f, 0.95f, 1.0f);

        [Tooltip("Intensidade emissiva normal em standby.")]
        [SerializeField] private float standbyEmissionIntensity = 1.8f;

        [Tooltip("Intensidade emissiva máxima no encaixe/ativação.")]
        [SerializeField] private float activeEmissionIntensity = 4.5f;

        [Header("Efeitos Visuais de Partículas")]
        [Tooltip("Sistema de partículas do feixe vertical de luz ascendente.")]
        [SerializeField] private ParticleSystem verticalBeamParticles;

        [Header("Áudio")]
        [Tooltip("Fonte de áudio local do pedestal.")]
        [SerializeField] private AudioSource socketAudioSource;

        [Tooltip("Som de encaixe magnético ressonante da esfera.")]
        [SerializeField] private AudioClip sfxSocketSnap;

        [Tooltip("Tempo de celebração visual com feixe de luz antes de carregar o mapa (segundos).")]
        [SerializeField] private float celebrationDuration = 1.2f;

        private MaterialPropertyBlock propBlock;
        private Coroutine celebrationCoroutine;
        private PortalWorldSphere currentOrb;

        protected override void Awake()
        {
            base.Awake();

            // Configuração do XRI Socket
            showInteractableHoverMeshes = false;
            recycleDelayTime = 0.5f;

            if (socketAttachPoint != null)
            {
                attachTransform = socketAttachPoint;
            }

            if (socketAudioSource == null)
            {
                socketAudioSource = GetComponent<AudioSource>();
                if (socketAudioSource == null)
                {
                    socketAudioSource = gameObject.AddComponent<AudioSource>();
                }
            }
            socketAudioSource.spatialBlend = 1.0f;
            socketAudioSource.playOnAwake = false;

            propBlock = new MaterialPropertyBlock();

            // Estado inicial de iluminação
            ApplyRingVisuals(standbyColor, standbyEmissionIntensity, 0.6f);

            if (verticalBeamParticles != null && verticalBeamParticles.isPlaying)
            {
                verticalBeamParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public void ApplyRingVisuals(Color color, float emissionIntensity, float lightIntensity)
        {
            if (emissiveRingRenderer != null)
            {
                if (propBlock == null) propBlock = new MaterialPropertyBlock();
                emissiveRingRenderer.GetPropertyBlock(propBlock);

                Color finalEmission = color * Mathf.LinearToGammaSpace(emissionIntensity);
                propBlock.SetColor("_EmissionColor", finalEmission);
                propBlock.SetColor("_BaseColor", color);
                propBlock.SetColor("_Color", color);

                emissiveRingRenderer.SetPropertyBlock(propBlock);
            }

            if (socketGlowLight != null)
            {
                socketGlowLight.color = color;
                socketGlowLight.intensity = lightIntensity;
                socketGlowLight.enabled = lightIntensity > 0.05f;
            }
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);

            if (args.interactableObject is PortalWorldSphere orb)
            {
                currentOrb = orb;
                orb.SetSocketedState(true);

                // Feedback sonoro de encaixe magnético
                if (sfxSocketSnap != null && socketAudioSource != null)
                {
                    socketAudioSource.PlayOneShot(sfxSocketSnap, 1.0f);
                }

                // Haptics no interactor que soltou a esfera no socket
                if (args.interactorObject != null)
                {
                    VRFeedbackManager.Instance.TriggerHaptic(args.interactorObject, 0.7f, 0.25f);
                }

                // Dispara celebração e transição do portal
                if (celebrationCoroutine != null)
                {
                    StopCoroutine(celebrationCoroutine);
                }
                celebrationCoroutine = StartCoroutine(SocketCelebrationRoutine(orb));
            }
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);

            if (args.interactableObject is PortalWorldSphere orb)
            {
                if (currentOrb == orb)
                {
                    currentOrb = null;
                }
                orb.SetSocketedState(false);
            }

            // Cancela celebração se o jogador retirar a esfera antes da transição
            if (celebrationCoroutine != null)
            {
                StopCoroutine(celebrationCoroutine);
                celebrationCoroutine = null;
            }

            // Retorna ao estado de standby
            ApplyRingVisuals(standbyColor, standbyEmissionIntensity, 0.6f);

            if (verticalBeamParticles != null)
            {
                verticalBeamParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private IEnumerator SocketCelebrationRoutine(PortalWorldSphere orb)
        {
            Debug.Log("[TASK-10] Esfera encaixada no pedestal! Ativando feixe de portal e celebração...");

            // 1. Ativa partículas do feixe de luz ascendente
            if (verticalBeamParticles != null)
            {
                verticalBeamParticles.Play(true);
            }

            // 2. Pulso dinâmico de brilho emissivo
            float elapsed = 0f;
            while (elapsed < celebrationDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / celebrationDuration);

                // Pulso senoidal suave com pico
                float pulse = 0.5f + 0.5f * Mathf.Sin(progress * Mathf.PI * 3f);
                float currentIntensity = Mathf.Lerp(standbyEmissionIntensity, activeEmissionIntensity, pulse);
                float currentLight = Mathf.Lerp(0.8f, 2.5f, pulse);

                ApplyRingVisuals(activeFlareColor, currentIntensity, currentLight);
                yield return null;
            }

            // 3. Conclui acionando a rotina de teletransporte da esfera
            if (orb != null)
            {
                orb.InitiateTeleportSequence("Pedestal Socket Celebration");
            }
        }
    }
}
