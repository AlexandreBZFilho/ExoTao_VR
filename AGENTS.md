# Orquestração Autônoma de Micro-Subagentes, Correções de Playtest & Otimização VR — VR Rehab Gamification (Unity XRI)

> **Instrução de Inicialização no Antigravity (Modo Deep Polish, Hotfix & VR Optimization):**
>
> 1. **PASSO ZERO OBRIGATÓRIO:** Salve este arquivo fisicamente na raiz do workspace em dois locais: `./AGENTS_IMPLEMENTATION_PLAN.md` e `./AGENTS.md` (para ativação contínua `always_on`).
> 2. **Unidade Atômica de Execução (Regra Anti-Limite de Output):** Como o modelo opera com janela de *output* restrita por chamada, **É TERMINANTEMENTE PROIBIDO que um único subagente tente fazer uma Task inteira de uma vez**.
> 3. **1 Subagente = 1 Única Coluna da Matriz (`[A]`, `[B]`, `[C]`, `[D]` ou `[E]`):** O Agente Orquestrador Principal deve chamar `invoke_subagent` para executar **apenas UM micro-passo por vez** (ex: primeiro invoca um subagente exclusivo para `TASK-01.A`, atualiza a tabela no disco; depois invoca outro subagente exclusivo para `TASK-01.B`, e assim por diante).
> 4. **Modo de Operação da Unity:** O **Unity Editor JÁ ESTÁ ABERTO** na máquina. Conecte-se diretamente à instância aberta via **Unity MCP / CLI Bridge** e menus `[MenuItem]` (não abra instâncias concorrentes que travem o `UnityLockfile`).
> 5. **Auto-Resume Pós-Limite de Tokens:** Se a sessão atingir o limite de contexto, basta enviar em um novo chat:
>    *"Leia `@AGENTS_IMPLEMENTATION_PLAN.md`, verifique a Matriz de Micro-Checkpoints na Seção 4 e retome a invocação de micro-subagentes exatamente da primeira célula marcada como `[~]` ou `[ ]`."*

---

## 1. Arquitetura de 5 Micro-Subagentes por Tarefa (Com Loop de Retrabalho Visual e Performance VR)

Para garantir qualidade máxima em cada linha de código C#, em cada malha 3D do Blender, na taxa de quadros do VR (72/90 FPS estáveis) e no posicionamento espacial das cenas (`Menu Principal` e `Floresta e Rio`), o Agente Orquestrador Principal gerencia **5 papéis de Subagentes Especialistas independentes**. Cada subagente dedica **100% do seu orçamento de tokens de saída** para uma única especialidade:

```
[Subagente A: Forensic Spec, Scene Furniture & Bug Scout]
       │ (Inspeciona cenas, móveis existentes, bugs de playtest e gera .agents/specs/TASK-XX_Spec.md)
       ▼
[Subagente B: Blender 3D Low-Poly VR & Audio Tech Artist] ◄────────────┐
       │ (Modela .fbx otimizados sem partes invisíveis + áudios suaves)│
       ▼                                                               │ (Ordem de Correção de Arte/Áudio:
[Subagente C: Senior Unity C# Gameplay & VR Physics Engineer] ◄────┐   │  ex: pássaro sem pernas, som estridente,
       │ (Escreve scripts C# zero-GC, física de laser e lógica XRI)│   │  malha pesada, pivô errado)
       ▼                                                           │   │
[Subagente D: Multi-Furniture Layout, (0,0,0) Purger & Grounding]◄┐│   │
       │ (Espalha nas mesas/estantes reais, limpa (0,0,0) e tira PNG)│   │
       ▼                                                         ││   │
[Subagente E: Ruthless Visual QA & VR Performance Auditor] ──────┴┴───┘
       │ (Inspeciona PNGs + JSON de Bounds/Duplicatas/DrawCalls.
       ▼  Só aprova [x] com 0 sobreposição, 0 lixo no (0,0,0), 0 flutuação e 0 bugs de playtest)
```

### Detalhamento Rigoroso de Cada Micro-Subagente

#### 1. Subagente `[A]` — `Forensic Spec, Furniture Mapper & Playtest Bug Scout`
* **Escopo Exclusivo:** Não escreve código C# de produção nem modela no Blender.
* **Missão:**
  1. Inspecionar as cenas abertas/disponíveis (`Menu Principal` e `Floresta e Rio`), os scripts atuais em `Assets/` e os prints em `.agents/visual_qa/`.
  2. Verificar se algum objeto necessário foi apagado acidentalmente durante a limpeza manual do usuário (para que seja recriado limpo) e mapear todas as duplicatas órfãs deixadas na coordenada `(0, 0, 0)` ou espalhadas em excesso.
  3. Na sala do Menu Principal, **listar todas as mesas, bancadas e estantes (`Shelves`/`Tables`/`Desks`) já existentes na arquitetura da sala** para distribuir os brinquedos e minigames entre elas, em vez de amontoar tudo em uma única mesa.
  4. Escrever `.agents/specs/TASK-XX_Spec.md` detalhando hierarquia, correções obrigatórias do playtest e orçamento de performance VR.

#### 2. Subagente `[B]` — `Blender MCP VR-Optimized 3D & Audio Tech Artist`
* **Escopo Exclusivo:** Dedica 100% do *output budget* para modelagem 3D otimizada para VR no **Blender MCP** (ou script Python `bpy`) e síntese de áudio/texturas em Python/Gemini API. **Proibido gastar tokens escrevendo C# da Unity.**
* **Regras de Qualidade Artística e Otimização VR Obrigatórias:**
  1. **Anatomia e Geometria 100% Completas (Zero Partes Invisíveis):** Nenhum modelo pode ter partes faltando ou normais invertidas (`backface culling`). O `LowPolyForestBird.fbx` **DEVE** ter corpo, cabeça, bico, olhos, cauda, duas asas separadas **E DUAS PERNAS/PATAS VISÍVEIS E SÓLIDAS** conectadas do corpo até o ponto de pouso ($Y = 0$).
  2. **Orçamento Rigoroso de Polígonos para VR (*Polycount Budget*):**
     * Objetos pequenos de mão/coleta (maçãs, fitas cassete, moedas, pinhões, bolinhas): **máximo de $300$ a $600\text{ triângulos}$**.
     * Objetos médios/estáticos (cestas, fidgets, pistola de balões, pássaro, troféus, totem): **máximo de $800$ a $1.800\text{ triângulos}$**.
     * Aplicar sempre `Shade Smooth` com `Auto Smooth` / `Weighted Normals` e chanfros de 1 segmento para parecerem redondos e bonitos gastando pouquíssimos vértices.
  3. **Áudio Confortável e Calibrado (Zero Sons Estridentes):** Nenhum efeito sonoro `.wav` pode ter pico de volume agressivo (`peak <= -6 dB`). O canto do pássaro (`forest_bird_chirp.wav`) deve ser suave, melódico, curto ($0.6\text{s}$), com filtro *low-pass* em $3.500\text{ Hz}$ para nunca irritar o ouvido do paciente.

#### 3. Subagente `[C]` — `Senior Unity C# Gameplay, Laser Physics & VR Performance Engineer`
* **Escopo Exclusivo:** Dedica 100% do *output budget* para escrever ou refatorar os scripts C# de runtime em `Assets/Scripts/Interactive/TaskXX/`.
* **Regras de Qualidade de Engenharia Obrigatórias:**
  1. **Zero Placeholders ou `// TODO`:** Todos os mecanismos, fidgets, cestas, árvores sacudíveis, rádio, basquete e drone devem funcionar de ponta a ponta.
  2. **Implementação Estrita dos Hotfixes do Playtest (Seção 2):** Garantir que o teleporte automático no Menu seja 100% removido, que o Drone funcione por arraste/scroll mantendo distância relativa sem empurrar o player e sem buscar maçãs, e que as maçãs tenham física real de arremesso ao balançar o raio laser.
  3. **Disciplina de Código VR (90 FPS / Zero GC Alloc):** Seguir todas as diretrizes da Seção 3 (zero `FindObjectsOfType` ou `GetComponent` em `Update`, uso de `sqrMagnitude`, *Object Pooling*, desativação de física contínua em repouso).

