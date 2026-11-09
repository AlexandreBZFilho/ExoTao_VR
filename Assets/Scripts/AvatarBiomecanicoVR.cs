using UnityEngine;

/// <summary>
/// Sistema completo de Avatar 3D (Y Bot Mixamo) + Exoesqueleto Robótico ExoTao + Cinemática Biomecânica (FK/IK).
/// Conecta automaticamente os dados do ReceptorUDP (UDP 5005 / TCP 1515 / Simulador F9) aos ossos do Y Bot:
/// - Quadril Direito/Esquerdo (mixamorig:RightUpLeg / LeftUpLeg)
/// - Joelho Direito/Esquerdo (mixamorig:RightLeg / LeftLeg)
/// - Tornozelo Direito/Esquerdo (mixamorig:RightFoot / LeftFoot) com compensação fisiológica de marcha
/// - Pelve (mixamorig:Hips) com oscilação vertical e báscula pélvica natural
/// - Braços (mixamorig:LeftArm / RightArm / ForeArms) tirando do T-Pose e aplicando balanço contralateral
/// - Exoesqueleto ExoTao 3D acoplado diretamente às coxas, canelas, quadris, joelhos e pés do personagem.
/// </summary>
[ExecuteAlways]
public class AvatarBiomecanicoVR : MonoBehaviour
{
    [Header("Referências Principais")]
    public ReceptorUDP receptorExo;
    public Transform raizAvatarYBot;
    public Transform cameraVR;

    [Header("Ossos Principais do Y Bot (Mixamo)")]
    public Transform ossoPelve;           // mixamorig:Hips
    public Transform ossoColuna;          // mixamorig:Spine
    public Transform ossoTorax;           // mixamorig:Spine2
    public Transform ossoCabeca;          // mixamorig:Head

    [Header("Cadeia Cinemática - Perna Direita")]
    public Transform ossoQuadrilDir;      // mixamorig:RightUpLeg
    public Transform ossoJoelhoDir;       // mixamorig:RightLeg
    public Transform ossoTornozeloDir;    // mixamorig:RightFoot
    public Transform ossoDedosDir;        // mixamorig:RightToeBase

    [Header("Cadeia Cinemática - Perna Esquerda")]
    public Transform ossoQuadrilEsq;      // mixamorig:LeftUpLeg
    public Transform ossoJoelhoEsq;       // mixamorig:LeftLeg
    public Transform ossoTornozeloEsq;    // mixamorig:LeftFoot
    public Transform ossoDedosEsq;        // mixamorig:LeftToeBase

    [Header("Cadeia Cinemática - Membros Superiores")]
    public Transform ossoBracoDir;        // mixamorig:RightArm
    public Transform ossoAntebracoDir;    // mixamorig:RightForeArm
    public Transform ossoBracoEsq;        // mixamorig:LeftArm
    public Transform ossoAntebracoEsq;    // mixamorig:LeftForeArm

    [Header("Ajustes de Cinemática e VR")]
    [Tooltip("Suavização Slerp das rotações articulares para 90Hz VR")]
    public float suavizacaoCinematica = 18f;
    [Tooltip("Ângulo de relaxamento dos braços para sair do T-Pose (graus)")]
    public float anguloAbaixarBracos = 74f;
    [Tooltip("Ganho do balanço contralateral dos braços durante a caminhada")]
    public float ganhoBalancoBracos = 0.45f;
    [Tooltip("Acoplamento automático de dorsiflexão do tornozelo durante o passo")]
    public bool compensarTornozeloAutomatico = true;
    [Tooltip("Acoplar visual 3D do Exoesqueleto ExoTao nas pernas do Y Bot")]
    public bool exibirExoesqueleto3D = true;
    [Tooltip("Seguir a direção horizontal (Yaw) da câmera VR e ocultar cabeça em Play Mode para não tampar a visão")]
    public bool sincronizarComCameraVR = true;
    public Vector3 offsetCorpoCamera = new Vector3(0f, 0f, -0.13f);

    // Rotações originais de repouso (Rest Pose do Mixamo)
    private Quaternion rotRepousoPelve;
    private Quaternion rotRepousoQuadrilDir, rotRepousoJoelhoDir, rotRepousoTornozeloDir;
    private Quaternion rotRepousoQuadrilEsq, rotRepousoJoelhoEsq, rotRepousoTornozeloEsq;
    private Quaternion rotRepousoBracoDir, rotRepousoAntebracoDir;
    private Quaternion rotRepousoBracoEsq, rotRepousoAntebracoEsq;
    private Vector3 posRepousoPelve;
    private bool poseInicialCapturada = false;

    // Ângulos interpolados atuais
    private float quadDirAtual, joelhoDirAtual, tornDirAtual;
    private float quadEsqAtual, joelhoEsqAtual, tornEsqAtual;

