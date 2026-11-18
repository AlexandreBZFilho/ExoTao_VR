using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRRehab.Interactive.Task13;

namespace VRRehab.EditorTools
{
    /// <summary>Inspeção e capturas do Pássaro Guia da Floresta (TASK-13).</summary>
    public static partial class ForestBirdTools
    {
        public static ReactiveForestBird FindBird()
        {
            return Object.FindFirstObjectByType<ReactiveForestBird>(FindObjectsInactive.Include);
        }

        public static string InspectBird()
        {
            Scene scene = SceneManager.GetSceneByPath(ForestAuditTool.ForestScene);
            if (!scene.IsValid() || !scene.isLoaded) EditorSceneManager.OpenScene(ForestAuditTool.ForestScene, OpenSceneMode.Single);

            var sb = new StringBuilder();
            sb.AppendLine("=== Inspeção do pássaro ===");
            ReactiveForestBird[] birds = Object.FindObjectsByType<ReactiveForestBird>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            sb.AppendLine($"Pássaros na cena: {birds.Length}");
            foreach (ReactiveForestBird bird in birds)
            {
                Transform t = bird.transform;
                sb.AppendLine($"- {ForestAuditTool.PathOf(t)} pos {ForestAuditTool.V(t.position)} escala {t.lossyScale} prefab={PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(bird.gameObject)}");
                int total = 0;
                foreach (Transform ch in t.GetComponentsInChildren<Transform>(true))
                {
                    MeshFilter mf = ch.GetComponent<MeshFilter>();
                    SkinnedMeshRenderer smr = ch.GetComponent<SkinnedMeshRenderer>();
                    Mesh m = mf != null ? mf.sharedMesh : (smr != null ? smr.sharedMesh : null);
                    int tris = m != null ? m.triangles.Length / 3 : 0;
                    total += tris;
                    Renderer r = ch.GetComponent<Renderer>();
                    string b = r != null ? $" bounds y[{r.bounds.min.y - t.position.y:0.000}..{r.bounds.max.y - t.position.y:0.000}] mats={r.sharedMaterials.Length}" : "";
                    sb.AppendLine($"    {ForestAuditTool.PathOf(ch).Substring(ForestAuditTool.PathOf(t).Length)} lp {ForestAuditTool.V(ch.localPosition)} tris={tris}{b}{(ch.gameObject.activeSelf ? "" : " (inativo)")}");
                }
                sb.AppendLine($"    TOTAL tris: {total}");

                var so = new SerializedObject(bird);
                SerializedProperty p = so.GetIterator();
                bool enter = true;
                while (p.NextVisible(enter))
                {
                    enter = false;
                    if (p.name == "m_Script") continue;
                    string v = p.propertyType == SerializedPropertyType.ObjectReference
                        ? (p.objectReferenceValue != null ? p.objectReferenceValue.name + " @" + AssetDatabase.GetAssetPath(p.objectReferenceValue) : "null")
                        : p.propertyType == SerializedPropertyType.Float ? p.floatValue.ToString("0.###")
                        : p.propertyType == SerializedPropertyType.Integer ? p.intValue.ToString()
                        : p.propertyType == SerializedPropertyType.Boolean ? p.boolValue.ToString()
                        : p.isArray ? $"[{p.arraySize}]" : p.propertyType.ToString();
                    sb.AppendLine($"    {p.name} = {v}");
                    if (p.isArray && p.name == "perchPositions")
                        for (int i = 0; i < p.arraySize; i++)
                            sb.AppendLine($"       [{i}] {ForestAuditTool.V(p.GetArrayElementAtIndex(i).vector3Value)}");
                }

                foreach (AudioSource a in bird.GetComponentsInChildren<AudioSource>(true))
                    sb.AppendLine($"    AudioSource: loop={a.loop} playOnAwake={a.playOnAwake} vol={a.volume} blend={a.spatialBlend} max={a.maxDistance} clip={(a.clip != null ? a.clip.name : "null")}");
                foreach (Collider c in bird.GetComponentsInChildren<Collider>(true))
                    sb.AppendLine($"    Collider: {c.GetType().Name} trigger={c.isTrigger} bounds {ForestAuditTool.V(c.bounds.size)}");

                Vector3 f = t.forward, pos = t.position;
                sb.AppendLine("    " + ForestAuditTool.RenderTo(".agents/visual_qa/FOREST_bird_before_front.png", pos + f * 0.55f + Vector3.up * 0.08f, pos + Vector3.up * 0.08f, 35f, 1600, 900));
                sb.AppendLine("    " + ForestAuditTool.RenderTo(".agents/visual_qa/FOREST_bird_before_side.png", pos + t.right * 0.55f + Vector3.up * 0.03f, pos + Vector3.up * 0.06f, 35f, 1600, 900));
            }
            return sb.ToString();
        }
    }
}
