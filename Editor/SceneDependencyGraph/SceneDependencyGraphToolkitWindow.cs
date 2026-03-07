using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace RonJames.DependencyGraphTool
{
    public sealed class SceneDependencyGraphToolkitWindow : EditorWindow
    {
        [Serializable]
        private sealed class HiddenNodeState
        {
            public List<string> HiddenNodeGuids = new();
            public List<string> KnownNodeGuids = new();
        }

        [Serializable]
        private sealed class NodeTypeColorEntry
        {
            public string TypeKey;
            public string HtmlColor;
        }

        [Serializable]
        private sealed class NodeTypeColorState
        {
            public List<NodeTypeColorEntry> Entries = new();
        }

        [Serializable]
        private sealed class NodeColorEntry
        {
            public string NodeGuid;
            public string HtmlColor;
        }

        [Serializable]
        private sealed class NodeColorState
        {
            public List<NodeColorEntry> Entries = new();
        }

        private const string HiddenNodePrefsPrefix = "SceneDependencyGraphToolkit.HiddenNodes";
        private const string NodeTypeColorPrefsPrefix = "SceneDependencyGraphToolkit.NodeTypeColors";
        private const string NodeColorPrefsPrefix = "SceneDependencyGraphToolkit.NodeColors";

        private SceneScanner _scanner;
        private DependencyModel _model;
        private ToolkitGraphCanvas _canvas;
        private ScrollView _hierarchyScrollView;
        private TextField _searchField;
        private Label _detailsLabel;
        private ColorField _nodeColorField;

        private readonly HashSet<string> _hiddenNodeGuids = new();
        private readonly HashSet<string> _knownNodeGuids = new();
        private readonly Dictionary<string, Color> _typeColorOverrides = new();
        private readonly Dictionary<string, Color> _nodeColorOverrides = new();

        private DependencyNode _selectedNode;
        private DependencyType? _currentFilter;

        [MenuItem("Tools/Scene Dependency Graph (UI Toolkit WIP)")]
        public static void OpenWindow()
        {
            GetWindow<SceneDependencyGraphToolkitWindow>("Scene Dependency Graph UI Toolkit");
        }

        private void OnEnable()
        {
            _scanner ??= new SceneScanner();
            LoadNodeTypeColorPreferences();
            LoadNodeColorPreferences();
            BuildUi();
            RefreshGraph();
        }

        private void BuildUi()
        {
            rootVisualElement.Clear();

            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(RefreshGraph) { text = "Refresh ⟳" });
            toolbar.Add(new ToolbarButton(() => _canvas?.Organize()) { text = "Organize" });

            var filterMenu = new ToolbarMenu { text = "Filter: All" };
            filterMenu.menu.AppendAction("All", _ => SetFilter(null));
            foreach (DependencyType type in Enum.GetValues(typeof(DependencyType)))
            {
                var captured = type;
                filterMenu.menu.AppendAction(type.ToString(), _ => SetFilter(captured));
            }

            toolbar.Add(filterMenu);
            rootVisualElement.Add(toolbar);

            var splitHorizontal = new TwoPaneSplitView(0, 300, TwoPaneSplitViewOrientation.Horizontal);
            var splitCenterRight = new TwoPaneSplitView(1, 950, TwoPaneSplitViewOrientation.Horizontal);

            splitHorizontal.Add(CreateLeftPane());
            splitHorizontal.Add(splitCenterRight);

            var graphScrollView = new ScrollView(ScrollViewMode.Both)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Auto,
                verticalScrollerVisibility = ScrollerVisibility.Auto,
            };

            _canvas = new ToolkitGraphCanvas();
            _canvas.style.minWidth = 3000f;
            _canvas.style.minHeight = 3000f;
            _canvas.OnNodeSelected += ShowNodeDetails;
            graphScrollView.Add(_canvas);

            splitCenterRight.Add(graphScrollView);
            splitCenterRight.Add(CreateRightPane());
            rootVisualElement.Add(splitHorizontal);
        }

        private VisualElement CreateLeftPane()
        {
            var pane = new VisualElement();
            pane.style.flexGrow = 1;
            pane.style.paddingLeft = 6f;
            pane.style.paddingTop = 6f;

            pane.Add(new Label("Dependencies") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            _searchField = new TextField("Search") { value = string.Empty };
            _searchField.RegisterValueChangedCallback(_ => RebuildHierarchyList());
            pane.Add(_searchField);

            var row1 = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            row1.Add(new Button(() => SetVisibilityForFilteredNodes(true)) { text = "Show Filtered" });
            row1.Add(new Button(() => SetVisibilityForFilteredNodes(false)) { text = "Hide Filtered" });
            pane.Add(row1);

            var row2 = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            row2.Add(new Button(() => SetAllNodeVisibility(true)) { text = "Show All" });
            row2.Add(new Button(() => SetAllNodeVisibility(false)) { text = "Hide All" });
            pane.Add(row2);

            _hierarchyScrollView = new ScrollView { style = { flexGrow = 1f } };
            pane.Add(_hierarchyScrollView);
            return pane;
        }

        private VisualElement CreateRightPane()
        {
            var pane = new VisualElement();
            pane.style.flexGrow = 1;
            pane.style.paddingLeft = 8f;
            pane.style.paddingTop = 8f;

            pane.Add(new Label("Details") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            _detailsLabel = new Label("Select a node to inspect details.") { style = { whiteSpace = WhiteSpace.Normal } };
            pane.Add(_detailsLabel);

            _nodeColorField = new ColorField("Node Color")
            {
                showAlpha = false,
                value = Color.gray,
                tooltip = "Apply a color to the selected node.",
            };
            _nodeColorField.RegisterValueChangedCallback(evt => ApplyColorToSelectedNode(evt.newValue));
            pane.Add(_nodeColorField);

            var applyTypeColorButton = new Button(ApplyColorToSelectedNodeType) { text = "Apply Color To Node Type" };
            pane.Add(applyTypeColorButton);

            var pingButton = new Button(PingSelectedObject) { text = "Ping" };
            pane.Add(pingButton);

            return pane;
        }

        private void RefreshGraph()
        {
            _model = _scanner?.ScanScene() ?? new DependencyModel();
            LoadHiddenNodePreferences();
            RedrawGraphOnly();
            RebuildHierarchyList();
        }

        private void RedrawGraphOnly()
        {
            _canvas?.SetGraph(_model, _hiddenNodeGuids, _currentFilter, _typeColorOverrides, _nodeColorOverrides);
        }

        private void SetFilter(DependencyType? dependencyType)
        {
            _currentFilter = dependencyType;
            RedrawGraphOnly();
            RebuildHierarchyList();
        }

        private void RebuildHierarchyList()
        {
            if (_hierarchyScrollView == null)
            {
                return;
            }

            _hierarchyScrollView.Clear();
            if (_model == null)
            {
                return;
            }

            foreach (var node in GetFilteredNodes().OrderBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                _hierarchyScrollView.Add(CreateNodeRow(node));
            }
        }

        private IEnumerable<DependencyNode> GetFilteredNodes()
        {
            if (_model?.Nodes == null)
            {
                yield break;
            }

            var query = _searchField?.value?.Trim();
            var hasQuery = !string.IsNullOrWhiteSpace(query);
            foreach (var node in _model.Nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.GUID))
                {
                    continue;
                }

                if (_currentFilter.HasValue)
                {
                    var hasType = _model.Edges.Any(edge => edge?.Type == _currentFilter.Value && (edge.From == node || edge.To == node));
                    if (!hasType)
                    {
                        continue;
                    }
                }

                if (!hasQuery)
                {
                    yield return node;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(node.DisplayName) && node.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    yield return node;
                }
            }
        }

        private VisualElement CreateNodeRow(DependencyNode node)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            var nameButton = new Button(() => FocusNode(node))
            {
                text = _hiddenNodeGuids.Contains(node.GUID) ? $"{node.DisplayName} (hidden)" : node.DisplayName,
            };
            nameButton.style.flexGrow = 1;
            nameButton.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.Add(nameButton);

            var toggleButton = new Button(() => ToggleNodeVisibility(node))
            {
                text = _hiddenNodeGuids.Contains(node.GUID) ? "Show" : "Hide",
            };
            toggleButton.style.width = 56f;
            row.Add(toggleButton);

            return row;
        }

        private void FocusNode(DependencyNode node)
        {
            if (node == null)
            {
                return;
            }

            if (_hiddenNodeGuids.Contains(node.GUID))
            {
                ShowNodeAndConnectedNodes(node.GUID);
                SaveHiddenNodePreferences();
                RebuildHierarchyList();
                RedrawGraphOnly();
            }

            _canvas?.FocusNode(node.GUID);
        }

        private void SetAllNodeVisibility(bool showNodes)
        {
            if (_model == null)
            {
                return;
            }

            foreach (var node in _model.Nodes)
            {
                if (string.IsNullOrWhiteSpace(node.GUID))
                {
                    continue;
                }

                _knownNodeGuids.Add(node.GUID);
                if (showNodes)
                {
                    _hiddenNodeGuids.Remove(node.GUID);
                }
                else
                {
                    _hiddenNodeGuids.Add(node.GUID);
                }
            }

            SaveHiddenNodePreferences();
            RebuildHierarchyList();
            RedrawGraphOnly();
        }

        private void SetVisibilityForFilteredNodes(bool showNodes)
        {
            foreach (var node in GetFilteredNodes())
            {
                _knownNodeGuids.Add(node.GUID);
                if (showNodes)
                {
                    ShowNodeAndConnectedNodes(node.GUID);
                }
                else
                {
                    _hiddenNodeGuids.Add(node.GUID);
                }
            }

            SaveHiddenNodePreferences();
            RebuildHierarchyList();
            RedrawGraphOnly();
        }

        private void ToggleNodeVisibility(DependencyNode node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.GUID))
            {
                return;
            }

            _knownNodeGuids.Add(node.GUID);
            if (_hiddenNodeGuids.Contains(node.GUID))
            {
                ShowNodeAndConnectedNodes(node.GUID);
            }
            else
            {
                _hiddenNodeGuids.Add(node.GUID);
            }

            SaveHiddenNodePreferences();
            RebuildHierarchyList();
            RedrawGraphOnly();
        }

        private void ShowNodeAndConnectedNodes(string startingNodeGuid)
        {
            foreach (var nodeGuid in CollectConnectedNodeGuids(startingNodeGuid))
            {
                _knownNodeGuids.Add(nodeGuid);
                _hiddenNodeGuids.Remove(nodeGuid);
            }
        }

        private HashSet<string> CollectConnectedNodeGuids(string startingNodeGuid)
        {
            var connectedNodeGuids = new HashSet<string>();
            if (_model?.Edges == null || string.IsNullOrWhiteSpace(startingNodeGuid))
            {
                return connectedNodeGuids;
            }

            var adjacency = new Dictionary<string, HashSet<string>>();
            foreach (var edge in _model.Edges)
            {
                if (edge?.From == null || edge.To == null || string.IsNullOrWhiteSpace(edge.From.GUID) || string.IsNullOrWhiteSpace(edge.To.GUID))
                {
                    continue;
                }

                if (!adjacency.TryGetValue(edge.From.GUID, out var fromSet))
                {
                    fromSet = new HashSet<string>();
                    adjacency[edge.From.GUID] = fromSet;
                }

                if (!adjacency.TryGetValue(edge.To.GUID, out var toSet))
                {
                    toSet = new HashSet<string>();
                    adjacency[edge.To.GUID] = toSet;
                }

                fromSet.Add(edge.To.GUID);
                toSet.Add(edge.From.GUID);
            }

            var queue = new Queue<string>();
            queue.Enqueue(startingNodeGuid);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!connectedNodeGuids.Add(current) || !adjacency.TryGetValue(current, out var neighbors))
                {
                    continue;
                }

                foreach (var neighbor in neighbors)
                {
                    if (!connectedNodeGuids.Contains(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return connectedNodeGuids;
        }

        private void ShowNodeDetails(DependencyNode node)
        {
            _selectedNode = node;
            if (node == null)
            {
                _detailsLabel.text = "Select a node to inspect details.";
                return;
            }

            var typeName = TypeUtility.GetFriendlyTypeName(node.Owner?.GetType());
            _detailsLabel.text = $"Name: {node.DisplayName}\nType: {typeName}\nGUID: {node.GUID}";
            _nodeColorField.SetValueWithoutNotify(GetNodeDisplayColor(node));
        }

        private void ApplyColorToSelectedNode(Color color)
        {
            if (_selectedNode == null || string.IsNullOrWhiteSpace(_selectedNode.GUID))
            {
                return;
            }

            _nodeColorOverrides[_selectedNode.GUID] = color;
            SaveNodeColorPreferences();
            RedrawGraphOnly();
        }

        private void ApplyColorToSelectedNodeType()
        {
            if (_selectedNode == null)
            {
                return;
            }

            var typeKey = SceneGraphView.GetNodeTypeKey(_selectedNode);
            if (string.IsNullOrWhiteSpace(typeKey))
            {
                return;
            }

            var selectedColor = _nodeColorField.value;
            _typeColorOverrides[typeKey] = selectedColor;
            SaveNodeTypeColorPreferences();
            RedrawGraphOnly();
        }

        private void PingSelectedObject()
        {
            if (_selectedNode?.Owner is not UnityEngine.Object unityObject)
            {
                return;
            }

            Selection.activeObject = unityObject;
            EditorGUIUtility.PingObject(unityObject);
        }

        private Color GetNodeDisplayColor(DependencyNode node)
        {
            if (node != null && !string.IsNullOrWhiteSpace(node.GUID) && _nodeColorOverrides.TryGetValue(node.GUID, out var nodeColor))
            {
                return nodeColor;
            }

            var typeKey = SceneGraphView.GetNodeTypeKey(node);
            if (!string.IsNullOrWhiteSpace(typeKey) && _typeColorOverrides.TryGetValue(typeKey, out var storedColor))
            {
                return storedColor;
            }

            return SceneGraphView.GetDefaultNodeColor(node);
        }

        private void LoadHiddenNodePreferences()
        {
            _hiddenNodeGuids.Clear();
            _knownNodeGuids.Clear();

            var rawJson = EditorPrefs.GetString(GetHiddenNodePrefsKey(), string.Empty);
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                HideAllCurrentNodesAsDefault();
                SaveHiddenNodePreferences();
                return;
            }

            var state = JsonUtility.FromJson<HiddenNodeState>(rawJson);
            if (state?.HiddenNodeGuids == null)
            {
                HideAllCurrentNodesAsDefault();
                SaveHiddenNodePreferences();
                return;
            }

            foreach (var guid in state.HiddenNodeGuids.Where(guid => !string.IsNullOrWhiteSpace(guid)))
            {
                _hiddenNodeGuids.Add(guid);
            }

            if (state.KnownNodeGuids != null)
            {
                foreach (var guid in state.KnownNodeGuids.Where(guid => !string.IsNullOrWhiteSpace(guid)))
                {
                    _knownNodeGuids.Add(guid);
                }
            }

            foreach (var node in _model.Nodes)
            {
                if (string.IsNullOrWhiteSpace(node.GUID))
                {
                    continue;
                }

                if (_knownNodeGuids.Add(node.GUID))
                {
                    _hiddenNodeGuids.Add(node.GUID);
                }
            }
        }

        private void HideAllCurrentNodesAsDefault()
        {
            foreach (var node in _model.Nodes)
            {
                if (string.IsNullOrWhiteSpace(node.GUID))
                {
                    continue;
                }

                _hiddenNodeGuids.Add(node.GUID);
                _knownNodeGuids.Add(node.GUID);
            }
        }

        private void SaveHiddenNodePreferences()
        {
            var state = new HiddenNodeState
            {
                HiddenNodeGuids = _hiddenNodeGuids.ToList(),
                KnownNodeGuids = _knownNodeGuids.ToList(),
            };

            EditorPrefs.SetString(GetHiddenNodePrefsKey(), JsonUtility.ToJson(state));
        }

        private static string GetHiddenNodePrefsKey()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var sceneKey = string.IsNullOrWhiteSpace(scene.path) ? scene.name : scene.path;
            if (string.IsNullOrWhiteSpace(sceneKey))
            {
                sceneKey = "UnsavedScene";
            }

            return $"{HiddenNodePrefsPrefix}.{sceneKey}";
        }

        private void LoadNodeTypeColorPreferences()
        {
            _typeColorOverrides.Clear();
            var rawJson = EditorPrefs.GetString(NodeTypeColorPrefsPrefix, string.Empty);
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return;
            }

            var state = JsonUtility.FromJson<NodeTypeColorState>(rawJson);
            if (state?.Entries == null)
            {
                return;
            }

            foreach (var entry in state.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.TypeKey) || string.IsNullOrWhiteSpace(entry.HtmlColor))
                {
                    continue;
                }

                if (ColorUtility.TryParseHtmlString(entry.HtmlColor, out var color))
                {
                    _typeColorOverrides[entry.TypeKey] = color;
                }
            }
        }

        private void SaveNodeTypeColorPreferences()
        {
            var state = new NodeTypeColorState();
            foreach (var pair in _typeColorOverrides)
            {
                state.Entries.Add(new NodeTypeColorEntry
                {
                    TypeKey = pair.Key,
                    HtmlColor = $"#{ColorUtility.ToHtmlStringRGB(pair.Value)}",
                });
            }

            EditorPrefs.SetString(NodeTypeColorPrefsPrefix, JsonUtility.ToJson(state));
        }

        private void LoadNodeColorPreferences()
        {
            _nodeColorOverrides.Clear();
            var rawJson = EditorPrefs.GetString(NodeColorPrefsPrefix, string.Empty);
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return;
            }

            var state = JsonUtility.FromJson<NodeColorState>(rawJson);
            if (state?.Entries == null)
            {
                return;
            }

            foreach (var entry in state.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.NodeGuid) || string.IsNullOrWhiteSpace(entry.HtmlColor))
                {
                    continue;
                }

                if (ColorUtility.TryParseHtmlString(entry.HtmlColor, out var color))
                {
                    _nodeColorOverrides[entry.NodeGuid] = color;
                }
            }
        }

        private void SaveNodeColorPreferences()
        {
            var state = new NodeColorState();
            foreach (var pair in _nodeColorOverrides)
            {
                state.Entries.Add(new NodeColorEntry
                {
                    NodeGuid = pair.Key,
                    HtmlColor = $"#{ColorUtility.ToHtmlStringRGB(pair.Value)}",
                });
            }

            EditorPrefs.SetString(NodeColorPrefsPrefix, JsonUtility.ToJson(state));
        }

        private sealed class ToolkitGraphCanvas : VisualElement
        {
            private const float NodeWidth = 260f;
            private const float MinimumNodeHeight = 112f;
            private const float HorizontalSpacing = 320f;
            private const float VerticalSpacing = 32f;

            private readonly Dictionary<string, Rect> _nodeRects = new();
            private readonly Dictionary<string, VisualElement> _nodeCards = new();
            private readonly Dictionary<string, VisualElement> _portVisualByKey = new();
            private readonly Dictionary<string, Vector2> _portAnchorByKey = new();
            private readonly Dictionary<string, List<PortDescriptor>> _outputPortsByNode = new();
            private readonly Dictionary<string, List<PortDescriptor>> _inputPortsByNode = new();
            private readonly List<DependencyEdge> _edges = new();
            private readonly Dictionary<string, DependencyNode> _nodesByGuid = new();

            private string _selectedNodeGuid;

            public Action<DependencyNode> OnNodeSelected;

            public ToolkitGraphCanvas()
            {
                style.position = Position.Relative;
                generateVisualContent += DrawEdges;
            }

            public void SetGraph(
                DependencyModel model,
                IReadOnlyCollection<string> hiddenNodeGuids,
                DependencyType? filter,
                IReadOnlyDictionary<string, Color> typeColorOverrides,
                IReadOnlyDictionary<string, Color> nodeColorOverrides)
            {
                Clear();
                _nodeRects.Clear();
                _nodeCards.Clear();
                _portVisualByKey.Clear();
                _portAnchorByKey.Clear();
                _outputPortsByNode.Clear();
                _inputPortsByNode.Clear();
                _edges.Clear();
                _nodesByGuid.Clear();
                _selectedNodeGuid = null;

                if (model == null)
                {
                    MarkDirtyRepaint();
                    return;
                }

                var visibleNodes = new List<DependencyNode>();
                foreach (var node in model.Nodes)
                {
                    if (node == null || string.IsNullOrWhiteSpace(node.GUID))
                    {
                        continue;
                    }

                    if (hiddenNodeGuids != null && hiddenNodeGuids.Contains(node.GUID))
                    {
                        continue;
                    }

                    _nodesByGuid[node.GUID] = node;
                    visibleNodes.Add(node);
                }

                foreach (var edge in model.Edges)
                {
                    if (edge?.From == null || edge.To == null)
                    {
                        continue;
                    }

                    if (filter.HasValue && edge.Type != filter.Value)
                    {
                        continue;
                    }

                    if (!_nodesByGuid.ContainsKey(edge.From.GUID) || !_nodesByGuid.ContainsKey(edge.To.GUID))
                    {
                        continue;
                    }

                    _edges.Add(edge);
                }

                BuildPortDescriptors(visibleNodes);

                LayoutNodes(visibleNodes);

                foreach (var node in visibleNodes)
                {
                    if (!_nodeRects.TryGetValue(node.GUID, out var rect))
                    {
                        continue;
                    }

                    var nodeColor = SceneGraphView.GetDefaultNodeColor(node);
                    var typeKey = SceneGraphView.GetNodeTypeKey(node);
                    if (!string.IsNullOrWhiteSpace(typeKey) && typeColorOverrides != null && typeColorOverrides.TryGetValue(typeKey, out var typeColor))
                    {
                        nodeColor = typeColor;
                    }

                    if (nodeColorOverrides != null && nodeColorOverrides.TryGetValue(node.GUID, out var nodeOverrideColor))
                    {
                        nodeColor = nodeOverrideColor;
                    }

                    var card = CreateNodeCard(node, rect, nodeColor);
                    _nodeCards[node.GUID] = card;
                    Add(card);
                }

                MarkDirtyRepaint();
            }

            public void Organize()
            {
                var nodes = _nodesByGuid.Values.ToList();
                LayoutNodes(nodes);
                foreach (var node in nodes)
                {
                    if (_nodeCards.TryGetValue(node.GUID, out var card) && _nodeRects.TryGetValue(node.GUID, out var rect))
                    {
                        card.style.left = rect.x;
                        card.style.top = rect.y;
                    }
                }

                MarkDirtyRepaint();
            }

            public void FocusNode(string guid)
            {
                if (string.IsNullOrWhiteSpace(guid) || !_nodeCards.TryGetValue(guid, out var card))
                {
                    return;
                }

                SelectNode(guid);
                card.BringToFront();
            }

            private VisualElement CreateNodeCard(DependencyNode node, Rect rect, Color nodeColor)
            {
                var card = new VisualElement();
                card.style.position = Position.Absolute;
                card.style.left = rect.x;
                card.style.top = rect.y;
                card.style.width = rect.width;
                card.style.height = rect.height;
                card.style.backgroundColor = new Color(0.11f, 0.11f, 0.11f, 0.96f);
                card.style.borderTopWidth = 2f;
                card.style.borderTopColor = nodeColor;
                card.style.borderBottomLeftRadius = 6f;
                card.style.borderBottomRightRadius = 6f;
                card.style.borderTopLeftRadius = 6f;
                card.style.borderTopRightRadius = 6f;
                card.style.paddingLeft = 8f;
                card.style.paddingRight = 8f;
                card.style.paddingTop = 6f;
                card.style.paddingBottom = 6f;

                card.RegisterCallback<ClickEvent>(_ => SelectNode(node.GUID));

                var titleRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                titleRow.style.alignItems = Align.Center;

                var icon = new Image();
                icon.style.width = 16f;
                icon.style.height = 16f;
                icon.style.marginRight = 4f;
                icon.image = ResolveNodeIcon(node);
                titleRow.Add(icon);

                var title = new Label(string.IsNullOrWhiteSpace(node.DisplayName) ? "<Unnamed>" : node.DisplayName);
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.flexGrow = 1f;
                title.style.whiteSpace = WhiteSpace.Normal;
                titleRow.Add(title);

                card.Add(titleRow);

                var subtitle = new Label(node.Owner != null ? TypeUtility.GetFriendlyTypeName(node.Owner.GetType()) : "Managed/Scratch Node");
                subtitle.style.fontSize = 10f;
                subtitle.style.color = new Color(0.75f, 0.75f, 0.75f, 1f);
                subtitle.style.marginTop = 4f;
                card.Add(subtitle);

                var inputPorts = _inputPortsByNode.TryGetValue(node.GUID, out var inPorts) ? inPorts : null;
                var outputPorts = _outputPortsByNode.TryGetValue(node.GUID, out var outPorts) ? outPorts : null;
                if ((inputPorts != null && inputPorts.Count > 0) || (outputPorts != null && outputPorts.Count > 0))
                {
                    var portRows = new VisualElement();
                    portRows.style.marginTop = 6f;
                    portRows.style.flexGrow = 1f;
                    card.Add(portRows);

                    var maxRows = Math.Max(inputPorts?.Count ?? 0, outputPorts?.Count ?? 0);
                    for (var row = 0; row < maxRows; row++)
                    {
                        var rowElement = new VisualElement { style = { flexDirection = FlexDirection.Row, minHeight = 18f } };
                        rowElement.style.alignItems = Align.Center;

                        if (inputPorts != null && row < inputPorts.Count)
                        {
                            var inPort = CreatePortVisual(node.GUID, inputPorts[row], isOutput: false);
                            rowElement.Add(inPort);
                        }
                        else
                        {
                            rowElement.Add(new VisualElement { style = { flexGrow = 1f } });
                        }

                        if (outputPorts != null && row < outputPorts.Count)
                        {
                            var outPort = CreatePortVisual(node.GUID, outputPorts[row], isOutput: true);
                            rowElement.Add(outPort);
                        }
                        else
                        {
                            rowElement.Add(new VisualElement { style = { flexGrow = 1f } });
                        }

                        portRows.Add(rowElement);
                    }
                }

                return card;
            }

            private VisualElement CreatePortVisual(string nodeGuid, PortDescriptor descriptor, bool isOutput)
            {
                var portKey = GetPortKey(nodeGuid, descriptor.FieldName, isOutput);
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1f } };
                row.style.alignItems = Align.Center;
                row.style.justifyContent = isOutput ? Justify.FlexEnd : Justify.FlexStart;

                var bubble = new VisualElement();
                bubble.style.width = 8f;
                bubble.style.height = 8f;
                bubble.style.borderTopLeftRadius = 4f;
                bubble.style.borderTopRightRadius = 4f;
                bubble.style.borderBottomLeftRadius = 4f;
                bubble.style.borderBottomRightRadius = 4f;
                bubble.style.backgroundColor = isOutput
                    ? (descriptor.HasValue ? new Color(0.3f, 0.8f, 0.4f) : new Color(0.75f, 0.45f, 0.2f))
                    : new Color(0.35f, 0.65f, 1f);

                var label = new Label(GetPortLabel(descriptor, isOutput));
                label.style.fontSize = 9f;
                label.style.color = new Color(0.85f, 0.85f, 0.85f, 1f);
                label.style.unityTextAlign = isOutput ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
                label.style.maxWidth = 108f;

                if (!isOutput)
                {
                    row.Add(bubble);
                    label.style.marginLeft = 3f;
                }

                var iconOrBadge = CreatePortTypeIcon(descriptor);
                if (iconOrBadge != null)
                {
                    row.Add(iconOrBadge);
                }

                row.Add(label);

                if (isOutput)
                {
                    row.Add(bubble);
                    bubble.style.marginLeft = 3f;
                }

                row.RegisterCallback<GeometryChangedEvent>(_ => UpdatePortAnchor(portKey, row, isOutput));
                _portVisualByKey[portKey] = row;
                return row;
            }

            private static string GetPortLabel(PortDescriptor descriptor, bool isOutput)
            {
                var prefix = isOutput ? "OUT" : "IN";
                var safeFieldName = string.IsNullOrWhiteSpace(descriptor.FieldName) ? "unknown" : descriptor.FieldName;
                var suffix = string.IsNullOrWhiteSpace(descriptor.ValueSummary) ? string.Empty : $" ({descriptor.ValueSummary})";
                var emptyMarker = isOutput && !descriptor.HasValue ? " [empty]" : string.Empty;
                return $"{prefix}: {safeFieldName}{emptyMarker}{suffix}";
            }

            private static VisualElement CreatePortTypeIcon(PortDescriptor descriptor)
            {
                if (descriptor.Type == DependencyType.SerializeReferenceManaged)
                {
                    var badge = new Label("C#");
                    badge.style.fontSize = 8f;
                    badge.style.paddingLeft = 3f;
                    badge.style.paddingRight = 3f;
                    badge.style.marginLeft = 3f;
                    badge.style.marginRight = 3f;
                    badge.style.color = Color.white;
                    badge.style.backgroundColor = new Color(0.14f, 0.14f, 0.14f, 0.9f);
                    badge.style.borderTopLeftRadius = 2f;
                    badge.style.borderTopRightRadius = 2f;
                    badge.style.borderBottomLeftRadius = 2f;
                    badge.style.borderBottomRightRadius = 2f;
                    return badge;
                }

                if (descriptor.Type == DependencyType.SerializedUnityRef || descriptor.Type == DependencyType.OdinSerializedRef)
                {
                    var icon = new Image
                    {
                        image = EditorGUIUtility.IconContent("cs Script Icon")?.image,
                        scaleMode = ScaleMode.ScaleToFit,
                    };
                    icon.style.width = 11f;
                    icon.style.height = 11f;
                    icon.style.marginLeft = 3f;
                    icon.style.marginRight = 3f;
                    return icon;
                }

                return null;
            }

            private void UpdatePortAnchor(string portKey, VisualElement portRow, bool isOutput)
            {
                var localRect = contentRect;
                var worldCenter = portRow.worldBound.center;
                var localCenter = WorldToLocal(worldCenter);
                var x = isOutput ? localCenter.x + (portRow.worldBound.width * 0.5f) : localCenter.x - (portRow.worldBound.width * 0.5f);
                x = Mathf.Clamp(x, localRect.xMin, localRect.xMax);
                _portAnchorByKey[portKey] = new Vector2(x, localCenter.y);
                MarkDirtyRepaint();
            }

            private static string GetPortKey(string nodeGuid, string fieldName, bool isOutput)
            {
                var safeFieldName = string.IsNullOrWhiteSpace(fieldName) ? "unknown" : fieldName;
                return $"{nodeGuid}|{safeFieldName}|{(isOutput ? "OUT" : "IN")}";
            }

            private void SelectNode(string guid)
            {
                _selectedNodeGuid = guid;
                foreach (var pair in _nodeCards)
                {
                    pair.Value.style.borderBottomWidth = pair.Key == guid ? 2f : 0f;
                    pair.Value.style.borderBottomColor = pair.Key == guid ? new Color(0.3f, 0.7f, 1f, 1f) : Color.clear;
                }

                OnNodeSelected?.Invoke(_nodesByGuid.TryGetValue(guid, out var node) ? node : null);
            }

            private static Texture ResolveNodeIcon(DependencyNode node)
            {
                var ownerType = node.Owner?.GetType();
                if (node.Owner is UnityEngine.Object unityObject)
                {
                    return EditorGUIUtility.ObjectContent(unityObject, ownerType)?.image
                           ?? EditorGUIUtility.ObjectContent(null, ownerType)?.image
                           ?? EditorGUIUtility.IconContent("Prefab Icon")?.image;
                }

                return EditorGUIUtility.IconContent("cs Script Icon")?.image;
            }

            private void LayoutNodes(List<DependencyNode> nodes)
            {
                var indegree = new Dictionary<string, int>();
                var outgoing = new Dictionary<string, HashSet<string>>();

                foreach (var node in nodes)
                {
                    indegree[node.GUID] = 0;
                    outgoing[node.GUID] = new HashSet<string>();
                }

                foreach (var edge in _edges)
                {
                    if (!outgoing.TryGetValue(edge.From.GUID, out var outgoingSet) || !indegree.ContainsKey(edge.To.GUID))
                    {
                        continue;
                    }

                    if (outgoingSet.Add(edge.To.GUID))
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

                var columnsHeight = new Dictionary<int, float>();
                foreach (var node in nodes)
                {
                    var column = depth.TryGetValue(node.GUID, out var col) ? col : 0;
                    var nodeHeight = GetNodeHeight(node.GUID);
                    if (!columnsHeight.TryGetValue(column, out var y))
                    {
                        y = 80f;
                    }

                    _nodeRects[node.GUID] = new Rect(80f + (column * HorizontalSpacing), y, NodeWidth, nodeHeight);
                    columnsHeight[column] = y + nodeHeight + VerticalSpacing;
                }
            }

            private float GetNodeHeight(string nodeGuid)
            {
                var inputCount = _inputPortsByNode.TryGetValue(nodeGuid, out var inputPorts) ? inputPorts.Count : 0;
                var outputCount = _outputPortsByNode.TryGetValue(nodeGuid, out var outputPorts) ? outputPorts.Count : 0;
                var rowCount = Math.Max(inputCount, outputCount);
                return Math.Max(MinimumNodeHeight, 70f + (rowCount * 18f));
            }

            private void BuildPortDescriptors(IReadOnlyList<DependencyNode> visibleNodes)
            {
                var visibleGuids = new HashSet<string>(visibleNodes.Select(node => node.GUID));
                foreach (var node in visibleNodes)
                {
                    var outputPorts = new Dictionary<string, PortDescriptor>(StringComparer.Ordinal);
                    if (node.FieldSlots != null)
                    {
                        foreach (var fieldSlot in node.FieldSlots)
                        {
                            if (fieldSlot == null || !fieldSlot.IsOutput)
                            {
                                continue;
                            }

                            var safeFieldName = string.IsNullOrWhiteSpace(fieldSlot.Name) ? "unknown" : fieldSlot.Name;
                            outputPorts[safeFieldName] = new PortDescriptor
                            {
                                FieldName = safeFieldName,
                                HasValue = fieldSlot.HasValue,
                                ValueSummary = fieldSlot.ValueSummary,
                            };
                        }
                    }

                    _outputPortsByNode[node.GUID] = outputPorts.Values.OrderBy(port => port.FieldName, StringComparer.Ordinal).ToList();
                    _inputPortsByNode[node.GUID] = new List<PortDescriptor>();
                }

                foreach (var edge in _edges)
                {
                    if (edge?.From == null || edge.To == null)
                    {
                        continue;
                    }

                    if (!visibleGuids.Contains(edge.From.GUID) || !visibleGuids.Contains(edge.To.GUID))
                    {
                        continue;
                    }

                    var fieldName = string.IsNullOrWhiteSpace(edge.FieldName) ? "unknown" : edge.FieldName;

                    if (_outputPortsByNode.TryGetValue(edge.From.GUID, out var fromPorts))
                    {
                        var existingOutput = fromPorts.FirstOrDefault(port => string.Equals(port.FieldName, fieldName, StringComparison.Ordinal));
                        if (existingOutput == null)
                        {
                            fromPorts.Add(new PortDescriptor { FieldName = fieldName, HasValue = true, Type = edge.Type });
                        }
                        else if (!existingOutput.Type.HasValue)
                        {
                            existingOutput.Type = edge.Type;
                        }
                    }

                    if (_inputPortsByNode.TryGetValue(edge.To.GUID, out var toPorts) && !toPorts.Any(port => string.Equals(port.FieldName, fieldName, StringComparison.Ordinal)))
                    {
                        toPorts.Add(new PortDescriptor { FieldName = fieldName, HasValue = true, Type = edge.Type });
                    }
                }

                foreach (var guid in visibleGuids)
                {
                    if (_outputPortsByNode.TryGetValue(guid, out var outPorts))
                    {
                        outPorts.Sort((a, b) => string.Compare(a.FieldName, b.FieldName, StringComparison.Ordinal));
                    }

                    if (_inputPortsByNode.TryGetValue(guid, out var inPorts))
                    {
                        inPorts.Sort((a, b) => string.Compare(a.FieldName, b.FieldName, StringComparison.Ordinal));
                    }
                }
            }

            private void DrawEdges(MeshGenerationContext context)
            {
                var painter = context.painter2D;
                painter.lineWidth = 2f;

                foreach (var edge in _edges)
                {
                    if (!_nodeRects.TryGetValue(edge.From.GUID, out var fromRect) || !_nodeRects.TryGetValue(edge.To.GUID, out var toRect))
                    {
                        continue;
                    }

                    var outputPortKey = GetPortKey(edge.From.GUID, edge.FieldName, isOutput: true);
                    var inputPortKey = GetPortKey(edge.To.GUID, edge.FieldName, isOutput: false);

                    var start = _portAnchorByKey.TryGetValue(outputPortKey, out var outputAnchor)
                        ? outputAnchor
                        : new Vector2(fromRect.xMax, fromRect.center.y);
                    var end = _portAnchorByKey.TryGetValue(inputPortKey, out var inputAnchor)
                        ? inputAnchor
                        : new Vector2(toRect.xMin, toRect.center.y);
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

            private sealed class PortDescriptor
            {
                public string FieldName;
                public bool HasValue;
                public string ValueSummary;
                public DependencyType? Type;
            }
        }
    }
}
