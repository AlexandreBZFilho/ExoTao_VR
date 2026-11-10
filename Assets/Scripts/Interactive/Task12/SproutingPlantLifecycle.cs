using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VRRehab.Interactive.Task12
{
    /// <summary>
    /// Gerencia o ciclo de vida visual e dinâmico dos canteiros brotados na floresta (Flores ou Cogumelos).
    /// Executa:
    /// 1. Animação de crescimento elástico (0 -> 1.15 -> 1.0) nos primeiros 0.6s ao nascer;
    /// 2. Suave balanço ao vento procedural via senoide contínua;
    /// 3. Emissão sutil de pólen/partículas cintilantes;
    /// 4. Se for cogumelo: reação tátil de 'Squash & Stretch' elástico e som boing ao ser tocado.
    /// </summary>
    [SelectionBase]
    public class SproutingPlantLifecycle : MonoBehaviour
    {
        [Header("Tipo de Botânica")]
        [SerializeField] private bool isMushroomCluster = false;

        [Header("Animação de Brotação Elástica")]
        [SerializeField] private float growDuration = 0.65f;
        [SerializeField] private float elasticOvershoot = 1.18f;
        [SerializeField] private AnimationCurve customGrowCurve;

        [Header("Balanço ao Vento (Wind Sway)")]
        [SerializeField] private bool enableWindSway = true;
        [SerializeField] private float windSpeed = 1.8f;
        [SerializeField] private float windAngleMax = 3.5f;

        [Header("Cogumelo Saltitante (Squash & Stretch)")]
        [SerializeField] private AudioClip sfxMushroomBounce;
        [SerializeField] private float bounceDuration = 0.5f;
        [SerializeField] private float minBounceInterval = 0.35f;

        [Header("Partículas de Ambiente")]
        [SerializeField] private ParticleSystem pollenParticles;

        private Vector3 initialTargetScale = Vector3.one;
        private Quaternion baseRotation;
        private float randomPhase;
        private float lastBounceTime = -10f;
        private Coroutine activeBounceRoutine;
        private List<Transform> swayTargets = new List<Transform>();

        public bool IsMushroomCluster => isMushroomCluster;

        private void Awake()
        {
            initialTargetScale = transform.localScale;
            baseRotation = transform.localRotation;
            randomPhase = Random.Range(0f, 100f);

            // Identifica caules/flores ou cabeças de cogumelo para balanço orgânico
            FindSwayTargets();
        }

        private void Start()
        {
            // Inicia do tamanho zero e expande elasticamente
            StartCoroutine(ElasticSproutRoutine());

            if (pollenParticles != null && !pollenParticles.isPlaying)
            {
                pollenParticles.Play();
            }
        }

        private void Update()
        {
            if (enableWindSway)
            {
                ApplyWindSway();
            }
        }

        public void Configure(bool isMushroom, AudioClip sfxBounce, ParticleSystem pollenPs)
        {
            isMushroomCluster = isMushroom;
            sfxMushroomBounce = sfxBounce;
            pollenParticles = pollenPs;
            FindSwayTargets();
        }

        private void FindSwayTargets()
        {
            swayTargets.Clear();
            // Busca filhos principais (Flower_X ou Mushroom_X)
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                string n = child.name;
                if (n.Contains("Flower_") || n.Contains("Mushroom_") || n.Contains("Head_"))
                {
                    swayTargets.Add(child);
                }
            }
        }

        private void ApplyWindSway()
        {
            float t = Time.time * windSpeed + randomPhase;
            float swayAngleX = Mathf.Sin(t) * windAngleMax;
            float swayAngleZ = Mathf.Cos(t * 0.75f) * (windAngleMax * 0.8f);

            // Se tiver alvos específicos, oscila eles individualmente com pequeno offset
            if (swayTargets.Count > 0)
            {
                for (int i = 0; i < swayTargets.Count; i++)
                {
                    Transform target = swayTargets[i];
                    if (target == null) continue;
                    float individualOffset = i * 0.85f;
                    float ax = Mathf.Sin(t + individualOffset) * windAngleMax;
                    float az = Mathf.Cos((t + individualOffset) * 0.8f) * windAngleMax;
                    target.localRotation = Quaternion.Euler(ax, 0f, az);
                }
            }
            else
            {
                transform.localRotation = baseRotation * Quaternion.Euler(swayAngleX, 0f, swayAngleZ);
            }
        }

        /// <summary>
        /// Coroutine de crescimento orgânico: parte de 0, ultrapassa 1.15x e estabiliza em 1.0x com física elástica.
        /// </summary>
        private IEnumerator ElasticSproutRoutine()
        {
            transform.localScale = Vector3.zero;
            float elapsed = 0f;

            while (elapsed < growDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / growDuration);

                // Curva matemática de overshoot elástico amortecido
                // f(p) = 1.0 + (overshoot - 1.0) * sin(p * pi) * (1 - p) + overshoot * (1 - cos(p * pi * 0.5))
                float scaleFactor;
                if (customGrowCurve != null && customGrowCurve.length > 0)
                {
                    scaleFactor = customGrowCurve.Evaluate(progress);
                }
                else
                {
                    float p = progress;
                    if (p < 0.65f)
                    {
                        // Crescimento rápido até o ápice elástico
                        float subP = p / 0.65f;
                        scaleFactor = Mathf.Sin(subP * Mathf.PI * 0.5f) * elasticOvershoot;
                    }
                    else
                    {
                        // Amortecimento suave de 1.18 até 1.0
                        float subP = (p - 0.65f) / 0.35f;
                        scaleFactor = Mathf.Lerp(elasticOvershoot, 1.0f, Mathf.SmoothStep(0f, 1f, subP));
                    }
                }

                transform.localScale = initialTargetScale * scaleFactor;
                yield return null;
            }

            transform.localScale = initialTargetScale;
        }

        #region Mushroom Bouncy Interaction
        private void OnTriggerEnter(Collider other)
        {
            if (!isMushroomCluster) return;
            TriggerBounceReaction();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!isMushroomCluster) return;
            TriggerBounceReaction();
        }

        /// <summary>
        /// Aciona o efeito elástico de Squash & Stretch no cogumelo.
        /// </summary>
        public void TriggerBounceReaction()
        {
            if (Time.time - lastBounceTime < minBounceInterval) return;
            lastBounceTime = Time.time;

            // Reproduz som de boing
            if (sfxMushroomBounce != null)
            {
                AudioSource.PlayClipAtPoint(sfxMushroomBounce, transform.position, 0.95f);
            }

            if (activeBounceRoutine != null)
            {
                StopCoroutine(activeBounceRoutine);
            }
            activeBounceRoutine = StartCoroutine(SquashAndStretchRoutine());
        }

        private IEnumerator SquashAndStretchRoutine()
        {
            float elapsed = 0f;
            Vector3 baseScale = initialTargetScale;

            while (elapsed < bounceDuration)
            {
                elapsed += Time.deltaTime;
                float p = Mathf.Clamp01(elapsed / bounceDuration);

                // Curva de mola senoidal amortecida
                float spring = Mathf.Sin(p * Mathf.PI * 4.0f) * Mathf.Exp(-p * 4.0f);

                // Comprime verticalmente (Y) e expande horizontalmente (XZ)
                float squashY = 1.0f - spring * 0.45f;
                float stretchXZ = 1.0f + spring * 0.30f;

                transform.localScale = new Vector3(
                    baseScale.x * stretchXZ,
                    baseScale.y * squashY,
                    baseScale.z * stretchXZ
                );

                yield return null;
            }

            transform.localScale = baseScale;
            activeBounceRoutine = null;
        }
        #endregion
    }
}
