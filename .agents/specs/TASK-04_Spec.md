# Especificação Técnica de Alta Precisão — TASK-04: Reatividade Afetiva do Drone Pet (Carinho Háptico, Cutucada Amortecida, Expressões LED & Dança Rítmica Otimizada)

**Documento Técnico:** `.agents/specs/TASK-04_Spec.md`  
**Autor:** Subagente [A] — Forensic Spec, Furniture Mapper & Playtest Bug Scout  
**Módulo:** Drone Pet / Companheiro de Reabilitação  
**Status:** APROVADO PARA EXECUÇÃO  
**Data:** 11/11/2026  
**Versão do Unity:** 6000.0.85f2 (Unity 6)  
**Versão do XR Interaction Toolkit:** 2.6.5  
**Pipeline de Renderização:** Built-in Render Pipeline / SRP-Agnostic (Compatível com SRP Batcher e GPU Instancing)  
**Cenas Afetadas:** `Assets/Scenes/Menu_Principal.unity` e `Assets/Scenes/Floresta_e_Rio.unity`  
**Prefabs e Scripts Afetados:** `[IC] Drone_Guia_IA_ExoBot`, `DroneAffectiveSystem.cs`, `DroneCompanionFollowAndGrab.cs`, `Task04_LEDTextureGenerator.cs`, `Task04_SceneAndPrefabBuilder.cs`

---

## 1. Diagnóstico Forense & Investigação de Causa Raiz

Uma inspeção minuciosa no código-fonte de runtime (`Assets/Scripts/Interactive/Task04/DroneAffectiveSystem.cs`), nos assets gráficos (`Assets/Models/Generated/Task04/` e `Assets/Textures/Generated/Task04/`), nos arquivos de áudio (`Assets/Audio/Generated/Task04/`) e nas cenas ativas revelou cinco inconformidades críticas que impactam diretamente a performance em VR e a usabilidade clínica:

```
┌──────────────────────────────────────────────────────────────────────────────────────────────────┐
│                                   DIAGNÓSTICO FORENSE DO TASK-04                                 │
├──────────────────────────────────────────────────────────────────────────────────────────────────┤
│ 1. VIOLAÇÃO CRÍTICA DO SRP BATCHER (RUNTIME MATERIAL INSTANTIATION):                            │
│    • Em 'DroneAffectiveSystem.ApplyTextureToVisor' (linhas 549–554), o código executa:          │
│        visorRenderer.material.mainTexture = tex;                                                 │
│    • O acesso à propriedade '.material' clona o material na memória heap em tempo de execução,   │
│      quebrando o SRP Batcher, anulando o GPU Instancing e gerando vazamento/pressão de GC Alloc! │
│                                                                                                  │
│ 2. ÁUDIO ESTRIDENTE COM PICOS ACIMA DO LIMITE SEGURO DE VR:                                     │
│    • 'drone_purr.wav' atinge pico digital absoluto em 0.00 dBFS (risco de distorção e fadiga)!  │
│    • 'drone_dance_groove.wav' atinge -1.39 dBFS, 'drone_giggle.wav' atinge -2.26 dBFS.          │
│    • Todos os áudios excedem drasticamente o teto estrito de VR-Safe (pico <= -8.5 dBFS) e      │
│      carecem de filtro passa-baixas (<= 3500 Hz).                                                │
│                                                                                                  │
│ 3. FALTA DE SUPORTE A CARINHO E CUTUCADA VIA RAIO LASER (XR RAY INTERACTOR):                    │
│    • O paciente em reabilitação motora (marcha ou cadeira) não consegue alcançar o Drone se ele │
│      estiver a 1.6m–2.5m de distância. A detecção atual exige proximidade física da mão (<0.28m)│
│    • Ausência de gatilho de carinho quando o feixe de laser acaricia a cúpula do Drone.         │
│                                                                                                  │
│ 4. COORDENAÇÃO DE FÍSICA COM O NOVO 'DroneCompanionFollowAndGrab' (TASK-03):                   │
│    • As oscilações de wobble, mola de cutucada e dança NÃO podem alterar a posição ou rotação    │
│      da raiz do Drone ('transform.position' / 'offsetLocal'), devendo atuar estritamente        │
│      sobre o nó filho visual 'Modelo_3D_ExoBot'.                                                 │
│                                                                                                  │
│ 5. ALOCAÇÕES OCULTAS DE STRING E REFLECTION EM 'Update()':                                       │
│    • Em 'UpdateMusicDance()', havia chamada a 'GameObject.Find("[IC] Radio_Menu_3D")' caso o     │
│      rádio fosse nulo, causando busca per-frame e micro-stutters prejudiciais a 90 FPS.          │
└──────────────────────────────────────────────────────────────────────────────────────────────────┘
```

