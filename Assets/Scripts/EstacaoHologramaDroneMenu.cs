using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;

/// <summary>
/// Estação Sci-Fi de Exibição e Holograma do Drone Guia IA ("EXO-BOT") para o Menu Principal.
/// Constrói um console/pedestal futurista 3D com projetor holográfico, modelo 3D animado do Drone
/// e um Display Wall multi-telas (inspirado em interfaces Sci-Fi de telemetria médica) explicando
/// todas as funções clínicas do mascote e exibindo telemetria ao vivo do exoesqueleto.
/// </summary>
[ExecuteAlways]
public class EstacaoHologramaDroneMenu : MonoBehaviour
{
    [Header("Referências Automáticas")]
    public ReceptorUDP receptorExo;
    public Transform droneHologramaRaiz;
    public Transform anelExternoDrone;
    public Transform anelInternoDrone;
    public Transform anelProjetorBase;
    public Light luzHolograma;
    public TextMeshProUGUI txtTelemetriaAoVivo;
    public TextMeshProUGUI txtEstadoDemonstracao;
    public Image imgIndicadorEstado;

    private Material matGlowDinamico;
    private float timerCicloDemo = 0f;
    private int indiceEstadoDemo = 0;

    private readonly Color[] coresEstados = new Color[]
    {
        new Color(0.00f, 0.96f, 0.85f), // 0: Ciano Biomédico (Guiando Marcha)
        new Color(0.22f, 0.72f, 1.00f), // 1: Azul Calmo (Aguardando Paciente)
        new Color(1.00f, 0.68f, 0.05f), // 2: Âmbar Clínico (Alerta de Simetria)
        new Color(0.35f, 1.00f, 0.35f)  // 3: Verde Energia (Celebrando Meta)
    };

    private readonly string[] nomesEstados = new string[]
    {
        "MODO ATIVO: GUIANDO MARCHA (SIMETRIA IDEAL > 80%)",
        "MODO ATIVO: AGUARDANDO INÍCIO DA PASSADA DO PACIENTE",
        "MODO ATIVO: ALERTA DE BIOFEEDBACK (AJUSTAR AMPLITUDE D/E)",
        "MODO ATIVO: CELEBRAÇÃO DE META CLÍNICA (15 PASSOS!)"
    };

    void OnEnable()
    {
        ConstruirEstacaoCompletaSeNecessario();
    }

    void Start()
    {
        ConstruirEstacaoCompletaSeNecessario();
    }

    public void ConstruirEstacaoCompletaSeNecessario()
    {
        if (receptorExo == null)
            receptorExo = FindFirstObjectByType<ReceptorUDP>();

        Shader shaderAtivo = ObterShaderCompativel();

        Material matTitanioEscuro = ObterOuCriarMaterial(shaderAtivo, "SciFi_Console_Titanium", new Color(0.11f, 0.14f, 0.18f), 0.78f, 0.72f, false);
        Material matAcoMedio      = ObterOuCriarMaterial(shaderAtivo, "SciFi_Console_Steel", new Color(0.20f, 0.25f, 0.31f), 0.65f, 0.68f, false);
        Material matBrancoMedico  = ObterOuCriarMaterial(shaderAtivo, "ExoBot_Body_Mat", new Color(0.92f, 0.96f, 1.00f), 0.25f, 0.86f, false);
        Material matCyanNeon      = ObterOuCriarMaterial(shaderAtivo, "SciFi_Console_CyanGlow", new Color(0.00f, 0.90f, 1.00f), 0.20f, 0.90f, true);
        Material matAmberWarn     = ObterOuCriarMaterial(shaderAtivo, "SciFi_Console_AmberGlow", new Color(1.00f, 0.45f, 0.05f), 0.20f, 0.85f, true);
        Material matFeixeHolo     = ObterOuCriarMaterial(shaderAtivo, "SciFi_Hologram_Beam", new Color(0.00f, 0.82f, 1.00f), 0.10f, 0.95f, true);

        matGlowDinamico = Application.isPlaying ? new Material(matCyanNeon) : matCyanNeon;

        // 1. Constrói o Pedestal / Console Holográfico 3D (se ainda não existir)
        ConstruirConsole3D(matTitanioEscuro, matAcoMedio, matCyanNeon, matAmberWarn, matFeixeHolo);

        // 2. Constrói o Drone 3D Flutuante no Holograma (se ainda não existir)
        ConstruirDroneShowcase3D(matBrancoMedico, matTitanioEscuro, matGlowDinamico);

        // 3. Constrói o Display Wall Sci-Fi Multi-Telas (Canvas WorldSpace)
        ConstruirDisplayWallSciFi();
    }

