# Especificação Forense, Layout Multimóveis e Infraestrutura Core — TASK-01
**Documento Técnico:** `.agents/specs/TASK-01_Spec.md`  
**Autor:** Subagente [A] — Forensic Spec, Furniture Mapper & Playtest Bug Scout  
**Status:** APROVADO PARA EXECUÇÃO  
**Data:** 11/11/2026  

---

## 1. Diagnóstico Forense: Bug do Teleporte Automático no Menu Principal & Neutralização

### 1.1. Causa Raiz Identificada
A investigação nos scripts de gameplay e nos arquivos de cena revelou a origem exata do teleporte indesejado ocorrido durante o playtest VR:

1. **Arquivo `Assets/Scripts/Interactive/Task10/PortalWorldSphere.cs`:**
   * Linhas 207-222 (`ExecuteSceneTransition`): Executa `SceneManager.LoadScene("CenaDeLoading")` ou `SceneManager.LoadScene(targetSceneName)`.
   * Linhas 147-151 (`CheckHeadsetProximity`): Dispara `InitiateTeleportSequence("Headset Proximity")`.
2. **Arquivo `Assets/Scripts/Interactive/Task10/PortalPedestalSocket.cs`:**
   * Linhas 117-144 (`OnSelectEntered`): Ao detectar o encaixe de qualquer `PortalWorldSphere`, inicia a corrotina `SocketCelebrationRoutine(orb)`.
   * Linhas 175-206 (`SocketCelebrationRoutine`): Aguarda `celebrationDuration = 1.2f` e executa:
     ```csharp
     orb.InitiateTeleportSequence("Pedestal Socket Celebration");
     ```
3. **Causa na Cena `Assets/Scenes/Menu_Principal.unity`:**
   * A esfera de portal `PortalOrbDiorama_Forest` foi posicionada em design-time diretamente sobre o berço magnético do pedestal `PortalPedestalSocket`.
   * Ao entrar no modo Play, o componente `XRSocketInteractor` do pedestal detectou imediatamente a esfera no seu volume de gatilho, disparando `OnSelectEntered` no primeiro segundo de execução.
   * Transcorridos os 1.2 segundos da celebração, `InitiateTeleportSequence` disparou o carregamento da cena `Floresta_e_Rio`, teletransportando o jogador involuntariamente.

### 1.2. Plano de Neutralização Imediata (Regra Inegociável da TASK-10)
* **Remoção Total:** Eliminar 100% de qualquer chamada a `SceneManager.LoadScene`, `SceneManager.LoadSceneAsync` ou transição de mapa dos scripts `PortalWorldSphere.cs` e `PortalPedestalSocket.cs`.
* **Novo Papel Estritamente Cosmético/Diorama:** O `PortalOrbDiorama_Forest` deve atuar única e exclusivamente como uma peça de diorama 3D interativa de alta tecnologia:
  1. O paciente pode pegá-la livremente, inspecionar a mini-floresta interna girando suavemente em 360°;
  2. Ao encaixar no pedestal, ocorrem efeitos de estalo magnético, pulso emissivo no anel de LED e subida de partículas luminosas verticais;
  3. **Jamais** alterar a cena ativa. A navegação entre mapas é competência exclusiva do Canvas 2D original da clínica (`Canvas Menu Principal` / `ControladorInterface.cs`).

---

## 2. Especificação do `SceneDuplicateAndOriginPurger`

### 2.1. Problema Diagnosticado no Playtest
A inspeção forense da cena `Assets/Scenes/Floresta_e_Rio.unity` revelou um acúmulo de objetos órfãos na coordenada `(0, 0, 0)`:
* Múltiplas instâncias de `GoldenApple_Beside_Basket` e `Apple_In_Basket` duplicadas na raiz da cena (`m_TransformParent: {fileID: 0}`) num raio de $0.4\text{m}$ ao redor de `Vector3.zero`.
* Na cena `Menu_Principal.unity`, instâncias residuais temporárias e sobreposições decorrentes de execuções anteriores.

### 2.2. Regras de Limpeza e Expurgo Automatizado
O utilitário de Editor `SceneDuplicateAndOriginPurger` deve operar segundo as seguintes diretrizes:

