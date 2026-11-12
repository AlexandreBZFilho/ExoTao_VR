using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;

namespace VRRehab.DronePet
{
    /// <summary>
    /// Expressões faciais disponíveis no visor LED cibernético do Drone Pet.
    /// </summary>
    public enum DroneLEDExpression
    {
        Normal,
        Happy,
        Love,
        Poked,
        Dance
    }

    /// <summary>
    /// Sistema Afetivo Completo do Drone Pet (TASK-04):
    /// - Reatividade a Carinho (Petting):
    ///   * Suporte nativo a carinho físico por aproximação e via raio laser (XRRayInteractor hover contínuo no topo).
    ///   * Vibração háptica de ronrom suave (purring tátil no controle).
    ///   * Emissão de corações holográficos flutuantes, som purr confortável (drone_purr_happy.wav ~0.18f) e expressão Love (♥‿♥).
    ///   * Balanço harmônico suave exclusivo no nó filho visual (zero interferência na raiz/offsetLocal).
    /// - Reatividade a Cutucão (Poke / Boop):
    ///   * Suporte a cutucão por contato físico ou clique de gatilho do laser mirado na face/nariz.
    ///   * Simulação de mola física amortecida (squash & stretch local), som elástico suave (drone_poke_chirp.wav ~0.20f),
    ///     risadinha robótica subsequente (drone_giggle_soft.wav ~0.18f) e expressão Poked (@_@).
    /// - Dança Rítmica Musical:
    ///   * Análise de espectro FFT da música do rádio do menu (zero chamadas a GameObject.Find em Update),
    ///     balanço sincronizado com os graves e expressão musical (♪‿♪).
    /// - Performance VR Ultra-Rigorosa:
    ///   * Troca de texturas e emissão EXCLUSIVAMENTE via MaterialPropertyBlock (zero clone de material na heap).
    ///   * Zero GC Alloc durante o gameplay, compatibilidade total com SRP Batcher e GPU Instancing.
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public class DroneAffectiveSystem : MonoBehaviour
    {
        public static DroneAffectiveSystem Instance { get; private set; }

        [Header("Nó Visual Filho (Desacoplado da Raiz Cinemática)")]
        [Tooltip("Transform do modelo 3D visual do Drone. Todos os offsets de mola, wobble e dança atuam estritamente aqui")]
        public Transform droneVisualRoot;

        [Header("Visor Frontal LED")]
        [Tooltip("Renderer do visor frontal LED do Drone")]
        public MeshRenderer visorRenderer;
        public Texture2D texNormal;
        public Texture2D texHappy;
        public Texture2D texLove;
        public Texture2D texPoked;
        public Texture2D texDance;

        [Header("Materiais e Cores")]
        public Color ledEmissiveColor = new Color(0.0f, 0.95f, 1.0f);
        public float ledEmissiveIntensity = 2.8f;

        [Header("Reatividade a Carinho (Petting)")]
        [Tooltip("Trigger na calota superior da cabeça do drone")]
        public Collider headPettingTrigger;
        [Tooltip("Interator XR para suporte nativo a carinho com o feixe de laser")]
        [SerializeField] private XRSimpleInteractable headLaserInteractable;
        [Tooltip("Emissor de corações holográficos flutuantes")]
        public ParticleSystem heartParticles;
        [Range(0.01f, 1.0f)] public float pettingHapticAmplitude = 0.20f;
        public float pettingHapticInterval = 0.05f;

        [Header("Reatividade a Cutucão (Poke / Boop)")]
        [Tooltip("Trigger no visor/nariz frontal")]
        public Collider facePokeTrigger;
        [Tooltip("Interator XR para suporte a cutucada com clique de gatilho do laser")]
        [SerializeField] private XRSimpleInteractable faceLaserInteractable;
        public float pokeRecoilZ = -0.055f;
        public float pokeWobbleAngle = 9.0f;
        public float pokeSpringStiffness = 320.0f;
        public float pokeSpringDamping = 16.0f;

        [Header("Dança Rítmica Musical")]
        [Tooltip("AudioSource do rádio ou música do menu para sincronia rítmica")]
        public AudioSource musicSource;
        public bool enableMusicDance = true;
        public float danceTiltAngle = 6.5f;
        public float danceBounceHeight = 0.022f;
        public float danceBeatSensitivity = 0.035f;

        [Header("Fontes e Clips de Áudio VR-Safe")]
        [Tooltip("AudioSource 3D dedicado aos efeitos vocais e reações afetivas do Drone")]
        public AudioSource petVoiceAudioSource;
        [Tooltip("Ronrom suave contínuo (drone_purr_happy.wav)")]
        public AudioClip sfxDronePurr;
        [Tooltip("Cutucão macio/boop (drone_poke_chirp.wav)")]
        public AudioClip sfxDronePoke;
        [Tooltip("Alias legado para compatibilidade de chamadas existentes")]
        public AudioClip sfxDroneBoop;
        [Tooltip("Risadinha suave (drone_giggle_soft.wav)")]
        public AudioClip sfxDroneGiggle;
        [Tooltip("Groove sci-fi musical (drone_dance_groove.wav)")]
        public AudioClip sfxDroneDance;

        [Header("Calibração de Volume Confortável VR-Safe (<= 0.25f)")]
        [Range(0.05f, 0.40f)] public float purrVolume = 0.18f;
        [Range(0.05f, 0.40f)] public float pokeVolume = 0.20f;
        [Range(0.05f, 0.40f)] public float giggleVolume = 0.18f;
        [Range(0.05f, 0.40f)] public float danceVolume = 0.18f;

        [Header("Estado Atual")]
        [SerializeField] private DroneLEDExpression currentExpression = DroneLEDExpression.Normal;
        [SerializeField] private bool isPettingActive = false;
        [SerializeField] private bool isPokedActive = false;
        [SerializeField] private bool isDancingActive = false;
        [SerializeField] private bool isLaserHoveringHead = false;

        public DroneLEDExpression CurrentExpression => currentExpression;
        public bool IsPettingActive => isPettingActive;
        public bool IsPokedActive => isPokedActive;
        public bool IsDancingActive => isDancingActive;

        // Controle de Materiais e Shader IDs (Zero Garbage Collection)
        private MaterialPropertyBlock propBlock;
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        // Transform Rest Pose do Modelo Visual (Garante 100% isolamento da raiz)
        private Vector3 modelRestPos = Vector3.zero;
        private Quaternion modelRestRot = Quaternion.identity;
        private bool hasCachedRestPose = false;

        // Físicas de Oscilação Amortecida (Wobble & Squash)
        private Vector3 wobblePosOffset = Vector3.zero;
        private Vector3 wobblePosVelocity = Vector3.zero;
        private Vector3 wobbleRotOffset = Vector3.zero;
        private Vector3 wobbleRotVelocity = Vector3.zero;

        // Temporizadores internos
        private float expressionTimer = 0f;
        private float lastHapticTime = 0f;
        private float petContinuousTimer = 0f;
        private float lastRadioCheckTime = -10f;

        // Rastreamento das mãos e interatores VR
        private readonly List<Transform> trackedHands = new List<Transform>();
        private readonly Dictionary<Transform, Vector3> lastHandPositions = new Dictionary<Transform, Vector3>();
        private XRBaseController activePettingController;
        private IXRInteractor activeLaserInteractor;

        // Espectro de áudio FFT pré-alocado
        private readonly float[] spectrumSamples = new float[256];
        private float smoothedBassEnergy = 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            propBlock = new MaterialPropertyBlock();
            CacheVisualRoot();
        }

