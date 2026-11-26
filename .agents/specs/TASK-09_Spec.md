# TASK-09 [A] — Spec forense (Fogos refeito + Bobblehead mais "bobblehead")

Fonte dos números: `.agents/visual_qa/TASK-09_ScoutReport.txt` (gerado por `Assets/Editor/AutoSetup/Task09Scout.cs`, batchmode, somente leitura, cena `Menu_Principal` NÃO salva). Plano de origem: `.agents/specs/TASK-09_Plano_Opus.md`.

## 1. Confirmação / refutação das causas

### 1.1 Fogos de artifício

| # | Causa do plano | Veredito | Evidência medida |
|---|---|---|---|
| 1 | Explosão desativada | **CONFIRMADA** | `Burst_Explosion_System`: `emission.enabled = False`, 1 burst (t=0, 180 partículas). Simulado a 0,3 / 0,8 s (= t−0,6 do disparo): `particleCount = 0`. A explosão nunca aparece. |
| 2 | Apex preso em coordenadas do mundo antigo | **CONFIRMADA em parte** | `Burst_Apex_Target` world `(7.796, 2.446, -2.536)`, local no launcher `(1.010, 1.495, 0.800)`; botão em `(8.978, 0.951, -2.025)` rot Y=270°, launcher rot Y=195°. Delta vs botão `(-1.183, +1.495, -0.511)`: **1,29 m de deslocamento horizontal** e 1,50 m de altura. **Refutado:** o apex NÃO está dentro de TV/parede/colisor (`OverlapSphere(0,15)` = 0 colisores, 0 AABBs contendo o ponto, `Linecast` livre). Está solto no ar da sala, fora do eixo do botão. |
| 3 | Trilha com direção velha | **CONFIRMADA (reformulada)** | `Ascent_Sparks_Trail` world `(8.978, 1.001, -2.025)`, forward `(-0.611, 0.746, -0.264)`: 0,0° de erro até o apex (o `LookAt` rodou junto), mas **41,7° fora da vertical**. Alcance = 8 m/s × 0,4 s = **3,20 m** contra 1,94 m até o apex: a trilha passa direto do ponto de explosão. `simulationSpace = Local`. |
| 4 | Tudo só na cena, prefab com refs nulas | **CONFIRMADA** | Prefab `Assets/Prefabs/Interactive/FireworksLaunchButton.prefab`: `fireworksAscentParticles`, `fireworksBurstParticles`, `fireworksBurstLight`, `burstLocation` = `null`; 0 ParticleSystems no prefab. Na cena existe `Room_Fireworks_Launcher` (1) com 4 filhos pendurados na raiz. |
| 5 | Material "Lit" opaco em partículas | **CONFIRMADA** | `M_Fireworks_Sparkle`: shader `Standard`, `_Mode = 0` (Opaque), renderQueue 2000, sem `_MainTex`, emissão ligada. Nas capturas "antes" as faíscas são **quadrados brancos sólidos**. As texturas `Firework_Spark.png` e `Firework_Trail.png` já existem em `Assets/Models/Generated/Task09/` mas nenhum material as usa. |
| 6 | Foguete fixo, sem cooldown, gatilho frágil | **CONFIRMADA** | `rocketTravelTime = 0.6` em `WaitForSeconds`; `isPressing` só cobre a animação (~0,5 s); `OnTriggerEnter` compara `other.name.ToLower().Contains("hand"/"controller")`. Adicional: o prefab tem `BoxCollider` e `SphereCollider` com `isTrigger = False` na raiz, e nenhum Rigidbody, então `OnTriggerEnter` do botão **nunca dispara** (o toque de mão hoje não funciona). |

Outros achados do sistema atual:
- Explosão: `startSpeed 3.8 × startLifetime 1,2` => raio de dispersão até ~4,5 m (reduzido pela gravidade 0,3). Muito grande para uma mesa: atravessaria a TV e a parede (+X a 0,92 m). O novo show precisa de raio efetivo de ~1,0 m.
- `maxParticles = 1000` nos dois sistemas (o plano pede ≤ 250 vivas). `startSize 0,04/0,09`, `shadowCasting = Off`, `receiveShadows = False`, `enableGPUInstancing = True` (corretos, manter).
- Luz de explosão: ponto, range 4,5, intensidade 1,8, `shadows = None`, desligada (plano pede range ≤ 3, int ≤ 2,5).
- Cada `Light` do botão e do bobblehead já está com `LightShadows.None`.

