using System.Collections.Generic;
using UnityEngine;

namespace VRRehab.Interactive.Task07
{
    /// <summary>
    /// Pool fixo de balões da pistola (TASK-07). Todos os balões são criados uma única vez no Start da pistola;
    /// inflar, estourar e murchar só ativam/desativam objetos (zero Instantiate/Destroy por inflada, zero GC por frame).
    /// No máximo <see cref="MaxLive"/> balões soltos ao mesmo tempo: ao inflar o próximo, o mais antigo estoura suave.
    /// Há 1 instância de reserva para o balão que está no bico enquanto o mais antigo faz o estouro suave.
    /// </summary>
    public class BalloonPool
    {
        public int MaxLive { get; }
        public Transform Root { get; }

        private readonly List<FloatingBalloon> all;
        private readonly Stack<FloatingBalloon> free;
        private readonly LinkedList<FloatingBalloon> live = new LinkedList<FloatingBalloon>(); // mais antigo primeiro
        private readonly LinkedListNode<FloatingBalloon>[] nodes;

        public int TotalInstances => all.Count;
        public int FreeCount => free.Count;
        /// <summary>Balões soltos no ar (sem contar o que murcha/estoura nem o que está no bico).</summary>
        public int LiveCount
        {
            get
            {
                int n = 0;
                for (var node = live.First; node != null; node = node.Next) if (node.Value.IsFree) n++;
                return n;
            }
        }
        /// <summary>Quantas vezes o pool teve que estourar o balão mais antigo (telemetria / testes).</summary>
        public int RecycledOldest { get; private set; }

        public BalloonPool(GameObject prefab, int maxLive, string rootName)
        {
            MaxLive = Mathf.Max(1, maxLive);
            int capacity = MaxLive + 1;
            all = new List<FloatingBalloon>(capacity);
            free = new Stack<FloatingBalloon>(capacity);
            nodes = new LinkedListNode<FloatingBalloon>[capacity];

            var rootGo = new GameObject(rootName);
            Root = rootGo.transform;

            for (int i = 0; i < capacity; i++)
            {
                GameObject go = Object.Instantiate(prefab, Root);
                go.name = $"PooledBalloon_{i:00}";
                FloatingBalloon b = go.GetComponent<FloatingBalloon>();
                if (b == null) { Object.Destroy(go); continue; }
                b.BindPool(this);
                go.SetActive(false);
                all.Add(b);
                free.Push(b);
                nodes[all.Count - 1] = new LinkedListNode<FloatingBalloon>(b);
            }
        }

        /// <summary>Pega um balão para inflar. Se já há MaxLive no ar, o mais antigo começa a estourar suave.</summary>
        public FloatingBalloon Acquire()
        {
            if (LiveCount >= MaxLive)
            {
                for (var node = live.First; node != null; node = node.Next)
                {
                    if (!node.Value.IsFree) continue;
                    node.Value.SoftPop();
                    RecycledOldest++;
                    break;
                }
            }

            if (free.Count == 0)
            {
                // Reserva ocupada (estouro suave anterior ainda acabando): encerra o mais antigo que não está no bico
                for (var node = live.First; node != null; node = node.Next)
                {
                    if (node.Value.IsInflating) continue;
                    node.Value.CancelSilently(); // chama Return(), que o devolve para a pilha
                    break;
                }
                if (free.Count == 0) return null;
            }

            FloatingBalloon b = free.Pop();
            live.AddLast(NodeOf(b));
            return b;
        }

        /// <summary>Chamado pelo próprio balão quando termina de estourar/murchar.</summary>
        public void Return(FloatingBalloon b)
        {
            if (b == null) return;
            var node = NodeOf(b);
            if (node.List != null) live.Remove(node);
            b.transform.SetParent(Root, false);
            b.gameObject.SetActive(false);
            if (!free.Contains(b)) free.Push(b);
        }

        private LinkedListNode<FloatingBalloon> NodeOf(FloatingBalloon b)
        {
            int i = all.IndexOf(b);
            return nodes[i];
        }
    }
}
