using System.Collections.Generic;
using UnityEngine;

namespace VRRehab.Ergonomics
{
    /// <summary>
    /// Interface para alvos válidos do sistema de Aim Assist Balístico (ex: aros de basquete,
    /// cestas de maçãs na floresta, baús ou o próprio Drone Pet no modo 'buscar').
    /// </summary>
    public interface IAimAssistTarget
    {
        Vector3 AimTargetPosition { get; }
        float AimTargetRadius { get; }
        bool IsAimTargetActive { get; }
        Transform TargetTransform { get; }
    }

    /// <summary>
    /// Registro global de alvos de arremesso ativo em cena, permitindo busca O(N) eficiente
    /// sem alocações de garbage collector durante o descarte de objetos.
    /// </summary>
    public static class AimAssistTargetRegistry
    {
        private static readonly List<IAimAssistTarget> s_ActiveTargets = new List<IAimAssistTarget>();

        public static IReadOnlyList<IAimAssistTarget> ActiveTargets => s_ActiveTargets;

        public static void Register(IAimAssistTarget target)
        {
            if (target != null && !s_ActiveTargets.Contains(target))
            {
                s_ActiveTargets.Add(target);
            }
        }

        public static void Unregister(IAimAssistTarget target)
        {
            if (target != null)
            {
                s_ActiveTargets.Remove(target);
            }
        }

        /// <summary>
        /// Localiza o alvo mais alinhado com o vetor de lançamento inicial dentro do cone angular especificado.
        /// </summary>
        public static bool FindBestTarget(
            Vector3 origin,
            Vector3 initialDirection,
            float maxAngleDegrees,
            float maxDistance,
            out IAimAssistTarget bestTarget,
            out Vector3 targetPosition)
        {
            bestTarget = null;
            targetPosition = Vector3.zero;

            if (s_ActiveTargets.Count == 0 || initialDirection.sqrMagnitude < 0.001f)
            {
                return false;
            }

            Vector3 throwDirNorm = initialDirection.normalized;
            float bestScore = -1f; // Pontuação baseada em proximidade angular e distância

            for (int i = 0; i < s_ActiveTargets.Count; i++)
            {
                IAimAssistTarget target = s_ActiveTargets[i];
                if (target == null || !target.IsAimTargetActive) continue;

                Vector3 targetPos = target.AimTargetPosition;
                Vector3 toTarget = targetPos - origin;
                float dist = toTarget.magnitude;

                if (dist > maxDistance || dist < 0.2f) continue;

                Vector3 toTargetNorm = toTarget / dist;
                float angle = Vector3.Angle(throwDirNorm, toTargetNorm);

                if (angle <= maxAngleDegrees)
                {
                    // Alvo dentro do cone: score privilegia menor desvio angular e proximidade
                    float angleWeight = 1.0f - (angle / maxAngleDegrees);
                    float distanceWeight = 1.0f - (dist / maxDistance) * 0.3f;
                    float score = angleWeight * 0.7f + distanceWeight * 0.3f;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestTarget = target;
                        targetPosition = targetPos;
                    }
                }
            }

            return bestTarget != null;
        }
    }

    /// <summary>
    /// Componente de conveniência que pode ser anexado a qualquer GameObject para transformá-lo
    /// em um receptor de arremesso assistido.
    /// </summary>
    [DisallowMultipleComponent]
    public class AimAssistTargetPoint : MonoBehaviour, IAimAssistTarget
    {
        [Header("Configuração de Alvo")]
        [SerializeField] private Vector3 offset = Vector3.zero;
        [SerializeField] private float targetRadius = 0.35f;
        [SerializeField] private bool targetActive = true;

        public Vector3 AimTargetPosition => transform.TransformPoint(offset);
        public float AimTargetRadius => targetRadius;
        public bool IsAimTargetActive => targetActive && gameObject.activeInHierarchy;
        public Transform TargetTransform => transform;

        private void OnEnable()
        {
            AimAssistTargetRegistry.Register(this);
        }

        private void OnDisable()
        {
            AimAssistTargetRegistry.Unregister(this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0.8f, 0.5f);
            Gizmos.DrawWireSphere(AimTargetPosition, targetRadius);
            Gizmos.color = new Color(0f, 1f, 0.8f, 1f);
            Gizmos.DrawSphere(AimTargetPosition, 0.05f);
        }
    }
}
