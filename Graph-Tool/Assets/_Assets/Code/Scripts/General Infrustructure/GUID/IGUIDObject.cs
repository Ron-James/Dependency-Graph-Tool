using System;
using Sirenix.OdinInspector;
using UnityEngine;

public interface IGUIDObject
{
    string Guid { get; }
}


public abstract class GuidScriptableObject : SerializedScriptableObject, IGUIDObject
{
    [SerializeField, ReadOnly] private string _guid;
    public string Guid => _guid;
    
    [Button("Regenerate GUID"), GUIColor("red"), FoldoutGroup("GUID Settings")]
    protected virtual void AssignGuid()
    {
        _guid = System.Guid.NewGuid().ToString();
    }


    protected virtual void OnValidate()
    {
        if (string.IsNullOrEmpty(_guid))
        {
            AssignGuid();
        }
            
    }

    
}
