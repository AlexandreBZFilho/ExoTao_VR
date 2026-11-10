# Orquestração Autônoma Multi-Agente & Visual QA — VR Rehab Gamification (Unity XRI)

> **Instrução de Inicialização no Antigravity:**
> 1. Salve este arquivo fisicamente na raiz do repositório em dois locais: `./AGENTS_IMPLEMENTATION_PLAN.md` e `./AGENTS.md` (para ativação contínua `always_on`).
> 2. **Modo de Operação da Unity:** O **Unity Editor já está ABERTO** na máquina host. Utilize o **Unity CLI / Unity MCP Bridge** conectando diretamente à instância aberta do Editor (ou acionando `AssetDatabase.Refresh()` e menus de Editor) sem tentar abrir uma segunda instância concorrente que trave o `UnityLockfile`.
> 3. **Recuperação Pós-Limite de Tokens (Auto-Resume):** Caso a sessão atinja o limite de contexto ou cota de tokens, basta iniciar um novo prompt ou digitar `/resume` com o comando:
>    *"Leia `@AGENTS_IMPLEMENTATION_PLAN.md`, verifique a tabela de estado na Seção 3 e retome o protocolo de subagentes a partir da tarefa marcada como `[~]` ou da primeira `[ ]`."*

---

## 1. Ecossistema de Ferramentas Habilitadas para os Subagentes

Cada subagente tem autonomia e obrigação de utilizar todo o arsenal de ferramentas disponíveis na máquina para entregar a funcionalidade **100% pronta, montada na cena, com modelos 3D, texturas, zero erros de compilação e visualmente inspecionada por captura de tela**:

1. **Blender MCP (`blender-mcp` / Python `bpy`):**
   * Sempre que uma tarefa exigir um objeto 3D inexistente no projeto (ex: *Fitas Cassete, Rádio Deck, Pistola de Balões, Maçãs Low-Poly, Cestas de Vime, Cogumelos Bioluminescentes, Totens de Checkpoint, Troféus, Esferas de Portal, Chapéu do Drone*), o subagente deve usar o **Blender MCP** (ou script Python no Blender) para modelar a malha 3D, aplicar materiais/cores base, configurar o ponto de pivô correto para pegada VR e exportar em `.fbx` diretamente para `Assets/Models/Generated/TaskXX/`.
2. **Unity Editor Aberto + Unity CLI / MCP + `EditorApplication.ExecuteMenuItem`:**
   * Como o Editor está aberto, o subagente deve criar scripts de automação em `Assets/Editor/AutoSetup/TaskXX_SceneAndPrefabBuilder.cs` contendo métodos estáticos com `[MenuItem("VRRehab/AutoBuild/Build and Verify TASK-XX")]`.
   * O script de setup deve instanciar os modelos `.fbx`, configurar materiais compatíveis com o Render Pipeline atual do projeto (evitando qualquer shader quebrado/rosa), adicionar `Colliders`, `Rigidbodies`, componentes `XRGrabInteractable` / `XRSocketInteractor`, posicionar na cena e salvar o **Prefab** em `Assets/Prefabs/Interactive/`.
3. **Sistema de Inspeção Visual Automatizada (`VisualQACaptureUtility`):**
   * O subagente deve renderizar capturas de tela em alta resolução (`1920x1080`) da `Game View` e de câmeras de inspeção focadas diretamente no objeto recém-criado (usando `Camera.Render()` para um `RenderTexture` salvo em `.agents/visual_qa/TASK-XX_screenshot.png` ou `ScreenCapture.CaptureScreenshot`).
   * O subagente **DEVE abrir e inspecionar visualmente a imagem `.png` gerada** usando sua capacidade multimodal (ou enviando para a Gemini API Vision) antes de concluir a tarefa.
4. **Gemini API (`GEMINI_API_KEY` / Google GenAI SDK) & Python Audio:**
   * Gerar texturas, ícones de UI para o visor do Drone, JSONs de frases motivacionais clínicas e sintetizar efeitos sonoros procedurais (`.wav`) salvos em `Assets/Audio/Generated/`.

---

## 2. Protocolo Rigoroso de Execução por Subagente (5 Estágios Obrigatórios)

Para garantir que **todas as 16 tarefas recebam exatamente o mesmo nível de esforço profundo** de uma tarefa individual isolada, o Agente Orquestrador Principal deve executar **UMA ÚNICA TAREFA POR VEZ** exigindo que o subagente cumpra os 5 estágios abaixo:

### Estágio A: Pesquisa Profunda & Descoberta de Contexto (Research & Discovery)
1. Inspecionar os arquivos atuais do projeto Unity (`Packages/manifest.json`, Render Pipeline ativo, scripts atuais do Drone, do Exoesqueleto e cenas do Menu e Floresta).
2. Pesquisar as melhores práticas da API do `XR Interaction Toolkit` instalado no projeto.
3. Redigir a especificação técnica em `.agents/specs/TASK-XX_Spec.md` definindo hierarquia, escala métrica real para VR (1 unidade Unity = 1 metro) e posicionamento espacial.

