using UnityEngine;
using UnityEngine.SceneManagement;

public class GerenciadorGlobal : MonoBehaviour
{
    // Variáveis estáticas ficam salvas na memória do PC mesmo trocando de cena
    public static string ModoReabilitacao = "Integrado"; // "Joelho", "Quadril" ou "Integrado"
    public static string PersonagemSelecionado = "YBot";

    // Função para selecionar o modo (os botões vão chamar isso)
    public void SelecionarModo(string novoModo)
    {
        ModoReabilitacao = novoModo;
        Debug.Log("Modo definido para: " + ModoReabilitacao);
    }

    // Função para carregar o mapa
    public void CarregarMapa(string nomeDaCena)
    {
        SceneManager.LoadScene(nomeDaCena);
    }
}