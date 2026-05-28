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

    [Header("Ossos do Mixamo")]
    public Transform articulacaoQuadrilDir;
    public Transform articulacaoJoelhoDir;
    public Transform articulacaoQuadrilEsq;
    public Transform articulacaoJoelhoEsq;

    [Header("Configuração de Movimento")]
    public CharacterController controlePaciente;
    public float sensibilidadePasso = 0.1f;

    // Rede
    private UdpClient udpClient;
    private Thread receiveThread;
    private bool aExecutar = true;

    // Variáveis de Movimento
    private float velocidadeVertical = 0f;
    private float anguloJoelhoDir = 0f, anguloQuadrilDir = 0f;
    private float anguloJoelhoEsq = 0f, anguloQuadrilEsq = 0f;

    void Start()
    {
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
        Debug.Log("Ouvindo Exoesqueleto na porta: " + portaUDP);
    }

    private void ReceiveData()
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
                    // InvariantCulture salva vidas aqui!
                    float.TryParse(valores[0], NumberStyles.Float, CultureInfo.InvariantCulture, out anguloJoelhoDir);
                    float.TryParse(valores[1], NumberStyles.Float, CultureInfo.InvariantCulture, out anguloQuadrilDir);
                    float.TryParse(valores[2], NumberStyles.Float, CultureInfo.InvariantCulture, out anguloJoelhoEsq);
                    float.TryParse(valores[3], NumberStyles.Float, CultureInfo.InvariantCulture, out anguloQuadrilEsq);
                }
            }
            catch (Exception err)
            {
                if (aExecutar) Debug.LogWarning("Erro UDP: " + err.ToString());
            }
        }
    }

    void Update()
    {
        // 1. Aplica a rotação nos ossos (Cinemática Direta)
        if (articulacaoQuadrilDir) articulacaoQuadrilDir.localRotation = Quaternion.Euler(anguloQuadrilDir, 0, 0);
        if (articulacaoJoelhoDir) articulacaoJoelhoDir.localRotation = Quaternion.Euler(anguloJoelhoDir, 0, 0);
        
        if (articulacaoQuadrilEsq) articulacaoQuadrilEsq.localRotation = Quaternion.Euler(anguloQuadrilEsq, 0, 0);
        if (articulacaoJoelhoEsq) articulacaoJoelhoEsq.localRotation = Quaternion.Euler(anguloJoelhoEsq, 0, 0);

        // 2. Calcula a locomoção física no mapa
        MoverUsuario();
    }

    void MoverUsuario()
    {
        if (controlePaciente == null) return;

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
        float avanço = (Mathf.Max(0, anguloQuadrilDir) + Mathf.Max(0, anguloQuadrilEsq)) / 2f;
        Vector3 movimento = Vector3.zero;

        if (avanço > 5f)
        {
            // UPGRADE VR: Usa transform.forward em vez de Vector3.forward para respeitar a rotação do corpo
            movimento = transform.forward * (avanço * sensibilidadePasso);
        }

        // Aplica a gravidade no vetor final
        movimento.y = velocidadeVertical;

        // Move o CharacterController
        controlePaciente.Move(movimento * Time.deltaTime);
    }

    void OnDestroy()
    {
        aExecutar = false;
        if (receiveThread != null) receiveThread.Abort();
        if (udpClient != null) udpClient.Close();
    }
}