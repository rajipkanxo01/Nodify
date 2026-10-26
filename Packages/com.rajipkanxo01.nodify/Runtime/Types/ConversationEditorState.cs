using System;
using UnityEngine;

namespace ToolForDialogue._Project.Dialogue.Runtime.Types
{
    /// <summary>
    /// Stores collaboration-safe default graph layout state for a conversation.
    /// </summary>
    [Serializable]
    public sealed class ConversationEditorState
    {
        [SerializeField] private Vector2 _defaultPan;
        [SerializeField] private float _defaultZoom = 1f;

        public Vector2 defaultPan => _defaultPan;
        public float defaultZoom => _defaultZoom;
    }
}
