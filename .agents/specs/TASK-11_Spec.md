# Especificação Técnica: TASK-11 — Sistema de Maçãs Interativas (Árvores Sacudíveis + Cestas de Pontuação + Splash no Rio)

## 1. Visão Geral & Contexto Clínico/Ergonômico (VR Rehab ExoTao)
No mapa da Floresta (`Assets/Scenes/Floresta_e_Rio.unity`), o paciente caminha assistido pelo exoesqueleto robótico ExoTao. Para tornar a sessão de reabilitação motora e cognitiva altamente engajadora, a **TASK-11** implementa uma mecânica completa de gamificação ambiental baseada na colheita e arremesso de maçãs:
- **Árvores Sacudíveis (`ShakeableFruitTree`):** Árvores frutíferas exuberantes posicionadas às margens da trilha. Ao apontar o raio laser do controle VR (`XRRayInteractor`) para a copa da árvore e pressionar o gatilho, a copa reage com uma oscilação física/harmônica natural, solta partículas de folhas cadentes, emite o som de farfalhar de folhas (`tree_leaves_rustle.wav`) e faz cair de 2 a 4 maçãs maduras com física realista.
- **Maçãs Interativas (`InteractiveApple`):** 
  - Herdam de `ErgonomicGrabInteractable` com suporte a **Force Grab** (atração magnética a distância para que o paciente não precise se curvar).
  - Implementam `IAimAssistTarget` e integram com o sistema balístico `ThrowAimAssist`.
  - São rastreadas pelo Drone Guia (`DroneFetchTarget`): se o paciente arremessar uma maçã para longe na floresta (> 3m), o Drone voa até ela, captura-a com o feixe trator e entrega de volta na altura confortável do peito do paciente (~1.15m).
  - **Física de Splash e Flutuação no Rio:** Se a maçã cair no plano d'água (`Plane` em Y = 2.50m), ela gera partículas de borrifo d'água, toca `water_splash.wav` e passa a flutuar com oscilação hidrodinâmica suave.
- **Cestas de Vime para Pontuação (`AppleCollectionBasket`):**
  - Cestas rústicas trançadas de vime com poste indicador e placar 3D destacando as pontuações (+10 PTS para maçã vermelha normal, +50 PTS para maçã dourada bônus).
  - Integradas com `IAimAssistTarget`, permitindo que arremessos do paciente recebam assistência magnética suave na parábola de lançamento.
  - Ao pontuar, tocam sino alegre (`basket_score_bell.wav`), disparam partículas de confetes e projetam um popup numérico flutuante com curva elástica.

---

## 2. Escala Métrica Real VR (1 unidade Unity = 1 metro)
- **Maçã Vermelha Interativa (`InteractiveRedApple`):**
  - **Diâmetro:** `0.085m` (raio `0.0425m`), com leve achatamento nos polos e depressões orgânicas.
  - **Haste/Pedúnculo:** Comprimento `0.024m`, espessura `0.003m`, levemente curvado.
  - **Folha Verde:** Folha estilizada low-poly de `0.035m x 0.018m` na junção da haste.
  - **Massa no Rigidbody:** `0.18 kg`.
- **Maçã Dourada Bônus (`GoldenBonusApple`):**
  - **Diâmetro:** `0.090m` (raio `0.045m`), malha geométrica facetada refinada com brilho áureo.
  - **Efeito Visual:** Material de ouro polido com partículas cintilantes em loop (`golden_apple_sparkle.wav` em proximidade ou ao segurar).
  - **Pontuação:** `50 PTS`.
- **Cesta de Vime (`WickerCollectionBasket`):**
  - **Boca aberta superior:** Diâmetro `0.52m`, facilitando a recepção de arremessos mesmo para pacientes com amplitude de movimento reduzida.
  - **Base inferior no solo:** Diâmetro `0.40m`.
  - **Altura do cesto:** `0.38m`.
  - **Alças trançadas laterais:** Arco semicircular de `0.08m` de raio em cada lado.
  - **Poste & Placa de Madeira:** Poste rústico de `0.70m` de altura com placa entalhada `0.40m x 0.16m` com inscrição em relevo e `TextMeshPro` 3D indicando pontuações.
  - **Sensor de Detecção:** Trigger interno no plano superior da cesta (`0.48m` de diâmetro) com verificação de sentido descendente de entrada (evita falsos positivos por baixo).
- **Árvores Frutíferas Sacudíveis (`ShakeableFruitTree`):**
  - Altura total de `5.5m` a `6.5m`, copa frondosa a partir de `2.2m` de altura, permitindo que as maçãs caiam de uma altura visível e com tempo de trajetória parabólica.

---

## 3. Mapeamento Espacial e Coordenadas na Cena `Floresta_e_Rio.unity`
- **Posição Inicial do Jogador:** `(58.00, 3.10, 60.70)` olhando na direção `(0, 286.7, 0)` (Vetor de avanço: `[-0.958, 0, 0.287]`).
- **Ponte de Madeira:** `(32.78, 2.96, 69.21)`.
- **Plano do Rio / Água:** `Plane` em `(50.00, 2.50, 78.00)` com nível de água na cota `Y = 2.50m`.
- **Pontos de Instalação das Árvores Sacudíveis:**
  - **Árvore 1 (Início da Trilha - Direita):** `(53.50, 4.80, 58.50)` — Apoiada no terreno lateral a 3.2m do centro da trilha. Copa ampla pendendo levemente em direção à trilha.
  - **Árvore 2 (Meio da Trilha - Esquerda/Margem do Rio):** `(45.50, 4.65, 67.50)` — Perto do declive da água, permitindo que maçãs arremessadas caiam no rio ou nas cestas.
  - **Árvore 3 (Antes da Ponte):** `(37.50, 4.55, 63.50)` — No trecho final antes da ponte de madeira.
