# TASK-16 (retoque) — Spec [A]: Estante de Troféus (medições, decisões e orçamento)

Gerado pelo Subagente [A] em 2026-11-25 (somente leitura; nada de runtime/prefab/cena foi alterado).
Evidências: `.agents/visual_qa/TASK-16_Before.txt` (Play Mode real), `TASK-16_ShelfScan.txt` (níveis da estante do móvel), `TASK-16_Before_1.png`, `TASK-16_Before_2.png`, scripts de diagnóstico `Assets/Editor/AutoSetup/Task16Scout.cs` e `Task16ShelfScan.cs`.

## 0. Aviso: o código de runtime mudou durante o scout

Outro agente alterou o working tree enquanto eu rodava: `TrophyShelfManager.cs` (+320/-114, 23:46), `ShelfTrophyGrabInteractable.cs` (novo, 23:45), `DroneCrownWearable.cs` (evento `EquipChanged`, 23:45) e já existe `Task16_ShelfRework.cs`. O Play Mode do scout (23:49) rodou **com o código novo e a cena/prefab antigos** (cena ainda com `ErgonomicGrabInteractable`, base 0,52 m e rótulos `fontSize 1`). Por isso a raiz ficou parada (0,0 cm) na simulação.

**Bug do teleporte (§1.1 do plano): CONFIRMADO no código commitado (`git HEAD`)**: `TrophyShelfManager.Awake` fazia `gameObject.AddComponent<AudioSource>()` (AudioSource na raiz, linha 54) e `PlayPickupSound` fazia `audioSource.transform.position = position` (linha 197), chamado em todo `selectEntered` (lambda, linha 127). Cada pegada movia a raiz `TrophyDisplayStand_Instance` (e todos os filhos) até o troféu. Não consegui reproduzir em execução porque o código já estava corrigido (a nova versão cria o filho `TrophyAudio`, só move esse filho: `trophyAudio.transform.position`, linha 409). Resultado do Play Mode (código novo): 4 troféus pegos, movidos 1,5 m e soltos longe → deslocamento da raiz/âncoras/base/rótulos = 0,0 cm. Em +6 s depois de soltos a 1,5 m, GoldenApple, Bonsai e StarCup ficaram a 30–33 cm do slot (sem retorno, ver §1); só a coroa voltou (0,1 cm).

## 1. Estado "antes" medido (cena `Menu_Principal`, Play Mode)

Contagens: 1 `TrophyShelfManager`, 1 `DroneCrownWearable`, 1 `RehabProgressionSaveSystem`, 1 raiz `TrophyDisplayStand*` (sem duplicata). Paciente (XR Origin/câmera) começa em (4,50; 1,80; 2,31) (origem y 0,099).

Estante: raiz `TrophyDisplayStand_Instance` em (9,583; 1,217; -0,055), rotY 270 (frente +Z local = -X mundo, virada para o interior da sala; dot com a direção do paciente = 0,91, distância horizontal 5,61 m). Raiz: `Transform + BoxCollider (0,52 × 0,045 × 0,14, centro y 0,023) + TrophyShelfManager`. AABB visual: 0,52 (z mundo) × 0,267 × 0,14 (x mundo): z -0,315..0,205.

Slots (x local): -0,18 / -0,06 / +0,06 / +0,18 (0,12 m entre centros), âncora y local 0,045 (topo da base em y mundo 1,262).

Troféus (colisor BoxCollider único, sem `PhysicMaterial`, Rigidbody dinâmico, CDM **Continuous**, interpolação None, `ErgonomicGrabInteractable` VelocityTracking, throwOnDetach true):

| Troféu | Massa | Box (m) | Centro y | AABB render | Tris |
|---|---|---|---|---|---|
| GoldenApple | 0,8 | 0,100 × 0,170 × 0,100 | 0,085 | 0,104 × 0,153 × 0,104 | 460 |
| LuminousBonsai | 0,9 | 0,120 × 0,190 × 0,120 | 0,095 | 0,116 × 0,189 × 0,117 | 428 |
| StarCup | 1,1 | 0,140 × 0,210 × 0,110 | 0,105 | 0,103 × 0,211 × 0,155 | 658 |
| DroneCrown | 0,5 | 0,160 × 0,100 × 0,160 | 0,050 | 0,203 × 0,114 × 0,205 | 772 |

Pivôs: todos na base (minY − pivô entre −0,5 e −1,1 cm, aceitável). Todos ≤ 800 tris (ok). A coroa é a peça mais larga (≈ 0,20 m de diâmetro visual).

