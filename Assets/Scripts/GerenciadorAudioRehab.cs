using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Sistema Central de Áudio, Ambientação Confortável, Biofeedback Rítmico de Marcha (RAS)
/// e Efeitos Sonoros Sci-Fi / Mascote ("EXO-BOT Pet") para o ExoTao VR Rehab.
/// </summary>
[ExecuteAlways]
public class GerenciadorAudioRehab : MonoBehaviour
{
    public static GerenciadorAudioRehab Instancia { get; private set; }

    [Header("Controle de Volume Global (Mixer Handshake: MusicVol / SFXVol)")]
    [Range(0f, 1f)] public float MusicVol = 0.42f;
    [Range(0f, 1f)] public float SFXVol = 0.78f;

    [Header("Clipes de Ambientação (Menu Sci-Fi & Mapas Naturais)")]
    public AudioClip clipMusicaMenuSciFi;
    public AudioClip clipAmbientePadSciFi;
    public AudioClip clipAmbienteNaturezaFloresta;

    [Header("Clipes de Interface (UI Sci-Fi Confortável)")]
    public AudioClip clipUIHover;
    public AudioClip clipUIClick;
    public AudioClip clipUIPop;
    public AudioClip clipUIIniciarSessao;

    [Header("Clipes do Mascote Drone ('EXO-BOT Pet')")]
    public AudioClip clipDronePetSaudacao;
    public AudioClip clipDronePetFeliz;
    public AudioClip clipDronePetCurioso;
    public AudioClip clipDronePetPurrCarinho;
    public AudioClip clipDroneHoverLoop;

    [Header("Clipes de Biofeedback de Marcha (Passos Exoesqueleto)")]
    public AudioClip clipPassoDireito;
    public AudioClip clipPassoEsquerdo;

    [Header("Fontes de Áudio Internas")]
    public AudioSource fonteMusicaAmbiente;
    public AudioSource fonteCamadaSecundaria;
    public AudioSource fonteSFX2D;

    // Monitoramento automático de passos da Telemetria para Biofeedback Auditivo (RAS)
    private TelemetriaClinica telemetriaAtiva;
    private int ultimoPassosDir = 0;
    private int ultimoPassosEsq = 0;
    private float ultimoTempoHoverUI = -10f;

    void Awake()
    {
        if (Application.isPlaying)
        {
            if (Instancia != null && Instancia != this)
            {
                Destroy(gameObject);
                return;
            }
            Instancia = this;
        }
        else
        {
            Instancia = this;
        }

        CarregarClipesResources();
        GarantirFontesDeAudio();
    }

    void OnEnable()
    {
        Instancia = this;
        CarregarClipesResources();
        GarantirFontesDeAudio();
    }

    void Start()
    {
        CarregarClipesResources();
        GarantirFontesDeAudio();
        ConfigurarAudioParaCenaAtual(SceneManager.GetActiveScene().name);
        ConectarTodosBotoesUIDaCena();
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        // Mantém os volumes sincronizados com a camada de ganho (MusicVol / SFXVol)
        AtualizarVolumesGainLayer();

        // Monitora passos do exoesqueleto em tempo real para tocar o biofeedback rítmico suave
        MonitorarPassosExoesqueleto();
    }

