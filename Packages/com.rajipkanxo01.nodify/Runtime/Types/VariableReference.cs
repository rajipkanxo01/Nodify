using System;
using ToolForDialogue._Project.Dialogue.Runtime.Assets;
using ToolForDialogue._Project.Dialogue.Runtime.Core;
using UnityEngine;

namespace ToolForDialogue._Project.Dialogue.Runtime.Types
{
    /// <summary>
    /// References a project- or conversation-scoped variable by owner and stable ID.
    /// </summary>
    [Serializable]
    public sealed class VariableReference
    {
        [SerializeField] private VariableScope _scope;
        [SerializeField] private DialogueProjectAsset _project;
        [SerializeField] private ConversationAsset _conversation;
        [SerializeField] private SerializableGuid _variableId;

        public VariableScope scope => _scope;
        public DialogueProjectAsset project => _project;
        public ConversationAsset conversation => _conversation;
        public SerializableGuid variableId => _variableId;
    }
}
