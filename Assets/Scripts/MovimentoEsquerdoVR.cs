using UnityEngine;
using UnityEngine.XR;

public class MovimentoEsquerdoVR : MonoBehaviour
{
    [Header("Configuração de Movimento")]
    public CharacterController controlePaciente;
    public float velocidadeMovimento = 1.2f; // Velocidade de caminhada segura
    
    [Header("Referências")]
    public Transform xrOrigin; // <-- MUDANÇA AQUI: Agora usamos o corpo, não a cabeça!
    public XRNode maoDoControle = XRNode.LeftHand; // Travado na mão esquerda

    private Vector2 toqueTouchpad;
    private float velocidadeVertical = 0f;

    void Update()
    {
        InputDevice controleVR = InputDevices.GetDeviceAtXRNode(maoDoControle);
        controleVR.TryGetFeatureValue(CommonUsages.primary2DAxis, out toqueTouchpad);

        if (controlePaciente == null || xrOrigin == null) return;

        // 1. Sistema de Gravidade
        if (controlePaciente.isGrounded)
            velocidadeVertical = -2f;
        else
            velocidadeVertical += Physics.gravity.y * Time.deltaTime;

        // 2. Puxa a direção do CORPO (XR Origin), ignorando totalmente para onde a cabeça olha
        Vector3 direcaoFrente = xrOrigin.forward;
        direcaoFrente.y = 0;
        direcaoFrente.Normalize();

        // 3. Movimento Linear (Frente/Trás)
        Vector3 direcaoMovimento = direcaoFrente * toqueTouchpad.y;

        Vector3 movimentoFinal = direcaoMovimento * velocidadeMovimento;
        movimentoFinal.y = velocidadeVertical;

        // 4. Executa o movimento físico
        controlePaciente.Move(movimentoFinal * Time.deltaTime);
    }
}