    // LEDs dos motores do Exoesqueleto para biofeedback visual nas juntas
    private Renderer ledMotorQuadrilDir, ledMotorJoelhoDir;
    private Renderer ledMotorQuadrilEsq, ledMotorJoelhoEsq;
    private Material matLedAtivo, matLedRepouso;

    void Awake()
    {
        InicializarAvatarECinematica();
    }

    void OnEnable()
    {
        InicializarAvatarECinematica();
    }

    public void InicializarAvatarECinematica()
    {
        // 1. Localiza ou cria ReceptorUDP
        if (receptorExo == null)
        {
            receptorExo = GetComponent<ReceptorUDP>();
            if (receptorExo == null) receptorExo = GetComponentInChildren<ReceptorUDP>();
            if (receptorExo == null) receptorExo = GetComponentInParent<ReceptorUDP>();
        }

        // 2. Localiza a Main Camera do XR Origin
        if (cameraVR == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam == null) cam = Camera.main;
            if (cam != null) cameraVR = cam.transform;
        }

        // 3. Localiza ou instancia o Y Bot 3D
        LocalizarOuInstanciarYBot();

        if (raizAvatarYBot == null) return;

        // 4. Desativa Animator para que não trave o modelo em T-Pose nem conflite com os dados UDP
        Animator anim = raizAvatarYBot.GetComponent<Animator>();
        if (anim != null && anim.enabled)
        {
            anim.enabled = false;
        }

        // 5. Limpa hierarquia antiga quebrada (Rig_RightUpLeg_Parent etc.) se existir
        DesfazerParentsIntermediariosAntigos(raizAvatarYBot);

        // 6. Mapeia todos os ossos Mixamo do Y Bot
        MapearOssosMixamo(raizAvatarYBot);

        // 7. Garante materiais URP corretos na malha do Y Bot (Alpha_Surface / Alpha_Joints)
        AtualizarMateriaisURP(raizAvatarYBot);

        // 8. Captura a Pose Inicial (Rest Pose) exata do Mixamo e aplica postura relaxada inicial
        CapturarPoseDeRepouso();

        // 9. Constrói o Exoesqueleto Robótico ExoTao 3D acoplado aos ossos das pernas do Y Bot
        if (exibirExoesqueleto3D)
        {
            ConstruirExoesqueletoExoTao();
        }

        // 10. Sincroniza as referências públicas no ReceptorUDP para inspeção e telemetria
        if (receptorExo != null)
        {
            receptorExo.articulacaoQuadrilDir = ossoQuadrilDir;
            receptorExo.articulacaoJoelhoDir = ossoJoelhoDir;
            receptorExo.articulacaoQuadrilEsq = ossoQuadrilEsq;
            receptorExo.articulacaoJoelhoEsq = ossoJoelhoEsq;
            if (receptorExo.controlePaciente == null)
                receptorExo.controlePaciente = GetComponent<CharacterController>();
        }

