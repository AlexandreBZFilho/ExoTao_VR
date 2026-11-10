using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;
using VRRehab.Ergonomics;

namespace VRRehab.Interactive.Task12
{
    /// <summary>
    /// Pinhão Mágico Arremessável para Reabilitação em VR.
    /// Herda de ErgonomicGrabInteractable (Force Grab facilitado + mira assistida).
    /// Ao ser arremessado e colidir com o solo/terreno:
    /// 1. Toca sons de impacto místico e brotação elástica;
    /// 2. Dispara partículas de explosão de pólen/brilho;
    /// 3. Instancia proceduralmente um Canteiro de Flores ou Canteiro de Cogumelos alinhado à normal do solo;
    /// 4. Desaparece em fumaça mágica e reaparece em seu pedestal após 3.0 segundos.
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public class MagicSeedPinecone : ErgonomicGrabInteractable
    {
        [Header("Prefabs Botânicos Procedurais")]
        [SerializeField] private GameObject flowerPatchPrefab;
        [SerializeField] private GameObject mushroomClusterPrefab;

        [Header("Efeitos Sonoros de Reabilitação")]
        [SerializeField] private AudioClip sfxPickupChime;
        [SerializeField] private AudioClip sfxImpactBurst;
        [SerializeField] private AudioClip sfxSproutElastic;

        [Header("Partículas Mágicas")]
        [SerializeField] private ParticleSystem auraGlowParticles;
        [SerializeField] private ParticleSystem impactBurstParticles;

        [Header("Parâmetros de Brotação e Respawn")]
        [Tooltip("Tempo de espera (em segundos) antes de reaparecer no ninho do pedestal.")]
        [SerializeField] private float respawnDelayTime = 3.0f;

        [Tooltip("Velocidade mínima de colisão ou tempo após arremesso para acionar brotação.")]
        [SerializeField] private float minImpactVelocity = 0.4f;

        [Tooltip("Alterna 50/50 entre flores e cogumelos")]
        [SerializeField] private bool randomizePlantType = true;

        private bool isThrown = false;
        private bool hasSprouted = false;
        private bool isRespawningRoutineActive = false;
        private Transform visualRoot;
        private Collider[] allColliders;
        private Rigidbody myRb;
        private bool initialKinematicState;

        protected override void Awake()
        {
            base.Awake();

            myRb = GetComponent<Rigidbody>();
            if (myRb != null)
            {
                initialKinematicState = myRb.isKinematic;
            }

            allColliders = GetComponentsInChildren<Collider>();
            visualRoot = transform.Find("Visual_Model");
            if (visualRoot == null && transform.childCount > 0)
            {
                visualRoot = transform.GetChild(0);
            }
        }

        public void Configure(GameObject flowerPrefab, GameObject mushroomPrefab,
                              AudioClip sfxPickup, AudioClip sfxBurst, AudioClip sfxSprout,
                              ParticleSystem auraPs, ParticleSystem burstPs)
        {
            flowerPatchPrefab = flowerPrefab;
            mushroomClusterPrefab = mushroomPrefab;
            sfxPickupChime = sfxPickup;
            sfxImpactBurst = sfxBurst;
            sfxSproutElastic = sfxSprout;
            auraGlowParticles = auraPs;
            impactBurstParticles = burstPs;
        }

        #region XRI Overrides
        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);

            isThrown = false;

            // Chime agradável ao puxar com o raio do laser ou pegar com a mão
            if (sfxPickupChime != null)
            {
                AudioSource.PlayClipAtPoint(sfxPickupChime, transform.position, 0.90f);
            }

