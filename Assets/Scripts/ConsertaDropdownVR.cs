using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class ConsertarDropdownVR : MonoBehaviour
{
    [Header("Configuração Correta do VR")]
    public string nomeDaLayer = "Default";
    public int ordemCorreta = 5;

    void LateUpdate()
    {
        // 1. ARRUMA A LISTA: O LateUpdate garante que nós temos a palavra final no frame!
        Canvas canvasDaLista = GetComponent<Canvas>();
        if (canvasDaLista != null)
        {
            canvasDaLista.overrideSorting = true;
            canvasDaLista.sortingLayerName = nomeDaLayer;
            canvasDaLista.sortingOrder = ordemCorreta;
        }

        // 2. CAÇADOR DE BLOCKER: Acha o painel invisível da Unity e arruma ele também
        GameObject blocker = GameObject.Find("Blocker");
        if (blocker != null)
        {
            Canvas canvasBlocker = blocker.GetComponent<Canvas>();
            if (canvasBlocker != null)
            {
                canvasBlocker.overrideSorting = true;
                canvasBlocker.sortingLayerName = nomeDaLayer;
                
                // O Blocker tem que ficar exatamente 1 número ATRÁS da sua lista
                canvasBlocker.sortingOrder = ordemCorreta - 1; 
            }

            // 3. O FIX DO LASER: Troca o leitor de mouse do Blocker pelo leitor de VR
            GraphicRaycaster raycasterMouse = blocker.GetComponent<GraphicRaycaster>();
            if (raycasterMouse != null && !(raycasterMouse is TrackedDeviceGraphicRaycaster))
            {
                Destroy(raycasterMouse);
                blocker.AddComponent<TrackedDeviceGraphicRaycaster>();
            }
        }
    }
}