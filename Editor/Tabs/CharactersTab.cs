using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ToolForDialogue._Project.Dialogue.Runtime.Assets;
using ToolForDialogue._Project.Dialogue.Runtime.Types;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToolForDialogue.Editor._Project.Scripts.Editor.Tabs
{
    /// <summary>
    /// Owns character, portrait, and expression management controls.
    /// </summary>
    internal sealed class CharactersTab : DialogueEditorTabBase
    {
        private DialogueProjectAsset _project;
        private CharacterAsset _selectedCharacter;
        private VisualElement _characterList;
        private VisualElement _editorPane;
        private string _searchQuery = string.Empty;

        public override DialogueEditorTabId id => DialogueEditorTabId.Characters;
        public override string label => "Characters";
        protected override void BuildContent(VisualElement container, DialogueEditorTabContext context)
        {
            _project = context.selectedProject;
            if (_selectedCharacter == null || !_project.characters.Contains(_selectedCharacter))
            {
                _selectedCharacter = _project.characters.FirstOrDefault(character => character != null);
            }

            var splitView = new VisualElement();
            splitView.AddToClassList(DialogueEditorStyleClasses.CharacterSplitView);

            var sidebar = new VisualElement();
            sidebar.AddToClassList(DialogueEditorStyleClasses.CharacterSidebar);

            var sidebarHeader = new VisualElement();
            sidebarHeader.AddToClassList(DialogueEditorStyleClasses.CharacterSidebarHeader);
            var sidebarTitle = new Label("Characters");
            sidebarTitle.AddToClassList(DialogueEditorStyleClasses.CharacterSidebarTitle);
            sidebarHeader.Add(sidebarTitle);
            var createButton = new Button(CreateCharacter)
            {
                text = "+",
                tooltip = "Create a new character"
            };
            createButton.AddToClassList(DialogueEditorStyleClasses.PrimaryAction);
            sidebarHeader.Add(createButton);
            sidebar.Add(sidebarHeader);

            var searchField = new ToolbarSearchField();
            searchField.AddToClassList(DialogueEditorStyleClasses.CharacterSearch);
            searchField.SetValueWithoutNotify(_searchQuery);
            searchField.RegisterValueChangedCallback(evt =>
            {
                _searchQuery = evt.newValue ?? string.Empty;
                RebuildCharacterList();
            });
            sidebar.Add(searchField);

            _characterList = new ScrollView(ScrollViewMode.Vertical);
            _characterList.AddToClassList(DialogueEditorStyleClasses.CharacterList);
            sidebar.Add(_characterList);
            splitView.Add(sidebar);

            var editorScroll = new ScrollView(ScrollViewMode.Vertical);
            editorScroll.AddToClassList(DialogueEditorStyleClasses.CharacterEditor);
            _editorPane = new VisualElement();
            _editorPane.AddToClassList(DialogueEditorStyleClasses.CharacterEditorContent);
            editorScroll.Add(_editorPane);
            splitView.Add(editorScroll);

            container.Add(splitView);
            RebuildCharacterList();
            RebuildCharacterEditor();
        }

        private void RebuildCharacterList()
        {
            if (_characterList == null)
            {
                return;
            }

            _characterList.Clear();
            var characters = _project.characters
                .Where(character => character != null)
                .Where(character => string.IsNullOrWhiteSpace(_searchQuery)
                                    || character.displayName.IndexOf(_searchQuery,
                                        StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (characters.Count == 0)
            {
                var message = new Label(string.IsNullOrWhiteSpace(_searchQuery)
                    ? "No characters yet. Create one to get started."
                    : "No characters match this search.");
                message.AddToClassList(DialogueEditorStyleClasses.CharacterListEmpty);
                _characterList.Add(message);
                return;
            }

            foreach (var character in characters)
            {
                var capturedCharacter = character;
                var row = new Button(() => SelectCharacter(capturedCharacter));

                row.AddToClassList(DialogueEditorStyleClasses.CharacterListItem);
                row.EnableInClassList(
                    DialogueEditorStyleClasses.SelectedCharacter,
                    character == _selectedCharacter);

                var portrait = new Image
                {
                    sprite = GetDefaultPortrait(character),
                    scaleMode = ScaleMode.ScaleToFit
                };

                portrait.AddToClassList(DialogueEditorStyleClasses.CharacterListPortrait);
                row.Add(portrait);

                var text = new VisualElement();
                text.AddToClassList(DialogueEditorStyleClasses.CharacterListText);
                var name = new Label(string.IsNullOrWhiteSpace(character.displayName)
                    ? "Unnamed Character"
                    : character.displayName);
                name.AddToClassList(DialogueEditorStyleClasses.CharacterListName);
                text.Add(name);
                text.Add(new Label(
                    $"{character.expressions.Count} expression{(character.expressions.Count == 1 ? string.Empty : "s")}"));
                row.Add(text);

                if (HasCharacterWarning(character))
                {
                    var warning = new Label("!") { tooltip = GetCharacterWarning(character) };
                    warning.AddToClassList(DialogueEditorStyleClasses.CharacterWarningBadge);
                    row.Add(warning);
                }

                _characterList.Add(row);
            }
        }

        private void RebuildCharacterEditor()
        {
            if (_editorPane == null)
            {
                return;
            }

            _editorPane.Clear();
            if (_selectedCharacter == null)
            {
                var emptyState = new HelpBox(
                    "Select a character from the list, or use + to create one.",
                    HelpBoxMessageType.Info);
                emptyState.AddToClassList(DialogueEditorStyleClasses.CharacterEditorEmpty);
                _editorPane.Add(emptyState);
                return;
            }

            var serializedCharacter = new SerializedObject(_selectedCharacter);
            serializedCharacter.Update();
            var displayNameProperty = serializedCharacter.FindProperty("_displayName");
            var expressionsProperty = serializedCharacter.FindProperty("_expressions");
            var defaultExpressionProperty = serializedCharacter.FindProperty("_defaultExpressionId");
            var usageCount = CountCharacterUsages(_selectedCharacter);

            var detailsPanel = new VisualElement();
            detailsPanel.AddToClassList(DialogueEditorStyleClasses.CharacterDetailsPanel);

            var headingRow = new VisualElement();
            headingRow.AddToClassList(DialogueEditorStyleClasses.CharacterEditorHeading);
            var heading = new Label("Character Details");
            heading.AddToClassList(DialogueEditorStyleClasses.SectionTitle);
            headingRow.Add(heading);

            var headingActions = new VisualElement();
            headingActions.AddToClassList(DialogueEditorStyleClasses.CharacterHeadingActions);
            var usageLabel = new Label($"{usageCount} line{(usageCount == 1 ? string.Empty : "s")} used");
            usageLabel.tooltip = "Number of dialogue lines spoken by this character";
            usageLabel.AddToClassList(DialogueEditorStyleClasses.CharacterUsageBadge);
            headingActions.Add(usageLabel);

            var revealButton = new Button(() =>
            {
                Selection.activeObject = _selectedCharacter;
                EditorGUIUtility.PingObject(_selectedCharacter);
            }) { text = "Reveal Asset" };
            headingActions.Add(revealButton);
            var moreButton = new Button(ShowCharacterMenu)
            {
                text = "•••",
                tooltip = "More character actions"
            };
            moreButton.AddToClassList(DialogueEditorStyleClasses.OverflowAction);
            headingActions.Add(moreButton);
            headingRow.Add(headingActions);
            detailsPanel.Add(headingRow);

            var nameField = new TextField("Name") { isDelayed = true };
            nameField.SetValueWithoutNotify(displayNameProperty.stringValue);
            nameField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(_selectedCharacter, "Rename Dialogue Character");
                serializedCharacter.Update();
                displayNameProperty.stringValue = evt.newValue;
                serializedCharacter.ApplyModifiedProperties();
                EditorUtility.SetDirty(_selectedCharacter);
                RebuildCharacterList();
            });
            detailsPanel.Add(nameField);

            var technicalFields = new VisualElement();
            technicalFields.AddToClassList(DialogueEditorStyleClasses.CharacterTechnicalFields);
            var technicalLabel = new Label("Asset & Debug");
            technicalLabel.AddToClassList(DialogueEditorStyleClasses.TechnicalLabel);
            technicalFields.Add(technicalLabel);

            var characterAssetPath = AssetDatabase.GetAssetPath(_selectedCharacter);
            var assetNameField = new TextField("Asset Name") { isDelayed = true };
            assetNameField.SetValueWithoutNotify(Path.GetFileNameWithoutExtension(characterAssetPath));
            var assetNameError = new Label();
            assetNameError.AddToClassList(DialogueEditorStyleClasses.InlineWarning);
            assetNameError.style.display = DisplayStyle.None;
            assetNameField.RegisterValueChangedCallback(evt =>
            {
                var currentPath = AssetDatabase.GetAssetPath(_selectedCharacter);
                var currentName = Path.GetFileNameWithoutExtension(currentPath);
                var requestedName = (evt.newValue ?? string.Empty).Trim();
                if (requestedName.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                {
                    requestedName = requestedName.Substring(0, requestedName.Length - ".asset".Length);
                }

                if (string.IsNullOrWhiteSpace(requestedName))
                {
                    assetNameField.SetValueWithoutNotify(currentName);
                    assetNameError.text = "Asset name cannot be empty.";
                    assetNameError.style.display = DisplayStyle.Flex;
                    return;
                }

                var error = AssetDatabase.RenameAsset(currentPath, requestedName);
                if (!string.IsNullOrEmpty(error))
                {
                    assetNameField.SetValueWithoutNotify(currentName);
                    assetNameError.text = error;
                    assetNameError.style.display = DisplayStyle.Flex;
                    return;
                }

                assetNameField.SetValueWithoutNotify(requestedName);
                assetNameError.style.display = DisplayStyle.None;
                AssetDatabase.SaveAssetIfDirty(_selectedCharacter);
            });
            technicalFields.Add(assetNameField);
            technicalFields.Add(assetNameError);

            var idRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row
                }
            };

            var idField = new TextField("Character ID")
            {
                value = _selectedCharacter.characterId.value,
                isReadOnly = true,
                tooltip = "Used for debug.",
                style =
                {
                    flexGrow = 1f
                }
            };

            idRow.Add(idField);
            var copyIdButton = new Button(() => GUIUtility.systemCopyBuffer = _selectedCharacter.characterId.value)
            {
                text = "Copy",
                tooltip = "Copy the stable character ID"
            };
            idRow.Add(copyIdButton);
            technicalFields.Add(idRow);
            detailsPanel.Add(technicalFields);

            detailsPanel.Add(CreateDefaultExpressionField(
                serializedCharacter,
                expressionsProperty,
                defaultExpressionProperty));
            _editorPane.Add(detailsPanel);

            var expressionHeading = new VisualElement();
            expressionHeading.AddToClassList(DialogueEditorStyleClasses.ExpressionHeading);
            var expressionTitle = new Label("Expressions (Optional)");
            expressionTitle.AddToClassList(DialogueEditorStyleClasses.SectionTitle);
            expressionHeading.Add(expressionTitle);
            expressionHeading.Add(new Button(AddExpression) { text = "+ Add Expression" });
            _editorPane.Add(expressionHeading);

            if (expressionsProperty.arraySize == 0)
            {
                var optionalInfo = new HelpBox(
                    "Expressions and portraits are optional. Add an expression only when this character needs visual variations.",
                    HelpBoxMessageType.Info);
                _editorPane.Add(optionalInfo);
            }
            else
            {
                var grid = new VisualElement();
                grid.AddToClassList(DialogueEditorStyleClasses.ExpressionGrid);
                for (var index = 0; index < expressionsProperty.arraySize; index++)
                {
                    grid.Add(CreateExpressionCard(serializedCharacter, expressionsProperty, index));
                }

                _editorPane.Add(grid);
            }

        }

        private VisualElement CreateDefaultExpressionField(
            SerializedObject serializedCharacter,
            SerializedProperty expressionsProperty,
            SerializedProperty defaultExpressionProperty)
        {
            var names = new List<string> { "None" };
            var ids = new List<string> { string.Empty };
            for (var index = 0; index < expressionsProperty.arraySize; index++)
            {
                var expression = expressionsProperty.GetArrayElementAtIndex(index);
                var name = expression.FindPropertyRelative("_displayName").stringValue;
                var baseChoiceName = string.IsNullOrWhiteSpace(name) ? $"Expression {index + 1}" : name;
                var choiceName = baseChoiceName;
                var suffix = 2;
                while (names.Contains(choiceName))
                {
                    choiceName = $"{baseChoiceName} ({suffix++})";
                }

                names.Add(choiceName);
                ids.Add(GetGuidValue(expression.FindPropertyRelative("_id")));
            }

            var currentId = GetGuidValue(defaultExpressionProperty);
            var selectedIndex = Math.Max(0, ids.IndexOf(currentId));
            var field = new PopupField<string>("Default Expression (Optional)", names, selectedIndex);
            field.RegisterValueChangedCallback(evt =>
            {
                var newIndex = names.IndexOf(evt.newValue);
                Undo.RecordObject(_selectedCharacter, "Set Default Character Expression");
                serializedCharacter.Update();
                SetGuidValue(defaultExpressionProperty, ids[Math.Max(0, newIndex)]);
                serializedCharacter.ApplyModifiedProperties();
                EditorUtility.SetDirty(_selectedCharacter);
                RebuildCharacterList();
                RebuildCharacterEditor();
            });
            return field;
        }

        private VisualElement CreateExpressionCard(
            SerializedObject serializedCharacter,
            SerializedProperty expressionsProperty,
            int index)
        {
            var expression = expressionsProperty.GetArrayElementAtIndex(index);
            var nameProperty = expression.FindPropertyRelative("_displayName");
            var portraitProperty = expression.FindPropertyRelative("_portrait");
            var expressionId = GetGuidValue(expression.FindPropertyRelative("_id"));
            var isDefault = expressionId == _selectedCharacter.defaultExpressionId.value;

            var card = new VisualElement();
            card.AddToClassList(DialogueEditorStyleClasses.ExpressionCard);
            card.EnableInClassList(DialogueEditorStyleClasses.DefaultExpression, isDefault);

            var preview = new Image
            {
                sprite = portraitProperty.objectReferenceValue as Sprite,
                scaleMode = ScaleMode.ScaleToFit
            };
            preview.AddToClassList(DialogueEditorStyleClasses.ExpressionPreview);
            card.Add(preview);

            var nameLabel = new Label("Expression Name");
            nameLabel.AddToClassList(DialogueEditorStyleClasses.ExpressionFieldLabel);
            card.Add(nameLabel);
            var nameField = new TextField { isDelayed = true };
            nameField.SetValueWithoutNotify(nameProperty.stringValue);
            nameField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(_selectedCharacter, "Rename Character Expression");
                serializedCharacter.Update();
                expressionsProperty.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("_displayName").stringValue = evt.newValue;
                serializedCharacter.ApplyModifiedProperties();
                EditorUtility.SetDirty(_selectedCharacter);
                RebuildCharacterList();
                RebuildCharacterEditor();
            });
            card.Add(nameField);

            var portraitField = new ObjectField("Portrait (Optional)")
            {
                objectType = typeof(Sprite),
                allowSceneObjects = false
            };
            portraitField.SetValueWithoutNotify(portraitProperty.objectReferenceValue);
            portraitField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(_selectedCharacter, "Change Character Portrait");
                serializedCharacter.Update();
                expressionsProperty.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("_portrait").objectReferenceValue = evt.newValue;
                serializedCharacter.ApplyModifiedProperties();
                EditorUtility.SetDirty(_selectedCharacter);
                RebuildCharacterList();
                RebuildCharacterEditor();
            });
            card.Add(portraitField);

            if (portraitProperty.objectReferenceValue == null)
            {
                var optionalPortrait = new Label("No portrait — text-only expression");
                optionalPortrait.AddToClassList(DialogueEditorStyleClasses.OptionalHint);
                card.Add(optionalPortrait);
            }

            var cardActions = new VisualElement();
            cardActions.AddToClassList(DialogueEditorStyleClasses.ExpressionActions);
            if (isDefault)
            {
                var defaultLabel = new Label("Default");
                defaultLabel.AddToClassList(DialogueEditorStyleClasses.DefaultExpressionLabel);
                cardActions.Add(defaultLabel);
            }

            var expressionMenu = new Button(() => ShowExpressionMenu(index, isDefault, expressionId))
            {
                text = "•••",
                tooltip = "More expression actions"
            };
            expressionMenu.AddToClassList(DialogueEditorStyleClasses.OverflowAction);
            cardActions.Add(expressionMenu);
            card.Add(cardActions);
            return card;
        }

        private void ShowCharacterMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Duplicate Character"), false, DuplicateCharacter);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Delete Character"), false, DeleteCharacter);
            menu.ShowAsContext();
        }

        private void ShowExpressionMenu(int index, bool isDefault, string expressionId)
        {
            var menu = new GenericMenu();
            if (isDefault)
            {
                menu.AddDisabledItem(new GUIContent("Set as Default"), true);
            }
            else
            {
                menu.AddItem(new GUIContent("Set as Default"), false, () => SetDefaultExpression(expressionId));
            }

            menu.AddItem(new GUIContent("Duplicate Expression"), false, () => DuplicateExpression(index));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Delete Expression"), false, () => DeleteExpression(index));
            menu.ShowAsContext();
        }

        private void SelectCharacter(CharacterAsset character)
        {
            _selectedCharacter = character;
            RebuildCharacterList();
            RebuildCharacterEditor();
        }

        private void CreateCharacter()
        {
            var directory = GetCharactersFolder();
            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{directory}/New Character.asset");
            var character = ScriptableObject.CreateInstance<CharacterAsset>();

            AssetDatabase.CreateAsset(character, assetPath);
            Undo.RegisterCreatedObjectUndo(character, "Create Dialogue Character");
            AddCharacterReference(character, "Add Dialogue Character");
            AssetDatabase.SaveAssetIfDirty(character);
            _selectedCharacter = character;
            RebuildCharacterList();
            RebuildCharacterEditor();
        }

        private void DuplicateCharacter()
        {
            if (_selectedCharacter == null)
            {
                return;
            }

            var sourcePath = AssetDatabase.GetAssetPath(_selectedCharacter);
            var directory = GetCharactersFolder();
            var sourceFileName = Path.GetFileNameWithoutExtension(sourcePath);
            var assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{directory}/{sourceFileName} Copy.asset");
            var duplicate = UnityEngine.Object.Instantiate(_selectedCharacter);
            duplicate.name = $"{_selectedCharacter.name} Copy";

            var serializedDuplicate = new SerializedObject(duplicate);
            serializedDuplicate.FindProperty("_displayName").stringValue = $"{_selectedCharacter.displayName} Copy";
            SetGuidValue(serializedDuplicate.FindProperty("_id"), Guid.NewGuid().ToString("N"));
            RegenerateExpressionIds(serializedDuplicate);
            serializedDuplicate.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(duplicate, assetPath);
            Undo.RegisterCreatedObjectUndo(duplicate, "Duplicate Dialogue Character");
            AddCharacterReference(duplicate, "Add Duplicated Dialogue Character");
            AssetDatabase.SaveAssetIfDirty(duplicate);
            _selectedCharacter = duplicate;
            RebuildCharacterList();
            RebuildCharacterEditor();
        }

        private void DeleteCharacter()
        {
            if (_selectedCharacter == null || !EditorUtility.DisplayDialog(
                    "Delete Character",
                    $"Delete '{_selectedCharacter.displayName}' and its asset? Dialogue lines using it will lose their speaker reference. This cannot be undone.",
                    "Delete",
                    "Cancel"))
            {
                return;
            }

            var character = _selectedCharacter;
            var projectObject = new SerializedObject(_project);
            var characters = projectObject.FindProperty("_characters");
            Undo.RecordObject(_project, "Remove Dialogue Character");
            for (var index = 0; index < characters.arraySize; index++)
            {
                if (characters.GetArrayElementAtIndex(index).objectReferenceValue != character)
                {
                    continue;
                }

                var previousSize = characters.arraySize;
                characters.DeleteArrayElementAtIndex(index);
                if (characters.arraySize == previousSize)
                {
                    characters.DeleteArrayElementAtIndex(index);
                }

                break;
            }

            projectObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(_project);

            var assetPath = AssetDatabase.GetAssetPath(character);
            _selectedCharacter =
                _project.characters.FirstOrDefault(candidate => candidate != null && candidate != character);
            if (!string.IsNullOrEmpty(assetPath))
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            RebuildCharacterList();
            RebuildCharacterEditor();
        }

        private string GetCharactersFolder()
        {
            var projectPath = AssetDatabase.GetAssetPath(_project);
            var projectDirectory = Path.GetDirectoryName(projectPath)?.Replace('\\', '/') ?? "Assets";
            var charactersDirectory = $"{projectDirectory}/Characters";
            if (!AssetDatabase.IsValidFolder(charactersDirectory))
            {
                AssetDatabase.CreateFolder(projectDirectory, "Characters");
            }

            return charactersDirectory;
        }

        private void AddCharacterReference(CharacterAsset character, string undoName)
        {
            Undo.RecordObject(_project, undoName);
            var projectObject = new SerializedObject(_project);
            var characters = projectObject.FindProperty("_characters");
            characters.arraySize++;
            characters.GetArrayElementAtIndex(characters.arraySize - 1).objectReferenceValue = character;
            projectObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(_project);
        }

        private void AddExpression()
        {
            var serializedCharacter = new SerializedObject(_selectedCharacter);
            var expressions = serializedCharacter.FindProperty("_expressions");
            Undo.RecordObject(_selectedCharacter, "Add Character Expression");
            expressions.InsertArrayElementAtIndex(expressions.arraySize);
            var expression = expressions.GetArrayElementAtIndex(expressions.arraySize - 1);
            SetGuidValue(expression.FindPropertyRelative("_id"), Guid.NewGuid().ToString("N"));
            expression.FindPropertyRelative("_displayName").stringValue = "Expression";
            expression.FindPropertyRelative("_portrait").objectReferenceValue = null;

            if (expressions.arraySize == 1)
            {
                SetGuidValue(
                    serializedCharacter.FindProperty("_defaultExpressionId"),
                    GetGuidValue(expression.FindPropertyRelative("_id")));
            }

            serializedCharacter.ApplyModifiedProperties();
            EditorUtility.SetDirty(_selectedCharacter);
            RebuildCharacterList();
            RebuildCharacterEditor();
        }

        private void DuplicateExpression(int index)
        {
            var serializedCharacter = new SerializedObject(_selectedCharacter);
            var expressions = serializedCharacter.FindProperty("_expressions");
            var source = expressions.GetArrayElementAtIndex(index);
            var sourceName = source.FindPropertyRelative("_displayName").stringValue;
            var sourcePortrait = source.FindPropertyRelative("_portrait").objectReferenceValue;
            Undo.RecordObject(_selectedCharacter, "Duplicate Character Expression");
            expressions.InsertArrayElementAtIndex(expressions.arraySize);
            var duplicate = expressions.GetArrayElementAtIndex(expressions.arraySize - 1);
            SetGuidValue(duplicate.FindPropertyRelative("_id"), Guid.NewGuid().ToString("N"));
            duplicate.FindPropertyRelative("_displayName").stringValue = $"{sourceName} Copy";
            duplicate.FindPropertyRelative("_portrait").objectReferenceValue = sourcePortrait;
            serializedCharacter.ApplyModifiedProperties();
            EditorUtility.SetDirty(_selectedCharacter);
            RebuildCharacterList();
            RebuildCharacterEditor();
        }

        private void DeleteExpression(int index)
        {
            var serializedCharacter = new SerializedObject(_selectedCharacter);
            var expressions = serializedCharacter.FindProperty("_expressions");
            var deletedId = GetGuidValue(
                expressions.GetArrayElementAtIndex(index).FindPropertyRelative("_id"));
            Undo.RecordObject(_selectedCharacter, "Delete Character Expression");
            expressions.DeleteArrayElementAtIndex(index);

            var defaultExpression = serializedCharacter.FindProperty("_defaultExpressionId");
            if (GetGuidValue(defaultExpression) == deletedId)
            {
                var replacementId = expressions.arraySize > 0
                    ? GetGuidValue(expressions.GetArrayElementAtIndex(0).FindPropertyRelative("_id"))
                    : string.Empty;
                SetGuidValue(defaultExpression, replacementId);
            }

            serializedCharacter.ApplyModifiedProperties();
            EditorUtility.SetDirty(_selectedCharacter);
            RebuildCharacterList();
            RebuildCharacterEditor();
        }

        private void SetDefaultExpression(string expressionId)
        {
            var serializedCharacter = new SerializedObject(_selectedCharacter);
            Undo.RecordObject(_selectedCharacter, "Set Default Character Expression");
            SetGuidValue(serializedCharacter.FindProperty("_defaultExpressionId"), expressionId);
            serializedCharacter.ApplyModifiedProperties();
            EditorUtility.SetDirty(_selectedCharacter);
            RebuildCharacterList();
            RebuildCharacterEditor();
        }

        private int CountCharacterUsages(CharacterAsset character)
        {
            var count = 0;
            foreach (var conversation in _project.conversations.Where(conversation => conversation != null))
            {
                foreach (var passage in conversation.passages.Where(passage => passage != null))
                {
                    count += CountCharacterUsages(passage.elements, character);
                }
            }

            return count;
        }

        private static int CountCharacterUsages(
            IReadOnlyList<DialogueElement> elements,
            CharacterAsset character)
        {
            var count = 0;
            foreach (var element in elements)
            {
                if (element is LineElement line && line.speaker == character)
                {
                    count++;
                }

                if (!(element is ConditionalElement conditional))
                {
                    continue;
                }

                foreach (var branch in conditional.branches)
                {
                    count += CountCharacterUsages(branch.elements, character);
                }

                count += CountCharacterUsages(conditional.elseElements, character);
            }

            return count;
        }

        private static Sprite GetDefaultPortrait(CharacterAsset character)
        {
            var defaultExpression = character.expressions.FirstOrDefault(expression =>
                expression != null && expression.id == character.defaultExpressionId);
            return defaultExpression?.portrait ?? character.expressions
                .FirstOrDefault(expression => expression?.portrait != null)?.portrait;
        }

        private static bool HasCharacterWarning(CharacterAsset character)
        {
            return !string.IsNullOrEmpty(GetCharacterWarning(character));
        }

        private static string GetCharacterWarning(CharacterAsset character)
        {
            if (string.IsNullOrWhiteSpace(character.displayName))
            {
                return "Character name is empty.";
            }

            if (character.expressions.Count > 0
                && character.defaultExpressionId.isValid
                && !character.expressions.Any(expression =>
                    expression != null && expression.id == character.defaultExpressionId))
            {
                return "Default expression is missing.";
            }

            var duplicateName = character.expressions
                .Where(expression => expression != null)
                .GroupBy(expression => expression.displayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() > 1);
            return duplicateName ? "Two or more expressions have the same name." : string.Empty;
        }

        private static string GetGuidValue(SerializedProperty guidProperty)
        {
            return guidProperty.FindPropertyRelative("_value")?.stringValue ?? string.Empty;
        }

        private static void SetGuidValue(SerializedProperty guidProperty, string value)
        {
            var valueProperty = guidProperty.FindPropertyRelative("_value");
            if (valueProperty != null)
            {
                valueProperty.stringValue = value;
            }
        }

        private static void RegenerateExpressionIds(SerializedObject serializedCharacter)
        {
            var expressions = serializedCharacter.FindProperty("_expressions");
            var oldDefaultId = GetGuidValue(serializedCharacter.FindProperty("_defaultExpressionId"));
            var newDefaultId = string.Empty;
            for (var index = 0; index < expressions.arraySize; index++)
            {
                var expressionId = expressions.GetArrayElementAtIndex(index).FindPropertyRelative("_id");
                var oldId = GetGuidValue(expressionId);
                var newId = Guid.NewGuid().ToString("N");
                SetGuidValue(expressionId, newId);
                if (oldId == oldDefaultId)
                {
                    newDefaultId = newId;
                }
            }

            SetGuidValue(serializedCharacter.FindProperty("_defaultExpressionId"), newDefaultId);
        }
    }
}