    private void ConstruirConsole3D(Material matTitanio, Material matAco, Material matCyan, Material matAmber, Material matFeixe)
    {
        Transform consoleRaiz = transform.Find("Console_Pedestal_3D");
        if (consoleRaiz != null && consoleRaiz.childCount > 0)
        {
            anelProjetorBase = consoleRaiz.Find("Anel_Projetor_Giratorio");
            Transform tLuz = consoleRaiz.Find("Luz_Holografica_Console");
            if (tLuz != null) luzHolograma = tLuz.GetComponent<Light>();
            return;
        }

        if (consoleRaiz == null)
        {
            consoleRaiz = new GameObject("Console_Pedestal_3D").transform;
            consoleRaiz.SetParent(transform, false);
        }

        // Base Octogonal / Plataforma no piso
        CriarPrimitiva(consoleRaiz, "Plataforma_Base_Solo", PrimitiveType.Cylinder,
            new Vector3(0f, 0.04f, 0f), Vector3.zero, new Vector3(1.55f, 0.04f, 1.35f), matTitanio);
        CriarPrimitiva(consoleRaiz, "Anel_LED_Solo", PrimitiveType.Cylinder,
            new Vector3(0f, 0.082f, 0f), Vector3.zero, new Vector3(1.42f, 0.008f, 1.24f), matCyan);

        // Corpo Central do Pedestal Holográfico (inspirado na mesa de holograma Sci-Fi)
        CriarPrimitiva(consoleRaiz, "Corpo_Pedestal_Principal", PrimitiveType.Cube,
            new Vector3(0f, 0.44f, -0.02f), Vector3.zero, new Vector3(1.18f, 0.72f, 0.78f), matTitanio);

        // Chanfros Laterais Tecnológicos com Acentos Âmbar/Vermelho
        CriarPrimitiva(consoleRaiz, "Bloco_Lateral_Esq", PrimitiveType.Cube,
            new Vector3(-0.62f, 0.42f, -0.02f), new Vector3(0f, 0f, 8f), new Vector3(0.14f, 0.66f, 0.68f), matAco);
        CriarPrimitiva(consoleRaiz, "Bloco_Lateral_Dir", PrimitiveType.Cube,
            new Vector3(0.62f, 0.42f, -0.02f), new Vector3(0f, 0f, -8f), new Vector3(0.14f, 0.66f, 0.68f), matAco);

        CriarPrimitiva(consoleRaiz, "Alerta_LED_Esq", PrimitiveType.Cube,
            new Vector3(-0.65f, 0.72f, -0.28f), new Vector3(0f, 0f, 8f), new Vector3(0.10f, 0.03f, 0.14f), matAmber);
        CriarPrimitiva(consoleRaiz, "Alerta_LED_Dir", PrimitiveType.Cube,
            new Vector3(0.65f, 0.72f, -0.28f), new Vector3(0f, 0f, -8f), new Vector3(0.10f, 0.03f, 0.14f), matAmber);

        // Faixas Emissivas Ciano Frontais no Pedestal
        CriarPrimitiva(consoleRaiz, "Faixa_Neon_Frontal_Sup", PrimitiveType.Cube,
            new Vector3(0f, 0.74f, -0.415f), Vector3.zero, new Vector3(1.05f, 0.018f, 0.02f), matCyan);
        CriarPrimitiva(consoleRaiz, "Faixa_Neon_Frontal_Inf", PrimitiveType.Cube,
            new Vector3(0f, 0.20f, -0.415f), Vector3.zero, new Vector3(0.95f, 0.018f, 0.02f), matCyan);

        // Deck de Controle Inclinado Frontal (estilo Programming Board)
        CriarPrimitiva(consoleRaiz, "Deck_Controle_Frontal", PrimitiveType.Cube,
            new Vector3(0f, 0.80f, -0.44f), new Vector3(-28f, 0f, 0f), new Vector3(0.96f, 0.06f, 0.34f), matAco);
        CriarPrimitiva(consoleRaiz, "Tela_Touch_Deck", PrimitiveType.Cube,
            new Vector3(0f, 0.825f, -0.445f), new Vector3(-28f, 0f, 0f), new Vector3(0.56f, 0.02f, 0.24f), matCyan);

        // Joysticks / Dials Sci-Fi no Deck
        CriarPrimitiva(consoleRaiz, "Dial_Controle_Esq", PrimitiveType.Cylinder,
            new Vector3(-0.36f, 0.83f, -0.44f), new Vector3(-28f, 0f, 0f), new Vector3(0.10f, 0.025f, 0.10f), matTitanio);
        CriarPrimitiva(consoleRaiz, "Joystick_Dir", PrimitiveType.Cylinder,
            new Vector3(0.36f, 0.86f, -0.44f), new Vector3(-28f, 0f, 0f), new Vector3(0.03f, 0.06f, 0.03f), matAmber);

        // Emissor Circular de Holograma no Topo da Mesa (y = 0.82m) - Limpo e estático (sem cubos girando)
        CriarPrimitiva(consoleRaiz, "Base_Projetor_Holograma", PrimitiveType.Cylinder,
            new Vector3(0f, 0.82f, -0.04f), Vector3.zero, new Vector3(0.68f, 0.03f, 0.68f), matAco);
        CriarPrimitiva(consoleRaiz, "Lente_Emissora_Central", PrimitiveType.Cylinder,
            new Vector3(0f, 0.852f, -0.04f), Vector3.zero, new Vector3(0.52f, 0.008f, 0.52f), matCyan);

        // Pilares Traseiros de Sustentação do Display Wall (estilo terminal Sierra Division)
        CriarPrimitiva(consoleRaiz, "Pilar_Traseiro_Esq", PrimitiveType.Cube,
            new Vector3(-0.55f, 1.35f, 0.34f), Vector3.zero, new Vector3(0.11f, 2.50f, 0.12f), matTitanio);
        CriarPrimitiva(consoleRaiz, "Pilar_Traseiro_Dir", PrimitiveType.Cube,
            new Vector3(0.55f, 1.35f, 0.34f), Vector3.zero, new Vector3(0.11f, 2.50f, 0.12f), matTitanio);
        CriarPrimitiva(consoleRaiz, "LED_Pilar_Esq", PrimitiveType.Cube,
            new Vector3(-0.55f, 1.35f, 0.275f), Vector3.zero, new Vector3(0.025f, 2.35f, 0.015f), matCyan);
        CriarPrimitiva(consoleRaiz, "LED_Pilar_Dir", PrimitiveType.Cube,
            new Vector3(0.55f, 1.35f, 0.275f), Vector3.zero, new Vector3(0.025f, 2.35f, 0.015f), matCyan);

        // Luz Pontual Ciano Iluminando o Holograma
        GameObject goLuz = new GameObject("Luz_Holografica_Console");
        goLuz.transform.SetParent(consoleRaiz, false);
        goLuz.transform.localPosition = new Vector3(0f, 1.15f, -0.25f);
        luzHolograma = goLuz.AddComponent<Light>();
        luzHolograma.type = LightType.Point;
        luzHolograma.range = 4.0f;
        luzHolograma.intensity = 1.8f;
        luzHolograma.color = coresEstados[0];
    }

