# Especificação Técnica de Alta Precisão — TASK-06: Rádio Deck com 4 Fitas Cassete Magnéticas, Ejeção Mecânica & Sincronização Afetiva com o Drone Companheiro na Mesa 1

**Documento Técnico:** `.agents/specs/TASK-06_Spec.md`  
**Autor:** Subagente [A] — Forensic Spec, Furniture Mapper & Playtest Bug Scout  
**Módulo:** Menu Principal Hub / Sala de Preparação Clínica  
**Status:** APROVADO PARA EXECUÇÃO  
**Data:** 11/11/2026  
**Versão do Unity:** 6000.0.85f2 (Unity 6)  
**Versão do XR Interaction Toolkit:** 2.6.5  
**Pipeline de Renderização:** Built-in Render Pipeline / SRP-Agnostic (Compatível com SRP Batcher e GPU Instancing via `MaterialShaderHelper`)  
**Cenas Afetadas:** `Assets/Scenes/Menu_Principal.unity`  
**Prefabs e Scripts Afetados:** `Assets/Scripts/Interactive/Task06/` (`MusicCassetteTape.cs`, `RadioCassetteSocket.cs`, `CassetteEjectButton.cs`), `Task06_SceneAndPrefabBuilder.cs`, `[IC] Radio_Menu_3D`, `full_desk_3 (1)`, `DroneAffectiveSystem.cs`  

---

## 1. Diagnóstico Forense & Investigação da Sala do Menu Principal

Uma inspeção cirúrgica na cena `Assets/Scenes/Menu_Principal.unity`, na arquitetura da mesa de recepção (`full_desk_3 (1)`), nos modelos 3D em `Assets/Models/Generated/Task06/`, nos áudios sintetizados em `Assets/Audio/Generated/Task06/` e na integração com o Drone Companheiro (TASK-04) revelou o estado atual e **cinco pontos críticos de atenção** para a TASK-06:

