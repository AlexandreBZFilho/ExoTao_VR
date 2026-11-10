using System.Collections.Generic;
using UnityEngine;

namespace VRRehab.Interactive.Task14
{
    /// <summary>
    /// Spawner e Organizador da Trilha de Moedas na Floresta.
    /// Mantém as referências dos waypoints e permite reconfigurar a densidade e o alinhamento da trilha.
    /// </summary>
    public class CoinTrailSpawner : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject energyCoinPrefab;
        [SerializeField] private GameObject bonusCoinPrefab;

        [Header("Parâmetros da Trilha")]
        [SerializeField] private float heightAboveGround = 1.15f;
        [SerializeField] private float defaultSpacing = 1.8f;

        [Header("Waypoints Principais da Trilha")]
        [SerializeField] private List<Vector3> trailWaypoints = new List<Vector3>();

        public float HeightAboveGround => heightAboveGround;
        public float DefaultSpacing => defaultSpacing;

        public void SetWaypoints(IEnumerable<Vector3> points)
        {
            trailWaypoints.Clear();
            trailWaypoints.AddRange(points);
        }

        private void OnDrawGizmosSelected()
        {
            if (trailWaypoints == null || trailWaypoints.Count < 2) return;

            Gizmos.color = new Color(1f, 0.84f, 0f, 0.75f);
            for (int i = 0; i < trailWaypoints.Count - 1; i++)
            {
                Gizmos.DrawLine(trailWaypoints[i], trailWaypoints[i + 1]);
                Gizmos.DrawWireSphere(trailWaypoints[i], 0.2f);
            }
            Gizmos.DrawWireSphere(trailWaypoints[trailWaypoints.Count - 1], 0.2f);
        }
    }
}
