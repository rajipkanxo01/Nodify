using UnityEngine.UIElements;

namespace ToolForDialogue.Editor._Project.Scripts.Editor.Tabs
{
    /// <summary>
    /// Owns conversation creation, selection, and passage-graph authoring controls.
    /// </summary>
    internal sealed class ConversationsTab : DialogueEditorTabBase
    {
        public override DialogueEditorTabId id => DialogueEditorTabId.Conversations;
        public override string label => "Conversations";
        protected override string description =>
            "Create conversation files and edit their passage graphs.";

        protected override void BuildContent(
            VisualElement container,
            DialogueEditorTabContext context)
        {
            container.Add(CreateComingSoonMessage());
        }
    }
}
