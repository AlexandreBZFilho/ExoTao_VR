# TASK-16 (retoque) — Estante de Troféus: corrigir o "teleporte", os textos e deixar bonita e boa de jogar

> Enviar este arquivo ao Opus junto com `AGENTS.md`. Executar **uma micro-célula por vez** (`[A]`→`[E]`), atualizando a matriz da Seção 4 de `AGENTS.md` e `AGENTS_IMPLEMENTATION_PLAN.md` a cada célula aprovada. Modelo: `opus` para `[B]`, `[C]`, `[D]` e qualquer retrabalho; `sonnet` pode fazer `[A]` (só leitura) e a primeira passada de `[E]`.
>
> **Escopo:** só a estante de troféus (`TrophyDisplayStand`, os 4 troféus, textos, jogabilidade, visual). A varredura global final e o `FINAL_VR_PLAYTEST_AND_PERFORMANCE_CERTIFICATE.md` da TASK-16 continuam pendentes e só devem ser feitos depois das TASKs 07, 09 e 10 (não fazer aqui).

## 0. Regras operacionais (valem para todos os subagentes)

1. **PROIBIDO Computer Use** (mouse, teclado, captura de tela do desktop, `open_application`, `AppActivate`). O usuário usa o PC ao mesmo tempo. Visual QA **somente** offscreen: câmera virtual na Unity → `Camera.Render()` → `RenderTexture` → `.png`. Blender via Blender MCP ou `blender -b -P`. Repita esta regra em todo prompt de subagente.
2. O Unity Editor **já está aberto**. Nunca abrir segunda instância. Usar o `unity` CLI (`unity command script eval/run/recompile`, `--detach`, `unity job status/wait`). Nunca usar `--focus`. O editor fica lento sem foco (eval simples 15–35 s, domain reload 130–300 s): use `--detach` e espere o job. Nunca usar `EditorUtility.CopySerialized` entre GameObjects (já derrubou o editor).
3. Pipeline: **Built-in Render Pipeline**. Unity 6 (6000.0.85f2), XR Interaction Toolkit 2.6.5, HTC Vive: **gatilho = Select (pegar tudo)**; botão lateral só para ações pontuais.
4. **Sem commit e sem push.** Quem decide é o usuário.
5. Regras VR (Seção 3 do plano): `enableInstancing`, nada de `renderer.material` em runtime (usar `MaterialPropertyBlock`), só colisores primitivos em objetos com Rigidbody, zero GC por frame, `LightShadows.None` em toda luz, nada em `Update` que possa ser evento.
6. Nenhum `SceneManager.LoadScene`.
7. Não alterar o comportamento da `ErgonomicGrabInteractable`/`LaserSwingThrowInteractable` para os outros objetos do jogo (maçãs, fitas, balões etc.). Mudanças nessas classes só se forem **aditivas** e sem efeito nos outros objetos; preferir resolver em `Assets/Scripts/Interactive/Task16/`.

## 1. Diagnóstico (já feito; confirmar no `[A]`)

Li `TrophyShelfManager.cs`, `DroneCrownWearable.cs`, `ErgonomicGrabInteractable.cs`, o prefab `TrophyDisplayStand.prefab`, o builder `Task16_SceneAndPrefabBuilder.cs` e a cena `Menu_Principal.unity`.

### 1.1 Causa do "a estante teleporta até o troféu e fica tudo flutuando, mexendo e travado"

**Bug confirmado** em `TrophyShelfManager.cs`:

```csharp
// Awake(): o AudioSource é criado NA RAIZ da estante (o próprio GameObject do TrophyShelfManager)
audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
...
// PlayPickupSound(Vector3 position): chamado em TODO selectEntered de troféu
audioSource.transform.position = position;   // <-- move a RAIZ da estante inteira para a posição do troféu
```

`audioSource.transform` é o `Transform` da raiz `TrophyDisplayStand_Instance`. Então, **toda vez que o jogador pega um troféu, a estante inteira é teletransportada para a posição do troféu** (base, pedestais, textos, âncoras e os outros troféus junto, porque são filhos). Como cada troféu é um Rigidbody dinâmico filho dessa raiz que se mexe, os troféus passam a ser empurrados/arrastados em coordenadas inconsistentes: o troféu pego se desloca junto com o pai, o `Home` dos troféus (gravado em **coordenadas de mundo** no `Awake`) fica velho, o respawn automático (3,5 s) leva o troféu para o lugar antigo e a estante vai se espalhando a cada pegada. É isso que o usuário vê: estante fora do móvel, flutuando, tremendo.

