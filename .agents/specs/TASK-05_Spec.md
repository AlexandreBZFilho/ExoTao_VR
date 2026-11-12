# Especificação Técnica de Alta Precisão — TASK-05: Celebração de Metas Clínicas do Drone Pet (Pirueta Acrobática Otimizada, Confetes VR-Safe, Fanfarra Suave & Balão de Fala Holográfico Ergonômico)

**Documento Técnico:** `.agents/specs/TASK-05_Spec.md`  
**Autor:** Subagente [A] — Forensic Spec, Furniture Mapper & Playtest Bug Scout  
**Módulo:** Drone Pet / Companheiro Robótico IA de Reabilitação  
**Status:** APROVADO PARA EXECUÇÃO  
**Data:** 11/11/2026  
**Versão do Unity:** 6000.0.85f2 (Unity 6)  
**Versão do XR Interaction Toolkit:** 2.6.5  
**Pipeline de Renderização:** Built-in Render Pipeline / SRP-Agnostic (Compatível com SRP Batcher e GPU Instancing via `MaterialShaderHelper`)  
**Cenas Afetadas:** `Assets/Scenes/Menu_Principal.unity` e `Assets/Scenes/Floresta_e_Rio.unity`  
**Prefabs e Scripts Afetados:** `[IC] Drone_Guia_IA_ExoBot`, `DroneMilestoneCelebration.cs`, `DroneAffectiveSystem.cs`, `DroneCompanionFollowAndGrab.cs`, `Task05_SceneAndPrefabBuilder.cs`  

---

## 1. Diagnóstico Forense & Investigação de Causa Raiz

Uma inspeção cirúrgica e analítica no código-fonte de runtime (`Assets/Scripts/Interactive/Task05/DroneMilestoneCelebration.cs`), nos assets gráficos (`Assets/Models/Generated/Task05/` e `Assets/Textures/Generated/Task05/`), nos arquivos de áudio (`Assets/Audio/Generated/Task05/`), no banco de dados de linguagem (`Assets/Resources/DroneEncouragementPhrases.json`) e na integração com a cinemática do `DroneCompanionFollowAndGrab` (TASK-03) e do `DroneAffectiveSystem` (TASK-04) revelou **cinco inconformidades críticas** que comprometem a estabilidade física, a ergonomia clínica e a taxa de quadros (90 FPS) em VR:

```
┌──────────────────────────────────────────────────────────────────────────────────────────────────┐
│                                   DIAGNÓSTICO FORENSE DO TASK-05                                 │
├──────────────────────────────────────────────────────────────────────────────────────────────────┤
│ 1. VIOLAÇÃO DA ANCORAGEM FÍSICA DA TASK-03 (ROTAÇÃO E MOVIMENTO DIRETO NO ROOT TRANSFORM):       │
│    • Em 'DroneMilestoneCelebration.UpdateCelebrationSequence' (linhas 427–434), o código executa:│
│        transform.rotation = transform.rotation * Quaternion.Euler(...);                          │
│    • Além disso, 'DroneGuiaIA.cs' (linhas 333–336) executava 'rotBase *= Quaternion.Euler(...)'. │
│    • Modificar o 'transform.rotation' ou 'transform.position' do nó raiz do Drone altera a       │
│      orientação base de acompanhamento, distorce o vetor 'offsetLocal' memorizado e pode empurrar│
│      fisicamente a zona de interação ou colidir com o paciente!                                  │
│                                                                                                  │
│ 2. OBSTRUÇÃO E DESATIVAÇÃO INDEVIDA DO HUD CLÍNICO DE MARCHA:                                    │
│    • O balão de fala foi posicionado na mesma altura do HUD Holográfico ('Y = 0.58m')!          │
│    • Para contornar essa sobreposição, o script desativava o HUD com:                            │
│        Transform hudT = transform.Find("Canvas_Holograma_Clinico"); hudT.SetActive(false);       │
│    • IMPACTO CLÍNICO CRÍTICO: O paciente e o fisioterapeuta perdiam a visão de contagem de      │
│      passos, gráfico de simetria e cadência biomecânica exatamente no momento da conquista!     │
│                                                                                                  │
│ 3. ÁUDIO DE FANFARRA E CANHÃO ESTRIDENTES COM PICOS DIGITAIS PERIGOSOS EM VR:                   │
│    • Análise espectral revelou 'fanfare_triumph.wav' com pico digital em -0.72 dBFS (30145/32767)│
│    • 'confetti_cannon_pop.wav' também atinge -0.72 dBFS. Em fones VR próximos ao ouvido,        │
│      esse volume quase saturado causa susto, desconforto acústico agudo e distorção harmônica!   │
│    • Ausência de corte de altas frequências (sem filtro passa-baixas <= 3800 Hz).                │
│                                                                                                  │
│ 4. ORÇAMENTO DE CONFETES EXCEDIDO COM CUSTO DE SOMBRAS E COLISÃO EM VR:                          │
│    • O sistema anterior disparava 40 partículas por canhão (total de 80 partículas simultâneas). │
│    • Não desativava 'ps.collision.enabled' (gerando cheques de PhysX desnecessários na CPU).    │
│    • Não desativava 'castShadows' e 'receiveShadows' nos renderers dos confetes, gerando passes  │
│      extras de profundidade nos dois olhos do headset VR.                                        │
│                                                                                                  │
│ 5. CONFLITO DE SOBREPOSIÇÃO VISUAL ENTRE 'DroneMilestoneCelebration' E 'DroneAffectiveSystem':   │
│    • Ambos os scripts tentam comandar 'Modelo_3D_ExoBot' em 'LateUpdate'/'Update' sem arbitragem.│
│    • Durante a pirueta, o wobble ou a dança do sistema afetivo competia com a rotação 360°,     │
│      gerando engasgos visuais (*jitter*).                                                        │
└──────────────────────────────────────────────────────────────────────────────────────────────────┘
```

