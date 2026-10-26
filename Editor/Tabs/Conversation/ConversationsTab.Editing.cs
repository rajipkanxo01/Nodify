using System;
using ToolForDialogue._Project.Dialogue.Runtime.Assets;
using ToolForDialogue._Project.Dialogue.Runtime.Types;
using UnityEditor;
using UnityEngine;

namespace ToolForDialogue.Editor._Project.Scripts.Editor.Tabs.Conversation
{
    internal sealed partial class ConversationsTab
    {
        private void AddElement(DialogueElement instance)
        {
            AddElementToPassage(_selectedPassageId, instance, "Add Dialogue Element");
        }

        private int AddElementToPassage(string passageId, DialogueElement instance, string undoName)
        {
            var serialized = new SerializedObject(_selectedConversation);
            var elements = GetPassageElements(serialized, passageId);
            if (elements == null) return -1;
            Undo.RecordObject(_selectedConversation, undoName);
            var insertionIndex = elements.arraySize;
            if (insertionIndex > 0
                && elements.GetArrayElementAtIndex(insertionIndex - 1).managedReferenceValue is EndElement)
            {
                insertionIndex--;
            }
            if (instance is LineElement
                && insertionIndex > 0
                && (elements.GetArrayElementAtIndex(insertionIndex - 1).managedReferenceValue is ChoiceElement
                    || elements.GetArrayElementAtIndex(insertionIndex - 1).managedReferenceValue is JumpElement))
            {
                insertionIndex--;
            }
            elements.InsertArrayElementAtIndex(insertionIndex);
            var newElement = elements.GetArrayElementAtIndex(insertionIndex);
            newElement.managedReferenceValue = instance;
            if (instance is LineElement)
            {
                AssignDefaultSpeaker(newElement);
            }
            serialized.ApplyModifiedProperties();
            MarkConversationChanged();
            RebuildGraph();
            RebuildInspector();
            return insertionIndex;
        }

        private void ChangeInlineSpeaker(string passageId, int elementIndex, CharacterAsset character)
        {
            var serialized = new SerializedObject(_selectedConversation);
            var element = GetElement(serialized, passageId, elementIndex);
            if (element == null || character == null) return;
            Undo.RecordObject(_selectedConversation, "Change Dialogue Speaker");
            element.FindPropertyRelative("_speaker").objectReferenceValue = character;
            SetGuidValue(element.FindPropertyRelative("_expressionId"), character.defaultExpressionId.value);
            serialized.ApplyModifiedProperties();
            MarkConversationChanged();
            RebuildGraph();
            if (_selectedPassageId == passageId) RebuildInspector();
        }

        private void ChangeInlineExpression(string passageId, int elementIndex, string expressionId)
        {
            var serialized = new SerializedObject(_selectedConversation);
            var element = GetElement(serialized, passageId, elementIndex);
            if (element == null) return;
            if (string.IsNullOrEmpty(expressionId))
            {
                var speaker = element.FindPropertyRelative("_speaker").objectReferenceValue as CharacterAsset;
                expressionId = speaker != null ? speaker.defaultExpressionId.value : string.Empty;
            }
            Undo.RecordObject(_selectedConversation, "Change Dialogue Expression");
            SetGuidValue(element.FindPropertyRelative("_expressionId"), expressionId);
            serialized.ApplyModifiedProperties();
            MarkConversationChanged();
        }

        private void ChangeInlineLineText(string passageId, int elementIndex, string value)
        {
            var serialized = new SerializedObject(_selectedConversation);
            var element = GetElement(serialized, passageId, elementIndex);
            if (element == null) return;
            Undo.RecordObject(_selectedConversation, "Edit Dialogue Line");
            element.FindPropertyRelative("_text").stringValue = value;
            serialized.ApplyModifiedProperties();
            MarkConversationChanged();
        }

