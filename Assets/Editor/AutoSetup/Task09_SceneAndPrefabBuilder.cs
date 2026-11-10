using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using VRRehab.Core;
using VRRehab.Interactive.Task09;
using VRRehab.EditorTools;

namespace VRRehab.EditorAutoSetup
{
    /// <summary>
    /// Builder e validador automatizado para a TASK-09 (Bancada Fidget 3D: Alavanca de Clima, Botão de Fogos e Mascote Bobblehead).
    /// </summary>
    public static class Task09_SceneAndPrefabBuilder
    {
        private const string ModelsDir = "Assets/Models/Generated/Task09";
        private const string PrefabsDir = "Assets/Prefabs/Interactive";
        private const string AudioDir = "Assets/Audio/Generated/Task09";
        private const string MenuScenePath = "Assets/Scenes/Menu_Principal.unity";
        private const string OutVisualQADir = ".agents/visual_qa";

        private const string LeverModelPath = "Assets/Models/Generated/Task09/EnvironmentControlLever.fbx";
        private const string ButtonModelPath = "Assets/Models/Generated/Task09/FireworksLaunchButton.fbx";
        private const string BobbleModelPath = "Assets/Models/Generated/Task09/ExoMascotBobblehead.fbx";

        private const string SfxLeverClickPath = "Assets/Audio/Generated/Task09/lever_click_notch.wav";
        private const string SfxAtmosphereWhooshPath = "Assets/Audio/Generated/Task09/atmosphere_transition_whoosh.wav";
        private const string SfxFireworksWhistlePath = "Assets/Audio/Generated/Task09/fireworks_launch_whistle.wav";
        private const string SfxFireworksBurstPath = "Assets/Audio/Generated/Task09/fireworks_burst_explosion.wav";
        private const string SfxBobbleBoingPath = "Assets/Audio/Generated/Task09/bobblehead_spring_boing.wav";
        private const string SfxButtonPressPath = "Assets/Audio/Generated/Task08/arcade_button_press.wav";

