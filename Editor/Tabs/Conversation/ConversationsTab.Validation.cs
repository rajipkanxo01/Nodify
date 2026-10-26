using System.Collections.Generic;
using System.Linq;
using ToolForDialogue._Project.Dialogue.Runtime.Assets;
using ToolForDialogue._Project.Dialogue.Runtime.Types;
using UnityEditor;
using UnityEngine.UIElements;

namespace ToolForDialogue.Editor._Project.Scripts.Editor.Tabs.Conversation
{
    internal sealed partial class ConversationsTab
    {
        private void RefreshValidationWarnings()
        {
            if (_selectedConversation == null) return;
            _graph?.RefreshWarnings(_selectedConversation);
            var passage = _selectedConversation.passages.FirstOrDefault(candidate =>
                candidate != null && candidate.id.value == _selectedPassageId);
            if (_passageWarning != null)
            {
                var warnings = passage == null ? new List<string>() : GetPassageWarnings(_selectedConversation, passage);
                _passageWarning.text = string.Join("\n", warnings);
                _passageWarning.style.display = warnings.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (_conversationWarningBadges.TryGetValue(_selectedConversation, out var badge))
            {
                var warnings = GetConversationWarnings(_selectedConversation);
                badge.tooltip = string.Join("\n", warnings);
                badge.style.display = warnings.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private static List<string> GetConversationWarnings(ConversationAsset conversation)
        {
            var warnings = new List<string>();
            if (conversation.passages.Count == 0)
            {
                warnings.Add("Conversation has no passages.");
                return warnings;
            }

            if (!conversation.passages.Any(passage => passage != null && passage.id == conversation.startPassageId))
            {
                warnings.Add("Start passage is missing.");
            }

            foreach (var passage in conversation.passages.Where(passage => passage != null))
            {
                warnings.AddRange(GetPassageWarnings(conversation, passage)
                    .Select(warning => $"{passage.title}: {warning}"));
            }

            var startId = conversation.startPassageId.value;
            if (!string.IsNullOrEmpty(startId))
            {
                var passagesById = conversation.passages
                    .Where(passage => passage != null)
                    .ToDictionary(passage => passage.id.value, passage => passage);
                var reachable = new HashSet<string>();
                var pending = new Queue<string>();
                pending.Enqueue(startId);
                while (pending.Count > 0)
                {
                    var passageId = pending.Dequeue();
                    if (!reachable.Add(passageId) || !passagesById.TryGetValue(passageId, out var passage)) continue;
                    foreach (var destination in GetDestinations(passage.elements))
                    {
                        if (passagesById.ContainsKey(destination)) pending.Enqueue(destination);
                    }
                }

                foreach (var unreachable in passagesById.Values.Where(passage => !reachable.Contains(passage.id.value)))
                {
                    warnings.Add($"'{unreachable.title}' is unreachable from the start passage.");
                }
            }

            return warnings;
        }

        private static List<string> GetPassageWarnings(ConversationAsset conversation, PassageData passage)
        {
            var warnings = new List<string>();
            if (passage.elements.Count == 0)
            {
                warnings.Add("Passage has no content.");
                return warnings;
            }

            var passageIds = new HashSet<string>(conversation.passages
                .Where(candidate => candidate != null)
                .Select(candidate => candidate.id.value));
            foreach (var element in passage.elements)
            {
                if (element is LineElement line && string.IsNullOrWhiteSpace(line.text))
                {
                    warnings.Add("A dialogue line is empty.");
                }

                switch (element)
                {
                    case LineElement spokenLine when spokenLine.speaker == null:
                        warnings.Add("A dialogue line has no speaker.");
                        break;

                    case JumpElement jump
                        when (!jump.destinationPassageId.isValid ||
                              !passageIds.Contains(jump.destinationPassageId.value)):
                        warnings.Add("A jump has no valid destination.");
                        break;

                    case ChoiceElement choice:
                    {
                        if (choice.options.Count == 0)
                        {
                            warnings.Add("A choice has no options.");
                        }

                        foreach (var option in choice.options.Where(option => option != null))
                        {
                            if (string.IsNullOrWhiteSpace(option.text)) warnings.Add("A choice option has no text.");
                            if (!option.destinationPassageId.isValid
                                || !passageIds.Contains(option.destinationPassageId.value))
                            {
                                warnings.Add("A choice option has no valid destination.");
                            }
                        }

                        break;
                    }

                    case ConditionalElement:
                        warnings.Add("Conditional Yarn generation is not implemented yet.");
                        break;

                    case ActionElement:
                        warnings.Add("Action Yarn generation is not implemented yet.");
                        break;
                }
            }

            var last = passage.elements.LastOrDefault();
            if (!(last is JumpElement) && !(last is ChoiceElement) && !(last is EndElement)
                && !(last is ConditionalElement))
            {
                warnings.Add("Passage has no explicit exit.");
            }

            return warnings.Distinct().ToList();
        }

        private static IEnumerable<string> GetDestinations(IReadOnlyList<DialogueElement> elements)
        {
            foreach (var element in elements)
            {
                if (element is JumpElement jump && jump.destinationPassageId.isValid)
                {
                    yield return jump.destinationPassageId.value;
                }

                if (element is ChoiceElement choice)
                {
                    foreach (var option in choice.options.Where(option =>
                                 option is { destinationPassageId: { isValid: true } }))
                    {
                        yield return option.destinationPassageId.value;
                    }
                }

                if (element is ConditionalElement conditional)
                {
                    foreach (var branch in conditional.branches)
                    foreach (var destination in GetDestinations(branch.elements))
                        yield return destination;
                    foreach (var destination in GetDestinations(conditional.elseElements)) yield return destination;
                }
            }
        }

        private static string CreatePassagePreview(PassageData passage)
        {
            if (passage.elements.Count == 0) return "Empty passage";
            var previews = passage.elements.Take(3).Select(element =>
            {
                return element switch
                {
                    LineElement line => string.IsNullOrWhiteSpace(line.text) ? "Dialogue line" : line.text,
                    ChoiceElement choice => $"Choice · {choice.options.Count} options",
                    JumpElement => "Jump",
                    EndElement => "End conversation",
                    ConditionalElement => "Conditional",
                    ActionElement => "Action",
                    _ => "Dialogue element"
                };
            });
            return string.Join("\n", previews);
        }

        private static string GetElementName(DialogueElement element)
        {
            return element switch
            {
                LineElement => "Dialogue Line",
                ChoiceElement => "Player Choice",
                JumpElement => "Jump",
                EndElement => "End Conversation",
                ConditionalElement => "Conditional",
                ActionElement => "Action",
                _ => "Unknown Element"
            };
        }

        private static string CreateUniqueChoiceName(IReadOnlyCollection<string> existing, string requested)
        {
            var baseName = string.IsNullOrWhiteSpace(requested) ? "Unnamed" : requested;
            var result = baseName;
            var suffix = 2;
            while (existing.Contains(result))
            {
                result = $"{baseName} ({suffix++})";
            }

            return result;
        }

        private static string GetGuidValue(SerializedProperty guidProperty)
        {
            return guidProperty?.FindPropertyRelative("_value")?.stringValue ?? string.Empty;
        }

        private static void SetGuidValue(SerializedProperty guidProperty, string value)
        {
            var property = guidProperty?.FindPropertyRelative("_value");
            if (property != null) property.stringValue = value;
        }
    }
}
