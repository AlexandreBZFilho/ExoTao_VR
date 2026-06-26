using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CarregadorDeFase : MonoBehaviour
{
    [Header("UI do Loading")]
    public Transform cabecaDoPaciente;
    public GameObject canvasTextoLoading;
    public float distanciaDoTexto = 2.0f;
    public Slider barraDeProgresso;

    [Header("Configuração de Tempo")]
    public float tempoMinimoDeLoading = 10f; // Força o jogo a esperar 10 segundos

    void Start()
    {
        // Posiciona o texto/barra na frente do rosto
        if (canvasTextoLoading != null && cabecaDoPaciente != null)
        {
            Vector3 posicaoFrente = cabecaDoPaciente.position + (cabecaDoPaciente.forward * distanciaDoTexto);
            canvasTextoLoading.transform.position = posicaoFrente;
            canvasTextoLoading.transform.rotation = Quaternion.LookRotation(canvasTextoLoading.transform.position - cabecaDoPaciente.position);
            canvasTextoLoading.SetActive(true);
        }

        if (barraDeProgresso != null) barraDeProgresso.value = 0f;

        // Inicia o processo
        StartCoroutine(CarregarMapaReal());
    }

    private IEnumerator CarregarMapaReal()
    {
        float tempoDecorrido = 0f;

        // 1. Começa a descompactar o mapa pesado em background IMEDIATAMENTE
        AsyncOperation operacao = SceneManager.LoadSceneAsync(ControladorInterface.mapaFuturo);
        
        // Impede a cena de abrir sozinha
        operacao.allowSceneActivation = false; 

        // 2. Loop que roda frame a frame segurando o paciente no vácuo
        while (!operacao.isDone)
        {
            // Conta o tempo real que passou desde que a cena abriu
            tempoDecorrido += Time.deltaTime;

            // Calcula o progresso do tempo (vai de 0 a 1 conforme chega perto dos 10 segundos)
            float progressoTempo = Mathf.Clamp01(tempoDecorrido / tempoMinimoDeLoading);

            // Atualiza a barra roxa com base no tempo (fica super suave visualmente)
            if (barraDeProgresso != null) 
            {
                barraDeProgresso.value = progressoTempo;
            }

            // CONDIÇÃO DE SEGURANÇA MÁXIMA:
            // Só deixa passar se a Unity terminou o loading (0.9f) E o relógio bateu 10 segundos
            if (operacao.progress >= 0.9f && tempoDecorrido >= tempoMinimoDeLoading)
            {
                Debug.Log("Sistema estável. Liberando o mapa!");
                operacao.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}