Sobreposição em repouso (`Physics.ComputePenetration`): 4 pares: base×GoldenApple 0,0 cm, base×Bonsai 0,2 cm, base×StarCup 0,2 cm e **Bonsai×StarCup 0,8 cm** (Bonsai ficou torto: rot (359,8; 270,1; 2,0), StarCup (0,17; 270; 358,6)): o PhysX os empurra para fora; confirmado "mexendo/travado" mesmo sem o bug da raiz. Em +6 s depois de soltos longe os troféus pegos ficaram a 30–33 cm do slot (retorno antigo = 2,5 s + `respawnMonitor` 0,4 s + planeio 0,85 s, e só dispara se `CheckOutOfBounds`: distância > 5 m ou y < 0,35: soltos a 1,5 m **nunca voltam**, ficam no chão/ar até serem recolocados; isto confirma a necessidade da `ShelfTrophyGrabInteractable` nova).

A coroa na cena está **parentada à cabeça do drone** (`[IC] Drone_Guia_IA_ExoBot/DroneHeadSocket/TrophyItem_DroneCrown`, rb kinematic) porque o save diz `isDroneCrowned`; o slot 3 fica sem troféu. Ao ser pega e solta longe ela voltou ao slot (0,1 cm).

## 2. Textos (Label_*) — todos errados

Cada `Label_*` é `TextMeshPro` com **fontSize 1**, autoSize off (min/max 18/72), `overflow = Overflow`, wrap ligado, rect 0,12 × 0,04, escala 1, pai = **a raiz**, fonte `Inter-Regular SDF` (não a LiberationSans SDF do plano), cor (0,95; 0,85; 0,35). Texto **sem acento**: "Maca Dourada", "Bonsai Radiante", "Taca Estelar", "Coroa do Drone". Caixa renderizada ≈ 0,11 × 0,7–0,9 m (3 linhas de letras de ~9 cm de altura): **todos os 4 aparecem gigantes, vazando atrás/em cima da prateleira** (ver `TASK-16_Before_1.png`: "C", "oa", "Drtel di"...).

**Correção de escala (importante para o [C]):** em TMP 3D (escala 1) **fontSize 1 ≈ 0,1 m de em** (medido: "Maca Dourada" tem preferredWidth 0,69 m a fontSize 1 ⇒ ~0,058 m por caractere). O plano pede `fontSize ≈ 0,035`: isso daria letras de ~3 mm (ilegível). Para uma placa de 0,17–0,20 m de largura com nomes de 12–15 caracteres: **fontSize ≈ 0,26–0,32 (autosize min 0,15 / max 0,36)**, altura de letra ≈ 2,5–3 cm, duas linhas permitidas na placa ("Maçã / Dourada") ou uma linha com fontSize ≈ 0,22. Cartão de info (0,28 m de largura): corpo ≈ 0,17, título ≈ 0,26. Verificar glifos `ç ã é í ó ú` na fonte escolhida (LiberationSans SDF) — `Inter-Regular SDF` do projeto não foi testada para acentos.

Locais dos rótulos: local (x slot, 0,024, -0,071), world z da estante: ficam atrás da frente da base e voltados para -X (sala): a orientação do texto estava ok, o problema é só tamanho/overflow.

## 3. Prefab da base e materiais

`TrophyDisplayStand.prefab` (em `Assets/Prefabs/Interactive/`) soma **2.682 tris** com os troféus aninhados (460+428+658+772 = 2.318) ⇒ a base/placas/veludo ≈ **364 tris** (valor por diferença; liso, sem pedestais/anéis/placas inclinadas). Materiais Standard com GPU Instancing ligado (`M_Trophy_PlaqueBrass`, `WoodDark`, `DisplayVelvet`, `GoldPolished`, `BonsaiTrunk`, `CrystalBiolum`, `JadePot`, `MarbleBase`, `StarEmblem`, `GemRuby`, `GemSapphire`, `RoyalVelvet`) + `Inter-Regular SDF Material` (TMP/Distance Field, sem instancing). Pipeline Built-in. Folga de orçamento: base nova ≤ 1.800 tris (sobram ~1.400); troféus ≤ 800 (maior = 772, coroa; sem folga para acrescentar detalhes).

## 4. Móvel `modern_shelves_3 (1)` (onde a estante está)

