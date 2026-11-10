# Especificação Técnica TASK-14: Trilha de Moedas/Orbes Flutuantes com Atração Magnética para Exoesqueleto (AdaptiveCoinTrailSystem)

## 1. Visão Geral e Propósito Clínico
Na reabilitação da marcha com o exoesqueleto robótico ExoTao, a motivação contínua do paciente para iniciar e manter ciclos de passada voluntária é crucial. O **AdaptiveCoinTrailSystem** transforma o percurso da floresta em um caminho gamificado onde moedas energéticas flutuam exatamente na altura do tronco do paciente (~1.15m acima do terreno).

A aproximação do paciente desencadeia atração magnética assistida que puxa as moedas suavemente para o peito, proporcionando feedback sonoro com pitch ascendente em sequência (Combo Streak) e pulsos hápticos nos controladores VR. Moedas bônus secretas de diamante ficam posicionadas estrategicamente nas laterais fora do alcance dos passos, incentivando a extensão voluntária dos braços e pontaria com o laser VR.

## 2. Escala Métrica e Ergonomia VR (1 unidade = 1 metro)
- **Moeda de Energia (`EnergyStarCoin`)**:
  - Diâmetro: `~0.26m` (26 cm), espessura: `0.04m` (4 cm).
  - Geometria: Disco octogonal com borda chanfrada, moldura em relevo, estrela de 5 pontas central em relevo e canais de energia emissivos ciano/ouro.
  - Ponto de Pivô: Exatamente no centro geométrico `(0, 0, 0)` para rotação pura no eixo Y sem oscilação excêntrica.
  - Flutuação: Altura ergonômica de `1.15m` acima do solo, com oscilação senoidal de amplitude `0.06m` e período de `1.8s`.
- **Moeda Bônus Secreta (`BonusDiamondCoin`)**:
  - Altura: `~0.28m` (28 cm), largura: `0.22m` (22 cm).
  - Geometria: Cristal romboédrico facetado com anel orbital tecnológico estilizado girando a 25° de inclinação.
  - Ponto de Pivô: Centro do diamante `(0, 0, 0)`.
  - Posição: Laterais da trilha (fora do alcance físico da marcha, e.g. sobre rochedos e margens do rio), coletáveis via mira do laser VR (`XRSimpleInteractable`).

## 3. Dinâmica de Movimento e Atração Magnética
- **Raio de Detecção Magnética**: `2.2 metros`.
- **Efeito Magnético**:
  - Quando a distância do paciente até a moeda cai para `< 2.2m`, o sistema inicia a atração.
  - A aceleração é suave com curva quadrática (`accel = Mathf.Lerp(1.5f, 9.0f, progress)`).
  - A moeda se curva suavemente em direção ao ponto de coleta no peito do paciente (`paciente.position + Vector3.up * 1.15f`).
  - Durante o voo magnético, emite som tridimensional de vácuo magnético suave (`coin_magnet_whoosh.wav`).
- **Coleta e Distância de Contato**:
  - Ao chegar a `< 0.35m` do centro de massa do paciente, a moeda é coletada:
    - Dispara explosão de partículas douradas cintilantes (`ParticleSystem`).
    - Dispara chime harmônico com pitch progressivo de combo (`coin_pickup_chime_combo.wav`).
    - Dispara feedback háptico nos dois controladores VR via `VRFeedbackManager`.
    - Exibe pontuação flutuante 3D (`+10`, `+50`, `Combo x2!`).
- **Coleta à Distância via Laser VR**:
  - Ray interactor com `XRSimpleInteractable`.
  - Ao receber ativação do gatilho ou hover selecionado, puxa a moeda supersônica para o controlador ou coleta instantaneamente com bônus e mini fanfarra (`secret_coin_fanfare.wav`).

## 4. Sistema de Combo e Gamificação
- Intervalo máximo entre coletas para manter o combo: `2.5 segundos`.
- Multiplicador de Combo: `1x, 2x, 3x ... até 8x`.
- Pitch Sonoro: `1.0f + (comboCount * 0.08f)` (limitado a `1.6f`).
- Pontuação:
  - Moeda Comum: `10 * ComboMultiplier`
  - Moeda Bônus: `50 * ComboMultiplier`

## 5. Mapeamento Espacial na Cena da Floresta (`Floresta_e_Rio.unity`)
A trilha de reabilitação se estende do ponto inicial da caminhada `(58.00, 4.72, 60.70)` até a entrada da ponte de madeira em `(32.78, 2.96, 69.21)`.
- Sequência de 14 Moedas de Energia (`EnergyStarCoin`) no eixo central da trilha, espaçadas em intervalos de `~1.8m`:
  1. `(57.00, y+1.15, 61.10)` - Logo à frente do paciente
  2. `(55.20, y+1.15, 61.80)`
  3. `(53.40, y+1.15, 62.40)`
  4. `(51.60, y+1.15, 63.00)` - Em frente ao recanto dos cogumelos musicais
  5. `(49.80, y+1.15, 63.70)`
  6. `(48.00, y+1.15, 64.30)`
  7. `(46.20, y+1.15, 64.80)` - Próximo à Lanterna 2
  8. `(44.40, y+1.15, 65.30)`
  9. `(42.60, y+1.15, 65.80)`
  10. `(40.80, y+1.15, 66.30)`
  11. `(39.00, y+1.15, 66.80)`
  12. `(37.20, y+1.15, 67.30)` - Próximo à Lanterna 3
  13. `(35.40, y+1.15, 67.80)` - Chegada à cabeceira da ponte
  14. `(33.50, y+1.15, 68.50)` - Sobre a ponte de madeira
- 3 Moedas Bônus Diamantes Secretas (`BonusDiamondCoin`):
  1. `Bonus_Rock_1`: `(52.80, 5.80, 60.50)` (No topo do rochedo à esquerda)
  2. `Bonus_Tree_Branch`: `(45.50, 6.20, 67.50)` (Flutuando sob a copa da árvore alta à direita)
  3. `Bonus_River_Float`: `(32.50, 4.60, 64.20)` (Flutuando sobre a água do rio ao lado da ponte)