    private void ConstruirDroneShowcase3D(Material matBranco, Material matEscuro, Material matGlow)
    {
        // Remove antigo Anel_Projetor_Giratorio se existia no console
        Transform consoleRaiz = transform.Find("Console_Pedestal_3D");
        if (consoleRaiz != null)
        {
            Transform antigoAnelProj = consoleRaiz.Find("Anel_Projetor_Giratorio");
            if (antigoAnelProj != null)
            {
                if (Application.isPlaying) Destroy(antigoAnelProj.gameObject);
                else DestroyImmediate(antigoAnelProj.gameObject);
            }
        }

        droneHologramaRaiz = transform.Find("Drone_ExoBot_Holograma_3D");
        if (droneHologramaRaiz != null)
        {
            // Se ainda tinha filhos antigos (cilindros/anéis segmentados), limpa todos para usar a malha única do Blueprint
            for (int i = droneHologramaRaiz.childCount - 1; i >= 0; i--)
            {
                GameObject filho = droneHologramaRaiz.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(filho);
                else DestroyImmediate(filho);
            }
        }
        else
        {
            droneHologramaRaiz = new GameObject("Drone_ExoBot_Holograma_3D").transform;
            droneHologramaRaiz.SetParent(transform, false);
        }

        // Flutua acima do emissor circular (y = 1.18m, z = -0.06m, olhando para -Z que é a direção do jogador)
        droneHologramaRaiz.localPosition = new Vector3(0f, 1.18f, -0.06f);
        droneHologramaRaiz.localRotation = Quaternion.Euler(0f, 180f, 0f);
        droneHologramaRaiz.localScale = Vector3.one * 1.05f;

        // Carrega a malha 3D unificada gerada no Blender 5.1 (fiel ao Blueprint CAD com dois aros toroidais laterais contínuos)
        Mesh malhaBlueprint = null;
        GameObject fbxDrone = Resources.Load<GameObject>("Models/ExoBot_Blueprint_Drone");
#if UNITY_EDITOR
        if (fbxDrone == null)
        {
            fbxDrone = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Models/ExoBot_Blueprint_Drone.fbx");
        }
#endif
        if (fbxDrone != null)
        {
            MeshFilter mfFbx = fbxDrone.GetComponentInChildren<MeshFilter>();
            if (mfFbx != null) malhaBlueprint = mfFbx.sharedMesh;
        }
        if (malhaBlueprint == null)
        {
            malhaBlueprint = GeradorMalhaDroneBlueprint.ConstruirMalhaDroneCAD();
        }

        Shader shaderAtivo = ObterShaderCompativel();
        // Material de blindagem azul-ciano holográfico escuro igual à imagem do blueprint
        Material matHologramaChassis = ObterOuCriarMaterial(shaderAtivo, "SciFi_Drone_Hologram_Armor",
            new Color(0.07f, 0.25f, 0.38f), 0.78f, 0.86f, false);

        MeshFilter mf = droneHologramaRaiz.GetComponent<MeshFilter>();
        if (mf == null) mf = droneHologramaRaiz.gameObject.AddComponent<MeshFilter>();
        mf.sharedMesh = malhaBlueprint;

        MeshRenderer mr = droneHologramaRaiz.GetComponent<MeshRenderer>();
        if (mr == null) mr = droneHologramaRaiz.gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterials = new Material[] { matHologramaChassis, matEscuro, matGlow };
    }

