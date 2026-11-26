# TASK-07 — Veredito do Auditor [E] (Pistola de Balões) — 2026-11-25

## VEREDITO: FAIL (1 item bloqueante: folga mínima de 0,25 m)

Tudo o mais passa com evidência. O único item reprovado é o 6 (folga). Ele não depende de código: o `[D]` avaliou 11 superfícies e nenhuma tem 0,25 m livres (maior folga possível 0,208 m em `counter_1`). A decisão de layout é do usuário (ver "Ordens").
Observação sobre o `[A]`: o Scout errou ao dizer que `counter_3_c (2)` estava vazio. O `BuilderResult` mostra `pc_case (2)`, teclado e monitor sobre ela (folga -0,005 m).

## Checklist

| # | Item | Evidência | Resultado |
|---|------|-----------|-----------|
| 1 | Cano para a frente na mão; balão sai pelo cano | `Gun_In_Hand.png` e `_Top.png`: eixo azul (+Z da mão) coincide com o cano, inclinado ~10° para cima. JSON `gunDotMuzzleVsAttach` = 0,985 (> 0,95) e bico 0,17 m à frente da pegada. PlayMode 1b/1c/2d: balão andou 0,50 m ao longo do cano | PASS |
| 2 | Balão de 0,35–0,45 m, bonito, cordinha coerente | PlayMode 2e: 0,415 m (escala 1,38); cordinha 0,47 m. `Size_Compare.png`: régua de 0,30 m = 322 px, balão grande = 446 px = 0,415 m. Redondo, com nó visível, sem partes faltando. 940 tris | PASS (ressalva abaixo) |
| 3 | Pool e regras VR no código | Pool 8+1 (`BalloonPool.cs`), 12 `FloatingBalloon` antes e depois de 9 infladas (PlayMode 5c). O único `Instantiate`/`Destroy` está no construtor do pool (`BalloonPool.cs:50,53`). Grep: sem `.material`, `FindObjectsOfType`, `LoadScene` nem `TODO`. `GetComponent` só em Awake/Start/eventos (`FloatingBalloon.cs:390,396` em trigger/colisão, raro). Sem `Light` no balão nem na pistola. `CollisionDetectionMode` Discrete (`FloatingBalloon.cs:148`, prefabs 0). `sharedMaterial` em `FloatingBalloon.cs:566` | PASS |
| 4 | Comportamento de estouro e física | PlayMode: 6a/6b tapinha empurra e não estoura; 7a gatilho estoura; 8a quica a 1,4 m/s; 8b impacto de 6 m/s estoura; 9a/9b teto 2,80 m sem travar; 10a/10b murcha após a vida útil e volta ao pool; 3a solta sozinho no máximo. 33/33 PASS | PASS |
| 5 | Áudio e polycount | Medido: `inflate_stretch` -10,0 dBFS, `pop_soft` -8,0 dBFS, `release_whoosh` -9,0 dBFS (todos ≤ -6). Balão 940 tris (800–1800) e pistola 1740 (≤ 1800) | PASS |
| 6 | Layout: aterramento, folga, (0,0,0), duplicatas, demos | Gap 0,001 m (ok). `objectsAtOriginZero` e `duplicateObjectsFound` vazios. 1 pistola e 3 demos, todos abaixo do teto, sem sobreposição. **`minClearanceMeters` = 0,208 m (pistola ↔ `laptop_variant (1)`) < 0,25 m** | **FAIL** |

## O que vi em cada PNG
- `View1_Front.png`: bancada `counter_1` com a pistola deitada na ponta do tampo, voltada para a sala. Três balões brilhantes (ciano, vermelho e amarelo) pairam no ar, sem atravessar parede ou móvel. Sem faixa de horizonte no equador.
- `View2_Side_Grounding.png`: vista rasante. A pistola está de lado sobre o tampo (grip preto horizontal) e o tanque azul fica por cima. Há sombra no tampo e nenhuma interpenetração visível. O grip fica ~1 cm acima do tampo porque a pistola repousa sobre o corpo, o que é natural.
- `View3_Closeup.png`: close do bico dourado, tanque, receiver e grip. Modelo íntegro, sem partes invisíveis, apoiado no tampo.
- `Balloons_Floating_Room.png`: panorama da sala com os 3 demos no ar, abaixo do teto, sem tocar a parede. O reflexo do equador não aparece na sala.
- `Gun_In_Hand.png`: eixo azul saindo da pegada para a frente, ligeiramente para cima, e o cano na mesma direção. Verde = Y e vermelho = X coerentes.
- `Gun_In_Hand_Top.png`: vista de cima. O cano está alinhado ao eixo azul (+Z), sem desvio lateral.
- `Balloon_Inflating.png`: balão ciano preso ao bico, crescendo para a frente com o nó no bico e formato de gota. A faixa clara no equador é só o céu padrão refletido, como já esperado no estúdio.
- `Balloon_Size_Compare.png`: balão antigo (~0,09 m), balão novo (~0,41 m), régua de 0,30 m e pistola. Proporção boa.