#### 4. Subagente `[D]` — `Multi-Furniture Layout, Origin (0,0,0) Purger & Grounding Engineer`
* **Escopo Exclusivo:** Dedica 100% do *output budget* para criar e rodar o Editor Script (`Assets/Editor/AutoSetup/TaskXX_SceneAndPrefabBuilder.cs`) na instância aberta da Unity e gerar os prints e relatórios de auditoria.
* **4 Leis Espaciais Inegociáveis:**
  1. **Lei 1 — Varredura e Expurgo de Duplicatas e Lixo no `(0, 0, 0)` (`Origin & Duplicate Sweeper`):**
     * Antes e depois de posicionar qualquer objeto na cena (`Menu Principal` ou `Floresta e Rio`), o script de Editor **DEVE varrer toda a hierarquia da cena** procurando:
       * Qualquer instância duplicada de objetos interativos (garantindo que exista apenas a quantidade exata planejada e recriando apenas se algum objeto essencial tiver sido apagado por engano);
       * Qualquer objeto interativo, prefab temporário ou malha órfã deixada próxima à origem da cena `Vector3.zero` (`Vector3.Distance(obj.transform.position, Vector3.zero) < 0.8f`) que já possua sua versão correta posicionada na sala ou na trilha, **deletando imediatamente a duplicata do `(0, 0, 0)`**!
  2. **Lei 2 — Distribuição Inteligente pelas Mesas e Estantes Existentes da Sala (`Multi-Furniture Distribution`):**
     * Na sala do Menu Principal, **NÃO amontoar todos os objetos em uma única mesa**. O script deve localizar as várias mesas, bancadas e estantes existentes na sala e espalhar cada estação interativa, minigame, brinquedo fidget e troféu em móveis diferentes com amplo espaço livre (mínimo de $0.25\text{m}$ entre itens no mesmo móvel, validado por `!boundsA.Intersects(boundsB)`).
  3. **Lei 3 — Aterramento Físico Absoluto e Prevenção de Queda Fora do Mapa (`Zero Floating & Map Bounds Clamp`):**
     * Na cena `Floresta e Rio`, todos os objetos (árvores, cestas, maçãs, cogumelos, lanternas, pássaros, totens) devem estar estritamente **dentro dos limites válidos do terreno/malha do mapa** (nunca fora da borda nem debaixo do chão) e perfeitamente aterrados via `Physics.Raycast` / `Terrain.SampleHeight`.
  4. **Lei 4 — Exportação de Telemetria Espacial + 3 Screenshots HD:**
     * Exportar `.agents/visual_qa/TASK-XX_SpatialAudit.json` (contendo contagem de instâncias para provar zero duplicatas, verificação de `(0,0,0)` vazio, `hasOverlap: false` e `groundGapMeters`) e renderizar 3 screenshots em `1920x1080` (`view1_front.png`, `view2_side_grounding.png`, `view3_closeup.png`).

#### 5. Subagente `[E]` — `Ruthless Visual QA & VR Performance Auditor (O Juiz Implacável)`
* **Escopo Exclusivo:** Subagente analítico e crítico. Inspeciona visualmente os prints `.png` (antigos e novos) e o `TASK-XX_SpatialAudit.json`.
* **Checklist de Reprovação Imediata (`FAIL`):**
  1. Há algum objeto duplicado ou largado na coordenada `(0, 0, 0)` da cena?
  2. Os fidgets e minigames continuam espremidos na mesma mesa em vez de espalhados pelas várias mesas e estantes da sala do Menu?
  3. Há qualquer árvore, cesta, cogumelo ou objeto flutuando acima do chão ou caindo para fora/debaixo do mapa na cena `Floresta e Rio`?
  4. O pássaro continua com pernas invisíveis ou apitando sem parar? As maçãs falham ao serem pegas/arremessadas com o laser? O menu ainda tenta teletransportar sozinho ao dar Play?
* **Poder de Veto:** Se qualquer item falhar, escreve `FAIL` em `.agents/visual_qa/TASK-XX_AuditVerdict.md` e ordena `RE-RUN [B]`, `RE-RUN [C]` ou `RE-RUN [D]` até atingir perfeição absoluta (`PASS`).

---

## 2. Hotfixes Críticos do Playtest VR (10 Regras Inegociáveis de Correção)

Todos os subagentes devem tratar estas 10 correções reportadas no teste prático como **prioridade máxima absoluta**:

1. **FIM DO TELEPORTE AUTOMÁTICO NO MENU PRINCIPAL (`TASK-10`):**
   * **Problema:** Ao dar Play na cena do Menu Principal, o jogador era teletransportado imediatamente para a cena da Floresta e Rio por causa do gatilho de proximidade/portal das esferas.
   * **Correção Obrigatória:** **Remover 100% de qualquer chamada `SceneManager.LoadScene` ou `LoadSceneAsync` dos scripts das Esferas de Portal/Diorama (`PortalWorldSphere.cs`, `PortalPedestalSocket.cs` ou similares)!** A mudança de cena para os mapas deve acontecer **EXCLUSIVAMENTE pelo sistema de Canvas 2D original do projeto**. O `PortalOrbDiorama_Forest` deve funcionar **apenas como uma peça de diorama 3D decorativa e interativa** (o jogador pode pegar, olhar a mini-floresta girando lá dentro e encaixar no pedestal para acender luzes/partículas, mas **jamais** carregar outra cena).
2. **NOVO COMPORTAMENTO DO DRONE: POSICIONAMENTO POR LASER/SCROLL, DISTÂNCIA RELATIVA E ZERO COLISÃO COM O PLAYER (`TASK-03`):**
   * **Problema:** O Drone tinha textos desnecessários (`- perto` e `+ longe`) embaixo dele, tinha colisão física que empurrava o jogador, e ia buscar maçãs (inclusive debaixo do mapa).
   * **Correção Obrigatória:**
     * **Remover os textos/botões `- perto` e `+ longe` embaixo do Drone.**
     * **O Drone NUNCA deve buscar maçãs nem sair para pegar objetos!** Ele é estritamente o companheiro de acompanhamento e informação do paciente.
     * **Zero Colisão com o Jogador:** Todos os colisores do Drone devem ignorar completamente a colisão física com o `CharacterController` / `XROrigin` / `Player` (`Physics.IgnoreCollision` + `Rigidbody.isKinematic = true`), de modo que o Drone **jamais empurre ou bloqueie o jogador**. Apenas o colisor de interação do raio laser permanece ativo para o controle.
     * **Mecânica de Segurar, Ajustar com Scroll e Manter Distância Relativa:**
       1. O Drone começa parado em uma posição confortável na frente do jogador no início da cena para que o jogador possa se aproximar e ler as informações tranquilamente.
       2. O jogador pode segurar o Drone apontando o raio laser (`XRRayInteractor` + `XRGrabInteractable` com `translate/anchor control` via thumbstick/scroll) para puxá-lo para mais perto, afastá-lo para mais longe ou colocá-lo em qualquer posição ao seu redor.
       3. No instante em que o jogador solta o Drone (`OnSelectExited`), o Drone **salva exatamente aquele vetor de distância/offset relativo ao jogador** e, a partir dali, passa a **seguir o jogador suavemente mantendo sempre aquela exata distância onde foi colocado** (permitindo que o jogador o reajuste para mais perto ou mais longe com o laser/scroll a qualquer momento durante a caminhada!).
3. **MAÇÃS 100% PEGÁVEIS E ARREMESSÁVEIS COM FÍSICA DE BALANÇO DO LASER (`TASK-02` & `TASK-11`):**
   * **Problema:** Clicar nas maçãs com o laser não as segurava corretamente, e não era possível arremessá-las balançando o laser.
   * **Correção Obrigatória:**
     * Configurar todas as maçãs (no chão e nas árvores sacudíveis) com `Rigidbody` (`useGravity = true`, `isKinematic = false`), `SphereCollider` sólido na layer compatível com a `InteractionLayerMask = Everything (-1)` do `XRRayInteractor`.
     * **Física de Arremesso por Chicote/Balanço do Laser (`LaserSwingVelocityTracker`):** Quando o jogador segura uma maçã com o raio laser e balança o feixe de laser (movimento angular do controle apontando de longe), a velocidade linear da mão quase não muda, mas a **ponta do raio onde a maçã está presa se move rapidamente pelo espaço**. O script deve gravar um *Ring Buffer* das últimas 10 posições globais (`transform.position`) da própria maçã a cada frame enquanto segurada e, no momento em que o jogador solta o botão (`OnSelectExited`), calcular o vetor de velocidade tangencial real ($\vec{v} = \frac{\vec{p}_{\text{atual}} - \vec{p}_{\text{anterior}}}{\Delta t}$) e aplicá-lo diretamente ao `rigidbody.linearVelocity` / `rigidbody.velocity`, fazendo a maçã voar fisicamente na exata direção e velocidade em que o laser foi balançado!
4. **PREVENÇÃO DE ITENS FORA DO MAPA NA FLORESTA E RIO (`TASK-11` & `TASK-12`):**
   * **Problema:** Maçãs e itens estavam nascendo fora do mapa ou caindo para debaixo do terreno na cena `Floresta e Rio`.
   * **Correção Obrigatória:** Todos os spawners e posições iniciais na cena `Floresta e Rio` devem validar se há chão/terreno sólido abaixo via `Physics.Raycast` antes de posicionar qualquer item. Além disso, qualquer objeto arremessável que caia abaixo do nível mínimo da trilha (`transform.position.y < terrainHeight - 1.5f`) deve ser automaticamente reposicionado na margem da trilha ou desativado no pool (nunca caindo infinitamente debaixo do mapa).
