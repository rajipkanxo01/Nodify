using System.IO;
using ToolForDialogue._Project.Dialogue.Runtime.Assets;
using UnityEditor;
using UnityEngine;
using Yarn.Unity;
using Yarn.Unity.Editor;

namespace ToolForDialogue.Editor._Project.Scripts.Editor.Services
{
    /// <summary>
    /// Creates and persists dialogue project assets and their directly referenced assets.
    /// </summary>
    internal static class DialogueProjectAssetService
    {
        /// <summary>
        /// Finds the first authoring project that still has a valid Yarn project link.
        /// </summary>
        public static DialogueProjectAsset FindFirstProject()
        {
            var projectGuids = AssetDatabase.FindAssets($"t:{nameof(DialogueProjectAsset)}");
            foreach (var projectGuid in projectGuids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(projectGuid);
                var project = AssetDatabase.LoadAssetAtPath<DialogueProjectAsset>(assetPath);
                if (project != null && project.yarnProject != null)
                {
                    return project;
                }
            }

            return null;
        }

        public static DialogueProjectAsset CreateProject()
        {
            var yarnProjectPath = EditorUtility.SaveFilePanelInProject(
                "Create Yarn Project",
                "Dialogue Project",
                "yarnproject",
                "Choose where to save the Yarn project and its dialogue authoring asset.");

            if (string.IsNullOrWhiteSpace(yarnProjectPath))
            {
                return null;
            }

            var yarnProject = CreateYarnProject(yarnProjectPath);
            if (yarnProject == null)
            {
                Debug.LogError($"Yarn Spinner did not import the project at '{yarnProjectPath}'.");
                return null;
            }

            var project = CreateAuthoringAsset(yarnProjectPath, yarnProject);

            Selection.activeObject = project;
            EditorGUIUtility.PingObject(project);
            return project;
        }

        private static YarnProject CreateYarnProject(string assetPath)
        {
            var compilerProject = new Yarn.Compiler.Project
            {
                SourceFilePatterns = new[] { "**/*.yarn" },
                ExcludeFilePatterns = new[] { "**/*~/*", "./Samples/Yarn Spinner*/*" },
                BaseLanguage = "en"
            };

            YarnEditorUtility.CreateYarnProject(assetPath, compilerProject);
            return AssetDatabase.LoadAssetAtPath<YarnProject>(assetPath);
        }

        private static DialogueProjectAsset CreateAuthoringAsset(string yarnProjectPath, YarnProject yarnProject)
        {
            var directory = Path.GetDirectoryName(yarnProjectPath) ?? "Assets";
            var fileName = Path.GetFileNameWithoutExtension(yarnProjectPath);
            var assetPath = Path.Combine(directory, $"{fileName}.asset").Replace('\\', '/');
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

            var project = ScriptableObject.CreateInstance<DialogueProjectAsset>();
            project.Initialize(yarnProject);

            AssetDatabase.CreateAsset(project, assetPath);
            AssetDatabase.SaveAssetIfDirty(project);
            return project;
        }

        public static void SaveProject(DialogueProjectAsset project)
        {
            if (project == null)
            {
                return;
            }

            AssetDatabase.SaveAssetIfDirty(project);

            foreach (var conversation in project.conversations)
            {
                if (conversation != null)
                {
                    AssetDatabase.SaveAssetIfDirty(conversation);
                }
            }

            foreach (var character in project.characters)
            {
                if (character != null)
                {
                    AssetDatabase.SaveAssetIfDirty(character);
                }
            }
        }
    }
}