        private void Start()
        {
            CacheVisualRoot();
            AutoDetectComponents();
            SetExpression(DroneLEDExpression.Normal);
        }

        private void OnEnable()
        {
            CacheVisualRoot();
            AutoDetectComponents();
            EnsureLaserInteractables();
        }

        private void OnDisable()
        {
            if (headLaserInteractable != null)
            {
                headLaserInteractable.firstHoverEntered.RemoveListener(OnHeadHoverEntered);
                headLaserInteractable.lastHoverExited.RemoveListener(OnHeadHoverExited);
            }

            if (faceLaserInteractable != null)
            {
                faceLaserInteractable.activated.RemoveListener(OnFaceLaserActivated);
                faceLaserInteractable.selectEntered.RemoveListener(OnFaceLaserSelected);
            }

            ResetTestPose();
        }

        /// <summary>
        /// Localiza e memoriza a pose de repouso do nó visual filho ('Modelo_3D_ExoBot').
        /// Os offsets de carinho, cutucada e dança atuarão unicamente nas coordenadas locais deste nó,
        /// preservando intactos o transform raiz e o offset memorizado pelo DroneCompanionFollowAndGrab.
        /// </summary>
        private void CacheVisualRoot()
        {
            if (droneVisualRoot == null)
            {
                droneVisualRoot = transform.Find("Modelo_3D_ExoBot");
                if (droneVisualRoot == null && transform.childCount > 0)
                {
                    droneVisualRoot = transform.GetChild(0);
                }
            }

            if (droneVisualRoot != null && !hasCachedRestPose)
            {
                modelRestPos = droneVisualRoot.localPosition;
                modelRestRot = droneVisualRoot.localRotation;
                hasCachedRestPose = true;
            }
        }

