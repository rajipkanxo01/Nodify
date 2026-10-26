using System;
using System.Collections.Generic;
using System.Linq;
using ToolForDialogue._Project.Dialogue.Runtime.Assets;
using ToolForDialogue._Project.Dialogue.Runtime.Conditions;
using ToolForDialogue._Project.Dialogue.Runtime.Types;
using ToolForDialogue.Editor._Project.Scripts.Editor.Services;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ToolForDialogue.Editor._Project.Scripts.Editor.Tabs.Conversation
{
    internal sealed partial class ConversationsTab
    {
        private readonly HashSet<string> _expandedChoiceConditions = new HashSet<string>();
        private static readonly List<string> ComparisonLabels = new List<string> { "=", "≠", ">", "≥", "<", "≤" };

        private List<VariableDefinition> EditableVariables() => _project.variables.Where(VariableYarnUtility.IsSupported).ToList();

        private void SetVariableReference(SerializedProperty reference, VariableDefinition variable)
        {
            reference.FindPropertyRelative("_scope").enumValueIndex = (int)VariableScope.Project;
            reference.FindPropertyRelative("_project").objectReferenceValue = _project;
            reference.FindPropertyRelative("_conversation").objectReferenceValue = null;
            SetGuidValue(reference.FindPropertyRelative("_variableId"), variable.id.value);
        }

        private void InitializeAssignment(SerializedProperty element, VariableDefinition variable)
        {
            SetVariableReference(element.FindPropertyRelative("_variable"), variable);
            element.FindPropertyRelative("_operation").enumValueIndex = (int)VariableAssignmentOperation.Set;
            var value = element.FindPropertyRelative("_value");
            value.FindPropertyRelative("_type").enumValueIndex = (int)(variable.type == VariableValueType.Boolean
                ? DialogueValueType.Boolean : DialogueValueType.Integer);
            value.FindPropertyRelative("_booleanValue").boolValue = variable.defaultValue.booleanValue;
            value.FindPropertyRelative("_integerValue").intValue = variable.defaultValue.integerValue;
        }

        private static int LineGroupEnd(IReadOnlyList<DialogueElement> elements, int lineIndex)
        {
            var end = lineIndex + 1;
            while (end < elements.Count && elements[end] is SetVariableElement) end++;
            return end;
        }

        private static int AssignmentOwner(IReadOnlyList<DialogueElement> elements, int assignmentIndex)
        {
            var index = assignmentIndex - 1;
            while (index >= 0 && elements[index] is SetVariableElement) index--;
            return index >= 0 && elements[index] is LineElement ? index : -1;
        }

        private void AddInlineVariable(string passageId, int lineIndex)
        {
            AddElementToPassage(passageId, new SetVariableElement(), "Add Line Variable", lineIndex);
        }

        private void EditVariableElement(string passageId, int elementIndex, string undoName,
            Action<SerializedProperty> edit, bool rebuild = false)
        {
            var serialized = new SerializedObject(_selectedConversation);
            var element = GetElement(serialized, passageId, elementIndex);
            if (element == null) return;
            Undo.RecordObject(_selectedConversation, undoName);
            edit(element);
            serialized.ApplyModifiedProperties();
            MarkConversationChanged();
            if (rebuild)
            {
                RebuildGraph();
                if (_selectedPassageId == passageId) RebuildInspector();
            }
        }

        private VisualElement CreateVariableEditor(string passageId, int elementIndex)
        {
            var passage = _selectedConversation.passages[FindPassageIndex(passageId)];
            var assignment = passage.elements[elementIndex] as SetVariableElement;
            var card = new VisualElement();
            card.AddToClassList(DialogueEditorStyleClasses.ConversationVariableBlock);
            if (assignment == null) return card;
            var heading = new VisualElement();
            heading.AddToClassList(DialogueEditorStyleClasses.ConversationInspectorHeader);
            var owner = AssignmentOwner(passage.elements, elementIndex);
            card.tooltip = owner >= 0 ? "Runs after: " + ((LineElement)passage.elements[owner]).text
                : "Runs before the first dialogue line";
            var actions = new ToolbarMenu { text = "···", tooltip = "Variable assignment actions" };
            actions.AddToClassList(DialogueEditorStyleClasses.ConversationInlineMenu);
            actions.menu.AppendAction("Move up", _ => MoveInlineElement(passageId, elementIndex, elementIndex - 1),
                _ => elementIndex > 0 && passage.elements[elementIndex - 1] is SetVariableElement
                    ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            actions.menu.AppendAction("Move down", _ => MoveInlineElement(passageId, elementIndex, elementIndex + 1),
                _ => elementIndex + 1 < passage.elements.Count
                    && passage.elements[elementIndex + 1] is SetVariableElement
                    ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            actions.menu.AppendAction("Delete assignment", _ => DeleteInlineElement(passageId, elementIndex));
            heading.Add(actions);
            var variables = EditableVariables();
            var current = VariableYarnUtility.Resolve(assignment.variable);
            var names = UniqueVariableNames(variables);
            var selected = variables.IndexOf(current);
            if (selected < 0) { selected = names.Count; names.Add("Choose a variable"); }
            var picker = new PopupField<string>("Variable", names, selected);
            picker.SetEnabled(variables.Count > 0);
            picker.RegisterValueChangedCallback(evt =>
            {
                var index = names.IndexOf(evt.newValue);
                if (index < 0 || index >= variables.Count) return;
                EditVariableElement(passageId, elementIndex, "Change Assignment Variable",
                    property => InitializeAssignment(property, variables[index]), true);
            });
            picker.style.flexGrow = 1;
            picker.style.flexShrink = 1;
            picker.style.minWidth = 0;
            heading.Insert(0, picker);
            card.Add(heading);
            if (!VariableYarnUtility.IsSupported(current))
            {
                card.Add(new HelpBox("Choose a Boolean or Integer from the Variables tab.", HelpBoxMessageType.Warning));
                return card;
            }
            if (current.type == VariableValueType.Integer)
            {
                var operations = new List<string> { "Set", "Add", "Subtract" };
                var operation = new PopupField<string>("Operation", operations, Math.Max(0, Math.Min(2, (int)assignment.operation)));
                operation.RegisterValueChangedCallback(evt => EditVariableElement(passageId, elementIndex, "Change Variable Operation",
                    property => property.FindPropertyRelative("_operation").enumValueIndex = operations.IndexOf(evt.newValue)));
                card.Add(operation);
            }
            AddLiteralEditor(card, current.type, assignment.value,
                edit => EditVariableElement(passageId, elementIndex, "Change Assignment Value",
                    property => edit(property.FindPropertyRelative("_value"))));
            if (!VariableYarnUtility.TryWriteAssignment(assignment, out _, out var error))
            {
                card.Add(new HelpBox(error, HelpBoxMessageType.Warning));
                card.Add(new Button(() => EditVariableElement(passageId, elementIndex, "Reset Variable Assignment",
                    property => InitializeAssignment(property, current), true)) { text = "Reset to variable default" });
            }
            return card;
        }

        private static List<string> UniqueVariableNames(IEnumerable<VariableDefinition> variables)
        {
            var names = new List<string>();
            foreach (var variable in variables) names.Add(CreateUniqueChoiceName(names, variable.displayName));
            return names;
        }

        private static void AddLiteralEditor(VisualElement container, VariableValueType type, SerializedValue value,
            Action<Action<SerializedProperty>> change)
        {
            if (type == VariableValueType.Boolean)
            {
                var field = new Toggle("Value");
                field.SetValueWithoutNotify(value.booleanValue);
                field.RegisterValueChangedCallback(evt => change(property =>
                {
                    property.FindPropertyRelative("_type").enumValueIndex = (int)DialogueValueType.Boolean;
                    property.FindPropertyRelative("_booleanValue").boolValue = evt.newValue;
                }));
                container.Add(field);
            }
            else
            {
                var field = new IntegerField("Value") { isDelayed = true };
                field.SetValueWithoutNotify(value.integerValue);
                field.RegisterValueChangedCallback(evt => change(property =>
                {
                    property.FindPropertyRelative("_type").enumValueIndex = (int)DialogueValueType.Integer;
                    property.FindPropertyRelative("_integerValue").intValue = evt.newValue;
                }));
                container.Add(field);
            }
        }

        private static VariableReference ConditionReference(ConditionExpression condition) => condition switch
        {
            BooleanVariableCondition boolean => boolean.variable,
            ComparisonCondition comparison => comparison.left.variable,
            _ => null
        };

        private static string ConditionSummary(ConditionExpression condition)
        {
            if (condition == null) return "Always";
            var variable = VariableYarnUtility.Resolve(ConditionReference(condition));
            if (variable == null) return "Missing variable";
            if (condition is BooleanVariableCondition boolean)
                return variable.displayName + (boolean.expectedValue ? " is true" : " is false");
            if (condition is ComparisonCondition comparison)
            {
                var index = (int)comparison.comparison;
                return variable.displayName + " " + (index >= 0 && index < ComparisonLabels.Count ? ComparisonLabels[index] : "?")
                    + " " + comparison.right.literal.integerValue;
            }
            return "Unsupported condition";
        }

        private void InitializeChoiceCondition(SerializedProperty property, VariableDefinition variable)
        {
            if (variable == null) { property.managedReferenceValue = null; return; }
            if (variable.type == VariableValueType.Boolean)
            {
                property.managedReferenceValue = new BooleanVariableCondition();
                SetVariableReference(property.FindPropertyRelative("_variable"), variable);
            }
            else
            {
                property.managedReferenceValue = new ComparisonCondition();
                var left = property.FindPropertyRelative("_left");
                left.FindPropertyRelative("_kind").enumValueIndex = (int)ValueOperandKind.Variable;
                SetVariableReference(left.FindPropertyRelative("_variable"), variable);
                var right = property.FindPropertyRelative("_right");
                right.FindPropertyRelative("_kind").enumValueIndex = (int)ValueOperandKind.Literal;
                right.FindPropertyRelative("_literal").FindPropertyRelative("_type").enumValueIndex = (int)DialogueValueType.Integer;
            }
        }

        private VisualElement CreateChoiceConditionEditor(string passageId, int elementIndex, int optionIndex)
        {
            var choice = _selectedConversation.passages[FindPassageIndex(passageId)].elements[elementIndex] as ChoiceElement;
            var option = choice.options[optionIndex];
            var key = _selectedConversation.id.value + option.id.value;
            var foldout = new Foldout { text = "Condition · " + ConditionSummary(option.availability), value = _expandedChoiceConditions.Contains(key) };
            foldout.AddToClassList(DialogueEditorStyleClasses.ConversationChoiceCondition);
            foldout.RegisterValueChangedCallback(evt =>
            {
                // Ignore ChangeEvents bubbling from the Boolean value field.
                if (!ReferenceEquals(evt.target, foldout)) return;
                if (evt.newValue) _expandedChoiceConditions.Add(key); else _expandedChoiceConditions.Remove(key);
            });
            void EditCondition(Action<SerializedProperty> edit, bool rebuild = false)
            {
                EditVariableElement(passageId, elementIndex, "Change Choice Condition", element =>
                    edit(element.FindPropertyRelative("_options").GetArrayElementAtIndex(optionIndex).FindPropertyRelative("_availability")), rebuild);
                var updated = (_selectedConversation.passages[FindPassageIndex(passageId)].elements[elementIndex] as ChoiceElement).options[optionIndex];
                foldout.text = "Condition · " + ConditionSummary(updated.availability);
            }
            var variables = EditableVariables();
            var current = VariableYarnUtility.Resolve(ConditionReference(option.availability));
            var names = UniqueVariableNames(variables);
            names.Insert(0, "Always");
            var selected = option.availability == null ? 0 : variables.IndexOf(current) + 1;
            if (option.availability != null && selected == 0) { selected = names.Count; names.Add("Missing or unsupported variable"); }
            var picker = new PopupField<string>("Available when", names, selected);
            picker.RegisterValueChangedCallback(evt =>
            {
                var index = names.IndexOf(evt.newValue) - 1;
                if (index >= variables.Count) return;
                EditCondition(property => InitializeChoiceCondition(property, index < 0 ? null : variables[index]), true);
            });
            foldout.Add(picker);
            if (option.availability is BooleanVariableCondition boolean && current?.type == VariableValueType.Boolean)
            {
                var expected = new PopupField<string>("Is", new List<string> { "True", "False" }, boolean.expectedValue ? 0 : 1);
                expected.RegisterValueChangedCallback(evt => EditCondition(property =>
                    property.FindPropertyRelative("_expectedValue").boolValue = evt.newValue == "True"));
                foldout.Add(expected);
            }
            else if (option.availability is ComparisonCondition comparison && current?.type == VariableValueType.Integer)
            {
                var operation = new PopupField<string>("Comparison", ComparisonLabels, Math.Max(0, Math.Min(5, (int)comparison.comparison)));
                operation.RegisterValueChangedCallback(evt => EditCondition(property =>
                    property.FindPropertyRelative("_comparison").enumValueIndex = ComparisonLabels.IndexOf(evt.newValue)));
                foldout.Add(operation);
                AddLiteralEditor(foldout, VariableValueType.Integer, comparison.right.literal,
                    edit => EditCondition(property => edit(property.FindPropertyRelative("_right").FindPropertyRelative("_literal"))));
            }
            if (!VariableYarnUtility.TryWriteCondition(option.availability, out _, out var error))
            {
                foldout.Add(new HelpBox(error, HelpBoxMessageType.Warning));
                if (VariableYarnUtility.IsSupported(current))
                    foldout.Add(new Button(() => EditCondition(property => InitializeChoiceCondition(property, current), true))
                        { text = "Reset condition to current variable type" });
            }
            return foldout;
        }
    }
}