    private void ConstruirDisplayWallSciFi()
    {
        Transform canvasExistente = transform.Find("Canvas_Display_Wall_SciFi");
        if (canvasExistente != null)
        {
            Object.DestroyImmediate(canvasExistente.gameObject);
        }

        GameObject goCanvas = new GameObject("Canvas_Display_Wall_SciFi");
        goCanvas.transform.SetParent(transform, false);
        // Posicionado acima/atrás do holograma (y = 1.92m, z = 0.26m) para nunca ser obstruído pelo drone 3D (y = 1.18m)
        goCanvas.transform.localPosition = new Vector3(0f, 1.92f, 0.26f);
        goCanvas.transform.localRotation = Quaternion.identity;
        goCanvas.transform.localScale = Vector3.one * 0.00125f;

        Canvas canvas = goCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.vertexColorAlwaysGammaSpace = true;
        if (Camera.main != null) canvas.worldCamera = Camera.main;

        CanvasScaler scaler = goCanvas.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 1f;
        scaler.referencePixelsPerUnit = 100f;

        goCanvas.AddComponent<GraphicRaycaster>();
        goCanvas.AddComponent<TrackedDeviceGraphicRaycaster>();

        RectTransform rtCanvas = goCanvas.GetComponent<RectTransform>();
        rtCanvas.sizeDelta = new Vector2(1380f, 820f);

        Sprite spriteMolduraHud = CarregarSprite("UI/SciFi_HUD_Frame", "Assets/Resources/UI/SciFi_HUD_Frame.jpg");
        Sprite spriteBlueprint  = CarregarSprite("UI/Drone_Holo_Blueprint", "Assets/Resources/UI/Drone_Holo_Blueprint.jpg");

        // =====================================================================
        // PAINEL CENTRAL PRINCIPAL (O que é o Drone EXO-BOT e Módulos Clínicos)
        // =====================================================================
        RectTransform painelCentral = CriarPainelHUD(goCanvas.transform, "Painel_Central_Info",
            Vector2.zero, new Vector2(820f, 780f), Vector3.zero, spriteMolduraHud);

        // Cabeçalho Sci-Fi (dentro da janela interna segura y: 0.17 .. 0.80)
        CriarTextoTMP(painelCentral, "Txt_Subtitulo_Lab",
            "REROBLAB • SISTEMA DE ASSISTÊNCIA ROBÓTICA & BIOFEEDBACK",
            new Vector2(0.12f, 0.755f), new Vector2(0.88f, 0.80f), 15.0f, new Color(0.0f, 0.92f, 1.0f), FontStyles.Bold, TextAlignmentOptions.Center);

        CriarTextoTMP(painelCentral, "Txt_Titulo_Drone",
            "DRONE COMPANHEIRO IA — EXO-BOT",
            new Vector2(0.11f, 0.675f), new Vector2(0.89f, 0.755f), 28.5f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);

        // Barra divisória luminosa
        CriarBarraCor(painelCentral, "Divisoria_Topo", new Vector2(0.13f, 0.660f), new Vector2(0.87f, 0.667f), new Color(0.0f, 0.88f, 1.0f, 0.85f));

        // Descrição das Funções do Mascote (Ajustado com folga vertical para nunca sobrepor a Caixa_Status_Demo abaixo!)
        string textoFuncoes =
            "<b><color=#00F0FF>1. GUIA DINÂMICO DE MARCHA (PACING ADAPTATIVO)</color></b>\n" +
            "Voa automaticamente a <b>2.7m</b> à frente do paciente em todos os mapas, ajustando sua velocidade e altura ao ritmo real da caminhada no exoesqueleto.\n\n" +
            "<b><color=#00F0FF>2. BIOFEEDBACK DE SIMETRIA EM TEMPO REAL</color></b>\n" +
            "Monitora via UDP/TCP os ângulos de <b>Quadril e Joelho (Dir/Esq)</b>, calculando o Índice de Simetria (%) e orientando qual perna precisa de maior flexão.\n\n" +
            "<b><color=#00F0FF>3. GAMIFICAÇÃO & TELEMETRIA CLÍNICA (CSV)</color></b>\n" +
            "Exibe um HUD holográfico com Passos, Distância e Cadência (SPM), celebrando metas a cada <b>15 passos</b> e gravando o relatório clínico completo.";

        TextMeshProUGUI tmpCorpo = CriarTextoTMP(painelCentral, "Txt_Corpo_Funcoes",
            textoFuncoes,
            new Vector2(0.12f, 0.305f), new Vector2(0.88f, 0.648f), 15.6f, new Color(0.92f, 0.97f, 1.0f), FontStyles.Normal, TextAlignmentOptions.TopLeft);
        tmpCorpo.overflowMode = TextOverflowModes.Truncate;

        // Barra Inferior de Status ao Vivo da Demonstração
        GameObject goCaixaStatus = CriarBarraCor(painelCentral, "Caixa_Status_Demo",
            new Vector2(0.12f, 0.175f), new Vector2(0.88f, 0.280f), new Color(0.02f, 0.14f, 0.24f, 0.95f));

        GameObject goIndicador = CriarBarraCor(goCaixaStatus.transform, "Indicador_Cor_Estado",
            new Vector2(0.015f, 0.15f), new Vector2(0.042f, 0.85f), coresEstados[0]);
        imgIndicadorEstado = goIndicador.GetComponent<Image>();

        txtEstadoDemonstracao = CriarTextoTMP(goCaixaStatus.transform, "Txt_Estado_Demo",
            nomesEstados[0],
            new Vector2(0.06f, 0.52f), new Vector2(0.98f, 0.92f), 16.0f, new Color(0.4f, 1.0f, 0.95f), FontStyles.Bold, TextAlignmentOptions.Left);

        txtTelemetriaAoVivo = CriarTextoTMP(goCaixaStatus.transform, "Txt_Telemetria_Live",
            "TELEMETRIA EXOTAO (PORTA 5005): Aguardando pacotes UDP/TCP ou Simulador [Tecla F9]",
            new Vector2(0.06f, 0.08f), new Vector2(0.98f, 0.48f), 14.2f, new Color(0.80f, 0.92f, 1.0f), FontStyles.Normal, TextAlignmentOptions.Left);

        // =====================================================================
        // PAINEL LATERAL ESQUERDO (Esquemático CAD / Blueprint Holográfico)
        // =====================================================================
        RectTransform painelEsq = CriarPainelHUD(goCanvas.transform, "Painel_Esquerdo_Blueprint",
            new Vector2(-690f, -10f), new Vector2(490f, 700f), new Vector3(0f, -18f, 0f), spriteMolduraHud);
        painelEsq.localPosition = new Vector3(-680f, -10f, -85f);

        CriarTextoTMP(painelEsq, "Txt_Titulo_CAD",
            "ESQUEMÁTICO HOLOGRÁFICO CAD",
            new Vector2(0.13f, 0.72f), new Vector2(0.87f, 0.79f), 19.5f, new Color(0.0f, 0.95f, 1.0f), FontStyles.Bold, TextAlignmentOptions.Center);

        // Imagem do Blueprint Gerada por IA
        GameObject goImgCad = new GameObject("Imagem_Blueprint_Drone");
        goImgCad.transform.SetParent(painelEsq, false);
        RectTransform rtCad = goImgCad.AddComponent<RectTransform>();
        rtCad.anchorMin = new Vector2(0.14f, 0.34f);
        rtCad.anchorMax = new Vector2(0.86f, 0.71f);
        rtCad.offsetMin = Vector2.zero;
        rtCad.offsetMax = Vector2.zero;
        Image imgCad = goImgCad.AddComponent<Image>();
        if (spriteBlueprint != null)
        {
            imgCad.sprite = spriteBlueprint;
            imgCad.preserveAspect = true;
            imgCad.color = Color.white;
        }
        else
        {
            imgCad.color = new Color(0.0f, 0.5f, 0.8f, 0.5f);
        }

        CriarTextoTMP(painelEsq, "Txt_Specs_Drone",
            "• <b>CHASSIS:</b> Esfera Biomédica Giroscópica\n" +
            "• <b>SENSORES:</b> Óptica de Rastreamento VR\n" +
            "• <b>REDE:</b> UDP 5005 / TCP 1515 (60 Hz)\n" +
            "• <b>ATUAÇÃO:</b> Presente em todos os Mapas",
            new Vector2(0.14f, 0.18f), new Vector2(0.86f, 0.33f), 15.5f, new Color(0.88f, 0.96f, 1.0f), FontStyles.Normal, TextAlignmentOptions.Left);

        // =====================================================================
        // PAINEL LATERAL DIREITO (Legenda de Estados Clínicos & Cores LED)
        // =====================================================================
        RectTransform painelDir = CriarPainelHUD(goCanvas.transform, "Painel_Direito_Estados",
            new Vector2(690f, -10f), new Vector2(490f, 700f), new Vector3(0f, 18f, 0f), spriteMolduraHud);
        painelDir.localPosition = new Vector3(680f, -10f, -85f);

        CriarTextoTMP(painelDir, "Txt_Titulo_Estados",
            "CÓDIGO DE CORES (BIOFEEDBACK)",
            new Vector2(0.13f, 0.72f), new Vector2(0.87f, 0.79f), 19.5f, new Color(0.0f, 0.95f, 1.0f), FontStyles.Bold, TextAlignmentOptions.Center);

        CriarCardEstadoCor(painelDir, 0, new Vector2(0.13f, 0.59f), new Vector2(0.87f, 0.70f),
            "CIANO — GUIANDO MARCHA",
            "Marcha fluida e simetria bilateral acima de 80%.");

        CriarCardEstadoCor(painelDir, 1, new Vector2(0.13f, 0.47f), new Vector2(0.87f, 0.58f),
            "AZUL — AGUARDANDO PACIENTE",
            "Flutua à frente esperando o paciente iniciar o passo.");

        CriarCardEstadoCor(painelDir, 2, new Vector2(0.13f, 0.35f), new Vector2(0.87f, 0.46f),
            "ÂMBAR — ALERTA DE SIMETRIA",
            "Assimetria (<76%): orienta qual perna flexionar mais.");

        CriarCardEstadoCor(painelDir, 3, new Vector2(0.13f, 0.23f), new Vector2(0.87f, 0.34f),
            "VERDE — META CELEBRADA!",
            "Pirueta de comemoração a cada 15 passos concluídos.");

        CriarTextoTMP(painelDir, "Txt_Dica_Interacao",
            "<color=#00F0FF>DICA:</color> Aperte <b>[F9]</b> em Play Mode para testar!",
            new Vector2(0.13f, 0.17f), new Vector2(0.87f, 0.22f), 15.0f, Color.white, FontStyles.Italic, TextAlignmentOptions.Center);
    }