Estante de 7 tábuas (colisor de malha não convexo; `MeshCollider` + 1 `MeshRenderer`), pos (9,611; 0; 0,027), rotY 270, AABB x 9,473..9,749 (**profundidade 0,276 m**), y 0,002..2,289, z -0,595..0,649 (**comprimento 1,244 m**). Cada tábua cobre x 9,473..9,749 × z -0,595..0,649. Topo das tábuas: **y = 0,10; 0,46; 0,83; 1,19; 1,56; 1,92; 2,29**; fundo das tábuas: 0,03; 0,40; 0,76; 1,13; 1,49; 1,86; 2,22 (espessura 0,06; passo 0,365).

A estante de troféus está na tábua de topo **y ≈ 1,194** (base em 1,196–1,217). **Altura livre até a tábua de cima: 1,49 − 1,19 = 0,30 m** (a estante atual chega a y 1,46: folga de 3 cm). Orçamento de altura da peça nova (base + troféu mais alto 0,214): **≤ 0,27 m** (preferir ≤ 0,26 para 4 cm livres sobre a coroa/taça e o laser).

Vizinhos na mesma tábua (y 1,19): só `Building/book_pile_2 (9)` em x 9,440..9,764 (transborda a tábua), **z 0,266..0,546**, y 1,192..1,442 ⇒ folga atual **0,061 m** da borda da base (z 0,205). Outros livros: `book_pile_2 (8)` (tábua 1,56, z -0,199..0,085), `book_pile_1_alt (2)` (tábua 0,83, z -0,498..0,007). `wall_medium` a 0,15 m atrás (lado +X); fora disso nada na frente (a sala é aberta para -X).

Depois da estante, a posição x atual é 9,583 (não centrada: centro da tábua é x 9,611). Base de 0,22 m de profundidade centrada em **x = 9,611** deixa 0,028 m de borda de cada lado.

### Encaixe de uma base nova (largura na direção Z; 0,22 m de profundidade; altura útil 0,30; box testado com `Physics.OverlapBox`)

| Cenário | Resultado |
|---|---|
| 0,86 × 0,22 na tábua 1,19, **com** `book_pile_2 (9)` | cabe com folga **0,00** m (z livre -0,595..0,266 = 0,861 m exatos) ⇒ **não atinge 0,08 m** |
| 0,86 × 0,22 na tábua 1,19, **sem** `book_pile_2 (9)` (movido para outra tábua) | folga **≥ 0,25 m** (testado; centro z ≈ -0,165 ou 0,027) |
| 0,72 × 0,22 na tábua 1,19, com o livro | folga **0,12 m** até o livro com a base em z -0,235 (borda esquerda da tábua; ao deixar 0,02 m de margem na ponta: folga 0,121 m) |
| 0,86 / 0,72 na tábua 0,83 ou 1,56 | **não cabe** com os livros (segmentos livres 0,64 e 0,56 / 0,40); sem livros cabe com ≥ 0,25 m |

Caminho recomendado: **manter `modern_shelves_3 (1)` na tábua 1,19, base 0,86 m, e realocar `book_pile_2 (9)`** (decoração da `Building`; pode ir para a tábua 0,83, z livre 0,007..0,649 [0,642 m], pilha com ~0,28–0,33 m de largura, ou para a tábua 1,92) — assim a folga vira ≥ 0,25 m (meta do plano) e a estante pode ser centrada (z ≈ 0,027, x 9,611). Alternativa sem mexer na cena: base 0,72 m com folga 0,12 m ao livro (≥ 0,08 ok), porém os espaçamentos ficam apertados (§5).

### Outras superfícies candidatas (peça 0,86 × 0,22; só móveis com AABB ≤ 2,4 m; `full_desk_3 (1)` ignorado: AABB 6,4 × 8,1 m)

Todas as bancadas têm tampo em y 0,949 (baixo demais para o peito de um paciente em pé: a base ficaria a 0,95 m) e estão ocupadas pelos brinquedos das outras tasks:

| Móvel | Resultado 0,86 | Resultado 0,72 |
|---|---|---|
| counter_1 (bobblehead, TASK-09) | folga 0,00 m (eixo Z) | 0,12 m |
| counter_2_b (alavanca+fogos) | 0,04 m | 0,08 m |
| counter_3_a / (1) (pistola, TASK-07) | não cabe | não cabe |
| counter_3_c, (1), (2) (PC), (3) | não cabe | (3): 0,00 m |
| drawer_simple / (1), Demo_Basketball_Table, table_lamp(s) | não cabe | não cabe |

Conclusão: **a única superfície que comporta a base nova com folga é a própria `modern_shelves_3 (1)` (tábua 1,19)**, com o ajuste dos livros; nenhuma bancada tem 0,25 m livres (coerente com o registro da TASK-07).

## 5. Layout recomendado