        [MenuItem("VRRehab/AutoBuild/Build and Verify TASK-09")]
        public static void BuildAndVerify()
        {
            Debug.Log("[TASK-09] Iniciando construção completa da Bancada Fidget 3D...");

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(LeverModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(ButtonModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(BobbleModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxLeverClickPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxAtmosphereWhooshPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxFireworksWhistlePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxFireworksBurstPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SfxBobbleBoingPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            string[] prefabsToDelete = new string[] {
                $"{PrefabsDir}/EnvironmentControlLever.prefab",
                $"{PrefabsDir}/FireworksLaunchButton.prefab",
                $"{PrefabsDir}/ExoMascotBobblehead.prefab"
            };
            foreach (var p in prefabsToDelete)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(p) != null)
                {
                    AssetDatabase.DeleteAsset(p);
                }
            }

            EnsureMaterials();

            GameObject prefabLever = BuildLeverPrefab();
            GameObject prefabButton = BuildButtonPrefab();
            GameObject prefabBobble = BuildBobbleheadPrefab();

            SetupMenuPrincipalScene(prefabLever, prefabButton, prefabBobble);
            CaptureVisualQA();

            Debug.Log("[TASK-09] Conclusão com sucesso de todos os estágios da Bancada Fidget 3D!");
        }

        public static void EnsureMaterials()
        {
            if (!AssetDatabase.IsValidFolder(ModelsDir))
            {
                AssetDatabase.CreateFolder("Assets/Models/Generated", "Task09");
            }

            EnsureMaterial($"{ModelsDir}/M_Lever_Chassis.mat", "M_Lever_Chassis", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Lever_Chassis", new Color(0.14f, 0.16f, 0.19f), 0.75f, 0.45f));

            EnsureMaterial($"{ModelsDir}/M_Lever_Metal.mat", "M_Lever_Metal", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Lever_Metal", new Color(0.85f, 0.88f, 0.92f), 0.92f, 0.85f));

            EnsureMaterial($"{ModelsDir}/M_Lever_Bezel.mat", "M_Lever_Bezel", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Lever_Bezel", new Color(0.08f, 0.09f, 0.11f), 0.20f, 0.40f));

            EnsureMaterial($"{ModelsDir}/M_Lever_Knob.mat", "M_Lever_Knob", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Lever_Knob", new Color(0.88f, 0.22f, 0.12f), 0.45f, 0.65f));

            EnsureMaterial($"{ModelsDir}/M_Lever_Ring.mat", "M_Lever_Ring", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_Lever_Ring", new Color(0.0f, 0.85f, 1.0f), new Color(0.0f, 0.85f, 1.0f), 2.5f));

            EnsureMaterial($"{ModelsDir}/M_Lever_LedDay.mat", "M_Lever_LedDay", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_Lever_LedDay", new Color(1.0f, 0.85f, 0.15f), new Color(1.0f, 0.85f, 0.15f), 2.2f));

            EnsureMaterial($"{ModelsDir}/M_Lever_LedSunset.mat", "M_Lever_LedSunset", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_Lever_LedSunset", new Color(1.0f, 0.45f, 0.08f), new Color(1.0f, 0.45f, 0.08f), 2.2f));

            EnsureMaterial($"{ModelsDir}/M_Lever_LedNight.mat", "M_Lever_LedNight", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_Lever_LedNight", new Color(0.20f, 0.45f, 1.0f), new Color(0.20f, 0.45f, 1.0f), 2.2f));

            EnsureMaterial($"{ModelsDir}/M_FwBtn_Chassis.mat", "M_FwBtn_Chassis", () =>
                MaterialShaderHelper.CreateLitMaterial("M_FwBtn_Chassis", new Color(0.12f, 0.13f, 0.16f), 0.60f, 0.50f));

            EnsureMaterial($"{ModelsDir}/M_FwBtn_Chrome.mat", "M_FwBtn_Chrome", () =>
                MaterialShaderHelper.CreateLitMaterial("M_FwBtn_Chrome", new Color(0.88f, 0.90f, 0.94f), 0.95f, 0.90f));

            EnsureMaterial($"{ModelsDir}/M_FwBtn_Dome.mat", "M_FwBtn_Dome", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_FwBtn_Dome", new Color(0.95f, 0.12f, 0.10f), new Color(0.95f, 0.12f, 0.10f), 1.8f));

            EnsureMaterial($"{ModelsDir}/M_FwBtn_Gold.mat", "M_FwBtn_Gold", () =>
                MaterialShaderHelper.CreateLitMaterial("M_FwBtn_Gold", new Color(1.0f, 0.78f, 0.15f), 0.88f, 0.75f));

            EnsureMaterial($"{ModelsDir}/M_FwBtn_Ring.mat", "M_FwBtn_Ring", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_FwBtn_Ring", new Color(1.0f, 0.60f, 0.0f), new Color(1.0f, 0.60f, 0.0f), 2.2f));

            EnsureMaterial($"{ModelsDir}/M_Bobble_Pedestal.mat", "M_Bobble_Pedestal", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Bobble_Pedestal", new Color(0.13f, 0.14f, 0.17f), 0.70f, 0.55f));

            EnsureMaterial($"{ModelsDir}/M_Bobble_Armor.mat", "M_Bobble_Armor", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Bobble_Armor", new Color(0.05f, 0.42f, 0.65f), 0.65f, 0.70f));

            EnsureMaterial($"{ModelsDir}/M_Bobble_Trim.mat", "M_Bobble_Trim", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Bobble_Trim", new Color(0.85f, 0.88f, 0.92f), 0.85f, 0.75f));

            EnsureMaterial($"{ModelsDir}/M_Bobble_Spring.mat", "M_Bobble_Spring", () =>
                MaterialShaderHelper.CreateLitMaterial("M_Bobble_Spring", new Color(0.75f, 0.77f, 0.80f), 0.95f, 0.85f));

            EnsureMaterial($"{ModelsDir}/M_Bobble_Visor.mat", "M_Bobble_Visor", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_Bobble_Visor", new Color(0.0f, 0.95f, 1.0f), new Color(0.0f, 0.95f, 1.0f), 2.6f));

            EnsureMaterial($"{ModelsDir}/M_Bobble_Ears.mat", "M_Bobble_Ears", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_Bobble_Ears", new Color(1.0f, 0.55f, 0.05f), new Color(1.0f, 0.55f, 0.05f), 1.8f));

            EnsureMaterial($"{ModelsDir}/M_Fireworks_Sparkle.mat", "M_Fireworks_Sparkle", () =>
                MaterialShaderHelper.CreateEmissiveMaterial("M_Fireworks_Sparkle", new Color(1.0f, 0.95f, 0.80f), new Color(1.0f, 0.90f, 0.70f), 3.0f));
        }

        private static void EnsureMaterial(string path, string name, Func<Material> creator)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = creator();
                AssetDatabase.CreateAsset(mat, path);
            }
        }

        private static GameObject BuildLeverPrefab()
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(LeverModelPath);
            if (fbx == null)
            {
                Debug.LogError($"[TASK-09] Modelo FBX da Alavanca não encontrado em {LeverModelPath}");
                return null;
            }

            GameObject root = new GameObject("EnvironmentControlLever");
            root.layer = LayerMask.NameToLayer("Default");
            root.tag = "Interactable";

            GameObject visual = UnityEngine.Object.Instantiate(fbx, root.transform);
            visual.name = "Visual_Model";

            Material matChassis = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Lever_Chassis.mat");
            Material matMetal = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Lever_Metal.mat");
            Material matBezel = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Lever_Bezel.mat");
            Material matKnob = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Lever_Knob.mat");
            Material matRing = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Lever_Ring.mat");
            Material matDay = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Lever_LedDay.mat");
            Material matSunset = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Lever_LedSunset.mat");
            Material matNight = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Lever_LedNight.mat");

            Renderer ledDayRend = null;
            Renderer ledSunsetRend = null;
            Renderer ledNightRend = null;
            Transform handlePivot = null;

            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = mr.gameObject.name;
                if (n.Contains("Chassis")) mr.sharedMaterial = matChassis;
                else if (n.Contains("Slot_Guide")) mr.sharedMaterial = matBezel;
                else if (n.Contains("Indicator_Day")) { mr.sharedMaterial = matDay; ledDayRend = mr; }
                else if (n.Contains("Indicator_Sunset")) { mr.sharedMaterial = matSunset; ledSunsetRend = mr; }
                else if (n.Contains("Indicator_Night")) { mr.sharedMaterial = matNight; ledNightRend = mr; }
                else if (n.Contains("Handle_Pivot"))
                {
                    handlePivot = mr.transform;
                    mr.sharedMaterials = new Material[] { matMetal, matKnob, matRing };
                }
                else mr.sharedMaterial = matChassis;
            }

            BoxCollider baseCol = root.AddComponent<BoxCollider>();
            baseCol.center = new Vector3(0f, 0.025f, 0f);
            baseCol.size = new Vector3(0.16f, 0.05f, 0.12f);

            SphereCollider knobCol = root.AddComponent<SphereCollider>();
            knobCol.center = new Vector3(0f, 0.16f, 0f);
            knobCol.radius = 0.040f;

            AudioSource audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1.0f;
            audio.minDistance = 0.2f;
            audio.maxDistance = 5.0f;

            AtmosphereLeverInteractable lever = root.AddComponent<AtmosphereLeverInteractable>();
            SerializedObject soLever = new SerializedObject(lever);
            soLever.FindProperty("handlePivot").objectReferenceValue = handlePivot;
            soLever.FindProperty("ledDayRenderer").objectReferenceValue = ledDayRend;
            soLever.FindProperty("ledSunsetRenderer").objectReferenceValue = ledSunsetRend;
            soLever.FindProperty("ledNightRenderer").objectReferenceValue = ledNightRend;
            soLever.FindProperty("sfxLeverClick").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxLeverClickPath);
            soLever.FindProperty("sfxAtmosphereWhoosh").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxAtmosphereWhooshPath);
            soLever.FindProperty("audioSource").objectReferenceValue = audio;
            soLever.FindProperty("rotationAxis").vector3Value = new Vector3(0f, 0f, 1f);
            soLever.FindProperty("angleDay").floatValue = 22f;
            soLever.FindProperty("angleSunset").floatValue = 0f;
            soLever.FindProperty("angleNight").floatValue = -22f;
            soLever.ApplyModifiedPropertiesWithoutUndo();

            string savePath = $"{PrefabsDir}/EnvironmentControlLever.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log($"[TASK-09] Prefab EnvironmentControlLever salvo em {savePath}");
            return savedPrefab;
        }

        private static GameObject BuildButtonPrefab()
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonModelPath);
            if (fbx == null)
            {
                Debug.LogError($"[TASK-09] Modelo FBX do Botão de Fogos não encontrado em {ButtonModelPath}");
                return null;
            }

            GameObject root = new GameObject("FireworksLaunchButton");
            root.layer = LayerMask.NameToLayer("Default");
            root.tag = "Interactable";

            GameObject visual = UnityEngine.Object.Instantiate(fbx, root.transform);
            visual.name = "Visual_Model";

            Material matChassis = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_FwBtn_Chassis.mat");
            Material matChrome = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_FwBtn_Chrome.mat");
            Material matDome = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_FwBtn_Dome.mat");
            Material matGold = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_FwBtn_Gold.mat");
            Material matRing = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_FwBtn_Ring.mat");

            Transform domeTransform = null;

            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = mr.gameObject.name;
                if (n.Contains("Chassis")) mr.sharedMaterial = matChassis;
                else if (n.Contains("Chrome") || n.Contains("Bezel")) mr.sharedMaterial = matChrome;
                else if (n.Contains("Dome")) { mr.sharedMaterial = matDome; domeTransform = mr.transform; }
                else if (n.Contains("Label") || n.Contains("Gold")) mr.sharedMaterial = matGold;
                else if (n.Contains("Glow") || n.Contains("Ring")) mr.sharedMaterial = matRing;
                else mr.sharedMaterial = matChassis;
            }

            BoxCollider baseCol = root.AddComponent<BoxCollider>();
            baseCol.center = new Vector3(0f, 0.021f, 0f);
            baseCol.size = new Vector3(0.12f, 0.042f, 0.12f);

            SphereCollider domeCol = root.AddComponent<SphereCollider>();
            domeCol.center = new Vector3(0f, 0.052f, 0f);
            domeCol.radius = 0.040f;

            GameObject lightGo = new GameObject("Button_Glow_Light");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.065f, 0f);
            Light btnLight = lightGo.AddComponent<Light>();
            btnLight.type = LightType.Point;
            btnLight.range = 0.35f;
            btnLight.intensity = 1.0f;
            btnLight.color = new Color(1.0f, 0.40f, 0.05f);

            AudioSource audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1.0f;
            audio.minDistance = 0.2f;
            audio.maxDistance = 6.0f;

            FireworksButtonInteractable btn = root.AddComponent<FireworksButtonInteractable>();
            SerializedObject soBtn = new SerializedObject(btn);
            soBtn.FindProperty("buttonDomeTransform").objectReferenceValue = domeTransform;
            soBtn.FindProperty("pressedLocalOffset").vector3Value = new Vector3(0f, -0.012f, 0f);
            soBtn.FindProperty("buttonGlowLight").objectReferenceValue = btnLight;
            soBtn.FindProperty("sfxButtonPress").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxButtonPressPath);
            soBtn.FindProperty("sfxLaunchWhistle").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxFireworksWhistlePath);
            soBtn.FindProperty("sfxBurstExplosion").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxFireworksBurstPath);
            soBtn.FindProperty("audioSource").objectReferenceValue = audio;
            soBtn.FindProperty("rocketTravelTime").floatValue = 0.60f;
            soBtn.ApplyModifiedPropertiesWithoutUndo();

            string savePath = $"{PrefabsDir}/FireworksLaunchButton.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log($"[TASK-09] Prefab FireworksLaunchButton salvo em {savePath}");
            return savedPrefab;
        }

        private static GameObject BuildBobbleheadPrefab()
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(BobbleModelPath);
            if (fbx == null)
            {
                Debug.LogError($"[TASK-09] Modelo FBX do Mascote Bobblehead não encontrado em {BobbleModelPath}");
                return null;
            }

            GameObject root = new GameObject("ExoMascotBobblehead");
            root.layer = LayerMask.NameToLayer("Default");
            root.tag = "Interactable";

            GameObject visual = UnityEngine.Object.Instantiate(fbx, root.transform);
            visual.name = "Visual_Model";

            Material matPedestal = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Bobble_Pedestal.mat");
            Material matArmor = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Bobble_Armor.mat");
            Material matTrim = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Bobble_Trim.mat");
            Material matSpring = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Bobble_Spring.mat");
            Material matVisor = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Bobble_Visor.mat");
            Material matEars = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Bobble_Ears.mat");

            Transform headPivot = null;
            Renderer visorRenderer = null;

            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = mr.gameObject.name;
                if (n.Contains("Pedestal") && !n.Contains("Ring")) mr.sharedMaterial = matPedestal;
                else if (n.Contains("Pedestal_Ring") || n.Contains("Reactor")) mr.sharedMaterial = matVisor;
                else if (n.Contains("Armor") || n.Contains("Torso")) mr.sharedMaterial = matArmor;
                else if (n.Contains("Trim") || n.Contains("Arm")) mr.sharedMaterial = matTrim;
                else if (n.Contains("Spring")) mr.sharedMaterial = matSpring;
                else if (n.Contains("Head_Pivot"))
                {
                    headPivot = mr.transform;
                    visorRenderer = mr;
                    mr.sharedMaterials = new Material[] { matArmor, matVisor, matTrim, matEars };
                }
                else mr.sharedMaterial = matArmor;
            }

            BoxCollider baseCol = root.AddComponent<BoxCollider>();
            baseCol.center = new Vector3(0f, 0.040f, 0f);
            baseCol.size = new Vector3(0.09f, 0.080f, 0.09f);

            SphereCollider headCol = root.AddComponent<SphereCollider>();
            headCol.center = new Vector3(0f, 0.125f, 0f);
            headCol.radius = 0.045f;

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            GameObject lightGo = new GameObject("Visor_Glow_Light");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.125f, 0.045f);
            Light visorLight = lightGo.AddComponent<Light>();
            visorLight.type = LightType.Point;
            visorLight.range = 0.30f;
            visorLight.intensity = 1.2f;
            visorLight.color = new Color(0.0f, 0.95f, 1.0f);

            AudioSource audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1.0f;
            audio.minDistance = 0.2f;
            audio.maxDistance = 5.0f;

            BobbleheadSpringToy bobble = root.AddComponent<BobbleheadSpringToy>();
            SerializedObject soBobble = new SerializedObject(bobble);
            soBobble.FindProperty("headTransform").objectReferenceValue = headPivot;
            soBobble.FindProperty("visorRenderer").objectReferenceValue = visorRenderer;
            soBobble.FindProperty("visorGlowLight").objectReferenceValue = visorLight;
            soBobble.FindProperty("sfxBoing").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxBobbleBoingPath);
            soBobble.FindProperty("audioSource").objectReferenceValue = audio;
            soBobble.FindProperty("naturalFrequency").floatValue = 13.5f;
            soBobble.FindProperty("dampingRatio").floatValue = 0.14f;
            soBobble.FindProperty("maxDeflectionAngle").floatValue = 45f;
            soBobble.ApplyModifiedPropertiesWithoutUndo();

