using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RonJames.DependencyGraphTool
{
    public sealed class SceneDependencyGraphToolkitWindow : EditorWindow
    {
        private SceneScanner _scanner;
        private DependencyModel _model;
        private ToolkitGraphCanvas _canvas;

        [MenuItem("Tools/Scene Dependency Graph (UI Toolkit WIP)")]
        public static void OpenWindow()
        {
            GetWindow<SceneDependencyGraphToolkitWindow>("Scene Dependency Graph UI Toolkit");
        }

        private void OnEnable()
        {
            _scanner ??= new SceneScanner();
            BuildUi();
            RefreshGraph();
        }

        private void BuildUi()
        {
            rootVisualElement.Clear();

            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(RefreshGraph) { text = "Refresh ⟳" });
            toolbar.Add(new ToolbarButton(() => _canvas?.Organize()) { text = "Organize" });
            toolbar.Add(new ToolbarButton(() => _canvas?.AddScratchNode()) { text = "Add Scratch Node" });

            var info = new Label("WIP UI Toolkit graph surface for migration. Scanner/model is shared with the current GraphView window.");
            info.style.marginLeft = 8f;
            info.style.unityFontStyleAndWeight = FontStyle.Italic;
            toolbar.Add(info);
            rootVisualElement.Add(toolbar);

            var scrollView = new ScrollView(ScrollViewMode.Both)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Auto,
                verticalScrollerVisibility = ScrollerVisibility.Auto,
            };
            scrollView.style.flexGrow = 1f;
            rootVisualElement.Add(scrollView);

            _canvas = new ToolkitGraphCanvas();
            _canvas.style.minWidth = 3000f;
            _canvas.style.minHeight = 3000f;
            scrollView.Add(_canvas);
        }

        private void RefreshGraph()
        {
            _model = _scanner?.ScanScene() ?? new DependencyModel();
            _canvas?.SetGraph(_model);
        }

        private sealed class ToolkitGraphCanvas : VisualElement
        {
            private const float NodeWidth = 260f;
            private const float NodeHeight = 96f;
            private const float HorizontalSpacing = 320f;
            private const float VerticalSpacing = 140f;

            private readonly Dictionary<string, Rect> _nodeRects = new();
            private readonly List<DependencyNode> _scratchNodes = new();
            private readonly List<DependencyEdge> _edges = new();

            public ToolkitGraphCanvas()
            {
                style.position = Position.Relative;
                generateVisualContent += DrawEdges;
            }

            public void SetGraph(DependencyModel model)
            {
                Clear();
                _nodeRects.Clear();
                _edges.Clear();
                _scratchNodes.Clear();

                if (model == null)
                {
                    MarkDirtyRepaint();
                    return;
                }

                foreach (var edge in model.Edges)
                {
                    if (edge != null)
                    {
                        _edges.Add(edge);
                    }
                }

                var allNodes = new List<DependencyNode>(model.Nodes);
                foreach (var edge in _edges)
                {
                    if (edge.From != null && !allNodes.Contains(edge.From))
                    {
                        allNodes.Add(edge.From);
                    }

                    if (edge.To != null && !allNodes.Contains(edge.To))
                    {
                        allNodes.Add(edge.To);
                    }
                }

                LayoutNodes(allNodes);
                CreateNodeCards(allNodes);
                MarkDirtyRepaint();
            }

            public void AddScratchNode()
            {
                var node = new DependencyNode
                {
                    GUID = Guid.NewGuid().ToString("N"),
                    DisplayName = "Scratch Node",
                    Owner = null,
                };

                _scratchNodes.Add(node);
                var column = _nodeRects.Count % 8;
                var row = _nodeRects.Count / 8;
                var position = new Rect(60f + (column * HorizontalSpacing), 1000f + (row * VerticalSpacing), NodeWidth, NodeHeight);
                _nodeRects[node.GUID] = position;
                Add(CreateNodeCard(node, position));
                MarkDirtyRepaint();
            }

            public void Organize()
            {
                var nodes = new List<DependencyNode>();
                foreach (var edge in _edges)
                {
                    if (edge.From != null && !nodes.Contains(edge.From))
                    {
                        nodes.Add(edge.From);
                    }

                    if (edge.To != null && !nodes.Contains(edge.To))
                    {
                        nodes.Add(edge.To);
                    }
                }

                nodes.AddRange(_scratchNodes);
                LayoutNodes(nodes);
                CreateNodeCards(nodes);
                MarkDirtyRepaint();
            }

            private void CreateNodeCards(List<DependencyNode> nodes)
            {
                Clear();
                foreach (var node in nodes)
                {
                    if (node == null || string.IsNullOrWhiteSpace(node.GUID))
                    {
                        continue;
                    }

                    if (!_nodeRects.TryGetValue(node.GUID, out var rect))
                    {
                        continue;
                    }

                    Add(CreateNodeCard(node, rect));
                }
            }

            private VisualElement CreateNodeCard(DependencyNode node, Rect rect)
            {
                var card = new VisualElement();
                card.style.position = Position.Absolute;
                card.style.left = rect.x;
                card.style.top = rect.y;
                card.style.width = rect.width;
                card.style.height = rect.height;
                card.style.backgroundColor = new Color(0.11f, 0.11f, 0.11f, 0.95f);
                card.style.borderTopWidth = 2f;
                card.style.borderTopColor = SceneGraphView.GetDefaultNodeColor(node);
                card.style.borderBottomLeftRadius = 6f;
                card.style.borderBottomRightRadius = 6f;
                card.style.borderTopLeftRadius = 6f;
                card.style.borderTopRightRadius = 6f;
                card.style.paddingLeft = 8f;
                card.style.paddingRight = 8f;
                card.style.paddingTop = 6f;
                card.style.paddingBottom = 6f;

                var titleRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                titleRow.style.alignItems = Align.Center;

                var iconImage = new Image();
                iconImage.style.width = 16f;
                iconImage.style.height = 16f;
                iconImage.style.marginRight = 4f;

                var ownerType = node.Owner?.GetType();
                Texture icon = null;
                if (node.Owner is UnityEngine.Object unityObject)
                {
                    icon = EditorGUIUtility.ObjectContent(unityObject, ownerType)?.image
                           ?? EditorGUIUtility.ObjectContent(null, ownerType)?.image;
                }
                icon ??= EditorGUIUtility.IconContent("cs Script Icon")?.image;
                iconImage.image = icon;

                var title = new Label(string.IsNullOrWhiteSpace(node.DisplayName) ? "<Unnamed>" : node.DisplayName);
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.unityTextAlign = TextAnchor.MiddleLeft;
                title.style.flexGrow = 1f;
                title.style.whiteSpace = WhiteSpace.Normal;

                titleRow.Add(iconImage);
                titleRow.Add(title);
                card.Add(titleRow);

                var subtitle = new Label(ownerType != null ? ownerType.Name : "Managed/Scratch Node");
                subtitle.style.fontSize = 10f;
                subtitle.style.color = new Color(0.72f, 0.72f, 0.72f, 1f);
                subtitle.style.marginTop = 3f;
                card.Add(subtitle);

                return card;
            }

            private void LayoutNodes(List<DependencyNode> nodes)
            {
                var indegree = new Dictionary<string, int>();
                var outgoing = new Dictionary<string, HashSet<string>>();

                foreach (var node in nodes)
                {
                    if (node == null || string.IsNullOrWhiteSpace(node.GUID))
                    {
                        continue;
                    }

                    indegree[node.GUID] = 0;
                    outgoing[node.GUID] = new HashSet<string>();
                }

                foreach (var edge in _edges)
                {
                    if (edge?.From == null || edge.To == null)
                    {
                        continue;
                    }

                    if (!outgoing.TryGetValue(edge.From.GUID, out var outSet) || !indegree.ContainsKey(edge.To.GUID))
                    {
                        continue;
                    }

                    if (outSet.Add(edge.To.GUID))
                    {
                        indegree[edge.To.GUID] += 1;
                    }
                }

                var queue = new Queue<string>();
                foreach (var pair in indegree)
                {
                    if (pair.Value == 0)
                    {
                        queue.Enqueue(pair.Key);
                    }
                }

                var depth = new Dictionary<string, int>();
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    depth.TryGetValue(current, out var currentDepth);
                    if (!outgoing.TryGetValue(current, out var neighbors))
                    {
                        continue;
                    }

                    foreach (var neighbor in neighbors)
                    {
                        depth[neighbor] = Mathf.Max(depth.TryGetValue(neighbor, out var existingDepth) ? existingDepth : 0, currentDepth + 1);
                        indegree[neighbor] -= 1;
                        if (indegree[neighbor] == 0)
                        {
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                var columns = new Dictionary<int, int>();
                foreach (var node in nodes)
                {
                    if (node == null || string.IsNullOrWhiteSpace(node.GUID))
                    {
                        continue;
                    }

                    var col = depth.TryGetValue(node.GUID, out var resolvedDepth) ? resolvedDepth : 0;
                    columns.TryGetValue(col, out var row);
                    columns[col] = row + 1;
                    _nodeRects[node.GUID] = new Rect(80f + (col * HorizontalSpacing), 80f + (row * VerticalSpacing), NodeWidth, NodeHeight);
                }
            }

            private void DrawEdges(MeshGenerationContext context)
            {
                var painter = context.painter2D;
                painter.lineWidth = 2f;

                foreach (var edge in _edges)
                {
                    if (edge?.From == null || edge.To == null)
                    {
                        continue;
                    }

                    if (!_nodeRects.TryGetValue(edge.From.GUID, out var fromRect) || !_nodeRects.TryGetValue(edge.To.GUID, out var toRect))
                    {
                        continue;
                    }

                    var start = new Vector2(fromRect.xMax, fromRect.center.y);
                    var end = new Vector2(toRect.xMin, toRect.center.y);
                    var controlOffset = Mathf.Max(40f, Mathf.Abs(end.x - start.x) * 0.4f);
                    var c1 = new Vector2(start.x + controlOffset, start.y);
                    var c2 = new Vector2(end.x - controlOffset, end.y);

                    painter.strokeColor = edge.IsBroken ? Color.red : EdgeColor(edge.Type);
                    painter.BeginPath();
                    painter.MoveTo(start);
                    painter.BezierCurveTo(c1, c2, end);
                    painter.Stroke();
                }
            }

            private static Color EdgeColor(DependencyType type)
            {
                return type switch
                {
                    DependencyType.UnityEvent => new Color(0.2f, 0.5f, 1f),
                    DependencyType.SerializedUnityRef => new Color(1f, 0.8f, 0.2f),
                    DependencyType.SerializeReferenceManaged => new Color(0.3f, 0.9f, 0.4f),
                    DependencyType.OdinSerializedRef => new Color(0.7f, 0.4f, 1f),
                    _ => Color.white,
                };
            }
        }
    }
}
