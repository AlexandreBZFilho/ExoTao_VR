using UnityEngine;
using UnityEngine.XR;

public class ControleTesteTouchpad : MonoBehaviour
{
    [Header("Configuração de Movimento")]
    public CharacterController controlePaciente;
    public float velocidadeMovimento = 2.5f;
    
    [Header("Referências de VR")]
    public Transform cameraDoJogador; // Arraste a Main Camera (cabeça) aqui no Inspector
    public XRNode maoDoControle = XRNode.RightHand; 

    private Vector2 toqueTouchpad;
    private float velocidadeVertical = 0f;

    void Start()
    {
        // Se você esquecer de arrastar a câmera no Inspector, o script tenta achar a Main Camera sozinho
        if (cameraDoJogador == null && Camera.main != null)
        {
            cameraDoJogador = Camera.main.transform;
        }
    }

    void Update()
    {
        // 1. Pega o controle do VR
        InputDevice controleVR = InputDevices.GetDeviceAtXRNode(maoDoControle);
        controleVR.TryGetFeatureValue(CommonUsages.primary2DAxis, out toqueTouchpad);

        if (controlePaciente == null || cameraDoJogador == null) return;

        // 2. Sistema de Gravidade
        if (controlePaciente.isGrounded)
        {
            velocidadeVertical = -2f;
        }
        else
        {
            velocidadeVertical += Physics.gravity.y * Time.deltaTime;
        }

        // 3. Pega a direção exata para onde a CÂMERA (cabeça) está olhando
        Vector3 direcaoFrenteCam = cameraDoJogador.forward;
        Vector3 direcaoLadoCam = cameraDoJogador.right;

        // 4. CORREÇÃO DE VOO: Zera o Y para impedir o boneco de voar ao olhar para cima ou afundar ao olhar para baixo
        direcaoFrenteCam.y = 0;
        direcaoLadoCam.y = 0;

        // Como alteramos o Y, precisamos normalizar os vetores para eles não perderem a força padrão de movimento
        direcaoFrenteCam.Normalize();
        direcaoLadoCam.Normalize();

        // 5. Calcula o movimento omnidirecional RELATIVO AO OLHAR
        Vector3 direcaoMovimento = (direcaoFrenteCam * toqueTouchpad.y) + (direcaoLadoCam * toqueTouchpad.x);
        
        if (direcaoMovimento.magnitude > 1f)
        {
            direcaoMovimento.Normalize();
        }

        Vector3 movimentoFinal = direcaoMovimento * velocidadeMovimento;
        movimentoFinal.y = velocidadeVertical;

        // 6. Executa o movimento físico na cidade
        controlePaciente.Move(movimentoFinal * Time.deltaTime);
    }
}