### Estágio B: Geração de Assets 3D (Blender MCP) & Áudio/Texturas (Gemini API / Python)
1. Acionar o **Blender MCP** para modelar os objetos 3D estilizados com escala real de VR (ex: uma maçã deve ter `~0.08m` de diâmetro, uma fita cassete `~0.10m x 0.06m`, uma cesta `~0.45m`), aplicando cores e materiais limpos e exportando `.fbx` para `Assets/Models/Generated/TaskXX/`.
2. Gerar os efeitos sonoros `.wav` e texturas necessárias em `Assets/Audio/Generated/TaskXX/`.

### Estágio C: Implementação C# Completa + Automação de Cena/Prefab (`Editor Script`)
1. Escrever os scripts de runtime em `Assets/Scripts/Interactive/TaskXX/` com **zero placeholders**, tratamento completo de eventos XRI, *Object Pooling* e suporte a *Force Grab*.
2. Escrever e executar o script de Editor `Assets/Editor/AutoSetup/TaskXX_SceneAndPrefabBuilder.cs` que monta automaticamente os Prefabs e os posiciona corretamente na cena ativa do Editor aberto.
3. Verificar os logs do Console da Unity garantindo **0 erros de compilação (`CSXXXX`) e 0 exceções**.

### Estágio D: Loop Obrigatório de Verificação e Correção Visual (Visual QA Loop)
**Nenhuma tarefa pode ser marcada como concluída sem aprovação visual por imagem:**
1. O subagente deve acionar o utilitário de captura visual na Unity para gerar prints de **3 ângulos** do objeto/mecânica na cena (Frente/Perspectiva do Jogador VR, Perfil Lateral para checar contato com o chão/mesa, e Close-up de UI/Materiais) salvos em `.agents/visual_qa/TASK-XX_view1.png`, `view2.png`, `view3.png`.
2. O subagente deve **analisar visualmente as imagens capturadas** verificando os seguintes critérios:
   * **Checagem de Shader/Material:** Há algum objeto com cor magenta/rosa (*Missing/Broken Shader*) ou totalmente preto por falta de luz/normal invertida?
   * **Checagem de Escala e Proporção VR:** O objeto tem tamanho coerente com as mãos do jogador, com a mesa do menu ou com as árvores da floresta? (Nem gigante, nem microscópico).
   * **Checagem de Posicionamento e Flutuação (*Grounding/Clipping*):** O objeto está apoiado corretamente sobre a mesa/terreno ou está flutuando no ar / enterrado dentro da geometria?
   * **Checagem de UI/Legibilidade:** Textos 3D (`TextMeshPro`), placares e visores do Drone estão legíveis, dentro do *Canvas* e não espelhados/invertidos?
3. **Loop de Auto-Correção:** Se qualquer anomalia visual for detectada no print, o subagente **DEVE ajustar o modelo no Blender MCP ou corrigir o `Transform`/Shader no script da Unity, regerar o print e inspecionar novamente** até que a imagem esteja visualmente impecável.

### Estágio E: Persistência de Checkpoint em Disco
1. Atualizar a tabela da **Seção 3** neste arquivo (`./AGENTS_IMPLEMENTATION_PLAN.md` e `./AGENTS.md`) mudando de `[~]` para `[x]` e registrando o caminho dos prints aprovados em `.agents/visual_qa/`.
2. Salvar as cenas modificadas (`EditorSceneManager.SaveOpenScenes()`) e realizar commit local (`git add . && git commit -m "feat(vr-task-XX): verified implementation and visual QA"`).

---

## 3. Tabela de Estado e Checkpoint (Persistência Anti-Token Limit)

*O Agente Orquestrador deve atualizar esta tabela diretamente no arquivo a cada mudança de estado (`[ ]` Pendente, `[~]` Em Execução, `[x]` Concluído e Validado Visualmente).*

