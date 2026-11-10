# TASK-13: Fauna e Flora Reativa ao Laser (Cogumelos Xilofone Pentatônicos, Pássaros Guia e Lanternas Rúnicas)
## Especificação Técnica & Arquitetura de VR

**Data:** 10/11/2026  
**Módulo:** Floresta & Gamificação Sensorial  
**Alvo:** Unity 6 LTS (6000.0.85f2) | XRI 2.6.5 | Exoesqueleto ExoTao  
**Autor:** Agente Especialista VR Rehab Gamification  

---

### 1. Visão Geral e Propósito Clínico
Na reabilitação neuromotora com o exoesqueleto ExoTao, o paciente caminha ao longo de uma trilha arborizada. Para enriquecer o ambiente multissensorial, estimular a exploração visual voluntária, o foco atencional e a coordenação motora fina do apontamento com o raio laser das mãos, a **TASK-13** introduz elementos reativos da fauna e da flora da floresta:

1. **Recanto dos Cogumelos Xilofone Pentatônicos (`MusicalMushroom`):**
   - Um canteiro em arco com 5 cogumelos bioluminescentes esculpidos em escala gradual (~0.20m a ~0.45m).
   - Cada cogumelo está afinado em uma nota da escala pentatônica maior de Dó (C4, D4, E4, G4, A4).
   - Ao varrer o raio laser ou tocar no chapéu do cogumelo, ele executa uma animação elástica de *Squash & Stretch*, pulsa sua luz bioluminescente e reproduz sua nota musical nítida de marimba. A escala pentatônica garante harmonia consonante contínua em qualquer ordem de execução, permitindo que o paciente componha melodias agradáveis instantaneamente.

2. **Lanternas Rúnicas Rústicas de Trilha (`PathLanternInteractable`):**
   - 3 totens de pedra e madeira entalhada com cúpula de ferro forjado dispostos ao longo do trajeto (Início, Meio e Ponte).
   - Ao serem ativadas com o laser ou toque, acendem uma chama mágica dourada com luz pontual suave, faiscamento e runas brilhantes na madeira, proporcionando sensação de calor, iluminação e progresso na caminhada.

3. **Pássaro Guia da Floresta (`ReactiveForestBird`):**
   - Um passarinho low-poly estilizado repousando inicialmente sobre o mourão de cerca na largada da trilha.
   - Ao receber o raio laser ou detectar a aproximação do paciente, o pássaro gorjeia alegremente, bate as asas proceduralmente e alça voo em curva Bezier suave até o próximo marco (o topo da lanterna intermediária e, em seguida, a balaustrada da ponte de madeira), incentivando a continuidade do passo do paciente.

---

### 2. Especificação Métrica Real de VR (1 unidade = 1 metro)

| Asset / Objeto | Altura (m) | Diâmetro/Largura (m) | Pivô Espacial | Materiais / Cores |
| :--- | :---: | :---: | :---: | :--- |
| **`XylophoneMushroom`** | 0.20m a 0.45m | 0.24m a 0.40m | Base do caule (Y=0.0m) | Caule bege orgânico (`M_Mushroom_Stem`), chapéu com cor base suave e lamelas/pintas com emissão vibrante (`M_Mushroom_Cap_C4` Ciano, `D4` Esmeralda, `E4` Ouro, `G4` Laranja, `A4` Violeta) |
| **`RunePathLantern`** | ~0.95 m | ~0.34 m | Base de pedra (Y=0.0m) | Base de pedra rústica (`M_Lantern_Stone`), poste entalhado com runas (`M_Lantern_Wood`, `M_Lantern_Runes`), cúpula de ferro forjado (`M_Lantern_Iron`) e núcleo de chama (`M_Lantern_Flame`) |
| **`LowPolyForestBird`** | ~0.22 m | ~0.20 m (envergadura) | Pés / Base de repouso (Y=0.0m) | Penugem do peito e cabeça amigável azul/laranja (`M_Bird_Body`), bico e patinhas amarelas (`M_Bird_Beak`), olhos pretos com brilho e asas móveis (`Wing_L`, `Wing_R`) |

---

### 3. Síntese de Áudio Procedural (`Assets/Audio/Generated/Task13/`)
- `marimba_C4.wav` (261.63 Hz): Timbre amadeirado ressonante de marimba/xilofone com harmônicos pares suaves e decaimento acústico natural de 0.8s.
- `marimba_D4.wav` (293.66 Hz): Nota Ré4 afinada.
- `marimba_E4.wav` (329.63 Hz): Nota Mi4 afinada.
- `marimba_G4.wav` (392.00 Hz): Nota Sol4 afinada.
- `marimba_A4.wav` (440.00 Hz): Nota Lá4 afinada.
- `lantern_ignite_whoosh.wav`: Som aveludado de ignição de fogo místico acompanhado de crepitar sutil (0.6s).
- `bird_chirp_cheerful.wav`: Gorjeio duplo melódico ascendente e alegre de rouxinol/canário (0.45s).
- `bird_wing_flutter.wav`: Bater de asas suave e rítmico (0.5s).

