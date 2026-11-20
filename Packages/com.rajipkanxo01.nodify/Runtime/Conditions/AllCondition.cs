using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToolForDialogue.Conditions
{
    /// <summary>
    /// Requires every child condition to evaluate to true.
    /// </summary>
    [Serializable]
    public sealed class AllCondition : ConditionExpression
    {
        [SerializeReference] private List<ConditionExpression> _children = new();

        public IReadOnlyList<ConditionExpression> children => _children;
    }
}
