using System;
using UnityEngine;

namespace ToolForDialogue._Project.Dialogue.Runtime.Types
{
    /// <summary>
    /// Stores one command parameter by stable parameter ID and serialized value.
    /// </summary>
    [Serializable]
    public sealed class CommandArgument
    {
        [SerializeField] private string _parameterId;
        [SerializeField] private SerializedValue _value = new SerializedValue();

        public string parameterId => _parameterId;
        public SerializedValue value => _value;
    }
}
