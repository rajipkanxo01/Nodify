using System;
using ToolForDialogue._Project.Dialogue.Runtime.Conditions;
using ToolForDialogue._Project.Dialogue.Runtime.Core;
using UnityEngine;

namespace ToolForDialogue._Project.Dialogue.Runtime.Types
{
    /// <summary>
    /// Defines one choice label, availability condition, display behavior, and destination.
    /// </summary>
    [Serializable]
    public sealed class ChoiceOption
    {
        [SerializeField, HideInInspector] private SerializableGuid _id = SerializableGuid.NewGuid();
        [SerializeField] private string _text = "Choice";
        [SerializeReference] private ConditionExpression _availability;
        [SerializeField] private UnavailableChoiceBehavior _unavailableBehavior;
        [SerializeField] private SerializableGuid _destinationPassageId;

        public SerializableGuid id => _id;
        public string text => _text;
        public ConditionExpression availability => _availability;
        public UnavailableChoiceBehavior unavailableBehavior => _unavailableBehavior;
        public SerializableGuid destinationPassageId => _destinationPassageId;
    }
}
