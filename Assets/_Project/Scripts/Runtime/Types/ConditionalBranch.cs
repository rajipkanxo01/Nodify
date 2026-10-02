using System;
using System.Collections.Generic;
using ToolForDialogue._Project.Dialogue.Runtime.Conditions;
using ToolForDialogue._Project.Dialogue.Runtime.Core;
using UnityEngine;

namespace ToolForDialogue._Project.Dialogue.Runtime.Types
{
    /// <summary>
    /// Pairs a condition with the nested dialogue elements executed when it succeeds.
    /// </summary>
    [Serializable]
    public sealed class ConditionalBranch
    {
        [SerializeField, HideInInspector] private SerializableGuid _id = SerializableGuid.NewGuid();
        [SerializeReference] private ConditionExpression _condition;
        [SerializeReference] private List<DialogueElement> _elements = new List<DialogueElement>();

        public SerializableGuid id => _id;
        public ConditionExpression condition => _condition;
        public IReadOnlyList<DialogueElement> elements => _elements;
    }
}
