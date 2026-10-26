using UnityEngine.UIElements;

namespace ToolForDialogue.Editor._Project.Scripts.Editor.Tabs
{
    /// <summary>
    /// Builds the shared tab layout and delegates tab-specific controls to derived classes.
    /// </summary>
    internal abstract class DialogueEditorTabBase : IDialogueEditorTab
    {
        public abstract DialogueEditorTabId id { get; }
        public abstract string label { get; }
        protected virtual bool requiresProject => true;
        protected virtual bool isWritingWorkspace => false;

        public VisualElement CreateContent(DialogueEditorTabContext context)
        {
            var root = new VisualElement();

            if (isWritingWorkspace)
            {
                root.AddToClassList(DialogueEditorStyleClasses.WritingWorkspace);
            }
            else
            {
                var heading = new Label(label);
                heading.AddToClassList(DialogueEditorStyleClasses.PanelTitle);
                root.Add(heading);
            }

            var panel = new VisualElement();
            panel.AddToClassList(DialogueEditorStyleClasses.Panel);

            if (requiresProject && context.selectedProject == null)
            {
                panel.Add(CreateMissingProjectMessage());
            }
            else
            {
                BuildContent(panel, context);
            }

            root.Add(panel);
            return root;
        }

        protected abstract void BuildContent(VisualElement container, DialogueEditorTabContext context);

        protected static Label CreateComingSoonMessage()
        {
            var label = new Label("To be implementd....");
            label.AddToClassList(DialogueEditorStyleClasses.ComingSoon);
            return label;
        }

        private static HelpBox CreateMissingProjectMessage()
        {
            var helpBox = new HelpBox("Select a Dialogue Project above to begin working in this tab.", HelpBoxMessageType.Info);
            helpBox.AddToClassList(DialogueEditorStyleClasses.EmptyState);
            return helpBox;
        }
    }
}
