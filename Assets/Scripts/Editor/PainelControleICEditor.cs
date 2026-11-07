using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Menu de ferramentas no topo da Unity ("ExoTao IC") para facilitar a operação no laboratório ReRobLab.
/// </summary>
public static class PainelControleICEditor
{
    [MenuItem("ExoTao IC/1. Fixar Drone Guia IA e Telemetria na Cena Atual", false, 10)]
    public static void InserirSistemasNaCenaAtual()
    {
        ReceptorUDP receptor = Object.FindFirstObjectByType<ReceptorUDP>();
        CharacterController controle = Object.FindFirstObjectByType<CharacterController>();

        TelemetriaClinica telemetria = Object.FindFirstObjectByType<TelemetriaClinica>();
        if (telemetria == null)
        {
            GameObject goTel = new GameObject("[IC] Telemetria_Clinica_ReRobLab");
            Undo.RegisterCreatedObjectUndo(goTel, "Criar Telemetria Clinica");
            telemetria = goTel.AddComponent<TelemetriaClinica>();
        }
        telemetria.receptorExo = receptor;
        telemetria.controlePaciente = controle;

        DroneGuiaIA drone = Object.FindFirstObjectByType<DroneGuiaIA>();
        if (drone == null)
        {
            GameObject goDrone = new GameObject("[IC] Drone_Guia_IA_ExoBot");
            Undo.RegisterCreatedObjectUndo(goDrone, "Criar Drone Guia IA");
            drone = goDrone.AddComponent<DroneGuiaIA>();
        }
        drone.receptorExo = receptor;
        drone.telemetria = telemetria;

        Selection.activeGameObject = drone.gameObject;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        UnityEngine.Debug.Log("[ExoTao IC] Drone Guia IA e Telemetria Clínica adicionados e selecionados na Hierarchy!");
    }

    [MenuItem("ExoTao IC/2. Abrir Pasta de Relatórios Clínicos (CSV e Resumos)", false, 25)]
    public static void AbrirPastaRelatorios()
    {
        DirectoryInfo raiz = Directory.GetParent(Application.dataPath);
        string pasta = Path.Combine(raiz != null ? raiz.FullName : Application.persistentDataPath, "Relatorios_Clinicos");
        if (!Directory.Exists(pasta))
            Directory.CreateDirectory(pasta);

        EditorUtility.RevealInFinder(pasta);
    }

    [MenuItem("ExoTao IC/3. Iniciar Simulador Python do Exoesqueleto (UDP 5005)", false, 40)]
    public static void IniciarSimuladorPython()
    {
        DirectoryInfo raiz = Directory.GetParent(Application.dataPath);
        if (raiz == null) return;

        string scriptSim = Path.Combine(raiz.FullName, "simulador_robo.py");
        if (!File.Exists(scriptSim))
        {
            EditorUtility.DisplayDialog("Simulador não encontrado", $"Arquivo não encontrado:\n{scriptSim}", "OK");
            return;
        }

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c start \"Simulador ExoTao UDP\" python \"{scriptSim}\"",
            WorkingDirectory = raiz.FullName,
            UseShellExecute = true
        };
        Process.Start(psi);
    }

    [MenuItem("ExoTao IC/4. Gerar Gráficos Científicos da Última Sessão (Python)", false, 41)]
    public static void GerarGraficosPython()
    {
        DirectoryInfo raiz = Directory.GetParent(Application.dataPath);
        if (raiz == null) return;

        string scriptGraficos = Path.Combine(raiz.FullName, "analisador_telemetria_ic.py");
        if (!File.Exists(scriptGraficos))
        {
            EditorUtility.DisplayDialog("Analisador não encontrado", $"Arquivo não encontrado:\n{scriptGraficos}", "OK");
            return;
        }

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c start \"Analisador Biomecanico IC\" cmd /k python \"{scriptGraficos}\"",
            WorkingDirectory = raiz.FullName,
            UseShellExecute = true
        };
        Process.Start(psi);
    }
}
