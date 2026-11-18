using UnityEngine;

namespace VRRehab.Interactive.Task08
{
    /// <summary>
    /// Encaminha o OnTriggerEnter de um sensor da cesta (boca do aro ou saída da rede) para o BasketballHoopScorer.
    /// IMPORTANTE: precisa ficar em arquivo próprio com o mesmo nome da classe. Quando estava declarado dentro de
    /// BasketballHoopScorer.cs a Unity não conseguia salvar a referência do script no prefab (virava Missing Script)
    /// e a cesta nunca pontuava.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class HoopSensorForwarder : MonoBehaviour
    {
        public enum SensorType { Top, Bottom }

        public SensorType sensorType;
        public BasketballHoopScorer scorer;

        private void OnTriggerEnter(Collider other)
        {
            if (scorer == null) return;
            if (sensorType == SensorType.Top) scorer.OnTriggerTopEnter(other);
            else scorer.OnTriggerBottomEnter(other);
        }
    }
}
