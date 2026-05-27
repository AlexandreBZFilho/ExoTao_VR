using UnityEngine;
using UnityEngine.InputSystem; // Para ler o botão do controle do HTC Vive
using UnityEngine.SceneManagement; // Para trocar de cena

public class MenuPauseVR : MonoBehaviour
{
    [Header("O Canvas do Menu")]
    public GameObject painelMenu;

    [Header("Configuração do Botão (HTC Vive)")]
    // Aqui você vai linkar qual botão do controle abre o menu
    public InputActionReference botaoMenuVR; 

    void Start()
    {
        // Garante que o menu comece desligado
        painelMenu.SetActive(false); 
        
        // Ativa a leitura do botão e avisa o que fazer quando for apertado
        botaoMenuVR.action.Enable();
        botaoMenuVR.action.performed += AlternarMenu;
    }

    private void AlternarMenu(InputAction.CallbackContext context)
    {
        // Inverte o estado: se tá ligado, desliga. Se tá desligado, liga.
        bool estaLigado = painelMenu.activeSelf;
        painelMenu.SetActive(!estaLigado);

        // Se o menu acabou de ligar, joga ele na frente do rosto do paciente
        if (!estaLigado) 
        {
            PosicionarMenuNaFrente();
        }
    }

    private void PosicionarMenuNaFrente()
    {
        // Pega a posição da câmera (cabeça do paciente)
        Transform cameraVR = Camera.main.transform;
        
        // Coloca o menu 1.5 metros pra frente na direção que ele está olhando
        painelMenu.transform.position = cameraVR.position + cameraVR.forward * 1.5f;
        
        // Faz o menu girar para "encarar" a câmera
        painelMenu.transform.rotation = Quaternion.LookRotation(painelMenu.transform.position - cameraVR.position);
    }

    // --- FUNÇÕES DOS BOTÕES ---

    public void FecharMenu()
    {
        painelMenu.SetActive(false);
    }

    public void SairDoMapa()
    {
        // Substitua pelo nome EXATO da cena do seu menu principal!
        SceneManager.LoadScene("Menu Principal"); 
    }
}