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
    public int port = 5005; // Porta padrão que usaremos para escutar

    // Variável para armazenar o ângulo recebido (thread-safe)
    private float anguloJoelho = 0f;

    void Start()
    {
        InitUDP();
    }

    private void InitUDP()
    {
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
        Debug.Log("Iniciando escuta UDP na porta " + port);
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

                // Agora ele entende que o ponto separa as casas decimais
                if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float angulo))
                {
                    anguloJoelho = angulo;
                }
            }
            catch (Exception err)
            {
                print(err.ToString());
            }
        }
    }

    void Update()
    {
        // Aplica o ângulo no eixo X do cilindro a cada frame da Unity
        transform.localRotation = Quaternion.Euler(anguloJoelho, 0, 0);
    }

    void OnApplicationQuit()
    {
        // Limpeza de memória importantíssima quando fechar o jogo
        if (receiveThread != null) receiveThread.Abort();
        if (client != null) client.Close();
    }
}