1. **Whitelist de Sistemas Globais no `(0, 0, 0)` (Preservação Absoluta):**
   * Objetos essenciais de infraestrutura que legitimamente residem na origem não podem ser tocados:
     * `[IC] Telemetria_Clinica_ReRobLab`
     * `[IC] Audio_Ambiente_E_SFX_ReRobLab`
     * `Gerenciador_HUD`
     * `EventSystem`
     * `Building` (estrutura base da sala)
     * `Pessoa_Virtual_sem3D` / `XR Origin (XR Rig)`
     * Grupos organizadores de hierarquia (`[TASK-XX] ..._Group`).
2. **Critério de Expurgo na Origem (`Origin Sweeper`):**
   * Qualquer GameObject folha ou prefab instanciado interativo (maçãs, bolas, fidgets, troféus, moedas, balões) cujo centro esteja a uma distância menor que $0.80\text{m}$ de `Vector3.zero` E que não pertença à whitelist deve ser sumariamente destruído (`Undo.DestroyObjectImmediate`).
3. **Critério de Eliminação de Duplicatas (`Duplicate Purger`):**
   * Para cada Task, o script mantém um registro da contagem canônica esperada de objetos únicos.
   * Se houver $N > 1$ cópias de um item singleton (ex: `RadioCassetteDeck_Instance`, `ArcadeBallSpawner_Instance`, `BalloonGunDispenser_Instance`), preserva-se a cópia canônica mais recente e removem-se os clones excedentes.
   * Caso algum objeto canônico tenha sido removido por engano pelo usuário, o purger sinaliza a ausência para recriação limpa pelo respectivo `SceneAndPrefabBuilder`.

---

## 3. Mapeamento e Distribuição Multimóveis do Menu Principal

A inspeção espacial da cena `Menu_Principal.unity` (composta pelos módulos do `VNB - Office Set 2` em um espaço de $10\text{m} \times 12\text{m}$) confirmou que os minigames e fidgets estavam todos amontoados em uma única mesa (`full_desk_3 (1)`).

A sala possui ampla variedade de móveis disponíveis. O novo layout distribui os elementos em 4 Zonas Físicas Distintas com folga mínima de $0.35\text{m}$ entre peças:

```
                      [PAREDE NORTE: Z = 7.4m - 8.0m]
    ┌─────────────────────────────────────────────────────────────┐
    │  [ZONA 3: MINIGAMES ARCADE & BALÕES]                        │
    │  - Tabela Basquete na Parede: (5.10, 2.20, 7.85)            │
    │  - Spawner & Bolas no Balcão: (5.10, 0.95, 7.30)            │
    │  - Pistola Dispenser Balões: (4.50, 0.95, 7.30)             │
    └─────────────────────────────────────────────────────────────┘
                                  ▲
                                  │ (Espaço Central Amplo)
                                  ▼
    ┌─────────────────────────┐             ┌─────────────────────┐
    │ [ZONA 2: FIDGET STATION]│             │ [ZONA 4: ESTANTES]  │
    │ Balcão Lateral (X=0.6m) │             │ modern_shelves_3    │
    │ - Alavanca Clima (4.40) │             │ (X=9.61m, Z=0.03m)  │
    │ - Botão Fogos   (4.80)  │             │ - Maçã Dourada      │
    │ - Bobblehead    (5.20)  │             │ - Bonsai Luminoso   │
    │ (Folga >= 0.40m)        │             │ - Taça Estrela      │
    └─────────────────────────┘             │ - Coroa do Drone    │
                                            └─────────────────────┘
                 ┌───────────────────────────┐
                 │ [CANVAS 2D MENU PRINCIPAL]│
                 │   Posição: (0, 0, 3.94)   │
                 └───────────────────────────┘
                               ▲
                               │ Visão Frontal
    ┌──────────────────────────┴──┐
    │ JOGADOR VR (X=0, Y=0, Z=0)  │
    └─────────────────────────────┘
                 │ (À Direita do Jogador)
                 ▼
    ┌────────────────────────────────────────┐
    │ [ZONA 1: MESA PRINCIPAL WORKSTATION]   │
    │ full_desk_3 (1) em (0.63, 0.00, -0.02) │
    │ - Rádio 3D + Deck Fitas: (0.66, 0.95, 0.14)
    │ - Rack c/ 4 Fitas Cassete: (0.70, 0.95, -0.15)
    │ - Diorama Orb Floresta: (0.66, 0.95, -0.42)
    └────────────────────────────────────────┘
```

