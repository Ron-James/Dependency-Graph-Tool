#if !HAS_NODE_GRAPH_PROCESSOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RonJames.DependencyGraphTool
{
    internal static class DependencyGraphAdapterBuilder
    {
        [Serializable]
        private sealed class Snapshot
        {
            public List<SnapshotNode> Nodes = new();
            public List<SnapshotEdge> Edges = new();
        }

        [Serializable]
        private sealed class SnapshotNode
        {
            public string Id;
            public string DisplayName;
            public string TypeName;
            public string UnityObjectPath;
            public string NodeKind;
            public List<SnapshotField> Fields = new();
        }

        [Serializable]
        private sealed class SnapshotField
        {
            public string Name;
            public bool HasValue;
            public string ValueSummary;
        }

        [Serializable]
        private sealed class SnapshotEdge
        {
            public string FromNodeId;
            public string ToNodeId;
            public string FieldName;
            public string DependencyKind;
            public bool IsBroken;
            public string Details;
        }

        public static object Build(DependencyModel model)
        {
            var adapted = new Snapshot();
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

                var adaptedNode = new SnapshotNode
                {
                    Id = node.GUID,
                    DisplayName = node.DisplayName,
                    TypeName = TypeUtility.GetFriendlyTypeName(node.Owner?.GetType()),
                    UnityObjectPath = GetUnityObjectPath(node.Owner as UnityEngine.Object),
                    NodeKind = GetNodeKind(node.Owner),
                };

                foreach (var field in node.FieldSlots.OrderBy(field => field.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if (field == null)
                    {
                        continue;
                    }

                    adaptedNode.Fields.Add(new SnapshotField
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

                adapted.Edges.Add(new SnapshotEdge
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

        private static string GetNodeKind(object owner)
        {
            return owner switch
            {
                MonoBehaviour => "MonoBehaviour",
                ScriptableObject => "ScriptableObject",
                UnityEngine.Object => "UnityObject",
                _ => "ManagedObject",
            };
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
}
#endif
