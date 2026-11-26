# TASK-07 — Spec do Scout `[A]` (Pistola de Balões) — 2026-11-25

> Escrito pelo Subagente `[A]` (somente leitura). Substitui a spec de 10/11.
> **Metodologia e limite:** a medição na Unity (batchmode) foi tentada uma vez (`TASK-07_Scout_unity.log`) e **abortou por erro de compilação de outro agente**: `Task07_SceneAndPrefabBuilder.cs(493): ForceInflateOnMuzzle` deixou de existir em `BalloonInflaterGun.cs` (reescrito às 17:00 por `[C]`). Por isso as medidas abaixo vêm de: (a) YAML do prefab e da cena; (b) parâmetros do `generate_task07_models.py` (bounds do FBX recalculados em Python, convenção Blender→Unity `(x,y,z) → (-x, z, -y)`, validada contra `Helium_Canister` e `Muzzle_Socket` do prefab); (c) `TASK-06_FurnitureScout.txt` / `TASK-06_SpatialAudit.json` (medidos na Unity em 11/11 e conferidos com o YAML atual da cena: posições idênticas); (d) `.wav` lidos com Python. **Não medido:** altura exata do teto (ver 3.4) e o resultado do `ModelPolycountAudit` para o balão novo (ver 6). Linhas de script citadas são do **HEAD** (`git show HEAD:...`), pois a árvore de trabalho já está sendo alterada por `[B]`/`[C]`/`[D]` em paralelo.

## 0. Estado da árvore de trabalho (já alterada durante o Scout)
| Arquivo | Estado | Observação |
|---|---|---|
| `generate_task07_models.py`, `InflatableBalloon.fbx` | modificados 16:56 | balão v3 (19 anéis). Pistola **não** alterada (FBX de 10/11). |
| `Audio/Generated/Task07/*.wav` | modificados 17:01 | `balloon_pop_loud.wav` virou `balloon_pop_soft.wav` (rename). **Prefab `InflatableBalloon` ainda aponta o clipe pelo GUID** (meta renomeada junto, GUID `5e5beb7b…` deve se manter; conferir). |
| `BalloonInflaterGun.cs`, `FloatingBalloon.cs`, `BalloonPool.cs` (novo) | reescritos 16:59-17:00 | `[C]` em andamento; **builder antigo não compila** com eles. |
| `Task07_SceneAndPrefabBuilder.cs` | reescrito 17:07 (48 KB) | `[D]` em andamento. |
| Prefabs `BalloonGunDispenser`/`InflatableBalloon`, cena `Menu_Principal` | intactos (10/11 e 16/11) | base de todas as medidas abaixo. |

## 1. Diagnóstico confirmado
**1.1 Orientação (180°) — CONFIRMADA e com um efeito a mais.**
- Prefab: `Muzzle_Socket` em `lp (0, 0.04, -0.155)`, rot identidade; `Nozzle_Rim` em `z=-0.15`, `Gun_Barrel` em `z=-0.095`. Cano = **−Z local**. Raiz sem `m_AttachTransform` (fileID 0), `m_MatchAttachRotation: 1`, `m_TrackRotation: 1`, `m_MovementType: 0` (VelocityTracking), `m_ThrowOnDetach: 1`.
- Consequência dupla: (i) o +Z da mão alinha com o +Z do objeto = **traseira** da pistola, então o cano aponta ao jogador; (ii) `BalloonInflaterGun.cs:130` põe o balão com `Euler(90,0,0)` no socket (cresce para +Z do socket = **para trás, atravessando o corpo e indo à mão**) e `:238` lança em `muzzleSocket.forward` (+Z = para trás). Quando o cano passar a ser +Z, as linhas 130 e 238 ficam corretas sem mudança (Rx(90°) leva o +Y do balão — nó embaixo, ápice em cima — para +Z).
- **Divergência com o plano (1):** o prefab **já tem** o filho `Visual_Model` (rot identidade, pai de TODOS os 24 filhos, inclusive `Muzzle_Socket`). Não precisa criar; basta girar `Visual_Model` para `(0,180,0)`. **Cuidado:** `Muzzle_Socket` é filho de `Visual_Model`. Girando o pai, a posição do socket vira `(0,0.04,+0.155)` na raiz, mas o `forward` do socket fica **−Z da raiz** (contra a regra "cano = +Z"). Solução: reparentar `Muzzle_Socket` para a **raiz** com `lp (0,0.04,+0.155)` e rot identidade (ou manter sob `Visual_Model` com `localEuler (0,180,0)` e `lp (0,0.04,-0.155)`). O código usa `transform.Find("Muzzle_Socket")` (HEAD :64) e o campo serializado `muzzleSocket` (fileID 864729066545159694): se reparentar, o fileID se preserva só se mover o mesmo objeto (não recriar).
- **Divergência com o plano (2):** o plano manda "espelhar em Z os BoxColliders que ficarem fora do modelo". O `BoxCollider` da raiz **já foi autorado para o referencial invertido** (ver seção 2); depois do giro ele passa a cobrir quase o modelo. **Não espelhar** de novo; apenas reajustar (receita na seção 7).

