#if UNITY_EDITOR
using UnityEditor;
using static vFavorites.VUtils;


namespace vFavorites
{
    [FilePath("Library/vFavorites State.asset", FilePathAttribute.Location.ProjectFolder)]
    public class VFavoritesState : ScriptableSingleton<VFavoritesState>
    {
        public int curPageIndex;

        public SerializableDictionary<int, PageState> pageStates_byPageId = new();

        public SerializableDictionary<int, ItemState> itemStates_byItemId = new();


        [System.Serializable]
        public class PageState
        {
            public long lastItemSelectTime_ticks;
            public long lastItemDragTime_ticks;

            public float scrollPos;

        }

        [System.Serializable]
        public class ItemState
        {
            public string _name;

            public string sceneGameObjectIconName;

            public long lastSelectTime_ticks;
            public bool isSelected;

        }


        public static void Save() => instance.Save(true);

    }
}
#endif