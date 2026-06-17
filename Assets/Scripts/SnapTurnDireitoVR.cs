using UnityEngine;
using UnityEngine.XR;

public class SnapTurnDireitoVR : MonoBehaviour
{
    [Header("Referências")]
    public Transform xrOrigin; // O objeto "pai" que contém todo o jogador
    public Transform cameraDoJogador; // A cabeça do jogador
    
    [Header("Configuração")]
    public float anguloSnap = 45f;
    public XRNode maoDoControle = XRNode.RightHand; // Travado na mão direita

    private Vector2 toqueTouchpad;
    private bool jaGirou = false;

    void Update()
    {
        InputDevice controleVR = InputDevices.GetDeviceAtXRNode(maoDoControle);
        controleVR.TryGetFeatureValue(CommonUsages.primary2DAxis, out toqueTouchpad);

        if (xrOrigin == null || cameraDoJogador == null) return;

        // Se empurrou o Touchpad para a DIREITA
        if (toqueTouchpad.x > 0.6f && !jaGirou)
        {
            Girar(anguloSnap);
            jaGirou = true;
        }
        // Se empurrou o Touchpad para a ESQUERDA
        else if (toqueTouchpad.x < -0.6f && !jaGirou)
        {
            Girar(-anguloSnap);
            jaGirou = true;
        }
        // Quando o dedo volta para o centro, destrava para permitir um novo giro
        else if (toqueTouchpad.x > -0.3f && toqueTouchpad.x < 0.3f)
        {
            jaGirou = false;
        }
    }

    private void Girar(float angulo)
    {
        // Gira o XR Origin inteiro, usando a posição da Câmera como eixo central.
        // Isso impede que o avatar dê um "passo para o lado" indesejado ao girar.
        xrOrigin.RotateAround(cameraDoJogador.position, Vector3.up, angulo);
    }
}