            // Ativa aura mágica cintilante na mão
            if (auraGlowParticles != null && !auraGlowParticles.isPlaying)
            {
                auraGlowParticles.Play();
            }
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);

            // Objeto foi arremessado / liberado pelo paciente
            isThrown = true;
            hasSprouted = false;
        }
        #endregion

        #region Physics & Sprouting Logic
        private void OnCollisionEnter(Collision collision)
        {
            if (!isThrown || hasSprouted || isRespawningRoutineActive) return;

            // Ignora colisões fracas ou contra a mão do jogador
            if (collision.relativeVelocity.magnitude < minImpactVelocity) return;

            // Verifica se colidiu com terreno ou chão
            ContactPoint contact = collision.GetContact(0);
            Vector3 hitPoint = contact.point;
            Vector3 hitNormal = contact.normal;

            // Deve colidir com uma superfície razoavelmente virada para cima (chão / topo de rocha)
            if (hitNormal.y > 0.30f)
            {
                ExecuteSprout(hitPoint, hitNormal);
            }
        }

        /// <summary>
        /// Realiza a brotação mágica e o ciclo de retorno ao ninho.
        /// </summary>
        public GameObject ExecuteSprout(Vector3 spawnPoint, Vector3 groundNormal, bool? forceMushroom = null)
        {
            hasSprouted = true;
            isThrown = false;

            // 1. Áudios de impacto e brotação elástica
            if (sfxImpactBurst != null)
            {
                AudioSource.PlayClipAtPoint(sfxImpactBurst, spawnPoint, 1.0f);
            }
            if (sfxSproutElastic != null)
            {
                AudioSource.PlayClipAtPoint(sfxSproutElastic, spawnPoint, 1.0f);
            }

            // 2. Partículas de impacto e pólen luminoso
            if (impactBurstParticles != null)
            {
                impactBurstParticles.transform.position = spawnPoint;
                impactBurstParticles.transform.rotation = Quaternion.LookRotation(groundNormal);
                impactBurstParticles.Play();
            }

            // 3. Escolhe o Prefab a brotar (Flores ou Cogumelos)
            bool spawnMushroom = forceMushroom.HasValue ? forceMushroom.Value :
                                 (randomizePlantType ? (Random.value > 0.5f) : false);

            GameObject chosenPrefab = spawnMushroom ? mushroomClusterPrefab : flowerPatchPrefab;
            GameObject sproutedInstance = null;

            if (chosenPrefab != null)
            {
                // Alinhamento perfeito com a normal do solo
                Quaternion groundRotation = Quaternion.FromToRotation(Vector3.up, groundNormal);
                // Leve rotação aleatória no eixo Y da normal para variação estética
                Quaternion randomYaw = Quaternion.AngleAxis(Random.Range(0f, 360f), groundNormal);
                Quaternion finalRot = randomYaw * groundRotation;

                sproutedInstance = Instantiate(chosenPrefab, spawnPoint, finalRot);
                sproutedInstance.name = spawnMushroom ? $"Sprouted_MushroomCluster_{Time.frameCount}" : $"Sprouted_FlowerPatch_{Time.frameCount}";
            }

            // 4. Inicia ciclo de respawn (some imediatamente e reaparece no ninho após 3s)
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(DisappearAndRespawnRoutine());
            }

            return sproutedInstance;
        }

        private IEnumerator DisappearAndRespawnRoutine()
        {
            isRespawningRoutineActive = true;

            // Desativa colisão e renderizadores da semente
            SetVisibilityAndPhysics(false);

            // Aguarda o tempo de recarga
            yield return new WaitForSeconds(respawnDelayTime);

            // Reposiciona no pedestal de origem
            transform.position = HomePosition;
            transform.rotation = HomeRotation;

            if (myRb != null)
            {
                myRb.linearVelocity = Vector3.zero;
                myRb.angularVelocity = Vector3.zero;
                myRb.isKinematic = initialKinematicState;
            }

            // Reativa a semente com efeito de aura
            SetVisibilityAndPhysics(true);

            if (auraGlowParticles != null)
            {
                auraGlowParticles.Play();
            }

            hasSprouted = false;
            isThrown = false;
            isRespawningRoutineActive = false;
        }

        private void SetVisibilityAndPhysics(bool visible)
        {
            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(visible);
            }
            else
            {
                Renderer[] rends = GetComponentsInChildren<Renderer>(true);
                foreach (var r in rends)
                {
                    r.enabled = visible;
                }
            }

            if (allColliders != null)
            {
                foreach (var col in allColliders)
                {
                    if (col != null) col.enabled = visible;
                }
            }

            if (myRb != null)
            {
                myRb.isKinematic = !visible || initialKinematicState;
            }
        }
        #endregion
    }
}
