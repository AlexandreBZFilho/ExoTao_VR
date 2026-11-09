using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;

/// <summary>
/// IA Companheira e Drone Guia de Reabilitação ("EXO-BOT") para o projeto ExoTao VR Rehab (ReRobLab / IC).
/// Possui máquina de estados clínica (FSM), modelo 3D CAD unificado (Blender 5.1), HUD Holográfico legível
/// e manipulação espacial estilo Meta Quest / SteamVR Hub (Pinça/Raycast com Controle VR ou Mouse para
/// mover livremente para os lados, cima/baixo e aproximar/afastar).
/// </summary>
[ExecuteAlways]
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

    [Header("Parâmetros de Navegação e Posicionamento Espacial (Estilo Quest / SteamVR)")]
    [Tooltip("Distância padrão inicial que o drone mantém à frente do paciente")]
    public float distanciaIdealFrente = 2.7f;
    [Tooltip("Distância máxima antes do drone parar e esperar pelo paciente")]
    public float distanciaMaximaEspera = 4.5f;
    [Tooltip("Altura base do drone em relação ao solo")]
    public float alturaFlutuacao = 1.45f;
    [Tooltip("Offset local personalizado pelo jogador ao arrastar o Drone/Canvas (X = Lado, Y = Altura extra, Z = Distância à frente)")]
    public Vector3 offsetLocalPaciente = new Vector3(0f, 0f, 2.7f);
    [Tooltip("Indica se o jogador já personalizou a posição do drone arrastando com o controle/mouse")]
    public bool posicaoPersonalizadaPeloJogador = false;
    [Tooltip("Velocidade de interpolação do movimento do drone")]
    public float suavidadeMovimento = 3.5f;
    [Tooltip("A cada quantos passos o drone comemora uma meta alcançada")]
    public int passosPorMeta = 15;

    [Header("Interação Espacial (Raycast VR / Mouse)")]
    public bool permitirArrasteEspacial = true;
    public bool estaArrastandoDrone = false;
    public bool ponteiroSobreDroneOuHUD = false;

    [Header("Cores de Biofeedback Clínico")]
    public Color corGuiandoNormal = new Color(0.0f, 0.96f, 0.83f);   // Ciano Biomédico
    public Color corAguardando = new Color(0.22f, 0.71f, 1.0f);       // Azul Calmo
    public Color corAlertaSimetria = new Color(1.0f, 0.68f, 0.0f);    // Âmbar / Alerta Clínico
    public Color corCelebracao = new Color(0.4f, 1.0f, 0.3f);         // Verde Energia

    // Componentes Visuais Internos do Drone
    private Transform modeloRaiz;
    private Light luzNucleo;
    private Material materialEmissivoDinamico;

    // HUD Holográfico Acoplado ao Drone
    private Canvas canvasHolograma;
    private TextMeshProUGUI txtFalaIA;
    private TextMeshProUGUI txtMetricasHUD;
    private TextMeshProUGUI txtBarraArraste;
    private Image imgBarraSimetria;
    private Image imgBordaPainel;
    private Image imgFundoBarraArraste;

    // Variáveis de Controle da FSM
    private float tempoNoEstado = 0f;
    private float tempoSemCaminhar = 0f;
    private int ultimaMetaPassosCelebrada = 0;
    private float anguloPiruetaCelebracao = 0f;
    private Vector3 posicaoAncoraHorizontal;
    private bool ancoraInicializada = false;

    // Estado interno do Arraste Espacial (VR Raycast + Mouse Raycast)
    private XRRayInteractor interactorArrasteAtivo;
    private float distanciaRaioArraste = 2.7f;
    private Vector3 offsetPontoPegadaMundo = Vector3.zero;
    private BoxCollider colisorInteracaoEspacial;

    void Awake()
    {
        if (Application.isPlaying)
        {
            if (Instancia != null && Instancia != this)
            {
                Destroy(gameObject);
                return;
            }
            Instancia = this;
        }
    }

    void OnEnable()
    {
        if (!Application.isPlaying)
        {
            LocalizarReferencias();
            ConstruirVisualProceduralSeNecessario();
            ConstruirHUDHolografico(false);
            GarantirOrientacaoCorretaCanvas();
        }
    }

    void Start()
    {
        LocalizarReferencias();
        GarantirEventSystemXR();
        ConstruirVisualProceduralSeNecessario();
        ConstruirHUDHolografico(false);
        GarantirOrientacaoCorretaCanvas();

        if (Application.isPlaying)
        {
            if (offsetLocalPaciente.sqrMagnitude < 0.1f)
                offsetLocalPaciente = new Vector3(0f, 0f, distanciaIdealFrente);

            PosicionarInicialmenteNaFrenteDoPaciente();
            MudarEstado(EstadoDrone.SaudacaoInicial);
        }
    }

    public void InicializarNoEditor()
    {
        offsetLocalPaciente = new Vector3(0f, 0f, distanciaIdealFrente);
        posicaoPersonalizadaPeloJogador = false;
        LocalizarReferencias();
        GarantirEventSystemXR();
        ConstruirVisualProceduralSeNecessario();
        ConstruirHUDHolografico(true);
        PosicionarInicialmenteNaFrenteDoPaciente();
        GarantirOrientacaoCorretaCanvas();
    }

    public void GarantirEventSystemXR()
    {
        EventSystem es = FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            GameObject goEs = new GameObject("EventSystem");
            es = goEs.AddComponent<EventSystem>();
            goEs.AddComponent<XRUIInputModule>();
        }
        else if (es.GetComponent<XRUIInputModule>() == null && es.GetComponent<StandaloneInputModule>() == null)
        {
            es.gameObject.AddComponent<XRUIInputModule>();
        }
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

        if (offsetLocalPaciente.sqrMagnitude < 0.1f)
            offsetLocalPaciente = new Vector3(0f, 0f, distanciaIdealFrente);

        Quaternion rotHoriz = Quaternion.LookRotation(ObterDirecaoHorizontalPaciente(), Vector3.up);
        Vector3 posInicial = corpoPaciente.position + rotHoriz * new Vector3(offsetLocalPaciente.x, 0f, offsetLocalPaciente.z);
        posInicial.y = ObterAlturaSolo(posInicial) + alturaFlutuacao + offsetLocalPaciente.y;

        transform.position = posInicial;
        posicaoAncoraHorizontal = posInicial;
        ancoraInicializada = true;

        OrientarDroneECanvasParaPaciente(true);
    }

    private void GarantirOrientacaoCorretaCanvas()
    {
        if (modeloRaiz != null)
        {
            modeloRaiz.localPosition = Vector3.zero;
            modeloRaiz.localRotation = Quaternion.identity;
        }

        if (canvasHolograma != null)
        {
            // Como o Drone raiz (+Z) olha PARA o paciente, o Canvas WorldSpace precisa de 180 graus em Y
            // para que a sua face frontal (-Z) fique virada para a câmera e o texto nunca fique espelhado!
            canvasHolograma.transform.localPosition = new Vector3(0f, 0.58f, 0f);
            canvasHolograma.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }
    }

    private void OrientarDroneECanvasParaPaciente(bool instantaneo)
    {
        Vector3 posOlhos = cameraPaciente != null
            ? cameraPaciente.position
            : (corpoPaciente != null ? corpoPaciente.position + Vector3.up * 1.55f : transform.position - transform.forward);

        Vector3 dirParaPaciente = posOlhos - transform.position;
        dirParaPaciente.y *= 0.25f;

        if (dirParaPaciente.sqrMagnitude > 0.001f)
        {
            Quaternion rotBase = Quaternion.LookRotation(dirParaPaciente.normalized, Vector3.up);
            if (Application.isPlaying && estadoAtual == EstadoDrone.CelebrandoMeta && !estaArrastandoDrone)
            {
                anguloPiruetaCelebracao += Time.deltaTime * 360f;
                rotBase *= Quaternion.Euler(0f, anguloPiruetaCelebracao, 0f);
            }

            transform.rotation = instantaneo
                ? rotBase
                : Quaternion.Slerp(transform.rotation, rotBase, Time.deltaTime * 6f);
        }

        if (canvasHolograma != null)
        {
            // Mantém o Canvas sempre de frente para os olhos do paciente (vetor da câmera PARA o canvas = +Z do Canvas)
            Vector3 dirCamParaCanvas = canvasHolograma.transform.position - posOlhos;
            dirCamParaCanvas.y *= 0.30f;
            if (dirCamParaCanvas.sqrMagnitude > 0.001f)
            {
                canvasHolograma.transform.rotation = Quaternion.LookRotation(dirCamParaCanvas.normalized, Vector3.up);
            }
            else
            {
                canvasHolograma.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
        }
    }

    void Update()
    {
        if (!Application.isPlaying)
        {
            GarantirOrientacaoCorretaCanvas();
            return;
        }

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

        ProcessarInteracaoEspacialRaycast();
        AtualizarMaquinaDeEstados(caminhandoAgora);

        if (!estaArrastandoDrone)
        {
            AtualizarLocomocaoEFlutuacao(caminhandoAgora);
        }
        else
        {
            OrientarDroneECanvasParaPaciente(false);
        }

        AtualizarPainelHolografico();
    }

    // =========================================================================
    // SISTEMA DE MANIPULAÇÃO ESPACIAL (META QUEST / STEAMVR HUB / MOUSE RAYCAST)
    // =========================================================================

    private void ProcessarInteracaoEspacialRaycast()
    {
        if (!permitirArrasteEspacial) return;

        // Atalho de teclado 'R' para centralizar o Drone caso desejado
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetarPosicaoPadraoDrone();
        }

        // 1. Se já está arrastando, atualiza a posição 3D ao longo do raio ativo (Controle VR ou Mouse)
        if (estaArrastandoDrone)
        {
            // Permite ajustar a distância (mais perto / mais longe) com Scroll do Mouse, Analógico VR ou Setas/W-S
            float deltaDist = ObterInputAjusteDistancia() * 2.2f * Time.deltaTime;
            if (Mathf.Abs(deltaDist) > 0.0001f)
            {
                distanciaRaioArraste = Mathf.Clamp(distanciaRaioArraste + deltaDist, 1.10f, 6.20f);
            }

            if (ObterRaioArrasteAtual(out Ray raioAtual))
            {
                Vector3 posAlvo = raioAtual.origin + raioAtual.direction * distanciaRaioArraste + offsetPontoPegadaMundo;
                float chaoLocal = ObterAlturaSolo(posAlvo);
                posAlvo.y = Mathf.Clamp(posAlvo.y, chaoLocal + 0.55f, chaoLocal + 3.40f);

                transform.position = Vector3.Lerp(transform.position, posAlvo, Time.deltaTime * 18f);
                SalvarOffsetPersonalizadoAtual();
            }

            // Verifica se soltou o botão (Mouse ou Gatilho/Grip do Controle VR)
            if (!BotaoArrasteContinuaPressionado())
            {
                FinalizarArrasteEspacial();
            }
            return;
        }

        // 2. Se o ponteiro está sobre o Drone/HUD (sem arrastar), permite usar o Scroll para aproximar/afastar
        if (ponteiroSobreDroneOuHUD)
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                AjustarDistanciaDrone(scroll * 0.28f);
            }
        }

        // 3. Verifica início de arraste direto no Colisor 3D do Drone via Controle VR (Grip/Trigger) ou Mouse
        VerificarInicioArrasteColisor3D();
    }

    private void VerificarInicioArrasteColisor3D()
    {
        if (colisorInteracaoEspacial == null) return;

        // A) Checa XRRayInteractors ativos na cena (Controles HTC Vive / Meta Quest)
        XRRayInteractor[] interactors = FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (XRRayInteractor xrRay in interactors)
        {
            if (xrRay == null || !xrRay.isActiveAndEnabled) continue;
            Transform origemT = xrRay.rayOriginTransform != null ? xrRay.rayOriginTransform : xrRay.transform;
            Ray raioVR = new Ray(origemT.position, origemT.forward);

            if (colisorInteracaoEspacial.Raycast(raioVR, out RaycastHit hitVR, 8.0f))
            {
                if (xrRay.isSelectActive || VerificarGatilhoOuGripVRPressionado())
                {
                    IniciarArrasteComRaio(raioVR, xrRay);
                    return;
                }
            }
        }

        // B) Checa clique com Mouse diretamente sobre o corpo 3D do Drone (se não clicou num botão da UI)
        if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
        {
            Camera cam = cameraPaciente != null ? cameraPaciente.GetComponent<Camera>() : Camera.main;
            if (cam == null) cam = Camera.main;
            if (cam != null)
            {
                Ray raioMouse = cam.ScreenPointToRay(Input.mousePosition);
                if (colisorInteracaoEspacial.Raycast(raioMouse, out RaycastHit hitMouse, 10.0f))
                {
                    IniciarArrasteComRaio(raioMouse, null);
                }
            }
        }
    }

    public void IniciarArrasteViaUI(PointerEventData eventData)
    {
        if (!permitirArrasteEspacial) return;

        XRRayInteractor xrInteractor = null;
        if (eventData is TrackedDeviceEventData trackedEvent && trackedEvent.interactor is XRRayInteractor rayInt)
        {
            xrInteractor = rayInt;
        }
        else
        {
            // Procura qual XRRayInteractor está apontando para o Canvas no momento
            XRRayInteractor[] interactors = FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (XRRayInteractor cand in interactors)
            {
                if (cand != null && cand.isActiveAndEnabled && cand.TryGetCurrentUIRaycastResult(out RaycastResult res) && res.isValid)
                {
                    xrInteractor = cand;
                    break;
                }
            }
        }

        if (xrInteractor != null)
        {
            Transform tOrig = xrInteractor.rayOriginTransform != null ? xrInteractor.rayOriginTransform : xrInteractor.transform;
            Ray raioVR = new Ray(tOrig.position, tOrig.forward);
            IniciarArrasteComRaio(raioVR, xrInteractor);
        }
        else
        {
            Camera cam = cameraPaciente != null ? cameraPaciente.GetComponent<Camera>() : Camera.main;
            if (cam == null) cam = Camera.main;
            if (cam != null)
            {
                Ray raioMouse = cam.ScreenPointToRay(eventData.position);
                IniciarArrasteComRaio(raioMouse, null);
            }
        }
    }

    private void IniciarArrasteComRaio(Ray raioInicial, XRRayInteractor xrInteractor)
    {
        estaArrastandoDrone = true;
        interactorArrasteAtivo = xrInteractor;
        distanciaRaioArraste = Mathf.Clamp(Vector3.Distance(raioInicial.origin, transform.position), 1.10f, 6.20f);
        Vector3 pontoNoRaio = raioInicial.origin + raioInicial.direction * distanciaRaioArraste;
        offsetPontoPegadaMundo = transform.position - pontoNoRaio;
        // Limita o offset para evitar desvios grandes
        if (offsetPontoPegadaMundo.magnitude > 0.85f)
            offsetPontoPegadaMundo = offsetPontoPegadaMundo.normalized * 0.85f;

        AtualizarDestaqueVisualBarraArraste();
    }

    public void FinalizarArrasteEspacial()
    {
        if (!estaArrastandoDrone) return;
        estaArrastandoDrone = false;
        interactorArrasteAtivo = null;
        SalvarOffsetPersonalizadoAtual();
        AtualizarDestaqueVisualBarraArraste();
    }

    public void DefinirHoverEspacial(bool hover)
    {
        ponteiroSobreDroneOuHUD = hover;
        AtualizarDestaqueVisualBarraArraste();
    }

    private void AtualizarDestaqueVisualBarraArraste()
    {
        if (imgFundoBarraArraste != null)
        {
            if (estaArrastandoDrone)
                imgFundoBarraArraste.color = new Color(0.0f, 0.95f, 0.88f, 0.95f);
            else if (ponteiroSobreDroneOuHUD)
                imgFundoBarraArraste.color = new Color(0.08f, 0.55f, 0.75f, 0.92f);
            else
                imgFundoBarraArraste.color = new Color(0.05f, 0.16f, 0.26f, 0.88f);
        }

        if (txtBarraArraste != null)
        {
            txtBarraArraste.color = estaArrastandoDrone ? new Color(0.02f, 0.08f, 0.12f, 1f) : Color.white;
            txtBarraArraste.text = estaArrastandoDrone
                ? "[+] MOVENDO DRONE (SCROLL / ANALÓGICO: DISTÂNCIA)"
                : "[+] PINÇAR / ARRASTAR PARA MOVER O DRONE";
        }
    }

    private bool ObterRaioArrasteAtual(out Ray raio)
    {
        if (interactorArrasteAtivo != null && interactorArrasteAtivo.isActiveAndEnabled)
        {
            Transform tOrig = interactorArrasteAtivo.rayOriginTransform != null
                ? interactorArrasteAtivo.rayOriginTransform
                : interactorArrasteAtivo.transform;
            raio = new Ray(tOrig.position, tOrig.forward);
            return true;
        }

        Camera cam = cameraPaciente != null ? cameraPaciente.GetComponent<Camera>() : Camera.main;
        if (cam == null) cam = Camera.main;
        if (cam != null)
        {
            raio = cam.ScreenPointToRay(Input.mousePosition);
            return true;
        }

        raio = new Ray(transform.position, transform.forward);
        return false;
    }

    private bool BotaoArrasteContinuaPressionado()
    {
        if (Input.GetMouseButton(0)) return true;
        if (interactorArrasteAtivo != null && interactorArrasteAtivo.isSelectActive) return true;
        if (VerificarGatilhoOuGripVRPressionado()) return true;
        return false;
    }

    private bool VerificarGatilhoOuGripVRPressionado()
    {
        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, devices);
        foreach (InputDevice dev in devices)
        {
            if (dev.TryGetFeatureValue(CommonUsages.triggerButton, out bool trig) && trig) return true;
            if (dev.TryGetFeatureValue(CommonUsages.gripButton, out bool grip) && grip) return true;
            if (dev.TryGetFeatureValue(CommonUsages.primaryButton, out bool prim) && prim) return true;
        }
        return false;
    }

    private float ObterInputAjusteDistancia()
    {
        float input = Input.mouseScrollDelta.y * 3.5f;

        // Analógico / Touchpad dos controles VR durante o arraste
        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, devices);
        foreach (InputDevice dev in devices)
        {
            if (dev.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 eixo) && Mathf.Abs(eixo.y) > 0.2f)
            {
                input += eixo.y;
            }
        }

        return Mathf.Clamp(input, -3f, 3f);
    }

    private void SalvarOffsetPersonalizadoAtual()
    {
        if (corpoPaciente == null) return;

        Quaternion rotHoriz = Quaternion.LookRotation(ObterDirecaoHorizontalPaciente(), Vector3.up);
        Vector3 delta = transform.position - corpoPaciente.position;
        Vector3 deltaHoriz = new Vector3(delta.x, 0f, delta.z);
        Vector3 localXZ = Quaternion.Inverse(rotHoriz) * deltaHoriz;

        // Garante distância mínima confortável do corpo do paciente
        if (new Vector2(localXZ.x, localXZ.z).magnitude < 1.10f)
        {
            Vector2 dir2D = new Vector2(localXZ.x, localXZ.z).normalized;
            if (dir2D.sqrMagnitude < 0.01f) dir2D = Vector2.up;
            localXZ.x = dir2D.x * 1.10f;
            localXZ.z = dir2D.y * 1.10f;
        }

        float chao = ObterAlturaSolo(transform.position);
        float offsetAltura = (transform.position.y - chao) - alturaFlutuacao;

        offsetLocalPaciente = new Vector3(
            Mathf.Clamp(localXZ.x, -3.8f, 3.8f),
            Mathf.Clamp(offsetAltura, -0.75f, 2.20f),
            Mathf.Clamp(localXZ.z, -1.5f, 5.8f)
        );
        posicaoPersonalizadaPeloJogador = true;
        posicaoAncoraHorizontal = new Vector3(transform.position.x, posAncoraY(), transform.position.z);
    }

    private float posAncoraY() => transform.position.y;

    public void AjustarDistanciaDrone(float deltaMetros)
    {
        if (corpoPaciente == null) LocalizarReferencias();
        if (corpoPaciente == null) return;

        Vector2 xz = new Vector2(offsetLocalPaciente.x, offsetLocalPaciente.z);
        float distAtual = xz.magnitude;
        if (distAtual < 0.1f)
        {
            xz = new Vector2(0f, distanciaIdealFrente);
            distAtual = distanciaIdealFrente;
        }

        float novaDist = Mathf.Clamp(distAtual + deltaMetros, 1.15f, 5.80f);
        Vector2 novoXZ = xz.normalized * novaDist;
        offsetLocalPaciente = new Vector3(novoXZ.x, offsetLocalPaciente.y, novoXZ.y);
        posicaoPersonalizadaPeloJogador = true;
        distanciaRaioArraste = novaDist;

        Quaternion rotHoriz = Quaternion.LookRotation(ObterDirecaoHorizontalPaciente(), Vector3.up);
        posicaoAncoraHorizontal = corpoPaciente.position + rotHoriz * new Vector3(offsetLocalPaciente.x, 0f, offsetLocalPaciente.z);
    }

    public void ResetarPosicaoPadraoDrone()
    {
        offsetLocalPaciente = new Vector3(0f, 0f, distanciaIdealFrente);
        posicaoPersonalizadaPeloJogador = false;
        distanciaRaioArraste = distanciaIdealFrente;

        if (corpoPaciente != null)
        {
            Quaternion rotHoriz = Quaternion.LookRotation(ObterDirecaoHorizontalPaciente(), Vector3.up);
            posicaoAncoraHorizontal = corpoPaciente.position + rotHoriz * new Vector3(0f, 0f, distanciaIdealFrente);
        }
    }

    // =========================================================================
    // MÁQUINA DE ESTADOS CLÍNICA (FSM) E NAVEGAÇÃO
    // =========================================================================

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
                mensagem = "Olá! Sou seu Drone Guia do ExoTao.\nInicie a marcha ou arraste-me para onde preferir!";
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
        if (offsetLocalPaciente.sqrMagnitude < 0.1f)
            offsetLocalPaciente = new Vector3(0f, 0f, distanciaIdealFrente);

        Quaternion rotHorizPaciente = Quaternion.LookRotation(ObterDirecaoHorizontalPaciente(), Vector3.up);
        Vector3 posAlvoHorizontal = corpoPaciente.position + rotHorizPaciente * new Vector3(offsetLocalPaciente.x, 0f, offsetLocalPaciente.z);

        if (!ancoraInicializada)
        {
            posicaoAncoraHorizontal = posAlvoHorizontal;
            ancoraInicializada = true;
        }

        float erroAncora = Vector3.Distance(
            new Vector3(posicaoAncoraHorizontal.x, 0f, posicaoAncoraHorizontal.z),
            new Vector3(posAlvoHorizontal.x, 0f, posAlvoHorizontal.z)
        );

        // Acompanha suavemente o paciente mantendo o offset lateral/frontal escolhido
        if (caminhando || erroAncora > 0.35f)
        {
            posicaoAncoraHorizontal = Vector3.Lerp(posicaoAncoraHorizontal, posAlvoHorizontal, Time.deltaTime * suavidadeMovimento);
        }

        // Flutuação vertical leve e econômica (um único Transform raiz)
        float alturaChao = ObterAlturaSolo(posicaoAncoraHorizontal);
        float offsetSenoidal = Mathf.Sin(Time.time * 2.0f) * 0.045f;

        Vector3 destinoFinal = new Vector3(
            posicaoAncoraHorizontal.x,
            alturaChao + alturaFlutuacao + offsetLocalPaciente.y + offsetSenoidal,
            posicaoAncoraHorizontal.z
        );

        transform.position = Vector3.Lerp(transform.position, destinoFinal, Time.deltaTime * (suavidadeMovimento * 1.4f));

        OrientarDroneECanvasParaPaciente(false);
    }

    private void AtualizarPainelHolografico()
    {
        // Primeiro garante que o Canvas está sempre virado para a câmera (mesmo sem telemetria)
        OrientarDroneECanvasParaPaciente(false);

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
    }

    private Vector3 ObterDirecaoHorizontalPaciente()
    {
        Vector3 frente = corpoPaciente != null ? corpoPaciente.forward : Vector3.forward;
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
        float refY = corpoPaciente != null ? corpoPaciente.position.y : transform.position.y;
        Vector3 origemRay = new Vector3(posHorizontal.x, refY + 2.5f, posHorizontal.z);
        if (Physics.Raycast(origemRay, Vector3.down, out RaycastHit hit, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            return hit.point.y;
        }
        return refY;
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

    // =========================================================================
    // CONSTRUÇÃO DO MODELO 3D UNIFICADO (BLENDER 5.1 CAD BLUEPRINT) E HUD
    // =========================================================================

    public void ConstruirVisualProceduralSeNecessario()
    {
        bool usandoSRP = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
        Shader shaderAtivo = usandoSRP
            ? (Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
            : (Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"));

        Material matHologramaChassis = ObterOuCriarMaterialDrone(shaderAtivo, "SciFi_Drone_Hologram_Armor", new Color(0.07f, 0.25f, 0.38f), 0.78f, 0.86f, false);
        Material matMetalEscuro      = ObterOuCriarMaterialDrone(shaderAtivo, "SciFi_Console_Titanium", new Color(0.11f, 0.14f, 0.18f), 0.78f, 0.72f, false);
        Material matGlowBase         = ObterOuCriarMaterialDrone(shaderAtivo, "SciFi_Console_CyanGlow", corGuiandoNormal, 0.20f, 0.90f, true);

        materialEmissivoDinamico = Application.isPlaying ? new Material(matGlowBase) : matGlowBase;

        if (modeloRaiz == null)
            modeloRaiz = transform.Find("Modelo_3D_ExoBot");

        if (modeloRaiz != null)
        {
            // Remove todos os filhos antigos (esfera branca, cilindros orbitais, etc.)
            for (int i = modeloRaiz.childCount - 1; i >= 0; i--)
            {
                GameObject f = modeloRaiz.GetChild(i).gameObject;
                if (f.name != "Luz_Biofeedback")
                {
                    if (Application.isPlaying) Destroy(f);
                    else DestroyImmediate(f);
                }
            }
        }
        else
        {
            modeloRaiz = new GameObject("Modelo_3D_ExoBot").transform;
            modeloRaiz.SetParent(transform, false);
        }

        modeloRaiz.localPosition = Vector3.zero;
        modeloRaiz.localRotation = Quaternion.identity;
        modeloRaiz.localScale = Vector3.one * 0.96f;

        // Carrega a malha 3D unificada gerada no Blender 5.1 (ExoBot_Blueprint_Drone.fbx)
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

        MeshFilter mf = modeloRaiz.GetComponent<MeshFilter>();
        if (mf == null) mf = modeloRaiz.gameObject.AddComponent<MeshFilter>();
        mf.sharedMesh = malhaBlueprint;

        MeshRenderer mr = modeloRaiz.GetComponent<MeshRenderer>();
        if (mr == null) mr = modeloRaiz.gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterials = new Material[] { matHologramaChassis, matMetalEscuro, materialEmissivoDinamico };

        // Colisor Trigger 3D no objeto raiz para permitir Raycast/Grab direto tanto no Drone quanto no Canvas
        colisorInteracaoEspacial = GetComponent<BoxCollider>();
        if (colisorInteracaoEspacial == null)
            colisorInteracaoEspacial = gameObject.AddComponent<BoxCollider>();
        colisorInteracaoEspacial.isTrigger = true;
        colisorInteracaoEspacial.center = new Vector3(0f, 0.25f, 0f);
        colisorInteracaoEspacial.size = new Vector3(1.10f, 0.95f, 0.45f);

        // Luz Pontual de Biofeedback
        Transform tLuz = modeloRaiz.Find("Luz_Biofeedback");
        if (tLuz != null)
        {
            luzNucleo = tLuz.GetComponent<Light>();
        }
        else
        {
            GameObject objLuz = new GameObject("Luz_Biofeedback");
            objLuz.transform.SetParent(modeloRaiz, false);
            objLuz.transform.localPosition = new Vector3(0f, -0.05f, 0.22f);
            luzNucleo = objLuz.AddComponent<Light>();
            luzNucleo.type = LightType.Point;
            luzNucleo.range = 4.5f;
            luzNucleo.intensity = 1.6f;
            luzNucleo.color = corGuiandoNormal;
        }
    }

    public void ConstruirHUDHolografico(bool forcarRecriacao = false)
    {
        Transform tCanvasExistente = transform.Find("Canvas_Holograma_Clinico");
        if (tCanvasExistente != null)
        {
            // Se for forçado ou se o Canvas antigo não tiver a Barra_Controle_Espacial, recria do zero
            if (forcarRecriacao || tCanvasExistente.Find("Barra_Controle_Espacial") == null)
            {
                if (Application.isPlaying) Destroy(tCanvasExistente.gameObject);
                else DestroyImmediate(tCanvasExistente.gameObject);
                canvasHolograma = null;
            }
            else
            {
                canvasHolograma = tCanvasExistente.GetComponent<Canvas>();
            }
        }

        if (canvasHolograma != null)
        {
            GarantirComponentesInteracaoCanvas(canvasHolograma.gameObject);
            GarantirOrientacaoCorretaCanvas();

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
            Transform bArr = canvasHolograma.transform.Find("Barra_Controle_Espacial/Btn_Pinch_Arraste");
            if (bArr != null)
            {
                imgFundoBarraArraste = bArr.GetComponent<Image>();
                txtBarraArraste = bArr.GetComponentInChildren<TextMeshProUGUI>();
            }
            return;
        }

        GameObject goCanvas = new GameObject("Canvas_Holograma_Clinico");
        goCanvas.transform.SetParent(transform, false);
        goCanvas.transform.localPosition = new Vector3(0f, 0.58f, 0f);
        // IMPORTANTE: 180 graus em Y para que a frente legível (-Z do Canvas) olhe diretamente para a Main Camera!
        goCanvas.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        goCanvas.transform.localScale = Vector3.one * 0.0022f;

        canvasHolograma = goCanvas.AddComponent<Canvas>();
        canvasHolograma.renderMode = RenderMode.WorldSpace;
        if (Camera.main != null) canvasHolograma.worldCamera = Camera.main;

        GarantirComponentesInteracaoCanvas(goCanvas);

        RectTransform rtCanvas = goCanvas.GetComponent<RectTransform>();
        rtCanvas.sizeDelta = new Vector2(460f, 200f);

        // 0. Zona de Captura de Arraste Invisível cobrindo tanto o HUD quanto o corpo 3D do Drone logo abaixo
        GameObject goZonaDrone = new GameObject("Zona_Arraste_Corpo_Drone");
        goZonaDrone.transform.SetParent(goCanvas.transform, false);
        RectTransform rtZonaDrone = goZonaDrone.AddComponent<RectTransform>();
        rtZonaDrone.anchorMin = new Vector2(0.05f, -1.15f);
        rtZonaDrone.anchorMax = new Vector2(0.95f, 1.00f);
        rtZonaDrone.offsetMin = Vector2.zero;
        rtZonaDrone.offsetMax = Vector2.zero;
        Image imgZonaTransparente = goZonaDrone.AddComponent<Image>();
        imgZonaTransparente.color = new Color(0f, 0f, 0f, 0.005f);
        imgZonaTransparente.raycastTarget = true;
        AdicionarGatilhoArrasteUI(goZonaDrone);

        // 1. Fundo Translúcido Escuro Estilo Holograma Médico
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
            imgFundo.color = new Color(1f, 1f, 1f, 0.95f);
        }
        else
        {
            imgFundo.color = new Color(0.04f, 0.08f, 0.14f, 0.88f);
        }
        imgFundo.raycastTarget = true;
        AdicionarGatilhoArrasteUI(goFundo);

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
        imgBordaPainel.raycastTarget = false;

        // Texto de Diálogo / Orientação da IA
        GameObject goTxtFala = new GameObject("Texto_Fala_IA");
        goTxtFala.transform.SetParent(goFundo.transform, false);
        RectTransform rtFala = goTxtFala.AddComponent<RectTransform>();
        rtFala.anchorMin = new Vector2(0.07f, 0.46f);
        rtFala.anchorMax = new Vector2(0.93f, 0.89f);
        rtFala.offsetMin = Vector2.zero;
        rtFala.offsetMax = Vector2.zero;

        txtFalaIA = goTxtFala.AddComponent<TextMeshProUGUI>();
        txtFalaIA.fontSize = 20.5f;
        txtFalaIA.alignment = TextAlignmentOptions.Center;
        txtFalaIA.color = Color.white;
        txtFalaIA.text = "EXO-BOT IA Inicializado";
        txtFalaIA.raycastTarget = false;

        // Texto das Métricas de Telemetria
        GameObject goTxtMetricas = new GameObject("Texto_Telemetria");
        goTxtMetricas.transform.SetParent(goFundo.transform, false);
        RectTransform rtMetricas = goTxtMetricas.AddComponent<RectTransform>();
        rtMetricas.anchorMin = new Vector2(0.07f, 0.16f);
        rtMetricas.anchorMax = new Vector2(0.93f, 0.44f);
        rtMetricas.offsetMin = Vector2.zero;
        rtMetricas.offsetMax = Vector2.zero;

        txtMetricasHUD = goTxtMetricas.AddComponent<TextMeshProUGUI>();
        txtMetricasHUD.fontSize = 16.0f;
        txtMetricasHUD.alignment = TextAlignmentOptions.Center;
        txtMetricasHUD.color = new Color(0.8f, 0.95f, 1f);
        txtMetricasHUD.text = "PASSOS: 0 | DISTÂNCIA: 0.0m | SIMETRIA: 100%";
        txtMetricasHUD.raycastTarget = false;

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
        imgBarraBg.raycastTarget = false;

        GameObject goBarraFill = new GameObject("Fill_Barra_Simetria");
        goBarraFill.transform.SetParent(goBarraFundo.transform, false);
        RectTransform rtBarraFill = goBarraFill.AddComponent<RectTransform>();
        rtBarraFill.anchorMin = Vector2.zero;
        rtBarraFill.anchorMax = Vector2.one;
        rtBarraFill.offsetMin = Vector2.zero;
        rtBarraFill.offsetMax = Vector2.zero;

        imgBarraSimetria = goBarraFill.AddComponent<Image>();
        imgBarraSimetria.color = corGuiandoNormal;
        imgBarraSimetria.raycastTarget = false;

        // 2. BARRA DE CONTROLE ESPACIAL (ESTILO META QUEST / STEAMVR HUB)
        GameObject goBarraQuest = new GameObject("Barra_Controle_Espacial");
        goBarraQuest.transform.SetParent(goCanvas.transform, false);
        RectTransform rtBarraQuest = goBarraQuest.AddComponent<RectTransform>();
        rtBarraQuest.anchorMin = new Vector2(0.02f, -0.20f);
        rtBarraQuest.anchorMax = new Vector2(0.98f, -0.02f);
        rtBarraQuest.offsetMin = Vector2.zero;
        rtBarraQuest.offsetMax = Vector2.zero;

        // Botão Esquerdo: [- Perto]
        CriarBotaoEspacialHUD(goBarraQuest.transform, "Btn_Mais_Perto", "- Perto",
            new Vector2(0.00f, 0.0f), new Vector2(0.17f, 1.0f), () => AjustarDistanciaDrone(-0.45f));

        // Barra Central de Pinça / Arraste (Handle Bar estilo Meta Quest)
        GameObject goHandle = new GameObject("Btn_Pinch_Arraste");
        goHandle.transform.SetParent(goBarraQuest.transform, false);
        RectTransform rtHandle = goHandle.AddComponent<RectTransform>();
        rtHandle.anchorMin = new Vector2(0.185f, 0.0f);
        rtHandle.anchorMax = new Vector2(0.655f, 1.0f);
        rtHandle.offsetMin = Vector2.zero;
        rtHandle.offsetMax = Vector2.zero;
        imgFundoBarraArraste = goHandle.AddComponent<Image>();
        imgFundoBarraArraste.color = new Color(0.05f, 0.16f, 0.26f, 0.88f);
        imgFundoBarraArraste.raycastTarget = true;
        AdicionarGatilhoArrasteUI(goHandle);

        GameObject goTxtHandle = new GameObject("Txt_Handle");
        goTxtHandle.transform.SetParent(goHandle.transform, false);
        RectTransform rtTxtHandle = goTxtHandle.AddComponent<RectTransform>();
        rtTxtHandle.anchorMin = Vector2.zero;
        rtTxtHandle.anchorMax = Vector2.one;
        rtTxtHandle.offsetMin = Vector2.zero;
        rtTxtHandle.offsetMax = Vector2.zero;
        txtBarraArraste = goTxtHandle.AddComponent<TextMeshProUGUI>();
        txtBarraArraste.fontSize = 11.5f;
        txtBarraArraste.fontStyle = FontStyles.Bold;
        txtBarraArraste.alignment = TextAlignmentOptions.Center;
        txtBarraArraste.color = Color.white;
        txtBarraArraste.text = "[+] PINÇAR / ARRASTAR PARA MOVER O DRONE";
        txtBarraArraste.raycastTarget = false;

        // Botão Direito 1: [+ Longe]
        CriarBotaoEspacialHUD(goBarraQuest.transform, "Btn_Mais_Longe", "+ Longe",
            new Vector2(0.67f, 0.0f), new Vector2(0.83f, 1.0f), () => AjustarDistanciaDrone(+0.45f));

        // Botão Direito 2: [Centro]
        CriarBotaoEspacialHUD(goBarraQuest.transform, "Btn_Reset_Centro", "Centro",
            new Vector2(0.845f, 0.0f), new Vector2(1.00f, 1.0f), () => ResetarPosicaoPadraoDrone());
    }

    private void GarantirComponentesInteracaoCanvas(GameObject goCanvas)
    {
        CanvasScaler cs = goCanvas.GetComponent<CanvasScaler>();
        if (cs == null) cs = goCanvas.AddComponent<CanvasScaler>();
        cs.dynamicPixelsPerUnit = 10f;

        if (goCanvas.GetComponent<GraphicRaycaster>() == null)
            goCanvas.AddComponent<GraphicRaycaster>();

        if (goCanvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            goCanvas.AddComponent<TrackedDeviceGraphicRaycaster>();
    }

    private void CriarBotaoEspacialHUD(Transform pai, string nome, string rotulo, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction acao)
    {
        GameObject goBtn = new GameObject(nome);
        goBtn.transform.SetParent(pai, false);
        RectTransform rt = goBtn.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = goBtn.AddComponent<Image>();
        img.color = new Color(0.08f, 0.26f, 0.38f, 0.92f);

        Button btn = goBtn.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.08f, 0.26f, 0.38f, 0.92f);
        cb.highlightedColor = new Color(0.0f, 0.78f, 0.92f, 1.0f);
        cb.pressedColor = new Color(0.0f, 0.98f, 0.85f, 1.0f);
        btn.colors = cb;
        btn.onClick.AddListener(acao);

        GameObject goTxt = new GameObject("Txt");
        goTxt.transform.SetParent(goBtn.transform, false);
        RectTransform rtTxt = goTxt.AddComponent<RectTransform>();
        rtTxt.anchorMin = Vector2.zero;
        rtTxt.anchorMax = Vector2.one;
        rtTxt.offsetMin = Vector2.zero;
        rtTxt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = goTxt.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 11.5f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.85f, 0.98f, 1.0f, 1.0f);
        tmp.text = rotulo;
        tmp.raycastTarget = false;
    }

    private void AdicionarGatilhoArrasteUI(GameObject alvoUI)
    {
        EventTrigger trigger = alvoUI.GetComponent<EventTrigger>();
        if (trigger == null) trigger = alvoUI.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        AdicionarEntradaTrigger(trigger, EventTriggerType.PointerEnter, (data) => DefinirHoverEspacial(true));
        AdicionarEntradaTrigger(trigger, EventTriggerType.PointerExit,  (data) => DefinirHoverEspacial(false));
        AdicionarEntradaTrigger(trigger, EventTriggerType.PointerDown,  (data) => IniciarArrasteViaUI((PointerEventData)data));
        AdicionarEntradaTrigger(trigger, EventTriggerType.BeginDrag,    (data) => IniciarArrasteViaUI((PointerEventData)data));
        AdicionarEntradaTrigger(trigger, EventTriggerType.EndDrag,      (data) => FinalizarArrasteEspacial());
        AdicionarEntradaTrigger(trigger, EventTriggerType.PointerUp,    (data) => FinalizarArrasteEspacial());
        AdicionarEntradaTrigger(trigger, EventTriggerType.Scroll,       (data) =>
        {
            PointerEventData ped = (PointerEventData)data;
            if (Mathf.Abs(ped.scrollDelta.y) > 0.01f)
                AjustarDistanciaDrone(ped.scrollDelta.y * 0.28f);
        });
    }

    private void AdicionarEntradaTrigger(EventTrigger trigger, EventTriggerType tipo, UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = tipo };
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
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

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }
}
