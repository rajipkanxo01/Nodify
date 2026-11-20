using System;
using ToolForDialogue.Types;
using UnityEngine;

namespace ToolForDialogue.Conditions
{
    /// <summary>
    /// Tests a Boolean variable against an expected true or false value.
    /// </summary>
    [Serializable]
    public sealed class BooleanVariableCondition : ConditionExpression
    {
        [SerializeField] private VariableReference _variable = new();
        [SerializeField] private bool _expectedValue = true;

        public VariableReference variable => _variable;
        public bool expectedValue => _expectedValue;
    }
}
