using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Ergonomics;

namespace VRRehab.Interactive.Task16
{
    /// <summary>
    /// Troféu da estante (TASK-16, retoque). Especialização ADITIVA da ErgonomicGrabInteractable usada só pelos
    /// troféus — maçãs, fitas, balões etc. continuam com a classe base sem nenhuma mudança.
    ///
    /// Ciclo de vida:
    ///  - DOCKED: parado no pedestal (Rigidbody cinemático, Discrete, sem interpolação) — nada de física empurrando,
    ///    nada escorregando. O "home" é o SlotAnchor (customHomeAnchor), então acompanha a estante.
    ///  - HELD: pego pelo gatilho; dinâmico, ContinuousDynamic e Interpolate só enquanto está na mão/voando.
    ///  - RELEASED: solto perto do pedestal (≤ snapRadius) e devagar (≤ snapMaxSpeed) → desliza para o pedestal em
    ///    snapGlideSeconds; solto longe → volta sozinho depois de returnDelaySeconds; caiu no chão → volta na hora.
    ///  - A coroa equipada no Drone não é puxada de volta (DroneCrownWearable.isEquippedOnDrone).
    /// Nada aqui escreve na raiz, nas âncoras ou na base da estante: só no próprio troféu.
    /// </summary>
    public class ShelfTrophyGrabInteractable : ErgonomicGrabInteractable
    {
        [Header("Estante (TASK-16)")]
        [Tooltip("Distância ao pedestal (m) em que soltar devagar encaixa na hora.")]
        [SerializeField] private float snapRadius = 0.20f;
        [Tooltip("Velocidade máxima (m/s) ao soltar para contar como 'colocar no pedestal'.")]
        [SerializeField] private float snapMaxSpeed = 0.6f;
        [SerializeField] private float snapGlideSeconds = 0.22f;
        [Tooltip("Solto longe do pedestal: volta sozinho depois deste tempo (s).")]
        [SerializeField] private float returnDelaySeconds = 1.5f;
        [SerializeField] private float returnGlideSeconds = 0.6f;
        [Tooltip("Depois de solto, volta a Discrete após este tempo se ainda não encaixou (regra VR).")]
        [SerializeField] private float continuousCollisionSeconds = 3f;

        /// <summary>Encaixou no pedestal (true = encaixe imediato por soltar perto; false = retorno automático).</summary>
        public event Action<ShelfTrophyGrabInteractable, bool> Docked;
        public event Action<ShelfTrophyGrabInteractable> Grabbed;

        public bool IsDocked { get; private set; }
        public int DockCount { get; private set; }
        public float LastDockTime { get; private set; } = -1f;
        public Transform HomeAnchor => customHomeAnchor;

        public float ReturnDelaySeconds { get => returnDelaySeconds; set => returnDelaySeconds = Mathf.Max(0f, value); }
        public float RespawnFloorY => respawnFloorThresholdY;

        private Transform homeParent;
        private Coroutine releaseRoutine;
        private DroneCrownWearable crown;

        protected override void Awake()
        {
            base.Awake();
            // O retorno é controlado aqui (por evento), não pelo monitor periódico da classe base.
            enableAutoRespawn = false;
            homeParent = transform.parent;
            crown = GetComponent<DroneCrownWearable>();
            if (cachedRb != null)
            {
                cachedRb.interpolation = RigidbodyInterpolation.None;
                cachedRb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            }
        }

        private void Start()
        {
            // Começa encaixado no pedestal (a coroa já equipada no Drone fica onde está)
            if (!IsOnDrone) SnapHomeInstant();
        }

        private bool IsOnDrone => crown != null && crown.isEquippedOnDrone;

