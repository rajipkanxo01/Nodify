using System;
using UnityEngine;

namespace ToolForDialogue._Project.Dialogue.Runtime.Types
{
    /// <summary>
    /// Stores project-level options controlling validation and generated Yarn output.
    /// </summary>
    [Serializable]
    public sealed class DialogueGenerationSettings
    {
        [SerializeField] private string _outputFolder = "Assets/Generated/Dialogue";
        [SerializeField] private bool _generateOnSave;
        [SerializeField] private bool _stopOnValidationErrors = true;

        public string outputFolder => _outputFolder;
        public bool generateOnSave => _generateOnSave;
        public bool stopOnValidationErrors => _stopOnValidationErrors;
    }
}
