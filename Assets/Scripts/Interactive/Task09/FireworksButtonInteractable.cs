using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;

namespace VRRehab.Interactive.Task09
{
    /// <summary>
    /// Botão Arcade de Fogos de Artifício (TASK-09, refeito).
    ///
    /// Tudo o que o show precisa é filho deste prefab (foguete, trilha, explosões, flash, luz, sons), então mover ou
    /// girar o botão não quebra nada. O ponto de detonação é calculado na hora do disparo:
    ///   1. sobe na vertical, inclinando até <see cref="maxTiltDegrees"/> para o lado com mais espaço livre (8 raios);
    ///   2. altura = min(<see cref="desiredBurstHeight"/>, espaço livre no caminho − <see cref="ceilingMargin"/>), mínimo
    ///      <see cref="minBurstHeight"/>; se não couber (TV/estante logo acima), faz um "show baixo" (leque de faíscas);
    ///   3. nunca abaixo do botão e nunca dentro de colisor sólido.
    /// O foguete viaja até o ápice (tempo = distância / velocidade) e a explosão só acontece quando ele chega.
    ///
    /// Disparo: gatilho do Vive (Select) com laser ou mão, ou toque da mão no domo (trigger dedicado, validado por
    /// XRBaseInteractor, sem comparar nomes). Cooldown de 1,2 s; no máximo 2 shows simultâneos (2 conjuntos pré-alocados).
    /// VR: zero alocação por frame, sem Update, partículas em World space com teto de partículas, luz sem sombra,
    /// brilho do botão por MaterialPropertyBlock.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class FireworksButtonInteractable : MonoBehaviour
    {
        public enum BurstVariant { Peony = 0, Ring = 1, Willow = 2, Glitter = 3 }

        [Serializable]
        public class ShowRig
        {
            [Tooltip("Transform que voa do botão até o ápice.")] public Transform rocket;
            [Tooltip("Faíscas da trilha (World space, emissão por distância).")] public ParticleSystem rocketTrail;
            [Tooltip("Brilho na ponta do foguete (1 partícula).")] public ParticleSystem rocketHead;
            [Tooltip("Raiz que é levada até o ápice no momento do estouro.")] public Transform burstRoot;
            [Tooltip("Explosão principal; o código troca forma/cor/gravidade conforme a variante.")] public ParticleSystem mainBurst;
            [Tooltip("Crepitação final (faíscas miúdas).")] public ParticleSystem crackle;
            [Tooltip("Clarão rápido no centro do estouro.")] public ParticleSystem flash;
            [Tooltip("Leque de faíscas do 'show baixo' (quando não há altura livre).")] public ParticleSystem lowFan;
            [Tooltip("Fonte 3D do estouro (fica no ápice).")] public AudioSource burstAudio;

            [NonSerialized] public float freeAtTime;
            [NonSerialized] public bool inFlight;
        }

        [Header("Hierarquia do Botão")]
        [SerializeField] private Transform buttonDomeTransform;
        [SerializeField] private Renderer domeRenderer;
        [SerializeField] private Vector3 pressedLocalOffset = new Vector3(0f, -0.012f, 0f);
        [SerializeField] private Light buttonGlowLight;
        [Tooltip("Ponto de saída do foguete (topo do domo).")]
        [SerializeField] private Transform launchPoint;

        [Header("Show (pré-alocado, 2 conjuntos)")]
        [SerializeField] private ShowRig[] shows = new ShowRig[0];
        [SerializeField] private Light fireworksBurstLight;

        [Header("Trajetória")]
        [SerializeField] private float desiredBurstHeight = 1.2f;
        [SerializeField] private float minBurstHeight = 0.6f;
        [Tooltip("Folga deixada entre o estouro e o teto/móvel acima.")]
        [SerializeField] private float ceilingMargin = 0.35f;
        [SerializeField] private float maxTiltDegrees = 12f;
        [Tooltip("Velocidade média do foguete (m/s). O tempo de voo fica entre 0,45 e 0,8 s.")]
        [SerializeField] private float rocketSpeed = 3.5f;
        [SerializeField] private Vector2 rocketTimeRange = new Vector2(0.45f, 0.80f);
        [Tooltip("Camadas consideradas obstáculo para o foguete (triggers são sempre ignorados).")]
        [SerializeField] private LayerMask obstacleMask = ~0;
        [SerializeField] private float probeRadius = 0.12f;

        [Header("Ritmo")]
        [SerializeField] private float cooldownSeconds = 1.2f;

        [Header("Luz do estouro")]
        [SerializeField] private float flashIntensity = 2.2f;
        [SerializeField] private float flashRange = 3.0f;
        [SerializeField] private float flashFadeSeconds = 0.3f;

        [Header("Efeitos Sonoros (3D, sem loop)")]
        [SerializeField] private AudioClip sfxButtonPress;
        [SerializeField] private AudioClip sfxLaunchWhistle;
        [SerializeField] private AudioClip sfxBurstExplosion;
        [SerializeField] private AudioSource audioSource;
        [SerializeField, Range(0f, 0.5f)] private float pressVolume = 0.40f;
        [SerializeField, Range(0f, 0.5f)] private float whistleVolume = 0.35f;
        [SerializeField, Range(0f, 0.5f)] private float burstVolume = 0.45f;

        [Header("Feedback")]
        [SerializeField] private float hapticAmplitude = 0.6f;
        [SerializeField] private Color domeGlowColor = new Color(1.6f, 0.35f, 0.15f, 1f);

        // ------------------------------------------------------------------ estado
        private Vector3 domeOriginalLocalPos;
        private Coroutine pressRoutine;
        private Coroutine lightRoutine;
        private XRSimpleInteractable xrInteractable;
        private MaterialPropertyBlock mpb;
        private Color domeBaseEmission = Color.black;
        private Transform playerRoot;
        private float lastFireTime = -999f;
        private BurstVariant lastVariant = (BurstVariant)(-1);
        private int forcedVariant = -1;
        private Gradient[] palettes;
        private Gradient willowGold, glitterWhite;

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private readonly Collider[] overlaps = new Collider[16];
        private static readonly Vector3[] HorizontalDirs =
        {
            new Vector3(1, 0, 0), new Vector3(0.7071f, 0, 0.7071f), new Vector3(0, 0, 1), new Vector3(-0.7071f, 0, 0.7071f),
            new Vector3(-1, 0, 0), new Vector3(-0.7071f, 0, -0.7071f), new Vector3(0, 0, -1), new Vector3(0.7071f, 0, -0.7071f)
        };
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        // ------------------------------------------------------------------ telemetria (testes / auditoria)
        public Vector3 LastApex { get; private set; }
        public Vector3 LastLaunchPosition { get; private set; }
        public float LastClearDistance { get; private set; }
        public bool LastWasLowShow { get; private set; }
        public BurstVariant LastVariant => lastVariant;
        public float LastRocketArrivalTime { get; private set; } = -1f;
        public float LastBurstTime { get; private set; } = -1f;
        public int ShowsFired { get; private set; }
        public int RejectedByCooldown { get; private set; }
        public bool IsOnCooldown => Time.time < lastFireTime + cooldownSeconds;
        public float CooldownSeconds => cooldownSeconds;
        public Transform LaunchPoint => launchPoint;
        public ShowRig[] Shows => shows;
        public Light BurstLight => fireworksBurstLight;

        public int ActiveShows
        {
            get
            {
                int n = 0;
                for (int i = 0; i < shows.Length; i++) if (Time.time < shows[i].freeAtTime || shows[i].inFlight) n++;
                return n;
            }
        }

        /// <summary>Partículas vivas somando todos os sistemas do show (sem alocação).</summary>
        public int LiveParticleCount()
        {
            int n = 0;
            for (int i = 0; i < shows.Length; i++)
            {
                ShowRig r = shows[i];
                n += Count(r.rocketTrail) + Count(r.rocketHead) + Count(r.mainBurst) + Count(r.crackle) + Count(r.flash) + Count(r.lowFan);
            }
            return n;
        }

        private static int Count(ParticleSystem ps) => ps != null ? ps.particleCount : 0;

        // ------------------------------------------------------------------ ciclo de vida
        private void Awake()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.loop = false;
                audioSource.spatialBlend = 1f;
            }
            if (buttonDomeTransform != null) domeOriginalLocalPos = buttonDomeTransform.localPosition;
            if (launchPoint == null) launchPoint = transform;

