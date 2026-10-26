using ToolForDialogue._Project.Dialogue.Runtime.Core;
using UnityEngine;

namespace ToolForDialogue._Project.Dialogue.Runtime.Assets
{
    /// <summary>
    /// Provides schema versioning and stable identity for every top-level dialogue asset.
    /// </summary>
    public abstract class DialogueAsset : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField, HideInInspector] private int _schemaVersion = CurrentSchemaVersion;
        [SerializeField, HideInInspector] private SerializableGuid _id = SerializableGuid.NewGuid();

        public int schemaVersion => _schemaVersion;
        public SerializableGuid id => _id;

        protected virtual void OnValidate()
        {
            if (!_id.isValid)
            {
                _id = SerializableGuid.NewGuid();
            }

            if (_schemaVersion <= 0)
            {
                _schemaVersion = CurrentSchemaVersion;
            }
        }
    }
}