### 1.1. Causa Raiz 1: Violação de Isolamento Cinemático (Linha 428 de `DroneMilestoneCelebration.cs`)
* **Localização:** `Assets/Scripts/Interactive/Task05/DroneMilestoneCelebration.cs`, método `UpdateCelebrationSequence()`:
  ```csharp
  // CÓDIGO PROBLEMÁTICO IDENTIFICADO:
  if (droneGuia != null && !droneGuia.estaArrastandoDrone)
  {
      transform.rotation = transform.rotation * Quaternion.Euler(pirouettePitchOffset * Time.deltaTime * 4f,
                                                                (celebrationTimer <= pirouetteDuration ? (360f / pirouetteDuration) : 0f) * Time.deltaTime,
                                                                pirouetteRollOffset * Time.deltaTime * 4f);
  }
  ```
* **Mecanismo de Falha:** O Drone possui o componente `DroneCompanionFollowAndGrab` (TASK-03) anexado ao nó raiz `[IC] Drone_Guia_IA_ExoBot`. Esse componente memoriza o vetor `offsetLocal` em coordenadas relativas ao Yaw do paciente. Ao aplicar rotações e translações procedurais de pirueta diretamente ao `transform.rotation` e `transform.position` do nó raiz, a pirueta altera a orientação global do Drone, desorienta os eixos do `XRGrabInteractable` e pode arremessar a âncora para longe ou aproximar o Drone bruscamente do paciente.
* **Correção Obrigatória:** A pirueta acrobática (rotação Yaw 360°, pitch de $-10^\circ$, roll de $14^\circ$ e micro-swoop vertical de $+0.14\text{m}$) deve ser aplicada **estrita e exclusivamente às coordenadas locais do nó filho visual `Modelo_3D_ExoBot`**:
  $$\vec{p}_{\text{visualLocal}}(t) = \vec{p}_{\text{rest}} + \begin{pmatrix} 0 \\ h_{\text{swoop}} \cdot \sin(\pi t / T) \\ 0 \end{pmatrix}$$
  $$\mathbf{q}_{\text{visualLocal}}(t) = \mathbf{q}_{\text{rest}} \times \text{Quaternion.Euler}(\theta_{\text{pitch}}(t), \theta_{\text{yaw}}(t), \phi_{\text{roll}}(t))$$
  O nó raiz (`[IC] Drone_Guia_IA_ExoBot`) **permanece 100% estático em relação ao paciente**, sem qualquer modificação em `transform.position`, `transform.rotation` ou `offsetLocal`.

---

### 1.2. Causa Raiz 2: Posicionamento Conflitante do Balão e Desativação do HUD Clínico
* **Localização:** Linhas 98 e 473 de `DroneMilestoneCelebration.cs`:
  ```csharp
  public float bubbleElevationOffset = 0.58f; // MESMO Y DO HUD!
  ...
  Transform hudT = transform.Find("Canvas_Holograma_Clinico") ?? transform.Find("HUD_Holograma_Drone");
  if (hudT != null) hudT.gameObject.SetActive(false); // DESLIGAMENTO INACEITÁVEL DO HUD
  ```
* **Mecanismo de Falha:** Em `DroneGuiaIA.cs` (linha 1150), o `Canvas_Holograma_Clinico` é construído com `localPosition = (0f, 0.58f, 0f)`, `sizeDelta = (460, 200)` e escala métrica `0.0022`, ocupando o intervalo vertical de $Y = 0.36\text{m}$ a $Y = 0.80\text{m}$. Ao configurar `bubbleElevationOffset = 0.58f`, o balão de fala 3D caía exatamente no mesmo centro tridimensional do painel médico. Para evitar Z-fighting, o autor anterior simplesmente desligou o HUD durante a celebração.
* **Impacto Clínico:** Durante o treino com exoesqueleto, o momento de celebração é quando o paciente mais precisa confirmar o número de passos superados e a estabilização da sua simetria articular. Apagar o HUD desorienta a percepção de biofeedback.
* **Correção Obrigatória:**
  1. Elevar o `bubbleElevationOffset` para **$0.96\text{m}$** (ou $0.98\text{m}$). O balão de fala ($500 \times 240$, escala $0.0012$, altura de $0.288\text{m}$) ocupará a faixa de $Y = 0.82\text{m}$ a $Y = 1.10\text{m}$, flutuando elegantemente **acima** do HUD clínico, com sua cauda apontando para baixo em direção ao topo do visor do Drone.
  2. **Eliminar 100% de qualquer chamada a `hudT.gameObject.SetActive(false)`.** O HUD clínico de telemetria permanece continuamente visível, legível e ativo antes, durante e depois de qualquer celebração.