**1.2 Tamanho do balão — CONFIRMADO.** FBX em escala 1: **0,30 m de altura × 0,24 m de largura** (pivô no nó, `y=0`; ápice `y=0.30`). `maxBalloonScale = 0.30` (HEAD `:35`, usado como `localScale` em `:172`) ⇒ balão final **0,09 m × 0,072 m**. Inicial `minBalloonScale 0.05` (`:32`, aplicado em `:131`). Balões demo da cena: escalas 0.27/0.29/0.28 ⇒ 0,081/0,087/0,084 m de altura (bate com `Demo_Balloon_*` bounds 0,081 do scout de 11/11). `stringLength 0.40` (`FloatingBalloon.cs:33`) não escala com o balão.

**1.3 Pool — AUSENTE no HEAD.** `Instantiate` por inflada (`BalloonInflaterGun.cs:127`), `Destroy` em micro-balão (`:220`) e no estouro (`FloatingBalloon.cs:291`, `:277` destrói o sistema de partículas depois de `SetParent(null)`). Árvore de trabalho já tem `BalloonPool.cs` (revisar na `[E]`).

**1.4 Material — CONFIRMADO.** `BalloonInflaterGun.cs:289` `mr.material = mat` (caminho de fallback sem `FloatingBalloon`; o caminho normal usa `sharedMaterial` em `FloatingBalloon.cs:302`). Atenção: **o projeto é Built-in RP** (`GraphicsSettings.m_CustomRenderPipeline: {fileID: 0}`; os 7+8 materiais usam o shader `Standard`, `fileID 46`), não URP como diz o briefing; `m_EnableInstancingVariants: 1` presente em ruby/string/body/accent. `_Glossiness` ruby = 0,86 (já brilhante), `_Metallic` 0,04.

**1.5 Estouro (HEAD).** Pop em qualquer `OnSelectEntered` (`FloatingBalloon.cs:196-201`, o gatilho/grab da mão estoura), em `OnActivated` (`:189-194`), e em **qualquer colisão** de trigger/collision após `launchGracePeriod 0.25` (`:238-241`) — mesa, chão, parede e outro balão estouram. Detecção de "mão" por nome (`:229` `ToLowerInvariant()` aloca string a cada colisão). Imunidade só contra `BalloonInflaterGun` (`:220-226`, `GetComponentInParent` no callback), nunca contra a mão que soltou.

**1.6 Input.** Infla no **Activate** (`BalloonInflaterGun.cs:84-92` `OnActivated`; solta em `:94-102` `OnDeactivated`; também solta em `OnSelectExited` `:104-113`). Coerente com a memória do projeto (gatilho = select/pegar; botão lateral = Activate). Manter.

## 2. Medidas do prefab `BalloonGunDispenser.prefab`
Hierarquia: `BalloonGunDispenser` (raiz, `lp 0, rot identidade, scale 1`) → `Visual_Model` (identidade) → **24 filhos planos** (todos com MeshFilter+MeshRenderer, exceto `Muzzle_Socket` que é só Transform).

Componentes da raiz: `BoxCollider` (não trigger), `Rigidbody`, `AudioSource`, `BalloonInflaterGun` (: `ErgonomicGrabInteractable` : `XRGrabInteractable`).
- `BoxCollider`: `size (0.09, 0.16, 0.24)`, `center (0.01, 0.01, 0.04)` → ocupa **x[-0.035, 0.055], y[-0.07, 0.09], z[-0.08, 0.16]**.
- `Rigidbody`: massa 0,65; gravidade ON; `isKinematic 0`; `m_Interpolate 1`; **`m_CollisionDetection 1` (Continuous)** — viola a regra Seção 3 (Discrete; ContinuousDynamic só segurada). Na cena há override `m_IsKinematic 0` e `m_ForceGravityOnDetach 1`.
- `XRGrabInteractable`: `m_AttachTransform 0`, `m_SecondaryAttachTransform 0`, `m_UseDynamicAttach 0`, `matchAttachPosition/Rotation 1`, `trackPosition/Rotation 1`, `smooth*` 1/10, `m_ThrowOnDetach 1`, `m_ThrowVelocityScale 1.5`, `m_InteractionLayers 1` (só layer Default; **diverge** da regra `interactionLayers = ~0` do `LaserSwingThrow`, mas pistola não precisa), `InteractionLayerMask 0xFFFFFFFF`.
- Ergonômicos: `ergonomicAttachEaseInTime 0.3`, `enableAutoRespawn 1`, `respawnDelay 3`, `respawnFloorThresholdY 0.4`, `respawnMaxDistance 5`, `customHomeAnchor` vazio, `alwaysDynamicAfterRelease` padrão true (`ErgonomicGrabInteractable.cs:47`).
- Gun: `inflateDuration 1.5`, `launchVelocity 0.85`, `min/maxBalloonScale 0.05/0.30`, 7 materiais de balão, clipes `inflate_stretch` e `release_whoosh`, `AudioSource` 3D (min 0.3 / max 10).

