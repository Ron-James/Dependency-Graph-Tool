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

            var adaptedModel = DependencyGraphAdapterBuilder.Build(model);
            PopulateGraph(graph, adaptedModel);
            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            Selection.activeObject = graph;
            EditorGUIUtility.PingObject(graph);
            SceneDependencyGraphGraphWindow.Open(graph);
            return true;
        }

        private static void PopulateGraph(SceneDependencyGraphAsset graph, DependencyGraphAdapterModel adaptedModel)
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
            foreach (var adapterNode in adaptedModel.Nodes)
            {
                if (adapterNode == null)
                {
                    continue;
                }

                var nodeType = ResolveNodeType(adapterNode.NodeKind);
                var position = new Vector2((index % 5) * HorizontalSpacing, (index / 5) * VerticalSpacing);
                var runtimeNode = BaseNode.CreateFromType(nodeType, position) as DependencyGraphBaseNode;
                if (runtimeNode == null)
                {
                    continue;
                }

                runtimeNode.SetGraphData(
                    adapterNode.Id,
                    adapterNode.DisplayName,
                    adapterNode.TypeName,
                    adapterNode.UnityObjectPath,
                    BuildNodeDetails(adapterNode),
                    adapterNode.UnityObjectInstanceId);

                graph.AddNode(runtimeNode);
                if (!string.IsNullOrWhiteSpace(adapterNode.Id))
                {
                    nodesByDependencyGuid[adapterNode.Id] = runtimeNode;
                }

                index++;
            }

            foreach (var adapterEdge in adaptedModel.Edges)
            {
                if (adapterEdge == null)
                {
                    continue;
                }

                if (!nodesByDependencyGuid.TryGetValue(adapterEdge.FromNodeId, out var fromNode) ||
                    !nodesByDependencyGuid.TryGetValue(adapterEdge.ToNodeId, out var toNode))
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

        private static Type ResolveNodeType(string nodeKind)
        {
            return nodeKind switch
            {
                "MonoBehaviour" => typeof(MonoBehaviourDependencyNode),
                "ScriptableObject" => typeof(ScriptableObjectDependencyNode),
                "UnityObject" => typeof(UnityObjectDependencyNode),
                _ => typeof(ManagedObjectDependencyNode),
            };
        }

        private static string BuildNodeDetails(DependencyGraphAdapterNode node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(node.UnityObjectPath)
                ? node.TypeName
                : $"{node.TypeName} @ {node.UnityObjectPath}";
        }
    }
}
