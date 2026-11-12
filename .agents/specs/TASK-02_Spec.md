# Especificação Técnica de Alta Precisão — TASK-02: Física de Arremesso por Balanço do Laser & Force Grab Ergonômico

**Documento Técnico:** `.agents/specs/TASK-02_Spec.md`  
**Autor:** Subagente [A] — Forensic Spec, Furniture Mapper & Playtest Bug Scout  
**Módulo:** Core / XRI Ergonomics  
**Status:** APROVADO PARA EXECUÇÃO  
**Data:** 11/11/2026  
**Versão do Unity:** 6000.0.85f2 (Unity 6)  
**Versão do XR Interaction Toolkit:** 2.6.5  
**Cenas Afetadas:** `Assets/Scenes/Menu_Principal.unity` e `Assets/Scenes/Floresta_e_Rio.unity`  
**Prefabs Afetados:** `Pessoa_Virtual_sem3D.prefab`, `Pessoa_Virtual_sem3D loading.prefab`, `Pessoa_Virtual.prefab`, `InteractiveRedApple.prefab`, `GoldenBonusApple.prefab`, `ErgonomicInteractiveToy.prefab`

---

## 1. Diagnóstico Forense & Investigação da Causa Raiz (Hotfix #3 do Playtest)

Durante a sessão de playtest com o protótipo de reabilitação motora em VR, foram reportadas duas falhas críticas de usabilidade e física:
1. **Dificuldade Extrema de Mira e Agarre com o Raio Laser:** Maçãs na árvore/chão e brinquedos na mesa exigiam alinhamento microscópico e frequentemente não respondiam ao clique do laser.
2. **Impossibilidade de Arremessar Balançando o Laser:** Ao segurar um objeto com o raio laser e fazer um movimento fluido de chicote/leque com o pulso para lançá-lo em uma cesta ou alvo, o objeto simplemente caía no chão aos pés do paciente ou flutuava desajeitadamente sem impulso algum.

### 1.1. Causa Raiz 1: Detecção por Raio Linear Fino vs. Alvos Pequenos (`HitDetectionType.Raycast`)
A inspeção dos prefabs do jogador (`Assets/Prefabs/Pessoa_Virtual_sem3D.prefab` e `Pessoa_Virtual_sem3D loading.prefab`) revelou:
* `m_HitDetectionType: 0` (`Raycast` 1D clássico, linear e com espessura zero);
* O campo `m_SphereCastRadius: 0.1` estava serializado, porém **inativo e ignorado pela Unity**, pois o tipo de detecção estava configurado como `0`;
* As maçãs interativas (`InteractiveApple`) possuem `SphereCollider` com raio de $0.043\text{m}$ (diâmetro de $8.6\text{cm}$);
* **Cálculo de Ângulo Sólido:** A uma distância de $6.0\text{m}$ (altura média da copa das árvores frutíferas), o diâmetro angular da maçã é de apenas $\theta \approx 2 \cdot \arctan\left(\frac{0.043}{6.0}\right) \approx 0.82^\circ$. Para pacientes em reabilitação motora (frequentemente com tremores posturais, hemiparesia ou rigidez por exoesqueleto), manter um feixe de espessura zero sobre um alvo de $0.82^\circ$ gera frustração e fadiga imediata;
* Além disso, `m_RaycastMask: 119` (`0b01110111`) excluía camadas de usuário e o `m_AllowAnchorControl: 0` impedia o paciente de puxar ou empurrar o objeto pelo eixo do raio com o analógico.

