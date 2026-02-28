#if HAS_NODE_GRAPH_PROCESSOR
using GraphProcessor;
using UnityEngine;

namespace RonJames.DependencyGraphTool.NodeGraphProcessorIntegration
{
    internal enum DependencyGraphNodeKind
    {
        MonoBehaviour,
        ScriptableObject,
        ManagedObject,
    }

    [System.Serializable]
    internal abstract class DependencyGraphBaseNode : BaseNode
    {
        [SerializeField]
        private string _dependencyGuid;

        [SerializeField]
        private string _displayName;

        [SerializeField]
        private string _typeName;

        [SerializeField]
        private string _unityObjectPath;

        public string DependencyGuid => _dependencyGuid;
        public string DisplayName => _displayName;
        public string TypeName => _typeName;
        public string UnityObjectPath => _unityObjectPath;

        public abstract DependencyGraphNodeKind Kind { get; }

        public void SetGraphData(string dependencyGuid, string displayName, string typeName, string unityObjectPath)
        {
            _dependencyGuid = dependencyGuid ?? string.Empty;
            _displayName = displayName ?? string.Empty;
            _typeName = typeName ?? string.Empty;
            _unityObjectPath = unityObjectPath ?? string.Empty;
        }
    }

    [System.Serializable]
    internal sealed class MonoBehaviourDependencyNode : DependencyGraphBaseNode
    {
        public override DependencyGraphNodeKind Kind => DependencyGraphNodeKind.MonoBehaviour;
    }

    [System.Serializable]
    internal sealed class ScriptableObjectDependencyNode : DependencyGraphBaseNode
    {
        public override DependencyGraphNodeKind Kind => DependencyGraphNodeKind.ScriptableObject;
    }

    [System.Serializable]
    internal sealed class ManagedObjectDependencyNode : DependencyGraphBaseNode
    {
        public override DependencyGraphNodeKind Kind => DependencyGraphNodeKind.ManagedObject;
    }
}
#endif
