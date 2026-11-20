using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ToolForDialogue.Assets;
using ToolForDialogue.Editor.Editor.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Tests.Editor
{
    public sealed class CharacterEditorExtensionTests
    {
        private static readonly Assembly EditorAssembly = typeof(CharacterEditorExtension).Assembly;
        private static bool _enabled;
        private static bool _fail;
        private static readonly List<(CharacterAsset character, DialogueProjectAsset project)> Calls = new();
        private readonly List<UnityEngine.Object> _assets = new();
        private CharacterAsset _character;
        private DialogueProjectAsset _project;

        [SetUp]
        public void SetUp()
        {
            _enabled = true;
            _fail = false;
            Calls.Clear();
            _character = Create<CharacterAsset>();
            _project = Create<DialogueProjectAsset>();
        }

        [TearDown]
        public void TearDown()
        {
            _enabled = false;
            _fail = false;
            Calls.Clear();
            foreach (var asset in _assets) UnityEngine.Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void OrdersByPriorityThenOrdinalTypeNameRegardlessOfDiscoveryOrder()
        {
            var types = new[] { typeof(ZSection), typeof(ASection), typeof(FirstSection) };
            var root = Build(types);
            CollectionAssert.AreEqual(new[] { "FirstSection", "ASection", "ZSection" },
                root.Children().Cast<Label>().Select(label => label.text));
            CollectionAssert.AreEqual(root.Children().Cast<Label>().Select(label => label.text),
                Build(types.Reverse()).Children().Cast<Label>().Select(label => label.text));
        }

        [Test]
        public void SkipsNullAbstractAndOpenGenericSections()
        {
            var root = Build(new[] { typeof(NullSection), typeof(AbstractSection), typeof(GenericSection<>) });
            Assert.That(root.childCount, Is.Zero);
        }

        [Test]
        public void ContainsConstructorOrderAndBuildFailuresAndContinues()
        {
            _fail = true;
            LogAssert.Expect(LogType.Error, new Regex("Nodify character extension .*ConstructorFailure.*initialization"));
            LogAssert.Expect(LogType.Error, new Regex("Nodify character extension .*OrderFailure.*initialization"));
            LogAssert.Expect(LogType.Error, new Regex("Nodify character extension .*BuildFailure.*section creation"));
            var root = Build(new[] { typeof(ConstructorFailure), typeof(OrderFailure), typeof(BuildFailure), typeof(ASection) });
            Assert.That(root.childCount, Is.EqualTo(1));
            Assert.That(((Label)root[0]).text, Is.EqualTo("ASection"));
        }

        [Test]
        public void DiscoversExtensionsInConsumerEditorAssemblyAndRebuildsForSelectionAndProject()
        {
            var secondCharacter = Create<CharacterAsset>();
            var secondProject = Create<DialogueProjectAsset>();
            SetCharacters(_project, _character, secondCharacter);
            SetCharacters(secondProject, secondCharacter);
            var tabType = EditorAssembly.GetType("ToolForDialogue.Editor._Project.Scripts.Editor.Tabs.CharactersTab", true);
            var tab = Activator.CreateInstance(tabType, true);
            var contextType = EditorAssembly.GetType("ToolForDialogue.Editor._Project.Scripts.Editor.Tabs.DialogueEditorTabContext", true);
            var create = tabType.GetMethod("CreateContent");
            object Context(DialogueProjectAsset project) => Activator.CreateInstance(contextType, project, null);

            var root = (VisualElement)create.Invoke(tab, new[] { Context(_project) });
            Assert.That(Calls.Last(), Is.EqualTo((_character, _project)));
            var pane = root.Q(className: "dialogue-character-editor-content");
            var details = pane.Q(className: "dialogue-character-details-panel");
            var section = pane.Children().OfType<Label>().Single(label => label.text == "ASection");
            var expressions = pane.Q(className: "dialogue-expression-heading");
            Assert.That(pane.IndexOf(section), Is.GreaterThan(pane.IndexOf(details)));
            Assert.That(pane.IndexOf(section), Is.LessThan(pane.IndexOf(expressions)));

            var oldSection = section;
            tabType.GetMethod("SelectCharacter", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(tab, new object[] { secondCharacter });
            Assert.That(Calls.Last(), Is.EqualTo((secondCharacter, _project)));
            Assert.That(oldSection.parent, Is.Null);
            create.Invoke(tab, new[] { Context(secondProject) });
            Assert.That(Calls.Last(), Is.EqualTo((secondCharacter, secondProject)));
            var count = Calls.Count;
            create.Invoke(tab, new[] { Context(null) });
            create.Invoke(tab, new[] { Context(Create<DialogueProjectAsset>()) });
            Assert.That(Calls.Count, Is.EqualTo(count));
        }

        [Test]
        public void BuiltInEditorSurvivesExtensionFailuresWithoutChangingCharacterData()
        {
            _fail = true;
            var before = EditorJsonUtility.ToJson(_character);
            SetCharacters(_project, _character);
            LogAssert.Expect(LogType.Error, new Regex("Nodify character extension .*ConstructorFailure.*initialization"));
            LogAssert.Expect(LogType.Error, new Regex("Nodify character extension .*OrderFailure.*initialization"));
            LogAssert.Expect(LogType.Error, new Regex("Nodify character extension .*BuildFailure.*section creation"));
            var tabType = EditorAssembly.GetType("ToolForDialogue.Editor._Project.Scripts.Editor.Tabs.CharactersTab", true);
            var contextType = EditorAssembly.GetType("ToolForDialogue.Editor._Project.Scripts.Editor.Tabs.DialogueEditorTabContext", true);
            var root = (VisualElement)tabType.GetMethod("CreateContent").Invoke(
                Activator.CreateInstance(tabType, true),
                new[] { Activator.CreateInstance(contextType, _project, null) });
            Assert.That(root.Q(className: "dialogue-character-details-panel"), Is.Not.Null);
            Assert.That(root.Q(className: "dialogue-expression-heading"), Is.Not.Null);
            Assert.That(root.Query<Label>().ToList().Any(label => label.text == "ASection"), Is.True);
            Assert.That(EditorJsonUtility.ToJson(_character), Is.EqualTo(before));
        }

        private VisualElement Build(IEnumerable<Type> types)
        {
            var root = new VisualElement();
            var service = EditorAssembly.GetType(
                "ToolForDialogue.Editor._Project.Scripts.Editor.Services.CharacterEditorExtensionService", true);
            service.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Single(method => method.Name == "AddSections" && method.GetParameters().Length == 4)
                .Invoke(null, new object[] { root, _character, _project, types });
            return root;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }

        private static void SetCharacters(DialogueProjectAsset project, params CharacterAsset[] characters)
        {
            var serialized = new SerializedObject(project);
            var list = serialized.FindProperty("_characters");
            list.arraySize = characters.Length;
            for (var index = 0; index < characters.Length; index++)
                list.GetArrayElementAtIndex(index).objectReferenceValue = characters[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Consumer-side fixtures: inert outside tests, with no production friend assembly needed.
        public class ASection : CharacterEditorExtension
        {
            public override VisualElement CreateSection(CharacterAsset character, DialogueProjectAsset project)
            {
                if (!_enabled) return null;
                Calls.Add((character, project));
                return new Label(GetType().Name);
            }
        }
        public sealed class ZSection : ASection { }
        public sealed class FirstSection : ASection { public override int order => -10; }
        public sealed class NullSection : CharacterEditorExtension
        {
            public override VisualElement CreateSection(CharacterAsset character, DialogueProjectAsset project) => null;
        }
        public abstract class AbstractSection : CharacterEditorExtension { }
        public sealed class GenericSection<T> : ASection { }
        public sealed class ConstructorFailure : ASection
        {
            public ConstructorFailure() { if (_fail) throw new InvalidOperationException("constructor test"); }
        }
        public sealed class OrderFailure : ASection
        {
            public override int order => _fail ? throw new InvalidOperationException("order test") : 0;
        }
        public sealed class BuildFailure : ASection
        {
            public override VisualElement CreateSection(CharacterAsset character, DialogueProjectAsset project)
                => _fail ? throw new InvalidOperationException("build test") : null;
        }
    }
}
