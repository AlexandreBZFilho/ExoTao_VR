using UnityEngine;

namespace VRRehab.Ergonomics
{
    /// <summary>
    /// Sistema de Assistência de Arremesso para Reabilitação em VR.
    /// Amplifica suavemente a força física do arremesso (compensando fraqueza muscular em pacientes com exoesqueleto)
    /// e calcula correções de parábola balística quando o arremesso se aproxima de receptores válidos (IAimAssistTarget).
    /// </summary>
    [DisallowMultipleComponent]
    public class ThrowAimAssist : MonoBehaviour
    {
        [Header("Ergonomia de Força")]
        [Tooltip("Multiplicador ergonômico de velocidade de arremesso aplicado no descarte.")]
        [Range(1.0f, 3.0f)]
        [SerializeField] private float throwVelocityMultiplier = 1.45f;

        [Tooltip("Velocidade mínima para ativar o auxílio balístico (m/s).")]
        [SerializeField] private float minThrowSpeedThreshold = 0.35f;

        [Header("Aim Assist Balístico")]
        [Tooltip("Força da assistência de mira (0 = sem correção, 1 = vetor perfeito em direção ao alvo).")]
        [Range(0.0f, 1.0f)]
        [SerializeField] private float aimAssistStrength = 0.65f;

        [Tooltip("Ângulo máximo do cone de captura em relação ao vetor de arremesso original (graus).")]
        [Range(10f, 60f)]
        [SerializeField] private float maxConeAngleDegrees = 38.0f;

        [Tooltip("Distância máxima de busca para alvos de arremesso (metros).")]
        [SerializeField] private float maxTargetDistance = 15.0f;

        [Header("Depuração")]
        [SerializeField] private bool showDebugRays = false;

        public float VelocityMultiplier => throwVelocityMultiplier;
        public float AimAssistStrength => aimAssistStrength;

        /// <summary>
        /// Calcula a velocidade resultante de arremesso combinando a força ergonômica com a trajetória balística ideal.
        /// </summary>
        public Vector3 CalculateAssistedVelocity(Vector3 releasePosition, Vector3 rawVelocity)
        {
            float rawSpeed = rawVelocity.magnitude;
            if (rawSpeed < minThrowSpeedThreshold)
            {
                // Arremesso muito fraco ou simples soltura de mão: apenas escala a velocidade
                return rawVelocity * throwVelocityMultiplier;
            }

            Vector3 scaledVelocity = rawVelocity * throwVelocityMultiplier;

            // Busca melhor alvo alinhado com o lançamento
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
                    // Interpolação suave entre o arremesso humano e a correção matemática
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
            // Supõe um ângulo médio de subida ergonômico (~35 a 45 graus)
            float flightTime = horizontalDist / (estimatedSpeed * 0.707f);
            if (flightTime < 0.15f) flightTime = 0.15f;

            // Decomposição cinemática:
            // x(t) = v_x * t  =>  v_x = x / t
            // z(t) = v_z * t  =>  v_z = z / t
            // y(t) = v_y * t - 0.5 * g * t^2  =>  v_y = (y + 0.5 * g * t^2) / t
            Vector3 horizontalDir = toTargetXZ.normalized;
            float vx = (horizontalDist / flightTime) * horizontalDir.x;
            float vz = (horizontalDist / flightTime) * horizontalDir.z;
            float vy = (verticalDist + 0.5f * g * flightTime * flightTime) / flightTime;

            return new Vector3(vx, vy, vz);
        }
    }
}
