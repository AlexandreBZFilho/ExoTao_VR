using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Globalization;

public class ReceptorUDP : MonoBehaviour
{
    [Header("Configuração de Rede")]
    public int portaUDP = 5005;
    public int portaTCP = 1515;

    [Header("Ossos do Mixamo (Y Bot)")]
    public Transform articulacaoQuadrilDir;
    public Transform articulacaoJoelhoDir;
    public Transform articulacaoQuadrilEsq;
    public Transform articulacaoJoelhoEsq;

    [Header("Configuração de Movimento")]
    public CharacterController controlePaciente;
    public float sensibilidadePasso = 0.1f;
    [Tooltip("Suavização da rotação dos ossos para evitar tremores em 90Hz VR")]
    public float suavizacaoOssos = 18f;

    [Header("Modo de Teste Rápido (Sem Robô)")]
    [Tooltip("Pressione F9 durante o jogo para ativar/desativar caminhada simulada automática")]
    public bool simuladorInternoAtivo = false;
    public float velocidadeSimuladorInterno = 2.2f;

    // Referência ao controlador biomecânico 3D completo (Y Bot + Exoesqueleto)
    [HideInInspector]
    public AvatarBiomecanicoVR avatarBiomecanico;

    // Rede (UDP + TCP simultâneos para aceitar tanto o simulador_robo.py quanto o ReRobApp)
    private UdpClient udpClient;
    private TcpListener tcpListener;
    private Thread receiveThreadUDP;
    private Thread receiveThreadTCP;
    private volatile bool aExecutar = true;
    private readonly object lockDados = new object();

    // Variáveis de Movimento Internas
    private float velocidadeVertical = 0f;
    private float anguloJoelhoDir = 0f, anguloQuadrilDir = 0f;
    private float anguloJoelhoEsq = 0f, anguloQuadrilEsq = 0f;
    private float tempoSimuladorInterno = 0f;

    // Rotações iniciais de repouso caso operado sem AvatarBiomecanicoVR (ex: cilindros de teste)
    private Quaternion rotBaseQuadDir = Quaternion.identity;
    private Quaternion rotBaseJoelhoDir = Quaternion.identity;
    private Quaternion rotBaseQuadEsq = Quaternion.identity;
    private Quaternion rotBaseJoelhoEsq = Quaternion.identity;
    private bool rotBaseCapturada = false;

    // Propriedades Públicas para Telemetria Clínica, Avatar 3D e Drone Guia IA
    public float AnguloJoelhoDir { get; private set; }
    public float AnguloQuadrilDir { get; private set; }
    public float AnguloJoelhoEsq { get; private set; }
    public float AnguloQuadrilEsq { get; private set; }
    public float VelocidadeHorizontalAtual { get; private set; }
    public bool PacienteCaminhando => VelocidadeHorizontalAtual > 0.08f || Mathf.Abs(AnguloQuadrilDir) > 6f || Mathf.Abs(AnguloQuadrilEsq) > 6f;
    public bool Pausado { get; set; } = false;
    public string FonteDeDadosAtiva { get; private set; } = "Aguardando...";

    void Start()
    {
        if (controlePaciente == null)
        {
            controlePaciente = GetComponent<CharacterController>();
            if (controlePaciente == null)
                controlePaciente = GetComponentInParent<CharacterController>();
        }

        // Conecta ou inicializa o AvatarBiomecanicoVR (Y Bot 3D + Exoesqueleto) se estivermos no jogador
        avatarBiomecanico = GetComponent<AvatarBiomecanicoVR>();
        if (avatarBiomecanico == null)
            avatarBiomecanico = GetComponentInParent<AvatarBiomecanicoVR>();
        if (avatarBiomecanico == null)
            avatarBiomecanico = GetComponentInChildren<AvatarBiomecanicoVR>();

        if (avatarBiomecanico == null && controlePaciente != null)
        {
            avatarBiomecanico = controlePaciente.gameObject.AddComponent<AvatarBiomecanicoVR>();
            avatarBiomecanico.receptorExo = this;
            avatarBiomecanico.InicializarAvatarECinematica();
        }

        CapturarRotacoesBase();

        aExecutar = true;

        // Inicia escuta UDP (Porta 5005 - simulador_robo.py / câmera)
        receiveThreadUDP = new Thread(new ThreadStart(ReceiveDataUDP));
        receiveThreadUDP.IsBackground = true;
        receiveThreadUDP.Start();

        // Inicia escuta TCP (Porta 1515 - ReRobApp / Exoesqueleto ExoTao)
        receiveThreadTCP = new Thread(new ThreadStart(ReceiveDataTCP));
        receiveThreadTCP.IsBackground = true;
        receiveThreadTCP.Start();

        Debug.Log($"[ExoTao Receptor] Ouvindo simultaneamente UDP (porta {portaUDP}) e TCP (porta {portaTCP}). Pressione F9 para Simulador Interno.");
    }

