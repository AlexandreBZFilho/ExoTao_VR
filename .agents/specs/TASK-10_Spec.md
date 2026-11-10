# Especificação Técnica: TASK-10 — Seletor de Mapas por Esferas de Portal 3D com Mini-Dioramas e Pedestal (WorldPortalSphereSelector)

## 1. Visão Geral & Contexto Ergonômico de Reabilitação VR (ExoTao)
No contexto de reabilitação motora em pacientes utilizando o exoesqueleto ExoTao, interfaces 2D planas e botões tradicionais podem gerar fadiga e baixa imersão espacial. A **TASK-10** introduz uma mecânica tátil e imersiva inspirada nos melhores padrões da Realidade Virtual (estilo *The Lab* e *SteamVR Destinations*):
- O paciente interage com uma **Esfera de Portal Tridimensional** contendo um **Mini-Diorama da Floresta e Rio** em escala miniatura dentro de uma redoma de vidro transparente.
- A seleção de mapa conta com **Dupla Mecânica de Ativação**:
  1. **Snap no Pedestal Tecnológico (`PortalPedestalSocket`):** Encaixar a esfera no berço magnético de um pedestal sci-fi no chão da sala aciona um feixe de luz vertical, pulso emissivo no anel e carrega a cena da Floresta após uma celebração audiovisual de 1.2s.
  2. **Interação "Mergulho de Cabeça" (Headset Proximity Teleport):** Puxar a esfera com as mãos VR até a altura dos olhos/visor (`Vector3.Distance(orb.position, headCamera.position) < 0.22m`) gera uma sensação de "mergulhar" no mundo miniatura, com feedback sonoro etéreo crescente e transição dimensional suave (*fade out* para a cena da Floresta).
- **Acessibilidade para Pacientes no Exoesqueleto:**
  - Compatibilidade nativa com **Force Grab** (atração magnética por raio laser a distância, sem necessidade do paciente se inclinar ou sair da postura ergonômica).
  - **Auto-Respawn Inteligente:** Se a esfera for solta e cair no chão ou se distanciar, ela retorna suavemente ao berço do pedestal.

---

## 2. Escala Métrica Real VR (1 unidade Unity = 1 metro)
- **Pedestal Sci-Fi (`PortalPedestalSocket`):**
  - **Altura total da coluna:** `0.88m` (topo do berço a `Y = 0.88m`), ideal para alcance confortável de um paciente sentado ou em pé no exoesqueleto.
  - **Diâmetro da base no chão:** `0.48m` (base chanfrada octogonal/cilíndrica pesada para solidez visual).
  - **Fuste central:** Diâmetro `0.22m` com canais verticais de condução de energia e anéis de suporte.
  - **Cúpula / Berço Superior:** Diâmetro externo `0.32m`, com depressão côncava de `0.19m` de diâmetro e `0.04m` de profundidade para acomodar a esfera.
  - **Anel de Luz Emissivo:** Anel circular embutido no aro superior do berço com raio `0.12m` e largura `0.025m`.
- **Esfera de Portal (`PortalOrbDiorama_Forest`):**
  - **Diâmetro total da redoma esférica:** `0.18m` (raio `0.09m`). Tamanho ideal para empunhadura com uma única mão VR (`XRGrabInteractable`).
  - **Hierarquia Interna:**
    - `Orb_Dome_Glass`: Esfera translúcida exterior, superfície reflexiva suave com transparência calibrada (Alpha ~0.28, smoothness ~0.95), sem shaders rosa.
    - `Orb_Inner_Diorama`: Nó filho com pivô perfeitamente centralizado em `(0, 0, 0)`, contendo a malha do mini-cenário:
      - **Mini-Terreno:** Disco circular de relevo suave com diâmetro de `0.14m` e relevo de montículo.
      - **Mini-Rio:** Sulco sinuoso em curva "S" cortando o disco, com malha aquática azul vívida.
      - **Mini-Pinheiros:** 5 árvores em miniatura distribuídas pelas margens do rio (troncos de `0.015m` a `0.035m` de altura com copas cônicas estilizadas).
      - **Mini-Maçãs:** Pequenas esferas vermelhas (`0.005m` de raio) espalhadas perto das copas e na grama.
  - **Rotação Contínua:** O diorama interno gira suavemente a `18.0°/seg` no eixo Y, conferindo vida e dinamismo ao objeto estático.

---

