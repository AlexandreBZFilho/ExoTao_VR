# TASK-07 — Pistola de Balões: plano de correção e melhoria (para o Opus)

> Leia `CLAUDE.md` (regras do projeto) e `AGENTS.md` (matriz da Seção 4) antes de começar.
> Projeto Unity 6 (6000.0.85f2), XRI 2.6.5, controle HTC Vive. Idioma das respostas: português.
> Este plano substitui a versão antiga da TASK-07 (código de 10/11, nunca revisado com os hotfixes).

## 0. Regras operacionais (inegociáveis)
1. **Proibido Computer Use** (mouse, teclado, screenshot do desktop). O usuário está usando o PC. Tudo em segundo plano.
2. **Visual QA só offscreen:** câmera virtual na Unity, `Camera.Render()` para `RenderTexture`, salvar `.png` em `.agents/visual_qa/TASK-07_*.png`. Use `Assets/Editor/VisualQA/VisualQACaptureEditor.cs` e `SpatialAuditUtility.cs` (já têm captura por textura e `pairwiseClearances`).
3. **Unity via CLI oficial** (`unity ...`, pacote com.unity.pipeline). Nunca abrir outra instância com o editor aberto, nunca usar `--focus`. Se o editor estiver fechado: `unity run . --log-file <f> --no-tail -- -executeMethod Ns.Tipo.Metodo` (o método deve chamar `EditorApplication.Exit`). Para testes em Play Mode: `Unity.exe -batchmode -projectPath ... -executeMethod X` direto, sem `-quit` (ver memória `unity-cli-usage`). Editor aberto e sem foco fica lento (eval de 15-35 s): use `--timeout` alto ou `--detach`.
4. **Nunca** usar `EditorUtility.CopySerialized` entre GameObjects diferentes (derrubou o editor). Copie campo a campo via `SerializedObject`.
5. Não fazer commit nem push. O usuário pede quando quiser.
6. Uma micro-célula por vez (`[A]`..`[E]`), atualizando a matriz em `AGENTS.md`, `AGENTS_IMPLEMENTATION_PLAN.md` e `CLAUDE.md` ao fim de cada uma. Modelos: `[B]`, `[C]`, `[D]` e qualquer retrabalho em Opus; `[A]` e primeiro `[E]` podem ser Sonnet.

## 1. Diagnóstico (já verificado nos arquivos)

### 1.1 Pistola virada 180° na mão — causa raiz
- No FBX (`Assets/Models/Generated/Task07/BalloonGunDispenser.fbx`, gerado por `generate_task07_models.py`) o cano aponta para **+Y do Blender**, que na Unity vira **−Z** local. Prova no prefab `Assets/Prefabs/Interactive/BalloonGunDispenser.prefab`: `Muzzle_Socket` em `(0, 0.04, -0.155)` com rotação identidade, `Gun_Barrel` em `z = -0.095`.
- O XRI alinha o **+Z** do attach point da mão com o **+Z** do objeto. O prefab não tem `m_AttachTransform` (fileID 0) e usa `m_MatchAttachRotation: 1`. Resultado: o cano aponta para o próprio jogador.
- Efeito colateral no código: `BalloonInflaterGun.ReleaseBalloon` lança o balão em `muzzleSocket.forward` (= +Z, ou seja, **para trás** do cano) e o balão nasce com `Quaternion.Euler(90,0,0)` relativo a um socket que também está virado. Corrigir a orientação conserta o lançamento junto.

### 1.2 Balão pequeno demais — causa raiz
- O modelo `InflatableBalloon.fbx` tem **0,30 m de altura e 0,24 m de largura em escala 1** (perfil em `generate_task07_models.py`, linhas ~235-250).
- O código usa `maxBalloonScale = 0.30f` (tooltip diz "metros") **como `localScale`**. O balão final fica com ~9 cm de altura e ~7 cm de largura. A escala deveria ser ~1.0+, não 0.30.
- O comprimento da cordinha (`stringLength = 0.40`) não acompanha a escala.
- Os balões de demonstração do Menu (`SpawnStaticBalloon(..., scale, ...)`, nomes `Demo_FloatingBalloon_*`) usam escalas na mesma lógica errada.