Fatos associados:
- A raiz da estante não tem Rigidbody (só `BoxCollider` e o `TrophyShelfManager`); ela se move só por esse bug.
- `SetupTrophyInteractivity` chama `grab.SetHomeTransform(posição de mundo)`; a estante se move e o home não acompanha. Deve ser relativo ao `SlotAnchor` (a `ErgonomicGrabInteractable` já tem `customHomeAnchor`, que é `[SerializeField] protected`).
- O listener é adicionado com lambda (`selectEntered.AddListener((args) => ...)`) e nunca removido.

### 1.2 Outras causas prováveis de "travado/mexendo" (confirmar com medição)

- **Colisores dos troféus se sobrepondo no repouso.** Os slots ficam a x = −0,18 / −0,06 / +0,06 / +0,18 (só 0,12 m entre os centros), mas as caixas de colisão dos troféus têm larguras de 0,10 / 0,12 / 0,14 / 0,16 m. Em pares vizinhos há 1 a 3 cm de interpenetração. Como os 4 são Rigidbodies **dinâmicos** (não kinematic) desde o início, o PhysX os empurra para fora um do outro e do `BoxCollider` da base (0,52 × 0,045 × 0,14) continuamente. A coroa ainda ocupa a ponta (x = 0,18 + meia largura 0,08 = 0,26 = borda exata da base de 0,52).
- **Sem estado de repouso estável:** nenhum `Sleep`, sem atrito/material, sem interpolação. Um troféu largado "escorrega" e não volta para o pedestal de forma definitiva.
- A `ReturnHomeRoutine` (3,5 s depois de largar) leva o troféu por `transform.position` (coordenadas de mundo) ignorando a hierarquia; com a raiz se movendo, quebra.

### 1.3 Textos bugados

- Cada `Label_*` é um `TextMeshPro` 3D com `fontSize = 1` (e `m_fontSizeBase 1`) em objeto de escala 1: isso significa **letras de ~1 metro de altura**, num retângulo de 0,12 × 0,04 m, sem `Auto Size`, com `overflowMode 0` (Overflow), então vaza e aparece como letras amarelas gigantes atrás da prateleira ("C", "M", "oa"…, como visto em `Relocated_Shelf_Trophies.png`).
- Todos os 4 rótulos (`Label_GoldenApple`, `Label_LuminousBonsai`, `Label_StarCup`, `Label_DroneCrown`) estão pendurados na raiz (e por isso viajam com a raiz do bug 1.1).
- Os textos estão sem acento ("Maca Dourada", "Taca Estelar", "Guardiao"…): o projeto é em português; usar acentos corretos e uma fonte TMP que tenha os glifos (LiberationSans SDF padrão tem).
- Estado bloqueado: o troféu fica **invisível** (`SetActive(false)`) e o texto vira `"??? (Bloqueado)"`; hoje `unlockAllForDemo = true` então nunca aparece, mas deve ser tratado direito.

### 1.4 Pendências conhecidas ligadas a esta estante

- Folga da estante para os vizinhos no móvel `modern_shelves_3 (1)`: **0,062 m** (< 0,08). Posição atual: ≈ `(9.583, 1.217, -0.055)`, rotY 270 (TASK-06 BuilderResult).
- A `DroneCrownWearable` encaixa a coroa na cabeça do drone no `Start()` se o save disser "coroado": nesse caso a coroa some da estante. Manter o comportamento, mas deixar a estante mostrar um marcador "coroa no Drone".

## 2. Micro-células

### `[A]` Scout + Spec (somente leitura; sonnet)

