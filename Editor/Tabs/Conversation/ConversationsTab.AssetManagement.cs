using System;
using System.IO;
using System.Linq;
using ToolForDialogue._Project.Dialogue.Runtime.Assets;
using ToolForDialogue._Project.Dialogue.Runtime.Types;
using ToolForDialogue.Editor._Project.Scripts.Editor.Services;
using UnityEditor;
using UnityEngine;

namespace ToolForDialogue.Editor._Project.Scripts.Editor.Tabs.Conversation
{
    internal sealed partial class ConversationsTab
    {
        private void SelectConversation(ConversationAsset conversation)
        {
            _selectedConversation = conversation;
            _expandedConversations.Add(conversation.id.value);
            _selectedPassageId = conversation.passages.FirstOrDefault(passage => passage != null)?.id.value;
            RebuildConversationList();
            RebuildGraph();
            RebuildInspector();
        }

        private void SelectPassage(string passageId)
        {
            _selectedPassageId = passageId;
            RebuildConversationList();
            RebuildInspector();
        }

        private void EnsureSelectedPassage()
        {
            if (_selectedConversation == null)
            {
                _selectedPassageId = null;
                return;
            }
            if (_selectedConversation.passages.Any(passage => passage != null && passage.id.value == _selectedPassageId))
            {
                return;
            }
            _selectedPassageId = _selectedConversation.passages.FirstOrDefault(passage => passage != null)?.id.value;
        }

        private void CreateConversation()
        {
            var directory = GetConversationsFolder();
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Dialogue Conversation",
                "New Conversation",
                "asset",
                "Choose a filename for the new conversation.",
                directory);
            if (string.IsNullOrWhiteSpace(path)) return;
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            var conversation = ScriptableObject.CreateInstance<ConversationAsset>();
            AssetDatabase.CreateAsset(conversation, path);
            InitializeConversation(conversation, Path.GetFileNameWithoutExtension(path));
            Undo.RegisterCreatedObjectUndo(conversation, "Create Dialogue Conversation");

            Undo.RecordObject(_project, "Add Dialogue Conversation");
            var projectObject = new SerializedObject(_project);
            var conversations = projectObject.FindProperty("_conversations");
            conversations.arraySize++;
            conversations.GetArrayElementAtIndex(conversations.arraySize - 1).objectReferenceValue = conversation;
            projectObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(_project);
            AssetDatabase.SaveAssetIfDirty(conversation);
            ConversationYarnGenerator.Schedule(conversation);
            SelectConversation(conversation);
        }

