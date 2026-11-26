# TASK-16 (retoque da estante) — Veredito do Subagente [E]

## VEREDITO: PASS

## Critérios de reprovação (plano, seção [E])

| # | Critério | Resultado | Evidência |
|---|----------|-----------|-----------|
| 1 | Escrita de pose na raiz/âncoras/base; deslocamento da raiz | OK | Grep em `Task16/`: `TrophyShelfManager.cs:346` move o `infoCard` (UI flutuante, permitido); `:376` gira o próprio troféu; `:412` move só o filho `trophyAudio` (criado como filho no Awake, linhas 113-121). `ShelfTrophyGrabInteractable.cs` 79/80/168-172 e `DroneCrownWearable.cs` 160/259 movem só o próprio troféu/coroa. Nenhuma escrita na raiz, âncora, base ou placa. Play Mode: deslocamento 0,000 mm em todos os testes (1c, 1f, 6f). |
| 2 | Troféu volta, repouso estável, colisores sobrepostos | OK | Play Mode 36/36 PASS: retorno em 1,09-1,10 s, erro 0 mm, cinemático; encaixe próximo 0,23 s; chão 0,80 s; repouso 0,000 mm; penetração 0,00 mm. JSON: `hasOverlap=false`, menor folga entre colisores 0,020 m. |
| 3 | Texto gigante/cortado/sem acento/fontSize 1 | OK | Prefab: nenhum `m_fontSize: 1` (0,13 / 0,18 / 0,3); `labelsWithFontSize1=0`. Capturas mostram "Maçã Dourada", "Taça Estelar", "Bonsai Radiante", "Coroa do Drone/no Drone" legíveis, com acento, dentro das placas, sem letras vazando (o antigo Before mostrava letras gigantes). |
| 4 | Flutuando, folga < 0,08 m, duplicata, (0,0,0) | OK | `groundGapMeters` 0,0; folga 0,097 m (book_pile_2 (8)); `objectsAtOriginZero=[]`; `duplicateObjectsFound=[]`; 1 `TrophyDisplayStand_Instance` na cena; nenhum `Label_*` solto. |
| 5 | `.material`, MeshCollider, luz com sombra, GC, LoadScene, partículas | OK | Grep em `Task16/`: zero `LoadScene`, `.material(s)`, `TODO`, `MeshCollider`; 7b = 0 bytes em 300 frames; 0d luzes sem sombra; 0e partículas <= 40; 7d sem MeshCollider com Rigidbody. |
| 6 | Cartão, bloqueado pegável, coroa | OK | 1b/1e/7a: cartão aparece e some ~0,3 s depois; 5a bloqueado = silhueta, não pegável, placa "Bloqueado" com cadeado; 6a-6f coroa (tirar do Drone, voltar, reencaixar, sem ser puxada de volta). Interaction_1 e 5 mostram o cartão legível. |
| 7 | Visual melhor que o anterior / tris | OK | Base 1168 tris (<= 1800); troféus 460/428/658/772 (<= 800). Pedestais, anéis, placas inclinadas, halos e painel de fundo visíveis em Front/Closeup. |
| 8 | Áudio | OK | Picos medidos: shimmer -12,0 dBFS / 0,45 s; click -12,0 / 0,15 s; fanfarra -9,0 / 1,80 s; coroa -10,0 / 1,60 s. Todos <= -6 dBFS; shimmer <= 0,5 s; fanfarra <= 2 s. |

`ErgonomicGrabInteractable.cs` (Task02): `git diff --stat` vazio, não foi alterado.

## Decisões de contexto avaliadas
- Base 0,70 m / espaçamento 0,17 m, folga final 0,097 m: aceitável (alternativa prevista no plano, folga >= 0,08 m).
- Cartão à frente/acima por falta de altura na prateleira: aceitável.
- TMP `fontSize` 0,13 com auto-size e teto comum: aceitável (letras legíveis).
- Coroa começando no Drone (save): aceitável, e o teste cobre tirar/recolocar e restaura o save.
- Bug da coroa reencaixada na hora: corrigido e coberto por 6d/6c.

## Capturas inspecionadas (1 linha cada)
- Before_1/2: estado antigo, letras amarelas gigantes vazando atrás da prateleira.
- Shelf_Front: 4 troféus com halos, pedestais e placas em português com acento, legíveis.
- Shelf_Side_Grounding: vista lateral, base apoiada na prateleira, placas inclinadas, sem flutuar.
- Shelf_Closeup_Plaques: placas em close, acentos corretos, anel emissivo e latão visíveis.
- Shelf_Panorama_Room: sala com painéis e Drone; estante minúscula ao fundo e um objeto escuro grande tapando a frente; sem texto vazando.
- Interaction_1: troféu da maçã na mão com cartão "Maçã Dourada / Maçãs colhidas: 24" legível.
- Interaction_2: estante inteira à distância, tudo no lugar.
- Interaction_4: troféus de volta nos pedestais, estáveis.
- Interaction_5: cartão "Taça Estelar / Cestas: 16 · Moedas: 150" no hover, legível (cena em tom alaranjado).
- Locked_Silhouette: taça em silhueta escura, placa "Bloqueado" com cadeado.
- Crown_On_Drone: placa "Coroa no Drone" com pedestal vazio e anel apagado.

## Ressalvas não bloqueantes
1. `Shelf_Panorama_Room.png` é fraca: a estante aparece muito pequena e há uma massa preta em primeiro plano; serve só para provar a ausência de texto vazando. Se quiser entregar bonito, refazer com câmera mais próxima e sem o obstáculo.
2. Falta `Interaction_3.png` (troféu solto/voltando); a sequência 1, 2, 4, 5 cobre o essencial.
3. `Crown_On_Drone.png` mostra a placa e o pedestal vazio, não o Drone com a coroa (existe `TASK-16_Drone_Crown_Equipped.png` à parte).
4. Folga de 0,097 m entre os livros vizinhos e a base está acima do mínimo (0,08 m), mas longe da meta de 0,25 m; já documentado como limite da sala.
5. Varredura global final e certificado da TASK-16 seguem pendentes (fora deste escopo).

---

## Adendo do orquestrador (2026-11-26)

- Ressalva "falta `Interaction_3.png`" corrigida: o troféu solto a 1,5 m cai no chão e volta antes de 1,75 s, então a condição antiga nunca disparava; agora a captura sai 0,3 s depois de o retorno começar (troféu em arco voltando).
- Ressalva "panorama fraco" corrigida: câmera a 2,2 m à frente do móvel, na altura dos olhos (a posição inicial do XR Origin ficava atrás de um móvel escuro).
- Builder e Play Mode refeitos depois dos ajustes: estante no mesmo lugar (folga 0,097 m, gap 0 mm), **Play Mode 36/36**.
- `Crown_On_Drone.png` mostra a placa "Coroa no Drone" e o pedestal vazio (é o que a estante precisa mostrar); o encaixe na cabeça do Drone é provado pelo teste 6a.
- **Status:** retoque da estante PASS -> parte da estante `COMPLETED*` (falta playtest no HMD). A TASK-16 inteira continua aberta até a varredura global final e o certificado.
