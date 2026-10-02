using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToolForDialogue._Project.Dialogue.Runtime.Types
{
    /// <summary>
    /// Invokes a discovered project command with serialized typed arguments.
    /// </summary>
    [Serializable]
    public sealed class ActionElement : DialogueElement
    {
        [SerializeField] private string _commandId;
        [SerializeField] private List<CommandArgument> _arguments = new List<CommandArgument>();

        public string commandId => _commandId;
        public IReadOnlyList<CommandArgument> arguments => _arguments;
    }
}
