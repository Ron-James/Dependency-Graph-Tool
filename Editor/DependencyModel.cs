using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public enum DependencyType
{
    UnityEvent,
    SerializedUnityRef,
    SerializeReferenceManaged,
    OdinSerializedRef,
}

[Serializable]
public class DependencyNode
{
    public string GUID;
    public object Owner;
    public string DisplayName;
    public readonly List<DependencyFieldSlot> FieldSlots = new();
}

[Serializable]
public class DependencyFieldSlot
{
    public string Name;
    public bool IsOutput;
    public bool HasValue;
    public bool IsUnityEvent;
    public string ValueSummary;
}

[Serializable]
public class DependencyEdge
{
    public DependencyNode From;
    public DependencyNode To;
    public string FieldName;
    public DependencyType Type;
    public bool IsBroken;
    public string Details;
    public DependencyActionContext ActionContext;
}

public class DependencyActionContext
{
    public UnityEngine.Object OwnerObject;
    public FieldInfo FieldInfo;
    public int PersistentListenerIndex = -1;
    public UnityEngine.Object UnityReferenceValue;
}

public class DependencyModel
{
    public readonly List<DependencyNode> Nodes = new();
    public readonly List<DependencyEdge> Edges = new();
}
