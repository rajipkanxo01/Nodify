using System;
using ToolForDialogue._Project.Dialogue.Runtime.Assets;
using ToolForDialogue._Project.Dialogue.Runtime.Core;
using UnityEngine;

namespace ToolForDialogue._Project.Dialogue.Runtime.Types
{
    /// <summary>
    /// Unity-serializable discriminated union used for command arguments and literal values.
    /// </summary>
    [Serializable]
    public sealed class SerializedValue
    {
        [SerializeField] private DialogueValueType _type;
        [SerializeField] private bool _booleanValue;
        [SerializeField] private int _integerValue;
        [SerializeField] private float _numberValue;
        [SerializeField] private string _stringValue;
        [SerializeField] private string _enumTypeName;
        [SerializeField] private string _enumValue;
        [SerializeField] private UnityEngine.Object _objectReference;
        [SerializeField] private DialogueAsset _entityAssetReference;
        [SerializeField] private SerializableGuid _entityId;
        [SerializeField] private string _sceneBindingKey;

        public DialogueValueType type => _type;
        public bool booleanValue => _booleanValue;
        public int integerValue => _integerValue;
        public float numberValue => _numberValue;
        public string stringValue => _stringValue;
        public string enumTypeName => _enumTypeName;
        public string enumValue => _enumValue;
        public UnityEngine.Object objectReference => _objectReference;
        public DialogueAsset entityAssetReference => _entityAssetReference;
        public SerializableGuid entityId => _entityId;
        public string sceneBindingKey => _sceneBindingKey;
    }
}
