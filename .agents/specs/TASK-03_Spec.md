# Especificação Técnica de Alta Precisão — TASK-03: Drone Companheiro Clínico (Acompanhamento Relativo, Posicionamento por Laser/Scroll & Zero Colisão)

**Documento Técnico:** `.agents/specs/TASK-03_Spec.md`  
**Autor:** Subagente [A] — Forensic Spec, Furniture Mapper & Playtest Bug Scout  
**Módulo:** Drone Pet / Companheiro de Reabilitação  
**Status:** APROVADO PARA EXECUÇÃO  
**Data:** 11/11/2026  
**Versão do Unity:** 6000.0.85f2 (Unity 6)  
**Versão do XR Interaction Toolkit:** 2.6.5  
**Cenas Afetadas:** `Assets/Scenes/Menu_Principal.unity` e `Assets/Scenes/Floresta_e_Rio.unity`  
**Prefabs e Scripts Afetados:** `[IC] Drone_Guia_IA_ExoBot`, `DroneGuiaIA.cs`, `InteractiveApple.cs`, `DroneFetchController.cs`, `DroneFetchTarget.cs`, `Task03_SceneAndPrefabBuilder.cs`

---

## 1. Diagnóstico Forense & Investigação de Causa Raiz (Hotfix #2 do Playtest)

Durante as sessões práticas de playtest com o exoesqueleto e o headset VR, foram identificados três defeitos críticos que prejudicaram a experiência do paciente e a segurança clínica:

```
┌──────────────────────────────────────────────────────────────────────────────────────────────────┐
│                                   DIAGNÓSTICO FORENSE DO PLAYTEST                                │
├──────────────────────────────────────────────────────────────────────────────────────────────────┤
│ 1. POLUIÇÃO DE UI & BOTÕES CLUMSY:                                                               │
│    • A barra inferior do Canvas do Drone possuía botões 2D pequenos "[- Perto]" e "[+ Longe]".   │
│    • Dificuldade motora para o paciente mirar com o laser em botões minúsculos flutuando no ar.  │
│                                                                                                  │
│ 2. COLISÃO FÍSICA QUE EMPURRAVA O PACIENTE:                                                      │
│    • O Drone e itens que ele carregava empurravam o CharacterController do jogador pelo cenário. │
│    • O Drone não possuía Rigidbody cinemático estrito, agindo como colisor estático móvel que   │
│      gerava despenetrações violentas na física do PhysX.                                          │
│                                                                                                  │
│ 3. ABANDONO DO PACIENTE PARA CATAR MAÇÃS (FETCHBOT DESCONTROLADO):                               │
│    • O DroneFetchController abandonava a função de guia para voar atrás de maçãs jogadas,      │
│      inclusive caindo no rio ou debaixo do mapa, e trazia o item colidindo contra o peito.       │
└──────────────────────────────────────────────────────────────────────────────────────────────────┘
```

### 1.1. Causa Raiz 1: Textos e Botões 2D Inferiores no Canvas Holográfico
* **Localização no Código:** `Assets/Scripts/DroneGuiaIA.cs`, linhas 1166–1214 (`ConstruirHUDHolografico`).
* **Estrutura:** O método criava um `GameObject` denominado `Barra_Controle_Espacial` contendo os botões `Btn_Mais_Perto` (`- Perto`), `Btn_Mais_Longe` (`+ Longe`) e `Btn_Reset_Centro` (`Centro`).
* **Impacto no Playtest:** Pacientes com limitação motora no punho sofriam para acertar o raio laser em um botão plano de apenas $0.15\text{m}$ de largura em movimento. O controle de distância em VR deve ser natural e intuitivo via manipulação direta do feixe (arraste e *thumbstick/scroll*), e não por cliques em botões de texto.

