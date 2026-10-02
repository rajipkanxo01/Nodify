using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToolForDialogue._Project.Dialogue.Runtime.Types
{
    /// <summary>
    /// Presents a set of player-selectable options in the current passage.
    /// </summary>
    [Serializable]
    public sealed class ChoiceElement : DialogueElement
    {
        [SerializeField] private List<ChoiceOption> _options = new List<ChoiceOption>();

        public IReadOnlyList<ChoiceOption> options => _options;
    }
}