**Bounds reais do modelo (referencial atual, antes de girar):** x[-0.052, +0.025], y[-0.100, +0.075], z[-0.154, +0.053] ⇒ **0,077 × 0,176 × 0,207 m**.

| Filho | lp (Unity, local da raiz) | rot (euler X) | Observação |
|---|---|---|---|
| `Muzzle_Socket` | (0, 0.040, **−0.155**) | 0 | ponta do cano; eixo do cano = **−Z** |
| `Nozzle_Rim` / `Gun_Nozzle` / `Gun_Barrel` | z = −0.150 / −0.135 / −0.095 | 90° (cilindros) | cano ao longo de Z |
| `Gun_Grip` | (0, −0.045, **+0.015**) | **−16°** | empunhadura; topo inclinado p/ o cano, base recuada p/ trás (ergonomia correta); escala do cubo 0,030×0,036×0,105 |
| `Grip_Pad_0/1/2` | (0,−0.025/−0.047/−0.069, 0.004/0.010/0.016) | −16° | 3 almofadas ciano |
| `Gun_Trigger` / `Trigger_Guard` | (0,−0.008,−0.018) / (0,−0.012,−0.022) | +22° / Z90 | à frente da empunhadura (−Z) |
| `Gun_Receiver` | (0, 0.038, −0.015) | 0 | caixa 0,046×0,048×0,130 (z de −0.080 a +0.050) |
| `Receiver_Fin_0..3` | (0, 0.064, 0.025/0.003/−0.019/−0.041) | 0 | aletas |
| `Helium_Canister` | (−0.036, 0.032, −0.010) | 90° | lado **−X** da raiz |
| `Pressure_Gauge_Bezel/Face/Needle` | (0.012,0.066/0.069/0.071, −0.020/−0.021/−0.022) | −25° (needle também Y/Z) | manômetro no topo, lado +X |

`Pressure_Gauge_Needle`: `lp (0.010, 0.071, −0.022)`, `localRot (−0.1999, −0.3736, −0.0828, 0.9020)` (≈ −25° X + 45° Z no Blender). Está no mesmo nível que as outras peças (sem pivô próprio); para animar o ponteiro por script, o `[C]` precisa de um pivô: criar um filho vazio `Gauge_Needle_Pivot` em `lp (0.012, 0.069, −0.021)` (centro do `Face`) e reparentar o ponteiro (o ponteiro é um cubo 0,002×0,008×0,001 centrado em si, o que rodaria em torno do próprio centro).

**Collider × malha (achado importante):** o `BoxCollider` está **espelhado em Z/X** em relação à malha atual — malha z[-0.154, +0.053], collider z[-0.08, +0.16]. Efeitos hoje: cano+bocal (z −0.154…−0.08) **fora do collider**; 0,107 m de collider vazio atrás; base da empunhadura (y −0.100…−0.070) **fora do collider**. Na cena, a pistola é `isKinematic 0` e foi colocada com gap 0,002 m: `y=1.0515` ⇒ base do collider em `1.0515−0.07 = 0.9815`, ou seja **0,0325 m acima do tampo (0,949)** — a pistola cai 3 cm no Play e a empunhadura penetra o tampo ~3 cm.
Depois de girar `Visual_Model` 180°, a malha passa a ocupar x[−0.025,+0.052], z[−0.053,+0.154]: o collider atual passa a cobrir quase tudo (autorado para esse referencial). Ajuste fino na seção 7.

**Cano aponta para:** **−Z local** (confirmado por `Muzzle_Socket`, `Gun_Barrel`, `Nozzle_*`).

**Suporte (stand):** **NÃO EXISTE.** Não há `*Stand*` nem `*Balloon*` além de gun/balloon em `Assets/Prefabs/Interactive` nem na cena; `BalloonGunStand.fbx` previsto na spec antiga nunca foi gerado. O plano fala em "suporte"; ou o `[B]` cria um (≤ 300 tris, só visual, sem colisor dinâmico) ou o `[D]` registra "N/A" e assenta a pistola sobre o tampo.