5. **CORREÇÃO DO ÁUDIO DO PÁSSARO NA FLORESTA E RIO (`TASK-13`):**
   * **Problema:** Quando o jogador chegava perto do pássaro na cena `Floresta e Rio`, ele ficava gritando sem parar e extremamente alto.
   * **Correção Obrigatória:**
     * `AudioSource.loop = false` (TERMINANTEMENTE PROIBIDO deixar em loop!).
     * Volume máximo reduzido para `0.18f` com `spatialBlend = 1.0f` (3D) e `maxDistance = 10.0f`.
     * Cooldown rígido (`chirpCooldown = 15.0f`) e trava de estado (`hasChirpedThisEncounter = true`): quando o jogador se aproxima, o pássaro toca **um único pio curto e suave** de $0.5\text{s}$ e voa silenciosamente para a próxima árvore, sem nunca repetir o som em loop.
6. **ESPALHAR FIDGETS, MINIGAMES E TROFÉUS PELAS VÁRIAS MESAS E ESTANTES DA SALA DO MENU (`TASK-06` a `TASK-10` & `TASK-16`):**
   * **Problema:** Todos os fidgets foram colocados espremidos em uma única mesa, sendo que a sala do Menu Principal possui várias mesas, bancadas e estantes.
   * **Correção Obrigatória:** Mapear todas as superfícies de móveis existentes na sala do Menu Principal e distribuir os itens:
     * **Mesa Principal / Perto do Rádio:** Rádio existente + Deck de Fitas + 4 Fitas Cassete espaçadas + Diorama da Floresta (sem teleporte).
     * **Segunda Mesa / Bancada Lateral Existente:** Estação Fidget espaçada (Alavanca de Clima, Botão de Fogos e Bobblehead com $0.35\text{m}$ de folga entre eles).
     * **Terceira Superfície / Mesa Auxiliar Existente:** Pistola de Balões no suporte + Botão Spawner e Bolas do Mini Basquete Arcade (com a tabela na parede livre correspondente).
     * **Estantes (`Shelves`) Existentes na Sala:** Os 4 Troféus Desbloqueáveis (`Maçã Dourada`, `Bonsai Luminoso`, `Taça Estrela` e `Coroa do Drone`) distribuídos nas prateleiras das estantes da sala!
7. **EXPURGO AUTOMÁTICO DE DUPLICATAS E OBJETOS NO `(0, 0, 0)` + RESTAURAÇÃO DE ITENS FALTANTES (`GLOBAL`):**
   * **Problema:** Havia várias cópias duplicadas de objetos no Menu e na Floresta e Rio, muitas concentradas na coordenada `(0, 0, 0)` da cena. O usuário apagou duplicatas manualmente e pode ter removido algo sem querer.
   * **Correção Obrigatória:** Criar e rodar em todas as cenas o utilitário `SceneDuplicateAndOriginPurger.cs` que:
     1. Verifica se todos os objetos únicos esperados por cada Task estão presentes em suas posições corretas na cena (recriando apenas se algum tiver sido apagado sem querer);
     2. Deleta automaticamente qualquer instância duplicada excedente;
     3. Varre a coordenada `(0, 0, 0)` (raio de $1.0\text{m}$) e remove qualquer objeto gerado que tenha sido esquecido lá como cópia de um objeto já posicionado na cena.
8. **COGUMELOS MUSICAIS PRESERVADOS E BEM DISTRIBUÍDOS (`TASK-13`):**
   * **Feedback:** Os cogumelos musicais funcionaram muito bem e são muito divertidos!
   * **Ação:** Preservar 100% da mecânica e timbre dos cogumelos musicais, garantindo apenas que sejam distribuídos em grupos limpos e bem aterrados ao longo das margens da trilha na cena `Floresta e Rio`, sem duplicatas sobrepostas nem cópias no `(0, 0, 0)`.

---

## 3. Segredos de Otimização de Indústria para VR (Garantia de 90 FPS Leve e Fluido)

Para garantir que todas as 16 implementações rodem com leveza extrema em VR sem causar *motion sickness* ou queda de FPS, todo subagente `[B]`, `[C]`, `[D]` e `[E]` é obrigado a aplicar estas técnicas de indústria:

1. **GPU Instancing & SRP Batcher Compatibility:**
   * Todos os materiais gerados devem habilitar `material.enableInstancing = true` e usar o mesmo shader base do pipeline (`Universal Render Pipeline/Lit` ou `Standard`).
   * **Proibido usar `renderer.material` em runtime** (isso duplica materiais na memória e quebra o *batching*). Para piscar luzes ou destaques de hover, usar exclusivamente `Renderer.GetPropertyBlock` / `SetPropertyBlock` (`MaterialPropertyBlock`).
2. **Física VR Ultra-Leve (`PhysX Optimization`):**
   * **Zero `MeshCollider` em objetos móveis:** Qualquer objeto com `Rigidbody` (maçãs, fitas, bolas de basquete, pinhões, balões, troféus) deve usar apenas colisores primitivos (`SphereCollider`, `BoxCollider` ou `CapsuleCollider`), que são $20\times$ mais rápidos para a CPU calcular.
   * **Dynamic Collision Detection Switching:** Objetos arremessáveis ficam com `CollisionDetectionMode.Discrete` e entram em `Sleep()` quando parados no chão. O script muda para `CollisionDetectionMode.ContinuousDynamic` **apenas no evento `OnSelectEntered`** (quando o jogador pega o objeto) e volta para `Discrete` $3\text{ segundos}$ após tocar o chão.
3. **Throttling por Distância ao Quadrado (`sqrMagnitude`) e Zero `Update()` Desnecessário:**
   * Objetos espalhados pelo mapa grande da `Floresta e Rio` (moedas, cogumelos, pássaros, lanternas, árvores) **não devem rodar lógica pesada todo frame se estiverem longe do jogador**.
   * Usar `if ((playerTransform.position - transform.position).sqrMagnitude > activeRadiusSqr) return;` ou acionar animações apenas via eventos do `XRRayInteractor` (`hoverEntered`, `selectEntered`, `activated`) e corrotinas temporárias que se encerram sozinhas quando a animação acaba!
4. **Iluminação VR sem Sombras Dinâmicas Extras (`Zero Extra Shadow Casters`):**
   * Qualquer `Light` adicionada em lanternas da trilha, feixe do drone ou pedestal deve ter obrigatoriamente `light.shadows = LightShadows.None`. Em VR, cada Point Light com sombra dinâmica renderiza 6 cubemaps por olho (12 passes extras!).
   * Desativar `Receive Shadows` e `Cast Shadows = Off` em partículas e pequenos detalhes de UI.

---

## 4. Matriz de Estado e Micro-Checkpoints (Persistência Anti-Token Limit)

*O Agente Orquestrador Principal deve atualizar esta tabela diretamente no disco a cada micro-passo concluído (`[ ]` Pendente, `[~]` Em Execução, `[x]` Aprovado, `[!]` Reprovado pelo Auditor E — aguardando retrabalho).*

