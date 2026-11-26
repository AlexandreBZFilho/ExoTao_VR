# TASK-09 — Plano de execução para o Opus (Fidgets: Fogos de Artifício refeito + Bobblehead mais "bobblehead")

> Enviar este arquivo ao Opus junto com `AGENTS.md`. Executar **uma micro-célula por vez** (`[A]`→`[E]`), atualizando a matriz da Seção 4 de `AGENTS.md` e `AGENTS_IMPLEMENTATION_PLAN.md` a cada célula aprovada. Modelo: `opus` para `[B]`, `[C]`, `[D]` e qualquer retrabalho; `sonnet` pode fazer `[A]` (só leitura) e a primeira passada de `[E]`.

## 0. Regras operacionais (valem para todos os subagentes)

1. **PROIBIDO Computer Use** (mouse, teclado, captura de tela do desktop, `open_application`, `AppActivate`). O usuário está usando o PC. Visual QA **somente** offscreen: câmera virtual na Unity → `Camera.Render()` → `RenderTexture` → `.png`. Blender via Blender MCP ou `blender -b -P`. Repita esta regra em todo prompt de subagente.
2. O Unity Editor **já está aberto**. Nunca abrir segunda instância. Usar o `unity` CLI (`unity command script eval/run/recompile`, `--detach`, `unity job status/wait`). Nunca usar `--focus`. O editor fica lento sem foco: eval simples leva 15–35 s e domain reload 130–300 s; use `--detach` e espere pelo job. Nunca usar `EditorUtility.CopySerialized` entre GameObjects (já derrubou o editor).
3. O pipeline é **Built-in Render Pipeline** (não URP). Isso afeta shaders de partículas (ver §1.1 causa 5).
4. Sem commit e sem push. Quem decide é o usuário.
5. **A alavanca de clima (hora do dia) está aprovada e NÃO pode ser alterada.** Não editar `AtmosphereLeverInteractable.cs`, o prefab nem o modelo da alavanca. Prove isso no `[E]` com `git diff --stat` sem nenhum arquivo da alavanca e com uma captura antes/depois idêntica.
6. VR (Seção 3 do plano): `enableInstancing`, nada de `renderer.material` em runtime (usar `MaterialPropertyBlock`), só colisores primitivos em objetos com Rigidbody, zero GC por frame, `LightShadows.None` em toda luz, `Update` desligado quando o objeto está em repouso.
7. Nenhum `SceneManager.LoadScene` em nenhum script.

## 1. Diagnóstico (já feito, confirmar no `[A]`)

### 1.1 Fogos de artifício — por que sai "do meio do chão, tudo bugado"

Li `FireworksButtonInteractable.cs`, `Task09_SceneAndPrefabBuilder.cs` (`SetupFireworksParticleLauncher`, linhas 469–566) e a cena `Menu_Principal.unity`. Causas confirmadas:

1. **Explosão desativada na cena.** O builder faz `emB.enabled = false` e depois `SetBursts(...)`. Na cena, o `EmissionModule` do `Burst_Explosion_System` está com `enabled: 0`. Com o módulo de emissão desligado, o `Burst` nunca emite. Ou seja, a explosão principal **nunca aparece**; só a trilha ascendente.
2. **Posições presas em coordenadas do mundo da mesa antiga.** O ponto de detonação (`Burst_Apex_Target`) foi criado em `(1.80, 2.45, 1.40)` do mundo, quando o botão ficava em `(0.79, 0.955, 0.60)`. Ele virou filho de `Room_Fireworks_Launcher`, com offset local `(1.01, 1.495, 0.80)`. Quando a TASK-06 realocou o botão para `counter_2_b` (≈ `(8.98, 0.95, -2.02)`, rotação ≈ −90° em Y), o launcher e os filhos foram junto, e o offset **girou com o botão**. Hoje o ponto de detonação cai ~1,3 m para o lado e 1,5 m acima do botão, em direção arbitrária: pode ficar dentro da TV, de uma estante ou da parede.
3. **Trilha com direção velha.** `Ascent_Sparks_Trail` recebeu `LookAt(apex)` no momento da criação. Depois da realocação o `LookAt` ficou rodado junto com o pai (rotação local ≈ `(-0.37, 0.40, 0.18, 0.82)`), então as faíscas saem numa direção que não aponta para o apex, às vezes para baixo/lateral, passando pelo tampo e chão. Também: `startSpeed 8 × startLifetime 0.4` = 3,2 m de alcance, enquanto o apex está a ~1,9 m; a trilha passa direto e não "chega" na explosão.
4. **Tudo montado só na cena, por script de editor.** O prefab `FireworksLaunchButton.prefab` tem as quatro referências (`fireworksAscentParticles`, `fireworksBurstParticles`, `fireworksBurstLight`, `burstLocation`) **nulas**. Qualquer realocação ou re-run do builder quebra o efeito.
5. **Hipótese a confirmar:** `M_Fireworks_Sparkle.mat` é um material emissivo "Lit" (`CreateEmissiveMaterial`). Em partículas isso renderiza esferas opacas sombreadas, não faíscas. Em Built-in RP o certo é um shader de partícula aditivo (`Particles/Standard Unlit` ou `Legacy Shaders/Particles/Additive`), com textura de ponto suave.
6. **Sem foguete de verdade, sem cooldown.** `rocketTravelTime` é um `WaitForSeconds` fixo (0,6 s), independente da distância. `isPressing` só trava durante a animação do botão (~0,5 s); dá para disparar outro show sobre o anterior. O disparo por mão usa `OnTriggerEnter` com `other.name.ToLower().Contains("hand")` — frágil e pode disparar por qualquer coisa chamada "controller".

### 1.2 Bobblehead — por que balança tão pouco

Li `BobbleheadSpringToy.cs` e o prefab `ExoMascotBobblehead.prefab`. Causa numérica:

- O oscilador trata o impulso como **velocidade angular em graus/s**. Amplitude resultante ≈ `v / ω`. Com `ω = 13,5 rad/s`, o clique (`±28`) dá ~2° de deslocamento e o hover (`±7`) dá ~0,5°. É praticamente invisível.
- `ζ = 0,14` com `ω = 13,5` faz o movimento morrer em ~1 s (decaimento `ζω ≈ 1,9/s`). Bobblehead de verdade balança por vários segundos.
- Só reage a: hover do laser, `selectEntered` (um impulso aleatório) e `OnCollisionEnter` (o Rigidbody da raiz é **kinematic**, então só colide com Rigidbody dinâmico; a mão do paciente não dispara nada).
- Só existe um eixo de pêndulo (pitch + roll), sem torção, sem limite com "batida" no ombro, sem movimento secundário (orelhas/mola/braços), sem som por cruzamento de zero.
- O colisor esférico da cabeça (`radius 0.045`, centro `y=0.125`) está na **raiz**, então não acompanha a cabeça.

### 1.3 Pendências herdadas (registrar, só tratar o que for pedido aqui)

- Bobblehead com folga de **0,076 m** do vizinho em `drawer_simple` (< 0,08 m) — tratar no `[D]`.
- Bobblehead e botão de fogos ~2% acima do orçamento de polígonos (checar com o log de polycount e corrigir só se mexer no modelo).
- `AtmosphereLeverInteractable.cs:393` usa `ledRenderer.material` (viola a regra VR). **Não tocar**, pois a alavanca está aprovada; deixar anotado para o usuário decidir.
- Lever e botão de fogos precisam de distância > 0,45 m entre si (plano original).

## 2. Micro-células

### `[A]` Scout + Spec (somente leitura; ~sonnet)