    public void CarregarClipesResources()
    {
        if (clipMusicaMenuSciFi == null)          clipMusicaMenuSciFi          = Resources.Load<AudioClip>("Audio/Music_SciFi_Menu");
        if (clipAmbientePadSciFi == null)         clipAmbientePadSciFi         = Resources.Load<AudioClip>("Audio/Ambient_SciFi_Pad");
        if (clipAmbienteNaturezaFloresta == null) clipAmbienteNaturezaFloresta = Resources.Load<AudioClip>("Audio/Ambient_Nature_Forest");

        if (clipUIHover == null)                  clipUIHover                  = Resources.Load<AudioClip>("Audio/SFX_UI_Hover");
        if (clipUIClick == null)                  clipUIClick                  = Resources.Load<AudioClip>("Audio/SFX_UI_Click");
        if (clipUIPop == null)                    clipUIPop                    = Resources.Load<AudioClip>("Audio/SFX_UI_Pop");
        if (clipUIIniciarSessao == null)          clipUIIniciarSessao          = Resources.Load<AudioClip>("Audio/SFX_UI_StartSession");

        if (clipDronePetSaudacao == null)         clipDronePetSaudacao         = Resources.Load<AudioClip>("Audio/SFX_Drone_Pet_Greeting");
        if (clipDronePetFeliz == null)            clipDronePetFeliz            = Resources.Load<AudioClip>("Audio/SFX_Drone_Pet_Happy");
        if (clipDronePetCurioso == null)          clipDronePetCurioso          = Resources.Load<AudioClip>("Audio/SFX_Drone_Pet_Curious");
        if (clipDronePetPurrCarinho == null)      clipDronePetPurrCarinho      = Resources.Load<AudioClip>("Audio/SFX_Drone_Pet_Purr");
        if (clipDroneHoverLoop == null)           clipDroneHoverLoop           = Resources.Load<AudioClip>("Audio/SFX_Drone_Hover_Loop");

        if (clipPassoDireito == null)             clipPassoDireito             = Resources.Load<AudioClip>("Audio/SFX_Exo_Step_Right");
        if (clipPassoEsquerdo == null)            clipPassoEsquerdo            = Resources.Load<AudioClip>("Audio/SFX_Exo_Step_Left");
    }

    public void GarantirFontesDeAudio()
    {
        Transform tMus = transform.Find("AudioSource_Ambiente_Principal");
        if (tMus == null)
        {
            GameObject goMus = new GameObject("AudioSource_Ambiente_Principal");
            goMus.transform.SetParent(transform, false);
            tMus = goMus.transform;
        }
        fonteMusicaAmbiente = tMus.GetComponent<AudioSource>();
        if (fonteMusicaAmbiente == null) fonteMusicaAmbiente = tMus.gameObject.AddComponent<AudioSource>();
        fonteMusicaAmbiente.loop = true;
        fonteMusicaAmbiente.playOnAwake = true;
        fonteMusicaAmbiente.spatialBlend = 0f; // 2D Stereo Confortável

        Transform tSec = transform.Find("AudioSource_Ambiente_Camada2");
        if (tSec == null)
        {
            GameObject goSec = new GameObject("AudioSource_Ambiente_Camada2");
            goSec.transform.SetParent(transform, false);
            tSec = goSec.transform;
        }
        fonteCamadaSecundaria = tSec.GetComponent<AudioSource>();
        if (fonteCamadaSecundaria == null) fonteCamadaSecundaria = tSec.gameObject.AddComponent<AudioSource>();
        fonteCamadaSecundaria.loop = true;
        fonteCamadaSecundaria.playOnAwake = true;
        fonteCamadaSecundaria.spatialBlend = 0f;

        Transform tSfx = transform.Find("AudioSource_SFX_UI_2D");
        if (tSfx == null)
        {
            GameObject goSfx = new GameObject("AudioSource_SFX_UI_2D");
            goSfx.transform.SetParent(transform, false);
            tSfx = goSfx.transform;
        }
        fonteSFX2D = tSfx.GetComponent<AudioSource>();
        if (fonteSFX2D == null) fonteSFX2D = tSfx.gameObject.AddComponent<AudioSource>();
        fonteSFX2D.loop = false;
        fonteSFX2D.playOnAwake = false;
        fonteSFX2D.spatialBlend = 0f;
    }

