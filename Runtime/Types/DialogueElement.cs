using System;
using ToolForDialogue._Project.Dialogue.Runtime.Core;
using UnityEngine;

namespace ToolForDialogue._Project.Dialogue.Runtime.Types
{
    /// <summary>
    /// Base type for every polymorphic item stored in a passage content sequence.
    /// </summary>
    [Serializable]
    public abstract class DialogueElement
    {
        [SerializeField, HideInInspector] private SerializableGuid _id = SerializableGuid.NewGuid();
        [SerializeField] private bool _enabled = true;

        public SerializableGuid id => _id;
        public bool enabled => _enabled;
    }
}
