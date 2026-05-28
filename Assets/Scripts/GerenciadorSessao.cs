using UnityEngine;

public class GerenciadorSessao : MonoBehaviour
{
    // Cria uma instância global que pode ser acessada de qualquer script
    public static GerenciadorSessao Instancia;

    [Header("Dados da Sessão")]
    public int indicePersonagemEscolhido = 0;
    public int indiceMapaEscolhido = 0;

    void Awake()
    {
        // Se já existe um Gerenciador, destrói o novo para não clonar
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        // Se é o primeiro, avisa a Unity para NUNCA destruir esse objeto
        Instancia = this;
        DontDestroyOnLoad(gameObject);
    }

    // Funções para os botões da UI chamarem
    public void SelecionarPersonagem(int indice)
    {
        indicePersonagemEscolhido = indice;
        Debug.Log("Personagem Selecionado: " + indice);
    }

    public void SelecionarMapa(int indice)
    {
        indiceMapaEscolhido = indice;
        Debug.Log("Mapa Selecionado: " + indice);
    }
}
