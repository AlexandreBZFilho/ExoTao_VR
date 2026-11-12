using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace VRRehab.Core
{
    /// <summary>
    /// Gerenciador Central de Feedback Multissensorial (Áudio 3D Espacializado e Hápticos VR).
    /// Possui pool de 16 AudioSources 3D reciclados com variação de pitch orgânica,
    /// zero alocação de memória (GC Alloc = 0 em runtime), doppler zerado para VR
    /// e integração nativa com o XR Interaction Toolkit (XRBaseControllerInteractor).
    /// </summary>
    [DisallowMultipleComponent]
    public class VRFeedbackManager : MonoBehaviour
    {
        private static VRFeedbackManager _instance;

        public static VRFeedbackManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<VRFeedbackManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("[VRRehab] VRFeedbackManager");
                        _instance = go.AddComponent<VRFeedbackManager>();
                    }
                }
                return _instance;
            }
        }

        [Header("Pool de Áudio 3D (Calibrado para VR Confortável)")]
        [SerializeField] private int poolSize = 16;
        [SerializeField] private float minDistance = 0.5f;
        [SerializeField] private float maxDistance = 10.0f;
        [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Linear;

        [Header("Clipes Procedurais Core")]
        public AudioClip clipUIHover;
        public AudioClip clipGrabPop;
        public AudioClip clipReleaseWoosh;
        public AudioClip clipButtonClick;
        public AudioClip clipSocketSnap;
        public AudioClip clipSuccessChime;

        [Header("Parâmetros de Conforto VR")]
        [Range(0f, 1f)] public float masterSFXVolume = 0.85f;
        public Vector2 pitchVariationRange = new Vector2(0.96f, 1.04f);

        private readonly List<AudioSource> audioPool = new List<AudioSource>();
        private int currentPoolIndex = 0;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            InitializeAudioPool();
            AutoLoadClipsIfMissing();
        }

        private void OnValidate()
        {
            if (audioPool.Count == 0 && poolSize > 0)
            {
                InitializeAudioPool();
            }
        }

        /// <summary>
        /// Inicializa o pool de AudioSources 3D pré-alocados sem GC Alloc em runtime.
        /// </summary>
        private void InitializeAudioPool()
        {
            // Limpa filhos órfãos pré-existentes se houver
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith("AudioSource_Pool_"))
                {
                    if (Application.isPlaying)
                        Destroy(child.gameObject);
                    else
                        DestroyImmediate(child.gameObject);
                }
            }

            audioPool.Clear();
            for (int i = 0; i < poolSize; i++)
            {
                GameObject poolItem = new GameObject($"AudioSource_Pool_{i:D2}");
                poolItem.transform.SetParent(transform);
                poolItem.transform.localPosition = Vector3.zero;

                AudioSource src = poolItem.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1.0f; // Totalmente 3D espacializado
                src.minDistance = minDistance;
                src.maxDistance = maxDistance;
                src.rolloffMode = rolloffMode;
                src.dopplerLevel = 0.0f; // Zero distorção/artefatos de pitch em movimentos rápidos no VR

                audioPool.Add(src);
            }
        }

        /// <summary>
        /// Carrega os clipes padrão de Assets/Audio/Generated/Core se não estiverem atribuídos.
        /// </summary>
        public void AutoLoadClipsIfMissing()
        {
#if UNITY_EDITOR
            if (clipUIHover == null)
                clipUIHover = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Generated/Core/ui_hover_soft.wav");
            if (clipGrabPop == null)
                clipGrabPop = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Generated/Core/grab_pop.wav");
            if (clipReleaseWoosh == null)
                clipReleaseWoosh = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Generated/Core/release_woosh.wav");
            if (clipButtonClick == null)
                clipButtonClick = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Generated/Core/button_click_mech.wav");
            if (clipSocketSnap == null)
                clipSocketSnap = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Generated/Core/socket_snap.wav");
            if (clipSuccessChime == null)
                clipSuccessChime = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Generated/Core/success_chime.wav");
#endif
        }

        /// <summary>
        /// Toca um som 3D na posição especificada, utilizando o pool circular de AudioSources
        /// com variação orgânica de pitch e zero alocação de memória (GC Alloc = 0).
        /// </summary>
        public AudioSource PlaySound(AudioClip clip, Vector3 position, float volume = 1.0f, float minPitch = 0.95f, float maxPitch = 1.05f)
        {
            if (clip == null) return null;

            if (audioPool.Count == 0)
            {
                InitializeAudioPool();
            }

            // Busca próximo AudioSource livre ou reutiliza circularmente
            AudioSource selectedSource = null;
            for (int i = 0; i < audioPool.Count; i++)
            {
                int index = (currentPoolIndex + i) % audioPool.Count;
                if (!audioPool[index].isPlaying)
                {
                    selectedSource = audioPool[index];
                    currentPoolIndex = (index + 1) % audioPool.Count;
                    break;
                }
            }

            if (selectedSource == null)
            {
                selectedSource = audioPool[currentPoolIndex];
                currentPoolIndex = (currentPoolIndex + 1) % audioPool.Count;
            }

            selectedSource.transform.position = position;
            selectedSource.clip = clip;
            selectedSource.volume = volume * masterSFXVolume;
            selectedSource.pitch = Random.Range(minPitch, maxPitch);
            selectedSource.Play();

            return selectedSource;
        }

        #region Haptics XRI
        /// <summary>
        /// Envia um pulso háptico tátil para o controlador XR associado ao interactor.
        /// </summary>
        public void TriggerHaptic(IXRInteractor interactor, float amplitude = 0.35f, float duration = 0.08f)
        {
            if (interactor is XRBaseControllerInteractor controllerInteractor && controllerInteractor.xrController != null)
            {
                controllerInteractor.xrController.SendHapticImpulse(amplitude, duration);
            }
        }

        /// <summary>
        /// Dispara vibração háptica nos controles que estiverem interagindo com este interactable.
        /// </summary>
        public void TriggerHaptic(XRBaseInteractable interactable, float amplitude = 0.35f, float duration = 0.08f)
        {
            if (interactable == null) return;

            foreach (var interactor in interactable.interactorsSelecting)
            {
                TriggerHaptic(interactor, amplitude, duration);
            }

            if (interactable.interactorsSelecting.Count == 0)
            {
                foreach (var interactor in interactable.interactorsHovering)
                {
                    TriggerHaptic(interactor, amplitude * 0.4f, duration * 0.6f);
                }
            }
        }
        #endregion

        #region Métodos de Atalho para Microinterações (Volumes Calibrados para VR Confortável)
        /// <summary>
        /// Som de Hover suave (volume 0.20f) + micro-pulso háptico.
        /// </summary>
        public void PlayHover(Vector3 position, IXRInteractor interactor = null)
        {
            PlaySound(clipUIHover, position, 0.20f, 0.98f, 1.05f);
            if (interactor != null)
            {
                TriggerHaptic(interactor, 0.15f, 0.04f);
            }
        }

        /// <summary>
        /// Som de Agarre pop tátil (volume 0.35f) + pulso háptico moderado.
        /// </summary>
        public void PlayGrab(Vector3 position, IXRInteractor interactor = null)
        {
            PlaySound(clipGrabPop, position, 0.35f, 0.94f, 1.05f);
            if (interactor != null)
            {
                TriggerHaptic(interactor, 0.50f, 0.10f);
            }
        }

        /// <summary>
        /// Som de Soltura woosh suave (volume 0.25f).
        /// </summary>
        public void PlayRelease(Vector3 position)
        {
            PlaySound(clipReleaseWoosh, position, 0.25f, 0.95f, 1.05f);
        }

        /// <summary>
        /// Som mecânico de clique de botão (volume 0.35f) + pulso háptico nítido.
        /// </summary>
        public void PlayButtonClick(Vector3 position, IXRInteractor interactor = null)
        {
            PlaySound(clipButtonClick, position, 0.35f, 0.96f, 1.04f);
            if (interactor != null)
            {
                TriggerHaptic(interactor, 0.40f, 0.06f);
            }
        }

        /// <summary>
        /// Som de encaixe em soquete magnético (volume 0.40f) + pulso firme de confirmação.
        /// </summary>
        public void PlaySocketSnap(Vector3 position, IXRInteractor interactor = null)
        {
            PlaySound(clipSocketSnap, position, 0.40f, 0.98f, 1.03f);
            if (interactor != null)
            {
                TriggerHaptic(interactor, 0.65f, 0.12f);
            }
        }

        /// <summary>
        /// Som de sino de sucesso / meta clínica (volume 0.40f).
        /// </summary>
        public void PlaySuccess(Vector3 position)
        {
            PlaySound(clipSuccessChime, position, 0.40f, 1.0f, 1.02f);
        }
        #endregion
    }
}