---

### 4. Arquitetura de Scripts C# (`Assets/Scripts/Interactive/Task13/`)

1. **`MusicalMushroom.cs`:**
   - Implementa `XRSimpleInteractable` e detecção de raio laser / toque.
   - Controla animação elástica procedural: compressão vertical (`Squash` Y=0.6, XZ=1.25) com retorno elástico amortecido (*Damped Harmonic Oscillator*).
   - Controla pulso luminoso: eleva a intensidade emissiva (`_EmissionColor`) do material do chapéu temporariamente via `MaterialPropertyBlock`, retornando suavemente ao brilho basal bioluminescente.
   - Toca a nota correspondente com espacialização 3D e resposta imediata sem atraso de latência.

2. **`PathLanternInteractable.cs`:**
   - Detecta hover ou clique com o laser VR ou aproximação física.
   - Estado de ignição: ativa objeto de chama com rotação e oscilação de escala procedural, ativa `Light` suave (cor dourada âmbar `RGB(255, 180, 70)`, intensidade ~1.8, alcance ~4.0m) e habilita a emissão mágica das runas esculpidas no poste.
   - Sistema de partículas suaves de brasas flutuantes ascendentes.

3. **`ReactiveForestBird.cs`:**
   - Máquina de estados: `PerchedIdle`, `Alerted`, `FlyingToWaypoint`, `Landing`.
   - Ao receber foco do laser ou aproximação, emite som de gorjeio, inicia o bater de asas procedural rotacionando `Wing_L` e `Wing_R` no eixo Z (`Mathf.Sin(Time.time * flapSpeed) * flapAngle`), e desloca-se suavemente ao longo de uma curva parabólica/Bezier cúbica até o próximo ponto de poleiro.
   - Ao aterrissar, recolhe as asas, ajusta a rotação para o poleiro e faz pequenas rotações aleatórias da cabeça (*Head Pecks/Look Around*).

4. **`Task13_SceneAndPrefabBuilder.cs` (em `Assets/Editor/AutoSetup/`):**
   - Criação automatizada de Prefabs em `Assets/Prefabs/Interactive/`.
   - Implantação e assentamento no terreno da cena `Assets/Scenes/Floresta_e_Rio.unity`.
   - Captura e inspeção visual automatizada de alta resolução (1920x1080).

---

### 5. Coordenadas Espaciais no Mapa da Floresta (`Floresta_e_Rio.unity`)
- **Recanto dos Cogumelos Xilofone (Arco Musical):**
  - Centro do arco: `(56.20f, 4.83f, 59.20f)` — Na relva aberta e limpa à esquerda da largada, com visibilidade direta para o paciente sentado/em pé no exoesqueleto.
  - 5 cogumelos dispostos em curvatura ergonômica:
    - M1 (C4, Cyan): `(56.60f, 4.83f, 58.75f)`
    - M2 (D4, Emerald): `(56.40f, 4.83f, 59.05f)`
    - M3 (E4, Gold): `(56.20f, 4.83f, 59.35f)`
    - M4 (G4, Orange): `(55.95f, 4.83f, 59.60f)`
    - M5 (A4, Violet): `(55.70f, 4.83f, 59.85f)`
- **Lanternas Rúnicas:**
  - Lanterna 1: `(56.60f, 4.63f, 61.50f)` — Na entrada da trilha, logo após a largada.
  - Lanterna 2: `(47.20f, 4.46f, 63.80f)` — No meio do percurso florestal.
  - Lanterna 3: `(36.50f, 4.76f, 66.80f)` — Na aproximação final antes da ponte de madeira.
- **Poleiros do Pássaro Guia:**
  - Poleiro 1 (Largada): `(54.44f, 4.52f, 65.08f)` — No topo do mourão da cerca de madeira ao lado da trilha.
  - Poleiro 2 (Meio): `(47.20f, 5.41f, 63.80f)` — No topo da cúpula da Lanterna 2.
  - Poleiro 3 (Ponte): `(35.50f, 4.48f, 68.30f)` — Na balaustrada de entrada da ponte rústica de madeira.

---

### 6. Critérios de Visual QA
- Imagens salvas em `.agents/visual_qa/`:
  - `TASK-13_Xylophone_Glade.png`: Visão de frente dos 5 cogumelos em arco na relva com cores bioluminescentes vibrantes.
  - `TASK-13_Lantern_Ignited.png`: Visão em close da lanterna rúnica acesa com chama, luz dourada suave e runas brilhantes no poste.
  - `TASK-13_Guide_Bird_Perched.png`: Close-up do pássaro guia pousado no mourão da cerca com penas coloridas e postura carismática.
- Verificação visual de materiais (zero magenta/Standard shader compatível).
- Verificação de escala e contato com superfícies (zero flutuação ou enterramento em geometria).
