#if UNITY_EDITOR
using UnityEditor;
using static vHierarchy.VUtils;
// using static VTools.VDebug;


#if UNITY_6000_3_OR_NEWER
using ObjectID = UnityEngine.EntityId;
#else
using ObjectID = System.Int32;
#endif






namespace vHierarchy
{
    [FilePath("Library/vHierarchy Cache.asset", FilePathAttribute.Location.ProjectFolder)]
    public class VHierarchyCache : ScriptableSingleton<VHierarchyCache>
    {
        // used for finding SceneData and SceneIdMap for objects that were moved out of their original scene 
        public SerializableDictionary<ObjectID, string> originalSceneGuids_byObjectId = new();

        // used as cache for converting GlobalID to InstanceID and as a way to find GameObjectData for prefabs in playmode (when prefabs produce invalid GlobalIDs)
        public SerializableDictionary<string, SceneIdMap> sceneIdMaps_bySceneGuid = new();

        // used for fetching icons set inside prefab instances in playmode (when prefabs produce invalid GlobalIDs)
        public SerializableDictionary<ObjectID, GlobalID> prefabInstanceGlobalIds_byObjectIds = new();



        [System.Serializable]
        public class SceneIdMap
        {
            public SerializableDictionary<ObjectID, GlobalID> globalIds_byObjectId = new();

            public ObjectID objectIdsHash;
            public int globalIdsHash;

        }







        public static void Clear()
        {
            instance.originalSceneGuids_byObjectId.Clear();
            instance.sceneIdMaps_bySceneGuid.Clear();

        }


    }
}
#endif