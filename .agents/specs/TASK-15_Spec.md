# Especificação Técnica TASK-15: Sistema de Missões no Visor do Drone + Totens de Checkpoint com Alavanca e Baú (SessionQuestAndCheckpointManager)

## 1. Visão Geral e Propósito Clínico
Na reabilitação neuromotora e treino de marcha com o exoesqueleto robótico ExoTao, estabelecer objetivos de curto e médio prazo claros e imediatamente visíveis reduz a fadiga percebida e potencializa a adesão do paciente. A **TASK-15** introduz dois pilares essenciais de gamificação terapêutica no cenário da Floresta:
1. **Painel de Missões Ativas no Visor do Drone Pet (`SessionQuestManager`)**:
   Exibe em tempo real o progresso de 3 metas terapêuticas multissensoriais que incentivam tanto a passada contínua quanto o alcance dos membros superiores com o raio laser VR.
2. **Totem de Checkpoint Interativo com Alavanca e Baú de Recompensas (`InteractiveCheckpointTotem`)**:
   Um marco imersivo rústico-mágico posicionado na margem da trilha antes da ponte, que recompensa a chegada a meio-percurso com uma rica experiência mecânica e audiovisual (puxar alavanca, abertura do baú com tesouro dourado, feixe de luz e pontuação comemorativa).

---

## 2. Especificação Métrica e Ergonomia VR (1 unidade = 1 metro)

### Totem de Checkpoint (`CheckpointRewardTotem.fbx`)
Projetado em escala biométrica para visualização e acionamento confortável por pacientes em postura ereta no exoesqueleto (altura do peito/olhos ~1.20m a 1.45m):
- **Altura Total do Totem**: `~1.32m` (topo do baú na altura do peito do paciente).
- **Base de Pedra Chanfrada (`Totem_StoneBase`)**:
  - Dimensões: `0.52m x 0.52m x 0.22m`.
  - Geometria: Bloco octogonal chanfrado em rocha granítica, garantindo ancoragem sólida no solo do terreno.
- **Pilar Central de Madeira Rúnica (`Totem_WoodPillar` & `Totem_RuneInlays`)**:
  - Dimensões: `0.32m x 0.32m x 0.82m`.
  - Geometria: Tronco de carvalho entalhado com reforços de ferro forjado e sulcos de runas nórdico-tecnológicas luminosas que brilham em ciano pulsante.
- **Alavanca Mecânica Lateral Articulada (`Totem_LeverMount`, `Totem_LeverPivot`, `Totem_LeverHandle`)**:
  - Posicionamento: Lateral direita do pilar, a `0.92m` do chão.
  - Haste metálica de `0.26m` com manopla esférica ergonômica de `0.08m` de diâmetro (cor vermelha/laranja de alta visibilidade).
  - Ponto de Pivô (`Totem_LeverPivot`): Eixo Z/X permitindo rotação angular de `0°` (em repouso elevado a +35°) até `55°` (abaixada e travada ao acionar).
- **Baú de Recompensas no Topo (`Totem_ChestBase`, `Totem_ChestLidPivot`, `Totem_ChestInteriorGold`)**:
  - Dimensões do Baú: `0.42m` de largura, `0.30m` de profundidade, `0.26m` de altura.
  - Tampa Abaulada Articulada (`Totem_ChestLid`): Pivô traseiro na dobradiça (`Totem_ChestLidPivot`) abrindo de `0°` (fechado) até `80°` (aberto para cima).
  - Interior do Baú: Pilhas de moedas de ouro 3D em alto-relevo e cristais reluzentes com brilho especular e emissivo dourado.

---

## 3. Dinâmica Interativa da Alavanca e Abertura do Baú

### Interação VR
- Suporte duplo ergonômico:
  1. **Acionamento à Distância via Raio Laser VR** (`XRSimpleInteractable` com `useForceGrab`/gatilho ou hover).
  2. **Acionamento por Toque / Puxada Direta** com a mão/controlador do paciente.
- Ao acionar:
  1. A alavanca desce suavemente de sua pose de repouso até a posição travada em `0.45s` com som mecânico `totem_lever_pull.wav`.
  2. A tampa do baú se eleva em arco senoidal suave de `0°` a `80°` em `0.85s` com som rangente `chest_open_creak.wav`.
  3. Uma coluna de luz ascendente dourada e ciano projeta-se para o céu (`Light` + partículas de confetes e faíscas cintilantes).
  4. Dispara som etéreo de checkpoint atingido `checkpoint_activate_hum.wav`.
  5. Concede `+100 Pontos Bônus` diretamente ao `CoinScoreManager`.
  6. Dispara celebração no Drone Pet: *"Checkpoint 1 Alcançado! Progresso da sessão salvo com sucesso!"*.
  7. As runas do pilar e o tesouro permanecem radiantes para marcar que o checkpoint já foi completado.

