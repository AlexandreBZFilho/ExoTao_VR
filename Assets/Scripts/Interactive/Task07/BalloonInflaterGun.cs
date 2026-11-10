using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;
using VRRehab.Ergonomics;

namespace VRRehab.Interactive.Task07
{
    /// <summary>
    /// Pistola/Dispenser Ergonômico de Balões Antigravidade (estilo The Lab / Valve).
    /// Herda de ErgonomicGrabInteractable garantindo Force Grab assistido e Auto-Respawn.
    /// Ao segurar o gatilho (OnActivated), infla um balão colorido no bico (Muzzle_Socket)
    /// com som de borracha esticando e haptics progressivos.
    /// Ao soltar o gatilho (OnDeactivated), desprende o balão no ar com física antigravidade.
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public class BalloonInflaterGun : ErgonomicGrabInteractable
    {
        [Header("Pistola & Bico de Hélio")]
        [Tooltip("Transform do bico frontal da pistola onde o balão é ancorado.")]
        [SerializeField] private Transform muzzleSocket;

        [Tooltip("Prefab do InflatableBalloon a ser instanciado.")]
        [SerializeField] private GameObject balloonPrefab;

        [Tooltip("Paleta de materiais coloridos brilhantes para os balões.")]
        [SerializeField] private Material[] balloonMaterials;

        [Header("Parâmetros de Inflação")]
        [Tooltip("Escala inicial mínima do balão ao brotar no bico (metros).")]
        [SerializeField] private float minBalloonScale = 0.05f;

        [Tooltip("Escala máxima recomendada do balão inflado (metros).")]
        [SerializeField] private float maxBalloonScale = 0.30f;

        [Tooltip("Tempo em segundos para inflar de min até max escala.")]
        [SerializeField] private float inflateDuration = 1.5f;

        [Tooltip("Velocidade inicial de lançamento ao soltar o gatilho (m/s).")]
        [SerializeField] private float launchVelocity = 0.85f;

        [Header("Áudios e Efeitos")]
        [SerializeField] private AudioClip inflateStretchAudioClip;
        [SerializeField] private AudioClip releaseWhooshAudioClip;
        [SerializeField] private AudioSource gunAudioSource;

        private GameObject currentBalloonObj;
        private FloatingBalloon currentFloatingBalloon;
        private Coroutine inflateCoroutine;
        private float currentInflationProgress = 0f;
        private bool isInflating = false;

        public bool IsInflating => isInflating;
        public GameObject CurrentBalloon => currentBalloonObj;
        public Transform MuzzleSocket => muzzleSocket;

        protected override void Awake()
        {
            base.Awake();

            if (muzzleSocket == null)
            {
                Transform found = transform.Find("Muzzle_Socket");
                muzzleSocket = found != null ? found : transform;
            }

            if (gunAudioSource == null)
            {
                gunAudioSource = GetComponent<AudioSource>();
                if (gunAudioSource == null)
                {
                    gunAudioSource = gameObject.AddComponent<AudioSource>();
                    gunAudioSource.spatialBlend = 1.0f;
                    gunAudioSource.playOnAwake = false;
                    gunAudioSource.rolloffMode = AudioRolloffMode.Linear;
                    gunAudioSource.minDistance = 0.3f;
                    gunAudioSource.maxDistance = 10.0f;
                }
            }
        }

        #region XRI Trigger Activation (Inflar e Soltar)
        protected override void OnActivated(ActivateEventArgs args)
        {
            base.OnActivated(args);

            if (!isInflating && currentBalloonObj == null)
            {
                StartInflation(args.interactorObject as IXRInteractor);
            }
        }

        protected override void OnDeactivated(DeactivateEventArgs args)
        {
            base.OnDeactivated(args);

            if (isInflating || currentBalloonObj != null)
            {
                ReleaseBalloon(args.interactorObject as IXRInteractor);
            }
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);

            // Se o paciente soltar a pistola enquanto inflava, libera o balão
            if (isInflating || currentBalloonObj != null)
            {
                ReleaseBalloon(args.interactorObject as IXRInteractor);
            }
        }
        #endregion

