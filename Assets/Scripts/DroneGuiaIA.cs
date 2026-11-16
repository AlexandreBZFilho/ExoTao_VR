using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;

/// <summary>
/// "Cérebro" do Drone Guia de Reabilitação (EXO-BOT) do ExoTao VR Rehab (ReRobLab / IC).
///
/// Responsabilidades (e SOMENTE estas):
/// - Máquina de estados clínica (saudação, aguardando, guiando marcha, alerta de simetria, celebração);
/// - HUD holográfico com mensagens e telemetria, sempre virado para o paciente;
/// - Cor de biofeedback (luz + anéis emissivos via MaterialPropertyBlock, sem clonar material);
/// - Sons de voz/hover do mascote.
///
/// A POSIÇÃO do drone (seguir o paciente, arrastar pelo laser, colisão) é responsabilidade exclusiva do
/// <see cref="VRRehab.DronePet.DroneCompanionFollowAndGrab"/>. Carinho/cutucão/dança ficam no
/// <see cref="VRRehab.DronePet.DroneAffectiveSystem"/> e a comemoração no <see cref="VRRehab.DronePet.DroneMilestoneCelebration"/>.
/// </summary>
[DisallowMultipleComponent]
public class DroneGuiaIA : MonoBehaviour
{
    public static DroneGuiaIA Instancia { get; private set; }

    public enum EstadoDrone
    {
        SaudacaoInicial,
        AguardandoPaciente,
        GuiandoMarcha,
        AlertaSimetria,
        CelebrandoMeta
    }

    [Header("Estado Atual da IA")]
    public EstadoDrone estadoAtual = EstadoDrone.SaudacaoInicial;
    public string EstadoAtualNome => estadoAtual.ToString();
    /// <summary>Incrementa toda vez que o drone entra em CelebrandoMeta (usado pelo DroneMilestoneCelebration).</summary>
    public int ContadorMetasCelebradas { get; private set; }

    [Header("Referências do Paciente")]
    public Transform corpoPaciente;
    public Transform cameraPaciente;
    public ReceptorUDP receptorExo;
    public TelemetriaClinica telemetria;

    [Header("Metas")]
    [Tooltip("A cada quantos passos o drone comemora uma meta alcançada")]
    public int passosPorMeta = 15;

    [Header("Cores de Biofeedback Clínico")]
    public Color corGuiandoNormal = new Color(0.0f, 0.96f, 0.83f);   // Ciano Biomédico
    public Color corAguardando = new Color(0.22f, 0.71f, 1.0f);       // Azul Calmo
    public Color corAlertaSimetria = new Color(1.0f, 0.68f, 0.0f);    // Âmbar / Alerta Clínico
    public Color corCelebracao = new Color(0.4f, 1.0f, 0.3f);         // Verde Energia

    private const string NomeModelo = "Modelo_3D_ExoBot";
    private const string NomeCanvas = "Canvas_Holograma_Clinico";
    private const int IndiceSubmeshGlow = 2;

    // Visual / áudio
    private Transform modeloRaiz;
    private MeshRenderer rendererModelo;
    private MaterialPropertyBlock blocoGlow;
    private Light luzNucleo;
    private AudioSource fonteHover3D;
    private AudioSource fonteVozPet3D;

    // HUD
    private Canvas canvasHolograma;
    private TextMeshProUGUI txtFalaIA;
    private TextMeshProUGUI txtMetricasHUD;
    private Image imgBarraSimetria;
    private Image imgBordaPainel;

    // FSM
    private float tempoNoEstado;
    private float tempoSemCaminhar;
    private int ultimaMetaPassosCelebrada;
    private int ultimoTotalPassosHUD = -1;
    private float ultimaSimetriaHUD = -1f;
    private float proximaAtualizacaoHUD;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    // =========================================================================
    // CICLO DE VIDA
    // =========================================================================