- **Móvel:** `modern_shelves_3 (1)`, tábua 1,19 (topo y = 1,190–1,195; base apoiada: `groundGapMeters ≤ 0,01`).
- **Rotação:** rotY = 270 (frente +Z = -X mundo, virada para a sala; o paciente chega por (4,5; 2,3), a ~25° do eixo da frente: legível; o `cartão de info` faz billboard).
- **Posição:** x = 9,611 (centro da tábua em profundidade), z = 0,027 (centro da tábua em comprimento) se o `book_pile_2 (9)` for realocado; senão z = -0,235 com base 0,72 m. Raiz com pivô na base central, y = topo da tábua.
- **Base:** 0,86 × 0,22 m (≤ 1.800 tris); alvo de altura ≤ 0,27 m total (base ≤ 0,05 m + placa de fundo ≤ 0,12 m; trofeu mais alto 0,214 m).
- **Slots (x local da base, +Z = frente):** com 0,86 m: **-0,30 / -0,10 / +0,10 / +0,30** (espaçamento 0,20 m). Folgas entre colisores dos troféus: Maçã (0,10)–Bonsai (0,12): 0,09 m; Bonsai–Taça (0,14): 0,07 m; Taça–Coroa (0,16): 0,05 m; coroa a 0,05 m da ponta (0,43). Com 0,72 m, espaçamento 0,17 m deixa a folga Taça–Coroa em 0,02 m (usar colisor da coroa de 0,14 ou espaçamento assimétrico -0,255/-0,085/+0,085/+0,255 com coroa estreitada). Posição Z (profundidade) do troféu: centrado na base (z local 0), a placa inclinada na frente (z local ≈ +0,075).
- **Troféus:** reduzir o `BoxCollider` aos tamanhos da tabela (já ≤ 0,16 m), CDM Discrete em repouso, `PhysicMaterial` dynamic 0,6 / bounce 0,05 (o `ShelfTrophyGrabInteractable` já deixa o rb kinematic no pedestal e só ativa Continuous quando segurado), sem sobreposição no repouso.
- **Rótulos:** filhos das placas inclinadas, fontSize ≈ 0,26–0,32 com autosize (min 0,15 / max 0,36), `Ellipsis`, sem emoji, com acento, fonte com os glifos PT-BR (validar).
- **Coroa:** o save atual (`isDroneCrowned`) coloca a coroa no drone no `Start`; a estante deve mostrar o marcador "Coroa no Drone" (o `DroneCrownWearable.EquipChanged` já existe no working tree).

## 6. API relevante (para [C]/[D] não quebrarem nada)

**Usos fora de `Task16/`: nenhum.** `TrophyShelfManager`, `DroneCrownWearable` e `RehabProgressionSaveSystem` só são referenciados em `Assets/Scripts/Interactive/Task16/*.cs`, `Task16_SceneAndPrefabBuilder.cs`, `Task16_ShelfRework.cs` (novo) e na cena `Menu_Principal.unity` (1 instância de cada). O drone (`DroneAffectiveSystem`, `DroneMilestoneCelebration`) é só lido por `DroneCrownWearable` (`SetExpression`, `Wiggle`, `droneHeadSocketTransform` = `DroneHeadSocket`).

