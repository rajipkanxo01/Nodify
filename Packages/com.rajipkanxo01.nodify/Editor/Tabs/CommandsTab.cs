using System;
using System.Collections.Generic;
using System.Linq;
using ToolForDialogue.Assets;
using ToolForDialogue.Editor.Editor.Services;
using ToolForDialogue.Types;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ToolForDialogue.Editor.Editor.Tabs
{
    internal sealed class CommandsTab : DialogueEditorTabBase
    {
        private DialogueProjectAsset _project;
        private ScrollView _rows;
        private string _search = string.Empty;
        internal static readonly List<string> TypeNames = new() { "String", "Boolean", "Integer", "Number" };
        internal static readonly DialogueValueType[] Types =
            { DialogueValueType.String, DialogueValueType.Boolean, DialogueValueType.Integer, DialogueValueType.Number };

        public override DialogueEditorTabId id => DialogueEditorTabId.Commands;
        public override string label => "Commands";
        protected override bool isWritingWorkspace => true;

        protected override void BuildContent(VisualElement container, DialogueEditorTabContext context)
        {
            _project = context.selectedProject;
            container.AddToClassList(DialogueEditorStyleClasses.VariablesWorkspace);
            container.Add(new HelpBox("Define Yarn command names and ordered parameters here. Your game registers the matching handlers on its Dialogue Runner. A coroutine handler pauses dialogue until it finishes.", HelpBoxMessageType.Info));
            var toolbar = new VisualElement();
            toolbar.AddToClassList(DialogueEditorStyleClasses.VariablesToolbar);
            var search = new ToolbarSearchField();
            search.AddToClassList(DialogueEditorStyleClasses.VariablesSearch);
            search.SetValueWithoutNotify(_search);
            search.RegisterValueChangedCallback(evt => { _search = evt.newValue; Rebuild(); });
            toolbar.Add(search);
            toolbar.Add(new Button(() => Edit("Add Command", commands =>
            {
                var names = _project.commands.Where(item => item != null).Select(item => item.name).ToList();
                var name = "new_command";
                for (var suffix = 2; names.Contains(name); suffix++) name = "new_command_" + suffix;
                commands.arraySize++;
                var command = commands.GetArrayElementAtIndex(commands.arraySize - 1);
                SetId(command);
                command.FindPropertyRelative("_name").stringValue = name;
                command.FindPropertyRelative("_parameters").ClearArray();
                _search = string.Empty;
                search.SetValueWithoutNotify(string.Empty);
            })) { text = "+ Command" });
            container.Add(toolbar);
            _rows = new ScrollView();
            _rows.style.flexGrow = 1;
            container.Add(_rows);
            container.RegisterCallback<AttachToPanelEvent>(_ => Undo.undoRedoPerformed += OnUndoRedo);
            container.RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= OnUndoRedo);
            Rebuild();
        }

        private void OnUndoRedo() { ConversationYarnGenerator.ScheduleAll(_project); Rebuild(); }

        private void Edit(string undoName, Action<SerializedProperty> edit)
        {
            var serialized = new SerializedObject(_project);
            Undo.RecordObject(_project, undoName);
            edit(serialized.FindProperty("_commands"));
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_project);
            ConversationYarnGenerator.ScheduleAll(_project);
            Rebuild();
        }

        private void EditCommand(string id, Action<SerializedProperty> edit) => Edit("Edit Command", commands =>
        {
            for (var index = 0; index < commands.arraySize; index++)
            {
                var command = commands.GetArrayElementAtIndex(index);
                if (command.FindPropertyRelative("_id").FindPropertyRelative("_value").stringValue == id)
                { edit(command); return; }
            }
        });

        private static void SetId(SerializedProperty property) =>
            property.FindPropertyRelative("_id").FindPropertyRelative("_value").stringValue = Guid.NewGuid().ToString("N");

        private void Rebuild()
        {
            _rows.Clear();
            foreach (var command in _project.commands.Where(item => item != null &&
                (string.IsNullOrEmpty(_search) || (item.name ?? string.Empty).IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)))
            {
                var id = command.id.value;
                var card = new VisualElement();
                card.AddToClassList(DialogueEditorStyleClasses.ConversationElementCard);
                var name = new TextField("Command name") { isDelayed = true, value = command.name };
                name.RegisterValueChangedCallback(evt => EditCommand(id, property =>
                    property.FindPropertyRelative("_name").stringValue = (evt.newValue ?? string.Empty).Trim()));
                card.Add(name);
                var error = CommandYarnUtility.DefinitionError(_project, command);
                if (error != null) card.Add(new HelpBox(error, HelpBoxMessageType.Warning));
                card.Add(new Label("Parameters · passed to the handler in this order"));
                for (var index = 0; index < command.parameters.Count; index++)
                {
                    var captured = index;
                    var parameter = command.parameters[index];
                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    var parameterName = new TextField { isDelayed = true, value = parameter.name, tooltip = "Parameter name" };
                    parameterName.style.flexGrow = 1;
                    parameterName.RegisterValueChangedCallback(evt => EditCommand(id, property =>
                        property.FindPropertyRelative("_parameters").GetArrayElementAtIndex(captured)
                            .FindPropertyRelative("_name").stringValue = (evt.newValue ?? string.Empty).Trim()));
                    row.Add(parameterName);
                    var type = new PopupField<string>(TypeNames, Math.Max(0, Array.IndexOf(Types, parameter.type)));
                    type.RegisterValueChangedCallback(evt => EditCommand(id, property =>
                        property.FindPropertyRelative("_parameters").GetArrayElementAtIndex(captured)
                            .FindPropertyRelative("_type").enumValueIndex = (int)Types[TypeNames.IndexOf(evt.newValue)]));
                    row.Add(type);
                    var up = new Button(() => EditCommand(id, property =>
                        property.FindPropertyRelative("_parameters").MoveArrayElement(captured, captured - 1))) { text = "↑", tooltip = "Move parameter up" };
                    up.SetEnabled(index > 0);
                    row.Add(up);
                    var down = new Button(() => EditCommand(id, property =>
                        property.FindPropertyRelative("_parameters").MoveArrayElement(captured, captured + 1))) { text = "↓", tooltip = "Move parameter down" };
                    down.SetEnabled(index < command.parameters.Count - 1);
                    row.Add(down);
                    row.Add(new Button(() => EditCommand(id, property =>
                        property.FindPropertyRelative("_parameters").DeleteArrayElementAtIndex(captured))) { text = "×", tooltip = "Delete parameter" });
                    card.Add(row);
                }
                card.Add(new Button(() => EditCommand(id, property =>
                {
                    var parameters = property.FindPropertyRelative("_parameters");
                    var names = command.parameters.Select(item => item.name).ToList();
                    var name = "argument";
                    for (var suffix = 2; names.Contains(name); suffix++) name = "argument_" + suffix;
                    parameters.arraySize++;
                    var parameter = parameters.GetArrayElementAtIndex(parameters.arraySize - 1);
                    SetId(parameter);
                    parameter.FindPropertyRelative("_name").stringValue = name;
                    parameter.FindPropertyRelative("_type").enumValueIndex = (int)DialogueValueType.String;
                })) { text = "+ Parameter" });
                card.Add(new Button(() => Edit("Delete Command", commands =>
                {
                    for (var index = 0; index < commands.arraySize; index++)
                        if (commands.GetArrayElementAtIndex(index).FindPropertyRelative("_id").FindPropertyRelative("_value").stringValue == id)
                        { commands.DeleteArrayElementAtIndex(index); break; }
                })) { text = "Delete command", tooltip = "Existing uses will report a missing command. Undo available." });
                _rows.Add(card);
            }
            if (_rows.childCount == 0) _rows.Add(new Label("No commands to show. Use + Command to define one."));
        }
    }
}