| ID | Módulo | Tarefa | Status | Assets (Blender / Áudio / API) | Scripts & Prefabs | Visual QA (Screenshots Validados) |
| :--- | :--- | :--- | :---: | :--- | :--- | :--- |
| **TASK-01** | Core / Base | Infraestrutura Multissensorial + Utilitário de Captura de Tela Automática (`VisualQACapture`) | `[x]` | 6 Áudios Core (`.wav` em `Assets/Audio/Generated/Core/`) | `MaterialShaderHelper`, `VRFeedbackManager`, `InteractableHighlightFeedback`, `VisualQACaptureEditor` | `TASK-01_Menu_Overview.png`, `TASK-01_Menu_DeskZone.png`, `TASK-01_Floresta_Overview.png` |
| **TASK-02** | Core / Base | Ergonomia Exoesqueleto: Force Grab Assistido, Auto-Respawn e Aim Assist Magnético | `[x]` | `ReticleTargetRing.fbx` + `M_Reticle_HoloCyan.mat` (Blender 5.1) | `IAimAssistTarget`, `ThrowAimAssist`, `TargetAimReticle`, `ErgonomicGrabInteractable`, `ErgonomicInteractiveToy.prefab` | `TASK-02_Reticle_Aim.png`, `TASK-02_ForceGrab_Preview.png`, `TASK-02_AutoRespawn_Zone.png` |
| **TASK-03** | Drone Pet | Máquina de Estados do Drone + Mecânica de "Buscar" (*FetchBot* com Feixe Trator) | `[x]` | `TractorBeamEmitter.fbx`, `CyberFetchBaton.fbx`, 2 Áudios (`.wav` em `Assets/Audio/Generated/Task03/`) | `DroneFetchTarget`, `DroneFetchController`, `Task03_SceneAndPrefabBuilder`, `CyberFetchBaton.prefab` | `TASK-03_Drone_TractorBeam.png`, `TASK-03_Fetch_Presenting.png`, `TASK-03_FetchBaton_Desk.png` |
| **TASK-04** | Drone Pet | Reatividade Afetiva (Carinho/Poke, Expressões LED e Dança no Ritmo do Rádio) | `[x]` | 5 Texturas LED + Sprite Coração + 4 Áudios (`.wav` em `Assets/Audio/Generated/Task04/`) + `M_Drone_LED_Visor.mat` | `DroneAffectiveSystem`, `Task04_LEDTextureGenerator`, `Task04_SceneAndPrefabBuilder`, Visor LED Mesh | `TASK-04_Drone_Visor_Happy.png`, `TASK-04_Drone_Petting_Feedback.png`, `TASK-04_Drone_Dancing_Pose.png` |
| **TASK-05** | Drone Pet | Sistema de Celebração de Metas Clínicas (Pirueta, Confetes, Fanfarra e Frases) | `[x]` | 50 Frases Clínicas JSON (`DroneEncouragementPhrases.json`) + 2 Áudios (`fanfare_triumph.wav`, `confetti_cannon_pop.wav`) + 2 Texturas Bubble + `M_Confetti_Multicolor.mat` | `DroneMilestoneCelebration`, `Task05_SceneAndPrefabBuilder`, Canhões Duplos de Confetes, Balão de Fala 3D | `TASK-05_Celebration_Pirouette.png`, `TASK-05_Celebration_SpeechBubble.png`, `TASK-05_Confetti_Explosion.png` |
| **TASK-06** | Menu Hub | Rádio Deck com Fitas Cassete Magnéticas (`XRSocketInteractor`) + Botão de Eject | `[x]` | 3 FBX Blender 5.1 (`RetroCassetteTape.fbx`, `CassetteDeckSlot.fbx`, `CassetteOrganizerRack.fbx`) + 5 Áudios (`.wav` em `Assets/Audio/Generated/Task06/`) + 10 Materiais | `MusicCassetteTape`, `RadioCassetteSocket`, `CassetteEjectButton`, `Task06_SceneAndPrefabBuilder`, 6 Prefabs | `TASK-06_Radio_Deck_Overview.png`, `TASK-06_Cassette_Tapes_Closeup.png`, `TASK-06_Tape_Inserted_Slot.png` |
| **TASK-07** | Menu Hub | Pistola/Dispenser de Balões Antigravidade (*The Lab Style*) + Física e Estouro | `[x]` | 2 FBX Blender 5.1 (`BalloonGunDispenser.fbx`, `InflatableBalloon.fbx`) + 3 Áudios (`.wav` em `Assets/Audio/Generated/Task07/`) + 14 Materiais | `BalloonInflaterGun`, `FloatingBalloon`, `Task07_SceneAndPrefabBuilder`, 2 Prefabs (`BalloonGunDispenser`, `InflatableBalloon`) | `TASK-07_BalloonGun_Desk.png`, `TASK-07_Balloon_Inflating.png`, `TASK-07_Balloons_Floating_Room.png` |
| **TASK-08** | Menu Hub | Mini Basquete Arcade de Mesa com Botão Spawner, Aro com Duplo Sensor e Placar 3D | `[ ]` | Tabela/Aro + Bola + Botão (`Blender MCP`) | - | - |
| **TASK-09** | Menu Hub | Bancada Fidget 3D (Alavanca de Clima/Skybox, Botão de Fogos e Mascote Bobblehead) | `[ ]` | Alavanca, Botão e Bobblehead (`Blender MCP`) | - | - |
| **TASK-10** | Menu Hub | Seletor de Mapas por Esferas de Portal 3D com Mini-Dioramas e Pedestal | `[ ]` | Esfera Portal + Pedestal (`Blender MCP`) | - | - |
| **TASK-11** | Floresta | Sistema de Maçãs Interativas (Árvores Sacudíveis + Cestas de Pontuação + Splash) | `[ ]` | Maçãs + Cesta de Vime (`Blender MCP`) | - | - |
| **TASK-12** | Floresta | Sementes Mágicas e Pinhões Arremessáveis (Brotação Procedural de Flores/Cogumelos) | `[ ]` | Pinhão + Flores + Cogumelos (`Blender MCP`) | - | - |
| **TASK-13** | Floresta | Fauna e Flora Reativa (Cogumelos Xilofone Pentatônicos, Pássaros e Lanternas) | `[ ]` | Cogumelos, Pássaro, Lanterna + Áudio | - | - |
| **TASK-14** | Gamificação | Trilha de Moedas/Orbes Flutuantes com Atração Magnética para Exoesqueleto | `[ ]` | Moeda de Energia (`Blender MCP`) | - | - |
| **TASK-15** | Gamificação | Sistema de Missões no Visor do Drone + Totens de Checkpoint com Alavanca e Baú | `[ ]` | Totem com Alavanca e Baú (`Blender MCP`) | - | - |
| **TASK-16** | Gamificação | Persistência (`JSON Save`) + Estante de Troféus Físicos Desbloqueáveis no Menu | `[ ]` | 4 Troféus + Coroa do Drone (`Blender MCP`) | - | - |

