using System;
using ToolForDialogue._Project.Dialogue.Runtime.Types;
using UnityEngine;

namespace ToolForDialogue._Project.Dialogue.Runtime.Conditions
{
    /// <summary>
    /// Tests a Boolean variable against an expected true or false value.
    /// </summary>
    [Serializable]
    public sealed class BooleanVariableCondition : ConditionExpression
    {
        [SerializeField] private VariableReference _variable = new VariableReference();
        [SerializeField] private bool _expectedValue = true;

        public VariableReference variable => _variable;
        public bool expectedValue => _expectedValue;
    }
}