---

### 1.3. Causa Raiz 3: Calibração de Volume e Espectro Acústico (Auditoria de Áudio)
* **Localização:** `Assets/Audio/Generated/Task05/fanfare_triumph.wav` e `confetti_cannon_pop.wav`.
* **Evidência Medida:**
  * `fanfare_triumph.wav`: Duração = 2.40s, Mono, 44100Hz, **Pico = -0.72 dBFS** (30145/32767).
  * `confetti_cannon_pop.wav`: Duração = 1.35s, Mono, 44100Hz, **Pico = -0.72 dBFS** (30145/32767).
* **Impacto em VR:** Em sistemas com áudio espacial próximo à orelha (alto-falantes de tiras do Meta Quest 2/3/Pro), sinais sonoros com pico em $-0.72\text{ dBFS}$ atingem volumes desconfortáveis, provocando reflexo de sobressalto no paciente neurológico e fadiga auditiva precoce.
* **Correção Obrigatória:**
  1. Re-sintetizar os arquivos de áudio via Subagente [B] com o nome padronizado `drone_milestone_fanfare.wav` (mantendo `fanfare_triumph.wav` como alias idêntico para compatibilidade total).
  2. Impor pico máximo digital absoluto estrito de **$\le -8.5\text{ dBFS}$** (alvo recomendado: $-9.5\text{ dBFS}$ a $-10.0\text{ dBFS}$).
  3. Aplicar filtragem passa-baixas ativa com corte em **$\le 3800\text{ Hz}$** (suavizando o brilho estridente dos metais sintetizados).
  4. Configurar o `AudioSource` dedicado: `spatialBlend = 1.0f` (3D), `rolloffMode = Logarithmic`, `minDistance = 1.0f`, `maxDistance = 12.0f`, e volume calibrado em `0.22f`.

---

### 1.4. Causa Raiz 4: Orçamento de Partículas e Otimização VR para Confetes
* **Evidência:** No script gerador anterior, cada canhão disparava um burst de 40 partículas com tempo de vida de até 2.5 segundos, totalizando 80 partículas ativas simultâneas. O renderer dos canhões mantinha `shadowCastingMode = On` e `receiveShadows = true`, e o módulo `ps.collision` não estava desativado.
* **Impacto em VR:** Partículas transparentes com sombras ativas forçam o renderizador a realizar passes de sombra volumosos para cada olho, reduzindo a taxa de quadros e correndo risco de causar *motion sickness*.
* **Correção Obrigatória:**
  1. **Teto Estrito de Partículas:** Máximo de **40 partículas ativas simultâneas no total da cena** (20 partículas disparadas por canhão lateral).
  2. **Renderização Leve:** `rend.shadowCastingMode = ShadowCastingMode.Off`, `rend.receiveShadows = false`, `rend.lightProbeUsage = LightProbeUsage.Off`.
  3. **PhysX Zero-Cost:** `var col = ps.collision; col.enabled = false;` (confetes caem puramente por gravidade e arrasto aerodinâmico sem calcular colisão com a malha da sala ou terreno).
  4. **GPU Instancing:** Material `M_Confetti_Multicolor.mat` com `enableInstancing = true`.

---

### 1.5. Causa Raiz 5: Arbitragem entre `DroneMilestoneCelebration` e `DroneAffectiveSystem`
* **Mecanismo:** O `DroneAffectiveSystem` (TASK-04) controla `Modelo_3D_ExoBot` para carinho, cutucão e dança rítmica musical. Se uma celebração for disparada enquanto uma música toca no rádio, o `DroneAffectiveSystem` tentaria aplicar `wobbleRotOffset` ao mesmo tempo em que a pirueta 360° gira o modelo.
* **Correção Obrigatória:**
  1. Durante a celebração (`isCelebrating == true`), o `DroneMilestoneCelebration` assume controle prioritário da pose local de `Modelo_3D_ExoBot`.
  2. O `DroneAffectiveSystem` detecta a celebração (`celebrationSystem != null && celebrationSystem.IsCelebrating`) e pausa momentaneamente seus cálculos de wobble físico e dança.
  3. O `DroneMilestoneCelebration` chama `affectiveSystem.SetExpression(DroneLEDExpression.Happy)` no início do evento, garantindo que o visor LED mostre o sorriso alegre cibernético durante todo o espetáculo.
  4. Na conclusão da celebração, a pose local é devolvida suavemente à pose de repouso (`modelRestPos`, `modelRestRot`) via `Quaternion.Slerp` e `Vector3.Lerp`, permitindo que o sistema afetivo retome o controle com total fluidez.

---