1. Confirmar cada causa de §1.1 e §1.2 no disco e na cena aberta (`unity command script eval`, somente leitura). Reportar: posição mundial atual do botão, do `Burst_Apex_Target`, do `Ascent_Sparks_Trail` e seu `forward` mundial, `EmissionModule.enabled`, shader/material dos dois sistemas, `simulationSpace`, número máximo de partículas.
2. Renderizar offscreen **como está hoje** o disparo (simular `ParticleSystem.Simulate` em 0,2/0,5/0,9/1,4 s) para ter o "antes" do bug.
3. Medir o espaço livre em cima e ao redor de `counter_2_b` (raycast para cima e em 8 direções horizontais): altura até a TV, estantes e teto, e direção com mais espaço livre.
4. Listar as superfícies para o Bobblehead (ver §2 `[D]`), com folga disponível em cada uma, e a folga atual entre alavanca ↔ fogos e do Bobblehead ao vizinho.
5. Medir tris do FBX do botão e do Bobblehead.
6. Escrever `.agents/specs/TASK-09_Spec.md` (substituir a versão antiga) com tudo isso.

### `[B]` Arte e áudio (Blender/Python, opus)

Só mexer no que a spec do `[A]` pedir. Esperado:

- **Bobblehead:** cabeça um pouco maior e mais "cabeçuda" (escala ~1,2–1,3 em relação ao corpo, mantendo o pivô no pescoço, `Bobble_Head_Pivot` em `y≈0.088`). Mola do pescoço visível (`Bobble_Neck_Spring`) e orelhas/antenas separadas como objetos próprios para o movimento secundário. Orçamento: **≤ 1.800 tris** no total. Se o FBX atual passa disso, reduzir.
- **Fogos:** botão ≤ 800 tris. Se ficar acima, reduzir. Criar um **halo/flash** simples (quad com textura radial, ou textura de ponto suave 64×64 gerada em Python) para as partículas; sem mais geometria.
- **Texturas de partícula:** gerar `Firework_Spark.png` (ponto suave com núcleo brilhante) e `Firework_Trail.png` (gradiente alongado), 64×64 ou 128×64, com alpha.
- **Áudio** (`.wav`, pico ≤ −6 dBFS, sem som estridente, low-pass ~6 kHz): `fireworks_launch_whistle` (assobio curto, subindo de tom, ~0,6 s), `fireworks_burst_explosion` (estouro macio + crepitar curto, ~1,2 s, **não** pode assustar o paciente), `bobblehead_spring_boing` (mola curta, ~0,35 s) e **duas variações novas**: `bobblehead_spring_soft` (mais leve, ~0,2 s) e `bobblehead_limit_tock` (batida seca de cabeça no limite, ~0,1 s).

### `[C]` C# (opus)

Trabalhar em `Assets/Scripts/Interactive/Task09/`. Zero `// TODO`.

**C.1 `FireworksButtonInteractable.cs` — reescrever o show.**

- **Auto-contido no prefab.** Tudo que o botão precisa (foguete, trilha, explosões, luz) vira filho do prefab `FireworksLaunchButton` e é referenciado por campos serializados do prefab. Nada pendurado na raiz da cena. Se o botão for movido ou girado, o efeito continua correto. Apagar o `Room_Fireworks_Launcher` da cena no `[D]`.
- **Ponto de detonação calculado em runtime, nunca fixo.** Ao disparar:
  1. Direção base = vertical (`Vector3.up`) inclinada até ~12° para o lado de maior espaço livre (testar 8 `Physics.RaycastNonAlloc`/`SphereCast` horizontais pré-alocados; escolher o mais livre; ignorar triggers).
  2. Altura da explosão = `min(alturaDesejada ≈ 1,2 m acima do botão, distânciaLivreAcima − 0,35 m)`, com mínimo de 0,6 m. Se a folga for menor que 0,6 m (TV logo acima), fazer um **show baixo** (leque de faíscas pequenas) em vez de explodir dentro do móvel.
  3. Nunca abaixo do botão, nunca dentro de colisor sólido.