---

## 4. Especificações Profundas de Engenharia e Critérios de Visual QA por Tarefa

### FASE 1: Fundação, Captura Visual e Ergonomia de Exoesqueleto

#### `TASK-01`: Infraestrutura Multissensorial + Utilitário de Captura Visual (`VRFeedbackManager` & `VisualQACapture`)
* **1. Pesquisa & Arquitetura:**
  * Identificar a versão do `XR Interaction Toolkit` e o Render Pipeline ativo (Built-in, URP ou HDRP) para criar um helper de materiais (`MaterialShaderHelper.cs`) que sempre atribui o shader padrão correto (`Standard` ou `Universal Render Pipeline/Lit`), eliminando qualquer risco de material rosa nas tasks seguintes.
* **2. Geração de Assets (Python / Gemini API):**
  * Gerar 6 arquivos `.wav` procedurais em `Assets/Audio/Generated/Core/`: `ui_hover_soft.wav`, `grab_pop.wav`, `release_woosh.wav`, `button_click_mech.wav`, `socket_snap.wav`, `success_chime.wav`.
* **3. Implementação C# Detalhada:**
  * `VRFeedbackManager.cs`: Singleton com *Object Pool* de 16 `AudioSource` 3D, variação de pitch e Haptics universal.
  * `InteractableHighlightFeedback.cs`: *Scale Punch* elástico suave + *Emission Highlight* via `MaterialPropertyBlock` ao receber Hover do raio laser.
  * `VisualQACaptureEditor.cs` (em `Assets/Editor/VisualQA/`): Script de Editor capaz de criar temporariamente uma câmera de inspeção iluminada que enquadra automaticamente os `Renderer.bounds` de qualquer `GameObject` ou cena, renderiza imagens `1920x1080` de múltiplos ângulos (Frente, 45° Isométrico, Topo/Perfil) para `.agents/visual_qa/` e limpa a câmera temporária em seguida.
* **4. Visual QA & Validação:**
  * Testar o `VisualQACaptureEditor` tirando um print da cena atual do Menu Principal e da Floresta para mapear onde ficam a mesa, o rádio, o drone e a trilha.

#### `TASK-02`: Ergonomia de Exoesqueleto (`ErgonomicGrabInteractable` & `ThrowAimAssist`)
* **1. Pesquisa & Arquitetura:**
  * Configurar os `XRRayInteractor` do jogador com `useForceGrab = true` e detecção assistida para que o paciente no exoesqueleto nunca precise se curvar para pegar itens no chão.
* **2. Geração de Assets (Blender MCP):**
  * Modelar `ReticleTargetRing.fbx` (anel tecnológico segmentado de `0.25m` de diâmetro, achatado, cor ciano emissiva) para indicar no chão qual objeto está na mira do raio laser.
* **3. Implementação C# Detalhada:**
  * `ErgonomicGrabInteractable.cs`: Retorno suave automático (`Auto-Respawn`) se o brinquedo cair longe da mesa do menu + multiplicador ergonômico de força de arremesso + cálculo de parábola balística (`ThrowAimAssist`) que ajuda suavemente objetos arremessados a caírem dentro de cestas/aros próximos.
* **4. Visual QA & Validação:**
  * Capturar print da retícula 3D posicionada sob um objeto interativo e validar que não está enterrada no chão nem com shader quebrado.

---

### FASE 2: Evolução do Drone Pet (Estilo *FetchBot* do SteamVR)

#### `TASK-03`: Máquina de Estados do Drone + Mecânica de "Buscar" (`DroneFetchBehavior`)
* **1. Pesquisa & Arquitetura:**
  * Inspecionar o Prefab e os scripts atuais do Drone no projeto para acoplar a máquina de estados de busca sem interferir nos painéis de telemetria do paciente.
* **2. Geração de Assets (Blender MCP & Audio):**
  * Modelar `TractorBeamEmitter.fbx` (módulo compacto acoplado na base inferior do drone) e `CyberFetchBaton.fbx` (bastão/brinquedo tecnológico colorido de `0.22m` para brincar de jogar para o drone).
  * Gerar `tractor_beam_hum.wav` e `drone_fetch_alert.wav`.
