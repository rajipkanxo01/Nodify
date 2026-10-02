using UnityEngine.UIElements;

namespace ToolForDialogue.Editor._Project.Scripts.Editor.Tabs
{
    /// <summary>
    /// Presents output and editor preferences that do not require a selected project.
    /// </summary>
    internal sealed class SettingsTab : DialogueEditorTabBase
    {
        public override DialogueEditorTabId id => DialogueEditorTabId.Settings;
        public override string label => "Settings";
        protected override string description =>
            "Configure generated Yarn output and editor behavior.";
        protected override bool requiresProject => false;

        protected override void BuildContent(
            VisualElement container,
            DialogueEditorTabContext context)
        {
            container.Add(CreateComingSoonMessage());
        }
    }
}
