# TASK-06 — Parecer do Auditor [E] (Visual QA e performance VR) — RE-AUDITORIA

**VEREDICTO: PASS**
**Data:** 2026-11-11 · **Cena:** `Menu_Principal` · **Móvel:** `full_desk_3 (1)` (Mesa 1)

> Resumo: as quatro pendências do parecer FAIL anterior foram resolvidas e comprovadas por JSON, código, prefab e capturas. Há ressalvas não bloqueantes (§4) e pendências de outras tasks (§5).

---

## 1. Critério PASS do parecer anterior (§4) — item a item

| # | Critério | Resultado | Evidência |
|---|----------|-----------|-----------|
| 1 | Folga ≥ 0.08 m entre todos os pares da Mesa 1 | PASS | `SpatialAudit.json`: `hasOverlap:false`, `requiredClearanceMeters:0.08`, `minPairClearanceMeters:0.1068` (Rack × `pc_monitor`). 28 pares em `pairwiseClearances[]`. Rádio × Deck = 0.678 m, Rádio × Rack = 0.408 m, Deck × Rack = 0.124 m, Deck × monitor = 0.107 m. Os únicos pares excluídos (`excludedFromOverlapCheck`) são fita↔slot do próprio deck/rack e fita↔fita no rack, com contato intencional. Visualmente, `View1_Front` e `Radio_Deck_Overview` mostram deck, rack e rádio separados sobre o tampo. |
| 2 | Bola, fidgets, troféus e pistola fora do entorno do deck e rack | PASS | A Mesa 1 só tem Rádio, Deck, Rack, 4 fitas e a decoração fixa (monitor, teclado e mouse). As capturas da mesa estão limpas. Todos os 12 itens realocados constam em `relocations[]`, nenhum foi deletado (`instanceCounts` = 1 para cada). |
| 3 | Capturas mostram deck, rack, fita no slot (LED e Eject legíveis) e fita ejetada pousada | PASS | Ver §2. |
| 4 | `ejectImpulseForce` ≤ 0.2 | PASS | Código: `public float ejectImpulseForce = 0.15f`. Prefab `RadioCassetteDeck.prefab` linha 969: `0.15`. Cena `Menu_Principal.unity`: 0 ocorrências de `ejectImpulseForce`, portanto não há override. JSON: `ejectImpulseForce:0.15`, `maxEjectSpeed:1.5`. |

---

## 2. Análise crítica de cada captura (`Read` de todos os PNGs)