- **Foguete real.** Um `Transform` "Rocket" pequeno com `TrailRenderer` fino ou sistema de partículas de faíscas em **World space**, que percorre da saída do botão ao apex com ease-out em tempo `distância / velocidade (≈ 3,5 m/s, 0,45–0,8 s)`. O estouro acontece **quando o foguete chega**, não por tempo fixo. Pequeno brilho (halo) na ponta.
- **Explosões variadas** (escolhe uma por disparo, sem repetir a anterior): esfera "peônia" multicolorida; anel; "salgueiro" dourado que cai devagar com gravidade leve; e uma "crepitação" final de faíscas miúdas. Orçamento total **≤ 250 partículas vivas**, `simulationSpace = World`, `maxParticles` definido, `gravityModifier` baixo, sem colisão de partículas, sem sombra (`shadowCastingMode = Off`, `receiveShadows = false`).
- **Material de partícula correto para Built-in RP**: shader aditivo, `enableInstancing`, `M_Fireworks_Sparkle` recriado ou substituído (`Particles/Standard Unlit` aditivo ou `Legacy Shaders/Particles/Additive`) com as texturas do `[B]`. Cor por gradiente do sistema, nunca `renderer.material` em runtime.
- **Luz de flash:** uma única `Light` ponto com `LightShadows.None`, intensidade moderada (≤ 2,5), alcance ≤ 3 m, fade ≈ 0,3 s, só ligada durante o estouro.
- **Disparo e cooldown.** Gatilho do Vive (Select) com laser ou mão, mais toque da mão no domo. O toque da mão usa um `Collider` trigger dedicado no domo e valida `other.GetComponentInParent<XRBaseInteractor>()`/`XRBaseController` — **sem** comparar nome com "hand". Cooldown de 1,2 s entre disparos; no máximo 2 shows ao mesmo tempo (pool pré-alocado de 2 conjuntos de partículas). Disparo durante o cooldown não faz nada (sem enfileirar).
- **Feedback:** domo afunda e volta com mola (manter a animação atual, é boa), háptico proporcional, brilho do botão via `MaterialPropertyBlock`.
- **Áudio:** assobio e estouro em `AudioSource` 3D (`spatialBlend = 1`, `maxDistance ≈ 12`), volume ≤ 0,5, estouro atrasado levemente pelo tempo do foguete (já é). Sem loop.
- Zero alocação por frame; corrotinas só durante o show; `Update` não usado.

**C.2 `BobbleheadSpringToy.cs` — balançar de verdade.**

- **Refazer o modelo físico em graus de deflexão, não em "impulso aleatório".** Novos parâmetros-base: `naturalFrequency ≈ 15–17 rad/s` (≈ 2,5 Hz), `dampingRatio ≈ 0.05–0.07` (balança 3–4 s), `maxDeflectionAngle ≈ 50°`. Definir `ApplyKick(Vector2 amplitudeDeg)` que converte amplitude em velocidade inicial (`v = amp × ω`), em vez de somar graus/s diretamente. Manter `ApplyImpulse` como compatibilidade (chamar de dentro do `ApplyKick`).
- **Eixo extra de torção (yaw)** com frequência um pouco diferente, para a cabeça "girar de lado" depois de uma pancada forte. Movimento total em 3 eixos, com leve acoplamento (o pitch alimenta um pouco o yaw) para parecer orgânico.
- **Limite com "batida" e quique.** Quando a deflexão passa de ~38°, suavizar com mola mais dura (limite elástico, não `ClampMagnitude` seco) e, ao encostar no limite, tocar `bobblehead_limit_tock` e transferir ~25% da velocidade de volta (quique no ombro).
- **Interações novas:**
  1. **Toque da mão** (controlador entra no volume da cabeça): impulso na direção do movimento da mão, proporcional à velocidade dela no momento do contato (rastrear a velocidade da mão por posição em `LateUpdate`, sem alocação); amplitude 10°–35°.
  2. **Gatilho do Vive (Select) na cabeça, com mão ou laser = "peteleco"** forte e **direcional** (empurra para longe do ponto apontado), 28°–35°. Nada de direção aleatória.
  3. **Segurar para puxar (opcional, desejável):** enquanto o gatilho está pressionado na cabeça, a cabeça acompanha o ponto apontado (inclina até ~40° na direção do raio com suavização). Ao soltar, ela vai para o oscilador com a velocidade atual e balança bastante. Implementar com `XRSimpleInteractable` mantendo o laser, sem pegar o objeto inteiro.
  4. **Hover do laser:** só um tremidinho sutil (3°–5°), uma vez por entrada (com cooldown de 0,5 s).
  5. **Colisão de objetos arremessados** (bola, fita, balão) e batida **na mesa**: manter `OnCollisionEnter`, mas com amplitude em graus proporcional à velocidade relativa e limitada.