### Detalhamento das 4 Zonas de Móveis:

| Zona | Móvel / Estrutura na Cena | Posição Base $(X, Y, Z)$ | Altura Superfície | Itens Alocados |
| :--- | :--- | :--- | :--- | :--- |
| **Zona 1: Workstation Musical** | `full_desk_3 (1)` | `(0.63, 0.00, -0.02)` | $Y = 0.95\text{m}$ | Rádio 3D, Deck de Fitas, Rack com 4 Fitas Cassete magnéticas, Diorama da Floresta (sem teleporte). |
| **Zona 2: Estação Fidget** | `counter_1` & `drawer_simple` | `(0.59, 0.00, 4.47)` a `(0.58, 0.00, 5.36)` | $Y = 0.95\text{m}$ | Alavanca de Clima (`Z = 4.40`), Botão de Fogos (`Z = 4.80`), Bobblehead (`Z = 5.20`). Espaçamento real $\ge 0.40\text{m}$. |
| **Zona 3: Minigames Arcade** | `counter_3_a / counter_3_c` & Parede Norte | `(4.17 - 6.17, 0.00, 7.44)` | $Y = 0.95\text{m}$ (Balcão) e $Y = 2.20\text{m}$ (Parede) | Pistola Dispenser de Balões (`X = 4.50`), Botão Spawner de Basquete (`X = 5.10`), Bolas de Basquete (`X = 5.35`), Aro Duplo na parede (`5.10, 2.20, 7.85`). |
| **Zona 4: Galeria de Troféus** | `modern_shelves_3 (1)` | `(9.61, 0.00, 0.03)` | Prateleiras $Y = 0.85\text{m}$ e $Y = 1.25\text{m}$ | Os 4 Troféus clínicos desbloqueáveis distribuídos ordenadamente com suas respectivas plaquetas. |

---

## 4. Especificação do `VisualQACaptureEditor` e `SpatialAuditUtility`

### 4.1. Câmeras e Renders de Auditoria Visual (1920x1080)
Para cada Task, o script de automação no Unity Editor deve renderizar e salvar exatamente 3 perspectivas em `.agents/visual_qa/`:
1. `TASK-XX_View1_Front.png`: Visão frontal ampla da estação ou objeto interativo em seu ambiente contextual.
2. `TASK-XX_View2_Side_Grounding.png`: Visão lateral rasante demonstrando contato físico real com a mesa, estante ou terreno (comprovando ausência de flutuação).
3. `TASK-XX_View3_Closeup.png`: Macro detalhado demonstrando affordances visuais, shaders URP/Standard, emissão e texto de UI legível.

### 4.2. Estrutura do Relatório de Telemetria Espacial (`TASK-XX_SpatialAudit.json`)
Cada validação automatizada salvará obrigatoriamente um arquivo JSON com o seguinte esquema:

```json
{
  "taskId": "TASK-01",
  "timestamp": "2026-11-11T04:30:00Z",
  "sceneName": "Menu_Principal",
  "originPurgerStatus": {
    "isOriginClean": true,
    "rogueObjectsFound": 0,
    "cleanedObjectsCount": 0
  },
  "instanceCounts": {
    "expected": 1,
    "actual": 1,
    "hasDuplicates": false
  },
  "spatialDistribution": {
    "furnitureAllocated": "full_desk_3 (1)",
    "position": {"x": 0.66, "y": 0.95, "z": 0.14},
    "hasOverlap": false,
    "minClearanceMeters": 0.38
  },
  "groundingAudit": {
    "groundGapMeters": 0.001,
    "isPhysicallyGrounded": true,
    "isInsideMapBounds": true
  },
  "vrPerformanceCompliance": {
    "gpuInstancingEnabled": true,
    "zeroDynamicMeshColliders": true,
    "zeroUpdateGetComponent": true,
    "srpBatcherCompatible": true
  }
}
```

---

## 5. Requisitos de Áudio Multissensorial Confortável (VR-Safe)