* **3. Implementação C# Detalhada:**
  * `DroneFetchTarget.cs` & `DroneFetchController.cs`: Quando o jogador arremessa uma maçã, bolinha ou bastão para longe (`> 3.0m`), o drone emite um bip alegre, voa até o item, ativa um feixe trator cônico luminoso (`LineRenderer` + partículas ascendentes), levanta o objeto e traz de volta flutuando na altura do peito do paciente (`1.15m` do chão, ideal para a postura do exoesqueleto).
* **4. Visual QA & Validação:**
  * Tirar print em *Close-up* do Drone com o emissor acoplado e o feixe trator ativo segurando o bastão; verificar se o feixe sai exatamente do centro inferior do drone e se o bastão não clipa na malha do drone.

#### `TASK-04`: Reatividade Afetiva do Drone (Carinho, Cutucada e Dança com o Rádio) (`DroneAffectiveSystem`)
* **1. Pesquisa & Arquitetura:**
  * Analisar a face/visor frontal do Drone para exibir expressões dinâmicas e configurar colisores de carinho no topo da cabeça.
* **2. Geração de Assets (Gemini API / Python Imaging & Audio):**
  * Gerar texturas de expressões faciais em estilo LED (`^‿^` Feliz, `♥‿♥` Carinho, `@_@` Cutucado, `♪‿♪` Dançando) e 4 sons robóticos expressivos (`drone_purr.wav`, `drone_giggle.wav`, `drone_boop.wav`).
* **3. Implementação C# Detalhada:**
  * `DroneAffectiveSystem.cs`: Detecta carinho contínuo com a mão VR (disparando vibração de ronronar no controle, partículas de coração e olhinhos felizes), reação elástica ao ser cutucado e sincronia rítmica (`GetSpectrumData` do `AudioSource` do rádio) fazendo o drone balançar no ritmo da música.
* **4. Visual QA & Validação:**
  * Capturar print frontal do visor do Drone exibindo cada expressão LED e as partículas de reação, garantindo que a textura dos olhos esteja alinhada com a frente do modelo 3D.

#### `TASK-05`: Comemoração de Metas Clínicas (`DroneMilestoneCelebration`)
* **1. Pesquisa & Arquitetura:**
  * Integrar com os contadores de passos/objetivos da caminhada.
* **2. Geração de Assets (Gemini API):**
  * Gerar via Gemini API o banco `Assets/Resources/DroneEncouragementPhrases.json` com 50 mensagens curtas e motivadoras em português.
* **3. Implementação C# Detalhada:**
  * `DroneMilestoneCelebration.cs`: Ao atingir metas, o drone faz uma pirueta 360° no ar, dispara canhões duplos de confete colorido, toca fanfarra e exibe uma mensagem motivadora em um balão 3D legível (`TextMeshPro`).
* **4. Visual QA & Validação:**
  * Tirar print do momento da celebração com o balão de texto ativo e verificar se o texto não está invertido, cortado ou sobreposto à geometria do drone.

---

### FASE 3: Sala do Menu Principal (Parquinho Interativo estilo *The Lab* & *XRI Demo*)

#### `TASK-06`: Rádio Deck com Fitas Cassete Magnéticas (`RadioCassetteDeckSystem`)
* **1. Pesquisa & Arquitetura:**
  * Inspecionar o rádio já existente na cena do Menu Principal (posicionamento, escala e botões de volume atuais) para posicionar o deck e as fitas ao lado dele de forma harmônica.
* **2. Geração de Assets (Blender MCP & Audio):**
  * Modelar `RetroCassetteTape.fbx` (`0.10m x 0.065m x 0.012m`, com 2 carretéis internos separados na hierarquia para girarem via script e etiqueta colorida) e `CassetteDeckSlot.fbx` com botão físico vermelho de **EJECT**.
* **3. Implementação C# Detalhada:**
  * `MusicCassetteTape.cs` & `RadioCassetteSocket.cs`: Sistema com `XRSocketInteractor` magnético. Ao aproximar qualquer uma das 4 fitas coloridas, o rádio puxa a fita com som mecânico de *clack*, gira os carretéis internos e troca a música da cena. Apertar o botão físico de *Eject* faz a fita saltar fisicamente de volta para a mesa.
* **4. Visual QA & Validação:**
  * Tirar print da mesa do Menu Principal mostrando o rádio existente, o deck e as 4 fitas cassete organizadas sobre a mesa. Verificar se as fitas estão perfeitamente apoiadas na superfície da mesa (sem flutuar e sem atravessar o tampo) e se a fita encaixada no socket fica na orientação correta.

#### `TASK-07`: Pistola/Dispenser de Balões Antigravidade (`BalloonGunAndFloatSystem`)
* **1. Pesquisa & Arquitetura:**
  * Reproduzir a pistola infladora de balões do *The Lab* da Valve.