    private void CapturarRotacoesBase()
    {
        if (rotBaseCapturada) return;
        if (articulacaoQuadrilDir != null) rotBaseQuadDir = articulacaoQuadrilDir.localRotation;
        if (articulacaoJoelhoDir != null) rotBaseJoelhoDir = articulacaoJoelhoDir.localRotation;
        if (articulacaoQuadrilEsq != null) rotBaseQuadEsq = articulacaoQuadrilEsq.localRotation;
        if (articulacaoJoelhoEsq != null) rotBaseJoelhoEsq = articulacaoJoelhoEsq.localRotation;
        rotBaseCapturada = true;
    }

    private void ReceiveDataUDP()
    {
        try
        {
            udpClient = new UdpClient(portaUDP);
            IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);

            while (aExecutar)
            {
                try
                {
                    byte[] data = udpClient.Receive(ref anyIP);
                    string text = Encoding.UTF8.GetString(data);
                    string[] valores = text.Split(',');

                    if (valores.Length >= 4)
                    {
                        float.TryParse(valores[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float jd);
                        float.TryParse(valores[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float qd);
                        float.TryParse(valores[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float je);
                        float.TryParse(valores[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float qe);

                        lock (lockDados)
                        {
                            anguloJoelhoDir = jd;
                            anguloQuadrilDir = qd;
                            anguloJoelhoEsq = je;
                            anguloQuadrilEsq = qe;
                            FonteDeDadosAtiva = "UDP (Simulador/Câmera)";
                        }
                    }
                }
                catch (SocketException)
                {
                    break;
                }
                catch (Exception err)
                {
                    if (aExecutar) Debug.LogWarning("Erro UDP: " + err.Message);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ExoTao Receptor] Porta UDP {portaUDP} já em uso ou indisponível: {e.Message}");
        }
    }

    private void ReceiveDataTCP()
    {
        try
        {
            tcpListener = new TcpListener(IPAddress.Any, portaTCP);
            tcpListener.Start();
        }
        catch (Exception e)
        {
            Debug.LogWarning("Não foi possível iniciar o servidor TCP: " + e.Message);
            return;
        }

        while (aExecutar)
        {
            try
            {
                if (!tcpListener.Pending())
                {
                    Thread.Sleep(50);
                    continue;
                }

                using (TcpClient client = tcpListener.AcceptTcpClient())
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] buffer = new byte[1024];
                    while (aExecutar && client.Connected)
                    {
                        if (stream.DataAvailable)
                        {
                            int bytesRead = stream.Read(buffer, 0, buffer.Length);
                            if (bytesRead > 0)
                            {
                                string text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                                string[] linhas = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                string ultimaMensagem = linhas.Length > 0 ? linhas[linhas.Length - 1] : text;
                                string[] valores = ultimaMensagem.Split(',');

                                lock (lockDados)
                                {
                                    if (valores.Length >= 4)
                                    {
                                        float.TryParse(valores[0], NumberStyles.Float, CultureInfo.InvariantCulture, out anguloJoelhoDir);
                                        float.TryParse(valores[1], NumberStyles.Float, CultureInfo.InvariantCulture, out anguloQuadrilDir);
                                        float.TryParse(valores[2], NumberStyles.Float, CultureInfo.InvariantCulture, out anguloJoelhoEsq);
                                        float.TryParse(valores[3], NumberStyles.Float, CultureInfo.InvariantCulture, out anguloQuadrilEsq);
                                    }
                                    else if (valores.Length == 3)
                                    {
                                        float.TryParse(valores[1], NumberStyles.Float, CultureInfo.InvariantCulture, out anguloJoelhoDir);
                                        float.TryParse(valores[2], NumberStyles.Float, CultureInfo.InvariantCulture, out anguloQuadrilDir);
                                        anguloJoelhoEsq = 0f;
                                        anguloQuadrilEsq = 0f;
                                    }
                                    FonteDeDadosAtiva = "TCP (ExoTao ReRobApp)";
                                }
                            }
                        }
                        else
                        {
                            Thread.Sleep(10);
                        }
                    }
                }
            }
            catch (SocketException)
            {
                break;
            }
            catch (Exception err)
            {
                if (aExecutar) Debug.LogWarning("Erro TCP: " + err.Message);
            }
        }
    }

    void Update()
    {
        // Atalho F9 para ligar/desligar simulador interno de marcha caso queira testar sem abrir o terminal Python
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.f9Key.wasPressedThisFrame)
        {
            simuladorInternoAtivo = !simuladorInternoAtivo;
            Debug.Log("[ExoTao Receptor] Simulador Interno de Marcha: " + (simuladorInternoAtivo ? "ATIVADO" : "DESATIVADO"));
        }

        if (Pausado)
        {
            VelocidadeHorizontalAtual = 0f;
            return;
        }

        if (simuladorInternoAtivo)
        {
            tempoSimuladorInterno += Time.deltaTime * velocidadeSimuladorInterno;
            lock (lockDados)
            {
                anguloQuadrilDir = 25f * Mathf.Sin(tempoSimuladorInterno);
                anguloJoelhoDir = -(30f * Mathf.Sin(tempoSimuladorInterno - 1.5f) + 30f);
                anguloQuadrilEsq = 25f * Mathf.Sin(tempoSimuladorInterno + Mathf.PI);
                anguloJoelhoEsq = -(30f * Mathf.Sin(tempoSimuladorInterno + Mathf.PI - 1.5f) + 30f);
                FonteDeDadosAtiva = "Simulador Interno (F9)";
            }
        }

        // Copia thread-safe dos ângulos atuais
        lock (lockDados)
        {
            AnguloQuadrilDir = anguloQuadrilDir;
            AnguloJoelhoDir = anguloJoelhoDir;
            AnguloQuadrilEsq = anguloQuadrilEsq;
            AnguloJoelhoEsq = anguloJoelhoEsq;
        }

        // 1. Se o AvatarBiomecanicoVR não estiver gerenciando o Y Bot (ex: objeto de teste isolado), aplica rotação relativa à base
        if (avatarBiomecanico == null)
        {
            CapturarRotacoesBase();
            float fatorSlerp = suavizacaoOssos > 0f ? Mathf.Clamp01(Time.deltaTime * suavizacaoOssos) : 1f;

            if (articulacaoQuadrilDir)
                articulacaoQuadrilDir.localRotation = Quaternion.Slerp(articulacaoQuadrilDir.localRotation, rotBaseQuadDir * Quaternion.Euler(AnguloQuadrilDir, 0, 0), fatorSlerp);
            if (articulacaoJoelhoDir)
                articulacaoJoelhoDir.localRotation = Quaternion.Slerp(articulacaoJoelhoDir.localRotation, rotBaseJoelhoDir * Quaternion.Euler(-Mathf.Abs(AnguloJoelhoDir), 0, 0), fatorSlerp);

            if (articulacaoQuadrilEsq)
                articulacaoQuadrilEsq.localRotation = Quaternion.Slerp(articulacaoQuadrilEsq.localRotation, rotBaseQuadEsq * Quaternion.Euler(AnguloQuadrilEsq, 0, 0), fatorSlerp);
            if (articulacaoJoelhoEsq)
                articulacaoJoelhoEsq.localRotation = Quaternion.Slerp(articulacaoJoelhoEsq.localRotation, rotBaseJoelhoEsq * Quaternion.Euler(-Mathf.Abs(AnguloJoelhoEsq), 0, 0), fatorSlerp);
        }

        // 2. Calcula a locomoção física no mapa
        MoverUsuario();
    }

    void MoverUsuario()
    {
        if (controlePaciente == null)
        {
            VelocidadeHorizontalAtual = 0f;
            return;
        }

        // No Menu Principal ou Tela de Loading, permite testar a cinemática 3D das pernas no lugar sem deslocar o jogador para longe do menu
        string cenaAtual = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (cenaAtual == "Menu_Principal" || cenaAtual == "CenaDeLoading")
        {
            VelocidadeHorizontalAtual = 0f;
            return;
        }

        // Gravidade
        if (controlePaciente.isGrounded)
        {
            velocidadeVertical = -2f;
        }
        else
        {
            velocidadeVertical += Physics.gravity.y * Time.deltaTime;
        }

        // Calcula a força do passo baseado na elevação do quadril
        float avanco = (Mathf.Max(0, AnguloQuadrilDir) + Mathf.Max(0, AnguloQuadrilEsq)) / 2f;
        Vector3 movimento = Vector3.zero;

        if (avanco > 5f)
        {
            // Usa a direção horizontal da câmera VR se disponível (respeitando SnapTurn e giro da cabeça), ou transform.forward
            Vector3 dirFrente = transform.forward;
            if (avatarBiomecanico != null && avatarBiomecanico.cameraVR != null)
            {
                Vector3 frenteCam = Vector3.ProjectOnPlane(avatarBiomecanico.cameraVR.forward, Vector3.up);
                if (frenteCam.sqrMagnitude > 0.01f)
                    dirFrente = frenteCam.normalized;
            }

            movimento = dirFrente * (avanco * sensibilidadePasso);
        }

        VelocidadeHorizontalAtual = new Vector3(movimento.x, 0f, movimento.z).magnitude;

        // Aplica a gravidade no vetor final
        movimento.y = velocidadeVertical;

        // Move o CharacterController
        controlePaciente.Move(movimento * Time.deltaTime);
    }

    void OnDestroy()
    {
        EncerrarConexoes();
    }

    void OnApplicationQuit()
    {
        EncerrarConexoes();
    }

    private void EncerrarConexoes()
    {
        aExecutar = false;
        try { if (udpClient != null) udpClient.Close(); } catch { }
        try { if (tcpListener != null) tcpListener.Stop(); } catch { }
    }
}
