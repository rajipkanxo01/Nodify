using System;
using System.Collections.Generic;
using ToolForDialogue._Project.Dialogue.Runtime.Core;
using UnityEngine;

namespace ToolForDialogue._Project.Dialogue.Runtime.Types
{
    /// <summary>
    /// Represents one graph node and its ordered sequence of dialogue elements.
    /// </summary>
    [Serializable]
    public sealed class PassageData
    {
        [SerializeField, HideInInspector] private SerializableGuid _id = SerializableGuid.NewGuid();
        [SerializeField] private string _title = "Passage";
        [SerializeField] private Vector2 _graphPosition;
        [SerializeReference] private List<DialogueElement> _elements = new List<DialogueElement>();

        public SerializableGuid id => _id;
        public string title => _title;
        public Vector2 graphPosition => _graphPosition;
        public IReadOnlyList<DialogueElement> elements => _elements;
    }
}