    void Update()
    {
        float tempo = Application.isPlaying ? Time.time : (float)
#if UNITY_EDITOR
            UnityEditor.EditorApplication.timeSinceStartup;
#else
            0f;
#endif

        // 1. Animação Leve e Otimizada de Flutuação (1 único Transform, zero peças soltas)
        if (droneHologramaRaiz != null)
        {
            float bobY = Mathf.Sin(tempo * 1.6f) * 0.032f;
            droneHologramaRaiz.localPosition = new Vector3(0f, 1.18f + bobY, -0.06f);
            float yawSuave = 180f + Mathf.Sin(tempo * 0.55f) * 14f;
            droneHologramaRaiz.localRotation = Quaternion.Euler(0f, yawSuave, 0f);
        }

        // 2. Atualização de Biofeedback e Telemetria ao Vivo em Play Mode
        if (Application.isPlaying)
        {
            timerCicloDemo += Time.deltaTime;
            if (timerCicloDemo >= 4.0f)
            {
                timerCicloDemo = 0f;
                indiceEstadoDemo = (indiceEstadoDemo + 1) % coresEstados.Length;
            }

            // Se o paciente estiver caminhando (ex: F9 ou UDP ativo), destaca o modo Guiando Marcha
            bool caminhando = receptorExo != null && receptorExo.PacienteCaminhando;
            int estadoExibido = caminhando ? 0 : indiceEstadoDemo;
            Color corAtual = coresEstados[estadoExibido];

            if (luzHolograma != null) luzHolograma.color = corAtual;
            if (imgIndicadorEstado != null) imgIndicadorEstado.color = corAtual;
            if (txtEstadoDemonstracao != null)
            {
                txtEstadoDemonstracao.text = nomesEstados[estadoExibido];
                txtEstadoDemonstracao.color = corAtual;
            }

            if (matGlowDinamico != null)
            {
                matGlowDinamico.color = corAtual;
                if (matGlowDinamico.HasProperty("_BaseColor")) matGlowDinamico.SetColor("_BaseColor", corAtual);
                if (matGlowDinamico.HasProperty("_EmissionColor")) matGlowDinamico.SetColor("_EmissionColor", corAtual * 2.2f);
            }

            if (txtTelemetriaAoVivo != null && receptorExo != null)
            {
                string fonte = receptorExo.simuladorInternoAtivo ? "SIMULADOR BIOMECÂNICO [F9] ATIVO" : $"REDE ({receptorExo.FonteDeDadosAtiva})";
                txtTelemetriaAoVivo.text =
                    $"<b>FONTE:</b> {fonte}   |   <b>QUADRIL D/E:</b> {receptorExo.AnguloQuadrilDir:F1}° / {receptorExo.AnguloQuadrilEsq:F1}°   |   " +
                    $"<b>JOELHO D/E:</b> {receptorExo.AnguloJoelhoDir:F1}° / {receptorExo.AnguloJoelhoEsq:F1}°";
            }
        }
    }

