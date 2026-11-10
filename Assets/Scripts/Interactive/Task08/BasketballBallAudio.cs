using System;
using UnityEngine;

namespace VRRehab.Interactive.Task08
{
    /// <summary>
    /// Componente acoplado à MiniBasketball para rastreamento de quiques com áudio realista
    /// e monitoramento de impactos com o aro metálico (para diferenciar Swish de cesta com aro).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class BasketballBallAudio : MonoBehaviour
    {
        [Header("Áudio de Quique")]
        [SerializeField] private AudioClip bounceClip;
        [SerializeField] private float minBounceVelocity = 0.45f;
        [SerializeField] private float maxBounceVelocity = 4.5f;

        [Header("Rastreamento de Cesta")]
        public bool hasTouchedRimThisShot = false;
        public float topTriggerEnterTimestamp = -1f;

        private AudioSource audioSource;
        private Rigidbody rb;
        private float lastBounceTime = -1f;

        public Rigidbody AttachedRigidbody => rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1.0f;
                audioSource.minDistance = 0.5f;
                audioSource.maxDistance = 15.0f;
                audioSource.playOnAwake = false;
            }
        }

        public void SetBounceClip(AudioClip clip)
        {
            bounceClip = clip;
        }

        public void ResetShotTracking()
        {
            hasTouchedRimThisShot = false;
            topTriggerEnterTimestamp = -1f;
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Verifica impacto com o aro
            if (collision.gameObject.name.Contains("Rim") || collision.gameObject.CompareTag("HoopRim"))
            {
                hasTouchedRimThisShot = true;
            }

            // Reproduz som de quique
            if (bounceClip == null || audioSource == null) return;

            float impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed < minBounceVelocity) return;

            if (Time.time - lastBounceTime < 0.08f) return; // Evita metralhadora de áudio
            lastBounceTime = Time.time;

            float t = Mathf.Clamp01((impactSpeed - minBounceVelocity) / (maxBounceVelocity - minBounceVelocity));
            float volume = Mathf.Lerp(0.25f, 1.0f, t);
            float pitch = UnityEngine.Random.Range(0.92f, 1.08f);

            audioSource.pitch = pitch;
            audioSource.PlayOneShot(bounceClip, volume);
        }
    }
}
