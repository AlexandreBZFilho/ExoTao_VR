using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace VRRehab.DronePet
{
    public enum CompanionFollowState
    {
        /// <summary>Parado na frente-esquerda do paciente para leitura calma do painel, até o paciente andar/virar ou pinçar o drone.</summary>
        WaitingInitialRead,
        /// <summary>Sendo arrastado pelo raio laser (pinçado pelo BoxCollider do corpo).</summary>
        BeingManipulatedByPlayer,
        /// <summary>Seguindo o paciente mantendo o offset escolhido no momento em que foi solto.</summary>
        FollowingPlayerAtCustomOffset
    }

    /// <summary>
    /// Locomoção e manipulação do Drone companheiro (única fonte de verdade da POSIÇÃO do drone).
    ///
    /// - Pinçar com o laser (XRRayInteractor) no BoxCollider sólido do corpo e arrastar para qualquer lugar.
    ///   O analógico (eixo Y) do controle que está segurando aproxima/afasta o drone ao longo do raio
    ///   (scroll do mouse também funciona no Editor/simulador).
    /// - Ao soltar, memoriza o offset relativo à cabeça do paciente e passa a segui-lo mantendo exatamente essa distância.
    /// - Posição padrão: um pouco à esquerda do campo de visão, para não bloquear a visão da frente.
    /// - Sem colisão com o jogador (Physics.IgnoreCollision + filtro nos casts), mas COM colisão contra paredes,
    ///   móveis e terreno (BoxCast com deslizamento + despenetração). Objetos dinâmicos (maçãs, bolas) são empurrados.
    /// - Se ficar preso atrás de uma parede por muito tempo, reaparece na posição alvo (se estiver livre).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    [SelectionBase]
    public class DroneCompanionFollowAndGrab : XRBaseInteractable
    {
        [Header("Estado")]
        [SerializeField] private CompanionFollowState currentState = CompanionFollowState.WaitingInitialRead;
        public CompanionFollowState CurrentState => currentState;
        public bool IsBeingDragged => currentState == CompanionFollowState.BeingManipulatedByPlayer;

        [Header("Posição Padrão (relativa à cabeça do paciente)")]
        [Tooltip("X = lado (negativo = esquerda), Y = altura relativa aos olhos, Z = distância à frente (m)")]
        [SerializeField] private Vector3 defaultOffset = new Vector3(-0.55f, -0.30f, 1.35f);

        [Header("Offset Memorizado (definido ao soltar o drone)")]
        [SerializeField] private Vector3 offsetLocal = new Vector3(-0.55f, -0.30f, 1.35f);
        public Vector3 OffsetLocal => offsetLocal;

        [Header("Limites")]
        [Tooltip("Distância horizontal mínima entre o centro do drone e a cabeça (perto o bastante para fazer carinho)")]
        [SerializeField] private float minDistance = 0.50f;
        [SerializeField] private float maxDistance = 5.50f;
        [SerializeField] private float minHeightAboveGround = 0.35f;
        [SerializeField] private float maxHeightAboveHead = 1.20f;

        [Header("Seguimento")]
        [SerializeField] private float smoothTime = 0.30f;
        [SerializeField] private float bobAmplitude = 0.02f;
        [Tooltip("O paciente precisa se afastar isso (m) do ponto inicial para o drone sair do estado de leitura inicial")]
        [SerializeField] private float startFollowDistance = 0.60f;

        [Header("Recentralização preguiçosa (estilo menus do Quest)")]
        [Tooltip("Se o paciente vira mais que isso e o drone sai do campo de visão, o drone volta para o lado dele")]
        [SerializeField] private float recenterYawThreshold = 60f;
        [SerializeField] private float recenterDelay = 1.0f;
        [SerializeField] private float recenterSpeedDegPerSec = 140f;
        [SerializeField] private float inViewHalfAngle = 50f;

        [Header("Arraste pelo Laser")]
        [SerializeField] private float thumbstickDistanceSpeed = 2.0f;
        [SerializeField] private float mouseScrollStep = 0.15f;
        [SerializeField] private float minGrabDistance = 0.35f;
        [SerializeField] private float maxGrabDistance = 6.0f;
        [SerializeField] private float dragFollowSharpness = 22f;

        [Header("Colisão com o Mundo")]
        [SerializeField] private LayerMask collisionMask = Physics.DefaultRaycastLayers;
        [SerializeField] private float skinWidth = 0.02f;
        [SerializeField] private float stuckTeleportDistance = 2.5f;
        [SerializeField] private float stuckTeleportDelay = 1.5f;

        [Header("Áudio")]
        public AudioClip sfxAnchorLock;
        [Range(0.05f, 0.40f)] [SerializeField] private float anchorLockVolume = 0.18f;

        [Header("Referências (auto-detectadas se vazias)")]
        [Tooltip("Cabeça/câmera do paciente")]
        public Transform headTransform;
        [Tooltip("Raiz do jogador (CharacterController / XR Origin) — seus colisores são ignorados")]
        public Transform playerRoot;

        // ---- Componentes ----
        private Rigidbody body;
        private BoxCollider bodyCollider;
        private AudioSource sfxSource;

        // ---- Seguimento ----
        private float referenceYaw;
        private float recenterTimer;
        private bool recentering;
        private Vector3 followVelocity;
        private Vector3 initialHeadPosition;
        private bool poseInitialized;
        private float stuckTimer;

        // ---- Arraste ----
        private IXRSelectInteractor dragInteractor;
        private Transform dragOrigin;
        private float dragDistance;
        private Vector3 dragGrabOffset;

        // ---- Filtro de colisão (sem alocação por frame) ----
        private readonly HashSet<Collider> playerColliders = new HashSet<Collider>();
        private readonly RaycastHit[] castHits = new RaycastHit[16];
        private readonly Collider[] overlapHits = new Collider[16];
        private float nextPlayerScanTime;

        // =====================================================================
        // CICLO DE VIDA
        // =====================================================================

        protected override void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None; // posição é escrita direto no transform
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;

            bodyCollider = GetComponent<BoxCollider>();
            if (bodyCollider == null) bodyCollider = gameObject.AddComponent<BoxCollider>();
            bodyCollider.isTrigger = false; // sólido: o laser (que ignora triggers) consegue pinçar e o drone colide com o mundo
            FitColliderToModel();

            // O ÚNICO colisor de interação do drone é o corpo (evita que triggers filhos/socket da coroa roubem o laser)
            colliders.Clear();
            colliders.Add(bodyCollider);
            interactionLayers = ~0;
            selectMode = InteractableSelectMode.Single;

            base.Awake();

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 1f;
            sfxSource.minDistance = 0.8f;
            sfxSource.maxDistance = 8f;
            sfxSource.dopplerLevel = 0f;
#if UNITY_EDITOR
            if (sfxAnchorLock == null)
                sfxAnchorLock = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Generated/Task03/drone_anchor_lock.wav");
#endif
        }

        protected virtual void Start()
        {
            EnsurePlayerReferences(true);
            InitializePose();
        }

        /// <summary>Ajusta o BoxCollider ao tamanho real da malha do drone (Modelo_3D_ExoBot).</summary>
        private void FitColliderToModel()
        {
            Transform model = transform.Find("Modelo_3D_ExoBot");
            MeshFilter mf = model != null ? model.GetComponent<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null)
            {
                bodyCollider.center = new Vector3(0f, 0.05f, 0f);
                bodyCollider.size = new Vector3(1.0f, 0.6f, 0.45f);
                return;
            }

            Bounds mb = mf.sharedMesh.bounds;
            Matrix4x4 toRoot = transform.worldToLocalMatrix * model.localToWorldMatrix;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = toRoot.MultiplyPoint3x4(c);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            bodyCollider.center = (min + max) * 0.5f;
            bodyCollider.size = (max - min) * 0.95f;
        }

        public void EnsurePlayerReferences(bool forceRescan = false)
        {
            if (headTransform == null)
            {
                DroneGuiaIA guia = GetComponent<DroneGuiaIA>();
                if (guia != null && guia.cameraPaciente != null) headTransform = guia.cameraPaciente;
                else if (Camera.main != null) headTransform = Camera.main.transform;
            }

            if (playerRoot == null)
            {
                CharacterController cc = FindFirstObjectByType<CharacterController>();
                if (cc != null) playerRoot = cc.transform;
                else if (headTransform != null)
                {
                    var origin = headTransform.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
                    playerRoot = origin != null ? origin.transform : headTransform;
                }
            }

            if (forceRescan || Time.time >= nextPlayerScanTime)
            {
                nextPlayerScanTime = Time.time + 5f;
                RescanPlayerColliders();
            }
        }

        /// <summary>Coleta colisores do jogador (CharacterController, mãos, cabeça) e desliga a colisão física com o drone.</summary>
        private void RescanPlayerColliders()
        {
            Collider[] mine = GetComponentsInChildren<Collider>(true);
            AddPlayerColliders(playerRoot, mine);
            if (headTransform != null)
            {
                var origin = headTransform.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
                AddPlayerColliders(origin != null ? origin.transform : headTransform, mine);
            }
            foreach (CharacterController cc in FindObjectsByType<CharacterController>(FindObjectsSortMode.None))
                AddPlayerColliders(cc.transform, mine);
        }

        private void AddPlayerColliders(Transform root, Collider[] mine)
        {
            if (root == null || root == transform || root.IsChildOf(transform)) return;
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
            {
                if (c == null || c.transform.IsChildOf(transform)) continue;
                if (playerColliders.Add(c))
                {
                    foreach (Collider m in mine)
                        if (m != null) Physics.IgnoreCollision(m, c, true);
                }
            }
        }

        private void InitializePose()
        {
            if (headTransform == null) return;
            referenceYaw = HorizontalYaw(headTransform.forward);
            offsetLocal = defaultOffset;
            initialHeadPosition = headTransform.position;
            currentState = CompanionFollowState.WaitingInitialRead;

            Vector3 start = ComputeFollowTarget(false);
            // Começa num lugar livre; se a posição padrão estiver dentro de algo, fica onde a cena colocou
            if (!IsBoxBlocked(start)) transform.position = start;
            transform.rotation = LookAtHeadRotation(transform.position);
            poseInitialized = true;
        }

        // =====================================================================
        // ARRASTE PELO LASER
        // =====================================================================

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);

            dragInteractor = args.interactorObject;
            XRRayInteractor ray = dragInteractor as XRRayInteractor;
            dragOrigin = ray != null && ray.rayOriginTransform != null ? ray.rayOriginTransform : dragInteractor.GetAttachTransform(this);

            Vector3 toDrone = transform.position - dragOrigin.position;
            if (ray != null)
            {
                dragDistance = Mathf.Clamp(Vector3.Dot(toDrone, dragOrigin.forward), minGrabDistance, maxGrabDistance);
                dragGrabOffset = transform.position - (dragOrigin.position + dragOrigin.forward * dragDistance);
                dragGrabOffset = Vector3.ClampMagnitude(dragGrabOffset, 0.6f);
            }
            else
            {
                // Interactor direto (mão encostada): o drone acompanha a mão mantendo o ponto de pegada
                dragDistance = 0f;
                dragGrabOffset = toDrone;
            }

            currentState = CompanionFollowState.BeingManipulatedByPlayer;
            followVelocity = Vector3.zero;
            Haptic(args.interactorObject, 0.15f, 0.08f);
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            if (args.interactorObject != dragInteractor) return;

            dragInteractor = null;
            dragOrigin = null;
            MemorizeCurrentOffset();
            currentState = CompanionFollowState.FollowingPlayerAtCustomOffset;
            recentering = false;
            recenterTimer = 0f;

            if (sfxAnchorLock != null && sfxSource != null)
            {
                sfxSource.pitch = Random.Range(0.98f, 1.02f);
                sfxSource.PlayOneShot(sfxAnchorLock, anchorLockVolume);
            }
            Haptic(args.interactorObject, 0.18f, 0.12f);
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);
            if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic && currentState == CompanionFollowState.BeingManipulatedByPlayer)
                UpdateDrag(Time.deltaTime);
        }

        private void UpdateDrag(float dt)
        {
            if (dragOrigin == null) return;

            Vector3 target;
            if (dragInteractor is XRRayInteractor)
            {
                dragDistance = Mathf.Clamp(dragDistance + ReadDistanceInput(dt), minGrabDistance, maxGrabDistance);
                target = dragOrigin.position + dragOrigin.forward * dragDistance + dragGrabOffset;
            }
            else
            {
                target = dragOrigin.position + dragGrabOffset;
            }

            target = ClampToComfortZone(target);
            Vector3 next = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-dragFollowSharpness * dt));
            MoveWithCollision(next);
            transform.rotation = Quaternion.Slerp(transform.rotation, LookAtHeadRotation(transform.position), 1f - Mathf.Exp(-10f * dt));
        }

        /// <summary>Eixo Y do analógico do controle que segura o drone (+ scroll do mouse): aproxima/afasta ao longo do raio.</summary>
        private float ReadDistanceInput(float dt)
        {
            float delta = 0f;
            float stickY = 0f;
            bool hasStick = false;

            if (dragInteractor is XRBaseControllerInteractor ctrlInteractor && ctrlInteractor.xrController != null)
            {
                if (ctrlInteractor.xrController is ActionBasedController abc)
                {
                    var action = abc.translateAnchorAction.action;
                    if (action != null && action.enabled && action.bindings.Count > 0)
                    {
                        stickY = action.ReadValue<Vector2>().y;
                        hasStick = true;
                    }
                }

                if (!hasStick)
                {
                    // Fallback: lê o analógico/trackpad direto do dispositivo XR da mão correspondente
                    bool left = ctrlInteractor.xrController.name.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    UnityEngine.XR.InputDevice dev = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(left ? UnityEngine.XR.XRNode.LeftHand : UnityEngine.XR.XRNode.RightHand);
                    if (dev.isValid && dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 axis))
                        stickY = axis.y;
                }
            }

            if (Mathf.Abs(stickY) > 0.2f) delta += stickY * thumbstickDistanceSpeed * dt;

