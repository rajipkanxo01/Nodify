using System;
using ToolForDialogue.Types;
using UnityEngine;

namespace ToolForDialogue.Conditions
{
    /// <summary>
    /// Evaluates a discovered Yarn function as a Boolean condition.
    /// </summary>
    [Serializable]
    public sealed class FunctionCondition : ConditionExpression
    {
        [SerializeField] private FunctionCall _function = new();
        [SerializeField] private bool _expectedValue = true;

        public FunctionCall function => _function;
        public bool expectedValue => _expectedValue;
    }
}
