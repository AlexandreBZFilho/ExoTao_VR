using UnityEngine;

namespace VRRehab.Ergonomics
{
    /// <summary>
    /// Sistema de Assistência de Arremesso para Reabilitação em VR (TASK-02).
    /// Amplifica a força física do arremesso clínico (compensando fraqueza muscular em pacientes com exoesqueleto)
    /// e calcula correções de parábola balística quando o arremesso se aproxima de receptores válidos (IAimAssistTarget).
    /// Calibrado com cone sutil de 18° para eliminar magnetismo artificial indesejado.
    /// </summary>
    [DisallowMultipleComponent]
    public class ThrowAimAssist : MonoBehaviour
    {
        [Header("Ergonomia de Força")]
        [Tooltip("Multiplicador ergonômico clínico de velocidade de arremesso aplicado no descarte.")]
        [Range(1.0f, 3.0f)]
        [SerializeField] private float throwVelocityMultiplier = 1.65f;

        [Tooltip("Velocidade mínima para ativar o auxílio balístico (m/s).")]
        [SerializeField] private float minThrowSpeedThreshold = 0.20f;

        [Header("Aim Assist Balístico (Cone Sutil VR)")]
        [Tooltip("Força da assistência de mira (0 = sem correção, 0.5 = 50% humano / 50% parábola ideal).")]
        [Range(0.0f, 1.0f)]
        [SerializeField] private float aimAssistStrength = 0.50f;

        [Tooltip("Ângulo máximo do cone de captura em relação ao vetor de arremesso original (graus). Padrão ergonômico: 18.0°.")]
        [Range(5f, 45f)]
        [SerializeField] private float maxConeAngleDegrees = 18.0f;

        [Tooltip("Distância máxima de busca para alvos de arremesso (metros). Padrão: 16.0m.")]
        [SerializeField] private float maxTargetDistance = 16.0f;

        [Header("Depuração")]
        [SerializeField] private bool showDebugRays = false;

        public float VelocityMultiplier
        {
            get => throwVelocityMultiplier;
            set => throwVelocityMultiplier = Mathf.Clamp(value, 1.0f, 3.0f);
        }

        public float AimAssistStrength
        {
            get => aimAssistStrength;
            set => aimAssistStrength = Mathf.Clamp01(value);
        }

        public float MaxConeAngleDegrees
        {
            get => maxConeAngleDegrees;
            set => maxConeAngleDegrees = Mathf.Clamp(value, 5.0f, 45.0f);
        }

        public float MaxTargetDistance
        {
            get => maxTargetDistance;
            set => maxTargetDistance = Mathf.Max(1.0f, value);
        }

        /// <summary>
        /// Calcula a velocidade resultante de arremesso combinando o vetor original (ou tangencial) com a parábola balística ideal.
        /// </summary>
        public Vector3 CalculateAssistedVelocity(Vector3 releasePosition, Vector3 rawVelocity)
        {
            float rawSpeedSqr = rawVelocity.sqrMagnitude;
            if (rawSpeedSqr < 0.0001f)
            {
                return Vector3.zero;
            }

            float rawSpeed = Mathf.Sqrt(rawSpeedSqr);
            if (rawSpeed < minThrowSpeedThreshold)
            {
                // Arremesso sutil ou soltura em baixa velocidade: escala suave sem forçar trajetória de alvo
                return rawVelocity * throwVelocityMultiplier;
            }

            Vector3 scaledVelocity = rawVelocity * throwVelocityMultiplier;

            // Busca melhor alvo alinhado com o lançamento no cone de 18 graus
            if (AimAssistTargetRegistry.FindBestTarget(
                releasePosition,
                scaledVelocity,
                maxConeAngleDegrees,
                maxTargetDistance,
                out IAimAssistTarget bestTarget,
                out Vector3 targetPos))
            {
                Vector3 idealBallisticVel = CalculateBallisticLaunchVelocity(
                    releasePosition,
                    targetPos,
                    scaledVelocity.magnitude
                );

                if (idealBallisticVel.sqrMagnitude > 0.01f)
                {
                    // Interpolação suave de 50% entre a curva humana e a correção matemática da parábola
                    Vector3 blendedVel = Vector3.Lerp(scaledVelocity, idealBallisticVel, aimAssistStrength);

                    if (showDebugRays)
                    {
                        Debug.DrawLine(releasePosition, releasePosition + scaledVelocity, Color.yellow, 2.0f);
                        Debug.DrawLine(releasePosition, releasePosition + idealBallisticVel, Color.cyan, 2.0f);
                        Debug.DrawLine(releasePosition, releasePosition + blendedVel, Color.green, 2.0f);
                    }

                    return blendedVel;
                }
            }

            return scaledVelocity;
        }

        /// <summary>
        /// Calcula o vetor de velocidade inicial necessário para que um projétil sob gravidade atinja targetPos.
        /// Decomposição cinemática com tempo de voo baseado na distância e ângulo ergonômico de lançamento.
        /// </summary>
        private Vector3 CalculateBallisticLaunchVelocity(Vector3 startPos, Vector3 targetPos, float desiredSpeed)
        {
            Vector3 toTarget = targetPos - startPos;
            Vector3 toTargetXZ = new Vector3(toTarget.x, 0f, toTarget.z);
            float horizontalDist = toTargetXZ.magnitude;
            float verticalDist = toTarget.y;

            if (horizontalDist < 0.2f)
            {
                return Vector3.up * Mathf.Max(desiredSpeed, 2.0f);
            }

            float g = Mathf.Abs(Physics.gravity.y);
            if (g < 0.1f) g = 9.81f;

            // Estimativa de tempo de voo com base na velocidade desejada
            float estimatedSpeed = Mathf.Max(desiredSpeed, 3.2f);
            // Supõe um ângulo médio de subida ergonômico (~35 a 45 graus, cos ~ 0.707f)
            float flightTime = horizontalDist / (estimatedSpeed * 0.707f);
            if (flightTime < 0.15f) flightTime = 0.15f;

            // Decomposição cinemática balística:
            // v_x = d_x / t
            // v_z = d_z / t
            // v_y = (d_y + 0.5 * g * t^2) / t
            Vector3 horizontalDir = toTargetXZ.normalized;
            float vx = (horizontalDist / flightTime) * horizontalDir.x;
            float vz = (horizontalDist / flightTime) * horizontalDir.z;
            float vy = (verticalDist + 0.5f * g * flightTime * flightTime) / flightTime;

            return new Vector3(vx, vy, vz);
        }
    }
}
