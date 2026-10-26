using System;
using ToolForDialogue._Project.Dialogue.Runtime.Types;
using UnityEngine;

namespace ToolForDialogue._Project.Dialogue.Runtime.Conditions
{
    /// <summary>
    /// Evaluates a discovered Yarn function as a Boolean condition.
    /// </summary>
    [Serializable]
    public sealed class FunctionCondition : ConditionExpression
    {
        [SerializeField] private FunctionCall _function = new FunctionCall();
        [SerializeField] private bool _expectedValue = true;

        public FunctionCall function => _function;
        public bool expectedValue => _expectedValue;
    }
}
