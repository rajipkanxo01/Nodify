using System;
using UnityEngine;

namespace ToolForDialogue._Project.Dialogue.Runtime.Conditions
{
    /// <summary>
    /// Inverts the result of its child condition.
    /// </summary>
    [Serializable]
    public sealed class NotCondition : ConditionExpression
    {
        [SerializeReference] private ConditionExpression _child;

        public ConditionExpression child => _child;
    }
}
