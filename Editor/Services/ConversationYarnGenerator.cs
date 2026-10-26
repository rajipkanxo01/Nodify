using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ToolForDialogue._Project.Dialogue.Runtime.Assets;
using ToolForDialogue._Project.Dialogue.Runtime.Types;
using UnityEditor;
using UnityEngine;

namespace ToolForDialogue.Editor._Project.Scripts.Editor.Services
{
    /// <summary>Produces the Yarn source owned by a conversation asset.</summary>
    [InitializeOnLoad]
    internal static class ConversationYarnGenerator
    {
        private const double GenerationDelaySeconds = 0.75d;
        private static readonly Dictionary<EntityId, PendingGeneration> Pending =
            new Dictionary<EntityId, PendingGeneration>();
        private static readonly HashSet<EntityId> Generating = new HashSet<EntityId>();

        private sealed class PendingGeneration
        {
            public ConversationAsset conversation;
            public double dueAt;
        }

        static ConversationYarnGenerator()
        {
            EditorApplication.update += ProcessPending;
            EditorApplication.delayCall += () =>
            {
                foreach (var guid in AssetDatabase.FindAssets("t:DialogueProjectAsset"))
                    ScheduleAll(AssetDatabase.LoadAssetAtPath<DialogueProjectAsset>(AssetDatabase.GUIDToAssetPath(guid)), true);
                foreach (var guid in AssetDatabase.FindAssets("t:ConversationAsset"))
                {
                    Schedule(AssetDatabase.LoadAssetAtPath<ConversationAsset>(
                        AssetDatabase.GUIDToAssetPath(guid)), true);
                }
            };
        }

        public static void ScheduleAll(DialogueProjectAsset project, bool immediate = false)
        {
            if (project == null) return;
            VariableYarnUtility.SyncDeclarations(project);
            foreach (var conversation in project.conversations) Schedule(conversation, immediate);
        }

        public static bool TryRename(ConversationAsset conversation, string requestedName, out string error)
        {
            error = string.Empty;
            var name = requestedName?.Trim();
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || name.EndsWith(".", StringComparison.Ordinal))
            {
                error = "Enter a non-empty filename without invalid characters or a trailing period.";
                return false;
            }

            var oldAssetPath = AssetDatabase.GetAssetPath(conversation);
            if (string.IsNullOrEmpty(oldAssetPath))
            {
                error = "Save the conversation asset before renaming it.";
                return false;
            }
            var newAssetPath = (Path.GetDirectoryName(oldAssetPath) + "/" + name + ".asset").Replace('\\', '/');
            var oldYarnPath = GetYarnPath(conversation);
            var newYarnPath = Path.ChangeExtension(newAssetPath, ".yarn");
            var moveFiles = !string.Equals(oldAssetPath, newAssetPath, StringComparison.Ordinal);
            var hasYarn = File.Exists(oldYarnPath);
            if (moveFiles)
            {
                error = AssetDatabase.ValidateMoveAsset(oldAssetPath, newAssetPath);
                if (!string.IsNullOrEmpty(error)) return false;
                // Reserve both filenames even when this conversation has not generated Yarn yet.
                if (!string.Equals(oldYarnPath, newYarnPath, StringComparison.OrdinalIgnoreCase)
                    && (File.Exists(newYarnPath) || File.Exists(newYarnPath + ".meta")))
                {
                    error = $"A Yarn file already exists at '{newYarnPath}'. Choose another name.";
                    return false;
                }
                if (hasYarn)
                {
                    error = AssetDatabase.ValidateMoveAsset(oldYarnPath, newYarnPath);
                    if (!string.IsNullOrEmpty(error)) return false;
                }
            }