## 2. Arquitetura da Máquina de Estados de Celebração & Mapeamento de Gatilhos Clínicos

```
                                  ┌────────────────────────────────────────────────────────┐
                                  │               FONTES DE TELEMETRIA & GAMEPLAY          │
                                  └───────────────────────────┬────────────────────────────┘
                                                              │
             ┌────────────────────────────────┬───────────────┴────────────────┬───────────────────────────────┐
             │                                │                                │                               │
             ▼                                ▼                                ▼                               ▼
┌─────────────────────────┐      ┌─────────────────────────┐      ┌─────────────────────────┐     ┌─────────────────────────┐
│     TelemetriaClinica   │      │       DroneGuiaIA       │      │  AppleCollectionBasket  │     │InteractiveCheckpointTotem│
│(totalPassos, simetria,  │      │(Estado: CelebrandoMeta) │      │ (Cesta: totalScore % 50)│     │(Alavanca ativada +100pts│
│ distanciaPercorrida)    │      └────────────┬────────────┘      └────────────┬────────────┘     └────────────┬────────────┘
└────────────┬────────────┘                   │                                │                               │
             │ Passos (a cada 15/25)          │ Estado alterado                │ Cesta atingida                │ Totem desbloqueado
             │ Simetria (> 80% pós-alerta)    │                                │                               │
             │ Distância (a cada 25m)         │                                │                               │
             └────────────────────────────────┼────────────────────────────────┴───────────────────────────────┘
                                              │
                                              ▼
                               ┌─────────────────────────────┐
                               │  DroneMilestoneCelebration  │
                               │  TriggerCelebration(cat, msg│
                               └──────────────┬──────────────┘
                                              │
        ┌─────────────────────────────────────┼─────────────────────────────────────┐
        │                                     │                                     │
        ▼                                     ▼                                     ▼
┌───────────────────────────┐   ┌───────────────────────────┐   ┌───────────────────────────┐
│    PIRUETA & SWOOP 3D     │   │   CONFETES DUPLOS VR      │   │  BALÃO HOLOGRÁFICO 3D     │
│   (EXCLUSIVO NO FILHO)    │   │ (40 PARTÍCULAS / NO-SHAD) │   │ (Y = 0.96m / AUTO-BILLB.) │
├───────────────────────────┤   ├───────────────────────────┤   ├───────────────────────────┤
│• 360° Yaw via SmoothStep  │   │• 20 part. canhão esquerdo │   │• Posicionado acima do HUD │
│• Swoop vertical +0.14m    │   │• 20 part. canhão direito  │   │• HUD Clínico NUNCA apaga  │
│• Pitch (-10°) / Roll (14°)│   │• Sem colisão PhysX (0 CPU)│   │• Frases PT-BR do JSON     │
│• Nó raiz 100% INTACTO     │   │• Sem sombras dinâmicas    │   │• Billboard para o Headset │
│• LED Face: Happy (^[__]^) │   │• Fanfarra: <= -8.5 dBFS   │   │• Entrada elástica suave   │
└───────────────────────────┘   └───────────────────────────┘   └───────────────────────────┘
```

### 2.1. Mapeamento Exaustivo dos Gatilhos de Celebração

| ID do Gatilho | Origem do Evento | Condição Disparadora | Categoria Selecionada | Mensagem Padrão / Dinâmica | Ação Multissensorial |
| :--- | :--- | :--- | :---: | :--- | :--- |
| **TRIG-01** | `TelemetriaClinica` / `DroneGuiaIA` | `totalPassos >= lastStep + stepsMilestoneInterval` (a cada 15 ou 25 passos) | `Passos` | Sorteada de `passos` em `DroneEncouragementPhrases.json` (ex: *"Meta de passos superada!..."*) | Fanfarra suave, 40 confetes, pirueta 360°, LED Happy, balão acima do HUD. |
| **TRIG-02** | `TelemetriaClinica` | `indiceSimetriaMarcha >= 80%` após período de alerta (`wasSymmetryPoor`), passos $\ge 8$, cooldown $25\text{s}$ | `Simetria` | Sorteada de `simetria` no JSON (ex: *"Simetria perfeita! Ambas as pernas trabalhando com equilíbrio!"*) | Fanfarra suave, 40 confetes, pirueta 360°, LED Happy, balão acima do HUD. |
| **TRIG-03** | `TelemetriaClinica` | `distanciaPercorridaMetros >= lastDist + distanceMilestoneInterval` (a cada 25m) | `Distancia` | Sorteada de `distancia` no JSON (ex: *"Nova marca de distância conquistada!..."*) | Fanfarra suave, 40 confetes, pirueta 360°, LED Happy, balão acima do HUD. |
| **TRIG-04** | `DroneGuiaIA` | FSM interna muda para `EstadoDrone.CelebrandoMeta` | `Passos` / `Geral` | Frase contextual de passos ou superação motora | Sincroniza máquina de estados do Drone Guia sem duplicação de pirueta. |
| **TRIG-05** | `AppleCollectionBasket` (TASK-11) | `totalScore % 50 == 0` ao encestar maçãs na Floresta | `Geral` | `"Cesta de Maçãs! Total: {totalScore} pontos conquistados!"` | Drone celebra a pontuação lúdica do minigame de arremesso. |
| **TRIG-06** | `InteractiveCheckpointTotem` (TASK-15) | Paciente puxa alavanca do totem na trilha | `Geral` | `"Checkpoint 1 Salvo! Baú de Tesouro Desbloqueado!"` | Drone comemora o marco geográfico alcançado na caminhada. |
| **TRIG-07** | `CoinScoreManager` (TASK-14) | Coleta de moeda secreta ou combo de 10 moedas | `Geral` | `"Combo de Moedas Magnéticas! Ritmo sensacional!"` | Reforço positivo imediato para coleta de itens na marcha. |
| **TRIG-08** | `SessionQuestManager` (TASK-15) | Conclusão de objetivo de missão da sessão | `Geral` | `"Missão de Marcha Concluída com Sucesso!"` | Celebração máxima de fechamento de ciclo de reabilitação. |
| **TRIG-09** | `DroneCrownWearable` (TASK-16) | Paciente equipa a coroa dourada no Drone | `Geral` | `"Coroa Real Equipada! Companheiro de Reabilitação Honorário!"` | Fanfarra triunfante e pirueta coroada. |

