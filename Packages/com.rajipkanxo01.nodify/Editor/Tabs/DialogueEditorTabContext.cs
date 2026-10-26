using System;
using ToolForDialogue._Project.Dialogue.Runtime.Assets;

namespace ToolForDialogue.Editor._Project.Scripts.Editor.Tabs
{
    /// <summary>
    /// Supplies shared window state to a tab without coupling the tab to the editor window.
    /// </summary>
    internal sealed class DialogueEditorTabContext
    {
        public DialogueEditorTabContext(
            DialogueProjectAsset selectedProject,
            Action<DialogueEditorTabId> selectTab)
        {
            this.selectedProject = selectedProject;
            _selectTab = selectTab;
        }

        private readonly Action<DialogueEditorTabId> _selectTab;
        public DialogueProjectAsset selectedProject { get; }

        public void SelectTab(DialogueEditorTabId tabId)
        {
            _selectTab?.Invoke(tabId);
        }
    }
}
