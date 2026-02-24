using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

internal sealed class DependencyNodeRegistry
{
    private readonly Dictionary<int, DependencyNode> _unityNodes = new();
    private readonly Dictionary<object, DependencyNode> _managedNodes = new(ReferenceEqualityComparer.Instance);
    private readonly List<DependencyNode> _nodes;

    public DependencyNodeRegistry(List<DependencyNode> nodes)
    {
        _nodes = nodes;
    }

    public DependencyNode GetOrCreateNode(object owner, string fallbackName = null)
    {
        if (owner == null)
        {
            return GetOrCreateManagedNode(new NullNodePlaceholder(fallbackName ?? "Null"), fallbackName ?? "Null");
        }

        if (owner is UnityEngine.Object unityObject)
        {
            if (unityObject == null)
            {
                return GetOrCreateManagedNode(
                    new NullNodePlaceholder(fallbackName ?? "Missing Unity Object"),
                    fallbackName ?? "Missing Unity Object");
            }

            return GetOrCreateUnityNode(unityObject, fallbackName);
        }

        return GetOrCreateManagedNode(owner, fallbackName);
    }

    private DependencyNode GetOrCreateUnityNode(UnityEngine.Object owner, string fallbackName)
    {
        if (owner == null)
        {
            return GetOrCreateManagedNode(
                new NullNodePlaceholder(fallbackName ?? "Missing Unity Object"),
                fallbackName ?? "Missing Unity Object");
        }

        var id = owner.GetInstanceID();
        if (_unityNodes.TryGetValue(id, out var node))
        {
            return node;
        }

        node = new DependencyNode
        {
            GUID = $"unity-{id}",
            Owner = owner,
            DisplayName = GraphNamingUtility.BuildNodeDisplayName(
                owner,
                fallbackName ?? $"{owner.name} ({TypeUtility.GetFriendlyTypeName(owner.GetType())})"),
        };

        _unityNodes[id] = node;
        _nodes.Add(node);
        return node;
    }

    private DependencyNode GetOrCreateManagedNode(object owner, string fallbackName)
    {
        if (_managedNodes.TryGetValue(owner, out var node))
        {
            return node;
        }

        node = new DependencyNode
        {
            GUID = $"managed-{RuntimeHelpers.GetHashCode(owner)}",
            Owner = owner,
            DisplayName = GraphNamingUtility.BuildNodeDisplayName(
                owner,
                fallbackName ?? TypeUtility.GetFriendlyTypeName(owner.GetType())),
        };

        _managedNodes[owner] = node;
        _nodes.Add(node);
        return node;
    }

    private sealed class NullNodePlaceholder
    {
        private readonly string _name;

        public NullNodePlaceholder(string name)
        {
            _name = name;
        }

        public override string ToString()
        {
            return _name;
        }
    }
}

internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
{
    public static readonly ReferenceEqualityComparer Instance = new();

    public new bool Equals(object x, object y)
    {
        return ReferenceEquals(x, y);
    }

    public int GetHashCode(object obj)
    {
        return RuntimeHelpers.GetHashCode(obj);
    }
}