#if ENABLE_LEGACY_INPUT_MANAGER
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f) delta += scroll * mouseScrollStep;
#endif
            return delta;
        }

        private void MemorizeCurrentOffset()
        {
            if (headTransform == null) return;

            Vector3 delta = transform.position - headTransform.position;
            Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, referenceYaw, 0f)) * new Vector3(delta.x, 0f, delta.z);
            Vector2 xz = new Vector2(local.x, local.z);
            float d = xz.magnitude;
            if (d < minDistance) xz = (d < 0.01f ? Vector2.up : xz / d) * minDistance;
            else if (d > maxDistance) xz = xz / d * maxDistance;

            offsetLocal = new Vector3(xz.x, Mathf.Clamp(delta.y, -1.4f, maxHeightAboveHead), xz.y);
        }

        // =====================================================================
        // SEGUIMENTO
        // =====================================================================

        private void Update()
        {
            EnsurePlayerReferences();
            if (headTransform == null) return;
            if (!poseInitialized) InitializePose();

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (currentState == CompanionFollowState.BeingManipulatedByPlayer) return; // tratado em ProcessInteractable

            UpdateReferenceYaw(dt);

            if (currentState == CompanionFollowState.WaitingInitialRead)
            {
                Vector3 moved = headTransform.position - initialHeadPosition;
                moved.y = 0f;
                if (moved.sqrMagnitude > startFollowDistance * startFollowDistance || recentering)
                    currentState = CompanionFollowState.FollowingPlayerAtCustomOffset;
            }

            Vector3 target = ComputeFollowTarget(true);
            Vector3 next = Vector3.SmoothDamp(transform.position, target, ref followVelocity, smoothTime, Mathf.Infinity, dt);
            Vector3 before = transform.position;
            MoveWithCollision(next);

            // Preso atrás de parede/porta: depois de um tempo, reaparece no alvo se o alvo estiver livre
            bool blocked = (transform.position - next).sqrMagnitude > 0.0004f;
            if (blocked) followVelocity = (transform.position - before) / dt;
            if (blocked && (target - transform.position).sqrMagnitude > stuckTeleportDistance * stuckTeleportDistance)
            {
                stuckTimer += dt;
                if (stuckTimer > stuckTeleportDelay && !IsBoxBlocked(target))
                {
                    transform.position = target;
                    followVelocity = Vector3.zero;
                    stuckTimer = 0f;
                }
            }
            else stuckTimer = 0f;

            transform.rotation = Quaternion.Slerp(transform.rotation, LookAtHeadRotation(transform.position), 1f - Mathf.Exp(-6f * dt));
        }

        /// <summary>
        /// Mantém uma direção de referência "preguiçosa": olhar para o drone nunca faz ele fugir;
        /// mas se o paciente se vira e o drone fica fora da visão por um tempo, ele volta para o lado do paciente.
        /// </summary>
        private void UpdateReferenceYaw(float dt)
        {
            float camYaw = HorizontalYaw(headTransform.forward);
            float deltaYaw = Mathf.DeltaAngle(referenceYaw, camYaw);

            if (recentering)
            {
                referenceYaw = Mathf.MoveTowardsAngle(referenceYaw, camYaw, recenterSpeedDegPerSec * dt);
                if (Mathf.Abs(Mathf.DeltaAngle(referenceYaw, camYaw)) < 2f)
                {
                    recentering = false;
                    recenterTimer = 0f;
                }
                return;
            }

            Vector3 toDrone = transform.position - headTransform.position;
            toDrone.y = 0f;
            Vector3 fwd = headTransform.forward;
            fwd.y = 0f;
            bool droneInView = toDrone.sqrMagnitude > 0.0001f && fwd.sqrMagnitude > 0.0001f &&
                               Vector3.Angle(fwd, toDrone) < inViewHalfAngle;

            if (Mathf.Abs(deltaYaw) > recenterYawThreshold && !droneInView)
            {
                recenterTimer += dt;
                if (recenterTimer > recenterDelay) recentering = true;
            }
            else recenterTimer = 0f;
        }

        private Vector3 ComputeFollowTarget(bool withBob)
        {
            Vector3 head = headTransform.position;
            Vector3 horiz = Quaternion.Euler(0f, referenceYaw, 0f) * new Vector3(offsetLocal.x, 0f, offsetLocal.z);
            Vector3 target = new Vector3(head.x + horiz.x, head.y + offsetLocal.y, head.z + horiz.z);
            if (withBob) target.y += Mathf.Sin(Time.time * 2f) * bobAmplitude;
            return ClampToComfortZone(target);
        }

        /// <summary>Garante distância mínima da cabeça, altura mínima acima do chão e máxima acima da cabeça.</summary>
        private Vector3 ClampToComfortZone(Vector3 p)
        {
            if (headTransform == null) return p;
            Vector3 head = headTransform.position;

            Vector3 flat = new Vector3(p.x - head.x, 0f, p.z - head.z);
            float d = flat.magnitude;
            float minD = currentState == CompanionFollowState.BeingManipulatedByPlayer ? minDistance * 0.8f : minDistance;
            if (d < minD)
            {
                Vector3 dir = d > 0.001f ? flat / d : HorizontalForward(headTransform);
                p = new Vector3(head.x + dir.x * minD, p.y, head.z + dir.z * minD);
            }

            float ground = GroundHeight(p, head.y);
            p.y = Mathf.Clamp(p.y, ground + minHeightAboveGround, Mathf.Max(ground + minHeightAboveGround, head.y + maxHeightAboveHead));
            return p;
        }

        // =====================================================================
        // COLISÃO COM O MUNDO (não com o jogador)
        // =====================================================================

        private void MoveWithCollision(Vector3 desired)
        {
            Vector3 pos = transform.position;
            Vector3 delta = desired - pos;

            for (int pass = 0; pass < 2; pass++)
            {
                float dist = delta.magnitude;
                if (dist < 0.0001f) break;
                Vector3 dir = delta / dist;

                if (!CastBody(pos, dir, dist + skinWidth, out RaycastHit hit))
                {
                    pos += delta;
                    break;
                }

                float allowed = Mathf.Max(0f, hit.distance - skinWidth);
                pos += dir * allowed;
                // desliza ao longo da superfície com o restante do movimento
                delta = Vector3.ProjectOnPlane(dir * (dist - allowed), hit.normal);
            }

            transform.position = Depenetrate(pos);
        }

        private bool CastBody(Vector3 rootPos, Vector3 dir, float distance, out RaycastHit nearest)
        {
            nearest = default;
            Quaternion rot = transform.rotation;
            Vector3 center = rootPos + rot * Vector3.Scale(bodyCollider.center, transform.lossyScale);
            Vector3 half = Vector3.Scale(bodyCollider.size * 0.5f, transform.lossyScale) * 0.98f;

            int n = Physics.BoxCastNonAlloc(center, half, dir, castHits, rot, distance, collisionMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = castHits[i];
                if (IsIgnored(h.collider)) continue;
                if (h.distance <= 0f) continue; // sobreposição inicial é resolvida pela despenetração
                if (h.distance < best)
                {
                    best = h.distance;
                    nearest = h;
                    found = true;
                }
            }
            return found;
        }

        private Vector3 Depenetrate(Vector3 rootPos)
        {
            Quaternion rot = transform.rotation;
            Vector3 center = rootPos + rot * Vector3.Scale(bodyCollider.center, transform.lossyScale);
            Vector3 half = Vector3.Scale(bodyCollider.size * 0.5f, transform.lossyScale);

            int n = Physics.OverlapBoxNonAlloc(center, half, overlapHits, rot, collisionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider other = overlapHits[i];
                if (IsIgnored(other)) continue;
                if (Physics.ComputePenetration(bodyCollider, rootPos, rot,
                        other, other.transform.position, other.transform.rotation,
                        out Vector3 pushDir, out float pushDist))
                {
                    rootPos += pushDir * Mathf.Min(pushDist + 0.001f, 0.25f);
                }
            }
            return rootPos;
        }

        private bool IsBoxBlocked(Vector3 rootPos)
        {
            Quaternion rot = transform.rotation;
            Vector3 center = rootPos + rot * Vector3.Scale(bodyCollider.center, transform.lossyScale);
            Vector3 half = Vector3.Scale(bodyCollider.size * 0.5f, transform.lossyScale);
            int n = Physics.OverlapBoxNonAlloc(center, half, overlapHits, rot, collisionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (!IsIgnored(overlapHits[i])) return true;
            return false;
        }

        /// <summary>Colisores que NÃO bloqueiam o drone: ele mesmo, o jogador e objetos dinâmicos (que são empurrados).</summary>
        private bool IsIgnored(Collider c)
        {
            if (c == null || c.isTrigger) return true;
            if (c.transform == transform || c.transform.IsChildOf(transform)) return true;
            if (playerColliders.Contains(c)) return true;
            Rigidbody rb = c.attachedRigidbody;
            if (rb != null && !rb.isKinematic) return true;
            return false;
        }

        private float GroundHeight(Vector3 p, float referenceY)
        {
            Vector3 origin = new Vector3(p.x, Mathf.Max(p.y, referenceY) + 1.0f, p.z);
            int n = Physics.RaycastNonAlloc(origin, Vector3.down, castHits, 12f, collisionMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            float y = referenceY - 1.6f; // sem chão detectado: assume olhos ~1.6 m acima do chão
            for (int i = 0; i < n; i++)
            {
                if (IsIgnored(castHits[i].collider)) continue;
                if (castHits[i].distance < best)
                {
                    best = castHits[i].distance;
                    y = castHits[i].point.y;
                }
            }
            return y;
        }

        // =====================================================================
        // UTILITÁRIOS
        // =====================================================================

        private Quaternion LookAtHeadRotation(Vector3 from)
        {
            Vector3 dir = headTransform != null ? headTransform.position - from : -transform.forward;
            dir.y = 0f;
            return dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : transform.rotation;
        }

        private static float HorizontalYaw(Vector3 forward)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return 0f;
            return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        private static Vector3 HorizontalForward(Transform t)
        {
            Vector3 f = t.forward;
            f.y = 0f;
            return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
        }

        private static void Haptic(IXRInteractor interactor, float amplitude, float duration)
        {
            if (interactor is XRBaseControllerInteractor ctrl) ctrl.SendHapticImpulse(amplitude, duration);
        }

        /// <summary>Volta para a posição padrão (frente-esquerda do paciente).</summary>
        public void ResetToDefaultPosition()
        {
            offsetLocal = defaultOffset;
            if (headTransform != null) referenceYaw = HorizontalYaw(headTransform.forward);
            if (currentState != CompanionFollowState.BeingManipulatedByPlayer)
                currentState = CompanionFollowState.FollowingPlayerAtCustomOffset;
        }

        /// <summary>Define a distância horizontal mantida (ex.: atalho de acessibilidade do terapeuta).</summary>
        public void SetCustomDistance(float meters)
        {
            Vector2 xz = new Vector2(offsetLocal.x, offsetLocal.z);
            if (xz.sqrMagnitude < 0.0001f) xz = Vector2.up;
            xz = xz.normalized * Mathf.Clamp(meters, minDistance, maxDistance);
            offsetLocal = new Vector3(xz.x, offsetLocal.y, xz.y);
        }
    }
}