- **Movimento secundário:** mola do pescoço (`Bobble_Neck_Spring`) estica/encolhe e dobra com a deflexão (rotação metade do ângulo + escala Y de 1 a ~1,12); orelhas/antenas seguem a cabeça com atraso e frequência maior (osciladores simples extras, 2–3, mesmo código); braços balançam levemente.
- **Visor:** brilho e cor pulsam com a energia do balanço, via `MaterialPropertyBlock` e a luz do visor (`LightShadows.None`), voltando ao normal quando para.
- **Som por movimento:** `boing` proporcional à energia na pancada, `soft` quando a cabeça cruza o zero com energia boa (no máximo 1 a cada 0,18 s), `tock` no limite. Pitch levemente aleatório. Volume ≤ 0,5. Sem sons enquanto a energia está baixa.
- **Háptico** proporcional à amplitude da pancada.
- **Performance:** quando `energia < epsilon` por 0,5 s, zera os estados, encaixa a cabeça na rotação base e **desliga o `Update`** (`enabled = false`); qualquer `ApplyKick` religa. Substeps fixos de 1/90 s para estabilidade com `ω` maior. Sem alocação.
- Expor no Inspector `[Header]` com tooltips para o usuário afinar a sensação sem mexer no código, e um `[ContextMenu]` "Boing Forte / Médio / Fraco" para teste.

**C.3 Testes automatizados** em `Assets/Editor/AutoSetup/Task09_PlayModeTest.cs` (ou script de Play Mode equivalente), usando um interator de teste (`ScriptedTestInteractor`, como nas TASKs 06/07):
- Fogos: dispara; confere que o apex mundial fica **acima do botão** (≥ 0,6 m) e sem colisor sólido entre botão e apex; que o estouro acontece só quando o foguete chega; que o cooldown bloqueia o segundo disparo em 1,2 s; que nunca há mais de 250 partículas vivas; que depois do show tudo para sozinho; nenhum `Light` com sombra.
- Bobblehead: pancada de 25° → pico ≥ 18°; ainda ≥ 3° após 1,5 s; ≤ 0,2° após ~8 s com o `Update` desligado depois; quique no limite toca `tock`; peteleco direcional empurra para o lado oposto ao do ponto apontado; `GC.Alloc` ≈ 0 em 300 frames.
- Gravar resultados em `.agents/visual_qa/TASK-09_PlayModeTest.txt` (esperado: todos PASS).

Compilar com `unity recompile`/`--detach` e esperar o job; corrigir qualquer erro de compilação antes de passar ao `[D]`.

### `[D]` Layout, prefabs e capturas (opus)

1. Recriar/ajustar prefabs `FireworksLaunchButton` e `ExoMascotBobblehead` com todas as referências **preenchidas dentro do prefab**. Criar/rodar `Task09_FidgetRework.cs` (editor script, `[MenuItem]`) que também atualiza a cena.
2. **Cena `Menu_Principal`:** apagar `Room_Fireworks_Launcher` e filhos órfãos; garantir **1** botão de fogos, **1** bobblehead, **1** alavanca (sem tocar na alavanca). Rodar `SceneDuplicateAndOriginPurger` antes e depois; `(0,0,0)` limpo.
3. **Posições** (herdadas da TASK-06; revalidar):
   - Alavanca + botão de fogos em `counter_2_b`: distância entre eles > 0,45 m, com folga ≥ 0,08 m de qualquer vizinho. Se a TV logo acima impedir o show alto, mover **só o botão de fogos** para um tampo com mais pé-direito (usar a medição do `[A]`), ou garantir o "show baixo".
   - Bobblehead em `drawer_simple` ou outra superfície mais folgada (prateleira/estante acessível), com folga ≥ 0,08 m (meta ≥ 0,25 m onde a sala permitir; documentar a melhor folga possível, como foi feito na TASK-07). Frente do mascote virada para o jogador.
   - Nada flutuando: aterrar com raycast; `groundGapMeters ≤ 0.01`.
