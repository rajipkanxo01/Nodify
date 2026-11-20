using System;
using UnityEngine;

namespace ToolForDialogue.Conditions
{
    /// <summary>
    /// Compares two operands with a selected comparison operator.
    /// </summary>
    [Serializable]
    public sealed class ComparisonCondition : ConditionExpression
    {
        [SerializeField] private ValueOperand _left = new();
        [SerializeField] private ComparisonOperator _comparison;
        [SerializeField] private ValueOperand _right = new();

        public ValueOperand left => _left;
        public ComparisonOperator comparison => _comparison;
        public ValueOperand right => _right;
    }
}