## 3. Posicionamento Espacial no `Menu_Principal.unity`
- **Posição do Jogador (XR Origin):** `(4.50, -0.026, 2.31)`, olhando em direção a `Canvas Menu Principal` a `Z = 3.94`.
- **Posição do Pedestal (`PortalPedestalSocket`):** `(5.20, 0.00, 2.90)`:
  - Fica posicionado à frente e à direita do paciente (ângulo de ~+38° em relação ao olhar frontal), alinhado ergonomicamente com o painel `Panel Mapas Exploráveis` do Canvas (`X = 5.65, Z = 3.72`).
  - Não obstrui nenhum botão nem a visão do menu principal.
  - Altura do chão: `Y = 0.000` (assentado solidamente sobre `floor_tile (8)`).
- **Posição Inicial da Esfera:** Apoiada no berço côncavo do pedestal em `(5.20, 0.96, 2.90)`.

---

## 4. Pipeline de Áudio Procedural
Os 4 clipes sonoros são sintetizados procedurais em 44.1 kHz, 16-bit mono WAV:
1. `portal_orb_hum.wav`: Zumbido sci-fi constante (ondas senoidais moduladas a 196 Hz e 294 Hz com harmônicos suaves) em loop contínuo no `AudioSource` da esfera.
2. `portal_socket_snap.wav`: Som de encaixe magnético ressonante com transiente seco (click metálico) seguido de ressonância eletromagnética decrescente (440 Hz -> 880 Hz -> 220 Hz).
3. `portal_teleport_whoosh.wav`: Woosh dimensional expansivo de 2.0s com filtro de varredura grave (sub-bass 60 Hz até 480 Hz) e ruído estéreo atenuado.
4. `portal_headset_proximity.wav`: Tom cristalino cintilante/etéreo (tríade maior 523 Hz, 659 Hz, 784 Hz modulada em sino com trêmolo espacial) cuja intensidade escala dinamicamente ao aproximar do visor VR.

---

## 5. Materiais e Shaders (Resilientes a Render Pipeline)
Utilização estrita de `MaterialShaderHelper` para evitar quebras de shader:
- `M_Orb_Glass`: Shader Standard (modo Transparent / Fade) com cor `Color(0.85, 0.95, 1.0, 0.28)`, Smoothness `0.95`, Metallic `0.10`.
- `M_Orb_Terrain`: Verde grama estilizado `Color(0.22, 0.58, 0.24)`, Smoothness `0.20`.
- `M_Orb_River`: Azul ciano vibrante `Color(0.05, 0.65, 0.95)`, com emissão suave `Color(0.02, 0.35, 0.60)` intensidade `0.8f`.
- `M_Orb_PineTrunk`: Marrom madeira aconchegante `Color(0.38, 0.24, 0.14)`.
- `M_Orb_PineFoliage`: Verde floresta profundo `Color(0.12, 0.42, 0.18)`.
- `M_Orb_Apple`: Vermelho maçã brilhante `Color(0.92, 0.12, 0.14)`.
- `M_Pedestal_Chassis`: Titânio fosco escuro `Color(0.14, 0.16, 0.19)`, Smoothness `0.65`, Metallic `0.70`.
- `M_Pedestal_Trim`: Metal cromado polido `Color(0.82, 0.85, 0.88)`, Smoothness `0.90`, Metallic `0.85`.
- `M_Pedestal_RingEmissive`: Ciano de alta emissão `Color(0.0, 0.90, 1.0)`, intensidade `3.2f`.
- `M_Pedestal_CradlePad`: Borracha preta texturizada `Color(0.08, 0.08, 0.10)`, Smoothness `0.15`.
- `M_Pedestal_Beam`: Material aditivo translúcido para o feixe vertical de ativação.

---

## 6. Automação, Visual QA & Critérios de Aceite
- **Screenshots Obrigatórios:**
  1. `TASK-10_Pedestal_Overview.png`: Perspectiva da sala a partir do visor VR do paciente enquadrando pedestal, esfera e menus circundantes.
  2. `TASK-10_Orb_Diorama_Closeup.png`: Close-up macro em alta resolução mostrando a transparência límpida da redoma esférica e os detalhes nítidos do mini-diorama da floresta em seu interior.
  3. `TASK-10_Orb_Socket_Docked.png`: A esfera assentada perfeitamente no berço magnético com anel emissivo aceso e alinhamento geométrico perfeito.
- **Critérios de Validação Visual:**
  - Zero shaders magenta/rosa.
  - A redoma externa deve ser transparente, permitindo ver com nitidez o relevo verde, os pinheiros e o rio.
  - O pedestal deve tocar o piso `floor_tile` sem flutuação nem afundamento.
  - Zero erros de compilação ou exceções no console da Unity.
