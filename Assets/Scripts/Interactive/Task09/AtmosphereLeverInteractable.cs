using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;

namespace VRRehab.Interactive.Task09
{
    /// <summary>
    /// Alavanca Fidget 3D de controle de iluminação/atmosfera do ambiente virtual.
    /// Alterna entre Dia, Pôr do Sol e Noite de forma suave via Lerp/Slerp.
    /// Interagível via toque VR, laser pointer (XRSimpleInteractable) e chamadas diretas.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AtmosphereLeverInteractable : MonoBehaviour
    {
        public enum AtmosphereState
        {
            Day = 0,
            Sunset = 1,
            Night = 2
        }

        [Header("Configuração de Estado")]
        [SerializeField] private AtmosphereState currentState = AtmosphereState.Day;
        [SerializeField] private float transitionDuration = 1.2f;

        [Header("Hierarquia Mecânica da Alavanca")]
        [SerializeField] private Transform handlePivot;
        [SerializeField] private Vector3 rotationAxis = new Vector3(1f, 0f, 0f); // Rotação no eixo X local
        [SerializeField] private float angleDay = -30f;
        [SerializeField] private float angleSunset = 0f;
        [SerializeField] private float angleNight = 30f;

        [Header("Iluminação e Ambiente")]
        [SerializeField] private Light directionalLight;
        [SerializeField] private Renderer ledDayRenderer;
        [SerializeField] private Renderer ledSunsetRenderer;
        [SerializeField] private Renderer ledNightRenderer;

        [Header("Áudio e Haptics")]
        [SerializeField] private AudioClip sfxLeverClick;
        [SerializeField] private AudioClip sfxAtmosphereWhoosh;
        [SerializeField] private AudioSource audioSource;

        private Coroutine activeTransitionCoroutine;
        private Material runtimeSkyboxMat;
        private XRSimpleInteractable xrInteractable;

        // Parâmetros de iluminação por estado
        private static readonly Color DaySunColor = new Color(1.0f, 0.957f, 0.839f);
        private const float DaySunIntensity = 1.0f;
        private static readonly Vector3 DaySunRotation = new Vector3(83.0f, 330.0f, 0.0f);
        private const float DayAmbientIntensity = 1.0f;
        private static readonly Color DayAmbientColor = new Color(0.212f, 0.227f, 0.259f);
        private static readonly Color DaySkyboxTint = new Color(0.575f, 0.575f, 0.575f, 0.5f);
        private const float DaySkyboxExposure = 1.0f;

        private static readonly Color SunsetSunColor = new Color(1.0f, 0.52f, 0.20f);
        private const float SunsetSunIntensity = 0.85f;
        private static readonly Vector3 SunsetSunRotation = new Vector3(22.0f, 310.0f, 0.0f);
        private const float SunsetAmbientIntensity = 0.60f;
        private static readonly Color SunsetAmbientColor = new Color(0.35f, 0.18f, 0.12f);
        private static readonly Color SunsetSkyboxTint = new Color(1.0f, 0.45f, 0.30f, 0.5f);
        private const float SunsetSkyboxExposure = 0.80f;

        private static readonly Color NightSunColor = new Color(0.20f, 0.35f, 0.75f);
        private const float NightSunIntensity = 0.18f;
        private static readonly Vector3 NightSunRotation = new Vector3(65.0f, 140.0f, 0.0f);
        private const float NightAmbientIntensity = 0.12f;
        private static readonly Color NightAmbientColor = new Color(0.04f, 0.06f, 0.15f);
        private static readonly Color NightSkyboxTint = new Color(0.12f, 0.15f, 0.30f, 0.5f);
        private const float NightSkyboxExposure = 0.25f;

        public AtmosphereState CurrentState => currentState;

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            // Garante instância própria de skybox em runtime
            if (RenderSettings.skybox != null)
            {
                runtimeSkyboxMat = new Material(RenderSettings.skybox);
                RenderSettings.skybox = runtimeSkyboxMat;
            }

            SetupXRInteraction();
        }

        private void Start()
        {
            // Aplica estado inicial sem interpolação
            ApplyAtmosphere(currentState, 1.0f, true);
            UpdateHandleRotationImmediate(GetAngleForState(currentState));
            UpdateIndicatorLEDs(currentState);
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
        }

        private void OnDestroy()
        {
            if (xrInteractable != null)
            {
                xrInteractable.selectEntered.RemoveListener(OnSelectEntered);
            }
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            AdvanceToNextState();
            TriggerHaptic(args.interactorObject);
        }

        /// <summary>
        /// Aciona vibração tátil no controlador VR responsável pela interação.
        /// </summary>
        private void TriggerHaptic(IXRInteractor interactor)
        {
            if (interactor != null)
            {
                VRFeedbackManager.Instance?.TriggerHaptic(interactor, 0.55f, 0.12f);
            }
        }

        /// <summary>
        /// Avança para o próximo estado de forma cíclica (Dia -> Pôr do Sol -> Noite -> Dia).
        /// </summary>
        [ContextMenu("Advance Next State")]
        public void AdvanceToNextState()
        {
            int nextIndex = ((int)currentState + 1) % 3;
            SetAtmosphereState((AtmosphereState)nextIndex, false);
        }

