using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        private const float DefaultTextSize = 11f;
        private const float MinimumTextSize = 9f;
        private const float MaximumTextSize = 24f;
        private const float DefaultIconSize = 16f;
        private const float MinimumIconSize = 10f;
        private const float MaximumIconSize = 48f;

        private SceneScanner _scanner;
        private DependencyModel _model;
        private ToolkitGraphCanvas _canvas;
        private ScrollView _hierarchyScrollView;
        private TextField _searchField;
        private Label _detailsLabel;
        private ColorField _nodeColorField;
        private ScrollView _fieldSlotsContainer;
        private FloatField _textSizeField;
        private FloatField _iconSizeField;

        private readonly HashSet<string> _hiddenNodeGuids = new();
        private readonly HashSet<string> _knownNodeGuids = new();
        private readonly Dictionary<string, Color> _typeColorOverrides = new();
        private readonly Dictionary<string, Color> _nodeColorOverrides = new();
        private readonly HashSet<string> _expandedHierarchyGroups = new();

        private DependencyNode _selectedNode;
        private DependencyType? _currentFilter;
        private float _textSize = DefaultTextSize;
        private float _iconSize = DefaultIconSize;

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

            _textSizeField = new FloatField("Text")
            {
                value = _textSize,
                tooltip = "Set text size used in graph nodes and ports.",
            };
            _textSizeField.style.width = 110f;
            _textSizeField.RegisterValueChangedCallback(evt =>
            {
                _textSize = Mathf.Clamp(evt.newValue, MinimumTextSize, MaximumTextSize);
                _textSizeField.SetValueWithoutNotify(_textSize);
                ApplyCanvasSizing();
                RedrawGraphOnly();
            });
            toolbar.Add(_textSizeField);

            _iconSizeField = new FloatField("Icons")
            {
                value = _iconSize,
                tooltip = "Set icon size used in graph nodes and ports.",
            };
            _iconSizeField.style.width = 120f;
            _iconSizeField.RegisterValueChangedCallback(evt =>
            {
                _iconSize = Mathf.Clamp(evt.newValue, MinimumIconSize, MaximumIconSize);
                _iconSizeField.SetValueWithoutNotify(_iconSize);
                ApplyCanvasSizing();
                RedrawGraphOnly();
            });
            toolbar.Add(_iconSizeField);

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

            var graphScrollView = new ScrollView()
            {
                horizontalScrollerVisibility = ScrollerVisibility.Auto,
                verticalScrollerVisibility = ScrollerVisibility.Auto,
            };

            _canvas = new ToolkitGraphCanvas();
            _canvas.style.minWidth = 3000f;
            _canvas.style.minHeight = 3000f;
            _canvas.OnNodeSelected += ShowNodeDetails;
            _canvas.OnGraphMutationRequested += RefreshGraph;
            _canvas.SetSizing(_textSize, _iconSize);
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

            _fieldSlotsContainer = new ScrollView { style = { maxHeight = 280f } };
            pane.Add(_fieldSlotsContainer);

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
            ApplyCanvasSizing();
            _canvas?.SetGraph(_model, _hiddenNodeGuids, _currentFilter, _typeColorOverrides, _nodeColorOverrides);
        }

        private void ApplyCanvasSizing()
        {
            _canvas?.SetSizing(_textSize, _iconSize);
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

            var monoBehaviourNodes = new Dictionary<GameObject, List<DependencyNode>>();
            var prefabNodesByRoot = new Dictionary<GameObject, List<DependencyNode>>();
            var scriptableObjectNodes = new List<DependencyNode>();
            var otherNodes = new List<DependencyNode>();

            foreach (var node in GetFilteredNodes())
            {
                if (TryGetPrefabAssetRoot(node, out var prefabRoot))
                {
                    if (!prefabNodesByRoot.TryGetValue(prefabRoot, out var prefabNodes))
                    {
                        prefabNodes = new List<DependencyNode>();
                        prefabNodesByRoot[prefabRoot] = prefabNodes;
                    }

                    prefabNodes.Add(node);
                    continue;
                }

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
            AddPrefabSection(prefabNodesByRoot);
            AddNodeSection("Scriptable Objects", "group:scriptable", scriptableObjectNodes);
            AddNodeSection("Other Nodes", "group:other", otherNodes);
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
                tooltip = node.DisplayName,
            };
            nameButton.style.flexGrow = 1;
            nameButton.style.flexShrink = 1;
            nameButton.style.flexBasis = 0f;
            nameButton.style.overflow = Overflow.Hidden;
            nameButton.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.Add(nameButton);

            var visibilityToggle = new Toggle
            {
                value = !_hiddenNodeGuids.Contains(node.GUID),
                tooltip = "Toggle this node's visibility in the graph.",
            };
            visibilityToggle.style.flexShrink = 0;
            visibilityToggle.style.marginLeft = 6f;
            visibilityToggle.RegisterValueChangedCallback(evt => SetNodeVisibility(node, evt.newValue));
            row.Add(visibilityToggle);

            return row;
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
                foreach (var node in nodesOnGameObject.OrderBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase))
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

            foreach (var node in nodes.OrderBy(node => node.DisplayName, StringComparer.OrdinalIgnoreCase))
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

        private void AddPrefabSection(Dictionary<GameObject, List<DependencyNode>> nodesByPrefabRoot)
        {
            var sectionKey = "group:prefabs";
            var prefabFoldout = CreateFoldout("Prefab Nodes", sectionKey, defaultExpanded: true);
            _hierarchyScrollView.Add(prefabFoldout);

            if (nodesByPrefabRoot.Count == 0)
            {
                prefabFoldout.Add(CreateInfoLabel("No prefab nodes in the current filter."));
                return;
            }

            foreach (var pair in nodesByPrefabRoot.OrderBy(entry => entry.Key.name, StringComparer.OrdinalIgnoreCase))
            {
                var prefabRoot = pair.Key;
                var assetPath = AssetDatabase.GetAssetPath(prefabRoot);
                var groupKey = string.IsNullOrWhiteSpace(assetPath) ? $"prefab:{prefabRoot.name}" : $"prefab:{assetPath}";
                var prefabGroup = CreateFoldout(prefabRoot.name, groupKey, defaultExpanded: false);
                prefabFoldout.Add(prefabGroup);

                var orderedNodes = pair.Value
                    .Distinct()
                    .OrderByDescending(node => node?.Owner == prefabRoot)
                    .ThenBy(node => node?.DisplayName, StringComparer.OrdinalIgnoreCase);

                foreach (var node in orderedNodes)
                {
                    prefabGroup.Add(CreateNodeRow(node));
                }
            }
        }

        private void SetNodeVisibility(DependencyNode node, bool shouldShow)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.GUID))
            {
                return;
            }

            _knownNodeGuids.Add(node.GUID);
            if (shouldShow)
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

        private static bool TryGetPrefabAssetRoot(DependencyNode node, out GameObject prefabRoot)
        {
            prefabRoot = null;
            if (node?.Owner is not UnityEngine.Object unityObject || unityObject == null || !EditorUtility.IsPersistent(unityObject))
            {
                return false;
            }

            var sourceGameObject = unityObject switch
            {
                GameObject gameObject => gameObject,
                Component component => component.gameObject,
                _ => null,
            };

            if (sourceGameObject == null)
            {
                return false;
            }

            var assetPath = AssetDatabase.GetAssetPath(sourceGameObject);
            if (string.IsNullOrWhiteSpace(assetPath) ||
                PrefabUtility.GetPrefabAssetType(sourceGameObject) == PrefabAssetType.NotAPrefab)
            {
                return false;
            }

            prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            return prefabRoot != null;
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
            SetNodeVisibility(node, _hiddenNodeGuids.Contains(node?.GUID));
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
                PopulateUnityReferenceSlots(null);
                return;
            }

            _detailsLabel.text = BuildNodeDetailsText(node);
            PopulateUnityReferenceSlots(node);
            _nodeColorField.SetValueWithoutNotify(GetNodeDisplayColor(node));
        }

        private string BuildNodeDetailsText(DependencyNode node)
        {
            var typeName = TypeUtility.GetFriendlyTypeName(node.Owner?.GetType());
            var lines = new List<string>
            {
                $"Name: {node.DisplayName}",
                $"Type: {typeName}",
                $"GUID: {node.GUID}",
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

        private void PopulateUnityReferenceSlots(DependencyNode node)
        {
            if (_fieldSlotsContainer == null)
            {
                return;
            }

            _fieldSlotsContainer.Clear();
            if (node == null)
            {
                return;
            }

            foreach (var slot in node.FieldSlots.OrderBy(slot => slot.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (slot == null || string.IsNullOrWhiteSpace(slot.Name))
                {
                    continue;
                }

                var slotType = slot.ValueType;
                var isUnityObjectSlot = slot.UnityReferenceValue != null
                                        || (slotType != null && typeof(UnityEngine.Object).IsAssignableFrom(slotType));
                if (!isUnityObjectSlot)
                {
                    continue;
                }

                var objectFieldType = slotType != null && typeof(UnityEngine.Object).IsAssignableFrom(slotType)
                    ? slotType
                    : typeof(UnityEngine.Object);

                var objectField = new ObjectField($"{(slot.IsOutput ? "OUT" : "IN")}: {slot.Name}")
                {
                    objectType = objectFieldType,
                    allowSceneObjects = true,
                    value = slot.UnityReferenceValue,
                    tooltip = "Drag and drop a Unity object to reassign this reference.",
                };

                objectField.RegisterValueChangedCallback(evt =>
                    ApplyNodeSlotReference(node, slot, evt.newValue as UnityEngine.Object));

                _fieldSlotsContainer.Add(objectField);
            }
        }

        private void ApplyNodeSlotReference(DependencyNode node, DependencyFieldSlot slot, UnityEngine.Object newValue)
        {
            if (node?.Owner is not UnityEngine.Object ownerObject || slot == null || string.IsNullOrWhiteSpace(slot.Name))
            {
                return;
            }

            var field = TypeUtility.GetAllInstanceFields(node.Owner.GetType())
                .FirstOrDefault(candidate => string.Equals(candidate.Name, slot.Name, StringComparison.Ordinal));
            if (field == null || !typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
            {
                return;
            }

            if (newValue != null && !field.FieldType.IsAssignableFrom(newValue.GetType()))
            {
                return;
            }

            Undo.RecordObject(ownerObject, "Reassign Dependency Reference");
            field.SetValue(node.Owner, newValue);
            EditorUtility.SetDirty(ownerObject);
            if (ownerObject is Component component)
            {
                EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
            }

            RefreshGraph();
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
            private const float BasePortRowHeight = 18f;

            private float _textSize = DefaultTextSize;
            private float _iconSize = DefaultIconSize;

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
            public Action OnGraphMutationRequested;

            public ToolkitGraphCanvas()
            {
                style.position = Position.Relative;
                generateVisualContent += DrawEdges;
            }

            public void SetSizing(float textSize, float iconSize)
            {
                _textSize = Mathf.Clamp(textSize, MinimumTextSize, MaximumTextSize);
                _iconSize = Mathf.Clamp(iconSize, MinimumIconSize, MaximumIconSize);
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
                card.style.borderTopWidth = 5f;
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

                var iconOrBadge = CreateNodeTypeIcon(node, _textSize, _iconSize);
                if (iconOrBadge != null)
                {
                    titleRow.Add(iconOrBadge);
                }

                var title = new Label(string.IsNullOrWhiteSpace(node.DisplayName) ? "<Unnamed>" : node.DisplayName);
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.fontSize = _textSize + 1f;
                title.style.flexGrow = 1f;
                title.style.whiteSpace = WhiteSpace.Normal;
                titleRow.Add(title);

                card.Add(titleRow);

                var subtitle = new Label(node.Owner != null ? TypeUtility.GetFriendlyTypeName(node.Owner.GetType()) : "Managed/Scratch Node");
                subtitle.style.fontSize = Mathf.Max(9f, _textSize - 1f);
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
                        var rowElement = new VisualElement { style = { flexDirection = FlexDirection.Row, minHeight = GetPortRowHeight() } };
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
                label.style.fontSize = _textSize;
                label.style.color = new Color(0.85f, 0.85f, 0.85f, 1f);
                label.style.unityTextAlign = isOutput ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
                label.style.maxWidth = Mathf.Max(100f, NodeWidth * 0.36f);
                label.style.overflow = Overflow.Hidden;
                label.style.textOverflow = TextOverflow.Ellipsis;
                label.style.whiteSpace = WhiteSpace.NoWrap;
                label.style.flexShrink = 1f;

                if (!isOutput)
                {
                    row.Add(bubble);
                    label.style.marginLeft = 3f;
                }

                var iconOrBadge = CreatePortTypeIcon(descriptor, _textSize, _iconSize);
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

            private static VisualElement CreatePortTypeIcon(PortDescriptor descriptor, float textSize, float iconSize)
            {
                var referencedType = descriptor.ValueType;
                var isUnityType = referencedType != null && typeof(UnityEngine.Object).IsAssignableFrom(referencedType);
                if (!isUnityType)
                {
                    return CreateManagedTypeBadge(textSize, iconSize);
                }

                if (referencedType != null && typeof(Component).IsAssignableFrom(referencedType))
                {
                    return CreateTypeIcon(EditorGUIUtility.IconContent("cs Script Icon")?.image, iconSize);
                }

                if (referencedType != null && typeof(ScriptableObject).IsAssignableFrom(referencedType))
                {
                    var iconTexture = EditorGUIUtility.ObjectContent(
                        descriptor.UnityReferenceValue,
                        referencedType)?.image;
                    return CreateTypeIcon(iconTexture, iconSize);
                }

                if (descriptor.Type == DependencyType.SerializedUnityRef || descriptor.Type == DependencyType.OdinSerializedRef)
                {
                    var iconTexture = EditorGUIUtility.ObjectContent(
                        descriptor.UnityReferenceValue,
                        referencedType)?.image
                        ?? EditorGUIUtility.ObjectContent(null, referencedType)?.image
                        ?? EditorGUIUtility.IconContent("cs Script Icon")?.image;
                    return CreateTypeIcon(iconTexture, iconSize);
                }

                return null;
            }

            private static VisualElement CreateManagedTypeBadge(float textSize, float iconSize)
            {
                var badge = new Label("C#");
                var clampedIconSize = Mathf.Clamp(iconSize, MinimumIconSize, MaximumIconSize);
                badge.style.minWidth = clampedIconSize * 2.1f;
                badge.style.height = Mathf.Max(clampedIconSize * 0.85f, textSize + 2f);
                badge.style.fontSize = Mathf.Max(8f, textSize - 1f);
                badge.style.paddingLeft = 3f;
                badge.style.paddingRight = 3f;
                badge.style.marginLeft = 3f;
                badge.style.marginRight = 3f;
                badge.style.color = Color.white;
                badge.style.unityTextAlign = TextAnchor.MiddleCenter;
                badge.style.backgroundColor = new Color(0.14f, 0.14f, 0.14f, 0.9f);
                badge.style.borderTopLeftRadius = 2f;
                badge.style.borderTopRightRadius = 2f;
                badge.style.borderBottomLeftRadius = 2f;
                badge.style.borderBottomRightRadius = 2f;
                return badge;
            }

            private static VisualElement CreateTypeIcon(Texture iconTexture, float iconSize)
            {
                if (iconTexture == null)
                {
                    return null;
                }

                var icon = new Image
                {
                    image = iconTexture,
                    scaleMode = ScaleMode.ScaleToFit,
                };
                var clampedIconSize = Mathf.Clamp(iconSize * 0.75f, MinimumIconSize, MaximumIconSize);
                icon.style.width = clampedIconSize;
                icon.style.height = clampedIconSize;
                icon.style.marginLeft = 3f;
                icon.style.marginRight = 3f;
                return icon;
            }

            private void UpdatePortAnchor(string portKey, VisualElement portRow, bool isOutput)
            {
                var localRect = contentRect;
                if (localRect.width <= 0f || localRect.height <= 0f)
                {
                    return;
                }

                var worldCenter = portRow.worldBound.center;
                var localCenter = new Vector2(
                    worldCenter.x - worldBound.xMin,
                    worldCenter.y - worldBound.yMin);

                if (!float.IsFinite(localCenter.x) || !float.IsFinite(localCenter.y))
                {
                    return;
                }

                var x = isOutput ? localCenter.x + (portRow.worldBound.width * 0.5f) : localCenter.x - (portRow.worldBound.width * 0.5f);
                x = Mathf.Clamp(x, localRect.xMin, localRect.xMax);
                var newAnchor = new Vector2(x, localCenter.y);

                if (_portAnchorByKey.TryGetValue(portKey, out var existingAnchor) &&
                    Vector2.SqrMagnitude(existingAnchor - newAnchor) < 0.01f)
                {
                    return;
                }

                _portAnchorByKey[portKey] = newAnchor;
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

            private static VisualElement CreateNodeTypeIcon(DependencyNode node, float textSize, float iconSize)
            {
                var ownerType = node.Owner?.GetType();
                if (node.Owner is UnityEngine.Object unityObject)
                {
                    var iconTexture = unityObject is Component
                        ? EditorGUIUtility.IconContent("cs Script Icon")?.image
                        : EditorGUIUtility.ObjectContent(unityObject, ownerType)?.image
                          ?? EditorGUIUtility.ObjectContent(null, ownerType)?.image
                          ?? EditorGUIUtility.IconContent("Prefab Icon")?.image;

                    return CreateTypeIcon(iconTexture, iconSize);
                }

                return CreateManagedTypeBadge(textSize, iconSize);
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
                return Math.Max(MinimumNodeHeight, 70f + (rowCount * GetPortRowHeight()));
            }

            private float GetPortRowHeight()
            {
                return Mathf.Max(BasePortRowHeight, _textSize + 10f, (_iconSize * 0.75f) + 6f);
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
                                ValueType = fieldSlot.ValueType,
                                UnityReferenceValue = fieldSlot.UnityReferenceValue,
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
                            fromPorts.Add(new PortDescriptor
                            {
                                FieldName = fieldName,
                                HasValue = true,
                                Type = edge.Type,
                            });
                        }

                        if (existingOutput != null && !existingOutput.Type.HasValue)
                        {
                            existingOutput.Type = edge.Type;
                        }
                    }

                    if (_inputPortsByNode.TryGetValue(edge.To.GUID, out var toPorts) && !toPorts.Any(port => string.Equals(port.FieldName, fieldName, StringComparison.Ordinal)))
                    {
                        toPorts.Add(new PortDescriptor
                        {
                            FieldName = fieldName,
                            HasValue = true,
                            Type = edge.Type,
                        });
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
                public Type ValueType;
                public UnityEngine.Object UnityReferenceValue;
            }
        }
    }
}