        /// <summary>
        /// Localiza automaticamente materiais, fontes de áudio, rádio, partículas e interatores XR.
        /// </summary>
        public void AutoDetectComponents()
        {
            CacheVisualRoot();

            if (visorRenderer == null)
            {
                Transform visorObj = transform.Find("Modelo_3D_ExoBot/Visor_LED_Frontal");
                if (visorObj == null) visorObj = transform.Find("Visor_LED_Frontal");
                if (visorObj != null) visorRenderer = visorObj.GetComponent<MeshRenderer>();
            }

            if (headPettingTrigger == null)
            {
                Transform tPet = transform.Find("HeadPettingTrigger");
                if (tPet != null) headPettingTrigger = tPet.GetComponent<Collider>();
            }

            if (facePokeTrigger == null)
            {
                Transform tPoke = transform.Find("FacePokeTrigger");
                if (tPoke != null) facePokeTrigger = tPoke.GetComponent<Collider>();
            }

            if (heartParticles == null)
            {
                Transform pt = transform.Find("ParticleSystem_Hearts");
                if (pt != null) heartParticles = pt.GetComponent<ParticleSystem>();
            }

            EnsureAudioSourceConfig();
            EnsureAudioClips();
            AutoDetectTextures();
            LocateRadioMusicSource();
            EnsureLaserInteractables();
            LocateVRControllers();
        }

        private void EnsureAudioSourceConfig()
        {
            if (petVoiceAudioSource == null)
            {
                AudioSource[] sources = GetComponentsInChildren<AudioSource>();
                if (sources.Length > 1) petVoiceAudioSource = sources[1];
                else if (sources.Length > 0) petVoiceAudioSource = sources[0];
                else petVoiceAudioSource = gameObject.AddComponent<AudioSource>();
            }

            if (petVoiceAudioSource != null)
            {
                petVoiceAudioSource.playOnAwake = false;
                petVoiceAudioSource.spatialBlend = 1.0f; // 100% 3D VR Audio
                petVoiceAudioSource.minDistance = 0.6f;
                petVoiceAudioSource.maxDistance = 8.0f;
                petVoiceAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            }
        }

        private void EnsureAudioClips()
        {
#if UNITY_EDITOR
            string audioDir = "Assets/Audio/Generated/Task04";
            if (sfxDronePurr == null || sfxDronePurr.name == "drone_purr")
            {
                var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{audioDir}/drone_purr_happy.wav");
                if (clip != null) sfxDronePurr = clip;
            }
            if (sfxDronePoke == null || sfxDronePoke.name == "drone_boop")
            {
                var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{audioDir}/drone_poke_chirp.wav");
                if (clip != null)
                {
                    sfxDronePoke = clip;
                    sfxDroneBoop = clip;
                }
            }
            if (sfxDroneGiggle == null || sfxDroneGiggle.name == "drone_giggle")
            {
                var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{audioDir}/drone_giggle_soft.wav");
                if (clip != null) sfxDroneGiggle = clip;
            }
            if (sfxDroneDance == null)
            {
                var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{audioDir}/drone_dance_groove.wav");
                if (clip != null) sfxDroneDance = clip;
            }
#endif
            if (sfxDroneBoop == null && sfxDronePoke != null) sfxDroneBoop = sfxDronePoke;
            if (sfxDronePoke == null && sfxDroneBoop != null) sfxDronePoke = sfxDroneBoop;
        }

        private void AutoDetectTextures()
        {
#if UNITY_EDITOR
            string texDir = "Assets/Textures/Generated/Task04";
            if (texNormal == null) texNormal = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/LED_Expression_Normal.png");
            if (texHappy == null)  texHappy  = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/LED_Expression_Happy.png");
            if (texLove == null)   texLove   = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/LED_Expression_Love.png");
            if (texPoked == null)  texPoked  = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/LED_Expression_Poked.png");
            if (texDance == null)  texDance  = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/LED_Expression_Dance.png");
#endif
        }

        private void LocateRadioMusicSource()
        {
            if (musicSource != null) return;

            // Busca O(1) através do singleton do RadioMenu3D
            if (RadioMenu3D.Instancia != null && RadioMenu3D.Instancia.audioFonte != null)
            {
                musicSource = RadioMenu3D.Instancia.audioFonte;
                return;
            }

            // Fallback pontual e seguro
            GameObject radioObj = GameObject.Find("[IC] Radio_Menu_3D");
            if (radioObj != null)
            {
                musicSource = radioObj.GetComponent<AudioSource>();
            }
        }