        /// <summary>
        /// Define o estado da atmosfera, interpolando ou aplicando instantaneamente.
        /// </summary>
        public void SetAtmosphereState(AtmosphereState targetState, bool immediate = false)
        {
            currentState = targetState;

            if (audioSource != null && sfxLeverClick != null)
            {
                audioSource.pitch = 0.95f + ((int)targetState) * 0.05f;
                audioSource.PlayOneShot(sfxLeverClick, 0.85f);
            }

            if (!immediate && audioSource != null && sfxAtmosphereWhoosh != null)
            {
                audioSource.PlayOneShot(sfxAtmosphereWhoosh, 0.65f);
            }

            UpdateIndicatorLEDs(targetState);

            if (activeTransitionCoroutine != null)
            {
                StopCoroutine(activeTransitionCoroutine);
            }

            if (immediate || !Application.isPlaying)
            {
                UpdateHandleRotationImmediate(GetAngleForState(targetState));
                ApplyAtmosphere(targetState, 1.0f, true);
            }
            else
            {
                activeTransitionCoroutine = StartCoroutine(AnimateTransitionRoutine(targetState));
            }
        }

        private float GetAngleForState(AtmosphereState state)
        {
            switch (state)
            {
                case AtmosphereState.Day: return angleDay;
                case AtmosphereState.Sunset: return angleSunset;
                case AtmosphereState.Night: return angleNight;
                default: return 0f;
            }
        }

        private void UpdateHandleRotationImmediate(float angle)
        {
            if (handlePivot != null)
            {
                handlePivot.localRotation = Quaternion.Euler(rotationAxis * angle);
            }
        }

        private IEnumerator AnimateTransitionRoutine(AtmosphereState targetState)
        {
            float targetAngle = GetAngleForState(targetState);
            Quaternion startRot = handlePivot != null ? handlePivot.localRotation : Quaternion.identity;
            Quaternion targetRot = Quaternion.Euler(rotationAxis * targetAngle);

            Color startSunColor = directionalLight != null ? directionalLight.color : DaySunColor;
            float startSunIntensity = directionalLight != null ? directionalLight.intensity : DaySunIntensity;
            Quaternion startSunRot = directionalLight != null ? directionalLight.transform.rotation : Quaternion.Euler(DaySunRotation);
            Color startAmbient = RenderSettings.ambientLight;
            float startAmbientIntensity = RenderSettings.ambientIntensity;

            Color targetSunColor = GetSunColor(targetState);
            float targetSunIntensity = GetSunIntensity(targetState);
            Quaternion targetSunRot = Quaternion.Euler(GetSunRotation(targetState));
            Color targetAmbient = GetAmbientColor(targetState);
            float targetAmbientIntensity = GetAmbientIntensity(targetState);

            Color startSkyTint = runtimeSkyboxMat != null && runtimeSkyboxMat.HasProperty("_Tint") ? runtimeSkyboxMat.GetColor("_Tint") : DaySkyboxTint;
            float startSkyExp = runtimeSkyboxMat != null && runtimeSkyboxMat.HasProperty("_Exposure") ? runtimeSkyboxMat.GetFloat("_Exposure") : DaySkyboxExposure;
            Color targetSkyTint = GetSkyboxTint(targetState);
            float targetSkyExp = GetSkyboxExposure(targetState);

            float elapsed = 0f;
            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / transitionDuration);
                // Suavização cúbica (SmoothStep)
                float smoothT = t * t * (3f - 2f * t);

                // Rotação da Alavanca
                if (handlePivot != null)
                {
                    handlePivot.localRotation = Quaternion.Slerp(startRot, targetRot, smoothT);
                }

                // Luz Direcional
                if (directionalLight != null)
                {
                    directionalLight.color = Color.Lerp(startSunColor, targetSunColor, smoothT);
                    directionalLight.intensity = Mathf.Lerp(startSunIntensity, targetSunIntensity, smoothT);
                    directionalLight.transform.rotation = Quaternion.Slerp(startSunRot, targetSunRot, smoothT);
                }

                // Iluminação Ambiente
                RenderSettings.ambientLight = Color.Lerp(startAmbient, targetAmbient, smoothT);
                RenderSettings.ambientIntensity = Mathf.Lerp(startAmbientIntensity, targetAmbientIntensity, smoothT);

                // Skybox
                if (runtimeSkyboxMat != null)
                {
                    if (runtimeSkyboxMat.HasProperty("_Tint"))
                    {
                        runtimeSkyboxMat.SetColor("_Tint", Color.Lerp(startSkyTint, targetSkyTint, smoothT));
                    }
                    if (runtimeSkyboxMat.HasProperty("_Exposure"))
                    {
                        runtimeSkyboxMat.SetFloat("_Exposure", Mathf.Lerp(startSkyExp, targetSkyExp, smoothT));
                    }
                }

