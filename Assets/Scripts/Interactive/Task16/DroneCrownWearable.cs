using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;
using VRRehab.DronePet;
using VRRehab.Ergonomics;
using VRRehab.Persistence;

namespace VRRehab.Interactive.Task16
{
    /// <summary>
    /// Componente acoplado à Coroa Real do Drone Pet (Trophy_DroneCrownHat).
    /// Controla o encaixe físico no topo da cabeça do Drone (DroneHeadSocket),
    /// reproduzindo a fanfarra real, partículas de estrelas de coroação e reações afetivas.
    /// </summary>
    public class DroneCrownWearable : MonoBehaviour
    {
        [Header("Estado")]
        public bool isEquippedOnDrone = false;

        [Header("Referências")]
        [Tooltip("Ponto de acoplamento da cabeça do Drone Pet")]
        public Transform droneHeadSocketTransform;
        [Tooltip("Sistema afetivo do Drone Pet")]
        public DroneAffectiveSystem droneAffective;
        [Tooltip("Sistema de celebração do Drone")]
        public DroneMilestoneCelebration droneCelebration;

        [Header("Efeitos & Áudio")]
        public AudioClip sfxCrownEquip;
        public ParticleSystem coronationParticles;

        private AudioSource audioSource;
        private ErgonomicGrabInteractable grabInteractable;
        private Rigidbody crownRb;

        private void Awake()
        {
            crownRb = GetComponent<Rigidbody>();
            grabInteractable = GetComponent<ErgonomicGrabInteractable>();
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1.0f;
                audioSource.minDistance = 0.5f;
                audioSource.maxDistance = 10.0f;
            }

            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.AddListener(OnGrabbed);
            }
        }

        private void Start()
        {
            AutoLocateDroneReferences();

            // Restaura estado se salvo previamente como coroado
            if (RehabProgressionSaveSystem.Instance != null && RehabProgressionSaveSystem.Instance.Data.isDroneCrowned)
            {
                EquipOnDrone(false);
            }
        }

        public void AutoLocateDroneReferences()
        {
            if (droneHeadSocketTransform == null)
            {
                GameObject socketObj = GameObject.Find("DroneHeadSocket");
                if (socketObj != null) droneHeadSocketTransform = socketObj.transform;
            }

            if (droneAffective == null)
            {
                droneAffective = FindFirstObjectByType<DroneAffectiveSystem>();
            }

            if (droneCelebration == null)
            {
                droneCelebration = FindFirstObjectByType<DroneMilestoneCelebration>();
            }
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            if (isEquippedOnDrone)
            {
                UnequipFromDrone();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Detecta aproximação do socket da cabeça do Drone
            if (other.CompareTag("Untagged") && other.name == "DroneHeadSocketTrigger")
            {
                if (!isEquippedOnDrone)
                {
                    EquipOnDrone(true);
                }
            }
        }

        /// <summary>
        /// Acopla a coroa imperial ao topo da cabeça do Drone Pet.
        /// </summary>
        public void EquipOnDrone(bool triggerFeedback = true)
        {
            AutoLocateDroneReferences();
            if (droneHeadSocketTransform == null) return;

            isEquippedOnDrone = true;

            // Se o jogador estiver segurando, força o soltar para acoplar no socket
            if (grabInteractable != null && grabInteractable.isSelected)
            {
                var interactor = grabInteractable.firstInteractorSelecting;
                if (interactor != null && grabInteractable.interactionManager != null)
                {
                    grabInteractable.interactionManager.SelectExit(interactor, grabInteractable);
                }
            }

            // Anexa firmemente à cabeça do Drone
            transform.SetParent(droneHeadSocketTransform, true);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            if (crownRb != null)
            {
                crownRb.isKinematic = true;
                crownRb.linearVelocity = Vector3.zero;
                crownRb.angularVelocity = Vector3.zero;
            }

            if (triggerFeedback)
            {
                PlayCoronationCeremony();
            }

            if (RehabProgressionSaveSystem.Instance != null)
            {
                RehabProgressionSaveSystem.Instance.SetDroneCrowned(true);
            }
        }

        /// <summary>
        /// Desacopla a coroa da cabeça do Drone.
        /// </summary>
        public void UnequipFromDrone()
        {
            isEquippedOnDrone = false;
            transform.SetParent(null, true);

            if (crownRb != null)
            {
                crownRb.isKinematic = false;
            }

            if (droneAffective != null)
            {
                droneAffective.SetExpression(DroneLEDExpression.Normal);
            }

            if (RehabProgressionSaveSystem.Instance != null)
            {
                RehabProgressionSaveSystem.Instance.SetDroneCrowned(false);
            }
        }

        private void PlayCoronationCeremony()
        {
            // 1. Áudio Majestoso de Coroação
            if (sfxCrownEquip != null && audioSource != null)
            {
                audioSource.pitch = 1.0f;
                audioSource.PlayOneShot(sfxCrownEquip, 1.0f);
            }
            else if (VRFeedbackManager.Instance != null)
            {
                VRFeedbackManager.Instance.PlaySuccess(transform.position);
            }

            // 2. Partículas de Estrelas e Brilho Imperial
            if (coronationParticles != null)
            {
                coronationParticles.Play();
            }

            // 3. Expressão Afetiva Radiante no Visor do Drone
            if (droneAffective != null)
            {
                droneAffective.SetExpression(DroneLEDExpression.Love, 6.0f);
            }

            // 4. Celebração do Drone
            StartCoroutine(CelebrationWiggleRoutine());
        }

        private IEnumerator CelebrationWiggleRoutine()
        {
            Transform droneRoot = droneHeadSocketTransform != null ? droneHeadSocketTransform.root : null;
            if (droneRoot == null) yield break;

            Quaternion origRot = droneRoot.localRotation;
            float elapsed = 0f;
            float duration = 1.8f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float wiggle = Mathf.Sin(t * Mathf.PI * 8f) * 12f * (1f - t);
                droneRoot.localRotation = origRot * Quaternion.Euler(0f, wiggle, wiggle * 0.4f);
                yield return null;
            }

            droneRoot.localRotation = origRot;
        }

        /// <summary>
        /// Posiciona a coroa diretamente no socket do Drone para inspeção visual e QA.
        /// </summary>
        public void SnapToDroneHeadInstant(Transform socket)
        {
            if (socket != null)
            {
                droneHeadSocketTransform = socket;
                transform.SetParent(socket, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                if (crownRb != null) crownRb.isKinematic = true;
                isEquippedOnDrone = true;
            }
        }
    }
}
