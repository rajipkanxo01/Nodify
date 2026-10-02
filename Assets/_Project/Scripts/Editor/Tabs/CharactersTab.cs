using UnityEngine.UIElements;

namespace ToolForDialogue.Editor._Project.Scripts.Editor.Tabs
{
    /// <summary>
    /// Owns character, portrait, and expression management controls.
    /// </summary>
    internal sealed class CharactersTab : DialogueEditorTabBase
    {
        public override DialogueEditorTabId id => DialogueEditorTabId.Characters;
        public override string label => "Characters";
        protected override string description => "Manage reusable characters, portraits, and expressions.";

        protected override void BuildContent(VisualElement container, DialogueEditorTabContext context)
        {
            container.Add(CreateComingSoonMessage());
        }
    }
}
