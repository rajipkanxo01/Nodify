using System.Collections.Generic;
using ToolForDialogue._Project.Dialogue.Runtime.Types;
using UnityEngine;
using Yarn.Unity;

namespace ToolForDialogue._Project.Dialogue.Runtime.Assets
{
    /// <summary>
    /// Acts as the root asset that collects conversations, characters, variables, and generation settings.
    /// </summary>
    [CreateAssetMenu(fileName = "Dialogue Project", menuName = "Dialogue/Project", order = 0)]
    public sealed class DialogueProjectAsset : DialogueAsset
    {
        [SerializeField] private string _displayName = "Dialogue Project";
        [SerializeField] private YarnProject _yarnProject;
        [SerializeField] private List<CharacterAsset> _characters = new List<CharacterAsset>();
        [SerializeField] private List<ConversationAsset> _conversations = new List<ConversationAsset>();
        [SerializeField] private List<VariableDefinition> _variables = new List<VariableDefinition>();
        [SerializeField] private DialogueGenerationSettings _generation = new DialogueGenerationSettings();

        public string displayName => _displayName;
        public YarnProject yarnProject => _yarnProject;
        public IReadOnlyList<CharacterAsset> characters => _characters;
        public IReadOnlyList<ConversationAsset> conversations => _conversations;
        public IReadOnlyList<VariableDefinition> variables => _variables;
        public DialogueGenerationSettings generation => _generation;

        /// <summary>
        /// Connects this authoring asset to the Yarn Spinner project it represents.
        /// Project creation is owned by the editor assembly.
        /// </summary>
        internal void Initialize(YarnProject yarnProject)
        {
            _yarnProject = yarnProject;
        }
    }
}
