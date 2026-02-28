using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace RonJames.DependencyGraphTool;

public sealed class SceneGraphView : GraphView
{
    private const float DefaultHorizontalSpacing = 360f;
    private const float DefaultVerticalSpacing = 48f;
    private const float DefaultGroupSpacing = 520f;
    private const float DefaultNodeWidth = 260f;
    private const float DefaultNodeHeight = 120f;
    private const float DefaultNodeFontSize = 12f;
    private const float DefaultNodeIconSize = 14f;

    private readonly Dictionary<string, Node> _nodeLookup = new();
    private readonly Dictionary<string, Dictionary<string, Port>> _outputPortsByNodeAndField = new();
    private readonly Dictionary<string, Dictionary<string, Port>> _inputPortsByNodeAndField = new();
    private readonly Dictionary<string, Color> _nodeTypeColors = new();
    private readonly Dictionary<string, Color> _nodeGuidColors = new();

    public Action<DependencyNode> OnNodeSelectionChanged;
    public Action<DependencyEdge> OnEdgeSelectionChanged;
    public Action OnNodePositionChanged;

    public float HorizontalSpacing { get; set; } = DefaultHorizontalSpacing;
    public float VerticalSpacing { get; set; } = DefaultVerticalSpacing;
    public float GroupSpacing { get; set; } = DefaultGroupSpacing;
    public float NodeFontSize { get; set; } = DefaultNodeFontSize;
    public float NodeIconSize { get; set; } = DefaultNodeIconSize;

    public SceneGraphView()
    {
        style.flexGrow = 1f;

        this.SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());

        var grid = new GridBackground();
        Insert(0, grid);
        grid.StretchToParentSize();

