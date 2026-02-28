using System;
using System.Collections.Generic;

[Serializable]
internal sealed class DependencyGraphAdapterModel
{
    public List<DependencyGraphAdapterNode> Nodes = new();
    public List<DependencyGraphAdapterEdge> Edges = new();
}

[Serializable]
internal sealed class DependencyGraphAdapterNode
{
    public string Id;
    public string DisplayName;
    public string TypeName;
    public string UnityObjectPath;
    public List<DependencyGraphAdapterField> Fields = new();
}

[Serializable]
internal sealed class DependencyGraphAdapterField
{
    public string Name;
    public bool HasValue;
    public string ValueSummary;
}

[Serializable]
internal sealed class DependencyGraphAdapterEdge
{
    public string FromNodeId;
    public string ToNodeId;
    public string FieldName;
    public string DependencyKind;
    public bool IsBroken;
    public string Details;
}