---

## 3. Coreografia Acrobática com Isolamento Físico Estrito da TASK-03

### 3.1. Formulação Matemática da Pirueta

A coreografia de celebração possui duração total de $T_{\text{total}} = 3.8\text{ segundos}$, dividida em duas fases bem delineadas:
* **Fase 1: Pirueta Acrobática Ativa ($0 \le t \le 2.0\text{s}$, $T_{\text{pir}} = 2.0\text{s}$):**
  1. *Rotação Yaw Suave (360° em torno do eixo Y local do modelo):*
     $$\tau = \frac{t}{T_{\text{pir}}}, \quad s(\tau) = \tau^2(3 - 2\tau) \quad (\text{SmoothStep})$$
     $$\theta_{\text{yaw}}(t) = 360^\circ \cdot s(\tau)$$
  2. *Swoop Vertical Harmônico (Micro-arco de ascensão elástica):*
     $$y_{\text{local}}(t) = y_{\text{rest}} + h_{\text{swoop}} \cdot \sin(\pi \tau), \quad h_{\text{swoop}} = 0.14\text{m}$$
  3. *Inclinação Dinâmica Acrobática (Roll e Pitch para sensação orgânica de voo):*
     $$\phi_{\text{roll}}(t) = 14^\circ \cdot \sin(2\pi \tau)$$
     $$\theta_{\text{pitch}}(t) = -10^\circ \cdot \sin(\pi \tau)$$
  4. *Composição Quaternária Local do Modelo:*
     $$\mathbf{q}_{\text{acrobat}}(t) = \text{Quaternion.Euler}(\theta_{\text{pitch}}(t), \theta_{\text{yaw}}(t), \phi_{\text{roll}}(t))$$
     $$\mathbf{q}_{\text{visualLocal}}(t) = \mathbf{q}_{\text{rest}} \times \mathbf{q}_{\text{acrobat}}(t)$$
* **Fase 2: Assentamento Suave e Leitura Confortável ($2.0\text{s} < t \le 3.8\text{s}$):**
  O modelo visual relaxa gradualmente de volta à pose de repouso através de amortecimento exponencial crítico:
  $$\mathbf{q}_{\text{visualLocal}}(t) = \text{Quaternion.Slerp}(\mathbf{q}_{\text{visualLocal}}, \mathbf{q}_{\text{rest}}, \Delta t \cdot 6.0)$$
  $$\vec{p}_{\text{visualLocal}}(t) = \text{Vector3.Lerp}(\vec{p}_{\text{visualLocal}}, \vec{p}_{\text{rest}}, \Delta t \cdot 6.0)$$
  Enquanto isso, o balão holográfico permanece estável no topo para leitura despreocupada do paciente.

### 3.2. Garantias Inegociáveis de Não-Interferência na Cinemática da TASK-03