- **Pontos de Instalação das Cestas de Vime:**
  - **Cesta 1 (Primeira Parada de Arremesso):** `(51.50, 4.70, 59.20)` — Cota do solo ajustada via `Terrain.SampleHeight`, a 3.0m do paciente em avanço inicial.
  - **Cesta 2 (Desafio Médio - Margem do Rio):** `(43.00, 4.60, 67.00)` — A 4.0m de distância, com vista simultânea para o rio e para a árvore 2.
  - **Cesta 3 (Desafio Longo - Próximo à Ponte):** `(35.20, 4.45, 64.00)` — Ótima linha de visão tanto da trilha quanto da cabeceira da ponte.

---

## 4. Pipeline de Modelagem 3D (Blender 5.1 CLI) & Materiais
Executado via script Python headless em `C:\Program Files\Blender Foundation\Blender 5.1\blender.exe`:
- `Assets/Models/Generated/Task11/InteractiveRedApple.fbx`:
  - Malha estilizada de maçã com pedúnculo marrom curvado e folha verde modelada. Pivô centrado no centro de massa da maçã.
- `Assets/Models/Generated/Task11/GoldenBonusApple.fbx`:
  - Malha da maçã mítica com geometria facetada e coroa superior estilizada.
- `Assets/Models/Generated/Task11/WickerCollectionBasket.fbx`:
  - Cesta de vime trançada com textura espiral estilizada, alças laterais, poste de suporte e placa de madeira entalhada.
- **Materiais Gerados via `MaterialShaderHelper`:**
  - `M_Apple_Red`: Vermelho carmesim orgânico `Color(0.85, 0.08, 0.10)`, smoothness `0.65`.
  - `M_Apple_Stem`: Marrom madeira pedúnculo `Color(0.35, 0.20, 0.10)`, smoothness `0.20`.
  - `M_Apple_Leaf`: Verde vívido translúcido `Color(0.20, 0.65, 0.15)`.
  - `M_Apple_Gold`: Ouro metálico polido `Color(1.0, 0.82, 0.18)`, metallic `0.85`, smoothness `0.90`.
  - `M_Wicker_Basket`: Marrom palha trançada `Color(0.68, 0.50, 0.28)`, smoothness `0.15`.
  - `M_Basket_SignPost`: Madeira rústica envelhecida `Color(0.48, 0.32, 0.18)`.
  - `M_Score_Text_Emissive`: Amarelo/Dourado emissivo `Color(1.0, 0.90, 0.20)`.

---

## 5. Pipeline de Efeitos Sonoros Procedurais (.wav Mono 44.1kHz 16-bit)
1. `tree_leaves_rustle.wav`: Farfalhar orgânico e volumoso de copa de folhas de árvore ao ser agitada (ruído filtrado passa-banda com modulação harmônica de baixa frequência 120-800Hz e decaimento natural de 1.4s).
2. `apple_drop_thud.wav`: Impacto amortecido de fruta fresca quicando sobre a terra e grama (senoide amortecida 110Hz -> 65Hz com transiente inicial de contato orgânico).
3. `basket_score_bell.wav`: Sino festivo de acerto na cesta (acorde maior brilhante C6-E6-G6-C7 em 1046Hz, 1318Hz, 1568Hz, 2093Hz com decaimento cintilante de 1.8s).
4. `water_splash.wav`: Som realista de objeto caindo na água do rio (borbulhas estourando, transiente rápido de impacto aquático com modulação de ruído rosa e decaimento de 1.1s).
5. `golden_apple_sparkle.wav`: Cintilância mágica contínua/acionada para a maçã dourada (glissando ascendente de tons cristalinos etéreos de 1200Hz a 2800Hz com trêmolo estéreo).

---

## 6. Critérios de Visual QA & Validação
1. **Zero Shaders Rosas:** Todos os materiais vinculados via `MaterialShaderHelper` utilizando Standard Lit shader.
2. **Perfeito Assentamento no Terreno:** As 3 cestas de vime devem estar solidamente apoiadas sobre o terreno (`Terrain.SampleHeight`) sem flutuar e sem afundar a boca da cesta.
3. **Escala Acurada:** Maçãs com ~8.5cm a 9cm de diâmetro na mão VR do paciente; cestas com ~52cm de boca aberta.
4. **Trilha Totalmente Livre:** Espaço de circulação da cadeira/paciente (largura mínima de 2.0m) 100% desobstruído.
5. **Captura de 3 Imagens Validadas:**
   - `.agents/visual_qa/TASK-11_Forest_Trail_Overview.png`: Visão da trilha da Floresta mostrando as árvores interativas e as cestas de vime nas margens.
   - `.agents/visual_qa/TASK-11_Apple_Basket_Closeup.png`: Close-up de alta definição focado na cesta de vime com as maçãs e placa de pontos.
   - `.agents/visual_qa/TASK-11_Tree_Shake_Event.png`: Cena no momento de sacudir a árvore, mostrando folhas caindo e maçãs no ar / caindo.