### 1.2. Causa Raiz 2: Falha Intrínseca do `throwOnDetach` Padrão da Unity XRI no Arremesso por Laser
A inspeção em `ErgonomicGrabInteractable.cs` e `XRGrabInteractable.cs` revelou a falha biomecânica da física padrão:
1. O XRI calcula a velocidade de descarte (`interactor.GetThrowVelocity(this)`) com base no **rastreamento inercial do GameObject do controle físico na mão do jogador**;
2. Quando o paciente segura uma maçã à distância com o raio laser ($R \approx 3.0\text{m}$ a $8.0\text{m}$) e flexiona o punho para arremessar:
   * A mão física se desloca linearmente apenas cerca de $5\text{cm}$ a $8\text{cm}$ em um intervalo de $\Delta t = 0.1\text{s}$, gerando uma velocidade de mão minúscula de $v_{\text{mão}} \approx 0.5\text{ m/s}$ a $0.8\text{ m/s}$;
   * No entanto, pela cinemática rotacional, a ponta do raio laser e o objeto segurado descrevem um arco no espaço com velocidade tangencial real dada por:
     $$\vec{v}_{\text{tangencial}} = \vec{\omega}_{\text{controle}} \times \vec{r}_{\text{laser}}$$
     Com uma velocidade angular de punho moderada de $\omega \approx 4.0\text{ rad/s}$ e um raio de alavanca de $r = 4.0\text{m}$, a velocidade escalar tangencial real na ponta é:
     $$v = 4.0 \times 4.0 = 16.0\text{ m/s}!$$
3. Como o `XRGrabInteractable` aplicava ao `Rigidbody` apenas a velocidade $v_{\text{mão}} \approx 0.5\text{ m/s}$, a maçã era solta sem qualquer inércia de balanço;
4. Em `ThrowAimAssist.cs`, a verificação `if (rawSpeed < minThrowSpeedThreshold)` com limiar de $0.35\text{ m/s}$ tratava quase todos os arremessos como meras "solturas estáticas", ignorando a trajetória balística e deixando o objeto cair em queda livre vertical.

---

## 2. Reconfiguração Ergonômica dos Controles do Jogador (`XRRayInteractor`)

Para sanar a dificuldade de mira e garantir acessibilidade total, os interatores de raio de ambos os controles (mão esquerda e mão direita) devem ser reconfigurados nos 3 prefabs do Rig de jogador e nas instâncias das cenas `Menu_Principal` e `Floresta_e_Rio`:

| Parâmetro | Valor Anterior (Bug) | Novo Valor Otimizado | Justificativa Clínica / Ergonômica |
| :--- | :--- | :--- | :--- |
| `hitDetectionType` | `0` (`Raycast`) | **`1` (`SphereCast`)** | Projeta uma esfera volumétrica ao longo do raio em vez de uma linha com espessura zero. |
| `sphereCastRadius` | `0.10f` (inativo) | **`0.06f` ($6\text{cm}$ de raio)** | Cria um cilindro de captura de $12\text{cm}$ de diâmetro. Combinado com o colisor da maçã ($8.6\text{cm}$), a tolerância de captura sobe de $0.82^\circ$ para mais de $2.0^\circ$ ($\approx 250\%$ maior). |
| `raycastMask` | `119` (restritivo) | **`-1` (`Everything` / `~0`)** | Garante que nenhum objeto interativo em camadas personalizadas seja ignorado. |
| `interactionLayers` | `4294967295` (-1) | **`-1` (`Everything`)** | Mantém compatibilidade plena com todas as máscaras de interação do XRI. |
| `raycastTriggerInteraction` | `1` (`Ignore`) | **`QueryTriggerInteraction.Ignore` (`1`)** | Impede que volumes de trigger invisíveis (ex: zonas de vento, retículas, áudio) bloqueiem o raio de alcançar o colisor sólido da maçã. |
| `allowAnchorControl` | `0` (`false`) | **`true`** | Permite ao paciente aproximar ou afastar o objeto segurado ao longo do raio usando o analógico (*thumbstick/scroll*). |
| `useForceGrab` | `1` (`true`) | **`true`** | Permite atração magnética e manipulação à distância. |
| `maxRaycastDistance` | `30.0f` | **`35.0f`** | Alcance suficiente para cobrir todas as copas de árvores do pomar e alvos no cenário. |

---

## 3. Arquitetura do Componente `LaserSwingThrowInteractable`

