using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Módulo de Telemetria Clínica e Biofeedback biomecânico para a Iniciação Científica (ExoTao VR Rehab - ReRobLab).
/// Coleta dados cinemáticos de quadril/joelho, detecta ciclos de marcha, calcula o Índice de Simetria (ASI %)
/// e exporta relatórios científicos em CSV e TXT/JSON automaticamente.
/// </summary>
public class TelemetriaClinica : MonoBehaviour
{
    public static TelemetriaClinica Instancia { get; private set; }

    [Header("Referências do Paciente")]
    public ReceptorUDP receptorExo;
    public CharacterController controlePaciente;
    public Transform transformPaciente;

    [Header("Configurações de Amostragem Científica")]
    [Tooltip("Intervalo em segundos entre gravações de linha no CSV (0.1s = 10 Hz)")]
    public float intervaloGravacaoCSV = 0.1f;
    [Tooltip("Ângulo mínimo de flexão do quadril (graus) para contabilizar o início de um passo")]
    public float limiarFlexaoPasso = 10f;
    [Tooltip("Ângulo de retorno do quadril (graus) para concluir o ciclo do passo (histerese)")]
    public float limiarRetornoPasso = 4f;

    [Header("Métricas em Tempo Real (Somente Leitura)")]
    public float tempoSessaoSegundos = 0f;
    public int passosPernaDireita = 0;
    public int passosPernaEsquerda = 0;
    public int totalPassos = 0;
    public float distanciaPercorridaMetros = 0f;
    public float velocidadeAtualMs = 0f;
    public float velocidadeMediaMs = 0f;
    public float cadenciaPassosPorMinuto = 0f;

    [Header("Biomecânica e Simetria Articular (ROM)")]
    [Range(0f, 100f)]
    public float indiceSimetriaMarcha = 100f;
    public string diagnosticoSimetria = "Simétrico";
    public float amplitudeMaxQuadrilDir = 0f;
    public float amplitudeMaxQuadrilEsq = 0f;
    public float amplitudeMaxJoelhoDir = 0f;
    public float amplitudeMaxJoelhoEsq = 0f;

    // Controle interno de detecção de ciclo de marcha (Heel-Strike / Swing Peak)
    private bool emFaseBalancoDir = false;
    private bool emFaseBalancoEsq = false;
    private float picoAtualQuadrilDir = 0f;
    private float picoAtualQuadrilEsq = 0f;

    // Janelas móveis para cálculo de simetria recente e cadência instantânea
    private readonly Queue<float> ultimosPicosQuadrilDir = new Queue<float>();
    private readonly Queue<float> ultimosPicosQuadrilEsq = new Queue<float>();
    private readonly Queue<float> timestampsUltimosPassos = new Queue<float>();
    private const int TAMANHO_JANELA_SIMETRIA = 6;

    // Posição anterior para cálculo preciso de distância real no espaço 3D
    private Vector3 ultimaPosicaoHorizontal;
    private bool posicaoInicializada = false;

    // Gravação de Arquivos
    private StreamWriter csvWriter;
    private string caminhoArquivoCSV;
    private string caminhoArquivoResumo;
    private string pastaRelatorios;
    private float cronometroCSV = 0f;
    private bool relatorioFinalizado = false;
    private string nomeCenaAtual = "";
    private DateTime inicioSessaoTimestamp;

    // Acumuladores para médias globais do relatório final
    private double somaSimetria = 0.0;
    private int amostrasSimetria = 0;

