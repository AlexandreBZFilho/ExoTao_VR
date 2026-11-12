using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Ergonomics;
using VRRehab.Core;

namespace VRRehab.Interactive.Task06
{
    /// <summary>
    /// Fita Cassete Musical Interativa de Reabilitação (TASK-06).
    /// Herda diretamente de LaserSwingThrowInteractable (TASK-02), integrando nativamente:
    /// 1. Force Grab ergonômico por raio laser em qualquer distância da mesa do Menu Principal;
    /// 2. Ring buffer de 12 amostras com física de balanço de chicote tangencial (Laser Swing Throw);
    /// 3. Rotação mecânica contínua dos carretéis Spool_Left e Spool_Right quando em reprodução no deck;
    /// 4. Auto-respawn suave para o slot correspondente no rack organizador em caso de perda ou queda;
    /// 5. Otimização PhysX: ContinuousDynamic ao ser segurada/arremessada -> Discrete + Sleep em repouso;
    /// 6. Zero GC Alloc em tempo de execução para VR 90 FPS estável.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class MusicCassetteTape : LaserSwingThrowInteractable
    {
        [Header("Metadados da Fita Cassete")]
        [Tooltip("Identificador único da fita.")]
        public string tapeId = "tape_cyan_01";

        [Tooltip("Título da faixa musical.")]
        public string tapeTitle = "Cyber Horizon";

        [Tooltip("Gênero ou estilo musical.")]
        public string tapeGenre = "Sci-Fi Synthwave";

        [Tooltip("Cor temática de destaque da fita.")]
        public Color themeColor = new Color(0.0f, 0.85f, 1.0f);

        [Tooltip("AudioClip musical associado a esta fita.")]
        public AudioClip musicClip;

        [Tooltip("Andamento aproximado da faixa (BPM) para sincronização rítmica.")]
        [Range(40f, 200f)]
        public float trackBpm = 110f;

        [Header("Carretéis Internos Rotativos")]
        [Tooltip("Transform do carretel esquerdo (Spool_Left).")]
        public Transform spoolLeft;

        [Tooltip("Transform do carretel direito (Spool_Right).")]
        public Transform spoolRight;

        [Tooltip("Velocidade angular dos carretéis em graus por segundo.")]
        public float spoolRotationSpeed = 240f;

        [Header("Estado de Reprodução")]
        [Tooltip("Indica se a fita está atualmente inserida no deck e tocando.")]
        [SerializeField] private bool isPlayingInDeck = false;

        [Header("Ancoragem de Rack / Estante")]
        [Tooltip("Slot de repouso atribuído no organizador de mesa.")]
        [SerializeField] private Transform assignedRackSlot;

        public bool IsPlayingInDeck => isPlayingInDeck;
        public Transform AssignedRackSlot => assignedRackSlot;

        protected override void Awake()
        {
            base.Awake();

            // Configuração ergonômica de salvaguarda para o tampo da mesa (Y = 0.952m)
            respawnFloorThresholdY = 0.70f;
            respawnDelaySeconds = 2.0f;
            enableAutoRespawn = true;

            ResolveSpoolReferences();
        }

        protected override void Reset()
        {
            base.Reset();

            respawnFloorThresholdY = 0.70f;
            respawnDelaySeconds = 2.0f;
            enableAutoRespawn = true;
            ThrowBoostMultiplier = 1.65f;
            MaxThrowSpeed = 18.0f;

            ResolveSpoolReferences();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ResolveSpoolReferences();
        }

        private void ResolveSpoolReferences()
        {
            if (spoolLeft == null)
            {
                Transform t = transform.Find("Spool_Left") ?? transform.Find("Visual_Model/Spool_Left");
                if (t == null)
                {
                    foreach (var child in GetComponentsInChildren<Transform>(true))
                    {
                        if (child.name == "Spool_Left") { spoolLeft = child; break; }
                    }
                }
                else
                {
                    spoolLeft = t;
                }
            }

            if (spoolRight == null)
            {
                Transform t = transform.Find("Spool_Right") ?? transform.Find("Visual_Model/Spool_Right");
                if (t == null)
                {
                    foreach (var child in GetComponentsInChildren<Transform>(true))
                    {
                        if (child.name == "Spool_Right") { spoolRight = child; break; }
                    }
                }
                else
                {
                    spoolRight = t;
                }
            }
        }

        protected override void Update()
        {
            // Amostragem cinemática da classe base quando selecionado/arremessado no laser
            base.Update();

            if (!isPlayingInDeck) return;

            // Rotação mecânica suave contínua dos dois carretéis ao redor do eixo Z local (Zero GC)
            float dt = Time.deltaTime;
            float deltaLeft = spoolRotationSpeed * dt;
            // Diferencial sutil de 6% para simular transferência orgânica de fita magnética
            float deltaRight = (spoolRotationSpeed * 0.94f) * dt;

            if (spoolLeft != null)
            {
                spoolLeft.Rotate(0f, 0f, deltaLeft, Space.Self);
            }

            if (spoolRight != null)
            {
                spoolRight.Rotate(0f, 0f, deltaRight, Space.Self);
            }
        }

        /// <summary>
        /// Comanda o início ou parada da reprodução da fita e rotação dos carretéis.
        /// </summary>
        public void SetPlaying(bool playing)
        {
            isPlayingInDeck = playing;
        }

        /// <summary>
        /// Define o slot de descanso permanente nesta fita (para auto-respawn suave e organizado na mesa).
        /// </summary>
        public void SetAssignedRackSlot(Transform rackSlot)
        {
            assignedRackSlot = rackSlot;
            if (rackSlot != null)
            {
                customHomeAnchor = rackSlot;
                SetHomeTransform(rackSlot.position, rackSlot.rotation);
            }
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);

            // Se for pego pela mão ou raio laser do jogador enquanto estava no deck, encerra reprodução
            if (args.interactorObject is not XRSocketInteractor)
            {
                SetPlaying(false);
            }
        }

        /// <summary>
        /// Impede que a fita acione auto-respawn acidental enquanto estiver legitimamente tocando no deck.
        /// </summary>
        protected override bool CheckOutOfBounds(Vector3 currentPos, Vector3 homePos)
        {
            if (isPlayingInDeck)
            {
                return false;
            }

            return base.CheckOutOfBounds(currentPos, homePos);
        }
    }
}
