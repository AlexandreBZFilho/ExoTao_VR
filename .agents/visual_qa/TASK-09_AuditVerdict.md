# TASK-09 — Veredito do Auditor [E] (Fogos de Artifício + Bobblehead)

## VEREDITO: PASS

Nenhum dos 7 critérios de FAIL do plano foi disparado. Há ressalvas não bloqueantes (abaixo). Status sugerido: `COMPLETED*` (verificado por simulação em Play Mode e capturas offscreen; falta playtest no HMD).

## Critérios de FAIL do plano (seção [E])

| # | Critério | Resultado | Evidência |
|---|---|---|---|
| 1 | Explosão perto do chão / dentro de móvel / foguete invisível / faíscas como esferas | PASS | `Fireworks_Seq_1..3`: brilho do foguete sobe em linha reta com trilha de pontos até ~y=430 px, e a explosão peônia abre bem acima do topo da TV, no centro do campo de visão. `Fireworks_Side`: explosão no ar acima da mesa, longe do tampo. `Fireworks_Variants`: 4 variantes (peônia verde-dourada, anel azul, salgueiro com riscos alongados, purpurina) todas como pontos/riscos suaves aditivos. `Before_Fireworks_t05` mostra o bug antigo (quadrados brancos sólidos) que não existe mais. Play Mode 1h: ápice 1,17 m acima do bico, livre 3,08 m. 1k: voo 0,462 s e 0 partículas de explosão durante o voo. |
| 2 | EmissionModule da explosão desligado / refs nulas no prefab | PASS | `FireworksLaunchButton.prefab`: 12 ParticleSystems, 12 `EmissionModule`, 0 com `enabled: 0`. `launchPoint`, `fireworksBurstLight` e os 2 elementos de `shows` (rocket, rocketTrail, rocketHead, burstRoot, mainBurst, crackle, flash, lowFan, burstAudio) têm todos fileID válido. `ExoMascotBobblehead.prefab`: headTransform, neckSpring, 2 antennas, 2 arms, visorRenderer, visorGlowLight, sfxBoing/sfxSoft/sfxTock preenchidos. Material `M_Fireworks_Sparkle`: shader builtin 211 (Particles/Standard Unlit), SrcBlend 5, DstBlend 1 (aditivo), PlayMode 1e PASS. |
| 3 | >250 partículas, luz com sombra, `.material`, MeshCollider, LoadScene | PASS | Grep nos 2 scripts: 0 `LoadScene`, 0 `.material/.materials`, 0 `TODO`, 0 `MeshCollider`, 0 `FindObjectsOfType`. Luzes: `LightShadows.None` em FireworksButtonInteractable.cs:175,180,603 e BobbleheadSpringToy.cs:203. Orçamento: soma maxParticles = 250; pico medido 138 vivas com 2 shows sobrepostos (1c, 1o). `Update` só existe no bobblehead (:362) e é desligado em repouso (2a, 2i). Os `GetComponent` achados (linhas 479 e 500) rodam só ao disparar/tocar, nunca por frame. GC: 0 bytes em 300 chamadas (1x, 2r). |
| 4 | Bobblehead balança menos de 18°, para antes de 2 s, "duro" no limite | PASS | Play Mode: pico 22,7° (2e); 6,9° em 1,4–1,75 s (2h); ~2,0° em 3 s; 0,000° e dormindo em ~8 s (2i); limite com tock e quique, máx 41,9° < 50° (2j). Capturas `Bobble_Seq_3/4/5`: deflexão visível para os dois lados (até ~35° no frame 5), antenas e mola acompanham, visor muda de cor com a energia. |
| 5 | Sobreposição, folga < 0,08 m, duplicata, (0,0,0), flutuação | PASS | `SpatialAudit.json`: `isOriginClean: true`, `duplicateObjectsFound: []`, `hasOverlap: false`, `maxGroundGapMeters 0.002`. Folgas: botão-TV 0,255 m, bobblehead-laptop 0,313 m, botão-alavanca 0,351 m AABB (0,496 m entre pivôs > 0,45). Cena: `Room_Fireworks_Launcher` = 0 ocorrências; 1 instância de cada fidget (Play Mode 0a). `Layout_Counter2b(_Top)`: botão e alavanca bem separados e assentados. `Layout_Bobble*`: bobblehead apoiado no tampo de counter_1, virado para o jogador. |
| 6 | Alavanca alterada | PASS | `git status`/`git diff --stat` não listam `AtmosphereLeverInteractable.cs`, `EnvironmentControlLever.fbx(.meta)`, `.prefab`, `M_Lever_*.mat`, `lever_click_notch.wav`, `atmosphere_transition_whoosh.wav` (único "lever" no status são os 2 PNGs novos). `ReworkResult`: "Alavanca intacta na cena: SIM". `Lever_Before` x `Lever_Unchanged`: visualmente idênticos (haste, bola vermelha, anel ciano, 3 LEDs, caixa, vaso e sombras na mesma posição). As 1362 px de diferença são só sombra/borda. |
| 7 | Áudio > −6 dBFS / estridente | PASS | Picos medidos (wave+struct): fireworks_launch_whistle −11,0; fireworks_burst_explosion −9,0; bobblehead_spring_boing −9,0; bobblehead_spring_soft −13,0; bobblehead_limit_tock −10,0. Duração 0,62/1,25/0,36/0,20/0,11 s. Low-pass em 6 kHz no gerador (e 4,8 kHz/2,6 kHz nas camadas de crepitar/ar). Sem loop (3c). |

## Capturas inspecionadas