1. Confirmar o bug de §1.1 **em execução** (somente leitura/simulação em Play Mode): anotar a posição mundial da raiz `TrophyDisplayStand_Instance` antes e depois de simular `selectEntered` num troféu. Registrar o "antes" em `.agents/visual_qa/TASK-16_Before.txt` e com 2 capturas offscreen.
2. Medir: tamanho real dos colisores e pivôs dos 4 troféus (`Trophy_*.prefab`), sobreposição entre pares no repouso, tamanho da base, margem até a borda do móvel `modern_shelves_3 (1)` (tampo, profundidade, vizinhos), altura livre acima (prateleira de cima), orientação da frente da estante em relação ao jogador (onde o paciente fica).
3. Listar o que cada texto mostra hoje, fonte TMP usada, tamanho. Confirmar quais `Label_*` aparecem fora do lugar.
4. Medir tris dos 4 troféus e da base (FBX/prefab) e listar materiais e `shader`.
5. Procurar usos de `TrophyShelfManager`, `DroneCrownWearable` e `RehabProgressionSaveSystem` na cena e em outros scripts (para não quebrar nada).
6. Escrever `.agents/specs/TASK-16_Spec.md` (substituir o antigo) com medidas, decisões de layout e orçamento de performance.

### `[B]` Arte 3D e áudio (Blender/Python, opus)

Objetivo: estante **mais bonita e legível**, sem mudar os 4 troféus em si além de pequenos acertos de pivô/escala (o usuário não reclamou deles).

- **Nova base `TrophyDisplayStand`** (substitui o bloco liso): ≥ **0,86 m de largura** e ~0,22 m de profundidade, para caber 4 troféus com **≥ 0,20 m entre centros** e margem nas pontas (se o `[A]` mostrar que o móvel não comporta, usar 0,72 m e espaçamento 0,17 m). Detalhes: 4 pedestais redondos baixos (cilindro chanfrado), com **anel emissivo** na cor do troféu (dourado/verde/azul-estrela/roxo-coroa) embutido na face superior; uma **placa inclinada ~30°** na frente de cada pedestal para o nome; uma moldura/painel de fundo discreto atrás (altura ~0,12 m, sem cobrir os troféus); cantos chanfrados, material escuro metálico + latão. **≤ 1.800 tris** no conjunto, `Shade Smooth`, pivô na base central (y = 0), faces para +Z.
- Os troféus (`Trophy_GoldenApple`, `Trophy_LuminousBonsai`, `Trophy_StarCup`, `Trophy_DroneCrownHat`): manter o modelo e **≤ 800 tris** cada; garantir pivô na base (y = 0) e centro de massa baixo; remover qualquer geometria invisível/faces invertidas. Se o `[A]` apontar que algum passa do orçamento, reduzir.
- **Silhueta de "bloqueado"**: material escuro semitransparente/emissivo fraco (um único material compartilhado) para mostrar o troféu ainda não conquistado sem precisar de outro modelo.
- Texturas simples (se precisar): ícone de cadeado 64×64 e um brilho radial 64×64 (para o halo atrás do troféu). Tudo com GPU Instancing e mesmo shader base.
- **Áudio** (`.wav`, pico ≤ −6 dBFS, nada estridente): revisar `trophy_pickup_shimmer.wav` (curto, ≤ 0,5 s), `trophy_unlock_fanfare.wav` (fanfarra curta, ≤ 2 s) e `crown_equip_royal.wav`; criar `trophy_place_click.wav` (clique suave de metal sobre pedestal, ~0,15 s) para quando o troféu volta/encaixa.

### `[C]` C# (opus) — `Assets/Scripts/Interactive/Task16/`

Zero `// TODO`. Compilar com `unity recompile` (`--detach`) e esperar o job; corrigir todos os erros antes do `[D]`.

**C.1 Correção do bug do teleporte (prioridade máxima)**
- Remover `audioSource.transform.position = position` (e qualquer outra escrita em `transform.position/rotation` da raiz, âncoras, bases ou rótulos em runtime). O `AudioSource` de feedback passa a ser um **GameObject filho dedicado** (`TrophyAudio`), criado no `Awake` se não existir, **ou** usar `VRFeedbackManager.Instance.PlaySound(clip, position, volume, pitchMin, pitchMax)` (pool de áudio já existente, sem alocação). Para tocar em outro ponto, mover **somente esse filho**, nunca a raiz.
- Proteção em runtime: o `TrophyShelfManager` grava a pose da raiz no `Awake` e em `LateUpdate`... **não**; fazer isso só no teste automatizado (§C.5), para não gastar CPU em VR.
- Trocar o lambda de `selectEntered` por método com `RemoveListener` simétrico em `OnDestroy`.

