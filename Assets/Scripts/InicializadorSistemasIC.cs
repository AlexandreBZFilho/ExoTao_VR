using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Inicializador automático para garantir que todas as cenas de reabilitação do ExoTao VR Rehab
/// tenham a Telemetria Clínica (CSV/Métricas) e o Drone Guia IA funcionando automaticamente sem precisar
/// configurar manualmente objeto por objeto em cada mapa.
/// </summary>
public static class InicializadorSistemasIC
{
    private static bool eventoRegistrado = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AoIniciarJogo()
    {
        if (!eventoRegistrado)
        {
            SceneManager.sceneLoaded += AoCarregarCena;
            eventoRegistrado = true;
        }

        VerificarEConfigurarCena(SceneManager.GetActiveScene());
    }

    private static void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        VerificarEConfigurarCena(cena);
    }

    private static void VerificarEConfigurarCena(Scene cena)
    {
        if (!cena.IsValid()) return;

        string nomeCena = cena.name;
        // Ignora o Menu Principal e a Tela de Loading
        if (nomeCena == "Menu_Principal" || nomeCena == "CenaDeLoading")
            return;

        // Verifica se é uma cena de reabilitação (tem ReceptorUDP ou CharacterController)
        ReceptorUDP receptor = Object.FindFirstObjectByType<ReceptorUDP>();
        CharacterController controle = Object.FindFirstObjectByType<CharacterController>();

        if (receptor == null && controle == null)
            return;

        // 1. Garante que o Módulo de Telemetria Clínica exista na cena
        TelemetriaClinica telemetria = Object.FindFirstObjectByType<TelemetriaClinica>();
        if (telemetria == null)
        {
            GameObject goTelemetria = new GameObject("[IC] Telemetria_Clinica_ReRobLab");
            telemetria = goTelemetria.AddComponent<TelemetriaClinica>();
            telemetria.receptorExo = receptor;
            telemetria.controlePaciente = controle;
            Debug.Log($"[ExoTao IC] Telemetria Clínica injetada automaticamente na cena '{nomeCena}'.");
        }

        // 2. Garante que o Drone Guia IA exista na cena
        DroneGuiaIA drone = Object.FindFirstObjectByType<DroneGuiaIA>();
        if (drone == null)
        {
            GameObject goDrone = new GameObject("[IC] Drone_Guia_IA_ExoBot");
            drone = goDrone.AddComponent<DroneGuiaIA>();
            drone.receptorExo = receptor;
            drone.telemetria = telemetria;
            Debug.Log($"[ExoTao IC] Drone Guia IA (EXO-BOT) injetado automaticamente na cena '{nomeCena}'.");
        }
    }
}