- `Before_Fireworks_t02/t05/t09/t14` (t05 e t09 vistos): estado antigo, quadrados brancos sólidos espalhados e nada de explosão legível. Confirma o bug corrigido.
- `Before_Bobble`: cabeça pequena, sem antenas visíveis nem mola, mascote parado.
- `Fireworks_Seq_1`: botão pressionado com brilho; foguete já com halo logo acima do topo da TV.
- `Fireworks_Seq_2`: foguete com trilha de faíscas laranja subindo em linha reta.
- `Fireworks_Seq_3`: explosão peônia rosa/dourada ampla, acima da TV, dentro da vista, faíscas suaves.
- `Fireworks_Seq_4`: faíscas caindo devagar, esparsas, sem esferas.
- `Fireworks_Seq_5`: tudo apagado, só o botão e a mesa. A luz ambiente do canto mudou de ciano para âmbar (ver ressalva 4).
- `Fireworks_Side`: vista lateral, explosão claramente acima da mesa; botão, alavanca e vaso no tampo.
- `Fireworks_Variants`: mosaico 2x2 (peônia, anel, salgueiro, purpurina), todas legíveis e distintas.
- `Bobble_Seq_1`: parado, antenas retas, visor ciano.
- `Bobble_Seq_2`: cabeça inclinada ~25° para a esquerda, antenas defasadas, visor lilás/branco.
- `Bobble_Seq_3`: idêntica a Seq_2 (mesmo hash md5).
- `Bobble_Seq_4`: inclinada para o lado oposto (~20°), antenas em leque, mola dobrada.
- `Bobble_Seq_5`: deflexão máxima (~40°), cabeça girando, visor branco/rosa, mola esticada (momento do tock).
- `Bobble_Seq_6`: voltou ao repouso.
- `Layout_Bobble`: bobblehead no canto de counter_1 com folga ao vaso e ao laptop; botão vermelho ao fundo.
- `Layout_Bobble_Close`: modelo com cabeça grande, mola com espiras, antenas, orelhas e pedestal ciano.
- `Layout_Bobble_Side`: vista de costas/lado; cabeça escura por contraluz (só iluminação), mola visível.
- `Layout_Counter2b`: alavanca, vaso e botão de fogos no mesmo tampo, bem separados, tudo apoiado.
- `Layout_Counter2b_Top`: vista de cima confirma o espaçamento e nenhuma sobreposição.
- `Lever_Before` / `Lever_Unchanged`: idênticas a olho.

## Ressalvas não bloqueantes

1. Fora de escopo e herdadas (não são FAIL desta task): `AtmosphereLeverInteractable.cs:393` usa `ledRenderer.material`; `lever_click_notch.wav` e `atmosphere_transition_whoosh.wav` têm pico −0,72 dBFS (> −6). A alavanca está aprovada e não foi tocada. Fica para o usuário decidir.
2. `Bobble_Seq_2` e `Bobble_Seq_3` são o mesmo frame (md5 igual). A sequência pedida (pancada → máx de um lado → outro lado → limite) fica coberta por 2/4/5, mas o frame 3 é redundante. Recomendo só regerar essa captura, sem refazer código.
3. O bobblehead está perto da borda do tampo de counter_1 (a base fica a poucos centímetros do canto, sem sair do móvel; gap vertical 0,0 mm). Aceitável. Vale conferir no HMD se o paciente não esbarra na beirada.
4. A cor da luz ambiente na vizinhança do botão muda entre frames (ciano em Seq_1–4, âmbar em Seq_5; âmbar/verde/ciano nas variantes). Parece ser o ciclo de atmosfera/luz de cena durante a captura, não o flash (a luz do flash tem LightShadows.None e fade de 0,3 s). Confirmar no HMD que o flash não deixa residual de cor.
5. Colateral: o Unity reserializou `M_Water_Splash.mat` (+`_EMISSION`), `M_Magic_Burst.mat` e `Anton SDF.asset` (TMP). Mudança só de serialização, sem impacto funcional, mas o usuário pode querer reverter com `git checkout` antes de commitar.
6. Polycount: os valores 568 (botão, limite 800) e 1248 (bobblehead, limite 1800) vêm do log do Blender. O gerador (`generate_task09_rework_models.py`) conta `loop_triangles` e imprime/valida contra os limites, coerente. Não foi reexecutado (fora do escopo desta auditoria).
7. Decisões de layout aceitas: botão de fogos mantido em counter_2_b (1,8 m+ livres acima, ápice calculado em runtime, com "show baixo" se algo ficar a menos de 0,6 m: testes 1t/1s PASS) e bobblehead movido para counter_1 (melhor folga da sala, 0,313 m).
8. Pendências herdadas para outras tasks, sem relação com esta: rótulos TMP gigantes dos troféus (TASK-16), collider do `ArcadeBallSpawner` (TASK-08).

## Ordens de retrabalho

Nenhuma. (Opcional, não bloqueante: regerar `Bobble_Seq_3.png` com um frame distinto de `Bobble_Seq_2`.)

---

## Adendo do orquestrador (2026-11-25)

- **Ressalva 2 (Bobble_Seq_2 = Bobble_Seq_3) corrigida:** no batchmode um frame real avançava ~0,1 s de jogo e a cabeça já estava no pico um frame após a pancada. O teste agora fixa `Time.captureDeltaTime = 1/90 s` durante as capturas do bobblehead; as 6 capturas `TASK-09_Bobble_Seq_1..6.png` são distintas (md5) e o Play Mode continua 47/47 (pico 22,7° em 0,09 s = ¼ de período).
- Ressalvas 1 (alavanca) e 5 (reserialização de `M_Water_Splash.mat`, `M_Magic_Burst.mat`, `Anton SDF.asset`, já presentes antes desta task) ficam fora do commit da TASK-09.
- **Status final: PASS -> `COMPLETED*`** (falta só o playtest no HMD).