4. Aplicar `Physics.IgnoreCollision` não é necessário aqui. Garantir que o colisor da cabeça do Bobblehead acompanhe o pivô da cabeça (filho de `Bobble_Head_Pivot`), colisores primitivos apenas.
5. **Capturas offscreen 1920×1080** (sem Computer Use), salvar em `.agents/visual_qa/`:
   - `TASK-09_Fireworks_Seq_1..5.png`: sequência de um disparo (botão pressionado → foguete subindo → estouro → faíscas caindo → fim), vista de onde o paciente fica, e **uma vista lateral** mostrando que o apex está acima da mesa.
   - `TASK-09_Fireworks_Variants.png`: as variantes de explosão.
   - `TASK-09_Bobble_Seq_1..6.png`: parado → pancada → deflexão máxima de um lado → do outro → limite (tock) → quase parado.
   - `TASK-09_Layout_*.png`: close de cada móvel com os fidgets mostrando espaçamento.
   - `TASK-09_Lever_Unchanged.png` (para provar que a alavanca é igual ao antes).
6. `TASK-09_SpatialAudit.json` com `pairwiseClearances[]`, `isOriginClean`, `duplicateObjectsFound: []`, `groundGapMeters`, `hasOverlap:false`, e uma seção `fireworksSimulation` (apex mundial, distância livre, partículas máximas) e `bobbleSimulation` (pico, tempo de decaimento).

### `[E]` Auditoria implacável (sonnet 1ª passada; opus se houver FAIL)

Ler **todos** os PNGs e o JSON. `FAIL` se qualquer um:

1. A explosão dos fogos aparece perto do chão, dentro de móvel/TV/parede, ou fora do alcance visual do jogador; ou o foguete não é visível subindo; ou as faíscas aparecem como esferas sólidas sombreadas.
2. O `EmissionModule` da explosão ainda está desativado, ou há referências nulas no prefab do botão.
3. Mais de 250 partículas vivas, qualquer luz com sombra, `renderer.material`/`.materials` em runtime, `MeshCollider` em objeto com Rigidbody, ou `LoadScene`.
4. O Bobblehead balança menos de ~18° de pico numa pancada média, ou para antes de ~2 s, ou a deflexão fica "dura" no limite, ou o movimento não aparece nas capturas.
5. Fidgets sobrepostos ou folga < 0,08 m, objeto duplicado ou no `(0,0,0)`, item flutuando.
6. Qualquer mudança nos arquivos da alavanca (`git diff --stat`), ou a captura da alavanca diferente do original.
7. Áudio com pico > −6 dBFS ou som estridente.

Escrever `.agents/visual_qa/TASK-09_AuditVerdict.md` (PASS/FAIL, tabela item a item, ressalvas não bloqueantes) e, se FAIL, ordenar `RE-RUN [B]`, `[C]` ou `[D]` com a causa. Só então marcar `[x]` na matriz.

## 3. Critérios de aceitação (resumo)

- Botão de fogos: um disparo sai do botão, **sobe como foguete**, estoura no ar numa altura livre acima do móvel, com cores e variações, para sozinho, tem cooldown, e sobrevive a mover/girar o botão.
- Bobblehead: uma pancada média tira a cabeça ~20–30° para os lados, ela oscila por 3–4 s com batida no limite, mola/orelhas acompanham, responde à mão, ao laser e ao arremesso, e é possível puxar a cabeça e soltar.
- Alavanca de clima intacta.
- Layout: sem sobreposição, sem duplicatas, sem flutuação, folga ≥ 0,08 m (meta 0,25 m onde existir).
- Play Mode test 100% PASS e sem alocação por frame.

## 4. Fora de escopo (apenas registrar)

- `AtmosphereLeverInteractable.cs:393` (`ledRenderer.material`): alavanca aprovada, não mexer.
- Rótulos TMP gigantes dos troféus (TASK-16); collider do `ArcadeBallSpawner` (TASK-08).
- Teste no óculos fica com o usuário; ao final a TASK-09 deve ficar `COMPLETED*` (verificada por simulação, falta playtest no HMD).
