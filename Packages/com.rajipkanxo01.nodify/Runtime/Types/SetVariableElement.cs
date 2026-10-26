using System;
using UnityEngine;

namespace ToolForDialogue._Project.Dialogue.Runtime.Types
{
    public enum VariableAssignmentOperation { Set, Add, Subtract }

    /// <summary>Changes a project variable when execution reaches this element.</summary>
    [Serializable]
    public sealed class SetVariableElement : DialogueElement
    {
        [SerializeField] private VariableReference _variable = new VariableReference();
        [SerializeField] private VariableAssignmentOperation _operation;
        [SerializeField] private SerializedValue _value = new SerializedValue();

        public VariableReference variable => _variable;
        public VariableAssignmentOperation operation => _operation;
        public SerializedValue value => _value;
    }
}
