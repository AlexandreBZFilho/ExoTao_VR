using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;

namespace VRRehab.Interactive.Task06
{
    /// <summary>
    /// Socket magnético XRI para o Rádio Deck com fita cassete.
    /// Detecta a aproximação de MusicCassetteTape, realiza o snap sonoro com travamento mecânico,
    /// comanda a rotação dos carretéis da fita, faz crossfade suave da música no RadioMenu3D
    /// e suporta ejeção física via CassetteEjectButton.
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

        [Tooltip("Som de trava e engate mecânico da fita.")]
        public AudioClip sfxInsert;

        [Tooltip("Som de ejeção com mola metálica.")]
        public AudioClip sfxEject;

        [Tooltip("Luz LED de status na carcaça do deck.")]
        public Light statusLED;

        [Tooltip("Cor do LED com fita inserida e ativa.")]
        public Color ledColorActive = new Color(0.0f, 0.95f, 1.0f);

        [Tooltip("Cor do LED em standby (sem fita).")]
        public Color ledColorStandby = new Color(0.25f, 0.25f, 0.30f);

        [Header("Ajustes de Crossfade e Física")]
        [Tooltip("Tempo de transição de volume entre faixas de áudio (segundos).")]
        public float crossfadeTime = 0.50f;

        [Tooltip("Força do impulso físico para empurrar a fita para fora na ejeção.")]
        public float ejectImpulseForce = 0.55f;

        private MusicCassetteTape currentlySocketedTape;
        private Coroutine crossfadeCoroutine;
        private AudioClip defaultRadioClip;
        private bool isEjectingCooldown = false;

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
            socketAudioSource.spatialBlend = 1.0f;
            socketAudioSource.playOnAwake = false;

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

        public override bool CanSelect(IXRSelectInteractable interactable)
        {
            if (isEjectingCooldown) return false;
            if (!base.CanSelect(interactable)) return false;

            // Aceita exclusivamente fitas cassete compatíveis
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

                // Feedback sonoro de inserção mecânica
                PlaySound(sfxInsert);

                // Feedback háptico no interactor se houver
                if (args.interactorObject is XRBaseControllerInteractor controllerInteractor)
                {
                    controllerInteractor.SendHapticImpulse(0.5f, 0.12f);
                }

                // Atualiza iluminação de status
                UpdateStatusLED(true, tape.themeColor);

                // Inicia reprodução no rádio
                if (tape.musicClip != null)
                {
                    CrossfadeRadioTrack(tape.musicClip);
                }
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

                // Restaura música padrão de fundo do menu
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
            if (currentlySocketedTape == null)
            {
                // Nenhum fita para ejetar, apenas clique seco do botão
                PlaySound(sfxEject, 0.8f);
                return;
            }

            PlaySound(sfxEject);
            StartCoroutine(EjectRoutine());
        }

        private IEnumerator EjectRoutine()
        {
            isEjectingCooldown = true;
            MusicCassetteTape tapeToEject = currentlySocketedTape;
            currentlySocketedTape = null;

            tapeToEject.SetPlaying(false);
            UpdateStatusLED(false);

            // Desconecta do interactor
            if (interactionManager != null && hasSelection)
            {
                interactionManager.SelectExit(this, tapeToEject);
            }

            // Impulso físico para cuspir a fita suavemente sobre a mesa
            Rigidbody rb = tapeToEject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                Vector3 ejectDir = (transform.forward * 0.70f + Vector3.up * 0.40f).normalized;
                rb.AddForce(ejectDir * ejectImpulseForce, ForceMode.Impulse);
                rb.AddTorque(transform.right * 0.05f, ForceMode.Impulse);
            }

            // Restaura faixa padrão de menu
            if (defaultRadioClip != null)
            {
                CrossfadeRadioTrack(defaultRadioClip);
            }

            // Intervalo de segurança para não reengatar a fita imediatamente
            yield return new WaitForSeconds(1.0f);
            isEjectingCooldown = false;
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

            // Fade out
            float elapsed = 0f;
            while (elapsed < crossfadeTime * 0.5f)
            {
                elapsed += Time.deltaTime;
                audio.volume = Mathf.Lerp(initialVol, 0f, elapsed / (crossfadeTime * 0.5f));
                yield return null;
            }

            // Troca o clip
            audio.clip = newClip;
            audio.time = 0f;
            if (!audio.isPlaying && radioTarget.ligado)
            {
                audio.Play();
            }

            // Garante que o rádio ligue se estava desligado
            if (!radioTarget.ligado)
            {
                radioTarget.AlternarLigadoDesligado();
            }

            // Fade in
            elapsed = 0f;
            float targetVol = radioTarget.volumeBase;
            while (elapsed < crossfadeTime * 0.5f)
            {
                elapsed += Time.deltaTime;
                audio.volume = Mathf.Lerp(0f, targetVol, elapsed / (crossfadeTime * 0.5f));
                yield return null;
            }
            audio.volume = targetVol;
            crossfadeCoroutine = null;
        }

        private void PlaySound(AudioClip clip, float volume = 1.0f)
        {
            if (clip == null) return;
            if (socketAudioSource != null)
            {
                socketAudioSource.pitch = Random.Range(0.96f, 1.04f);
                socketAudioSource.PlayOneShot(clip, volume);
            }
            else
            {
                AudioSource.PlayClipAtPoint(clip, transform.position, volume);
            }
        }

        private void UpdateStatusLED(bool active, Color? activeColor = null)
        {
            if (statusLED == null) return;

            statusLED.color = active ? (activeColor ?? ledColorActive) : ledColorStandby;
            statusLED.intensity = active ? 1.6f : 0.4f;
        }
    }
}
