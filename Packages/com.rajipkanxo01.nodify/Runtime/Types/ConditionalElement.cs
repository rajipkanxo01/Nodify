using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToolForDialogue._Project.Dialogue.Runtime.Types
{
    /// <summary>
    /// Selects one nested content sequence by evaluating ordered conditional branches.
    /// </summary>
    [Serializable]
    public sealed class ConditionalElement : DialogueElement
    {
        [SerializeField] private List<ConditionalBranch> _branches = new List<ConditionalBranch>();
        [SerializeReference] private List<DialogueElement> _elseElements = new List<DialogueElement>();

        public IReadOnlyList<ConditionalBranch> branches => _branches;
        public IReadOnlyList<DialogueElement> elseElements => _elseElements;
    }
}
