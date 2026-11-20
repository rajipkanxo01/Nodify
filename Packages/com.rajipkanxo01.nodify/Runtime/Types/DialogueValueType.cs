namespace ToolForDialogue.Types
{
    /// <summary>
    /// Identifies the active field in a SerializedValue instance.
    /// </summary>
    public enum DialogueValueType
    {
        Boolean,
        Integer,
        Number,
        String,
        Enum,
        UnityObject,
        EntityReference,
        SceneBinding
    }
}
