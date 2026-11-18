using System.Collections.Generic;
using UnityEngine;

namespace VRRehab.Core
{
    /// <summary>
    /// Altura do terreno em mapas com VÁRIOS blocos de Terrain (a Floresta_e_Rio tem 9).
    /// NÃO usar Terrain.activeTerrain.SampleHeight: ele consulta um bloco só, e para pontos em outro bloco devolve uma
    /// altura errada. Foi isso que deixou os itens da floresta ~1,5 m acima do chão e fazia a proteção "caiu do mapa"
    /// mandar de volta maçãs que estavam paradas no chão.
    /// Sem alocação por chamada (lista em cache, renovada a cada 5 s).
    /// </summary>
    public static class TerrainGroundUtil
    {
        private static readonly List<Terrain> terrains = new List<Terrain>();
        private static float nextRefreshTime = float.NegativeInfinity;

        /// <summary>Existe algum Terrain na cena atual?</summary>
        public static bool HasTerrain
        {
            get
            {
                Refresh();
                return terrains.Count > 0;
            }
        }

        /// <summary>Altura (Y de mundo) do terreno no XZ de 'worldPos'. False se o ponto está fora de todos os blocos (fora do mapa).</summary>
        public static bool TryGetHeight(Vector3 worldPos, out float height)
        {
            Refresh();
            height = 0f;
            bool found = false;
            for (int i = 0; i < terrains.Count; i++)
            {
                Terrain t = terrains[i];
                if (t == null || t.terrainData == null) continue;
                Vector3 origin = t.transform.position;
                Vector3 size = t.terrainData.size;
                if (worldPos.x < origin.x || worldPos.x > origin.x + size.x || worldPos.z < origin.z || worldPos.z > origin.z + size.z) continue;

                float h = t.SampleHeight(worldPos) + origin.y;
                if (!found || h > height) height = h;
                found = true;
            }
            return found;
        }

        /// <summary>Força a releitura dos blocos (ex.: logo depois de carregar uma cena).</summary>
        public static void Invalidate()
        {
            nextRefreshTime = float.NegativeInfinity;
        }

        private static void Refresh()
        {
            float now = Time.realtimeSinceStartup;
            bool stale = now >= nextRefreshTime || (terrains.Count > 0 && terrains[0] == null);
            if (!stale) return;
            nextRefreshTime = now + 5f;
            terrains.Clear();
            Terrain.GetActiveTerrains(terrains);
        }
    }
}