```
┌──────────────────────────────────────────────────────────────────────────────────────────────────┐
│                             GARANTIAS DE ISOLAMENTO CINEMÁTICO TASK-03                           │
├──────────────────────────────────────────────────────────────────────────────────────────────────┤
│ 1. IMUTABILIDADE DO ROOT TRANSFORM:                                                              │
│    • 'transform.position' do nó raiz '[IC] Drone_Guia_IA_ExoBot' NUNCA é lido ou alterado pela   │
│      lógica de pirueta da TASK-05.                                                               │
│    • 'transform.rotation' do nó raiz NUNCA recebe multiplicações ou rotações incrementais.       │
│                                                                                                  │
│ 2. PRESERVAÇÃO INTEGRAL DO 'offsetLocal':                                                        │
│    • O vetor 'offsetLocal' memorizado em 'DroneCompanionFollowAndGrab' mantém-se 100% idêntico.  │
│    • A ancoragem definida pelo jogador ao soltar o laser permanece intacta após a celebração.    │
│                                                                                                  │
│ 3. ZERO FORÇA / ZERO EMPURRÃO NO JOGADOR:                                                        │
│    • O colisor do Drone ('BoxCollider') permanece na raiz e segue estritamente a posição da     │
│      caminhada. A pirueta visual do modelo 3D não move colisores nem interatores de agarrar.    │
│    • 'Physics.IgnoreCollision' com o Player Rig continua garantindo que o Drone nunca colida     │
│      nem bloqueie a visão ou passada do paciente.                                                │
└──────────────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 4. Posicionamento Ergonômico do Balão Holográfico & Frases Motivacionais

### 4.1. Espaçamento Vertical Ergonômico (Zero Obstrução do HUD Clínico)

A estrutura de empilhamento vertical do Drone foi reprojetada para manter clara visibilidade de todas as camadas de informação simultâneas:

```
    Y (m)
    ▲
    │  ┌──────────────────────────────────────────────────┐
1.10┼──┤            BALÃO DE FALA MOTIVACIONAL             │ (500x240, scale 0.0012)
    │  │   "Meta de passos superada! Cada passo..."        │ (Texto branco de alto contraste)
0.96┼──┤             [ Centro da Âncora: Y = 0.96m ]      │
    │  └────────────────────────┬─────────────────────────┘
0.82┼───────────────────────────┴── Cauda Apontando p/ Baixo
    │
0.80┼──┌──────────────────────────────────────────────────┐
    │  │       HUD HOLOGRÁFICO CLÍNICO DA REABILITAÇÃO    │ (460x200, scale 0.0022)
    │  │  Passos: 25 | Simetria: 84% | Cadência: 48 p/min │ (SEMPRE ATIVO E VISÍVEL!)
0.58┼──┤             [ Centro do HUD: Y = 0.58m ]         │
    │  └──────────────────────────────────────────────────┘
0.36┼── Topo do Visor LED
    │  ┌─────────────────────────┐
    │  │  Visor LED do Drone     │
0.00┼──┤  Expressão: Happy (^_^) │
    │  │  Corpo 3D do Drone      │
    │  └─────────────────────────┘
    ▼
```

### 4.2. Algoritmo de Auto-Billboard Estável (Sem Inversão de Texto)

Para garantir que o balão seja perfeitamente legível de qualquer altura e ângulo em que o paciente se encontre:
1. O balão acompanha a posição global da âncora:
   $$\vec{P}_{\text{ancoraMundo}} = \vec{P}_{\text{droneRaiz}} + \vec{U}_{\text{mundo}} \cdot y_{\text{elevation}}$$
2. O vetor de visão horizontal em relação à câmera do paciente é calculado:
   $$\vec{D}_{\text{alvo}} = \vec{P}_{\text{cameraPaciente}} - \vec{P}_{\text{ancoraMundo}}$$
   $$\vec{D}_{\text{alvoHorizontal}} = \begin{pmatrix} D_x \\ D_y \cdot 0.20 \\ D_z \end{pmatrix}$$
3. Aplica-se rotação orientada com inversão de 180° no eixo Y (correção da normal do Canvas UI):
   $$\mathbf{R}_{\text{billboard}} = \text{Quaternion.LookRotation}(\vec{D}_{\text{alvoHorizontal}}, \vec{U}_{\text{mundo}}) \times \text{Quaternion.Euler}(0^\circ, 180^\circ, 0^\circ)$$

### 4.3. Animação Elástica de Entrada e Saída (Pop-in & Pop-out)
* **Entrada ($t = 0.0\text{s}$):** Escala elástica via `Mathf.SmoothDamp` de $0.05$ para $1.00$ com leve *overshoot* de até $1.08$, proporcionando toque tátil sci-fi alegre.
* **Saída ($t \ge 3.8\text{s}$):** Escala decrescente amortecida de $1.00$ para $0.00$ em $0.25\text{s}$, desativando o objeto do balão sem micro-alocações per-frame.

### 4.4. Estrutura do Banco de Frases em Português (`Assets/Resources/DroneEncouragementPhrases.json`)
O arquivo JSON contém exatamente 50 frases clinicamente revisadas, distribuídas em 4 categorias essenciais:
* **`passos` (15 frases):** Foco em repetição motora, cadência, ativação neuroplástica e reforço positivo da marcha.
* **`simetria` (15 frases):** Foco em transferência equilibrada de peso, alinhamento pélvico e harmonia bilateral.
* **`distancia` (10 frases):** Foco em resistência muscular, capacidade aeróbica e alcance espacial na trilha.
* **`geral` (10 frases):** Elogios de companheirismo, perseverança clínica e valorização da dedicação do paciente.

---

## 5. Orçamento Rigoroso de Otimização VR para Confetes (PhysX & Rendering)

Para cumprir o padrão estrito de 90 FPS em headsets standalone (como Meta Quest):

```
┌──────────────────────────────────────────────────────────────────────────────────────────────────┐
│                            ORÇAMENTO DE PERFORMANCE VR: CANHÕES DE CONFETES                      │
├──────────────────────────────────────────────────────────────────────────────────────────────────┤
│ • Partículas Ativas Simultâneas: Máximo 40 partículas no total (20 esquerda + 20 direita).       │
│ • Sistema de Emissão: Burst único de 20 partículas por canhão no instante t = 0.0s.              │
│ • Duração de Vida (Lifetime): MinMaxCurve(1.4s, 2.0s) com desvanecimento suave de alpha no fim.  │
│ • Gravidade e Arrasto: 'gravityModifier = 0.85f', 'dampen = 0.38f' (queda realista de confete).  │
│ • Espaço de Simulação: 'ParticleSystemSimulationSpace.World'.                                    │
│ • Custo de Física PhysX: 'ps.collision.enabled = false' (ZERO custo de colisão per-frame!).      │
│ • Custo de Iluminação: 'shadowCastingMode = Off', 'receiveShadows = false',                      │
│   'lightProbeUsage = Off', 'reflectionProbeUsage = Off' (ZERO passes extras de sombra por olho). │
│ • Shading e Batching: 'M_Confetti_Multicolor.mat' com 'enableInstancing = true', compatível      │
│   com SRP Batcher e GPU Instancing.                                                              │
└──────────────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 6. Calibração Acústica VR-Safe & Síntese Sonora