    public void ConfigurarAudioParaCenaAtual(string nomeCena)
    {
        CarregarClipesResources();
        GarantirFontesDeAudio();

        bool ehMenuOuLoading = (nomeCena == "Menu_Principal" || nomeCena == "CenaDeLoading");

        if (ehMenuOuLoading)
        {
            // No Menu Principal: Toca a música ambiente Sci-Fi suave + Pad holográfico acolhedor
            fonteMusicaAmbiente.clip = clipMusicaMenuSciFi != null ? clipMusicaMenuSciFi : clipAmbientePadSciFi;
            fonteMusicaAmbiente.volume = MusicVol * 0.72f;

            fonteCamadaSecundaria.clip = clipAmbientePadSciFi;
            fonteCamadaSecundaria.volume = MusicVol * 0.45f;
        }
        else
        {
            // Nos Mapas de Reabilitação: Ambientação natural relaxante (riacho, brisa e pássaros) + fundo musical suave
            fonteMusicaAmbiente.clip = clipAmbienteNaturezaFloresta != null ? clipAmbienteNaturezaFloresta : clipAmbientePadSciFi;
            fonteMusicaAmbiente.volume = MusicVol * 0.85f;

            fonteCamadaSecundaria.clip = clipMusicaMenuSciFi != null ? clipMusicaMenuSciFi : clipAmbientePadSciFi;
            fonteCamadaSecundaria.volume = MusicVol * 0.28f;
        }

        if (Application.isPlaying)
        {
            if (fonteMusicaAmbiente.clip != null && !fonteMusicaAmbiente.isPlaying)
                fonteMusicaAmbiente.Play();
            if (fonteCamadaSecundaria.clip != null && !fonteCamadaSecundaria.isPlaying)
                fonteCamadaSecundaria.Play();
        }
    }

    private void AtualizarVolumesGainLayer()
    {
        string nomeCena = SceneManager.GetActiveScene().name;
        bool ehMenu = (nomeCena == "Menu_Principal" || nomeCena == "CenaDeLoading");

        if (fonteMusicaAmbiente != null)
            fonteMusicaAmbiente.volume = ehMenu ? (MusicVol * 0.72f) : (MusicVol * 0.85f);

        if (fonteCamadaSecundaria != null)
            fonteCamadaSecundaria.volume = ehMenu ? (MusicVol * 0.45f) : (MusicVol * 0.28f);

        if (fonteSFX2D != null)
            fonteSFX2D.volume = SFXVol;
    }

    // =========================================================================
    // MÉTODOS PÚBLICOS PARA TOCAR SONS DE UI, DRONE PET E BIOFEEDBACK
    // =========================================================================

    public void TocarSomUIHover()
    {
        if (fonteSFX2D == null || clipUIHover == null) return;
        if (Time.unscaledTime - ultimoTempoHoverUI < 0.045f) return;
        ultimoTempoHoverUI = Time.unscaledTime;

        fonteSFX2D.pitch = Random.Range(0.95f, 1.06f);
        fonteSFX2D.PlayOneShot(clipUIHover, SFXVol * 0.55f);
    }

    public void TocarSomUIClick(bool ehIniciarSessao = false)
    {
        if (fonteSFX2D == null) return;

        if (ehIniciarSessao && clipUIIniciarSessao != null)
        {
            fonteSFX2D.pitch = 1.0f;
            fonteSFX2D.PlayOneShot(clipUIIniciarSessao, SFXVol * 0.85f);
            return;
        }

        AudioClip clip = clipUIClick != null ? clipUIClick : clipUIPop;
        if (clip != null)
        {
            fonteSFX2D.pitch = Random.Range(0.96f, 1.04f);
            fonteSFX2D.PlayOneShot(clip, SFXVol * 0.75f);
        }
    }

    public void TocarSomDronePetSaudacao(AudioSource fonte3DOpcional = null)
    {
        TocarSomDroneInterno(clipDronePetSaudacao, 0.72f, fonte3DOpcional);
    }

    public void TocarSomDronePetFeliz(AudioSource fonte3DOpcional = null)
    {
        TocarSomDroneInterno(clipDronePetFeliz, 0.82f, fonte3DOpcional);
    }

