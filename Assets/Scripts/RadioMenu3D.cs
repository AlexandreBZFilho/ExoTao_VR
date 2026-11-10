using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Controlador do Rádio 3D Low-Poly posicionado sobre a mesa do Menu Principal.
/// Emite a música do menu no espaço 3D (spatialBlend = 1.0) com atenuação suave
/// pela distância (fade-in ao se aproximar da mesa e som sutil ao centro da sala),
/// visor analógico iluminado e suporte a interação tátil via Raycast VR e Mouse.
/// </summary>
[ExecuteAlways]
public class RadioMenu3D : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public static RadioMenu3D Instancia { get; private set; }

    [Header("Estado do Rádio")]
    public bool ligado = true;
    [Range(0f, 1f)] public float volumeBase = 1.0f;

    [Header("Componentes Internos")]
    public AudioSource audioFonte;
    public Light luzVisor;
    public MeshRenderer meshRenderer;
    public BoxCollider colisorInteracao;

    [Header("Parâmetros de Iluminação")]
    public Color corVisorGlow = new Color(1.0f, 0.72f, 0.28f); // Âmbar Dourado Acolhedor
    public float intensidadeLuz = 0.90f;
    public float alcanceLuz = 1.80f;

    private Material matDisplayDinamico;
    private float timerFlicker = 0f;

    void Awake()
    {
        Instancia = this;
    }

    void OnEnable()
    {
        Instancia = this;
        ConfigurarRadioSeNecessario();
    }

    void Start()
    {
        ConfigurarRadioSeNecessario();
        if (Application.isPlaying && ligado && audioFonte != null && !audioFonte.isPlaying)
        {
            audioFonte.Play();
        }
    }

    void Update()
    {
        if (audioFonte == null) return;

        // Sincroniza o volume máximo com o volume global do jogo (MusicVol)
        float masterMusicVol = 0.78f;
        if (GerenciadorAudioRehab.Instancia != null)
        {
            masterMusicVol = GerenciadorAudioRehab.Instancia.MusicVol;
        }

        audioFonte.volume = ligado ? (volumeBase * masterMusicVol) : 0f;

        // Leve respiração analógica na intensidade da luz e emissão do visor
        if (ligado && luzVisor != null)
        {
            timerFlicker += Time.deltaTime * 3.5f;
            float microFlicker = 1.0f + Mathf.Sin(timerFlicker) * 0.04f + Mathf.Sin(timerFlicker * 2.7f) * 0.02f;
            luzVisor.intensity = intensidadeLuz * microFlicker;
        }
    }

    public void ConfigurarRadioSeNecessario()
    {
        // 1. Configura a Malha 3D FBX gerada no Blender 5.1
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null) mf = gameObject.AddComponent<MeshFilter>();

        Mesh malhaRadio = null;
        GameObject fbxRadio = Resources.Load<GameObject>("Models/Radio_Retro_LowPoly");
#if UNITY_EDITOR
        if (fbxRadio == null)
        {
            fbxRadio = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Models/Radio_Retro_LowPoly.fbx");
        }