            // Rename existing generated nodes as well, even if unfinished dialogue currently
            // prevents regeneration. Restrict replacements to node headers and jump commands.
            string renamedSource = null;
            string originalSource = null;
            if (hasYarn && moveFiles)
            {
                var oldPrefix = MakeIdentifier(Path.GetFileNameWithoutExtension(oldYarnPath), "Conversation");
                var newPrefix = MakeIdentifier(name, "Conversation");
                originalSource = File.ReadAllText(oldYarnPath);
                var nodeNames = Regex.Matches(originalSource, @"(?m)^title: ([^\r\n]+)")
                    .Cast<Match>().Select(match => match.Groups[1].Value)
                    .Where(node => node == oldPrefix || node.StartsWith(oldPrefix + "_", StringComparison.Ordinal))
                    .ToDictionary(node => node, node => newPrefix + node.Substring(oldPrefix.Length));
                renamedSource = Regex.Replace(originalSource,
                    @"(?m)(^title: )([^\r\n]+)|(<<jump )([^\s>]+)(>>)", match =>
                    {
                        var isTitle = match.Groups[1].Success;
                        var node = match.Groups[isTitle ? 2 : 4].Value;
                        return nodeNames.TryGetValue(node, out var renamed)
                            ? (isTitle ? match.Groups[1].Value + renamed : match.Groups[3].Value + renamed + match.Groups[5].Value)
                            : match.Value;
                    });
                if (!TryCompileSource(renamedSource, out error)) return false;
            }

            if (moveFiles)
            {
                error = AssetDatabase.MoveAsset(oldAssetPath, newAssetPath);
                if (!string.IsNullOrEmpty(error)) return false;
                if (hasYarn)
                {
                    error = AssetDatabase.MoveAsset(oldYarnPath, newYarnPath);
                    if (!string.IsNullOrEmpty(error))
                    {
                        var rollbackError = AssetDatabase.MoveAsset(newAssetPath, oldAssetPath);
                        if (!string.IsNullOrEmpty(rollbackError)) error += "\n" + rollbackError;
                        return false;
                    }
                    try
                    {
                        File.WriteAllText(newYarnPath, renamedSource, new UTF8Encoding(false));
                    }
                    catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                    {
                        error = exception.Message;
                        try
                        {
                            File.WriteAllText(newYarnPath, originalSource, new UTF8Encoding(false));
                        }
                        catch (Exception restoreException) when (restoreException is IOException || restoreException is UnauthorizedAccessException)
                        {
                            error += "\nCould not restore Yarn source: " + restoreException.Message;
                        }
                        var yarnRollbackError = AssetDatabase.MoveAsset(newYarnPath, oldYarnPath);
                        var assetRollbackError = AssetDatabase.MoveAsset(newAssetPath, oldAssetPath);
                        if (!string.IsNullOrEmpty(yarnRollbackError)) error += "\n" + yarnRollbackError;
                        if (!string.IsNullOrEmpty(assetRollbackError)) error += "\n" + assetRollbackError;
                        return false;
                    }
                    AssetDatabase.ImportAsset(newYarnPath, ImportAssetOptions.ForceUpdate);
                }
            }

            var serialized = new SerializedObject(conversation);
            serialized.FindProperty("_displayName").stringValue = name;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(conversation);
            AssetDatabase.SaveAssetIfDirty(conversation);
            Schedule(conversation, true);
            return true;
        }

        public static void Schedule(ConversationAsset conversation, bool immediate = false)
        {
            if (conversation == null) return;
            var entityId = conversation.GetEntityId();
            if (Generating.Contains(entityId)) return;
            Pending[entityId] = new PendingGeneration
            {
                conversation = conversation,
                dueAt = EditorApplication.timeSinceStartup + (immediate ? 0d : GenerationDelaySeconds)
            };
        }

        private static void ProcessPending()
        {
            if (Pending.Count == 0) return;
            var now = EditorApplication.timeSinceStartup;
            var ready = Pending
                .Where(pair => pair.Value.conversation == null || pair.Value.dueAt <= now)
                .Select(pair => pair.Key)
                .ToList();
            foreach (var entityId in ready)
            {
                var request = Pending[entityId];
                Pending.Remove(entityId);
                if (request.conversation != null) Generate(request.conversation);
            }
        }

