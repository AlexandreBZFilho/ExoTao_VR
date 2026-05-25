using UnityEngine;
using UnityEngine.SceneManagement; // Essencial para trocar de cenas

public class GerenciadorMenu : MonoBehaviour
{
    // Esta função será chamada pelo botão
    public void AbrirFloresta() 
    {
        Debug.Log("Teletransportando para a Floresta...");
        
        // Substitua o nome entre aspas pelo nome EXATO da sua cena da floresta
        // (Olhe na sua aba Project como ela se chama, ex: "SimpleNaturePack_Demo")
        SceneManager.LoadScene("SimpleNaturePack_Demo"); 
    }

    public void SairDoJogo()
    {
        Application.Quit();
        Debug.Log("O jogo fecharia agora.");
    }
}