### 1.3 Outras pendências herdadas (do relatório de auditoria)
- Não existe o limite de **8 balões simultâneos / pool** exigido pelo plano. Hoje cada inflada faz `Instantiate` e `Destroy`.
- `BalloonInflaterGun.cs:289` usa `mr.material = mat` em runtime (proibido: duplica material). Usar `sharedMaterial` ou `MaterialPropertyBlock`.
- O balão estoura em **qualquer colisão** após 0,25 s (mesa, chão, parede, outro balão). Isso é frustrante e contraria "zero frustração" da spec.
- Itens da pistola foram realocados na TASK-06.D para `counter_3_a (1)` em `(4.990, 1.051, 7.621)` e 3 balões demo ficaram "acima da pistola". Precisam ser revalidados (folga ≥ 0,25 m dos vizinhos, balões abaixo do teto 2,85 m, nada em `(0,0,0)`).
- Memória do projeto: o gatilho do Vive (Select) segura/seleciona tudo; o botão lateral (Activate) é só para ação pontual, como **inflar o balão**. Manter inflar no Activate, a menos que o usuário peça outra coisa.
- `FloatingBalloon` é `XRSimpleInteractable` de propósito (não derivar de `ErgonomicGrabInteractable`). A pistola continua derivando de `ErgonomicGrabInteractable` (`alwaysDynamicAfterRelease = true`: ao soltar, cai com física normal, nunca flutua).

## 2. Micro-células

### `[A]` Scout (Sonnet, só leitura)
- Medir a orientação real: abrir o prefab (via CLI/eval) e registrar bounds, posição/rotação do `Muzzle_Socket`, `Gun_Grip` (centro e inclinação ~−16°) e BoxColliders da raiz.
- Registrar a posição/rotação atuais da pistola, do suporte e dos balões demo no `Menu_Principal` e os vizinhos num raio de 0,6 m.
- Confirmar o pico de volume (dBFS) de `Assets/Audio/Generated/Task07/*.wav`; o arquivo `balloon_pop_loud.wav` tem "loud" no nome e a regra do projeto é pico ≤ −6 dB.
- Escrever `.agents/specs/TASK-07_Spec.md` (sobrescrever a versão antiga) com tudo isso.

### `[B]` Arte (Opus, Blender via `blender -b -P` ou Blender MCP)
- **Balão:** manter o formato de gota, mas deixar mais redondo e bonito: mais anéis no ventre (até ~700 tris, limite de objeto médio 800-1800), nó (knot) mais claro, normais suaves. Altura nominal em escala 1 continua 0,30 m (o código passa a usar escala > 1). Zero partes invisíveis ou normais invertidas.
- **Materiais:** mais brilho (specular/smoothness alto, leve fresnel ou rim claro), GPU Instancing ligado. Considerar leve translucidez só se o shader do projeto suportar sem custo extra.
- **Pistola:** só mexer no FBX se for realmente necessário. A correção de orientação é feita no prefab (`[D]`), não regenerando o modelo.
- **Áudio:** garantir pico ≤ −6 dBFS em inflar, soltar e estouro (`balloon_pop_loud.wav` deve ficar claramente mais baixo e curto). Sem sons estridentes.
- Rodar a auditoria de polígonos (`ModelPolycountAudit`) e gerar um print antes/depois do balão.

