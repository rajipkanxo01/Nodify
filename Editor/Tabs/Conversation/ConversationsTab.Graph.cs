using System;
using System.Collections.Generic;
using System.Linq;
using ToolForDialogue._Project.Dialogue.Runtime.Assets;
using ToolForDialogue._Project.Dialogue.Runtime.Types;
using UnityEditor.Experimental.GraphView;
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
                Action<PortBinding> createTarget)
            {
                passageId = passage.id.value;
                _select = select;
                _move = move;
                title = string.Empty;
                AddToClassList(DialogueEditorStyleClasses.ConversationInlineNode);
                capabilities &= ~Capabilities.Deletable;
                titleContainer.AddToClassList(DialogueEditorStyleClasses.ConversationInlineHeader);
                extensionContainer.AddToClassList(DialogueEditorStyleClasses.ConversationInlineContent);

                if (isStart)
                {
                    var startBadge = new Label("★ START");
                    startBadge.AddToClassList(DialogueEditorStyleClasses.ConversationInlineStartBadge);
                    titleContainer.Add(startBadge);
                }
                _warningBadge = new Label("!") { tooltip = "This passage has validation warnings" };
                _warningBadge.AddToClassList(DialogueEditorStyleClasses.ConversationInlineWarningBadge);
                _warningBadge.style.display = hasWarning ? DisplayStyle.Flex : DisplayStyle.None;
                titleContainer.Add(_warningBadge);

                var titleField = new TextField { isDelayed = true };
                titleField.SetValueWithoutNotify(passage.title);
                titleField.tooltip = "Passage title";
                titleField.AddToClassList(DialogueEditorStyleClasses.ConversationInlineTitle);
                titleField.RegisterValueChangedCallback(evt => renamePassage(passageId, evt.newValue));
                titleContainer.Add(titleField);

                input = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
                input.portName = string.Empty;
                input.userData = passageId;
                input.AddToClassList(DialogueEditorStyleClasses.ConversationInlineInput);
                titleContainer.Insert(0, input);

                var terminalIndex = passage.elements.Count - 1;
                var terminalElement = terminalIndex >= 0 ? passage.elements[terminalIndex] : null;
                var choiceTerminatesPassage = terminalElement is EndElement
                    && terminalIndex > 0
                    && passage.elements[terminalIndex - 1] is ChoiceElement;
                var endsConversation = terminalElement is EndElement && !choiceTerminatesPassage;
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
                        var lineCard = new VisualElement();
                        lineCard.AddToClassList(DialogueEditorStyleClasses.ConversationInlineLine);
                        lineCard.RegisterCallback<FocusInEvent>(_ =>
                            lineCard.AddToClassList(DialogueEditorStyleClasses.ConversationInlineLineActive));
                        lineCard.RegisterCallback<FocusOutEvent>(_ =>
                            lineCard.RemoveFromClassList(DialogueEditorStyleClasses.ConversationInlineLineActive));

                        var portrait = GetPortrait(line);
                        var avatar = new VisualElement();
                        avatar.AddToClassList(DialogueEditorStyleClasses.ConversationInlineAvatar);
                        if (portrait != null)
                        {
                            avatar.style.backgroundImage = new StyleBackground(portrait);
                        }
                        else
                        {
                            var initial = string.IsNullOrWhiteSpace(line.speaker?.displayName)
                                ? "?"
                                : line.speaker.displayName.Substring(0, 1).ToUpperInvariant();
                            avatar.Add(new Label(initial));
                        }
                        lineCard.Add(avatar);

                        var lineBody = new VisualElement();
                        lineBody.AddToClassList(DialogueEditorStyleClasses.ConversationInlineBody);
                        var speakerRow = new VisualElement();
                        speakerRow.AddToClassList(DialogueEditorStyleClasses.ConversationInlineSpeakerRow);

                        var speakerNames = new List<string> { "Select speaker" };
                        foreach (var character in characters)
                        {
                            speakerNames.Add(CreateUniqueChoiceName(speakerNames, character.displayName));
                        }
                        var speakerIndex = line.speaker == null
                            ? 0
                            : Math.Max(0, characters.IndexOf(line.speaker) + 1);
                        var speaker = new PopupField<string>(speakerNames, speakerIndex);
                        speaker.tooltip = line.speaker == null ? "Speaker is required" : "Speaker";
                        speaker.AddToClassList(DialogueEditorStyleClasses.ConversationInlineSpeaker);
                        speaker.SetEnabled(characters.Count > 0);
                        speaker.RegisterValueChangedCallback(evt =>
                        {
                            var index = speakerNames.IndexOf(evt.newValue) - 1;
                            if (index >= 0 && index < characters.Count)
                            {
                                changeSpeaker(passageId, capturedElement, characters[index]);
                            }
                        });
                        speakerRow.Add(speaker);

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
                        var expressionPopup = new PopupField<string>(expressionNames, currentExpression);
                        expressionPopup.tooltip = "Portrait expression (optional)";
                        expressionPopup.AddToClassList(DialogueEditorStyleClasses.ConversationInlineExpression);
                        expressionPopup.SetEnabled(line.speaker != null && expressionNames.Count > 1);
                        expressionPopup.style.display = DisplayStyle.None;
                        var expressionToggle = new Button
                        {
                            text = currentExpression == 0 ? "☺" : expressionNames[currentExpression],
                            tooltip = "Choose an optional portrait expression"
                        };
                        expressionToggle.AddToClassList(DialogueEditorStyleClasses.ConversationInlineExpressionToggle);
                        expressionToggle.SetEnabled(line.speaker != null && expressionNames.Count > 1);
                        expressionToggle.clicked += () =>
                        {
                            expressionPopup.style.display = expressionPopup.style.display == DisplayStyle.None
                                ? DisplayStyle.Flex
                                : DisplayStyle.None;
                        };
                        expressionPopup.RegisterValueChangedCallback(evt =>
                        {
                            var index = Math.Max(0, expressionNames.IndexOf(evt.newValue));
                            expressionToggle.text = index == 0 ? "☺" : expressionNames[index];
                            expressionPopup.style.display = DisplayStyle.None;
                            changeExpression(passageId, capturedElement, expressionIds[index]);
                        });
                        var expressionGroup = new VisualElement();
                        expressionGroup.AddToClassList(DialogueEditorStyleClasses.ConversationInlineExpressionGroup);
                        expressionGroup.Add(expressionToggle);
                        expressionGroup.Add(expressionPopup);
                        speakerRow.Add(expressionGroup);

                        var lineActions = new VisualElement();
                        lineActions.AddToClassList(DialogueEditorStyleClasses.ConversationInlineLineActions);
                        var moveUp = new Button(() => moveElement(passageId, capturedElement, capturedElement - 1))
                            { text = "↑", tooltip = "Move line up" };
                        moveUp.SetEnabled(capturedElement > 0
                            && passage.elements[capturedElement - 1] is LineElement);
                        lineActions.Add(moveUp);
                        var moveDown = new Button(() => moveElement(passageId, capturedElement, capturedElement + 1))
                            { text = "↓", tooltip = "Move line down" };
                        moveDown.SetEnabled(capturedElement + 1 < passage.elements.Count
                            && passage.elements[capturedElement + 1] is LineElement);
                        lineActions.Add(moveDown);
                        lineActions.Add(new Button(() => duplicateElement(passageId, capturedElement))
                            { text = "⧉", tooltip = "Duplicate line" });
                        lineActions.Add(new Button(() => deleteElement(passageId, capturedElement))
                            { text = "×", tooltip = "Delete line" });
                        speakerRow.Add(lineActions);
                        lineBody.Add(speakerRow);

                        var text = new TextField { multiline = true };
                        text.SetValueWithoutNotify(line.text);
                        text.AddToClassList(DialogueEditorStyleClasses.ConversationInlineText);
                        text.textEdition.placeholder = "Write dialogue…";
                        text.style.height = GetDialogueFieldHeight(line.text);
                        text.tooltip = "Dialogue text";
                        text.RegisterValueChangedCallback(evt => changeLineText(passageId, capturedElement, evt.newValue));
                        _lineEditors[capturedElement] = text;
                        lineBody.Add(text);
                        lineCard.Add(lineBody);
                        extensionContainer.Add(lineCard);
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
                            var choiceActions = new VisualElement();
                            choiceActions.AddToClassList(DialogueEditorStyleClasses.ConversationInlineChoiceActions);
                            var moveUp = new Button(() =>
                                moveChoiceOption(passageId, capturedElement, capturedOption, capturedOption - 1))
                                { text = "↑", tooltip = "Move choice up" };
                            moveUp.SetEnabled(capturedOption > 0);
                            choiceActions.Add(moveUp);
                            var moveDown = new Button(() =>
                                moveChoiceOption(passageId, capturedElement, capturedOption, capturedOption + 1))
                                { text = "↓", tooltip = "Move choice down" };
                            moveDown.SetEnabled(capturedOption < choice.options.Count - 1);
                            choiceActions.Add(moveDown);
                            choiceActions.Add(new Button(() =>
                                deleteChoiceOption(passageId, capturedElement, capturedOption))
                                { text = "×", tooltip = "Delete choice" });
                            row.Add(choiceActions);
                            var port = CreateOutputPort(binding);
                            row.Add(port);
                            connectionPorts.Add(port);
                            choiceGroup.Add(row);
                        }
                        extensionContainer.Add(choiceGroup);
                    }
                }

                var addRow = new VisualElement();
                addRow.AddToClassList(DialogueEditorStyleClasses.ConversationInlineActions);
                var endButton = new Button(() => endConversation(passageId))
                {
                    text = endsConversation ? "✓ Ends Here" : "End Conversation",
                    tooltip = endsConversation
                        ? "This passage ends the conversation"
                        : "Replace the passage's outgoing route with an End element"
                };
                endButton.AddToClassList(DialogueEditorStyleClasses.ConversationInlineEndAction);
                endButton.SetEnabled(!endsConversation);
                addRow.Add(endButton);
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

            private static float GetDialogueFieldHeight(string value)
            {
                var source = value ?? string.Empty;
                var explicitLines = source.Count(character => character == '\n') + 1;
                var wrappedLines = Math.Max(1, (int)Math.Ceiling(source.Length / 52f));
                return 17f + Math.Min(5, Math.Max(explicitLines, wrappedLines)) * 15f;
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
                Action<PortBinding> disconnect)
            {
                focusable = true;
                this.AddManipulator(new ContentZoomer());
                this.AddManipulator(new ContentDragger());
                this.AddManipulator(new SelectionDragger());
                this.AddManipulator(new RectangleSelector());
                RegisterCallback<KeyDownEvent>(OnGraphKeyDown);
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
                Insert(0, grid);
                grid.StretchToParentSize();

                foreach (var passage in conversation.passages.Where(passage => passage != null))
                {
                    var node = new PassageNode(
                        project, passage, passage.id == conversation.startPassageId,
                        GetPassageWarnings(conversation, passage).Count > 0,
                        selectPassage, movePassage, renamePassage, changeSpeaker, changeExpression, changeLineText,
                        changeChoiceText, addLine, addChoice, endConversation, moveElement, duplicateElement, deleteElement,
                        moveChoiceOption, deleteChoiceOption, createTarget);
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
                if (evt.keyCode != KeyCode.Space || IsTextInput(evt.target)) return;
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
                            || evt.target is GridBackground))
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
                    FrameSelection();
                }
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
