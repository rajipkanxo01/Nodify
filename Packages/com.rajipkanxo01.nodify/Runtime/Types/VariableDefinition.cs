using System;
using ToolForDialogue.Core;
using UnityEngine;

namespace ToolForDialogue.Types
{
    /// <summary>
    /// Defines an authored variable, its stable identity, type, and default value.
    /// </summary>
    [Serializable]
    public sealed class VariableDefinition
    {
        [SerializeField] private SerializableGuid _id = SerializableGuid.NewGuid();
        [SerializeField] private string _displayName = "Variable";
        [SerializeField] private VariableValueType _type;
        [SerializeField] private SerializedValue _defaultValue = new();

        public SerializableGuid id => _id;
        public string displayName => _displayName;
        public VariableValueType type => _type;
        public SerializedValue defaultValue => _defaultValue;
    }
}
