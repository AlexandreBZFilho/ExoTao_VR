using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace VRRehab.Core
{
    /// <summary>
    /// Gerenciador Central de Feedback Multissensorial (Áudio 3D Espacializado e Hápticos VR).
    /// Possui pool de 16 AudioSources 3D reciclados com variação de pitch orgânica
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

        [Header("Pool de Áudio 3D")]
        [SerializeField] private int poolSize = 16;
        [SerializeField] private float minDistance = 0.5f;
        [SerializeField] private float maxDistance = 20.0f;
        [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Linear;

        [Header("Clipes Procedurais Core")]
        public AudioClip clipUIHover;
        public AudioClip clipGrabPop;
        public AudioClip clipReleaseWoosh;
        public AudioClip clipButtonClick;
        public AudioClip clipSocketSnap;
        public AudioClip clipSuccessChime;

        [Header("Parâmetros Padrão")]
        [Range(0f, 1f)] public float masterSFXVolume = 0.85f;
        public Vector2 pitchVariationRange = new Vector2(0.94f, 1.06f);

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
        /// Inicializa o pool de AudioSources 3D.
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
                src.spatialBlend = 1.0f; // Totalmente 3D
                src.minDistance = minDistance;
                src.maxDistance = maxDistance;
                src.rolloffMode = rolloffMode;
                src.dopplerLevel = 0.0f; // Evita artefatos de pitch em movimentos rápidos de VR

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
        /// Toca um som 3D na posição especificada, utilizando o pool de AudioSources com variação de pitch.
        /// </summary>
        public AudioSource PlaySound(AudioClip clip, Vector3 position, float volume = 1.0f, float minPitch = 0.94f, float maxPitch = 1.06f)
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
        public void TriggerHaptic(IXRInteractor interactor, float amplitude = 0.4f, float duration = 0.08f)
        {
            if (interactor is XRBaseControllerInteractor controllerInteractor && controllerInteractor.xrController != null)
            {
                controllerInteractor.xrController.SendHapticImpulse(amplitude, duration);
            }
        }

        /// <summary>
        /// Dispara vibração háptica nos controles que estiverem interagindo com este interactable.
        /// </summary>
        public void TriggerHaptic(XRBaseInteractable interactable, float amplitude = 0.4f, float duration = 0.08f)
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
                    TriggerHaptic(interactor, amplitude * 0.5f, duration * 0.7f);
                }
            }
        }
        #endregion

        #region Métodos de Atalho para Microinterações
        public void PlayHover(Vector3 position, IXRInteractor interactor = null)
        {
            PlaySound(clipUIHover, position, 0.6f, 0.98f, 1.05f);
            if (interactor != null)
            {
                TriggerHaptic(interactor, 0.15f, 0.04f);
            }
        }

        public void PlayGrab(Vector3 position, IXRInteractor interactor = null)
        {
            PlaySound(clipGrabPop, position, 0.9f, 0.92f, 1.05f);
            if (interactor != null)
            {
                TriggerHaptic(interactor, 0.55f, 0.12f);
            }
        }

        public void PlayRelease(Vector3 position)
        {
            PlaySound(clipReleaseWoosh, position, 0.7f, 0.95f, 1.05f);
        }

        public void PlayButtonClick(Vector3 position, IXRInteractor interactor = null)
        {
            PlaySound(clipButtonClick, position, 0.85f, 0.96f, 1.04f);
            if (interactor != null)
            {
                TriggerHaptic(interactor, 0.4f, 0.06f);
            }
        }

        public void PlaySocketSnap(Vector3 position, IXRInteractor interactor = null)
        {
            PlaySound(clipSocketSnap, position, 0.95f, 0.98f, 1.03f);
            if (interactor != null)
            {
                TriggerHaptic(interactor, 0.7f, 0.15f);
            }
        }

        public void PlaySuccess(Vector3 position)
        {
            PlaySound(clipSuccessChime, position, 1.0f, 1.0f, 1.02f);
        }
        #endregion
    }
}
