using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Ergonomics;
using VRRehab.Core;

namespace VRRehab.Interactive.Task06
{
    /// <summary>
    /// Fita Cassete Musical Interativa para o Rádio Deck do Menu Principal.
    /// Herda de ErgonomicGrabInteractable garantindo Force Grab, Auto-Respawn inteligente,
    /// rotação mecânica contínua dos carretéis internos durante a reprodução,
    /// e armazenamento de metadados da faixa musical clínica.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class MusicCassetteTape : ErgonomicGrabInteractable
    {
        [Header("Metadados da Fita Cassete")]
        [Tooltip("Identificador único da fita.")]
        public string tapeId = "tape_cyan_01";

        [Tooltip("Título da faixa musical.")]
        public string tapeTitle = "Cyber Horizon";

        [Tooltip("Gênero ou ambiente sonoro.")]
        public string tapeGenre = "Sci-Fi Synthwave";

        [Tooltip("Cor de destaque da fita.")]
        public Color themeColor = new Color(0.0f, 0.85f, 1.0f);

        [Tooltip("AudioClip musical associado a esta fita.")]
        public AudioClip musicClip;

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
            ResolveSpoolReferences();
        }

        private void ResolveSpoolReferences()
        {
            if (spoolLeft == null)
            {
                Transform t = transform.Find("Spool_Left") ?? transform.Find("Visual_Model/Spool_Left");
                if (t == null)
                {
                    // Busca recursiva profunda
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

        protected override void OnEnable()
        {
            base.OnEnable();
            ResolveSpoolReferences();
        }

        private void Update()
        {
            if (!isPlayingInDeck) return;

            // Rotação suave contínua dos dois carretéis mecânicos ao redor do eixo Z local
            float deltaLeft = spoolRotationSpeed * Time.deltaTime;
            // Efeito realista de transferência de fita: carretel receptor gira com leve diferencial de raio
            float deltaRight = (spoolRotationSpeed * 0.94f) * Time.deltaTime;

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
        /// Define o slot de descanso permanente nesta fita (para auto-respawn organizado na mesa).
        /// </summary>
        public void SetAssignedRackSlot(Transform rackSlot)
        {
            assignedRackSlot = rackSlot;
            if (rackSlot != null)
            {
                SetHomeTransform(rackSlot.position, rackSlot.rotation);
            }
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);

            // Se for pego pela mão do jogador enquanto estava no deck, desativa reprodução
            if (args.interactorObject is not XRSocketInteractor)
            {
                SetPlaying(false);
            }
        }
    }
}