## 3. Prefab `InflatableBalloon.prefab` e cena `Menu_Principal`
### 3.1 Prefab do balão (raiz `InflatableBalloon`, escala 1)
- Filhos: `Visual_Model` (id.) → `Balloon_Body` (malha), `Knot_Bead` (lp y 0.006 — no prefab antigo; v3 do FBX usa 0.010), `String_Anchor` (0,0,0). `Burst_Particles` (filho da raiz, `lp (0,0.18,0)`, `maxNumParticles 1000`, `looping 0`, `playOnAwake 0`, cast/receive shadows 0).
- Raiz: `SphereCollider` **raio 0,13, centro (0, 0.18, 0)** (cobre y 0,05…0,31; largura 0,26; deve escalar junto — com `scale 1.4`: raio 0,18); `Rigidbody` massa 0,012, drag 1,35, angDrag 1,6, `useGravity 0`, **`m_CollisionDetection 2` (ContinuousDynamic permanente)** e `interpolate 1` — viola a Seção 3 (usar Discrete); `LineRenderer` (corda, 6 nós, world space); `AudioSource` 3D; `FloatingBalloon` (: `XRSimpleInteractable`) com `ceilingHeightLimit 2.85`, `buoyancy 0.22`, `maxAscent 0.45`, `wind 0.08`, `stringLength 0.40`, `launchGracePeriod 0.25`.
- `Balloon_Body`/`Knot_Bead`/`LineRenderer`: `m_CastShadows 1`, `m_ReceiveShadows 1` (projetam sombra; para 8 balões convém `CastShadows Off` no corpo e na corda).
- Tamanho em escala 1: 0,30 × 0,24 m; **pivô no nó**, então "teto" aplicado ao pivô deixa o corpo furar o teto: com escala 1,4 o ápice fica 0,42 m acima do pivô (ver 3.4).

### 3.2 Cena `Assets/Scenes/Menu_Principal.unity` (YAML atual = scout de 11/11)
| Objeto | Posição mundo | Rotação | Escala |
|---|---|---|---|
| `BalloonGunDispenser_Instance` (PrefabInstance, raiz da cena) | **(4.9896, 1.0515, 7.6208)** | rotY **145°** (quat y −0.9537, w −0.3007) | 1 |
| `Demo_Balloon_Red` | (4.7404, 1.55, 7.1703) | id. | 0.27 |
| `Demo_Balloon_Cyan` | (5.1907, 1.75, 7.0212) | id. | 0.29 |
| `Demo_Balloon_Yellow` | (5.4402, 1.90, 7.2716) | id. | 0.28 |
| `counter_3_a (1)` (filho de `Building`) | (5.170, 0, 7.443), rotY ≈ 359.6° | — | tampo y **0,949**, AABB x[4.667, 5.673] z[7.058, 7.828] |

- **Para onde a pistola mira hoje:** rotY 145° ⇒ cano (−Z local) aponta para mundo `(−0.574, 0, +0.819)`, isto é, **para a parede do fundo (z ≈ 8,00; `wall_standard_door_frame (1)` em z 7,888-8,112)**, a ~0,25 m do bico. Pelo plano, o cano deve apontar **para a sala** (−Z mundo).
- Contagens (YAML + scouts): **1** pistola, **0** suportes, **3** balões demo (`Demo_Balloon_*`), `InflatingBalloon_Preview` ausente, **sem duplicatas**. Nada a < 1 m de (0,0,0) relacionado à TASK-07 (o `TASK-06_SpatialAudit` registra `(0,0,0) r=1.0 m limpo`; objetos da raiz em (0,0,0) são só gerenciadores sem renderer: `Gerenciador_HUD`, `GerenciadorPause`, `EventSystem`, áudio ambiente).
- Folga hoje (TASK-06): pistola ↔ vizinhos **0,062 m** (< 0,25 exigidos).

### 3.3 Vizinhos num raio de 0,6 m da pistola (Mesa `counter_3_a (1)`)
Fonte: `TASK-06_FurnitureScout.txt` ("ITEMS ABOVE FURNITURE") + YAML. Pistola em (4.99, 7.62), AABB atual ≈ x[4.85,5.13] z[7.48,7.76] (girada 145°).
| Vizinho | AABB (min→max) | Folga aprox. à pistola |
|---|---|---|
| `pc_mouse_pad` | x 4.793-5.084, y 0.949-0.971, z 7.077-7.396 | **~0,08 m** (em Z) |
| `pc_mouse` | x 4.861-4.975, y 0.963-1.006, z 7.209-7.371 | ~0,11 m |
| `table_lamp (1)` (+`Top`) | x 5.281-5.528, y 0.947-1.318, z 7.297-7.542 | ~0,15 m (x) |
| Balões demo (ar) | ver 3.2 | acima da pistola ≥ 0,5 m |
| Parede do fundo | z ≈ 7,888 | 0,13 m atrás do bico |

