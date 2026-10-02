using ToolForDialogue._Project.Dialogue.Runtime.Assets;

namespace ToolForDialogue.Editor._Project.Scripts.Editor.Tabs
{
    /// <summary>
    /// Supplies shared window state to a tab without coupling the tab to the editor window.
    /// </summary>
    internal sealed class DialogueEditorTabContext
    {
        public DialogueEditorTabContext(DialogueProjectAsset selectedProject)
        {
            this.selectedProject = selectedProject;
        }

        public DialogueProjectAsset selectedProject { get; }
    }
}
