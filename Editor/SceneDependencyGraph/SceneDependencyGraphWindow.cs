using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

public sealed class SceneDependencyGraphWindow : EditorWindow
{
    [Serializable]
    private sealed class HiddenNodeState
    {
        public List<string> HiddenNodeGuids = new();
        public List<string> KnownNodeGuids = new();
    }

    private const string HiddenNodePrefsPrefix = "SceneDependencyGraph.HiddenNodes";
    private const string NodeTypeColorPrefsPrefix = "SceneDependencyGraph.NodeTypeColors";
    private const string NodePositionPrefsPrefix = "SceneDependencyGraph.NodePositions";
    private const string WindowSettingsPrefsPrefix = "SceneDependencyGraph.WindowSettings";
    private const string NodeColorPrefsPrefix = "SceneDependencyGraph.NodeColors";

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
    private sealed class NodeLayoutEntry
    {
        public string NodeGuid;
        public Rect Position;
    }

    [Serializable]
    private sealed class NodeLayoutState
    {
        public List<NodeLayoutEntry> Entries = new();
    }

    [Serializable]
    private sealed class WindowSettingsState
    {
        public float HorizontalSpacing = 360f;
        public float VerticalSpacing = 48f;
        public float GroupSpacing = 520f;
        public float NodeFontSize = DefaultNodeFontSize;
        public float NodeIconSize = DefaultNodeIconSize;
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
    private const float DefaultNodeFontSize = 12f;
    private const float MinimumNodeFontSize = 10f;
    private const float MaximumNodeFontSize = 28f;
    private const float NodeFontSizeStep = 2f;
    private const float DefaultNodeIconSize = 14f;
    private const float MinimumNodeIconSize = 8f;
    private const float MaximumNodeIconSize = 32f;

    private SceneGraphView _graphView;
    private ScrollView _hierarchyScrollView;
    private TextField _searchField;
    private Label _detailsLabel;
    private ObjectField _reassignField;
    private ColorField _nodeColorField;
    private FloatField _horizontalSpacingField;
    private FloatField _verticalSpacingField;
    private FloatField _groupSpacingField;
    private Label _fontSizeLabel;
    private Slider _iconSizeSlider;
    private Label _nodeGraphProcessorStatusLabel;
    private float _nodeFontSize = DefaultNodeFontSize;
    private float _nodeIconSize = DefaultNodeIconSize;
    private float _horizontalSpacing = 360f;
    private float _verticalSpacing = 48f;
    private float _groupSpacing = 520f;

    private DependencyType? _currentFilter;
    private DependencyModel _model;
    private readonly HashSet<string> _hiddenNodeGuids = new();
    private readonly HashSet<string> _knownNodeGuids = new();
    private readonly HashSet<string> _expandedHierarchyGroups = new();
    private readonly Dictionary<string, Color> _nodeTypeColorOverrides = new();
    private readonly Dictionary<string, Rect> _savedNodePositions = new();
    private readonly Dictionary<string, Color> _nodeColorOverrides = new();
    private NodeGraphProcessorApi _nodeGraphProcessorApi;

    [MenuItem("Tools/Scene Dependency Graph")]
    public static void OpenWindow()
    {
        GetWindow<SceneDependencyGraphWindow>("Scene Dependency Graph");
    }

    private void OnEnable()
    {
        _nodeGraphProcessorApi = NodeGraphProcessorApi.Detect();
        LoadNodeTypeColorPreferences();
        LoadNodeColorPreferences();
        LoadWindowSettings();
        LoadNodePositionPreferences();
        BuildUi();
        RefreshGraph();
    }

