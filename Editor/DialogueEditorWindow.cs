using System.Collections.Generic;
using System.Linq;
using ToolForDialogue._Project.Dialogue.Runtime.Assets;
using ToolForDialogue.Editor._Project.Scripts.Editor.Services;
using ToolForDialogue.Editor._Project.Scripts.Editor.Tabs;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using ConversationsTab = ToolForDialogue.Editor._Project.Scripts.Editor.Tabs.Conversation.ConversationsTab;

namespace ToolForDialogue.Editor._Project.Scripts.Editor
{
    /// <summary>
    /// Owns the dialogue editor window lifecycle, project selection, and tab navigation.
    /// Individual tabs own their content and behavior.
    /// </summary>
    public sealed class DialogueEditorWindow : EditorWindow
    {
        private const string windowTitle = "Dialogue Editor";
        private const string styleSheetGuid = "ac5ff64b979ee334b920d45239f5a4b6";

        [SerializeField] private DialogueProjectAsset _selectedProject;
        [SerializeField] private DialogueEditorTabId _selectedTabId;

        private readonly Dictionary<DialogueEditorTabId, Button> _tabButtons = new Dictionary<DialogueEditorTabId, Button>();

        private IReadOnlyList<IDialogueEditorTab> _tabs;
        private VisualElement _content;
        private ObjectField _projectField;
        private Button _saveButton;

        [MenuItem("Window/Tool for Dialogue/Dialogue Editor")]
        public static void Open()
        {
            var window = GetWindow<DialogueEditorWindow>();
            window.titleContent = new GUIContent(windowTitle);
            window.minSize = new Vector2(760f, 480f);
            window.Show();
        }

        public void CreateGUI()
        {
            _tabs = CreateTabs();
            rootVisualElement.Clear();

            AddStyleSheet();
            BuildWindow();
        }

        private void BuildWindow()
        {
            rootVisualElement.Clear();

            var root = new VisualElement();
            root.AddToClassList(DialogueEditorStyleClasses.EditorRoot);
            rootVisualElement.Add(root);

            var existingProject = DialogueProjectAssetService.FindFirstProject();
            if (existingProject == null)
            {
                _selectedProject = null;
                BuildFirstRunView(root);
                return;
            }

            if (_selectedProject == null || _selectedProject.yarnProject == null)
            {
                _selectedProject = existingProject;
            }

            BuildHeader(root);
            BuildTabBar(root);

            _content = new VisualElement();
            _content.AddToClassList(DialogueEditorStyleClasses.Content);
            root.Add(_content);

            SelectTab(_selectedTabId);
        }

        private void BuildFirstRunView(VisualElement root)
        {
            var firstRun = new VisualElement();
            firstRun.AddToClassList(DialogueEditorStyleClasses.FirstRun);

            var card = new VisualElement();
            card.AddToClassList(DialogueEditorStyleClasses.FirstRunCard);
            
            var description = new Label("Create a Yarn Spinner project to start writing dialogue.");
            description.AddToClassList(DialogueEditorStyleClasses.FirstRunDescription);
            card.Add(description);

            var createButton = new Button(CreateNewProject)
            {
                text = "Create Yarn Project"
            };
            createButton.AddToClassList(DialogueEditorStyleClasses.FirstRunAction);
            card.Add(createButton);

            firstRun.Add(card);
            root.Add(firstRun);
        }

        private static IReadOnlyList<IDialogueEditorTab> CreateTabs()
        {
            return new IDialogueEditorTab[]
            {
                new ProjectTab(),
                new ConversationsTab(),
                new CharactersTab()
            };
        }

        private void AddStyleSheet()
        {
            var styleSheetPath = AssetDatabase.GUIDToAssetPath(styleSheetGuid);
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(styleSheetPath);
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
                return;
            }

            Debug.LogError($"Unable to load the dialogue editor stylesheet with GUID '{styleSheetGuid}'.", this);
        }