### 1.2 Bobblehead (causa numérica confirmada)

`BobbleheadSpringToy.cs` lido por inteiro:
- `angularVelocity += impulse` (graus/s) com `naturalFrequency = 13,5`: amplitude ≈ v/ω. Clique `±28` => **~2,1°**; hover `±7` => **~0,5°** (impulso aleatório em X e Y, sem direção). Pancada de teste (`BoingTest 35,-25`) => ~2,6°/1,9°.
- `dampingRatio = 0,14` => decaimento `ζω = 1,89 /s`, constante de tempo 0,53 s, cai a 5 % em ~1,6 s (plano quer 3–4 s).
- Só 2 eixos (pitch/roll), sem yaw, limite por `ClampMagnitude(45)` seco (nunca chega a ser atingido), sem movimento secundário, sem torção.
- `Update()` roda para sempre (sem estado de repouso) e atualiza a luz do visor todo frame.
- Rigidbody da raiz `isKinematic = True`; `OnCollisionEnter` na raiz só dispara com Rigidbody dinâmico; mão do paciente não dispara nada.
- Colisores na **raiz**: `BoxCollider` center `(0,0.04,0)` size `(0.09,0.08,0.09)` e `SphereCollider` center `(0,0.125,0)` r 0,045. Não seguem o pivô da cabeça (`Bobble_Head_Pivot` em y 0,088). Todos primitivos, ok.
- Hover usa `hoverEntered` sem cooldown (re-dispara a cada entrada).

## 2. Medições da cena `Menu_Principal`

### 2.1 Contagens e `(0,0,0)`
- `FireworksButtonInteractable`: 1 (`FireworksLaunchButton_Instance`, `(8.978, 0.951, -2.025)`). `BobbleheadSpringToy`: 1 (`ExoMascotBobblehead_Instance`, `(0.747, 0.920, 5.157)`). `AtmosphereLeverInteractable`: 1 (`EnvironmentControlLever_Instance`, `(8.974, 0.951, -1.529)`).
- `Room_Fireworks_Launcher` 1, `Burst_Apex_Target` 1, `Ascent_Sparks_Trail` 1, `Burst_Explosion_System` 1, `Burst_Flash_Light` 1 (todos devem ser apagados no [D] e substituídos por filhos do prefab).
- Objetos a < 1 m de `(0,0,0)`: apenas estruturais (`Gerenciador_HUD`, `GerenciadorPause`, `EventSystem`, `Building`). Nenhuma duplicata solta. `isOriginClean = true`.

### 2.2 Botão de fogos em `counter_2_b` (apoio: `Building/counter_2_b`, tampo y 0,949)
- AABB do botão: min `(8.914, 0.951, -2.089)` max `(9.042, 1.016, -1.961)`. Topo y 1,016. Forward do botão: `-X`.
- **Raycast para cima (centro + 4 offsets de 0,15 m): nada em 6 m.** Nenhum colisor nem renderer grande acima do botão. Não foi achado teto sólido nesta coluna (a medição da TASK-07 em outro balcão deu teto em y 2,85; use 2,85 como limite conservador e mantenha o apex ≤ y 2,5).
- Renderers na coluna de 0,6 m de raio (+3 m): `tv` (dXZ 0,320, y 0,946–1,593, size `0,204 × 0,647 × 0,994`), `deco_variant_3` (dXZ 0,466, y até 1,062) e a alavanca (dXZ 0,417). A TV está **ao lado**, não por cima: não obstrui um foguete vertical.
- Raycasts horizontais a tampo+0,1 m e +1,0 m (yaw medido do +Z, horário): 0° livre >8 m (+Z); 45° `wall_medium(11)` 1,30 m; **90° (+X) `wall_medium(10)` 0,92 m**; 135° 1,30 m; 180° (−Z) `wall_medium(12)` 1,88 m; 225° 2,65 m (a +1,0 m: `painting_small(2)` 2,55 m); **270° (−X, frente do botão) livre >8 m**; 315° `Pessoa_Virtual_sem3D` 5,8 m (a +1,0 m: livre).
- **Conclusão:** cabe show alto (≥ 0,6 m livre + 0,35 m de folga) com sobra: há ≥ 1,8 m livre acima do botão. **NÃO é preciso mover o botão.** Direção livre: −X (frente) e +Z. A parede +X está a 0,92 m, então o raio horizontal da explosão deve ser ≤ 0,8 m para esse lado, ou o apex deve ser inclinado ~10–12° para −X. Apex alvo: ~1,0–1,2 m acima do topo do botão (y ≈ 2,1–2,2).

