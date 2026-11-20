using System;
using ToolForDialogue.Types;
using UnityEngine;

namespace ToolForDialogue.Conditions
{
    /// <summary>
    /// Stores one side of a comparison as either a literal value or variable reference.
    /// </summary>
    [Serializable]
    public sealed class ValueOperand
    {
        [SerializeField] private ValueOperandKind _kind;
        [SerializeField] private SerializedValue _literal = new();
        [SerializeField] private VariableReference _variable = new();

        public ValueOperandKind kind => _kind;
        public SerializedValue literal => _literal;
        public VariableReference variable => _variable;
    }
}
