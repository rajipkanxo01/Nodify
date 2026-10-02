using UnityEngine.UIElements;

namespace ToolForDialogue.Editor._Project.Scripts.Editor.Tabs
{
    /// <summary>
    /// Presents project-level information and links to the assets owned by the selected project.
    /// </summary>
    internal sealed class ProjectTab : DialogueEditorTabBase
    {
        public override DialogueEditorTabId id => DialogueEditorTabId.Project;
        public override string label => "Project";
        protected override string description =>
            "Review the selected project and its linked dialogue assets.";

        protected override void BuildContent(
            VisualElement container,
            DialogueEditorTabContext context)
        {
            container.Add(CreateComingSoonMessage());
        }
    }
}
