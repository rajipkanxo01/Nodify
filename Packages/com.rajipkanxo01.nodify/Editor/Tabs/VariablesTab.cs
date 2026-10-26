using System;
using System.Collections.Generic;
using System.Linq;
using ToolForDialogue._Project.Dialogue.Runtime.Assets;
using ToolForDialogue._Project.Dialogue.Runtime.Types;
using ToolForDialogue.Editor._Project.Scripts.Editor.Services;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ToolForDialogue.Editor._Project.Scripts.Editor.Tabs
{
    /// <summary>
    /// Authors project variables with boolean and integer defaults.
    /// </summary>
    internal sealed class VariablesTab : DialogueEditorTabBase
    {
        private DialogueProjectAsset _project;
        private ScrollView _rows;
        private string _searchQuery = string.Empty;
        private static readonly List<string> TypeNames = new List<string> { "Boolean", "Integer" };

        public override DialogueEditorTabId id => DialogueEditorTabId.Variables;
        public override string label => "Variables";
        protected override bool isWritingWorkspace => true;
        protected override void BuildContent(
            VisualElement container,
            DialogueEditorTabContext context)
        {
            _project = context.selectedProject;
            container.AddToClassList(DialogueEditorStyleClasses.VariablesWorkspace);
            var description = new Label("Project variables and their starting values.");
            description.AddToClassList(DialogueEditorStyleClasses.PanelDescription);
            container.Add(description);

            var toolbar = new VisualElement();
            toolbar.AddToClassList(DialogueEditorStyleClasses.VariablesToolbar);
            var search = new ToolbarSearchField();
            search.AddToClassList(DialogueEditorStyleClasses.VariablesSearch);
            search.SetValueWithoutNotify(_searchQuery);
            search.RegisterValueChangedCallback(evt =>
            {
                _searchQuery = evt.newValue ?? string.Empty;
                RebuildRows();
            });
            toolbar.Add(search);
            var add = new ToolbarMenu { text = "+ Variable", tooltip = "Add a project variable" };
            add.menu.AppendAction("Boolean", _ => AddVariable(VariableValueType.Boolean, search));
            add.menu.AppendAction("Integer", _ => AddVariable(VariableValueType.Integer, search));
            toolbar.Add(add);
            container.Add(toolbar);

            var headings = new VisualElement();
            headings.AddToClassList(DialogueEditorStyleClasses.VariableRow);
            headings.AddToClassList(DialogueEditorStyleClasses.VariableHeadings);
            headings.Add(CreateHeading("Name", DialogueEditorStyleClasses.VariableName));
            headings.Add(CreateHeading("Type", DialogueEditorStyleClasses.VariableType));
            headings.Add(CreateHeading("Default value", DialogueEditorStyleClasses.VariableDefault));
            headings.Add(CreateHeading(string.Empty, DialogueEditorStyleClasses.VariableDelete));
            container.Add(headings);
            _rows = new ScrollView(ScrollViewMode.Vertical);
            _rows.AddToClassList(DialogueEditorStyleClasses.VariablesRows);
            container.Add(_rows);

            container.RegisterCallback<AttachToPanelEvent>(_ => Undo.undoRedoPerformed += OnUndoRedo);
            container.RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= OnUndoRedo);
            RebuildRows();
        }

        private static Label CreateHeading(string text, string className)
        {
            var heading = new Label(text);
            heading.AddToClassList(className);
            return heading;
        }

        private void OnUndoRedo()
        {
            if (_project == null) return;
            ConversationYarnGenerator.ScheduleAll(_project);
            RebuildRows();
        }

        private void RebuildRows()
        {
            _rows.Clear();
            var variables = _project.variables.Where(variable => variable != null)
                .Where(variable => string.IsNullOrWhiteSpace(_searchQuery)
                    || (variable.displayName ?? string.Empty).IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            if (variables.Count == 0)
            {
                var empty = new Label(_project.variables.Any(variable => variable != null)
                    ? "No variables match this search."
                    : "No variables yet. Add a Boolean or Integer to get started.");
                empty.AddToClassList(DialogueEditorStyleClasses.CharacterListEmpty);
                _rows.Add(empty);
            }
            foreach (var variable in variables) _rows.Add(CreateRow(variable));
        }

        private VisualElement CreateRow(VariableDefinition variable)
        {
            var id = variable.id.value;
            var entry = new VisualElement();
            var row = new VisualElement();
            row.AddToClassList(DialogueEditorStyleClasses.VariableRow);
            var error = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            void ShowNameError(string message)
            {
                error.text = message ?? string.Empty;
                error.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            var name = new TextField { isDelayed = true, name = "variable-name-" + id,
                tooltip = "Variable name · Yarn: " + VariableYarnUtility.Symbol(_project, variable) };
            name.AddToClassList(DialogueEditorStyleClasses.VariableName);
            name.SetValueWithoutNotify(variable.displayName);
            name.RegisterValueChangedCallback(evt =>
            {
                var value = (evt.newValue ?? string.Empty).Trim();
                var message = GetNameError(value, id);
                ShowNameError(message);
                if (message != null)
                {
                    name.SetValueWithoutNotify(variable.displayName);
                    return;
                }
                EditVariable(id, "Rename Dialogue Variable", property => property.FindPropertyRelative("_displayName").stringValue = value);
                name.SetValueWithoutNotify(value);
                name.tooltip = "Variable name · Yarn: " + VariableYarnUtility.Symbol(_project, variable);
            });
            row.Add(name);

            var supported = variable.type == VariableValueType.Boolean || variable.type == VariableValueType.Integer;
            if (supported)
            {
                var type = new PopupField<string>(TypeNames, variable.type == VariableValueType.Boolean ? 0 : 1)
                    { tooltip = "Variable type" };
                type.AddToClassList(DialogueEditorStyleClasses.VariableType);
                type.RegisterValueChangedCallback(evt =>
                {
                    var target = evt.newValue == "Boolean" ? VariableValueType.Boolean : VariableValueType.Integer;
                    EditVariable(id, "Change Dialogue Variable Type", property =>
                    {
                        var oldType = (VariableValueType)property.FindPropertyRelative("_type").enumValueIndex;
                        var value = property.FindPropertyRelative("_defaultValue");
                        if (target == VariableValueType.Boolean && oldType == VariableValueType.Integer)
                            value.FindPropertyRelative("_booleanValue").boolValue = value.FindPropertyRelative("_integerValue").intValue != 0;
                        if (target == VariableValueType.Integer && oldType == VariableValueType.Boolean)
                            value.FindPropertyRelative("_integerValue").intValue = value.FindPropertyRelative("_booleanValue").boolValue ? 1 : 0;
                        property.FindPropertyRelative("_type").enumValueIndex = (int)target;
                        value.FindPropertyRelative("_type").enumValueIndex = (int)(target == VariableValueType.Boolean
                            ? DialogueValueType.Boolean : DialogueValueType.Integer);
                    });
                    RebuildRows();
                });
                row.Add(type);
                if (variable.type == VariableValueType.Boolean)
                {
                    var value = new Toggle { text = variable.defaultValue.booleanValue ? "True" : "False", tooltip = "Default boolean value" };
                    value.SetValueWithoutNotify(variable.defaultValue.booleanValue);
                    value.AddToClassList(DialogueEditorStyleClasses.VariableDefault);
                    value.RegisterValueChangedCallback(evt =>
                    {
                        EditVariable(id, "Change Variable Default", property =>
                        {
                            var defaults = property.FindPropertyRelative("_defaultValue");
                            defaults.FindPropertyRelative("_type").enumValueIndex = (int)DialogueValueType.Boolean;
                            defaults.FindPropertyRelative("_booleanValue").boolValue = evt.newValue;
                        });
                        value.text = evt.newValue ? "True" : "False";
                    });
                    row.Add(value);
                }
                else
                {
                    var value = new IntegerField { isDelayed = true, tooltip = "Default integer value" };
                    value.SetValueWithoutNotify(variable.defaultValue.integerValue);
                    value.AddToClassList(DialogueEditorStyleClasses.VariableDefault);
                    value.RegisterValueChangedCallback(evt => EditVariable(id, "Change Variable Default", property =>
                    {
                        var defaults = property.FindPropertyRelative("_defaultValue");
                        defaults.FindPropertyRelative("_type").enumValueIndex = (int)DialogueValueType.Integer;
                        defaults.FindPropertyRelative("_integerValue").intValue = evt.newValue;
                    }));
                    row.Add(value);
                }
            }
            else
            {
                row.Add(CreateHeading(variable.type.ToString(), DialogueEditorStyleClasses.VariableType));
                row.Add(CreateHeading("Not editable yet", DialogueEditorStyleClasses.VariableDefault));
            }
            var remove = new Button(() => RemoveVariable(id)) { text = "×", tooltip = "Delete variable (Undo available)" };
            remove.AddToClassList(DialogueEditorStyleClasses.VariableDelete);
            row.Add(remove);
            entry.Add(row);
            ShowNameError(GetNameError(variable.displayName, id));
            entry.Add(error);
            return entry;
        }

        private string GetNameError(string name, string id)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Enter a variable name.";
            return _project.variables.Any(variable => variable != null && variable.id.value != id
                && string.Equals(VariableYarnUtility.VariableName(variable.displayName), VariableYarnUtility.VariableName(name), StringComparison.OrdinalIgnoreCase))
                ? "A variable with this Yarn name already exists." : null;
        }

        private void EditVariable(string id, string undoName, Action<SerializedProperty> edit)
        {
            var serialized = new SerializedObject(_project);
            var variables = serialized.FindProperty("_variables");
            for (var index = 0; index < variables.arraySize; index++)
            {
                var variable = variables.GetArrayElementAtIndex(index);
                if (variable.FindPropertyRelative("_id").FindPropertyRelative("_value").stringValue != id) continue;
                Undo.RecordObject(_project, undoName);
                edit(variable);
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(_project);
                ConversationYarnGenerator.ScheduleAll(_project);
                return;
            }
        }

        private void AddVariable(VariableValueType type, ToolbarSearchField search)
        {
            var baseName = type == VariableValueType.Boolean ? "new_boolean" : "new_integer";
            var name = baseName;
            for (var suffix = 2; GetNameError(name, null) != null; suffix++) name = baseName + "_" + suffix;
            var serialized = new SerializedObject(_project);
            var variables = serialized.FindProperty("_variables");
            Undo.RecordObject(_project, "Add Dialogue Variable");
            variables.InsertArrayElementAtIndex(variables.arraySize);
            var variable = variables.GetArrayElementAtIndex(variables.arraySize - 1);
            var id = Guid.NewGuid().ToString("N");
            variable.FindPropertyRelative("_id").FindPropertyRelative("_value").stringValue = id;
            variable.FindPropertyRelative("_displayName").stringValue = name;
            variable.FindPropertyRelative("_type").enumValueIndex = (int)type;
            var defaults = variable.FindPropertyRelative("_defaultValue");
            defaults.FindPropertyRelative("_type").enumValueIndex = (int)(type == VariableValueType.Boolean
                ? DialogueValueType.Boolean : DialogueValueType.Integer);
            defaults.FindPropertyRelative("_booleanValue").boolValue = false;
            defaults.FindPropertyRelative("_integerValue").intValue = 0;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_project);
            ConversationYarnGenerator.ScheduleAll(_project);
            _searchQuery = string.Empty;
            search.SetValueWithoutNotify(string.Empty);
            RebuildRows();
            _rows.schedule.Execute(() =>
            {
                var field = _rows.Q<TextField>("variable-name-" + id);
                if (field == null) return;
                _rows.ScrollTo(field);
                field.Focus();
                field.SelectAll();
            });
        }

        private void RemoveVariable(string id)
        {
            var serialized = new SerializedObject(_project);
            var variables = serialized.FindProperty("_variables");
            for (var index = 0; index < variables.arraySize; index++)
            {
                if (variables.GetArrayElementAtIndex(index).FindPropertyRelative("_id").FindPropertyRelative("_value").stringValue != id) continue;
                Undo.RecordObject(_project, "Delete Dialogue Variable");
                variables.DeleteArrayElementAtIndex(index);
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(_project);
                ConversationYarnGenerator.ScheduleAll(_project);
                RebuildRows();
                return;
            }
        }
    }
}