**C.2 Home relativo ao slot (nada de coordenada de mundo)**
- Cada troféu usa o `SlotAnchor` como `customHomeAnchor` (campo `protected` serializado; **atribuir via `SerializedObject` no builder do `[D]`**, sem mexer na classe base). Assim, se a estante for mexida no editor, o home acompanha.
- `TriggerManualRespawn`/`ReturnHomeRoutine` continuam sendo o mecanismo de retorno (arco curto + `PlaySuccess`). Garantir que, ao terminar, o troféu **fica parado**: velocidade zerada, `Sleep()`, sem sobrepor outro colisor.

**C.3 Jogabilidade (melhorar bastante)**
- **Encaixe imediato no pedestal:** se o jogador solta o troféu a ≤ 0,20 m do seu pedestal e devagar (`velocidade < 0,6 m/s`), ele **desliza para o pedestal em ≤ 0,25 s** (reusar `TriggerManualRespawn` com atraso zero) com `trophy_place_click` e um brilho curto no anel emissivo. Se solto longe, **volta sozinho 1,5 s depois** (reduzir `autoRespawnDelay` de 3,5 para ~1,5 s no troféu da estante, via campo do `TrophyShelfManager`) em vez de ficar caído. Se cair no chão da sala (`y < respawnFloorThresholdY`), volta imediatamente.
- **Repouso estável:** em repouso no pedestal, o troféu fica `Sleep`; ao ser pego passa a dinâmico, `CollisionDetectionMode.ContinuousDynamic` **só durante o grab/arremesso**, voltando a `Discrete` 3 s depois (regra VR). Colisor primitivo apenas. Material físico com atrito razoável (`PhysicMaterial`, `dynamicFriction 0,6`, `bounciness 0.05`). `interpolation = Interpolate` enquanto segurado.
- **Cartão de informação:** ao apontar o laser para o troféu (hover) ou segurá-lo, aparece um **cartão flutuante** (TMP + fundo arredondado, `Canvas`/TMP 3D leve) com **nome**, **como foi conquistado** (a `description`) e, se houver dados no `RehabProgressionSaveSystem`, o **progresso** (ex.: "Maçãs colhidas: 12"). O cartão olha para a cabeça do jogador (billboard sem rotação em Z), fica a ~0,25 m acima do troféu, some 0,3 s depois do hover acabar, sem recriar strings por frame (só quando o texto muda). Um único cartão compartilhado pela estante (não um por troféu).
- **Estado bloqueado:** troféu não conquistado aparece em **silhueta escura** (material do `[B]`), não-pegável (`enabled = false` do interactable) e com a placa mostrando "🔒 Bloqueado" (sem emoji: usar ícone de cadeado do `[B]`). Conquistado: material normal, pegável. `unlockAllForDemo = true` continua ligado por padrão.
- **Idle bonito (barato):** troféu em repouso gira lentamente em torno do próprio Y (≈ 12°/s) e o halo atrás pulsa devagar. Só roda quando o jogador está a ≤ 4 m (`sqrMagnitude`), via um único loop do `TrophyShelfManager` (não `Update` por troféu). Para quando o troféu é pego.
- **Desbloqueio:** manter fanfarra + partículas (`unlockParticles`), orçamento ≤ 40 partículas, sem sombra, sem luz com sombra.
- **Coroa do Drone:** manter o encaixe (`DroneCrownWearable`) como está; apenas garantir que: (a) pegar a coroa da cabeça do drone funciona e ela volta à estante (via `ReturnHome`) se for solta longe; (b) enquanto estiver no drone, o pedestal da coroa mostra a placa "Coroa no Drone" e um slot vazio com anel apagado; (c) nada move a raiz da estante.

