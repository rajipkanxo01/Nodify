using System;
using System.Collections.Generic;
using ToolForDialogue.Core;
using UnityEngine;

namespace ToolForDialogue.Types
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
        [SerializeReference] private List<DialogueElement> _elements = new();

        public SerializableGuid id => _id;
        public string title => _title;
        public Vector2 graphPosition => _graphPosition;
        public IReadOnlyList<DialogueElement> elements => _elements;
    }
}
