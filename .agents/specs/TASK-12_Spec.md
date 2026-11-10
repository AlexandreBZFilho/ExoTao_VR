# TASK-12: Sementes Mágicas e Pinhões Arremessáveis (Brotação Procedural de Flores e Cogumelos)
## Especificação Técnica & Arquitetura de VR

**Data:** 10/11/2026  
**Módulo:** Floresta & Gamificação Sensorial  
**Alvo:** Unity 6 LTS (6000.0.85f2) | XRI 2.6.5 | Exoesqueleto ExoTao  
**Autor:** Agente Especialista VR Rehab Gamification  

---

### 1. Visão Geral e Propósito Clínico
Na reabilitação neuromotora com o exoesqueleto ExoTao, o paciente caminha pela trilha da floresta virtual. Para estimular a extensão de braços, alcance voluntário em diferentes direções e mira motora sem exigir esforço excessivo de flexão lombar, a **TASK-12** introduz **Pinhões Mágicos Cintilantes (`GlowingPineconeSeed`)** dispostos em pedestais na margem da trilha.

O paciente pode puxar as sementes à distância através do **Force Grab Assistido** (`ErgonomicGrabInteractable`) e arremessá-las em direção à clareira ou às margens do caminho. Ao tocar o solo ou pedras, o pinhão detona em partículas de pólen luminoso e brota proceduralmente um **Canteiro de Flores Coloridas (`SproutingFlowerPatch`)** ou um **Canteiro de Cogumelos Saltitantes (`BouncyMushroomCluster`)**, alinhando-se organicamente à normal do terreno.

---

### 2. Especificação Métrica Real de VR (1 unidade = 1 metro)

| Asset / Objeto | Altura (m) | Diâmetro/Largura (m) | Pivô Espacial | Materiais / Cores |
| :--- | :---: | :---: | :---: | :--- |
| **`GlowingPineconeSeed`** | ~0.10 m | ~0.06 m | Base / Centro Ergonômico (Y=0.03m) | Casca amadeirada estilizada com pontas emissivas ciano/douradas (`M_Pinecone_Bark`, `M_Pinecone_Glow`) |
| **`SproutingFlowerPatch`** | ~0.35 m | ~0.60 m | Base do solo (Y=0.0 m) | Canteiro de 6 flores vibrantes (pétalas amarelo, magenta, azul e laranja, centros dourados e folhas verde-esmeralda) |
| **`BouncyMushroomCluster`**| ~0.30 m | ~0.45 m | Base do solo (Y=0.0 m) | Trio de cogumelos (1 grande, 1 médio, 1 pequeno), chapéus vermelhos com pintas brancas e caules rechonchudos |
| **`SeedNestPedestal`** | ~0.65 m | ~0.35 m | Base do solo (Y=0.0 m) | Pedestal rústico de pedra/madeira musgosa com ninho côncavo acolhedor para repouso da semente |

---

### 3. Áudio Procedural Sintetizado (`Assets/Audio/Generated/Task12/`)
- `seed_pickup_chime.wav`: Chime ascendente cintilante (frequências de 1200Hz a 2400Hz com harmônicos brilhantes e decaimento suave de 0.6s) ao puxar a semente com o laser de Force Grab.
- `seed_impact_burst.wav`: Som místico de estalo suave com ressonância de pólen mágico (transiente rápido de 0.35s).
- `plant_sprout_elastic.wav`: Som orgânico "pop-stretch-grow" de brotação elástica (curva de pitch ascendente de 180Hz a 520Hz com modulação vibrato suave de 0.55s).
- `mushroom_bounce_boing.wav`: Efeito gomoso "boing" elástico de mola de cogumelo (envelope de pitch senoidal de 0.45s).

---

### 4. Arquitetura de Scripts C# (`Assets/Scripts/Interactive/Task12/`)
1. **`MagicSeedPinecone.cs`:**
   - Herda de `VRRehab.Ergonomics.ErgonomicGrabInteractable`.
   - Gerencia estados: *Em Repouso no Pedestal*, *Segurado pelo Paciente*, *Em Voo Arremessado*, *Impactado / Brotando*.
   - Ao colidir com o solo (`OnCollisionEnter`):
     - Executa Raycast para baixo para confirmar colisão com `Terrain` ou superfícies válidas.
     - Calcula rotação via `Quaternion.FromToRotation(Vector3.up, contact.normal)`.
     - Alterna aleatoriamente entre instanciar `SproutingFlowerPatch` ou `BouncyMushroomCluster`.
     - Dispara partículas de explosão de pólen (`SeedBurstParticles`) e áudios espaciais.
     - Inicia coroutine de desaparição em fumaça mágica e respawn suave no ninho do pedestal após 3.0s.
2. **`SproutingPlantLifecycle.cs`:**
   - Animação matemática procedural de crescimento elástico: escala `0 -> 1.15 -> 1.0` nos primeiros 0.6s (`Mathf.SmoothStep` com overshoot elástico).
   - Efeito contínuo de brisa suave: oscilação sinusoidal lenta (`Mathf.Sin(Time.time * freq) * amplitude`) nos eixos X e Z para conferir vida ao canteiro.
   - Partículas ambiente de pólen/brilho suave flutuando acima das flores.
   - Componente de interação tátil nos cogumelos: ao colidir com o paciente ou objetos, comprime verticalmente (`squash`) e expande horizontalmente (`stretch`), tocando `mushroom_bounce_boing.wav`.
3. **`Task12_SceneAndPrefabBuilder.cs` (em `Assets/Editor/AutoSetup/`):**
   - Criação e montagem automatizada dos prefabs em `Assets/Prefabs/Interactive/`.
   - Setup dos pedestais de pinhões ao longo da trilha da Floresta (início, meio e próximo à ponte).
   - Configuração de iluminação de destaque e partículas de ninho mágico.
   - Execução e verificação com 0 erros de compilação.
   - Rotina automatizada de Visual QA gerando screenshots de alta resolução (1920x1080).

---

### 5. Pontos de Posicionamento na Trilha (`Floresta_e_Rio.unity`)
- **Estação de Sementes 1 (Início da Trilha):** `(56.20f, 4.55f, 62.40f)` — Pedestal com pinhão ao alcance visual imediato do paciente ao iniciar a caminhada.
- **Estação de Sementes 2 (Meio da Trilha):** `(48.50f, 4.45f, 63.20f)` — Pedestal na margem esquerda da trilha, convidando a um arremesso em direção à clareira ou ao rio.
- **Estação de Sementes 3 (Aproximação da Ponte):** `(39.50f, 4.60f, 65.50f)` — Pedestal próximo à ponte de madeira.

---

### 6. Critérios de Visual QA
- Imagens salvas em `.agents/visual_qa/`:
  - `TASK-12_Magic_Pinecone_Trail.png`: Pedestal e pinhão místico na margem da trilha.
  - `TASK-12_Flower_Patch_Sprouted.png`: Canteiro de flores multicoloridas brotadas rentes ao terreno.
  - `TASK-12_Mushroom_Cluster_Sprouted.png`: Canteiro de cogumelos saltitantes brotados na margem.
- Verificação de shaders (zero magenta/Standard lit shader correto).
- Verificação de assentamento no chão (zero clipping e zero flutuação).
- Trilha do exoesqueleto permanece 100% desobstruída para a locomoção do paciente.
