using System;
using System.Collections.Generic;
using System.Linq;
using ToolForDialogue._Project.Dialogue.Runtime.Assets;
using ToolForDialogue.Editor._Project.Scripts.Editor.Services;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToolForDialogue.Editor._Project.Scripts.Editor.Tabs.Conversation
{
    /// <summary>
    /// Owns conversation creation, selection, and passage-graph authoring controls.
    /// </summary>
    internal sealed partial class ConversationsTab : DialogueEditorTabBase
    {
        private DialogueProjectAsset _project;
        private ConversationAsset _selectedConversation;
        private string _selectedPassageId;
        private string _searchQuery = string.Empty;
        private readonly HashSet<string> _expandedConversations = new HashSet<string>();
        private VisualElement _conversationList;
        private VisualElement _graphHost;
        private VisualElement _inspector;
        private bool _inspectorVisible;
        private Button _inspectorToggle;
        private ConversationGraph _graph;
        private ConversationAsset _graphConversation;
        private bool _terminalRefreshScheduled;
        private HelpBox _passageWarning;
        private readonly Dictionary<ConversationAsset, Label> _conversationWarningBadges =
            new Dictionary<ConversationAsset, Label>();

        private void MarkConversationChanged()
        {
            if (_selectedConversation == null) return;
            EditorUtility.SetDirty(_selectedConversation);
            ConversationYarnGenerator.Schedule(_selectedConversation);
            RefreshValidationWarnings();
        }

        public override DialogueEditorTabId id => DialogueEditorTabId.Conversations;
        public override string label => "Conversations";
        protected override bool isWritingWorkspace => true;

        protected override void BuildContent(VisualElement container, DialogueEditorTabContext context)
        {
            _project = context.selectedProject;
            if (_selectedConversation == null || !_project.conversations.Contains(_selectedConversation))
            {
                _selectedConversation = _project.conversations.FirstOrDefault(conversation => conversation != null);
            }
            EnsureSelectedPassage();
            if (_selectedConversation != null)
            {
                _expandedConversations.Add(_selectedConversation.id.value);
            }
            ConversationYarnGenerator.ScheduleAll(_project);

            var workspace = new VisualElement();
            workspace.AddToClassList(DialogueEditorStyleClasses.ConversationWorkspace);
            workspace.Add(CreateConversationSidebar());

            _graphHost = new VisualElement();
            _graphHost.AddToClassList(DialogueEditorStyleClasses.ConversationGraphHost);
            workspace.Add(_graphHost);

            _inspector = new ScrollView(ScrollViewMode.Vertical);
            _inspector.AddToClassList(DialogueEditorStyleClasses.ConversationInspector);
            _inspector.style.display = _inspectorVisible ? DisplayStyle.Flex : DisplayStyle.None;
            workspace.Add(_inspector);
            container.Add(workspace);

            RebuildConversationList();
            RebuildGraph();
            RebuildInspector();
        }

        private void SetInspectorVisible(bool visible)
        {
            _inspectorVisible = visible;
            _inspector.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_inspectorToggle != null)
            {
                _inspectorToggle.text = visible ? "Hide Details" : "Details";
            }
        }

        private VisualElement CreateConversationSidebar()
        {
            var sidebar = new VisualElement();
            sidebar.AddToClassList(DialogueEditorStyleClasses.ConversationSidebar);

            var header = new VisualElement();
            header.AddToClassList(DialogueEditorStyleClasses.ConversationSidebarHeader);

            var title = new Label("Conversations");
            title.AddToClassList(DialogueEditorStyleClasses.CharacterSidebarTitle);

            header.Add(title);

            header.Add(new Button(CreateConversation) { text = "+", tooltip = "Create a conversation" });
            sidebar.Add(header);

            var search = new ToolbarSearchField();

            search.SetValueWithoutNotify(_searchQuery);
            search.RegisterValueChangedCallback(evt =>
            {
                _searchQuery = evt.newValue ?? string.Empty;
                RebuildConversationList();
            });
            search.AddToClassList(DialogueEditorStyleClasses.ConversationSearch);

            sidebar.Add(search);

            _conversationList = new ScrollView(ScrollViewMode.Vertical);
            _conversationList.AddToClassList(DialogueEditorStyleClasses.ConversationList);

            sidebar.Add(_conversationList);
            return sidebar;
        }

        private void RebuildConversationList()
        {
            _conversationList.Clear();
            _conversationWarningBadges.Clear();
            var conversations = _project.conversations
                .Where(conversation => conversation != null)
                .Where(conversation => string.IsNullOrWhiteSpace(_searchQuery)
                    || conversation.displayName.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0
                    || conversation.passages.Any(passage => passage != null
                        && passage.title.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();

            if (conversations.Count == 0)
            {
                var empty = new Label(string.IsNullOrWhiteSpace(_searchQuery)
                    ? "Who speaks first?\nUse + to start a conversation."
                    : "No conversations match this search.");
                empty.AddToClassList(DialogueEditorStyleClasses.CharacterListEmpty);
                _conversationList.Add(empty);
                return;
            }

            foreach (var conversation in conversations)
            {
                var captured = conversation;
                var group = new VisualElement();
                group.AddToClassList(DialogueEditorStyleClasses.ConversationGroup);
                var conversationKey = conversation.id.value;
                var expanded = _expandedConversations.Contains(conversationKey)
                    || !string.IsNullOrWhiteSpace(_searchQuery);

                var button = new Button(() =>
                {
                    if (_expandedConversations.Contains(conversationKey))
                    {
                        _expandedConversations.Remove(conversationKey);
                    }
                    else
                    {
                        _expandedConversations.Add(conversationKey);
                    }
                    if (_selectedConversation != captured)
                    {
                        SelectConversation(captured);
                    }
                    else
                    {
                        RebuildConversationList();
                    }
                });
                button.AddToClassList(DialogueEditorStyleClasses.ConversationGroupHeader);
                button.EnableInClassList(
                    DialogueEditorStyleClasses.SelectedConversation,
                    conversation == _selectedConversation);
                var chevron = new Label(expanded ? "▾" : "▸");
                chevron.AddToClassList(DialogueEditorStyleClasses.ConversationGroupChevron);
                button.Add(chevron);
                var name = new Label(string.IsNullOrWhiteSpace(conversation.displayName)
                    ? "Unnamed Conversation"
                    : conversation.displayName);
                name.AddToClassList(DialogueEditorStyleClasses.CharacterListName);
                button.Add(name);
                var count = new Label(conversation.passages.Count.ToString());
                count.AddToClassList(DialogueEditorStyleClasses.ConversationGroupCount);
                button.Add(count);
                var warnings = GetConversationWarnings(conversation);
                var warning = new Label("!")
                {
                    tooltip = string.Join("\n", warnings)
                };
                warning.style.display = warnings.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                warning.AddToClassList(DialogueEditorStyleClasses.CharacterWarningBadge);
                button.Add(warning);
                _conversationWarningBadges[conversation] = warning;
                group.Add(button);

                if (expanded)
                {
                    var passageList = new VisualElement();
                    passageList.AddToClassList(DialogueEditorStyleClasses.ConversationPassageList);
                    var conversationNameMatches = !string.IsNullOrWhiteSpace(_searchQuery)
                        && conversation.displayName.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0;
                    foreach (var passage in conversation.passages.Where(passage => passage != null))
                    {
                        if (!string.IsNullOrWhiteSpace(_searchQuery)
                            && !conversationNameMatches
                            && passage.title.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            continue;
                        }
                        var capturedPassageId = passage.id.value;
                        var passageButton = new Button(() => SelectPassageFromSidebar(captured, capturedPassageId));
                        passageButton.AddToClassList(DialogueEditorStyleClasses.ConversationPassageItem);
                        passageButton.EnableInClassList(
                            DialogueEditorStyleClasses.SelectedPassage,
                            conversation == _selectedConversation && capturedPassageId == _selectedPassageId);
                        var icon = new Label(passage.id == conversation.startPassageId ? "★" : "□");
                        icon.AddToClassList(DialogueEditorStyleClasses.ConversationPassageIcon);
                        passageButton.Add(icon);
                        passageButton.Add(new Label(string.IsNullOrWhiteSpace(passage.title)
                            ? "Untitled Passage"
                            : passage.title));
                        passageList.Add(passageButton);
                    }
                    group.Add(passageList);
                }

                _conversationList.Add(group);
            }
        }

        private void SelectPassageFromSidebar(ConversationAsset conversation, string passageId)
        {
            if (_selectedConversation != conversation)
            {
                _selectedConversation = conversation;
                _expandedConversations.Add(conversation.id.value);
                _selectedPassageId = passageId;
                RebuildConversationList();
                RebuildGraph();
                RebuildInspector();
            }
            else
            {
                SelectPassage(passageId);
            }
            _graph?.SelectPassage(passageId);
        }

        private void RebuildGraph()
        {
            var preserveView = _graph != null && _graphConversation == _selectedConversation;
            var previousViewPosition = Vector3.zero;
            var previousViewScale = Vector3.one;
            if (preserveView)
            {
                var resolvedTransform = _graph.contentViewContainer.resolvedStyle;
                previousViewPosition = new Vector3(
                    resolvedTransform.translate.x,
                    resolvedTransform.translate.y,
                    0f);
                previousViewScale = resolvedTransform.scale.value;
            }
            _graphHost.Clear();
            if (_selectedConversation == null)
            {
                _graph = null;
                _graphHost.Add(new HelpBox("Create or select a conversation.", HelpBoxMessageType.Info));
                return;
            }

            var toolbar = new VisualElement();
            toolbar.AddToClassList(DialogueEditorStyleClasses.ConversationGraphToolbar);

            var title = new TextField { isDelayed = true };
            title.SetValueWithoutNotify(_selectedConversation.displayName);
            title.tooltip = "Conversation name, asset filename, and Yarn node title prefix";
            title.AddToClassList(DialogueEditorStyleClasses.ConversationTitleField);
            title.RegisterValueChangedCallback(evt =>
            {
                if (!ConversationYarnGenerator.TryRename(_selectedConversation, evt.newValue, out var error))
                {
                    title.SetValueWithoutNotify(_selectedConversation.displayName);
                    EditorUtility.DisplayDialog("Cannot Rename Conversation", error, "OK");
                    return;
                }
                title.SetValueWithoutNotify(_selectedConversation.displayName);
                RebuildConversationList();
            });
            toolbar.Add(title);

            var actions = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row
                }
            };

            actions.Add(new Button(AddPassage) { text = "+ Passage" });
            actions.Add(new Button(() => _graph?.FrameEverything()) { text = "Frame All" });
            actions.Add(new Button(() => _graph?.SelectPassage(_selectedConversation.startPassageId.value)) { text = "Start" });
            _inspectorToggle = new Button(() => SetInspectorVisible(!_inspectorVisible))
            {
                text = _inspectorVisible ? "Hide Details" : "Details",
                tooltip = "Show or hide details for the selected passage"
            };
            actions.Add(_inspectorToggle);
            actions.Add(new Button(ShowConversationMenu) { text = "•••", tooltip = "Conversation actions" });
            toolbar.Add(actions);

            _graphHost.Add(toolbar);

            _graph = new ConversationGraph(
                _project,
                _selectedConversation,
                SelectPassage,
                DeletePassage,
                MovePassage,
                RenameInlinePassage,
                ChangeInlineSpeaker,
                ChangeInlineExpression,
                ChangeInlineLineText,
                ChangeInlineChoiceText,
                AddInlineLine,
                AddInlineChoice,
                EndInlineConversation,
                MoveInlineElement,
                DuplicateInlineElement,
                DeleteInlineElement,
                MoveInlineChoiceOption,
                DeleteInlineChoiceOption,
                CreateAndConnectTarget,
                ConnectPort,
                DisconnectPort,
                AddInlineVariable,
                CreateVariableEditor,
                CreateChoiceConditionEditor);

            _graph.AddToClassList(DialogueEditorStyleClasses.ConversationGraph);
            _graphHost.Add(_graph);
            _graphConversation = _selectedConversation;

            if (preserveView)
            {
                _graph.UpdateViewTransform(previousViewPosition, previousViewScale);
            }
            else
            {
                _graph.SelectPassage(_selectedPassageId);
            }
        }
    }
}