    public void TocarSomDronePetCurioso(AudioSource fonte3DOpcional = null)
    {
        TocarSomDroneInterno(clipDronePetCurioso, 0.68f, fonte3DOpcional);
    }

    public void TocarSomDronePetCarinhoPinch(AudioSource fonte3DOpcional = null)
    {
        TocarSomDroneInterno(clipDronePetPurrCarinho, 0.75f, fonte3DOpcional);
    }

    private void TocarSomDroneInterno(AudioClip clip, float escalaVol, AudioSource fonte3D)
    {
        if (clip == null) return;
        AudioSource alvo = fonte3D != null ? fonte3D : fonteSFX2D;
        if (alvo == null) return;

        alvo.pitch = Random.Range(0.95f, 1.06f);
        alvo.PlayOneShot(clip, SFXVol * escalaVol);
    }

    private void MonitorarPassosExoesqueleto()
    {
        if (telemetriaAtiva == null)
        {
            telemetriaAtiva = FindFirstObjectByType<TelemetriaClinica>();
            if (telemetriaAtiva != null)
            {
                ultimoPassosDir = telemetriaAtiva.passosPernaDireita;
                ultimoPassosEsq = telemetriaAtiva.passosPernaEsquerda;
            }
            return;
        }

        if (telemetriaAtiva.passosPernaDireita > ultimoPassosDir)
        {
            ultimoPassosDir = telemetriaAtiva.passosPernaDireita;
            if (fonteSFX2D != null && clipPassoDireito != null)
            {
                fonteSFX2D.pitch = Random.Range(0.96f, 1.04f);
                fonteSFX2D.PlayOneShot(clipPassoDireito, SFXVol * 0.52f);
            }
        }

        if (telemetriaAtiva.passosPernaEsquerda > ultimoPassosEsq)
        {
            ultimoPassosEsq = telemetriaAtiva.passosPernaEsquerda;
            if (fonteSFX2D != null && clipPassoEsquerdo != null)
            {
                fonteSFX2D.pitch = Random.Range(0.96f, 1.04f);
                fonteSFX2D.PlayOneShot(clipPassoEsquerdo, SFXVol * 0.52f);
            }
        }
    }

    // =========================================================================
    // CONEXÃO AUTOMÁTICA DE SONS EM TODOS OS BOTÕES E DROPDOWNS DA CENA
    // =========================================================================

    public void ConectarTodosBotoesUIDaCena()
    {
        Selectable[] selecionaveis = FindObjectsByType<Selectable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Selectable sel in selecionaveis)
        {
            if (sel == null) continue;
            if (sel.GetComponent<GatilhoSomUIRehab>() == null)
            {
                sel.gameObject.AddComponent<GatilhoSomUIRehab>();
            }
        }
    }
}

/// <summary>
/// Componente leve anexado automaticamente a todos os Botões e Dropdowns do jogo
/// para tocar os efeitos sonoros suaves Sci-Fi ao passar o laser VR/Mouse e ao clicar.
/// </summary>
public class GatilhoSomUIRehab : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (GerenciadorAudioRehab.Instancia != null)
        {
            GerenciadorAudioRehab.Instancia.TocarSomUIHover();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (GerenciadorAudioRehab.Instancia != null)
        {
            string nomeObj = gameObject.name.ToLowerInvariant();
            TextMeshProUGUI tmp = GetComponentInChildren<TextMeshProUGUI>();
            string textoBtn = tmp != null && tmp.text != null ? tmp.text.ToLowerInvariant() : "";

            bool ehIniciar = nomeObj.Contains("iniciar") || textoBtn.Contains("iniciar");
            bool ehBotaoDrone = nomeObj.Contains("pinch") || nomeObj.Contains("perto") || nomeObj.Contains("longe") || nomeObj.Contains("reset");

            GerenciadorAudioRehab.Instancia.TocarSomUIClick(ehIniciar);

            if (ehBotaoDrone)
            {
                GerenciadorAudioRehab.Instancia.TocarSomDronePetCarinhoPinch();
            }
        }
    }
}