```
┌──────────────────────────────────────────────────────────────────────────────────────────────────┐
│                                   DIAGNÓSTICO FORENSE DO TASK-06                                 │
├──────────────────────────────────────────────────────────────────────────────────────────────────┤
│ 1. LOCALIZAÇÃO DO RÁDIO ORIGINAL E DA MESA 1 (full_desk_3 (1)):                                  │
│    • A mesa 'full_desk_3 (1)' situa-se em (0.63, 0.0, -0.02) com tampo a Y = 0.952m.            │
│    • O rádio vintage original '[IC] Radio_Menu_3D' repousa em (0.66, 0.952, 0.36) com rotação    │
│      Euler (0°, 75°, 0°), voltado para a área central da sala.                                   │
│    • A estante de troféus 'TrophyDisplayStand_Instance' (TASK-16) situa-se na extremidade oposta │
│      em (0.68, 0.952, -0.38).                                                                    │
│    • Há um vão livre central perfeitamente desimpedido entre Z = -0.15m e Z = +0.26m na mesa     │
│      para o Radio Deck e o Organizador de Fitas, sem empilhar objetos nem invadir outros móveis. │
│                                                                                                  │
│ 2. BUDGET DE POLÍGONOS DO MODELO DE FITA CASSETE (EXCEDIDO ANTERIORMENTE):                       │
│    • O modelo anterior 'RetroCassetteTape.fbx' possuía 928 triângulos.                           │
│    • Causa: Carretéis internos (Spool_Left e Spool_Right) tinham 32 segmentos (256 tris cada,     │
│      somando 512 tris) e as bobinas de fita (Ribbon_Rolls) somavam 248 tris.                     │
│    • Para VR 90 FPS, o budget de itens pequenos de mão é de 300 a 600 triângulos. O modelo da   │
│      fita deve ser refinado pelo Subagente [B] para < 400 triângulos (16 segmentos de carretel), │
│      mantendo bevel suave e aspecto 100% autêntico.                                              │
│                                                                                                  │
│ 3. CALIBRAÇÃO ACÚSTICA DE EFEITOS SONOROS (PICOS PERIGOSOS A -0.77 dBFS):                        │
│    • 'cassette_insert_click.wav' e 'cassette_eject_snap.wav' atingiam -0.77 dBFS (30000/32767).  │
│    • Em VR próximo ao ouvido do paciente, estalos mecânicos perto de 0 dBFS geram susto,        │
│      distorção harmônica e desconforto clínico.                                                  │
│    • Correção: Recalibrar para pico absoluto <= -8.5 dBFS e filtro passa-baixas <= 3800 Hz.      │
│                                                                                                  │
│ 4. PADRONIZAÇÃO DE FÍSICA XRI: HERANÇA DE 'LaserSwingThrowInteractable' (TASK-02):              │
│    • 'MusicCassetteTape.cs' herdava de 'ErgonomicGrabInteractable' legado.                       │
│    • Deve herdar diretamente de 'LaserSwingThrowInteractable' para incorporar:                   │
│      a) Ring Buffer de 12 amostras com cálculo cinemático de chicote/balanço do raio laser;     │
│      b) Force Grab ergonômico com clamp de velocidade a 18 m/s;                                  │
│      c) Auto-respawn seguro de volta ao slot do organizador caso o paciente a deixe cair;        │
│      d) Comutação PhysX: ContinuousDynamic em voo -> Discrete + Sleep em repouso.               │
│                                                                                                  │
│ 5. INTEGRAÇÃO HARMÔNICA COM A DANÇA DO DRONE COMPANHEIRO (TASK-04):                              │
│    • O 'DroneAffectiveSystem' possui FFT em tempo real monitorando 'RadioMenu3D.audioFonte'.     │
│    • O deck de fitas comanda o 'RadioMenu3D' via crossfade estéreo suave; o Drone detecta o      │
│      ritmo da fita automaticamente, ativando 'DroneLEDExpression.Dance' e dancinha de cabeça!    │
└──────────────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Arquitetura do Sistema e Diagrama de Interação

```
                              ┌────────────────────────────────────────┐
                              │            full_desk_3 (1)             │
                              │        Superfície Y = 0.952m           │
                              └──────────────────┬─────────────────────┘
                                                 │
          ┌──────────────────────────────────────┼──────────────────────────────────────┐
          │                                      │                                      │
          ▼                                      ▼                                      ▼
┌──────────────────┐                  ┌──────────────────────┐               ┌──────────────────────┐
│ [IC] Radio_3D    │                  │  RadioCassetteDeck   │               │ CassetteOrganizerRack│
│ Pos: (0.66, 0.36)│◄─Crossfade Audio─│  Pos: (0.66, 0.14)   │               │ Pos: (0.70, -0.08)   │
│ Rotary Tuner/Amp │  (0.50s Lerp)    │  - XRSocketInteractor│               │ - 4 Stepped Slots    │
└─────────┬────────┘                  │  - CassetteEjectBtn  │               │   (18° Back Tilt)    │
          │                           └──────────▲───────────┘               └──────────▲───────────┘
          │ FFT Spectrum Analysis                │                                      │
          │ (Smoothed Bass Energy)               │ Magnetic Snap / Socket               │ Rest & Respawn
          ▼                                      │                                      │