| ID | Módulo | Tarefa (Com Hotfixes do Playtest + Otimização VR) | `[A]` Spec & Scout | `[B]` Blender 3D & Audio | `[C]` C# Gameplay Code | `[D]` Scene Layout & Purge `(0,0,0)` | `[E]` Visual & VR QA Audit | Status Final |
| :--- | :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **TASK-01** | Core / Purge | Infraestrutura Multissensorial + `SceneDuplicateAndOriginPurger` + `VisualQACapture` | `[x]` | `[x]` | `[x]` | `[x]` | `[x]` | `COMPLETED` |
| **TASK-02** | Core / XRI | Física de Arremesso por Balanço do Laser (`LaserSwingThrow`) + Force Grab Ergonômico | `[x]` | `[x]` | `[x]` | `[x]` | `[x]` | `COMPLETED` |
| **TASK-03** | Drone Pet | Drone Companheiro: Puxar/Afastar no Laser/Scroll + Distância Relativa + Zero Colisão Player + Sem Textos `-/+` + Sem Pegar Maçãs | `[x]` | `[x]` | `[x]` | `[x]` | `[x]` | `COMPLETED` |
| **TASK-04** | Drone Pet | Reatividade Afetiva (Carinho/Poke, Expressões LED e Dança Otimizada com o Rádio) | `[x]` | `[x]` | `[x]` | `[x]` | `[x]` | `COMPLETED` |
| **TASK-05** | Drone Pet | Celebração de Metas Clínicas (Pirueta, Confetes VR-Safe, Fanfarra e Frases) | `[x]` | `[x]` | `[x]` | `[x]` | `[x]` | `COMPLETED` |
| **TASK-06** | Menu Hub | Rádio Deck com 4 Fitas Cassete Magnéticas (`XRSocketInteractor`) + Eject Físico (Mesa 1) | `[x]` | `[x]` | `[x]` | `[x]` | `[x]` | `COMPLETED` |
| **TASK-07** | Menu Hub | Pistola/Dispenser de Balões Antigravidade (*The Lab*) + Física Leve (Mesa Auxiliar) | `[ ]` | `[ ]` | `[ ]` | `[ ]` | `[ ]` | `PENDING` |
| **TASK-08** | Menu Hub | Mini Basquete Arcade com Botão Spawner, Aro Duplo Sensor e Placar 3D | `[ ]` | `[ ]` | `[ ]` | `[ ]` | `[ ]` | `PENDING` |
| **TASK-09** | Menu Hub | Distribuição dos Fidgets nas Mesas/Estantes Existentes da Sala (Alavanca, Fogos, Bobblehead) | `[ ]` | `[ ]` | `[ ]` | `[ ]` | `[ ]` | `PENDING` |
| **TASK-10** | Menu Hub | Esfera Diorama 3D Puramente Decorativa (REMOÇÃO TOTAL de Teleporte por Proximidade; Navegação via Canvas) | `[ ]` | `[ ]` | `[ ]` | `[ ]` | `[ ]` | `PENDING` |
| **TASK-11** | Floresta/Rio | Maçãs Pegáveis/Jogáveis no Laser + Árvores Sacudíveis + Cestas + Limite de Mapa Anti-Queda | `[ ]` | `[ ]` | `[ ]` | `[ ]` | `[ ]` | `PENDING` |
| **TASK-12** | Floresta/Rio | Sementes Mágicas e Pinhões Arremessáveis Dentro do Mapa (Brotação Elástica de Flores) | `[ ]` | `[ ]` | `[ ]` | `[ ]` | `[ ]` | `PENDING` |
| **TASK-13** | Floresta/Rio | Pássaro Silencioso/Suave com Cooldown e Pernas Visíveis + Cogumelos Musicais Distribuídos sem Duplicatas | `[ ]` | `[ ]` | `[ ]` | `[ ]` | `[ ]` | `PENDING` |
| **TASK-14** | Gamificação | Trilha de Moedas/Orbes Flutuantes Otimizada (`sqrMagnitude`) para Exoesqueleto | `[ ]` | `[ ]` | `[ ]` | `[ ]` | `[ ]` | `PENDING` |
| **TASK-15** | Gamificação | Missões no Visor do Drone + Totens de Checkpoint Aterrados com Alavanca e Baú | `[ ]` | `[ ]` | `[ ]` | `[ ]` | `[ ]` | `PENDING` |
| **TASK-16** | Gamificação | Persistência (`JSON Save`) + Troféus nas Estantes Existentes do Menu + Varredura Geral `(0,0,0)` | `[ ]` | `[ ]` | `[ ]` | `[ ]` | `[ ]` | `PENDING` |

> **Notas da Auditoria Inicial (Claude Code, 2026-11-11):**
> * TASK-01..05 aprovadas (`AuditVerdict.md` PASS). TASK-06: `[E]` deu FAIL -> ordens: `RE-RUN [C]` (reduzir `ejectImpulseForce` 0.55 -> 0.15) e `RE-RUN [D]` (deck atravessa o rádio ~8 cm; bola/fidgets/troféus fora da Mesa 1; `pairwiseClearances[]` no JSON; refazer 4 capturas).
> * TASK-07..16 têm código/specs/prints da rodada ANTIGA (10/11) e NÃO foram refeitos com os hotfixes: `PortalWorldSphere.cs` ainda tem `LoadScene` (TASK-10); `DroneFetchController/Target` ainda existem em `Task03/` (garantir que nunca sejam acoplados a maçãs, TASK-11); itens da Mesa 1 (bola, fogos, troféus) precisam ser realocados em TASK-07/08/09/16.
> * **TASK-06 COMPLETED (PASS do [E] em 2026-11-11).** Pausa programada pelo usuário: a retomada futura deve começar pela **`TASK-07.A`** (primeira célula `[ ]`). Ressalvas herdadas: clamp `maxEjectSpeed` em `EjectRoutine` lê velocidade antes do passo de física (código morto, não bloqueante); itens realocados do Menu (Bobblehead 0.076 m e outros ~0.062-0.066 m) devem ser revalidados com folga >= 0.08 m pelas TASKs 07/08/09/16; textos TMP `Label_*` dos troféus gigantes (TASK-16); collider do `ArcadeBallSpawner` 0.058 m abaixo da malha (TASK-08); validar balão demo com a regra do pool (TASK-07); `Floresta_e_Rio.unity` teve instâncias removidas na limpeza manual (ex.: `[TASK-15] CheckpointRewardTotem`, `Apple_In_Basket`, `EnergyStarCoin_*`) — o `[A]` das TASKs 11+ deve confirmar se algo essencial foi apagado por engano.

---

## 5. Especificações Profundas de Engenharia, Arte 3D, Hotfixes e Auditoria por Tarefa

### FASE 1: Saneamento de Cenas, Limpeza de Duplicatas `(0,0,0)` e Física de Arremesso no Laser

#### `TASK-01`: Infraestrutura Multissensorial + Expurgador de Duplicatas `(0,0,0)` + Utilitário de Captura Visual
* **`[A]` Spec & Scout:**
  * Inspecionar imediatamente a cena do `Menu Principal` e a cena `Floresta e Rio`.
  * **Urgência #1 no Scout:** Localizar qualquer script da rodada anterior que esteja disparando `SceneManager.LoadScene` sozinho no Menu Principal (para desativá-lo já na largada) e listar todos os objetos duplicados ou amontoados na coordenada `(0, 0, 0)` de ambas as cenas.
* **`[B]` Artista (Áudio & Shaders):**
  * Verificar/gerar os 6 arquivos `.wav` base em `Assets/Audio/Generated/Core/` com volume normalizado suave (`-8 dB`).
* **`[C]` Engenheiro C#:**
  * `MaterialShaderHelper.cs`: Configura shaders compatíveis com URP/Standard com `enableInstancing = true` para performance VR.
  * `VRFeedbackManager.cs` & `InteractableHighlightFeedback.cs`: Pool de áudio 3D sem alocação de memória (`GC Alloc = 0`) e highlight via `MaterialPropertyBlock`.
* **`[D]` Scene Layout, Expurgador de `(0,0,0)` & Visual Capture:**
  * Criar `SceneDuplicateAndOriginPurger.cs` em `Assets/Editor/AutoSetup/`:
    * Varre a cena aberta buscando objetos com nomes repetidos (`(Clone)`, `(1)`, `(2)` ou múltiplos grupos idênticos gerados por scripts anteriores) e remove as duplicatas.
    * Inspeciona todos os objetos dentro de um raio de $1.0\text{m}$ da origem `(0, 0, 0)`. Se o objeto for uma cópia desnecessária de um item interativo que já está devidamente posicionado na sala ou na trilha, **remove imediatamente a cópia do `(0, 0, 0)`**.
  * Criar/atualizar `VisualQACaptureEditor.cs` e `SpatialAuditUtility.cs` para incluir no `TASK-XX_SpatialAudit.json` o campo `"objectsAtOriginZero": []` e `"duplicateObjectsFound": []`.
* **`[E]` Auditoria Visual e de Cena:**
  * Confirmar que `(0, 0, 0)` está limpo de lixo/duplicatas e que os prints de diagnóstico das duas cenas foram gerados.

#### `TASK-02`: Física Real de Pegar e Arremessar Balançando o Laser (`LaserSwingThrowInteractable`) + Ergonomia
* **`[A]` Spec & Scout:**
  * Diagnosticar por que as maçãs e arremessáveis não estavam sendo pegas ou jogadas com o `XRRayInteractor`. Mapear a configuração exata do `XRRayInteractor` nas mãos do jogador nas cenas `Menu Principal` e `Floresta e Rio`.
* **`[B]` Artista (Blender MCP):**
  * Verificar/modelar `ReticleTargetRing.fbx` ($< 250\text{ tris}$, pivô na base $Y = 0$, material ciano instanciado).