        // Aplica a postura inicial imediatamente (tira do T-Pose mesmo no Editor!)
        AplicarCinematicaCompleta(0f, 0f, 0f, 0f, 1f);
    }

    private void LocalizarOuInstanciarYBot()
    {
        if (raizAvatarYBot != null) return;

        // Procura filho "Y Bot" ou que contenha "mixamorig:Hips"
        Transform hipsExistente = EncontrarFilhoRecursivo(transform, "mixamorig:Hips");
        if (hipsExistente != null)
        {
            raizAvatarYBot = hipsExistente.parent;
            return;
        }

        // Tenta carregar de Resources/YBot_Avatar
        GameObject prefabResource = Resources.Load<GameObject>("YBot_Avatar");
#if UNITY_EDITOR
        if (prefabResource == null)
        {
            prefabResource = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Y Bot.fbx");
        }
#endif
        if (prefabResource != null)
        {
            GameObject instancia = Instantiate(prefabResource, transform);
            instancia.name = "Y Bot";
            instancia.transform.localPosition = Vector3.zero;
            instancia.transform.localRotation = Quaternion.identity;
            instancia.transform.localScale = Vector3.one;
            raizAvatarYBot = instancia.transform;
        }
    }

    private void DesfazerParentsIntermediariosAntigos(Transform raiz)
    {
        string[] nomesParentsAntigos = {
            "Rig_RightUpLeg_Parent",
            "Rig_LeftUpLeg_Parent",
            "Rig_RightLeg_Parent",
            "Rig_LeftLeg_Parent"
        };

        foreach (string nomeParent in nomesParentsAntigos)
        {
            Transform parentAntigo = EncontrarFilhoRecursivo(raiz, nomeParent);
            if (parentAntigo != null && parentAntigo.parent != null)
            {
                Transform avo = parentAntigo.parent;
                // Move todos os filhos reais (ex: mixamorig:RightUpLeg) de volta para o osso pai original (ex: mixamorig:Hips)
                for (int i = parentAntigo.childCount - 1; i >= 0; i--)
                {
                    Transform filho = parentAntigo.GetChild(i);
                    Vector3 posMundo = filho.position;
                    Quaternion rotMundo = filho.rotation;
                    filho.SetParent(avo, true);
                    filho.position = posMundo;
                    filho.rotation = rotMundo;
                }
            }
        }
    }

    private void MapearOssosMixamo(Transform raiz)
    {
        ossoPelve        = EncontrarFilhoRecursivo(raiz, "mixamorig:Hips");
        ossoColuna       = EncontrarFilhoRecursivo(raiz, "mixamorig:Spine");
        ossoTorax        = EncontrarFilhoRecursivo(raiz, "mixamorig:Spine2");
        ossoCabeca       = EncontrarFilhoRecursivo(raiz, "mixamorig:Head");

        ossoQuadrilDir   = EncontrarFilhoRecursivo(raiz, "mixamorig:RightUpLeg");
        ossoJoelhoDir    = EncontrarFilhoRecursivo(raiz, "mixamorig:RightLeg");
        ossoTornozeloDir = EncontrarFilhoRecursivo(raiz, "mixamorig:RightFoot");
        ossoDedosDir     = EncontrarFilhoRecursivo(raiz, "mixamorig:RightToeBase");

        ossoQuadrilEsq   = EncontrarFilhoRecursivo(raiz, "mixamorig:LeftUpLeg");
        ossoJoelhoEsq    = EncontrarFilhoRecursivo(raiz, "mixamorig:LeftLeg");
        ossoTornozeloEsq = EncontrarFilhoRecursivo(raiz, "mixamorig:LeftFoot");
        ossoDedosEsq     = EncontrarFilhoRecursivo(raiz, "mixamorig:LeftToeBase");

        ossoBracoDir     = EncontrarFilhoRecursivo(raiz, "mixamorig:RightArm");
        ossoAntebracoDir = EncontrarFilhoRecursivo(raiz, "mixamorig:RightForeArm");
        ossoBracoEsq     = EncontrarFilhoRecursivo(raiz, "mixamorig:LeftArm");
        ossoAntebracoEsq = EncontrarFilhoRecursivo(raiz, "mixamorig:LeftForeArm");
    }

    private void CapturarPoseDeRepouso()
    {
        // Define as rotações de repouso anatômicas padrão do rig Mixamo Y Bot
        // Isso garante que mesmo que a cena seja salva após rodar, a referência de T-Pose nunca seja perdida!
        rotRepousoPelve          = Quaternion.identity;
        posRepousoPelve          = ossoPelve != null ? new Vector3(0f, 0.998f, 0f) : Vector3.up;

        // No Mixamo Y Bot, RightUpLeg e LeftUpLeg têm rotação de repouso de 180 graus em Z (apontando +Y local para baixo)
        rotRepousoQuadrilDir     = new Quaternion(0.0000024f, 0.0000022f, 0.9999954f, -0.0030580f);
        rotRepousoJoelhoDir      = new Quaternion(-0.0181471f, 0.0001825f, -0.0059202f, 0.9998178f);
        rotRepousoTornozeloDir   = new Quaternion(0.5405632f, 0.0143663f, 0.0249517f, 0.8408106f);

        rotRepousoQuadrilEsq     = new Quaternion(-0.0000203f, -0.0063399f, 0.9999755f, 0.0029698f);
        rotRepousoJoelhoEsq      = new Quaternion(-0.0181341f, -0.0001826f, 0.0059201f, 0.9998180f);
        rotRepousoTornozeloEsq   = new Quaternion(0.5405577f, -0.0143661f, -0.0249509f, 0.8408142f);

        rotRepousoBracoDir       = new Quaternion(-0.0104355f, -0.0010613f, 0.1011499f, 0.9948159f);
        rotRepousoAntebracoDir   = Quaternion.identity;

        rotRepousoBracoEsq       = new Quaternion(-0.0104708f, 0.0010649f, -0.1011494f, 0.9948156f);
        rotRepousoAntebracoEsq   = Quaternion.identity;

        poseInicialCapturada = true;
    }

    private Shader ObterShaderCompativelPipeline()
    {
        bool usandoSRP = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
        if (usandoSRP)
        {
            Shader urp = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit");
            if (urp != null) return urp;
        }
        return Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
    }

    private void AtualizarMateriaisURP(Transform raiz)
    {
        Shader shaderAtivo = ObterShaderCompativelPipeline();
        if (shaderAtivo == null) return;

        SkinnedMeshRenderer[] renderers = raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (SkinnedMeshRenderer smr in renderers)
        {
            // Permite que a malha continue visível mesmo quando o jogador olha para baixo de perto em VR
            smr.updateWhenOffscreen = true;

            if (smr.name.Contains("Joints"))
            {
                smr.sharedMaterial = CriarMaterial(shaderAtivo, "YBot_Joints_URP", new Color(0.14f, 0.18f, 0.22f, 1f), 0.6f, 0.5f);
            }
            else
            {
                smr.sharedMaterial = CriarMaterial(shaderAtivo, "YBot_Surface_URP", new Color(0.24f, 0.36f, 0.48f, 1f), 0.35f, 0.65f);
            }
        }
    }

    private void ConstruirExoesqueletoExoTao()
    {
        if (ossoPelve == null || ossoQuadrilDir == null || ossoJoelhoDir == null ||
            ossoQuadrilEsq == null || ossoJoelhoEsq == null)
            return;

        Shader shader = ObterShaderCompativelPipeline();
        Material matEstrutura = CriarMaterial(shader, "ExoTao_Titanium", new Color(0.16f, 0.19f, 0.23f), 0.75f, 0.7f);
        Material matCinta = CriarMaterial(shader, "ExoTao_Straps", new Color(0.08f, 0.10f, 0.12f), 0.2f, 0.3f);
        Material matAtuador = CriarMaterial(shader, "ExoTao_Motor", new Color(0.85f, 0.38f, 0.08f), 0.8f, 0.75f);
        matLedRepouso = CriarMaterialEmissivo(shader, "ExoTao_LED_Cyan", new Color(0.0f, 0.85f, 1.0f));
        matLedAtivo = CriarMaterialEmissivo(shader, "ExoTao_LED_Green", new Color(0.1f, 1.0f, 0.4f));

        // 1. Cinto Pélvico Lombar ExoTao (acoplado à Pelve)
        Transform cintoPelvico = ObterOuCriarGrupo(ossoPelve, "ExoTao_Cinto_Pelvico");
        if (cintoPelvico.childCount == 0)
        {
            CriarPeca(cintoPelvico, "Arco_Pelvico", PrimitiveType.Cylinder,
                new Vector3(0f, -0.02f, -0.01f), Vector3.zero, new Vector3(0.23f, 0.035f, 0.17f), matEstrutura);
            CriarPeca(cintoPelvico, "Unidade_Controle_Traseira", PrimitiveType.Cube,
                new Vector3(0f, 0.04f, -0.10f), Vector3.zero, new Vector3(0.14f, 0.11f, 0.05f), matEstrutura);
            CriarPeca(cintoPelvico, "LED_Unidade_Traseira", PrimitiveType.Cube,
                new Vector3(0f, 0.04f, -0.128f), Vector3.zero, new Vector3(0.09f, 0.02f, 0.01f), matLedRepouso);
        }
        else
        {
            AtualizarMaterialFilho(cintoPelvico, "Arco_Pelvico", matEstrutura);
            AtualizarMaterialFilho(cintoPelvico, "Unidade_Controle_Traseira", matEstrutura);
            AtualizarMaterialFilho(cintoPelvico, "LED_Unidade_Traseira", matLedRepouso);
        }

        // 2. Exoesqueleto da Perna Direita (Lado Lateral = +X no espaço da coxa/canela)
        MontarMembroExoesqueleto(
            ossoQuadrilDir, ossoJoelhoDir, ossoTornozeloDir,
            ladoLateralX: -0.085f, // Nota: como RightUpLeg tem 180° em Z, local -X aponta para a direita (+X do corpo)!
            sufixo: "Dir",
            matEstrutura, matCinta, matAtuador,
            out ledMotorQuadrilDir, out ledMotorJoelhoDir);

        // 3. Exoesqueleto da Perna Esquerda (como LeftUpLeg tem 180° em Z, local +X aponta para a esquerda (-X do corpo)!)
        MontarMembroExoesqueleto(
            ossoQuadrilEsq, ossoJoelhoEsq, ossoTornozeloEsq,
            ladoLateralX: 0.085f,
            sufixo: "Esq",
            matEstrutura, matCinta, matAtuador,
            out ledMotorQuadrilEsq, out ledMotorJoelhoEsq);
    }

    private void MontarMembroExoesqueleto(
        Transform ossoCoxa, Transform ossoCanela, Transform ossoPe,
        float ladoLateralX, string sufixo,
        Material matEstrutura, Material matCinta, Material matAtuador,
        out Renderer ledQuadril, out Renderer ledJoelho)
    {
        ledQuadril = null;
        ledJoelho = null;

        // --- COXA E MOTOR DO QUADRIL ---
        Transform grupoCoxa = ObterOuCriarGrupo(ossoCoxa, $"ExoTao_Coxa_{sufixo}");
        if (grupoCoxa.childCount == 0)
        {
            // Motor rotativo do quadril (alinhado ao eixo X de flexão/extensão)
            CriarPeca(grupoCoxa, $"Motor_Quadril_{sufixo}", PrimitiveType.Cylinder,
                new Vector3(ladoLateralX, 0.0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.075f, 0.022f, 0.075f), matAtuador);

            GameObject ledQ = CriarPeca(grupoCoxa, $"LED_Quadril_{sufixo}", PrimitiveType.Cylinder,
                new Vector3(ladoLateralX * 1.18f, 0.0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.05f, 0.008f, 0.05f), matLedRepouso);
            ledQuadril = ledQ.GetComponent<Renderer>();

            // Haste telescópica lateral da coxa (vai de y=0 até y=0.40m ao longo do fêmur)
            CriarPeca(grupoCoxa, $"Haste_Femoral_{sufixo}", PrimitiveType.Cube,
                new Vector3(ladoLateralX, 0.20f, 0f), Vector3.zero, new Vector3(0.022f, 0.38f, 0.032f), matEstrutura);

            // Cintas de velcro/neoprene na coxa
            CriarPeca(grupoCoxa, $"Cinta_Coxa_Sup_{sufixo}", PrimitiveType.Cylinder,
                new Vector3(0f, 0.14f, 0f), Vector3.zero, new Vector3(0.145f, 0.025f, 0.145f), matCinta);
            CriarPeca(grupoCoxa, $"Cinta_Coxa_Inf_{sufixo}", PrimitiveType.Cylinder,
                new Vector3(0f, 0.28f, 0f), Vector3.zero, new Vector3(0.125f, 0.025f, 0.125f), matCinta);
        }
        else
        {
            AtualizarMaterialFilho(grupoCoxa, $"Motor_Quadril_{sufixo}", matAtuador);
            AtualizarMaterialFilho(grupoCoxa, $"LED_Quadril_{sufixo}", matLedRepouso);
            AtualizarMaterialFilho(grupoCoxa, $"Haste_Femoral_{sufixo}", matEstrutura);
            AtualizarMaterialFilho(grupoCoxa, $"Cinta_Coxa_Sup_{sufixo}", matCinta);
            AtualizarMaterialFilho(grupoCoxa, $"Cinta_Coxa_Inf_{sufixo}", matCinta);
            Transform lq = grupoCoxa.Find($"LED_Quadril_{sufixo}");
            if (lq != null) ledQuadril = lq.GetComponent<Renderer>();
        }

        // --- CANELA E MOTOR DO JOELHO ---
        Transform grupoCanela = ObterOuCriarGrupo(ossoCanela, $"ExoTao_Canela_{sufixo}");
        if (grupoCanela.childCount == 0)
        {
            // Motor rotativo do joelho
            CriarPeca(grupoCanela, $"Motor_Joelho_{sufixo}", PrimitiveType.Cylinder,
                new Vector3(ladoLateralX * 0.92f, 0.0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.068f, 0.020f, 0.068f), matAtuador);

            GameObject ledJ = CriarPeca(grupoCanela, $"LED_Joelho_{sufixo}", PrimitiveType.Cylinder,
                new Vector3(ladoLateralX * 1.08f, 0.0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.044f, 0.008f, 0.044f), matLedRepouso);
            ledJoelho = ledJ.GetComponent<Renderer>();

            // Haste lateral da tíbia/canela (vai de y=0 até y=0.41m)
            CriarPeca(grupoCanela, $"Haste_Tibial_{sufixo}", PrimitiveType.Cube,
                new Vector3(ladoLateralX * 0.88f, 0.205f, 0f), Vector3.zero, new Vector3(0.020f, 0.39f, 0.028f), matEstrutura);

            // Cinta da panturrilha
            CriarPeca(grupoCanela, $"Cinta_Canela_{sufixo}", PrimitiveType.Cylinder,
                new Vector3(0f, 0.16f, 0f), Vector3.zero, new Vector3(0.11f, 0.025f, 0.11f), matCinta);
        }
        else
        {
            AtualizarMaterialFilho(grupoCanela, $"Motor_Joelho_{sufixo}", matAtuador);
            AtualizarMaterialFilho(grupoCanela, $"LED_Joelho_{sufixo}", matLedRepouso);
            AtualizarMaterialFilho(grupoCanela, $"Haste_Tibial_{sufixo}", matEstrutura);
            AtualizarMaterialFilho(grupoCanela, $"Cinta_Canela_{sufixo}", matCinta);
            Transform lj = grupoCanela.Find($"LED_Joelho_{sufixo}");
            if (lj != null) ledJoelho = lj.GetComponent<Renderer>();
        }

        // --- PÉ / SUPORTE PLANTAR ---
        if (ossoPe != null)
        {
            Transform grupoPe = ObterOuCriarGrupo(ossoPe, $"ExoTao_Pe_{sufixo}");
            if (grupoPe.childCount == 0)
            {
                CriarPeca(grupoPe, $"Articulacao_Tornozelo_{sufixo}", PrimitiveType.Sphere,
                    new Vector3(ladoLateralX * 0.82f, 0f, 0f), Vector3.zero, new Vector3(0.04f, 0.04f, 0.04f), matAtuador);
            }
            else
            {
                AtualizarMaterialFilho(grupoPe, $"Articulacao_Tornozelo_{sufixo}", matAtuador);
            }
        }
    }

    void LateUpdate()
    {
        if (!poseInicialCapturada || ossoQuadrilDir == null)
        {
            InicializarAvatarECinematica();
            if (!poseInicialCapturada) return;
        }

        // 1. Alinhamento com a Câmera VR (em Play Mode)
        if (Application.isPlaying && sincronizarComCameraVR && cameraVR != null && raizAvatarYBot != null)
        {
            // Mantém a cabeça em escala mínima em Play Mode para não obstruir a visão em 1ª pessoa
            if (ossoCabeca != null)
                ossoCabeca.localScale = new Vector3(0.001f, 0.001f, 0.001f);

            // Alinha o giro horizontal (Yaw) do corpo com a direção que o paciente está olhando
            Vector3 direcaoOlhar = cameraVR.forward;
            direcaoOlhar.y = 0f;
            if (direcaoOlhar.sqrMagnitude > 0.001f)
            {
                Quaternion rotAlvoYaw = Quaternion.LookRotation(direcaoOlhar.normalized, Vector3.up);
                raizAvatarYBot.rotation = Quaternion.Slerp(raizAvatarYBot.rotation, rotAlvoYaw, Time.deltaTime * 12f);
            }

            // Posiciona o tronco ligeiramente atrás do globo ocular para visão natural do peito e das pernas ao olhar para baixo
            Vector3 posPes = transform.position;
            Vector3 offsetMundo = raizAvatarYBot.rotation * offsetCorpoCamera;
            raizAvatarYBot.position = new Vector3(cameraVR.position.x + offsetMundo.x, posPes.y, cameraVR.position.z + offsetMundo.z);
        }
        else if (!Application.isPlaying && ossoCabeca != null)
        {
            // No Editor (Scene View), mantém a cabeça visível (escala 1) para inspeção completa do Y Bot
            ossoCabeca.localScale = Vector3.one;
        }

        // 2. Leitura dos ângulos recebidos do Exoesqueleto (UDP / TCP / F9)
        float alvoQuadDir = 0f, alvoJoelhoDir = 0f;
        float alvoQuadEsq = 0f, alvoJoelhoEsq = 0f;

        if (receptorExo != null)
        {
            alvoQuadDir   = Mathf.Clamp(receptorExo.AnguloQuadrilDir, -35f, 75f);
            // Garante que o joelho flexione anatomicamente para trás (valores negativos na convenção local do Mixamo)
            alvoJoelhoDir = -Mathf.Clamp(Mathf.Abs(receptorExo.AnguloJoelhoDir), 0f, 115f);

            alvoQuadEsq   = Mathf.Clamp(receptorExo.AnguloQuadrilEsq, -35f, 75f);
            alvoJoelhoEsq = -Mathf.Clamp(Mathf.Abs(receptorExo.AnguloJoelhoEsq), 0f, 115f);
        }

        float fator = Application.isPlaying && suavizacaoCinematica > 0f
            ? Mathf.Clamp01(Time.deltaTime * suavizacaoCinematica)
            : 1f;

        AplicarCinematicaCompleta(alvoQuadDir, alvoJoelhoDir, alvoQuadEsq, alvoJoelhoEsq, fator);
    }

    public void AplicarCinematicaCompleta(float alvoQuadDir, float alvoJoelhoDir, float alvoQuadEsq, float alvoJoelhoEsq, float fatorInterpolacao)
    {
        quadDirAtual   = Mathf.Lerp(quadDirAtual, alvoQuadDir, fatorInterpolacao);
        joelhoDirAtual = Mathf.Lerp(joelhoDirAtual, alvoJoelhoDir, fatorInterpolacao);
        quadEsqAtual   = Mathf.Lerp(quadEsqAtual, alvoQuadEsq, fatorInterpolacao);
        joelhoEsqAtual = Mathf.Lerp(joelhoEsqAtual, alvoJoelhoEsq, fatorInterpolacao);

        // 1. QUADRIL E JOELHO DIREITO (Cinemática Direta Exata sobre a Rest Pose do Mixamo)
        if (ossoQuadrilDir != null)
        {
            ossoQuadrilDir.localRotation = rotRepousoQuadrilDir * Quaternion.Euler(quadDirAtual, 0f, 0f);
        }
        if (ossoJoelhoDir != null)
        {
            ossoJoelhoDir.localRotation = rotRepousoJoelhoDir * Quaternion.Euler(joelhoDirAtual, 0f, 0f);
        }

        // 2. QUADRIL E JOELHO ESQUERDO
        if (ossoQuadrilEsq != null)
        {
            ossoQuadrilEsq.localRotation = rotRepousoQuadrilEsq * Quaternion.Euler(quadEsqAtual, 0f, 0f);
        }
        if (ossoJoelhoEsq != null)
        {
            ossoJoelhoEsq.localRotation = rotRepousoJoelhoEsq * Quaternion.Euler(joelhoEsqAtual, 0f, 0f);
        }

        // 3. TORNOZELOS (Compensação Fisiológica de Dorsiflexão/Plantarflexão)
        if (compensarTornozeloAutomatico)
        {
            // Durante a fase de balanço (joelho fletido), dorsiflexiona o pé (+X) para não arrastar a ponta do pé no chão
            tornDirAtual = Mathf.Clamp(-0.30f * quadDirAtual - 0.25f * joelhoDirAtual, -20f, 25f);
            tornEsqAtual = Mathf.Clamp(-0.30f * quadEsqAtual - 0.25f * joelhoEsqAtual, -20f, 25f);

            if (ossoTornozeloDir != null)
                ossoTornozeloDir.localRotation = rotRepousoTornozeloDir * Quaternion.Euler(tornDirAtual, 0f, 0f);
            if (ossoTornozeloEsq != null)
                ossoTornozeloEsq.localRotation = rotRepousoTornozeloEsq * Quaternion.Euler(tornEsqAtual, 0f, 0f);
        }

        // 4. PELVE (Oscilação Vertical e Báscula Pélvica Sutil durante a Marcha)
        if (ossoPelve != null)
        {
            float aberturaPasso = Mathf.Max(Mathf.Abs(quadDirAtual), Mathf.Abs(quadEsqAtual));
            float abaixamentoPelvico = -0.022f * Mathf.Sin(Mathf.Clamp01(aberturaPasso / 30f) * Mathf.PI * 0.5f);
            ossoPelve.localPosition = posRepousoPelve + new Vector3(0f, abaixamentoPelvico, 0f);

            float basculaRoll = (quadDirAtual - quadEsqAtual) * 0.06f;
            float rotacaoYawPelvica = (quadEsqAtual - quadDirAtual) * 0.10f;
            ossoPelve.localRotation = rotRepousoPelve * Quaternion.Euler(0f, rotacaoYawPelvica, basculaRoll);
        }

        // 5. MEMBROS SUPERIORES (Retira do T-Pose e aplica Balanço Contralateral Natural)
        // Quando a perna direita avança (+quadDirAtual), o braço esquerdo avança e o direito recua
        float swingBracoEsq = quadDirAtual * ganhoBalancoBracos;
        float swingBracoDir = quadEsqAtual * ganhoBalancoBracos;

        if (ossoBracoEsq != null)
        {
            ossoBracoEsq.localRotation = rotRepousoBracoEsq
                * Quaternion.Euler(anguloAbaixarBracos, 0f, 0f)
                * Quaternion.Euler(0f, 0f, -swingBracoEsq);
        }
        if (ossoAntebracoEsq != null)
        {
            float flexaoCotoveloEsq = 22f + Mathf.Max(0f, swingBracoEsq * 0.4f);
            ossoAntebracoEsq.localRotation = rotRepousoAntebracoEsq * Quaternion.Euler(0f, 0f, -flexaoCotoveloEsq);
        }

        if (ossoBracoDir != null)
        {
            ossoBracoDir.localRotation = rotRepousoBracoDir
                * Quaternion.Euler(anguloAbaixarBracos, 0f, 0f)
                * Quaternion.Euler(0f, 0f, swingBracoDir);
        }
        if (ossoAntebracoDir != null)
        {
            float flexaoCotoveloDir = 22f + Mathf.Max(0f, swingBracoDir * 0.4f);
            ossoAntebracoDir.localRotation = rotRepousoAntebracoDir * Quaternion.Euler(0f, 0f, flexaoCotoveloDir);
        }

        // 6. Atualiza Biofeedback de LEDs nos Motores do Exoesqueleto
        AtualizarLedsMotores();
    }

    private void AtualizarLedsMotores()
    {
        if (matLedAtivo == null || matLedRepouso == null) return;

        if (ledMotorQuadrilDir != null)
            ledMotorQuadrilDir.sharedMaterial = Mathf.Abs(quadDirAtual) > 5f ? matLedAtivo : matLedRepouso;
        if (ledMotorJoelhoDir != null)
            ledMotorJoelhoDir.sharedMaterial = Mathf.Abs(joelhoDirAtual) > 8f ? matLedAtivo : matLedRepouso;
        if (ledMotorQuadrilEsq != null)
            ledMotorQuadrilEsq.sharedMaterial = Mathf.Abs(quadEsqAtual) > 5f ? matLedAtivo : matLedRepouso;
        if (ledMotorJoelhoEsq != null)
            ledMotorJoelhoEsq.sharedMaterial = Mathf.Abs(joelhoEsqAtual) > 8f ? matLedAtivo : matLedRepouso;
    }

    // =========================================================================
    // UTILITÁRIOS DE CONSTRUÇÃO 3D E MATERIAIS
    // =========================================================================

    private Transform EncontrarFilhoRecursivo(Transform pai, string nomeExato)
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

    private Transform ObterOuCriarGrupo(Transform pai, string nome)
    {
        Transform existente = pai.Find(nome);
        if (existente != null) return existente;

        GameObject go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    private GameObject CriarPeca(Transform pai, string nome, PrimitiveType tipo, Vector3 posLocal, Vector3 rotEuler, Vector3 escalaLocal, Material mat)
    {
        GameObject peca = GameObject.CreatePrimitive(tipo);
        peca.name = nome;
        peca.transform.SetParent(pai, false);
        peca.transform.localPosition = posLocal;
        peca.transform.localRotation = Quaternion.Euler(rotEuler);
        peca.transform.localScale = escalaLocal;

        Collider col = peca.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }

        Renderer r = peca.GetComponent<Renderer>();
        if (r != null && mat != null)
        {
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
        return peca;
    }

    private void AtualizarMaterialFilho(Transform pai, string nomeFilho, Material mat)
    {
        if (pai == null || mat == null) return;
        Transform f = pai.Find(nomeFilho);
        if (f == null) return;
        Renderer r = f.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = mat;
    }

    private Material CriarMaterial(Shader shader, string nome, Color cor, float metallic, float smoothness)
    {
        Material existente = Resources.Load<Material>("Materials/" + nome);
        if (existente != null)
        {
            if (shader != null && existente.shader != shader)
            {
                existente.shader = shader;
                DefinirCorMaterial(existente, cor, metallic, smoothness);
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(existente);
#endif
            }
            return existente;
        }

        Material m = new Material(shader);
        m.name = nome;
        DefinirCorMaterial(m, cor, metallic, smoothness);

#if UNITY_EDITOR
        SalvarMaterialComoAsset(m, nome);
#endif
        return m;
    }

    private void DefinirCorMaterial(Material m, Color cor, float metallic, float smoothness)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", cor);
        if (m.HasProperty("_Color")) m.SetColor("_Color", cor);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
    }

    private Material CriarMaterialEmissivo(Shader shader, string nome, Color corEmissao)
    {
        Material existente = Resources.Load<Material>("Materials/" + nome);
        if (existente != null)
        {
            if (shader != null && existente.shader != shader)
            {
                existente.shader = shader;
                DefinirCorMaterial(existente, corEmissao, 0.2f, 0.9f);
                existente.EnableKeyword("_EMISSION");
                if (existente.HasProperty("_EmissionColor"))
                    existente.SetColor("_EmissionColor", corEmissao * 2.2f);
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(existente);
#endif
            }
            return existente;
        }

        Material m = new Material(shader);
        m.name = nome;
        DefinirCorMaterial(m, corEmissao, 0.2f, 0.9f);
        m.EnableKeyword("_EMISSION");
        if (m.HasProperty("_EmissionColor"))
            m.SetColor("_EmissionColor", corEmissao * 2.2f);

#if UNITY_EDITOR
        SalvarMaterialComoAsset(m, nome);
#endif
        return m;
    }

#if UNITY_EDITOR
    private void SalvarMaterialComoAsset(Material mat, string nome)
    {
        if (Application.isPlaying) return;
        if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources"))
            UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
        if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources/Materials"))
            UnityEditor.AssetDatabase.CreateFolder("Assets/Resources", "Materials");

        string assetPath = $"Assets/Resources/Materials/{nome}.mat";
        Material jaSalvo = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (jaSalvo == null)
        {
            UnityEditor.AssetDatabase.CreateAsset(mat, assetPath);
        }
    }
#endif
}
