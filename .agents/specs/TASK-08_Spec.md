# Especificação Técnica — TASK-08: Mini Basquete Arcade de Mesa

**Módulo:** Menu Hub (Rehab Gamification)  
**Projeto:** VR Rehab Gamification — ExoTao (Unity 6 LTS 6000.0.85f2)  
**Status:** Em Implementação (`[~]`)  
**Data:** 10/11/2026  

---

## 1. Visão Geral e Contexto Clínico

O minigame de **Mini Basquete Arcade de Mesa** foi concebido como uma atividade lúdica e de reabilitação motora para membros superiores de pacientes em exoesqueleto ou cadeira de rodas no Menu Principal.
Ele integra:
1. **Botão Arcade Spawner na Mesa:** Posicionado na mesa de reabilitação ao alcance imediato do paciente (toque físico direto ou laser pointer `XRSimpleInteractable`). Ao ser pressionado, despacha uma minibola com feedback sonoro arcade e animação elástica do botão.
2. **Minibola de Basquete (`MiniBasketball`):** Esfera texturizada de basquete estilizada com física elástica (`PhysicMaterial` de quique ~0.75), suporte nativo a `ErgonomicGrabInteractable` (Force Grab assistido, auto-respawn se cair no chão/longe da área de jogo) e integração com `ThrowAimAssist`.
3. **Tabela de Basquete de Parede (`ArcadeBasketballHoop`):** Montada na parede lateral da sala (`X = 0.12m`, `Y = 1.85m`, `Z = 2.70m`), com ângulo voltado diretamente para o paciente (`rot = (0, 90, 0)`), fora do caminho dos menus da interface principal e totalmente visível tanto da posição sentada do jogador (`XR Origin`) quanto da estação da mesa.
4. **Aro com Duplo Sensor de Passagem:** Sensores lógicos (`TriggerTop` e `TriggerBottom`) que validam cestas apenas na direção correta (de cima para baixo), distinguindo cesta limpa (*Swish!*, 3 pontos) de cesta com impacto no aro (2 pontos).
5. **Placar Digital 3D Neon:** Integrado na parte superior da tabela (`TextMeshPro`), exibindo Pontuação Atual, Sequência (*Streak*) e Recorde (*High Score*), com iluminação emissiva pulsante e efeitos de buzina arcade.

---

## 2. Métricas Espaciais e Escala VR (1 Unidade = 1 Metro)

| Componente | Parâmetro | Dimensão VR | Descrição / Função |
| :--- | :--- | :--- | :--- |
| **Tabela (Backboard)** | Largura x Altura x Prof. | `0.70m x 0.50m x 0.035m` | Acrílico escuro translúcido com moldura neon ciano e retângulo guia alvo |
| **Aro (Hoop Rim)** | Diâmetro Interno | `0.38m` | Tubo metálico laranja de alta resistência (`espessura 0.025m`), montado a `Y = 1.72m` |
| **Rede (Hoop Net)** | Comprimento x Diâmetro | `0.35m x 0.38m -> 0.22m` | Malha cônica vazada estilizada com oscilação procedural pós-cesta |
| **Placar Digital 3D** | Largura x Altura | `0.45m x 0.15m` | Display digital em acrílico com LEDs emissivos de alta legibilidade (TMP) |
| **Bola (MiniBasketball)** | Diâmetro | `0.18m` | Bola de basquete clássica com gomos pretos (passa com folga de 20cm no aro de 0.38m) |
| **Botão Spawner Base** | Largura x Comp. x Altura | `0.12m x 0.12m x 0.045m` | Base angular de polímero escuro chanfrada com parafuso decorativo e legenda "BALL" |
| **Botão Spawner Domo** | Diâmetro x Altura | `0.08m x 0.025m` | Domo translúcido vermelho/ciano com mola mecânica e clique físico |

---

## 3. Coordenadas Espaciais no Menu Principal (`Menu_Principal.unity`)

### 3.1 Ponto de Fixação da Tabela (`ArcadeBasketballHoop`)
- **Posição:** `(0.12m, 1.85m, 2.70m)`
- **Rotação:** `(0°, 90°, 0°)` (face voltada para `+X`, perpendicular à parede lateral esquerda e alinhada ao campo de visão do paciente)
- **Distância da Cadeira VR (`XR Origin` em `4.50, 1.67, 2.31`):** `~4.38m` em linha de visada direta com elevação suave (~6° para cima)
- **Distância do Spawner da Mesa (`0.80, 0.955, 0.15`):** `~2.60m` (distância padrão internacional de fliperamas estilo *Pop-A-Shot*)

