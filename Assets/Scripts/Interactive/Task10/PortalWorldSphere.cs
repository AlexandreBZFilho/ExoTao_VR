using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;
using VRRehab.Ergonomics;

namespace VRRehab.Interactive.Task10
{
    /// <summary>
    /// Esfera de Portal Interativa para Seleção de Mapas 3D no VR (TASK-10).
    /// Herda de ErgonomicGrabInteractable para Force Grab assistido e Auto-Respawn.
    /// Possui rotação suave contínua do mini-diorama interno e suporte a ativação dupla:
    /// 1. Por encaixe no pedestal magnético (PortalPedestalSocket);
    /// 2. Por aproximação do visor VR (Headset Proximity Teleport, estilo The Lab).
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public class PortalWorldSphere : ErgonomicGrabInteractable
    {
        [Header("Configuração de Destino")]
        [Tooltip("Nome da cena Unity de destino para este portal.")]
        [SerializeField] private string targetSceneName = "Floresta_e_Rio";

        [Tooltip("Tempo de espera de celebração antes de iniciar o carregamento (segundos).")]
        [SerializeField] private float transitionDelay = 0.8f;

        [Header("Diorama Interno")]
        [Tooltip("Transform do diorama interno para rotação independente da redoma externa.")]
        [SerializeField] private Transform innerDioramaTransform;

        [Tooltip("Velocidade de rotação contínua no eixo Y (graus por segundo).")]
        [SerializeField] private float dioramaRotationSpeed = 20.0f;

        [Header("Detecção de Proximidade com o Headset (VR HMD)")]
        [Tooltip("Câmera do jogador (Headset). Se nula, utiliza Camera.main.")]
        [SerializeField] private Camera headsetCamera;

        [Tooltip("Distância máxima a partir da qual o áudio de proximidade começa a subir (metros).")]
        [SerializeField] private float proximityNoticeDistance = 0.50f;

        [Tooltip("Distância mínima para acionar o teletransporte ao encostar a esfera no rosto/visor (metros).")]
        [SerializeField] private float proximityTriggerDistance = 0.22f;

        [Header("Áudio Espacializado")]
        [Tooltip("Fonte de áudio em loop para o zumbido místico ambiente da esfera.")]
        [SerializeField] private AudioSource ambientAudioSource;

        [Tooltip("Fonte de áudio para o efeito sonoro de aproximação do headset.")]
        [SerializeField] private AudioSource proximityAudioSource;

        [Tooltip("Som de transição dimensional ao acionar o teletransporte.")]
        [SerializeField] private AudioClip sfxTeleportWhoosh;

        [Header("Estado do Portal")]
        [SerializeField] private bool isSocketed = false;

        private bool isTeleporting = false;
        private Coroutine teleportCoroutine;

        public string TargetSceneName => targetSceneName;
        public bool IsSocketed => isSocketed;
        public bool IsTeleporting => isTeleporting;

        protected override void Awake()
        {
            base.Awake();

            if (innerDioramaTransform == null)
            {
                innerDioramaTransform = transform.Find("Orb_Inner_Diorama") ??
                                        transform.Find("Visual_Model/Orb_Inner_Diorama") ??
                                        transform.Find("Visual_Model/PortalOrbDiorama_Forest/Orb_Inner_Diorama");
            }

            if (headsetCamera == null)
            {
                headsetCamera = Camera.main;
            }

            EnsureAudioSources();
        }

        private void Start()
        {
            if (ambientAudioSource != null && !ambientAudioSource.isPlaying)
            {
                ambientAudioSource.Play();
            }

            if (proximityAudioSource != null)
            {
                proximityAudioSource.volume = 0f;
                if (!proximityAudioSource.isPlaying)
                {
                    proximityAudioSource.Play();
                }
            }
        }

        private void Update()
        {
            // 1. Rotação contínua suave do diorama interno no eixo Y
            if (innerDioramaTransform != null)
            {
                innerDioramaTransform.Rotate(Vector3.up, dioramaRotationSpeed * Time.deltaTime, Space.Self);
            }

            // 2. Detecção dinâmica de aproximação do headset quando o paciente estiver segurando a esfera
            if (!isTeleporting && !isSocketed && isSelected)
            {
                CheckHeadsetProximity();
            }
            else
            {
                // Silencia áudio de proximidade se não estiver na mão do paciente
                if (proximityAudioSource != null && proximityAudioSource.volume > 0.001f)
                {
                    proximityAudioSource.volume = Mathf.MoveTowards(proximityAudioSource.volume, 0f, Time.deltaTime * 3.0f);
                }
            }
        }

        /// <summary>
        /// Monitora a distância entre a esfera na mão do jogador e o visor VR (HMD).
        /// </summary>
        private void CheckHeadsetProximity()
        {
            if (headsetCamera == null)
            {
                headsetCamera = Camera.main;
                if (headsetCamera == null) return;
            }

            Vector3 headPos = headsetCamera.transform.position;
            float dist = Vector3.Distance(transform.position, headPos);

            if (dist < proximityNoticeDistance)
            {
                // Gradiente de volume proporcional à proximidade (0 no limiar distante, 1 no rosto)
                float t = Mathf.Clamp01(1.0f - ((dist - proximityTriggerDistance) / (proximityNoticeDistance - proximityTriggerDistance)));
                if (proximityAudioSource != null)
                {
                    proximityAudioSource.volume = Mathf.Lerp(0.05f, 0.95f, t);
                }

                // Disparo da transição estilo The Lab ao aproximar a menos de 0.22m
                if (dist <= proximityTriggerDistance)
                {
                    InitiateTeleportSequence("Headset Proximity");
                }
            }
            else
            {
                if (proximityAudioSource != null)
                {
                    proximityAudioSource.volume = Mathf.MoveTowards(proximityAudioSource.volume, 0f, Time.deltaTime * 2.5f);
                }
            }
        }

        /// <summary>
        /// Dispara o teletransporte com transição audiovisual suave e carregamento da cena de destino.
        /// </summary>
        public void InitiateTeleportSequence(string sourceTrigger)
        {
            if (isTeleporting) return;
            isTeleporting = true;

            Debug.Log($"[TASK-10] Ativando teletransporte para '{targetSceneName}' via {sourceTrigger}!");

            if (teleportCoroutine != null)
            {
                StopCoroutine(teleportCoroutine);
            }
            teleportCoroutine = StartCoroutine(TeleportRoutine());
        }

        private IEnumerator TeleportRoutine()
        {
            // Toca som de woosh dimensional
            if (sfxTeleportWhoosh != null)
            {
                if (ambientAudioSource != null)
                {
                    ambientAudioSource.PlayOneShot(sfxTeleportWhoosh, 1.0f);
                }
                else
                {
                    VRFeedbackManager.Instance.PlaySound(sfxTeleportWhoosh, transform.position, 1.0f);
                }
            }

            // Haptics nos controles que estiverem segurando
            if (firstInteractorSelecting != null)
            {
                VRFeedbackManager.Instance.TriggerHaptic(firstInteractorSelecting, 0.8f, 0.4f);
            }

            // Aguarda fração de tempo para o efeito sonoro expandir
            yield return new WaitForSeconds(transitionDelay);

            // Carrega cena via CenaDeLoading ou SceneManager
            ExecuteSceneTransition();
        }

        private void ExecuteSceneTransition()
        {
            // Salva na variável global do projeto
            ControladorInterface.mapaFuturo = targetSceneName;

            // Se a cena CenaDeLoading existir nas Build Settings, utiliza o fluxo canônico do projeto
            bool hasLoadingScene = Application.CanStreamedLevelBeLoaded("CenaDeLoading");
            if (hasLoadingScene)
            {
                SceneManager.LoadScene("CenaDeLoading");
            }
            else
            {
                SceneManager.LoadScene(targetSceneName);
            }
        }

        public void SetSocketedState(bool socketed)
        {
            isSocketed = socketed;
            if (isSocketed)
            {
                // Silencia som de headset se encaixado
                if (proximityAudioSource != null)
                {
                    proximityAudioSource.volume = 0f;
                }
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

            ambientAudioSource.spatialBlend = 1.0f;
            ambientAudioSource.loop = true;
            ambientAudioSource.playOnAwake = false;
            ambientAudioSource.volume = 0.45f;
            ambientAudioSource.minDistance = 0.5f;
            ambientAudioSource.maxDistance = 5.0f;

            proximityAudioSource.spatialBlend = 1.0f;
            proximityAudioSource.loop = true;
            proximityAudioSource.playOnAwake = false;
            proximityAudioSource.volume = 0.0f;
            proximityAudioSource.minDistance = 0.2f;
            proximityAudioSource.maxDistance = 3.0f;
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            // Ao ser pega na mão, não está mais encaixada
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