### 3.4 Teto
**Não medido** (a Unity não pôde rodar). Dados: batentes das portas chegam a y = **3,000** (`wall_standard_door_frame`, max y 3.000); `Building` bounds y[−0.038, 6.038] (a estrutura tem 6 m de altura de AABB, provavelmente 2 pavimentos/laje); nenhum objeto se chama "ceiling/roof/teto" (grep no YAML e nos scouts). O `ceilingHeightLimit = 2.85` do código é aplicado ao **pivô (nó)**. Com `maxScale 1.4` o ápice fica em `2.85 + 0.42 = 3.27 m` ⇒ **atravessa um teto a 3,0 m**. Pedir ao `[D]`/`[E]` um raycast ↑ em ~5 pontos da bancada (a ferramenta `SpatialAuditUtility` pode incluir `ceilingY`). Regra recomendada: `pivotMaxY = ceilingY − 0.30·scale − 0.10` (com ceilingY=3.0 e scale 1.4 ⇒ **2,48 m**); se o teto real for > 3,3 m, o 2,85 original serve.

### 3.5 Móveis candidatos (tampo 0,949, da `TASK-06_FurnitureScout`)
- `counter_3_a (1)`: ocupada (mouse pad, mouse, lâmpada) — **inviável para folga ≥ 0,25 m** (restam ≤ 0,18 m de profundidade entre o mouse pad e a parede; o `TASK-06` já ficou com 0,062 m).
- **`counter_3_c (2)`**: pos (4.170, 0, 7.436), rotY ≈ 359.6°, AABB x[3.667, 4.673] z[7.051, 7.820], tampo y 0,949. **Sem itens** (não aparece em "ITEMS ABOVE FURNITURE"; raios para baixo a partir do centro e dos 4 lados só batem no próprio móvel). É contíguo (x 4.673 = início de `counter_3_a (1)`) e encostado na mesma parede. Distância ao mouse pad mais próximo: > 0,45 m. **Candidato recomendado.**
- Outros livres (ver scout): `counter_3_c`/`(1)` ocupados por PC; `counter_2_b` tem TV+deco; `drawer_simple` (tampo 0,918) é onde está o `ArcadeBallSpawner`.

## 4. Áudio — `Assets/Audio/Generated/Task07/*.wav` (Python `wave`/`array`)
| Arquivo | Dur. | SR/ch/bits | Pico dBFS | RMS dBFS | Regra ≤ −6 |
|---|---|---|---|---|---|
| **HEAD** `balloon_inflate_stretch.wav` | 1,600 s | 44100/1/16 | **−0,54** | −12,16 | **FALHA** |
| **HEAD** `balloon_pop_loud.wav` | 0,500 s | 44100/1/16 | **−0,54** | −25,60 | **FALHA** |
| **HEAD** `balloon_release_whoosh.wav` | 0,450 s | 44100/1/16 | **−0,54** | −16,96 | **FALHA** |
| agora `balloon_inflate_stretch.wav` | 1,600 s | 44100/1/16 | −10,00 | −18,61 | OK |
| agora `balloon_pop_soft.wav` (renomeado) | 0,280 s | 44100/1/16 | −8,00 | −22,26 | OK |
| agora `balloon_release_whoosh.wav` | 0,450 s | 44100/1/16 | −9,00 | −26,74 | OK |
`[B]` já corrigiu (17:01). Pendências: confirmar que o prefab do balão e o `AudioSource` do builder referenciam `balloon_pop_soft.wav` (GUID preservado pela rename com `.meta`), e que o `AudioSource.volume` + `PlayOneShot(..., 1.0f)` (`FloatingBalloon.cs:259/258`, `BalloonInflaterGun.cs:259`) não levantam o pico acima de −6 dBFS efetivo (sem ganho extra).