        /// <summary>
        /// Configura componentes XRSimpleInteractable dedicados na cabeça e na face para suporte
        /// nativo ao raio laser (XRRayInteractor), sem sobreposição com o corpo principal do drone.
        /// </summary>
        public void EnsureLaserInteractables()
        {
            if (headPettingTrigger != null)
            {
                if (headLaserInteractable == null)
                {
                    headLaserInteractable = headPettingTrigger.GetComponent<XRSimpleInteractable>();
                    if (headLaserInteractable == null)
                    {
                        headLaserInteractable = headPettingTrigger.gameObject.AddComponent<XRSimpleInteractable>();
                    }
                }

                headLaserInteractable.interactionLayers = ~0;
                if (!headLaserInteractable.colliders.Contains(headPettingTrigger))
                {
                    headLaserInteractable.colliders.Clear();
                    headLaserInteractable.colliders.Add(headPettingTrigger);
                }

                headLaserInteractable.firstHoverEntered.RemoveListener(OnHeadHoverEntered);
                headLaserInteractable.lastHoverExited.RemoveListener(OnHeadHoverExited);
                headLaserInteractable.firstHoverEntered.AddListener(OnHeadHoverEntered);
                headLaserInteractable.lastHoverExited.AddListener(OnHeadHoverExited);
            }

            if (facePokeTrigger != null)
            {
                if (faceLaserInteractable == null)
                {
                    faceLaserInteractable = facePokeTrigger.GetComponent<XRSimpleInteractable>();
                    if (faceLaserInteractable == null)
                    {
                        faceLaserInteractable = facePokeTrigger.gameObject.AddComponent<XRSimpleInteractable>();
                    }
                }

                faceLaserInteractable.interactionLayers = ~0;
                if (!faceLaserInteractable.colliders.Contains(facePokeTrigger))
                {
                    faceLaserInteractable.colliders.Clear();
                    faceLaserInteractable.colliders.Add(facePokeTrigger);
                }

                faceLaserInteractable.activated.RemoveListener(OnFaceLaserActivated);
                faceLaserInteractable.selectEntered.RemoveListener(OnFaceLaserSelected);
                faceLaserInteractable.activated.AddListener(OnFaceLaserActivated);
                faceLaserInteractable.selectEntered.AddListener(OnFaceLaserSelected);
            }
        }