    // =========================================================================
    // UTILITÁRIOS DE UI E PRIMITIVAS 3D
    // =========================================================================

    private RectTransform CriarPainelHUD(Transform pai, string nome, Vector2 posAncora, Vector2 tamanho, Vector3 rotEuler, Sprite spriteFundo)
    {
        GameObject go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = posAncora;
        rt.sizeDelta = tamanho;
        rt.localRotation = Quaternion.Euler(rotEuler);

        Image img = go.AddComponent<Image>();
        if (spriteFundo != null)
        {
            img.sprite = spriteFundo;
            img.color = new Color(1f, 1f, 1f, 0.96f);
        }
        else
        {
            img.color = new Color(0.03f, 0.08f, 0.15f, 0.94f);
        }

        return rt;
    }

    private void CriarCardEstadoCor(Transform pai, int indiceCor, Vector2 anchorMin, Vector2 anchorMax, string titulo, string descricao)
    {
        Color cor = coresEstados[indiceCor];
        GameObject card = CriarBarraCor(pai, $"Card_Estado_{indiceCor}", anchorMin, anchorMax, new Color(0.03f, 0.11f, 0.19f, 0.90f));

        CriarBarraCor(card.transform, "Barra_LED", new Vector2(0.0f, 0.0f), new Vector2(0.035f, 1.0f), cor);

        CriarTextoTMP(card.transform, "Titulo", titulo,
            new Vector2(0.06f, 0.56f), new Vector2(0.97f, 0.94f), 17.5f, cor, FontStyles.Bold, TextAlignmentOptions.Left);

        CriarTextoTMP(card.transform, "Desc", descricao,
            new Vector2(0.06f, 0.06f), new Vector2(0.97f, 0.56f), 14.5f, new Color(0.88f, 0.94f, 1.0f), FontStyles.Normal, TextAlignmentOptions.Left);
    }

