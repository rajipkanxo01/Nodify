using ToolForDialogue.Assets;
using UnityEngine.UIElements;

namespace ToolForDialogue.Editor.Editor.Extensions
{
    /// <summary>
    /// Adds a project-owned UI Toolkit section between Character Details and Expressions.
    /// Concrete, non-generic types with public parameterless constructors are discovered automatically.
    /// </summary>
    public abstract class CharacterEditorExtension
    {
        /// <summary>
        /// Lower values appear first. Ties use the ordinal assembly-qualified type name.
        /// </summary>
        public virtual int order => 0;

        /// <summary>
        /// Returns a fresh section for the selected assets, or null when not applicable.
        /// A fresh extension instance is created on each character editor rebuild.
        /// Implementations own headings, bindings, Undo, persistence, and callback cleanup.
        /// </summary>
        public abstract VisualElement CreateSection(CharacterAsset character, DialogueProjectAsset project);
    }
}