### 3.2 Ponto de Instalação do Botão Spawner (`ArcadeBallSpawner`)
- **Posição:** `(0.80m, 0.955m, 0.15m)`
- **Rotação:** `(0°, 75°, 0°)` (alinhado com a borda da mesa e ao alcance imediato da mão do paciente ou do raio laser)
- **Spawn Point da Bola:** `(0.80m, 1.05m, 0.05m)` (repouso suave sobre o tampo da mesa)

---

## 4. Arquitetura de Software e Classes

### 4.1 `BasketballHoopScorer.cs`
- Herda de `MonoBehaviour` e implementa `IAimAssistTarget` (para integração direta com `ThrowAimAssist`).
- **Sensores de Passagem:**
  - `TriggerTop`: `SphereCollider` ou `BoxCollider` no plano superior do aro (`Y = 1.76m`).
  - `TriggerBottom`: `BoxCollider` logo abaixo da base do aro (`Y = 1.62m`).
  - Validação de sentido: a bola DEVE entrar no `TriggerTop` primeiro e no `TriggerBottom` dentro de uma janela de tempo de `0.8s`.
- **Detecção de Swish:**
  - Registra colisões mecânicas no aro (`Hoop_Rim_Solid_Collider`).
  - Se a bola pontua sem registrar colisão no aro: `Cesta Limpa (Swish!)` -> `+3 Pontos`, SFX `hoop_swish.wav`, cor de emissão verde neon / dourado.
  - Se a bola tocou no aro antes da cesta: `Cesta Regular` -> `+2 Pontos`, SFX `hoop_rim_clang.wav` + `hoop_swish.wav`, cor de emissão ciano.
- **Placar e Gamificação:**
  - Salva o recorde (*High Score*) via `PlayerPrefs` (`VRRehab_Basketball_HighScore`).
  - Contador de *Streak* (cestas consecutivas sem errar).
  - Animação procedural da rede (escala Y esticada e balanço senoidal amortecido).
  - Partículas de confete / flash neon no aro.
  - SFX `arcade_buzzer.wav` e `hoop_swish.wav`.

### 4.2 `ArcadeBallSpawner.cs`
- Componente acoplado ao botão de mesa com `XRSimpleInteractable`.
- Reage a:
  - Clique físico com colisor de dedo / mão VR;
  - `Select` do `XRRayInteractor` (gatilho do controle);
  - `Hover` com animação suave de realce emissivo (`VRFeedbackManager.PlayHover`).
- Animação do botão: afunda `1.2cm` no eixo Y local e retorna suavemente com som `arcade_button_press.wav` e vibração háptica.
- Gerenciamento de Pool: Máximo de 4 bolas ativas simultâneas. Se uma 5ª bola for invocada, recicla a bola mais antiga não segurada.
- Bola invocada: Instancia `MiniBasketball.prefab` com `ErgonomicGrabInteractable`, `SphereCollider` (raio `0.09m`), `PhysicMaterial` com `bounciness = 0.75`, `friction = 0.4`, e suporte a Force Grab ergonômico.

---

## 5. Áudios Procedurais Necessários (`Assets/Audio/Generated/Task08/`)
1. `basketball_bounce.wav` — Quique oco de borracha sintética em madeira/chão (`0.35s`).
2. `hoop_swish.wav` — Ruído de arremesso limpo esticando as cordas da rede (`0.45s`).
3. `hoop_rim_clang.wav` — Ressonância metálica de impacto no aro de aço (`0.30s`).
4. `arcade_buzzer.wav` — Buzina eletrônica clássica de pontuação de fliperama (`0.60s`).
5. `arcade_button_press.wav` — Clique mecânico com switch tátil microswitch arcade (`0.15s`).

---

## 6. Critérios de Visual QA e Validação (1920x1080)
1. `TASK-08_Basketball_Hoop_Wall.png`: Visão da câmera do jogador sentado olhando para a tabela de basquete montada na parede lateral.
2. `TASK-08_Scoreboard_Closeup.png`: Close-up nítido da tabela, aro metálico, rede e placar digital luminoso.
3. `TASK-08_Ball_Spawn_Button.png`: Visão da mesa mostrando o botão arcade com legenda "BALL" e uma minibola de basquete recém-invocada sobre o tampo.
