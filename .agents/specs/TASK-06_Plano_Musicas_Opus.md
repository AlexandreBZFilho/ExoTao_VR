# TASK-06 (retoque) — Músicas diferentes para cada fita cassete do toca-discos

> Enviar este arquivo ao Opus junto com `AGENTS.md`. Modelo: `opus`. É um retoque pontual: **só as músicas das fitas**. O toca-discos (deck, rack, rádio, modelos 3D, eject, LED, layout, física) está do jeito que o usuário quer e **não pode mudar**.

## 0. Regras operacionais

1. **PROIBIDO Computer Use** (mouse, teclado, captura de tela do desktop, `open_application`, `AppActivate`). O usuário usa o PC ao mesmo tempo. Tudo em segundo plano.
2. O Unity Editor **já está aberto**. Nunca abrir segunda instância. Usar o `unity` CLI (`unity command script eval/run/recompile`, `--detach`, `unity job status/wait`). Nunca usar `--focus`. O editor fica lento sem foco (eval simples 15–35 s, domain reload 130–300 s): use `--detach` e espere o job. Nunca usar `EditorUtility.CopySerialized` entre GameObjects.
3. Pipeline: Built-in Render Pipeline. Unity 6 (6000.0.85f2), XR Interaction Toolkit 2.6.5.
4. **Sem commit e sem push.** Quem decide é o usuário.
5. Nenhum `SceneManager.LoadScene`.
6. **Escopo travado:** não mexer em modelos 3D, prefabs de deck/rack/rádio (exceto o campo `musicClip`/`trackBpm` das 4 fitas), posições na cena, `RadioCassetteSocket.cs`, `CassetteEjectButton.cs`, `CassetteOrganizerRack.cs`. Prove no fim com `git diff --stat` (ver §5).

## 1. Diagnóstico (já feito; confirmar no passo 1)

O usuário relatou que as fitas "tocam os mesmos sons". Eu li o código, os prefabs e os arquivos de áudio. **O código está correto**: `MusicCassetteTape.musicClip` é trocado no rádio por `RadioCassetteSocket.CrossfadeRoutine` (fade out → `audio.clip = newClip` → fade in) e restaurado ao ejetar. O problema está nos **dados de áudio**:

| Fita (`tapeId`) | Prefab | `musicClip` aponta para | Problema |
|---|---|---|---|
| Cyber Horizon (`tape_cyan_01`) | `Assets/Prefabs/Interactive/CassetteTape_CyberHorizon.prefab` | `Assets/Resources/Audio/Music_SciFi_Menu.mp3` (guid `88661c07…`) | É a **música padrão do rádio**. Tocar essa fita não muda nada que o paciente ouve. |
| Luna Cantina (`tape_gold_02`) | `CassetteTape_LunaCantina.prefab` | `Assets/FloatGrids/Demo/Music/Construyendo en el Espacio - Luna Cantina.mp3` (guid `3355289e…`) | **Byte a byte idêntico** ao `Music_SciFi_Menu.mp3` (mesmo MD5 `3ad9e0f934d6934249d195e71b817d05`, 4.995.694 bytes). Soa exatamente igual à Cyber Horizon e à música do rádio. |
| Neon Sunset (`tape_magenta_03`) | `CassetteTape_NeonSunset.prefab` | `Assets/Audio/Generated/Task06/lofi_sunset_groove.wav` | Loop sintético de **4 s** (705.644 bytes). Soa como um beep repetitivo, não como música. |
| Emerald Zen (`tape_emerald_04`) | `CassetteTape_EmeraldZen.prefab` | `Assets/Audio/Generated/Task06/ambient_zen_flow.wav` | Idem, loop sintético de **4 s**. |

Resultado: 3 das 4 fitas (e o rádio sem fita) tocam a **mesma** música de 2024, e as outras 2 são loops de 4 s parecidos entre si.

