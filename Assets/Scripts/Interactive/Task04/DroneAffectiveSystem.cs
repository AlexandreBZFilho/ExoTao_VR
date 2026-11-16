using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace VRRehab.DronePet
{
    /// <summary>Expressões faciais disponíveis no visor LED do Drone Pet.</summary>
    public enum DroneLEDExpression
    {
        Normal,
        Happy,
        Love,
        Poked,
        Dance
    }

    /// <summary>
    /// Sistema afetivo do Drone Pet:
    /// - CARINHO com o CONTROLE: aproxime o controle da cabeça (topo) do drone e faça movimentos suaves de esfregar.
    ///   O drone mostra ♥‿♥, solta corações, ronrona baixinho e o controle vibra. (Dica: pince o drone com o laser,
    ///   traga-o para perto, solte, e faça carinho com a mão.)
    /// - CUTUCÃO: encostar o controle com rapidez na face/visor -> recuo elástico, "boop", risadinha e expressão @_@.
    /// - DANÇA: balança no ritmo dos graves do rádio do menu.
    /// Toda a animação é aplicada SÓ no nó visual filho (Modelo_3D_ExoBot); a raiz é do DroneCompanionFollowAndGrab.
    /// O visor troca de textura via MaterialPropertyBlock (zero clone de material).
    /// </summary>
    [DisallowMultipleComponent]
    public class DroneAffectiveSystem : MonoBehaviour
    {
        public static DroneAffectiveSystem Instance { get; private set; }

        [Header("Nó Visual Filho")]
        public Transform droneVisualRoot;

        [Header("Visor Frontal LED")]
        public MeshRenderer visorRenderer;
        public Texture2D texNormal;
        public Texture2D texHappy;
        public Texture2D texLove;
        public Texture2D texPoked;
        public Texture2D texDance;
        public Color ledEmissiveColor = new Color(0.0f, 0.95f, 1.0f);
        public float ledEmissiveIntensity = 2.8f;

        [Header("Carinho com o Controle")]
        [Tooltip("Raio (m) em volta do topo da cabeça em que a mão conta como carinho")]
        public float petRadius = 0.26f;
        [Tooltip("Velocidade mínima/máxima da mão (m/s) para contar como esfregar (parada não conta, tapa não conta)")]
        public float petMinHandSpeed = 0.04f;
        public float petMaxHandSpeed = 2.2f;
        [Tooltip("Tempo de esfregar (s) até o drone reagir")]
        public float petActivationTime = 0.15f;
        public ParticleSystem heartParticles;
        [Range(0.01f, 1.0f)] public float pettingHapticAmplitude = 0.20f;
        public float pettingHapticInterval = 0.05f;

        [Header("Cutucão no Visor")]
        public float pokeRadius = 0.18f;
        [Tooltip("Velocidade mínima (m/s) da mão indo em direção ao visor")]
        public float pokeMinApproachSpeed = 0.35f;
        public float pokeCooldown = 0.8f;
        public float pokeWobbleAngle = 9.0f;
        public float pokeSpringStiffness = 320.0f;
        public float pokeSpringDamping = 16.0f;

        [Header("Dança Rítmica Musical")]
        public AudioSource musicSource;
        public bool enableMusicDance = true;
        public float danceTiltAngle = 6.5f;
        public float danceBounceHeight = 0.022f;
        public float danceBeatSensitivity = 0.035f;

        [Header("Áudio (volumes confortáveis <= 0.25)")]
        public AudioClip sfxDronePurr;
        public AudioClip sfxDronePoke;
        public AudioClip sfxDroneGiggle;
        [Range(0.05f, 0.40f)] public float purrVolume = 0.18f;
        [Range(0.05f, 0.40f)] public float pokeVolume = 0.20f;
        [Range(0.05f, 0.40f)] public float giggleVolume = 0.18f;

        [Header("Estado Atual")]
        [SerializeField] private DroneLEDExpression currentExpression = DroneLEDExpression.Normal;
        [SerializeField] private bool isPettingActive;
        [SerializeField] private bool isPokedActive;
        [SerializeField] private bool isDancingActive;

        public DroneLEDExpression CurrentExpression => currentExpression;
        public bool IsPettingActive => isPettingActive;
        public bool IsPokedActive => isPokedActive;
        public bool IsDancingActive => isDancingActive;

        private MaterialPropertyBlock propBlock;
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private Vector3 modelRestPos;
        private Quaternion modelRestRot = Quaternion.identity;
        private bool hasCachedRestPose;

        private Vector3 wobblePosOffset, wobblePosVelocity, wobbleRotOffset, wobbleRotVelocity;

        private float expressionTimer;
        private float lastHapticTime;
        private float petAccumulated;
        private float petReleaseTimer;
        private float lastPokeTime = -10f;
        private float lastRadioCheckTime = -10f;
        private float nextControllerScanTime;

        private AudioSource voiceSource;
        private BoxCollider bodyCollider;
        private XRBaseController[] controllers = new XRBaseController[0];
        private Vector3[] lastHandPositions = new Vector3[0];
        private XRBaseController pettingController;

        private readonly float[] spectrumSamples = new float[256];
        private float smoothedBassEnergy;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            propBlock = new MaterialPropertyBlock();
            bodyCollider = GetComponent<BoxCollider>();
            CacheVisualRoot();
            AutoDetectComponents();

            // Fonte de áudio dedicada (não disputa com a voz/hover do DroneGuiaIA)
            voiceSource = gameObject.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            voiceSource.spatialBlend = 1f;
            voiceSource.dopplerLevel = 0f;
            voiceSource.minDistance = 0.6f;
            voiceSource.maxDistance = 8f;
            voiceSource.rolloffMode = AudioRolloffMode.Logarithmic;

            DisableLegacyLaserTriggers();
        }

        private void Start()
        {
            SetExpression(DroneLEDExpression.Normal);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnDisable()
        {
            ResetTestPose();
        }

        private void CacheVisualRoot()
        {
            if (droneVisualRoot == null) droneVisualRoot = transform.Find("Modelo_3D_ExoBot");
            if (droneVisualRoot != null && !hasCachedRestPose)
            {
                modelRestPos = droneVisualRoot.localPosition;
                modelRestRot = droneVisualRoot.localRotation;
                hasCachedRestPose = true;
            }
        }

        public void AutoDetectComponents()
        {
            CacheVisualRoot();

            if (visorRenderer == null)
            {
                Transform v = transform.Find("Modelo_3D_ExoBot/Visor_LED_Frontal");
                if (v != null) visorRenderer = v.GetComponent<MeshRenderer>();
            }
            if (heartParticles == null)
            {
                Transform pt = transform.Find("ParticleSystem_Hearts");
                if (pt != null) heartParticles = pt.GetComponent<ParticleSystem>();
            }
#if UNITY_EDITOR
            const string audioDir = "Assets/Audio/Generated/Task04";
            if (sfxDronePurr == null) sfxDronePurr = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{audioDir}/drone_purr_happy.wav");
            if (sfxDronePoke == null) sfxDronePoke = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{audioDir}/drone_poke_chirp.wav");
            if (sfxDroneGiggle == null) sfxDroneGiggle = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"{audioDir}/drone_giggle_soft.wav");

            const string texDir = "Assets/Textures/Generated/Task04";
            if (texNormal == null) texNormal = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/LED_Expression_Normal.png");
            if (texHappy == null) texHappy = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/LED_Expression_Happy.png");
            if (texLove == null) texLove = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/LED_Expression_Love.png");
            if (texPoked == null) texPoked = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/LED_Expression_Poked.png");
            if (texDance == null) texDance = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/LED_Expression_Dance.png");
#endif
            LocateRadioMusicSource();
        }

        /// <summary>
        /// A versão antiga fazia carinho/cutucão apontando o laser (XRSimpleInteractable em triggers filhos).
        /// Isso disparava carinho toda vez que o jogador mirava para pegar o drone. Agora é só com o controle físico.
        /// </summary>
        private void DisableLegacyLaserTriggers()
        {
            foreach (string nome in new[] { "HeadPettingTrigger", "FacePokeTrigger" })
            {
                Transform t = transform.Find(nome);
                if (t == null) continue;
                foreach (var simple in t.GetComponents<XRSimpleInteractable>()) Destroy(simple);
                t.gameObject.SetActive(false);
            }
        }

        private void LocateRadioMusicSource()
        {
            if (musicSource != null) return;
            if (RadioMenu3D.Instancia != null && RadioMenu3D.Instancia.audioFonte != null)
                musicSource = RadioMenu3D.Instancia.audioFonte;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            ProcessControllerTouch(dt);
            UpdateSpringPhysics(dt);
            UpdateMusicDance(dt);
            UpdateExpressionLifecycle(dt);
        }

        private void LateUpdate()
        {
            if (DroneMilestoneCelebration.Instance != null && DroneMilestoneCelebration.Instance.IsCelebrating) return;
            if (droneVisualRoot == null) return;
            droneVisualRoot.localPosition = modelRestPos + wobblePosOffset;
            droneVisualRoot.localRotation = modelRestRot * Quaternion.Euler(wobbleRotOffset);
        }

        // =====================================================================
        // DETECÇÃO DE CARINHO / CUTUCÃO PELO CONTROLE
        // =====================================================================

        private void RefreshControllers()
        {
            nextControllerScanTime = Time.time + 2f;
            XRBaseController[] found = FindObjectsByType<XRBaseController>(FindObjectsSortMode.None);
            if (found.Length == controllers.Length) return;
            controllers = found;
            lastHandPositions = new Vector3[found.Length];
            for (int i = 0; i < found.Length; i++) lastHandPositions[i] = found[i].transform.position;
        }

        private void ProcessControllerTouch(float dt)
        {
            if (controllers.Length == 0 || Time.time >= nextControllerScanTime) RefreshControllers();
            if (controllers.Length == 0) return;

            Bounds b = bodyCollider != null ? bodyCollider.bounds : new Bounds(transform.position, Vector3.one * 0.6f);
            Vector3 headTop = new Vector3(b.center.x, b.max.y - 0.05f, b.center.z);
            Vector3 facePoint = b.center + transform.forward * Mathf.Min(b.extents.z, b.extents.x);

            bool rubbing = false;
            for (int i = 0; i < controllers.Length; i++)
            {
                XRBaseController c = controllers[i];
                if (c == null || !c.isActiveAndEnabled) continue;

                Vector3 pos = c.transform.position;
                Vector3 vel = (pos - lastHandPositions[i]) / dt;
                lastHandPositions[i] = pos;
                float speed = vel.magnitude;

                // Carinho: mão no topo da cabeça, se mexendo suavemente
                if ((pos - headTop).sqrMagnitude <= petRadius * petRadius && pos.y >= b.center.y &&
                    speed >= petMinHandSpeed && speed <= petMaxHandSpeed)
                {
                    rubbing = true;
                    pettingController = c;
                }

                // Cutucão: mão chegando rápido no visor
                if (!isPettingActive && Time.time - lastPokeTime > pokeCooldown &&
                    (pos - facePoint).sqrMagnitude <= pokeRadius * pokeRadius &&
                    Vector3.Dot(vel, -transform.forward) > pokeMinApproachSpeed)
                {
                    lastPokeTime = Time.time;
                    TriggerPoke(pos, vel, c);
                }
            }

            if (rubbing)
            {
                petAccumulated += dt;
                petReleaseTimer = 0.4f;
                if (petAccumulated >= petActivationTime) ApplyPettingBehavior();
            }
            else
            {
                petAccumulated = Mathf.Max(0f, petAccumulated - dt * 0.5f);
                if (isPettingActive)
                {
                    petReleaseTimer -= dt;
                    if (petReleaseTimer <= 0f) StopPettingBehavior();
                }
            }
        }

        /// <summary>Força o carinho (ex.: botão de teste).</summary>
        public void TriggerPetting(XRBaseController controller = null)
        {
            if (controller != null) pettingController = controller;
            petReleaseTimer = 0.4f;
            ApplyPettingBehavior();
        }

        private void ApplyPettingBehavior()
        {
            isPettingActive = true;

            if (pettingController != null && Time.time - lastHapticTime >= pettingHapticInterval)
            {
                lastHapticTime = Time.time;
                pettingController.SendHapticImpulse(pettingHapticAmplitude, pettingHapticInterval * 0.9f);
            }

            if (heartParticles != null && !heartParticles.isEmitting) heartParticles.Play();

            if (voiceSource != null && sfxDronePurr != null && (!voiceSource.isPlaying || voiceSource.clip != sfxDronePurr))
            {
                voiceSource.clip = sfxDronePurr;
                voiceSource.loop = true;
                voiceSource.pitch = Random.Range(0.98f, 1.02f);
                voiceSource.volume = purrVolume;
                voiceSource.Play();
            }

            // balanço suave de "gostando do carinho"
            wobblePosOffset.y = Mathf.Sin(Time.time * 4.5f) * 0.012f;
            wobbleRotOffset.z = Mathf.Sin(Time.time * 2.2f) * 4.0f;

            if (currentExpression != DroneLEDExpression.Love && !isPokedActive)
                SetExpression(DroneLEDExpression.Love, 2.5f);
        }

        private void StopPettingBehavior()
        {
            isPettingActive = false;
            petAccumulated = 0f;

            if (heartParticles != null && heartParticles.isEmitting)
                heartParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            if (voiceSource != null && voiceSource.clip == sfxDronePurr)
            {
                voiceSource.Stop();
                voiceSource.loop = false;
            }

            if (currentExpression == DroneLEDExpression.Love) SetExpression(DroneLEDExpression.Happy, 1.5f);
        }

        public void TriggerPoke(Vector3 contactWorldPos, Vector3 impactVelocity, XRBaseController controller = null)
        {
            isPokedActive = true;
            expressionTimer = 2.4f;

            if (controller != null) controller.SendHapticImpulse(0.25f, 0.12f);
            if (isPettingActive) StopPettingBehavior();

            float recoil = Mathf.Clamp(impactVelocity.magnitude * 0.04f, 0.035f, 0.075f);
            wobblePosVelocity += new Vector3(Random.Range(-0.02f, 0.02f), Random.Range(-0.015f, 0.015f), -recoil);
            wobbleRotVelocity += new Vector3(
                Random.Range(-pokeWobbleAngle, -pokeWobbleAngle * 0.5f),
                Random.Range(-pokeWobbleAngle, pokeWobbleAngle),
                Random.Range(-pokeWobbleAngle * 0.5f, pokeWobbleAngle * 0.5f));

            if (voiceSource != null)
            {
                voiceSource.Stop();
                voiceSource.loop = false;
                voiceSource.pitch = Random.Range(0.96f, 1.04f);
                if (sfxDronePoke != null) voiceSource.PlayOneShot(sfxDronePoke, pokeVolume);
            }

            SetExpression(DroneLEDExpression.Poked, 2.4f);
            CancelInvoke(nameof(PlayGiggleAfterBoop));
            Invoke(nameof(PlayGiggleAfterBoop), 0.35f);
        }

        /// <summary>Balanço de alegria no nó visual (ex.: ao ganhar a coroa). Não mexe na raiz/posição do drone.</summary>
        public void Wiggle(float strength = 1f)
        {
            float a = pokeWobbleAngle * 1.6f * Mathf.Clamp01(strength);
            wobbleRotVelocity += new Vector3(0f, (Random.value < 0.5f ? -1f : 1f) * a * 9f, a * 4f);
            wobblePosVelocity += new Vector3(0f, 0.12f * strength, 0f);
        }

        private void PlayGiggleAfterBoop()
        {
            if (voiceSource != null && sfxDroneGiggle != null)
            {
                voiceSource.pitch = Random.Range(0.98f, 1.04f);
                voiceSource.PlayOneShot(sfxDroneGiggle, giggleVolume);
            }
        }

        private void UpdateSpringPhysics(float dt)
        {
            wobblePosVelocity += (-pokeSpringStiffness * wobblePosOffset - pokeSpringDamping * wobblePosVelocity) * dt;
            wobblePosOffset += wobblePosVelocity * dt;
            wobbleRotVelocity += (-pokeSpringStiffness * wobbleRotOffset - pokeSpringDamping * wobbleRotVelocity) * dt;
            wobbleRotOffset += wobbleRotVelocity * dt;

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

        // =====================================================================
        // DANÇA COM O RÁDIO
        // =====================================================================

        private void UpdateMusicDance(float dt)
        {
            if (!enableMusicDance || isPettingActive || isPokedActive) return;

            if (musicSource == null)
            {
                if (Time.time - lastRadioCheckTime > 3.0f)
                {
                    lastRadioCheckTime = Time.time;
                    LocateRadioMusicSource();
                }
                if (musicSource == null) return;
            }

            bool beat = false;
            if (musicSource.isPlaying && musicSource.volume > 0.05f)
            {
                musicSource.GetSpectrumData(spectrumSamples, 0, FFTWindow.BlackmanHarris);
                float bass = 0f;
                for (int i = 1; i < 8; i++) bass += spectrumSamples[i];
                smoothedBassEnergy = Mathf.Lerp(smoothedBassEnergy, bass, dt * 10f);
                beat = smoothedBassEnergy > danceBeatSensitivity;
            }

            if (beat)
            {
                isDancingActive = true;
                float tempo = Time.time * 3.8f;
                wobbleRotOffset.z = Mathf.Lerp(wobbleRotOffset.z, Mathf.Sin(tempo) * danceTiltAngle, dt * 8f);
                wobbleRotOffset.y = Mathf.Lerp(wobbleRotOffset.y, Mathf.Cos(tempo * 0.5f) * danceTiltAngle * 0.6f, dt * 8f);
                wobblePosOffset.y = Mathf.Lerp(wobblePosOffset.y, Mathf.Abs(Mathf.Sin(tempo * 2f)) * danceBounceHeight, dt * 8f);
                if (currentExpression != DroneLEDExpression.Dance && expressionTimer <= 0f) SetExpression(DroneLEDExpression.Dance);
            }
            else if (isDancingActive)
            {
                isDancingActive = false;
                if (currentExpression == DroneLEDExpression.Dance) SetExpression(DroneLEDExpression.Normal);
            }
        }

        // =====================================================================
        // EXPRESSÕES LED (MaterialPropertyBlock)
        // =====================================================================

        public void SetExpression(DroneLEDExpression expression, float duration = 0f)
        {
            currentExpression = expression;
            expressionTimer = duration;
            ApplyTextureToVisor(GetTextureForExpression(expression));
        }

        private Texture2D GetTextureForExpression(DroneLEDExpression expr)
        {
            switch (expr)
            {
                case DroneLEDExpression.Happy: return texHappy != null ? texHappy : texNormal;
                case DroneLEDExpression.Love: return texLove != null ? texLove : texNormal;
                case DroneLEDExpression.Poked: return texPoked != null ? texPoked : texNormal;
                case DroneLEDExpression.Dance: return texDance != null ? texDance : texNormal;
                default: return texNormal;
            }
        }

        private void ApplyTextureToVisor(Texture2D tex)
        {
            if (visorRenderer == null || tex == null) return;
            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            visorRenderer.GetPropertyBlock(propBlock);
            propBlock.SetTexture(MainTexId, tex);
            propBlock.SetTexture(BaseMapId, tex);
            propBlock.SetTexture(EmissionMapId, tex);
            propBlock.SetColor(EmissionColorId, ledEmissiveColor * Mathf.LinearToGammaSpace(ledEmissiveIntensity));
            visorRenderer.SetPropertyBlock(propBlock);
        }

        private void UpdateExpressionLifecycle(float dt)
        {
            if (DroneMilestoneCelebration.Instance != null && DroneMilestoneCelebration.Instance.IsCelebrating) return;
            if (expressionTimer <= 0f) return;

            expressionTimer -= dt;
            if (expressionTimer > 0f) return;

            isPokedActive = false;
            if (isPettingActive) SetExpression(DroneLEDExpression.Love, 1.5f);
            else if (isDancingActive) SetExpression(DroneLEDExpression.Dance);
            else SetExpression(DroneLEDExpression.Normal);
        }

        // =====================================================================
        // TESTES (Visual QA)
        // =====================================================================

        [ContextMenu("Teste - Carinho")]
        public void TestPettingPose()
        {
            SetExpression(DroneLEDExpression.Love, 5.0f);
            if (heartParticles != null) heartParticles.Play();
            wobbleRotOffset = new Vector3(-4f, 6f, 3f);
            wobblePosOffset = new Vector3(0.01f, 0.02f, -0.01f);
        }

        [ContextMenu("Teste - Cutucão")]
        public void TestPokedPose() => TriggerPoke(transform.position + transform.forward * 0.3f, -transform.forward * 1.5f);

        public void ResetTestPose()
        {
            SetExpression(DroneLEDExpression.Normal);
            wobbleRotOffset = wobblePosOffset = wobblePosVelocity = wobbleRotVelocity = Vector3.zero;
            isPettingActive = isPokedActive = isDancingActive = false;
            petAccumulated = 0f;
            if (droneVisualRoot != null && hasCachedRestPose)
            {
                droneVisualRoot.localPosition = modelRestPos;
                droneVisualRoot.localRotation = modelRestRot;
            }
            if (heartParticles != null) heartParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (voiceSource != null && voiceSource.clip == sfxDronePurr) voiceSource.Stop();
        }
    }
}
