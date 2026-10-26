using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ToolForDialogue._Project.Dialogue.Runtime.Assets;
using ToolForDialogue._Project.Dialogue.Runtime.Conditions;
using ToolForDialogue._Project.Dialogue.Runtime.Types;
using UnityEditor;

namespace ToolForDialogue.Editor._Project.Scripts.Editor.Services
{
    /// <summary>Resolves stable variable references and translates typed values into Yarn.</summary>
    internal static class VariableYarnUtility
    {
        public static VariableDefinition Resolve(VariableReference reference)
        {
            return reference != null && reference.scope == VariableScope.Project && reference.project != null
                ? reference.project.variables.FirstOrDefault(variable => variable != null && variable.id == reference.variableId)
                : null;
        }

        public static bool IsSupported(VariableDefinition variable) => variable != null
            && (variable.type == VariableValueType.Boolean || variable.type == VariableValueType.Integer);

        public static string Symbol(DialogueProjectAsset project, VariableDefinition variable)
        {
            if (project == null || !project.id.isValid || variable == null || !variable.id.isValid)
                throw new InvalidOperationException("A variable or its project has no valid identity.");
            var name = VariableName(variable.displayName);
            var collision = project.variables.Any(other => other != null && other.id != variable.id
                && string.Equals(VariableName(other.displayName), name, StringComparison.OrdinalIgnoreCase));
            return "$" + name + (collision ? "_" + variable.id.value.Substring(0, 8) : string.Empty);
        }

        public static string VariableName(string displayName)
        {
            var name = Regex.Replace((displayName ?? string.Empty).Trim().TrimStart('$'), "[^A-Za-z0-9_]", "_");
            if (string.IsNullOrEmpty(name)) return "variable";
            return char.IsDigit(name[0]) ? "_" + name : name;
        }

        public static bool TryWriteAssignment(SetVariableElement element, out string command, out string error)
        {
            command = string.Empty;
            var variable = Resolve(element.variable);
            if (!IsSupported(variable)) { error = "A variable assignment refers to a missing or unsupported variable."; return false; }
            var expectedType = variable.type == VariableValueType.Boolean ? DialogueValueType.Boolean : DialogueValueType.Integer;
            if (element.value == null || element.value.type != expectedType)
            { error = $"The assignment value no longer matches '{variable.displayName}'. Choose the variable again."; return false; }
            if (!Enum.IsDefined(typeof(VariableAssignmentOperation), element.operation)
                || (variable.type == VariableValueType.Boolean && element.operation != VariableAssignmentOperation.Set))
            { error = "Only integer variables support Add and Subtract."; return false; }
            var symbol = Symbol(element.variable.project, variable);
            var literal = variable.type == VariableValueType.Boolean
                ? (element.value.booleanValue ? "true" : "false")
                : element.value.integerValue.ToString(CultureInfo.InvariantCulture);
            var expression = element.operation == VariableAssignmentOperation.Set ? literal
                : symbol + (element.operation == VariableAssignmentOperation.Add ? " + " : " - ") + "(" + literal + ")";
            command = "<<set " + symbol + " = " + expression + ">>";
            error = null;
            return true;
        }

        public static bool TryWriteCondition(ConditionExpression condition, out string expression, out string error)
        {
            expression = string.Empty;
            error = null;
            if (condition == null) return true;
            if (condition is BooleanVariableCondition boolean)
            {
                var variable = Resolve(boolean.variable);
                if (variable == null || variable.type != VariableValueType.Boolean)
                { error = "A condition refers to a missing variable or a variable that is no longer Boolean."; return false; }
                expression = (boolean.expectedValue ? "" : "not ") + Symbol(boolean.variable.project, variable);
                return true;
            }
            if (condition is ComparisonCondition comparison)
            {
                var variable = comparison.left.kind == ValueOperandKind.Variable ? Resolve(comparison.left.variable) : null;
                if (variable == null || variable.type != VariableValueType.Integer
                    || comparison.right.kind != ValueOperandKind.Literal || comparison.right.literal.type != DialogueValueType.Integer)
                { error = "An integer condition refers to a missing variable or has mismatched operand types."; return false; }
                var operators = new[] { "==", "!=", ">", ">=", "<", "<=" };
                var index = (int)comparison.comparison;
                if (index < 0 || index >= operators.Length) { error = "Unknown comparison operator."; return false; }
                expression = Symbol(comparison.left.variable.project, variable) + " " + operators[index] + " "
                    + comparison.right.literal.integerValue.ToString(CultureInfo.InvariantCulture);
                return true;
            }
            error = "This condition type is not supported by Yarn generation yet.";
            return false;
        }