- **`View1_Front`:** a mesa está limpa. O deck (com fita cyan no slot, LED e botão vermelho) fica à esquerda do monitor. O rack fica na frente do monitor, com a fita verde visível. O rádio fica à direita. Nada se sobrepõe e nada flutua.
- **`View2_Side_Grounding`:** câmera rasante na altura do tampo, como ordenado. Deck e rack inteiros no quadro, com a linha de contato com o tampo visível e sombra de contato. Está aterrado, sem flutuação e sem enterramento. O parecer anterior apontava a coroa e a taça aqui, e isso foi corrigido.
- **`View3_Closeup`:** close frontal do deck sem nenhum objeto na frente. A fita cyan está encaixada no slot, o LED cyan está aceso e o botão vermelho de Eject está legível.
- **`Tape_Inserted_Slot`:** visão 3/4 do mesmo deck, também nítida. A fita está assentada no slot, com LED e Eject legíveis.
- **`Cassette_Tapes_Closeup`:** o rack tem degraus escalonados e a fita verde na frente. O deck fica ao fundo, sem bola atravessando.
- **`Radio_Deck_Overview`:** vista alta da Mesa 1, com deck, rack (verde, magenta e roxo visíveis), monitor e rádio. A mesa está organizada, sem abarrotamento.
- **`Eject_Sequence` (3 quadros):** "1 ANTES" tem a fita no slot com LED aceso. "2 EM VOO" mostra a fita no ar com a sombra no tampo, o slot vazio e o LED apagado. "3 POUSADA" mostra a fita deitada sobre o tampo, à frente do deck, longe da borda, sem cair no chão. Isso cumpre o requisito de mostrar a fita ejetada em voo e pousada.
- **`Relocated_Counter1_Spawner`:** o spawner (placa preta) e o botão vermelho estão no balcão `counter_1`. A bola de basquete aparece em outro móvel, à direita. Sem sobreposição visível.
- **`Relocated_Counter2b_Fidgets`:** a alavanca de clima e o botão de fogos estão bem espaçados no `counter_2_b`, sob a TV e ao lado do vaso. Sem colisão visível. A estante com troféus aparece à esquerda.
- **`Relocated_Counter3a_Toy`:** a esfera de brinquedo ergonômico (preta com faixa cyan) está sobre a bancada longa, entre o gabinete de PC e o mousepad. Sem sobreposição.
- **`Relocated_Drawers_Bobble_Ball`:** o Bobblehead está sobre a gaveteira, ao lado do vaso de cacto. A bola de basquete está na gaveteira vizinha. O spawner e o botão ficam no balcão à esquerda. Todos estão apoiados nos tampos, sem flutuar. O Bobblehead e o cacto estão apertados (ver §4).
- **`Relocated_NorthCounters_Gun_Lamp`:** a pistola de balões está sobre a bancada norte, com o balão demo flutuando acima. A luminária foi realocada para outra bancada. A pistola está apoiada, sem sobreposição visível.
- **`Relocated_Shelf_Trophies`:** os quatro troféus (coroa, taça, bonsai e maçã dourada) estão sobre o suporte, na prateleira. Todos aterrados, sem colisão entre si. Há texto TMP amarelo gigante atrás da prateleira (ver §5).

---

## 3. JSON, código e física

**JSON** (`TASK-06_SpatialAudit.json`):
- `isOriginClean:true`, `objectsAtOriginZero:[]`, `duplicateObjectsFound:[]`.
- Contagens: rádio 1, deck 1, rack 1, fitas 4 (1 `Tape_CyberHorizon_Active` + 3 `_Rack`), `RadioCassetteSocket` 1.
- `groundGapMeters:0.003`, `isPhysicallyGrounded:true`, `isInsideFurnitureTop:true`.
- `ejectSimulation.result = "PASS"`. O Eject usa a corrotina real do runtime com `Physics.Simulate`. Pico Y 1.065, repouso a 0.230 m do slot, base 0.9487 com tampo 0.949, margem de 0.134 m da borda, nunca abaixo do `respawnFloorThresholdY` (0.70). `IgnoreCollision` foi restaurado e o socket recusou recaptura durante o cooldown (1.0 s).
- Coerência temporal: a cena `Menu_Principal.unity` foi salva às 23:11:23. As capturas são das 23:11 e a re-simulação das 23:38 recarregou a cena do disco, então o layout das capturas é o final.

**Código** (`Assets/Scripts/Interactive/Task06/*.cs`):
- Nenhum `SceneManager.LoadScene` (grep nos scripts da Task06 e Task07 a Task09). A única ocorrência de `LoadScene` em Task10 é um comentário proibitivo em `PortalWorldSphere.cs`.
- Nenhum `renderer.material`, `.materials` nem `.sharedMaterial`. O LED usa `MaterialPropertyBlock`.
- `LightShadows.None` no código (`Awake` e `UpdateStatusLED`). Na prefab `m_Shadows` está presente.
- Zero `MeshCollider`: 0 ocorrências em `RadioCassetteDeck.prefab`, nas 4 `CassetteTape_*.prefab` e em `CassetteOrganizerRack.prefab`. As fitas usam `BoxCollider`, Rigidbody com `m_Mass 0.12`.
- `MusicCassetteTape : LaserSwingThrowInteractable`. Confirmado.
- Zero alocação por frame: `Update` de `MusicCassetteTape` só roda quando `isPlayingInDeck`, sem alocar. `CassetteEjectButton.Update` já tem saída antecipada com `isSettled` (a melhoria sugerida foi aplicada). As listas de colliders são pré-alocadas e reaproveitadas na ejeção.
- Eject: `IgnoreCollision` fita↔deck por 0.30 s e cooldown de recaptura de 1.0 s implementados e restaurados em `EndEjectCollisionIgnore` e `OnDisable`.
- Deck: prefab com `BoxCollider` de 0.20×0.048×0.15 e `Collider_SideRim_L/R`, slot livre.