            mpb = new MaterialPropertyBlock();
            if (domeRenderer != null && domeRenderer.sharedMaterial != null && domeRenderer.sharedMaterial.HasProperty(EmissionColorId))
                domeBaseEmission = domeRenderer.sharedMaterial.GetColor(EmissionColorId);

            if (fireworksBurstLight != null)
            {
                fireworksBurstLight.shadows = LightShadows.None;
                fireworksBurstLight.range = flashRange;
                fireworksBurstLight.intensity = 0f;
                fireworksBurstLight.enabled = false;
            }
            if (buttonGlowLight != null) buttonGlowLight.shadows = LightShadows.None;

            BuildPalettes();
            for (int i = 0; i < shows.Length; i++) ResetRig(shows[i]);

            xrInteractable = GetComponent<XRSimpleInteractable>();
            if (xrInteractable == null) xrInteractable = gameObject.AddComponent<XRSimpleInteractable>();
            xrInteractable.selectEntered.AddListener(OnSelectEntered);
        }

        private void Start()
        {
            var origin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (origin != null) playerRoot = origin.transform;
        }

        private void OnDestroy()
        {
            if (xrInteractable != null) xrInteractable.selectEntered.RemoveListener(OnSelectEntered);
        }

        private void OnDisable()
        {
            if (fireworksBurstLight != null) fireworksBurstLight.enabled = false;
            for (int i = 0; i < shows.Length; i++) { shows[i].inFlight = false; shows[i].freeAtTime = 0f; }
        }

        private void BuildPalettes()
        {
            // Alocação única no Awake; em runtime só reatribuímos as referências (struct MinMaxGradient).
            palettes = new[]
            {
                TwoColor(new Color(1.00f, 0.30f, 0.45f), new Color(1.00f, 0.85f, 0.30f)),   // rosa + dourado
                TwoColor(new Color(0.30f, 0.80f, 1.00f), new Color(0.75f, 0.40f, 1.00f)),   // ciano + violeta
                TwoColor(new Color(0.45f, 1.00f, 0.45f), new Color(1.00f, 0.95f, 0.45f)),   // verde + amarelo
                TwoColor(new Color(1.00f, 0.45f, 0.15f), new Color(1.00f, 0.25f, 0.70f)),   // laranja + magenta
                TwoColor(new Color(0.40f, 0.55f, 1.00f), new Color(1.00f, 1.00f, 1.00f)),   // azul + branco
            };
            willowGold = TwoColor(new Color(1.00f, 0.78f, 0.30f), new Color(1.00f, 0.60f, 0.15f));
            glitterWhite = TwoColor(new Color(1.00f, 1.00f, 0.90f), new Color(1.00f, 0.85f, 0.55f));
        }

        private static Gradient TwoColor(Color a, Color b)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        private static void ResetRig(ShowRig r)
        {
            r.inFlight = false;
            r.freeAtTime = 0f;
            StopClear(r.rocketTrail);
            StopClear(r.rocketHead);
            StopClear(r.mainBurst);
            StopClear(r.crackle);
            StopClear(r.flash);
            StopClear(r.lowFan);
            if (r.burstAudio != null) { r.burstAudio.playOnAwake = false; r.burstAudio.loop = false; r.burstAudio.spatialBlend = 1f; }
        }

        private static void StopClear(ParticleSystem ps)
        {
            if (ps == null) return;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // ------------------------------------------------------------------ entrada
        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (TriggerFireworks() && args.interactorObject != null)
                VRFeedbackManager.Instance?.TriggerHaptic(args.interactorObject, hapticAmplitude, 0.12f);
        }

        /// <summary>Toque da mão no domo: só aceita interactors de mão (não laser nem socket).</summary>
        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;
            XRBaseInteractor hand = other.GetComponentInParent<XRBaseInteractor>();
            if (hand == null || hand is XRRayInteractor || hand is XRSocketInteractor) return;
            if (TriggerFireworks())
                VRFeedbackManager.Instance?.TriggerHaptic(hand, hapticAmplitude * 0.8f, 0.10f);
        }

        /// <summary>Dispara um show. Durante o cooldown ou com os 2 conjuntos ocupados não faz nada (sem fila).</summary>
        [ContextMenu("Disparar Fogos")]
        public bool TriggerFireworks()
        {
            if (!isActiveAndEnabled) return false;
            if (IsOnCooldown) { RejectedByCooldown++; return false; }
            ShowRig rig = FreeRig();
            if (rig == null) { RejectedByCooldown++; return false; }

            lastFireTime = Time.time;
            ShowsFired++;

            if (pressRoutine != null) StopCoroutine(pressRoutine);
            pressRoutine = StartCoroutine(PressRoutine());
            StartCoroutine(ShowRoutine(rig));
            return true;
        }

        private ShowRig FreeRig()
        {
            for (int i = 0; i < shows.Length; i++)
                if (!shows[i].inFlight && Time.time >= shows[i].freeAtTime) return shows[i];
            return null;
        }

        // ------------------------------------------------------------------ trajetória
        /// <summary>
        /// Calcula o ápice livre. Retorna false quando não há altura mínima (show baixo).
        /// Público para os testes/auditoria (não dispara nada).
        /// </summary>
        public bool ComputeApex(out Vector3 apex, out float clearDistance)
        {
            Vector3 origin = launchPoint.position;

            // 1. Lado mais livre (raios horizontais 0,45 m acima do botão)
            Vector3 probe = origin + Vector3.up * 0.45f;
            float bestFree = 0f;
            Vector3 bestDir = Vector3.zero;
            for (int i = 0; i < HorizontalDirs.Length; i++)
            {
                float free = FreeDistance(probe, HorizontalDirs[i], 1.2f, 0.05f);
                if (free > bestFree + 0.01f) { bestFree = free; bestDir = HorizontalDirs[i]; }
            }
            Vector3 dir = Vector3.up;
            if (bestFree >= 0.5f && bestDir != Vector3.zero)
                dir = Quaternion.AngleAxis(maxTiltDegrees, Vector3.Cross(Vector3.up, bestDir)) * Vector3.up;

            // 2. Espaço livre ao longo do caminho (SphereCast), começando logo acima do próprio domo
            Vector3 start = origin + Vector3.up * 0.03f;
            clearDistance = FreeDistance(start, dir, desiredBurstHeight + ceilingMargin + 1.5f, probeRadius) + 0.03f;
            float height = Mathf.Min(desiredBurstHeight, clearDistance - ceilingMargin);

            // 3. Nunca dentro de colisor sólido: desce de 0,15 em 0,15 m se precisar
            while (height >= minBurstHeight && Occupied(origin + dir * height, 0.10f)) height -= 0.15f;

            if (height < minBurstHeight)
            {
                apex = origin + Vector3.up * 0.30f;
                return false;
            }
            apex = origin + dir * height;
            return true;
        }

        private float FreeDistance(Vector3 from, Vector3 dir, float maxDist, float radius)
        {
            int n = radius > 0.0001f
                ? Physics.SphereCastNonAlloc(from, radius, dir, hits, maxDist, obstacleMask, QueryTriggerInteraction.Ignore)
                : Physics.RaycastNonAlloc(from, dir, hits, maxDist, obstacleMask, QueryTriggerInteraction.Ignore);
            float best = maxDist;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = hits[i];
                if (h.distance <= 0f) continue;           // já sobrepondo no início (vizinho encostado): não é teto
                if (IsIgnored(h.collider)) continue;
                if (h.distance < best) best = h.distance;
            }
            return best;
        }

        private bool Occupied(Vector3 point, float radius)
        {
            int n = Physics.OverlapSphereNonAlloc(point, radius, overlaps, obstacleMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!IsIgnored(overlaps[i])) return true;
            return false;
        }

        private bool IsIgnored(Collider c)
        {
            if (c == null) return true;
            Transform t = c.transform;
            if (t.IsChildOf(transform)) return true;                                     // o próprio botão
            if (playerRoot != null && t.IsChildOf(playerRoot)) return true;              // o paciente
            Rigidbody rb = c.attachedRigidbody;
            if (rb != null && !rb.isKinematic) return true;                              // objetos soltos (balões, bolas)
            return false;
        }

        // ------------------------------------------------------------------ show
        private IEnumerator ShowRoutine(ShowRig rig)
        {
            rig.inFlight = true;
            bool high = ComputeApex(out Vector3 apex, out float clear);
            Vector3 origin = launchPoint.position;
            LastLaunchPosition = origin;
            LastApex = apex;
            LastClearDistance = clear;
            LastWasLowShow = !high;

            if (audioSource != null && sfxLaunchWhistle != null && high)
            {
                audioSource.pitch = UnityEngine.Random.Range(0.96f, 1.05f);
                audioSource.PlayOneShot(sfxLaunchWhistle, whistleVolume);
            }

            if (!high)
            {
                yield return LowShow(rig, origin);
                yield break;
            }

            // --- Foguete: viaja do botão ao ápice com ease-out; o estouro acontece quando ele chega
            float dist = Vector3.Distance(origin, apex);
            float flight = Mathf.Clamp(dist / Mathf.Max(0.1f, rocketSpeed), rocketTimeRange.x, rocketTimeRange.y);
            Transform rocket = rig.rocket;
            if (rocket != null)
            {
                rocket.position = origin;
                rocket.rotation = Quaternion.FromToRotation(Vector3.up, apex - origin);
            }
            if (rig.rocketHead != null) { rig.rocketHead.Clear(true); rig.rocketHead.Play(true); }
            if (rig.rocketTrail != null) { rig.rocketTrail.Clear(true); rig.rocketTrail.Play(true); }

            float wiggleSeed = UnityEngine.Random.value * 10f;
            float t = 0f;
            while (t < flight)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / flight);
                float eased = 1f - (1f - u) * (1f - u) * (1f - u) * 0.6f - (1f - u) * 0.4f; // sobe rápido e desacelera
                Vector3 p = Vector3.LerpUnclamped(origin, apex, Mathf.Clamp01(eased));
                // Leve zigue-zague horizontal (some perto do ápice)
                float w = 0.012f * (1f - u);
                p.x += Mathf.Sin((t + wiggleSeed) * 31f) * w;
                p.z += Mathf.Cos((t + wiggleSeed) * 27f) * w;
                if (rocket != null) rocket.position = p;
                yield return null;
            }
            if (rocket != null) rocket.position = apex;
            LastRocketArrivalTime = Time.time;

            if (rig.rocketTrail != null) rig.rocketTrail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (rig.rocketHead != null) rig.rocketHead.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // --- Estouro
            float space = Mathf.Clamp((clear - Vector3.Distance(origin, apex)) / 0.55f, 0.55f, 1f);
            Detonate(rig, apex, space, out float longestLife);
            rig.inFlight = false;
            rig.freeAtTime = Time.time + longestLife + 0.1f;

            // Crepitação final: pipocos miúdos espalhados em volta do ápice
            if (rig.crackle != null)
            {
                float wait = 0.35f + UnityEngine.Random.value * 0.15f;
                while (wait > 0f) { wait -= Time.deltaTime; yield return null; }
                EmitCrackle(rig.crackle, apex, 0.45f * space, 22);
            }
        }

        private void Detonate(ShowRig rig, Vector3 apex, float space, out float longestLife)
        {
            LastBurstTime = Time.time;
            if (rig.burstRoot != null) rig.burstRoot.position = apex;

            BurstVariant v = PickVariant();
            longestLife = ConfigureMainBurst(rig.mainBurst, v, space);
            if (rig.mainBurst != null) rig.mainBurst.Play(false);
            if (rig.flash != null) { rig.flash.Clear(false); rig.flash.Play(false); }
            longestLife = Mathf.Max(longestLife, 1.0f);

            if (rig.burstAudio != null && sfxBurstExplosion != null)
            {
                rig.burstAudio.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
                rig.burstAudio.PlayOneShot(sfxBurstExplosion, burstVolume);
            }
            Flash(apex, 1f);
        }

        /// <summary>Usado pelos testes/capturas para escolher a próxima variante (-1 = aleatória sem repetir).</summary>
        public void ForceNextVariant(BurstVariant v) => forcedVariant = (int)v;

        private BurstVariant PickVariant()
        {
            if (forcedVariant >= 0)
            {
                lastVariant = (BurstVariant)forcedVariant;
                forcedVariant = -1;
                return lastVariant;
            }
            int v = UnityEngine.Random.Range(0, 4);
            if (v == (int)lastVariant) v = (v + 1 + UnityEngine.Random.Range(0, 3)) % 4;
            lastVariant = (BurstVariant)v;
            return lastVariant;
        }

        /// <summary>Reconfigura a explosão principal para a variante (só structs; sem alocação).</summary>
        private float ConfigureMainBurst(ParticleSystem ps, BurstVariant v, float space)
        {
            if (ps == null) return 0f;
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            var shape = ps.shape;
            var emission = ps.emission;
            var psr = ps.GetComponent<ParticleSystemRenderer>();
            short count;
            float life;

            switch (v)
            {
                case BurstVariant.Ring:
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = 0.02f;
                    shape.radiusThickness = 0f;
                    shape.arc = 360f;
                    shape.rotation = new Vector3(UnityEngine.Random.Range(55f, 80f), UnityEngine.Random.Range(0f, 360f), 0f);
                    count = 48; life = 1.3f;
                    main.startSpeed = new ParticleSystem.MinMaxCurve(2.1f * space, 2.4f * space);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, life);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.07f);
                    main.gravityModifier = 0.06f;
                    main.startColor = RandomColor(palettes[UnityEngine.Random.Range(0, palettes.Length)]);
                    SetStretch(psr, false);
                    break;
                case BurstVariant.Willow:
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.03f;
                    shape.radiusThickness = 1f;
                    shape.rotation = Vector3.zero;
                    count = 56; life = 2.4f;
                    main.startSpeed = new ParticleSystem.MinMaxCurve(1.3f * space, 1.8f * space);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1.9f, life);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.05f);
                    main.gravityModifier = 0.22f;
                    main.startColor = RandomColor(willowGold);
                    SetStretch(psr, true);
                    break;
                case BurstVariant.Glitter:
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.05f;
                    shape.radiusThickness = 1f;
                    shape.rotation = Vector3.zero;
                    count = 70; life = 1.4f;
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.9f * space, 2.2f * space);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, life);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.028f, 0.045f);
                    main.gravityModifier = 0.10f;
                    main.startColor = RandomColor(glitterWhite);
                    SetStretch(psr, false);
                    break;
                default: // Peony: esfera multicolorida
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.02f;
                    shape.radiusThickness = 1f;
                    shape.rotation = Vector3.zero;
                    count = 64; life = 1.5f;
                    main.startSpeed = new ParticleSystem.MinMaxCurve(2.0f * space, 2.6f * space);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, life);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.08f);
                    main.gravityModifier = 0.08f;
                    main.startColor = RandomColor(palettes[UnityEngine.Random.Range(0, palettes.Length)]);
                    SetStretch(psr, false);
                    break;
            }
            emission.enabled = true;
            emission.SetBurst(0, new ParticleSystem.Burst(0f, count));
            return life;
        }

        private static ParticleSystem.MinMaxGradient RandomColor(Gradient g)
        {
            var mm = new ParticleSystem.MinMaxGradient(g);
            mm.mode = ParticleSystemGradientMode.RandomColor;
            return mm;
        }

        private static void SetStretch(ParticleSystemRenderer psr, bool stretch)
        {
            if (psr == null) return;
            psr.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            psr.velocityScale = stretch ? 0.10f : 0f;
            psr.lengthScale = stretch ? 1.6f : 2f;
        }

        private static void EmitCrackle(ParticleSystem ps, Vector3 center, float radius, int count)
        {
            var ep = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                ep.position = center + UnityEngine.Random.insideUnitSphere * radius;
                ps.Emit(ep, 1);
            }
        }

        private IEnumerator LowShow(ShowRig rig, Vector3 origin)
        {
            // Pouca altura livre: leque de faíscas baixo, sem foguete, sem explodir dentro do móvel.
            LastRocketArrivalTime = Time.time;
            LastBurstTime = Time.time;
            if (rig.lowFan != null)
            {
                rig.lowFan.transform.position = origin;
                rig.lowFan.Clear(false);
                rig.lowFan.Play(false);
            }
            if (rig.burstAudio != null && sfxBurstExplosion != null)
            {
                rig.burstAudio.transform.position = origin + Vector3.up * 0.3f;
                rig.burstAudio.pitch = UnityEngine.Random.Range(1.05f, 1.15f);
                rig.burstAudio.PlayOneShot(sfxBurstExplosion, burstVolume * 0.6f);
            }
            Flash(origin + Vector3.up * 0.25f, 0.5f);
            rig.inFlight = false;
            rig.freeAtTime = Time.time + 1.0f;
            yield break;
        }

        private void Flash(Vector3 position, float strength)
        {
            if (fireworksBurstLight == null) return;
            fireworksBurstLight.transform.position = position;
            if (lightRoutine != null) StopCoroutine(lightRoutine);
            lightRoutine = StartCoroutine(FlashLightRoutine(strength));
        }

        private IEnumerator FlashLightRoutine(float strength)
        {
            Light l = fireworksBurstLight;
            l.shadows = LightShadows.None;
            l.range = flashRange;
            l.enabled = true;
            float peak = flashIntensity * strength;
            float t = 0f;
            while (t < flashFadeSeconds)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / flashFadeSeconds);
                l.intensity = peak * (1f - u) * (1f - u);
                yield return null;
            }
            l.intensity = 0f;
            l.enabled = false;
            lightRoutine = null;
        }

        // ------------------------------------------------------------------ botão (animação mantida)
        private IEnumerator PressRoutine()
        {
            if (audioSource != null && sfxButtonPress != null)
            {
                audioSource.pitch = UnityEngine.Random.Range(0.96f, 1.04f);
                audioSource.PlayOneShot(sfxButtonPress, pressVolume);
            }
            SetDomeGlow(1f);
            if (buttonGlowLight != null) buttonGlowLight.intensity = 2.2f;

            if (buttonDomeTransform != null)
            {
                Vector3 down = domeOriginalLocalPos + pressedLocalOffset;
                float e = 0f;
                Vector3 from = buttonDomeTransform.localPosition;
                while (e < 0.05f)
                {
                    e += Time.deltaTime;
                    buttonDomeTransform.localPosition = Vector3.Lerp(from, down, e / 0.05f);
                    yield return null;
                }
                buttonDomeTransform.localPosition = down;
                e = 0f;
                while (e < 0.06f) { e += Time.deltaTime; yield return null; }

                e = 0f;
                while (e < 0.16f)
                {
                    e += Time.deltaTime;
                    float bounce = Mathf.Sin(Mathf.Clamp01(e / 0.16f) * Mathf.PI * 1.5f);
                    buttonDomeTransform.localPosition = Vector3.LerpUnclamped(down, domeOriginalLocalPos, bounce);
                    yield return null;
                }
                buttonDomeTransform.localPosition = domeOriginalLocalPos;
            }

            float f = 0f;
            float startI = buttonGlowLight != null ? buttonGlowLight.intensity : 0f;
            while (f < 0.25f)
            {
                f += Time.deltaTime;
                float u = Mathf.Clamp01(f / 0.25f);
                SetDomeGlow(1f - u);
                if (buttonGlowLight != null) buttonGlowLight.intensity = Mathf.Lerp(startI, 1.0f, u);
                yield return null;
            }
            SetDomeGlow(0f);
            if (buttonGlowLight != null) buttonGlowLight.intensity = 1.0f;
            pressRoutine = null;
        }

        private void SetDomeGlow(float amount)
        {
            if (domeRenderer == null) return;
            domeRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(EmissionColorId, Color.Lerp(domeBaseEmission, domeGlowColor, amount));
            domeRenderer.SetPropertyBlock(mpb);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (launchPoint == null) return;
            Gizmos.color = LastWasLowShow ? Color.yellow : Color.magenta;
            Gizmos.DrawLine(launchPoint.position, LastApex);
            Gizmos.DrawWireSphere(LastApex, 0.1f);
        }
#endif
    }
}