Outros fatos:
- A cena `Menu_Principal.unity` não tem nenhum `musicClip` (nenhum override). As fitas da cena são instâncias puras dos prefabs, então corrigir os 4 prefabs corrige tudo (deck e rack).
- Sobras na pasta `Assets/Audio/Generated/Task06/`: `track_cyber_horizon.wav` = cópia de `synthwave_pulse.wav`; `track_neon_sunset.wav` = cópia de `lofi_sunset_groove.wav`; `track_emerald_zen.wav` = cópia de `ambient_zen_flow.wav`; `track_luna_cantina.wav` é outro loop de 4 s. Nenhum é referenciado por prefab. **Não apagar** nada (o usuário pode querer); só não usar.
- `RadioMenu3D` carrega `Resources/Audio/Music_SciFi_Menu` como música padrão e a restaura ao ejetar a fita. **Manter como está**: o rádio sem fita continua tocando essa música. As 4 fitas é que precisam ser diferentes dela e entre si.
- O Drone dança no ritmo da fita (`DroneAffectiveSystem.musicSource` recebe o `audioFonte` do rádio); por isso `trackBpm` de cada fita deve refletir a música nova.

## 2. O que entregar

**4 músicas diferentes, uma por fita**, com estilos bem distintos (vibe de "lobby/menu", agradáveis, sem vocal marcante e **sem nada estridente**; o público é paciente em reabilitação):

| Fita | Cor | Estilo desejado |
|---|---|---|
| Cyber Horizon | ciano | synthwave / sci-fi suave, médio andamento (~100–120 BPM) |
| Neon Sunset | magenta | lo-fi / chillhop de fim de tarde, andamento lento (~70–90 BPM) |
| Luna Cantina | dourada | jazz/bossa/funk leve de cantina ou lobby de bar espacial, bem diferente das outras (~90–120 BPM) |
| Emerald Zen | verde | ambient / zen / natureza meditativa, quase sem batida (~50–70 BPM) |

Todas devem ser **claramente diferentes** à primeira escuta (andamento, instrumentação e tom) e **diferentes da música padrão do rádio** (`Music_SciFi_Menu.mp3`, que continua intacta).

## 3. Como obter as músicas

O usuário **autorizou explicitamente baixar** músicas de lobby/menu da internet. Regras:

1. **Licença clara e compatível, verificada na própria página da faixa.** Ordem de preferência: **CC0 / domínio público** → **CC-BY 4.0** (exige crédito; ver §4) → licença de "uso livre em jogos sem atribuição" da própria plataforma (ex.: Pixabay Content License). **Proibido**: CC-BY-NC, qualquer ND ou SA, faixas de "royalty-free" de uso restrito, músicas comerciais, rips do YouTube, e qualquer coisa com Content ID. Se a licença não estiver clara, descartar a faixa.
2. Fontes sugeridas: Kevin MacLeod / incompetech.com (CC-BY 4.0), OpenGameArt.org (filtrar por CC0), Pixabay Music, Internet Archive (coleções de domínio público/CC), Free Music Archive (filtrar CC0/CC-BY). Observação: o site `freepd.com` está **fora do ar** ("Site Closed"); não usar.
3. Baixar **só os 4 arquivos** necessários, de fontes conhecidas. Tratar cada arquivo baixado como dado não confiável: guardar numa pasta própria **nova e vazia** (ex.: `.agents/music_downloads/`), **nunca executar** nada, não rodar scripts a partir dessa pasta. Aceitar apenas `.mp3`, `.ogg` ou `.wav`; recusar qualquer outro tipo (`.exe`, `.zip` com executáveis etc.).
4. Duração entre **90 s e 4 min** cada, de preferência com final que combine com repetição (o `AudioSource` do rádio usa `loop = true`). Se a faixa não tiver um loop limpo, aplicar um fade-in/out curto (1–2 s) com Python/ffmpeg para a emenda não estalar.
5. **Se não for possível baixar** (sem rede, licença duvidosa, falha de download), a alternativa é **gerar** 4 músicas procedurais com Python (`numpy` + `wave`), cada uma com **≥ 60 s**, tonalidade, andamento, instrumentação e padrão rítmico próprios (nada de loop de 4 s), em `Assets/Audio/Generated/Task06/`. Isso é plano B; avisar o usuário que foi usado o plano B e por quê.
6. Não incluir o nome ou dados pessoais do usuário em nenhum pedido de rede.