### 1.2. Causa Raiz 2: Interação Física Conflitante com o `CharacterController` do Jogador
* **Localização no Código:** `[IC] Drone_Guia_IA_ExoBot` nas cenas `Menu_Principal.unity` e `Floresta_e_Rio.unity`.
* **Física do PhysX:** O objeto raiz do Drone continha um `BoxCollider` (`isTrigger = true`), mas não possuía `Rigidbody`. No PhysX, qualquer colisor sem `Rigidbody` que tem seu `transform.position` alterado em runtime é tratado como um *Moving Static Collider*, forçando recalculo de broadphase a cada frame.
* **Colisão por Proxy:** Quando o script `DroneFetchController` acoplava o bastão (`CyberFetchBaton`) ou uma maçã na fuselagem inferior (`TractorBeamEmitter_Anchor`), esses objetos mantinham colisores sólidos (`CapsuleCollider` / `SphereCollider`) e `Rigidbody` dinâmico. Ao entregar o objeto na posição `presentationDistanceFront = 0.65f` na frente do jogador, o colisor sólido do item penetrava o volume cilíndrico do `CharacterController` do paciente ($r = 0.35\text{m}$), empurrando o jogador para trás e desequilibrando o paciente no treino de marcha!
* **Ausência de `Physics.IgnoreCollision`:** Não havia nenhuma rotina de isolamento de colisão entre os colisores do Drone/seus itens e o `CharacterController` / colisores dos controles do paciente.

### 1.3. Causa Raiz 3: Rotinas Parasitas de Busca de Maçãs (`DroneFetchController` & `DroneFetchTarget`)
* **Localização no Código:**
  - `Assets/Scripts/Interactive/Task03/DroneFetchController.cs` (FSM de busca com estados `FlyingToTarget`, `TractorBeamEngage`, `CarryingBack`, `PresentingAtChestHeight`).
  - `Assets/Scripts/Interactive/Task03/DroneFetchTarget.cs`.
  - `Assets/Scripts/Interactive/Task11/InteractiveApple.cs` (linhas 86–91: injetava compulsoriamente `DroneFetchTarget` em todas as maçãs sacudidas das árvores ou presentes no pomar).
* **Impacto no Playtest:** O Drone abandonava repentinamente sua missão primordial (exibir telemetria clínica, biofeedback de simetria e guiar o paciente) para voar dezenas de metros através do cenário atrás de maçãs jogadas na grama ou caídas no rio. O paciente ficava sem visualização dos seus passos e confuso ao ver o Drone voar para longe.

---

## 2. Plano de Erradicação & Limpeza Arquitetural

| Componente / Elemento | Ação de Engenharia | Destino / Procedimento de Limpeza |
| :--- | :--- | :--- |
| Botões `Btn_Mais_Perto` e `Btn_Mais_Longe` | **Remoção Definitiva** | Deletar criação em `DroneGuiaIA.cs` e destruir instâncias nas cenas. |
| Botão `Btn_Reset_Centro` | **Substituição por Gesto/Atalho** | Posição padrão redefinida ao soltar no limite confortável ou comando central. |
| `DroneFetchController.cs` | **Desativação / Remoção do Drone** | Remover o componente do Drone nas cenas `Menu_Principal` e `Floresta_e_Rio`. |
| Injeção em `InteractiveApple.cs` | **Expurgo de `DroneFetchTarget`** | Eliminar linhas 64 e 86–91 de `InteractiveApple.cs`. Maçãs não devem acionar o Drone. |
| `CyberFetchBaton` na Mesa | **Substituição por Brinquedo Fidget** | Limpar das mesas do Menu; manter apenas objetos ergonômicos clínicos. |
| Colisores Conflitantes | **Kinematic + IgnoreCollision** | Configurar `Rigidbody.isKinematic = true` no Drone e executar `Physics.IgnoreCollision` com todo o Player Rig. |

---

## 3. Nova Arquitetura de FSM & Acompanhamento Dinâmico do Drone

O Drone passa a operar exclusivamente como **Companheiro Clínico de Biofeedback e Guia Espacial**, obedecendo a uma máquina de estados de acompanhamento em 3 fases:

```
┌──────────────────────────────────────────────────────────────────────────────────────────────────┐
│                                MÁQUINA DE ESTADOS DO DRONE COMPANHEIRO                           │
└──────────────────────────────────────────────────────────────────────────────────────────────────┘
                                                │
                                                ▼ [Scene Start / Awake]
                    ┌────────────────────────────────────────────────────────┐
                    │               1. WaitingInitialRead                    │
                    │  • Posicionado confortavelmente a 1.6m à frente        │
                    │  • Altura do peito/olhos do paciente (Y ≈ 1.45m)       │
                    │  • Flutuação estacionária calma (senoidal A = 0.025m)  │
                    │  • Permite leitura atenta de métricas e instruções     │
                    └───────────────────────────┬────────────────────────────┘
                                                │
                 ┌──────────────────────────────┴──────────────────────────────┐
                 │ Jogador aponta Laser e segura                              │ Paciente inicia caminhada
                 │ (XRGrabInteractable / Trigger)                              │ (ReceptorUDP / Vel > 0.15 m/s)
                 ▼                                                             ▼
┌───────────────────────────────────────────────┐             ┌──────────────────────────────────────────────┐
│          2. BeingManipulatedByPlayer          │             │        3. FollowingPlayerAtCustomOffset      │
│  • Laser Raycast / Force Grab ativado         │             │  • Segue o paciente mantendo EXATAMENTE      │
│  • allowAnchorControl = true                  │             │    o vetor relativo Δ gravado na soltura     │
│  • Thumbstick / Scroll afasta ou aproxima     │   Soltura   │  • Movimento orgânico via Vector3.SmoothDamp │
│  • Expressão LED curiosa / atenta             │  (Select    │  • Rotação suave olhando para o paciente     │
│  • Zero interferência na marcha               │   Exited)   │  • Biofeedback clínico em tempo real         │
│  • Limites: 1.20m <= Distância <= 4.50m       ├────────────►│  • Áudio: 'drone_anchor_lock.wav'            │
└───────────────────────────────────────────────┘             └──────────────────────┬───────────────────────┘
                 ▲                                                                   │
                 └───────────────────────────────────────────────────────────────────┘
                           Reajuste a Qualquer Momento (Laser + Thumbstick)
```

### 3.1. Estado 1: `WaitingInitialRead` (Boas-Vindas e Leitura Inicial Estacionária)
* **Objetivo Clínico:** Evitar que o paciente precise se deslocar ou girar a cabeça para encontrar o painel no início da sessão.
* **Posicionamento de Entrada:**
  $$\vec{P}_{\text{inicial}} = \vec{P}_{\text{paciente}} + \vec{f}_{\text{paciente}} \cdot 1.60\text{m} + \vec{u} \cdot 1.45\text{m}$$
  Onde $\vec{f}_{\text{paciente}}$ é a direção horizontal da visão do paciente e $\vec{u} = (0, 1, 0)$.
* **Comportamento Cinemático:** Permanece rigorosamente parado horizontalmente, aplicando apenas uma micro-flutuação harmônica vertical suave:
  $$\Delta y(t) = \sin(1.8 \cdot t) \cdot 0.025\text{m}$$
* **Transições:**
  - Se o paciente começar a caminhar ($\text{velocidade} > 0.15\text{ m/s}$ ou passos detectados) $\rightarrow$ Transita para `FollowingPlayerAtCustomOffset` com offset padrão ($1.60\text{m}$ à frente).
  - Se o paciente apontar o laser e segurar $\rightarrow$ Transita para `BeingManipulatedByPlayer`.

### 3.2. Estado 2: `BeingManipulatedByPlayer` (Manipulação Ergonômica por Laser & Scroll)
* **Mecanismo de Interação:** O Drone possui o componente `XRGrabInteractable` configurado com:
  - `movementType = XRBaseInteractable.MovementType.Instantaneous` (ou `VelocityTracking` cinemático sem gravidade).
  - `trackRotation = false`: O Drone preserva sua orientação autônoma olhando para o paciente (`LookRotation` suavizado), permitindo que o paciente o reposicione no espaço sem entortar o painel holográfico.
  - `allowAnchorControl = true`: Ao inclinar o *thumbstick* do controle VR para frente ou girar o scroll do mouse, o ponto de ancoragem do raio se afasta; inclinando para trás, aproxima.