* **2. Geração de Assets (Blender MCP):**
  * Modelar `BalloonGunDispenser.fbx` (pistolinha sci-fi ergonômica de `0.22m` com cilindro de hélio lateral colorido e bico cônico frontal, pivô exatamente na empunhadura para encaixar na mão VR) e `InflatableBalloon.fbx` (malha de balão liso brilhante com nó na base).
* **3. Implementação C# Detalhada:**
  * `BalloonInflaterGun.cs` & `FloatingBalloon.cs`: Segurar o gatilho infla um balão na ponta do bico com som de borracha esticando; soltar o gatilho libera o balão com cor vibrante sorteada, cordinha pendurada (`LineRenderer`) e empuxo antigravidade suave. Apontar o laser e atirar (ou tocar nele) estoura o balão com partículas e som de *POP!*.
* **4. Visual QA & Validação:**
  * Simular o disparo de 4 balões via código no Editor, tirar print da sala do Menu com os balões flutuando e a pistola sobre a mesa, validando que o ponto de inflar coincide exatamente com a ponta do bico da pistola.

#### `TASK-08`: Mini Basquete Arcade de Mesa (`ArcadeHoopMinigame`)
* **1. Pesquisa & Arquitetura:**
  * Encontrar uma parede ou espaço livre visível na frente/lateral da mesa na sala do Menu Principal para instalar a tabela de basquete sem tampar menus existentes.
* **2. Geração de Assets (Blender MCP):**
  * Modelar `ArcadeBasketballHoop.fbx` (tabela com moldura neon, aro metálico laranja de `0.38m` de diâmetro e rede cônica), `MiniBasketball.fbx` (bola laranja com gomos pretos de `0.18m`) e `ArcadeSpawnButton.fbx` (botão fliperama de mesa com legenda "BALL").
* **3. Implementação C# Detalhada:**
  * `BasketballHoopScorer.cs` & `ArcadeBallSpawner.cs`: Duplo sensor de passagem no aro (detecta cestas normais e cestas limpas *Swish!*), placar digital 3D com contador de pontos/recorde, buzina de cesta e botão na mesa que invoca bolas na frente do jogador.
* **4. Visual QA & Validação:**
  * Tirar print a partir da posição da câmera do jogador no Menu olhando para a tabela de basquete e para o botão na mesa. Confirmar que o placar digital está legível e que a bola passa com folga por dentro do anel do aro.

#### `TASK-09`: Bancada Fidget 3D (`DesktopFidgetToys`)
* **1. Pesquisa & Arquitetura:**
  * Criar uma estação de brinquedos táteis (*fidgets*) no canto da mesa do Menu Principal.
* **2. Geração de Assets (Blender MCP):**
  * Modelar `EnvironmentControlLever.fbx` (caixa com alavanca articulada de 3 posições: Dia / Pôr do Sol / Noite), `FireworksLaunchButton.fbx` (botão vermelho de emergência/festa) e `ExoMascotBobblehead.fbx` (mini bonequinho cabeçudo simpático de `0.16m` de altura com cabeça separada do corpo).
* **3. Implementação C# Detalhada:**
  * `AtmosphereLeverInteractable.cs` (muda a iluminação da cena suavemente ao puxar a alavanca), `FireworksButtonInteractable.cs` (dispara fogos de artifício coloridos no céu ao apertar) e `BobbleheadSpringToy.cs` (oscilador harmônico amortecido que faz a cabeça do boneco balançar ao ser tocado pelo laser ou pela mão).
* **4. Visual QA & Validação:**
  * Tirar print em close-up dos 3 brinquedos lado a lado na mesa e tirar 3 prints comparativos da iluminação (Dia, Pôr do Sol e Noite) acionando a alavanca via script para provar que a transição visual está bonita e sem estourar a exposição.

#### `TASK-10`: Seletor de Mapas por Esferas de Portal 3D (`WorldPortalSphereSelector`)
* **1. Pesquisa & Arquitetura:**
  * Manter os botões de UI originais funcionando e adicionar o pedestal 3D com as esferas de mundo estilo *The Lab*.
* **2. Geração de Assets (Blender MCP):**
  * Modelar `PortalOrbDiorama_Forest.fbx` (esfera translúcida de `0.18m` contendo mini pinheiros, riacho e mini maçãs lá dentro) e `PortalPedestalSocket.fbx` (base tecnológica luminosa de encaixe).
* **3. Implementação C# Detalhada:**
  * `PortalWorldSphere.cs`: Permite carregar a cena da Floresta tanto encaixando a esfera no pedestal quanto puxando a esfera em direção ao próprio rosto/headset com transição de *Fade*.
* **4. Visual QA & Validação:**
  * Tirar print da esfera com o mini-diorama visível dentro dela (verificando se o material externo da redoma está realmente transparente/semitransparente no Render Pipeline atual e não opaco).

---

### FASE 4: Mapa da Floresta (Microinterações de Cenário durante a Caminhada)

