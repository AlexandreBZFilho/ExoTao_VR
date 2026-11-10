# Especificação Técnica — TASK-16: Persistência de Progresso, Estante de Troféus Físicos e Coroa do Drone Pet

**Projeto:** VR Rehab Gamification (ExoTao)  
**Versão Unity:** 6 LTS (6000.0.85f2)  
**Módulo:** Gamificação & Persistência (Fase Final de Fechamento)  
**Data:** 10 de Novembro de 2026  
**Status:** Em Implementação (`[~]` -> `[x]`)

---

## 1. Visão Geral da Tarefa
A **TASK-16** coroa o sistema completo de reabilitação motora em Realidade Virtual fornecendo persistência de dados em disco (`JSON Save`), feedback visual e tátil de progressão através de uma estante de troféus 3D no Menu Principal e um item vestível interativo: a Coroa Real do Drone Pet (`Trophy_DroneCrownHat`).

Quando o paciente atinge marcos clínicos (colher maçãs, acertar cestas, plantar sementes, coletar moedas e concluir sessões), troféus físicos são desbloqueados na sua bancada/estante de honra. O paciente pode pegá-los com as mãos em VR (ou puxá-los via *Force Grab* do exoesqueleto), examiná-los com reflexos dourados e colocá-los no chão ou na mesa — com sistema de *Auto-Respawn* suave garantindo retorno automático ao pedestal.
Ao coroar o Drone Pet com a Coroa Real, o Drone celebra em êxtase com fanfarra real, efeito de estrelas e expressão afetiva radiante!

---

## 2. Modelagem 3D & Escala Métrica Real VR (1 unidade = 1 metro)
Todos os modelos são modelados no Blender 5.1 e exportados em `.fbx` com materiais e pivôs ajustados para pegada ergonômica:

1. **`Trophy_GoldenApple.fbx`**:
   - Dimensões: ~0.10m x 0.10m x 0.18m (Altura total ~18cm).
   - Componentes: Pedestal de madeira nobre polida / obsidiana com chanfros, placa chanfrada dourada frontal com inscrição e maçã esculpida em ouro polido com caule e folha estilizada.
   - Materiais: `M_Trophy_WoodDark.mat`, `M_Trophy_GoldPolished.mat`, `M_Trophy_PlaqueBrass.mat`.
   - Conquista: "Colheita de Ouro" (Maçãs colhidas na floresta).

2. **`Trophy_LuminousBonsai.fbx`**:
   - Dimensões: ~0.12m x 0.12m x 0.20m (Altura total ~20cm).
   - Componentes: Vaso hexagonal de cerâmica jade, tronco sinuoso de madeira ancestral e copas de cristal bioluminescente emitindo suave brilho ciano/esmeralda.
   - Materiais: `M_Trophy_JadePot.mat`, `M_Trophy_BonsaiTrunk.mat`, `M_Trophy_CrystalBiolum.mat`.
   - Conquista: "Guardião da Natureza" (Sementes mágicas e cogumelos brotados).

3. **`Trophy_StarCup.fbx`**:
   - Dimensões: ~0.14m x 0.11m x 0.22m (Altura total ~22cm).
   - Componentes: Base quadrada pesada de mármore negro com colar de bronze, haste clássica estriada, taça ampla de ouro brilhante com alças duplas elegantes e uma grande estrela de cinco pontas em relevo no corpo central.
   - Materiais: `M_Trophy_MarbleBase.mat`, `M_Trophy_GoldPolished.mat`, `M_Trophy_StarEmblem.mat`.
   - Conquista: "Campeão Arcade" (Pontuação no mini-basquete e moedas recolhidas).

4. **`Trophy_DroneCrownHat.fbx`**:
   - Dimensões: Diâmetro externo ~0.16m, Altura ~0.10m. Base interna oca (~0.13m de diâmetro esférico côncavo) permitindo assentamento perfeito na calota superior do Drone Pet.
   - Componentes: Aro dourado imperial com detalhes ornamentais, 6 pontas régias encimadas por pérolas esféricas, almofada interna de veludo vermelho real e gemas de rubi e safira engastadas no aro.
   - Materiais: `M_Trophy_GoldPolished.mat`, `M_Trophy_RoyalVelvet.mat`, `M_Trophy_GemRuby.mat`, `M_Trophy_GemSapphire.mat`.
   - Conquista: "Parceria Lendária" (Amizade máxima e carinho com o Drone Pet).

