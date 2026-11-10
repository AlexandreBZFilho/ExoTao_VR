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
    /// - Reatividade a Carinho (Petting): Vibração háptica de ronrom contínuo no controle VR,
    ///   emissão de corações holográficos flutuantes, som purr e expressão de amor.
    /// - Reatividade a Cutucão (Poke/Boop): Simulação de mola física amortecida (squash & stretch),
    ///   sons elásticos (boop + risadinha) e expressão tonta (@_@).
    /// - Dança Rítmica Musical: Análise de espectro FFT da música do rádio do menu,
    ///   balanço sincronizado com a batida e expressão musical (♪‿♪).
    /// </summary>
    [DisallowMultipleComponent]
    public class DroneAffectiveSystem : MonoBehaviour
    {
        public static DroneAffectiveSystem Instance { get; private set; }

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
        public float ledEmissiveIntensity = 2.5f;

        [Header("Reatividade a Carinho (Petting)")]
        [Tooltip("Trigger na calota superior da cabeça do drone")]
        public Collider headPettingTrigger;
        [Tooltip("Emissor de corações holográficos")]
        public ParticleSystem heartParticles;
        public AudioClip sfxDronePurr;
        [Range(0.01f, 1.0f)] public float pettingHapticAmplitude = 0.22f;
        public float pettingHapticInterval = 0.05f;

        [Header("Reatividade a Cutucão (Poke / Boop)")]
        [Tooltip("Trigger no visor/nariz frontal")]
        public Collider facePokeTrigger;
        public AudioClip sfxDroneBoop;
        public AudioClip sfxDroneGiggle;
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

        [Header("Fonte de Áudio do Pet")]
        public AudioSource petVoiceAudioSource;

        [Header("Estado Atual")]
        [SerializeField] private DroneLEDExpression currentExpression = DroneLEDExpression.Normal;
        [SerializeField] private bool isPettingActive = false;
        [SerializeField] private bool isPokedActive = false;
        [SerializeField] private bool isDancingActive = false;

        // Controle interno de tempo e mola
        private MaterialPropertyBlock propBlock;
        private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        private Transform modelTransform;
        private Vector3 modelRestPos;
        private Quaternion modelRestRot;

        private Vector3 wobblePosOffset = Vector3.zero;
        private Vector3 wobblePosVelocity = Vector3.zero;
        private Vector3 wobbleRotOffset = Vector3.zero;
        private Vector3 wobbleRotVelocity = Vector3.zero;

        private float expressionTimer = 0f;
        private float lastHapticTime = 0f;
        private float petContinuousTimer = 0f;

        // Rastreamento das mãos VR
        private readonly List<Transform> trackedHands = new List<Transform>();
        private readonly Dictionary<Transform, Vector3> lastHandPositions = new Dictionary<Transform, Vector3>();
        private XRBaseController activePettingController;

        // Espectro de áudio FFT
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
            CacheModelTransform();
        }

        private void Start()
        {
            CacheModelTransform();
            AutoDetectComponents();
            SetExpression(DroneLEDExpression.Normal);
        }

        private void OnEnable()
        {
            CacheModelTransform();
            AutoDetectComponents();
        }

        /// <summary>
        /// Localiza o transform do modelo 3D do Drone para a física de wobble.
        /// </summary>
        private void CacheModelTransform()
        {
            if (modelTransform == null)
            {
                modelTransform = transform.Find("Modelo_3D_ExoBot");
                if (modelTransform == null && transform.childCount > 0)
                {
                    modelTransform = transform.GetChild(0);
                }
            }

            if (modelTransform != null)
            {
                modelRestPos = modelTransform.localPosition;
                modelRestRot = modelTransform.localRotation;
            }
        }

        /// <summary>
        /// Localiza automaticamente materiais, fontes de áudio, rádio e partículas.
        /// </summary>
        public void AutoDetectComponents()
        {
            if (visorRenderer == null)
            {
                Transform visorObj = transform.Find("Modelo_3D_ExoBot/Visor_LED_Frontal");
                if (visorObj == null) visorObj = transform.Find("Visor_LED_Frontal");
                if (visorObj != null) visorRenderer = visorObj.GetComponent<MeshRenderer>();
            }

            if (petVoiceAudioSource == null)
            {
                AudioSource[] sources = GetComponentsInChildren<AudioSource>();
                if (sources.Length > 1) petVoiceAudioSource = sources[1];
                else if (sources.Length > 0) petVoiceAudioSource = sources[0];
            }

            if (musicSource == null)
            {
                GameObject radioObj = GameObject.Find("[IC] Radio_Menu_3D");
                if (radioObj != null)
                {
                    musicSource = radioObj.GetComponent<AudioSource>();
                }
            }

            if (heartParticles == null)
            {
                Transform pt = transform.Find("ParticleSystem_Hearts");
                if (pt != null) heartParticles = pt.GetComponent<ParticleSystem>();
            }

            // Localiza controladores do XR Rig para haptics contínuo
            LocateVRControllers();
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

            // 1. Processa proximidade e carinho das mãos VR (fallback robusto para triggers)
            ProcessHandProximityAndPetting(dt);

            // 2. Simula física de oscilação amortecida de wobble (Poke recoil)
            UpdateSpringPhysics(dt);

            // 3. Processa áudio e dança no ritmo se música estiver tocando
            UpdateMusicDance(dt);

            // 4. Temporizador de retorno de expressões
            UpdateExpressionLifecycle(dt);
        }

        private void LateUpdate()
        {
            // Aplica offsets de wobble e dança ao modelo sem conflitar com rotação do DroneGuiaIA
            if (modelTransform != null)
            {
                modelTransform.localPosition = modelRestPos + wobblePosOffset;
                modelTransform.localRotation = modelRestRot * Quaternion.Euler(wobbleRotOffset);
            }
        }

        #region Reatividade a Carinho (Petting)

        private void ProcessHandProximityAndPetting(float dt)
        {
            if (headPettingTrigger == null) return;

            Vector3 headCenter = headPettingTrigger.bounds.center;
            float petRadius = 0.28f;
            bool handPettingDetected = false;

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
                        handPettingDetected = true;
                        var controller = hand.GetComponentInParent<XRBaseController>();
                        if (controller != null) activePettingController = controller;
                        break;
                    }
                }
            }

            if (handPettingDetected)
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

            // Vibração háptica contínua (purring / ronronar tátil)
            if (Time.time - lastHapticTime >= pettingHapticInterval)
            {
                lastHapticTime = Time.time;
                if (activePettingController != null)
                {
                    activePettingController.SendHapticImpulse(pettingHapticAmplitude, pettingHapticInterval * 0.9f);
                }
            }

            // Emite partículas de coração flutuante
            if (heartParticles != null && !heartParticles.isPlaying)
            {
                heartParticles.Play();
            }

            // Toca som de ronronar do Drone
            if (petVoiceAudioSource != null && sfxDronePurr != null && !petVoiceAudioSource.isPlaying)
            {
                petVoiceAudioSource.clip = sfxDronePurr;
                petVoiceAudioSource.loop = true;
                petVoiceAudioSource.pitch = UnityEngine.Random.Range(0.98f, 1.04f);
                petVoiceAudioSource.volume = 0.75f;
                petVoiceAudioSource.Play();
            }

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

        #region Reatividade a Cutucão (Poke / Boop)

        public void TriggerPoke(Vector3 contactWorldPos, Vector3 impactVelocity)
        {
            isPokedActive = true;
            expressionTimer = 2.4f;

            // Interrompe carinho se houver
            StopPettingBehavior();

            // Física de recuo e balanço elástico
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

            // Sons de Boop e Risadinha
            if (petVoiceAudioSource != null)
            {
                petVoiceAudioSource.Stop();
                petVoiceAudioSource.loop = false;
                petVoiceAudioSource.pitch = UnityEngine.Random.Range(0.95f, 1.08f);
                petVoiceAudioSource.volume = 0.90f;
                if (sfxDroneBoop != null)
                {
                    petVoiceAudioSource.PlayOneShot(sfxDroneBoop);
                }
            }

            // Expressão tonta (@_@)
            SetExpression(DroneLEDExpression.Poked, 2.4f);

            // Agenda risadinha subsequente
            CancelInvoke(nameof(PlayGiggleAfterBoop));
            Invoke(nameof(PlayGiggleAfterBoop), 0.35f);
        }

        private void PlayGiggleAfterBoop()
        {
            if (petVoiceAudioSource != null && sfxDroneGiggle != null)
            {
                petVoiceAudioSource.pitch = UnityEngine.Random.Range(0.98f, 1.06f);
                petVoiceAudioSource.volume = 0.80f;
                petVoiceAudioSource.PlayOneShot(sfxDroneGiggle);
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

            // Limites de parada
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

        #region Dança Rítmica Musical

        private void UpdateMusicDance(float dt)
        {
            if (!enableMusicDance || isPettingActive || isPokedActive) return;

            // Se o rádio não estiver configurado, tenta encontrá-lo
            if (musicSource == null)
            {
                GameObject radio = GameObject.Find("[IC] Radio_Menu_3D");
                if (radio != null) musicSource = radio.GetComponent<AudioSource>();
            }

            bool musicPlaying = (musicSource != null && musicSource.isPlaying && musicSource.volume > 0.05f);

            if (musicPlaying)
            {
                musicSource.GetSpectrumData(spectrumSamples, 0, FFTWindow.BlackmanHarris);

                // Acumula bandas de frequências graves (Kick / Bass: 50Hz - 220Hz)
                float bassEnergy = 0f;
                for (int i = 1; i < 8; i++)
                {
                    bassEnergy += spectrumSamples[i];
                }

                smoothedBassEnergy = Mathf.Lerp(smoothedBassEnergy, bassEnergy, dt * 10f);

                if (smoothedBassEnergy > danceBeatSensitivity)
                {
                    isDancingActive = true;

                    // Balanço lateral e saltinho no ritmo da batida
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
                    if (currentExpression == DroneLEDExpression.Dance)
                    {
                        SetExpression(DroneLEDExpression.Normal, 0f);
                    }
                }
            }
            else if (isDancingActive)
            {
                isDancingActive = false;
                if (currentExpression == DroneLEDExpression.Dance)
                {
                    SetExpression(DroneLEDExpression.Normal, 0f);
                }
            }
        }

        #endregion

        #region Gerenciamento de Expressões LED

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

        private void ApplyTextureToVisor(Texture2D tex)
        {
            if (visorRenderer == null || tex == null) return;

            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            visorRenderer.GetPropertyBlock(propBlock);

            propBlock.SetTexture(MainTexId, tex);
            propBlock.SetTexture(EmissionMapId, tex);

            Color finalEmissive = ledEmissiveColor * ledEmissiveIntensity;
            propBlock.SetColor(EmissionColorId, finalEmissive);

            visorRenderer.SetPropertyBlock(propBlock);

            // Garante atribuição direta no material também
            if (Application.isPlaying && visorRenderer.material != null)
            {
                visorRenderer.material.mainTexture = tex;
                if (visorRenderer.material.HasProperty(EmissionMapId))
                    visorRenderer.material.SetTexture(EmissionMapId, tex);
            }
            else if (visorRenderer.sharedMaterial != null)
            {
                visorRenderer.sharedMaterial.mainTexture = tex;
                if (visorRenderer.sharedMaterial.HasProperty(EmissionMapId))
                    visorRenderer.sharedMaterial.SetTexture(EmissionMapId, tex);
            }
        }

        private void UpdateExpressionLifecycle(float dt)
        {
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

        #region Detecção por Triggers Físicos

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

            // Ignora colisores do próprio drone
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
                TriggerPoke(contact, vel);
            }
        }

        #endregion

        #region Métodos de Teste e Validação Automática

        public void TestPettingPose()
        {
            SetExpression(DroneLEDExpression.Love, 5.0f);
            if (heartParticles != null)
            {
                heartParticles.Play();
            }
            wobbleRotOffset = new Vector3(-4f, 6f, 3f);
            wobblePosOffset = new Vector3(0.01f, 0.02f, -0.01f);
        }

        public void TestPokedPose()
        {
            SetExpression(DroneLEDExpression.Poked, 5.0f);
            wobbleRotOffset = new Vector3(pokeWobbleAngle, -pokeWobbleAngle * 0.8f, -4f);
            wobblePosOffset = new Vector3(0f, -0.015f, pokeRecoilZ);
        }

        public void TestDancingPose()
        {
            SetExpression(DroneLEDExpression.Dance, 5.0f);
            wobbleRotOffset = new Vector3(-3f, 8f, danceTiltAngle);
            wobblePosOffset = new Vector3(0.02f, danceBounceHeight, 0f);
        }

        public void ResetTestPose()
        {
            SetExpression(DroneLEDExpression.Normal);
            wobbleRotOffset = Vector3.zero;
            wobblePosOffset = Vector3.zero;
            if (heartParticles != null)
            {
                heartParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        #endregion
    }
}