        private void ChangeInlineChoiceText(string passageId, int elementIndex, int optionIndex, string value)
        {
            var serialized = new SerializedObject(_selectedConversation);
            var element = GetElement(serialized, passageId, elementIndex);
            var options = element?.FindPropertyRelative("_options");
            if (options == null || optionIndex < 0 || optionIndex >= options.arraySize) return;
            Undo.RecordObject(_selectedConversation, "Edit Dialogue Choice");
            options.GetArrayElementAtIndex(optionIndex).FindPropertyRelative("_text").stringValue = value;
            serialized.ApplyModifiedProperties();
            MarkConversationChanged();
        }

        private void AddInlineLine(string passageId)
        {
            var elementIndex = AddElementToPassage(passageId, new LineElement(), "Add Dialogue Line");
            if (elementIndex >= 0)
            {
                _graph?.FocusLine(passageId, elementIndex);
            }
        }

        private void MoveInlineElement(string passageId, int from, int to)
        {
            var serialized = new SerializedObject(_selectedConversation);
            var elements = GetPassageElements(serialized, passageId);
            if (elements == null || from < 0 || from >= elements.arraySize || to < 0 || to >= elements.arraySize)
            {
                return;
            }
            Undo.RecordObject(_selectedConversation, "Reorder Dialogue Line");
            elements.MoveArrayElement(from, to);
            serialized.ApplyModifiedProperties();
            MarkConversationChanged();
            RebuildGraph();
            if (_selectedPassageId == passageId) RebuildInspector();
            _graph?.FocusLine(passageId, to);
        }

