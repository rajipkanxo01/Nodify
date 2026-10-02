using UnityEngine.UIElements;

namespace ToolForDialogue.Editor._Project.Scripts.Editor.Tabs
{
    /// <summary>
    /// Presents validation diagnostics and navigation to invalid authored data.
    /// </summary>
    internal sealed class ValidationTab : DialogueEditorTabBase
    {
        public override DialogueEditorTabId id => DialogueEditorTabId.Validation;
        public override string label => "Validation";
        protected override string description => "Find broken references and generation problems before compiling Yarn.";

        protected override void BuildContent(
            VisualElement container,
            DialogueEditorTabContext context)
        {
            container.Add(CreateComingSoonMessage());
        }
    }
}
