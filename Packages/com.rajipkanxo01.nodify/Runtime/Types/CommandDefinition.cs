using System;
using System.Collections.Generic;
using ToolForDialogue.Core;
using UnityEngine;

namespace ToolForDialogue.Types
{
    /// <summary>An authoring signature; execution belongs to the consuming Yarn Dialogue Runner.</summary>
    [Serializable]
    public sealed class CommandDefinition
    {
        [SerializeField] private SerializableGuid _id = SerializableGuid.NewGuid();
        [SerializeField] private string _name = "new_command";
        [SerializeField] private List<CommandParameter> _parameters = new();

        public SerializableGuid id => _id;
        public string name => _name;
        public IReadOnlyList<CommandParameter> parameters => _parameters;
    }

    [Serializable]
    public sealed class CommandParameter
    {
        [SerializeField] private SerializableGuid _id = SerializableGuid.NewGuid();
        [SerializeField] private string _name = "argument";
        [SerializeField] private DialogueValueType _type = DialogueValueType.String;

        public SerializableGuid id => _id;
        public string name => _name;
        public DialogueValueType type => _type;
    }
}
