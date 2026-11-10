using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;

namespace VRRehab.Interactive.Task09
{
    /// <summary>
    /// Mascote Fidget Bobblehead com oscilador harmônico amortecido em C#.
    /// Reage a toques do raio laser VR, toques diretos de mão ou colisões físicas
    /// com brinquedos (bolas de basquete, fitas cassete, bastões), balançando a cabeça
    /// de forma elástica, cômica e fluida com áudio procedural de mola.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class BobbleheadSpringToy : MonoBehaviour
    {
        [Header("Hierarquia do Bobblehead")]
        [SerializeField] private Transform headTransform;
        [SerializeField] private Renderer visorRenderer;
        [SerializeField] private Light visorGlowLight;

        [Header("Parâmetros do Oscilador Harmônico")]
        [Tooltip("Frequência natural de oscilação da mola (rad/s)")]
        [SerializeField] private float naturalFrequency = 13.5f;

        [Tooltip("Razão de amortecimento (0.10 a 0.20 para balanço elástico duradouro)")]
        [SerializeField] private float dampingRatio = 0.14f;

        [Tooltip("Ângulo máximo de deflexão permitido (graus)")]
        [SerializeField] private float maxDeflectionAngle = 45f;

        [Header("Efeitos Sonoros e Hápticos")]
        [SerializeField] private AudioClip sfxBoing;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private float minImpulseForSound = 8.0f;

        // Estado do oscilador: X = Pitch (frente/trás), Y = Roll (laterais)
        private Vector2 angularDisplacement = Vector2.zero;
        private Vector2 angularVelocity = Vector2.zero;
        private Quaternion headBaseLocalRotation = Quaternion.identity;

        private XRSimpleInteractable xrInteractable;
        private float lastSoundTime = 0f;

        public Vector2 AngularDisplacement => angularDisplacement;

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (headTransform != null)
            {
                headBaseLocalRotation = headTransform.localRotation;
            }

            SetupXRInteraction();
        }

        private void SetupXRInteraction()
        {
            xrInteractable = GetComponent<XRSimpleInteractable>();
            if (xrInteractable == null)
            {
                xrInteractable = gameObject.AddComponent<XRSimpleInteractable>();
            }

            xrInteractable.selectEntered.RemoveListener(OnSelectEntered);
            xrInteractable.selectEntered.AddListener(OnSelectEntered);

            xrInteractable.hoverEntered.RemoveListener(OnHoverEntered);
            xrInteractable.hoverEntered.AddListener(OnHoverEntered);
        }

        private void OnDestroy()
        {
            if (xrInteractable != null)
            {
                xrInteractable.selectEntered.RemoveListener(OnSelectEntered);
                xrInteractable.hoverEntered.RemoveListener(OnHoverEntered);
            }
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            // Cutucada forte ao clicar com o gatilho
            Vector2 randomImpulse = new Vector2(
                UnityEngine.Random.Range(-28f, 28f),
                UnityEngine.Random.Range(-28f, 28f)
            );
            ApplyImpulse(randomImpulse);
            TriggerHaptic(args.interactorObject, 0.65f, 0.12f);
        }

        private void OnHoverEntered(HoverEnterEventArgs args)
        {
            // Cutucada sutil ao passar a mira do laser
            Vector2 gentleImpulse = new Vector2(
                UnityEngine.Random.Range(-7f, 7f),
                UnityEngine.Random.Range(-7f, 7f)
            );
            ApplyImpulse(gentleImpulse);
            TriggerHaptic(args.interactorObject, 0.25f, 0.05f);
        }

        private void TriggerHaptic(IXRInteractor interactor, float amplitude, float duration)
        {
            if (interactor != null)
            {
                VRFeedbackManager.Instance?.TriggerHaptic(interactor, amplitude, duration);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || headTransform == null) return;

            // Limite de passo temporal para estabilidade numérica
            dt = Mathf.Min(dt, 0.033f);

            // Equação diferencial do oscilador harmônico amortecido em 2D:
            // a = -2 * zeta * omega * v - omega^2 * x
            Vector2 acceleration = -2f * dampingRatio * naturalFrequency * angularVelocity 
                                   - (naturalFrequency * naturalFrequency) * angularDisplacement;

            // Integração semi-implícita de Euler
            angularVelocity += acceleration * dt;
            angularDisplacement += angularVelocity * dt;

            // Limita a deflexão máxima para manter a estética
            angularDisplacement = Vector2.ClampMagnitude(angularDisplacement, maxDeflectionAngle);

            // Aplica rotação ao pivô da cabeça
            Quaternion springRotation = Quaternion.Euler(angularDisplacement.x, 0f, angularDisplacement.y);
            headTransform.localRotation = headBaseLocalRotation * springRotation;

            // Pulsação suave do visor quando a cabeça está balançando
            float speed = angularVelocity.magnitude;
            if (visorGlowLight != null)
            {
                visorGlowLight.intensity = Mathf.Lerp(0.8f, 2.2f, Mathf.Clamp01(speed / 60f));
            }
        }

        /// <summary>
        /// Aplica um impulso angular à cabeça do mascote.
        /// </summary>
        public void ApplyImpulse(Vector2 impulse)
        {
            angularVelocity += impulse;

            float mag = impulse.magnitude;
            if (mag >= minImpulseForSound && Time.time - lastSoundTime > 0.15f)
            {
                lastSoundTime = Time.time;
                PlayBoingSound(mag);
            }
        }

        private void PlayBoingSound(float intensity)
        {
            if (audioSource != null && sfxBoing != null)
            {
                // Varia o pitch de acordo com o vigor da oscilação
                audioSource.pitch = Mathf.Clamp(0.92f + (intensity / 100f), 0.85f, 1.35f);
                audioSource.PlayOneShot(sfxBoing, Mathf.Clamp01(intensity / 40f) * 0.90f);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Se um brinquedo arremessável colidir com a cabeça ou corpo do mascote
            Vector3 relativeVelocity = collision.relativeVelocity;
            float speed = relativeVelocity.magnitude;

            if (speed > 0.2f)
            {
                // Converte a direção do impacto em coordenadas locais da cabeça
                Vector3 localImpact = transform.InverseTransformDirection(relativeVelocity);
                Vector2 impactImpulse = new Vector2(localImpact.z, localImpact.x) * (speed * 12.0f);
                ApplyImpulse(impactImpulse);
            }
        }

        /// <summary>
        /// Teste rápido de vibração/oscilação pelo menu de contexto no Unity Inspector.
        /// </summary>
        [ContextMenu("Boing Test")]
        public void BoingTest()
        {
            ApplyImpulse(new Vector2(35f, -25f));
        }
    }
}
