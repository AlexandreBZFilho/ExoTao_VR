using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Inicializador automático para garantir que todas as cenas de reabilitação do ExoTao VR Rehab
/// tenham:
/// 1. O Avatar 3D (Y Bot Mixamo) com Exoesqueleto ExoTao e Cinemática conectada ao ReceptorUDP;
/// 2. A Telemetria Clínica (CSV/Métricas);
/// 3. O Drone Guia IA (EXO-BOT);
/// 4. A configuração correta de Canvases VR.
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

        // 0. Em QUALQUER cena (inclusive Menu Principal), configura Canvases WorldSpace para VR e Sistema de Áudio
        ConfigurarCanvasesVR();
        GarantirSistemaDeAudio(nomeCena);

        // Ignora o Menu Principal e a Tela de Loading para injeção de locomoção/drone/telemetria
        if (nomeCena == "Menu_Principal" || nomeCena == "CenaDeLoading")
            return;

        // Localiza o jogador (CharacterController ou ReceptorUDP)
        CharacterController controle = Object.FindFirstObjectByType<CharacterController>();
        ReceptorUDP receptor = Object.FindFirstObjectByType<ReceptorUDP>();

        if (receptor == null && controle == null)
            return;

        GameObject goJogador = controle != null ? controle.gameObject : receptor.gameObject;

        // 1. Garante que o ReceptorUDP exista no jogador e esteja vinculado ao CharacterController
        if (receptor == null)
        {
            receptor = goJogador.AddComponent<ReceptorUDP>();
            receptor.controlePaciente = controle;
            Debug.Log($"[ExoTao IC] ReceptorUDP adicionado automaticamente ao jogador '{goJogador.name}' na cena '{nomeCena}'.");
        }
        else if (receptor.controlePaciente == null && controle != null)
        {
            receptor.controlePaciente = controle;
        }

        // 2. Garante que o Avatar 3D (Y Bot + Exoesqueleto ExoTao + Cinemática) exista e esteja conectado aos ossos
        AvatarBiomecanicoVR avatar = goJogador.GetComponent<AvatarBiomecanicoVR>();
        if (avatar == null)
        {
            avatar = Object.FindFirstObjectByType<AvatarBiomecanicoVR>();
        }
        if (avatar == null)
        {
            avatar = goJogador.AddComponent<AvatarBiomecanicoVR>();
        }
        avatar.receptorExo = receptor;
        avatar.InicializarAvatarECinematica();

        // 3. Garante que o Módulo de Telemetria Clínica exista na cena
        TelemetriaClinica telemetria = Object.FindFirstObjectByType<TelemetriaClinica>();
        if (telemetria == null)
        {
            GameObject goTelemetria = new GameObject("[IC] Telemetria_Clinica_ReRobLab");
            telemetria = goTelemetria.AddComponent<TelemetriaClinica>();
            Debug.Log($"[ExoTao IC] Telemetria Clínica injetada automaticamente na cena '{nomeCena}'.");
        }
        telemetria.receptorExo = receptor;
        telemetria.controlePaciente = controle;

        // 4. Garante que o Drone Guia IA exista na cena
        DroneGuiaIA drone = Object.FindFirstObjectByType<DroneGuiaIA>();
        if (drone == null)
        {
            GameObject goDrone = new GameObject("[IC] Drone_Guia_IA_ExoBot");
            drone = goDrone.AddComponent<DroneGuiaIA>();
            Debug.Log($"[ExoTao IC] Drone Guia IA (EXO-BOT) injetado automaticamente na cena '{nomeCena}'.");
        }
        drone.receptorExo = receptor;
        drone.telemetria = telemetria;

        // Reconecta todos os botões UI da cena (incluindo os botões da Barra Espacial do Drone) aos efeitos sonoros
        if (GerenciadorAudioRehab.Instancia != null)
        {
            GerenciadorAudioRehab.Instancia.ConectarTodosBotoesUIDaCena();
        }
    }

    private static void GarantirSistemaDeAudio(string nomeCena)
    {
        if (nomeCena == "Menu_Principal")
        {
            RadioMenu3D radio = Object.FindFirstObjectByType<RadioMenu3D>();
            if (radio != null) radio.ConfigurarRadioSeNecessario();
        }

        GerenciadorAudioRehab audio = Object.FindFirstObjectByType<GerenciadorAudioRehab>();
        if (audio == null)
        {
            GameObject goAudio = new GameObject("[IC] Audio_Ambiente_E_SFX_ReRobLab");
            audio = goAudio.AddComponent<GerenciadorAudioRehab>();
        }
        audio.ConfigurarAudioParaCenaAtual(nomeCena);
        audio.ConectarTodosBotoesUIDaCena();
    }

    private static void ConfigurarCanvasesVR()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            mainCam = Object.FindFirstObjectByType<Camera>();
        }

        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas c in canvases)
        {
            if (c == null) continue;

            if (c.renderMode == RenderMode.WorldSpace)
            {
                c.vertexColorAlwaysGammaSpace = true;

                if (c.worldCamera == null && mainCam != null)
                {
                    c.worldCamera = mainCam;
                }

                // Garante suporte a laser de controles VR
                TrackedDeviceGraphicRaycaster raycaster = c.GetComponent<TrackedDeviceGraphicRaycaster>();
                if (raycaster == null)
                {
                    c.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
                }
            }
        }
    }
}