**C.4 Textos (TMP) — arrumar de vez**
- Criar o texto dos rótulos **no código/ builder com tamanhos reais**: placa de 0,12 × 0,045 m → `fontSize ≈ 0,035` (em unidades de mundo com escala 1; **nunca 1**), `enableAutoSizing = true` com `fontSizeMin = 0.015`, `fontSizeMax = 0.04`, `overflowMode = Truncate`/`Ellipsis` (`TextOverflowModes.Ellipsis`), `enableWordWrapping = false` na placa e `true` no cartão (largura 0,28 m), `alignment = Center`, `characterSpacing` sutil, **negrito** no nome. `richText` ligado, cor dourada suave com leve contorno escuro (`outlineWidth ≈ 0.12`) para ler contra o latão. Fonte: `LiberationSans SDF` do TMP com os glifos PT-BR completos (conferir `ç`, `ã`, `é`, `í`, `ó`, `ú`).
- Textos finais (com acento): **"Maçã Dourada"**, **"Bonsai Radiante"**, **"Taça Estelar"**, **"Coroa do Drone"**; descrições em português correto (ex.: "Colheita de Ouro — maçãs colhidas na floresta.").
- Os rótulos passam a ser **filhos de cada placa inclinada** (do prefab da base), e não da raiz solta. `TMP` com `sortingOrder` correto, sem `Z-fighting`, sem sombra (`Cast Shadows Off`), voltado para o jogador (frente da estante).
- Zero string alocada por frame; só atualizar o texto quando o estado muda.

**C.5 Testes automatizados** em `Assets/Editor/AutoSetup/Task16_PlayModeTest.cs`, gravar em `.agents/visual_qa/TASK-16_PlayModeTest.txt` (esperado: tudo PASS), usando o interator de teste das tasks anteriores (`ScriptedTestInteractor`):
1. Para cada troféu: simular pegar, mover 1,5 m, soltar longe → a **raiz da estante, âncoras, base e rótulos não se movem nem 1 mm** (comparar poses antes/depois); o troféu volta ao pedestal em ≤ 2,5 s; ao chegar, `velocidade < 0,01 m/s` e posição dentro de 5 mm do slot.
2. Soltar a ≤ 0,15 m do pedestal e devagar → encaixa em ≤ 0,4 s.
3. Largar no chão → volta em ≤ 1,5 s e nunca fica abaixo de `respawnFloorThresholdY`.
4. Repouso: 3 s sem nenhum troféu se mover mais que 1 mm e nenhum par de colisores da estante se sobrepondo (`Physics.ComputePenetration` ≈ 0).
5. Pegar a coroa, encostar na cabeça do drone → encaixa; pegar da cabeça do drone → solta e volta.
6. `GC.Alloc` ≈ 0 em 300 frames de idle; nenhum `Light` com sombra; cartão de info aparece no hover e some depois.
7. Nenhum `LoadScene`.

### `[D]` Layout, prefabs e capturas (opus)

1. Criar/rodar `Assets/Editor/AutoSetup/Task16_ShelfRework.cs` (`[MenuItem]` + chamável pelo CLI) que: reconstrói o prefab `TrophyDisplayStand` com a nova base do `[B]`, 4 `SlotAnchor`, 4 troféus, rótulos nas placas, cartão de info, `TrophyAudio` filho, **e atribui `customHomeAnchor` de cada troféu = seu `SlotAnchor` via `SerializedObject`**. Nada de `CopySerialized`.
2. **Cena `Menu_Principal`:** substituir a instância `TrophyDisplayStand_Instance` (uma só!), rodar o `SceneDuplicateAndOriginPurger` antes e depois: **1** estante, **4** troféus, 0 duplicatas, `(0,0,0)` limpo. Preservar o `RehabProgressionSaveSystem` e a coroa do drone.
3. **Posição:** manter no móvel `modern_shelves_3 (1)` (tampo ≈ 1,194; verificar altura/profundidade com a base nova) — ou, se a base nova não couber com folga, escolher a **melhor estante/prateleira existente** da sala (medida do `[A]`), sempre **aterrada** (`groundGapMeters ≤ 0,01`), com **folga ≥ 0,08 m** de qualquer vizinho (meta ≥ 0,25 m onde a sala permitir; documentar o melhor valor possível, como nas TASKs 06/07), frente virada para onde o paciente fica, troféus acessíveis na altura do peito e sem prateleira bloqueando o laser.
4. **Capturas offscreen 1920×1080** em `.agents/visual_qa/`:
   - `TASK-16_Shelf_Front.png`, `TASK-16_Shelf_Side_Grounding.png`, `TASK-16_Shelf_Closeup_Plaques.png` (textos legíveis e com acento), `TASK-16_Shelf_Panorama_Room.png` (a estante no contexto da sala, **sem texto gigante vazando**).
   - Sequência de interação: `TASK-16_Interaction_1..5.png` (troféu pego, cartão de info visível, solto longe, voltando, de volta no pedestal com brilho).
   - `TASK-16_Locked_Silhouette.png` (um troféu bloqueado em silhueta) e `TASK-16_Crown_On_Drone.png` (placa "Coroa no Drone").