        /// <summary>
        /// Inicia o processo de inflar balão no bico.
        /// </summary>
        public void StartInflation(IXRInteractor interactor)
        {
            if (balloonPrefab == null || muzzleSocket == null) return;

            isInflating = true;
            currentInflationProgress = 0f;

            // 1. Instancia o balão filho do MuzzleSocket
            currentBalloonObj = Instantiate(balloonPrefab, muzzleSocket);
            currentBalloonObj.transform.localPosition = Vector3.zero;
            // Alinha o balão para crescer para frente a partir do bico (+Z)
            currentBalloonObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            currentBalloonObj.transform.localScale = Vector3.one * minBalloonScale;

            currentFloatingBalloon = currentBalloonObj.GetComponent<FloatingBalloon>();

            // 2. Trava física e colisores durante a inflação no bico
            Rigidbody balloonRb = currentBalloonObj.GetComponent<Rigidbody>();
            if (balloonRb != null) balloonRb.isKinematic = true;

            Collider balloonCol = currentBalloonObj.GetComponent<Collider>();
            if (balloonCol != null) balloonCol.enabled = false;

            LineRenderer lr = currentBalloonObj.GetComponent<LineRenderer>();
            if (lr != null) lr.enabled = false;

            // 3. Aplica material colorido aleatório
            ApplyRandomColor(currentBalloonObj);

            // 4. Inicia áudio de estiramento de borracha em loop
            if (gunAudioSource != null && inflateStretchAudioClip != null)
            {
                gunAudioSource.clip = inflateStretchAudioClip;
                gunAudioSource.loop = true;
                gunAudioSource.pitch = 0.85f;
                gunAudioSource.Play();
            }

            // 5. Inicia corrotina de expansão
            if (inflateCoroutine != null) StopCoroutine(inflateCoroutine);
            inflateCoroutine = StartCoroutine(InflateRoutine(interactor));
        }

        private IEnumerator InflateRoutine(IXRInteractor interactor)
        {
            float elapsed = 0f;

            while (isInflating && currentBalloonObj != null)
            {
                elapsed += Time.deltaTime;
                currentInflationProgress = Mathf.Clamp01(elapsed / inflateDuration);

                // Curva suave de crescimento com leve pulso elástico
                float currentScale = Mathf.Lerp(minBalloonScale, maxBalloonScale, currentInflationProgress);
                currentBalloonObj.transform.localScale = Vector3.one * currentScale;

                // Modula o pitch do áudio conforme o balão fica mais tensionado
                if (gunAudioSource != null && gunAudioSource.isPlaying)
                {
                    gunAudioSource.pitch = Mathf.Lerp(0.85f, 1.35f, currentInflationProgress);
                }

                // Hápticos progressivos no controle do paciente
                if (interactor != null && VRFeedbackManager.Instance != null && Time.frameCount % 8 == 0)
                {
                    float hapticAmp = Mathf.Lerp(0.15f, 0.55f, currentInflationProgress);
                    VRFeedbackManager.Instance.TriggerHaptic(interactor, hapticAmp, 0.05f);
                }

                yield return null;
            }
        }

