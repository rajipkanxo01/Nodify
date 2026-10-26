using System;
using System.Collections.Generic;
using System.Linq;
using ToolForDialogue._Project.Dialogue.Runtime.Assets;
using ToolForDialogue._Project.Dialogue.Runtime.Types;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToolForDialogue.Editor._Project.Scripts.Editor.Tabs.Conversation
{
    internal sealed partial class ConversationsTab
    {
        private void RebuildInspector()
        {
            _inspector.Clear();
            _passageWarning = null;
            if (_selectedConversation == null)
            {
                _inspector.Add(new HelpBox("Select a conversation.", HelpBoxMessageType.Info));
                return;
            }

            var passageIndex = FindPassageIndex(_selectedPassageId);
            if (passageIndex < 0)
            {
                _inspector.Add(new HelpBox("Select a passage in the graph.", HelpBoxMessageType.Info));
                return;
            }

            var serializedConversation = new SerializedObject(_selectedConversation);
            serializedConversation.Update();
            var passage = serializedConversation.FindProperty("_passages").GetArrayElementAtIndex(passageIndex);
            var runtimePassage = _selectedConversation.passages[passageIndex];

            var header = new VisualElement();
            header.AddToClassList(DialogueEditorStyleClasses.ConversationInspectorHeader);
            var heading = new Label("Passage Inspector");
            heading.AddToClassList(DialogueEditorStyleClasses.SectionTitle);
            header.Add(heading);
            header.Add(new Button(ShowPassageMenu) { text = "•••", tooltip = "Passage actions" });
            _inspector.Add(header);

            var titleProperty = passage.FindPropertyRelative("_title");
            var titleField = new TextField("Title") { isDelayed = true };
            titleField.SetValueWithoutNotify(titleProperty.stringValue);
            titleField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(_selectedConversation, "Rename Dialogue Passage");
                serializedConversation.Update();
                titleProperty.stringValue = evt.newValue;
                serializedConversation.ApplyModifiedProperties();
                MarkConversationChanged();
                RebuildGraph();
            });
            _inspector.Add(titleField);

            var isStart = runtimePassage.id == _selectedConversation.startPassageId;
            var startButton = new Button(() => SetStartPassage(_selectedPassageId))
            {
                text = isStart ? "★ Start Passage" : "Set as Start Passage"
            };
            startButton.SetEnabled(!isStart);
            _inspector.Add(startButton);

            var passageWarnings = GetPassageWarnings(_selectedConversation, runtimePassage);
            _passageWarning = new HelpBox(string.Join("\n", passageWarnings), HelpBoxMessageType.Warning);
            _passageWarning.style.display = passageWarnings.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _inspector.Add(_passageWarning);

            var divider = new VisualElement();
            divider.AddToClassList(DialogueEditorStyleClasses.ConversationInspectorDivider);
            _inspector.Add(divider);

            var structured = new Foldout
            {
                text = "Structured Elements (Advanced)",
                value = false
            };
            structured.AddToClassList(DialogueEditorStyleClasses.ConversationStructuredEditor);
            structured.RegisterCallback<SerializedPropertyChangeEvent>(_ => MarkConversationChanged());

            var contentHeader = new VisualElement();
            contentHeader.AddToClassList(DialogueEditorStyleClasses.ConversationInspectorHeader);
            var contentTitle = new Label("Content");
            contentTitle.AddToClassList(DialogueEditorStyleClasses.SectionTitle);
            contentHeader.Add(contentTitle);
            var contentActions = new VisualElement();
            contentActions.style.flexDirection = FlexDirection.Row;
            contentActions.Add(new Button(() => AddElement(new LineElement())) { text = "+ Dialogue Line" });
            contentActions.Add(CreateAddElementButton());
            contentHeader.Add(contentActions);
            structured.Add(contentHeader);

            var elements = passage.FindPropertyRelative("_elements");
            if (elements.arraySize == 0)
            {
                structured.Add(new HelpBox(
                    "This passage has no content. Add a dialogue element to begin.",
                    HelpBoxMessageType.Info));
            }

            for (var index = 0; index < elements.arraySize; index++)
            {
                structured.Add(CreateElementCard(serializedConversation, elements, index));
            }

            _inspector.Add(structured);
        }

        private VisualElement CreateAddElementButton()
        {
            return new Button(() =>
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Player Choice"), false, () => AddElement(new ChoiceElement()));
                menu.AddItem(new GUIContent("Jump"), false, () => AddElement(new JumpElement()));
                menu.AddItem(new GUIContent("End Conversation"), false, () => AddElement(new EndElement()));
                menu.ShowAsContext();
            }) { text = "More ▾" };
        }

        private VisualElement CreateElementCard(
            SerializedObject serializedConversation,
            SerializedProperty elements,
            int index)
        {
            var element = elements.GetArrayElementAtIndex(index);
            var instance = element.managedReferenceValue as DialogueElement;
            var card = new VisualElement();
            card.AddToClassList(DialogueEditorStyleClasses.ConversationElementCard);

            var header = new VisualElement();
            header.AddToClassList(DialogueEditorStyleClasses.ConversationElementHeader);
            var title = new Label(GetElementName(instance));
            title.AddToClassList(DialogueEditorStyleClasses.ConversationElementTitle);
            header.Add(title);
            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            var up = new Button(() => MoveElement(index, index - 1)) { text = "↑", tooltip = "Move up" };
            up.SetEnabled(index > 0);
            actions.Add(up);
            var down = new Button(() => MoveElement(index, index + 1)) { text = "↓", tooltip = "Move down" };
            down.SetEnabled(index < elements.arraySize - 1);
            actions.Add(down);
            actions.Add(new Button(() => DeleteElement(index)) { text = "×", tooltip = "Delete element" });
            header.Add(actions);
            card.Add(header);

            var enabledProperty = element.FindPropertyRelative("_enabled");
            if (enabledProperty != null)
            {
                var enabled = new PropertyField(enabledProperty, "Enabled");
                enabled.Bind(serializedConversation);
                card.Add(enabled);
            }

            if (instance is LineElement)
            {
                BuildLineEditor(card, serializedConversation, element);
            }
            else if (instance is JumpElement)
            {
                card.Add(CreatePassagePopup(
                    "Destination",
                    element.FindPropertyRelative("_destinationPassageId"),
                    serializedConversation,
                    RebuildGraph));
            }
            else if (instance is ChoiceElement)
            {
                BuildChoiceEditor(card, serializedConversation, element, index);
            }
            else if (instance is EndElement)
            {
                var info = new Label("Dialogue ends when execution reaches this element.");
                info.AddToClassList(DialogueEditorStyleClasses.OptionalHint);
                card.Add(info);
            }
            else
            {
                var property = new PropertyField(element, "Settings");
                property.Bind(serializedConversation);
                card.Add(property);
            }

            return card;
        }

        private void BuildLineEditor(
            VisualElement card,
            SerializedObject serializedConversation,
            SerializedProperty element)
        {
            var speakerProperty = element.FindPropertyRelative("_speaker");
            var expressionProperty = element.FindPropertyRelative("_expressionId");
            var characters = _project.characters.Where(character => character != null).ToList();
            if (speakerProperty.objectReferenceValue == null && characters.Count > 0)
            {
                speakerProperty.objectReferenceValue = characters[0];
                SetGuidValue(expressionProperty, characters[0].defaultExpressionId.value);
                serializedConversation.ApplyModifiedPropertiesWithoutUndo();
                MarkConversationChanged();
            }

            var speakerNames = new List<string>();
            foreach (var character in characters)
            {
                speakerNames.Add(CreateUniqueChoiceName(speakerNames, character.displayName));
            }

            if (speakerNames.Count == 0)
            {
                speakerNames.Add("No characters available");
            }

            var selectedSpeaker =
                Math.Max(0, characters.IndexOf(speakerProperty.objectReferenceValue as CharacterAsset));
            var speaker = new PopupField<string>("Speaker", speakerNames, selectedSpeaker);
            speaker.SetEnabled(characters.Count > 0);
            speaker.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(_selectedConversation, "Change Dialogue Speaker");
                serializedConversation.Update();
                var character = characters[Math.Max(0, speakerNames.IndexOf(evt.newValue))];
                speakerProperty.objectReferenceValue = character;
                SetGuidValue(expressionProperty, character.defaultExpressionId.value);
                serializedConversation.ApplyModifiedProperties();
                MarkConversationChanged();
                RebuildInspector();
                RebuildGraph();
            });
            card.Add(speaker);

            if (characters.Count == 0)
            {
                card.Add(new HelpBox(
                    "Dialogue lines require a speaker. Create a character in the Characters tab first.",
                    HelpBoxMessageType.Error));
            }

            var speakerAsset = speakerProperty.objectReferenceValue as CharacterAsset;
            card.Add(CreateExpressionPopup(
                speakerAsset,
                expressionProperty,
                serializedConversation));

            var textProperty = element.FindPropertyRelative("_text");
            var text = new TextField("Text") { multiline = true };
            text.style.minHeight = 58f;
            text.tooltip = "Press Ctrl+Enter to add another dialogue line.";
            text.SetValueWithoutNotify(textProperty.stringValue);
            text.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(_selectedConversation, "Edit Dialogue Line");
                serializedConversation.Update();
                textProperty.stringValue = evt.newValue;
                serializedConversation.ApplyModifiedProperties();
                MarkConversationChanged();
            });
            text.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return || !evt.ctrlKey)
                {
                    return;
                }

                evt.StopPropagation();
                AddElement(new LineElement());
            });
            card.Add(text);

            var tags = new PropertyField(element.FindPropertyRelative("_tags"), "Tags");
            tags.Bind(serializedConversation);
            card.Add(tags);
        }

        private VisualElement CreateExpressionPopup(
            CharacterAsset character,
            SerializedProperty expressionId,
            SerializedObject serializedConversation)
        {
            var names = new List<string> { "None" };
            var ids = new List<string> { string.Empty };
            if (character != null)
            {
                foreach (var expression in character.expressions.Where(expression => expression != null))
                {
                    names.Add(CreateUniqueChoiceName(names, expression.displayName));
                    ids.Add(expression.id.value);
                }
            }

            var current = Math.Max(0, ids.IndexOf(GetGuidValue(expressionId)));
            var popup = new PopupField<string>("Expression (Optional)", names, current);
            popup.SetEnabled(character != null && names.Count > 1);
            popup.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(_selectedConversation, "Change Dialogue Expression");
                serializedConversation.Update();
                SetGuidValue(expressionId, ids[Math.Max(0, names.IndexOf(evt.newValue))]);
                serializedConversation.ApplyModifiedProperties();
                MarkConversationChanged();
            });
            return popup;
        }

        private void BuildChoiceEditor(
            VisualElement card,
            SerializedObject serializedConversation,
            SerializedProperty element,
            int elementIndex)
        {
            var options = element.FindPropertyRelative("_options");
            for (var optionIndex = 0; optionIndex < options.arraySize; optionIndex++)
            {
                var capturedOption = optionIndex;
                var option = options.GetArrayElementAtIndex(optionIndex);
                var optionCard = new VisualElement();
                optionCard.AddToClassList(DialogueEditorStyleClasses.ConversationChoiceOption);
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                var textProperty = option.FindPropertyRelative("_text");
                var text = new TextField($"Option {optionIndex + 1}");
                text.style.flexGrow = 1f;
                text.SetValueWithoutNotify(textProperty.stringValue);
                text.RegisterValueChangedCallback(evt =>
                {
                    Undo.RecordObject(_selectedConversation, "Edit Dialogue Choice");
                    serializedConversation.Update();
                    options.GetArrayElementAtIndex(capturedOption)
                        .FindPropertyRelative("_text").stringValue = evt.newValue;
                    serializedConversation.ApplyModifiedProperties();
                    MarkConversationChanged();
                });
                row.Add(text);
                row.Add(new Button(() => DeleteChoiceOption(elementIndex, capturedOption))
                    { text = "×", tooltip = "Delete option" });
                optionCard.Add(row);
                optionCard.Add(CreatePassagePopup(
                    "Destination",
                    option.FindPropertyRelative("_destinationPassageId"),
                    serializedConversation,
                    RebuildGraph));
                card.Add(optionCard);
            }

            card.Add(new Button(() => AddChoiceOption(elementIndex)) { text = "+ Add Option" });
        }

        private VisualElement CreatePassagePopup(
            string label,
            SerializedProperty destination,
            SerializedObject serializedConversation,
            Action afterChange)
        {
            var names = new List<string> { "None" };
            var ids = new List<string> { string.Empty };
            foreach (var passage in _selectedConversation.passages.Where(passage => passage != null))
            {
                names.Add(CreateUniqueChoiceName(names, passage.title));
                ids.Add(passage.id.value);
            }

            var current = Math.Max(0, ids.IndexOf(GetGuidValue(destination)));
            var popup = new PopupField<string>(label, names, current);
            popup.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(_selectedConversation, "Change Passage Destination");
                serializedConversation.Update();
                SetGuidValue(destination, ids[Math.Max(0, names.IndexOf(evt.newValue))]);
                serializedConversation.ApplyModifiedProperties();
                MarkConversationChanged();
                afterChange?.Invoke();
            });
            return popup;
        }
    }
}