- **`RehabProgressionSaveSystem`** (singleton `Instance`; `Data`; eventos `OnSaveDataUpdated`, `OnTrophyUnlocked(string)`; ids `GoldenApple/LuminousBonsai/StarCup/DroneCrown`; `IsTrophyUnlocked(id)` (true sempre se `unlockAllForDemo`), `UnlockTrophy`, `SetDroneCrowned(bool)`, contadores `AddApple/AddPinecone/AddBasket/AddCoins/AddSteps/AddCheckpoint`; dados: `totalApplesHarvested` 24, `totalPineconesPlanted` 14, `totalBasketsScored` 16, `totalCoinsCollected` 150, `totalStepsTaken` 520, `totalCheckpointsCleared` 8, `isDroneCrowned`). Não alterar além de ler para o cartão.
- **`DroneCrownWearable`**: `isEquippedOnDrone`, `droneHeadSocketTransform`, `EquipOnDrone(bool)`, `UnequipFromDrone()`, `SnapToDroneHeadInstant(socket)`, `AutoLocateDroneReferences()`, evento novo `EquipChanged(bool)`; encaixa por `OnTriggerEnter` em `DroneHeadSocket` só se segurada ou solta há ≤ 1,5 s; usa `transform.SetParent(socket, true)`; `Unequip` faz `SetParent(null)` (o `ShelfTrophyGrabInteractable` guarda `homeParent` para devolver ao slot).
- **`TrophyShelfManager`** (versão nova no working tree): `slots` (`TrophySlot`: trophyId, title, description, trophyObject, slotAnchor, plaqueText, unlockParticles, plaqueLockIcon, ringRenderer, haloRenderer, accentColor, progress), `sfxUnlockFanfare/sfxPickupShimmer/sfxPlaceClick`, `trophyAudio` (filho `TrophyAudio`), `autoRespawnDelay` (1,5), `lockedSilhouetteMaterial`, cores, `infoCard/infoTitle/infoBody/infoCardHeight/infoCardHideDelay`, `idleSpinDegreesPerSecond` 12, `activeRadius` 4, `haloPulseSpeed`; `InfoCardVisible`, `InfoCardTitle`, `GetSlot(id)`, `RefreshShelfState(RehabSaveData)`, `RespawnAllTrophies()`. A cena atual ainda não tem os campos novos preenchidos (infoCard, ring/halo, lockedSilhouetteMaterial nulos) ⇒ o `[D]` tem de reconstruir.
- **`ErgonomicGrabInteractable`** (`Assets/Scripts/Interactive/Task02/`): `[SerializeField] protected`: `ergonomicAttachEaseInTime 0,3`, `enableAutoRespawn true`, `respawnDelaySeconds 2,5`, `respawnFloorThresholdY 0,35`, `respawnMaxDistance 5`, `returnGlideDuration 0,85`, `customHomeAnchor` (atribuir via `SerializedObject`), `alwaysDynamicAfterRelease true`, `playAudioHapticFeedback`; públicos: `HomePosition/HomeRotation` (seguem `customHomeAnchor`), `IsReturningHome`, `IsCurrentlyHeld`, `SetHomeTransform(pos, rot)` (coordenadas de mundo: causa de "home velho" se a raiz se move), `TriggerManualRespawn()` (só se não segurado/voltando), `ReturnHomeRoutine()` (virtual: planeio com arco, rb kinematic durante, restaura `originalWasKinematic`). `Awake` força VelocityTracking, smooth 10, `throwOnDetach = true`. O retorno automático só dispara via `CheckOutOfBounds` (distância > `respawnMaxDistance` ou y < `respawnFloorThresholdY` no Menu): por isso troféus soltos a 1,5 m não voltam. Não mudar esta classe (usada por fitas, maçãs etc.).
- **`ShelfTrophyGrabInteractable`** (novo, aditivo): `snapRadius 0,20`, `snapMaxSpeed 0,6`, `snapGlideSeconds 0,22`, `returnDelaySeconds 1,5`, `returnGlideSeconds 0,6`, `continuousCollisionSeconds 3`; eventos `Docked(grab, snapped)`, `Grabbed`; `IsDocked`, `DockCount`, `HomeAnchor`, `SnapHomeInstant()`; `enableAutoRespawn = false` (retorno por evento); homeParent guardado no `Awake`.

## 7. Cenário/estado da cena (para o [D])

- `Menu_Principal.unity` tem 1 estante, 3 troféus no slot + a coroa no drone; nada no `(0,0,0)` ligado à estante.
- Colisor da estante: o `MeshCollider` do móvel (não convexo) é atingido por OverlapBox em y 1,20; as tábuas existem fisicamente (a raycast de cima só devolveu a tábua do topo, por limitação da malha não legível; usar `OverlapBox`/renderer bounds, não raycast, para validar `groundGapMeters`).
- `Camera.main` em (4,5; 1,8; 2,31); XR Origin em y 0,099; a estante a 5,6 m do ponto de partida (o paciente precisa caminhar; a coroa/cartão devem ser legíveis a 1,5 m).

## 8. Orçamento de performance

- Base ≤ 1.800 tris; troféus já ≤ 800 (ok); total da estante ≈ 1.800 + 2.318 ≈ 4.100 tris e ≤ 12 draw calls (materiais instanciados, mesmo shader Standard, sem sombras extras).
- Sem `Light` com sombra; partículas ≤ 40; áudio ≤ −6 dBFS, `AudioSource` dedicado só no filho `TrophyAudio`.
- Troféus: Rigidbody kinematic em repouso, CDM Discrete, Continuous só enquanto segurado/voando (+3 s); sem `MeshCollider`; 1 loop no manager, ativo só a ≤ 4 m (`sqrMagnitude`).
- Textos: 1 `TextMeshPro` por placa + 2 no cartão único; atualizar só na mudança; sem `renderer.material` (MaterialPropertyBlock).
- Zero `LoadScene`; zero alocação por frame (testar 300 frames de idle).