        public static void DeleteGeneratedFile(ConversationAsset conversation)
        {
            if (conversation != null) Pending.Remove(conversation.GetEntityId());
            var yarnPath = GetYarnPath(conversation);
            if (string.IsNullOrEmpty(yarnPath)) return;
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(yarnPath) != null)
            {
                AssetDatabase.DeleteAsset(yarnPath);
                return;
            }
            if (!File.Exists(yarnPath)) return;
            File.Delete(yarnPath);
            if (File.Exists(yarnPath + ".meta")) File.Delete(yarnPath + ".meta");
        }

        private static void Generate(ConversationAsset conversation)
        {
            var entityId = conversation.GetEntityId();
            if (!Generating.Add(entityId)) return;
            try
            {
                var yarnPath = GetYarnPath(conversation);
                if (string.IsNullOrEmpty(yarnPath)) return;
                VariableYarnUtility.SyncOwners(conversation);

                // Persist only this conversation. Avoiding SaveAssets keeps unrelated scenes and assets untouched.
                AssetDatabase.SaveAssetIfDirty(conversation);

                if (!TryBuildSource(conversation, yarnPath, out var source, out var error))
                {
                    Debug.LogWarning($"Yarn generation skipped for '{conversation.displayName}': {error}", conversation);
                    return;
                }
                if (!TryCompileSource(source, out error))
                {
                    Debug.LogWarning(
                        $"Yarn generation skipped for '{conversation.displayName}' because the generated source did not compile: {error}",
                        conversation);
                    return;
                }

                var fullPath = Path.GetFullPath(yarnPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? string.Empty);
                if (File.Exists(fullPath) && File.ReadAllText(fullPath) == source) return;
                File.WriteAllText(fullPath, source, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(yarnPath, ImportAssetOptions.ForceUpdate);
            }
            finally
            {
                Generating.Remove(entityId);
            }
        }

        private static bool TryCompileSource(string source, out string error)
        {
            var temporaryPath = Path.Combine(
                Path.GetTempPath(),
                $"ToolForDialogue_{Guid.NewGuid():N}.yarn");
            try
            {
                File.WriteAllText(temporaryPath, source, new UTF8Encoding(false));
                var job = Yarn.Compiler.CompilationJob.CreateFromFiles(new[] { temporaryPath });
                var result = Yarn.Compiler.Compiler.Compile(job);
                var errors = result.Diagnostics
                    .Where(diagnostic => diagnostic.Severity == Yarn.Compiler.Diagnostic.DiagnosticSeverity.Error)
                    .Select(diagnostic => diagnostic.Message)
                    .ToList();
                error = string.Join("\n", errors);
                return errors.Count == 0;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private static string GetYarnPath(ConversationAsset conversation)
        {
            var assetPath = AssetDatabase.GetAssetPath(conversation);
            return string.IsNullOrEmpty(assetPath)
                ? string.Empty
                : Path.ChangeExtension(assetPath, ".yarn").Replace('\\', '/');
        }

        private static bool TryBuildSource(ConversationAsset conversation, string yarnPath,
            out string source, out string error)
        {
            source = string.Empty;
            error = string.Empty;
            var passages = conversation.passages.Where(passage => passage != null).ToList();
            if (passages.Count == 0)
            {
                error = "the conversation has no passages.";
                return false;
            }

            var passageIds = new HashSet<string>(passages.Select(passage => passage.id.value));
            if (!conversation.startPassageId.isValid || !passageIds.Contains(conversation.startPassageId.value))
            {
                error = "the start passage is missing.";
                return false;
            }

            var conversationName = MakeIdentifier(Path.GetFileNameWithoutExtension(yarnPath), "Conversation");
            var nodeNames = CreateNodeNames(passages, conversationName);
            var builder = new StringBuilder();
            foreach (var passage in passages)
            {
                if (TryWritePassage(builder, passage, passageIds, nodeNames, out error)) continue;
                error = $"{passage.title}: {error}";
                return false;
            }
            source = builder.ToString();
            return true;
        }

        private static Dictionary<string, string> CreateNodeNames(
            IEnumerable<PassageData> passages, string conversationName)
        {
            var result = new Dictionary<string, string>();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var passage in passages)
            {
                var requested = $"{conversationName}_{MakeIdentifier(passage.title, "Passage")}";
                var unique = requested;
                var suffix = 2;
                while (!used.Add(unique)) unique = $"{requested}_{suffix++}";
                result[passage.id.value] = unique;
            }
            return result;
        }

        private static bool TryWritePassage(StringBuilder builder, PassageData passage,
            HashSet<string> passageIds, IReadOnlyDictionary<string, string> nodeNames, out string error)
        {
            error = string.Empty;
            builder.Append("title: ").AppendLine(nodeNames[passage.id.value]);
            builder.AppendLine("---");
            foreach (var element in passage.elements.Where(element => element != null && element.enabled))
            {
                switch (element)
                {
                    case LineElement line:
                        if (line.speaker == null)
                        {
                            error = "a dialogue line has no speaker.";
                            return false;
                        }
                        if (string.IsNullOrWhiteSpace(line.text))
                        {
                            error = "a dialogue line is empty.";
                            return false;
                        }
                        WriteLine(builder, line);
                        break;
                    case ChoiceElement choice:
                        if (choice.options.Count == 0)
                        {
                            error = "a choice has no options.";
                            return false;
                        }
                        foreach (var option in choice.options)
                        {
                            if (option == null || string.IsNullOrWhiteSpace(option.text))
                            {
                                error = "a choice option has no text.";
                                return false;
                            }
                            if (!option.destinationPassageId.isValid
                                || !passageIds.Contains(option.destinationPassageId.value))
                            {
                                error = $"choice '{option.text}' has no valid destination.";
                                return false;
                            }
                            if (!VariableYarnUtility.TryWriteCondition(option.availability, out var condition, out error)) return false;
                            builder.Append("-> ").Append(OneLine(option.text));
                            if (!string.IsNullOrEmpty(condition)) builder.Append(" <<if ").Append(condition).Append(">>");
                            builder.AppendLine();
                            builder.Append("    <<jump ").Append(nodeNames[option.destinationPassageId.value])
                                .AppendLine(">>");
                        }
                        break;
                    case JumpElement jump:
                        if (!jump.destinationPassageId.isValid
                            || !passageIds.Contains(jump.destinationPassageId.value))
                        {
                            error = "a jump has no valid destination.";
                            return false;
                        }
                        builder.Append("<<jump ").Append(nodeNames[jump.destinationPassageId.value])
                            .AppendLine(">>");
                        break;
                    case EndElement:
                        builder.AppendLine("<<stop>>");
                        break;
                    case SetVariableElement assignment:
                        if (!VariableYarnUtility.TryWriteAssignment(assignment, out var command, out error)) return false;
                        builder.AppendLine(command);
                        break;
                    case ConditionalElement:
                        error = "conditional generation is not implemented yet.";
                        return false;
                    case ActionElement:
                        error = "action generation is not implemented yet.";
                        return false;
                }
            }
            builder.AppendLine("===");
            builder.AppendLine();
            return true;
        }

        private static void WriteLine(StringBuilder builder, LineElement line)
        {
            var speaker = OneLine(line.speaker.displayName);
            var suffix = BuildLineTags(line);
            foreach (var textLine in line.text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                builder.Append(speaker).Append(": ").Append(textLine);
                if (!string.IsNullOrEmpty(suffix)) builder.Append(' ').Append(suffix);
                builder.AppendLine();
            }
        }

        private static string BuildLineTags(LineElement line)
        {
            var tags = line.tags.Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => "#" + MakeIdentifier(tag.TrimStart('#'), "tag")).ToList();
            var expression = line.speaker.expressions.FirstOrDefault(item =>
                item != null && item.id == line.expressionId);
            if (expression != null && expression.id != line.speaker.defaultExpressionId)
            {
                tags.Add("#expression_" + MakeIdentifier(expression.displayName, "Expression"));
            }
            return string.Join(" ", tags);
        }

        private static string OneLine(string value)
        {
            return (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static string MakeIdentifier(string value, string fallback)
        {
            var builder = new StringBuilder();
            foreach (var character in value ?? string.Empty)
            {
                builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
            }
            var result = builder.ToString().Trim('_');
            if (string.IsNullOrEmpty(result)) result = fallback;
            if (char.IsDigit(result[0])) result = "_" + result;
            return result;
        }
    }

    /// <summary>Also keeps Yarn current when assets are saved outside the custom editor.</summary>
    internal sealed class ConversationYarnSaveProcessor : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            var characterChanged = false;
            foreach (var path in paths)
            {
                var conversation = AssetDatabase.LoadAssetAtPath<ConversationAsset>(path);
                if (conversation != null) ConversationYarnGenerator.Schedule(conversation, true);
                characterChanged |= AssetDatabase.LoadAssetAtPath<CharacterAsset>(path) != null;
                var project = AssetDatabase.LoadAssetAtPath<DialogueProjectAsset>(path);
                if (project != null) ConversationYarnGenerator.ScheduleAll(project, true);
            }

            if (characterChanged)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:DialogueProjectAsset"))
                {
                    ConversationYarnGenerator.ScheduleAll(
                        AssetDatabase.LoadAssetAtPath<DialogueProjectAsset>(AssetDatabase.GUIDToAssetPath(guid)),
                        true);
                }
            }
            return paths;
        }
    }
}