* **`[C]` Engenheiro C# (Solução Definitiva de Arremesso por Laser):**
  * Criar `LaserSwingThrowInteractable.cs` (acoplado a todas as maçãs, bolas de basquete, pinhões e objetos jogáveis):
    1. **Garantia de Pegada no Laser (`Ray Grab Fix`):** No `Awake()`/`OnValidate()`, garante que o `XRGrabInteractable` tenha `interactionLayers = ~0` (*Everything*), `movementType = XRBaseInteractable.MovementType.Instantaneous` (ou `VelocityTracking` suave enquanto segurado pelo raio), `throwOnDetach = false` (pois calcularemos a física real customizada no momento de soltar!) e um `SphereCollider`/`BoxCollider` sólido com `isTrigger = false`.
    2. **Rastreador de Velocidade Espacial na Ponta do Laser (*World-Space Ring Buffer*):**
       * Quando o jogador segura um objeto à distância com o laser (ou puxa com *Force Grab*) e **balança o feixe do laser para os lados/frente para arremessar**, o script grava em um array circular fixo de 12 posições (`Vector3[12] posBuffer`, `float[12] timeBuffer` — zero GC!) a posição global exata do objeto (`transform.position`) a cada frame.
       * No exato instante em que o jogador solta o botão do controle (`OnSelectExited`), o script restaura `rigidbody.isKinematic = false`, `rigidbody.useGravity = true` e calcula o vetor de velocidade média ponderada dos últimos $0.08\text{s}$ do movimento do objeto na ponta do laser:
         $$\vec{v}_{\text{throw}} = \left(\frac{\vec{p}_{\text{now}} - \vec{p}_{\text{window}}}{t_{\text{now}} - t_{\text{window}}}\right) \times \text{throwBoostMultiplier}$$
       * Aplica $\vec{v}_{\text{throw}}$ diretamente ao `rigidbody.velocity` (com limite de segurança máximo de $18\text{ m/s}$) e adiciona rotação física (`rigidbody.angularVelocity`), fazendo o objeto **voar exatamente na direção e velocidade em que o jogador balançou o laser**, além de aplicar assistência sutil (`ThrowAimAssist` de $18^\circ$) se houver uma cesta ou aro na direção do lançamento!
* **`[D]` Scene Layout & Prints:**
  * Configurar os `XRRayInteractor` do player (`enableUIInteraction = true`, `hitDetectionType = SphereCast`, `sphereCastRadius = 0.06f`, `raycastTriggerInteraction = Ignore` para que triggers invisíveis na cena nunca bloqueiem o clique do laser nos objetos físicos!).
* **`[E]` Auditoria Visual e Funcional:**
  * Validar que nenhum colisor de trigger na cena bloqueia o raio laser de alcançar os objetos interativos.

---

### FASE 2: Novo Comportamento do Drone Companheiro (Arraste Livre, Distância Relativa e Zero Colisão)

#### `TASK-03`: Drone Companheiro com Arraste por Laser/Scroll, Distância Relativa Dinâmica e Zero Colisão com o Player (`DroneCompanionAnchorSystem`)
* **`[A]` Spec & Scout:**
  * Inspecionar o GameObject do Drone nas cenas `Menu Principal` e `Floresta e Rio`.
  * Localizar os textos/objetos de UI embaixo do Drone que dizem `- perto` / `+ longe` (para remoção) e localizar onde a colisão física do Drone estava empurrando o jogador e onde ele estava indo buscar maçãs.
* **`[B]` Artista (Blender MCP & Audio):**
  * Manter os detalhes visuais do Drone leves para VR ($< 1.500\text{ tris}$) e sons discretos de confirmação quando o jogador reposiciona o Drone (`drone_anchor_lock.wav`).
* **`[C]` Engenheiro C# (Implementação Exata Solicitada pelo Usuário):**
  * **1. Proibição Total de Buscar Maçãs:** Remover/desativar qualquer lógica que faça o Drone perseguir maçãs ou itens caídos. O Drone **nunca** sai para pegar maçãs e **nunca** desce abaixo do terreno.
  * **2. Remoção das Mensagens `- perto` / `+ longe`:** Desativar e remover da hierarquia do Drone qualquer texto ou botão inferior de `- perto` e `+ longe`.
  * **3. Zero Colisão Física Empurrando o Jogador:**
    * Configurar o `Rigidbody` do Drone como `isKinematic = true` e `useGravity = false`.
    * No `Awake()`, localizar o `CharacterController` e todos os `Collider` do `XROrigin` / `Player` e executar `Physics.IgnoreCollision(droneCollider, playerCollider, true)` para todos eles. Assim, o Drone possui apenas um colisor para ser segurado pelo raio laser do controle, mas **jamais empurra ou colide com o corpo do jogador**!
  * **4. Mecânica de Início na Frente + Arrastar com Laser/Scroll + Seguir Mantendo a Distância Escolhida (`DroneCompanionFollowAndGrab.cs`):**
    * **Estado Inicial (`WaitingInitialRead`):** Quando a cena começa, o Drone inicia estacionado suavemente na frente do jogador (ex: a $1.6\text{m}$ à frente, na altura dos olhos/peito) para que o jogador possa chegar perto e ler as informações do painel com calma.
    * **Segurar e Arrastar com o Laser + Scroll (`BeingManipulatedByPlayer`):** O Drone possui um `XRGrabInteractable` configurado para o `XRRayInteractor` (com suporte nativo ao **Scroll/Thumbstick do controle para puxar para mais perto ou empurrar para mais longe ao longo do raio**, além de arrastar para os lados/altura).
    * **Ancoragem e Seguimento com Distância Relativa Constante (`FollowingPlayerAtCustomOffset`):**
      * Assim que o jogador posiciona o Drone onde quiser (ou começa a caminhar) e solta o botão de segurar (`OnSelectExited`), o Drone calcula e salva o **vetor de offset/distância exato em relação ao jogador** naquele instante:
        $$\vec{\Delta}_{\text{custom}} = \vec{P}_{\text{drone}} - \vec{P}_{\text{player}} \quad (\text{mantendo a distância escalar } d = \|\vec{\Delta}_{\text{custom}}\|)$$
      * Dali em diante, conforme o jogador anda pelo mapa, o Drone segue o jogador suavemente (`Vector3.SmoothDamp` sem GC Alloc) **mantendo sempre exatamente aquela posição/distância relativa que o jogador escolheu**, olhando suavemente para o jogador para que a tela de informações esteja sempre legível.
      * A qualquer momento do jogo, o jogador pode segurar o Drone novamente com o laser e usar o scroll/movimento para puxá-lo para mais perto ou afastá-lo para mais longe, atualizando instantaneamente a nova distância mantida!
* **`[D]` Scene Layout & Prints:**
  * Aplicar a configuração no Drone de ambas as cenas (`Menu Principal` e `Floresta e Rio`), remover qualquer texto `- perto / + longe` residual, rodar o `SceneDuplicateAndOriginPurger` e capturar 3 prints do Drone posicionado na frente do player sem os textos inferiores.
* **`[E]` Auditoria Visual e de Cena:**
  * Verificar nos prints que os textos `- perto` / `+ longe` sumiram completamente, que o painel principal de informações do paciente está intacto e legível, e que não há duplicatas do Drone na cena.

#### `TASK-04`: Reatividade Afetiva do Drone (Carinho, Cutucada e Dança Otimizada com o Rádio) (`DroneAffectiveSystem`)
* **`[A]` Spec & Scout:**
  * Verificar a integração das expressões faciais em LED do Drone com o novo `DroneCompanionFollowAndGrab`.
* **`[B]` Artista (Texturas & Audio):**
  * Garantir que o atlas de expressões LED (`^‿^`, `♥‿♥`, `@_@`, `♪‿♪`) use uma única textura leve e sons suaves (`volume <= 0.25f`).
* **`[C]` Engenheiro C#:**
  * `DroneAffectiveSystem.cs`: Reação de carinho e dança sutil no ritmo do rádio usando `MaterialPropertyBlock` / troca de UV sem instanciar materiais e sem alterar a distância escolhida pelo jogador.
* **`[D]` Scene Layout & Prints:**
  * Capturar print frontal do visor LED do Drone.
* **`[E]` Auditoria Visual:**
  * Confirmar alinhamento perfeito dos olhos LED sem *Z-fighting*.

#### `TASK-05`: Comemoração de Metas Clínicas (`DroneMilestoneCelebration`)
* **`[A]` Spec & Scout:**
  * Mapear os gatilhos de celebração (passos, cestas de maçã, totens) mantendo o Drone na distância segura escolhida pelo jogador.
* **`[B]` Artista (Gemini API & Audio):**
  * Validar `DroneEncouragementPhrases.json` e som de fanfarra suave.
* **`[C]` Engenheiro C#:**
  * `DroneMilestoneCelebration.cs`: Faz o giro comemorativo no próprio eixo local (sem sair do lugar e sem avançar em cima do jogador!) e solta partículas leves de confete (`maxParticles = 40`, sem colisão física).
* **`[D]` Scene Layout & Prints:**
  * Capturar print da celebração ativa.
* **`[E]` Auditoria Visual:**
  * Verificar legibilidade e garantir que o balão não tampa os dados clínicos principais.

---

### FASE 3: Sala do Menu Principal (Distribuição pelas Várias Mesas/Estantes Existentes e Diorama Sem Teleporte)

#### `TASK-06`: Rádio Deck com 4 Fitas Cassete Magnéticas + Eject Físico (`RadioCassetteDeckSystem`)
* **`[A]` Spec & Scout:**
  * Localizar a mesa onde está o rádio original na sala do Menu Principal e verificar se há fitas ou rádios duplicados da rodada anterior.