### `[C]` C# (Opus) — `Assets/Scripts/Interactive/Task07/`
1. **Orientação e lançamento:** o código deve assumir `+Z` da pistola = direção do cano. Lançar em `muzzleSocket.forward`. O balão em inflação nasce alinhado ao socket (bico), com a "boca" do balão (nó) voltada para o bico e crescendo para frente/cima.
2. **Tamanho:** trocar `minBalloonScale/maxBalloonScale` por parâmetros em escala real: `minScale ≈ 0.15`, `maxScale ≈ 1.4` (balão final ≈ 0,42 m de altura e 0,34 m de largura; ajustar visualmente). O tamanho final depende do tempo segurando o botão (de ~0,7 a `maxScale`). Pequena variação aleatória ±8%. `stringLength` e espessura da cordinha escalam com o balão. Atualizar os balões demo.
3. **Pool de balões:** `BalloonPool` (singleton leve ou estático) com 8 balões pré-instanciados, reutilizados. Se o jogador inflar o 9º, o mais antigo encolhe e estoura suave (sem susto). Zero `Instantiate`/`Destroy` por inflada, zero GC por frame.
4. **Regras de estouro (menos frustração, mais jogabilidade):**
   - Mão direta encostando em baixa velocidade: **empurra/bate** o balão (impulso proporcional à velocidade da mão), sem estourar. Isso vira um "joguinho de manter no ar" bom para reabilitação.
   - Gatilho (Select) mirando com o laser, ou com a mão direta: **estoura** com háptico e partícula (mantém o exercício de alcance).
   - Colisão com mesa/parede/chão/outro balão: **quica suave** (PhysicsMaterial com bounciness ~0.5 e atrito baixo). Só estoura se a velocidade relativa for alta (> ~2,5 m/s) ou ao tocar algo marcado como afiado.
   - Imunidade inicial: `Physics.IgnoreCollision` entre balão recém-solto, a pistola e a mão que segurou, por ~0,4 s.
   - Balão que chega ao teto (2,85 m) fica balançando perto do teto, sem travar e sem atravessar.
   - Vida útil: após ~60 s o balão murcha devagar e some (encolhe), para a sala não encher. Sem som alto.
5. **Inflar com mais "sensação":** pulso elástico (squash/stretch) durante a inflação; o ponteiro do manômetro (`Pressure_Gauge_Needle`) acompanha o progresso; tremor leve perto do máximo; háptico crescente (já existe). **Ao chegar no máximo, solta sozinho** (não estoura na mão do paciente).
6. **Física de voo:** manter o empuxo suave, mas adicionar leve rotação/oscilação, resistência do ar proporcional à escala e deriva lateral suave. A cordinha: manter `LineRenderer` com 6 nós, melhorar o atraso/balanço (2-3 iterações de restrição de distância) e puxar o nó pela velocidade. Sem custo por frame fora do alcance (`sqrMagnitude` ao jogador > raio: pular o update da corda).
7. **Conformidade VR:** remover `mr.material` (usar `sharedMaterial` ou `MaterialPropertyBlock`), zero `GetComponent` em `Update/FixedUpdate`, cache no `Awake`, sem `FindObjectsOfType`, `CollisionDetectionMode` Discrete (ContinuousDynamic só em quem o jogador segura), qualquer `Light` com `LightShadows.None`, `rb.Sleep()` quando parado no teto. Nenhum `LoadScene`, nenhum `// TODO`.
8. Compilar via `unity recompile --timeout 300` e conferir o `Editor.log` (sem `error CS`).

### `[D]` Prefab, cena e capturas (Opus) — `Assets/Editor/AutoSetup/Task07_*.cs`
1. **Prefab da pistola (correção dos 180°):** sem mexer no FBX:
   - Criar o filho `Visual_Model` com rotação `(0, 180, 0)` e mover o `Muzzle_Socket` para `+Z` (≈ `(0, 0.04, +0.155)`, rotação identidade), de modo que a raiz tenha **+Z = direção do cano**. Espelhar em Z os BoxColliders da raiz que ficarem fora do modelo.
   - Criar `Grip_Attach` (filho da raiz) no centro da empunhadura (`Gun_Grip`, ~−16° de inclinação) e ligar em `m_AttachTransform`; ajustar o pitch de forma que, com o controle na posição natural, o cano fique levemente para cima. Fazer a mesma coisa para a mão esquerda e direita (não precisa espelhar se o attach for simétrico).
   - Validação numérica no editor: `Vector3.Dot(muzzle.forward, grip_attach.forward) > 0.95` e o bico fica à frente do ponto de pegada. Renderizar offscreen um "boneco de mão" (3 eixos) segurando a pistola, vista lateral e de cima, e confirmar visualmente que o cano aponta para a frente.
