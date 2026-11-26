using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;
using VRRehab.Ergonomics;

namespace VRRehab.Interactive.Task07
{
    /// <summary>
    /// Pistola de Balões Antigravidade (TASK-07, estilo The Lab).
    /// Convenção do prefab: o +Z local da raiz é a direção do cano (o Muzzle_Socket aponta para +Z) e o attachTransform
    /// (Grip_Attach) fica na empunhadura, com +Z também para a frente. Assim o XRI segura a pistola com o cano para a frente.
    /// Controle HTC Vive: GATILHO (Select) segura a pistola; botão LATERAL/grip (Activate) infla. Soltar o Activate solta o balão.
    /// O tamanho final depende do tempo segurando; ao chegar no máximo o balão solta sozinho (nunca estoura na mão).
    /// Balões vêm de um <see cref="BalloonPool"/> de 8 (+1 reserva), criado uma vez no Start.
    /// Ao soltar a pistola ela cai com física normal (alwaysDynamicAfterRelease da base ErgonomicGrabInteractable).
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public class BalloonInflaterGun : ErgonomicGrabInteractable
    {
        [Header("Pistola & Bico")]
        [Tooltip("Bico onde o balão nasce. Seu +Z (forward) é a direção do cano.")]
        [SerializeField] private Transform muzzleSocket;
        [Tooltip("Ponteiro do manômetro (opcional): gira conforme o balão enche.")]
        [SerializeField] private Transform gaugeNeedle;
        [SerializeField] private float gaugeSweepDegrees = 210f;
        [SerializeField] private GameObject balloonPrefab;
        [SerializeField] private Material[] balloonMaterials;

        [Header("Pool")]
        [Tooltip("Máximo de balões soltos ao mesmo tempo (o 9º faz o mais antigo estourar suave).")]
        [SerializeField] private int maxLiveBalloons = 8;

        [Header("Inflação (escala real do modelo: 1.0 = 0,30 m de altura)")]
        [SerializeField] private float minScale = 0.15f;
        [SerializeField] private float maxScale = 1.36f;
        [Tooltip("Se soltar antes, o balão ainda cresce até este tamanho no ar (balão nunca sai murcho).")]
        [SerializeField] private float minReleaseScale = 0.7f;
        [Tooltip("Variação aleatória do tamanho máximo (±).")]
        [Range(0f, 0.2f)] [SerializeField] private float sizeVariation = 0.08f;
        [Tooltip("Segundos de min até max.")]
        [SerializeField] private float inflateDuration = 1.6f;
        [Tooltip("Soltar antes deste progresso cancela (evita balões vazios por clique acidental).")]
        [SerializeField] private float cancelBelowProgress = 0.06f;

        [Header("Lançamento")]
        [SerializeField] private float launchVelocity = 0.9f;
        [SerializeField] private float launchUpBias = 0.2f;

        [Header("Áudio")]
        [SerializeField] private AudioClip inflateStretchAudioClip;
        [SerializeField] private AudioClip releaseWhooshAudioClip;
        [SerializeField] private AudioSource gunAudioSource;
        [Range(0f, 1f)] [SerializeField] private float inflateVolume = 0.45f;
        [Range(0f, 1f)] [SerializeField] private float whooshVolume = 0.5f;

        private BalloonPool pool;
        private FloatingBalloon current;
        private IXRInteractor inflatingInteractor;
        private float progress;
        private float currentMaxScale;
        private bool isInflating;
        private bool waitForActivateRelease;
        private float nextHapticTime;
        private float jitterSeed;
        private Collider[] gunColliders;
        private Rigidbody gunRb;
        private float discreteAt = -1f;

        // Manômetro (gira em torno de uma ponta do ponteiro, no plano do mostrador)
        private Vector3 needleRestPos;
        private Quaternion needleRestRot;
        private Vector3 needlePivotParent;
        private Vector3 needleAxisParent;
        private bool needleReady;

        public bool IsInflating => isInflating;
        public float InflationProgress => progress;
        public FloatingBalloon CurrentBalloon => current;
        public Transform MuzzleSocket => muzzleSocket;
        public BalloonPool Pool => pool;
        public float MaxScale => maxScale;

        protected override void Awake()
        {
            base.Awake();
            if (muzzleSocket == null)
            {
                Transform found = FindDeep(transform, "Muzzle_Socket");
                muzzleSocket = found != null ? found : transform;
            }
            if (gaugeNeedle == null) gaugeNeedle = FindDeep(transform, "Pressure_Gauge_Needle");
            if (gunAudioSource == null) gunAudioSource = GetComponent<AudioSource>();
            if (gunAudioSource != null)
            {
                gunAudioSource.playOnAwake = false;
                gunAudioSource.spatialBlend = 1f;
            }
            gunColliders = GetComponentsInChildren<Collider>(true);
            gunRb = GetComponent<Rigidbody>();
            jitterSeed = Random.Range(0f, 100f);
            SetupNeedle();
        }

        private void Start()
        {
            if (balloonPrefab != null && pool == null)
            {
                pool = new BalloonPool(balloonPrefab, maxLiveBalloons, "BalloonPool_Runtime (TASK-07)");
                pool.Root.position = transform.position + Vector3.up * 0.5f;
            }
        }

        #region XRI: Activate infla, Deactivate solta
        protected override void OnActivated(ActivateEventArgs args)
        {
            base.OnActivated(args);
            if (!isInflating) StartInflation(args.interactorObject);
        }

        protected override void OnDeactivated(DeactivateEventArgs args)
        {
            base.OnDeactivated(args);
            waitForActivateRelease = false;
            if (isInflating) ReleaseBalloon();
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            // Na mão: detecção contínua (não atravessa o tampo se for jogada); volta a Discrete 3 s depois de soltar
            if (gunRb != null) gunRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            discreteAt = -1f;
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            discreteAt = Time.time + 3f;
            waitForActivateRelease = false;
            if (isInflating) ReleaseBalloon(); // soltou a pistola no meio da inflação: o balão sai, não fica preso
        }
        #endregion

        /// <summary>Começa a encher um balão no bico. Público para automação de testes.</summary>
        public bool StartInflation(IXRInteractor interactor)
        {
            if (isInflating || waitForActivateRelease || pool == null || muzzleSocket == null) return false;
            FloatingBalloon b = pool.Acquire();
            if (b == null) return false;

            Material mat = null;
            Color burst = new Color(1f, 0.3f, 0.4f);
            if (balloonMaterials != null && balloonMaterials.Length > 0)
            {
                mat = balloonMaterials[Random.Range(0, balloonMaterials.Length)];
                if (mat != null && mat.HasProperty("_BaseColor")) burst = mat.GetColor("_BaseColor");
                else if (mat != null && mat.HasProperty("_Color")) burst = mat.color;
            }

            current = b;
            inflatingInteractor = interactor;
            isInflating = true;
            progress = 0f;
            currentMaxScale = maxScale * (1f + Random.Range(-sizeVariation, sizeVariation));
            b.BeginInflation(muzzleSocket, minScale, mat, burst);

            if (gunAudioSource != null && inflateStretchAudioClip != null)
            {
                gunAudioSource.clip = inflateStretchAudioClip;
                gunAudioSource.loop = true;
                gunAudioSource.pitch = 0.85f;
                gunAudioSource.volume = inflateVolume;
                gunAudioSource.Play();
            }
            nextHapticTime = 0f;
            return true;
        }

        private void Update()
        {
            if (discreteAt > 0f && Time.time >= discreteAt && !isSelected)
            {
                discreteAt = -1f;
                if (gunRb != null) gunRb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            }
            if (!isInflating || current == null) return;

            progress = Mathf.Clamp01(progress + Time.deltaTime / Mathf.Max(0.1f, inflateDuration));
            float eased = 1f - (1f - progress) * (1f - progress); // enche rápido no começo, devagar no fim
            float s = Mathf.Lerp(minScale, currentMaxScale, eased);

            // Pulso elástico (squash/stretch) e tremor leve perto do máximo
            float t = Time.time;
            float stretch = 1f + Mathf.Sin(t * 13f) * 0.045f * (1f - progress * 0.5f);
            Vector3 jitter = Vector3.zero;
            if (progress > 0.85f)
            {
                float a = (progress - 0.85f) / 0.15f * 0.0025f * s;
                jitter = new Vector3(Mathf.PerlinNoise(jitterSeed, t * 30f) - 0.5f, 0f, Mathf.PerlinNoise(t * 30f, jitterSeed) - 0.5f) * (2f * a);
            }
            current.SetInflationShape(s, stretch, jitter);
            UpdateNeedle(progress);

            if (gunAudioSource != null && gunAudioSource.isPlaying)
                gunAudioSource.pitch = Mathf.Lerp(0.85f, 1.3f, progress);

            if (inflatingInteractor != null && VRFeedbackManager.Instance != null && t >= nextHapticTime)
            {
                VRFeedbackManager.Instance.TriggerHaptic(inflatingInteractor, Mathf.Lerp(0.12f, 0.5f, progress), 0.05f);
                nextHapticTime = t + 0.09f;
            }

            if (progress >= 1f)
            {
                ReleaseBalloon();               // cheio: solta sozinho, nunca estoura na mão
                waitForActivateRelease = true;  // o próximo exige soltar e apertar o botão de novo
            }
        }

        /// <summary>Solta o balão do bico na direção do cano. Público para automação de testes.</summary>
        public void ReleaseBalloon()
        {
            if (!isInflating) return;
            isInflating = false;
            if (gunAudioSource != null && gunAudioSource.isPlaying)
            {
                gunAudioSource.Stop();
                gunAudioSource.loop = false;
                gunAudioSource.pitch = 1f;
            }
            UpdateNeedle(0f);

            FloatingBalloon b = current;
            current = null;
            IXRInteractor interactor = inflatingInteractor;
            inflatingInteractor = null;
            if (b == null) return;

            if (progress < cancelBelowProgress)
            {
                b.CancelSilently();
                return;
            }

            Vector3 dir = muzzleSocket.forward;
            Vector3 vel = (dir + Vector3.up * launchUpBias).normalized * launchVelocity;
            Transform hand = interactor != null ? interactor.transform : null;
            b.Launch(vel, Mathf.Max(minReleaseScale, b.CurrentScale), gunColliders, hand);

            if (gunAudioSource != null && releaseWhooshAudioClip != null)
                gunAudioSource.PlayOneShot(releaseWhooshAudioClip, whooshVolume);
            if (interactor != null && VRFeedbackManager.Instance != null)
                VRFeedbackManager.Instance.TriggerHaptic(interactor, 0.55f, 0.08f);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (pool != null && pool.Root != null) Destroy(pool.Root.gameObject); // o pool não fica órfão na cena
        }

        #region Manômetro
        private void SetupNeedle()
        {
            needleReady = false;
            if (gaugeNeedle == null) return;
            MeshFilter mf = gaugeNeedle.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            // Eixo mais fino do ponteiro = normal do mostrador; eixo mais longo = comprimento do ponteiro
            Bounds mb = mf.sharedMesh.bounds;
            Vector3 size = Vector3.Scale(mb.size, gaugeNeedle.localScale);
            int thin = size.x <= size.y && size.x <= size.z ? 0 : (size.y <= size.z ? 1 : 2);
            int longest = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
            Vector3 thinAxis = Vector3.zero; thinAxis[thin] = 1f;
            Vector3 longAxis = Vector3.zero; longAxis[longest] = mb.extents[longest];

            needleRestPos = gaugeNeedle.localPosition;
            needleRestRot = gaugeNeedle.localRotation;
            needleAxisParent = needleRestRot * thinAxis;
            Vector3 centerParent = needleRestPos + needleRestRot * Vector3.Scale(mb.center, gaugeNeedle.localScale);
            needlePivotParent = centerParent - needleRestRot * Vector3.Scale(longAxis, gaugeNeedle.localScale);
            needleReady = true;
        }

        private void UpdateNeedle(float p)
        {
            if (!needleReady) return;
            Quaternion q = Quaternion.AngleAxis(-p * gaugeSweepDegrees, needleAxisParent);
            gaugeNeedle.localRotation = q * needleRestRot;
            gaugeNeedle.localPosition = needlePivotParent + q * (needleRestPos - needlePivotParent);
        }
        #endregion

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