        /// <summary>
        /// Solta o balão no ar com velocidade inicial e física antigravidade ativada.
        /// </summary>
        public void ReleaseBalloon(IXRInteractor interactor)
        {
            if (!isInflating && currentBalloonObj == null) return;

            isInflating = false;

            if (inflateCoroutine != null)
            {
                StopCoroutine(inflateCoroutine);
                inflateCoroutine = null;
            }

            // Para o som de estiramento
            if (gunAudioSource != null && gunAudioSource.isPlaying)
            {
                gunAudioSource.Stop();
                gunAudioSource.loop = false;
                gunAudioSource.pitch = 1.0f;
            }

            if (currentBalloonObj != null)
            {
                // Se mal começou a inflar (< 0.08s), destrói para evitar micro-balões vazios
                if (currentInflationProgress < 0.08f)
                {
                    Destroy(currentBalloonObj);
                    currentBalloonObj = null;
                    currentFloatingBalloon = null;
                    return;
                }

                // 1. Desvincula da ponta do bico para o espaço do mundo e orienta na vertical
                currentBalloonObj.transform.SetParent(null);
                currentBalloonObj.transform.rotation = Quaternion.identity;

                // 2. Reativa colisores e cordinha
                Collider balloonCol = currentBalloonObj.GetComponent<Collider>();
                if (balloonCol != null) balloonCol.enabled = true;

                LineRenderer lr = currentBalloonObj.GetComponent<LineRenderer>();
                if (lr != null) lr.enabled = true;

                // 3. Calcula vetor de lançamento: direção do bico + leve componente para cima
                Vector3 launchDir = (muzzleSocket != null ? muzzleSocket.forward : transform.forward);
                Vector3 launchVel = (launchDir + Vector3.up * 0.25f).normalized * launchVelocity;

                // 4. Lança o balão
                if (currentFloatingBalloon != null)
                {
                    currentFloatingBalloon.Launch(launchVel);
                }
                else
                {
                    Rigidbody rb = currentBalloonObj.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = false;
                        rb.linearVelocity = launchVel;
                    }
                }

                // 5. Áudio de desprendimento (whoosh de ar pressurizado)
                if (gunAudioSource != null && releaseWhooshAudioClip != null)
                {
                    gunAudioSource.PlayOneShot(releaseWhooshAudioClip, 1.0f);
                }

                // 6. Pulso háptico de soltura
                if (interactor != null && VRFeedbackManager.Instance != null)
                {
                    VRFeedbackManager.Instance.TriggerHaptic(interactor, 0.65f, 0.08f);
                }

                currentBalloonObj = null;
                currentFloatingBalloon = null;
            }
        }

        private void ApplyRandomColor(GameObject balloon)
        {
            if (balloonMaterials == null || balloonMaterials.Length == 0) return;

            int idx = Random.Range(0, balloonMaterials.Length);
            Material mat = balloonMaterials[idx];

            FloatingBalloon fb = balloon.GetComponent<FloatingBalloon>();
            if (fb != null)
            {
                Color burstColor = mat != null && mat.HasProperty("_Color") ? mat.color : Color.cyan;
                fb.ApplyColorMaterial(mat, burstColor);
            }
            else
            {
                MeshRenderer mr = balloon.GetComponentInChildren<MeshRenderer>();
                if (mr != null && mat != null) mr.material = mat;
            }
        }

        /// <summary>
        /// Método estático/utilitário para gerar balão em repouso/flutuação (utilizado para demonstração e Visual QA).
        /// </summary>
        public GameObject SpawnStaticBalloon(Vector3 worldPos, Quaternion worldRot, float scale, Material mat, Color color)
        {
            if (balloonPrefab == null) return null;

            GameObject b = Instantiate(balloonPrefab, worldPos, worldRot);
            b.name = "Demo_FloatingBalloon_" + mat.name.Replace("M_Balloon_", "");
            b.transform.localScale = Vector3.one * scale;

            FloatingBalloon fb = b.GetComponent<FloatingBalloon>();
            if (fb != null)
            {
                fb.ApplyColorMaterial(mat, color);
            }

            return b;
        }

        /// <summary>
        /// Força o disparo de um balão no bico (para automação e testes).
        /// </summary>
        public GameObject ForceInflateOnMuzzle(float scale, Material mat)
        {
            if (balloonPrefab == null || muzzleSocket == null) return null;

            GameObject b = Instantiate(balloonPrefab, muzzleSocket);
            b.name = "InflatingBalloon_Preview";
            b.transform.localPosition = Vector3.zero;
            b.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            b.transform.localScale = Vector3.one * scale;

            Rigidbody rb = b.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            Collider col = b.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            LineRenderer lr = b.GetComponent<LineRenderer>();
            if (lr != null) lr.enabled = false;

            FloatingBalloon fb = b.GetComponent<FloatingBalloon>();
            if (fb != null && mat != null)
            {
                Color c = mat.HasProperty("_Color") ? mat.color : Color.red;
                fb.ApplyColorMaterial(mat, c);
            }

            currentBalloonObj = b;
            return b;
        }
    }
}
