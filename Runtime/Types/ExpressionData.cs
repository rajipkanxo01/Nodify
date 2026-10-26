using System;
using ToolForDialogue._Project.Dialogue.Runtime.Core;
using UnityEngine;

namespace ToolForDialogue._Project.Dialogue.Runtime.Types
{
    /// <summary>
    /// Defines one named portrait expression owned by a character asset.
    /// </summary>
    [Serializable]
    public sealed class ExpressionData
    {
        [SerializeField] private SerializableGuid _id = SerializableGuid.NewGuid();
        [SerializeField] private string _displayName = "Expression";
        [SerializeField] private Sprite _portrait;

        public SerializableGuid id => _id;
        public string displayName => _displayName;
        public Sprite portrait => _portrait;
    }
}