    void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }
        Instancia = this;
    }

    void Start()
    {
        LocalizarReferenciasAutomaticamente();
        InicializarArquivosDeTelemetria();
    }

    public void LocalizarReferenciasAutomaticamente()
    {
        if (receptorExo == null)
            receptorExo = FindFirstObjectByType<ReceptorUDP>();

        if (controlePaciente == null)
        {
            if (receptorExo != null && receptorExo.controlePaciente != null)
                controlePaciente = receptorExo.controlePaciente;
            else
                controlePaciente = FindFirstObjectByType<CharacterController>();
        }

        if (transformPaciente == null)
        {
            if (controlePaciente != null)
                transformPaciente = controlePaciente.transform;
            else if (receptorExo != null)
                transformPaciente = receptorExo.transform;
            else if (Camera.main != null)
                transformPaciente = Camera.main.transform;
        }

        if (transformPaciente != null)
        {
            ultimaPosicaoHorizontal = new Vector3(transformPaciente.position.x, 0f, transformPaciente.position.z);
            posicaoInicializada = true;
        }
    }

    private void InicializarArquivosDeTelemetria()
    {
        try
        {
            inicioSessaoTimestamp = DateTime.Now;
            nomeCenaAtual = SceneManager.GetActiveScene().name;

            // Cria a pasta Relatorios_Clinicos na raiz do projeto (fora de Assets para não travar o Editor)
            DirectoryInfo raizProjeto = Directory.GetParent(Application.dataPath);
            pastaRelatorios = Path.Combine(raizProjeto != null ? raizProjeto.FullName : Application.persistentDataPath, "Relatorios_Clinicos");

            if (!Directory.Exists(pastaRelatorios))
                Directory.CreateDirectory(pastaRelatorios);

            string carimboData = inicioSessaoTimestamp.ToString("yyyy-MM-dd_HH-mm-ss");
            string nomeCenaLimpo = nomeCenaAtual.Replace(" ", "_");

            caminhoArquivoCSV = Path.Combine(pastaRelatorios, $"Sessao_{carimboData}_{nomeCenaLimpo}_Telemetria.csv");
            caminhoArquivoResumo = Path.Combine(pastaRelatorios, $"Sessao_{carimboData}_{nomeCenaLimpo}_Resumo_IC.txt");

            csvWriter = new StreamWriter(caminhoArquivoCSV, false, Encoding.UTF8);
            csvWriter.WriteLine("Tempo_s,Quadril_D_deg,Joelho_D_deg,Quadril_E_deg,Joelho_E_deg,Velocidade_ms,Distancia_m,Passos_D,Passos_E,Passos_Total,Cadencia_SPM,Simetria_ASI_Pct,Estado_Drone_IA");
            csvWriter.Flush();

            Debug.Log($"[Telemetria Clínica IC] Gravando sessão em: {caminhoArquivoCSV}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Telemetria Clínica IC] Não foi possível criar arquivo CSV: {e.Message}");
        }
    }

    void Update()
    {
        if (receptorExo != null && receptorExo.Pausado)
            return;

        tempoSessaoSegundos += Time.deltaTime;

        AtualizarDeslocamentoFisico();
        ProcessarCinematicaECicloDeMarcha();
        AtualizarCadencia();

        // Amostragem periódica para o CSV
        cronometroCSV += Time.deltaTime;
        if (cronometroCSV >= intervaloGravacaoCSV)
        {
            cronometroCSV = 0f;
            GravarAmostraCSV();
        }
    }

    private void AtualizarDeslocamentoFisico()
    {
        if (transformPaciente == null)
        {
            LocalizarReferenciasAutomaticamente();
            return;
        }

        Vector3 posAtual = new Vector3(transformPaciente.position.x, 0f, transformPaciente.position.z);
        if (!posicaoInicializada)
        {
            ultimaPosicaoHorizontal = posAtual;
            posicaoInicializada = true;
            return;
        }

        float deltaDist = Vector3.Distance(posAtual, ultimaPosicaoHorizontal);

        // Ignora teletransportes bruscos (> 3m num único frame)
        if (deltaDist < 3.0f && Time.deltaTime > 0.0001f)
        {
            distanciaPercorridaMetros += deltaDist;
            float velInstantanea = deltaDist / Time.deltaTime;
            velocidadeAtualMs = Mathf.Lerp(velocidadeAtualMs, velInstantanea, Time.deltaTime * 8f);
        }
        else
        {
            velocidadeAtualMs = 0f;
        }

        if (tempoSessaoSegundos > 0.5f)
            velocidadeMediaMs = distanciaPercorridaMetros / tempoSessaoSegundos;

        ultimaPosicaoHorizontal = posAtual;
    }

    private void ProcessarCinematicaECicloDeMarcha()
    {
        if (receptorExo == null) return;

        float qDir = Mathf.Abs(receptorExo.AnguloQuadrilDir);
        float qEsq = Mathf.Abs(receptorExo.AnguloQuadrilEsq);
        float jDir = Mathf.Abs(receptorExo.AnguloJoelhoDir);
        float jEsq = Mathf.Abs(receptorExo.AnguloJoelhoEsq);

        // Atualiza Amplitudes Máximas (ROM) da Sessão
        if (qDir > amplitudeMaxQuadrilDir) amplitudeMaxQuadrilDir = qDir;
        if (qEsq > amplitudeMaxQuadrilEsq) amplitudeMaxQuadrilEsq = qEsq;
        if (jDir > amplitudeMaxJoelhoDir) amplitudeMaxJoelhoDir = jDir;
        if (jEsq > amplitudeMaxJoelhoEsq) amplitudeMaxJoelhoEsq = jEsq;

        // Detecção de Passo - Perna Direita (com histerese para evitar múltiplos disparos)
        if (!emFaseBalancoDir)
        {
            if (receptorExo.AnguloQuadrilDir >= limiarFlexaoPasso)
            {
                emFaseBalancoDir = true;
                picoAtualQuadrilDir = receptorExo.AnguloQuadrilDir;
            }
        }
        else
        {
            if (receptorExo.AnguloQuadrilDir > picoAtualQuadrilDir)
                picoAtualQuadrilDir = receptorExo.AnguloQuadrilDir;

            if (receptorExo.AnguloQuadrilDir <= limiarRetornoPasso)
            {
                emFaseBalancoDir = false;
                passosPernaDireita++;
                totalPassos = passosPernaDireita + passosPernaEsquerda;
                timestampsUltimosPassos.Enqueue(tempoSessaoSegundos);

                RegistrarPicoPasso(ultimosPicosQuadrilDir, picoAtualQuadrilDir);
                RecalcularSimetriaDeMarcha();
            }
        }

        // Detecção de Passo - Perna Esquerda
        if (!emFaseBalancoEsq)
        {
            if (receptorExo.AnguloQuadrilEsq >= limiarFlexaoPasso)
            {
                emFaseBalancoEsq = true;
                picoAtualQuadrilEsq = receptorExo.AnguloQuadrilEsq;
            }
        }
        else
        {
            if (receptorExo.AnguloQuadrilEsq > picoAtualQuadrilEsq)
                picoAtualQuadrilEsq = receptorExo.AnguloQuadrilEsq;

            if (receptorExo.AnguloQuadrilEsq <= limiarRetornoPasso)
            {
                emFaseBalancoEsq = false;
                passosPernaEsquerda++;
                totalPassos = passosPernaDireita + passosPernaEsquerda;
                timestampsUltimosPassos.Enqueue(tempoSessaoSegundos);

                RegistrarPicoPasso(ultimosPicosQuadrilEsq, picoAtualQuadrilEsq);
                RecalcularSimetriaDeMarcha();
            }
        }
    }

    private void RegistrarPicoPasso(Queue<float> fila, float valorPico)
    {
        fila.Enqueue(valorPico);
        while (fila.Count > TAMANHO_JANELA_SIMETRIA)
            fila.Dequeue();
    }

    private void RecalcularSimetriaDeMarcha()
    {
        if (ultimosPicosQuadrilDir.Count == 0 || ultimosPicosQuadrilEsq.Count == 0)
            return;

        float mediaDir = CalcularMediaFila(ultimosPicosQuadrilDir);
        float mediaEsq = CalcularMediaFila(ultimosPicosQuadrilEsq);

        float denominador = (mediaDir + mediaEsq) * 0.5f;
        if (denominador > 1.0f)
        {
            // Índice de Assimetria de Robinson (ASI) convertido para Simetria (100% = perfeitamente simétrico)
            float assimetria = Mathf.Abs(mediaDir - mediaEsq) / denominador * 100f;
            indiceSimetriaMarcha = Mathf.Clamp(100f - assimetria, 0f, 100f);

            somaSimetria += indiceSimetriaMarcha;
            amostrasSimetria++;

            if (indiceSimetriaMarcha >= 82f)
            {
                diagnosticoSimetria = "Simétrico";
            }
            else if (mediaDir > mediaEsq)
            {
                diagnosticoSimetria = "Déficit Perna Esquerda";
            }
            else
            {
                diagnosticoSimetria = "Déficit Perna Direita";
            }
        }
    }

    private float CalcularMediaFila(Queue<float> fila)
    {
        if (fila.Count == 0) return 0f;
        float soma = 0f;
        foreach (float v in fila) soma += v;
        return soma / fila.Count;
    }

    private void AtualizarCadencia()
    {
        // Mantém apenas os passos dos últimos 20 segundos na janela móvel para cadência reativa
        while (timestampsUltimosPassos.Count > 0 && (tempoSessaoSegundos - timestampsUltimosPassos.Peek()) > 20f)
        {
            timestampsUltimosPassos.Dequeue();
        }

        float janelaEfetiva = Mathf.Min(tempoSessaoSegundos, 20f);
        if (janelaEfetiva > 2f && timestampsUltimosPassos.Count > 0)
        {
            cadenciaPassosPorMinuto = (timestampsUltimosPassos.Count / janelaEfetiva) * 60f;
        }
        else
        {
            cadenciaPassosPorMinuto = 0f;
        }
    }

    private void GravarAmostraCSV()
    {
        if (csvWriter == null || relatorioFinalizado) return;

        try
        {
            float qD = receptorExo != null ? receptorExo.AnguloQuadrilDir : 0f;
            float jD = receptorExo != null ? receptorExo.AnguloJoelhoDir : 0f;
            float qE = receptorExo != null ? receptorExo.AnguloQuadrilEsq : 0f;
            float jE = receptorExo != null ? receptorExo.AnguloJoelhoEsq : 0f;
            string estadoDrone = DroneGuiaIA.Instancia != null ? DroneGuiaIA.Instancia.EstadoAtualNome : "N/A";

            string linha = string.Format(
                CultureInfo.InvariantCulture,
                "{0:F2},{1:F2},{2:F2},{3:F2},{4:F2},{5:F3},{6:F2},{7},{8},{9},{10:F1},{11:F1},{12}",
                tempoSessaoSegundos,
                qD, jD, qE, jE,
                velocidadeAtualMs,
                distanciaPercorridaMetros,
                passosPernaDireita,
                passosPernaEsquerda,
                totalPassos,
                cadenciaPassosPorMinuto,
                indiceSimetriaMarcha,
                estadoDrone
            );

            csvWriter.WriteLine(linha);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Telemetria Clínica IC] Erro ao gravar amostra CSV: " + e.Message);
        }
    }

    public void SalvarRelatorioFinal()
    {
        if (relatorioFinalizado) return;
        relatorioFinalizado = true;

        try
        {
            if (csvWriter != null)
            {
                csvWriter.Flush();
                csvWriter.Close();
                csvWriter = null;
            }

            float simetriaMediaSessao = amostrasSimetria > 0 ? (float)(somaSimetria / amostrasSimetria) : indiceSimetriaMarcha;
            float cadenciaMediaGlobal = tempoSessaoSegundos > 1f ? (totalPassos / tempoSessaoSegundos) * 60f : 0f;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("======================================================================");
            sb.AppendLine("   RELATÓRIO BIOMECÂNICO DE SESSÃO - EXOTAO VR REHAB (ReRobLab / IC)  ");
            sb.AppendLine("======================================================================");
            sb.AppendLine($"Data/Hora Início     : {inicioSessaoTimestamp:dd/MM/yyyy HH:mm:ss}");
            sb.AppendLine($"Data/Hora Término    : {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            sb.AppendLine($"Ambiente Virtual     : {nomeCenaAtual}");
            sb.AppendLine($"Modo de Reabilitação : {GerenciadorGlobal.ModoReabilitacao}");
            sb.AppendLine($"Fonte de Sinais      : {(receptorExo != null ? receptorExo.FonteDeDadosAtiva : "Controlador VR")}");
            sb.AppendLine("----------------------------------------------------------------------");
            sb.AppendLine("1. DESEMPENHO ESPAÇO-TEMPORAL DA MARCHA");
            sb.AppendLine($"   - Duração Total da Sessão : {tempoSessaoSegundos:F1} s ({tempoSessaoSegundos / 60f:F1} min)");
            sb.AppendLine($"   - Distância Percorrida    : {distanciaPercorridaMetros:F2} m");
            sb.AppendLine($"   - Velocidade Média        : {velocidadeMediaMs:F2} m/s ({velocidadeMediaMs * 3.6f:F2} km/h)");
            sb.AppendLine($"   - Total de Passos         : {totalPassos} (Direita: {passosPernaDireita} | Esquerda: {passosPernaEsquerda})");
            sb.AppendLine($"   - Cadência Média Global   : {cadenciaMediaGlobal:F1} passos/min (SPM)");
            sb.AppendLine("----------------------------------------------------------------------");
            sb.AppendLine("2. AMPLITUDE DE MOVIMENTO ARTICULAR MÁXIMA (ROM)");
            sb.AppendLine($"   - Quadril Direito (Máx)   : {amplitudeMaxQuadrilDir:F1}°");
            sb.AppendLine($"   - Quadril Esquerdo (Máx)  : {amplitudeMaxQuadrilEsq:F1}°");
            sb.AppendLine($"   - Joelho Direito (Máx)    : {amplitudeMaxJoelhoDir:F1}°");
            sb.AppendLine($"   - Joelho Esquerdo (Máx)   : {amplitudeMaxJoelhoEsq:F1}°");
            sb.AppendLine("----------------------------------------------------------------------");
            sb.AppendLine("3. ANÁLISE DE SIMETRIA MOTORA BILATERAL (Índice Robinson ASI)");
            sb.AppendLine($"   - Simetria Média da Sessão: {simetriaMediaSessao:F1}%");
            sb.AppendLine($"   - Simetria Final          : {indiceSimetriaMarcha:F1}%");
            sb.AppendLine($"   - Classificação Clínica   : {diagnosticoSimetria}");
            sb.AppendLine("----------------------------------------------------------------------");
            sb.AppendLine($"Arquivo CSV Bruto: {Path.GetFileName(caminhoArquivoCSV)}");
            sb.AppendLine("======================================================================");

            File.WriteAllText(caminhoArquivoResumo, sb.ToString(), Encoding.UTF8);
            Debug.Log($"[Telemetria Clínica IC] Relatório final salvo com sucesso em:\n{caminhoArquivoResumo}");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Telemetria Clínica IC] Erro ao salvar relatório resumo: " + e.Message);
        }
    }

    void OnDestroy()
    {
        SalvarRelatorioFinal();
        if (Instancia == this) Instancia = null;
    }

    void OnApplicationQuit()
    {
        SalvarRelatorioFinal();
    }
}