            string savePath = $"{PrefabsDir}/ExoMascotBobblehead.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log($"[TASK-09] Prefab ExoMascotBobblehead salvo em {savePath}");
            return savedPrefab;
        }

        public static void SetupMenuPrincipalScene(GameObject prefabLever, GameObject prefabButton, GameObject prefabBobble)
        {
            if (EditorSceneManager.GetActiveScene().path != MenuScenePath)
            {
                EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            }

            string[] toClean = new string[] {
                "EnvironmentControlLever_Instance",
                "FireworksLaunchButton_Instance",
                "ExoMascotBobblehead_Instance",
                "Room_Fireworks_Launcher"
            };
            foreach (var cName in toClean)
            {
                GameObject existing = GameObject.Find(cName);
                if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
            }

            GameObject deco = GameObject.Find("Deco Variant 3");
            if (deco != null)
            {
                deco.transform.position = new Vector3(0.56f, 0.955f, 0.60f);
            }

            Vector3 leverPos = new Vector3(0.66f, 0.955f, 0.62f);
            Quaternion leverRot = Quaternion.Euler(0f, 75f, 0f);

            GameObject leverInst = (GameObject)PrefabUtility.InstantiatePrefab(prefabLever);
            leverInst.name = "EnvironmentControlLever_Instance";
            leverInst.transform.position = leverPos;
            leverInst.transform.rotation = leverRot;

            Light dirLight = GameObject.Find("Directional Light")?.GetComponent<Light>();
            AtmosphereLeverInteractable leverComp = leverInst.GetComponent<AtmosphereLeverInteractable>();
            if (dirLight != null && leverComp != null)
            {
                SerializedObject so = new SerializedObject(leverComp);
                so.FindProperty("directionalLight").objectReferenceValue = dirLight;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            Vector3 btnPos = new Vector3(0.79f, 0.955f, 0.60f);
            Quaternion btnRot = Quaternion.Euler(0f, 75f, 0f);

            GameObject btnInst = (GameObject)PrefabUtility.InstantiatePrefab(prefabButton);
            btnInst.name = "FireworksLaunchButton_Instance";
            btnInst.transform.position = btnPos;
            btnInst.transform.rotation = btnRot;

            Vector3 bobblePos = new Vector3(0.73f, 0.955f, 0.72f);
            Quaternion bobbleRot = Quaternion.Euler(0f, 200f, 0f);

            GameObject bobbleInst = (GameObject)PrefabUtility.InstantiatePrefab(prefabBobble);
            bobbleInst.name = "ExoMascotBobblehead_Instance";
            bobbleInst.transform.position = bobblePos;
            bobbleInst.transform.rotation = bobbleRot;

            SetupFireworksParticleLauncher(btnInst);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("[TASK-09] Cena Menu_Principal configurada com Bancada Fidget completa!");
        }

        private static void SetupFireworksParticleLauncher(GameObject buttonInstance)
        {
            GameObject launcherGo = new GameObject("Room_Fireworks_Launcher");
            launcherGo.transform.position = buttonInstance.transform.position;

            // Ponto de detonação no ar acima da mesa perfeitamente enquadrado na câmera (X = 1.80m, Y = 2.45m, Z = 1.40m)
            GameObject burstTargetGo = new GameObject("Burst_Apex_Target");
            burstTargetGo.transform.SetParent(launcherGo.transform, false);
            burstTargetGo.transform.position = new Vector3(1.80f, 2.45f, 1.40f);

            GameObject ascentGo = new GameObject("Ascent_Sparks_Trail");
            ascentGo.transform.SetParent(launcherGo.transform, false);
            ascentGo.transform.position = buttonInstance.transform.position + new Vector3(0f, 0.05f, 0f);
            ParticleSystem psAscent = ascentGo.AddComponent<ParticleSystem>();
            var mainA = psAscent.main;
            mainA.playOnAwake = false;
            mainA.loop = false;
            mainA.duration = 0.55f;
            mainA.startLifetime = 0.40f;
            mainA.startSpeed = 8.0f;
            mainA.startSize = 0.04f;
            mainA.startColor = new Color(1.0f, 0.75f, 0.20f);

            var emA = psAscent.emission;
            emA.rateOverTime = 80f;

            var shapeA = psAscent.shape;
            shapeA.shapeType = ParticleSystemShapeType.Cone;
            shapeA.angle = 8f;
            ascentGo.transform.LookAt(burstTargetGo.transform.position);

            GameObject burstGo = new GameObject("Burst_Explosion_System");
            burstGo.transform.SetParent(launcherGo.transform, false);
            burstGo.transform.position = burstTargetGo.transform.position;
            ParticleSystem psBurst = burstGo.AddComponent<ParticleSystem>();

            var mainB = psBurst.main;
            mainB.playOnAwake = false;
            mainB.loop = false;
            mainB.duration = 1.2f;
            mainB.startLifetime = 1.2f;
            mainB.startSpeed = 3.8f;
            mainB.startSize = 0.09f;
            mainB.gravityModifier = 0.30f;

            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.0f, 0.95f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(1.0f, 0.85f, 0.10f), 0.35f),
                    new GradientColorKey(new Color(1.0f, 0.20f, 0.65f), 0.70f),
                    new GradientColorKey(new Color(1.0f, 0.45f, 0.05f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.95f, 0.6f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            mainB.startColor = new ParticleSystem.MinMaxGradient(grad);

            var emB = psBurst.emission;
            emB.enabled = false;
            emB.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0.0f, 180)
            });