* **`[B]` Artista (Blender MCP & Audio):**
  * `RetroCassetteTape.fbx` ($< 400\text{ tris}$, com `Reel_Left` e `Reel_Right`) e `CassetteDeckSlot.fbx` com botão físico `Btn_Eject`.
* **`[C]` Engenheiro C#:**
  * `MusicCassetteTape.cs` & `RadioCassetteSocket.cs`: Fitas pegáveis com o laser (`LaserSwingThrowInteractable`), encaixe magnético via `XRSocketInteractor` que troca a música do rádio e botão físico de Eject que lança a fita suavemente de volta para a superfície da mesa.
* **`[D]` Scene Layout & Purge `(0,0,0)`:**
  * Posicionar o deck ao lado do rádio existente e alinhar exatamente 4 fitas cassete (uma de cada cor/música, **zero duplicatas**) sobre a mesa do rádio com $0.08\text{m}$ de folga entre elas. Executar o `SceneDuplicateAndOriginPurger` para deletar qualquer fita extra no `(0,0,0)`.
* **`[E]` Auditoria Visual:**
  * Confirmar no print que existem exatamente 4 fitas alinhadas na mesa do rádio e nenhuma duplicata espalhada.

#### `TASK-07`: Pistola/Dispenser de Balões Antigravidade (*The Lab Style*) em Mesa/Estante Própria (`BalloonGunAndFloatSystem`)
* **`[A]` Spec & Scout:**
  * Escolher uma **mesa lateral ou bancada secundária já existente na sala do Menu** para colocar a estação de balões (longe do rádio e longe dos fidgets!).
* **`[B]` Artista (Blender MCP & Audio):**
  * `BalloonGunDispenser.fbx`, `BalloonGunStand.fbx` e `InflatableBalloon.fbx` ($< 500\text{ tris}$, shader com GPU Instancing).
* **`[C]` Engenheiro C#:**
  * `BalloonInflaterGun.cs` & `FloatingBalloon.cs`: Infla balões ao segurar o gatilho com o controle; limite máximo de **8 balões simultâneos em Object Pool** (se o jogador inflar o 9º, o mais antigo estoura suavemente, garantindo performance VR infinita!). Estoura ao levar tiro do raio laser.
* **`[D]` Scene Layout & Purge `(0,0,0)`:**
  * Posicionar o suporte e a pistola na mesa/bancada secundária existente, limpar duplicatas no `(0,0,0)` e tirar 3 prints.
* **`[E]` Auditoria Visual:**
  * Confirmar que a pistola está em sua própria área livre, sem sobrepor nenhum fidget ou rádio.

#### `TASK-08`: Mini Basquete Arcade com Botão Spawner e Aro Duplo Sensor (`ArcadeHoopMinigame`)
* **`[A]` Spec & Scout:**
  * Localizar uma parede livre na sala do Menu Principal e uma mesa/bancada existente em frente a ela para o botão spawner de bolas.
* **`[B]` Artista (Blender MCP & Audio):**
  * `ArcadeBasketballHoop.fbx`, `MiniBasketball.fbx` e `ArcadeSpawnButton.fbx`.
* **`[C]` Engenheiro C#:**
  * `BasketballHoopScorer.cs` & `ArcadeBallSpawner.cs`: As bolas de basquete **DEVEM** usar o novo `LaserSwingThrowInteractable` (`TASK-02`) para que o jogador possa segurar a bola com o laser, balançar o controle/laser e arremessar na cesta com física realista! Pool limitado a 3 bolas ativas.
* **`[D]` Scene Layout & Purge `(0,0,0)`:**
  * Instalar a tabela na parede livre e o botão spawner na mesa correspondente, remover bolas/tabelas duplicadas no `(0,0,0)` e tirar prints.
* **`[E]` Auditoria Visual:**
  * Confirmar placar legível, aro desobstruído e zero duplicatas.

#### `TASK-09`: Espalhamento dos Fidgets nas Várias Mesas e Estantes Existentes da Sala (`DesktopFidgetToys` Distributed Layout)
* **`[A]` Spec & Scout:**
  * **Correção Crítica do Playtest:** Na versão anterior, todos os fidgets foram colocados espremidos em uma única mesa. O Scout deve mapear **todas as outras mesas, bancadas e estantes (`Shelves`/`Tables`) que já existem na sala do Menu Principal** para espalhar os brinquedos pela sala!
* **`[B]` Artista (Blender MCP & Audio):**
  * Verificar/refinar `EnvironmentControlLever.fbx` (Alavanca de Clima), `FireworksLaunchButton.fbx` (Botão de Fogos) e `ExoMascotBobblehead.fbx` (Boneco Cabeçudo com mola).
* **`[C]` Engenheiro C#:**
  * `AtmosphereLeverInteractable.cs`, `FireworksButtonInteractable.cs` e `BobbleheadSpringToy.cs`: Garantir funcionamento perfeito tanto clicando de longe com o `XRRayInteractor` quanto tocando de perto, com zero impacto de performance (fogos de artifício sem luzes de sombra dinâmica e com pool de partículas).
* **`[D]` Scene Layout (Distribuição Multi-Móveis) & Purge `(0,0,0)`:**
  * **Proibido deixar tudo na mesma mesa!** Distribuir os itens usando as diferentes mesas e estantes reais já presentes na sala do Menu Principal (sem precisar empilhar móveis estranhos se a sala já tem várias mesas e estantes):
    * Colocar a **Alavanca de Clima** e o **Botão de Fogos** bem espaçados ($> 0.45\text{m}$ de distância) em uma mesa lateral livre existente;
    * Colocar o **Mascote Bobblehead** em destaque em outra mesa ou prateleira/estante acessível;
    * Verificar se havia objetos sobrepostos da rodada anterior, **deletar todas as cópias sobrepostas e tudo o que estiver largado no `(0, 0, 0)`**, e gerar prints de cada móvel mostrando o espaçamento amplo.
* **`[E]` Auditoria Visual Implacável:**
  * Inspecionar os prints da sala do Menu: se os fidgets ainda estiverem amontoados em uma mesa só ou se houver qualquer sobreposição/duplicata, emitir `FAIL -> RE-RUN [D]` imediatamente!

#### `TASK-10`: Esfera de Diorama 3D Puramente Decorativa — REMOÇÃO TOTAL DO TELEPORTE AUTOMÁTICO (`DioramaDisplayOrb`)
* **`[A]` Spec & Scout:**
  * **Correção Crítica #1 do Playtest:** Identificar o script `PortalWorldSphere.cs` / `WorldPortalSphereSelector` que estava teletransportando o jogador automaticamente para a cena `Floresta e Rio` assim que dava Play no Menu Principal.
* **`[B]` Artista (Blender MCP):**
  * Manter o belo modelo `PortalOrbDiorama_Forest.fbx` (esfera transparente com mini pinheiros, rio e maçãs dentro) e sua base `PortalPedestalSocket.fbx`.
* **`[C]` Engenheiro C# (Desativação Total de Teleporte por Esfera/Proximidade):**
  * Reescrever `PortalWorldSphere.cs` como um **Diorama 3D Interativo Puro (`InteractiveDioramaOrb.cs`)**:
    * **REMOVER TODAS as chamadas a `SceneManager.LoadScene` e `SceneManager.LoadSceneAsync` deste script e do pedestal!**
    * **REMOVER qualquer checagem de distância para a câmera (`Vector3.Distance(transform.position, cam.position)`) que causava o teleporte por proximidade!**
    * Quando o jogador pega a esfera com o laser ou a coloca no pedestal, ela gira a mini-floresta interna, acende partículas mágicas suaves dentro do globo de vidro e toca uma melodia tranquila da floresta, servindo como uma peça de exibição interativa na sala.
    * Garantir que a transição real de cenas para os mapas ocorra **100% pelo Canvas 2D original do Menu Principal**, que deve permanecer totalmente funcional e desobstruído.
* **`[D]` Scene Layout & Teste de Play Mode:**
  * Posicionar o pedestal e o diorama em uma estante ou canto livre de uma mesa, remover qualquer esfera duplicada no `(0, 0, 0)` e verificar que entrar em Play Mode na cena do Menu Principal **mantém o jogador na sala do Menu Principal sem teletransportar sozinho**!
* **`[E]` Auditoria Visual e Funcional:**
  * Confirmar no código (`grep` por `LoadScene` nos scripts gerados) que **nenhum script novo chama `LoadScene`**, garantindo que apenas o Canvas original do projeto troque de fase.

---

### FASE 4: Cena da Floresta e Rio (Maçãs Jogáveis no Laser, Limite de Mapa, Pássaro Suave e Cogumelos Distribuídos)