## 5. Violações VR e de jogabilidade (HEAD, arquivo:linha)
| # | Arquivo:linha | Violação | Regra |
|---|---|---|---|
| V1 | `BalloonInflaterGun.cs:127`, `:300`, `:320` | `Instantiate` por inflada/demo | pool (plano) |
| V2 | `BalloonInflaterGun.cs:220`; `FloatingBalloon.cs:291` (`Destroy(gameObject,0.8f)`), `:277` (`Destroy(particles,2f)`) | `Destroy` por balão e por estouro | pool |
| V3 | `BalloonInflaterGun.cs:289` | `mr.material = mat` (duplica material) | Seção 3.1 |
| V4 | `BalloonInflaterGun.cs:133,136,139,142,231,234,248,280,288,304,326,329,332,335`; `FloatingBalloon.cs:65-67,71,109,222,299,305` | `GetComponent/GetComponentInChildren/GetComponentInParent` por inflada e **dentro de callback de colisão** (`:222`) | cache no `Awake`/pool |
| V5 | `FloatingBalloon.cs:229` | `ToLowerInvariant()` + 6× `Contains` por colisão (GC) | Zero-GC |
| V6 | `FloatingBalloon.cs:158-163,185` | `SetPositions` da corda **todo `LateUpdate` de cada balão**, sem throttle por distância | `sqrMagnitude` ao jogador |
| V7 | `FloatingBalloon.cs:122-156` | `FixedUpdate` roda `Slerp`+Perlin em cada balão ativo sem teto de distância; não dorme no teto | `rb.Sleep()` no teto |
| V8 | Prefab balão `m_CollisionDetection 2`; Prefab pistola `m_CollisionDetection 1` | Continuous* permanente | Discrete; ContinuousDynamic só segurado |
| V9 | `FloatingBalloon.cs:238-241`, `:196-201` | estoura em qualquer sólido, na mão, no select | spec: quicar/empurrar; laser/gatilho estoura |
| V10 | `FloatingBalloon.cs:26`+`:130` | teto aplicado ao pivô, corpo fura teto com escala nova | 3.4 |
| V11 | Prefabs (corpo/corda) `m_CastShadows 1` | 8 balões lançam sombra | Seção 3.4 (sombras extra) |
| V12 | `FloatingBalloon.cs:277` | partículas viram `SetParent(null)` e são destruídas ⇒ o prefab perde o `Burst_Particles` | pool: não reparentar |
| V13 | `FloatingBalloon` sem expiração | nunca some | plano: murchar em ~60 s |
`LoadScene`: **nenhum** no código da TASK-07 (grep). `Light`/`LightShadows`: **nenhuma** `Light` no prefab nem nos scripts (módulo `LightsModule` das partículas presente mas desligado; conferir `m_Enabled`/`maxLights 20` no `[D]`). `FindObjectsOfType`: nenhum. `Update`: nenhum (usa `FixedUpdate`/`LateUpdate`/corrotina). Corrotina `InflateRoutine` (`:162-190`) sem alocação, háptico a cada 8 frames: OK.

## 6. Orçamento VR (triângulos)
| Modelo | Tris | Cálculo/fonte | Orçamento | Status |
|---|---|---|---|---|
| `BalloonGunDispenser.fbx` | **1740** | `MODELS_PolycountAudit.txt` (16/11; FBX inalterado desde 10/11) | ≤ 1800 (médio) | OK (**o auditor marca `!!` por bug**: `ModelPolycountAudit.cs:61` trata qualquer nome com "ball" — "Balloon" — como bola, limite 1100) |
| `InflatableBalloon.fbx` HEAD | 560 | audit + fórmula (12 anéis ×20 seg: 11·20·2+40+knot 80) | 800-1800 | (era OK pelo limite errado de 1100) |
| `InflatableBalloon.fbx` v3 (hoje) | **≈ 820** | 17 faixas·20·2 = 680 + 2 tampas·20 + `Knot_Bead` 10×6 ≈ 100 | 800-1800 | OK, margem pequena; **a re-auditar** (não medido na Unity) |
Sugestão ao `[B]`/`[C]`: corrigir `ModelPolycountAudit.cs:61` para excluir "balloon" (`!lower.Contains("balloon")`), e se o balão chegar a ~1000 tris, o pool de 8 = ~8000 tris de balão: ainda aceitável, mas vale lembrar da meta de 90 FPS.

## 7. Recomendações numéricas
### 7.1 `[B]` (Blender)
- Balão: mantido 0,30 × 0,24 m, pivô no nó (compatível com o código antigo e novo). Se alterar os anéis, manter tris ≤ 1.000 e normais externas. Sem partes soltas. Material Standard: `_Glossiness` 0,86-0,9, `_Metallic` ≤ 0,1, `enableInstancing` ligado (já está). Cast/Receive shadows desligados nos prefabs.
- Pistola: **não regenerar o FBX** (a orientação é corrigida no prefab). Opcional: `Gauge_Needle_Pivot` e um suporte ≤ 300 tris.
- Audio: já OK (seção 4); conferir `RMS`/picos após qualquer novo ganho no Unity.