        private void BuildHeader(VisualElement root)
        {
            var header = new VisualElement();
            header.AddToClassList(DialogueEditorStyleClasses.Header);

            var titleGroup = new VisualElement();
            titleGroup.AddToClassList(DialogueEditorStyleClasses.TitleGroup);

            var title = new Label("Tool for Dialogue");
            title.AddToClassList(DialogueEditorStyleClasses.Title);
            titleGroup.Add(title);

            var subtitle = new Label("Visual Yarn Spinner authoring");
            subtitle.AddToClassList(DialogueEditorStyleClasses.Subtitle);
            titleGroup.Add(subtitle);

            header.Add(titleGroup);
            header.Add(CreateProjectControls());
            root.Add(header);
        }

        private VisualElement CreateProjectControls()
        {
            var controls = new VisualElement();
            controls.AddToClassList(DialogueEditorStyleClasses.HeaderActions);

            _projectField = new ObjectField("Project")
            {
                objectType = typeof(DialogueProjectAsset),
                allowSceneObjects = false,
                value = _selectedProject
            };

            _projectField.AddToClassList(DialogueEditorStyleClasses.ProjectField);
            _projectField.RegisterValueChangedCallback(evt =>
                SetSelectedProject(evt.newValue as DialogueProjectAsset));
            controls.Add(_projectField);

            _saveButton = new Button(SaveSelectedProject)
            {
                text = "Save"
            };
            _saveButton.AddToClassList(DialogueEditorStyleClasses.HeaderAction);
            controls.Add(_saveButton);

            UpdateSaveButtonState();
            return controls;
        }

        private void CreateNewProject()
        {
            var project = DialogueProjectAssetService.CreateProject();
            if (project == null)
            {
                return;
            }

            _selectedProject = project;
            BuildWindow();
            ShowNotification(new GUIContent("Yarn project created."));
        }

        private void SaveSelectedProject()
        {
            if (_selectedProject == null)
            {
                return;
            }

            DialogueProjectAssetService.SaveProject(_selectedProject);
            ShowNotification(new GUIContent("Dialogue project saved."));
        }

        private void SetSelectedProject(DialogueProjectAsset project)
        {
            _selectedProject = project;
            UpdateSaveButtonState();
            RenderSelectedTab();
        }

        private void UpdateSaveButtonState()
        {
            _saveButton?.SetEnabled(_selectedProject != null);
        }

        private void BuildTabBar(VisualElement root)
        {
            _tabButtons.Clear();

            var tabBar = new VisualElement();
            tabBar.AddToClassList(DialogueEditorStyleClasses.TabBar);

            foreach (var tab in _tabs)
            {
                var capturedTabId = tab.id;
                var button = new Button(() => SelectTab(capturedTabId))
                {
                    text = tab.label
                };

                button.AddToClassList(DialogueEditorStyleClasses.Tab);
                tabBar.Add(button);
                _tabButtons.Add(tab.id, button);
            }

            root.Add(tabBar);
        }

        private void SelectTab(DialogueEditorTabId tabId)
        {
            var selectedTab = FindTab(tabId) ?? _tabs[0];
            _selectedTabId = selectedTab.id;

            foreach (var pair in _tabButtons)
            {
                pair.Value.EnableInClassList(
                    DialogueEditorStyleClasses.SelectedTab,
                    pair.Key == _selectedTabId);
            }

            RenderSelectedTab();
        }

        private void RenderSelectedTab()
        {
            if (_content == null || _tabs == null)
            {
                return;
            }

            var selectedTab = FindTab(_selectedTabId) ?? _tabs[0];
            var context = new DialogueEditorTabContext(_selectedProject, SelectTab);

            _content.Clear();
            _content.Add(selectedTab.CreateContent(context));
        }

        private IDialogueEditorTab FindTab(DialogueEditorTabId tabId)
        {
            return _tabs.FirstOrDefault(tab => tab.id == tabId);
        }
    }
}