## Bugs e ressalvas (nenhum bloqueante, exceto o item 6)
1. `FloatingBalloon.cs:265`: `SoftPop` começa a curva em k≈0,86, então o balão encolhe para ~35% em 1 frame (um "snap" visual). Um estouro suave do 9º balão deveria começar em k=0.
2. `BalloonInflaterGun.cs:153` junto de `FloatingBalloon.cs:222`: com a variação de +8% (1,4×1,08 = 1,512) o balão tem 0,454 m, 4 mm acima do limite de 0,45 m. O caso típico (0,415 m) está dentro do limite.
3. `FloatingBalloon.cs:222`: `Launch` chama `SetScaleImmediate(scale)` e perde o squash/stretch no frame da soltura (pequeno "pop" de forma).
4. `FloatingBalloon.cs:527-531`: com o jogador a mais de 8 m, `SetPositions` ainda roda todo `LateUpdate` por balão. Deveria sair cedo depois da primeira vez.
5. `BalloonPool.cs:45`: o `Root` do pool não é destruído nem reparentado se a pistola for destruída (órfãos na cena). Baixo impacto.
6. `FloatingBalloon.cs:336`: `rb.Sleep()` com o jogador a mais de 8 m congela o balão no ar (sem empuxo) até o jogador voltar. É aceitável por performance.
7. Prefab do balão (`Balloon_Body`, `m_CastShadows: 1`): os balões lançam sombra dinâmica. É só o corpo; sombras de luz extra seriam o problema, e não há `Light`. Recomendo `Cast Shadows = Off`.
8. A pistola nunca vai a `ContinuousDynamic` ao ser segurada (só Discrete). Cumpre a regra de repouso e é mais leve, mas pode atravessar o tampo se for arremessada muito rápido.
9. `Task07_SceneAndPrefabBuilder` precisa de `AssetDatabase.SaveAssets()` no fim; não verifiquei o disco. A cena e os prefabs aparecem como `M` no git.

## Ordens
- **Item 6 (folga ≥ 0,25 m): decisão do usuário.** A maior folga possível é 0,208 m (`counter_1`, vizinho `laptop_variant (1)`). As opções são:
  1. aceitar 0,208 m (a pistola fica a 4,2 cm do critério e visualmente isolada);
  2. mover o laptop de `counter_1` (ou o `ArcadeBallSpawner`/bobblehead vizinhos) para liberar ≥ 0,25 m;
  3. escolher outra bancada.
  O usuário escolhe. Se escolher 2 ou 3: **`RE-RUN [D]`** (reposicionar, rodar Purger antes e depois, refazer `SpatialAudit.json` e as 3 capturas).
- **`RE-RUN [C]` (opcional, melhoria):** itens 1 a 4 e 7 da lista de bugs. Não bloqueiam.
- Nenhum `RE-RUN [B]` necessário.

---
## Adendo do orquestrador (RE-RUN [C], 2026-11-25)
Ressalvas não bloqueantes do [E] corrigidas e retestadas (Play Mode 33/33):
- `SoftPop` agora usa duração própria (0,35 s) sem salto de escala.
- `maxScale` 1,36 (±8% => 0,37-0,44 m de altura; teste mediu 0,430 m).
- Longe do jogador (> 8 m) e parado no teto/ponto de origem o balão dorme sem forças; a cordinha não é reescrita enquanto ele dorme.
- O pool é destruído junto com a pistola (`OnDestroy`).
- Balões sem sombra dinâmica (`ShadowCastingMode.Off`).
- Pistola: `ContinuousDynamic` ao ser segurada, volta a `Discrete` 3 s após soltar.
Item 6 (folga 0,25 m) continua FAIL: depende de decisão de layout do usuário.

## Decisão do usuário sobre o item 6 (2026-11-25)
O usuário pediu para manter a pistola na mesa anterior, `counter_3_a (1)`, aceitando a folga possível ali (0,160 m da luminária `table_lamp (1)`; gap da malha 0,001 m; (0,0,0) limpo; zero duplicatas). Builder refeito com `ForcedCounter = "counter_3_a (1)"`, capturas regeneradas e Play Mode 33/33 nessa posição.
**Veredito final: PASS com exceção de layout aprovada pelo usuário** (falta só o playtest no óculos).
