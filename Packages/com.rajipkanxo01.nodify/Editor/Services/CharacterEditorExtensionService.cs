using System;
using System.Collections.Generic;
using System.Linq;
using ToolForDialogue.Assets;
using ToolForDialogue.Editor.Editor.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToolForDialogue.Editor.Editor.Services
{
    /// <summary>
    /// Discovers and builds independent character sections without exposing the tab implementation.
    /// </summary>
    internal static class CharacterEditorExtensionService
    {
        internal static void AddSections(
            VisualElement container, CharacterAsset character, DialogueProjectAsset project)
        {
            AddSections(container, character, project, TypeCache.GetTypesDerivedFrom<CharacterEditorExtension>());
        }

        internal static void AddSections(
            VisualElement container, CharacterAsset character, DialogueProjectAsset project,
            IEnumerable<Type> types)
        {
            if (character == null || project == null)
            {
                return;
            }

            var extensions = new List<(CharacterEditorExtension extension, int order, string typeName)>();
            foreach (var type in types.OrderBy(candidate => candidate.AssemblyQualifiedName, StringComparer.Ordinal))
            {
                if (type.IsAbstract || type.ContainsGenericParameters
                    || !typeof(CharacterEditorExtension).IsAssignableFrom(type))
                {
                    continue;
                }

                try
                {
                    var extension = (CharacterEditorExtension)Activator.CreateInstance(type);
                    extensions.Add((extension, extension.order, type.AssemblyQualifiedName));
                }
                catch (Exception exception)
                {
                    ReportFailure(type, "initialization (constructor or order)", character, project, exception);
                }
            }

            foreach (var entry in extensions.OrderBy(entry => entry.order)
                         .ThenBy(entry => entry.typeName, StringComparer.Ordinal))
            {
                try
                {
                    var section = entry.extension.CreateSection(character, project);
                    if (section != null)
                    {
                        container.Add(section);
                    }
                }
                catch (Exception exception)
                {
                    ReportFailure(entry.extension.GetType(), "section creation", character, project, exception);
                }
            }
        }

        private static void ReportFailure(
            Type type, string stage, CharacterAsset character, DialogueProjectAsset project, Exception exception)
        {
            Debug.LogError(
                $"Nodify character extension '{type.AssemblyQualifiedName}' failed during {stage} "
                + $"for character '{character.name}' ({AssetDatabase.GetAssetPath(character)}) "
                + $"in project '{project.name}' ({AssetDatabase.GetAssetPath(project)}).\n{exception}", character);
        }
    }
}