#### `TASK-11`: Sistema de Maçãs Interativas, Árvores Sacudíveis e Cestas (`ForestAppleInteractionSystem`)
* **1. Pesquisa & Arquitetura:**
  * Abrir/inspecionar a cena da Floresta para mapear as coordenadas exatas da trilha de caminhada do paciente, as árvores próximas e as maçãs existentes.
* **2. Geração de Assets (Blender MCP & Audio):**
  * Modelar `InteractiveRedApple.fbx` (`0.085m`), `GoldenBonusApple.fbx` e `WickerCollectionBasket.fbx` (cesta de vime aberta de `0.50m` de largura com placa de madeira indicando pontos).
* **3. Implementação C# Detalhada:**
  * `InteractiveApple.cs`, `ShakeableFruitTree.cs` & `AppleCollectionBasket.cs`:
    * Apontar o raio laser para a copa das árvores e apertar o gatilho sacode os galhos, solta folhas e derruba maçãs físicas que quicam até o chão.
    * O jogador puxa as maçãs do chão com `Force Grab` (sem precisar abaixar com o exoesqueleto) e pode jogá-las longe (para o Drone buscar), jogá-las dentro das cestas de vime ao longo da trilha (ganhando pontos e confetes) ou na água (efeito de *splash*).
* **4. Visual QA & Validação:**
  * Tirar print da trilha da Floresta mostrando as cestas de vime posicionadas nas margens da trilha e as maçãs no chão/cesta, garantindo que nenhuma cesta esteja flutuando acima do terreno ou bloqueando o caminho onde o paciente passa.

#### `TASK-12`: Sementes Mágicas e Pinhões Arremessáveis (`MagicSeedThrowables`)
* **1. Pesquisa & Arquitetura:**
  * Criar objetos pequenos na beira da trilha que transformam o cenário onde caem.
* **2. Geração de Assets (Blender MCP):**
  * Modelar `GlowingPineconeSeed.fbx` (pinhão com detalhes dourados/ciano), `SproutingFlowerPatch.fbx` (conjunto alegre de flores coloridas e grama com pivô na base) e `BouncyMushroomCluster.fbx`.
* **3. Implementação C# Detalhada:**
  * `MagicSeedPinecone.cs`: Quando arremessado pelo jogador, ao atingir o chão da floresta, abre-se em partículas e faz brotar suavemente (animação elástica de escala `0` -> `1`) um canteiro de flores luminosas alinhado à inclinação do terreno.
* **4. Visual QA & Validação:**
  * Simular o impacto de uma semente no terreno da Floresta, tirar print do canteiro de flores brotado e validar que as flores nascem rentes ao solo com a orientação vertical correta.

#### `TASK-13`: Fauna e Flora Reativa ao Laser (`ForestReactiveEnvironment`)
* **1. Pesquisa & Arquitetura:**
  * Espalhar elementos reativos nas bordas da trilha que respondem instantaneamente ao toque do raio laser durante a marcha.
* **2. Geração de Assets (Blender MCP & Síntese Pentatônica Python):**
  * Modelar `XylophoneMushroom.fbx` (cogumelo colorido bioluminescente), `RunePathLantern.fbx` (lanterna rústica de madeira/pedra de `0.90m` de altura) e `LowPolyForestBird.fbx` (passarinho azul/amarelo com asas articuladas).
  * Sintetizar 5 notas de marimba na escala pentatônica (`C4, D4, E4, G4, A4`).
* **3. Implementação C# Detalhada:**
  * `MusicalMushroom.cs` (toca notas musicais harmoniosas e faz *squash & stretch* ao passar o laser), `ReactiveForestBird.cs` (canta e voa em curva Bezier para a próxima árvore à frente guiando o jogador) e `PathLanternInteractable.cs` (acende fogo dourado ao clicar com o laser).
* **4. Visual QA & Validação:**
  * Tirar print na cena da Floresta enquadrando um grupo de 5 cogumelos musicais, uma lanterna acesa e um pássaro pousado, conferindo cores, iluminação da chama e escala em relação às árvores.

---

### FASE 5: Gamificação Completa, Objetivos e Sala de Troféus

#### `TASK-14`: Trilha de Moedas/Orbes com Atração Magnética Adaptativa (`AdaptiveCoinTrailSystem`)
* **1. Pesquisa & Arquitetura:**
  * Distribuir moedas ao longo do eixo central da caminhada para premiar cada metro percorrido com o exoesqueleto.
* **2. Geração de Assets (Blender MCP & Audio):**
  * Modelar `EnergyStarCoin.fbx` (moeda octogonal dourada/ciano de `0.28m` com estrela central em relevo) e gerar som cristalino de coleta com pitch progressivo.
* **3. Implementação C# Detalhada:**
  * `CollectibleOrbCoin.cs` & `CoinTrailSpawner.cs`: Moedas giratórias que voam magneticamente para o jogador quando ele se aproxima a menos de `2.0m` caminhando, além de moedas bônus laterais que podem ser coletadas apontando o raio laser.
