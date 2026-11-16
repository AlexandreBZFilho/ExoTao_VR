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
                grabInteractable.selectExited.AddListener(_ => lastReleaseTime = Time.time);
            }
        }

        /// <summary>Instante em que o jogador soltou/arremessou a coroa (só encaixa se estiver na mão ou acabou de ser solta).</summary>
        private float lastReleaseTime = -100f;
        private const float EquipWindowAfterRelease = 1.5f;

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
            // Detecta a coroa encostando no trigger da cabeça do Drone (DroneHeadSocket).
            // Só encaixa se o jogador estiver segurando a coroa ou acabou de soltá-la/arremessá-la:
            // assim o drone voando perto da estante de troféus não "rouba" a coroa sozinho.
            if (isEquippedOnDrone) return;
            bool isHeadSocket = other.name == "DroneHeadSocket" || other.name == "DroneHeadSocketTrigger" ||
                                (droneHeadSocketTransform != null && other.transform == droneHeadSocketTransform);
            if (!isHeadSocket) return;

            bool heldOrJustThrown = (grabInteractable != null && grabInteractable.isSelected) ||
                                    Time.time - lastReleaseTime < EquipWindowAfterRelease;
            if (!heldOrJustThrown) return;

            droneHeadSocketTransform = other.transform;
            EquipOnDrone(true);
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
            // O balanço é feito pelo sistema afetivo no nó visual (Modelo_3D_ExoBot). Girar a raiz aqui
            // brigava com o DroneCompanionFollowAndGrab (dono da posição/rotação) e dava um "tranco" no fim.
            if (droneAffective == null) yield break;
            for (int i = 0; i < 3; i++)
            {
                droneAffective.Wiggle(1f - i * 0.25f);
                yield return new WaitForSeconds(0.45f);
            }
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