    private void BuildUi()
    {
        rootVisualElement.Clear();

        var toolbar = new Toolbar();
        var refreshButton = new ToolbarButton(RefreshGraph) { text = "Refresh ⟳" };
        toolbar.Add(refreshButton);

        var organizeButton = new ToolbarButton(() =>
        {
            ApplySpacingSettings();
            _graphView?.OrganizeNodes();
        }) { text = "Organize" };
        toolbar.Add(organizeButton);

        _horizontalSpacingField = new FloatField("H Spacing")
        {
            value = _horizontalSpacing,
            tooltip = "Horizontal distance between organized node columns.",
        };
        _horizontalSpacingField.style.width = 170f;
        _horizontalSpacingField.RegisterValueChangedCallback(_ =>
        {
            ApplySpacingSettings();
            SaveWindowSettings();
        });
        toolbar.Add(_horizontalSpacingField);

        _verticalSpacingField = new FloatField("V Spacing")
        {
            value = _verticalSpacing,
            tooltip = "Vertical distance between organized nodes.",
        };
        _verticalSpacingField.style.width = 150f;
        _verticalSpacingField.RegisterValueChangedCallback(_ =>
        {
            ApplySpacingSettings();
            SaveWindowSettings();
        });
        toolbar.Add(_verticalSpacingField);

        _groupSpacingField = new FloatField("Group Spacing")
        {
            value = _groupSpacing,
            tooltip = "Distance between disconnected node groups when organizing.",
        };
        _groupSpacingField.style.width = 190f;
        _groupSpacingField.RegisterValueChangedCallback(_ =>
        {
            ApplySpacingSettings();
            SaveWindowSettings();
        });
        toolbar.Add(_groupSpacingField);

        var decreaseFontButton = new ToolbarButton(() => AdjustNodeFontSize(-NodeFontSizeStep)) { text = "Text -" };
        decreaseFontButton.tooltip = "Decrease node text size.";
        toolbar.Add(decreaseFontButton);

        _fontSizeLabel = new Label($"{_nodeFontSize:0}px")
        {
            tooltip = "Current node text size.",
        };
        _fontSizeLabel.style.minWidth = 46f;
        _fontSizeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        toolbar.Add(_fontSizeLabel);

        var increaseFontButton = new ToolbarButton(() => AdjustNodeFontSize(NodeFontSizeStep)) { text = "Text +" };
        increaseFontButton.tooltip = "Increase node text size.";
        toolbar.Add(increaseFontButton);

        _iconSizeSlider = new Slider("Icons", MinimumNodeIconSize, MaximumNodeIconSize)
        {
            value = _nodeIconSize,
            tooltip = "Adjust node icon size.",
        };
        _iconSizeSlider.style.width = 230f;
        _iconSizeSlider.RegisterValueChangedCallback(evt => SetNodeIconSize(evt.newValue));
        toolbar.Add(_iconSizeSlider);

        var ngpWindowButton = new ToolbarButton(() =>
        {
            if (_nodeGraphProcessorApi.TryOpenGraphWindow())
            {
                return;
            }

            EditorUtility.DisplayDialog(
                "NodeGraphProcessor Not Found",
                "Install com.alelievr.node-graph-processor in your project to enable NodeGraphProcessor editor integration.",
                "OK");
        })
        {
            text = "NGP Window",
            tooltip = "Open NodeGraphProcessor graph window if the package is installed.",
        };
        toolbar.Add(ngpWindowButton);

        var createNgpAssetButton = new ToolbarButton(CreateNodeGraphProcessorAsset)
        {
            text = "Create NGP Graph",
            tooltip = "Create a BaseGraph asset when NodeGraphProcessor is installed.",
        };
        toolbar.Add(createNgpAssetButton);

        _nodeGraphProcessorStatusLabel = new Label();
        _nodeGraphProcessorStatusLabel.style.marginLeft = 8f;
        toolbar.Add(_nodeGraphProcessorStatusLabel);
        UpdateNodeGraphProcessorStatus();

        var filterField = new ToolbarMenu { text = "Filter: All" };
        filterField.menu.AppendAction("All", _ => SetFilter(null));
        foreach (DependencyType type in Enum.GetValues(typeof(DependencyType)))
        {
            var capturedType = type;
            filterField.menu.AppendAction(type.ToString(), _ => SetFilter(capturedType));
        }
        toolbar.Add(filterField);


        rootVisualElement.Add(toolbar);

        var splitHorizontal = new TwoPaneSplitView(0, 260, TwoPaneSplitViewOrientation.Horizontal);
        var splitCenterRight = new TwoPaneSplitView(1, 900, TwoPaneSplitViewOrientation.Horizontal);

        splitHorizontal.Add(CreateLeftPane());
        splitHorizontal.Add(splitCenterRight);

        _graphView = new SceneGraphView();
        ApplySpacingSettings();
        _graphView.OnNodePositionChanged += SaveCurrentNodePositions;
        _graphView.OnNodeSelectionChanged += ShowNodeDetails;
        _graphView.OnEdgeSelectionChanged += ShowEdgeDetails;
        splitCenterRight.Add(_graphView);
        splitCenterRight.Add(CreateRightPane());

        rootVisualElement.Add(splitHorizontal);
    }