    void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }
        Instancia = this;

        LocalizarReferencias();
        ConstruirVisualSeNecessario();
        ConstruirHUDHolografico(false);
        LimparRestosLegados();

        // Componente de locomoção/arraste (é adicionado automaticamente nas cenas que ainda não o têm)
        if (GetComponent<VRRehab.DronePet.DroneCompanionFollowAndGrab>() == null)
            gameObject.AddComponent<VRRehab.DronePet.DroneCompanionFollowAndGrab>();
    }

    void Start()
    {
        GarantirEventSystemXR();
        MudarEstado(EstadoDrone.SaudacaoInicial);
    }

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    public void LocalizarReferencias()
    {
        if (receptorExo == null) receptorExo = FindFirstObjectByType<ReceptorUDP>();
        if (telemetria == null) telemetria = FindFirstObjectByType<TelemetriaClinica>();
        if (cameraPaciente == null && Camera.main != null) cameraPaciente = Camera.main.transform;

        if (corpoPaciente == null)
        {
            if (receptorExo != null && receptorExo.controlePaciente != null) corpoPaciente = receptorExo.controlePaciente.transform;
            else
            {
                CharacterController cc = FindFirstObjectByType<CharacterController>();
                if (cc != null) corpoPaciente = cc.transform;
                else if (cameraPaciente != null) corpoPaciente = cameraPaciente;
            }
        }
    }

    /// <summary>Usado pelo painel do Editor (ExoTao IC) ao criar o drone numa cena.</summary>
    public void InicializarNoEditor()
    {
        LocalizarReferencias();
        GarantirEventSystemXR();
        ConstruirVisualSeNecessario();
        ConstruirHUDHolografico(true);

        if (corpoPaciente != null)
        {
            Vector3 frente = corpoPaciente.forward;
            frente.y = 0f;
            if (frente.sqrMagnitude < 0.001f) frente = Vector3.forward;
            frente.Normalize();
            Vector3 esquerda = Vector3.Cross(frente, Vector3.up);
            transform.position = corpoPaciente.position + frente * 1.35f + esquerda * 0.55f + Vector3.up * 1.3f;
            transform.rotation = Quaternion.LookRotation(-frente, Vector3.up);
        }
    }

    public void GarantirEventSystemXR()
    {
        EventSystem es = FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            GameObject goEs = new GameObject("EventSystem");
            goEs.AddComponent<EventSystem>();
            goEs.AddComponent<XRUIInputModule>();
        }
        else if (es.GetComponent<XRUIInputModule>() == null && es.GetComponent<StandaloneInputModule>() == null)
        {
            es.gameObject.AddComponent<XRUIInputModule>();
        }
    }

    void Update()
    {
        if (corpoPaciente == null || cameraPaciente == null)
        {
            LocalizarReferencias();
            if (corpoPaciente == null) return;
        }

        tempoNoEstado += Time.deltaTime;

        bool caminhandoAgora = VerificarSePacienteEstaCaminhando();
        tempoSemCaminhar = caminhandoAgora ? 0f : tempoSemCaminhar + Time.deltaTime;

        AtualizarMaquinaDeEstados(caminhandoAgora);
        AtualizarPainelHolografico();
    }

    void LateUpdate()
    {
        // O painel sempre de frente para o paciente (texto nunca espelhado)
        if (canvasHolograma != null && cameraPaciente != null)
        {
            Vector3 dir = canvasHolograma.transform.position - cameraPaciente.position;
            if (dir.sqrMagnitude > 0.0001f)
                canvasHolograma.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
    }

    // =========================================================================
    // MÁQUINA DE ESTADOS CLÍNICA
    // =========================================================================

    private bool VerificarSePacienteEstaCaminhando()
    {
        if (receptorExo != null && receptorExo.PacienteCaminhando) return true;
        if (telemetria != null && telemetria.velocidadeAtualMs > 0.12f) return true;
        return false;
    }

    private void AtualizarMaquinaDeEstados(bool caminhando)
    {
        if (telemetria != null && passosPorMeta > 0 && telemetria.totalPassos >= ultimaMetaPassosCelebrada + passosPorMeta)
        {
            ultimaMetaPassosCelebrada = (telemetria.totalPassos / passosPorMeta) * passosPorMeta;
            MudarEstado(EstadoDrone.CelebrandoMeta);
            return;
        }

        switch (estadoAtual)
        {
            case EstadoDrone.SaudacaoInicial:
                if (caminhando || tempoNoEstado > 5.0f)
                    MudarEstado(caminhando ? EstadoDrone.GuiandoMarcha : EstadoDrone.AguardandoPaciente);
                break;

            case EstadoDrone.AguardandoPaciente:
                if (caminhando) MudarEstado(EstadoDrone.GuiandoMarcha);
                break;

            case EstadoDrone.GuiandoMarcha:
                if (!caminhando && tempoSemCaminhar > 2.8f) MudarEstado(EstadoDrone.AguardandoPaciente);
                else if (telemetria != null && telemetria.totalPassos >= 4 && telemetria.indiceSimetriaMarcha < 76f)
                    MudarEstado(EstadoDrone.AlertaSimetria);
                break;

            case EstadoDrone.AlertaSimetria:
                if (!caminhando && tempoSemCaminhar > 3.0f) MudarEstado(EstadoDrone.AguardandoPaciente);
                else if (telemetria != null && telemetria.indiceSimetriaMarcha >= 81f) MudarEstado(EstadoDrone.GuiandoMarcha);
                break;

            case EstadoDrone.CelebrandoMeta:
                if (tempoNoEstado > 3.2f)
                    MudarEstado(caminhando ? EstadoDrone.GuiandoMarcha : EstadoDrone.AguardandoPaciente);
                break;
        }
    }

    private void MudarEstado(EstadoDrone novoEstado, bool tocarSom = true)
    {
        estadoAtual = novoEstado;
        tempoNoEstado = 0f;

        Color corAlvo = corGuiandoNormal;
        string mensagem = "";

        switch (novoEstado)
        {
            case EstadoDrone.SaudacaoInicial:
                corAlvo = corAguardando;
                mensagem = "Olá! Sou seu Drone Guia do ExoTao.\nAponte o laser em mim e segure para me mover!";
                break;
            case EstadoDrone.AguardandoPaciente:
                corAlvo = corAguardando;
                mensagem = "Estou te esperando!\nEleve o quadril suavemente para avançar.";
                break;
            case EstadoDrone.GuiandoMarcha:
                corAlvo = corGuiandoNormal;
                mensagem = "Excelente padrão de marcha!\nSiga meu ritmo mantendo a simetria.";
                break;
            case EstadoDrone.AlertaSimetria:
                corAlvo = corAlertaSimetria;
                string dicaLado = (telemetria != null && telemetria.diagnosticoSimetria != null && telemetria.diagnosticoSimetria.Contains("Esquerda"))
                    ? "Tente flexionar um pouco mais a PERNA ESQUERDA!"
                    : "Tente flexionar um pouco mais a PERNA DIREITA!";
                mensagem = $"Ajuste de Biofeedback:\n{dicaLado}";
                break;
            case EstadoDrone.CelebrandoMeta:
                corAlvo = corCelebracao;
                ContadorMetasCelebradas++;
                int passosAtuais = telemetria != null ? telemetria.totalPassos : passosPorMeta;
                mensagem = $"META ALCANÇADA! ({passosAtuais} Passos)\nÓtimo desempenho biomecânico!";
                break;
        }

        AplicarCorBiofeedback(corAlvo);
        if (txtFalaIA != null) txtFalaIA.text = mensagem;

        if (tocarSom && GerenciadorAudioRehab.Instancia != null && fonteVozPet3D != null)
        {
            switch (novoEstado)
            {
                case EstadoDrone.SaudacaoInicial:
                case EstadoDrone.GuiandoMarcha:
                    GerenciadorAudioRehab.Instancia.TocarSomDronePetSaudacao(fonteVozPet3D);
                    break;
                case EstadoDrone.CelebrandoMeta:
                    GerenciadorAudioRehab.Instancia.TocarSomDronePetFeliz(fonteVozPet3D);
                    break;
                case EstadoDrone.AlertaSimetria:
                case EstadoDrone.AguardandoPaciente:
                    GerenciadorAudioRehab.Instancia.TocarSomDronePetCurioso(fonteVozPet3D);
                    break;
            }
        }
    }

    /// <summary>Exibe mensagem customizada no HUD com alteração opcional da cor de biofeedback.</summary>
    public void DefinirMensagemPersonalizada(string mensagem, Color? cor = null)
    {
        if (txtFalaIA != null) txtFalaIA.text = mensagem;
        if (cor.HasValue) AplicarCorBiofeedback(cor.Value);
    }

    /// <summary>Restaura o texto e a cor do estado clínico atual (sem repetir o som).</summary>
    public void RestaurarMensagemEstado()
    {
        float t = tempoNoEstado;
        MudarEstado(estadoAtual, false);
        tempoNoEstado = t;
    }

    private void AtualizarPainelHolografico()
    {
        if (telemetria == null || txtMetricasHUD == null) return;

        // Atualiza o texto só quando os números mudam (evita alocar strings todo frame)
        if (Time.time < proximaAtualizacaoHUD) return;
        proximaAtualizacaoHUD = Time.time + 0.25f;
        if (telemetria.totalPassos == ultimoTotalPassosHUD && Mathf.Abs(telemetria.indiceSimetriaMarcha - ultimaSimetriaHUD) < 0.5f && ultimoTotalPassosHUD > 0)
            return;
        ultimoTotalPassosHUD = telemetria.totalPassos;
        ultimaSimetriaHUD = telemetria.indiceSimetriaMarcha;

        txtMetricasHUD.text =
            $"PASSOS: <b>{telemetria.totalPassos}</b> (D:{telemetria.passosPernaDireita} | E:{telemetria.passosPernaEsquerda})   " +
            $"DISTÂNCIA: <b>{telemetria.distanciaPercorridaMetros:F1}m</b>\n" +
            $"CADÊNCIA: <b>{telemetria.cadenciaPassosPorMinuto:F0} SPM</b>   " +
            $"SIMETRIA: <b>{telemetria.indiceSimetriaMarcha:F0}%</b> ({telemetria.diagnosticoSimetria})";

        if (imgBarraSimetria != null)
        {
            imgBarraSimetria.fillAmount = Mathf.Clamp01(telemetria.indiceSimetriaMarcha / 100f);
            if (telemetria.indiceSimetriaMarcha >= 82f) imgBarraSimetria.color = corGuiandoNormal;
            else if (telemetria.indiceSimetriaMarcha >= 68f) imgBarraSimetria.color = corAlertaSimetria;
            else imgBarraSimetria.color = new Color(1f, 0.25f, 0.25f);
        }
    }

    private void AplicarCorBiofeedback(Color cor)
    {
        if (luzNucleo != null) luzNucleo.color = cor;

        if (rendererModelo != null && rendererModelo.sharedMaterials.Length > IndiceSubmeshGlow)
        {
            if (blocoGlow == null) blocoGlow = new MaterialPropertyBlock();
            rendererModelo.GetPropertyBlock(blocoGlow, IndiceSubmeshGlow);
            blocoGlow.SetColor(ColorId, cor);
            blocoGlow.SetColor(BaseColorId, cor);
            blocoGlow.SetColor(EmissionColorId, cor * 2.2f);
            rendererModelo.SetPropertyBlock(blocoGlow, IndiceSubmeshGlow);
        }

        if (imgBordaPainel != null) imgBordaPainel.color = new Color(cor.r, cor.g, cor.b, 0.85f);
    }

    // =========================================================================
    // VISUAL 3D (NÃO DESTRUTIVO: só cria o que estiver faltando)
    // =========================================================================

    public void ConstruirVisualSeNecessario()
    {
        modeloRaiz = transform.Find(NomeModelo);
        if (modeloRaiz == null)
        {
            modeloRaiz = new GameObject(NomeModelo).transform;
            modeloRaiz.SetParent(transform, false);
            modeloRaiz.localScale = Vector3.one * 0.96f;
        }

        MeshFilter mf = modeloRaiz.GetComponent<MeshFilter>();
        if (mf == null) mf = modeloRaiz.gameObject.AddComponent<MeshFilter>();
        if (mf.sharedMesh == null)
        {
            GameObject fbx = Resources.Load<GameObject>("Models/ExoBot_Blueprint_Drone");
            MeshFilter mfFbx = fbx != null ? fbx.GetComponentInChildren<MeshFilter>() : null;
            if (mfFbx != null) mf.sharedMesh = mfFbx.sharedMesh;
            else Debug.LogError("[DroneGuiaIA] Malha Resources/Models/ExoBot_Blueprint_Drone não encontrada.", this);
        }

        rendererModelo = modeloRaiz.GetComponent<MeshRenderer>();
        if (rendererModelo == null) rendererModelo = modeloRaiz.gameObject.AddComponent<MeshRenderer>();
        Material[] mats = rendererModelo.sharedMaterials;
        if (mats.Length < 3 || mats[0] == null || mats[1] == null || mats[2] == null)
        {
            rendererModelo.sharedMaterials = new Material[]
            {
                Resources.Load<Material>("Materials/SciFi_Drone_Hologram_Armor"),
                Resources.Load<Material>("Materials/SciFi_Console_Titanium"),
                Resources.Load<Material>("Materials/SciFi_Console_CyanGlow")
            };
        }
        // Sem sombra projetada: a malha tem ~25k triângulos e voa ao lado do paciente (em VR a sombra re-desenha tudo por olho)
        rendererModelo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rendererModelo.receiveShadows = true;

        // Luz pontual de biofeedback (sem sombra: em VR cada sombra de point light custa 6 passes por olho)
        Transform tLuz = modeloRaiz.Find("Luz_Biofeedback");
        if (tLuz == null)
        {
            tLuz = new GameObject("Luz_Biofeedback").transform;
            tLuz.SetParent(modeloRaiz, false);
            tLuz.localPosition = new Vector3(0f, -0.05f, 0.22f);
        }
        luzNucleo = tLuz.GetComponent<Light>();
        if (luzNucleo == null) luzNucleo = tLuz.gameObject.AddComponent<Light>();
        luzNucleo.type = LightType.Point;
        luzNucleo.range = 2.5f;
        luzNucleo.intensity = 1.2f;
        luzNucleo.shadows = LightShadows.None;

        // Fontes de áudio 3D do modelo: [0] hover em loop suave, [1] voz do mascote
        AudioSource[] fontes = modeloRaiz.GetComponents<AudioSource>();
        fonteHover3D = fontes.Length > 0 ? fontes[0] : modeloRaiz.gameObject.AddComponent<AudioSource>();
        fonteVozPet3D = fontes.Length > 1 ? fontes[1] : modeloRaiz.gameObject.AddComponent<AudioSource>();

        AudioClip clipHover = Resources.Load<AudioClip>("Audio/SFX_Drone_Hover_Loop");
        fonteHover3D.clip = clipHover;
        fonteHover3D.loop = true;
        fonteHover3D.playOnAwake = true;
        fonteHover3D.spatialBlend = 1.0f;
        fonteHover3D.dopplerLevel = 0f;
        fonteHover3D.minDistance = 0.8f;
        fonteHover3D.maxDistance = 8.0f;
        fonteHover3D.volume = 0.12f;
        if (Application.isPlaying && clipHover != null && !fonteHover3D.isPlaying) fonteHover3D.Play();

        fonteVozPet3D.loop = false;
        fonteVozPet3D.playOnAwake = false;
        fonteVozPet3D.spatialBlend = 1.0f;
        fonteVozPet3D.dopplerLevel = 0f;
        fonteVozPet3D.minDistance = 1.0f;
        fonteVozPet3D.maxDistance = 10.0f;
        fonteVozPet3D.volume = 0.5f;
    }

    /// <summary>Remove peças antigas do sistema de arraste por UI (zona invisível, EventTriggers, botões -perto/+longe).</summary>
    private void LimparRestosLegados()
    {
        if (canvasHolograma == null) return;
        Transform c = canvasHolograma.transform;

        Transform zona = c.Find("Zona_Arraste_Corpo_Drone");
        if (zona != null) DestruirObjeto(zona.gameObject);
        Transform barra = c.Find("Barra_Controle_Espacial");
        if (barra != null) DestruirObjeto(barra.gameObject);

        foreach (EventTrigger et in c.GetComponentsInChildren<EventTrigger>(true)) DestruirObjeto(et);
        // O painel não intercepta o laser: o drone é pinçado pelo corpo (BoxCollider)
        foreach (Graphic g in c.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
    }

    private static void DestruirObjeto(Object o)
    {
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    // =========================================================================
    // HUD HOLOGRÁFICO
    // =========================================================================

    public void ConstruirHUDHolografico(bool forcarRecriacao = false)
    {
        Transform tCanvasExistente = transform.Find(NomeCanvas);
        if (tCanvasExistente != null)
        {
            if (forcarRecriacao)
            {
                DestruirObjeto(tCanvasExistente.gameObject);
                canvasHolograma = null;
            }
            else
            {
                canvasHolograma = tCanvasExistente.GetComponent<Canvas>();
            }
        }

        if (canvasHolograma != null)
        {
            if (cameraPaciente != null && cameraPaciente.TryGetComponent(out Camera cam)) canvasHolograma.worldCamera = cam;
            Transform f = canvasHolograma.transform.Find("Fundo_Holograma");
            if (f != null)
            {
                Transform b = f.Find("Barra_Estado_Topo");
                if (b != null) imgBordaPainel = b.GetComponent<Image>();
                Transform tf = f.Find("Texto_Fala_IA");
                if (tf != null) txtFalaIA = tf.GetComponent<TextMeshProUGUI>();
                Transform tm = f.Find("Texto_Telemetria");
                if (tm != null) txtMetricasHUD = tm.GetComponent<TextMeshProUGUI>();
                Transform bf = f.Find("Fundo_Barra_Simetria/Fill_Barra_Simetria");
                if (bf != null) imgBarraSimetria = bf.GetComponent<Image>();
            }
            return;
        }

        GameObject goCanvas = new GameObject(NomeCanvas);
        goCanvas.transform.SetParent(transform, false);
        goCanvas.transform.localPosition = new Vector3(0f, 0.58f, 0f);
        goCanvas.transform.localRotation = Quaternion.identity;
        goCanvas.transform.localScale = Vector3.one * 0.0022f;

        canvasHolograma = goCanvas.AddComponent<Canvas>();
        canvasHolograma.renderMode = RenderMode.WorldSpace;
        if (Camera.main != null) canvasHolograma.worldCamera = Camera.main;
        CanvasScaler cs = goCanvas.AddComponent<CanvasScaler>();
        cs.dynamicPixelsPerUnit = 10f;

        RectTransform rtCanvas = goCanvas.GetComponent<RectTransform>();
        rtCanvas.sizeDelta = new Vector2(460f, 200f);

        GameObject goFundo = CriarFilhoUI("Fundo_Holograma", goCanvas.transform, Vector2.zero, Vector2.one);
        Image imgFundo = goFundo.AddComponent<Image>();
        Sprite spriteHud = Resources.Load<Sprite>("UI/SciFi_HUD_Frame");
        if (spriteHud != null)
        {
            imgFundo.sprite = spriteHud;
            imgFundo.color = new Color(1f, 1f, 1f, 0.95f);
        }
        else imgFundo.color = new Color(0.04f, 0.08f, 0.14f, 0.88f);
        imgFundo.raycastTarget = false;

        imgBordaPainel = CriarFilhoUI("Barra_Estado_Topo", goFundo.transform, new Vector2(0.08f, 0.92f), new Vector2(0.92f, 0.96f)).AddComponent<Image>();
        imgBordaPainel.color = corGuiandoNormal;
        imgBordaPainel.raycastTarget = false;

        txtFalaIA = CriarFilhoUI("Texto_Fala_IA", goFundo.transform, new Vector2(0.07f, 0.46f), new Vector2(0.93f, 0.89f)).AddComponent<TextMeshProUGUI>();
        txtFalaIA.fontSize = 20.5f;
        txtFalaIA.alignment = TextAlignmentOptions.Center;
        txtFalaIA.color = Color.white;
        txtFalaIA.text = "EXO-BOT IA Inicializado";
        txtFalaIA.raycastTarget = false;

        txtMetricasHUD = CriarFilhoUI("Texto_Telemetria", goFundo.transform, new Vector2(0.07f, 0.16f), new Vector2(0.93f, 0.44f)).AddComponent<TextMeshProUGUI>();
        txtMetricasHUD.fontSize = 16.0f;
        txtMetricasHUD.alignment = TextAlignmentOptions.Center;
        txtMetricasHUD.color = new Color(0.8f, 0.95f, 1f);
        txtMetricasHUD.text = "PASSOS: 0 | DISTÂNCIA: 0.0m | SIMETRIA: 100%";
        txtMetricasHUD.raycastTarget = false;

        GameObject goBarraFundo = CriarFilhoUI("Fundo_Barra_Simetria", goFundo.transform, new Vector2(0.12f, 0.07f), new Vector2(0.88f, 0.13f));
        Image imgBarraBg = goBarraFundo.AddComponent<Image>();
        imgBarraBg.color = new Color(0.2f, 0.25f, 0.3f, 0.8f);
        imgBarraBg.raycastTarget = false;

        imgBarraSimetria = CriarFilhoUI("Fill_Barra_Simetria", goBarraFundo.transform, Vector2.zero, Vector2.one).AddComponent<Image>();
        imgBarraSimetria.color = corGuiandoNormal;
        imgBarraSimetria.raycastTarget = false;
    }

    private static GameObject CriarFilhoUI(string nome, Transform pai, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }
}