### 2.3 Folgas e distâncias
- Distância pivô alavanca↔botão = **0,496 m** (> 0,45). Folga entre AABBs = **0,352 m**.
- Botão: vizinho mais próximo `tv` 0,256 m; alavanca 0,352 m. Alavanca: vizinho mais próximo `deco_variant_3` **0,198 m**; `tv` 0,264 m. Todos > 0,08 m.
- Bobblehead (em `drawer_simple`, tampo y 0,918, gap ao tampo 0,002 m): `deco_variant_2` **0,076 m (< 0,08, FALHA herdada)**, `counter_1` 0,124 m, `counter_1/Ac` 0,132 m, `ArcadeBallSpawner_Instance` 0,275 m. AABB do bobble: `0,134 × 0,160 × 0,134`.

### 2.4 Superfícies candidatas para o bobblehead (item ~0,12 m, grade 2 cm, só móveis com tampo 0,4–2,0 m e AABB ≤ 2,4 m; `full_desk_3 (1)` ignorado)

| Móvel | Tampo y | Melhor folga | Ponto | Vizinho limitante |
|---|---|---|---|---|
| `counter_1` | 0,949 | **0,350 m** | `(1.134, 0.949, 4.906)` | `laptop_variant (1)` |
| `counter_3_c (3)` | 0,949 | 0,221 m | `(6.607, 0.949, 7.765)` | `laptop_variant` |
| `counter_2_b` (fogos+alavanca) | 0,949 | 0,220 m | `(8.954, 0.949, -2.369)` | botão de fogos |
| `drawer_simple (1)` | 0,918 | 0,214 m | `(0.382, 0.918, 6.205)` | `chair_2` |
| `counter_3_a (1)` (pistola de balões) | 0,949 | 0,197 m | `(5.607, 0.949, 7.758)` | `table_lamp (1)` |
| `counter_3_c (1)` | 0,949 | 0,184 m | `(9.671, 0.949, 6.056)` | `pc_mouse_pad (1)` |
| `counter_3_c (2)` | 0,949 | 0,170 m | `(3.727, 0.949, 7.111)` | `pc_case (2)` |
| `counter_3_a` | 0,949 | 0,142 m | `(9.053, 0.949, 5.036)` | `chair_3` |
| `drawer_simple` (posição atual) | 0,918 | 0,126 m | `(0.770, 0.918, 5.150)` | `counter_1` |
| `counter_3_c` | 0,949 | 0,103 m | `(9.096, 0.949, 3.176)` | `pc_case` |

Atenção: `Demo_Basketball_Table` apareceu no ranking com 0,425 m, mas **é a bola de demonstração da TASK-08, não um móvel**; ignorar. Nenhum móvel entrega 0,25 m para o bobblehead além de `counter_1` (0,35 m).

**Recomendação para o [D]:** mover o bobblehead para `counter_1` em torno de `(1.134, 0.949, 4.906)` (a 0,35 m do laptop, ~0,38 m do deslocamento atual), frente voltada para o jogador. Reconferir no [D] com o grid antes/depois que o `ArcadeBallSpawner` e outros itens da TASK-08/16 não ocupem o mesmo ponto (a memória do projeto diz que `counter_1` é o melhor ponto livre e disputado por TASK-08/09/16). Se a superfície ficar tomada, o fallback é `counter_3_c (3)` (0,221 m). Meta ≥ 0,25 m só é atingida em `counter_1`; documentar a folga real no `SpatialAudit`.

## 3. Orçamento de triângulos (FBX) — `Assets/Models/Generated/Task09/`

