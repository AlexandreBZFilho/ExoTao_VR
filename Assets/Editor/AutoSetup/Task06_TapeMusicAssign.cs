using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRRehab.Interactive.Task06;
using Object = UnityEngine.Object;

namespace VRRehab.EditorAutoSetup
{
    /// <summary>
    /// TASK-06 (retoque) — liga uma música diferente a cada fita cassete.
    /// Só mexe em MusicCassetteTape.musicClip / trackBpm / tapeGenre dos 4 prefabs CassetteTape_*.prefab
    /// (deck, rack, rádio, modelos, posições e scripts do Task06 ficam intactos) e configura o import das 4 músicas.
    /// Também faz o teste estático (clips distintos, nenhum igual à música padrão do rádio, duração, import) e confere a cena.
    /// Rodar: unity run . --log-file X --no-tail -- -executeMethod VRRehab.EditorAutoSetup.Task06_TapeMusicAssign.RunBatch
    /// </summary>
    public static class Task06_TapeMusicAssign
    {
        private const string MusicDir = "Assets/Audio/Music";
        private const string PrefabDir = "Assets/Prefabs/Interactive";
        private const string RadioDefault = "Assets/Resources/Audio/Music_SciFi_Menu.mp3";
        private const string MenuScene = "Assets/Scenes/Menu_Principal.unity";
        private const string ReportPath = ".agents/visual_qa/TASK-06_MusicTest.txt";
        private const string AnalysisJson = ".agents/visual_qa/TASK-06_MusicAnalysis.json";

        private struct TapeSpec
        {
            public string prefab, tapeId, clip, genre, credit;
            public float bpm;
        }

        // BPM = andamento oficial publicado pelo autor na página de cada faixa (incompetech.com)
        private static readonly TapeSpec[] Specs =
        {
            new TapeSpec { prefab = "CassetteTape_CyberHorizon.prefab", tapeId = "tape_cyan_01",    clip = "tape_cyber_horizon.ogg", bpm = 110f, genre = "Synthwave Anos 80",        credit = "\"Newer Wave\" Kevin MacLeod" },
            new TapeSpec { prefab = "CassetteTape_NeonSunset.prefab",  tapeId = "tape_magenta_03", clip = "tape_neon_sunset.ogg",   bpm = 72f,  genre = "Lo-Fi Jazz de Fim de Tarde", credit = "\"Study And Relax\" Kevin MacLeod" },
            new TapeSpec { prefab = "CassetteTape_LunaCantina.prefab", tapeId = "tape_gold_02",    clip = "tape_luna_cantina.ogg",  bpm = 93f,  genre = "Jazz Lounge de Cantina",    credit = "\"Local Forecast\" Kevin MacLeod" },
            new TapeSpec { prefab = "CassetteTape_EmeraldZen.prefab",  tapeId = "tape_emerald_04", clip = "tape_emerald_zen.ogg",   bpm = 57f,  genre = "Ambient Zen (Koto)",        credit = "\"Ripples\" Kevin MacLeod" },
        };

        private static StringBuilder sb;
        private static int pass, fail;

        [MenuItem("ExoTao IC/Menu/Fitas: Ligar Músicas Distintas (TASK-06)")]
        public static void Menu() => Debug.Log(RunAll());