        private void LocateVRControllers()
        {
            trackedHands.Clear();
            lastHandPositions.Clear();

            XRBaseController[] controllers = FindObjectsByType<XRBaseController>(FindObjectsSortMode.None);
            foreach (var c in controllers)
            {
                if (c != null && !trackedHands.Contains(c.transform))
                {
                    trackedHands.Add(c.transform);
                    lastHandPositions[c.transform] = c.transform.position;
                }
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // 1. Processa carinho físico e contínuo via laser
            ProcessLaserAndProximityPetting(dt);

            // 2. Simula física de oscilação elástica amortecida (Poke squash & stretch)
            UpdateSpringPhysics(dt);

            // 3. Processa áudio FFT e dança rítmica sincronizada com o rádio
            UpdateMusicDance(dt);

            // 4. Ciclo de vida e retorno das expressões faciais LED
            UpdateExpressionLifecycle(dt);
        }

        private void LateUpdate()
        {
            // Aplica os offsets visuais EXCLUSIVAMENTE nas coordenadas locais do nó filho
            ApplyVisualOffsets();
        }

        /// <summary>
        /// Aplica os deslocamentos de carinho, recuo de mola e dança ao modelo sem conflitar com
        /// a raiz (transform.position) e preservando a rotação/posição gerenciada pelo DroneCompanionFollowAndGrab.
        /// Durante a celebração de metas (TASK-05), o DroneMilestoneCelebration assume controle prioritário da pose local.
        /// </summary>
        private void ApplyVisualOffsets()
        {
            if (DroneMilestoneCelebration.Instance != null && DroneMilestoneCelebration.Instance.IsCelebrating)
            {
                return;
            }

            if (droneVisualRoot != null)
            {
                droneVisualRoot.localPosition = modelRestPos + wobblePosOffset;
                droneVisualRoot.localRotation = modelRestRot * Quaternion.Euler(wobbleRotOffset);
            }
        }

        #region Reatividade a Carinho (Petting) - Laser & Mão Física

        private void OnHeadHoverEntered(HoverEnterEventArgs args)
        {
            isLaserHoveringHead = true;
            activeLaserInteractor = args.interactorObject;

            if (args.interactorObject is XRBaseControllerInteractor ctrlInteractor)
            {
                activePettingController = ctrlInteractor.xrController != null
                    ? ctrlInteractor.xrController
                    : ctrlInteractor.GetComponentInParent<XRBaseController>();
            }

            ApplyPettingBehavior(Time.deltaTime);
        }

        private void OnHeadHoverExited(HoverExitEventArgs args)
        {
            isLaserHoveringHead = false;
            activeLaserInteractor = null;
        }

        private void ProcessLaserAndProximityPetting(float dt)
        {
            bool pettingDetected = false;

            // A) Detecção ativa por feixe de laser pairando na cúpula
            if (isLaserHoveringHead && activeLaserInteractor != null)
            {
                pettingDetected = true;
            }

            // B) Detecção por proximidade suave da mão física (fallback ergonômico)
            if (!pettingDetected && headPettingTrigger != null)
            {
                Vector3 headCenter = headPettingTrigger.bounds.center;
                float petRadius = 0.28f;

                for (int i = 0; i < trackedHands.Count; i++)
                {
                    Transform hand = trackedHands[i];
                    if (hand == null) continue;

                    Vector3 currentPos = hand.position;
                    Vector3 lastPos = lastHandPositions.ContainsKey(hand) ? lastHandPositions[hand] : currentPos;
                    float speed = Vector3.Distance(currentPos, lastPos) / Mathf.Max(0.001f, dt);
                    lastHandPositions[hand] = currentPos;

                    float distToHead = Vector3.Distance(currentPos, headCenter);
                    if (distToHead <= petRadius)
                    {
                        // Mão próxima da cabeça se movendo suavemente = Carinho!
                        if (speed > 0.03f && speed < 1.8f)
                        {
                            pettingDetected = true;
                            var controller = hand.GetComponentInParent<XRBaseController>();
                            if (controller != null) activePettingController = controller;
                            break;
                        }
                    }
                }
            }

            if (pettingDetected)
            {
                ApplyPettingBehavior(dt);
            }
            else if (isPettingActive)
            {
                petContinuousTimer -= dt;
                if (petContinuousTimer <= 0f)
                {
                    StopPettingBehavior();
                }
            }
        }

        public void TriggerPetting(XRBaseController controller = null)
        {
            if (controller != null) activePettingController = controller;
            ApplyPettingBehavior(Time.deltaTime);
        }

        private void ApplyPettingBehavior(float dt)
        {
            isPettingActive = true;
            petContinuousTimer = 0.35f;

            // Vibração háptica contínua (purring tátil suave no controle VR)
            if (Time.time - lastHapticTime >= pettingHapticInterval)
            {
                lastHapticTime = Time.time;
                if (activePettingController != null)
                {
                    activePettingController.SendHapticImpulse(pettingHapticAmplitude, pettingHapticInterval * 0.9f);
                }
                else if (activeLaserInteractor is XRBaseControllerInteractor ctrl)
                {
                    ctrl.SendHapticImpulse(pettingHapticAmplitude, pettingHapticInterval * 0.9f);
                }
            }

            // Emite partículas de coração flutuante
            if (heartParticles != null && !heartParticles.isPlaying)
            {
                heartParticles.Play();
            }

            // Toca som suave de ronrom do Drone (volume calibrado para VR-Safe)
            if (petVoiceAudioSource != null && sfxDronePurr != null)
            {
                if (!petVoiceAudioSource.isPlaying || petVoiceAudioSource.clip != sfxDronePurr)
                {
                    petVoiceAudioSource.clip = sfxDronePurr;
                    petVoiceAudioSource.loop = true;
                    petVoiceAudioSource.pitch = UnityEngine.Random.Range(0.98f, 1.02f);
                    petVoiceAudioSource.volume = purrVolume;
                    petVoiceAudioSource.Play();
                }
            }

            // Micro-flutuação harmônica sutil (balanço de carinho)
            float petTime = Time.time * 4.5f;
            wobblePosOffset.y = Mathf.Sin(petTime) * 0.012f;
            wobbleRotOffset.z = Mathf.Sin(Time.time * 2.2f) * 2.0f;

            // Expressão amorosa no visor
            if (currentExpression != DroneLEDExpression.Love && !isPokedActive)
            {
                SetExpression(DroneLEDExpression.Love, 2.5f);
            }
        }

        private void StopPettingBehavior()
        {
            isPettingActive = false;

            if (heartParticles != null && heartParticles.isPlaying)
            {
                heartParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (petVoiceAudioSource != null && petVoiceAudioSource.clip == sfxDronePurr)
            {
                petVoiceAudioSource.Stop();
                petVoiceAudioSource.loop = false;
            }

            if (currentExpression == DroneLEDExpression.Love)
            {
                SetExpression(DroneLEDExpression.Happy, 1.5f);
            }
        }

        #endregion

        #region Reatividade a Cutucão (Poke / Boop) - Laser & Mão Física

        private void OnFaceLaserActivated(ActivateEventArgs args)
        {
            HandleFaceLaserTrigger(args.interactorObject);
        }

        private void OnFaceLaserSelected(SelectEnterEventArgs args)
        {
            HandleFaceLaserTrigger(args.interactorObject);
        }

        private void HandleFaceLaserTrigger(IXRInteractor interactor)
        {
            Vector3 contact = facePokeTrigger != null ? facePokeTrigger.bounds.center : transform.position;
            Vector3 dir = (contact - transform.position).normalized;
            if (dir.sqrMagnitude < 0.01f) dir = -transform.forward;

            XRBaseController ctrl = null;
            if (interactor is XRBaseControllerInteractor ctrlInteractor)
            {
                ctrl = ctrlInteractor.xrController != null
                    ? ctrlInteractor.xrController
                    : ctrlInteractor.GetComponentInParent<XRBaseController>();
            }

            TriggerPoke(contact, dir * 1.5f, ctrl);
        }

        public void TriggerPoke(Vector3 contactWorldPos, Vector3 impactVelocity, XRBaseController controller = null)
        {
            isPokedActive = true;
            expressionTimer = 2.4f;

            // Pulso háptico de impacto na mão do paciente
            if (controller != null)
            {
                controller.SendHapticImpulse(0.25f, 0.12f);
            }

            // Interrompe carinho se houver
            StopPettingBehavior();

            // Física de recuo e mola elástica (atua estritamente nas variáveis locais de wobble)
            float recoilMagnitude = Mathf.Clamp(impactVelocity.magnitude * 0.04f, 0.035f, 0.075f);
            wobblePosVelocity += new Vector3(
                UnityEngine.Random.Range(-0.02f, 0.02f),
                UnityEngine.Random.Range(-0.015f, 0.015f),
                -recoilMagnitude
            );

            wobbleRotVelocity += new Vector3(
                UnityEngine.Random.Range(-pokeWobbleAngle, -pokeWobbleAngle * 0.5f),
                UnityEngine.Random.Range(-pokeWobbleAngle, pokeWobbleAngle),
                UnityEngine.Random.Range(-pokeWobbleAngle * 0.5f, pokeWobbleAngle * 0.5f)
            );

            // Sons de Boop e Risadinha (calibrados para VR-Safe)
            if (petVoiceAudioSource != null)
            {
                petVoiceAudioSource.Stop();
                petVoiceAudioSource.loop = false;
                petVoiceAudioSource.pitch = UnityEngine.Random.Range(0.96f, 1.04f);

                AudioClip clip = sfxDronePoke != null ? sfxDronePoke : sfxDroneBoop;
                if (clip != null)
                {
                    petVoiceAudioSource.PlayOneShot(clip, pokeVolume);
                }
            }

            // Expressão tonta (@_@)
            SetExpression(DroneLEDExpression.Poked, 2.4f);

            // Agenda risadinha suave subsequente
            CancelInvoke(nameof(PlayGiggleAfterBoop));
            Invoke(nameof(PlayGiggleAfterBoop), 0.35f);
        }

        private void PlayGiggleAfterBoop()
        {
            if (petVoiceAudioSource != null && sfxDroneGiggle != null)
            {
                petVoiceAudioSource.pitch = UnityEngine.Random.Range(0.98f, 1.04f);
                petVoiceAudioSource.PlayOneShot(sfxDroneGiggle, giggleVolume);
            }
        }

        private void UpdateSpringPhysics(float dt)
        {
            // Sistema de mola-amortecedor para posição local
            Vector3 springForcePos = -pokeSpringStiffness * wobblePosOffset;
            Vector3 dampingForcePos = -pokeSpringDamping * wobblePosVelocity;
            wobblePosVelocity += (springForcePos + dampingForcePos) * dt;
            wobblePosOffset += wobblePosVelocity * dt;

            // Sistema de mola-amortecedor para rotação local
            Vector3 springForceRot = -pokeSpringStiffness * wobbleRotOffset;
            Vector3 dampingForceRot = -pokeSpringDamping * wobbleRotVelocity;
            wobbleRotVelocity += (springForceRot + dampingForceRot) * dt;
            wobbleRotOffset += wobbleRotVelocity * dt;

            // Limites de repouso para desligar cálculo quando estável
            if (wobblePosOffset.sqrMagnitude < 0.00001f && wobblePosVelocity.sqrMagnitude < 0.0001f)
            {
                wobblePosOffset = Vector3.zero;
                wobblePosVelocity = Vector3.zero;
            }
            if (wobbleRotOffset.sqrMagnitude < 0.001f && wobbleRotVelocity.sqrMagnitude < 0.01f)
            {
                wobbleRotOffset = Vector3.zero;
                wobbleRotVelocity = Vector3.zero;
            }
        }

        #endregion

        #region Dança Rítmica Musical (Zero GameObject.Find em Update)

        private void UpdateMusicDance(float dt)
        {
            if (!enableMusicDance || isPettingActive || isPokedActive) return;

            // Busca de rádio controlada por temporizador (Zero chamadas per-frame na cena vazia)
            if (musicSource == null)
            {
                if (Time.time - lastRadioCheckTime > 3.0f)
                {
                    lastRadioCheckTime = Time.time;
                    LocateRadioMusicSource();
                }
                if (musicSource == null) return;
            }

            bool musicPlaying = (musicSource != null && musicSource.isPlaying && musicSource.volume > 0.05f);

            if (musicPlaying)
            {
                musicSource.GetSpectrumData(spectrumSamples, 0, FFTWindow.BlackmanHarris);

                // Acumula faixas de graves e kick (50Hz - 220Hz, bins 1 a 7)
                float bassEnergy = 0f;
                for (int i = 1; i < 8; i++)
                {
                    bassEnergy += spectrumSamples[i];
                }

                smoothedBassEnergy = Mathf.Lerp(smoothedBassEnergy, bassEnergy, dt * 10f);

                if (smoothedBassEnergy > danceBeatSensitivity)
                {
                    isDancingActive = true;

                    // Balanço lateral e saltinho no ritmo da batida (apenas no nó filho visual)
                    float tempo = Time.time * 3.8f;
                    float roll = Mathf.Sin(tempo) * danceTiltAngle;
                    float yaw = Mathf.Cos(tempo * 0.5f) * (danceTiltAngle * 0.6f);
                    float bounce = Mathf.Abs(Mathf.Sin(tempo * 2.0f)) * danceBounceHeight;

                    wobbleRotOffset.z = Mathf.Lerp(wobbleRotOffset.z, roll, dt * 8f);
                    wobbleRotOffset.y = Mathf.Lerp(wobbleRotOffset.y, yaw, dt * 8f);
                    wobblePosOffset.y = Mathf.Lerp(wobblePosOffset.y, bounce, dt * 8f);

                    if (currentExpression != DroneLEDExpression.Dance && expressionTimer <= 0f)
                    {
                        SetExpression(DroneLEDExpression.Dance, 0f);
                    }
                }
                else if (isDancingActive)
                {
                    isDancingActive = false;
                    wobbleRotOffset.z = Mathf.Lerp(wobbleRotOffset.z, 0f, dt * 6f);
                    wobbleRotOffset.y = Mathf.Lerp(wobbleRotOffset.y, 0f, dt * 6f);
                    wobblePosOffset.y = Mathf.Lerp(wobblePosOffset.y, 0f, dt * 6f);

                    if (currentExpression == DroneLEDExpression.Dance)
                    {
                        SetExpression(DroneLEDExpression.Normal, 0f);
                    }
                }
            }
            else if (isDancingActive)
            {
                isDancingActive = false;
                wobbleRotOffset.z = Mathf.Lerp(wobbleRotOffset.z, 0f, dt * 6f);
                wobbleRotOffset.y = Mathf.Lerp(wobbleRotOffset.y, 0f, dt * 6f);
                wobblePosOffset.y = Mathf.Lerp(wobblePosOffset.y, 0f, dt * 6f);

                if (currentExpression == DroneLEDExpression.Dance)
                {
                    SetExpression(DroneLEDExpression.Normal, 0f);
                }
            }
        }

        #endregion

        #region Gerenciamento de Expressões LED (100% MaterialPropertyBlock - Zero GC / Zero Clonagem)

        public void SetExpression(DroneLEDExpression expression, float duration = 0f)
        {
            currentExpression = expression;
            expressionTimer = duration;

            Texture2D targetTex = GetTextureForExpression(expression);
            ApplyTextureToVisor(targetTex);
        }

        private Texture2D GetTextureForExpression(DroneLEDExpression expr)
        {
            switch (expr)
            {
                case DroneLEDExpression.Happy: return texHappy != null ? texHappy : texNormal;
                case DroneLEDExpression.Love:  return texLove  != null ? texLove  : texNormal;
                case DroneLEDExpression.Poked: return texPoked != null ? texPoked : texNormal;
                case DroneLEDExpression.Dance: return texDance != null ? texDance : texNormal;
                case DroneLEDExpression.Normal:
                default:
                    return texNormal;
            }
        }

        /// <summary>
        /// Aplica texturas e propriedades de emissão EXCLUSIVAMENTE via MaterialPropertyBlock.
        /// ELIMINA 100% qualquer acesso a 'visorRenderer.material', preservando o SRP Batcher,
        /// permitindo GPU Instancing e evitando vazamentos na heap em VR.
        /// </summary>
        private void ApplyTextureToVisor(Texture2D tex)
        {
            if (visorRenderer == null || tex == null) return;

            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            visorRenderer.GetPropertyBlock(propBlock);

            propBlock.SetTexture(MainTexId, tex);
            propBlock.SetTexture(BaseMapId, tex);
            propBlock.SetTexture(EmissionMapId, tex);

            Color finalEmissive = ledEmissiveColor * Mathf.LinearToGammaSpace(ledEmissiveIntensity);
            propBlock.SetColor(EmissionColorId, finalEmissive);

            visorRenderer.SetPropertyBlock(propBlock);
        }

        private void UpdateExpressionLifecycle(float dt)
        {
            if (DroneMilestoneCelebration.Instance != null && DroneMilestoneCelebration.Instance.IsCelebrating)
            {
                return;
            }

            if (expressionTimer > 0f)
            {
                expressionTimer -= dt;
                if (expressionTimer <= 0f)
                {
                    isPokedActive = false;
                    if (isPettingActive)
                    {
                        SetExpression(DroneLEDExpression.Love, 1.5f);
                    }
                    else if (isDancingActive)
                    {
                        SetExpression(DroneLEDExpression.Dance);
                    }
                    else
                    {
                        SetExpression(DroneLEDExpression.Normal);
                    }
                }
            }
        }

        #endregion

        #region Detecção por Triggers Físicos (Contato Direto com Mãos VR)

        private void OnTriggerEnter(Collider other)
        {
            ProcessTriggerInteraction(other, isEntering: true);
        }

        private void OnTriggerStay(Collider other)
        {
            ProcessTriggerInteraction(other, isEntering: false);
        }

        private void ProcessTriggerInteraction(Collider other, bool isEntering)
        {
            if (other == null) return;

            // Ignora colisores internos do próprio drone
            if (other.transform.IsChildOf(transform)) return;

            // Verifica se pertence ao jogador/interactor XR
            var controller = other.GetComponentInParent<XRBaseController>();
            bool isHand = (controller != null) ||
                          other.name.IndexOf("Hand", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          other.name.IndexOf("Controller", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          other.name.IndexOf("Interactor", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!isHand) return;

            // Distingue entre carinho no topo ou cutucão na face
            Vector3 contact = other.ClosestPoint(transform.position);

            if (headPettingTrigger != null && headPettingTrigger.bounds.Contains(contact))
            {
                ApplyPettingBehavior(Time.deltaTime);
            }
            else if (isEntering && facePokeTrigger != null && facePokeTrigger.bounds.Contains(contact))
            {
                Vector3 vel = (contact - transform.position).normalized * 1.5f;
                TriggerPoke(contact, vel, controller);
            }
        }

        #endregion

        #region Métodos de Teste e Validação Automática (Visual QA)

        public void TestPettingPose()
        {
            SetExpression(DroneLEDExpression.Love, 5.0f);
            if (heartParticles != null)
            {
                heartParticles.Play();
            }
            wobbleRotOffset = new Vector3(-4f, 6f, 3f);
            wobblePosOffset = new Vector3(0.01f, 0.02f, -0.01f);
            ApplyVisualOffsets();
        }

        public void TestPokedPose()
        {
            SetExpression(DroneLEDExpression.Poked, 5.0f);
            wobbleRotOffset = new Vector3(pokeWobbleAngle, -pokeWobbleAngle * 0.8f, -4f);
            wobblePosOffset = new Vector3(0f, -0.015f, pokeRecoilZ);
            ApplyVisualOffsets();
        }

        public void TestDancingPose()
        {
            SetExpression(DroneLEDExpression.Dance, 5.0f);
            wobbleRotOffset = new Vector3(-3f, 8f, danceTiltAngle);
            wobblePosOffset = new Vector3(0.02f, danceBounceHeight, 0f);
            ApplyVisualOffsets();
        }

        public void ResetTestPose()
        {
            SetExpression(DroneLEDExpression.Normal);
            wobbleRotOffset = Vector3.zero;
            wobblePosOffset = Vector3.zero;
            wobblePosVelocity = Vector3.zero;
            wobbleRotVelocity = Vector3.zero;
            isPettingActive = false;
            isPokedActive = false;
            isDancingActive = false;
            isLaserHoveringHead = false;
            ApplyVisualOffsets();

            if (heartParticles != null)
            {
                heartParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (petVoiceAudioSource != null && petVoiceAudioSource.isPlaying && petVoiceAudioSource.clip == sfxDronePurr)
            {
                petVoiceAudioSource.Stop();
                petVoiceAudioSource.loop = false;
            }
        }

        #endregion
    }
}