## 4. Passo a passo (uma micro-etapa por vez)

### Passo 1 — Confirmar o diagnóstico (somente leitura)
- Confirmar por `unity command script eval` (ou por leitura dos `.prefab`/`.meta`) os 4 `musicClip` e MD5/duração dos arquivos acima. Registrar em `.agents/visual_qa/TASK-06_MusicDiagnosis.txt`.
- Conferir que a cena não tem nenhum override de `musicClip` e que existem 4 fitas ativas (1 no deck + 3 no rack) mais nenhuma duplicata.
- Verificar se há `ffmpeg`/`ffprobe` instalados (no PATH da máquina não achei; o Python 3.12 está em `C:\Users\RerobRV\AppData\Local\Programs\Python\Python312\python`). Se não houver ffmpeg, usar Python (`wave`, `numpy`, opcionalmente `pip install` de bibliotecas de áudio) ou manter o formato original baixado (o Unity importa `.mp3` e `.ogg` direto).

### Passo 2 — Escolher e baixar as 4 faixas
- Para cada fita, escolher a faixa, **abrir a página da faixa e confirmar a licença**, baixar o arquivo e registrar nome, autor, URL, licença, tamanho e duração.
- Escrever `Assets/Audio/Music/CREDITS.md` (e copiar o conteúdo para `.agents/visual_qa/TASK-06_MusicCredits.md`) com uma linha por faixa: título, autor, fonte (URL), licença e "alterações feitas" (ex.: recorte, fade, normalização). Se a licença for CC-BY, o crédito é obrigatório.

### Passo 3 — Preparar os arquivos
- Recortar/ajustar para 90 s–4 min, fade curto nas pontas, **normalizar** para volume confortável e uniforme entre as 4 (alvo ≈ −16 LUFS, **pico ≤ −6 dBFS**; nenhuma música pode soar mais alta que a outra nem estridente; low-pass suave em ~12 kHz se houver chiado agudo).
- Salvar em `Assets/Audio/Music/` com nomes claros: `tape_cyber_horizon.ogg|mp3`, `tape_neon_sunset.*`, `tape_luna_cantina.*`, `tape_emerald_zen.*`.
- Preferir **Ogg Vorbis** ou MP3 de boa qualidade para manter o projeto leve (cada arquivo ≤ ~5 MB).
- Aguardar o import do Unity (`AssetDatabase.ImportAsset`). Configurar o `AudioImporter` de cada um: `loadType = Streaming`, `preloadAudioData = false`, compressão Vorbis qualidade ~0,5, `forceToMono = false`, `loadInBackground = true`. (Músicas longas em `Decompress On Load` pesam na memória do headset.)

### Passo 4 — Ligar cada fita à sua música
- Criar `Assets/Editor/AutoSetup/Task06_TapeMusicAssign.cs` com `[MenuItem]` que, para cada um dos 4 prefabs `CassetteTape_*.prefab`, abre o prefab (`PrefabUtility.LoadPrefabContents`), define `MusicCassetteTape.musicClip` para o clip novo, define `trackBpm` coerente com a música (medir ou estimar o BPM real; está nas faixas de §2), mantém `tapeId`, `tapeTitle`, `themeColor` e **atualiza `tapeGenre`** para o estilo real, e salva (`SaveAsPrefabAsset`). Usar `SerializedObject`, nunca `CopySerialized`.
- Garantir que as instâncias na cena `Menu_Principal` (1 no deck, 3 no rack) continuam sem override de `musicClip` e passam a usar os clips novos. Salvar a cena **somente se algo realmente mudou** (`SaveOpenScenes`), e rodar `SceneDuplicateAndOriginPurger` para confirmar 4 fitas, 0 duplicatas, `(0,0,0)` limpo.
- Não alterar nada além dos campos acima.