┌──────────────────────┐              ┌──────────┴──────────────────────────────────────┴───────────┐
│ DroneAffectiveSystem │              │             MusicCassetteTape (x4 Variantes)                │
│ (TASK-04 Companion)  │              │  - Herda de LaserSwingThrowInteractable (TASK-02)           │
│ - LED Dance Face     │              │  - Laser Grab, Swing Throw & Physical Ejection Impulses     │
│ - Head Wobble/Bounce │              │  - Spool_Left / Spool_Right Rotation (240 deg/s)            │
└──────────────────────┘              └─────────────────────────────────────────────────────────────┘
```

---

## 3. Catálogo das 4 Fitas Cassete & Relação Terapêutica

As 4 fitas foram desenhadas para responder a diferentes estados emocionais e objetivos da sessão fisioterapêutica (mobilização passiva, treino de marcha robótica, foco de equilíbrio e relaxamento pós-exercício):

| ID / Nome do Prefab | Título da Fita | Gênero / Estilo | Cores da Carcaça & Label | Fonte do Áudio Clip | Propósito Terapêutico |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`CassetteTape_CyberHorizon`** | *Cyber Horizon* | Sci-Fi Synthwave | Neon Cyan (`#00D9FF`) & Dark Charcoal | `Assets/Resources/Audio/Music_SciFi_Menu.mp3` | Ritmo cadenciado (110 BPM) para foco e motivação de marcha. |
| **`CassetteTape_LunaCantina`** | *Luna Cantina* | Cantina Latina Retro | Warm Gold (`#F2C738`) & Bronze Mahogany | `Assets/FloatGrids/Demo/Music/Construyendo en el Espacio - Luna Cantina.mp3` | Sincronia motora alegre e coordenação rítmica de membros superiores. |
| **`CassetteTape_NeonSunset`** | *Neon Sunset* | Lo-Fi Chill Rehab | Synth Magenta (`#F226A6`) & Twilight Purple | `Assets/Audio/Generated/Task06/lofi_sunset_groove.wav` | Batida acolhedora e cadência suave para redução de ansiedade. |
| **`CassetteTape_EmeraldZen`** | *Emerald Zen* | Nature Zen Ambient | Emerald Green (`#26D973`) & Silver Pearl | `Assets/Audio/Generated/Task06/ambient_zen_flow.wav` | Relaxamento muscular profundo e biofeedback respiratório. |

---

## 4. Especificações 3D e Áudio para o Subagente [B]

O Subagente `[B]` deve focar 100% de sua cota de saída em modelagem no Blender 5.1 (`bpy`) e síntese/calibração acústica em Python.

### 4.1. Modelagem 3D Low-Poly VR no Blender

1. **`RetroCassetteTape.fbx`:**
   * **Dimensões Reais:** Largura = $0.100\text{m}$ (10 cm), Altura = $0.064\text{m}$ (6.4 cm), Espessura = $0.012\text{m}$ (1.2 cm).
   * **Hierarquia Obrigatória de Objetos:**
     * `Body_Shell`: Carcaça plástica chanfrada (1 segmento de bevel).
     * `Window_Glass`: Visor retangular acrílico central transparente.
     * `Spool_Left`: Carretel esquerdo com furo central dentado de 6 pontas. **Reduzir resolução de 32 para 16 segmentos angulares** (garantindo $\le 128$ tris por carretel).
     * `Spool_Right`: Carretel direito simétrico ($\le 128$ tris).
     * `Ribbon_Roll_Left` e `Ribbon_Roll_Right`: Cilindros de fita magnética enrolada ($\le 64$ tris cada).
     * `Label_Front`: Face frontal para aplicação do material emissivo temático da fita.
   * **Orçamento de Polígonos:** **Máximo de 380 a 450 triângulos no total da fita** (redução de mais de 50% em relação aos 928 tris anteriores!).
   * **Pivô:** Localizado no exato centro volumétrico da fita `(0, 0, 0)`.

2. **`CassetteDeckSlot.fbx`:**
   * **Dimensões:** Largura = $0.200\text{m}$, Altura = $0.075\text{m}$, Profundidade = $0.150\text{m}$.
   * **Elementos:**
     * `Deck_Housing`: Caixa de madeira nobre escura chanfrada.
     * `Deck_Faceplate`: Moldura frontal de latão/metal escovado dourado.
     * `Slot_Cavity`: Compartimento rebaixado com ângulo de $-18^\circ$ para acomodar a fita deixando metade do corpo visível.
     * `Eject_Button`: Tecla física mecânica destacada no canto superior esquerdo da faceplate.
     * `Status_LED`: Pequeno domo acrílico para indicador luminoso de reprodução.
   * **Orçamento:** **$\le 400$ triângulos**.

