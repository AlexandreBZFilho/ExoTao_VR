using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;
using VRRehab.Ergonomics;
using VRRehab.Persistence;

namespace VRRehab.Interactive.Task16
{
    /// <summary>
    /// Gerencia a estante de troféus 3D no Menu Principal.
    /// Controla a visibilidade, efeitos de brilho, placas informativas e auto-respawn
    /// dos 4 troféus de conquista clínica: Maçã Dourada, Bonsai Luminoso, Taça Estelar e Coroa do Drone.
    /// </summary>
    public class TrophyShelfManager : MonoBehaviour
    {
        [System.Serializable]
        public class TrophySlot
        {
            public string trophyId;
            public string title;
            [TextArea]
            public string description;
            public GameObject trophyObject;
            public Transform slotAnchor;
            public TextMeshPro plaqueText;
            public ParticleSystem unlockParticles;
            [HideInInspector]
            public Vector3 initialLocalPos;
            [HideInInspector]
            public Quaternion initialLocalRot;
        }

        [Header("Slots dos Troféus")]
        public List<TrophySlot> slots = new List<TrophySlot>();

        [Header("Efeitos Sonoros")]
        public AudioClip sfxUnlockFanfare;
        public AudioClip sfxPickupShimmer;

        [Header("Configurações de Ergonomia")]
        [Tooltip("Tempo em segundos antes de um troféu largado no chão retornar suavemente ao seu pedestal")]
        public float autoRespawnDelay = 3.5f;

        private AudioSource audioSource;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1.0f;
                audioSource.minDistance = 0.5f;
                audioSource.maxDistance = 8.0f;
            }

            foreach (var slot in slots)
            {
                if (slot.trophyObject == null && slot.slotAnchor != null && slot.slotAnchor.childCount > 0)
                {
                    slot.trophyObject = slot.slotAnchor.GetChild(0).gameObject;
                }
                if (slot.trophyObject == null)
                {
                    foreach (var child in GetComponentsInChildren<Transform>(true))
                    {
                        if (child.name == $"TrophyItem_{slot.trophyId}")
                        {
                            slot.trophyObject = child.gameObject;
                            break;
                        }
                    }
                }

                if (slot.trophyObject != null)
                {
                    slot.initialLocalPos = slot.trophyObject.transform.localPosition;
                    slot.initialLocalRot = slot.trophyObject.transform.localRotation;
                    SetupTrophyInteractivity(slot);
                }
            }
        }

        private void Start()
        {
            if (RehabProgressionSaveSystem.Instance != null)
            {
                RehabProgressionSaveSystem.Instance.OnSaveDataUpdated += RefreshShelfState;
                RehabProgressionSaveSystem.Instance.OnTrophyUnlocked += HandleTrophyUnlocked;
                RefreshShelfState(RehabProgressionSaveSystem.Instance.Data);
            }
            else
            {
                RefreshShelfState(null);
            }
        }

        private void OnDestroy()
        {
            if (RehabProgressionSaveSystem.Instance != null)
            {
                RehabProgressionSaveSystem.Instance.OnSaveDataUpdated -= RefreshShelfState;
                RehabProgressionSaveSystem.Instance.OnTrophyUnlocked -= HandleTrophyUnlocked;
            }
        }

        private void SetupTrophyInteractivity(TrophySlot slot)
        {
            if (slot.trophyObject == null) return;

            // Garante ErgonomicGrabInteractable com Force Grab e Auto Respawn
            ErgonomicGrabInteractable grab = slot.trophyObject.GetComponent<ErgonomicGrabInteractable>();
            if (grab == null)
            {
                grab = slot.trophyObject.AddComponent<ErgonomicGrabInteractable>();
            }

            // Define Home Transform no slot
            Transform homeTarget = slot.slotAnchor != null ? slot.slotAnchor : transform;
            grab.SetHomeTransform(slot.trophyObject.transform.position, slot.trophyObject.transform.rotation);

            // Hook de áudio ao segurar o troféu
            grab.selectEntered.AddListener((args) =>
            {
                PlayPickupSound(slot.trophyObject.transform.position);
            });

            // Highlight Feedback ao mirar laser
            InteractableHighlightFeedback highlight = slot.trophyObject.GetComponent<InteractableHighlightFeedback>();
            if (highlight == null)
            {
                highlight = slot.trophyObject.AddComponent<InteractableHighlightFeedback>();
            }

            // Atualiza texto da plaqueta
            if (slot.plaqueText != null)
            {
                slot.plaqueText.text = slot.title;
            }
        }

        public void RefreshShelfState(RehabSaveData data)
        {
            foreach (var slot in slots)
            {
                if (slot.trophyObject == null) continue;

                bool isUnlocked = true;
                if (RehabProgressionSaveSystem.Instance != null)
                {
                    isUnlocked = RehabProgressionSaveSystem.Instance.IsTrophyUnlocked(slot.trophyId);
                }

                // Troféu desbloqueado fica visível e ativo
                slot.trophyObject.SetActive(isUnlocked);

                if (slot.plaqueText != null)
                {
                    slot.plaqueText.text = isUnlocked ? slot.title : "??? (Bloqueado)";
                    slot.plaqueText.color = isUnlocked ? new Color(0.95f, 0.85f, 0.35f) : new Color(0.5f, 0.5f, 0.5f);
                }
            }
        }

        private void HandleTrophyUnlocked(string trophyId)
        {
            foreach (var slot in slots)
            {
                if (slot.trophyId == trophyId)
                {
                    slot.trophyObject.SetActive(true);
                    if (slot.unlockParticles != null)
                    {
                        slot.unlockParticles.Play();
                    }
                    if (sfxUnlockFanfare != null && audioSource != null)
                    {
                        audioSource.PlayOneShot(sfxUnlockFanfare, 0.95f);
                    }
                    if (VRFeedbackManager.Instance != null)
                    {
                        VRFeedbackManager.Instance.PlaySuccess(slot.trophyObject.transform.position);
                    }
                    break;
                }
            }
        }

        private void PlayPickupSound(Vector3 position)
        {
            if (sfxPickupShimmer != null && audioSource != null)
            {
                audioSource.transform.position = position;
                audioSource.pitch = Random.Range(0.96f, 1.04f);
                audioSource.PlayOneShot(sfxPickupShimmer, 0.85f);
            }
        }

        /// <summary>
        /// Força o retorno suave de todos os troféus aos seus pedestais na estante.
        /// </summary>
        public void RespawnAllTrophies()
        {
            foreach (var slot in slots)
            {
                if (slot.trophyObject != null)
                {
                    var grab = slot.trophyObject.GetComponent<ErgonomicGrabInteractable>();
                    if (grab != null)
                    {
                        grab.TriggerManualRespawn();
                    }
                }
            }
        }
    }
}