        public static void RunBatch()
        {
            try
            {
                string r = RunAll();
                Debug.Log(r);
                EditorApplication.Exit(fail == 0 ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                File.WriteAllText(ReportPath, "EXCEÇÃO: " + e);
                EditorApplication.Exit(1);
            }
        }

        public static string RunAll()
        {
            sb = new StringBuilder();
            pass = fail = 0;
            sb.AppendLine($"=== TASK-06 (retoque) — Músicas das fitas: ligação e teste estático ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");

            // 1. Import das músicas (streaming, Vorbis 0,5, sem preload, carregamento em segundo plano)
            sb.AppendLine("## 1. Import");
            foreach (var s in Specs)
            {
                string path = $"{MusicDir}/{s.clip}";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var imp = (AudioImporter)AssetImporter.GetAtPath(path);
                if (imp == null) throw new Exception("Música não encontrada: " + path);
                var st = imp.defaultSampleSettings;
                st.loadType = AudioClipLoadType.Streaming;
                st.compressionFormat = AudioCompressionFormat.Vorbis;
                st.quality = 0.5f;
                st.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                st.preloadAudioData = false;
                imp.defaultSampleSettings = st;
                imp.forceToMono = false;
                imp.loadInBackground = true;
                imp.SaveAndReimport();
                var chk = ((AudioImporter)AssetImporter.GetAtPath(path)).defaultSampleSettings;
                Check($"1) Import de {s.clip}", chk.loadType == AudioClipLoadType.Streaming && chk.compressionFormat == AudioCompressionFormat.Vorbis && !chk.preloadAudioData,
                    $"{chk.loadType}, {chk.compressionFormat} q={chk.quality:0.00}, preload={chk.preloadAudioData}");
            }

            // 2. Ligar cada fita à sua música (só musicClip / trackBpm / tapeGenre)
            sb.AppendLine("## 2. Prefabs das fitas");
            foreach (var s in Specs)
            {
                string pp = $"{PrefabDir}/{s.prefab}";
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(pp);
                var tape = asset != null ? asset.GetComponent<MusicCassetteTape>() : null;
                if (tape == null) throw new Exception("Fita não encontrada: " + pp);
                if (tape.tapeId != s.tapeId) throw new Exception($"{pp}: tapeId {tape.tapeId} != {s.tapeId}");
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{MusicDir}/{s.clip}");
                var so = new SerializedObject(tape);
                so.FindProperty("musicClip").objectReferenceValue = clip;
                so.FindProperty("trackBpm").floatValue = s.bpm;
                so.FindProperty("tapeGenre").stringValue = s.genre;
                bool changed = so.ApplyModifiedPropertiesWithoutUndo();
                if (changed) { EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); }
                sb.AppendLine($"   {s.prefab}: '{tape.tapeTitle}' -> {s.clip}, {s.bpm} BPM, \"{s.genre}\" ({(changed ? "atualizado" : "já estava")})");
            }
            AssetDatabase.SaveAssets();

            // 3. Teste estático
            sb.AppendLine("## 3. Teste estático");
            string radioMd5 = Md5(RadioDefault);
            string radioGuid = AssetDatabase.AssetPathToGUID(RadioDefault);
            var guids = new List<string>();
            var md5s = new List<string>();
            foreach (var s in Specs)
            {
                var tape = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{s.prefab}").GetComponent<MusicCassetteTape>();
                string cp = tape.musicClip != null ? AssetDatabase.GetAssetPath(tape.musicClip) : "(nulo)";
                string g = AssetDatabase.AssetPathToGUID(cp);
                string m = tape.musicClip != null ? Md5(cp) : "-";
                guids.Add(g); md5s.Add(m);
                float len = tape.musicClip != null ? tape.musicClip.length : 0f;
                Check($"3a) {tape.tapeTitle}: clip certo, não nulo, >= 60 s", tape.musicClip != null && cp.EndsWith(s.clip) && len >= 60f,
                    $"{cp} ({len:0.0} s, {new FileInfo(cp).Length / 1024f / 1024f:0.00} MB), guid {g}, md5 {m}");
                Check($"3b) {tape.tapeTitle}: diferente da música padrão do rádio", g != radioGuid && m != radioMd5, $"rádio md5 {radioMd5}");
                Check($"3c) {tape.tapeTitle}: trackBpm e gênero coerentes", Mathf.Approximately(tape.trackBpm, s.bpm) && tape.tapeGenre == s.genre,
                    $"{tape.trackBpm} BPM, \"{tape.tapeGenre}\" ({s.credit})");
            }
            Check("3d) 4 guids distintos e 4 MD5 distintos", guids.Distinct().Count() == 4 && md5s.Distinct().Count() == 4, string.Join(" / ", md5s));
            AppendAnalysis();

            // 4. Cena: 4 fitas, sem override de música, sem duplicatas, origem limpa (a cena só é salva se algo mudar)
            sb.AppendLine("## 4. Cena Menu_Principal");
            var scene = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
            var tapes = Object.FindObjectsByType<MusicCassetteTape>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Check("4a) Exatamente 4 fitas, uma de cada", tapes.Length == 4 && tapes.Select(t => t.tapeId).Distinct().Count() == 4,
                string.Join(", ", tapes.Select(t => $"{t.tapeTitle}@{t.transform.parent?.name ?? "raiz"}")));
            var overrides = new List<string>();
            foreach (var t in tapes)
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(t)) { overrides.Add(t.name + " não é instância de prefab"); continue; }
                var mods = PrefabUtility.GetPropertyModifications(t.gameObject) ?? new PropertyModification[0];
                foreach (var mod in mods)
                    if (mod.target is MusicCassetteTape && (mod.propertyPath == "musicClip" || mod.propertyPath == "trackBpm" || mod.propertyPath == "tapeGenre"))
                        overrides.Add($"{t.name}.{mod.propertyPath}");
            }
            Check("4b) Nenhum override de musicClip/trackBpm/tapeGenre nas instâncias (usam o prefab)", overrides.Count == 0, overrides.Count == 0 ? "ok" : string.Join("; ", overrides));
            foreach (var t in tapes)
            {
                var spec = Specs.First(s => s.tapeId == t.tapeId);
                Check($"4c) Instância {t.tapeTitle} usa a música nova", t.musicClip != null && AssetDatabase.GetAssetPath(t.musicClip).EndsWith(spec.clip), AssetDatabase.GetAssetPath(t.musicClip));
            }
            SceneDuplicateAndOriginPurger.PurgeMenuScene(out int o, out int d, new StringBuilder());
            Check("4d) Purga: 0 objetos na origem e 0 duplicatas", o == 0 && d == 0, $"origem {o}, duplicatas {d}");
            if (o > 0 || d > 0)
            {
                EditorSceneManager.SaveScene(scene);
                sb.AppendLine("   cena salva (a purga removeu algo)");
            }
            else sb.AppendLine("   cena NÃO salva (nada mudou)");