O novo componente `LaserSwingThrowInteractable` (herdeiro direto de `ErgonomicGrabInteractable` ou integrado a ele) substitui o cálculo tradicional de descarte do XRI por um rastreador cinemático de alta precisão em espaço global.

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                        ErgonomicGrabInteractable (XRGrabInteractable)                  │
├────────────────────────────────────────────────────────────────────────────────────────┤
│  • useForceGrab = true                                                                  │
│  • Ring Buffer Circular de Poses (12 amostras fixas, Zero GC Alloc)                     │
│  • Laser Swing Velocity Tracker (Tangencial + Angular)                                 │
│  • Interceptador OnSelectExited -> Aplicação de Inércia Real ao Rigidbody               │
│  • Dynamic PhysX VR Optimization (Discrete -> ContinuousDynamic -> Sleep)              │
│  • OutOfBoundsSafetyGuard (Proteção Anti-Queda debaixo do Terreno)                      │
└──────────────────────────────────────┬─────────────────────────────────────────────────┘
                                       │ Delega Trajetória
                                       ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                                 ThrowAimAssist                                         │
├────────────────────────────────────────────────────────────────────────────────────────┤
│  • Cone de captura sutil: 18° (sem magnetismo artificial exagerado)                    │
│  • Varredura O(N) sem GC via AimAssistTargetRegistry (Cestas, Aros, Drone)             │
│  • Cálculo de parábola balística com gravidade real (g = 9.81 m/s²)                    │
│  • Interpolação suave (Lerp 50%) entre vetor humano tangencial e parábola ideal       │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

### 3.1. Estrutura de Amostragem (Zero Alocações de GC)
Para evitar picos de Garbage Collector em VR a 90 FPS, a amostragem de movimento é realizada através de um buffer circular pré-alocado:

```csharp
[System.Serializable]
private struct PoseSample
{
    public Vector3 position;
    public Quaternion rotation;
    public float timestamp;
}

// 12 amostras fixas cobrindo ~0.13s a 90Hz (ou ~0.20s a 60Hz)
private const int BUFFER_SIZE = 12;
private readonly PoseSample[] poseRingBuffer = new PoseSample[BUFFER_SIZE];
private int bufferHead = 0;
private int sampleCount = 0;
```

* **Frequência de Amostragem:** Executada a cada frame durante `Update` ou `LateUpdate` enquanto `isSelected == true` e o interator for um `XRRayInteractor`.
* **Amostragem Global:** Registra `transform.position` e `transform.rotation` no espaço de mundo (*World Space*), capturando perfeitamente tanto o movimento da mão quanto o efeito alavanca angular do feixe do laser.

---

## 4. Algoritmo Matemático de Velocidade Tangencial e Rotação Física

### 4.1. Cálculo da Velocidade Linear Tangencial no Momento de Soltura (`OnSelectExited`)
Quando o paciente solta o botão de agarrar, o algoritmo não consulta o controle; em vez disso, ele processa o histórico de poses da própria ponta/objeto no buffer:

1. **Janela Temporal Relevante:** Filtra as amostras contidas na janela de $\Delta t \in [0.03\text{s}, 0.12\text{s}]$ anteriores a `Time.time`. Amostras com menos de $0.02\text{s}$ podem conter ruído de soltura do botão mecânico, enquanto amostras com mais de $0.12\text{s}$ perdem o pico da aceleração do chicote.
2. **Média Ponderada por Tempo (Temporal Weighting):** Amostras mais recentes recebem peso progressivamente maior:
   $$\vec{v}_{\text{raw}} = \frac{\sum_{i=1}^{K} w_i \cdot \frac{\vec{p}_i - \vec{p}_{i-1}}{t_i - t_{i-1}}}{\sum_{i=1}^{K} w_i}, \quad w_i = 1.0 + \frac{i}{K}$$
3. **Multiplicador Ergonômico de Impulso (`throwBoostMultiplier`):**
   * Configuração padrão: `1.65f` (ajustável no Inspector entre `1.2f` e `2.2f`);
   * Compensa a menor excursão articular de pacientes clínicos em exoesqueleto.
