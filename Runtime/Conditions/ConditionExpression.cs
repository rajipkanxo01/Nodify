using System;
using ToolForDialogue._Project.Dialogue.Runtime.Core;
using UnityEngine;

namespace ToolForDialogue._Project.Dialogue.Runtime.Conditions
{
    /// <summary>
    /// Base type for polymorphic condition nodes authored through the visual editor.
    /// </summary>
    [Serializable]
    public abstract class ConditionExpression
    {
        [SerializeField, HideInInspector] private SerializableGuid _id = SerializableGuid.NewGuid();

        public SerializableGuid id => _id;
    }
}
