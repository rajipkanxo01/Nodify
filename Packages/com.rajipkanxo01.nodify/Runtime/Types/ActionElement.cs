using System;
using System.Collections.Generic;
using UnityEngine;
using ToolForDialogue.Assets;

namespace ToolForDialogue.Types
{
    /// <summary>
    /// Invokes a project-defined Yarn command. The consumer registers its runtime handler.
    /// </summary>
    [Serializable]
    public sealed class ActionElement : DialogueElement
    {
        [SerializeField] private string _commandId;
        [SerializeField] private DialogueProjectAsset _commandProject;
        [SerializeField] private List<CommandArgument> _arguments = new();

        public string commandId => _commandId;
        public DialogueProjectAsset commandProject => _commandProject;
        public IReadOnlyList<CommandArgument> arguments => _arguments;
    }
}