3. **`CassetteOrganizerRack.fbx`:**
   * **Dimensões:** Largura = $0.140\text{m}$, Profundidade = $0.140\text{m}$, Altura = $0.035\text{m}$.
   * **Elementos:**
     * `Rack_Base`: Bloco de madeira com 4 degraus escalonados.
     * `Divider_0` até `Divider_4`: 5 aletas divisórias de metal inclinadas a $18^\circ$ para trás.
   * **Orçamento:** **$\le 180$ triângulos**.

### 4.2. Síntese e Calibração de Áudio VR-Safe

* **Pico Digital Máximo:** **Estritamente $\le -8.5\text{ dBFS}$** (amplitude $\le 12.000$ em escala 16-bit PCM).
* **Filtro Passa-Baixas:** Corte suave em **$3.800\text{ Hz}$** para eliminar frequências agudas metálicas irritantes.
* **Arquivos e Parâmetros Acústicos:**
  1. `cassette_insert_click.wav`: Duração = $0.32\text{s}$, ruído mecânico de trilho plástico seguido de travamento seco de trinco metálico, pico calibrado a **$-9.5\text{ dBFS}$**.
  2. `cassette_eject_snap.wav`: Duração = $0.35\text{s}$, liberação rápida de mola com clique de retorno elástico, pico calibrado a **$-9.0\text{ dBFS}$**.
  3. `lofi_sunset_groove.wav`: Faixa completa (16.0s em loop suave), ritmo Lo-Fi relaxante (80 BPM), pico normalizado em **$-9.0\text{ dBFS}$**.
  4. `ambient_zen_flow.wav`: Faixa ambiental com sintetizadores de ondas serenas e sino tibetano suave (16.0s em loop), pico normalizado em **$-9.5\text{ dBFS}$**.

---

## 5. Especificações de Engenharia C# e Física VR para o Subagente [C]

O Subagente `[C]` deve dedicar 100% de sua cota de saída para os scripts em `Assets/Scripts/Interactive/Task06/`.

### 5.1. Refatoração de `MusicCassetteTape.cs` (Herança de `LaserSwingThrowInteractable`)

```csharp
namespace VRRehab.Interactive.Task06
{
    /// <summary>
    /// Fita Cassete Musical Interativa de Reabilitação (TASK-06).
    /// Herda de LaserSwingThrowInteractable (TASK-02), integrando nativamente:
    /// 1. Force Grab ergonômico por raio laser em qualquer distância da mesa;
    /// 2. Ring buffer de 12 amostras com física de balanço de chicote tangencial (Laser Swing);
    /// 3. Rotação mecânica dos carretéis Spool_Left e Spool_Right quando em reprodução no deck;
    /// 4. Auto-respawn suave para o slot do rack organizador em caso de perda ou queda;
    /// 5. Otimização PhysX: ContinuousDynamic ao ser segurada/arremessada -> Discrete + Sleep em repouso.
    /// </summary>
    [DisallowMultipleComponent]
    [SelectionBase]
    public class MusicCassetteTape : LaserSwingThrowInteractable
    {
        [Header("Metadados da Fita Cassete")]
        public string tapeId = "tape_cyan_01";
        public string tapeTitle = "Cyber Horizon";
        public string tapeGenre = "Sci-Fi Synthwave";
        public Color themeColor = new Color(0.0f, 0.85f, 1.0f);
        public AudioClip musicClip;

        [Header("Carretéis Internos Rotativos")]
        public Transform spoolLeft;
        public Transform spoolRight;
        public float spoolRotationSpeed = 240f; // Graus por segundo

        [Header("Estado de Reprodução")]
        [SerializeField] private bool isPlayingInDeck = false;

        [Header("Ancoragem de Rack / Estante")]
        [SerializeField] private Transform assignedRackSlot;

        // Implementação zero-GC da rotação dos carretéis em Update() apenas quando isPlayingInDeck == true
        // Override de OnSelectEntered para desativar reprodução ao ser retirada do deck manualmente
        // Métodos SetPlaying(bool) e SetAssignedRackSlot(Transform)
    }
}
```

