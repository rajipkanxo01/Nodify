#if UNITY_EDITOR
using UnityEditor;
using static vInspector.VUtils;

// using static VTools.VDebug;


namespace vInspector
{
    [FilePath("Library/vInspector State.asset", FilePathAttribute.Location.ProjectFolder)]
    public class VInspectorState : ScriptableSingleton<VInspectorState>
    {

        public SerializableDictionary<string, AttributesState> attributeStates_byScriptName = new();

        [System.Serializable]
        public class AttributesState
        {
            public SerializableDictionary<string, int> selectedSubtabIndexes_byTabPath = new();
            public SerializableDictionary<string, bool> isExpandeds_byFoldoutPath = new();
            public SerializableDictionary<string, bool> isExpandeds_byButtonPath = new();

        }






        public SerializableDictionary<int, BookmarkState> bookmarkStates_byBookmarkId = new();

        [System.Serializable]
        public class BookmarkState
        {
            public string _name;
            public string sceneGameObjectIconName;
        }






        public static void Clear()
        {
            instance.attributeStates_byScriptName.Clear();
            instance.bookmarkStates_byBookmarkId.Clear();

        }

        public static void Save() => instance.Save(true);

    }
}
#endif