### 1.1. Causa Raiz 1: Clonagem Inadvertida de Material em Runtime (Linha 549)
* **Localização:** `Assets/Scripts/Interactive/Task04/DroneAffectiveSystem.cs`, método `ApplyTextureToVisor(Texture2D tex)`:
  ```csharp
  // CÓDIGO PROBLEMÁTICO IDENTIFICADO:
  if (Application.isPlaying && visorRenderer.material != null)
  {
      visorRenderer.material.mainTexture = tex;
      if (visorRenderer.material.HasProperty(EmissionMapId))
          visorRenderer.material.SetTexture(EmissionMapId, tex);
  }
  ```
* **Mecanismo de Falha:** Embora o script inicialize e aplique um `MaterialPropertyBlock`, logo em seguida ele acessa `visorRenderer.material`. Na Unity, chamar o getter `.material` força a criação imediata de uma nova instância `M_Drone_LED_Visor (Instance)`. Isso duplica o material na VRAM, impede o agrupamento pelo SRP Batcher e gera sobrecarga na CPU para sincronização de draws.
* **Correção Obrigatória:** Excluir terminantemente as chamadas a `visorRenderer.material` em runtime. A atribuição de texturas e propriedades de emissão deve ocorrer **exclusivamente via `MaterialPropertyBlock`** (`SetTexture` e `SetColor`), preservando o `sharedMaterial` intacto.

### 1.2. Causa Raiz 2: Calibração de Volume e Espectro Acústico (Auditoria de Áudio)
Uma análise espectral por script Python nos arquivos em `Assets/Audio/Generated/Task04/` revelou:
* `drone_purr.wav`: Duração = 1.80s, Taxa = 44100Hz, Mono, **Pico = 0.00 dBFS** (100% de saturação digital!).
* `drone_giggle.wav`: Duração = 0.65s, Taxa = 44100Hz, Mono, **Pico = -2.26 dBFS**.
* `drone_dance_groove.wav`: Duração = 0.80s, Taxa = 44100Hz, Mono, **Pico = -1.39 dBFS**.
* `drone_boop.wav`: Duração = 0.28s, Taxa = 44100Hz, Mono, **Pico = -6.31 dBFS**.
* **Impacto Clínico:** Em um headset VR com caixas de som próximas ao canal auditivo (como Meta Quest), sons a 0 dBFS causam desconforto acústico agudo, sobressaltos e irritabilidade no paciente. O ronrom do drone deve ser uma textura sonora quente e relaxante, nunca agressiva.
* **Correção Obrigatória:** Re-sintetizar os áudios via Subagente [B] garantindo pico máximo rigoroso de **$\le -8.5\text{ dBFS}$** (alvo recomendado: $-9.5\text{ dBFS}$ a $-10.0\text{ dBFS}$) e filtragem passa-baixas ativa com corte em **$\le 3500\text{ Hz}$** (para eliminar sibilâncias robóticas agudas).

### 1.3. Causa Raiz 3: Ausência de Interação de Carinho por Feixe de Laser
* **Localização:** `ProcessHandProximityAndPetting()` em `DroneAffectiveSystem.cs`.
* **Problema:** Apenas rastreia a proximidade da mão física através de `trackedHands` com raio de $0.28\text{m}$. Contudo, pacientes com exoesqueleto frequentemente posicionam o Drone a uma distância confortável de $1.60\text{m}$ a $2.50\text{m}$ à frente via `XRGrabInteractable` (TASK-03). Nessas distâncias, o paciente não consegue esticar o braço até o Drone para fazer carinho.
* **Correção Obrigatória:** Adicionar suporte nativo à interação por raio laser (`XRRayInteractor`):
  1. Adicionar `XRSimpleInteractable` ou evento de hover na cúpula superior (`HeadPettingTrigger`).
  2. Quando o feixe do laser pairar sobre a cúpula superior do Drone e o jogador realizar movimentos suaves de vaivém com o controle (angular velocity ou movimento do ponto de impacto entre $0.05\text{ m/s}$ e $1.5\text{ m/s}$), disparar `ApplyPettingBehavior()`.
  3. Enviar pulsos hápticos suaves de ronrom (`SendHapticImpulse(0.18f, 0.05f)`) diretamente para o controle que está emitindo o feixe de laser.