### 5.2. `RadioCassetteSocket.cs` (Socket Magnético e Integração com Rádio)

1. **Herança:** `XRSocketInteractor`.
2. **Filtro Estrito:** `CanSelect` e `CanHover` validam `interactable.transform.GetComponent<MusicCassetteTape>() != null`. Rejeita qualquer outro objeto (maçãs, baquetas, etc.).
3. **Ancoragem:** `slotAttachPoint` alinhado a $-18^\circ$ de inclinação em X, elevando a fita para que 50% de sua altura fique projetada para fora do deck.
4. **Alimentação do Rádio Vintage:**
   * Localiza `RadioMenu3D` na mesa (`[IC] Radio_Menu_3D`).
   * Executa corrotina `CrossfadeRoutine`:
     * Fade out de 0.25s no volume atual.
     * Troca de `audio.clip = tape.musicClip`.
     * Se o rádio estiver desligado (`!radioTarget.ligado`), chama `radioTarget.AlternarLigadoDesligado()` automaticamente.
     * Fade in de 0.25s retornando ao `volumeBase`.
5. **LED de Status Reativo:**
   * Em standby (sem fita): cor azul escuro/apagado (`intensity = 0.4f`).
   * Em reprodução (com fita): assume a cor `themeColor` da fita inserida com emissão suave (`intensity = 1.6f`, `light.shadows = LightShadows.None`).
6. **Ejeção Física (`EjectCurrentTape`):**
   * Cooldown de proteção contra re-snap imediato (`isEjectingCooldown = true` por 1.0s).
   * Desconecta do interactor via `interactionManager.SelectExit(this, tapeToEject)`.
   * Habilita física no Rigidbody da fita (`rb.isKinematic = false`).
   * Aplica impulso tangencial suave para a frente e para cima:
     $$\vec{F}_{\text{eject}} = \left(0.70 \cdot \vec{f}_{\text{forward}} + 0.40 \cdot \vec{u}_{\text{up}}\right) \times 0.55\text{ N}\cdot\text{s}$$
   * Dispara SFX `cassette_eject_snap.wav`.
   * Restaura a música padrão do menu ou silêncio caso o usuário deseje.

### 5.3. `CassetteEjectButton.cs` (Botão Físico com Cinemática de Mola)

1. **Herança:** `XRBaseInteractable`, `IPointerClickHandler`, `IPointerEnterHandler`.
2. **Cinemática de Recuo:**
   * Afunda $0.006\text{m}$ para dentro da carcaça do deck.
   * Retorna suavemente via amortecimento elástico: `buttonMeshTransform.localPosition = Vector3.Lerp(current, initial, Time.deltaTime * 30f)`.
3. **Acionamento:**
   * Dispara ao receber raio laser (`OnSelectEntered` / `OnActivated`), clique de pointer ou toque físico.
   * Chama `deckSocket.EjectCurrentTape()`.
   * Fornece pulso háptico no controle do jogador (`amplitude = 0.7f`, `duration = 0.15s`).

### 5.4. Sincronização Harmônica com o Drone Companheiro (TASK-04)

* Em `Assets/Scripts/Interactive/Task04/DroneAffectiveSystem.cs`:
  * O método `LocateRadioMusicSource()` sintoniza em `RadioMenu3D.Instancia.audioFonte`.
  * Como o `RadioCassetteSocket` injeta o áudio da fita diretamente no `RadioMenu3D.Instancia.audioFonte`, o Drone Companheiro analisa o espectro FFT da fita inserida sem necessidade de linhas extras de acoplamento.
  * O Subagente [C] deve garantir que o volume mínimo emitido pelo rádio seja suficiente (`> 0.05f`) para excitar a sensibilidade de beat (`danceBeatSensitivity = 0.035f`), acionando a expressão `DroneLEDExpression.Dance`, o balanço rítmico de cabeça e as partículas de alegria!

