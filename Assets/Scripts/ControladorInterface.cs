using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class ControladorInterface : MonoBehaviour
{
    [Header("Painéis Principais")]
    public GameObject menuPrincipal;
    public GameObject subMenuPersonagens;
    public GameObject subMenuMapas;   // A galeria cheia de botões
    public GameObject menuMapas;      // A tela que mostra o mapa atual selecionado
    public GameObject menuPersonagens;

    [Header("Dados da Sessão (Coleta para o Exoesqueleto)")]
    public TMP_Dropdown dropdownModo;
    public string mapaSelecionado = "Floresta"; // Guarda o nome para carregar a cena depois

    [Header("Elementos Visuais do Mapa Atual")]
    public RawImage imagemDoMenuMapas;
    public TextMeshProUGUI tituloDoMenuMapas;
    public TextMeshProUGUI descricaoDoMenuMapas;

    [Header("Banco de Dados dos Mapas")]
    public DadosDoMapa[] mapasDisponiveis;

    // --- NAVEGAÇÃO BÁSICA ---

    // Chamado pelo botão "Selecionar Personagem"
    public void IrParaPersonagens() {
        menuPrincipal.SetActive(true);
        subMenuPersonagens.SetActive(true);
        menuPersonagens.SetActive(false);
    }

    // Chamado pelo botão "Selecionar Mapas"
    public void IrParaMapas() {
        menuPrincipal.SetActive(true);
        subMenuMapas.SetActive(true); // Abre a Galeria
        menuMapas.SetActive(false);   // Esconde a tela de exibição
    }

    // Botão de Voltar (Seta lá em cima)
    public void VoltarAoMenu() {
        menuPrincipal.SetActive(true);
        subMenuPersonagens.SetActive(false);
        subMenuMapas.SetActive(false); // Fecha a Galeria
        menuMapas.SetActive(true);     // Mostra a tela de exibição novamente
        menuPersonagens.SetActive(true);
    }

    // --- LÓGICA DE ESCOLHA DE MAPA ---

    // Chamado quando você clica em um Card específico na Galeria
    public void SelecionarMapaNaGaleria(int indexDoMapa)
    {
        if (indexDoMapa >= 0 && indexDoMapa < mapasDisponiveis.Length)
        {
            // 1. Atualiza os dados visuais na tela
            imagemDoMenuMapas.texture = mapasDisponiveis[indexDoMapa].imagem;
            tituloDoMenuMapas.text = mapasDisponiveis[indexDoMapa].titulo;
            descricaoDoMenuMapas.text = mapasDisponiveis[indexDoMapa].descricao;

            // 2. Salva o nome interno para usar no SceneManager depois
            mapaSelecionado = mapasDisponiveis[indexDoMapa].nomeDaCena;

            // 3. Volta para a tela anterior automaticamente
            VoltarAoMenu(); 
        }
        else
        {
            Debug.LogWarning("Mapa não encontrado no array!");
        }
    }

    // --- START DA REABILITAÇÃO ---

    // Botão INICIAR SESSÃO
    public void IniciarSessao() {
        string modo = dropdownModo.options[dropdownModo.value].text;
        Debug.Log("Iniciando reabilitação no modo: " + modo + " no mapa: " + mapaSelecionado);
        SceneManager.LoadScene(mapaSelecionado);
    }
}

// Classe de dados para organizar a lista no Inspector
[System.Serializable]
public class DadosDoMapa
{
    public string titulo; // Título bonito para o usuário (ex: "Floresta e Rio")
    public string nomeDaCena; // Nome EXATO da cena na Unity (ex: "CenaFlorestaVR")
    [TextArea(3, 6)] 
    public string descricao;
    public Texture2D imagem;
}