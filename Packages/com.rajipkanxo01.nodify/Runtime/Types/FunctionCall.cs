using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToolForDialogue._Project.Dialogue.Runtime.Types
{
    /// <summary>
    /// Stores a reference to a discovered Yarn function and its authored arguments.
    /// </summary>
    [Serializable]
    public sealed class FunctionCall
    {
        [SerializeField] private string _functionId;
        [SerializeField] private List<CommandArgument> _arguments = new List<CommandArgument>();

        public string functionId => _functionId;
        public IReadOnlyList<CommandArgument> arguments => _arguments;
    }
}