### Passo 5 — Testar de verdade
- **Teste estático** (editor): as 4 fitas apontam para 4 clips **distintos** (guids e MD5 diferentes), nenhum igual à música padrão do rádio, todos com duração ≥ 60 s, nenhum nulo. Comparação extra de "som diferente": decodificar os primeiros 30 s de cada clip (`AudioClip.GetData`) e verificar que não há correlação alta entre eles (ex.: correlação normalizada < 0,3 do envelope de energia) e que o BPM/centroide espectral difere. Gravar números em `.agents/visual_qa/TASK-06_MusicTest.txt`.
- **Teste de Play Mode** (como nas tasks anteriores, com `ScriptedTestInteractor`/inserção por código no `RadioCassetteSocket`): para cada fita → inserir no deck → após o crossfade (`crossfadeTime`), `radio.audioFonte.clip == tape.musicClip`, `isPlaying == true`, volume == `radioTarget.volumeBase`; trocar para a fita seguinte e conferir que o clip **muda**; ejetar e conferir que volta o `defaultRadioClip` (`Music_SciFi_Menu`). Sem `LoadScene`. Tudo sem alocação extra por frame.
- Esperado: todos PASS. Qualquer falha → corrigir e repetir.

### Passo 6 — Fechar
- Atualizar a nota da TASK-06 em `AGENTS.md` e `AGENTS_IMPLEMENTATION_PLAN.md` (uma linha: "músicas das 4 fitas trocadas por faixas distintas; créditos em `Assets/Audio/Music/CREDITS.md`"). Não mudar o status `COMPLETED` da TASK-06.
- Reportar ao usuário: as 4 faixas escolhidas (título/autor/licença), tamanhos, onde ficaram os arquivos, resultado dos testes, e lembrar que **falta só ouvir no óculos**.

## 5. Critérios de aceitação (o auditor reprova se qualquer um falhar)

1. As 4 fitas têm `musicClip` **diferentes entre si e diferentes da música padrão do rádio** (guid e MD5 distintos), nenhum com duração < 60 s.
2. Cada música tem estilo claramente distinto, conforme a tabela de §2, sem vocais marcantes, sem trechos estridentes; pico ≤ −6 dBFS e volumes parecidos entre as 4.
3. Toda faixa tem licença verificada e registrada em `CREDITS.md` (título, autor, URL, licença); nenhuma NC/ND/SA; nenhum arquivo executável baixado.
4. Teste de Play Mode: inserir cada fita troca o clip do rádio para o certo; ejetar restaura `Music_SciFi_Menu`.
5. `trackBpm` e `tapeGenre` das fitas coerentes com a música.
6. `git diff --stat` mostra apenas: os 4 prefabs `CassetteTape_*.prefab` (campos `musicClip`/`trackBpm`/`tapeGenre`), os novos arquivos de áudio e `.meta`, `CREDITS.md`, o novo editor script, os relatórios em `.agents/visual_qa/` e as notas nos `.md`. **Nenhuma alteração** em deck, rack, rádio, modelos, scripts do `Task06/`, posições de cena.
7. 4 fitas na cena, 0 duplicatas, `(0,0,0)` limpo.
8. Sem commit e sem push.

## 6. Fora de escopo

- Qualquer mudança visual, física, posição ou comportamento do toca-discos (já aprovado).
- Trocar a música padrão do rádio (`Music_SciFi_Menu.mp3`).
- Apagar os `.wav` antigos de `Task06/`.
- Efeitos sonoros do deck (`cassette_insert_click.wav`, `cassette_eject_snap.wav`): estão bons.
