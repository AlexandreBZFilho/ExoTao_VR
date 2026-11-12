using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;
using VRRehab.DronePet;

namespace VRRehab.Interactive.Task06
{
    /// <summary>
    /// Socket magnético XRI para o Rádio Deck com fita cassete (TASK-06).
    /// Detecta a aproximação de MusicCassetteTape, realiza o snap sonoro com travamento mecânico,
    /// comanda a rotação dos carretéis da fita, faz crossfade suave da música no RadioMenu3D,
    /// acende o LED de status via MaterialPropertyBlock (zero clone de materiais / SRP Batcher friendly),
    /// sincroniza afetivamente com a dança do Drone Companheiro (TASK-04),
    /// e suporta ejeção física suave via CassetteEjectButton.
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public class RadioCassetteSocket : XRSocketInteractor
    {
        [Header("Integração com Rádio")]
        [Tooltip("Referência ao rádio 3D principal da mesa.")]
        public RadioMenu3D radioTarget;

        [Tooltip("Transform de ancoragem para a fita encaixada no deck.")]
        public Transform slotAttachPoint;

        [Tooltip("Botão físico de ejeção mecânica.")]
        public CassetteEjectButton ejectButton;

        [Header("Efeitos Sonoros & Iluminação")]
        [Tooltip("Fonte de áudio 3D local do deck para cliques e ejeção.")]
        public AudioSource socketAudioSource;

        [Tooltip("Som de trava e engate mecânico da fita (cassette_insert_click.wav).")]
        public AudioClip sfxInsert;

        [Tooltip("Som de ejeção com mola metálica (cassette_eject_snap.wav).")]
        public AudioClip sfxEject;

        [Tooltip("Luz LED de status na carcaça do deck.")]
        public Light statusLED;

        [Tooltip("MeshRenderer da lente acrílica do LED para MaterialPropertyBlock.")]
        public MeshRenderer ledMeshRenderer;

        [Tooltip("Cor do LED com fita inserida e ativa.")]
        public Color ledColorActive = new Color(0.0f, 0.95f, 1.0f);

        [Tooltip("Cor do LED em standby (sem fita).")]
        public Color ledColorStandby = new Color(0.12f, 0.16f, 0.22f);

        [Range(0.1f, 1.0f)]
        public float ledStandbyIntensity = 0.40f;

        [Range(0.5f, 3.0f)]
        public float ledActiveIntensity = 1.60f;

        [Header("Ajustes de Crossfade e Física")]
        [Tooltip("Tempo de transição de volume entre faixas de áudio (segundos).")]
        public float crossfadeTime = 0.45f;

        [Tooltip("Impulso físico (N·s) para empurrar a fita para fora na ejeção. Fita 0.12 kg => ~1.25 m/s; com a colisão fita↔deck ignorada ela pousa no tampo ~0.23 m à frente do deck.")]
        public float ejectImpulseForce = 0.15f;

        [Tooltip("Velocidade linear máxima (m/s) da fita logo após a ejeção, para nunca ser arremessada para fora da mesa.")]
        public float maxEjectSpeed = 1.5f;

        [Tooltip("Tempo (s) em que a fita ejetada ignora os colliders do deck (Eject_Button, aros laterais, laje) para conseguir sair do slot.")]
        public float ejectIgnoreCollisionSeconds = 0.30f;

        [Tooltip("Cooldown total (s) após a ejeção durante o qual o socket não recaptura fitas.")]
        public float ejectRecaptureCooldownSeconds = 1.0f;

        [Tooltip("Raiz do RadioCassetteDeck (raiz + filhos). Se vazio, usa o pai do socket.")]
        public Transform deckRoot;

        private MusicCassetteTape currentlySocketedTape;
        private Coroutine crossfadeCoroutine;
        private Coroutine ejectCoroutine;
        private AudioClip defaultRadioClip;
        private bool isEjectingCooldown = false;

        // Colliders sólidos do deck (cacheados uma vez) e buffer reutilizável dos colliders da fita ejetada (Zero GC por ejeção)
        private Collider[] deckColliders = System.Array.Empty<Collider>();
        private readonly List<Collider> deckColliderScratch = new List<Collider>(16);
        private readonly List<Collider> ejectedTapeColliders = new List<Collider>(8);
        private bool ejectCollisionIgnored = false;

        // Propriedades de shader cacheadas para MaterialPropertyBlock (Zero GC)
        private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProp = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock ledPropertyBlock;

        public MusicCassetteTape CurrentlySocketedTape => currentlySocketedTape;
        public bool IsEjectingCooldown => isEjectingCooldown;

        protected override void Awake()
        {
            base.Awake();

            // Configuração do socket XRI
            showInteractableHoverMeshes = false;
            recycleDelayTime = 0.6f;

            if (slotAttachPoint != null)
            {
                attachTransform = slotAttachPoint;
            }

            if (socketAudioSource == null)
            {
                socketAudioSource = GetComponent<AudioSource>();
                if (socketAudioSource == null)
                {
                    socketAudioSource = gameObject.AddComponent<AudioSource>();
                }
            }
            socketAudioSource.spatialBlend = 1.0f; // 100% 3D VR Audio
            socketAudioSource.playOnAwake = false;
            socketAudioSource.minDistance = 0.4f;
            socketAudioSource.maxDistance = 6.0f;

            AutoDetectLedComponents();
            CacheDeckColliders();

            if (statusLED != null)
            {
                statusLED.shadows = LightShadows.None; // Regra VR: Zero Dynamic Shadows em luzes cosméticas
            }

            if (radioTarget == null)
            {
                radioTarget = RadioMenu3D.Instancia ?? FindFirstObjectByType<RadioMenu3D>();
            }

            if (radioTarget != null && radioTarget.audioFonte != null)
            {
                defaultRadioClip = radioTarget.audioFonte.clip;
            }

            UpdateStatusLED(false);
        }

        protected override void Start()
        {
            base.Start();

            if (radioTarget == null)
            {
                radioTarget = RadioMenu3D.Instancia ?? FindFirstObjectByType<RadioMenu3D>();
            }
            if (radioTarget != null && radioTarget.audioFonte != null && defaultRadioClip == null)
            {
                defaultRadioClip = radioTarget.audioFonte.clip;
            }
        }

        private void AutoDetectLedComponents()
        {
            if (ledMeshRenderer == null)
            {
                Transform parentRoot = transform.parent != null ? transform.parent : transform;
                foreach (var mr in parentRoot.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr.gameObject.name.Contains("Status_LED"))
                    {
                        ledMeshRenderer = mr;
                        break;
                    }
                }
            }

            if (statusLED == null)
            {
                Transform parentRoot = transform.parent != null ? transform.parent : transform;
                statusLED = parentRoot.GetComponentInChildren<Light>(true);
            }
        }

