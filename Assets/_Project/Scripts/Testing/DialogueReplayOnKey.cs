using UnityEngine;
using UnityEngine.InputSystem;
using Yarn.Unity;

namespace ToolForDialogue.Testing
{
    /// <summary>Starts another interaction without resetting the runner's variable storage.</summary>
    public sealed class DialogueReplayOnKey : MonoBehaviour
    {
        [SerializeField] private DialogueRunner _runner;
        [SerializeField] private Key _interactionKey = Key.E;
        private bool _starting;

        private void Awake()
        {
            if (_runner == null) _runner = FindFirstObjectByType<DialogueRunner>();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[_interactionKey].wasPressedThisFrame)
                PlayAgain().Forget();
        }

        public async YarnTask PlayAgain()
        {
            if (_starting || _runner == null || _runner.IsDialogueRunning) return;
            _starting = true;
            try
            {
                await _runner.StartDialogue(_runner.startNode);
            }
            finally
            {
                _starting = false;
            }
        }
    }
}