* **Envelope de Segurança Espacial:**
  - Distância mínima de aproximação: $1.20\text{m}$ (impede que o Drone invada a visão do headset ou colida com a cabeça).
  - Distância máxima: $4.50\text{m}$ (garante legibilidade das fontes do HUD de métricas).
  - Altura em relação ao chão: Clamped entre $0.80\text{m}$ (nível da cintura) e $2.20\text{m}$ (pouco acima da cabeça).
* **Feedback Multissensorial:**
  - O visor LED muda para a expressão atenta/curiosa (`texPoked` ou pupilas dilatadas).
  - Haptic sutil no controle ao iniciar a pegada ($0.15\text{s}$, amplitude $0.2$).

### 3.3. Estado 3: `FollowingPlayerAtCustomOffset` (Acompanhamento Relativo com Memória de Vetor)
* **Gatilho de Entrada:** Disparado no evento `OnSelectExited` quando o paciente solta o botão do laser.
* **Cálculo Matemático do Vetor de Offset Relativo:**
  1. No frame exato da soltura, capturam-se as posições globais: $\vec{P}_{\text{drone}}$ e $\vec{P}_{\text{paciente}}$.
  2. Calcula-se o vetor delta global:
     $$\vec{\Delta}_{\text{global}} = \vec{P}_{\text{drone}} - \vec{P}_{\text{paciente}}$$
  3. Extrai-se o ângulo de rotação horizontal (*Yaw*) do paciente:
     $$\mathbf{R}_{\text{yaw}} = \text{Quaternion.Euler}(0, \theta_{\text{paciente}}, 0)$$
  4. Converte-se para o espaço local relativo da marcha do paciente:
     $$\vec{\Delta}_{\text{local}} = \mathbf{R}_{\text{yaw}}^{-1} \cdot \vec{\Delta}_{\text{global}}$$
  5. Salva-se esse vetor $\vec{\Delta}_{\text{local}} = (x_{\text{rel}}, y_{\text{rel}}, z_{\text{rel}})$.
* **Confirmação de Ancoragem:**
  - O Drone emite o novo som suave `drone_anchor_lock.wav` ($\text{peak} \le -8.5\text{ dBFS}$).
  - Pulso háptico suave de confirmação no controle ($0.12\text{s}$, amplitude $0.18$).
* **Rotina de Locomoção Suave (Zero Alocação de GC):**
  A cada frame, o Drone calcula sua posição alvo no mundo:
  $$\vec{P}_{\text{alvo}} = \vec{P}_{\text{paciente}} + \mathbf{R}_{\text{yaw}} \cdot \vec{\Delta}_{\text{local}}$$
  $$\vec{P}_{\text{alvo}}.y = \max(\text{SoloY}(\vec{P}_{\text{alvo}}) + 0.65\text{m}, \vec{P}_{\text{paciente}}.y + y_{\text{rel}} + \Delta y(t))$$
  O deslocamento é processado através de `Vector3.SmoothDamp`:
  ```csharp
  transform.position = Vector3.SmoothDamp(transform.position, posAlvo, ref velocidadeDampAtual, tempoSuavizacao);
  ```
  Onde `tempoSuavizacao = 0.35f` e `velocidadeDampAtual` é um campo estático/privado da classe para zero alocação de memória.

---

## 4. Arquitetura de Física & Blindagem Anti-Colisão com o Jogador

