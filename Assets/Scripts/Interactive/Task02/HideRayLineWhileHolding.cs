using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace VRRehab.Ergonomics
{
    /// <summary>
    /// Esconde o laser (XRInteractorLineVisual) enquanto o raio está SEGURANDO um objeto pegável
    /// (XRGrabInteractable: bolas, maçãs, fitas, troféus, pistola de balões... e o Drone, que é arrastado pelo laser)
    /// e mostra de novo ao soltar. Botões de toque rápido (ejetar fita, spawner, balão) não escondem, para não piscar.
    ///
    /// Por que: segurando algo, o raio passa a bater no próprio objeto e o XRInteractorLineVisual
    /// (com "Override Line Origin" ligado no prefab do jogador) desenha uma curva branca (gradiente "inválido")
    /// presa num ponto estranho da cena. Sem o laser, o jogador vê só o objeto seguindo a mão.
    ///
    /// Não mexe no raio em si (continua mirando, puxando/afastando com o scroll e soltando normalmente) e
    /// não esconde a curva de teleporte (só reage a XRGrabInteractable e ao Drone). Sem Update: só eventos de seleção.
    /// Instalado nos prefabs do jogador e, por segurança, em qualquer raio com laser de qualquer cena (instalador abaixo).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRRayInteractor))]
    public class HideRayLineWhileHolding : MonoBehaviour
    {
        private XRRayInteractor ray;
        private XRInteractorLineVisual lineVisual;
        private bool hiddenByUs;

        /// <summary>True enquanto este componente está escondendo o laser.</summary>
        public bool IsLineHidden => hiddenByUs;

        private void Awake()
        {
            ray = GetComponent<XRRayInteractor>();
            lineVisual = GetComponent<XRInteractorLineVisual>();
        }

        private void OnEnable()
        {
            if (ray == null) return;
            ray.selectEntered.AddListener(OnSelectEntered);
            ray.selectExited.AddListener(OnSelectExited);
            Refresh();
        }

        private void OnDisable()
        {
            if (ray != null)
            {
                ray.selectEntered.RemoveListener(OnSelectEntered);
                ray.selectExited.RemoveListener(OnSelectExited);
            }
            ShowLine();
        }

        private void OnSelectEntered(SelectEnterEventArgs args) => Refresh();
        private void OnSelectExited(SelectExitEventArgs args) => Refresh();

        private void Refresh()
        {
            if (lineVisual == null) return;
            if (IsHoldingGrabbable()) HideLine();
            else ShowLine();
        }

        private bool IsHoldingGrabbable()
        {
            if (ray == null || !ray.hasSelection) return false;
            List<IXRSelectInteractable> selected = ray.interactablesSelected;
            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i] is XRGrabInteractable || selected[i] is VRRehab.DronePet.DroneCompanionFollowAndGrab) return true;
            }
            return false;
        }

        private void HideLine()
        {
            // Desligar o componente faz o próprio XRI desligar o LineRenderer e a retícula (OnDisable dele).
            if (hiddenByUs || !lineVisual.enabled) return;
            lineVisual.enabled = false;
            hiddenByUs = true;
        }

        private void ShowLine()
        {
            // Só religa se fomos nós que desligamos (não briga com outro script que tenha desligado o laser).
            if (!hiddenByUs) return;
            hiddenByUs = false;
            if (lineVisual != null) lineVisual.enabled = true;
        }
    }

    /// <summary>
    /// Rede de segurança: ao carregar qualquer cena, coloca o HideRayLineWhileHolding em todo raio com laser
    /// que ainda não tenha (ex.: rigs de outras cenas que não usam os prefabs Pessoa_Virtual).
    /// Roda uma vez por cena carregada, nunca por frame.
    /// </summary>
    public static class HideRayLineWhileHoldingInstaller
    {
        private static bool registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (!registered)
            {
                SceneManager.sceneLoaded += (scene, mode) => InstallAll();
                registered = true;
            }
            InstallAll();
        }

        public static int InstallAll()
        {
            int added = 0;
            var visuals = Object.FindObjectsByType<XRInteractorLineVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < visuals.Length; i++)
            {
                GameObject go = visuals[i].gameObject;
                if (go.GetComponent<XRRayInteractor>() == null) continue;
                if (go.GetComponent<HideRayLineWhileHolding>() != null) continue;
                go.AddComponent<HideRayLineWhileHolding>();
                added++;
            }
            return added;
        }
    }
}