5. `TASK-16_SpatialAudit.json` com `pairwiseClearances[]`, `isOriginClean`, `duplicateObjectsFound: []`, `groundGapMeters`, `hasOverlap:false`, e seção `shelfStability` (deslocamento máximo da raiz após os testes = 0; deriva dos troféus em repouso; sobreposição de colisores = 0).

### `[E]` Auditoria implacável (sonnet 1ª passada; opus se houver FAIL)

Ler **todos** os PNGs e o JSON; conferir o código. `FAIL` se qualquer um:
1. Qualquer escrita em `transform.position/rotation` da raiz/âncoras/base da estante em runtime (grep em `Task16/`), ou o teste mostra deslocamento da raiz > 0 após pegar/soltar.
2. Troféu que não volta ao pedestal, fica balançando/escorregando em repouso, ou colisores sobrepostos no repouso.
3. Texto gigante, cortado, sem acento, ilegível ou fora da placa em qualquer captura; fonte/tamanho `fontSize = 1`.
4. Estante flutuando, atravessando o móvel, ou com folga < 0,08 m / sobreposição; duplicata ou objeto no `(0,0,0)`.
5. `renderer.material`/`.materials` em runtime, `MeshCollider` em Rigidbody, `Light` com sombra, alocação por frame, `LoadScene`, mais de 40 partículas por efeito.
6. Cartão de info ausente/ilegível ou sem sumir; troféu bloqueado pegável; coroa com comportamento quebrado.
7. Visual pouco melhor que o anterior (base lisa sem pedestais/placas/anéis) ou orçamento de tris estourado (base > 1.800, troféu > 800).
8. Áudio com pico > −6 dBFS ou som estridente.

Escrever `.agents/visual_qa/TASK-16_ShelfAuditVerdict.md` (PASS/FAIL, tabela item a item, ressalvas) e, se FAIL, ordenar `RE-RUN [B]`, `[C]` ou `[D]` com a causa. Só então marcar `[x]` na matriz (a TASK-16 inteira só vira `COMPLETED*` depois da varredura global e do certificado, que ficam para o fim).

## 3. Critérios de aceitação (resumo)

- Pegar e soltar qualquer troféu **nunca** move a estante; o troféu volta (ou encaixa) sozinho no pedestal e fica parado.
- Textos corretos, em português com acento, legíveis a 1,5 m, dentro das placas, sem nada vazando para trás da prateleira.
- Estante mais bonita: base com pedestais, anéis emissivos, placas inclinadas, troféus girando devagar, silhueta para bloqueados.
- Jogabilidade: cartão de informação no laser/segurar, encaixe imediato no pedestal, retorno automático em ~1,5 s, coroa do drone funcionando.
- Layout: 1 estante, 4 troféus, 0 duplicatas, `(0,0,0)` limpo, aterrada, folga ≥ 0,08 m (meta 0,25 m).
- Play Mode test 100% PASS; sem alocação por frame; sem luzes com sombra.

## 4. Fora de escopo (apenas registrar)

- Varredura global final e `FINAL_VR_PLAYTEST_AND_PERFORMANCE_CERTIFICATE.md` (depois das TASKs 07/09/10).
- Persistência/JSON (`RehabProgressionSaveSystem`): não alterar além de ler o progresso para o cartão.
- Collider do `ArcadeBallSpawner` (TASK-08) e `ledRenderer.material` da alavanca (TASK-09).
- Teste no óculos: fica com o usuário; ao final registrar como `COMPLETED*` na parte da estante (verificada por simulação, falta playtest no HMD).