#### `TASK-11`: Maçãs Pegáveis/Jogáveis no Laser + Árvores Sacudíveis + Cestas + Proteção Contra Itens Fora do Mapa (`ForestAppleInteractionSystem`)
* **`[A]` Spec & Scout:**
  * Abrir a cena `Floresta e Rio` (e verificar se há outra cena de floresta).
  * **Diagnóstico Crítico do Playtest:**
    1. Verificar todas as maçãs que estavam fora do mapa (fazendo o drone antigo ir para debaixo do chão) e todas as duplicatas no `(0, 0, 0)`.
    2. Verificar por que as maçãs não estavam sendo seguradas e arremessadas com o laser.
    3. Verificar se alguma cesta ou árvore interativa foi apagada durante a limpeza manual de duplicatas para restaurar apenas as instâncias corretas.
* **`[B]` Artista (Blender MCP & Audio):**
  * `InteractiveRedApple.fbx` ($< 320\text{ tris}$, pivô no centro para rolar/voar com física natural!), `GoldenBonusApple.fbx` e `WickerCollectionBasket.fbx` ($< 900\text{ tris}$, cavidade aberta).
* **`[C]` Engenheiro C# (Física Real de Maçã no Laser + Anti-Out-of-Bounds):**
  * `InteractiveApple.cs`:
    * **NÃO adicionar `DroneFetchTarget` nas maçãs!** O Drone nunca deve pegar maçãs.
    * Acoplar obrigatoriamente o `LaserSwingThrowInteractable` (`TASK-02`) + `XRGrabInteractable` (`useForceGrab = true`, `interactionLayers = ~0`, `movementType = Instantaneous` durante o grab para seguir o laser sem atraso, e aplicando a velocidade tangencial do balanço do laser ao `Rigidbody` quando solta!).
    * Assim, quando o jogador aponta o laser para uma maçã no chão, segura com o botão do controle, dá um impulso balançando o laser e solta, **a maçã voa fisicamente na exata direção e velocidade em que foi lançada**, quicando no chão ou caindo dentro das cestas!
    * **Proteção Anti-Debaixo do Mapa (`OutOfBoundsSafetyGuard`):** Se a maçã cair abaixo do terreno (`transform.position.y < startTerrainY - 1.5f`) ou sair dos limites da trilha, ela reseta imediatamente para sua posição segura na grama ao lado da trilha e entra em `rigidbody.Sleep()`.
  * `ShakeableFruitTree.cs` & `AppleCollectionBasket.cs`:
    * Sacudir árvores com o laser derruba maçãs apenas **dentro do raio seguro da trilha** (com limite de pool de 12 maçãs na cena para zero peso em VR).
* **`[D]` Scene Layout, Expurgo de Fora do Mapa / `(0,0,0)` & Aterramento:**
  * **Limpeza Obrigatória na Cena `Floresta e Rio`:**
    1. Deletar imediatamente todas as maçãs, cestas ou objetos que estejam **fora do limites do terreno/mapa** ou na coordenada `(0, 0, 0)`.
    2. Executar o `ForestGlobalGroundingFixer` para aterrar todas as árvores e posicionar grupos limpos de maçãs pegáveis e cestas de vime **estritamente nas margens visíveis e acessíveis da trilha onde o jogador caminha** (validadas por `Physics.Raycast` contra o chão real da trilha).
    3. Capturar 3 prints da trilha mostrando as maçãs e cestas perfeitamente apoiadas no chão.
* **`[E]` Auditoria Visual e Espacial:**
  * Confirmar no `SpatialAudit.json` e nos prints que **0 maçãs estão fora do mapa**, **0 objetos estão no `(0,0,0)`**, todas as maçãs têm `LaserSwingThrowInteractable` ativo e nenhuma árvore flutua.

#### `TASK-12`: Sementes Mágicas e Pinhões Arremessáveis com Física de Laser (`MagicSeedThrowables`)
* **`[A]` Spec & Scout:**
  * Verificar os pinhões (`GlowingPineconeSeed`) na cena `Floresta e Rio`, limpando duplicatas no `(0,0,0)` ou itens fora do mapa.
* **`[B]` Artista (Blender MCP & Audio):**
  * `GlowingPineconeSeed.fbx`, `SproutingFlowerPatch.fbx` e `BouncyMushroomCluster.fbx` ($< 600\text{ tris}$ cada, GPU Instancing ativo).
* **`[C]` Engenheiro C#:**
  * `MagicSeedPinecone.cs`: Usa a mesma física de arremesso por balanço do laser (`LaserSwingThrowInteractable`) das maçãs! Quando o jogador pega um pinhão com o laser, balança e joga no chão, faz brotar um canteiro de flores (limitado a um *Object Pool* de 10 canteiros para nunca pesar em VR).
* **`[D]` Scene Layout & Purge `(0,0,0)`:**
  * Posicionar os pinhões apenas nas bordas válidas da trilha, remover duplicatas no `(0,0,0)` e tirar prints.
* **`[E]` Auditoria Visual:**
  * Confirmar aterramento perfeito e ausência de duplicatas.

#### `TASK-13`: Pássaro Silencioso/Suave com Pernas Visíveis + Distribuição Limpa dos Cogumelos Musicais e Lanternas (`ForestReactiveEnvironment`)
* **`[A]` Spec & Scout:**
  * **Diagnóstico Crítico do Playtest:**
    1. O pássaro na cena `Floresta e Rio` ficava gritando sem parar e extremamente alto quando o jogador chegava perto, além de precisar ter as duas pernas visíveis.
    2. Os **Cogumelos Musicais funcionam muito bem e são muito divertidos**, mas havia duplicatas desnecessárias espalhadas e objetos no `(0, 0, 0)`. O usuário apagou duplicatas manualmente, então o Scout deve verificar a distribuição atual dos cogumelos ao longo da trilha para garantir que estejam completos, sem sobreposição e sem cópias no `(0, 0, 0)`.
* **`[B]` Artista (Blender MCP & Áudio Suave Calibrado):**
  * Modelar/validar `LowPolyForestBird.fbx` com **`Leg_Left` e `Leg_Right` sólidos e visíveis** apoiados em $Y = 0$.
  * **Refazer `forest_bird_chirp.wav`:** Gerar um som de passarinho **curto ($0.45\text{s}$), suave, melodioso, em frequência média-baixa agradável ($1.200\text{ Hz}$ a $2.200\text{ Hz}$) e com amplitude baixa (`peak = -12 dB`)**, eliminando qualquer timbre agudo ou estridente!
  * Preservar intactos os 5 áudios pentatônicos dos cogumelos musicais que o usuário aprovou.
* **`[C]` Engenheiro C# (Correção Definitiva do Grito do Pássaro):**
  * `ReactiveForestBird.cs`:
    * Configurar no código: `audioSource.loop = false;`, `audioSource.playOnAwake = false;`, `audioSource.volume = 0.16f;` (volume baixo e confortável!), `audioSource.spatialBlend = 1.0f;`.
    * **Trava Anti-Spam de Áudio (`Strict Cooldown & Single Trigger`):** Quando o jogador chega perto ou aponta o laser, o pássaro verifica `if (Time.time < lastChirpTime + 15.0f || isFlying) return;`. Ele atualiza `lastChirpTime = Time.time`, toca `PlayOneShot` **uma única vez** com volume `0.16f` e inicia imediatamente o voo suave em curva Bezier para uma árvore mais adiante na trilha! **Ele NUNCA fica parado gritando repetidamente perto do jogador.**
  * `MusicalMushroom.cs` & `PathLanternInteractable.cs`:
    * Manter a mecânica divertida dos cogumelos musicais intacta; garantir que as lanternas usem luzes sem sombra dinâmica (`LightShadows.None`) para máxima performance em VR.
* **`[D]` Scene Layout, Distribuição na Trilha & Purge `(0,0,0)`:**
  * Na cena `Floresta e Rio`:
    1. Executar o `SceneDuplicateAndOriginPurger` para remover todos os cogumelos, pássaros ou lanternas duplicados que estavam no `(0, 0, 0)` ou sobrepostos no mesmo lugar.
    2. Distribuir os grupos de **Cogumelos Musicais** de forma limpa, harmônica e bem espaçada ao longo de toda a trilha de caminhada (ex: 3 ou 4 estações musicais de 5 cogumelos cada, perfeitamente aterradas nas curvas da trilha e do rio).
    3. Posicionar os pássaros e lanternas em pontos únicos e aterrados ao longo do caminho, tirando o close-up das pernas do pássaro e o print das estações de cogumelos.
* **`[E]` Auditoria Visual e de Áudio:**
  * Verificar no código de `ReactiveForestBird.cs` que `loop == false`, `volume <= 0.20f` e `cooldown >= 15f`. Verificar nos prints que as pernas do pássaro estão visíveis, que os cogumelos musicais estão bem distribuídos ao longo do mapa e que o `(0, 0, 0)` está 100% vazio.

---

### FASE 5: Gamificação Leve para VR, Missões, Totens e Troféus nas Estantes do Menu

