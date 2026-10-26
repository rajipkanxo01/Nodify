using System.Collections.Generic;
using ToolForDialogue._Project.Dialogue.Runtime.Core;
using ToolForDialogue._Project.Dialogue.Runtime.Types;
using UnityEngine;

namespace ToolForDialogue._Project.Dialogue.Runtime.Assets
{
    /// <summary>
    /// Defines a reusable dialogue character and the portrait expressions available to it.
    /// </summary>
    [CreateAssetMenu(fileName = "Character", menuName = "Nodify/Character", order = 2)]
    public sealed class CharacterAsset : DialogueAsset
    {
        [SerializeField] private string _displayName = "Character";
        [SerializeField] private SerializableGuid _defaultExpressionId;
        [SerializeField] private List<ExpressionData> _expressions = new List<ExpressionData>();

        public SerializableGuid characterId => id;
        public string displayName => _displayName;
        public SerializableGuid defaultExpressionId => _defaultExpressionId;
        public IReadOnlyList<ExpressionData> expressions => _expressions;
    }
}