4. **Clamp de Segurança para VR (`maxThrowSpeed`):**
   * Velocidade máxima limitada rigidamente a $18.0\text{ m/s}$ ($64.8\text{ km/h}$);
   * Previne tunelamento físico através de paredes finas e instabilidades no solver da PhysX.

### 4.2. Cálculo da Velocidade Angular para Giro Realista em Voo
Para que maçãs, pinhas e bolas não voem parecendo blocos estáticos congelados no ar:
1. Extrai a rotação inicial e final na janela de amostragem:
   $$q_{\text{delta}} = q_{\text{mais\_recente}} \times q_{\text{antigo}}^{-1}$$
2. Converte para representação de ângulo e eixo (`q_delta.ToAngleAxis(out float angleDeg, out Vector3 axis)`):
   * Converte para radianos por segundo:
     $$\vec{\omega} = \left(\frac{\text{angleDeg} \times \frac{\pi}{180}}{\Delta t}\right) \cdot \text{axis}$$
3. Clampa a rotação máxima a $25.0\text{ rad/s}$ e aplica diretamente ao `rigidbody.angularVelocity`.

---

## 5. Calibração do `ThrowAimAssist` (Cone Sutil de 18°)

O sistema de assistência balística foi reajustado para eliminar o comportamento "magnético artificial" que ocorria com o cone anterior de $38^\circ$:

* **Ângulo Máximo do Cone (`maxConeAngleDegrees`):** Reduzido para **`18.0f`** graus. O auxílio balístico só entra em ação se o arremesso do paciente tiver intenção clara em direção ao alvo (cesta de maçãs, aro de basquete ou baú).
* **Alcance Máximo de Detecção:** $16.0\text{m}$.
* **Força de Interpolação (`aimAssistStrength`):** `0.50f` ($50\%$ vetor humano tangencial real, $50\%$ parábola balística matematicamente corrigida). O arremesso mantém a curva pessoal do paciente, mas recebe a elevação necessária para entrar na cesta com arco satisfatório.
* **Fórmula da Parábola Balística:**
  $$\vec{v}_{\text{horizontal}} = \frac{\vec{d}_{xz}}{t_{\text{voo}}}, \quad v_y = \frac{\Delta y + \frac{1}{2} g \cdot t_{\text{voo}}^2}{t_{\text{voo}}}$$
  onde $t_{\text{voo}} = \frac{\|\vec{d}_{xz}\|}{v_{\text{tangencial}} \times \cos(40^\circ)}$.

---

## 6. Otimização PhysX para VR 90 FPS & Proteção Anti-Queda

### 6.1. Ciclo de Vida de Detecção de Colisão Dinâmica (`PhysX Lifecycle`)
Para economizar CPU do headset VR quando dezenas de maçãs caírem das árvores:
1. **Em Repouso no Solo/Mesa:** `CollisionDetectionMode.Discrete` e `rigidbody.Sleep()`.
2. **Durante o Agarre por Laser:** `CollisionDetectionMode.ContinuousDynamic` (garante rastreamento confiável e sem penetração).
3. **Em Voo Balístico:** Permanece em `ContinuousDynamic` enquanto a velocidade for alta.
4. **Transição Automática Pós-Pouso:** Uma corrotina leve monitora `rigidbody.linearVelocity.sqrMagnitude < 0.04f`. Após $3.0\text{ segundos}$ consecutivos em repouso estável, o script rebaixa para `CollisionDetectionMode.Discrete` e força `Sleep()`.
5. **Colisores Primitivos Exclusivos:** Todos os itens móveis usam estritamente `SphereCollider` ou `BoxCollider`. Proibido `MeshCollider` em objetos com `Rigidbody`.