5. **`Trophy_DisplayStand.fbx`**:
   - Dimensões: ~0.52m de comprimento, ~0.14m de profundidade, ~0.06m de altura.
   - Suporte de exibição de luxo em madeira mogno com 4 berços circulares acolchoados em veludo azul-marinho e placas gravadas para cada troféu.
   - Posicionamento: Na mesa principal (`full_desk_3`), em `x: 0.68f, y: 0.952f, z: -0.42f`.

---

## 3. Arquitetura de Áudio Procedural (Python Synthesizer)
Três faixas estéreo em 44.1kHz geradas via síntese espectral e física em `Assets/Audio/Generated/Task16/`:
- `trophy_unlock_fanfare.wav`: Acorde maior glorioso (Dó-Mi-Sol-Dó) sintetizado com ondas dente-de-serra ricas em harmônicos, seguido de arpejo de sinos celestiais.
- `trophy_pickup_shimmer.wav`: Toque cristalino suave de 880Hz com modulação FM cintilante (shimmer) ao segurar qualquer troféu.
- `crown_equip_royal.wav`: Fanfarra imperial triunfante com trombetas polifônicas, eco e sinos de comemoração pela coroação do drone.

---

## 4. Engenharia de Software C# & Eventos de Runtime

### `RehabProgressionSaveSystem.cs` (`VRRehab.Persistence`)
- **Padrão:** Singleton persistente (`DontDestroyOnLoad`).
- **Arquivo:** `Application.persistentDataPath + "/vr_rehab_save.json"`.
- **Campos Salvos:**
  - `int totalCoinsCollected`
  - `int totalStepsTaken`
  - `int totalBasketsScored`
  - `int totalApplesHarvested`
  - `int totalPineconesPlanted`
  - `int totalCheckpointsCleared`
  - `bool trophyGoldenAppleUnlocked`
  - `bool trophyLuminousBonsaiUnlocked`
  - `bool trophyStarCupUnlocked`
  - `bool trophyDroneCrownUnlocked`
  - `bool isDroneCrowned`
  - `string lastSessionDate`
- **Flag de Demonstração:** `public bool unlockAllForDemo = true;` (ativada por padrão para que todos os troféus estejam imediatamente prontos e interativos no Hub para validação clínica).
- **Eventos:** `Action<RehabSaveData> OnSaveDataUpdated`.

### `TrophyShelfManager.cs` (`VRRehab.Interactive.Task16`)
- Gerencia os 4 pedestais do display stand.
- Ativa/desativa visuais com base no save.
- Cada troféu configurado com:
  - `Rigidbody` (massa equilibrada de ~0.8kg).
  - `ErgonomicGrabInteractable` (Force Grab ativado, escala de lançamento balanceada).
  - `InteractableHighlightFeedback` (brilho sutil no contorno ao mirar com o raio laser).
  - `Auto-Respawn`: se o paciente soltar ou arremessar o troféu para fora da mesa, ele flutua suavemente de volta ao berço em ~3 segundos com feedback sonoro suave.
  - Hover tooltip / Placa de texto 3D (`TextMeshPro`) com título e descrição do troféu.

### `DroneCrownWearable.cs` (`VRRehab.Interactive.Task16`)
- Acoplado ao `Trophy_DroneCrownHat`.
- Contém lógica para interagir com o `XRSocketInteractor` no topo da cabeça do Drone (`DroneHeadSocket`).
- Ao acoplar:
  - Reproduz `crown_equip_royal.wav`.
  - Dispara emissão de partículas de estrelas douradas (`ParticleSystem`).
  - Aciona o `DroneAffectiveSystem` do Drone Pet com expressão alegre (`Happy` ou `Love`).
  - Salva o estado `isDroneCrowned = true`.

---

## 5. Protocolo de Visual QA & Critérios de Aceite
1. **`TASK-16_Trophy_Shelf_Closeup.png`**: Enquadramento fechado da estante com os 4 troféus reluzentes perfeitamente alinhados sobre a mesa do Hub.
2. **`TASK-16_Drone_Crown_Equipped.png`**: Close-up do Drone Pet vestindo a coroa dourada imperial no topo de sua cabeça, demonstrando encaixe físico natural e expressão facial correspondente.
3. **`TASK-16_Menu_Hub_Master_Panorama.png`**: Panorama mestre amplo de toda a sala do Menu Principal mostrando a integração perfeita de todas as 6 mecânicas do Hub (Rádio & Fitas, Pistola de Balões, Basquete Arcade, Estação Fidget, Portal 3D e Estante de Troféus).