        private void DuplicateInlineElement(string passageId, int elementIndex)
        {
            var serialized = new SerializedObject(_selectedConversation);
            var elements = GetPassageElements(serialized, passageId);
            if (elements == null || elementIndex < 0 || elementIndex >= elements.arraySize) return;
            if (!(elements.GetArrayElementAtIndex(elementIndex).managedReferenceValue is DialogueElement source)) return;
            var copy = Activator.CreateInstance(source.GetType()) as DialogueElement;
            if (copy == null) return;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), copy);
            Undo.RecordObject(_selectedConversation, "Duplicate Dialogue Line");
            elements.InsertArrayElementAtIndex(elementIndex + 1);
            elements.GetArrayElementAtIndex(elementIndex + 1).managedReferenceValue = copy;
            serialized.ApplyModifiedProperties();
            MarkConversationChanged();
            RebuildGraph();
            if (_selectedPassageId == passageId) RebuildInspector();
            _graph?.FocusLine(passageId, elementIndex + 1);
        }

        private void DeleteInlineElement(string passageId, int elementIndex)
        {
            var serialized = new SerializedObject(_selectedConversation);
            var elements = GetPassageElements(serialized, passageId);
            if (elements == null || elementIndex < 0 || elementIndex >= elements.arraySize) return;
            Undo.RecordObject(_selectedConversation, "Delete Dialogue Line");
            elements.DeleteArrayElementAtIndex(elementIndex);
            serialized.ApplyModifiedProperties();
            MarkConversationChanged();
            RebuildGraph();
            if (_selectedPassageId == passageId) RebuildInspector();
        }

        private void MoveInlineChoiceOption(
            string passageId,
            int elementIndex,
            int from,
            int to)
        {
            var serialized = new SerializedObject(_selectedConversation);
            var element = GetElement(serialized, passageId, elementIndex);
            var options = element?.FindPropertyRelative("_options");
            if (options == null || from < 0 || from >= options.arraySize || to < 0 || to >= options.arraySize)
            {
                return;
            }
            Undo.RecordObject(_selectedConversation, "Reorder Dialogue Choice");
            options.MoveArrayElement(from, to);
            serialized.ApplyModifiedProperties();
            MarkConversationChanged();
            RebuildGraph();
            if (_selectedPassageId == passageId) RebuildInspector();
        }

        private void DeleteInlineChoiceOption(string passageId, int elementIndex, int optionIndex)
        {
            var serialized = new SerializedObject(_selectedConversation);
            var elements = GetPassageElements(serialized, passageId);
            if (elements == null || elementIndex < 0 || elementIndex >= elements.arraySize) return;
            var options = elements.GetArrayElementAtIndex(elementIndex).FindPropertyRelative("_options");
            if (options == null || optionIndex < 0 || optionIndex >= options.arraySize) return;
            Undo.RecordObject(_selectedConversation, "Delete Dialogue Choice");
            if (options.arraySize == 1)
            {
                elements.DeleteArrayElementAtIndex(elementIndex);
            }
            else
            {
                options.DeleteArrayElementAtIndex(optionIndex);
            }
            serialized.ApplyModifiedProperties();
            MarkConversationChanged();
            RebuildGraph();
            if (_selectedPassageId == passageId) RebuildInspector();
        }

        private void AddInlineChoice(string passageId)
        {
            var serialized = new SerializedObject(_selectedConversation);
            var elements = GetPassageElements(serialized, passageId);
            if (elements == null) return;
            Undo.RecordObject(_selectedConversation, "Add Dialogue Choice");

            var lastContentIndex = elements.arraySize - 1;
            if (lastContentIndex >= 0
                && elements.GetArrayElementAtIndex(lastContentIndex).managedReferenceValue is EndElement)
            {
                lastContentIndex--;
            }
            if (lastContentIndex >= 0
                && elements.GetArrayElementAtIndex(lastContentIndex).managedReferenceValue is ChoiceElement)
            {
                var existingOptions = elements.GetArrayElementAtIndex(lastContentIndex)
                    .FindPropertyRelative("_options");
                existingOptions.InsertArrayElementAtIndex(existingOptions.arraySize);
                InitializeChoiceOption(existingOptions.GetArrayElementAtIndex(existingOptions.arraySize - 1));
                serialized.ApplyModifiedProperties();
                MarkConversationChanged();
                RebuildGraph();
                if (_selectedPassageId == passageId) RebuildInspector();
                return;
            }

            var insertionIndex = elements.arraySize;
            if (insertionIndex > 0
                && elements.GetArrayElementAtIndex(insertionIndex - 1).managedReferenceValue is EndElement)
            {
                insertionIndex--;
            }
            elements.InsertArrayElementAtIndex(insertionIndex);
            var choice = elements.GetArrayElementAtIndex(insertionIndex);
            choice.managedReferenceValue = new ChoiceElement();
            var options = choice.FindPropertyRelative("_options");
            options.arraySize = 1;
            InitializeChoiceOption(options.GetArrayElementAtIndex(0));
            serialized.ApplyModifiedProperties();
            MarkConversationChanged();
            RebuildGraph();
            if (_selectedPassageId == passageId) RebuildInspector();
        }

        private void EndInlineConversation(string passageId)
        {
            var serialized = new SerializedObject(_selectedConversation);
            var elements = GetPassageElements(serialized, passageId);
            if (elements == null) return;

            var terminalStart = elements.arraySize;
            var removesChoices = false;
            for (var index = elements.arraySize - 1; index >= 0; index--)
            {
                var terminal = elements.GetArrayElementAtIndex(index).managedReferenceValue;
                if (!(terminal is EndElement) && !(terminal is JumpElement) && !(terminal is ChoiceElement))
                {
                    break;
                }
                removesChoices |= terminal is ChoiceElement;
                terminalStart = index;
            }

            if (removesChoices && !EditorUtility.DisplayDialog(
                    "End Conversation Here",
                    "Ending this passage will remove its player choices and their connections.",
                    "End Conversation",
                    "Cancel"))
            {
                return;
            }

            Undo.RecordObject(_selectedConversation, "End Dialogue Conversation");
            while (elements.arraySize > terminalStart)
            {
                elements.DeleteArrayElementAtIndex(elements.arraySize - 1);
            }
            elements.InsertArrayElementAtIndex(elements.arraySize);
            elements.GetArrayElementAtIndex(elements.arraySize - 1).managedReferenceValue = new EndElement();
            serialized.ApplyModifiedProperties();
            MarkConversationChanged();
            RebuildConversationList();
            RebuildGraph();
            if (_selectedPassageId == passageId) RebuildInspector();
        }

        private void CreateAndConnectTarget(PortBinding binding)
        {
            if (binding == null) return;
            var sourceIndex = FindPassageIndex(binding.passageId);
            if (sourceIndex < 0) return;
            var sourcePosition = _selectedConversation.passages[sourceIndex].graphPosition;
            var serialized = new SerializedObject(_selectedConversation);
            var passages = serialized.FindProperty("_passages");
            Undo.RecordObject(_selectedConversation, "Create Connected Dialogue Passage");
            passages.InsertArrayElementAtIndex(passages.arraySize);
            var newPassage = passages.GetArrayElementAtIndex(passages.arraySize - 1);
            var id = InitializePassage(
                newPassage,
                $"Passage {passages.arraySize}",
                sourcePosition + new Vector2(430f, 80f + Math.Max(0, binding.optionIndex) * 70f));
            SetPortDestination(serialized, binding, id);
            serialized.ApplyModifiedProperties();
            MarkConversationChanged();
            _selectedPassageId = id;
            RebuildConversationList();
            RebuildGraph();
            RebuildInspector();
            _graph?.SelectPassage(id);
        }

        private void ConnectPort(PortBinding binding, string destinationId)
        {
            if (binding == null) return;
            var serialized = new SerializedObject(_selectedConversation);
            Undo.RecordObject(_selectedConversation, "Connect Dialogue Passages");
            SetPortDestination(serialized, binding, destinationId);
            serialized.ApplyModifiedProperties();
            MarkConversationChanged();
            if (binding.isTerminal) RefreshTerminalUiLater();
        }

        private void DisconnectPort(PortBinding binding)
        {
            if (binding == null) return;
            var serialized = new SerializedObject(_selectedConversation);
            Undo.RecordObject(_selectedConversation, "Disconnect Dialogue Passages");
            SetPortDestination(serialized, binding, string.Empty);
            serialized.ApplyModifiedProperties();
            MarkConversationChanged();
            if (binding.isTerminal) RefreshTerminalUiLater();
        }

        private void RefreshTerminalUiLater()
        {
            if (_terminalRefreshScheduled) return;
            _terminalRefreshScheduled = true;
            var conversation = _selectedConversation;
            EditorApplication.delayCall += () =>
            {
                _terminalRefreshScheduled = false;
                if (_selectedConversation != conversation) return;
                RebuildConversationList();
                RebuildGraph();
                RebuildInspector();
            };
        }

        private void SetPortDestination(SerializedObject serialized, PortBinding binding, string destinationId)
        {
            var element = GetElement(serialized, binding.passageId, binding.elementIndex);
            if (element == null && binding.isTerminal && !string.IsNullOrEmpty(destinationId))
            {
                var elements = GetPassageElements(serialized, binding.passageId);
                if (elements != null && binding.elementIndex == elements.arraySize)
                {
                    elements.InsertArrayElementAtIndex(elements.arraySize);
                    element = elements.GetArrayElementAtIndex(elements.arraySize - 1);
                    element.managedReferenceValue = new JumpElement();
                }
            }
            if (element == null) return;
            if (binding.optionIndex >= 0)
            {
                var options = element.FindPropertyRelative("_options");
                if (binding.optionIndex >= options.arraySize) return;
                SetGuidValue(
                    options.GetArrayElementAtIndex(binding.optionIndex)
                        .FindPropertyRelative("_destinationPassageId"),
                    destinationId);
            }
            else if (binding.isTerminal)
            {
                if (string.IsNullOrEmpty(destinationId))
                {
                    element.managedReferenceValue = new EndElement();
                    return;
                }
                if (!(element.managedReferenceValue is JumpElement))
                {
                    element.managedReferenceValue = new JumpElement();
                }
                SetGuidValue(element.FindPropertyRelative("_destinationPassageId"), destinationId);
            }
            else
            {
                SetGuidValue(element.FindPropertyRelative("_destinationPassageId"), destinationId);
            }
        }

        private static void InitializeChoiceOption(SerializedProperty option)
        {
            SetGuidValue(option.FindPropertyRelative("_id"), Guid.NewGuid().ToString("N"));
            option.FindPropertyRelative("_text").stringValue = "Choice";
            SetGuidValue(option.FindPropertyRelative("_destinationPassageId"), string.Empty);
            option.FindPropertyRelative("_availability").managedReferenceValue = null;
            option.FindPropertyRelative("_unavailableBehavior").enumValueIndex = 0;
        }

    }
}
