#if HAS_NODE_GRAPH_PROCESSOR
using System;
using System.Collections.Generic;
using GraphProcessor;
using UnityEditor;
using UnityEngine;

namespace RonJames.DependencyGraphTool.NodeGraphProcessorIntegration
{
    internal sealed class SceneDependencyGraphAsset : BaseGraph
    {
    }

    internal sealed class SceneDependencyGraphGraphWindow : BaseGraphWindow
    {
        public static void Open(SceneDependencyGraphAsset graph)
        {
            var window = GetWindow<SceneDependencyGraphGraphWindow>();
            window.titleContent = new GUIContent("Scene Dependency Graph");
            window.InitializeGraph(graph);
            window.Show();
        }

        protected override void InitializeWindow(BaseGraph graph)
        {
            titleContent = new GUIContent("Scene Dependency Graph");

            if (graphView == null)
            {
                graphView = new BaseGraphView(this);
            }

            rootView.Add(graphView);
        }
    }

    internal static class NodeGraphProcessorBridge
    {
        private const string GraphAssetPath = "Assets/SceneDependencyGraph.asset";
        private const float HorizontalSpacing = 340f;
        private const float VerticalSpacing = 160f;

        public static bool TryOpenOrCreateGraph(DependencyModel model, out string error)
        {
            error = null;
            if (model == null)
            {
                error = "No scene dependency model is available.";
                return false;
            }

            var graph = AssetDatabase.LoadAssetAtPath<SceneDependencyGraphAsset>(GraphAssetPath);
            if (graph == null)
            {
                graph = ScriptableObject.CreateInstance<SceneDependencyGraphAsset>();
                AssetDatabase.CreateAsset(graph, GraphAssetPath);
            }

            PopulateGraph(graph, model);
            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            Selection.activeObject = graph;
            EditorGUIUtility.PingObject(graph);
            SceneDependencyGraphGraphWindow.Open(graph);
            return true;
        }

        private static void PopulateGraph(SceneDependencyGraphAsset graph, DependencyModel model)
        {
            graph.Deserialize();
            graph.edges.Clear();
            graph.nodes.Clear();
            graph.edgesPerGUID.Clear();
            graph.nodesPerGUID.Clear();
            graph.exposedParameters ??= new List<ExposedParameter>();
            graph.exposedParameters.RemoveAll(param => param == null);

            var nodesByDependencyGuid = new Dictionary<string, DependencyGraphBaseNode>(StringComparer.Ordinal);
            var index = 0;
            foreach (var modelNode in model.Nodes)
            {
                if (modelNode == null)
                {
                    continue;
                }

                var nodeType = ResolveNodeType(modelNode.Owner);
                var position = new Vector2((index % 5) * HorizontalSpacing, (index / 5) * VerticalSpacing);
                var runtimeNode = BaseNode.CreateFromType(nodeType, position) as DependencyGraphBaseNode;
                if (runtimeNode == null)
                {
                    continue;
                }

                runtimeNode.SetGraphData(
                    modelNode.GUID,
                    modelNode.DisplayName,
                    TypeUtility.GetFriendlyTypeName(modelNode.Owner?.GetType()),
                    GetUnityObjectPath(modelNode.Owner as UnityEngine.Object),
                    BuildNodeDetails(modelNode));

                graph.AddNode(runtimeNode);
                if (!string.IsNullOrWhiteSpace(modelNode.GUID))
                {
                    nodesByDependencyGuid[modelNode.GUID] = runtimeNode;
                }

                index++;
            }

            foreach (var modelEdge in model.Edges)
            {
                if (modelEdge?.From == null || modelEdge.To == null)
                {
                    continue;
                }

                if (!nodesByDependencyGuid.TryGetValue(modelEdge.From.GUID, out var fromNode) ||
                    !nodesByDependencyGuid.TryGetValue(modelEdge.To.GUID, out var toNode))
                {
                    continue;
                }

                var outputPort = fromNode.GetPort("_output", null);
                var inputPort = toNode.GetPort("_input", null);
                if (outputPort == null || inputPort == null)
                {
                    continue;
                }

                graph.Connect(inputPort, outputPort);
            }

            graph.UpdateComputeOrder();
        }

        private static Type ResolveNodeType(object owner)
        {
            return owner switch
            {
                MonoBehaviour => typeof(MonoBehaviourDependencyNode),
                ScriptableObject => typeof(ScriptableObjectDependencyNode),
                _ => typeof(ManagedObjectDependencyNode),
            };
        }

        private static string BuildNodeDetails(DependencyNode node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            var ownerType = TypeUtility.GetFriendlyTypeName(node.Owner?.GetType());
            var path = GetUnityObjectPath(node.Owner as UnityEngine.Object);
            return string.IsNullOrWhiteSpace(path)
                ? ownerType
                : $"{ownerType} @ {path}";
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
