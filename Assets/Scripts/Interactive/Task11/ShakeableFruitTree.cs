using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;

namespace VRRehab.Interactive.Task11
{
    /// <summary>
    /// Árvore Frutífera Sacudível para o mapa da Floresta (TASK-11).
    /// Permite que o paciente aponte o raio laser VR (XRRayInteractor) para a copa,
    /// aperte o gatilho e acione o sacudir dos galhos, farfalhar de folhas,
    /// partículas de folhas cadentes e queda física de 2 a 4 maçãs (com chance de maçã dourada).
    /// </summary>
    [DisallowMultipleComponent]
    public class ShakeableFruitTree : MonoBehaviour
    {
        [Header("Prefabs de Frutas")]
        [SerializeField] private GameObject redApplePrefab;
        [SerializeField] private GameObject goldenApplePrefab;

        [Header("Configuração de Queda")]
        [Tooltip("Quantidade mínima de maçãs por sacudida")]
        [SerializeField] private int minApplesPerShake = 2;
        [Tooltip("Quantidade máxima de maçãs por sacudida")]
        [SerializeField] private int maxApplesPerShake = 4;
        [Tooltip("Probabilidade de gerar uma maçã dourada bônus (0.0 a 1.0)")]
        [Range(0f, 1f)]
        [SerializeField] private float goldenAppleChance = 0.25f;

        [Header("Cooldown e Dinâmica")]
        [Tooltip("Tempo de recarga entre sacudidas (segundos)")]
        [SerializeField] private float shakeCooldown = 3.5f;
        [Tooltip("Duração da animação de tremor da copa (segundos)")]
        [SerializeField] private float shakeDuration = 1.2f;
        [Tooltip("Intensidade angular máxima de tremor (graus)")]
        [SerializeField] private float shakeAmplitude = 4.5f;
        [Tooltip("Frequência de oscilação do tremor (Hz)")]
        [SerializeField] private float shakeFrequency = 14.0f;

        [Header("Áudio e Efeitos Visuais")]
        [SerializeField] private AudioClip sfxLeavesRustle;
        [SerializeField] private ParticleSystem fallingLeavesFx;
        [SerializeField] private Transform canopyTransform;

        [Header("Área de Disparo na Copa")]
        [SerializeField] private Vector3 canopyCenterOffset = new Vector3(0f, 3.8f, 0f);
        [SerializeField] private Vector3 canopySpawnRadius = new Vector3(1.2f, 0.4f, 1.2f);

        private XRSimpleInteractable interactable;
        private bool isShaking = false;
        private float lastShakeTime = -99f;
        private Quaternion initialCanopyRot;
        private Coroutine shakeCoroutine;

        public bool CanShake => Time.time - lastShakeTime >= shakeCooldown && !isShaking;

        private void Awake()
        {
            if (canopyTransform == null)
            {
                canopyTransform = transform;
            }
            initialCanopyRot = canopyTransform.localRotation;

            // Configura o XRSimpleInteractable para detecção por raio laser
            interactable = GetComponent<XRSimpleInteractable>();
            if (interactable == null)
            {
                interactable = gameObject.AddComponent<XRSimpleInteractable>();
            }

            // Garante collider na copa se não houver
            Collider col = GetComponent<Collider>();
            if (col == null)
            {
                SphereCollider sc = gameObject.AddComponent<SphereCollider>();
                sc.center = canopyCenterOffset;
                sc.radius = 2.0f;
            }

            interactable.selectEntered.AddListener(OnTreeSelected);
            interactable.activated.AddListener(OnTreeActivated);
        }

        private void OnDestroy()
        {
            if (interactable != null)
            {
                interactable.selectEntered.RemoveListener(OnTreeSelected);
                interactable.activated.RemoveListener(OnTreeActivated);
            }
        }

        private void OnTreeSelected(SelectEnterEventArgs args)
        {
            TryShakeTree();
        }

        private void OnTreeActivated(ActivateEventArgs args)
        {
            TryShakeTree();
        }