        /// <summary>Coloca o troféu no pedestal sem animação (início da cena / testes).</summary>
        public void SnapHomeInstant()
        {
            if (homeParent != null && transform.parent != homeParent) transform.SetParent(homeParent, true);
            FreezeDocked();
            transform.SetPositionAndRotation(HomePosition, HomeRotation);
            if (cachedRb != null) { cachedRb.position = HomePosition; cachedRb.rotation = HomeRotation; }
            IsDocked = true;
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (releaseRoutine != null) { StopCoroutine(releaseRoutine); releaseRoutine = null; }
            bool wasReturning = isReturningHome;
            isReturningHome = false;          // impede a base de reiniciar o monitor periódico
            base.OnSelectEntered(args);
            if (wasReturning) StopAllCoroutines();
            IsDocked = false;
            if (cachedRb != null)
            {
                cachedRb.interpolation = RigidbodyInterpolation.Interpolate;
                cachedRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
            Grabbed?.Invoke(this);
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            if (!isActiveAndEnabled) return;
            if (releaseRoutine != null) StopCoroutine(releaseRoutine);
            releaseRoutine = StartCoroutine(ReleaseRoutine());
        }

        private IEnumerator ReleaseRoutine()
        {
            // Espera o XRI aplicar a velocidade de arremesso (SetupRigidbodyDrop) antes de decidir
            yield return null;
            if (IsOnDrone) { releaseRoutine = null; yield break; }

            if (ShouldSnap())
            {
                releaseRoutine = null;
                yield return DockRoutine(snapGlideSeconds, true);
                yield break;
            }

            float t = 0f;
            bool discreteAgain = false;
            while (t < returnDelaySeconds)
            {
                if (isSelected || IsOnDrone) { releaseRoutine = null; yield break; }
                if (transform.position.y < respawnFloorThresholdY) break;          // caiu no chão: volta já
                if (t > 0.25f && ShouldSnap()) { releaseRoutine = null; yield return DockRoutine(snapGlideSeconds, true); yield break; }
                if (!discreteAgain && t >= continuousCollisionSeconds && cachedRb != null)
                {
                    cachedRb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                    discreteAgain = true;
                }
                t += Time.deltaTime;
                yield return null;
            }
            releaseRoutine = null;
            if (!isSelected && !IsOnDrone) yield return DockRoutine(returnGlideSeconds, false);
        }

        private bool ShouldSnap()
        {
            if (cachedRb == null) return false;
            float d2 = (transform.position - HomePosition).sqrMagnitude;
            Vector3 v = cachedRb.isKinematic ? Vector3.zero : GetRigidbodyVelocity(cachedRb);
            return d2 <= snapRadius * snapRadius && v.sqrMagnitude <= snapMaxSpeed * snapMaxSpeed;
        }

        /// <summary>Retorno manual (TriggerManualRespawn / RespawnAllTrophies) usa o mesmo encaixe.</summary>
        protected override IEnumerator ReturnHomeRoutine() => DockRoutine(returnGlideSeconds, false);

        private IEnumerator DockRoutine(float duration, bool snapped)
        {
            isReturningHome = true;
            FreezeDocked();
            if (homeParent != null && transform.parent != homeParent) transform.SetParent(homeParent, true);

            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            float arc = snapped ? 0.02f : Mathf.Clamp(Vector3.Distance(startPos, HomePosition) * 0.15f, 0.06f, 0.40f);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                float s = u * u * (3f - 2f * u);
                Vector3 p = Vector3.Lerp(startPos, HomePosition, s);   // home lido a cada frame (segue o slot)
                p.y += Mathf.Sin(u * Mathf.PI) * arc;
                transform.SetPositionAndRotation(p, Quaternion.Slerp(startRot, HomeRotation, s));
                yield return null;
            }
            transform.SetPositionAndRotation(HomePosition, HomeRotation);
            if (cachedRb != null) { cachedRb.position = HomePosition; cachedRb.rotation = HomeRotation; }
            isReturningHome = false;
            lastDropTimestamp = -1f;
            IsDocked = true;
            DockCount++;
            LastDockTime = Time.time;
            Docked?.Invoke(this, snapped);
        }

        /// <summary>Estado de repouso no pedestal: cinemático, parado, Discrete, sem interpolação.</summary>
        private void FreezeDocked()
        {
            if (cachedRb == null) return;
            if (!cachedRb.isKinematic)
            {
                SetRigidbodyVelocity(cachedRb, Vector3.zero);
                SetRigidbodyAngularVelocity(cachedRb, Vector3.zero);
            }
            cachedRb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            cachedRb.interpolation = RigidbodyInterpolation.None;
            cachedRb.isKinematic = true;
        }
    }
}
