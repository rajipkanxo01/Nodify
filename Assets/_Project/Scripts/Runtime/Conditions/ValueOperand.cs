using System;
using ToolForDialogue._Project.Dialogue.Runtime.Types;
using UnityEngine;

namespace ToolForDialogue._Project.Dialogue.Runtime.Conditions
{
    /// <summary>
    /// Stores one side of a comparison as either a literal value or variable reference.
    /// </summary>
    [Serializable]
    public sealed class ValueOperand
    {
        [SerializeField] private ValueOperandKind _kind;
        [SerializeField] private SerializedValue _literal = new SerializedValue();
        [SerializeField] private VariableReference _variable = new VariableReference();

        public ValueOperandKind kind => _kind;
        public SerializedValue literal => _literal;
        public VariableReference variable => _variable;
    }
}
