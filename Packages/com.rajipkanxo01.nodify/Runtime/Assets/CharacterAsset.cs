using System.Collections.Generic;
using ToolForDialogue.Core;
using ToolForDialogue.Types;
using UnityEngine;

namespace ToolForDialogue.Assets
{
    /// <summary>
    /// Defines a reusable dialogue character and the portrait expressions available to it.
    /// </summary>
    [CreateAssetMenu(fileName = "Character", menuName = "Nodify/Character", order = 2)]
    public sealed class CharacterAsset : DialogueAsset
    {
        [SerializeField] private string _displayName = "Character";
        [SerializeField] private SerializableGuid _defaultExpressionId;
        [SerializeField] private List<ExpressionData> _expressions = new();

        public SerializableGuid characterId => id;
        public string displayName => _displayName;
        public SerializableGuid defaultExpressionId => _defaultExpressionId;
        public IReadOnlyList<ExpressionData> expressions => _expressions;
    }
}
