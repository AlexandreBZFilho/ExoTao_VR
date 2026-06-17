using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class ControladorInterface : MonoBehaviour
{
    [Header("Painéis de Personagens")]
    public GameObject menuPersonagens;
    public GameObject subMenuPersonagens;

    [Header("Lado Estático (Esquerda)")]
    public GameObject pnl_Capa_Est;       // 1. Selecionar o Modo
    public GameObject pnl_MenuMapa_Est;   // 2. O painel com a foto grande e botão de abrir galeria
    public GameObject pnl_Grade_Est;      // 3. A Galeria com os botões menores

    [Header("Elementos Visuais - Estático")]
    public RawImage img_MenuMapa_Est;
    public TextMeshProUGUI txt_Titulo_Est;
    public TextMeshProUGUI txt_Desc_Est;

    [Header("Lado Explorável (Direita)")]
    public GameObject pnl_Capa_Exp;       // 1. Selecionar o Modo
    public GameObject pnl_MenuMapa_Exp;   // 2. O painel com a foto grande e botão de abrir galeria
    public GameObject pnl_Grade_Exp;      // 3. A Galeria com os botões menores

    [Header("Elementos Visuais - Explorável")]
    public RawImage img_MenuMapa_Exp;
    public TextMeshProUGUI txt_Titulo_Exp;
    public TextMeshProUGUI txt_Desc_Exp;

    [Header("Dados da Sessão")]
    public TMP_Dropdown dropdownModo;
    public string mapaSelecionado = ""; 

    [Header("Bancos de Dados")]
    public DadosDoMapa[] mapasEstaticos;
    public DadosDoMapa[] mapasExploraveis;

    void Start()
    {
        // Começa o jogo mostrando apenas as Capas
        ResetarPaineisDeMapa();
        
        // Opcional: Carrega o primeiro mapa como padrão só para a tela não ficar vazia
        if (mapasEstaticos.Length > 0) CarregarDadosEstatico(0);
        if (mapasExploraveis.Length > 0) CarregarDadosExploravel(0);
    }

    // ==========================================
    // 1. GANGORRA DOS MODOS (BOTÕES DAS CAPAS)
    // ==========================================

    public void AtivarLadoEstatico()
    {
        // Abre o MenuMapa Estático
        pnl_Capa_Est.SetActive(false);
        pnl_MenuMapa_Est.SetActive(true);
        pnl_Grade_Est.SetActive(false);

        // Reseta o lado Explorável para a Capa
        pnl_Capa_Exp.SetActive(true);
        pnl_MenuMapa_Exp.SetActive(false);
        pnl_Grade_Exp.SetActive(false);
    }

    public void AtivarLadoExploravel()
    {
        // Abre o MenuMapa Explorável
        pnl_Capa_Exp.SetActive(false);
        pnl_MenuMapa_Exp.SetActive(true);
        pnl_Grade_Exp.SetActive(false);

        // Reseta o lado Estático para a Capa
        pnl_Capa_Est.SetActive(true);
        pnl_MenuMapa_Est.SetActive(false);
        pnl_Grade_Est.SetActive(false);
    }

    // ==========================================
    // 2. ABRIR AS GALERIAS (BOTÕES "TROCAR MAPA")
    // ==========================================

    public void AbrirGaleriaEstatica()
    {
        pnl_MenuMapa_Est.SetActive(false);
        pnl_Grade_Est.SetActive(true);
    }

    public void AbrirGaleriaExploravel()
    {
        pnl_MenuMapa_Exp.SetActive(false);
        pnl_Grade_Exp.SetActive(true);
    }

    // ==========================================
    // 3. SELECIONAR NA GALERIA E VOLTAR (BOTÕES DA GRID)
    // ==========================================

    public void SelecionarMapaEstatico(int index)
    {
        CarregarDadosEstatico(index);
        
        // Esconde a grade e volta pro Menu do Mapa
        pnl_Grade_Est.SetActive(false);
        pnl_MenuMapa_Est.SetActive(true);
    }

    public void SelecionarMapaExploravel(int index)
    {
        CarregarDadosExploravel(index);
        
        // Esconde a grade e volta pro Menu do Mapa
        pnl_Grade_Exp.SetActive(false);
        pnl_MenuMapa_Exp.SetActive(true);
    }

    // Funções internas para não repetir código
    private void CarregarDadosEstatico(int index)
    {
        if (index >= 0 && index < mapasEstaticos.Length)
        {
            img_MenuMapa_Est.texture = mapasEstaticos[index].imagem;
            txt_Titulo_Est.text = mapasEstaticos[index].titulo;
            txt_Desc_Est.text = mapasEstaticos[index].descricao;
            mapaSelecionado = mapasEstaticos[index].nomeDaCena;
        }
    }

    private void CarregarDadosExploravel(int index)
    {
        if (index >= 0 && index < mapasExploraveis.Length)
        {
            img_MenuMapa_Exp.texture = mapasExploraveis[index].imagem;
            txt_Titulo_Exp.text = mapasExploraveis[index].titulo;
            txt_Desc_Exp.text = mapasExploraveis[index].descricao;
            mapaSelecionado = mapasExploraveis[index].nomeDaCena;
        }
    }

    // ==========================================
    // 4. RESET GERAL E INÍCIO DE SESSÃO
    // ==========================================

    public void ResetarPaineisDeMapa()
    {
        pnl_Capa_Est.SetActive(true);
        pnl_MenuMapa_Est.SetActive(false);
        pnl_Grade_Est.SetActive(false);

        pnl_Capa_Exp.SetActive(true);
        pnl_MenuMapa_Exp.SetActive(false);
        pnl_Grade_Exp.SetActive(false);
    }

    public void IniciarSessao() 
    {
        if(string.IsNullOrEmpty(mapaSelecionado))
        {
            Debug.LogWarning("Travado: Selecione um mapa primeiro!");
            return;
        }

        string modo = dropdownModo.options[dropdownModo.value].text;
        Debug.Log("Iniciando reabilitação: " + modo + " | Cena: " + mapaSelecionado);
        SceneManager.LoadScene(mapaSelecionado);
    }
}

[System.Serializable]
public class DadosDoMapa
{
    public string titulo; 
    public string nomeDaCena; 
    [TextArea(3, 6)] 
    public string descricao;
    public Texture2D imagem;
}