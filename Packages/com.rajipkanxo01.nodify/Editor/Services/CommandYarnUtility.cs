using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ToolForDialogue.Assets;
using ToolForDialogue.Types;

namespace ToolForDialogue.Editor.Editor.Services
{
    internal static class CommandYarnUtility
    {
        private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
        { "if", "elseif", "else", "endif", "set", "declare", "jump", "stop", "call", "return", "detour", "enum", "case", "endenum", "local", "once", "endonce" };

        public static bool IsSupported(DialogueValueType type) => type == DialogueValueType.String
            || type == DialogueValueType.Boolean || type == DialogueValueType.Integer || type == DialogueValueType.Number;

        public static CommandDefinition Resolve(ActionElement action) => action?.commandProject == null ? null
            : action.commandProject.commands.FirstOrDefault(command => command != null && command.id.value == action.commandId);

        public static string NameError(string name) => string.IsNullOrEmpty(name)
            || !Regex.IsMatch(name, @"\A[A-Za-z_][A-Za-z0-9_]*\z")
            ? "Use a name beginning with a letter or underscore, followed by letters, digits or underscores."
            : Reserved.Contains(name) ? "This name is reserved by Yarn." : null;

        public static string DefinitionError(DialogueProjectAsset project, CommandDefinition command)
        {
            if (command == null) return "Choose a command from the Commands tab.";
            if (!command.id.isValid || project.commands.Count(item => item != null && item.id == command.id) != 1)
                return "Invalid or duplicate command identity.";
            var error = NameError(command.name);
            if (error != null) return error;
            if (project.commands.Count(item => item != null && item.name == command.name) != 1)
                return "Command names must be unique.";
            var ids = new HashSet<string>();
            var names = new HashSet<string>();
            foreach (var parameter in command.parameters)
            {
                if (parameter == null || !parameter.id.isValid || !ids.Add(parameter.id.value)) return "Invalid or duplicate parameter identity.";
                if (NameError(parameter.name) != null || !names.Add(parameter.name)) return "Parameters need valid, unique names.";
                if (!IsSupported(parameter.type)) return "Supported parameter types: String, Boolean, Integer and Number.";
            }
            return null;
        }

        public static bool TryWrite(ActionElement action, out string source, out string error)
        {
            source = string.Empty;
            var command = Resolve(action);
            error = DefinitionError(action?.commandProject, command);
            if (error != null) return false;
            var builder = new StringBuilder("<<").Append(command.name);
            foreach (var parameter in command.parameters)
            {
                var arguments = action.arguments.Where(argument => argument != null && argument.parameterId == parameter.id.value).ToList();
                if (arguments.Count != 1 || arguments[0].value == null || arguments[0].value.type != parameter.type)
                {
                    error = $"Command '{command.name}': set argument '{parameter.name}' ({parameter.type}).";
                    return false;
                }
                var value = arguments[0].value;
                builder.Append(' ');
                switch (parameter.type)
                {
                    case DialogueValueType.Boolean: builder.Append(value.booleanValue ? "true" : "false"); break;
                    case DialogueValueType.Integer: builder.Append(value.integerValue.ToString(CultureInfo.InvariantCulture)); break;
                    case DialogueValueType.Number:
                        if (float.IsNaN(value.numberValue) || float.IsInfinity(value.numberValue))
                        { error = "Command numbers must be finite."; return false; }
                        builder.Append(value.numberValue.ToString("R", CultureInfo.InvariantCulture));
                        break;
                    case DialogueValueType.String:
                        var text = value.stringValue ?? string.Empty;
                        // These delimiters have meaning to Yarn before its command handler sees the text.
                        if (text.IndexOfAny(new[] { '\r', '\n', '{', '}' }) >= 0 || text.Contains(">>"))
                        { error = "Command strings cannot contain newlines, braces or >>."; return false; }
                        builder.Append('"').Append(text.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
                        break;
                }
            }
            source = builder.Append(">>").ToString();
            return true;
        }
    }
}
