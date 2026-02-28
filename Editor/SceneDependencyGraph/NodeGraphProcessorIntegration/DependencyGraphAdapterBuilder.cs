using System;
using System.Linq;
using UnityEngine;

internal static class DependencyGraphAdapterBuilder
{
    public static DependencyGraphAdapterModel Build(DependencyModel model)
    {
        var adapted = new DependencyGraphAdapterModel();
        if (model == null)
        {
            return adapted;
        }

        foreach (var node in model.Nodes)
        {
            if (node == null)
            {
                continue;
            }

            var adaptedNode = new DependencyGraphAdapterNode
            {
                Id = node.GUID,
                DisplayName = node.DisplayName,
                TypeName = TypeUtility.GetFriendlyTypeName(node.Owner?.GetType()),
                UnityObjectPath = GetUnityObjectPath(node.Owner as UnityEngine.Object),
            };

            foreach (var field in node.FieldSlots.OrderBy(field => field.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (field == null)
                {
                    continue;
                }

                adaptedNode.Fields.Add(new DependencyGraphAdapterField
                {
                    Name = field.Name,
                    HasValue = field.HasValue,
                    ValueSummary = field.ValueSummary,
                });
            }

            adapted.Nodes.Add(adaptedNode);
        }

        foreach (var edge in model.Edges)
        {
            if (edge?.From == null || edge.To == null)
            {
                continue;
            }

            adapted.Edges.Add(new DependencyGraphAdapterEdge
            {
                FromNodeId = edge.From.GUID,
                ToNodeId = edge.To.GUID,
                FieldName = edge.FieldName,
                DependencyKind = edge.Type.ToString(),
                IsBroken = edge.IsBroken,
                Details = edge.Details,
            });
        }

        return adapted;
    }

    private static string GetUnityObjectPath(UnityEngine.Object unityObject)
    {
        if (unityObject == null)
        {
            return string.Empty;
        }

        if (unityObject is Component component)
        {
            return component.transform.GetHierarchyPath();
        }

        if (unityObject is GameObject gameObject)
        {
            return gameObject.transform.GetHierarchyPath();
        }

        return unityObject.name;
    }

    private static string GetHierarchyPath(this Transform transform)
    {
        if (transform == null)
        {
            return string.Empty;
        }

        var path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = $"{transform.name}/{path}";
        }

        return path;
    }
}