            var shapeB = psBurst.shape;
            shapeB.shapeType = ParticleSystemShapeType.Sphere;
            shapeB.radius = 0.20f;

            Material matSparkle = AssetDatabase.LoadAssetAtPath<Material>($"{ModelsDir}/M_Fireworks_Sparkle.mat");
            var rendB = burstGo.GetComponent<ParticleSystemRenderer>();
            if (rendB != null && matSparkle != null) rendB.sharedMaterial = matSparkle;
            var rendA = ascentGo.GetComponent<ParticleSystemRenderer>();
            if (rendA != null && matSparkle != null) rendA.sharedMaterial = matSparkle;

            GameObject lightGo = new GameObject("Burst_Flash_Light");
            lightGo.transform.SetParent(burstGo.transform, false);
            lightGo.transform.localPosition = Vector3.zero;
            Light burstLight = lightGo.AddComponent<Light>();
            burstLight.type = LightType.Point;
            burstLight.range = 4.5f;
            burstLight.intensity = 0f;
            burstLight.color = new Color(1.0f, 0.85f, 0.45f);
            burstLight.enabled = false;

            FireworksButtonInteractable btnComp = buttonInstance.GetComponent<FireworksButtonInteractable>();
            if (btnComp != null)
            {
                SerializedObject so = new SerializedObject(btnComp);
                so.FindProperty("fireworksAscentParticles").objectReferenceValue = psAscent;
                so.FindProperty("fireworksBurstParticles").objectReferenceValue = psBurst;
                so.FindProperty("fireworksBurstLight").objectReferenceValue = burstLight;
                so.FindProperty("burstLocation").objectReferenceValue = burstTargetGo.transform;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        public static void CaptureVisualQA()
        {
            string outDir = Path.GetFullPath(OutVisualQADir);
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            if (EditorSceneManager.GetActiveScene().path != MenuScenePath)
            {
                EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            }

            GameObject leverGo = GameObject.Find("EnvironmentControlLever_Instance");
            AtmosphereLeverInteractable lever = leverGo != null ? leverGo.GetComponent<AtmosphereLeverInteractable>() : null;

            // 1. TASK-09_Fidget_Station_Desk.png: Close-up perfeitamente enquadrado dos 3 brinquedos na mesa
            string pathDesk = Path.Combine(outDir, "TASK-09_Fidget_Station_Desk.png");
            Vector3 deskCamPos = new Vector3(1.28f, 1.22f, 0.64f);
            Vector3 deskTarget = new Vector3(0.72f, 1.04f, 0.64f);
            VisualQACaptureEditor.CaptureCameraView(pathDesk, deskCamPos, deskTarget, 1920, 1080);
            Debug.Log($"[TASK-09] Visual QA 1 capturado: {pathDesk}");

            // 2. TASK-09_Atmosphere_Day.png: Visão geral da sala na iluminação de Dia
            if (lever != null) lever.ApplyAtmosphere(AtmosphereLeverInteractable.AtmosphereState.Day, 1.0f, true);
            string pathDay = Path.Combine(outDir, "TASK-09_Atmosphere_Day.png");
            Vector3 roomCamPos = new Vector3(4.50f, 1.67f, 2.31f);
            Vector3 roomTarget = new Vector3(0.70f, 1.15f, 0.65f);
            VisualQACaptureEditor.CaptureCameraView(pathDay, roomCamPos, roomTarget, 1920, 1080);
            Debug.Log($"[TASK-09] Visual QA 2 capturado: {pathDay}");

            // 3. TASK-09_Atmosphere_Sunset.png: Visão geral da sala na iluminação de Pôr do Sol
            if (lever != null) lever.ApplyAtmosphere(AtmosphereLeverInteractable.AtmosphereState.Sunset, 1.0f, true);
            string pathSunset = Path.Combine(outDir, "TASK-09_Atmosphere_Sunset.png");
            VisualQACaptureEditor.CaptureCameraView(pathSunset, roomCamPos, roomTarget, 1920, 1080);
            Debug.Log($"[TASK-09] Visual QA 3 capturado: {pathSunset}");

            // 4. TASK-09_Atmosphere_Night.png: Visão da sala na iluminação de Noite com fogos ativos
            if (lever != null) lever.ApplyAtmosphere(AtmosphereLeverInteractable.AtmosphereState.Night, 1.0f, true);
            ParticleSystem psBurst = GameObject.Find("Burst_Explosion_System")?.GetComponent<ParticleSystem>();
            if (psBurst != null)
            {
                psBurst.Simulate(0.35f, true, true);
            }
            Light flashLight = GameObject.Find("Burst_Flash_Light")?.GetComponent<Light>();
            if (flashLight != null)
            {
                flashLight.enabled = true;
                flashLight.intensity = 1.4f;
                flashLight.range = 3.5f;
            }

            string pathNight = Path.Combine(outDir, "TASK-09_Atmosphere_Night.png");
            Vector3 nightLookTarget = new Vector3(1.60f, 1.65f, 1.20f);
            VisualQACaptureEditor.CaptureCameraView(pathNight, roomCamPos, nightLookTarget, 1920, 1080);
            Debug.Log($"[TASK-09] Visual QA 4 capturado: {pathNight}");

            if (lever != null) lever.ApplyAtmosphere(AtmosphereLeverInteractable.AtmosphereState.Day, 1.0f, true);
            if (flashLight != null) flashLight.enabled = false;
            if (psBurst != null) psBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }
    }
}