    private GameObject CriarBarraCor(Transform pai, string nome, Vector2 anchorMin, Vector2 anchorMax, Color cor)
    {
        GameObject go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image img = go.AddComponent<Image>();
        img.color = cor;
        return go;
    }

    private TextMeshProUGUI CriarTextoTMP(Transform pai, string nome, string conteudo, Vector2 anchorMin, Vector2 anchorMax, float tamanhoFonte, Color cor, FontStyles estilo, TextAlignmentOptions alinhamento)
    {
        GameObject go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = conteudo;
        tmp.fontSize = tamanhoFonte;
        tmp.color = cor;
        tmp.fontStyle = estilo;
        tmp.alignment = alinhamento;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    private GameObject CriarPrimitiva(Transform pai, string nome, PrimitiveType tipo, Vector3 posLocal, Vector3 rotEuler, Vector3 escalaLocal, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(tipo);
        go.name = nome;
        go.transform.SetParent(pai, false);
        go.transform.localPosition = posLocal;
        go.transform.localRotation = Quaternion.Euler(rotEuler);
        go.transform.localScale = escalaLocal;

        Collider col = go.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }

        Renderer r = go.GetComponent<Renderer>();
        if (r != null && mat != null)
        {
            r.sharedMaterial = mat;
        }
        return go;
    }

