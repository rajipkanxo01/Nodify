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
            return CreateConditionEditor(passageId, elementIndex, key, option.availability,
                element => element.FindPropertyRelative("_options").GetArrayElementAtIndex(optionIndex).FindPropertyRelative("_availability"));
        }

        private VisualElement CreateConditionEditor(string passageId, int elementIndex, string key,
            ConditionExpression condition, Func<SerializedProperty, SerializedProperty> getCondition, bool required = false)
        {
            string Summary(ConditionExpression value) => required && value == null ? "Choose variable" : ConditionSummary(value);
            var foldout = new Foldout { text = "Condition · " + Summary(condition), value = required || _expandedChoiceConditions.Contains(key) };
            foldout.AddToClassList(DialogueEditorStyleClasses.ConversationChoiceCondition);
            foldout.RegisterValueChangedCallback(evt =>
            {
                // Ignore ChangeEvents bubbling from the Boolean value field.
                if (!ReferenceEquals(evt.target, foldout)) return;
                if (evt.newValue) _expandedChoiceConditions.Add(key); else _expandedChoiceConditions.Remove(key);
            });
            void EditCondition(Action<SerializedProperty> edit, bool rebuild = false)
            {
                EditVariableElement(passageId, elementIndex, "Change Dialogue Condition", element =>
                    edit(getCondition(element)), rebuild);
                var serialized = new SerializedObject(_selectedConversation);
                foldout.text = "Condition · " + Summary(getCondition(GetElement(serialized, passageId, elementIndex)).managedReferenceValue as ConditionExpression);
            }
            var variables = EditableVariables();
            var current = VariableYarnUtility.Resolve(ConditionReference(condition));
            var names = UniqueVariableNames(variables);
            names.Insert(0, required ? "Choose variable" : "Always");
            var selected = condition == null ? 0 : variables.IndexOf(current) + 1;
            if (condition != null && selected == 0) { selected = names.Count; names.Add("Missing or unsupported variable"); }
            var picker = new PopupField<string>(required ? "When" : "Available when", names, selected);
            picker.RegisterValueChangedCallback(evt =>
            {
                var index = names.IndexOf(evt.newValue) - 1;
                if (index >= variables.Count) return;
                EditCondition(property => InitializeChoiceCondition(property, index < 0 ? null : variables[index]), true);
            });
            foldout.Add(picker);
            if (required && condition == null)
                foldout.Add(new HelpBox("Choose a variable for this branch. Use Else for the fallback route.", HelpBoxMessageType.Warning));
            if (condition is BooleanVariableCondition boolean && current?.type == VariableValueType.Boolean)
            {
                var expected = new PopupField<string>("Is", new List<string> { "True", "False" }, boolean.expectedValue ? 0 : 1);
                expected.RegisterValueChangedCallback(evt => EditCondition(property =>
                    property.FindPropertyRelative("_expectedValue").boolValue = evt.newValue == "True"));
                foldout.Add(expected);
            }
            else if (condition is ComparisonCondition comparison && current?.type == VariableValueType.Integer)
            {
                var operation = new PopupField<string>("Comparison", ComparisonLabels, Math.Max(0, Math.Min(5, (int)comparison.comparison)));
                operation.RegisterValueChangedCallback(evt => EditCondition(property =>
                    property.FindPropertyRelative("_comparison").enumValueIndex = ComparisonLabels.IndexOf(evt.newValue)));
                foldout.Add(operation);
                AddLiteralEditor(foldout, VariableValueType.Integer, comparison.right.literal,
                    edit => EditCondition(property => edit(property.FindPropertyRelative("_right").FindPropertyRelative("_literal"))));
            }
            if (!VariableYarnUtility.TryWriteCondition(condition, out _, out var error))
            {
                foldout.Add(new HelpBox(error, HelpBoxMessageType.Warning));
                if (VariableYarnUtility.IsSupported(current))
                    foldout.Add(new Button(() => EditCondition(property => InitializeChoiceCondition(property, current), true))
                        { text = "Reset condition to current variable type" });
            }
            return foldout;
        }

        private void AddConditional(string passageId)
        {
            var index = AddElementToPassage(passageId, new ConditionalElement(), "Add Conditional Branch");
            if (index < 0) return;
            EditVariableElement(passageId, index, "Initialize Conditional Branch", element =>
            {
                var branches = element.FindPropertyRelative("_branches");
                branches.arraySize = 1;
                InitializeConditionalBranch(branches.GetArrayElementAtIndex(0));
                var otherwise = element.FindPropertyRelative("_elseElements");
                otherwise.arraySize = 1;
                otherwise.GetArrayElementAtIndex(0).managedReferenceValue = new JumpElement();
            }, true);
        }

        private void InitializeConditionalBranch(SerializedProperty branch)
        {
            SetGuidValue(branch.FindPropertyRelative("_id"), Guid.NewGuid().ToString("N"));
            InitializeChoiceCondition(branch.FindPropertyRelative("_condition"), EditableVariables().FirstOrDefault());
            var elements = branch.FindPropertyRelative("_elements");
            elements.arraySize = 1;
            elements.GetArrayElementAtIndex(0).managedReferenceValue = new JumpElement();
        }

        private VisualElement CreateConditionalEditor(string passageId, int elementIndex)
        {
            var conditional = (ConditionalElement)_selectedConversation.passages[FindPassageIndex(passageId)].elements[elementIndex];
            var card = new VisualElement();
            card.AddToClassList(DialogueEditorStyleClasses.ConversationElementCard);
            card.Add(new Label("Automatic Branch · first matching condition wins"));
            if (EditableVariables().Count == 0)
                card.Add(new HelpBox("Create a Boolean or Integer in the Variables tab first.", HelpBoxMessageType.Info));
            for (var index = 0; index < conditional.branches.Count; index++)
            {
                var captured = index;
                var branch = conditional.branches[index];
                var branchHeader = new VisualElement();
                branchHeader.style.flexDirection = FlexDirection.Row;
                var branchTitle = new Label(index == 0 ? "If" : "Else If");
                branchTitle.style.flexGrow = 1;
                branchHeader.Add(branchTitle);
                branchHeader.Add(new Button(() =>
                {
                    if (conditional.branches.Count == 1)
                        DeleteInlineElement(passageId, elementIndex);
                    else
                        EditVariableElement(passageId, elementIndex, "Delete Conditional Branch",
                            element => element.FindPropertyRelative("_branches").DeleteArrayElementAtIndex(captured), true);
                }) { text = "×", tooltip = conditional.branches.Count == 1
                    ? "Delete automatic branch" : "Delete this condition branch" });
                card.Add(branchHeader);
                card.Add(CreateConditionEditor(passageId, elementIndex, _selectedConversation.id.value + branch.id.value,
                    branch.condition, element => element.FindPropertyRelative("_branches").GetArrayElementAtIndex(captured)
                        .FindPropertyRelative("_condition"), true));
                AddRouteDestination(card, passageId, elementIndex, index, branch.elements);
            }
            card.Add(new Button(() => EditVariableElement(passageId, elementIndex, "Add Else If Branch", element =>
            {
                var branches = element.FindPropertyRelative("_branches");
                branches.arraySize++;
                InitializeConditionalBranch(branches.GetArrayElementAtIndex(branches.arraySize - 1));
            }, true)) { text = "+ Else If" });
            if (conditional.elseElements.Count > 0)
            {
                var elseHeader = new VisualElement();
                elseHeader.style.flexDirection = FlexDirection.Row;
                var elseTitle = new Label("Else");
                elseTitle.style.flexGrow = 1;
                elseHeader.Add(elseTitle);
                elseHeader.Add(new Button(() => EditVariableElement(passageId, elementIndex, "Delete Else Branch",
                    element => element.FindPropertyRelative("_elseElements").ClearArray(), true))
                    { text = "×", tooltip = "Delete Else branch" });
                card.Add(elseHeader);
                AddRouteDestination(card, passageId, elementIndex, -2, conditional.elseElements);
            }
            else
            {
                card.Add(new Button(() => EditVariableElement(passageId, elementIndex, "Add Else Branch", element =>
                {
                    var otherwise = element.FindPropertyRelative("_elseElements");
                    otherwise.arraySize = 1;
                    otherwise.GetArrayElementAtIndex(0).managedReferenceValue = new JumpElement();
                }, true)) { text = "+ Else" });
            }
            card.Add(new Button(() => DeleteInlineElement(passageId, elementIndex)) { text = "Delete automatic branch" });
            return card;
        }

        private static SerializedProperty RouteElements(SerializedProperty element, int branchIndex) => branchIndex == -2
            ? element.FindPropertyRelative("_elseElements")
            : element.FindPropertyRelative("_branches").GetArrayElementAtIndex(branchIndex).FindPropertyRelative("_elements");

        private void AddRouteDestination(VisualElement card, string passageId, int elementIndex, int branchIndex,
            IReadOnlyList<DialogueElement> elements)
        {
            // The visual routing editor owns a single jump. Preserve richer authored branch content.
            if (elements.Count != 0 && !(elements.Count == 1 && elements[0] is JumpElement))
            {
                card.Add(new HelpBox("This branch contains structured dialogue content.", HelpBoxMessageType.Info));
                return;
            }
            var jump = elements.FirstOrDefault() as JumpElement;
            var names = new List<string> { "None" };
            var ids = new List<string> { string.Empty };
            foreach (var passage in _selectedConversation.passages.Where(item => item != null))
            {
                names.Add(CreateUniqueChoiceName(names, passage.title));
                ids.Add(passage.id.value);
            }
            var picker = new PopupField<string>("Destination", names, Math.Max(0, ids.IndexOf(jump?.destinationPassageId.value ?? string.Empty)));
            picker.RegisterValueChangedCallback(evt => EditVariableElement(passageId, elementIndex, "Change Branch Destination", element =>
            {
                var nested = RouteElements(element, branchIndex);
                nested.arraySize = 1;
                if (!(nested.GetArrayElementAtIndex(0).managedReferenceValue is JumpElement))
                    nested.GetArrayElementAtIndex(0).managedReferenceValue = new JumpElement();
                SetGuidValue(nested.GetArrayElementAtIndex(0).FindPropertyRelative("_destinationPassageId"), ids[names.IndexOf(evt.newValue)]);
            }, true));
            card.Add(picker);
        }
    }
}
