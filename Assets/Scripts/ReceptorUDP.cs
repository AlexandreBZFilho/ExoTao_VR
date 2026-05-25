using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Globalization;

public class ReceptorUDP : MonoBehaviour
{
    Thread receiveThread;
    UdpClient client;
    public int port = 5005;

    [Header("Articulações Perna Direita")]
    public Transform articulacaoQuadrilDir;
    public Transform articulacaoJoelhoDir;

    [Header("Articulações Perna Esquerda")]
    public Transform articulacaoQuadrilEsq;
    public Transform articulacaoJoelhoEsq;

    [Header("Configuração de Movimento")]
    public CharacterController controlePaciente;
    public float sensibilidadePasso = 0.1f;

    private float velocidadeVertical = 0f;

    private float anguloJoelhoDir = 0f, anguloQuadrilDir = 0f;
    private float anguloJoelhoEsq = 0f, anguloQuadrilEsq = 0f;

    void Start() { InitUDP(); }

    private void InitUDP()
    {
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    private void ReceiveData()
    {
        client = new UdpClient(port);
        while (true)
        {
            try
            {
                IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = client.Receive(ref anyIP);
                string text = Encoding.UTF8.GetString(data);

                string[] valores = text.Split(',');

                if (valores.Length >= 4)
                {
                    float.TryParse(valores[0], NumberStyles.Float, CultureInfo.InvariantCulture, out anguloJoelhoDir);
                    float.TryParse(valores[1], NumberStyles.Float, CultureInfo.InvariantCulture, out anguloQuadrilDir);
                    float.TryParse(valores[2], NumberStyles.Float, CultureInfo.InvariantCulture, out anguloJoelhoEsq);
                    float.TryParse(valores[3], NumberStyles.Float, CultureInfo.InvariantCulture, out anguloQuadrilEsq);
                }
            }
            catch (Exception err) { Debug.LogWarning(err.ToString()); }
        }
    }

    void Update()
    {
        if (articulacaoQuadrilDir) articulacaoQuadrilDir.localRotation = Quaternion.Euler(anguloQuadrilDir, 0, 0);
        if (articulacaoJoelhoDir) articulacaoJoelhoDir.localRotation = Quaternion.Euler(anguloJoelhoDir, 0, 0);

        if (articulacaoQuadrilEsq) articulacaoQuadrilEsq.localRotation = Quaternion.Euler(anguloQuadrilEsq, 0, 0);
        if (articulacaoJoelhoEsq) articulacaoJoelhoEsq.localRotation = Quaternion.Euler(anguloJoelhoEsq, 0, 0);

        MoverUsuario();
    }

    void MoverUsuario()
    {
        if (controlePaciente == null) return;

        if (controlePaciente.isGrounded)
        {
            velocidadeVertical = -2f;
        }
        else
        {
            velocidadeVertical += Physics.gravity.y * Time.deltaTime;
        }

        float avanço = (Mathf.Max(0, anguloQuadrilDir) + Mathf.Max(0, anguloQuadrilEsq)) / 2f;
        Vector3 movimento = Vector3.zero;

        if (avanço > 5f)
        {
            movimento = Vector3.forward * (avanço * sensibilidadePasso);
        }

        movimento.y = velocidadeVertical;

        controlePaciente.Move(movimento * Time.deltaTime);
    }

    void OnDestroy()
    {
        if (receiveThread != null) receiveThread.Abort();
        if (client != null) client.Close();
    }
}