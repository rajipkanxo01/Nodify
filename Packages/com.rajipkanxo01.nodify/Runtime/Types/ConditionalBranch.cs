using System;
using System.Collections.Generic;
using ToolForDialogue.Conditions;
using ToolForDialogue.Core;
using UnityEngine;

namespace ToolForDialogue.Types
{
    /// <summary>
    /// Pairs a condition with the nested dialogue elements executed when it succeeds.
    /// </summary>
    [Serializable]
    public sealed class ConditionalBranch
    {
        [SerializeField, HideInInspector] private SerializableGuid _id = SerializableGuid.NewGuid();
        [SerializeReference] private ConditionExpression _condition;
        [SerializeReference] private List<DialogueElement> _elements = new();

        public SerializableGuid id => _id;
        public ConditionExpression condition => _condition;
        public IReadOnlyList<DialogueElement> elements => _elements;
    }
}
