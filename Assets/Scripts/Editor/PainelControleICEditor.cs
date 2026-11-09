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

    [MenuItem("ExoTao IC/2. Configurar TODAS as Cenas (Y Bot + Drone Sci-Fi + Corrigir Rosa)", false, 8)]
    public static void ConfigurarTodasAsCenasEArrumarMateriais()
    {
        // 1. Configura os Sprites UI gerados (SciFi_HUD_Frame e Drone_Holo_Blueprint) e Importers de Áudio 3D
        ConfigurarSpritesUISciFi();
        ConfigurarImportersDeAudio3D();

        // 2. Corrige materiais globais e o prefab do HTC Vive Controller
        Material matVive = ObterOuCriarMaterialViveController();
        CorrigirPrefabHTCViveController(matVive);
        ConverterMateriaisURPParaStandardNoProjeto();

        // 3. Atualiza os 3 prefabs de jogador
        VerificarEConfigurarAvatar3DAutomatico(forcarRebuildPrefabs: true);

        // 4. Percorre todas as cenas do projeto em Assets/Scenes
        string[] guidsCenas = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        int cenasProcessadas = 0;

        foreach (string guid in guidsCenas)
        {
            string caminhoCena = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(caminhoCena) || !caminhoCena.EndsWith(".unity"))
                continue;

            var cena = EditorSceneManager.OpenScene(caminhoCena, OpenSceneMode.Single);
            if (!cena.IsValid()) continue;

            // a) Corrige Canvases VR
            AutoCorrigirAvisosCanvas();

            // b) Corrige qualquer Renderer sem material (ex: HTC Vive Controller 3D) ou com shader rosa
            CorrigirRenderersNaCenaAtual(matVive);

            // c) Configura Y Bot + Exoesqueleto + ReceptorUDP no jogador da cena
            ReceptorUDP receptor = null;
            CharacterController cc = Object.FindFirstObjectByType<CharacterController>();
            if (cc != null)
            {
                GameObject goJogador = cc.gameObject;
                receptor = goJogador.GetComponent<ReceptorUDP>();
                if (receptor == null)
                    receptor = goJogador.AddComponent<ReceptorUDP>();
                receptor.controlePaciente = cc;

                AvatarBiomecanicoVR avatar = goJogador.GetComponent<AvatarBiomecanicoVR>();
                if (avatar == null)
                    avatar = goJogador.AddComponent<AvatarBiomecanicoVR>();

                avatar.receptorExo = receptor;
                avatar.InicializarAvatarECinematica();
            }

            // d) No Menu Principal: constrói a Estação Holográfica Sci-Fi do Drone
            if (cena.name == "Menu_Principal")
            {
                ConfigurarEstacaoHologramaNoMenu(receptor);
            }
            // e) Nos Mapas de Reabilitação: adiciona e inicializa o Drone Guia IA 3D + Telemetria na cena
            else if (cena.name != "CenaDeLoading")
            {
                InserirSistemasNaCenaAtualSilencioso(receptor, cc);
            }

            // f) Em TODAS as cenas: garante o Sistema Central de Áudio (Ambientação + SFX UI + Voz Pet do Drone)
            InserirGerenciadorAudioNaCenaAtual(cena.name);

            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            cenasProcessadas++;
            UnityEngine.Debug.Log($"[ExoTao IC] Cena '{cena.name}' atualizada e salva com sucesso (Y Bot + Materiais + Drone IA)!");
        }

        // Reabre o Menu_Principal ao final
        if (File.Exists("Assets/Scenes/Menu_Principal.unity"))
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Menu_Principal.unity", OpenSceneMode.Single);
        }

        AssetDatabase.SaveAssets();
        SceneView.RepaintAll();
        UnityEngine.Debug.Log($"[ExoTao IC] TODAS as {cenasProcessadas} cenas foram configuradas com Y Bot 3D, controles HTC Vive corrigidos e Estação Sci-Fi do Drone!");
    }

    private static void ConfigurarSpritesUISciFi()
    {
        AssetDatabase.Refresh();
        string[] caminhosSprites = {
            "Assets/Resources/UI/SciFi_HUD_Frame.jpg",
            "Assets/Resources/UI/Drone_Holo_Blueprint.jpg"
        };

        foreach (string p in caminhosSprites)
        {
            if (!File.Exists(p)) continue;
            TextureImporter ti = AssetImporter.GetAtPath(p) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.Sprite)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
            }
        }
    }

    private static void ConfigurarImportersDeAudio3D()
    {
        string[] clipes3DMono = {
            "Assets/Resources/Audio/SFX_Drone_Hover_Loop.wav",
            "Assets/Resources/Audio/SFX_Drone_Pet_Greeting.wav",
            "Assets/Resources/Audio/SFX_Drone_Pet_Happy.wav",
            "Assets/Resources/Audio/SFX_Drone_Pet_Curious.wav",
            "Assets/Resources/Audio/SFX_Drone_Pet_Purr.wav"
        };

        foreach (string p in clipes3DMono)
        {
            if (!File.Exists(p)) continue;
            AudioImporter ai = AssetImporter.GetAtPath(p) as AudioImporter;
            if (ai != null && !ai.forceToMono)
            {
                ai.forceToMono = true;
                ai.SaveAndReimport();
            }
        }
    }

    private static void InserirGerenciadorAudioNaCenaAtual(string nomeCena)
    {
        GerenciadorAudioRehab audio = Object.FindFirstObjectByType<GerenciadorAudioRehab>();
        if (audio == null)
        {
            GameObject goAudio = new GameObject("[IC] Audio_Ambiente_E_SFX_ReRobLab");
            audio = goAudio.AddComponent<GerenciadorAudioRehab>();
        }
        audio.ConfigurarAudioParaCenaAtual(nomeCena);
        audio.ConectarTodosBotoesUIDaCena();
    }

    private static Material ObterOuCriarMaterialViveController()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Materials"))
            AssetDatabase.CreateFolder("Assets/Resources", "Materials");

        string matPath = "Assets/Resources/Materials/HTC_Vive_Controller_Mat.mat";
        Shader std = Shader.Find("Standard");
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(std);
            mat.name = "HTC_Vive_Controller_Mat";
            mat.color = new Color(0.16f, 0.18f, 0.22f, 1f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.65f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.72f);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        else if (std != null && mat.shader != std)
        {
            mat.shader = std;
            EditorUtility.SetDirty(mat);
        }
        return mat;
    }

    private static void CorrigirPrefabHTCViveController(Material matVive)
    {
        string prefabPath = "Assets/Prefabs/HTC Vive Controller.prefab";
        if (!File.Exists(prefabPath) || matVive == null) return;

        GameObject raiz = PrefabUtility.LoadPrefabContents(prefabPath);
        if (raiz == null) return;

        try
        {
            bool alterou = false;
            foreach (Renderer r in raiz.GetComponentsInChildren<Renderer>(true))
            {
                if (r.sharedMaterial == null || (r.sharedMaterial.shader != null && r.sharedMaterial.shader.name.Contains("Universal")))
                {
                    r.sharedMaterial = matVive;
                    alterou = true;
                }
            }
            if (alterou)
            {
                PrefabUtility.SaveAsPrefabAsset(raiz, prefabPath);
                UnityEngine.Debug.Log("[ExoTao IC] Prefab 'HTC Vive Controller.prefab' corrigido com material Standard escuro metálico!");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    private static void ConverterMateriaisURPParaStandardNoProjeto()
    {
        if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
            return;

        Shader std = Shader.Find("Standard");
        if (std == null) return;

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
        int convertidos = 0;

        foreach (string g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (p.Contains("Assets/Samples/Universal RP")) continue;

            Material m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null || m.shader == null) continue;

            string sn = m.shader.name;
            if (sn.Contains("Universal Render Pipeline") || sn.Contains("Error"))
            {
                Texture tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;
                Color cor = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : (m.HasProperty("_Color") ? m.color : Color.white);
                float metallic = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0.1f;
                float smooth = m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : 0.5f;

                m.shader = std;
                m.color = cor;
                if (tex != null) m.mainTexture = tex;
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);

                EditorUtility.SetDirty(m);
                convertidos++;
            }
        }

        if (convertidos > 0)
        {
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log($"[ExoTao IC] {convertidos} materiais URP/Error convertidos para Standard shader!");
        }
    }

    private static void CorrigirRenderersNaCenaAtual(Material matFallback)
    {
        Shader std = Shader.Find("Standard");
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            // Ignora ParticleSystemRenderer sem malha
            if (r is ParticleSystemRenderer) continue;

            Material[] mats = r.sharedMaterials;
            bool mudou = false;

            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null)
                {
                    mats[i] = matFallback;
                    mudou = true;
                }
                else if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null &&
                         mats[i].shader != null &&
                         (mats[i].shader.name.Contains("Universal") || mats[i].shader.name.Contains("Error")))
                {
                    Texture tex = mats[i].HasProperty("_BaseMap") ? mats[i].GetTexture("_BaseMap") : mats[i].mainTexture;
                    Color cor = mats[i].HasProperty("_BaseColor") ? mats[i].GetColor("_BaseColor") : (mats[i].HasProperty("_Color") ? mats[i].color : Color.white);
                    mats[i].shader = std;
                    mats[i].color = cor;
                    if (tex != null) mats[i].mainTexture = tex;
                    EditorUtility.SetDirty(mats[i]);
                    mudou = true;
                }
            }

            if (mudou)
            {
                r.sharedMaterials = mats;
            }
        }
    }

    private static void ConfigurarEstacaoHologramaNoMenu(ReceptorUDP receptor)
    {
        EstacaoHologramaDroneMenu estacaoExistente = Object.FindFirstObjectByType<EstacaoHologramaDroneMenu>(FindObjectsInactive.Include);
        GameObject goEstacao;

        if (estacaoExistente != null)
        {
            goEstacao = estacaoExistente.gameObject;
        }
        else
        {
            goEstacao = new GameObject("[IC] Estacao_Holograma_Drone_SciFi");
            estacaoExistente = goEstacao.AddComponent<EstacaoHologramaDroneMenu>();
        }

        // Posiciona à direita do jogador, em área ampla e livre da sala (X=7.05, Y=0.0, Z=1.85),
        // angulado em 75 graus para que a frente (-Z) olhe diretamente para o jogador em (4.50, 2.31)
        // sem encostar no painel de Mapas Exploráveis nem nas cadeiras ao fundo.
        goEstacao.transform.position = new Vector3(7.05f, 0.00f, 1.85f);
        goEstacao.transform.rotation = Quaternion.Euler(0f, 75f, 0f);
        goEstacao.transform.localScale = Vector3.one;

        estacaoExistente.receptorExo = receptor;
        estacaoExistente.ConstruirEstacaoCompletaSeNecessario();
    }

    private static void InserirSistemasNaCenaAtualSilencioso(ReceptorUDP receptor, CharacterController controle)
    {
        TelemetriaClinica telemetria = Object.FindFirstObjectByType<TelemetriaClinica>();
        if (telemetria == null)
        {
            GameObject goTel = new GameObject("[IC] Telemetria_Clinica_ReRobLab");
            telemetria = goTel.AddComponent<TelemetriaClinica>();
        }
        telemetria.receptorExo = receptor;
        telemetria.controlePaciente = controle;

        DroneGuiaIA drone = Object.FindFirstObjectByType<DroneGuiaIA>();
        if (drone == null)
        {
            GameObject goDrone = new GameObject("[IC] Drone_Guia_IA_ExoBot");
            drone = goDrone.AddComponent<DroneGuiaIA>();
        }
        drone.receptorExo = receptor;
        drone.telemetria = telemetria;
        drone.InicializarNoEditor();
    }

    [MenuItem("ExoTao IC/3. Fixar Drone Guia IA e Telemetria na Cena Atual", false, 10)]
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
