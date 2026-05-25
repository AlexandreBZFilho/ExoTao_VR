using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ControladorInterface : MonoBehaviour
{
    [Header("Painéis Principais")]
    public GameObject menuPrincipal;
    public GameObject subMenuPersonagens;
    public GameObject subMenuMapas;
    public GameObject menuMapas;
    public GameObject menuPersonagens;

    [Header("Dados da Sessão")]
    public TMP_Dropdown dropdownModo;
    public string mapaSelecionado = "Floresta";

    // Chamado pelo botão "Selecionar Personagem"
    public void IrParaPersonagens() {
        menuPrincipal.SetActive(true);
        subMenuPersonagens.SetActive(true);
        menuPersonagens.SetActive(false);
    }

    // Chamado pelo botão "Selecionar Mapa"
    public void IrParaMapas() {
        menuPrincipal.SetActive(true);
        subMenuMapas.SetActive(true);
        menuMapas.SetActive(false);
    }

    // Botão de Voltar
    public void VoltarAoMenu() {
        menuPrincipal.SetActive(true);
        subMenuPersonagens.SetActive(false);
        subMenuMapas.SetActive(false);
        menuMapas.SetActive(true);
        menuPersonagens.SetActive(true);
    }

    // Botão INICIAR SESSÃO
    public void IniciarSessao() {
        string modo = dropdownModo.options[dropdownModo.value].text;
        Debug.Log("Iniciando reabilitação no modo: " + modo + " no mapa: " + mapaSelecionado);
        // Aqui chamamos o SceneManager.LoadScene(mapaSelecionado);
    }
}