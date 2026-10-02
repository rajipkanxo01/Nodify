using UnityEngine.UIElements;

namespace ToolForDialogue.Editor._Project.Scripts.Editor.Tabs
{
    /// <summary>
    /// Owns project- and conversation-scoped variable authoring controls.
    /// </summary>
    internal sealed class VariablesTab : DialogueEditorTabBase
    {
        public override DialogueEditorTabId id => DialogueEditorTabId.Variables;
        public override string label => "Variables";
        protected override string description =>
            "Define project and conversation state used by branching dialogue.";

        protected override void BuildContent(
            VisualElement container,
            DialogueEditorTabContext context)
        {
            container.Add(CreateComingSoonMessage());
        }
    }
}
