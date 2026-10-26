using System;
using System.Collections.Generic;
using System.Linq;
using ToolForDialogue._Project.Dialogue.Runtime.Assets;
using ToolForDialogue._Project.Dialogue.Runtime.Types;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToolForDialogue.Editor._Project.Scripts.Editor.Tabs.Conversation
{
    internal sealed partial class ConversationsTab
    {
        private sealed class PortBinding
        {
            public string passageId;
            public int elementIndex;
            public int optionIndex = -1;
            public string destinationId;
            public bool isTerminal;
        }

        private sealed class PassageNode : Node
        {
            private readonly Action<string> _select;
            private readonly Action<string, Vector2> _move;
            private readonly Dictionary<int, TextField> _lineEditors = new Dictionary<int, TextField>();
            private bool _ready;
            private readonly Label _warningBadge;

            public PassageNode(
                DialogueProjectAsset project,
                PassageData passage,
                bool isStart,
                bool hasWarning,
                Action<string> select,
                Action<string, Vector2> move,
                Action<string, string> renamePassage,
                Action<string, int, CharacterAsset> changeSpeaker,
                Action<string, int, string> changeExpression,
                Action<string, int, string> changeLineText,
                Action<string, int, int, string> changeChoiceText,
                Action<string> addLine,
                Action<string> addChoice,
                Action<string> endConversation,
                Action<string, int, int> moveElement,
                Action<string, int> duplicateElement,
                Action<string, int> deleteElement,
                Action<string, int, int, int> moveChoiceOption,
                Action<string, int, int> deleteChoiceOption,
                Action<PortBinding> createTarget,
                Action<string, int> addVariable,
                Func<string, int, VisualElement> createVariableEditor,
                Func<string, int, int, VisualElement> createConditionEditor)
            {
                passageId = passage.id.value;
                _select = select;
                _move = move;
                title = string.Empty;
                AddToClassList(DialogueEditorStyleClasses.ConversationInlineNode);
                capabilities &= ~Capabilities.Deletable;
                titleContainer.AddToClassList(DialogueEditorStyleClasses.ConversationInlineHeader);
                extensionContainer.AddToClassList(DialogueEditorStyleClasses.ConversationInlineContent);

                _warningBadge = new Label("!") { tooltip = "This passage has validation warnings" };
                _warningBadge.AddToClassList(DialogueEditorStyleClasses.ConversationInlineWarningBadge);
                _warningBadge.style.display = hasWarning ? DisplayStyle.Flex : DisplayStyle.None;

                var titleField = new TextField { isDelayed = true };
                titleField.SetValueWithoutNotify(passage.title);
                titleField.tooltip = "Passage title";
                titleField.AddToClassList(DialogueEditorStyleClasses.ConversationInlineTitle);
                titleField.RegisterValueChangedCallback(evt => renamePassage(passageId, evt.newValue));
                titleContainer.Add(titleField);
                if (isStart)
                {
                    var startBadge = new Label("●") { tooltip = "Conversation starts here" };
                    startBadge.AddToClassList(DialogueEditorStyleClasses.ConversationInlineStartBadge);
                    titleContainer.Add(startBadge);
                }
                titleContainer.Add(_warningBadge);

                input = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
                input.portName = string.Empty;
                input.portColor = new Color32(149, 136, 114, 255);
                input.userData = passageId;
                input.AddToClassList(DialogueEditorStyleClasses.ConversationInlineInput);
                titleContainer.Insert(0, input);

                var terminalIndex = passage.elements.Count - 1;
                var terminalElement = terminalIndex >= 0 ? passage.elements[terminalIndex] : null;
                var choiceTerminatesPassage = terminalElement is EndElement
                    && terminalIndex > 0
                    && passage.elements[terminalIndex - 1] is ChoiceElement;
                var endsConversation = terminalElement is EndElement && !choiceTerminatesPassage;
                var passageMenu = CreateActionsMenu("Passage actions");
                AppendAction(passageMenu, "Add choice", () => addChoice(passageId));
                AppendAction(passageMenu, "End conversation", () => endConversation(passageId), !endsConversation);
                titleContainer.Add(passageMenu);
                if (!choiceTerminatesPassage && !(terminalElement is ChoiceElement))
                {
                    var continuationBinding = new PortBinding
                    {
                        passageId = passageId,
                        elementIndex = terminalElement is EndElement || terminalElement is JumpElement
                            ? terminalIndex
                            : passage.elements.Count,
                        destinationId = terminalElement is JumpElement terminalJump
                            ? terminalJump.destinationPassageId.value
                            : string.Empty,
                        isTerminal = true
                    };
                    var continuationPort = CreateOutputPort(continuationBinding);
                    continuationPort.tooltip = "Continue to another passage";
                    continuationPort.AddToClassList(DialogueEditorStyleClasses.ConversationInlineOutput);
                    titleContainer.Add(continuationPort);
                    connectionPorts.Add(continuationPort);
                }

                var characters = project.characters.Where(character => character != null).ToList();
                for (var elementIndex = 0; elementIndex < passage.elements.Count; elementIndex++)
                {
                    var capturedElement = elementIndex;
                    var element = passage.elements[elementIndex];
                    if (element is LineElement line)
                    {
                        var previousIndex = elementIndex - 1;
                        while (previousIndex >= 0 && passage.elements[previousIndex] is SetVariableElement) previousIndex--;
                        var previousLine = previousIndex >= 0 ? passage.elements[previousIndex] as LineElement : null;
                        // An expression change starts a new group so its portrait remains visible.
                        var continuesSpeaker = line.speaker != null && previousLine?.speaker == line.speaker
                            && previousLine.expressionId == line.expressionId;
                        var lineCard = new VisualElement();
                        lineCard.AddToClassList(DialogueEditorStyleClasses.ConversationInlineLine);
                        lineCard.EnableInClassList(DialogueEditorStyleClasses.ConversationInlineLineContinuation, continuesSpeaker);
                        lineCard.RegisterCallback<FocusInEvent>(_ =>
                            lineCard.AddToClassList(DialogueEditorStyleClasses.ConversationInlineLineActive));
                        lineCard.RegisterCallback<FocusOutEvent>(_ =>
                            lineCard.RemoveFromClassList(DialogueEditorStyleClasses.ConversationInlineLineActive));

                        var portrait = GetPortrait(line);
                        var speakerColor = GetSpeakerColor(line.speaker);
                        var avatar = new VisualElement();
                        avatar.AddToClassList(DialogueEditorStyleClasses.ConversationInlineAvatar);
                        avatar.tooltip = line.speaker == null ? "Choose a speaker" : line.speaker.displayName;
                        avatar.style.borderLeftColor = speakerColor;
                        avatar.style.borderRightColor = speakerColor;
                        avatar.style.borderTopColor = speakerColor;
                        avatar.style.borderBottomColor = speakerColor;
                        if (portrait != null)
                        {
                            avatar.style.backgroundImage = new StyleBackground(portrait);
                        }
                        else
                        {
                            var initial = string.IsNullOrWhiteSpace(line.speaker?.displayName)
                                ? "?"
                                : line.speaker.displayName.Substring(0, 1).ToUpperInvariant();
                            var initialLabel = new Label(initial);
                            initialLabel.style.color = speakerColor;
                            avatar.Add(initialLabel);
                        }
                        if (!continuesSpeaker) lineCard.Add(avatar);

                        var lineBody = new VisualElement();
                        lineBody.AddToClassList(DialogueEditorStyleClasses.ConversationInlineBody);
                        var speakerRow = new VisualElement();
                        speakerRow.AddToClassList(DialogueEditorStyleClasses.ConversationInlineSpeakerRow);

                        var speaker = CreateSpeakerPicker(characters, line.speaker,
                            character => changeSpeaker(passageId, capturedElement, character));
                        speaker.AddToClassList(DialogueEditorStyleClasses.ConversationInlineSpeaker);
                        speaker.style.color = speakerColor;
                        speakerRow.Add(speaker);
                        var addLineVariable = new Button(() => addVariable(passageId, capturedElement))
                        {
                            text = "+ Variable",
                            tooltip = "Change a variable after this dialogue line"
                        };
                        addLineVariable.AddToClassList(DialogueEditorStyleClasses.ConversationLineVariableButton);
                        speakerRow.Add(addLineVariable);
                        lineBody.Add(speakerRow);

                        var expressionNames = new List<string> { "Default" };
                        var expressionIds = new List<string> { string.Empty };
                        if (line.speaker != null)
                        {
                            foreach (var expression in line.speaker.expressions.Where(expression => expression != null))
                            {
                                expressionNames.Add(CreateUniqueChoiceName(expressionNames, expression.displayName));
                                expressionIds.Add(expression.id.value);
                            }
                        }
                        var currentExpression = line.expressionId == line.speaker?.defaultExpressionId
                            ? 0
                            : Math.Max(0, expressionIds.IndexOf(line.expressionId.value));
                        var lineActions = CreateActionsMenu("Line actions and portrait expression");
                        for (var expressionIndex = 0; expressionIndex < expressionNames.Count; expressionIndex++)
                        {
                            var capturedExpression = expressionIndex;
                            lineActions.menu.AppendAction("Expression/" + expressionNames[expressionIndex], _ =>
                            {
                                currentExpression = capturedExpression;
                                changeExpression(passageId, capturedElement, expressionIds[capturedExpression]);
                            }, _ => line.speaker == null
                                ? DropdownMenuAction.Status.Disabled
                                : capturedExpression == currentExpression
                                    ? DropdownMenuAction.Status.Checked
                                    : DropdownMenuAction.Status.Normal);
                        }
                        lineActions.menu.AppendSeparator();
                        var nextLineIndex = LineGroupEnd(passage.elements, capturedElement);
                        AppendAction(lineActions, "Move up", () => moveElement(passageId, capturedElement, capturedElement - 1),
                            previousLine != null);
                        AppendAction(lineActions, "Move down", () => moveElement(passageId, capturedElement, capturedElement + 1),
                            nextLineIndex < passage.elements.Count && passage.elements[nextLineIndex] is LineElement);
                        AppendAction(lineActions, "Duplicate line", () => duplicateElement(passageId, capturedElement));
                        lineActions.menu.AppendSeparator();
                        AppendAction(lineActions, "Delete line", () => deleteElement(passageId, capturedElement));
                        var text = new TextField { multiline = true };
                        text.SetValueWithoutNotify(line.text);
                        text.AddToClassList(DialogueEditorStyleClasses.ConversationInlineText);
                        text.textEdition.placeholder = "Write dialogue…";
                        text.style.height = GetDialogueFieldHeight(line.text);
                        text.tooltip = "Dialogue text";
                        text.RegisterValueChangedCallback(evt =>
                        {
                            text.style.height = GetDialogueFieldHeight(evt.newValue);
                            changeLineText(passageId, capturedElement, evt.newValue);
                        });
                        _lineEditors[capturedElement] = text;
                        var textRow = new VisualElement();
                        textRow.AddToClassList(DialogueEditorStyleClasses.ConversationInlineTextRow);
                        textRow.Add(text);
                        textRow.Add(lineActions);
                        lineBody.Add(textRow);
                        if (capturedElement + 1 < nextLineIndex)
                        {
                            var assignments = new VisualElement();
                            assignments.AddToClassList(DialogueEditorStyleClasses.ConversationLineVariables);
                            for (var assignmentIndex = capturedElement + 1; assignmentIndex < nextLineIndex; assignmentIndex++)
                                assignments.Add(createVariableEditor(passageId, assignmentIndex));
                            lineBody.Add(assignments);
                        }
                        lineCard.Add(lineBody);
                        extensionContainer.Add(lineCard);
                    }
                    else if (element is SetVariableElement)
                    {
                        if (AssignmentOwner(passage.elements, capturedElement) < 0)
                            extensionContainer.Add(createVariableEditor(passageId, capturedElement));
                    }
                    else if (element is ChoiceElement choice)
                    {
                        var choiceGroup = new VisualElement();
                        choiceGroup.AddToClassList(DialogueEditorStyleClasses.ConversationInlineChoiceGroup);
                        for (var optionIndex = 0; optionIndex < choice.options.Count; optionIndex++)
                        {
                            var capturedOption = optionIndex;
                            var option = choice.options[optionIndex];
                            if (option == null) continue;
                            var row = new VisualElement();
                            row.AddToClassList(DialogueEditorStyleClasses.ConversationInlineChoice);
                            row.EnableInClassList(
                                DialogueEditorStyleClasses.ConversationInlineChoiceLast,
                                optionIndex == choice.options.Count - 1);
                            var choiceIcon = new Label("›");
                            choiceIcon.AddToClassList(DialogueEditorStyleClasses.ConversationInlineChoiceIcon);
                            row.Add(choiceIcon);
                            var choiceText = new TextField();
                            choiceText.SetValueWithoutNotify(option.text);
                            choiceText.RegisterValueChangedCallback(evt =>
                                changeChoiceText(passageId, capturedElement, capturedOption, evt.newValue));
                            row.Add(choiceText);
                            var binding = new PortBinding
                            {
                                passageId = passageId,
                                elementIndex = capturedElement,
                                optionIndex = capturedOption,
                                destinationId = option.destinationPassageId.value
                            };
                            var createButton = new Button(() => createTarget(binding))
                                { text = "+", tooltip = "Create and connect a new passage" };
                            row.Add(createButton);
                            var choiceActions = CreateActionsMenu("Choice actions");
                            AppendAction(choiceActions, "Move up", () =>
                                moveChoiceOption(passageId, capturedElement, capturedOption, capturedOption - 1), capturedOption > 0);
                            AppendAction(choiceActions, "Move down", () =>
                                moveChoiceOption(passageId, capturedElement, capturedOption, capturedOption + 1),
                                capturedOption < choice.options.Count - 1);
                            AppendAction(choiceActions, "Delete choice", () =>
                                deleteChoiceOption(passageId, capturedElement, capturedOption));
                            row.Add(choiceActions);
                            var port = CreateOutputPort(binding);
                            row.Add(port);
                            connectionPorts.Add(port);
                            choiceGroup.Add(row);
                            choiceGroup.Add(createConditionEditor(passageId, capturedElement, capturedOption));
                        }
                        extensionContainer.Add(choiceGroup);
                    }
                }

                var addRow = new VisualElement();
                addRow.AddToClassList(DialogueEditorStyleClasses.ConversationInlineActions);
                if (endsConversation)
                {
                    var ending = new Label("End of conversation");
                    ending.AddToClassList(DialogueEditorStyleClasses.ConversationInlineEndAction);
                    addRow.Add(ending);
                }
                var actionSpacer = new VisualElement();
                actionSpacer.style.flexGrow = 1f;
                addRow.Add(actionSpacer);
                addRow.Add(new Button(() => addLine(passageId)) { text = "+ Line" });
                addRow.Add(new Button(() => addChoice(passageId)) { text = "+ Choice" });
                extensionContainer.Add(addRow);

                RefreshExpandedState();
                RefreshPorts();
                SetPosition(new Rect(passage.graphPosition, new Vector2(420f, 180f)));
                _ready = true;
            }

            public string passageId { get; }

            private static ToolbarMenu CreateActionsMenu(string tooltip)
            {
                var menu = new ToolbarMenu { text = "···", tooltip = tooltip };
                menu.AddToClassList(DialogueEditorStyleClasses.ConversationInlineMenu);
                return menu;
            }

            private static ToolbarMenu CreateSpeakerPicker(
                IReadOnlyList<CharacterAsset> characters, CharacterAsset selected, Action<CharacterAsset> changeSpeaker)
            {
                var picker = new ToolbarMenu
                {
                    text = selected == null ? "Choose a speaker" : selected.displayName,
                    tooltip = characters.Count == 0
                        ? "Create a character in the Characters tab first"
                        : $"{selected?.displayName ?? "Choose a speaker"}: change the speaker for this line"
                };
                picker.SetEnabled(characters.Count > 0);
                var names = new List<string>();
                foreach (var character in characters)
                {
                    var captured = character;
                    var name = CreateUniqueChoiceName(names, character.displayName);
                    names.Add(name);
                    picker.menu.AppendAction(name, _ => changeSpeaker(captured), _ => captured == selected
                        ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
                }
                return picker;
            }

            private static void AppendAction(ToolbarMenu menu, string label, Action action, bool enabled = true)
            {
                menu.menu.AppendAction(label, _ => action(), _ => enabled
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
            }
            public void SetWarnings(IReadOnlyList<string> warnings)
            {
                _warningBadge.tooltip = string.Join("\n", warnings);
                _warningBadge.style.display = warnings.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            public Port input { get; }
            public List<Port> connectionPorts { get; } = new List<Port>();

            public void FocusLine(int elementIndex)
            {
                if (!_lineEditors.TryGetValue(elementIndex, out var editor)) return;
                editor.schedule.Execute(() => editor.Focus());
            }

            private static Port CreateOutputPort(PortBinding binding)
            {
                var port = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                port.portName = string.Empty;
                port.portColor = new Color32(149, 136, 114, 255);
                port.userData = binding;
                return port;
            }

            private static Sprite GetPortrait(LineElement line)
            {
                if (line.speaker == null) return null;
                var selected = line.speaker.expressions.FirstOrDefault(expression =>
                    expression != null && expression.id == line.expressionId);
                if (selected?.portrait != null) return selected.portrait;
                var fallback = line.speaker.expressions.FirstOrDefault(expression =>
                    expression != null && expression.id == line.speaker.defaultExpressionId);
                return fallback?.portrait;
            }

            private static Color GetSpeakerColor(CharacterAsset speaker)
            {
                if (speaker == null) return new Color32(177, 168, 153, 255);

                // Use the persistent asset identity so colours survive renaming and reordering.
                uint hash = 2166136261;
                foreach (var character in speaker.id.value)
                {
                    hash = unchecked((hash ^ character) * 16777619);
                }

                switch (hash % 5)
                {
                    case 0: return new Color32(216, 177, 121, 255);
                    case 1: return new Color32(148, 190, 170, 255);
                    case 2: return new Color32(185, 166, 205, 255);
                    case 3: return new Color32(216, 158, 139, 255);
                    default: return new Color32(148, 184, 206, 255);
                }
            }

            private static float GetDialogueFieldHeight(string value)
            {
                var source = value ?? string.Empty;
                var explicitLines = source.Count(character => character == '\n') + 1;
                var wrappedLines = Math.Max(1, (int)Math.Ceiling(source.Length / 44f));
                return 26f + Math.Min(5, Math.Max(explicitLines, wrappedLines)) * 18f;
            }

            public override void OnSelected()
            {
                base.OnSelected();
                _select?.Invoke(passageId);
            }

            public override void SetPosition(Rect newPos)
            {
                base.SetPosition(newPos);
                if (_ready) _move?.Invoke(passageId, newPos.position);
            }
        }

        private sealed class ConversationGraph : GraphView
        {
            public void RefreshWarnings(ConversationAsset conversation)
            {
                foreach (var passage in conversation.passages.Where(passage => passage != null))
                {
                    if (_nodes.TryGetValue(passage.id.value, out var node))
                        node.SetWarnings(GetPassageWarnings(conversation, passage));
                }
            }

            private readonly Dictionary<string, PassageNode> _nodes = new Dictionary<string, PassageNode>();
            private readonly Action<string> _deletePassage;
            private bool _spaceHeld;
            private bool _spacePanning;
            private int _panPointerId = -1;
            private Vector3 _panStartPointer;
            private Vector3 _panStartPosition;
            private Vector3 _panScale = Vector3.one;

            public ConversationGraph(
                DialogueProjectAsset project,
                ConversationAsset conversation,
                Action<string> selectPassage,
                Action<string> deletePassage,
                Action<string, Vector2> movePassage,
                Action<string, string> renamePassage,
                Action<string, int, CharacterAsset> changeSpeaker,
                Action<string, int, string> changeExpression,
                Action<string, int, string> changeLineText,
                Action<string, int, int, string> changeChoiceText,
                Action<string> addLine,
                Action<string> addChoice,
                Action<string> endConversation,
                Action<string, int, int> moveElement,
                Action<string, int> duplicateElement,
                Action<string, int> deleteElement,
                Action<string, int, int, int> moveChoiceOption,
                Action<string, int, int> deleteChoiceOption,
                Action<PortBinding> createTarget,
                Action<PortBinding, string> connect,
                Action<PortBinding> disconnect,
                Action<string, int> addVariable,
                Func<string, int, VisualElement> createVariableEditor,
                Func<string, int, int, VisualElement> createConditionEditor)
            {
                _deletePassage = deletePassage;
                focusable = true;
                this.AddManipulator(new ContentZoomer());
                this.AddManipulator(new ContentDragger());
                this.AddManipulator(new SelectionDragger());
                this.AddManipulator(new RectangleSelector());
                RegisterCallback<KeyDownEvent>(OnGraphKeyDown, TrickleDown.TrickleDown);
                RegisterCallback<KeyUpEvent>(OnGraphKeyUp);
                RegisterCallback<PointerDownEvent>(OnGraphPointerDown, TrickleDown.TrickleDown);
                RegisterCallback<PointerMoveEvent>(OnGraphPointerMove, TrickleDown.TrickleDown);
                RegisterCallback<PointerUpEvent>(OnGraphPointerUp, TrickleDown.TrickleDown);
                RegisterCallback<PointerCaptureOutEvent>(_ => StopSpacePan());
                RegisterCallback<DetachFromPanelEvent>(_ =>
                {
                    _spaceHeld = false;
                    StopSpacePan();
                });
                var grid = new GridBackground();
                grid.AddToClassList(DialogueEditorStyleClasses.ConversationGraphGrid);
                Insert(0, grid);
                grid.StretchToParentSize();

                foreach (var passage in conversation.passages.Where(passage => passage != null))
                {
                    var node = new PassageNode(
                        project, passage, passage.id == conversation.startPassageId,
                        GetPassageWarnings(conversation, passage).Count > 0,
                        selectPassage, movePassage, renamePassage, changeSpeaker, changeExpression, changeLineText,
                        changeChoiceText, addLine, addChoice, endConversation, moveElement, duplicateElement, deleteElement,
                        moveChoiceOption, deleteChoiceOption, createTarget, addVariable, createVariableEditor, createConditionEditor);
                    _nodes.Add(passage.id.value, node);
                    AddElement(node);
                }

                foreach (var node in _nodes.Values)
                {
                    foreach (var port in node.connectionPorts)
                    {
                        if (port.userData is PortBinding binding
                            && _nodes.TryGetValue(binding.destinationId ?? string.Empty, out var destination))
                        {
                            AddElement(port.ConnectTo(destination.input));
                        }
                    }
                }

                graphViewChanged = change =>
                {
                    if (change.elementsToRemove != null)
                    {
                        foreach (var edge in change.elementsToRemove.OfType<Edge>())
                        {
                            if (edge.output?.userData is PortBinding binding)
                            {
                                disconnect(binding);
                                binding.destinationId = string.Empty;
                            }
                        }
                    }
                    if (change.edgesToCreate != null)
                    {
                        foreach (var edge in change.edgesToCreate)
                        {
                            if (edge.output?.userData is PortBinding binding
                                && edge.input?.userData is string destinationId)
                            {
                                connect(binding, destinationId);
                                binding.destinationId = destinationId;
                            }
                        }
                    }
                    return change;
                };
            }

            private void OnGraphKeyDown(KeyDownEvent evt)
            {
                if (IsTextInput(evt.target)) return;
                if (evt.keyCode == KeyCode.Delete && !evt.altKey && !evt.ctrlKey && !evt.commandKey && !evt.shiftKey)
                {
                    var selectedPassage = selection.OfType<PassageNode>().LastOrDefault();
                    if (selectedPassage == null) return;
                    evt.StopImmediatePropagation();
                    _deletePassage?.Invoke(selectedPassage.passageId);
                    return;
                }
                if (evt.keyCode != KeyCode.Space) return;
                _spaceHeld = true;
                evt.StopImmediatePropagation();
            }

            private void OnGraphKeyUp(KeyUpEvent evt)
            {
                if (evt.keyCode != KeyCode.Space || !_spaceHeld) return;
                _spaceHeld = false;
                evt.StopImmediatePropagation();
            }

            private void OnGraphPointerDown(PointerDownEvent evt)
            {
                if (!_spaceHeld)
                {
                    if (evt.button == 0
                        && (ReferenceEquals(evt.target, this)
                            || ReferenceEquals(evt.target, contentViewContainer)
                            || evt.target is GridBackground
                            || (!IsTextInput(evt.target) && evt.target is VisualElement target
                                && (target is PassageNode || target.GetFirstAncestorOfType<PassageNode>() != null))))
                    {
                        Focus();
                    }
                    return;
                }
                if (!_spaceHeld || evt.button != 0 || IsTextInput(evt.target)) return;
                _spacePanning = true;
                _panPointerId = evt.pointerId;
                _panStartPointer = evt.position;
                var resolvedTransform = contentViewContainer.resolvedStyle;
                _panStartPosition = new Vector3(
                    resolvedTransform.translate.x,
                    resolvedTransform.translate.y,
                    0f);
                _panScale = resolvedTransform.scale.value;
                this.CapturePointer(evt.pointerId);
                evt.StopImmediatePropagation();
            }

            private void OnGraphPointerMove(PointerMoveEvent evt)
            {
                if (!_spacePanning || evt.pointerId != _panPointerId) return;
                var delta = evt.position - _panStartPointer;
                UpdateViewTransform(_panStartPosition + delta, _panScale);
                evt.StopImmediatePropagation();
            }

            private void OnGraphPointerUp(PointerUpEvent evt)
            {
                if (!_spacePanning || evt.pointerId != _panPointerId || evt.button != 0) return;
                if (this.HasPointerCapture(evt.pointerId)) this.ReleasePointer(evt.pointerId);
                StopSpacePan();
                evt.StopImmediatePropagation();
            }

            private void StopSpacePan()
            {
                _spacePanning = false;
                _panPointerId = -1;
            }

            private static bool IsTextInput(IEventHandler eventTarget)
            {
                var element = eventTarget as VisualElement;
                return element is TextField || element?.GetFirstAncestorOfType<TextField>() != null;
            }

            public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
            {
                return ports.ToList().Where(port =>
                    port != startPort
                    && port.direction != startPort.direction
                    && port.node != startPort.node).ToList();
            }

            public void FrameEverything() => FrameAll();

            public void SelectPassage(string passageId)
            {
                ClearSelection();
                if (_nodes.TryGetValue(passageId ?? string.Empty, out var node))
                {
                    AddToSelection(node);
                    if (node.layout.width > 0f && node.layout.height > 0f && contentRect.width > 0f)
                    {
                        CenterPassage(node);
                    }
                    else
                    {
                        EventCallback<GeometryChangedEvent> onLayout = null;
                        onLayout = _ =>
                        {
                            if (node.layout.width <= 0f || contentRect.width <= 0f) return;
                            node.UnregisterCallback(onLayout);
                            CenterPassage(node);
                        };
                        node.RegisterCallback(onLayout);
                    }
                }
            }

            private void CenterPassage(PassageNode node)
            {
                var bounds = node.GetPosition();
                var zoom = Mathf.Clamp((contentRect.width - 96f) / bounds.width, 0.85f, 1.1f);
                var offset = new Vector3(
                    (contentRect.width - bounds.width * zoom) * 0.5f,
                    Mathf.Max(32f, (contentRect.height - bounds.height * zoom) * 0.5f), 0f);
                UpdateViewTransform(offset - new Vector3(bounds.x, bounds.y, 0f) * zoom, Vector3.one * zoom);
            }

            public void FocusLine(string passageId, int elementIndex)
            {
                if (_nodes.TryGetValue(passageId ?? string.Empty, out var node))
                {
                    node.FocusLine(elementIndex);
                }
            }
        }

    }
}
