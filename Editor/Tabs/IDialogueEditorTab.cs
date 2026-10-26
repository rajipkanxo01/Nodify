using UnityEngine.UIElements;

namespace ToolForDialogue.Editor._Project.Scripts.Editor.Tabs
{
    /// <summary>
    /// Defines the metadata and content factory required by every dialogue editor tab.
    /// </summary>
    internal interface IDialogueEditorTab
    {
        DialogueEditorTabId id { get; }
        string label { get; }
        VisualElement CreateContent(DialogueEditorTabContext context);
    }
}