---

## 6. Layout Espacial na Mesa 1, Tolerâncias Métricas & Purga de (0, 0, 0) para o Subagente [D]

O Subagente `[D]` deve implementar e executar `Assets/Editor/AutoSetup/Task06_SceneAndPrefabBuilder.cs`.

### 6.1. Coordenadas Absolutas na Mesa 1 (`full_desk_3 (1)`)

A mesa `full_desk_3 (1)` possui seu centro em `(0.63, 0.0, -0.02)` e o tampo superior em `Y = 0.952m`. O layout linear ao longo do eixo Z da mesa deve respeitar as seguintes posições:

```
[Extremidade Traseira: Z = -0.38]                                             [Extremidade Frontal: Z = +0.36]
┌─────────────────────────────────┬──────────────────────┬──────────────────────┬────────────────────────────┐
│ TrophyDisplayStand_Instance     │ CassetteOrganizerRack│ RadioCassetteDeck    │ [IC] Radio_Menu_3D         │
│ (TASK-16 - Estante de Troféus)  │ (Organizador de 4)   │ (Deck + Eject + LED) │ (Rádio Vintage Original)   │
│ Pos: (0.68, 0.952, -0.38)       │ Pos: (0.70, 0.952,   │ Pos: (0.66, 0.952,   │ Pos: (0.66, 0.952, +0.36)  │
│                                 │      -0.08)          │      +0.14)          │                            │
│                                 │ Rot: Euler(0, 75, 0) │ Rot: Euler(0, 75, 0) │ Rot: Euler(0, 75, 0)       │
└─────────────────────────────────┴──────────────────────┴──────────────────────┴────────────────────────────┘
                ▲                               ▲                      ▲
                │          Folga: 0.18m         │     Folga: 0.09m     │        Folga: 0.12m
                └───────────────────────────────┴──────────────────────┴─────────────────────────────┘
```

### 6.2. Distribuição Inicial das 4 Fitas Cassete

1. **Fita 1 (`Tape_CyberHorizon_Active` - Cyan):**
   * Inicialmente inserida no `SlotAttachPoint` do `RadioCassetteDeck` em `(0.66, 1.034, 0.15)`.
   * `isPlayingInDeck = true`, carretéis girando, rádio tocando sua faixa.
   * `assignedRackSlot` apontando para o `Slot_0` do Organizador (para onde ela retornará em caso de auto-respawn).
2. **Fita 2 (`Tape_LunaCantina_Rack` - Gold):**
   * Acomodada no `Slot_1` do Organizador em repouso inclinado a $18^\circ$.
3. **Fita 3 (`Tape_NeonSunset_Rack` - Magenta):**
   * Acomodada no `Slot_2` do Organizador em repouso inclinado a $18^\circ$.
4. **Fita 4 (`Tape_EmeraldZen_Rack` - Emerald):**
   * Acomodada no `Slot_3` do Organizador em repouso inclinado a $18^\circ$.
5. **Slot 0 do Organizador:**
   * Vazio no início, reservado como espaço livre imediato para acomodar a Fita 1 caso seja retirada do deck!

### 6.3. Lei de Purga de Duplicatas e Verificação de (0, 0, 0)

1. Antes de instanciar os prefabs, o script `Task06_SceneAndPrefabBuilder.cs` deve executar uma varredura rigorosa:
   ```csharp
   SceneDuplicateAndOriginPurger.PurgeSceneDuplicatesAndOriginTrash(EditorSceneManager.GetActiveScene());
   ```