### 6.1. Requisitos Obrigatórios de Áudio

| Arquivo de Áudio | Propósito Clínico | Duração Alvo | Pico Máximo Exigido | Filtro Passa-Baixas | Spatial Blend | Rolloff / Distâncias |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| `drone_milestone_fanfare.wav` (e `fanfare_triumph.wav`) | Fanfarra triunfante suave de celebração | $2.20\text{s} - 2.40\text{s}$ | **$\le -8.5\text{ dBFS}$** (Alvo: $-9.5\text{ dBFS}$) | $\le 3800\text{ Hz}$ | $1.0$ (3D Espacial) | Logarítmico, $1.0\text{m} - 12.0\text{m}$, Vol: $0.22$ |
| `confetti_cannon_pop.wav` | Estouro duplo abafado e suave dos canhões | $0.80\text{s} - 1.20\text{s}$ | **$\le -8.5\text{ dBFS}$** (Alvo: $-10.0\text{ dBFS}$) | $\le 3500\text{ Hz}$ | $1.0$ (3D Espacial) | Logarítmico, $1.0\text{m} - 10.0\text{m}$, Vol: $0.20$ |

### 6.2. Estratégia de Síntese Musical (Subagente [B])
* **Motivo Melódico:** Acorde arpejado maior triunfante em dó maior ($G_4 \to C_5 \to E_5 \to G_5$), com sustentação rica em harmônicos ímpares quentes (timbre de brass sintético caloroso, semelhante a trompete com surdina).
* **Curva de Envelope:** Ataque rápido de $20\text{ms}$ para nitidez rítmica, seguido de decaimento suave e cauda de reverberação de $0.4\text{s}$ com decaimento exponencial, sem cortes abruptos.
* **Normalização Digital:** A amplitude máxima dos samples de 16-bit inteiros é escalada para $0.35$ a $0.37$ do valor máximo de $32767$, cravando o pico medido em $-9.0\text{ dBFS}$ a $-8.6\text{ dBFS}$.

---

## 7. Matriz de Responsabilidades para os Subagentes [B], [C], [D] e [E]

### 7.1. Subagente [B] — `Blender 3D Low-Poly VR & Audio Tech Artist`
1. Re-sintetizar via script Python `generate_task05_audio.py`:
   - `Assets/Audio/Generated/Task05/drone_milestone_fanfare.wav` com pico máximo $\le -8.5\text{ dBFS}$ e corte passa-baixas em $3800\text{ Hz}$.
   - Copiar / criar `fanfare_triumph.wav` com os mesmos dados acústicos seguros.
   - `Assets/Audio/Generated/Task05/confetti_cannon_pop.wav` com pico máximo $\le -8.5\text{ dBFS}$.
2. Validar que as texturas do balão (`SpeechBubble_Panel.png`) e os materiais de confete (`M_Confetti_Multicolor.mat`) estão com GPU Instancing ativo (`enableInstancing = true`).

### 7.2. Subagente [C] — `Senior Unity C# Gameplay, Laser Physics & VR Performance Engineer`
1. Refatorar completamente `Assets/Scripts/Interactive/Task05/DroneMilestoneCelebration.cs`:
   - Localizar e memorizar a pose de repouso de `Modelo_3D_ExoBot`.
   - Executar a pirueta (yaw 360°, swoop +0.14m, pitch e roll) **exclusivamente nas coordenadas locais de `Modelo_3D_ExoBot`**.
   - **Remover 100% de qualquer rotação ou movimento em `transform.rotation` / `transform.position` do nó raiz.**
   - Elevar `bubbleElevationOffset` para $0.96\text{m}$.
   - **Remover terminantemente qualquer chamada a `hudT.gameObject.SetActive(false)`**, mantendo o HUD clínico 100% visível durante toda a celebração.
   - Integrar com `DroneAffectiveSystem`: ativar `DroneLEDExpression.Happy` no início e restaurar para `Normal` ao término; sinalizar pausa do wobble durante a comemoração.
   - Garantir que `AudioSource` use volume calibrado ($0.22f$), `spatialBlend = 1.0f` e clipes de áudio atualizados.