        public override bool CanSelect(IXRSelectInteractable interactable)
        {
            if (isEjectingCooldown) return false;
            if (!base.CanSelect(interactable)) return false;

            // Filtro estrito: aceita exclusivamente fitas cassete compatíveis
            return interactable.transform.GetComponent<MusicCassetteTape>() != null;
        }

        public override bool CanHover(IXRHoverInteractable interactable)
        {
            if (isEjectingCooldown) return false;
            if (!base.CanHover(interactable)) return false;

            return interactable.transform.GetComponent<MusicCassetteTape>() != null;
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);

            MusicCassetteTape tape = args.interactableObject.transform.GetComponent<MusicCassetteTape>();
            if (tape != null)
            {
                currentlySocketedTape = tape;
                tape.SetPlaying(true);

                // Feedback sonoro mecânico de engate calibrado
                PlaySound(sfxInsert, 0.85f);

                // Feedback háptico tátil no interactor
                if (args.interactorObject is XRBaseControllerInteractor controllerInteractor)
                {
                    controllerInteractor.SendHapticImpulse(0.55f, 0.12f);
                }
                else if (VRFeedbackManager.Instance != null && args.interactorObject is IXRInteractor ixr)
                {
                    VRFeedbackManager.Instance.TriggerHaptic(ixr, 0.55f, 0.12f);
                }

                // Atualiza iluminação de status via MaterialPropertyBlock
                UpdateStatusLED(true, tape.themeColor);

                // Inicia reprodução no rádio vintage
                if (tape.musicClip != null)
                {
                    CrossfadeRadioTrack(tape.musicClip);
                }

                // Sincroniza com a dança afetiva do Drone Companheiro
                NotifyDroneCompanion(tape);
            }
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);

            MusicCassetteTape tape = args.interactableObject.transform.GetComponent<MusicCassetteTape>();
            if (tape != null && tape == currentlySocketedTape)
            {
                tape.SetPlaying(false);
                currentlySocketedTape = null;
                UpdateStatusLED(false);

                // Restaura música padrão de fundo do menu caso não tenha sido ejeção física comandada
                if (!isEjectingCooldown && defaultRadioClip != null)
                {
                    CrossfadeRadioTrack(defaultRadioClip);
                }
            }
        }

        /// <summary>
        /// Acionado pelo botão físico de Eject para liberar e empurrar a fita para fora.
        /// </summary>
        public void EjectCurrentTape()
        {
            if (isEjectingCooldown) return;

            if (currentlySocketedTape == null)
            {
                // Nenhuma fita no deck: apenas clique seco de aviso
                PlaySound(sfxEject, 0.45f);
                return;
            }

            PlaySound(sfxEject, 0.85f);
            ejectCoroutine = StartCoroutine(EjectRoutine());
        }

        protected override void OnDisable()
        {
            // Garante que nenhuma fita fique permanentemente ignorando o deck se o socket for desativado no meio da ejeção
            if (ejectCoroutine != null)
            {
                StopCoroutine(ejectCoroutine);
                ejectCoroutine = null;
            }
            EndEjectCollisionIgnore();
            isEjectingCooldown = false;

            base.OnDisable();
        }

        private IEnumerator EjectRoutine()
        {
            isEjectingCooldown = true;
            MusicCassetteTape tapeToEject = currentlySocketedTape;
            currentlySocketedTape = null;

            tapeToEject.SetPlaying(false);
            UpdateStatusLED(false);

            // Desconecta do socket XRI
            if (interactionManager != null && hasSelection)
            {
                interactionManager.SelectExit(this, tapeToEject);
            }

            // Desliga temporariamente a colisão fita↔deck (Eject_Button, aros, laje) para a fita conseguir sair do slot
            BeginEjectCollisionIgnore(tapeToEject);

            // Impulso físico para projetar a fita suavemente sobre a mesa (Mesa 1)
            Rigidbody rb = tapeToEject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                Vector3 forwardDir = transform.forward;
                Vector3 ejectDir = (forwardDir * 0.70f + Vector3.up * 0.40f).normalized;
                rb.AddForce(ejectDir * ejectImpulseForce, ForceMode.Impulse);
                rb.AddTorque(transform.right * 0.05f, ForceMode.Impulse);

                // AddForce(Impulse) já altera a velocidade imediatamente: limita para manter a fita sobre a mesa
                float maxSpeedSqr = maxEjectSpeed * maxEjectSpeed;
                if (rb.linearVelocity.sqrMagnitude > maxSpeedSqr)
                {
                    rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, maxEjectSpeed);
                }
            }

            // Restaura faixa padrão do menu principal
            if (defaultRadioClip != null)
            {
                CrossfadeRadioTrack(defaultRadioClip);
            }

            // Janela de colisão ignorada (timer sem alocação). Restaura antes se a fita for pega/desativada.
            float elapsed = 0f;
            while (elapsed < ejectIgnoreCollisionSeconds)
            {
                if (tapeToEject == null || !tapeToEject.isActiveAndEnabled || tapeToEject.isSelected) break;
                yield return null;
                elapsed += Time.deltaTime;
            }
            EndEjectCollisionIgnore();

            // Cooldown de segurança para o socket não recapturar a fita imediatamente
            while (elapsed < ejectRecaptureCooldownSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
            isEjectingCooldown = false;
            ejectCoroutine = null;
        }

        private void CacheDeckColliders()
        {
            Transform root = deckRoot != null ? deckRoot : (transform.parent != null ? transform.parent : transform);

            deckColliderScratch.Clear();
            root.GetComponentsInChildren(true, deckColliderScratch);

            int solidCount = 0;
            for (int i = 0; i < deckColliderScratch.Count; i++)
            {
                if (IsDeckSolidCollider(deckColliderScratch[i])) solidCount++;
            }

            // Eject_Button fora da hierarquia do deck: inclui seus colliders também
            if (ejectButton != null && !ejectButton.transform.IsChildOf(root))
            {
                var extra = ejectButton.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < extra.Length; i++)
                {
                    deckColliderScratch.Add(extra[i]);
                    if (IsDeckSolidCollider(extra[i])) solidCount++;
                }
            }

            deckColliders = new Collider[solidCount];
            int idx = 0;
            for (int i = 0; i < deckColliderScratch.Count; i++)
            {
                if (IsDeckSolidCollider(deckColliderScratch[i])) deckColliders[idx++] = deckColliderScratch[i];
            }
            deckColliderScratch.Clear();
        }

        private static bool IsDeckSolidCollider(Collider c)
        {
            // Apenas colliders sólidos do deck; nunca colliders de fitas (a fita inicial pode estar aninhada no deck)
            return c != null && !c.isTrigger && c.GetComponentInParent<MusicCassetteTape>() == null;
        }

        private static bool IsColliderLive(Collider c)
        {
            return c != null && c.enabled && c.gameObject.activeInHierarchy;
        }

        private void BeginEjectCollisionIgnore(MusicCassetteTape tape)
        {
            EndEjectCollisionIgnore();
            if (tape == null || deckColliders.Length == 0) return;

            ejectedTapeColliders.Clear();
            tape.GetComponentsInChildren(false, ejectedTapeColliders);

            for (int t = 0; t < ejectedTapeColliders.Count; t++)
            {
                Collider tc = ejectedTapeColliders[t];
                if (!IsColliderLive(tc) || tc.isTrigger) continue;
                for (int d = 0; d < deckColliders.Length; d++)
                {
                    Collider dc = deckColliders[d];
                    if (!IsColliderLive(dc)) continue;
                    Physics.IgnoreCollision(tc, dc, true);
                }
            }
            ejectCollisionIgnored = true;
        }

        private void EndEjectCollisionIgnore()
        {
            if (!ejectCollisionIgnored) return;
            ejectCollisionIgnored = false;

            for (int t = 0; t < ejectedTapeColliders.Count; t++)
            {
                Collider tc = ejectedTapeColliders[t];
                // Collider desativado/destruído já perde o estado de ignore no PhysX
                if (!IsColliderLive(tc) || tc.isTrigger) continue;
                for (int d = 0; d < deckColliders.Length; d++)
                {
                    Collider dc = deckColliders[d];
                    if (!IsColliderLive(dc)) continue;
                    Physics.IgnoreCollision(tc, dc, false);
                }
            }
            ejectedTapeColliders.Clear();
        }

        private void CrossfadeRadioTrack(AudioClip newClip)
        {
            if (radioTarget == null)
            {
                radioTarget = RadioMenu3D.Instancia ?? FindFirstObjectByType<RadioMenu3D>();
            }

            if (radioTarget == null || radioTarget.audioFonte == null) return;

            if (crossfadeCoroutine != null)
            {
                StopCoroutine(crossfadeCoroutine);
            }

            crossfadeCoroutine = StartCoroutine(CrossfadeRoutine(newClip));
        }

        private IEnumerator CrossfadeRoutine(AudioClip newClip)
        {
            AudioSource audio = radioTarget.audioFonte;
            float initialVol = audio.volume;
            float halfDuration = Mathf.Max(0.1f, crossfadeTime * 0.5f);

            // 1. Fade Out
            float elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                audio.volume = Mathf.Lerp(initialVol, 0f, elapsed / halfDuration);
                yield return null;
            }

            // 2. Troca de Clip e Play
            audio.clip = newClip;
            audio.time = 0f;
            if (!audio.isPlaying && radioTarget.ligado)
            {
                audio.Play();
            }

            // Garante que o rádio esteja ligado
            if (!radioTarget.ligado)
            {
                radioTarget.AlternarLigadoDesligado();
            }

            // 3. Fade In
            elapsed = 0f;
            float targetVol = radioTarget.volumeBase;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                audio.volume = Mathf.Lerp(0f, targetVol, elapsed / halfDuration);
                yield return null;
            }
            audio.volume = targetVol;
            crossfadeCoroutine = null;
        }

        private void NotifyDroneCompanion(MusicCassetteTape tape)
        {
            if (DroneAffectiveSystem.Instance != null)
            {
                if (radioTarget != null && radioTarget.audioFonte != null)
                {
                    DroneAffectiveSystem.Instance.musicSource = radioTarget.audioFonte;
                }
                DroneAffectiveSystem.Instance.enableMusicDance = true;
            }
        }

        private void PlaySound(AudioClip clip, float volume = 1.0f)
        {
            if (clip == null) return;

            if (socketAudioSource != null)
            {
                socketAudioSource.pitch = Random.Range(0.97f, 1.03f);
                socketAudioSource.PlayOneShot(clip, volume);
            }
            else if (VRFeedbackManager.Instance != null)
            {
                VRFeedbackManager.Instance.PlaySound(clip, transform.position, volume, 0.97f, 1.03f);
            }
            else
            {
                AudioSource.PlayClipAtPoint(clip, transform.position, volume);
            }
        }

        private void UpdateStatusLED(bool active, Color? activeColor = null)
        {
            Color targetColor = active ? (activeColor ?? ledColorActive) : ledColorStandby;
            float intensity = active ? ledActiveIntensity : ledStandbyIntensity;

            // 1. Atualização do domo acrílico via MaterialPropertyBlock (Zero Clone / SRP Batcher Friendly)
            if (ledMeshRenderer != null)
            {
                if (ledPropertyBlock == null)
                {
                    ledPropertyBlock = new MaterialPropertyBlock();
                }

                ledMeshRenderer.GetPropertyBlock(ledPropertyBlock);

                Color finalEmissive = targetColor * intensity;
                ledPropertyBlock.SetColor(EmissionColorProp, finalEmissive);
                ledPropertyBlock.SetColor(BaseColorProp, targetColor);
                ledPropertyBlock.SetColor(ColorProp, targetColor);

                ledMeshRenderer.SetPropertyBlock(ledPropertyBlock);
            }

            // 2. Luz pontual sem sombras extras
            if (statusLED != null)
            {
                statusLED.color = targetColor;
                statusLED.intensity = intensity;
                statusLED.shadows = LightShadows.None;
                statusLED.enabled = true;
            }
        }
    }
}