Para garantir que o Drone **jamais empurre, bloqueie ou desestabilize o paciente**, aplicam-se as seguintes regras de física:

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                        HIERARQUIA E FÍSICA DO DRONE COMPANHEIRO                        │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ [IC] Drone_Guia_IA_ExoBot                                                             │
│   ├── Rigidbody (isKinematic = true, useGravity = false, Interpolate)                  │
│   ├── BoxCollider (isTrigger = true, Layer = Interaction/Default)                      │
│   ├── DroneGuiaIA (FSM Clínica, Telemetria HUD, Locomoção SmoothDamp)                  │
│   ├── XRGrabInteractable (allowAnchorControl = true, trackRotation = false)            │
│   │                                                                                    │
│   ├── Modelo_Blueprint_Raiz (Visual 3D - SEM Colisores)                                │
│   │     └── Luz_Biofeedback (Light Type Point, shadows = LightShadows.None)           │
│   │                                                                                    │
│   ├── Canvas_Holograma (World Space, GraphicRaycaster, TrackedDeviceGraphicRaycaster)  │
│   │     ├── Fundo_Holografico (Painel Biofeedback)                                     │
│   │     └── [SEM BARRAS COM BOTÕES "- PERTO" OU "+ LONGE"]                             │
│   │                                                                                    │
│   └── AudioSource_AnchorLock (spatialBlend = 1.0, volume = 0.18, maxDistance = 8.0m)   │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

### 4.1. Código Mandatório de Isolamento no `Awake` / `Start`
No método `Awake` / `Start` de `DroneGuiaIA.cs`:
```csharp
// 1. Rigidbody estritamente cinemático
Rigidbody rb = GetComponent<Rigidbody>();
if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
rb.isKinematic = true;
rb.useGravity = false;
rb.interpolation = RigidbodyInterpolation.Interpolate;

// 2. Colisor de interação como Trigger
BoxCollider col = GetComponent<BoxCollider>();
if (col == null) col = gameObject.AddComponent<BoxCollider>();
col.isTrigger = true;

// 3. Ignorar colisão com todos os CharacterControllers e Colisores do Jogador
CharacterController cc = FindFirstObjectByType<CharacterController>();
if (cc != null)
{
    Physics.IgnoreCollision(col, cc, true);
}

// Varre colisores nas mãos e cabeça do paciente
if (corpoPaciente != null)
{
    foreach (Collider colPaciente in corpoPaciente.GetComponentsInChildren<Collider>())
    {
        Physics.IgnoreCollision(col, colPaciente, true);
    }
}
```

---

## 5. Especificação de Arte, Áudio e Otimização VR (90 FPS)

### 5.1. Áudio de Confirmação de Ancoragem (`drone_anchor_lock.wav`)
* **Papel:** Subagente `[B]` gerará o arquivo em `Assets/Audio/Generated/Task03/drone_anchor_lock.wav`.
* **Perfil Acústico:**
  - **Duração:** $0.32\text{s}$ (curto, discreto e elegante).
  - **Timbre:** Ressonância harmônica senoidal suave em $880\text{ Hz} \rightarrow 1.320\text{ Hz}$ (acorde de quinta justa com decaimento exponencial).
  - **Filtro:** *Low-pass* em $3.800\text{ Hz}$ para eliminar qualquer aspereza em agudos no fone do Quest/Vive.
  - **Loudness Normalizado:** Pico rigorosamente limitado a $-8.5\text{ dBFS}$ (RMS $\approx -18\text{ dBFS}$).
  - **Spatial Blend:** $1.0$ (3D total com curva de atenuação logarítmica).

### 5.2. Otimizações de Renderização e VR Performance
1. **Zero Sombras Dinâmicas no Drone:**
   - A luz interna `Luz_Biofeedback` deve ser configurada estritamente com `light.shadows = LightShadows.None`.
   - O feixe holográfico e partículas de coração/brilho usam `Cast Shadows = Off` e `Receive Shadows = Off`.
2. **GPU Instancing e SRP Batcher:**
   - Os materiais `M_Drone_LED_Visor`, `M_Hologram_Chassis` e `M_Metal_Dark` devem ter `enableInstancing = true`.
   - Nenhuma propriedade de material deve ser alterada instanciando `renderer.material` em runtime; usar exclusivamente `MaterialPropertyBlock` para alterações de emissão e cor de biofeedback.
3. **Zero Garbage Collection (GC Alloc):**
   - Eliminar `new Vector3`, `new Ray` ou criação de strings em `Update()`.
   - Textos de telemetria utilizam strings pré-alocadas ou concatenações com interpolação formatada apenas quando os valores de passos mudam.