| FBX | Tris | Orçamento | Situação |
|---|---|---|---|
| `FireworksLaunchButton.fbx` | **1.828** | ≤ 800 | **ESTOURADO (+129 %)**: `Button_Dome` 960, `Button_Glow_Ring` 512, `Button_Chassis` 220, `Button_Bezel_Chrome` 124, `Button_Label_Plate` 12. |
| `ExoMascotBobblehead.fbx` | **1.840** | ≤ 1.800 | Estourado em 2,2 %: `Bobble_Head_Pivot` 520 (submeshes 300+12+120+88), `Bobble_Pedestal_Ring` 512, `Bobble_Pedestal` 380, `Bobble_Torso` 188, 4 × 60 (braços L/R, reator, mola). |
| `EnvironmentControlLever.fbx` (só registro; não alterar) | 1.768 | — | Aprovado, fora do escopo. |

Nota: o plano falava em "~2 %" também para o botão; o número real é muito maior. O [B] precisa reduzir o botão a ≤ 800 (domo 960 → ~200, anel luminoso 512 → ~64–96).

## 4. Hierarquia atual do bobblehead (prefab `Assets/Prefabs/Interactive/ExoMascotBobblehead.prefab`)

```
ExoMascotBobblehead  [BoxCollider, SphereCollider, Rigidbody(kinematic), AudioSource, BobbleheadSpringToy]
  Visual_Model
    Bobble_Pedestal (380 tris)            lp (0, 0.009, 0)
    Bobble_Pedestal_Ring (512 tris)       lp (0, 0.012, 0)
    Bobble_Torso (188)                    lp (0, 0.048, 0)
    Bobble_Chest_Reactor (60)             lp (0, 0.052, 0.021)
    Bobble_Arm_L / Bobble_Arm_R (60 cada) lp (±0.030, 0.042, 0)
    Bobble_Neck_Spring (60, ~0,018 m)     lp (0, 0.084, 0)    <- cilindro único de 60 tris
    Bobble_Head_Pivot (520, 4 materiais)  lp (0, 0.088, 0)    <- cabeça + visor + orelhas num único mesh
  Visor_Glow_Light                        lp (0, 0.125, 0.045) (point, range 0,3, int 1,2, shadows None)
```
Cabeça: bounds locais `0,097 × 0,068 × 0,074`; corpo total ~0,16 m de altura. Materiais da cabeça: `M_Bobble_Armor`, `M_Bobble_Visor`, `M_Bobble_Trim`, `M_Bobble_Ears` (submeshes 300 / 12 / 120 / 88). **Orelhas/antenas NÃO são objetos separados** (são submesh `M_Bobble_Ears` do mesh da cabeça). A mola do pescoço existe mas é um prisma de 60 tris, sem espiras. Visor é submesh da cabeça (12 tris com `M_Bobble_Visor`); `visorRenderer` serializado aponta para o próprio `Bobble_Head_Pivot` (MeshRenderer com 4 materiais), o que dificulta pintar só o visor por `MaterialPropertyBlock` (precisará de índice de submesh ou de visor separado).
Serializado: `naturalFrequency 13,5`, `dampingRatio 0,14`, `maxDeflectionAngle 45`, `minImpulseForSound 8`, `sfxBoing = bobblehead_spring_boing`.

O que o [B] precisa modelar: (1) cabeça ~1,2–1,3× mais "cabeçuda" mantendo pivô no pescoço (`Bobble_Head_Pivot` y 0,088); (2) **orelhas/antenas como 2 objetos separados** (`Bobble_Ear_L/R`, pivôs na base) para movimento secundário; (3) **visor separado** (`Bobble_Visor`) ou índice de submesh fixo e documentado para o MaterialPropertyBlock; (4) mola do pescoço com algumas espiras (≤ ~150 tris, pivô na base); (5) enxugar `Bobble_Pedestal_Ring` 512 → ≤ ~96 e o pedestal para caber em ≤ 1.800 tris no total.
Áudios `bobblehead_spring_boing/soft`, `bobblehead_limit_tock` e `fireworks_launch_whistle/burst_explosion` já existem em `Assets/Audio/Generated/Task09/` (o [B] só refaz se estiverem fora de −6 dBFS ou estridentes).

