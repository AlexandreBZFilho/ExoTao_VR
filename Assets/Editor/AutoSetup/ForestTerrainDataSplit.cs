using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VRRehab.EditorTools
{
    /// <summary>
    /// A Floresta_e_Rio usava o TerrainData de DEMONSTRAÇÃO da Polytope ("Helpers/Terrain/New Terrain.asset") num dos
    /// 9 blocos. A limpeza do capim em volta dos cogumelos musicais foi gravada nesse arquivo do pacote, o que também
    /// alterava a cena demo da Polytope e seria perdido numa atualização do pacote.
    /// Esta ferramenta copia o TerrainData (já com o capim limpo) para um arquivo próprio da floresta e troca a referência
    /// no Terrain e no TerrainCollider desse bloco. Depois o arquivo da Polytope pode voltar ao original (git checkout).
    /// Batch: unity run . -- -executeMethod VRRehab.EditorTools.ForestTerrainDataSplit.ApplyBatch
    /// </summary>
    public static class ForestTerrainDataSplit
    {
        public const string PolytopeData = "Assets/Polytope Studio/Lowpoly_Demos/Environment_Free/Helpers/Terrain/New Terrain.asset";
        public const string ForestData = "Assets/Terrains/Floresta_e_Rio/Floresta_Terrain_Centro.asset";

        public static void ApplyBatch()
        {
            int code = 1;
            try { Debug.Log(Apply()); code = 0; }
            catch (System.Exception e) { Debug.LogException(e); }
            EditorApplication.Exit(code);
        }

        public static string Apply()
        {
            var sb = new StringBuilder();
            var source = AssetDatabase.LoadAssetAtPath<TerrainData>(PolytopeData);
            if (source == null) throw new System.Exception("TerrainData da Polytope não encontrado");

            if (AssetDatabase.LoadAssetAtPath<TerrainData>(ForestData) == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ForestData));
                AssetDatabase.Refresh();
                if (!AssetDatabase.CopyAsset(PolytopeData, ForestData)) throw new System.Exception("falha ao copiar o TerrainData");
                sb.AppendLine($"Copiado: {PolytopeData} -> {ForestData}");
            }
            var copy = AssetDatabase.LoadAssetAtPath<TerrainData>(ForestData);

            var scene = EditorSceneManager.OpenScene(ForestAuditTool.ForestScene, OpenSceneMode.Single);
            int swapped = 0;
            foreach (Terrain t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.terrainData == source) { t.terrainData = copy; EditorUtility.SetDirty(t); swapped++; sb.AppendLine($"  Terrain {t.name}: agora usa {ForestData}"); }
                var col = t.GetComponent<TerrainCollider>();
                if (col != null && col.terrainData == source) { col.terrainData = copy; EditorUtility.SetDirty(col); swapped++; sb.AppendLine($"  TerrainCollider {t.name}: agora usa {ForestData}"); }
            }
            int leftover = 0;
            foreach (Terrain t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.terrainData == source || (t.GetComponent<TerrainCollider>() != null && t.GetComponent<TerrainCollider>().terrainData == source)) leftover++;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            sb.AppendLine($"Referências trocadas: {swapped} | ainda apontando para a Polytope: {leftover}");
            File.WriteAllText(".agents/visual_qa/FOREST_TerrainDataSplit.txt", sb.ToString());
            return sb.ToString();
        }
    }
}