### 1.4. Causa Raiz 4: Desacoplamento Físico Estrito com `DroneCompanionFollowAndGrab`
* O script `DroneCompanionFollowAndGrab` (TASK-03) gerencia a translação global do Drone no mundo através de `Vector3.SmoothDamp` e rotação Yaw voltada para o paciente, memorizando o `offsetLocal`.
* As reatividades afetivas da TASK-04 (amortecimento de cutucada, oscilação de carinho e dança rítmica) **devem atuar única e exclusivamente no nó filho `Modelo_3D_ExoBot`**:
  $$\vec{p}_{\text{modelo}} = \vec{p}_{\text{rest}} + \vec{w}_{\text{pos}}$$
  $$\mathbf{q}_{\text{modelo}} = \mathbf{q}_{\text{rest}} \times \text{Quaternion.Euler}(\vec{w}_{\text{rot}})$$
* **Garantia de Não-Interferência:** O nó raiz do Drone (`[IC] Drone_Guia_IA_ExoBot`) mantém sua posição e rotação absolutamente controladas pela cinemática do `DroneCompanionFollowAndGrab`. O offset ancorado memorizado pelo jogador nunca sofre deriva acumulada.

---

## 2. Arquitetura da Máquina de Estados Afetiva & Fluxos de Interação

```
                             ┌─────────────────────────────────────────────────────────┐
                             │               DRONE AFFECTIVE CONTROLLER                │
                             │       (DroneAffectiveSystem : MonoBehaviour)            │
                             └────────────────────────────┬────────────────────────────┘
                                                          │
             ┌────────────────────────────────────────────┼────────────────────────────────────────────┐
             │                                            │                                            │
             ▼                                            ▼                                            ▼
   ┌────────────────────┐                       ┌────────────────────┐                       ┌────────────────────┐
   │    PETTING MODE    │                       │     POKE MODE      │                       │     DANCE MODE     │
   ├────────────────────┤                       ├────────────────────┤                       ├────────────────────┤
   │ Gatilho:           │                       │ Gatilho:           │                       │ Gatilho:           │
   │ • Mão física topo  │                       │ • Cutucada rápida  │                       │ • Espectro FFT do  │
   │ • Laser balançando │                       │ • Clique laser no  │                       │   rádio ativo      │
   │   na calota        │                       │   nariz/lente      │                       │   (50Hz - 220Hz)   │
   ├────────────────────┤                       ├────────────────────┤                       ├────────────────────┤
   │ Efeitos:           │                       │ Efeitos:           │                       │ Efeitos:           │
   │ • Visor: '♥‿♥'     │                       │ • Visor: '@_@'     │                       │ • Visor: '♪‿♪'     │
   │ • Som: purr suave  │                       │ • Som: boop +      │                       │ • Balanço lateral  │
   │ • Hápticos: 20Hz   │                       │   risadinha        │                       │   roll ±6.5°       │
   │ • Corações 3D      │                       │ • Mola física z(t) │                       │ • Bounce vertical  │
   │ • Micro-bobbing    │                       │   recuo amortecido │                       │   +0.022m local    │
   └────────────────────┘                       └────────────────────┘                       └────────────────────┘
```

### 2.1. Reatividade a Carinho (Petting):
1. **Condições de Ativação:**
   * Mão física do jogador a uma distância $d \le 0.28\text{m}$ do centro de `HeadPettingTrigger` com velocidade suave ($0.03\text{ m/s} \le v \le 1.8\text{ m/s}$); OU
   * Feixe do laser (`XRRayInteractor`) colidindo com `HeadPettingTrigger` com varredura contínua.
