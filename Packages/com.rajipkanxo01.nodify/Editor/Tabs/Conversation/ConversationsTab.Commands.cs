using System;
using System.Linq;
using ToolForDialogue.Editor.Editor.Services;
using ToolForDialogue.Types;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ToolForDialogue.Editor.Editor.Tabs.Conversation
{
    internal sealed partial class ConversationsTab
    {
        private void AddInlineCommand(string passageId, int lineIndex) =>
            AddElementToPassage(passageId, new ActionElement(), "Add Command", lineIndex);

        private void InitializeCommand(SerializedProperty element, CommandDefinition command)
        {
            element.FindPropertyRelative("_commandProject").objectReferenceValue = _project;
            element.FindPropertyRelative("_commandId").stringValue = command?.id.value ?? string.Empty;
            var arguments = element.FindPropertyRelative("_arguments");
            arguments.ClearArray();
            if (command == null) return;
            foreach (var parameter in command.parameters)
            {
                arguments.arraySize++;
                var argument = arguments.GetArrayElementAtIndex(arguments.arraySize - 1);
                argument.FindPropertyRelative("_parameterId").stringValue = parameter.id.value;
                var value = argument.FindPropertyRelative("_value");
                value.FindPropertyRelative("_type").enumValueIndex = (int)parameter.type;
                value.FindPropertyRelative("_stringValue").stringValue = string.Empty;
                value.FindPropertyRelative("_booleanValue").boolValue = false;
                value.FindPropertyRelative("_integerValue").intValue = 0;
                value.FindPropertyRelative("_numberValue").floatValue = 0;
            }
        }

        private VisualElement CreateCommandEditor(string passageId, int elementIndex)
        {
            var passage = _selectedConversation.passages[FindPassageIndex(passageId)];
            var action = (ActionElement)passage.elements[elementIndex];
            var card = new VisualElement();
            card.AddToClassList(DialogueEditorStyleClasses.ConversationVariableBlock);
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            var title = new Label("Command");
            title.style.flexGrow = 1;
            header.Add(title);
            var menu = new ToolbarMenu { text = "···" };
            menu.menu.AppendAction("Move up", _ => MoveInlineElement(passageId, elementIndex, elementIndex - 1),
                _ => elementIndex > 0 && passage.elements[elementIndex - 1] is SetVariableElement or ActionElement
                    ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.menu.AppendAction("Move down", _ => MoveInlineElement(passageId, elementIndex, elementIndex + 1),
                _ => elementIndex + 1 < passage.elements.Count && passage.elements[elementIndex + 1] is SetVariableElement or ActionElement
                    ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.menu.AppendAction("Duplicate", _ => DuplicateInlineElement(passageId, elementIndex));
            menu.menu.AppendAction("Delete", _ => DeleteInlineElement(passageId, elementIndex));
            header.Add(menu);
            card.Add(header);
            var commands = _project.commands.Where(item => item != null).ToList();
            var current = CommandYarnUtility.Resolve(action);
            var names = commands.Select(item => item.name).ToList();
            // Invalid duplicate definitions still need distinguishable picker entries.
            var labels = new System.Collections.Generic.List<string> { "Choose command" };
            foreach (var name in names) labels.Add(CreateUniqueChoiceName(labels, name));
            var selected = action.commandProject == _project ? commands.IndexOf(current) + 1 : 0;
            if (selected == 0 && !string.IsNullOrEmpty(action.commandId))
            { selected = labels.Count; labels.Add("Missing command"); }
            var picker = new PopupField<string>("Command", labels, selected);
            picker.RegisterValueChangedCallback(evt =>
            {
                var index = labels.IndexOf(evt.newValue) - 1;
                if (index >= commands.Count) return;
                EditVariableElement(passageId, elementIndex, "Choose Command",
                    element => InitializeCommand(element, index < 0 ? null : commands[index]), true);
            });
            card.Add(picker);
            if (commands.Count == 0) card.Add(new HelpBox("Define a command in the Commands tab first.", HelpBoxMessageType.Info));
            if (current != null)
            {
                foreach (var parameter in current.parameters.Where(item => item != null))
                {
                    var argument = action.arguments.FirstOrDefault(item => item != null && item.parameterId == parameter.id.value);
                    void Change(Action<SerializedProperty> edit)
                    {
                        EditVariableElement(passageId, elementIndex, "Edit Command Argument", element =>
                        {
                            var arguments = element.FindPropertyRelative("_arguments");
                            SerializedProperty target = null;
                            for (var index = 0; index < arguments.arraySize; index++)
                            {
                                var item = arguments.GetArrayElementAtIndex(index);
                                if (item.FindPropertyRelative("_parameterId").stringValue == parameter.id.value) { target = item; break; }
                            }
                            if (target == null)
                            {
                                arguments.arraySize++;
                                target = arguments.GetArrayElementAtIndex(arguments.arraySize - 1);
                                target.FindPropertyRelative("_parameterId").stringValue = parameter.id.value;
                                // Unity duplicates the last serialized entry when growing an array.
                                var initial = target.FindPropertyRelative("_value");
                                initial.FindPropertyRelative("_stringValue").stringValue = string.Empty;
                                initial.FindPropertyRelative("_booleanValue").boolValue = false;
                                initial.FindPropertyRelative("_integerValue").intValue = 0;
                                initial.FindPropertyRelative("_numberValue").floatValue = 0;
                            }
                            var value = target.FindPropertyRelative("_value");
                            value.FindPropertyRelative("_type").enumValueIndex = (int)parameter.type;
                            edit(value);
                        }, true);
                    }
                    var value = argument?.value;
                    switch (parameter.type)
                    {
                        case DialogueValueType.String:
                            var text = new TextField(parameter.name) { isDelayed = true, value = value?.stringValue ?? string.Empty };
                            text.RegisterValueChangedCallback(evt => Change(property => property.FindPropertyRelative("_stringValue").stringValue = evt.newValue));
                            card.Add(text); break;
                        case DialogueValueType.Boolean:
                            var toggle = new Toggle(parameter.name) { value = value?.booleanValue ?? false };
                            toggle.RegisterValueChangedCallback(evt => Change(property => property.FindPropertyRelative("_booleanValue").boolValue = evt.newValue));
                            card.Add(toggle); break;
                        case DialogueValueType.Integer:
                            var integer = new IntegerField(parameter.name) { isDelayed = true, value = value?.integerValue ?? 0 };
                            integer.RegisterValueChangedCallback(evt => Change(property => property.FindPropertyRelative("_integerValue").intValue = evt.newValue));
                            card.Add(integer); break;
                        case DialogueValueType.Number:
                            var number = new FloatField(parameter.name) { isDelayed = true, value = value?.numberValue ?? 0 };
                            number.RegisterValueChangedCallback(evt => Change(property => property.FindPropertyRelative("_numberValue").floatValue = evt.newValue));
                            card.Add(number); break;
                    }
                    if (argument == null || value?.type != parameter.type)
                        card.Add(new Button(() => Change(_ => { })) { text = "Confirm " + parameter.name + " value" });
                }
            }
            if (CommandYarnUtility.TryWrite(action, out var source, out var error)) card.Add(new Label(source));
            else card.Add(new HelpBox(error, HelpBoxMessageType.Warning));
            return card;
        }
    }
}