---

## 4. Sistema de Missões Clínicas Dinâmicas (`SessionQuestManager`)

O sistema gerencia as 3 missões fundamentais da sessão na Floresta:
1. **Missão 1: Marcha Ativa**
   - *Objetivo Clínico*: Estimular ciclos contínuos de passada e manutenção do ritmo simétrico.
   - *Alvo*: Dar 15 passos (ou avançar 10 metros se passos em tempo real não estiverem calibrados).
   - *Fonte de Dados*: `TelemetriaClinica.Instancia.totalPassos` ou deslocamento horizontal do jogador.
2. **Missão 2: Amigo da Floresta**
   - *Objetivo Clínico*: Incentivar flexão anterior do tronco ou arremesso balístico guiado.
   - *Alvo*: Colher 2 maçãs na cesta (`AppleCollectionBasket`) ou acender 2 lanternas rúnicas (`PathLanternInteractable`).
   - *Fonte de Dados*: Eventos `OnAppleScoredEvent` e `OnLanternIgnitedEvent`.
3. **Missão 3: Magia Botânica**
   - *Objetivo Clínico*: Praticar alcance e mira com os membros superiores via laser VR ou caminhar pela trilha.
   - *Alvo*: Coletar 8 moedas de energia (`CoinScoreManager`) ou brotar 1 canteiro de flores/cogumelos (`SproutingPlantLifecycle`).
   - *Fonte de Dados*: Eventos `OnCoinCollectedEvent` e `OnPlantSproutedEvent`.

### Recompensa e Notificação de Conclusão:
- Ao completar cada uma das 3 missões:
  - Toca jingle triunfante `quest_complete_jingle.wav`.
  - Dispara celebração completa do Drone Pet (`DroneMilestoneCelebration` - TASK-05) com pirueta 360°, confetes e balão de mensagem motivadora.
  - Concede `+50 Pontos Bônus` por missão concluída.
  - Atualiza a barra de progresso para 100% e acende o marcador `[✓]` neon ciano/dourado no visor do Drone.

---

## 5. Interface Holográfica de Missões no Visor do Drone Pet

- **Hierarquia no Drone**:
  Sub-painel dedicado acoplado lateralmente ou no topo do `Canvas_Holograma_Clinico` do Drone Pet (`[IC] Drone_Guia_IA_ExoBot`):
  - Nome do GameObject: `Painel_Missoes_Floresta`.
  - Dimensões do Painel: `360 x 180` pixels no espaço de Canvas do Drone, com fundo translúcido escuro de holograma médico e bordas em ciano.
  - 3 Linhas de Missão contendo:
    - Ícone de Status: Caixa de seleção neon com marcador `[  ]` que se transforma em `[✓]` luminoso verde-ciano ao concluir.
    - Título da Missão (`TextMeshProUGUI` tamanho 13-14, nítido e legível).
    - Barra de Progresso Horizontal com fundo cinza-azulado escuro e preenchimento gradual ciano/ouro.
    - Contador numérico textual: `"10/15"`, `"1/2"`, `"5/8"`.

---

## 6. Mapeamento Espacial na Cena da Floresta (`Floresta_e_Rio.unity`)
- **Posicionamento do Totem**:
  - Localização: Margem esquerda da trilha de terra, na aproximação final antes da ponte de madeira.
  - Coordenadas 3D: `(40.20, terrenoY + 0.02, 68.30)`.
  - Rotação: `(0, 155, 0)` para que a alavanca e a face do baú fiquem voltadas diagonalmente para o paciente conforme ele caminha pela trilha.
- **Posicionamento das Câmeras de Visual QA**:
  - `TASK-15_Drone_Quest_Panel.png`: Câmera focada diretamente no visor do Drone Pet exibindo o painel de missões com textos nítidos e barras de progresso.
  - `TASK-15_Checkpoint_Totem_Trail.png`: Perspectiva em três quartos mostrando o Totem firmemente ancorado na margem da trilha florestal ao lado das árvores e do rio.
  - `TASK-15_Checkpoint_Chest_Open.png`: Close-up focado no baú aberto revelando a alavanca abaixada, a tampa inclinada a 80°, o tesouro reluzente e os efeitos de celebração.