---

## 6. Procedimento de Instalação e Verificação no Editor (`Task03_SceneAndPrefabBuilder.cs`)

O Subagente `[D]` atualizará o construtor automatizado para garantir os seguintes passos:

1. **Varredura e Purga de Lixo:**
   - Varre as cenas `Menu_Principal.unity` e `Floresta_e_Rio.unity`.
   - Remove qualquer componente `DroneFetchController` órfão acoplado ao Drone.
   - Remove do Canvas holográfico os nós filhos `Btn_Mais_Perto` e `Btn_Mais_Longe`.
   - Remove qualquer duplicata órfã do Drone próxima à coordenada `(0, 0, 0)`.
2. **Configuração de Componentes e Rigidbody:**
   - Garante a presença do `Rigidbody` cinemático e `BoxCollider` trigger no Drone.
   - Configura o `XRGrabInteractable` no Drone com `allowAnchorControl = true` e `trackRotation = false`.
   - Vincula o áudio `drone_anchor_lock.wav` ao slot de áudio de ancoragem.
3. **Aterramento e Posição Inicial:**
   - No `Menu_Principal`: Posiciona o Drone em repouso elegante a $1.60\text{m}$ à frente da cadeira/posição inicial do paciente.
   - Na `Floresta_e_Rio`: Posiciona o Drone aterrado sobre a trilha principal a $1.60\text{m}$ à frente do ponto de desova do paciente.
4. **Captura Visual QA:**
   - Renderiza 3 capturas em 1920x1080 em `.agents/visual_qa/`:
     - `TASK-03_view1_front_hud.png` (vista frontal provando ausência dos botões `- perto` e `+ longe`);
     - `TASK-03_view2_side_distance.png` (vista lateral provando flutuação confortável a $1.6\text{m}$ sem penetração no player);
     - `TASK-03_view3_laser_interact.png` (vista em perspectiva exibindo o volume de interação e o visor LED limpo).
   - Exporta `.agents/visual_qa/TASK-03_SpatialAudit.json` comprovando zero duplicatas, zero lixo no `(0,0,0)` e ausência de `DroneFetchController`.

---

## 7. Critérios de Auditoria e Aceitação para o Subagente [E]

O Juiz Implacável (Subagente `[E]`) só emitirá o veredito `PASS` se todos os critérios forem atendidos:

* [ ] **C01 — Ausência Completa dos Botões `- perto` e `+ longe`:** O Canvas holográfico do Drone está limpo, sem botões de texto 2D inferiores poluindo a visão.
* [ ] **C02 — Zero Colisão com o Jogador:** O Drone possui `Rigidbody.isKinematic = true`, `isTrigger = true` e ignora colisões com o `CharacterController`. O jogador pode caminhar através ou ao redor do Drone sem ser empurrado ou bloqueado.
* [ ] **C03 — Erradicação Definitiva de Busca de Maçãs:** O Drone não possui `DroneFetchController`, não reage ao arremesso de maçãs e nunca abandona o paciente. Maçãs (`InteractiveApple`) não possuem `DroneFetchTarget`.
* [ ] **C04 — Posicionamento Inicial Estacionário:** Ao iniciar a cena, o Drone paira parado e calmo a $1.60\text{m}$ na frente do paciente, facilitando a leitura das métricas clínicas.
* [ ] **C05 — Mecânica de Arraste e Distância Relativa:** O jogador pode puxar/empurrar o Drone com o raio laser e *thumbstick/scroll*, e ao soltar, o Drone acompanha a marcha mantendo exatamente o vetor de distância escolhido.
* [ ] **C06 — Áudio Suave de Ancoragem:** `drone_anchor_lock.wav` está configurado com pico $\le -8.5\text{ dBFS}$ sem ruídos estridentes.
* [ ] **C07 — Limpeza de Origem e Duplicatas:** Nenhuma cópia do Drone deixada em `(0, 0, 0)` e exatamente 1 instância por cena.