2. **Cena `Menu_Principal`:** reposicionar a pistola e o suporte na bancada (`counter_3_a (1)`), com o cano apontando **para fora do móvel** (para a sala), aterrada no tampo (gap ≤ 0,005 m), folga ≥ 0,25 m de qualquer vizinho (`pairwiseClearances[]` no JSON). Os balões demo: reescalar para a escala nova, manter abaixo do teto e sem interpenetrar luminária ou parede.
3. Rodar `SceneDuplicateAndOriginPurger` antes e depois: zero duplicatas (1 pistola, 1 suporte, quantidade planejada de balões demo), `(0,0,0)` limpo.
4. **Teste em Play Mode (batchmode, sem GUI)** usando a "mão de teste" `ScriptedTestInteractor` (ver `ForestPlayModeTests` como referência): pegar a pistola, Activate por 1,5 s, soltar o Activate, verificar que o balão sobe no sentido do cano, tem o tamanho esperado (altura 0,35-0,45 m), que o 9º balão faz o mais antigo estourar, que bater com a mão não estoura e que o laser estoura. Salvar `TASK-07_PlayModeTest.txt`.
5. Capturas 1920×1080 offscreen: `TASK-07_View1_Front.png`, `View2_Side_Grounding.png`, `View3_Closeup.png`, `TASK-07_Gun_In_Hand.png` (cano para frente), `TASK-07_Balloon_Inflating.png`, `TASK-07_Balloons_Floating_Room.png`, `TASK-07_Balloon_Size_Compare.png` (antes/depois + régua de 0,30 m). Olhar cada PNG com a ferramenta Read antes de entregar.
6. Gerar `TASK-07_SpatialAudit.json` (`hasOverlap`, `groundGapMeters`, `pairwiseClearances[]`, `objectsAtOriginZero: []`, `duplicateObjectsFound: []`, contagens) e `TASK-07_BuilderResult.txt`. Salvar cena e prefabs (`AssetDatabase.SaveAssets()` e `EditorSceneManager.SaveOpenScenes()`).

### `[E]` Auditoria (Sonnet na 1ª passada; FAIL → retrabalho em Opus)
Reprovação imediata se qualquer item falhar:
- A pistola, na captura "em mão", tem o cano apontando para a frente (e o teste numérico do `[D]` confirma). O balão sai na direção do cano.
- Balão final com altura de 0,35 a 0,45 m e proporção visual correta contra a pistola; cordinha com comprimento coerente.
- Pool de 8 funcionando (teste de Play Mode); sem `Instantiate`/`Destroy` por inflada; sem `renderer.material` e sem `GetComponent` em loops; nada de `LoadScene`.
- Bater com a mão não estoura; laser/gatilho estoura; colisão suave quica; teto sem travar.
- Pico de áudio ≤ −6 dBFS; polycount do balão dentro de 800-1800 e da pistola dentro do orçamento.
- Pistola aterrada, folga ≥ 0,25 m dos vizinhos, `(0,0,0)` limpo, zero duplicatas, balões demo dentro da sala.
- Escrever `.agents/visual_qa/TASK-07_AuditVerdict.md` com PASS ou FAIL e as ordens de `RE-RUN`.

## 3. Critérios de aceite (resumo)
1. Ao pegar a pistola, o cano aponta para frente, nas duas mãos.
2. Balão inflado com ~0,40 m de altura, cordinha proporcional, cores vivas e brilho bom.
3. Jogabilidade: inflar por tempo (tamanho variável), soltar automático no máximo, bater para manter no ar, estourar com laser/gatilho, pool de 8, murchar após ~60 s.
4. Todas as regras de VR da Seção 3 do `CLAUDE.md` cumpridas.
5. Matriz da Seção 4: TASK-07 `[x]` nos 5 passos e `COMPLETED*` (falta apenas o playtest no óculos, que o usuário faz).

## 4. Pendências para registrar sem resolver aqui
- Rótulos TMP gigantes dos troféus (TASK-16).
- `ArcadeBallSpawner` com collider 0,058 m abaixo da malha (TASK-08).
- `AtmosphereLeverInteractable.cs:393` usa `ledRenderer.material` em runtime (TASK-09).