            sb.AppendLine();
            sb.AppendLine($"--- RESULTADO: {pass} passaram, {fail} falharam ---");
            File.WriteAllText(ReportPath, sb.ToString());
            return sb.ToString();
        }

        private static void AppendAnalysis()
        {
            if (!File.Exists(AnalysisJson)) { Check("3e) Análise de áudio (Python)", false, "TASK-06_MusicAnalysis.json não encontrado"); return; }
            string json = File.ReadAllText(AnalysisJson);
            float max = ExtractFloat(json, "\"maxCorrelation\":");
            float ctrl = ExtractFloat(json, "\"controlSameTrackShifted1s\":");
            Check("3e) Som diferente: correlação de envelope < 0,3 entre todos os pares (inclui a música do rádio)", max < 0.3f && ctrl > 0.8f,
                $"maior correlação entre pares {max:0.000}; controle (mesma faixa deslocada 1 s) {ctrl:0.000} — detalhes em TASK-06_MusicAnalysis.json");
        }

        private static float ExtractFloat(string json, string key)
        {
            int i = json.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return float.NaN;
            i += key.Length;
            int j = i;
            while (j < json.Length && "-0123456789.eE ".IndexOf(json[j]) >= 0) j++;
            return float.Parse(json.Substring(i, j - i).Trim(), System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string Md5(string assetPath)
        {
            using (var md5 = MD5.Create())
            using (var fs = File.OpenRead(assetPath))
                return BitConverter.ToString(md5.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
        }

        private static void Check(string name, bool ok, string detail)
        {
            if (ok) pass++; else fail++;
            sb.AppendLine($"[{(ok ? "PASS" : "FAIL")}] {name}: {detail}");
        }
    }
}
