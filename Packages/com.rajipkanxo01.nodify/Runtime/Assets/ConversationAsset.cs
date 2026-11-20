using System.Collections.Generic;
using ToolForDialogue.Core;
using ToolForDialogue.Types;
using UnityEngine;

namespace ToolForDialogue.Assets
{
    /// <summary>
    /// Stores one independently versionable conversation graph and its local variables.
    /// </summary>
    [CreateAssetMenu(fileName = "Conversation", menuName = "Nodify/Conversation", order = 1)]
    public sealed class ConversationAsset : DialogueAsset
    {
        [SerializeField] private string _displayName = "Conversation";
        [SerializeField] private SerializableGuid _startPassageId;
        [SerializeField] private List<PassageData> _passages = new();
        [SerializeField] private ConversationEditorState _editorState = new();

        public string displayName => _displayName;
        public SerializableGuid startPassageId => _startPassageId;
        public IReadOnlyList<PassageData> passages => _passages;
        public ConversationEditorState editorState => _editorState;
    }
}