---

## 4. Ressalvas NÃO bloqueantes

1. **Clamp de velocidade inerte em `EjectRoutine` (reportado por [D]):** o clamp lê `rb.linearVelocity` logo após `AddForce(..., Impulse)`, quando o PhysX ainda não integrou o impulso, então o valor lido é 0. É código morto, e `launchSpeed:0.0` no JSON confirma isso. Não bloqueia porque a velocidade real é 0.15/0.12 = 1.25 m/s, abaixo de 1.5 m/s, e a simulação real prova pouso seguro (0.23 m). Sugestão para uma passada futura: aplicar o clamp no `FixedUpdate` seguinte, ou calcular a velocidade esperada como `impulso / mass`.
2. **Deck não está literalmente "ao lado do rádio":** o Hotfix 6 e a especificação falam em deck "perto do rádio". O deck fica a 0.678 m de folga, do outro lado do monitor, porque o lado do rádio não comporta mais itens com folga de 0.08 m. Funcionalmente o vínculo é por referência (`radioTarget`), e a Mesa 1 continua sendo a mesa do rádio. Se o usuário quiser mais proximidade, é um ajuste de layout em `[D]` numa passada de polimento, não um defeito desta entrega.
3. **Bobblehead com folga de 0.076 m do vizinho:** em `drawer_simple`, abaixo dos 0.08 m. Isso é um destino realocado (TASK-09), fora da Mesa 1. A TASK-09 deve refinar.
4. **Itens realocados com folga mínima de 0.062 a 0.066 m aos vizinhos:** `TrophyDisplayStand`, `BalloonGunDispenser`, `ArcadeBallSpawner`, `[IC] Ergonomic_Toy_Demo`, `Table Lamp` e `Deco Variant 3`. O valor é medido contra decoração fixa do móvel de destino. Nenhuma sobreposição visível nos `Relocated_*.png`. Cada TASK respectiva deve revalidar com 0.08 m ao assumir o item.
5. O JSON repete 4 vezes a mesma nota "Pós-simulação: cena recarregada…" em `notes[]`. É cosmético.

---

## 5. Pendências para tasks futuras (não reprovam a TASK-06)

- **TASK-16 (troféus):** texto TMP amarelo gigante ("C", "M", "oa", "on ar…") aparece atrás da prateleira em `Relocated_Shelf_Trophies` e `Relocated_Counter2b_Fidgets`. É o rótulo do `TrophyDisplayStand` com escala errada ou `Overflow` desligado, e vaza para a cena.
- **TASK-08:** o collider do prefab de `ArcadeBallSpawner` desce 0.058 m abaixo da malha visível (registrado por [D] como geometria do prefab). A tabela do aro e a bola precisam de revisão na TASK-08.
- **TASK-07:** o balão demo flutuante fica no ar acima da pistola, com altura fixa. Validar na TASK-07 o pool e a regra de estouro.
- **Cena `Floresta_e_Rio.unity`:** o working tree tem um diff grande contra o HEAD (inclusive remoção de instâncias como `[TASK-15] CheckpointRewardTotem`, `Apple_In_Basket`, `EnergyStarCoin_*`). O arquivo tem mtime de 05:45 de 2026-11-11, anterior a todo o trabalho da TASK-06, então vem da limpeza manual de duplicatas do usuário, não desta task. Quando chegar a TASK-11 em diante, o `[A]` deve confirmar se algum totem, cesta ou maçã necessário foi removido por engano e recriar apenas o que faltar.

---

## 6. Ordens de retrabalho

Nenhuma. `[B]`, `[C]` e `[D]` aprovados para a TASK-06. Marcar `[E] = [x]` e status `COMPLETED` na Matriz da Seção 4.