### 7.2 `[C]` (C#)
- Cano = **+Z da raiz**; `muzzleSocket.forward` = direção de lançamento; balão nasce com `localRotation Euler(90,0,0)` e `localPosition 0` no socket (linhas 130/238 do HEAD continuam válidas).
- Escala real: `minScale ≈ 0.15`, `maxScale ≈ 1.4` (balão final 0,42 × 0,34 m; alvo do plano 0,35-0,45 de altura ⇒ **escala 1,17-1,50**; ±8% aleatório ⇒ manter o teto em 1,40·1,08 = 1,51 no pior caso = 0,45 m). `stringLength = 0.40·scale` (0,16-0,56 m), larguras da corda `0.0035/0.0020 · scale`. `SphereCollider` radius 0,13·scale (scale 1,4 ⇒ 0,18; centro y 0,18·scale).
- `ceilingHeightLimit` = f(teto medido, escala) (seção 3.4). Pool: 8 balões (+1 reserva, como já foi escrito em `BalloonPool.cs`); o mais antigo encolhe/estoura suave.
- `rb.collisionDetectionMode = Discrete` por padrão; `ContinuousDynamic` somente na pistola enquanto segurada e 3 s após soltar no chão (Seção 3.2).
- Ponteiro do manômetro: rotacionar `Gauge_Needle_Pivot` (criar no `[D]`), zero `GetComponent` por frame.
- `Physics.IgnoreCollision(balão, pistola/mão, true)` por ~0,4 s e restaurar (usar `Physics.IgnoreCollision(..., false)` ao expirar; sem corrotina por balão: um `float ignoreUntil` no `FloatingBalloon.FixedUpdate`).

### 7.3 `[D]` (prefab e cena)
**Prefab da pistola** (editar o asset existente com `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset`; **não deletar e recriar** como o builder de 10/11 faz em `Task07_SceneAndPrefabBuilder.cs:~40`, para preservar GUID `1bb9291c…` e o PrefabInstance da cena):
1. `Visual_Model.localRotation = (0,180,0)`.
2. `Muzzle_Socket`: reparentar para a raiz, `lp (0, 0.040, +0.155)`, rot identidade (ou filho de `Visual_Model` com `lp (0,0.04,−0.155)` e `localEuler (0,180,0)`; ambos dão `forward = +Z` raiz e posição de mundo `+0.155`).
3. `Grip_Attach` (novo filho da raiz), ligar em `m_AttachTransform` (e `m_SecondaryAttachTransform` vazio):
   - posição: centro da empunhadura em referencial raiz **depois do giro**: `lp (0, −0.045, −0.015)` (a posição antiga `+0.015` em Z inverte; X=0 não muda). A `Gun_Grip` fica em `lp (0,−0.045,−0.015)` com euler **+16° em X** (a base da empunhadura recua para −Z, na direção da mão).
   - rotação: `localEuler (+10°, 0, 0)` ⇒ cano **~10° acima** do eixo Z da mão (em Unity, Rx(θ) leva +Z a (0,−sinθ,cosθ); girar o attach +10° pitcha o objeto −10°, i.e., cano para cima). Teste numérico: `Vector3.Dot(muzzle.forward, gripAttach.forward) = cos(10°) = 0,985 > 0,95` ✓ e `muzzle.position` à frente: `(muzzle.position − gripAttach.position)·gripAttach.forward = 0,155 + 0,015 ≈ 0,17 m > 0`. Se o playtest achar o cano alto/baixo demais, variar o pitch entre 0° e +16°; não exceder +20°.