                yield return null;
            }

            // Finaliza garantindo valores exatos
            UpdateHandleRotationImmediate(targetAngle);
            ApplyAtmosphere(targetState, 1.0f, false);
            activeTransitionCoroutine = null;
        }

        public void ApplyAtmosphere(AtmosphereState state, float intensityFactor = 1.0f, bool snapHandle = true)
        {
            if (snapHandle)
            {
                UpdateHandleRotationImmediate(GetAngleForState(state));
            }

            if (directionalLight != null)
            {
                directionalLight.color = GetSunColor(state);
                directionalLight.intensity = GetSunIntensity(state) * intensityFactor;
                directionalLight.transform.rotation = Quaternion.Euler(GetSunRotation(state));
            }

            RenderSettings.ambientLight = GetAmbientColor(state);
            RenderSettings.ambientIntensity = GetAmbientIntensity(state) * intensityFactor;

            Material targetMat = runtimeSkyboxMat != null ? runtimeSkyboxMat : RenderSettings.skybox;
            if (targetMat != null)
            {
                if (targetMat.HasProperty("_Tint"))
                {
                    targetMat.SetColor("_Tint", GetSkyboxTint(state));
                }
                if (targetMat.HasProperty("_Exposure"))
                {
                    targetMat.SetFloat("_Exposure", GetSkyboxExposure(state) * intensityFactor);
                }
            }
        }

        private float GetAmbientIntensity(AtmosphereState state)
        {
            switch (state)
            {
                case AtmosphereState.Day: return DayAmbientIntensity;
                case AtmosphereState.Sunset: return SunsetAmbientIntensity;
                case AtmosphereState.Night: return NightAmbientIntensity;
                default: return DayAmbientIntensity;
            }
        }

        private Color GetSunColor(AtmosphereState state)
        {
            switch (state)
            {
                case AtmosphereState.Day: return DaySunColor;
                case AtmosphereState.Sunset: return SunsetSunColor;
                case AtmosphereState.Night: return NightSunColor;
                default: return DaySunColor;
            }
        }

        private float GetSunIntensity(AtmosphereState state)
        {
            switch (state)
            {
                case AtmosphereState.Day: return DaySunIntensity;
                case AtmosphereState.Sunset: return SunsetSunIntensity;
                case AtmosphereState.Night: return NightSunIntensity;
                default: return DaySunIntensity;
            }
        }

        private Vector3 GetSunRotation(AtmosphereState state)
        {
            switch (state)
            {
                case AtmosphereState.Day: return DaySunRotation;
                case AtmosphereState.Sunset: return SunsetSunRotation;
                case AtmosphereState.Night: return NightSunRotation;
                default: return DaySunRotation;
            }
        }

        private Color GetAmbientColor(AtmosphereState state)
        {
            switch (state)
            {
                case AtmosphereState.Day: return DayAmbientColor;
                case AtmosphereState.Sunset: return SunsetAmbientColor;
                case AtmosphereState.Night: return NightAmbientColor;
                default: return DayAmbientColor;
            }
        }

        private Color GetSkyboxTint(AtmosphereState state)
        {
            switch (state)
            {
                case AtmosphereState.Day: return DaySkyboxTint;
                case AtmosphereState.Sunset: return SunsetSkyboxTint;
                case AtmosphereState.Night: return NightSkyboxTint;
                default: return DaySkyboxTint;
            }
        }

        private float GetSkyboxExposure(AtmosphereState state)
        {
            switch (state)
            {
                case AtmosphereState.Day: return DaySkyboxExposure;
                case AtmosphereState.Sunset: return SunsetSkyboxExposure;
                case AtmosphereState.Night: return NightSkyboxExposure;
                default: return DaySkyboxExposure;
            }
        }

        private void UpdateIndicatorLEDs(AtmosphereState state)
        {
            SetLedEmission(ledDayRenderer, state == AtmosphereState.Day);
            SetLedEmission(ledSunsetRenderer, state == AtmosphereState.Sunset);
            SetLedEmission(ledNightRenderer, state == AtmosphereState.Night);
        }

        private void SetLedEmission(Renderer ledRenderer, bool active)
        {
            if (ledRenderer == null) return;
            Material mat = Application.isPlaying ? ledRenderer.material : ledRenderer.sharedMaterial;
            if (mat == null) return;

            if (mat.HasProperty("_EmissionColor"))
            {
                Color baseCol = mat.GetColor("_EmissionColor");
                float factor = active ? 2.5f : 0.25f;
                // Normaliza e escala
                Color brightCol = new Color(baseCol.r, baseCol.g, baseCol.b, 1.0f) * factor;
                mat.SetColor("_EmissionColor", brightCol);
                mat.EnableKeyword("_EMISSION");
            }
        }

        [ContextMenu("Set State: Day")]
        public void SetStateDay() => SetAtmosphereState(AtmosphereState.Day, false);

        [ContextMenu("Set State: Sunset")]
        public void SetStateSunset() => SetAtmosphereState(AtmosphereState.Sunset, false);

        [ContextMenu("Set State: Night")]
        public void SetStateNight() => SetAtmosphereState(AtmosphereState.Night, false);
    }
}