## 5. Capturas "antes" (offscreen, 1920×1080, em `.agents/visual_qa/`)
- `TASK-09_Before_Fireworks_t02.png`, `_t05`, `_t09`, `_t14` (tempos 0,2/0,5/0,9/1,4 s). Câmera: `(6.978, 1.500, -2.025)` (2 m à frente do botão, direção −X, y 1,5), olhando para `(8.387, 1.699, -2.281)` (ponto médio botão–apex), FOV 70. Trilha simulada em t; explosão simulada em `t − 0,6 s` (igual ao runtime). Partículas vivas: ascent 15 / 32 / 3 / 0; burst 0 / 0 / 0 / 0. Mostram faíscas como quadrados brancos sólidos subindo inclinadas e **nenhuma explosão**.
- `TASK-09_Before_Bobble.png`: câmera `(0.402, 1.170, 4.665)` (0,6 m à frente do forward achatado `(-0.574, 0, -0.819)` + 0,25 m acima do pivô), olhando para o centro do AABB `(0.747, 1.000, 5.157)`, FOV 45.
- `TASK-09_Lever_Before.png`: câmera `(8.374, 1.201, -1.529)` (0,6 m à frente do forward achatado `(-1, 0, 0)` + 0,25 m acima), olhando para o centro do AABB `(8.974, 1.041, -1.529)`, FOV 45. **Reusar exatamente essa pose na captura "depois"** (alavanca não se move). A cena sai bem escura nas capturas (estado de iluminação gravado na cena); manter o mesmo estado de atmosfera na captura "depois".

## 6. Arquivos por célula

**[B] (arte/áudio):** `Assets/Models/Generated/Task09/ExoMascotBobblehead.fbx`, `FireworksLaunchButton.fbx` (+ `.meta` se reimportar), `M_Bobble_*.mat` e `M_Fireworks_Sparkle.mat` (recriar como partícula aditiva Built-in), `Firework_Spark.png`/`Firework_Trail.png` (conferir alpha), `Assets/Audio/Generated/Task09/*.wav` só se necessário. Scripts Python/Blender do [B] ficam em `Assets/Models/Generated/Task09/` ou pasta equivalente já usada.

**[C] (C#):** `Assets/Scripts/Interactive/Task09/FireworksButtonInteractable.cs` (reescrever), `Assets/Scripts/Interactive/Task09/BobbleheadSpringToy.cs` (reescrever), `Assets/Editor/AutoSetup/Task09_PlayModeTest.cs` (novo). Pode usar `VRFeedbackManager` e helpers já existentes (`Assets/Scripts/Core`).

**[D] (cena/prefab):** `Assets/Prefabs/Interactive/FireworksLaunchButton.prefab` (referências para filhos do próprio prefab), `Assets/Prefabs/Interactive/ExoMascotBobblehead.prefab` (colisor da cabeça como filho de `Bobble_Head_Pivot`, novos filhos), `Assets/Scenes/Menu_Principal.unity` (apagar `Room_Fireworks_Launcher`, mover bobblehead), novo `Assets/Editor/AutoSetup/Task09_FidgetRework.cs`; saídas em `.agents/visual_qa/TASK-09_*` e `TASK-09_SpatialAudit.json`. O `Task09_SceneAndPrefabBuilder.cs` antigo (`SetupFireworksParticleLauncher`, posições `(0.79, 0.955, 0.60)` etc.) **não deve ser re-executado** (recria o launcher quebrado); deprecar ou deletar o trecho.

**NÃO PODEM MUDAR (alavanca aprovada):** `Assets/Scripts/Interactive/Task09/AtmosphereLeverInteractable.cs` (linha 393 `ledRenderer.material` só registrada), `Assets/Models/Generated/Task09/EnvironmentControlLever.fbx` (+ `.meta`), `Assets/Prefabs/Interactive/EnvironmentControlLever.prefab` (e variantes), os materiais da alavanca, e o objeto `EnvironmentControlLever_Instance` na cena (posição `(8.974, 0.951, -1.529)`, rot Y 270°). Provar no [E] com `git diff --stat` sem esses arquivos e com `TASK-09_Lever_*` idêntico ao `Lever_Before`.

## 7. Pontos de atenção para os próximos subagentes
- Hover/select do bobblehead: usar `XRSimpleInteractable` e o Vive trigger = Select (ver memória do projeto); sem `LoadScene` em nenhum script.
- Os dois colisores do botão são sólidos (não trigger); o toque de mão exige trigger dedicado no domo.
- Não usar `EditorUtility.CopySerialized`; Unity precisa estar fechado ou usar `unity` CLI conforme o plano; pipeline Built-in (Standard), não URP.
- Cena `Menu_Principal.unity` não foi modificada nem salva por este scout (`git status` não deve mostrar mudança na cena por causa dele).