    private void UpdateNodeGraphProcessorStatus()
    {
        if (_nodeGraphProcessorStatusLabel == null)
        {
            return;
        }

        var status = _nodeGraphProcessorApi == null || !_nodeGraphProcessorApi.IsAvailable
            ? "NGP: Not Installed"
            : $"NGP: {_nodeGraphProcessorApi.DisplayVersion}";
        _nodeGraphProcessorStatusLabel.text = status;
    }

    private void CreateNodeGraphProcessorAsset()
    {
        if (_nodeGraphProcessorApi == null || !_nodeGraphProcessorApi.IsAvailable)
        {
            EditorUtility.DisplayDialog(
                "NodeGraphProcessor Not Found",
                "Install com.alelievr.node-graph-processor in your project to create NodeGraphProcessor graph assets.",
                "OK");
            return;
        }

        var path = EditorUtility.SaveFilePanelInProject(
            "Create NodeGraphProcessor Graph",
            "SceneDependencyGraph",
            "asset",
            "Select a destination for the NodeGraphProcessor graph asset.");

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (_nodeGraphProcessorApi.TryCreateGraphAsset(path))
        {
            UpdateNodeGraphProcessorStatus();
            return;
        }

        EditorUtility.DisplayDialog(
            "Unsupported NodeGraphProcessor Version",
            "Could not create a BaseGraph asset. The installed NodeGraphProcessor version may require a custom graph type.",
            "OK");
    }

