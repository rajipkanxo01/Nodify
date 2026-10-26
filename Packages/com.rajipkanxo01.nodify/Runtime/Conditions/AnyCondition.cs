using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToolForDialogue._Project.Dialogue.Runtime.Conditions
{
    /// <summary>
    /// Requires at least one child condition to evaluate to true.
    /// </summary>
    [Serializable]
    public sealed class AnyCondition : ConditionExpression
    {
        [SerializeReference] private List<ConditionExpression> _children = new List<ConditionExpression>();

        public IReadOnlyList<ConditionExpression> children => _children;
    }
}
