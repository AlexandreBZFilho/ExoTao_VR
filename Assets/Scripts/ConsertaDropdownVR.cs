using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ConsertaDropdownVR : MonoBehaviour, IPointerClickHandler
{
    // Espera um milissegundo para a Unity gerar a lista antes de agirmos
    public void OnPointerClick(PointerEventData eventData)
    {
        Invoke("AplicarCorrecaoTotal", 0.05f); 
    }

    void AplicarCorrecaoTotal()
    {
        // Pega o seu Canvas principal (o cérebro que já tem o leitor de laser funcionando)
        Canvas canvasPrincipal = GetComponentInParent<Canvas>().rootCanvas;
        if (canvasPrincipal == null) return;

        // ---------------------------------------------------------
        // 1. DESTRUIR A PAREDE INVISÍVEL (BLOCKER)
        // ---------------------------------------------------------
        Transform blocker = canvasPrincipal.transform.Find("Blocker");
        if (blocker != null)
        {
            // Arrancamos o leitor de mouse e o Canvas intruso.
            // O Blocker vira uma imagem normal e passa a obedecer o seu Laser do menu!
            if (blocker.GetComponent<GraphicRaycaster>()) Destroy(blocker.GetComponent<GraphicRaycaster>());
            if (blocker.GetComponent<Canvas>()) Destroy(blocker.GetComponent<Canvas>());
        }

        // ---------------------------------------------------------
        // 2. DESTRUIR O CANVAS DA LISTA (FIM DO CORTE NO LASER)
        // ---------------------------------------------------------
        Transform dropdownList = canvasPrincipal.transform.Find("Dropdown List");
        if (dropdownList != null)
        {
            // Arrancamos o Canvas rebelde da lista!
            // Isso devolve a lista para o mundo físico do seu FloatGrids.
            if (dropdownList.GetComponent<GraphicRaycaster>()) Destroy(dropdownList.GetComponent<GraphicRaycaster>());
            if (dropdownList.GetComponent<Canvas>()) Destroy(dropdownList.GetComponent<Canvas>());
            
            // Puxa a lista 1 milímetro pra frente para ela não afundar na placa de trás
            Vector3 posicaoFisica = dropdownList.localPosition;
            posicaoFisica.z -= 1f; 
            dropdownList.localPosition = posicaoFisica;
        }
    }
}