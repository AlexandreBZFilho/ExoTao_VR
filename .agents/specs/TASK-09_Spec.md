# Especificação Técnica — TASK-09: Bancada Fidget 3D (Alavanca de Clima, Botão de Fogos e Mascote Bobblehead)

**Módulo:** Menu Hub (Rehab Gamification)  
**Projeto:** VR Rehab Gamification — ExoTao (Unity 6 LTS 6000.0.85f2)  
**Status:** Em Implementação (`[~]`)  
**Data:** 10/11/2026  

---

## 1. Visão Geral e Contexto Clínico

A **Bancada Fidget 3D** é um conjunto de brinquedos e instrumentos táteis de mesa projetados para promover engajamento sensorial imediato, relaxamento pré e pós-exercício, e calibração motora fina para pacientes em exoesqueleto ou cadeira de rodas.

No contexto de neurorreabilitação (ExoTao), pacientes frequentemente enfrentam fadiga neuromuscular e ansiedade de desempenho. A bancada fidget proporciona:
1. **Sensação de Agência e Controle do Ambiente:** A alavanca de clima permite ao paciente transformar instantaneamente a atmosfera da clínica virtual (Dia ensolarado, Pôr do Sol sereno e Noite relaxante sob o luar).
2. **Recompensa e Celebração Espontânea:** O botão de lançamento de fogos de artifício dá ao paciente a capacidade de comemorar um bom desempenho a qualquer instante com explosões festivas e estalidos coloridos pelo teto da sala.
3. **Estímulo Lúdico Contínuo:** O mascote Bobblehead Chibi responde a qualquer toque sutil, raio laser ou impacto de objetos (bolas, fitas cassete) balançando a cabeça de forma cômica e elástica via física oscilatória harmônica amortecida.

---

## 2. Métricas Espaciais e Escala VR (1 Unidade = 1 Metro)

| Componente | Parâmetro | Dimensão VR | Descrição / Função |
| :--- | :--- | :--- | :--- |
| **Alavanca de Clima (`EnvironmentControlLever`)** | Base (L x P x A) | `0.16m x 0.12m x 0.055m` | Base angular tecnológica com canaleta de arco circular, 3 LEDs/entalhes (`Dia -30°`, `Pôr do Sol 0°`, `Noite +30°`) e indicadores emissivos |
| | Haste e Manopla | Haste `0.14m`, Bola `Ø 0.04m` | Haste de liga de titânio articulada na base com manopla esférica ergonômica emissiva |
| **Botão de Fogos (`FireworksLaunchButton`)** | Base (L x P x A) | `0.12m x 0.12m x 0.05m` | Base metálica chanfrada com chanfros de segurança, placa gravada "FIREWORKS / FESTA" e anel de LED |
| | Domo do Botão | Diâmetro `0.08m`, Altura `0.025m` | Domo translúcido vermelho/dourado com curso elástico de 12mm no eixo Y |
| **Mascote Bobblehead (`ExoMascotBobblehead`)** | Altura Total | `0.165m` | Proporção chibi (cabeça ~0.08m, corpo ~0.065m, pedestal ~0.02m) |
| | Pedestal / Base | Diâmetro `0.09m`, Altura `0.02m` | Base circular ponderada com anel de borracha antiderrapante |
| | Cabeça Móvel | L x P x A: `0.08m x 0.075m x 0.08m` | Cabeça cibernética estilizada com visor LED emissivo, orelhas/antenas e pivô angular na junção do pescoço |

---

## 3. Coordenadas Espaciais no Menu Principal (`Menu_Principal.unity`)

Os 3 brinquedos são organizados na extremidade direita frontal da mesa principal de reabilitação (`full_desk_3 (1)`):

- **Alavanca de Clima:**
  - Posição: `(0.66m, 0.955m, 0.62m)`
  - Rotação: `(0°, 75°, 0°)` (acompanhando o alinhamento da borda da mesa e do rádio)
- **Botão de Fogos:**
  - Posição: `(0.79m, 0.955m, 0.60m)`
  - Rotação: `(0°, 75°, 0°)` (acessível na borda frontal da mesa ao alcance de mão ou laser)
- **Mascote Bobblehead:**
  - Posição: `(0.73m, 0.955m, 0.72m)`
  - Rotação: `(0°, 200°, 0°)` (ligeiramente voltado para o paciente no centro da sala)
- **Reposicionamento do pequeno cacto (`Deco Variant 3`):**
  - Movido para `(0.56m, 0.955m, 0.60m)` para servir de charmoso fundo decorativo sem colidir com a bancada.

---

## 4. Arquitetura de Software e Parâmetros

### 4.1 `AtmosphereLeverInteractable.cs`
- Controla os estados de iluminação do ambiente através de máquina de estados de 3 entalhes:
  - `Day` (Entalhe -30°): Sol dourado brilhante (`color = (1.0, 0.96, 0.84)`, `intensity = 1.0`, `rot = (83, 330, 0)`), Céu claro (`exposure = 1.0`, `tint = (0.58, 0.58, 0.58)`), luz ambiente quente.
  - `Sunset` (Entalhe 0°): Sol poente dourado-alaranjado (`color = (1.0, 0.52, 0.20)`, `intensity = 0.85`, `rot = (22, 310, 0)`), Céu avermelhado crepuscular (`exposure = 0.80`, `tint = (1.0, 0.45, 0.30)`), luz ambiente âmbar.
  - `Night` (Entalhe +30°): Luar prateado azulado (`color = (0.30, 0.40, 0.75)`, `intensity = 0.25`, `rot = (65, 140, 0)`), Céu noturno escuro (`exposure = 0.35`, `tint = (0.15, 0.18, 0.35)`), luz ambiente azulada.