    private VisualElement CreateLeftPane()
    {
        var pane = new VisualElement { style = { flexGrow = 1 } };
        pane.Add(new Label("Hierarchy") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
        _searchField = new TextField("Search") { value = string.Empty };
        _searchField.RegisterValueChangedCallback(_ => RebuildHierarchyList());
        pane.Add(_searchField);

        var visibilityToolbar = new VisualElement();
        visibilityToolbar.style.flexDirection = FlexDirection.Row;

        var showAllButton = new Button(() => SetVisibilityForFilteredNodes(true)) { text = "Show Filtered" };
        var hideAllButton = new Button(() => SetVisibilityForFilteredNodes(false)) { text = "Hide Filtered" };
        visibilityToolbar.Add(showAllButton);
        visibilityToolbar.Add(hideAllButton);
        pane.Add(visibilityToolbar);

        _hierarchyScrollView = new ScrollView { style = { flexGrow = 1 } };
        pane.Add(_hierarchyScrollView);
        return pane;
    }

    private VisualElement CreateRightPane()
    {
        var pane = new VisualElement { style = { flexGrow = 1, paddingLeft = 8, paddingTop = 8 } };
        pane.Add(new Label("Details") { style = { unityFontStyleAndWeight = FontStyle.Bold } });

        _detailsLabel = new Label("Select a node or edge.") { style = { whiteSpace = WhiteSpace.Normal } };
        pane.Add(_detailsLabel);

        _nodeColorField = new ColorField("Node Color")
        {
            value = Color.gray,
            showAlpha = false,
            tooltip = "Choose a color for the selected node."
        };
        _nodeColorField.RegisterValueChangedCallback(evt => ApplyColorToSelectedNode(evt.newValue));
        pane.Add(_nodeColorField);

        var applyTypeColorButton = new Button(ApplyColorToSelectedNodeType) { text = "Apply Color To Node Type" };
        applyTypeColorButton.tooltip = "Apply the current node color to every node of this type.";
        pane.Add(applyTypeColorButton);

        var pingButton = new Button(() => PingSelectedObject()) { text = "Ping" };
        pane.Add(pingButton);

        _reassignField = new ObjectField("Reassign") { allowSceneObjects = true, objectType = typeof(UnityEngine.Object) };
        pane.Add(_reassignField);

        var reassignButton = new Button(ApplyReassign) { text = "Apply Reference" };
        pane.Add(reassignButton);

        var removeListenerButton = new Button(RemovePersistentListener) { text = "Remove UnityEvent Listener" };
        pane.Add(removeListenerButton);

        return pane;
    }

    private DependencyNode _selectedNode;
    private DependencyEdge _selectedEdge;

    private void ShowNodeDetails(DependencyNode node)
    {
        _selectedNode = node;
        _selectedEdge = null;
        if (node == null)
        {
            _detailsLabel.text = "Select a node or edge.";
            return;
        }

        _detailsLabel.text = BuildNodeDetailsText(node);

        if (_nodeColorField != null)
        {
            _nodeColorField.SetValueWithoutNotify(GetNodeDisplayColor(node));
        }

        if (node.Owner is UnityEngine.Object unityObject)
        {
            Selection.activeObject = unityObject;
        }
    }

    private string BuildNodeDetailsText(DependencyNode node)
    {
        var typeName = TypeUtility.GetFriendlyTypeName(node.Owner?.GetType());
        var lines = new List<string>
        {
            $"Name: {node.DisplayName}",
            $"Type: {typeName}",
        };

        if (node.FieldSlots.Count > 0)
        {
            lines.Add("\nFields:");
            foreach (var slot in node.FieldSlots.OrderBy(slot => slot.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (slot == null)
                {
                    continue;
                }

                var direction = slot.IsOutput ? "OUT" : "IN";
                var state = slot.HasValue ? "filled" : "empty";
                var summary = string.IsNullOrWhiteSpace(slot.ValueSummary) ? string.Empty : $" ({slot.ValueSummary})";
                lines.Add($"- {direction} {slot.Name}: {state}{summary}");
            }
        }

        var outgoingEdges = _model?.Edges?.Where(edge => edge.From == node).ToList() ?? new List<DependencyEdge>();
        if (outgoingEdges.Count > 0)
        {
            lines.Add("\nReferences:");
            foreach (var edge in outgoingEdges.OrderBy(edge => edge.FieldName, StringComparer.OrdinalIgnoreCase))
            {
                lines.Add($"- {edge.FieldName} -> {edge.To.DisplayName}");
            }
        }

        return string.Join("\n", lines);
    }

    private void ShowEdgeDetails(DependencyEdge edge)
    {
        if (edge == null)
        {
            _selectedEdge = null;
            return;
        }

        _selectedNode = null;
        _selectedEdge = edge;

        _detailsLabel.text = $"Edge: {edge.From.DisplayName} -> {edge.To.DisplayName}\nField: {edge.FieldName}\nType: {edge.Type}\nDetails: {edge.Details}";
        _reassignField.value = edge.ActionContext?.UnityReferenceValue;
    }


    private void ApplySpacingSettings()
    {
        if (_graphView == null)
        {
            return;
        }

        _horizontalSpacing = Mathf.Max(80f, _horizontalSpacingField?.value ?? _horizontalSpacing);
        _verticalSpacing = Mathf.Max(12f, _verticalSpacingField?.value ?? _verticalSpacing);
        _groupSpacing = Mathf.Max(120f, _groupSpacingField?.value ?? _groupSpacing);
        _graphView.HorizontalSpacing = _horizontalSpacing;
        _graphView.VerticalSpacing = _verticalSpacing;
        _graphView.GroupSpacing = _groupSpacing;
        _graphView.NodeFontSize = Mathf.Clamp(_nodeFontSize, MinimumNodeFontSize, MaximumNodeFontSize);
        _graphView.NodeIconSize = Mathf.Clamp(_nodeIconSize, MinimumNodeIconSize, MaximumNodeIconSize);
    }

    private void SetNodeIconSize(float newSize)
    {
        var updatedSize = Mathf.Clamp(newSize, MinimumNodeIconSize, MaximumNodeIconSize);
        if (Mathf.Approximately(updatedSize, _nodeIconSize))
        {
            return;
        }

        _nodeIconSize = updatedSize;
        ApplySpacingSettings();
        SaveWindowSettings();
        RedrawGraphOnly();
    }

    private void AdjustNodeFontSize(float delta)
    {
        var updatedSize = Mathf.Clamp(_nodeFontSize + delta, MinimumNodeFontSize, MaximumNodeFontSize);
        if (Mathf.Approximately(updatedSize, _nodeFontSize))
        {
            return;
        }

        _nodeFontSize = updatedSize;
        if (_fontSizeLabel != null)
        {
            _fontSizeLabel.text = $"{_nodeFontSize:0}px";
        }

        ApplySpacingSettings();
        SaveWindowSettings();
        RedrawGraphOnly();
    }

    private void SetFilter(DependencyType? filter)
    {
        _currentFilter = filter;
        RedrawGraphOnly();
    }

    private void RefreshGraph()
    {
        _model = new SceneScanner().ScanScene();
        LoadHiddenNodePreferences();
        LoadNodePositionPreferences();
        LoadNodeColorPreferences();
        RebuildHierarchyList();
        RedrawGraphOnly();
    }

    private void RedrawGraphOnly()
    {
        if (_model == null)
        {
            return;
        }

        var visibleNodes = _model.Nodes.FindAll(node => !_hiddenNodeGuids.Contains(node.GUID));
        var visibleEdges = _model.Edges.FindAll(edge =>
            !_hiddenNodeGuids.Contains(edge.From.GUID) &&
            !_hiddenNodeGuids.Contains(edge.To.GUID));

        _graphView.SetTypeColorOverrides(_nodeTypeColorOverrides);
        _graphView.Populate(visibleNodes, visibleEdges, _currentFilter);
        foreach (var pair in _nodeColorOverrides)
        {
            _graphView.SetNodeColorOverride(pair.Key, pair.Value);
        }

        _graphView.OrganizeNodes();
        _graphView.ApplyNodePositions(_savedNodePositions);
    }

    private void RebuildHierarchyList()
    {
        if (_model == null || _hierarchyScrollView == null)
        {
            return;
        }

        _hierarchyScrollView.Clear();

        var filteredNodes = GetFilteredNodes();

        var monoBehaviourNodes = new Dictionary<GameObject, List<DependencyNode>>();
        var scriptableObjectNodes = new List<DependencyNode>();
        var otherNodes = new List<DependencyNode>();

        foreach (var node in filteredNodes)
        {
            if (node?.Owner is MonoBehaviour monoBehaviour && monoBehaviour != null)
            {
                if (!monoBehaviourNodes.TryGetValue(monoBehaviour.gameObject, out var gameObjectNodes))
                {
                    gameObjectNodes = new List<DependencyNode>();
                    monoBehaviourNodes[monoBehaviour.gameObject] = gameObjectNodes;
                }

                gameObjectNodes.Add(node);
                continue;
            }

            if (node?.Owner is ScriptableObject)
            {
                scriptableObjectNodes.Add(node);
                continue;
            }

            otherNodes.Add(node);
        }

        AddHierarchySection(monoBehaviourNodes);
        AddNodeSection("Scriptable Objects", "group:scriptable", scriptableObjectNodes);
        AddNodeSection("Other Nodes", "group:other", otherNodes);
    }

    private List<DependencyNode> GetFilteredNodes()
    {
        var search = _searchField?.value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(search))
        {
            return _model.Nodes;
        }

        return _model.Nodes.FindAll(node => node.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private void AddHierarchySection(Dictionary<GameObject, List<DependencyNode>> nodesByGameObject)
    {
        var sectionKey = "group:scene-hierarchy";
        var hierarchyFoldout = CreateFoldout("Scene Hierarchy (MonoBehaviours)", sectionKey, defaultExpanded: true);
        _hierarchyScrollView.Add(hierarchyFoldout);

        if (nodesByGameObject.Count == 0)
        {
            hierarchyFoldout.Add(CreateInfoLabel("No MonoBehaviour nodes in the current filter."));
            return;
        }

        var includedTransforms = new HashSet<Transform>();
        foreach (var gameObject in nodesByGameObject.Keys)
        {
            var current = gameObject.transform;
            while (current != null)
            {
                includedTransforms.Add(current);
                current = current.parent;
            }
        }

        var activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            hierarchyFoldout.Add(CreateInfoLabel("Active scene is not valid."));
            return;
        }

        foreach (var rootObject in activeScene.GetRootGameObjects())
        {
            var branch = BuildGameObjectBranch(rootObject.transform, nodesByGameObject, includedTransforms);
            if (branch != null)
            {
                hierarchyFoldout.Add(branch);
            }
        }
    }

    private VisualElement BuildGameObjectBranch(
        Transform transform,
        Dictionary<GameObject, List<DependencyNode>> nodesByGameObject,
        HashSet<Transform> includedTransforms)
    {
        if (transform == null || !includedTransforms.Contains(transform))
        {
            return null;
        }

        var foldout = CreateFoldout(transform.name, $"go:{GetTransformPath(transform)}", defaultExpanded: false);

        if (nodesByGameObject.TryGetValue(transform.gameObject, out var nodesOnGameObject))
        {
            foreach (var node in nodesOnGameObject.OrderBy(node => node.DisplayName))
            {
                foldout.Add(CreateNodeRow(node));
            }
        }

        for (var childIndex = 0; childIndex < transform.childCount; childIndex++)
        {
            var childBranch = BuildGameObjectBranch(transform.GetChild(childIndex), nodesByGameObject, includedTransforms);
            if (childBranch != null)
            {
                foldout.Add(childBranch);
            }
        }

        return foldout;
    }

    private void AddNodeSection(string title, string sectionKey, List<DependencyNode> nodes)
    {
        var foldout = CreateFoldout(title, sectionKey, defaultExpanded: true);
        _hierarchyScrollView.Add(foldout);

        if (nodes.Count == 0)
        {
            foldout.Add(CreateInfoLabel("No nodes in the current filter."));
            return;
        }

        foreach (var node in nodes.OrderBy(node => node.DisplayName))
        {
            foldout.Add(CreateNodeRow(node));
        }
    }

    private Foldout CreateFoldout(string title, string groupKey, bool defaultExpanded)
    {
        var isExpanded = _expandedHierarchyGroups.Contains(groupKey) || defaultExpanded;
        var foldout = new Foldout
        {
            text = title,
            value = isExpanded,
        };

        if (foldout.value)
        {
            _expandedHierarchyGroups.Add(groupKey);
        }

        foldout.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue)
            {
                _expandedHierarchyGroups.Add(groupKey);
            }
            else
            {
                _expandedHierarchyGroups.Remove(groupKey);
            }
        });

