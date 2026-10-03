using System;
using System.Collections.Generic;
using System.Linq;
using ToolForDialogue._Project.Dialogue.Runtime.Assets;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToolForDialogue.Editor._Project.Scripts.Editor.Tabs
{
    /// <summary>
    /// Presents project-level information and links to the assets owned by the selected project.
    /// </summary>
    internal sealed class ProjectTab : DialogueEditorTabBase
    {
        private sealed class ProjectIssue
        {
            public ProjectIssue(string message, bool isError = false)
            {
                this.message = message;
                this.isError = isError;
            }

            public string message { get; }
            public bool isError { get; }
        }

        public override DialogueEditorTabId id => DialogueEditorTabId.Project;
        public override string label => "Project";

        protected override void BuildContent(
            VisualElement container,
            DialogueEditorTabContext context)
        {
            var project = context.selectedProject;
            var issues = FindProjectIssues(project);

            var description = new Label(
                "Overview, health, and shortcuts for the selected dialogue project.");
            description.AddToClassList(DialogueEditorStyleClasses.PanelDescription);
            container.Add(description);

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.AddToClassList(DialogueEditorStyleClasses.ProjectScrollView);
            var content = new VisualElement();
            content.AddToClassList(DialogueEditorStyleClasses.ProjectDashboard);
            scrollView.Add(content);

            content.Add(CreateProjectDetails(project, issues));
            content.Add(CreateSummary(project, issues.Count, context));
            content.Add(CreateQuickActions(context));
            content.Add(CreateProjectHealth(project, issues, context));
            container.Add(scrollView);
        }

        private static VisualElement CreateProjectDetails(
            DialogueProjectAsset project,
            IReadOnlyList<ProjectIssue> issues)
        {
            var card = new VisualElement();
            card.AddToClassList(DialogueEditorStyleClasses.ProjectCard);

            var header = new VisualElement();
            header.AddToClassList(DialogueEditorStyleClasses.ProjectSectionHeader);
            var headerText = new VisualElement();
            var title = new Label("Project Overview");
            title.AddToClassList(DialogueEditorStyleClasses.SectionTitle);
            headerText.Add(title);
            var subtitle = new Label(issues.Count == 0
                ? "Project is ready for authoring"
                : $"{issues.Count} item{(issues.Count == 1 ? string.Empty : "s")} need attention");
            subtitle.AddToClassList(DialogueEditorStyleClasses.ProjectSectionSubtitle);
            headerText.Add(subtitle);
            header.Add(headerText);

            var revealProject = new Button(() => RevealAsset(project)) { text = "Reveal Project" };
            header.Add(revealProject);
            card.Add(header);

            var serializedProject = new SerializedObject(project);
            serializedProject.Update();
            var displayNameProperty = serializedProject.FindProperty("_displayName");
            var projectName = new TextField("Project Name") { isDelayed = true };
            projectName.SetValueWithoutNotify(displayNameProperty.stringValue);
            projectName.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(project, "Rename Dialogue Project");
                serializedProject.Update();
                displayNameProperty.stringValue = evt.newValue;
                serializedProject.ApplyModifiedProperties();
                EditorUtility.SetDirty(project);
            });
            card.Add(projectName);

            var projectPath = AssetDatabase.GetAssetPath(project);
            card.Add(CreateAssetRow("Project Asset", projectPath, () => RevealAsset(project)));

            if (project.yarnProject != null)
            {
                var yarnPath = AssetDatabase.GetAssetPath(project.yarnProject);
                card.Add(CreateAssetRow(
                    "Yarn Project",
                    yarnPath,
                    () => AssetDatabase.OpenAsset(project.yarnProject),
                    "Open"));
            }
            else
            {
                var missingYarn = new HelpBox(
                    "No Yarn Spinner project is linked.",
                    HelpBoxMessageType.Error);
                card.Add(missingYarn);
            }

            return card;
        }

        private static VisualElement CreateAssetRow(
            string label,
            string path,
            Action action,
            string actionLabel = "Reveal")
        {
            var row = new VisualElement();
            row.AddToClassList(DialogueEditorStyleClasses.ProjectAssetRow);
            var pathField = new TextField(label)
            {
                value = path,
                isReadOnly = true
            };
            pathField.AddToClassList(DialogueEditorStyleClasses.ProjectAssetPath);
            row.Add(pathField);
            row.Add(new Button(action) { text = actionLabel });
            return row;
        }

        private static VisualElement CreateSummary(
            DialogueProjectAsset project,
            int issueCount,
            DialogueEditorTabContext context)
        {
            var section = new VisualElement();
            section.AddToClassList(DialogueEditorStyleClasses.ProjectSection);
            var heading = new Label("At a Glance");
            heading.AddToClassList(DialogueEditorStyleClasses.SectionTitle);
            section.Add(heading);

            var cards = new VisualElement();
            cards.AddToClassList(DialogueEditorStyleClasses.ProjectStats);
            cards.Add(CreateStatCard(
                "Conversations",
                project.conversations.Count.ToString(),
                () => context.SelectTab(DialogueEditorTabId.Conversations)));
            cards.Add(CreateStatCard(
                "Characters",
                project.characters.Count.ToString(),
                () => context.SelectTab(DialogueEditorTabId.Characters)));
            cards.Add(CreateStatCard(
                "Variables",
                project.variables.Count.ToString(),
                () => context.SelectTab(DialogueEditorTabId.Variables)));
            cards.Add(CreateStatCard(
                "Validation",
                issueCount == 0 ? "Clear" : issueCount.ToString(),
                () => context.SelectTab(DialogueEditorTabId.Validation),
                issueCount > 0));
            section.Add(cards);
            return section;
        }

        private static VisualElement CreateStatCard(
            string title,
            string value,
            Action open,
            bool hasWarning = false)
        {
            var card = new Button(open);
            card.AddToClassList(DialogueEditorStyleClasses.ProjectStatCard);
            card.EnableInClassList(DialogueEditorStyleClasses.ProjectStatWarning, hasWarning);
            var valueLabel = new Label(value);
            valueLabel.AddToClassList(DialogueEditorStyleClasses.ProjectStatValue);
            card.Add(valueLabel);
            var titleLabel = new Label(title);
            titleLabel.AddToClassList(DialogueEditorStyleClasses.ProjectStatTitle);
            card.Add(titleLabel);
            return card;
        }

        private static VisualElement CreateQuickActions(DialogueEditorTabContext context)
        {
            var section = new VisualElement();
            section.AddToClassList(DialogueEditorStyleClasses.ProjectSection);
            var heading = new Label("Quick Actions");
            heading.AddToClassList(DialogueEditorStyleClasses.SectionTitle);
            section.Add(heading);

            var actions = new VisualElement();
            actions.AddToClassList(DialogueEditorStyleClasses.ProjectQuickActions);
            actions.Add(new Button(() => context.SelectTab(DialogueEditorTabId.Conversations))
                { text = "Open Conversations" });
            actions.Add(new Button(() => context.SelectTab(DialogueEditorTabId.Characters))
                { text = "Open Characters" });
            actions.Add(new Button(() => context.SelectTab(DialogueEditorTabId.Variables))
                { text = "Review Variables" });
            actions.Add(new Button(() => context.SelectTab(DialogueEditorTabId.Validation))
                { text = "Review Validation" });
            actions.Add(new Button(() => context.SelectTab(DialogueEditorTabId.Settings))
                { text = "Generation Settings" });
            section.Add(actions);
            return section;
        }

        private static VisualElement CreateProjectHealth(
            DialogueProjectAsset project,
            IReadOnlyList<ProjectIssue> issues,
            DialogueEditorTabContext context)
        {
            var section = new VisualElement();
            section.AddToClassList(DialogueEditorStyleClasses.ProjectSection);
            var header = new VisualElement();
            header.AddToClassList(DialogueEditorStyleClasses.ProjectSectionHeader);
            var heading = new Label("Project Health");
            heading.AddToClassList(DialogueEditorStyleClasses.SectionTitle);
            header.Add(heading);
            if (issues.Count > 0)
            {
                header.Add(new Button(() => context.SelectTab(DialogueEditorTabId.Validation))
                    { text = "Review All" });
            }
            section.Add(header);

            var healthList = new VisualElement();
            healthList.AddToClassList(DialogueEditorStyleClasses.ProjectHealthList);
            if (project.yarnProject != null)
            {
                healthList.Add(CreateHealthRow("✓", "Yarn Spinner project linked", false));
            }
            if (!string.IsNullOrWhiteSpace(project.generation.outputFolder))
            {
                healthList.Add(CreateHealthRow(
                    "✓",
                    $"Generation output: {project.generation.outputFolder}",
                    false));
            }

            if (issues.Count == 0)
            {
                healthList.Add(CreateHealthRow("✓", "No immediate project issues found", false));
            }
            else
            {
                foreach (var issue in issues.Take(4))
                {
                    healthList.Add(CreateHealthRow(issue.isError ? "×" : "!", issue.message, true));
                }
            }

            section.Add(healthList);
            return section;
        }

        private static VisualElement CreateHealthRow(string icon, string message, bool warning)
        {
            var row = new VisualElement();
            row.AddToClassList(DialogueEditorStyleClasses.ProjectHealthRow);
            row.EnableInClassList(DialogueEditorStyleClasses.ProjectHealthWarning, warning);
            var iconLabel = new Label(icon);
            iconLabel.AddToClassList(DialogueEditorStyleClasses.ProjectHealthIcon);
            row.Add(iconLabel);
            row.Add(new Label(message));
            return row;
        }

        private static List<ProjectIssue> FindProjectIssues(DialogueProjectAsset project)
        {
            var issues = new List<ProjectIssue>();
            if (project.yarnProject == null)
            {
                issues.Add(new ProjectIssue("Yarn Spinner project link is missing.", true));
            }
            if (project.characters.Any(character => character == null))
            {
                issues.Add(new ProjectIssue("The character list contains a missing asset reference.", true));
            }
            if (project.conversations.Any(conversation => conversation == null))
            {
                issues.Add(new ProjectIssue("The conversation list contains a missing asset reference.", true));
            }
            if (project.characters
                .Where(character => character != null)
                .GroupBy(character => character.displayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() > 1))
            {
                issues.Add(new ProjectIssue("Two or more characters have the same display name."));
            }
            if (project.variables
                .Where(variable => variable != null)
                .GroupBy(variable => variable.displayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() > 1))
            {
                issues.Add(new ProjectIssue("Two or more variables have the same display name."));
            }
            if (string.IsNullOrWhiteSpace(project.generation.outputFolder))
            {
                issues.Add(new ProjectIssue("The generated Yarn output folder is not configured."));
            }
            return issues;
        }

        private static void RevealAsset(UnityEngine.Object asset)
        {
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
