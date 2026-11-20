using System;
using System.Collections.Generic;
using ToolForDialogue.Assets;
using ToolForDialogue.Core;
using UnityEngine;

namespace ToolForDialogue.Types
{
    /// <summary>
    /// Stores one spoken line with an optional speaker, expression, and metadata tags.
    /// </summary>
    [Serializable]
    public sealed class LineElement : DialogueElement
    {
        [SerializeField] private CharacterAsset _speaker;
        [SerializeField] private SerializableGuid _expressionId;
        [SerializeField, TextArea(2, 8)] private string _text;
        [SerializeField] private List<string> _tags = new();

        public CharacterAsset speaker => _speaker;
        public SerializableGuid expressionId => _expressionId;
        public string text => _text;
        public IReadOnlyList<string> tags => _tags;
    }
}
