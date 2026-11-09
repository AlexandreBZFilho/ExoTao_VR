using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Menu de ferramentas no topo da Unity ("ExoTao IC") para facilitar a operação no laboratório ReRobLab.
/// Executa correções automáticas de VR UI, Canvases e do Avatar 3D (Y Bot + Exoesqueleto ExoTao) no carregamento do Editor.
/// </summary>
[InitializeOnLoad]
public static class PainelControleICEditor
{
    static PainelControleICEditor()
    {
        EditorApplication.delayCall += ExecutarConfiguracaoAutomaticaInicial;
    }

    private static void ExecutarConfiguracaoAutomaticaInicial()
    {
        if (Application.isPlaying) return;
        AutoCorrigirAvisosCanvas();

        bool precisaCorrigirShader = false;
        Material matTeste = Resources.Load<Material>("Materials/YBot_Surface_URP");
        if (matTeste != null && UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null)
        {
            if (matTeste.shader != null && matTeste.shader.name.Contains("Universal"))
            {
                precisaCorrigirShader = true;
            }
        }

        VerificarEConfigurarAvatar3DAutomatico(forcarRebuildPrefabs: precisaCorrigirShader);
    }

    [MenuItem("ExoTao IC/0. Corrigir Avisos do Canvas e VR UI", false, 0)]
    public static void AutoCorrigirAvisosCanvas()
    {
        bool alterou = false;

        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            mainCam = Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude);
        }

        foreach (Canvas c in canvases)
        {
            if (c == null) continue;

            // 1. Ativa Gamma Space no Unity 6 URP para corrigir gradientes de cores
            if (!c.vertexColorAlwaysGammaSpace)
            {
                c.vertexColorAlwaysGammaSpace = true;
                alterou = true;
                UnityEngine.Debug.Log($"[ExoTao IC] 'vertexColorAlwaysGammaSpace' ativado no Canvas '{c.gameObject.name}'.");
            }

            // 2. Associa a Event Camera se estiver em World Space e vazia
            if (c.renderMode == RenderMode.WorldSpace && c.worldCamera == null && mainCam != null)
            {
                c.worldCamera = mainCam;
                alterou = true;
                UnityEngine.Debug.Log($"[ExoTao IC] Event Camera associada à '{mainCam.name}' no Canvas '{c.gameObject.name}'.");
            }

            // 3. Remove XRRayInteractor indevido no Canvas (ele pertence somente aos controles das mãos)
            var rayInteractor = c.GetComponent<XRRayInteractor>();
            if (rayInteractor != null)
            {
                Object.DestroyImmediate(rayInteractor, true);
                alterou = true;
                UnityEngine.Debug.Log($"[ExoTao IC] Removido XRRayInteractor indevido do GameObject '{c.gameObject.name}'.");
            }

            // 4. Garante TrackedDeviceGraphicRaycaster no Canvas para compatibilidade com o laser VR
            if (c.renderMode == RenderMode.WorldSpace)
            {
                var trackedRaycaster = c.GetComponent<TrackedDeviceGraphicRaycaster>();
                if (trackedRaycaster == null)
                {
                    c.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
                    alterou = true;
                    UnityEngine.Debug.Log($"[ExoTao IC] TrackedDeviceGraphicRaycaster adicionado ao Canvas '{c.gameObject.name}'.");
                }
            }
        }

        if (alterou)
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                UnityEngine.Debug.Log($"[ExoTao IC] Cena '{activeScene.name}' salva com sucesso após correção dos Canvases!");
            }
        }
    }

    [MenuItem("ExoTao IC/1. Configurar Avatar 3D (Y Bot + Exoesqueleto + Cinemática UDP)", false, 5)]
    public static void MenuConfigurarAvatar3D()
    {
        VerificarEConfigurarAvatar3DAutomatico(forcarRebuildPrefabs: true);
    }

    public static void VerificarEConfigurarAvatar3DAutomatico(bool forcarRebuildPrefabs)
    {
        // 1. Garante que o prefab base Resources/YBot_Avatar.prefab exista a partir de Assets/Prefabs/Y Bot.fbx
        GarantirResourceYBot();

        // 2. Configura os 3 prefabs de jogador do projeto com o Y Bot 3D + Exoesqueleto + ReceptorUDP + AvatarBiomecanicoVR
        string[] prefabsJogador = {
            "Assets/Prefabs/Pessoa_Virtual_sem3D.prefab",
            "Assets/Prefabs/Pessoa_Virtual_sem3D loading.prefab",
            "Assets/Prefabs/Pessoa_Virtual.prefab"
        };

        bool algumPrefabAtualizado = false;
        foreach (string caminho in prefabsJogador)
        {
            if (File.Exists(caminho))
            {
                if (ConfigurarPrefabJogadorComYBot(caminho, forcarRebuildPrefabs))
                    algumPrefabAtualizado = true;
            }
        }

        if (algumPrefabAtualizado)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // 3. Configura a cena aberta atualmente no Editor
        var cenaAtiva = EditorSceneManager.GetActiveScene();
        if (!cenaAtiva.IsValid()) return;

        bool cenaAlterada = false;

        // Remove os cilindros antigos de teste do Menu_Principal que roubavam a porta UDP 5005
        if (cenaAtiva.name == "Menu_Principal")
        {
            GameObject cyl1 = GameObject.Find("Cylinder");
            if (cyl1 != null && cyl1.GetComponent<ReceptorUDP>() != null)
            {
                Object.DestroyImmediate(cyl1);
                cenaAlterada = true;
                UnityEngine.Debug.Log("[ExoTao IC] Objeto temporário 'Cylinder' (com ReceptorUDP duplicado) removido do Menu_Principal.");
            }
            GameObject cyl2 = GameObject.Find("Cylinder (1)");
            if (cyl2 != null && cyl2.transform.parent == null)
            {
                Object.DestroyImmediate(cyl2);
                cenaAlterada = true;
            }
        }

        // Garante que o jogador na cena atual tenha ReceptorUDP + AvatarBiomecanicoVR inicializados
        CharacterController cc = Object.FindFirstObjectByType<CharacterController>();
        if (cc != null)
        {
            GameObject goJogador = cc.gameObject;
            ReceptorUDP receptor = goJogador.GetComponent<ReceptorUDP>();
            if (receptor == null)
            {
                receptor = goJogador.AddComponent<ReceptorUDP>();
                cenaAlterada = true;
            }
            receptor.controlePaciente = cc;

            AvatarBiomecanicoVR avatar = goJogador.GetComponent<AvatarBiomecanicoVR>();
            if (avatar == null)
            {
                avatar = goJogador.AddComponent<AvatarBiomecanicoVR>();
                cenaAlterada = true;
            }

            if (avatar.ossoQuadrilDir == null || receptor.articulacaoQuadrilDir == null || forcarRebuildPrefabs)
            {
                avatar.receptorExo = receptor;
                avatar.InicializarAvatarECinematica();
                cenaAlterada = true;
            }

            if (forcarRebuildPrefabs)
            {
                Selection.activeGameObject = goJogador;
            }
        }

        if (cenaAlterada)
        {
            EditorSceneManager.MarkSceneDirty(cenaAtiva);
            EditorSceneManager.SaveScene(cenaAtiva);
            UnityEngine.Debug.Log($"[ExoTao IC] Avatar 3D (Y Bot + Exoesqueleto ExoTao) e conexões UDP configurados e salvos na cena '{cenaAtiva.name}'!");
        }
    }

    private static void GarantirResourceYBot()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        string destPath = "Assets/Resources/YBot_Avatar.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(destPath) != null)
            return;

        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Y Bot.fbx");
        if (fbx == null) return;

        GameObject temp = Object.Instantiate(fbx);
        temp.name = "Y Bot";
        Animator anim = temp.GetComponent<Animator>();
        if (anim != null) anim.enabled = false;

        PrefabUtility.SaveAsPrefabAsset(temp, destPath);
        Object.DestroyImmediate(temp);
        UnityEngine.Debug.Log("[ExoTao IC] Prefab base 'Assets/Resources/YBot_Avatar.prefab' criado a partir de 'Y Bot.fbx'.");
    }

    private static bool ConfigurarPrefabJogadorComYBot(string caminhoPrefab, bool forcar)
    {
        GameObject assetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(caminhoPrefab);
        if (assetPrefab == null) return false;

        // Verifica se já está totalmente configurado para evitar regravar sem necessidade
        if (!forcar)
        {
            var av = assetPrefab.GetComponent<AvatarBiomecanicoVR>();
            var rec = assetPrefab.GetComponent<ReceptorUDP>();
            if (av != null && rec != null && rec.articulacaoQuadrilDir != null && av.ossoQuadrilDir != null)
            {
                return false;
            }
        }

        GameObject raiz = PrefabUtility.LoadPrefabContents(caminhoPrefab);
        if (raiz == null) return false;

        try
        {
            CharacterController cc = raiz.GetComponent<CharacterController>();

            // Remove ReceptorUDP duplicado se estiver em filho (como ocorria no Pessoa_Virtual.prefab antigo)
            ReceptorUDP[] receptoresFilhos = raiz.GetComponentsInChildren<ReceptorUDP>(true);
            foreach (ReceptorUDP r in receptoresFilhos)
            {
                if (r.gameObject != raiz)
                {
                    Object.DestroyImmediate(r, true);
                }
            }

            // Garante ReceptorUDP na raiz do jogador
            ReceptorUDP receptor = raiz.GetComponent<ReceptorUDP>();
            if (receptor == null)
                receptor = raiz.AddComponent<ReceptorUDP>();
            receptor.controlePaciente = cc;

            // Garante AvatarBiomecanicoVR na raiz do jogador
            AvatarBiomecanicoVR avatar = raiz.GetComponent<AvatarBiomecanicoVR>();
            if (avatar == null)
                avatar = raiz.AddComponent<AvatarBiomecanicoVR>();

            avatar.receptorExo = receptor;
            avatar.InicializarAvatarECinematica();

            // Remove objetos parents intermediários vazios antigos se sobraram vazios
            string[] nomesParentsAntigos = {
                "Rig_RightUpLeg_Parent",
                "Rig_LeftUpLeg_Parent",
                "Rig_RightLeg_Parent",
                "Rig_LeftLeg_Parent"
            };
            foreach (string nomeAntigo in nomesParentsAntigos)
            {
                Transform tAntigo = EncontrarFilhoRecursivo(raiz.transform, nomeAntigo);
                if (tAntigo != null && tAntigo.childCount == 0)
                {
                    Object.DestroyImmediate(tAntigo.gameObject);
                }
            }

            PrefabUtility.SaveAsPrefabAsset(raiz, caminhoPrefab);
            UnityEngine.Debug.Log($"[ExoTao IC] Prefab '{caminhoPrefab}' atualizado com Y Bot 3D, Exoesqueleto ExoTao e conexões de ossos no ReceptorUDP!");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    private static Transform EncontrarFilhoRecursivo(Transform pai, string nomeExato)
    {
        if (pai == null) return null;
        if (pai.name == nomeExato) return pai;
        for (int i = 0; i < pai.childCount; i++)
        {
            Transform encontrado = EncontrarFilhoRecursivo(pai.GetChild(i), nomeExato);
            if (encontrado != null) return encontrado;
        }
        return null;
    }

    [MenuItem("ExoTao IC/2. Fixar Drone Guia IA e Telemetria na Cena Atual", false, 10)]
    public static void InserirSistemasNaCenaAtual()
    {
        ReceptorUDP receptor = Object.FindFirstObjectByType<ReceptorUDP>();
        CharacterController controle = Object.FindFirstObjectByType<CharacterController>();

        TelemetriaClinica telemetria = Object.FindFirstObjectByType<TelemetriaClinica>();
        if (telemetria == null)
        {
            GameObject goTel = new GameObject("[IC] Telemetria_Clinica_ReRobLab");
            Undo.RegisterCreatedObjectUndo(goTel, "Criar Telemetria Clinica");
            telemetria = goTel.AddComponent<TelemetriaClinica>();
        }
        telemetria.receptorExo = receptor;
        telemetria.controlePaciente = controle;

        DroneGuiaIA drone = Object.FindFirstObjectByType<DroneGuiaIA>();
        if (drone == null)
        {
            GameObject goDrone = new GameObject("[IC] Drone_Guia_IA_ExoBot");
            Undo.RegisterCreatedObjectUndo(goDrone, "Criar Drone Guia IA");
            drone = goDrone.AddComponent<DroneGuiaIA>();
        }
        drone.receptorExo = receptor;
        drone.telemetria = telemetria;

        Selection.activeGameObject = drone.gameObject;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        UnityEngine.Debug.Log("[ExoTao IC] Drone Guia IA e Telemetria Clínica adicionados e selecionados na Hierarchy!");
    }

    [MenuItem("ExoTao IC/3. Abrir Pasta de Relatórios Clínicos (CSV e Resumos)", false, 25)]
    public static void AbrirPastaRelatorios()
    {
        DirectoryInfo raiz = Directory.GetParent(Application.dataPath);
        string pasta = Path.Combine(raiz != null ? raiz.FullName : Application.persistentDataPath, "Relatorios_Clinicos");
        if (!Directory.Exists(pasta))
            Directory.CreateDirectory(pasta);

        EditorUtility.RevealInFinder(pasta);
    }

    [MenuItem("ExoTao IC/4. Iniciar Simulador Python do Exoesqueleto (UDP 5005)", false, 40)]
    public static void IniciarSimuladorPython()
    {
        DirectoryInfo raiz = Directory.GetParent(Application.dataPath);
        if (raiz == null) return;

        string scriptSim = Path.Combine(raiz.FullName, "simulador_robo.py");
        if (!File.Exists(scriptSim))
        {
            EditorUtility.DisplayDialog("Simulador não encontrado", $"Arquivo não encontrado:\n{scriptSim}", "OK");
            return;
        }

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c start \"Simulador ExoTao UDP\" python \"{scriptSim}\"",
            WorkingDirectory = raiz.FullName,
            UseShellExecute = true
        };
        Process.Start(psi);
    }

    [MenuItem("ExoTao IC/5. Gerar Gráficos Científicos da Última Sessão (Python)", false, 41)]
    public static void GerarGraficosPython()
    {
        DirectoryInfo raiz = Directory.GetParent(Application.dataPath);
        if (raiz == null) return;

        string scriptGraficos = Path.Combine(raiz.FullName, "analisador_telemetria_ic.py");
        if (!File.Exists(scriptGraficos))
        {
            EditorUtility.DisplayDialog("Analisador não encontrado", $"Arquivo não encontrado:\n{scriptGraficos}", "OK");
            return;
        }

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c start \"Analisador Biomecanico IC\" cmd /k python \"{scriptGraficos}\"",
            WorkingDirectory = raiz.FullName,
            UseShellExecute = true
        };
        Process.Start(psi);
    }
}