#### `TASK-14`: Trilha de Moedas/Orbes Flutuantes Otimizada para VR (`AdaptiveCoinTrailSystem`)
* **`[A]` Spec & Scout:**
  * Verificar a trilha de moedas na cena `Floresta e Rio`, limpando moedas fora do mapa ou duplicadas no `(0,0,0)`.
* **`[B]` Artista (Blender MCP & Audio):**
  * `EnergyStarCoin.fbx` ($< 280\text{ tris}$, material com GPU Instancing).
* **`[C]` Engenheiro C# (Otimização de Rotação Unificada):**
  * Em vez de 30 moedas rodando `Update()` individualmente, o `CoinTrailSpawner.cs` / `CoinManager` gira todas as moedas visíveis em um único loop centralizado e checa a distância ao jogador usando `sqrMagnitude`!
* **`[D]` Scene Layout & Purge `(0,0,0)`:**
  * Distribuir uma única fileira limpa de moedas acompanhando o caminho real da trilha na cena `Floresta e Rio`, sem duplicatas e sem moedas fora do mapa.
* **`[E]` Auditoria Visual:**
  * Confirmar alinhamento da trilha e zero objetos em `(0,0,0)`.

#### `TASK-15`: Checklist de Missões no Visor do Drone + Totens de Checkpoint Aterrados (`SessionQuestAndCheckpointManager`)
* **`[A]` Spec & Scout:**
  * Verificar os Totens de Checkpoint na trilha da `Floresta e Rio` e o painel de missões acoplado ao Drone.
* **`[B]` Artista (Blender MCP & Audio):**
  * `CheckpointRewardTotem.fbx` ($< 1.200\text{ tris}$) com alavanca lateral e tampa de baú articulada.
* **`[C]` Engenheiro C#:**
  * `SessionQuestManager.cs` & `InteractiveCheckpointTotem.cs`: Conectar a coleta de maçãs nas cestas, cogumelos tocados, lanternas acesas e moedas ao progresso das missões, garantindo que a UI no Drone não recrie strings todo frame (atualiza apenas quando o contador muda!).
* **`[D]` Scene Layout & Purge `(0,0,0)`:**
  * Posicionar os Totens aterrados nas margens da trilha da `Floresta e Rio`, remover qualquer totem duplicado no `(0,0,0)` e tirar prints.
* **`[E]` Auditoria Visual:**
  * Confirmar totens aterrados e UI do Drone limpa (sem os textos antigos `- perto / + longe`).

#### `TASK-16`: Troféus nas Estantes Existentes do Menu + Varredura Global Final Anti-Duplicatas e Certificação VR (`TrophyRoomProgressionSystem`)
* **`[A]` Spec & Scout:**
  * Mapear as **estantes (`Shelves`) e prateleiras existentes na sala do Menu Principal** para colocar os troféus e fazer uma inspeção final completa nas cenas `Menu Principal` e `Floresta e Rio`.
* **`[B]` Artista (Blender MCP):**
  * `Trophy_GoldenApple.fbx`, `Trophy_LuminousBonsai.fbx`, `Trophy_StarCup.fbx` e `Trophy_DroneCrownHat.fbx` ($< 800\text{ tris}$ cada).
* **`[C]` Engenheiro C#:**
  * `RehabProgressionSaveSystem.cs` & `TrophyShelfManager.cs`: Persistência em JSON com `unlockAllForDemo = true` e encaixe da coroa na cabeça do Drone.
* **`[D]` Scene Layout nas Estantes & Varredura Global Final `(0,0,0)`:**
  * Posicionar os Troféus distribuídos nas **estantes existentes da sala do Menu Principal** (deixando as mesas livres e organizadas!).
  * Rodar a **Varredura Global Final (`SceneDuplicateAndOriginPurger`)** tanto na cena do `Menu Principal` quanto na cena `Floresta e Rio`, garantindo:
    1. **Zero objetos duplicados** em qualquer lugar das cenas;
    2. **Zero objetos esquecidos na coordenada `(0, 0, 0)`**;
    3. **Zero scripts chamando `LoadScene` fora do Canvas original do Menu**;
    4. **Todos os objetos necessários presentes, testados e funcionais**.
  * Gerar os prints panorâmicos finais das duas cenas e o relatório `FINAL_VR_PLAYTEST_AND_PERFORMANCE_CERTIFICATE.md`.
* **`[E]` Auditoria Visual e de Performance Global Final:**
  * Inspecionar rigorosamente todos os prints e o relatório JSON final, validando que todas as 10 correções do playtest do usuário e todas as metas de otimização VR foram 100% cumpridas.

---

## 6. Prompt de Disparo Otimizado para Gemini Flash High (Copie e Envie em um Chat Novo no Antigravity)

Abra uma **nova conversa limpa** no Antigravity, anexe este arquivo `.md` e envie exatamente o texto abaixo:

```text
Estou te enviando em anexo o plano mestre atualizado com as correções do meu playtest em VR e regras de otimização de performance (AGENTS_IMPLEMENTATION_PLAN.md). Uma primeira versão das tasks já existe no workspace, mas apresentou bugs específicos no meu teste prático que precisam ser corrigidos com rigor absoluto.

Execute obrigatoriamente este PASSO ZERO antes de qualquer coisa:
1. Salve INTEGRALMENTE o conteúdo do arquivo em anexo na raiz do workspace em `./AGENTS_IMPLEMENTATION_PLAN.md` e `./AGENTS.md`.
2. Garanta que as pastas `./.agents/specs/` e `./.agents/visual_qa/` existam.

REGRA CRÍTICA DE ORÇAMENTO DE OUTPUT (MODO MICRO-SUBAGENTES):
Como estamos usando o modelo Flash High, NUNCA peça para um único subagente fazer uma Task inteira de uma vez!
Atue como Orquestrador Mestre e execute a Matriz da Seção 4 CÉLULA POR CÉLULA, fazendo chamadas `invoke_subagent` SEPARADAS E SEQUENCIAIS para cada especialista (`[A]`, `[B]`, `[C]`, `[D]` e `[E]`):

Enquanto percorre da TASK-01 à TASK-16, garanta que os especialistas apliquem TODAS as 10 correções obrigatórias do meu playtest (Seção 2) e as otimizações de VR (Seção 3):
1. MENU SEM TELEPORTE AUTOMÁTICO (TASK-01 / TASK-10): Remova imediatamente qualquer `LoadScene` ou teleporte por proximidade das esferas de portal/diorama! Ao dar Play no Menu Principal, não posso ser teletransportado sozinho para a Floresta e Rio. Deixe apenas o diorama 3D decorativo; a ida para os mapas será feita exclusivamente pelo sistema de Canvas normal do projeto.
2. DRONE COMPANHEIRO (TASK-03):
   - Tire as mensagens `- perto` e `+ longe` de baixo do Drone.
   - Tire a colisão física do Drone com o Player (`Physics.IgnoreCollision` / Kinematic) para ele NUNCA empurrar o jogador.
   - O Drone NÃO deve pegar maçãs nunca, apenas acompanhar!
   - Ele deve começar na frente do player (para o player ler as infos dele), permitir que o player o segure com o laser do controle e o arraste/ajuste mais perto ou mais longe com o scroll/movimento, e a partir de onde foi solto ele deve passar a seguir o player mantendo sempre aquela exata distância escolhida.
3. MAÇÃS PEGÁVEIS E JOGÁVEIS BALANÇANDO O LASER (TASK-02 / TASK-11): As maçãs precisam ser pegáveis clicando com o laser e ter física real de arremesso (`LaserSwingThrowInteractable`): quando pego com o laser, dou velocidade balançando o laser e solto, ela deve voar fisicamente na direção em que foi solta. Corrija também itens fora do mapa na Floresta e Rio que caíam debaixo do chão.
4. PÁSSARO E COGUMELOS NA FLORESTA E RIO (TASK-13): O pássaro estava gritando sem parar e extremamente alto quando chega perto — tire o loop, baixe bastante o volume, coloque cooldown longo para dar apenas um pio curto e suave, e garanta as pernas visíveis. Mantenha os cogumelos musicais (que ficaram ótimos!), apenas distribuindo-os de forma limpa pelo mapa.
5. ESPALHAR NAS VÁRIAS MESAS/ESTANTES E LIMPAR DUPLICATAS NO `(0,0,0)` (GLOBAL / TASK-06 a 16): Em vez de espremer todos os fidgets em uma mesa só no Menu, espalhe os minigames, fidgets e troféus pelas várias mesas e estantes que já existem na sala. Em todas as tasks, verifique se algum item necessário foi apagado sem querer, elimine todas as duplicatas desnecessárias no Menu e na Floresta e Rio, e delete qualquer objeto duplicado largado na coordenada `(0, 0, 0)` da cena!

Atualize a tabela da Seção 4 em `./AGENTS_IMPLEMENTATION_PLAN.md` e `./AGENTS.md` a cada micro-célula concluída (`[x]`). Comece agora pelo Passo Zero e dispare imediatamente o Subagente `TASK-01.A`.
```