    private Sprite CarregarSprite(string caminhoResources, string caminhoAssetEditor)
    {
        Sprite s = Resources.Load<Sprite>(caminhoResources);
#if UNITY_EDITOR
        if (s == null)
        {
            s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(caminhoAssetEditor);
        }
#endif
        return s;
    }

    private Shader ObterShaderCompativel()
    {
        bool usandoSRP = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
        if (usandoSRP)
        {
            Shader urp = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit");
            if (urp != null) return urp;
        }
        return Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
    }

    private Material ObterOuCriarMaterial(Shader shader, string nome, Color cor, float metallic, float smoothness, bool emissivo)
    {
        Material existente = Resources.Load<Material>("Materials/" + nome);
        if (existente != null)
        {
            if (shader != null && existente.shader != shader)
            {
                existente.shader = shader;
            }
            return existente;
        }

        Material m = new Material(shader);
        m.name = nome;
        m.color = cor;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", cor);
        if (m.HasProperty("_Color")) m.SetColor("_Color", cor);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);

        if (emissivo)
        {
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor"))
                m.SetColor("_EmissionColor", cor * 2.2f);
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources"))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources/Materials"))
                UnityEditor.AssetDatabase.CreateFolder("Assets/Resources", "Materials");
            string path = $"Assets/Resources/Materials/{nome}.mat";
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path) == null)
                UnityEditor.AssetDatabase.CreateAsset(m, path);
        }
#endif
        return m;
    }
}