        public static string BuildDeclarations(DialogueProjectAsset project)
        {
            var builder = new StringBuilder();
            builder.Append("title: Variables_").AppendLine(project.id.value).AppendLine("---");
            var seen = new HashSet<string>();
            foreach (var variable in project.variables.Where(IsSupported))
            {
                if (!seen.Add(variable.id.value)) throw new InvalidOperationException("Two variables share the same identity.");
                var literal = variable.type == VariableValueType.Boolean
                    ? (variable.defaultValue.booleanValue ? "true" : "false")
                    : variable.defaultValue.integerValue.ToString(CultureInfo.InvariantCulture);
                builder.Append("// ").AppendLine((variable.displayName ?? string.Empty).Replace('\r', ' ').Replace('\n', ' '));
                builder.Append("// variable-id: ").AppendLine(variable.id.value);
                builder.Append("<<declare ").Append(Symbol(project, variable)).Append(" = ").Append(literal).AppendLine(">>");
            }
            builder.AppendLine("===");
            return builder.ToString();
        }

        public static void SyncDeclarations(DialogueProjectAsset project)
        {
            if (project == null || project.yarnProject == null) return;
            var yarnProjectPath = AssetDatabase.GetAssetPath(project.yarnProject);
            if (string.IsNullOrEmpty(yarnProjectPath)) return;
            var directory = Path.GetDirectoryName(yarnProjectPath) ?? "Assets";
            var path = Path.Combine(directory, "Dialogue Variables.yarn").Replace('\\', '/');
            var legacyPath = Path.Combine(directory, "DialogueVariables_" + project.id.value + ".yarn").Replace('\\', '/');
            if (!File.Exists(path) && File.Exists(legacyPath))
            {
                var moveError = AssetDatabase.MoveAsset(legacyPath, path);
                if (!string.IsNullOrEmpty(moveError)) throw new IOException(moveError);
            }
            var source = BuildDeclarations(project);
            if (File.Exists(path) && File.ReadAllText(path) == source) return;
            var previous = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            var replacements = new Dictionary<string, string>();
            foreach (var variable in project.variables.Where(IsSupported))
            {
                var symbol = Symbol(project, variable);
                replacements["$v_" + project.id.value + "_" + variable.id.value] = symbol;
                var match = Regex.Match(previous, @"(?m)^// variable-id: " + variable.id.value
                    + @"\r?\n<<declare (?<symbol>\$[A-Za-z_][A-Za-z0-9_]*)");
                if (match.Success && match.Groups["symbol"].Value != symbol)
                    replacements[match.Groups["symbol"].Value] = symbol;
            }
            File.WriteAllText(path, source, new UTF8Encoding(false));
            var updatedFiles = new List<string>();
            foreach (var conversation in project.conversations.Where(item => item != null))
            {
                var assetPath = AssetDatabase.GetAssetPath(conversation);
                if (string.IsNullOrEmpty(assetPath)) continue;
                var conversationPath = Path.ChangeExtension(assetPath, ".yarn");
                if (!File.Exists(conversationPath)) continue;
                var original = File.ReadAllText(conversationPath);
                // Update commands/interpolations, including files whose unfinished dialogue prevents regeneration.
                var updated = Regex.Replace(original, @"<<[^\r\n]*?>>|\{[^{}\r\n]*\}", region =>
                    Regex.Replace(region.Value, @"\$[A-Za-z_][A-Za-z0-9_]*", token =>
                        replacements.TryGetValue(token.Value, out var replacement) ? replacement : token.Value));
                if (updated == original) continue;
                File.WriteAllText(conversationPath, updated, new UTF8Encoding(false));
                updatedFiles.Add(conversationPath.Replace('\\', '/'));
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            foreach (var updated in updatedFiles) AssetDatabase.ImportAsset(updated, ImportAssetOptions.ForceUpdate);
        }

        public static void SyncOwners(ConversationAsset conversation)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:DialogueProjectAsset"))
            {
                var project = AssetDatabase.LoadAssetAtPath<DialogueProjectAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (project != null && project.conversations.Contains(conversation)) SyncDeclarations(project);
            }
        }
    }
}
