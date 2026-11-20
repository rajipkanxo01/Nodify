using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ToolForDialogue.Assets;
using ToolForDialogue.Editor.Editor.Services;
using ToolForDialogue.Editor.Editor.Tabs;
using ToolForDialogue.Types;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tests.Commands
{
    public sealed class CommandTests
    {
        private DialogueProjectAsset _project;
        private ActionElement _action;
        private CommandDefinition _command;

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        [SetUp]
        public void SetUp()
        {
            _project = ScriptableObject.CreateInstance<DialogueProjectAsset>();
            _command = new CommandDefinition();
            Set(_command, "_name", "play_song_and_wait");
            Set(_project, "_commands", new System.Collections.Generic.List<CommandDefinition> { _command });
            _action = new ActionElement();
            Set(_action, "_commandProject", _project);
            Set(_action, "_commandId", _command.id.value);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_project);

        private CommandParameter AddArgument(string name, DialogueValueType type, object value)
        {
            var parameter = new CommandParameter();
            Set(parameter, "_name", name);
            Set(parameter, "_type", type);
            ((System.Collections.Generic.List<CommandParameter>)_command.parameters).Add(parameter);
            var argument = new CommandArgument();
            Set(argument, "_parameterId", parameter.id.value);
            Set(argument.value, "_type", type);
            var field = type switch
            {
                DialogueValueType.String => "_stringValue",
                DialogueValueType.Boolean => "_booleanValue",
                DialogueValueType.Integer => "_integerValue",
                _ => "_numberValue"
            };
            Set(argument.value, field, value);
            ((System.Collections.Generic.List<CommandArgument>)_action.arguments).Add(argument);
            return parameter;
        }

        [Test]
        public void EmitsTypedArgumentsUsingInvariantCulture()
        {
            AddArgument("song", DialogueValueType.String, "evening theme");
            AddArgument("loop", DialogueValueType.Boolean, false);
            AddArgument("track", DialogueValueType.Integer, 3);
            AddArgument("volume", DialogueValueType.Number, 0.75f);
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("da-DK");
                Assert.That(CommandYarnUtility.TryWrite(_action, out var source, out var error), Is.True, error);
                Assert.That(source, Is.EqualTo("<<play_song_and_wait \"evening theme\" false 3 0.75>>"));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [TestCase("")]
        [TestCase("evening theme")]
        [TestCase("a \"quoted\" song")]
        [TestCase("music\\evening")]
        public void CompiledCommandPreservesStringAndPausesBeforeNextLine(string song)
        {
            AddArgument("song", DialogueValueType.String, song);
            Assert.That(CommandYarnUtility.TryWrite(_action, out var command, out var error), Is.True, error);
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".yarn");
            try
            {
                File.WriteAllText(path, "title: Start\n---\n" + command + "\nThe song finished.\n===\n");
                var result = Yarn.Compiler.Compiler.Compile(Yarn.Compiler.CompilationJob.CreateFromFiles(new[] { path }));
                Assert.That(result.Diagnostics.Where(item => item.Severity == Yarn.Compiler.Diagnostic.DiagnosticSeverity.Error), Is.Empty);
                var dialogue = new Yarn.Dialogue(new Yarn.MemoryVariableStore());
                string[] dispatched = null;
                var lines = 0;
                dialogue.SetProgram(result.Program);
                dialogue.CommandHandler = value => dispatched = Yarn.Unity.DialogueRunner.SplitCommandText(value.Text).ToArray();
                dialogue.LineHandler = _ => lines++;
                dialogue.OptionsHandler = _ => Assert.Fail("No choices expected.");
                dialogue.SetNode("Start");
                dialogue.Continue();
                CollectionAssert.AreEqual(new[] { "play_song_and_wait", song }, dispatched);
                Assert.That(lines, Is.Zero, "Dialogue must remain at the command until continued.");
                dialogue.Continue();
                Assert.That(lines, Is.EqualTo(1));
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void RenameAndReorderKeepValuesByStableIdentity()
        {
            var song = AddArgument("song", DialogueValueType.String, "evening");
            var volume = AddArgument("volume", DialogueValueType.Number, 0.5f);
            Set(_command, "_name", "music");
            Set(song, "_name", "clip");
            Set(_command, "_parameters", new System.Collections.Generic.List<CommandParameter> { volume, song });
            Assert.That(CommandYarnUtility.TryWrite(_action, out var source, out var error), Is.True, error);
            Assert.That(source, Is.EqualTo("<<music 0.5 \"evening\">>"));
        }

        [Test]
        public void ChangedTypeAndMissingArgumentRequireExplicitValue()
        {
            var parameter = AddArgument("song", DialogueValueType.String, "evening");
            Set(parameter, "_type", DialogueValueType.Integer);
            Assert.That(CommandYarnUtility.TryWrite(_action, out _, out _), Is.False);
            ((System.Collections.Generic.List<CommandArgument>)_action.arguments).Clear();
            Assert.That(CommandYarnUtility.TryWrite(_action, out _, out _), Is.False);
        }

        [Test]
        public void DeletedCommandReportsMissingDefinition()
        {
            Set(_project, "_commands", new System.Collections.Generic.List<CommandDefinition>());
            Assert.That(CommandYarnUtility.TryWrite(_action, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("Choose a command"));
        }

        [TestCase("if")]
        [TestCase("bad name")]
        [TestCase("stop")]
        public void RejectsInvalidOrReservedNames(string name)
        {
            Set(_command, "_name", name);
            Assert.That(CommandYarnUtility.TryWrite(_action, out _, out _), Is.False);
        }

        [TestCase("song\n<<stop>>")]
        [TestCase("{$variable}")]
        [TestCase("song>>")]
        public void RejectsYarnDelimitersInStringArguments(string value)
        {
            AddArgument("song", DialogueValueType.String, value);
            Assert.That(CommandYarnUtility.TryWrite(_action, out _, out _), Is.False);
        }

        [Test]
        public void CommandsTabBuildsEditableSignature()
        {
            AddArgument("song", DialogueValueType.String, "evening");
            var content = new CommandsTab().CreateContent(new DialogueEditorTabContext(_project, _ => { }));
            Assert.That(content.Query<TextField>().ToList().Any(field => field.value == "play_song_and_wait"), Is.True);
            Assert.That(content.Query<TextField>().ToList().Any(field => field.value == "song"), Is.True);
        }

        [Test]
        public void ConversationGenerationIncludesCommandsInsideConditionalBranches()
        {
            var conversation = ScriptableObject.CreateInstance<ConversationAsset>();
            try
            {
                AddArgument("song", DialogueValueType.String, "evening");
                var passage = new PassageData();
                var branch = new ConditionalBranch();
                Set(branch, "_condition", new ToolForDialogue.Conditions.BooleanVariableCondition());
                // Use an else-only action in addition to a top-level call to exercise recursive generation.
                var conditional = new ConditionalElement();
                Set(conditional, "_elseElements", new System.Collections.Generic.List<DialogueElement> { _action });
                // A declared project variable supplies the branch condition.
                var variable = new VariableDefinition();
                Set(variable, "_displayName", "ready");
                Set(_project, "_variables", new System.Collections.Generic.List<VariableDefinition> { variable });
                var condition = new ToolForDialogue.Conditions.BooleanVariableCondition();
                Set(condition.variable, "_project", _project);
                Set(condition.variable, "_variableId", variable.id);
                Set(branch, "_condition", condition);
                Set(conditional, "_branches", new System.Collections.Generic.List<ConditionalBranch> { branch });
                Set(passage, "_elements", new System.Collections.Generic.List<DialogueElement> { _action, conditional, new EndElement() });
                Set(conversation, "_passages", new System.Collections.Generic.List<PassageData> { passage });
                Set(conversation, "_startPassageId", passage.id);
                var arguments = new object[] { conversation, "Test.yarn", null, null };
                var method = typeof(ConversationYarnGenerator).GetMethod("TryBuildSource", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(method.Invoke(null, arguments), Is.True, arguments[3] as string);
                var source = (string)arguments[2];
                Assert.That(source.Split(new[] { "<<play_song_and_wait" }, StringSplitOptions.None).Length - 1, Is.EqualTo(2));
                Assert.That(source, Does.Contain("<<else>>\n<<play_song_and_wait"));
            }
            finally { UnityEngine.Object.DestroyImmediate(conversation); }
        }

        [Test]
        public void LineGroupIncludesMixedCommandsAndVariableAssignments()
        {
            var elements = new System.Collections.Generic.List<DialogueElement>
                { new LineElement(), _action, new SetVariableElement(), new ActionElement(), new LineElement() };
            var type = typeof(CommandsTab).Assembly.GetType("ToolForDialogue.Editor.Editor.Tabs.Conversation.ConversationsTab");
            var method = type.GetMethod("LineGroupEnd", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method.Invoke(null, new object[] { elements, 0 }), Is.EqualTo(4));
        }
    }
}
