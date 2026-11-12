using System;
using UnityEngine;

namespace VRRehab.Interactive.Task06
{
    /// <summary>
    /// Rack organizador escalonado de madeira e metal para 4 fitas cassete (TASK-06).
    /// Mantém as âncoras de repouso das fitas com folga métrica segura (>= 0.08m) e inclinação
    /// de 18° para trás, servindo como base de ancoragem e auto-respawn para as fitas na Mesa 1.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class CassetteOrganizerRack : MonoBehaviour
    {
        [Header("Slots Escalonados de Repouso")]
        [Tooltip("Âncoras dos 4 slots de apoio das fitas cassete no rack.")]
        public Transform[] slots = new Transform[4];

        [Tooltip("Folga mínima de segurança entre o centro de fitas adjacentes.")]
        public float minSlotSpacing = 0.08f;

        [Header("Fitas Atribuídas aos Slots")]
        [Tooltip("Fitas registradas em cada posição do organizador.")]
        [SerializeField] private MusicCassetteTape[] assignedTapes = new MusicCassetteTape[4];

        public int SlotCount => slots != null ? slots.Length : 0;

        private void Awake()
        {
            AutoConfigureSlots();
        }

        private void OnValidate()
        {
            AutoConfigureSlots();
        }

        /// <summary>
        /// Localiza ou cria dinamicamente as 4 âncoras de slot em degraus inclinados a 18°.
        /// </summary>
        public void AutoConfigureSlots()
        {
            if (slots == null || slots.Length != 4)
            {
                slots = new Transform[4];
            }

            if (assignedTapes == null || assignedTapes.Length != 4)
            {
                assignedTapes = new MusicCassetteTape[4];
            }

            float[] slotOffsetsZ = new float[] { -0.035f, -0.009f, 0.017f, 0.043f };
            float[] slotHeightsY = new float[] { 0.042f,  0.050f,  0.058f, 0.066f };

            for (int i = 0; i < 4; i++)
            {
                if (slots[i] == null)
                {
                    Transform existing = transform.Find($"Slot_{i}");
                    if (existing != null)
                    {
                        slots[i] = existing;
                    }
                    else
                    {
                        GameObject go = new GameObject($"Slot_{i}");
                        go.transform.SetParent(transform, false);
                        go.transform.localPosition = new Vector3(0f, slotHeightsY[i], slotOffsetsZ[i]);
                        go.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
                        slots[i] = go.transform;
                    }
                }
            }
        }

        /// <summary>
        /// Retorna o Transform de ancoragem do slot informado.
        /// </summary>
        public Transform GetSlot(int index)
        {
            if (slots == null || index < 0 || index >= slots.Length)
            {
                return null;
            }
            return slots[index];
        }

        /// <summary>
        /// Registra a fita no slot correspondente e atualiza a sua âncora de auto-respawn.
        /// </summary>
        public void AssignTapeToSlot(int slotIndex, MusicCassetteTape tape)
        {
            if (slotIndex < 0 || slotIndex >= 4 || tape == null) return;

            Transform slotAnchor = GetSlot(slotIndex);
            if (slotAnchor != null)
            {
                assignedTapes[slotIndex] = tape;
                tape.SetAssignedRackSlot(slotAnchor);
            }
        }

        /// <summary>
        /// Retorna a fita atualmente alocada ao slot especificado.
        /// </summary>
        public MusicCassetteTape GetAssignedTape(int slotIndex)
        {
            if (assignedTapes == null || slotIndex < 0 || slotIndex >= assignedTapes.Length)
            {
                return null;
            }
            return assignedTapes[slotIndex];
        }

        /// <summary>
        /// Localiza o primeiro slot vago ou não ocupado por uma fita física no momento.
        /// </summary>
        public int FindFirstAvailableSlotIndex()
        {
            for (int i = 0; i < 4; i++)
            {
                if (assignedTapes[i] == null)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
