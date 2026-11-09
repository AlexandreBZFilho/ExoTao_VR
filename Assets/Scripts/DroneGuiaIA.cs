using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// IA Companheira e Drone Guia de Reabilitação ("EXO-BOT") para o projeto ExoTao VR Rehab (ReRobLab / IC).
/// Possui máquina de estados clínica (FSM), construção procedural 3D em URP e HUD Holográfico integrado
/// conectado em tempo real ao módulo TelemetriaClinica.
/// </summary>
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

    [Header("Referências do Paciente")]
    public Transform corpoPaciente;
    public Transform cameraPaciente;
    public ReceptorUDP receptorExo;
    public TelemetriaClinica telemetria;

    [Header("Parâmetros de Navegação e Ritmo (Pacing)")]
    [Tooltip("Distância ideal que o drone mantém à frente do paciente durante a caminhada")]
    public float distanciaIdealFrente = 2.7f;
    [Tooltip("Distância máxima antes do drone parar e esperar pelo paciente")]
    public float distanciaMaximaEspera = 4.2f;
    [Tooltip("Altura do drone em relação ao solo/olhos do paciente")]
    public float alturaFlutuacao = 1.45f;
    [Tooltip("Velocidade de interpolação do movimento do drone")]
    public float suavidadeMovimento = 3.5f;
    [Tooltip("A cada quantos passos o drone comemora uma meta alcançada")]
    public int passosPorMeta = 15;

    [Header("Cores de Biofeedback Clínico")]
    public Color corGuiandoNormal = new Color(0.0f, 0.96f, 0.83f);   // Ciano Biomédico
    public Color corAguardando = new Color(0.22f, 0.71f, 1.0f);       // Azul Calmo
    public Color corAlertaSimetria = new Color(1.0f, 0.68f, 0.0f);    // Âmbar / Alerta Clínico
    public Color corCelebracao = new Color(0.4f, 1.0f, 0.3f);         // Verde Energia

    // Componentes Visuais Internos do Drone
    private Transform modeloRaiz;
    private Transform anelGiroscopico;
    private Transform olhoVisor;
    private Light luzNucleo;
    private Renderer[] renderizadoresEmissivos;
    private Material materialEmissivoDinamico;

    // HUD Holográfico Acoplado ao Drone
    private Canvas canvasHolograma;
    private TextMeshProUGUI txtFalaIA;
    private TextMeshProUGUI txtMetricasHUD;
    private Image imgBarraSimetria;
    private Image imgBordaPainel;

    // Variáveis de Controle da FSM
    private float tempoNoEstado = 0f;
    private float tempoSemCaminhar = 0f;
    private int ultimaMetaPassosCelebrada = 0;
    private float anguloPiruetaCelebracao = 0f;
    private Vector3 posicaoAncoraFrente;
    private bool ancoraInicializada = false;

    void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }
        Instancia = this;
    }

    void Start()
    {
        LocalizarReferencias();
        ConstruirVisualProceduralSeNecessario();
        ConstruirHUDHolografico();
        if (Application.isPlaying)
        {
            PosicionarInicialmenteNaFrenteDoPaciente();
            MudarEstado(EstadoDrone.SaudacaoInicial);
        }
    }

    public void InicializarNoEditor()
    {
        LocalizarReferencias();
        ConstruirVisualProceduralSeNecessario();
        ConstruirHUDHolografico();
        PosicionarInicialmenteNaFrenteDoPaciente();
    }

    public void LocalizarReferencias()
    {
        if (receptorExo == null)
            receptorExo = FindFirstObjectByType<ReceptorUDP>();

        if (telemetria == null)
            telemetria = FindFirstObjectByType<TelemetriaClinica>();

        if (cameraPaciente == null && Camera.main != null)
            cameraPaciente = Camera.main.transform;

        if (corpoPaciente == null)
        {
            if (receptorExo != null && receptorExo.controlePaciente != null)
                corpoPaciente = receptorExo.controlePaciente.transform;
            else if (receptorExo != null)
                corpoPaciente = receptorExo.transform;
            else
            {
                CharacterController cc = FindFirstObjectByType<CharacterController>();
                if (cc != null) corpoPaciente = cc.transform;
                else if (cameraPaciente != null) corpoPaciente = cameraPaciente;
            }
        }
    }

    private void PosicionarInicialmenteNaFrenteDoPaciente()
    {
        if (corpoPaciente == null) return;

        Vector3 direcaoOlhar = ObterDirecaoHorizontalPaciente();
        Vector3 posInicial = corpoPaciente.position + direcaoOlhar * distanciaIdealFrente;
        posInicial.y = ObterAlturaSolo(posInicial) + alturaFlutuacao;

        transform.position = posInicial;
        posicaoAncoraFrente = posInicial;
        ancoraInicializada = true;

        // Começa olhando para o paciente
        Vector3 dirParaPaciente = corpoPaciente.position - transform.position;
        dirParaPaciente.y = 0f;
        if (dirParaPaciente.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dirParaPaciente.normalized);
    }

    void Update()
    {
        if (corpoPaciente == null)
        {
            LocalizarReferencias();
            if (corpoPaciente == null) return;
        }

        tempoNoEstado += Time.deltaTime;

        bool caminhandoAgora = VerificarSePacienteEstaCaminhando();
        if (caminhandoAgora)
            tempoSemCaminhar = 0f;
        else
            tempoSemCaminhar += Time.deltaTime;

        AtualizarMaquinaDeEstados(caminhandoAgora);
        AtualizarLocomocaoEFlutuacao(caminhandoAgora);
        AtualizarAnimacoesGiroscopicas(caminhandoAgora);
        AtualizarPainelHolografico();
    }

    private bool VerificarSePacienteEstaCaminhando()
    {
        if (receptorExo != null && receptorExo.PacienteCaminhando)
            return true;
        if (telemetria != null && telemetria.velocidadeAtualMs > 0.12f)
            return true;
        return false;
    }

    private void AtualizarMaquinaDeEstados(bool caminhando)
    {
        // 1. Checa se atingiu nova meta de passos para celebrar
        if (telemetria != null && telemetria.totalPassos >= ultimaMetaPassosCelebrada + passosPorMeta)
        {
            ultimaMetaPassosCelebrada = (telemetria.totalPassos / passosPorMeta) * passosPorMeta;
            MudarEstado(EstadoDrone.CelebrandoMeta);
            return;
        }

        switch (estadoAtual)
        {
            case EstadoDrone.SaudacaoInicial:
                if (caminhando || tempoNoEstado > 5.0f)
                {
                    MudarEstado(caminhando ? EstadoDrone.GuiandoMarcha : EstadoDrone.AguardandoPaciente);
                }
                break;

            case EstadoDrone.AguardandoPaciente:
                if (caminhando)
                {
                    MudarEstado(EstadoDrone.GuiandoMarcha);
                }
                break;

            case EstadoDrone.GuiandoMarcha:
                if (!caminhando && tempoSemCaminhar > 2.8f)
                {
                    MudarEstado(EstadoDrone.AguardandoPaciente);
                }
                else if (telemetria != null && telemetria.totalPassos >= 4 && telemetria.indiceSimetriaMarcha < 76f)
                {
                    MudarEstado(EstadoDrone.AlertaSimetria);
                }
                break;

            case EstadoDrone.AlertaSimetria:
                if (!caminhando && tempoSemCaminhar > 3.0f)
                {
                    MudarEstado(EstadoDrone.AguardandoPaciente);
                }
                else if (telemetria != null && telemetria.indiceSimetriaMarcha >= 81f)
                {
                    MudarEstado(EstadoDrone.GuiandoMarcha);
                }
                break;

            case EstadoDrone.CelebrandoMeta:
                if (tempoNoEstado > 3.2f)
                {
                    MudarEstado(caminhando ? EstadoDrone.GuiandoMarcha : EstadoDrone.AguardandoPaciente);
                }
                break;
        }
    }

    private void MudarEstado(EstadoDrone novoEstado)
    {
        estadoAtual = novoEstado;
        tempoNoEstado = 0f;
        anguloPiruetaCelebracao = 0f;

        Color corAlvo = corGuiandoNormal;
        string mensagem = "";

        switch (novoEstado)
        {
            case EstadoDrone.SaudacaoInicial:
                corAlvo = corAguardando;
                mensagem = "Olá! Sou seu Drone Guia do ExoTao.\nInicie a marcha para começarmos!";
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
                string dicaLado = (telemetria != null && telemetria.diagnosticoSimetria.Contains("Esquerda"))
                    ? "Tente flexionar um pouco mais a PERNA ESQUERDA!"
                    : "Tente flexionar um pouco mais a PERNA DIREITA!";
                mensagem = $"Ajuste de Biofeedback:\n{dicaLado}";
                break;

            case EstadoDrone.CelebrandoMeta:
                corAlvo = corCelebracao;
                int passosAtuais = telemetria != null ? telemetria.totalPassos : passosPorMeta;
                mensagem = $"META ALCANÇADA! ({passosAtuais} Passos)\nÓtimo desempenho biomecânico!";
                break;
        }

        AplicarCorBiofeedback(corAlvo);
        if (txtFalaIA != null) txtFalaIA.text = mensagem;
    }

    private void AtualizarLocomocaoEFlutuacao(bool caminhando)
    {
        Vector3 direcaoFrente = ObterDirecaoHorizontalPaciente();
        Vector3 posAlvoHorizontal = corpoPaciente.position + direcaoFrente * distanciaIdealFrente;

        if (!ancoraInicializada)
        {
            posicaoAncoraFrente = posAlvoHorizontal;
            ancoraInicializada = true;
        }

        // Quando o paciente caminha ou gira, atualiza suavemente o ponto-alvo à frente
        float distDroneAoPaciente = Vector3.Distance(
            new Vector3(transform.position.x, 0f, transform.position.z),
            new Vector3(corpoPaciente.position.x, 0f, corpoPaciente.position.z)
        );

        if (caminhando || distDroneAoPaciente > distanciaMaximaEspera || distDroneAoPaciente < 1.3f)
        {
            posicaoAncoraFrente = Vector3.Lerp(posicaoAncoraFrente, posAlvoHorizontal, Time.deltaTime * suavidadeMovimento);
        }

        // Calcula altura do chão via Raycast para acompanhar subidas/escadas + flutuação senoidal (Hover)
        float alturaChao = ObterAlturaSolo(posicaoAncoraFrente);
        float offsetSenoidal = Mathf.Sin(Time.time * 2.6f) * 0.09f;

        Vector3 destinoFinal = new Vector3(
            posicaoAncoraFrente.x,
            alturaChao + alturaFlutuacao + offsetSenoidal,
            posicaoAncoraFrente.z
        );

        transform.position = Vector3.Lerp(transform.position, destinoFinal, Time.deltaTime * suavidadeMovimento);

        // Rotação: olha para o paciente para dar feedback visual constante
        Vector3 alvoOlhar = cameraPaciente != null ? cameraPaciente.position : (corpoPaciente.position + Vector3.up * 1.5f);
        Vector3 dirParaPaciente = alvoOlhar - transform.position;
        dirParaPaciente.y *= 0.25f; // Inclinação vertical suave

        if (dirParaPaciente.sqrMagnitude > 0.01f)
        {
            Quaternion rotBase = Quaternion.LookRotation(dirParaPaciente.normalized);
            if (estadoAtual == EstadoDrone.CelebrandoMeta)
            {
                anguloPiruetaCelebracao += Time.deltaTime * 360f;
                rotBase *= Quaternion.Euler(0f, anguloPiruetaCelebracao, 0f);
            }
            transform.rotation = Quaternion.Slerp(transform.rotation, rotBase, Time.deltaTime * 5f);
        }
    }

    private void AtualizarAnimacoesGiroscopicas(bool caminhando)
    {
        if (anelGiroscopico != null)
        {
            float velGiro = caminhando ? 140f : 55f;
            if (estadoAtual == EstadoDrone.CelebrandoMeta) velGiro = 320f;
            anelGiroscopico.Rotate(Vector3.up, velGiro * Time.deltaTime, Space.Self);
            anelGiroscopico.Rotate(Vector3.right, (velGiro * 0.35f) * Time.deltaTime, Space.Self);
        }
    }

    private void AtualizarPainelHolografico()
    {
        if (telemetria == null || txtMetricasHUD == null) return;

        txtMetricasHUD.text =
            $"PASSOS: <b>{telemetria.totalPassos}</b> (D:{telemetria.passosPernaDireita} | E:{telemetria.passosPernaEsquerda})   " +
            $"DISTÂNCIA: <b>{telemetria.distanciaPercorridaMetros:F1}m</b>\n" +
            $"CADÊNCIA: <b>{telemetria.cadenciaPassosPorMinuto:F0} SPM</b>   " +
            $"SIMETRIA: <b>{telemetria.indiceSimetriaMarcha:F0}%</b> ({telemetria.diagnosticoSimetria})";

        if (imgBarraSimetria != null)
        {
            imgBarraSimetria.fillAmount = Mathf.Clamp01(telemetria.indiceSimetriaMarcha / 100f);
            if (telemetria.indiceSimetriaMarcha >= 82f)
                imgBarraSimetria.color = corGuiandoNormal;
            else if (telemetria.indiceSimetriaMarcha >= 68f)
                imgBarraSimetria.color = corAlertaSimetria;
            else
                imgBarraSimetria.color = new Color(1f, 0.25f, 0.25f);
        }

        // Mantém o Canvas holográfico sempre legível de frente para a câmera do paciente
        if (canvasHolograma != null && cameraPaciente != null)
        {
            Vector3 dirCam = canvasHolograma.transform.position - cameraPaciente.position;
            dirCam.y = 0f;
            if (dirCam.sqrMagnitude > 0.001f)
                canvasHolograma.transform.rotation = Quaternion.LookRotation(dirCam.normalized);
        }
    }

    private Vector3 ObterDirecaoHorizontalPaciente()
    {
        Vector3 frente = corpoPaciente.forward;
        frente.y = 0f;
        if (frente.sqrMagnitude < 0.001f && cameraPaciente != null)
        {
            frente = cameraPaciente.forward;
            frente.y = 0f;
        }
        return frente.sqrMagnitude > 0.001f ? frente.normalized : Vector3.forward;
    }

    private float ObterAlturaSolo(Vector3 posHorizontal)
    {
        Vector3 origemRay = new Vector3(posHorizontal.x, corpoPaciente.position.y + 2.5f, posHorizontal.z);
        if (Physics.Raycast(origemRay, Vector3.down, out RaycastHit hit, 10f))
        {
            return hit.point.y;
        }
        return corpoPaciente.position.y;
    }

    private void AplicarCorBiofeedback(Color cor)
    {
        if (luzNucleo != null)
            luzNucleo.color = cor;

        if (materialEmissivoDinamico != null)
        {
            materialEmissivoDinamico.color = cor;
            if (materialEmissivoDinamico.HasProperty("_BaseColor"))
                materialEmissivoDinamico.SetColor("_BaseColor", cor);
            if (materialEmissivoDinamico.HasProperty("_EmissionColor"))
                materialEmissivoDinamico.SetColor("_EmissionColor", cor * 2.2f);
        }

        if (imgBordaPainel != null)
            imgBordaPainel.color = new Color(cor.r, cor.g, cor.b, 0.85f);
    }

    public void ConstruirVisualProceduralSeNecessario()
    {
        bool usandoSRP = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
        Shader shaderAtivo = usandoSRP
            ? (Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
            : (Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"));

        Material matCorpoBranco = ObterOuCriarMaterialDrone(shaderAtivo, "ExoBot_Body_Mat", new Color(0.92f, 0.95f, 0.98f), 0.25f, 0.85f, false);
        Material matMetalEscuro = ObterOuCriarMaterialDrone(shaderAtivo, "ExoBot_DarkMetal_Mat", new Color(0.15f, 0.18f, 0.22f), 0.70f, 0.75f, false);
        Material matGlowBase = ObterOuCriarMaterialDrone(shaderAtivo, "ExoBot_Glow_Mat", corGuiandoNormal, 0.20f, 0.90f, true);

        materialEmissivoDinamico = Application.isPlaying ? new Material(matGlowBase) : matGlowBase;

        if (modeloRaiz == null)
            modeloRaiz = transform.Find("Modelo_3D_ExoBot");

        if (modeloRaiz != null && modeloRaiz.childCount > 0)
        {
            anelGiroscopico = modeloRaiz.Find("Anel_Giroscopico_Orbital");
            olhoVisor = modeloRaiz.Find("Olho_Biomedico_Emissivo");
            Transform tLuz = modeloRaiz.Find("Luz_Biofeedback");
            if (tLuz != null) luzNucleo = tLuz.GetComponent<Light>();
            return;
        }

        if (modeloRaiz == null)
        {
            modeloRaiz = new GameObject("Modelo_3D_ExoBot").transform;
            modeloRaiz.SetParent(transform, false);
        }

        // 1. Esfera Central do Drone Médico
        GameObject esferaCentral = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        esferaCentral.name = "Chassis_Central";
        esferaCentral.transform.SetParent(modeloRaiz, false);
        esferaCentral.transform.localScale = new Vector3(0.38f, 0.34f, 0.38f);
        RemoverCollider(esferaCentral);
        esferaCentral.GetComponent<Renderer>().sharedMaterial = matCorpoBranco;

        // 2. Visor Frontal Escuro + Olho Biomédico Emissivo
        GameObject molduraVisor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        molduraVisor.name = "Moldura_Visor";
        molduraVisor.transform.SetParent(modeloRaiz, false);
        molduraVisor.transform.localPosition = new Vector3(0f, 0.02f, 0.12f);
        molduraVisor.transform.localScale = new Vector3(0.26f, 0.16f, 0.18f);
        RemoverCollider(molduraVisor);
        molduraVisor.GetComponent<Renderer>().sharedMaterial = matMetalEscuro;

        GameObject olho = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        olho.name = "Olho_Biomedico_Emissivo";
        olho.transform.SetParent(modeloRaiz, false);
        olho.transform.localPosition = new Vector3(0f, 0.02f, 0.17f);
        olho.transform.localScale = new Vector3(0.14f, 0.08f, 0.10f);
        RemoverCollider(olho);
        olho.GetComponent<Renderer>().sharedMaterial = materialEmissivoDinamico;
        olhoVisor = olho.transform;

        // 3. Anel Giroscópico Orbital
        anelGiroscopico = new GameObject("Anel_Giroscopico_Orbital").transform;
        anelGiroscopico.SetParent(modeloRaiz, false);
        anelGiroscopico.localRotation = Quaternion.Euler(18f, 0f, 18f);

        int numSegmentos = 12;
        float raioAnel = 0.32f;
        for (int i = 0; i < numSegmentos; i++)
        {
            float angulo = i * Mathf.PI * 2f / numSegmentos;
            Vector3 posSeg = new Vector3(Mathf.Cos(angulo) * raioAnel, 0f, Mathf.Sin(angulo) * raioAnel);

            GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.name = $"Seg_Orbital_{i}";
            seg.transform.SetParent(anelGiroscopico, false);
            seg.transform.localPosition = posSeg;
            seg.transform.LookAt(anelGiroscopico.position);
            seg.transform.localScale = new Vector3(0.12f, 0.025f, 0.035f);
            RemoverCollider(seg);
            seg.GetComponent<Renderer>().sharedMaterial = (i % 2 == 0) ? materialEmissivoDinamico : matMetalEscuro;
        }

        // 4. Propulsores Laterais Flutuantes
        for (int lado = -1; lado <= 1; lado += 2)
        {
            GameObject asa = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            asa.name = lado < 0 ? "Propulsor_Esq" : "Propulsor_Dir";
            asa.transform.SetParent(modeloRaiz, false);
            asa.transform.localPosition = new Vector3(lado * 0.28f, -0.04f, 0f);
            asa.transform.localScale = new Vector3(0.08f, 0.12f, 0.08f);
            RemoverCollider(asa);
            asa.GetComponent<Renderer>().sharedMaterial = matCorpoBranco;
        }

        // 5. Luz Pontual de Biofeedback
        GameObject objLuz = new GameObject("Luz_Biofeedback");
        objLuz.transform.SetParent(modeloRaiz, false);
        objLuz.transform.localPosition = new Vector3(0f, -0.1f, 0.2f);
        luzNucleo = objLuz.AddComponent<Light>();
        luzNucleo.type = LightType.Point;
        luzNucleo.range = 4.5f;
        luzNucleo.intensity = 1.6f;
        luzNucleo.color = corGuiandoNormal;
    }

    public void ConstruirHUDHolografico()
    {
        if (canvasHolograma == null)
        {
            Transform tCanvas = transform.Find("Canvas_Holograma_Clinico");
            if (tCanvas != null) canvasHolograma = tCanvas.GetComponent<Canvas>();
        }

        if (canvasHolograma != null)
        {
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

        GameObject goCanvas = new GameObject("Canvas_Holograma_Clinico");
        goCanvas.transform.SetParent(transform, false);
        goCanvas.transform.localPosition = new Vector3(0f, 0.48f, 0f);
        goCanvas.transform.localScale = Vector3.one * 0.0022f;

        canvasHolograma = goCanvas.AddComponent<Canvas>();
        canvasHolograma.renderMode = RenderMode.WorldSpace;

        RectTransform rtCanvas = goCanvas.GetComponent<RectTransform>();
        rtCanvas.sizeDelta = new Vector2(460f, 195f);

        // Fundo Translúcido Escuro Estilo Holograma Médico
        GameObject goFundo = new GameObject("Fundo_Holograma");
        goFundo.transform.SetParent(goCanvas.transform, false);
        RectTransform rtFundo = goFundo.AddComponent<RectTransform>();
        rtFundo.anchorMin = Vector2.zero;
        rtFundo.anchorMax = Vector2.one;
        rtFundo.offsetMin = Vector2.zero;
        rtFundo.offsetMax = Vector2.zero;

        Image imgFundo = goFundo.AddComponent<Image>();
        Sprite spriteHud = Resources.Load<Sprite>("UI/SciFi_HUD_Frame");
        if (spriteHud != null)
        {
            imgFundo.sprite = spriteHud;
            imgFundo.color = new Color(1f, 1f, 1f, 0.94f);
        }
        else
        {
            imgFundo.color = new Color(0.04f, 0.08f, 0.14f, 0.86f);
        }

        // Barra Superior de Cor de Estado
        GameObject goBorda = new GameObject("Barra_Estado_Topo");
        goBorda.transform.SetParent(goFundo.transform, false);
        RectTransform rtBorda = goBorda.AddComponent<RectTransform>();
        rtBorda.anchorMin = new Vector2(0.08f, 0.92f);
        rtBorda.anchorMax = new Vector2(0.92f, 0.96f);
        rtBorda.offsetMin = Vector2.zero;
        rtBorda.offsetMax = Vector2.zero;
        imgBordaPainel = goBorda.AddComponent<Image>();
        imgBordaPainel.color = corGuiandoNormal;

        // Texto de Diálogo / Orientação da IA
        GameObject goTxtFala = new GameObject("Texto_Fala_IA");
        goTxtFala.transform.SetParent(goFundo.transform, false);
        RectTransform rtFala = goTxtFala.AddComponent<RectTransform>();
        rtFala.anchorMin = new Vector2(0.07f, 0.46f);
        rtFala.anchorMax = new Vector2(0.93f, 0.89f);
        rtFala.offsetMin = Vector2.zero;
        rtFala.offsetMax = Vector2.zero;

        txtFalaIA = goTxtFala.AddComponent<TextMeshProUGUI>();
        txtFalaIA.fontSize = 21f;
        txtFalaIA.alignment = TextAlignmentOptions.Center;
        txtFalaIA.color = Color.white;
        txtFalaIA.text = "EXO-BOT IA Inicializado";

        // Texto das Métricas de Telemetria
        GameObject goTxtMetricas = new GameObject("Texto_Telemetria");
        goTxtMetricas.transform.SetParent(goFundo.transform, false);
        RectTransform rtMetricas = goTxtMetricas.AddComponent<RectTransform>();
        rtMetricas.anchorMin = new Vector2(0.07f, 0.16f);
        rtMetricas.anchorMax = new Vector2(0.93f, 0.44f);
        rtMetricas.offsetMin = Vector2.zero;
        rtMetricas.offsetMax = Vector2.zero;

        txtMetricasHUD = goTxtMetricas.AddComponent<TextMeshProUGUI>();
        txtMetricasHUD.fontSize = 16.5f;
        txtMetricasHUD.alignment = TextAlignmentOptions.Center;
        txtMetricasHUD.color = new Color(0.8f, 0.95f, 1f);
        txtMetricasHUD.text = "PASSOS: 0 | DISTÂNCIA: 0.0m | SIMETRIA: 100%";

        // Barra de Progresso de Simetria Bilateral
        GameObject goBarraFundo = new GameObject("Fundo_Barra_Simetria");
        goBarraFundo.transform.SetParent(goFundo.transform, false);
        RectTransform rtBarraFundo = goBarraFundo.AddComponent<RectTransform>();
        rtBarraFundo.anchorMin = new Vector2(0.12f, 0.07f);
        rtBarraFundo.anchorMax = new Vector2(0.88f, 0.13f);
        rtBarraFundo.offsetMin = Vector2.zero;
        rtBarraFundo.offsetMax = Vector2.zero;
        Image imgBarraBg = goBarraFundo.AddComponent<Image>();
        imgBarraBg.color = new Color(0.2f, 0.25f, 0.3f, 0.8f);

        GameObject goBarraFill = new GameObject("Fill_Barra_Simetria");
        goBarraFill.transform.SetParent(goBarraFundo.transform, false);
        RectTransform rtBarraFill = goBarraFill.AddComponent<RectTransform>();
        rtBarraFill.anchorMin = Vector2.zero;
        rtBarraFill.anchorMax = Vector2.one;
        rtBarraFill.offsetMin = Vector2.zero;
        rtBarraFill.offsetMax = Vector2.zero;

        imgBarraSimetria = goBarraFill.AddComponent<Image>();
        imgBarraSimetria.color = corGuiandoNormal;
    }

    private Material ObterOuCriarMaterialDrone(Shader shader, string nome, Color cor, float metallic, float smoothness, bool emissivo)
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

    private void RemoverCollider(GameObject go)
    {
        Collider col = go.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }
    }

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }
}