        /// <summary>
        /// Aciona o tremor e a queda de maçãs caso o cooldown permita.
        /// </summary>
        public bool TryShakeTree()
        {
            if (!CanShake) return false;

            lastShakeTime = Time.time;
            if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
            shakeCoroutine = StartCoroutine(ShakeRoutine());
            return true;
        }

        private IEnumerator ShakeRoutine()
        {
            isShaking = true;

            // 1. Áudio de farfalhar de folhas
            if (sfxLeavesRustle != null)
            {
                Vector3 soundPos = transform.position + canopyCenterOffset;
                VRFeedbackManager.Instance?.PlaySound(sfxLeavesRustle, soundPos, 0.95f, 0.95f, 1.05f);
            }

            // 2. Partículas de folhas cadentes
            if (fallingLeavesFx != null)
            {
                fallingLeavesFx.transform.position = transform.position + canopyCenterOffset;
                fallingLeavesFx.Play();
            }

            // 3. Drop de Maçãs da copa
            SpawnFallingApples();

            // 4. Tremor procedural harmônico amortecido da copa
            float elapsed = 0f;
            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / shakeDuration;
                float decay = Mathf.Exp(-3.2f * progress);
                float angleX = Mathf.Sin(elapsed * shakeFrequency * 2f * Mathf.PI) * shakeAmplitude * decay;
                float angleZ = Mathf.Cos(elapsed * (shakeFrequency * 0.85f) * 2f * Mathf.PI) * (shakeAmplitude * 0.8f) * decay;

                canopyTransform.localRotation = initialCanopyRot * Quaternion.Euler(angleX, 0f, angleZ);
                yield return null;
            }

            canopyTransform.localRotation = initialCanopyRot;
            isShaking = false;
        }

        private void SpawnFallingApples()
        {
            int count = Random.Range(minApplesPerShake, maxApplesPerShake + 1);
            bool hasGolden = (goldenApplePrefab != null) && (Random.value < goldenAppleChance);

            for (int i = 0; i < count; i++)
            {
                bool isThisAppleGolden = (i == 0 && hasGolden);
                GameObject prefabToUse = isThisAppleGolden ? goldenApplePrefab : redApplePrefab;
                if (prefabToUse == null) prefabToUse = redApplePrefab;
                if (prefabToUse == null) continue;

                // Calcula posição aleatória no volume da copa
                Vector3 localOffset = new Vector3(
                    Random.Range(-canopySpawnRadius.x, canopySpawnRadius.x),
                    Random.Range(-canopySpawnRadius.y, canopySpawnRadius.y),
                    Random.Range(-canopySpawnRadius.z, canopySpawnRadius.z)
                );
                Vector3 spawnPos = transform.position + canopyCenterOffset + localOffset;
                Quaternion spawnRot = Random.rotation;

                GameObject appleInstance = Instantiate(prefabToUse, spawnPos, spawnRot);
                appleInstance.name = isThisAppleGolden ? $"GoldenBonusApple_{Time.frameCount}_{i}" : $"InteractiveRedApple_{Time.frameCount}_{i}";

                // Impulso inicial suave para espalhar as maçãs no solo
                Rigidbody rb = appleInstance.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 impulse = new Vector3(
                        Random.Range(-0.8f, 0.8f),
                        Random.Range(-0.2f, 0.5f),
                        Random.Range(-0.8f, 0.8f)
                    );
                    rb.AddForce(impulse, ForceMode.VelocityChange);
                    rb.AddTorque(Random.insideUnitSphere * 4f, ForceMode.VelocityChange);
                }
            }
        }

        public void Configure(GameObject redPrefab, GameObject goldenPrefab, AudioClip leavesSfx, ParticleSystem leavesFx, Transform canopy, Vector3 centerOffset)
        {
            redApplePrefab = redPrefab;
            goldenApplePrefab = goldenPrefab;
            sfxLeavesRustle = leavesSfx;
            fallingLeavesFx = leavesFx;
            canopyTransform = canopy != null ? canopy : transform;
            canopyCenterOffset = centerOffset;
            initialCanopyRot = canopyTransform.localRotation;
        }
    }
}
