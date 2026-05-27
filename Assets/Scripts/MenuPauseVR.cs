using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR; 

public class MenuPauseVR : MonoBehaviour
{
    [Header("O Canvas do Menu")]
    public GameObject painelMenu;

    [Header("Configuração do Botão (HTC Vive)")]
    public InputActionReference botaoMenuVR; 

    [Header("Configurações de Movimento (Lazy Follow)")]
    public float distanciaDoRosto = 1.5f;
    public float velocidadeDeSeguimento = 5f;
    [Tooltip("Valores negativos abaixam o menu. Ex: -0.3 abaixa 30cm")]
    public float ajusteDeAltura = -0.3f; // <-- VARIÁVEL NOVA AQUI

    void Start()
    {
        painelMenu.SetActive(false); 
        
        if (botaoMenuVR != null)
        {
            botaoMenuVR.action.Enable();
            botaoMenuVR.action.performed += AlternarMenu;
        }
    }

    void Update()
    {
        if (painelMenu.activeSelf)
        {
            Transform cameraVR = Camera.main.transform;
            
            // Calcula a posição na frente do rosto e abaixa no eixo Y
            Vector3 posicaoAlvo = cameraVR.position + cameraVR.forward * distanciaDoRosto;
            posicaoAlvo.y += ajusteDeAltura; // <-- APLICA O AJUSTE AQUI
            
            painelMenu.transform.position = Vector3.Lerp(painelMenu.transform.position, posicaoAlvo, Time.deltaTime * velocidadeDeSeguimento);
            
            Quaternion rotacaoAlvo = Quaternion.LookRotation(painelMenu.transform.position - cameraVR.position);
            painelMenu.transform.rotation = Quaternion.Slerp(painelMenu.transform.rotation, rotacaoAlvo, Time.deltaTime * velocidadeDeSeguimento);
        }
    }

    private void AlternarMenu(InputAction.CallbackContext context)
    {
        bool estaLigado = painelMenu.activeSelf;
        painelMenu.SetActive(!estaLigado);

        if (!estaLigado) 
        {
            Transform cameraVR = Camera.main.transform;
            
            // Aplica o mesmo ajuste na hora que o menu liga pela primeira vez
            Vector3 posicaoInicial = cameraVR.position + cameraVR.forward * distanciaDoRosto;
            posicaoInicial.y += ajusteDeAltura; // <-- APLICA O AJUSTE AQUI TAMBÉM
            
            painelMenu.transform.position = posicaoInicial;
            painelMenu.transform.rotation = Quaternion.LookRotation(painelMenu.transform.position - cameraVR.position);
        }
    }

    // --- FUNCIONALIDADES DOS BOTÕES ---

    public void ContinuarSessao()
    {
        painelMenu.SetActive(false);
    }

    public void RecentralizarVR()
    {
        var inputSubsystems = new System.Collections.Generic.List<XRInputSubsystem>();
        SubsystemManager.GetInstances<XRInputSubsystem>(inputSubsystems);
        foreach (var subsystem in inputSubsystems)
        {
            subsystem.TryRecenter();
        }
        ContinuarSessao();
    }

    public void VoltarAoMenuPrincipal()
    {
        SceneManager.LoadScene("Menu_Principal"); 
    }

    public void BotaoPausarExo()
    {
        Debug.Log("Pausando Exoesqueleto.");
    }
}