Para prevenir desconforto ou fadiga auditiva em pacientes em reabilitação:
1. **Calibração de Volume e Headroom:**
   * Nenhum efeito sonoro de interação pode exceder o pico de volume de $-8\text{ dB}$ (valor nominal `volume <= 0.40f` no `AudioSource`).
   * Som de hover suave: volume $0.20\text{f}$.
   * Som de grab/pegada: volume $0.35\text{f}$.
   * Som de encaixe no berço/socket: volume $0.40\text{f}$.
2. **Filtragem de Frequências Irritantes:**
   * Todos os sons sintetizados devem conter filtro passa-baixas (*low-pass*) ativo cortando frequências acima de $4.000\text{ Hz}$, eliminando estalos agressivos e chiados.
3. **Espacialização e Efeito Doppler Nulo:**
   * `AudioSource.spatialBlend = 1.0f` (áudio puramente tridimensional).
   * `AudioSource.dopplerLevel = 0.0f`: Desativação total do Doppler para evitar distorções de afinação (*pitch wobble*) quando o paciente move a cabeça ou as mãos rapidamente no VR.
   * `AudioSource.rolloffMode = AudioRolloffMode.Linear`, com alcance máximo calibrado para ambientes fechados ($10.0\text{m}$).
4. **Proibição de Loops Não-Supervisionados:**
   * Sons de aproximação ou presença de criaturas (ex: pássaro da floresta) devem conter travas de evento (`hasTriggered`) e tempo de espera rígido ($\ge 15\text{s}$), tocando no máximo um pio melódico curto de $0.5\text{s}$ por aproximação.

---

## 6. Orçamento Rigoroso de Otimização VR (Meta: 90 FPS Cúbicos)

1. **GPU Instancing & SRP Batcher:**
   * Todos os materiais gerados no pipeline via `MaterialShaderHelper` devem definir expressamente `material.enableInstancing = true`.
   * **Proibido o uso de `renderer.material` em runtime.** Toda alteração de cor, brilho emissivo ou destaque visual durante o *hover* de interactables deve ocorrer via `MaterialPropertyBlock` (`Renderer.SetPropertyBlock`), evitando alocações e quebra de instancing.
2. **Física Leve e Dinâmica:**
   * **Zero `MeshCollider` em objetos dinâmicos móveis.** Todos os objetos interativos com `Rigidbody` devem utilizar colisores primitivos (`SphereCollider` ou `BoxCollider`).
   * Configuração de detecção de colisão sob demanda: repouso em `CollisionDetectionMode.Discrete`; alteração para `ContinuousDynamic` apenas enquanto o objeto estiver sendo segurado pelo jogador (`OnSelectEntered`), retornando a `Discrete` após o repouso.
3. **Throttling de Distância (`sqrMagnitude`):**
   * Objetos com scripts de animação ou verificação no cenário amplo da `Floresta_e_Rio` devem abortar rotinas pesadas se `(playerPos - transform.position).sqrMagnitude > activeRadiusSqr`.
4. **Iluminação Segura para VR:**
   * Fontes de luz pontuais adicionadas em pedestais ou lanternas devem ter `light.shadows = LightShadows.None`. Em renderização VR estéreo, sombras dinâmicas pontuais duplicam passes de renderização em cada olho, consumindo a taxa de quadros.

---

## 7. Próximos Passos Imediatos para a TASK-01
Com esta especificação analítica concluída e aprovada pelo Subagente [A]:
1. **Micro-Passo [B]:** Tech Artist gera/valida os clipes de áudio suaves calibrados em `-8 dB` (`ui_hover_soft.wav`, `grab_pop.wav`, `socket_snap.wav`, etc.).
2. **Micro-Passo [C]:** Senior Unity C# Engineer ajusta `MaterialShaderHelper.cs` (garantindo `enableInstancing = true`), `VRFeedbackManager.cs` (calibrando volumes e Doppler zero) e neutraliza de imediato o teleporte em `PortalWorldSphere.cs` e `PortalPedestalSocket.cs`.
3. **Micro-Passo [D]:** Automation Engineer implementa `SceneDuplicateAndOriginPurger.cs` e `SpatialAuditUtility.cs`, executa a varredura e expurgo no Unity Editor aberto e gera os primeiros prints e JSONs de telemetria.
4. **Micro-Passo [E]:** Visual QA & VR Auditor inspeciona as evidências geradas e assina o primeiro veredito `PASS`.