        return foldout;
    }

    private static string GetTransformPath(Transform transform)
    {
        var names = new List<string>();
        var current = transform;
        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }

    private VisualElement CreateNodeRow(DependencyNode node)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.width = Length.Percent(100);
        row.style.marginRight = 6f;

        var nameButton = new Button(() => _graphView?.FocusNode(node))
        {
            text = _hiddenNodeGuids.Contains(node.GUID) ? $"{node.DisplayName} (hidden)" : node.DisplayName,
        };
        nameButton.style.flexGrow = 1;
        nameButton.style.unityTextAlign = TextAnchor.MiddleLeft;
        row.Add(nameButton);

        var visibilityButton = new Button(() => ToggleNodeVisibility(node))
        {
            text = _hiddenNodeGuids.Contains(node.GUID) ? "Show" : "Hide",
        };
        visibilityButton.style.width = 56f;
        visibilityButton.style.flexShrink = 0;
        visibilityButton.style.unityTextAlign = TextAnchor.MiddleCenter;
        row.Add(visibilityButton);

        return row;
    }

    private static Label CreateInfoLabel(string message)
    {
        return new Label(message)
        {
            style =
            {
                unityFontStyleAndWeight = FontStyle.Italic,
                whiteSpace = WhiteSpace.Normal,
            },
        };
    }

    private void SetVisibilityForFilteredNodes(bool showNodes)
    {
        if (_model == null)
        {
            return;
        }

        foreach (var node in GetFilteredNodes())
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

        var savedState = JsonUtility.FromJson<HiddenNodeState>(rawJson);
        if (savedState?.HiddenNodeGuids == null)
        {
            HideAllCurrentNodesAsDefault();
            SaveHiddenNodePreferences();
            return;
        }

        foreach (var guid in savedState.HiddenNodeGuids)
        {
            if (!string.IsNullOrWhiteSpace(guid))
            {
                _hiddenNodeGuids.Add(guid);
            }
        }

        var stateHasKnownNodes = savedState.KnownNodeGuids != null && savedState.KnownNodeGuids.Count > 0;
        if (stateHasKnownNodes)
        {
            foreach (var guid in savedState.KnownNodeGuids)
            {
                if (!string.IsNullOrWhiteSpace(guid))
                {
                    _knownNodeGuids.Add(guid);
                }
            }
        }
        else
        {
            foreach (var node in _model.Nodes)
            {
                if (!string.IsNullOrWhiteSpace(node.GUID))
                {
                    _knownNodeGuids.Add(node.GUID);
                }
            }
        }

        var discoveredNewNode = false;
        foreach (var node in _model.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.GUID))
            {
                continue;
            }

            if (_knownNodeGuids.Add(node.GUID))
            {
                _hiddenNodeGuids.Add(node.GUID);
                discoveredNewNode = true;
            }
        }

        if (discoveredNewNode)
        {
            SaveHiddenNodePreferences();
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

        var json = JsonUtility.ToJson(state);
        EditorPrefs.SetString(GetHiddenNodePrefsKey(), json);
    }

    private string GetHiddenNodePrefsKey()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var sceneKey = string.IsNullOrWhiteSpace(scene.path) ? scene.name : scene.path;
        if (string.IsNullOrWhiteSpace(sceneKey))
        {
            sceneKey = "UnsavedScene";
        }

        return $"{HiddenNodePrefsPrefix}.{sceneKey}";
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
            _hiddenNodeGuids.Remove(node.GUID);
        }
        else
        {
            _hiddenNodeGuids.Add(node.GUID);
        }

        SaveHiddenNodePreferences();
        RebuildHierarchyList();
        RedrawGraphOnly();
    }



    private void ApplyColorToSelectedNode(Color color)
    {
        if (_selectedNode == null || string.IsNullOrWhiteSpace(_selectedNode.GUID))
        {
            return;
        }

        _nodeColorOverrides[_selectedNode.GUID] = color;
        SaveNodeColorPreferences();
        _graphView?.SetNodeColorOverride(_selectedNode.GUID, color);
    }

    private void ApplyColorToSelectedNodeType()
    {
        if (_selectedNode == null)
        {
            return;
        }

        var typeKey = SceneGraphView.GetNodeTypeKey(_selectedNode);
        if (string.IsNullOrWhiteSpace(typeKey) || _nodeColorField == null)
        {
            return;
        }

        var selectedColor = _nodeColorField.value;
        _nodeTypeColorOverrides[typeKey] = selectedColor;
        SaveNodeTypeColorPreferences();
        _graphView?.SetTypeColorOverride(typeKey, selectedColor);
    }

    private Color GetNodeDisplayColor(DependencyNode node)
    {
        var typeKey = SceneGraphView.GetNodeTypeKey(node);
        if (!string.IsNullOrWhiteSpace(typeKey) && _nodeTypeColorOverrides.TryGetValue(typeKey, out var storedColor))
        {
            return storedColor;
        }

        return SceneGraphView.GetDefaultNodeColor(node);
    }

    private void LoadNodeTypeColorPreferences()
    {
        _nodeTypeColorOverrides.Clear();

        var rawJson = EditorPrefs.GetString(GetNodeTypeColorPrefsKey(), string.Empty);
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

            if (ColorUtility.TryParseHtmlString(entry.HtmlColor, out var parsedColor))
            {
                _nodeTypeColorOverrides[entry.TypeKey] = parsedColor;
            }
        }
    }

    private void SaveNodeTypeColorPreferences()
    {
        var state = new NodeTypeColorState();
        foreach (var pair in _nodeTypeColorOverrides)
        {
            state.Entries.Add(new NodeTypeColorEntry
            {
                TypeKey = pair.Key,
                HtmlColor = $"#{ColorUtility.ToHtmlStringRGB(pair.Value)}",
            });
        }

        EditorPrefs.SetString(GetNodeTypeColorPrefsKey(), JsonUtility.ToJson(state));
    }

    private static string GetNodeTypeColorPrefsKey()
    {
        return NodeTypeColorPrefsPrefix;
    }

    private void SaveCurrentNodePositions()
    {
        if (_graphView == null)
        {
            return;
        }

        var latestPositions = _graphView.CaptureNodePositions();
        _savedNodePositions.Clear();
        foreach (var pair in latestPositions)
        {
            _savedNodePositions[pair.Key] = pair.Value;
        }

        SaveNodePositionPreferences();
    }

    private void LoadNodePositionPreferences()
    {
        _savedNodePositions.Clear();
        var rawJson = EditorPrefs.GetString(GetNodePositionPrefsKey(), string.Empty);
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return;
        }

        var state = JsonUtility.FromJson<NodeLayoutState>(rawJson);
        if (state?.Entries == null)
        {
            return;
        }

        foreach (var entry in state.Entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.NodeGuid))
            {
                continue;
            }

            _savedNodePositions[entry.NodeGuid] = entry.Position;
        }
    }

    private void SaveNodePositionPreferences()
    {
        var state = new NodeLayoutState();
        foreach (var pair in _savedNodePositions)
        {
            state.Entries.Add(new NodeLayoutEntry
            {
                NodeGuid = pair.Key,
                Position = pair.Value,
            });
        }

        EditorPrefs.SetString(GetNodePositionPrefsKey(), JsonUtility.ToJson(state));
    }

    private string GetNodePositionPrefsKey()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var sceneKey = string.IsNullOrWhiteSpace(scene.path) ? scene.name : scene.path;
        if (string.IsNullOrWhiteSpace(sceneKey))
        {
            sceneKey = "UnsavedScene";
        }

        return $"{NodePositionPrefsPrefix}.{sceneKey}";
    }

    private void LoadWindowSettings()
    {
        var rawJson = EditorPrefs.GetString(WindowSettingsPrefsPrefix, string.Empty);
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return;
        }

        var settings = JsonUtility.FromJson<WindowSettingsState>(rawJson);
        if (settings == null)
        {
            return;
        }

        _nodeFontSize = Mathf.Clamp(settings.NodeFontSize, MinimumNodeFontSize, MaximumNodeFontSize);
        _nodeIconSize = Mathf.Clamp(settings.NodeIconSize, MinimumNodeIconSize, MaximumNodeIconSize);
        _horizontalSpacing = Mathf.Max(80f, settings.HorizontalSpacing);
        _verticalSpacing = Mathf.Max(12f, settings.VerticalSpacing);
        _groupSpacing = Mathf.Max(120f, settings.GroupSpacing);

        if (_horizontalSpacingField != null)
        {
            _horizontalSpacingField.SetValueWithoutNotify(_horizontalSpacing);
        }

        if (_verticalSpacingField != null)
        {
            _verticalSpacingField.SetValueWithoutNotify(_verticalSpacing);
        }

        if (_groupSpacingField != null)
        {
            _groupSpacingField.SetValueWithoutNotify(_groupSpacing);
        }
    }

    private void SaveWindowSettings()
    {
        var settings = new WindowSettingsState
        {
            HorizontalSpacing = _horizontalSpacing,
            VerticalSpacing = _verticalSpacing,
            GroupSpacing = _groupSpacing,
            NodeFontSize = Mathf.Clamp(_nodeFontSize, MinimumNodeFontSize, MaximumNodeFontSize),
            NodeIconSize = Mathf.Clamp(_nodeIconSize, MinimumNodeIconSize, MaximumNodeIconSize),
        };

        EditorPrefs.SetString(WindowSettingsPrefsPrefix, JsonUtility.ToJson(settings));
    }

    private void LoadNodeColorPreferences()
    {
        _nodeColorOverrides.Clear();
        var rawJson = EditorPrefs.GetString(GetNodeColorPrefsKey(), string.Empty);
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

        EditorPrefs.SetString(GetNodeColorPrefsKey(), JsonUtility.ToJson(state));
    }

    private string GetNodeColorPrefsKey()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var sceneKey = string.IsNullOrWhiteSpace(scene.path) ? scene.name : scene.path;
        if (string.IsNullOrWhiteSpace(sceneKey))
        {
            sceneKey = "UnsavedScene";
        }

        return $"{NodeColorPrefsPrefix}.{sceneKey}";
    }

    private void PingSelectedObject()
    {
        if (_selectedNode?.Owner is UnityEngine.Object unityObject)
        {
            EditorGUIUtility.PingObject(unityObject);
        }
    }

    private void RemovePersistentListener()
    {
        if (_selectedEdge?.Type != DependencyType.UnityEvent || _selectedEdge.ActionContext == null)
        {
            return;
        }

        var context = _selectedEdge.ActionContext;
        var unityEvent = context.FieldInfo.GetValue(context.OwnerObject) as UnityEventBase;
        if (unityEvent == null || context.PersistentListenerIndex < 0)
        {
            return;
        }

        UnityEventTools.RemovePersistentListener(unityEvent, context.PersistentListenerIndex);
        EditorUtility.SetDirty(context.OwnerObject);
        if (context.OwnerObject is Component component)
        {
            EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
        }
        RefreshGraph();
    }

    private void ApplyReassign()
    {
        if (_selectedEdge?.ActionContext == null)
        {
            return;
        }

        var context = _selectedEdge.ActionContext;
        if (context.FieldInfo == null || context.OwnerObject == null)
        {
            return;
        }

        if (!typeof(UnityEngine.Object).IsAssignableFrom(context.FieldInfo.FieldType))
        {
            return;
        }

        var newValue = _reassignField.value;
        if (newValue != null && !context.FieldInfo.FieldType.IsInstanceOfType(newValue))
        {
            return;
        }

        context.FieldInfo.SetValue(context.OwnerObject, newValue);
        EditorUtility.SetDirty(context.OwnerObject);
        if (context.OwnerObject is Component component)
        {
            EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
        }
        RefreshGraph();
    }
}