#endif
        if (fbxRadio != null)
        {
            MeshFilter mfFbx = fbxRadio.GetComponentInChildren<MeshFilter>();
            if (mfFbx != null) malhaRadio = mfFbx.sharedMesh;
        }

        if (malhaRadio != null) mf.sharedMesh = malhaRadio;

        // 2. Configura os 4 Materiais Standard / URP no mapeamento exato dos slots do FBX
        Shader shaderCompativel = ObterShaderCompativel();
        Shader shaderUnlit = Shader.Find("Unlit/Color") ?? shaderCompativel;

        Material matChassis = ObterOuCriarMaterial(shaderCompativel, "Radio_Chassis_Mat", new Color(0.24f, 0.14f, 0.09f), 0.10f, 0.60f, false); // Madeira Nobre Mogno/Nogal
        Material matMetal   = ObterOuCriarMaterial(shaderCompativel, "Radio_Metal_Accents", new Color(0.88f, 0.74f, 0.38f), 0.92f, 0.85f, false); // Detalhes em Latão/Ouro Escovado
        Material matSpeaker = ObterOuCriarMaterial(shaderCompativel, "Radio_Speaker_Grill", new Color(0.11f, 0.12f, 0.14f), 0.20f, 0.25f, false); // Tecido Acústico Fosco
        Material matDial    = ObterOuCriarMaterial(shaderUnlit, "Radio_Display_Emissive", corVisorGlow, 0.00f, 0.95f, true); // Visor Analógico Âmbar Brilhante Autoiluminado

        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
        // Ordem exata dos submeshes do modelo FBX (Slot 0: Chassis, Slot 1: Metal, Slot 2: Speaker, Slot 3: Display):
        meshRenderer.sharedMaterials = new Material[] { matChassis, matMetal, matSpeaker, matDial };

        // 3. Configura a Fonte de Áudio 3D e a Curva de Atenuação Acústica Espacial Normalizada
        if (audioFonte == null) audioFonte = GetComponent<AudioSource>();
        if (audioFonte == null) audioFonte = gameObject.AddComponent<AudioSource>();

        AudioClip clipMusica = Resources.Load<AudioClip>("Audio/Music_SciFi_Menu");
        if (clipMusica != null) audioFonte.clip = clipMusica;

        audioFonte.loop = true;
        audioFonte.playOnAwake = true;
        audioFonte.spatialBlend = 1.0f; // 100% 3D surround posicional
        audioFonte.dopplerLevel = 0.0f;

        // Curva personalizada suave (Normalizada de 0.0 a 1.0 onde 1.0 = maxDistance):
        // maxDistance = 10.5m.
        // - De 0.0m a 0.90m (t: 0.086): 100% volume ao lado da mesa de rádio
        // - A 2.20m (t: 0.210): 55% volume (transição suave no quarto)
        // - A 4.50m (t: 0.429 - centro do menu onde o jogador fica): 18% volume (som suave de fundo)
        // - A 7.50m (t: 0.714): 7% volume
        // - A 10.50m (t: 1.000): 0% volume
        audioFonte.minDistance = 0.90f;
        audioFonte.maxDistance = 10.50f;
        audioFonte.rolloffMode = AudioRolloffMode.Custom;

        AnimationCurve curva = new AnimationCurve();
        curva.AddKey(new Keyframe(0.000f, 1.00f, 0.0f, 0.0f));
        curva.AddKey(new Keyframe(0.086f, 1.00f, 0.0f, -1.8f));
        curva.AddKey(new Keyframe(0.210f, 0.55f, -1.5f, -1.0f));
        curva.AddKey(new Keyframe(0.429f, 0.18f, -0.6f, -0.4f));
        curva.AddKey(new Keyframe(0.714f, 0.07f, -0.2f, -0.1f));
        curva.AddKey(new Keyframe(1.000f, 0.00f, 0.0f, 0.0f));

        audioFonte.SetCustomCurve(AudioSourceCurveType.CustomRolloff, curva);

        // 4. Configura a Luz Suave do Visor Analógico (Point Light)
        Transform tLuz = transform.Find("Luz_Visor_Radio");
        if (tLuz == null)
        {
            GameObject goLuz = new GameObject("Luz_Visor_Radio");
            goLuz.transform.SetParent(transform, false);
            tLuz = goLuz.transform;
        }
        tLuz.localPosition = new Vector3(0.072f, 0.134f, 0.095f); // Na frente do visor iluminado

        luzVisor = tLuz.GetComponent<Light>();
        if (luzVisor == null) luzVisor = tLuz.gameObject.AddComponent<Light>();
        luzVisor.type = LightType.Point;
        luzVisor.color = corVisorGlow;
        luzVisor.range = alcanceLuz;
        luzVisor.intensity = intensidadeLuz;

        // 5. Configura o Colisor para Interação Raycast VR / Mouse
        colisorInteracao = GetComponent<BoxCollider>();
        if (colisorInteracao == null) colisorInteracao = gameObject.AddComponent<BoxCollider>();
        colisorInteracao.isTrigger = false;
        colisorInteracao.center = new Vector3(0f, 0.10f, 0f);
        colisorInteracao.size = new Vector3(0.34f, 0.20f, 0.18f);
    }

    public void AlternarLigadoDesligado()
    {
        ligado = !ligado;
        if (luzVisor != null) luzVisor.enabled = ligado;

        if (GerenciadorAudioRehab.Instancia != null)
        {
            GerenciadorAudioRehab.Instancia.TocarSomUIClick();
        }

        if (ligado && audioFonte != null && !audioFonte.isPlaying)
        {
            audioFonte.Play();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        AlternarLigadoDesligado();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (GerenciadorAudioRehab.Instancia != null)
        {
            GerenciadorAudioRehab.Instancia.TocarSomUIHover();
        }
    }

    public void OnPointerExit(PointerEventData eventData) { }

    private Shader ObterShaderCompativel()
    {
        bool usandoSRP = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
        if (usandoSRP)
        {
            Shader urp = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit");
            if (urp != null) return urp;
        }
        return Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
    }

    private Material ObterOuCriarMaterial(Shader shader, string nome, Color cor, float metallic, float smoothness, bool emissivo)
    {
        Material m = Resources.Load<Material>("Materials/" + nome);
        if (m == null)
        {
            m = new Material(shader);
            m.name = nome;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources"))
                    UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
                if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources/Materials"))
                    UnityEditor.AssetDatabase.CreateFolder("Assets/Resources", "Materials");
                string path = $"Assets/Resources/Materials/{nome}.mat";
                UnityEditor.AssetDatabase.CreateAsset(m, path);
            }
#endif
        }

        if (shader != null && m.shader != shader)
        {
            m.shader = shader;
        }

        m.color = cor;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", cor);
        if (m.HasProperty("_Color")) m.SetColor("_Color", cor);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);

        if (emissivo)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.HasProperty("_EmissionColor"))
                m.SetColor("_EmissionColor", cor * 3.5f);
        }
        else
        {
            m.DisableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor"))
                m.SetColor("_EmissionColor", Color.black);
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(m);
        }
#endif
        return m;
    }
}