4. `BoxCollider` raiz (referencial pós-giro): malha vira x[−0.025, +0.052], y[−0.100, +0.075], z[−0.053, +0.154] ⇒ **`center (0.0135, −0.0125, 0.0505)`, `size (0.080, 0.180, 0.210)`** (substitui `(0.01,0.01,0.04)`/`(0.09,0.16,0.24)`). Conferir com os AABB do `SpatialAuditUtility`; **não** espelhar. Manter 1 collider primitivo (zero `MeshCollider`).
5. Rigidbody: `collisionDetectionMode = Discrete`, `interpolation` = `Interpolate` OK; `sleepThreshold` padrão.
6. Origem do prefab: base da empunhadura em `y = −0.100` da raiz ⇒ para apoiar no tampo (0,949): `y_raiz = 0,949 + 0,100 + 0,002 = 1,051`.
**Cena `Menu_Principal`** (Lei 2/3 e plano):
- Mover a pistola para **`counter_3_c (2)`**, posição alvo `(4.170, 1.051, 7.430)`, `rotY ≈ 175°` (cano −Z mundo, levemente para o centro da sala) — pistola pós-giro com `forward = (sin175°, 0, cos175°) = (0.087, 0, −0.996)`; bico em mundo ≈ `(4.18, 1.091, 7.276)` (0,15 m à frente da origem), 0,22 m de margem até a borda frontal do tampo (z 7.051) e 0,39 m até a parede (z 7.888→ ver). AABB estimado: 0,21 m (em Z) × 0,08 m (em X) ⇒ **x[4.13, 4.21], z[7.33, 7.53]**; folga ≥ 0,25 m: mouse pad (x 4.793) ≈ **0,58 m**; lâmpada ≈ 1,07 m. Folga às bordas do tampo ≥ 0,28 m. Se o suporte (opcional) existir, centralizar nele.
- Antes de mover, **conferir na Unity** que `counter_3_c (2)` continua vazio e que `counter_3_a (1)` não foi alterado pela limpeza manual do usuário (o scout é de 11/11; a cena foi limpa à mão em 16/11).
- Balões demo: manter 3 (`Demo_Balloon_Red/Cyan/Yellow`), escala nova **1,15-1,40** (altura 0,35-0,42 m), pivô (nó) **y ≥ 1,75** para a corda (0,16-0,56 m) ficar ≥ 1,2 m do chão e ≥ 0,5 m acima do tampo; **y_pivô ≤ ceilingY − 0,30·scale − 0,10**. Posições sugeridas sobre `counter_3_c (2)` / corredor logo à frente (x 3.7-4.6, z 6.4-7.0): `(3.95, 1.85, 6.85)`, `(4.40, 2.10, 6.60)`, `(4.65, 1.80, 7.05)` — todos a ≥ 0,45 m de qualquer parede e da pistola; **rodar `SceneDuplicateAndOriginPurger` antes/depois**; os demo devem ser `isKinematic` (ou repouso) para não flutuar até o teto.
- Capturas e JSON: incluir `ceilingY` e `balloonHeightMeters` (altura medida do balão final) no `TASK-07_SpatialAudit.json`; `pairwiseClearances[]` pistola↔{mouse pad, lâmpada, balões}.

### 7.4 `[E]` (auditoria)
Reprovar se: teste de `muzzle.forward·gripAttach.forward ≤ 0,95`; balão final fora de 0,35-0,45 m; qualquer `Instantiate/Destroy/GetComponent/renderer.material` no caminho de inflar; pico de áudio > −6 dBFS; `CollisionDetectionMode != Discrete` em repouso; pistola a < 0,25 m de vizinho; balão acima do teto; `ModelPolycountAudit` com falso positivo não corrigido (anotar, não reprovar a pistola por esse motivo).

## 8. Divergências com `TASK-07_Plano_Opus.md` (resumo)
1. `Visual_Model` **já existe** (identidade) e já contém `Muzzle_Socket`; não criar, apenas girar; e **reparentar/ajustar** `Muzzle_Socket` para que o `forward` seja +Z (ou ele fica apontando para trás).
2. O `BoxCollider` da raiz **não** deve ser espelhado: já está no referencial pós-giro; hoje é a malha que está "errada" em relação a ele. Ajuste dado na seção 7.3.
3. **Não há suporte (stand)** no projeto; "suporte" do plano é inexistente.
4. O projeto é **Built-in RP + Standard**, não URP.
5. O auditor de polígonos classifica pistola e balão como "bola" (limite 1100) por conter "ball" em "balloon"; o limite real (1800) está respeitado (pistola 1740).
6. O plano diz que o balão tem 0,30 m e 0,24 m (confirmado), mas o pivô fica no nó; o "teto 2,85" aplicado ao pivô é **incompatível** com escala 1,4 se o teto real for 3,0 m.
7. `Task07_SceneAndPrefabBuilder.cs` (versão de 10/11) **deleta e recria** os prefabs (`:~40`) e usa posição/rotação da cena de antes da TASK-06 (`gunPos (0.78,0.976,0.48)`, `rot (0,40,−90)`): se o `[D]` reutilizar essas partes, perde o GUID/relocação (`TASK-06` moveu para (4.99, 1.05, 7.62), rotY 145). A versão reescrita às 17:07 precisa ser conferida pelo `[E]`.
8. **Concorrência:** já há edições de `[B]`/`[C]`/`[D]` na árvore enquanto o `[A]` corria; a ordem do plano (uma célula por vez) está sendo violada, e o Unity não compila no momento da leitura do `[A]` (resolvido pela reescrita do builder às 17:07, a confirmar).

## 9. Itens não medidos (a pedir ao `[D]`/`[E]`)
- `ceilingY` real sobre `counter_3_c (2)` (raycast ↑).
- Bounds do balão v3 importado e contagem real de tris (`ModelPolycountAudit`).
- Conferir `counter_3_c (2)` vazio na cena atual e folgas reais da pistola (AABB do Unity).
- `LightsModule` das partículas do balão desligado.