2. **Respostas Multissensoriais Concomitantes:**
   * **Visual (LED Face):** Transição imediata do visor para `LED_Expression_Love.png` (`♥‿♥`). Ao encerrar o carinho, transição de transição para `LED_Expression_Happy.png` (`^‿^`) durante $1.5\text{s}$ antes de retornar a `Normal`.
   * **Partículas Holográficas:** Ativação do sistema de partículas `ParticleSystem_Hearts` emitindo corações suaves com flutuação vertical ascendente ($0.4\text{ m/s}$) e fade gradiente rosa-magenta-ciano.
   * **Vibração Háptica Contínua (Purring Tátil):** Disparo de pulsos hápticos em intervalo regular de $0.05\text{s}$ com amplitude ergonômica moderada ($0.18$ a $0.22$), gerando a sensação física de um motor biônico ronronando suavemente na mão do paciente.
   * **Áudio Bioacústico:** Reprodução em loop suave de `drone_purr_happy.wav` com volume regulado ($0.18$ a $0.25$) e pitch randomizado entre $0.98$ e $1.02$.
   * **Micro-Flutuação Harmônica:** Aplicação local em `wobblePosOffset.y` de uma oscilação senoidal suave ($\Delta y = \sin(4.5 t) \cdot 0.012\text{m}$, $\Delta \theta_z = \sin(2.2 t) \cdot 2.0^\circ$).

### 2.2. Reatividade a Cutucão (Poke / Boop):
1. **Condições de Ativação:**
   * Contato brusco da mão física entrando em `FacePokeTrigger` com velocidade $> 0.25\text{ m/s}$; OU
   * Clique pontual com o gatilho do laser mirado na lente frontal (`FacePokeTrigger`).
2. **Respostas Físicas e Multissensoriais:**
   * **Dinâmica de Mola Amortecida (Squash & Stretch Local):**
     Equação de movimento linear sob amortecimento subcrítico:
     $$\ddot{\vec{w}}_{\text{pos}} = -k_{\text{spring}} \cdot \vec{w}_{\text{pos}} - c_{\text{damp}} \cdot \dot{\vec{w}}_{\text{pos}}$$
     Com rigidez $k = 320\text{ N/m}$ e amortecimento $c = 16\text{ Ns/m}$. O recuo no eixo local Z é calibrado para $-0.055\text{m}$, seguido de oscilação amortecida que se estabiliza em $< 0.8\text{s}$.
   * **Visual (LED Face):** Expressão imediata de surpresa / tontura cósmica `LED_Expression_Poked.png` (`@_@`) por $2.2\text{s}$.
   * **Áudio:** Disparo de `drone_poke_chirp.wav` (som emborrachado) seguido após $0.35\text{s}$ por uma risadinha robótica amigável `drone_giggle_soft.wav`.

### 2.3. Dança Rítmica Musical (Music Dance):
1. **Condições de Ativação:**
   * Fonte musical ativa no rádio da cena (`musicSource.isPlaying == true` e `volume > 0.05f`).
2. **Processamento Espectral FFT:**
   * Extração de 256 amostras com janela `FFTWindow.BlackmanHarris`.
   * Soma de energia nas faixas de sub-graves e graves ($50\text{ Hz}$ a $220\text{ Hz}$, bins 1 a 7).
   * Suavização temporal via `Mathf.Lerp` para evitar oscilações espasmódicas.
3. **Coreografia Procedural sem Afetar o Anchor:**
   * Quando a energia ultrapassa o limiar de sensibilidade ($0.035$):
     * Visor exibe a expressão musical `LED_Expression_Dance.png` (`♪‿♪`).
     * Movimento angular no eixo Roll local: $\theta_z = \sin(3.8 t) \cdot 6.5^\circ$.
     * Movimento angular no eixo Yaw local: $\theta_y = \cos(1.9 t) \cdot 3.9^\circ$.
     * Salto vertical rítmico local: $y_{\text{bounce}} = |\sin(7.6 t)| \cdot 0.022\text{m}$.
   * Quando o rádio é desligado, a dança é amortecida suavemente e o visor retorna para `Normal`.

---

## 3. Diretrizes Rigorosas de Performance VR (90 FPS & Zero GC Alloc)

Para assegurar estabilidade perfeita a 90 FPS sem quedas de quadro nem descarte de memória:

1. **Eliminação Absoluta de `renderer.material`:**
   * Todo chaveamento de expressão facial deve utilizar exclusivamente `MaterialPropertyBlock`:
     ```csharp
     propBlock.SetTexture(MainTexId, targetTex);
     propBlock.SetTexture(EmissionMapId, targetTex);
     propBlock.SetColor(EmissionColorId, ledEmissiveColor * ledEmissiveIntensity);
     visorRenderer.SetPropertyBlock(propBlock);
     ```
   * Nunca invocar propriedades que criem instâncias do material na memória VRAM/RAM.