### 7.3. Subagente [D] — `Multi-Furniture Layout, Origin (0,0,0) Purger & Grounding Engineer`
1. Refatorar o script de Editor `Assets/Editor/AutoSetup/Task05_SceneAndPrefabBuilder.cs`:
   - Configurar os canhões de confetes para **máximo 20 partículas por canhão** (40 total), com sombras desligadas (`shadowCastingMode = Off`, `receiveShadows = false`) e colisão desativada (`collision = false`).
   - Ajustar o posicionamento do balão holográfico no topo ($Y = 0.96\text{m}$) e vincular as referências de TextMeshPro e imagens.
   - Executar varredura de duplicatas e limpeza profunda da coordenada `(0, 0, 0)` em ambas as cenas (`Menu_Principal` e `Floresta_e_Rio`).
   - Exportar telemetria espacial `.agents/visual_qa/TASK-05_SpatialAudit.json`.
   - Renderizar as 3 capturas HD de Visual QA (`1920x1080`):
     - `TASK-05_Celebration_Pirouette.png`: Drone no ápice do giro acrobático 360° com confetes e HUD visível.
     - `TASK-05_Celebration_SpeechBubble.png`: Close-up focado no balão holográfico acima do HUD, demonstrando texto em português legível e não invertido.
     - `TASK-05_Confetti_Explosion.png`: Visão frontal ampla com confetes dispersos e estática total do nó raiz.

### 7.4. Subagente [E] — `Ruthless Visual QA & VR Performance Auditor`
1. Inspecionar minuciosamente os prints `.png` e o arquivo `.agents/visual_qa/TASK-05_SpatialAudit.json`.
2. Verificar no checklist:
   - O nó raiz do Drone se moveu ou rodou? (`rootTransformDriftMeters == 0.0f` e `rootRotationDeltaDegrees == 0.0f`).
   - O HUD clínico de marcha continuou ligado e visível?
   - O balão está posicionado acima do HUD sem sobreposição visual?
   - Os áudios cumpriram o teto rigoroso de $\le -8.5\text{ dBFS}$?
   - As partículas de confetes respeitaram o teto de 40 partículas ativas sem sombras?
3. Se qualquer item falhar, emitir ordem de retrabalho; se 100% conforme, aprovar com `PASS`.

---

## 8. Critérios de Aceitação & Protocolo de Visual QA

Para que a TASK-05 seja homologada com status `COMPLETED` na Matriz da Seção 4 do `AGENTS.md`, todas as condições abaixo devem ser satisfeitas:

* [ ] **C-01 (Isolamento Físico Absoluto):** A pirueta e o micro-swoop ocorrem exclusivamente em `Modelo_3D_ExoBot`. O nó raiz `[IC] Drone_Guia_IA_ExoBot` mantém posição global e rotação idênticas às determinadas pelo `DroneCompanionFollowAndGrab`, com $0.00\text{m}$ de deriva no `offsetLocal`.
* [ ] **C-02 (Convivência Ergonômica com HUD Clínico):** O balão holográfico de fala situa-se a $Y \ge 0.96\text{m}$ do centro do Drone, e o `Canvas_Holograma_Clinico` permanece 100% ativo e desobstruído durante toda a comemoração.
* [ ] **C-03 (Calibração Acústica VR-Safe):** `drone_milestone_fanfare.wav` e `confetti_cannon_pop.wav` apresentam pico digital comprovado de $\le -8.5\text{ dBFS}$ e corte passa-baixas em $\le 3800\text{ Hz}$.
* [ ] **C-04 (Orçamento VR de Confetes):** Canhões esquerdo e direito emitem no máximo 20 partículas cada (40 partículas no total), com `collision.enabled = false` e renderers sem sombras dinâmicas.
* [ ] **C-05 (Expressão LED Sincronizada):** O visor LED do Drone comuta instantaneamente para a expressão `Happy` no início da comemoração e retorna ordenadamente ao estado normal após o ciclo.
* [ ] **C-06 (Integração Plena de Gatilhos):** O sistema responde confiavelmente a metas de passos, simetria articular excelente (> 80%), cestas de maçãs e totens de checkpoint na Floresta.
* [ ] **C-07 (Higienização Espacial e Origem Limpa):** Ambas as cenas (`Menu_Principal` e `Floresta_e_Rio`) possuem exatamente uma única instância do Drone, com a coordenada `(0, 0, 0)` rigorosamente limpa e livre de duplicatas órfãs.
* [ ] **C-08 (Telemetria e Evidências Gráficas HD):** `.agents/visual_qa/TASK-05_SpatialAudit.json` exportado com validação de zero conflitos, e as 3 capturas HD (`1920x1080`) aprovadas pelo Auditor [E].
