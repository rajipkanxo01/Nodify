using System;
using ToolForDialogue.Core;
using UnityEngine;

namespace ToolForDialogue.Types
{
    /// <summary>
    /// Transfers dialogue execution to another passage in the same conversation.
    /// </summary>
    [Serializable]
    public sealed class JumpElement : DialogueElement
    {
        [SerializeField] private SerializableGuid _destinationPassageId;

        public SerializableGuid destinationPassageId => _destinationPassageId;
    }
}