2. Garantir que **nenhuma fita cassete, deck ou rack** permaneça esquecido na origem `Vector3.zero` (`distance < 0.8f`).
3. Validar `!boundsA.Intersects(boundsB)` entre todos os objetos da mesa com margem mínima de segurança de $0.08\text{m}$.
4. Exportar o arquivo `.agents/visual_qa/TASK-06_SpatialAudit.json` com telemetria métrica completa.
5. Renderizar 3 capturas HD em 1920x1080 via `VisualQACaptureEditor`:
   * `TASK-06_Radio_Deck_Overview.png` (Visão geral da Mesa 1 com Rádio, Deck, Rack e Fitas).
   * `TASK-06_Cassette_Tapes_Closeup.png` (Close-up no rack escalonado e etiquetas coloridas).
   * `TASK-06_Tape_Inserted_Slot.png` (Close-up na fita inserida no deck, LED aceso e botão de Eject).

---

## 7. Checklist Rigorosa de Qualidade e Auditoria para o Subagente [E]

O Subagente `[E]` atuará como juiz implacável e só concederá o veredito `PASS` se todos os critérios forem estritamente atendidos:

```
┌──────────────────────────────────────────────────────────────────────────────────────────────────┐
│                                CHECKLIST DE AUDITORIA TASK-06 (SUBAGENTE [E])                    │
├──────────────────────────────────────────────────────────────────────────────────────────────────┤
│ [ ] 1. MODELAGEM BLENDER LOW-POLY VR (< 400 TRIS POR FITA):                                      │
│     • 'RetroCassetteTape.fbx' possui menos de 450 triângulos (idealmente ~380 tris).             │
│     • Carretéis possuem 16 segmentos (e não 32). Zero partes invisíveis ou normais invertidas.   │
│     • 'CassetteDeckSlot.fbx' <= 400 tris; 'CassetteOrganizerRack.fbx' <= 180 tris.               │
│                                                                                                  │
│ [ ] 2. CALIBRAÇÃO ACÚSTICA VR-SAFE (PICOS <= -8.5 dBFS):                                         │
│     • 'cassette_insert_click.wav' e 'cassette_eject_snap.wav' com picos <= -8.5 dBFS.            │
│     • Sem cliques agudos estridentes ou estouros em fones de ouvido VR.                          │
│                                                                                                  │
│ [ ] 3. ENGENHARIA C# E FÍSICA XRI:                                                               │
│     • 'MusicCassetteTape' herda de 'LaserSwingThrowInteractable'.                                │
│     • Fita pode ser arremessada no laser e pega à distância via Force Grab.                     │
│     • Socket magnético XRSocketInteractor aceita apenas fitas e rejeita outros itens.            │
│     • Botão físico de Eject afunda suavemente com mola e ejeta a fita para a frente da mesa.     │
│     • Carretéis da fita giram continuamente no eixo Z local durante reprodução.                  │
│                                                                                                  │
│ [ ] 4. SINCRONIA AFETIVA COM O DRONE:                                                            │
│     • Ao tocar uma fita, o rádio transmite o sinal de áudio; o Drone Companheiro detecta o      │
│       espectro FFT e inicia a dança rítmica com a expressão facial LED 'Dance'.                  │
│                                                                                                  │
│ [ ] 5. DISTRIBUIÇÃO NA MESA 1 E ZERO LIXO NO (0,0,0):                                            │
│     • Deck e Rack perfeitamente assentados na mesa 'full_desk_3 (1)' a Y = 0.952m.               │
│     • Folga >= 0.08m entre Rádio, Deck, Rack e Estante de Troféus (sem sobreposições).           │
│     • Telemetria 'TASK-06_SpatialAudit.json' com 'hasOverlap: false' e 'originTrashCount: 0'.    │
│     • 3 screenshots nítidos em 1920x1080 salvos em '.agents/visual_qa/'.                         │
└──────────────────────────────────────────────────────────────────────────────────────────────────┘
```
