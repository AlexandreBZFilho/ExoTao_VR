using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace VRRehab.EditorTools
{
    /// <summary>
    /// Fotos offscreen (antes/depois) dos modelos gerados que tiveram a contagem de polígonos reduzida.
    /// Abre uma cena vazia temporária (não salva), põe o modelo isolado com luz neutra e fotografa de 3/4.
    /// Batch: unity run . -- -executeMethod VRRehab.EditorTools.ModelPreviewCapture.CaptureBefore  (ou CaptureAfter)
    /// </summary>
    public static class ModelPreviewCapture
    {
        public static readonly string[] Models =
        {
            "Assets/Models/Generated/Task08/MiniBasketball.fbx",
            "Assets/Models/Generated/Task08/ArcadeBasketballHoop.fbx",
            "Assets/Models/Generated/Task07/InflatableBalloon.fbx",
            "Assets/Models/Generated/Task12/BouncyMushroomCluster.fbx",
            "Assets/Models/Generated/Task14/BonusDiamondCoin.fbx",
            "Assets/Models/Generated/Task16/Trophy_GoldenApple.fbx",
            "Assets/Models/Generated/Task16/Trophy_DroneCrownHat.fbx",
        };

        public static void CaptureBefore() => Batch("antes");
        public static void CaptureAfter() => Batch("depois");

        /// <summary>Depois de regerar os FBX: fotos, auditoria de polígonos e checagem de malhas perdidas em prefabs/cenas.</summary>
        public static void AfterRegen()
        {
            int code = 1;
            try
            {
                Capture("depois");
                ModelPolycountAudit.Run();
                var sb = new System.Text.StringBuilder();
                int missing = 0;
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
                        if (mf.sharedMesh == null && mf.GetComponent<MeshRenderer>() != null) { missing++; sb.AppendLine($"  prefab {p} -> {mf.name}"); }
                }
                foreach (string scenePath in new[] { "Assets/Scenes/Menu_Principal.unity", "Assets/Scenes/Floresta_e_Rio.unity" })
                {
                    var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    foreach (GameObject root in scene.GetRootGameObjects())
                        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
                            if (mf.sharedMesh == null && mf.GetComponent<MeshRenderer>() != null && mf.GetComponent<TMPro.TMP_Text>() == null)
                            { missing++; sb.AppendLine($"  cena {scene.name} -> {ForestAuditTool.PathOf(mf.transform)}"); }
                }
                File.WriteAllText(".agents/visual_qa/MODELS_MissingMeshCheck.txt", $"MeshFilter sem malha (prefabs + Menu + Floresta): {missing}\n{sb}");
                code = 0;
            }
            catch (System.Exception e) { Debug.LogException(e); }
            EditorApplication.Exit(code);
        }

        private static void Batch(string suffix)
        {
            int code = 1;
            try { Capture(suffix); code = 0; }
            catch (System.Exception e) { Debug.LogException(e); }
            EditorApplication.Exit(code);
        }

        public static void Capture(string suffix)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var lightGo = new GameObject("Key");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);

            foreach (string path in Models)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { Debug.LogWarning("não achei " + path); continue; }
                var go = (GameObject)Object.Instantiate(prefab);
                go.transform.position = Vector3.zero;
                Bounds b = ForestAuditTool.RendererBounds(go, out bool has);
                if (!has) { Object.DestroyImmediate(go); continue; }
                float size = b.size.magnitude;
                Vector3 dir = new Vector3(0.55f, 0.35f, -0.75f).normalized;
                Vector3 camPos = b.center + dir * size * 1.35f;
                string name = Path.GetFileNameWithoutExtension(path);
                ForestAuditTool.RenderTo($".agents/visual_qa/MODELS_{name}_{suffix}.png", camPos, b.center, 40f, 900, 700,
                    ~0, new Color(0.18f, 0.2f, 0.24f));
                Object.DestroyImmediate(go);
            }
        }
    }
}