### 6.2. Proteção Anti-Queda Fora do Mapa (`OutOfBoundsSafetyGuard`)
Para sanar o problema de itens caindo debaixo do terreno ou atravessando o piso:
* **Na Floresta (`Floresta_e_Rio.unity`):**
  * Consulta periódica via `Terrain.activeTerrain.SampleHeight(pos)` ou `Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, ...)`;
  * Se $Y_{\text{objeto}} < Y_{\text{terreno}} - 1.2\text{m}$ (ou $Y < -1.0\text{m}$ caso não haja terreno abaixo):
    * O objeto tem sua velocidade zerada;
    * É reposicionado suavemente para a borda da trilha mais próxima ou para o ponto de descanso inicial (`HomePosition`).
* **No Menu Principal (`Menu_Principal.unity`):**
  * Se $Y_{\text{objeto}} < 0.20\text{m}$ ou distância da mesa $> 4.5\text{m}$ por mais de $2.5\text{s}$, aciona o voo suave de retorno à mesa (`ReturnHomeRoutine`).

---

## 7. Matriz de Tarefas para os Subagentes Especialistas

| Subagente | Escopo Exclusivo | Artefato a Produzir / Modificar |
| :--- | :--- | :--- |
| **`[B]` Blender & Audio Artist** | Modelagem 3D & Áudio VR | FBX da Retícula de Mira (`ReticleTargetRing.fbx`) já existente verificado; gerar áudio suave de "woosh" de arremesso (`laser_throw_woosh.wav`, peak -6dB, 0.4s) para feedback tátil/sonoro de arremesso. |
| **`[C]` Senior C# VR Engineer** | Código C# de Runtime | Refatorar `ErgonomicGrabInteractable.cs` e `ThrowAimAssist.cs` implementando o Ring Buffer de velocidade tangencial, o cálculo de rotação física, o clamp de segurança e a otimização dinâmica PhysX. Atualizar `InteractiveApple.cs` para herdar perfeitamente esse comportamento. |
| **`[D]` Layout & Purger Engineer** | Editor Scripting & Setup | Criar e executar `Task02_SceneAndPrefabBuilder.cs`: atualizar os prefabs de jogador (`Pessoa_Virtual*.prefab`) com `SphereCast` ($r=0.06\text{m}$), máscara `Everything` e `AllowAnchorControl`; validar cenas `Menu_Principal` e `Floresta_e_Rio`; purgar resíduos no `(0,0,0)`; gerar telemetria `TASK-02_SpatialAudit.json` e as 3 capturas visuais. |
| **`[E]` Ruthless Visual QA Auditor** | Auditoria e Veredito | Inspecionar capturas, verificar ausência de erros no console da Unity, validar que não há objetos duplicados ou flutuando, e emitir veredito `PASS` em `TASK-02_AuditVerdict.md`. |

---

## 8. Critérios de Aceite Inegociáveis (QA Checklist)

- [ ] **SphereCast Ativo:** Ambos os `XRRayInteractor` no Rig do jogador utilizam `hitDetectionType = SphereCast` com raio de $0.06\text{m}$.
- [ ] **Máscara Tudo:** `raycastMask` e `interactionLayers` configurados como `-1` (`Everything`).
- [ ] **Arremesso Tangencial Funcional:** Balançar o laser e soltar uma maçã ou o brinquedo ergonômico projeta o item fisicamente para a frente com velocidade proporcional ao movimento angular da ponta do raio.
- [ ] **Zero Descarte Nulo:** O objeto nunca cai morto verticalmente no chão durante um movimento de balanço.
- [ ] **Aim Assist Sutil:** O cone de $18^\circ$ guia suavemente arremessos em direção a cestas de maçãs e aros de basquete sem parecer magnético ou brusco.
- [ ] **Proteção Anti-Queda:** Nenhum objeto cai abaixo do solo ou se perde no infinito da cena da Floresta.
- [ ] **Performance VR:** 0 alocações de memória (`0 B GC Alloc`) durante a amostragem do Ring Buffer no frame de descarte.
- [ ] **Zero Lixo na Origem:** Nenhum prefab ou maçã deixada órfã em `(0, 0, 0)`.

---
*Fim da Especificação Técnica TASK-02. Subagente [A] concluiu com sucesso a investigação forense e a especificação detalhada.*
