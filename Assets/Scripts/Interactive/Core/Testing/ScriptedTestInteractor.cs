using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace VRRehab.Core.Testing
{
    /// <summary>
    /// "Mão de teste" usada SOMENTE pelos testes automáticos em Play Mode (Assets/Editor/AutoSetup/ForestPlayModeTests.cs).
    /// É um interactor do XRI sem controle físico: o teste chama XRInteractionManager.SelectEnter/SelectExit e move
    /// este transform quadro a quadro para simular o paciente segurando, balançando e soltando um objeto.
    /// Não é colocado em nenhuma cena nem prefab.
    /// </summary>
    [AddComponentMenu("")]
    public class ScriptedTestInteractor : XRBaseInteractor
    {
    }
}