        graphViewChanged += change =>
        {
            if (change.movedElements != null && change.movedElements.Count > 0)
            {
                OnNodePositionChanged?.Invoke();
            }

            NotifySelection();
            return change;
        };
    }


    public override void AddToSelection(ISelectable selectable)
    {
        base.AddToSelection(selectable);
        NotifySelection();
    }

    public override void RemoveFromSelection(ISelectable selectable)
    {
        base.RemoveFromSelection(selectable);
        NotifySelection();
    }

    public override void ClearSelection()
    {
        base.ClearSelection();
        NotifySelection();
    }

    public void Populate(IReadOnlyList<DependencyNode> nodes, IReadOnlyList<DependencyEdge> edges, DependencyType? filterType)
    {
        DeleteElements(graphElements);
        _nodeLookup.Clear();
        _outputPortsByNodeAndField.Clear();
        _inputPortsByNodeAndField.Clear();

        var filteredEdges = new List<DependencyEdge>();
        foreach (var edge in edges)
        {
            if (filterType.HasValue && edge.Type != filterType.Value)
            {
                continue;
            }

            filteredEdges.Add(edge);
            CreateVisualNode(edge.From);
            CreateVisualNode(edge.To);
        }

        foreach (var node in nodes)
        {
            if (_nodeLookup.ContainsKey(node.GUID))
            {
                continue;
            }

            if (filterType.HasValue)
            {
                continue;
            }

            CreateVisualNode(node);
        }

        foreach (var node in nodes)
        {
            if (node?.FieldSlots == null || !_nodeLookup.TryGetValue(node.GUID, out var visualNode))
            {
                continue;
            }

            foreach (var fieldSlot in node.FieldSlots)
            {
                if (fieldSlot == null || !fieldSlot.IsOutput)
                {
                    continue;
                }

                GetOrCreatePort(node.GUID, fieldSlot.Name, visualNode, Direction.Output, fieldSlot.HasValue, fieldSlot.ValueSummary);
            }
        }

        foreach (var edge in filteredEdges)
        {
            if (!_nodeLookup.TryGetValue(edge.From.GUID, out var fromNode) ||
                !_nodeLookup.TryGetValue(edge.To.GUID, out var toNode))
            {
                continue;
            }

            var outputPort = GetOrCreatePort(edge.From.GUID, edge.FieldName, fromNode, Direction.Output, hasValue: true, valueSummary: null);
            var inputPort = GetOrCreatePort(edge.To.GUID, edge.FieldName, toNode, Direction.Input, hasValue: true, valueSummary: null);
            var graphEdge = outputPort.ConnectTo(inputPort);
            graphEdge.userData = edge;
            graphEdge.tooltip = $"{edge.FieldName} ({edge.Type})";
            graphEdge.edgeControl.outputColor = EdgeColor(edge.Type, edge.IsBroken);
            AddElement(graphEdge);
        }

        FrameAll();
    }

    public void SetTypeColorOverrides(IReadOnlyDictionary<string, Color> typeColorOverrides)
    {
        _nodeTypeColors.Clear();
        if (typeColorOverrides == null)
        {
            return;
        }

        foreach (var pair in typeColorOverrides)
        {
            _nodeTypeColors[pair.Key] = pair.Value;
        }
    }

    public void SetNodeColorOverride(string nodeGuid, Color color)
    {
        if (string.IsNullOrWhiteSpace(nodeGuid))
        {
            return;
        }

        _nodeGuidColors[nodeGuid] = color;
        if (_nodeLookup.TryGetValue(nodeGuid, out var visualNode))
        {
            ApplyNodeColorStyles(visualNode, color, Color.white);
        }
    }

    public void SetTypeColorOverride(string typeKey, Color color)
    {
        if (string.IsNullOrWhiteSpace(typeKey))
        {
            return;
        }

        _nodeTypeColors[typeKey] = color;
        foreach (var pair in _nodeLookup)
        {
            if (pair.Value.userData is not DependencyNode node || GetNodeTypeKey(node) != typeKey)
            {
                continue;
            }

            if (_nodeGuidColors.ContainsKey(node.GUID))
            {
                continue;
            }

            ApplyNodeColorStyles(pair.Value, color, Color.white);
        }
    }

    public void OrganizeNodes()
    {
        if (_nodeLookup.Count == 0)
        {
            return;
        }

        var outgoing = new Dictionary<string, HashSet<string>>(_nodeLookup.Count);
        var incoming = new Dictionary<string, HashSet<string>>(_nodeLookup.Count);
        var indegree = new Dictionary<string, int>(_nodeLookup.Count);
        foreach (var nodeId in _nodeLookup.Keys)
        {
            outgoing[nodeId] = new HashSet<string>();
            incoming[nodeId] = new HashSet<string>();
            indegree[nodeId] = 0;
        }

        foreach (var edgeElement in edges)
        {
            if (edgeElement.userData is not DependencyEdge edge)
            {
                continue;
            }

            if (!outgoing.ContainsKey(edge.From.GUID) || !incoming.ContainsKey(edge.To.GUID))
            {
                continue;
            }

            if (outgoing[edge.From.GUID].Add(edge.To.GUID))
            {
                incoming[edge.To.GUID].Add(edge.From.GUID);
                indegree[edge.To.GUID] += 1;
            }
        }

        const float startX = 80f;
        const float startY = 60f;
        var horizontalSpacing = Mathf.Max(80f, HorizontalSpacing);
        var verticalSpacing = Mathf.Max(12f, VerticalSpacing);
        var groupSpacing = Mathf.Max(120f, GroupSpacing);

        var connectedGroups = BuildConnectedGroups(outgoing, incoming);
        connectedGroups.Sort((a, b) => b.Count.CompareTo(a.Count));

        var currentGroupStartX = startX;
        foreach (var group in connectedGroups)
        {
            var groupWidth = LayoutGroup(group, outgoing, incoming, indegree, currentGroupStartX, startY, horizontalSpacing, verticalSpacing);
            currentGroupStartX += groupWidth + groupSpacing;
        }
    }

    public IReadOnlyDictionary<string, Rect> CaptureNodePositions()
    {
        var positions = new Dictionary<string, Rect>(_nodeLookup.Count);
        foreach (var pair in _nodeLookup)
        {
            positions[pair.Key] = pair.Value.GetPosition();
        }

        return positions;
    }

    public void ApplyNodePositions(IReadOnlyDictionary<string, Rect> positions)
    {
        if (positions == null)
        {
            return;
        }

        foreach (var pair in positions)
        {
            if (!_nodeLookup.TryGetValue(pair.Key, out var node))
            {
                continue;
            }

            var current = node.GetPosition();
            var saved = pair.Value;
            current.x = saved.x;
            current.y = saved.y;
            node.SetPosition(current);
        }
    }

    private List<List<string>> BuildConnectedGroups(IReadOnlyDictionary<string, HashSet<string>> outgoing, IReadOnlyDictionary<string, HashSet<string>> incoming)
    {
        var groups = new List<List<string>>();
        var visited = new HashSet<string>();

        foreach (var rootNodeId in _nodeLookup.Keys)
        {
            if (!visited.Add(rootNodeId))
            {
                continue;
            }

            var group = new List<string>();
            var stack = new Stack<string>();
            stack.Push(rootNodeId);

            while (stack.Count > 0)
            {
                var nodeId = stack.Pop();
                group.Add(nodeId);

                if (outgoing.TryGetValue(nodeId, out var outgoingNeighbors))
                {
                    foreach (var neighbor in outgoingNeighbors)
                    {
                        if (visited.Add(neighbor))
                        {
                            stack.Push(neighbor);
                        }
                    }
                }

                if (!incoming.TryGetValue(nodeId, out var incomingNeighbors))
                {
                    continue;
                }

                foreach (var neighbor in incomingNeighbors)
                {
                    if (visited.Add(neighbor))
                    {
                        stack.Push(neighbor);
                    }
                }
            }

            groups.Add(group);
        }

        return groups;
    }

    private float LayoutGroup(
        IReadOnlyList<string> groupNodeIds,
        IReadOnlyDictionary<string, HashSet<string>> outgoing,
        IReadOnlyDictionary<string, HashSet<string>> incoming,
        IReadOnlyDictionary<string, int> globalIndegree,
        float startX,
        float startY,
        float horizontalSpacing,
        float verticalSpacing)
    {
        var groupSet = new HashSet<string>(groupNodeIds);
        var indegree = new Dictionary<string, int>(groupSet.Count);
        foreach (var nodeId in groupSet)
        {
            indegree[nodeId] = 0;
        }

        foreach (var nodeId in groupSet)
        {
            if (!outgoing.TryGetValue(nodeId, out var nextNodes))
            {
                continue;
            }

            foreach (var next in nextNodes)
            {
                if (!groupSet.Contains(next))
                {
                    continue;
                }

                indegree[next] += 1;
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

        var levelByNode = new Dictionary<string, int>(groupSet.Count);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var currentLevel = levelByNode.TryGetValue(current, out var knownLevel) ? knownLevel : 0;

            foreach (var next in outgoing[current])
            {
                if (!groupSet.Contains(next))
                {
                    continue;
                }

                var nextLevel = currentLevel + 1;
                if (!levelByNode.TryGetValue(next, out var existingLevel) || nextLevel > existingLevel)
                {
                    levelByNode[next] = nextLevel;
                }

                indegree[next] -= 1;
                if (indegree[next] == 0)
                {
                    queue.Enqueue(next);
                }
            }
        }

        var fallbackLevel = 0;
        foreach (var nodeId in groupNodeIds)
        {
            if (levelByNode.ContainsKey(nodeId))
            {
                continue;
            }

            var preferredLevel = globalIndegree.TryGetValue(nodeId, out var globalValue) ? globalValue : fallbackLevel;
            levelByNode[nodeId] = Mathf.Max(0, preferredLevel);
            fallbackLevel += 1;
        }

        var levelBuckets = new Dictionary<int, List<string>>();
        foreach (var nodeId in groupNodeIds)
        {
            var level = levelByNode[nodeId];
            if (!levelBuckets.TryGetValue(level, out var bucket))
            {
                bucket = new List<string>();
                levelBuckets[level] = bucket;
            }

            bucket.Add(nodeId);
        }

        var orderedLevels = new List<int>(levelBuckets.Keys);
        orderedLevels.Sort();

        foreach (var level in orderedLevels)
        {
            levelBuckets[level].Sort((a, b) => string.Compare(_nodeLookup[a].title, _nodeLookup[b].title, StringComparison.OrdinalIgnoreCase));
        }

        const int relaxationPasses = 4;
        for (var pass = 0; pass < relaxationPasses; pass++)
        {
            foreach (var level in orderedLevels)
            {
                if (level == orderedLevels[0])
                {
                    continue;
                }

                SortLevelByNeighborBarycenter(levelBuckets[level], incoming, levelBuckets, levelByNode, true);
            }

            for (var index = orderedLevels.Count - 1; index >= 0; index--)
            {
                var level = orderedLevels[index];
                if (level == orderedLevels[orderedLevels.Count - 1])
                {
                    continue;
                }

                SortLevelByNeighborBarycenter(levelBuckets[level], outgoing, levelBuckets, levelByNode, false);
            }
        }

        var maxColumnWidth = 0f;
        foreach (var level in orderedLevels)
        {
            var nodeIds = levelBuckets[level];
            var y = startY;
            foreach (var nodeId in nodeIds)
            {
                var node = _nodeLookup[nodeId];
                var rect = node.GetPosition();
                rect.x = startX + (level * horizontalSpacing);
                rect.y = y;
                node.SetPosition(rect);
                y += rect.height + verticalSpacing;
                maxColumnWidth = Mathf.Max(maxColumnWidth, rect.width);
            }
        }

        return (orderedLevels.Count * horizontalSpacing) + maxColumnWidth;
    }


    public void FocusNode(DependencyNode node)
    {
        if (node == null || !_nodeLookup.TryGetValue(node.GUID, out var visualNode))
        {
            return;
        }

        ClearSelection();
        AddToSelection(visualNode);
        FrameSelection();

        if (node.Owner is UnityEngine.Object unityObject)
        {
            Selection.activeObject = unityObject;
            EditorGUIUtility.PingObject(unityObject);
        }

        NotifySelection();
    }

    private void SortLevelByNeighborBarycenter(
        List<string> nodeIds,
        IReadOnlyDictionary<string, HashSet<string>> neighborLookup,
        IReadOnlyDictionary<int, List<string>> levelBuckets,
        Dictionary<string, int> levelByNode,
        bool usePreviousLevel)
    {
        nodeIds.Sort((left, right) =>
        {
            var leftCenter = NeighborCenter(left);
            var rightCenter = NeighborCenter(right);
            var byCenter = leftCenter.CompareTo(rightCenter);
            if (byCenter != 0)
            {
                return byCenter;
            }

            return string.Compare(_nodeLookup[left].title, _nodeLookup[right].title, StringComparison.OrdinalIgnoreCase);
        });

        float NeighborCenter(string nodeId)
        {
            if (!neighborLookup.TryGetValue(nodeId, out var neighbors) || neighbors.Count == 0)
            {
                return float.MaxValue;
            }

            var total = 0f;
            var count = 0;
            foreach (var neighbor in neighbors)
            {
                if (!levelByNode.TryGetValue(neighbor, out var neighborLevel))
                {
                    continue;
                }

                var sameDirection = usePreviousLevel ? neighborLevel < levelByNode[nodeId] : neighborLevel > levelByNode[nodeId];
                if (!sameDirection || !levelBuckets.TryGetValue(neighborLevel, out var neighborBucket))
                {
                    continue;
                }

                var position = neighborBucket.IndexOf(neighbor);
                if (position < 0)
                {
                    continue;
                }

                total += position;
                count++;
            }

            return count == 0 ? float.MaxValue : total / count;
        }
    }

    private void CreateVisualNode(DependencyNode node)
    {
        if (_nodeLookup.ContainsKey(node.GUID))
        {
            return;
        }

        var visualNode = new Node
        {
            title = node.DisplayName,
            userData = node,
        };

        var nodeColor = NodeColor(node);
        var nodeTextColor = Color.white;
        ApplyNodeColorStyles(visualNode, nodeColor, nodeTextColor);
        visualNode.titleContainer.style.unityFontStyleAndWeight = FontStyle.Bold;
        visualNode.titleContainer.style.fontSize = NodeFontSize;
        visualNode.mainContainer.style.fontSize = NodeFontSize;
        visualNode.inputContainer.style.fontSize = NodeFontSize;
        visualNode.outputContainer.style.fontSize = NodeFontSize;

        var fontScale = Mathf.Clamp(NodeFontSize / DefaultNodeFontSize, 0.8f, 2.5f);
        var nodeWidth = DefaultNodeWidth * fontScale;
        var nodeHeight = DefaultNodeHeight * fontScale;

        ApplyNodeIcon(visualNode, node, NodeIconSize);

        visualNode.SetPosition(new Rect(20f, 20f, nodeWidth, nodeHeight));
        visualNode.RefreshExpandedState();
        visualNode.RefreshPorts();

        AddElement(visualNode);
        _nodeLookup[node.GUID] = visualNode;
        _outputPortsByNodeAndField[node.GUID] = new Dictionary<string, Port>();
        _inputPortsByNodeAndField[node.GUID] = new Dictionary<string, Port>();
    }

    private static void ApplyNodeColorStyles(Node visualNode, Color nodeColor, Color nodeTextColor)
    {
        visualNode.titleContainer.style.backgroundColor = nodeColor;
        visualNode.titleContainer.style.color = nodeTextColor;
        visualNode.mainContainer.style.backgroundColor = new Color(nodeColor.r, nodeColor.g, nodeColor.b, 0.2f);
        visualNode.mainContainer.style.color = nodeTextColor;
        visualNode.inputContainer.style.color = nodeTextColor;
        visualNode.outputContainer.style.color = nodeTextColor;
    }


    private static void ApplyNodeIcon(Node visualNode, DependencyNode node, float iconSize)
    {
        if (visualNode == null || node?.Owner == null)
        {
            return;
        }

        var ownerType = node.Owner.GetType();
        Texture icon = null;

        if (node.Owner is UnityEngine.Object unityObject)
        {
            icon = EditorGUIUtility.ObjectContent(unityObject, ownerType)?.image
                   ?? EditorGUIUtility.ObjectContent(null, ownerType)?.image;
        }

        if (node.Owner is not UnityEngine.Object)
        {
            CreateManagedTypeBadge(visualNode, iconSize);
            return;
        }

        icon ??= EditorGUIUtility.IconContent("cs Script Icon")?.image;

        if (icon == null)
        {
            return;
        }

        var image = new Image
        {
            image = icon,
            scaleMode = ScaleMode.ScaleToFit,
        };
        var clampedIconSize = Mathf.Clamp(iconSize, 8f, 48f);
        image.style.width = clampedIconSize;
        image.style.height = clampedIconSize;
        image.style.marginRight = 4f;

        visualNode.titleContainer.Insert(0, image);
    }

    private static void CreateManagedTypeBadge(Node visualNode, float iconSize)
    {
        var badge = new Label("C#");
        var clampedIconSize = Mathf.Clamp(iconSize, 8f, 48f);
        badge.style.minWidth = clampedIconSize * 2.6f;
        badge.style.height = clampedIconSize;
        badge.style.marginRight = 4f;
        badge.style.unityTextAlign = TextAnchor.MiddleCenter;
        badge.style.fontSize = Mathf.Max(8f, clampedIconSize * 0.55f);
        badge.style.paddingLeft = 3f;
        badge.style.paddingRight = 3f;
        badge.style.color = Color.white;
        badge.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.9f);
        badge.style.borderTopLeftRadius = 3f;
        badge.style.borderTopRightRadius = 3f;
        badge.style.borderBottomLeftRadius = 3f;
        badge.style.borderBottomRightRadius = 3f;
        visualNode.titleContainer.Insert(0, badge);
    }

    private Port GetOrCreatePort(string nodeGuid, string fieldName, Node node, Direction direction, bool hasValue, string valueSummary)
    {
        var safeFieldName = string.IsNullOrWhiteSpace(fieldName) ? "unknown" : fieldName;
        var lookup = direction == Direction.Output ? _outputPortsByNodeAndField : _inputPortsByNodeAndField;
        var container = direction == Direction.Output ? node.outputContainer : node.inputContainer;

        if (!lookup.TryGetValue(nodeGuid, out var ports))
        {
            ports = new Dictionary<string, Port>();
            lookup[nodeGuid] = ports;
        }

        if (ports.TryGetValue(safeFieldName, out var existingPort))
        {
            return existingPort;
        }

        var newPort = node.InstantiatePort(Orientation.Horizontal, direction, Port.Capacity.Multi, typeof(UnityEngine.Object));
        var suffix = string.IsNullOrWhiteSpace(valueSummary)
            ? string.Empty
            : $" ({valueSummary})";

        if (direction == Direction.Output)
        {
            var emptyMarker = hasValue ? string.Empty : " [empty]";
            newPort.portName = $"OUT: {safeFieldName}{emptyMarker}{suffix}";
            newPort.portColor = hasValue ? new Color(0.3f, 0.8f, 0.4f) : new Color(0.75f, 0.45f, 0.2f);
        }
        else
        {
            newPort.portName = $"IN: {safeFieldName}{suffix}";
        }

        container.Add(newPort);
        node.RefreshPorts();
        node.RefreshExpandedState();
        ports[safeFieldName] = newPort;
        return newPort;
    }

    private Color NodeColor(DependencyNode node)
    {
        if (node != null && !string.IsNullOrWhiteSpace(node.GUID) && _nodeGuidColors.TryGetValue(node.GUID, out var nodeColor))
        {
            return nodeColor;
        }

        var typeKey = GetNodeTypeKey(node);
        if (!string.IsNullOrWhiteSpace(typeKey) && _nodeTypeColors.TryGetValue(typeKey, out var typeColor))
        {
            return typeColor;
        }

        if (string.IsNullOrEmpty(typeKey))
        {
            typeKey = node?.DisplayName;
        }

        return GenerateColorFromKey(typeKey);
    }

    public static string GetNodeTypeKey(DependencyNode node)
    {
        return node?.Owner?.GetType().FullName;
    }

    public static Color GetDefaultNodeColor(DependencyNode node)
    {
        var typeKey = GetNodeTypeKey(node);
        if (string.IsNullOrEmpty(typeKey))
        {
            typeKey = node?.DisplayName;
        }

        return GenerateColorFromKey(typeKey);
    }

    private static Color GenerateColorFromKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return Color.gray;
        }

        unchecked
        {
            var hash = 17;
            foreach (var character in key)
            {
                hash = (hash * 31) + character;
            }

            var normalizedHue = (hash & 0x7fffffff) / (float)int.MaxValue;
            return Color.HSVToRGB(normalizedHue, 0.7f, 0.5f);
        }
    }

    private static Color EdgeColor(DependencyType type, bool isBroken)
    {
        if (isBroken)
        {
            return Color.red;
        }

        return type switch
        {
            DependencyType.UnityEvent => new Color(0.2f, 0.5f, 1f),
            DependencyType.SerializedUnityRef => new Color(1f, 0.8f, 0.2f),
            DependencyType.SerializeReferenceManaged => new Color(0.3f, 0.9f, 0.4f),
            DependencyType.OdinSerializedRef => new Color(0.7f, 0.4f, 1f),
            _ => Color.white,
        };
    }

    private void NotifySelection()
    {
        var selectedNode = selection.Find(item => item is Node) as Node;
        var selectedEdge = selection.Find(item => item is Edge) as Edge;

        OnNodeSelectionChanged?.Invoke(selectedNode?.userData as DependencyNode);
        OnEdgeSelectionChanged?.Invoke(selectedEdge?.userData as DependencyEdge);
    }
}