2. **Identificadores de Propriedade Pré-Compilados:**
   * Todos os identificadores de shader devem ser estáticos via `Shader.PropertyToID`:
     - `private static readonly int MainTexId = Shader.PropertyToID("_MainTex");`
     - `private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");`
     - `private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");`
3. **Eliminação de Buscas em `Update()`:**
   * As referências ao rádio (`musicSource`), câmera do paciente e controladores VR devem ser obtidas no `Start()` ou via eventos de cena. É expressamente proibido chamar `GameObject.Find()` ou `FindObjectsByType()` dentro do ciclo de `Update()`.
4. **Isolamento de Colisão com o Paciente:**
   * O `HeadPettingTrigger` ($r = 0.18\text{m}$) e o `FacePokeTrigger` ($r = 0.095\text{m}$) devem operar estritamente com `isTrigger = true` na layer configurada para detecção pelo `XRRayInteractor`.
   * Ambos os colisores são registrados no método de isolamento de colisão do `DroneCompanionFollowAndGrab` para nunca bloquear a locomoção do paciente.

---

## 4. Especificação de Mídia e Requisitos de Produção Artística (Subagente [B])

### 4.1. Texturas LED Procedurais em Alta Fidelidade (512x512 PNG)
As texturas devem residir em `Assets/Textures/Generated/Task04/` com formatação RGBA32, moldura escura metálica circular e micropixels LED cibernéticos:

| Arquivo de Textura | Expressão | Representação LED | Paleta de Cores Primária |
| :--- | :--- | :---: | :--- |
| `LED_Expression_Normal.png` | Normal / Calmo | `•‿•` | Ciano (#00F5FF) com brilho especular branco |
| `LED_Expression_Happy.png` | Feliz / Aprovador | `^‿^` | Arcos ciano (#00F5FF) + bochechas rosa (#FF2E93) |
| `LED_Expression_Love.png` | Amoroso / Carinho | `♥‿♥` | Olhos de coração rosa radiante + sorriso ciano |
| `LED_Expression_Poked.png` | Cutucado / Surpreso | `@_@` | Espirais concêntricas ciano/rosa + boca ondulada ouro (#FFD700) |
| `LED_Expression_Dance.png` | Musical / Dança | `♪‿♪` | Notas musicais em semínima ciano + sorriso largo ouro |
| `Holo_Heart_Particle.png` | Partícula Flutuante | Coração | Gradiente translúcido rosa neon com borda brilhante |

### 4.2. Especificação Acústica Calibrada para VR-Safe
Os arquivos de áudio devem ser produzidos e salvos em `Assets/Audio/Generated/Task04/` no formato WAV PCM 16-bit Mono (44.1 kHz), atendendo aos seguintes parâmetros físicos de síntese:

| Arquivo de Áudio | Tipo de Efeito | Frequência Base | Filtro Passa-Baixas | Pico Máximo Permitido | Duração Alvo |
| :--- | :--- | :---: | :---: | :---: | :---: |
| `drone_purr_happy.wav` | Ronrom Contínuo | $55\text{ Hz}$ com tremolo $18\text{ Hz}$ | $2800\text{ Hz}$ | **$\le -9.5\text{ dBFS}$** | $1.80\text{s}$ (Loop perfeito) |
| `drone_poke_chirp.wav` | Boop Emborrachado | Sweep $720\text{ Hz} \to 260\text{ Hz}$ | $3200\text{ Hz}$ | **$\le -9.0\text{ dBFS}$** | $0.26\text{s}$ |
| `drone_giggle_soft.wav` | Risadinha Robótica | Arpejo $440, 554, 659\text{ Hz}$ | $3500\text{ Hz}$ | **$\le -9.5\text{ dBFS}$** | $0.55\text{s}$ |
| `drone_dance_groove.wav`| Pulso Rítmico Sci-Fi | Kick suave $65\text{ Hz}$ + blip | $3000\text{ Hz}$ | **$\le -9.0\text{ dBFS}$** | $0.80\text{s}$ |

---

## 5. Especificação de Engenharia C# (Subagente [C])

O script `Assets/Scripts/Interactive/Task04/DroneAffectiveSystem.cs` deve ser consolidado e limpo, atendendo às seguintes regras:

1. **Remoção de Código Obsoleto/Prejudicial:**
   * Deletar as linhas 549–554 de `ApplyTextureToVisor` que acessam `.material`.
   * Deletar a chamada `GameObject.Find("[IC] Radio_Menu_3D")` de dentro do `UpdateMusicDance()`.
2. **Integração com Laser Petting:**
   * Implementar `OnLaserPettingHover(HoverEnterEventArgs args)` e `OnLaserPettingExit(HoverExitEventArgs args)` ou detecção de raycast via `XRSimpleInteractable` posicionado em `HeadPettingTrigger`.
   * Emitir pulso háptico no controle que disparou o raio laser enquanto ele acaricia a cúpula superior.
3. **Integração com Laser Poke:**
   * Adicionar evento `OnLaserPokeActivated(ActivateEventArgs args)` no `FacePokeTrigger`. Quando o paciente mirar no nariz do drone e apertar o gatilho, disparar `TriggerPoke(hitPoint, direction)`.
4. **Isolamento de Wobble no Nó Filho:**
   * Assegurar que `wobblePosOffset` e `wobbleRotOffset` são somados estritamente às coordenadas locais de `modelTransform` no `LateUpdate()`, nunca afetando o `transform.position` global nem o `offsetLocal` do `DroneCompanionFollowAndGrab`.

---

## 6. Especificação de Montagem de Cena e Verificação Espacial (Subagente [D])

O script de montagem `Assets/Editor/AutoSetup/Task04_SceneAndPrefabBuilder.cs` deve:

1. **Varredura e Higienização de Origem (Lei 1):**
   * Verificar se existe apenas 1 instância de `[IC] Drone_Guia_IA_ExoBot` em cada cena.
   * Confirmar que a origem `(0, 0, 0)` está livre de clones órfãos do Drone.
2. **Construção de Componentes na Hierarquia do Drone:**
   * Garante a malha convexa do visor (`Visor_LED_Frontal`) com 36 segmentos e material `M_Drone_LED_Visor.mat`.
   * Garante `HeadPettingTrigger` (SphereCollider $r = 0.18\text{m}$, `isTrigger = true`).
   * Garante `FacePokeTrigger` (SphereCollider $r = 0.095\text{m}$, `isTrigger = true`).
   * Garante `ParticleSystem_Hearts` com textura `Holo_Heart_Particle.png`.
   * Atribui os 4 novos áudios VR-safe nos campos correspondentes.
3. **Exportação de Telemetria e Capturas HD:**
   * Exportar `.agents/visual_qa/TASK-04_SpatialAudit.json` com validação de zero duplicatas, zero lixo na origem e gap de solo.
   * Renderizar 3 imagens em 1920x1080 em `.agents/visual_qa/`:
     1. `TASK-04_Drone_Visor_Happy.png`: Macro close-up frontal do visor exibindo a expressão `^‿^`.
     2. `TASK-04_Drone_Petting_Feedback.png`: Perspectiva em ação mostrando carinho ativo, corações holográficos e expressão `♥‿♥`.
     3. `TASK-04_Drone_Dancing_Pose.png`: Perspectiva contextual do Drone dançando no ritmo do rádio com expressão `♪‿♪`.

---

## 7. Critérios de Auditoria e Aprovação Visual & VR (Subagente [E])

O Auditor [E] deve reprovar sumariamente (`FAIL`) se qualquer uma das seguintes violações for detectada:

```
┌──────────────────────────────────────────────────────────────────────────────────────────────────┐
│                             CRITÉRIOS DE APROVAÇÃO DO AUDITOR [E]                                │
├──────────────────────────────────────────────────────────────────────────────────────────────────┤
│ [ ] O visor LED apresenta alinhamento perfeito sem Z-fighting contra a carcaça frontal?          │
│ [ ] As texturas dos olhos LED estão perfeitamente centralizadas e legíveis no close-up?         │
│ [ ] Os corações holográficos flutuam elegantemente a partir do topo do Drone na captura 2?       │
│ [ ] O Drone mantém pose inclinada dinâmica harmoniosa na captura de dança 3?                     │
│ [ ] 'TASK-04_SpatialAudit.json' confirma exatamente 1 Drone e 0 objetos deixados no (0,0,0)?      │
│ [ ] Todos os 4 arquivos WAV de áudio possuem pico estritamente <= -8.5 dBFS?                    │
│ [ ] Zero chamadas a 'renderer.material' no código C# (100% via MaterialPropertyBlock)?          │
└──────────────────────────────────────────────────────────────────────────────────────────────────┘
```

Somente quando todos os itens forem rigorosamente validados com `true`, a TASK-04 receberá o veredito final `PASS`.