* **4. Visual QA & Validação:**
  * Tirar print em perspectiva da trilha da Floresta mostrando a fileira de moedas flutuando a `1.1m` de altura ao longo do caminho do jogador.

#### `TASK-15`: Checklist de Missões no Visor do Drone + Totens de Checkpoint (`SessionQuestAndCheckpointManager`)
* **1. Pesquisa & Arquitetura:**
  * Conectar todas as interações da Floresta (maçãs, lanternas, sementes, moedas, passos) em um sistema de missões visível no Drone e checkpoints físicos na trilha.
* **2. Geração de Assets (Blender MCP):**
  * Modelar `CheckpointRewardTotem.fbx` (totem estilizado de madeira e pedra na lateral da trilha com alavanca lateral e baú de recompensas no topo).
* **3. Implementação C# Detalhada:**
  * `SessionQuestManager.cs` & `InteractiveCheckpointTotem.cs`: Exibe 3 missões dinâmicas com barras de progresso; ao puxar a alavanca de um totem de checkpoint na trilha, o baú abre com chuva de moedas e salva o progresso.
* **4. Visual QA & Validação:**
  * Tirar print do Totem de Checkpoint posicionado na margem da trilha e do painel de missões ativo no Drone, garantindo legibilidade perfeita das fontes.

#### `TASK-16`: Persistência de Progresso + Troféus Físicos Desbloqueáveis no Menu (`TrophyRoomProgressionSystem`)
* **1. Pesquisa & Arquitetura:**
  * Transformar as conquistas das sessões em troféus físicos interativos permanentes na sala do Menu Principal.
* **2. Geração de Assets (Blender MCP):**
  * Modelar 4 troféus colecionáveis: `Trophy_GoldenApple.fbx`, `Trophy_LuminousBonsai.fbx`, `Trophy_StarCup.fbx` e `Trophy_DroneCrownHat.fbx` (uma coroa dourada que o jogador pode pegar na mesa e **colocar na cabeça do Drone Pet** via `XRSocketInteractor`!).
* **3. Implementação C# Detalhada:**
  * `RehabProgressionSaveSystem.cs` & `TrophyShelfManager.cs`: Sistema de save JSON com flag `unlockAllForDemo = true` ativada por padrão para que todos os troféus já apareçam na mesa prontos para teste imediato.
* **4. Visual QA & Validação Final Geral:**
  * Tirar print panorâmico final da sala do Menu Principal com todos os troféus, a coroa equipada no topo do Drone Pet, o rádio com fitas, a pistola de balões, os brinquedos Fidget e a tabela de basquete, gerando o relatório final `VISUAL_QA_REPORT.md`.

---

## 5. Prompt de Disparo (Para Enviar pelo App do Celular junto com o Anexo)

Copie e envie exatamente este texto junto com o arquivo `.md` anexado:

```text
Estou te enviando em anexo o plano mestre de arquitetura e implementação (AGENTS_IMPLEMENTATION_PLAN.md).

O Editor da Unity JÁ ESTÁ ABERTO no computador. Execute obrigatoriamente este PASSO ZERO antes de começar as tasks:
1. Copie INTEGRALMENTE o conteúdo do arquivo em anexo e salve-o fisicamente na raiz do meu workspace em dois locais: `./AGENTS_IMPLEMENTATION_PLAN.md` e `./AGENTS.md`.
2. Crie as pastas `./.agents/specs/` e `./.agents/visual_qa/`.

Após salvar os arquivos no disco, atue como Orquestrador Mestre e execute da TASK-01 até a TASK-16 em ordem estrita, UMA TAREFA POR VEZ, invocando subagentes dedicados (`invoke_subagent`) para que cada tarefa tenha esforço, pesquisa e qualidade máximos.

Para CADA TASK, o subagente é OBRIGADO a cumprir os 5 estágios:
1. Pesquisa do projeto atual e criação da Spec em `.agents/specs/TASK-XX_Spec.md`;
2. Geração dos modelos 3D no Blender MCP (`.fbx`), áudios procedurais em Python (`.wav`) e dados via Gemini API;
3. Implementação completa dos scripts C# (Unity XRI) + execução de Editor Script no Unity aberto para montar os Prefabs e posicioná-los na cena com 0 erros no Console;
4. LOOP DE VISUAL QA OBRIGATÓRIO: Renderizar/capturar screenshots em alta resolução da cena/objeto na Unity (`.agents/visual_qa/TASK-XX_*.png`), INSPECIONAR VISUALMENTE A IMAGEM GERADA procurando por shaders rosas, objetos flutuando, escala errada ou UI cortada, e corrigir automaticamente na Unity/Blender até que o print esteja visualmente perfeito;
5. Atualizar a tabela de progresso no arquivo `./AGENTS_IMPLEMENTATION_PLAN.md` e `./AGENTS.md` marcando `[x]` antes de avançar para a próxima task.

Comece agora pelo Passo Zero e inicie imediatamente a TASK-01.
```
