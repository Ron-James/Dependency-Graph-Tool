using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;


public class GuidObjectReference : IValueAsset<IGUIDObject>
{
    [SerializeField] private string _guid;

    private IGUIDObject Get()
    {
        return GuidObjectObjectFinder.GetByGuid(_guid);
    }
    [ShowInInspector, ReadOnly]
    public IGUIDObject Value
    {
        get
        {
            return Get();
        }
        set
        {
            if (value != null)
            {
                _guid = value.Guid;
            }
            else
            {
                _guid = string.Empty;
            }
        }
    }

    public T GetAs<T>() where T : class, IGUIDObject
    {
        return Get() as T;
    }
}
public static class GuidObjectObjectFinder
{
    private static Dictionary<string, IGUIDObject> _guidLookup;
    private static List<IGUIDObject> _allObjects;
    private static bool _isInitialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitOnLoad()
    {
        Refresh();
    }

    /// <summary>
    /// Gets a ScriptableObject by GUID. Returns null if not found.
    /// </summary>
    public static IGUIDObject GetByGuid(string guid)
    {
        if (!_isInitialized) return null;
        if (string.IsNullOrEmpty(guid)) return null;
        _guidLookup.TryGetValue(guid, out var obj);
        return obj;
    }
    
    

    /// <summary>
    /// Gets all found IGUIDObject ScriptableObjects.
    /// </summary>
    public static IReadOnlyList<IGUIDObject> GetAll()
    {
        if (!_isInitialized) return null;
        return _allObjects;
    }

    /// <summary>
    /// Refreshes the lookup by reloading all ScriptableObjects from Resources.
    /// </summary>
    public static void Refresh()
    {
        _guidLookup = new Dictionary<string, IGUIDObject>();
        _allObjects = new List<IGUIDObject>();
        var allScriptables = Resources.LoadAll<ScriptableObject>("");
        foreach (var so in allScriptables)
        {
            if (so is IGUIDObject guidObj && !string.IsNullOrEmpty(guidObj.Guid))
            {
                if (_guidLookup.ContainsKey(guidObj.Guid))
                {
#if UNITY_EDITOR
                    Debug.LogWarning($"Duplicate GUID found: {guidObj.Guid} in {so.name}. Skipping.");
#endif
                    continue;
                }
                _guidLookup.Add(guidObj.Guid, guidObj);
                _allObjects.Add(guidObj);
            }
        }
        _isInitialized = true;
    }
}