- Suporta interação via clique de laser (`XRSimpleInteractable`), toque físico da mão VR ou avanço manual por script no Editor.
- Interpolação suave em Coroutine (`Lerp`/`Slerp` em 1.5s) com amortecimento suave.
- Áudio: `lever_click_notch.wav` ao travar o entalhe + `atmosphere_transition_whoosh.wav` durante a transição.
- Indicadores LED no painel da alavanca com emissão acesa para o estado ativo.

### 4.2 `FireworksButtonInteractable.cs`
- Componente acoplado ao botão mecânico com `XRSimpleInteractable` e colisor físico.
- Animação elástica do domo com compressão de 12mm no eixo Y e retorno amortecido.
- Sistema de Partículas de Fogos de Artifício (`FireworksRocketSystem`):
  - Foguete ascendente com cauda de faíscas partindo da base e subindo até o teto alto da sala (`Y = 4.2m`).
  - Explosão esférica multicolorida com 120+ partículas com gravidade suave, cintilação e rastro de fumaça dourada/ciano/magenta.
  - Flash de luz pontual dinâmico (`Light`) sincronizado com o estouro.
- Áudio: `fireworks_launch_whistle.wav` no lançamento e `fireworks_burst_explosion.wav` na detonação.

### 4.3 `BobbleheadSpringToy.cs`
- Oscilador harmônico amortecido em 2 eixos angulares:
  $$\ddot{\theta} + 2\zeta\omega_n \dot{\theta} + \omega_n^2 \theta = 0$$
  - Frequência natural $\omega_n \approx 13.5\text{ rad/s}$
  - Coeficiente de amortecimento $\zeta \approx 0.14$
- Entradas de impulso:
  - Clique com raio laser / gatilho XRI: aplica impulso angular aleatório na cabeça;
  - Toque direto com mão do jogador (`OnTriggerEnter` / `XRBaseInteractable`);
  - Colisão física com projéteis ou brinquedos arremessáveis (Mini Basquete, Fitas Cassete, Bastão de Busca) via `OnCollisionEnter`.
- Áudio: `bobblehead_spring_boing.wav` com modulação procedural de pitch de acordo com a amplitude do impacto.
- Feedback háptico via `VRFeedbackManager`.

---

## 5. Assets Necessários

### 5.1 Modelos 3D (Blender 5.1 CLI em `Assets/Models/Generated/Task09/`)
1. `EnvironmentControlLever.fbx`:
   - `Lever_Chassis`: Base tecnológica angular chanfrada com fenda guia curva em arco.
   - `Lever_Indicator_Day`, `Lever_Indicator_Sunset`, `Lever_Indicator_Night`: Pequenos domos LED na carcaça.
   - `Lever_Handle_Pivot`: Objeto articulado com pivô na base inferior, haste cilíndrica e manopla esférica (`Lever_Knob`).
2. `FireworksLaunchButton.fbx`:
   - `Button_Chassis`: Base hexagonal/chanfrada com moldura chanfrada de policarbonato escuro.
   - `Button_Bezel_Chrome`: Anel de retenção metálico cromado.
   - `Button_Dome`: Domo de acionamento móvel com pivô para afundamento no eixo Y.
   - `Button_Label_Plate`: Plaqueta decorativa com relevo "FIREWORKS".
3. `ExoMascotBobblehead.fbx`:
   - `Bobble_Pedestal`: Base pesada estilizada com anel ciano e logotipo ExoBot.
   - `Bobble_Torso`: Corpo chibi com peitoral cibernético, manoplas e micro braços fixos.
   - `Bobble_Spring_Neck`: Mola helicoidal visível no pescoço.
   - `Bobble_Head_Pivot`: Cabeça chibi expressiva (pivô na junção do pescoço) com capacete robótico, antenas laterais e visor LED curvo emissivo.

### 5.2 Efeitos Sonoros Procedurais (`.wav` em `Assets/Audio/Generated/Task09/`)
1. `lever_click_notch.wav`: Clique mecânico pesado e metálico de chave seletora com ressonância.
2. `atmosphere_transition_whoosh.wav`: Brisa atmosférica profunda com vento estéreo e chimes etéreos.
3. `fireworks_launch_whistle.wav`: Assobio agudo ascendente com chiado de propelente pirotécnico.
4. `fireworks_burst_explosion.wav`: Estouro grave e retumbante com estalidos secundários e decaimento estéreo.
5. `bobblehead_spring_boing.wav`: Som elástico de mola de desenho animado com harmônicos metálicos oscilantes.

---

## 6. Critérios de Visual QA (1920x1080)
1. `TASK-09_Fidget_Station_Desk.png`: Enquadramento em ângulo de trabalho mostrando os 3 brinquedos organizados harmonicamente na mesa, materiais Standard limpos (0 shaders rosas), sem clipping e perfeitamente apoiados na madeira do tampo.
2. `TASK-09_Atmosphere_Day.png`: Visão geral do Menu na iluminação de Dia (sol dourado claro, céu aberto e alavanca em -30°).
3. `TASK-09_Atmosphere_Sunset.png`: Visão geral do Menu na iluminação de Pôr do Sol (iluminação alaranjada suave, céu avermelhado e alavanca em 0°).
4. `TASK-09_Atmosphere_Night.png`: Visão geral do Menu na iluminação de Noite com fogos de artifício multicoloridos explodindo no teto alto e alavanca em +30°.