        private string GetConversationsFolder()
        {
            var projectDirectory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(_project))?.Replace('\\', '/') ?? "Assets";
            var directory = $"{projectDirectory}/Conversations";
            if (!AssetDatabase.IsValidFolder(directory))
            {
                AssetDatabase.CreateFolder(projectDirectory, "Conversations");
            }
            return directory;
        }

        private void InitializeConversation(ConversationAsset conversation, string displayName)
        {
            var serialized = new SerializedObject(conversation);
            serialized.FindProperty("_displayName").stringValue = displayName;
            var passages = serialized.FindProperty("_passages");
            passages.InsertArrayElementAtIndex(0);
            var startId = InitializePassage(
                passages.GetArrayElementAtIndex(0),
                "Start",
                new Vector2(80f, 80f));
            SetGuidValue(serialized.FindProperty("_startPassageId"), startId);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(conversation);
        }

        private string InitializePassage(SerializedProperty passage, string title, Vector2 position)
        {
            var id = Guid.NewGuid().ToString("N");
            SetGuidValue(passage.FindPropertyRelative("_id"), id);
            passage.FindPropertyRelative("_title").stringValue = title;
            passage.FindPropertyRelative("_graphPosition").vector2Value = position;

            var elements = passage.FindPropertyRelative("_elements");
            elements.arraySize = 2;
            var line = elements.GetArrayElementAtIndex(0);
            line.managedReferenceValue = new LineElement();
            AssignDefaultSpeaker(line);
            elements.GetArrayElementAtIndex(1).managedReferenceValue = new EndElement();
            return id;
        }

        private void AssignDefaultSpeaker(SerializedProperty lineElement, CharacterAsset previousSpeaker = null)
        {
            var characters = _project.characters.Where(candidate => candidate != null).Distinct().ToList();
            if (characters.Count == 0)
            {
                return;
            }
            var character = characters[(characters.IndexOf(previousSpeaker) + 1) % characters.Count];
            lineElement.FindPropertyRelative("_speaker").objectReferenceValue = character;
            SetGuidValue(
                lineElement.FindPropertyRelative("_expressionId"),
                character.defaultExpressionId.value);
        }

        private void AddPassage()
        {
            var serialized = new SerializedObject(_selectedConversation);
            var passages = serialized.FindProperty("_passages");
            Undo.RecordObject(_selectedConversation, "Add Dialogue Passage");
            passages.InsertArrayElementAtIndex(passages.arraySize);
            var passage = passages.GetArrayElementAtIndex(passages.arraySize - 1);
            var id = InitializePassage(
                passage,
                $"Passage {passages.arraySize}",
                new Vector2(
                60f + passages.arraySize * 28f,
                60f + passages.arraySize * 18f));
            if (passages.arraySize == 1)
            {
                SetGuidValue(serialized.FindProperty("_startPassageId"), id);
            }
            serialized.ApplyModifiedProperties();
            MarkConversationChanged();
            _selectedPassageId = id;
            RebuildConversationList();
            RebuildGraph();
            RebuildInspector();
            _graph?.SelectPassage(id);
        }

        private void MovePassage(string passageId, Vector2 position)
        {
            var index = FindPassageIndex(passageId);
            if (index < 0) return;
            Undo.RecordObject(_selectedConversation, "Move Dialogue Passage");
            var serialized = new SerializedObject(_selectedConversation);
            serialized.FindProperty("_passages").GetArrayElementAtIndex(index)
                .FindPropertyRelative("_graphPosition").vector2Value = position;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_selectedConversation);
        }

        private void RenameInlinePassage(string passageId, string value)
        {
            var index = FindPassageIndex(passageId);
            if (index < 0) return;
            Undo.RecordObject(_selectedConversation, "Rename Dialogue Passage");
            var serialized = new SerializedObject(_selectedConversation);
            serialized.FindProperty("_passages").GetArrayElementAtIndex(index)
                .FindPropertyRelative("_title").stringValue = value;
            serialized.ApplyModifiedProperties();
            MarkConversationChanged();
            RebuildGraph();
            if (_selectedPassageId == passageId) RebuildInspector();
        }

        private void SetStartPassage(string passageId)
        {
            Undo.RecordObject(_selectedConversation, "Set Start Passage");
            var serialized = new SerializedObject(_selectedConversation);
            SetGuidValue(serialized.FindProperty("_startPassageId"), passageId);
            serialized.ApplyModifiedProperties();
            MarkConversationChanged();
            RebuildConversationList();
            RebuildGraph();
            RebuildInspector();
        }

        private void MoveElement(int from, int to)
        {
            var serialized = new SerializedObject(_selectedConversation);
            var elements = GetSelectedElements(serialized);
            if (to < 0 || to >= elements.arraySize) return;
            Undo.RecordObject(_selectedConversation, "Reorder Dialogue Element");
            elements.MoveArrayElement(from, to);
            serialized.ApplyModifiedProperties();
            MarkConversationChanged();
            RebuildInspector();
        }

        private void DeleteElement(int index)
        {
            var serialized = new SerializedObject(_selectedConversation);
            var elements = GetSelectedElements(serialized);
            Undo.RecordObject(_selectedConversation, "Delete Dialogue Element");
            elements.DeleteArrayElementAtIndex(index);
            serialized.ApplyModifiedProperties();
            MarkConversationChanged();
            RebuildGraph();
            RebuildInspector();
        }

        private void AddChoiceOption(int elementIndex)
        {
            var serialized = new SerializedObject(_selectedConversation);
            var options = GetSelectedElements(serialized).GetArrayElementAtIndex(elementIndex)
                .FindPropertyRelative("_options");
            Undo.RecordObject(_selectedConversation, "Add Dialogue Choice Option");
            options.InsertArrayElementAtIndex(options.arraySize);
            var option = options.GetArrayElementAtIndex(options.arraySize - 1);
            SetGuidValue(option.FindPropertyRelative("_id"), Guid.NewGuid().ToString("N"));
            option.FindPropertyRelative("_text").stringValue = "Choice";
            SetGuidValue(option.FindPropertyRelative("_destinationPassageId"), string.Empty);
            option.FindPropertyRelative("_availability").managedReferenceValue = null;
            option.FindPropertyRelative("_unavailableBehavior").enumValueIndex = 0;
            serialized.ApplyModifiedProperties();
            MarkConversationChanged();
            RebuildInspector();
        }

        private void DeleteChoiceOption(int elementIndex, int optionIndex)
        {
            var serialized = new SerializedObject(_selectedConversation);
            var options = GetSelectedElements(serialized).GetArrayElementAtIndex(elementIndex)
                .FindPropertyRelative("_options");
            Undo.RecordObject(_selectedConversation, "Delete Dialogue Choice Option");
            options.DeleteArrayElementAtIndex(optionIndex);
            serialized.ApplyModifiedProperties();
            MarkConversationChanged();
            RebuildGraph();
            RebuildInspector();
        }

        private SerializedProperty GetSelectedElements(SerializedObject serialized)
        {
            return GetPassageElements(serialized, _selectedPassageId);
        }

        private SerializedProperty GetPassageElements(SerializedObject serialized, string passageId)
        {
            var passageIndex = FindPassageIndex(passageId);
            if (passageIndex < 0) return null;
            return serialized.FindProperty("_passages").GetArrayElementAtIndex(passageIndex)
                .FindPropertyRelative("_elements");
        }

        private SerializedProperty GetElement(SerializedObject serialized, string passageId, int elementIndex)
        {
            var elements = GetPassageElements(serialized, passageId);
            if (elements == null || elementIndex < 0 || elementIndex >= elements.arraySize) return null;
            return elements.GetArrayElementAtIndex(elementIndex);
        }

        private int FindPassageIndex(string passageId)
        {
            if (_selectedConversation == null) return -1;
            for (var index = 0; index < _selectedConversation.passages.Count; index++)
            {
                if (_selectedConversation.passages[index]?.id.value == passageId) return index;
            }
            return -1;
        }

        private void ShowConversationMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Reveal Asset"), false, () =>
            {
                Selection.activeObject = _selectedConversation;
                EditorGUIUtility.PingObject(_selectedConversation);
            });
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Delete Conversation"), false, DeleteConversation);
            menu.ShowAsContext();
        }

        private void ShowPassageMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Set as Start Passage"), false, () => SetStartPassage(_selectedPassageId));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Delete Passage"), false, DeleteSelectedPassage);
            menu.ShowAsContext();
        }

        private void DeleteSelectedPassage()
        {
            var index = FindPassageIndex(_selectedPassageId);
            if (index < 0 || !EditorUtility.DisplayDialog(
                    "Delete Passage",
                    "Delete this passage and the choice options that lead to it? Existing jumps to it will become invalid.",
                    "Delete",
                    "Cancel")) return;
            var serialized = new SerializedObject(_selectedConversation);
            var passages = serialized.FindProperty("_passages");
            var deletedId = _selectedPassageId;
            Undo.RecordObject(_selectedConversation, "Delete Dialogue Passage");
            RemoveChoicesToPassage(passages, deletedId);
            passages.DeleteArrayElementAtIndex(index);
            if (GetGuidValue(serialized.FindProperty("_startPassageId")) == deletedId)
            {
                SetGuidValue(serialized.FindProperty("_startPassageId"), passages.arraySize > 0
                    ? GetGuidValue(passages.GetArrayElementAtIndex(0).FindPropertyRelative("_id"))
                    : string.Empty);
            }
            serialized.ApplyModifiedProperties();
            MarkConversationChanged();
            EnsureSelectedPassage();
            RebuildConversationList();
            RebuildGraph();
            RebuildInspector();
        }

        private static void RemoveChoicesToPassage(SerializedProperty passages, string destinationId)
        {
            for (var passageIndex = 0; passageIndex < passages.arraySize; passageIndex++)
            {
                var passage = passages.GetArrayElementAtIndex(passageIndex);
                if (GetGuidValue(passage.FindPropertyRelative("_id")) == destinationId) continue;
                var elements = passage.FindPropertyRelative("_elements");
                for (var elementIndex = elements.arraySize - 1; elementIndex >= 0; elementIndex--)
                {
                    var element = elements.GetArrayElementAtIndex(elementIndex);
                    if (!(element.managedReferenceValue is ChoiceElement)) continue;
                    var options = element.FindPropertyRelative("_options");
                    var removedOption = false;
                    for (var optionIndex = options.arraySize - 1; optionIndex >= 0; optionIndex--)
                    {
                        var option = options.GetArrayElementAtIndex(optionIndex);
                        if (GetGuidValue(option.FindPropertyRelative("_destinationPassageId")) != destinationId) continue;
                        options.DeleteArrayElementAtIndex(optionIndex);
                        removedOption = true;
                    }
                    if (removedOption && options.arraySize == 0)
                    {
                        elements.DeleteArrayElementAtIndex(elementIndex);
                    }
                }
            }
        }

        private void DeletePassage(string passageId)
        {
            if (FindPassageIndex(passageId) < 0) return;
            _selectedPassageId = passageId;
            DeleteSelectedPassage();
            _graph?.Focus();
        }

        private void DeleteConversation()
        {
            if (!EditorUtility.DisplayDialog(
                    "Delete Conversation",
                    $"Delete '{_selectedConversation.displayName}' and its asset? This cannot be undone.",
                    "Delete",
                    "Cancel")) return;
            var target = _selectedConversation;
            var projectObject = new SerializedObject(_project);
            var conversations = projectObject.FindProperty("_conversations");
            Undo.RecordObject(_project, "Remove Dialogue Conversation");
            for (var index = 0; index < conversations.arraySize; index++)
            {
                if (conversations.GetArrayElementAtIndex(index).objectReferenceValue != target) continue;
                var previousSize = conversations.arraySize;
                conversations.DeleteArrayElementAtIndex(index);
                if (conversations.arraySize == previousSize)
                {
                    conversations.DeleteArrayElementAtIndex(index);
                }
                break;
            }
            projectObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(_project);
            var path = AssetDatabase.GetAssetPath(target);
            ConversationYarnGenerator.DeleteGeneratedFile(target);
            _selectedConversation = _project.conversations.FirstOrDefault(conversation => conversation != null && conversation != target);
            AssetDatabase.DeleteAsset(path);
            EnsureSelectedPassage();
            RebuildConversationList();
            RebuildGraph();
            RebuildInspector();
        }
    }
}
