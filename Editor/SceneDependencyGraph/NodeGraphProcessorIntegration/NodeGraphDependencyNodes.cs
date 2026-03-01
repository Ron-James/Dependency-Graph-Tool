
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
    public abstract class DependencyGraphBaseNode : BaseNode
    {
        [Input(name = "IN", allowMultiple = true)]
        [SerializeField]
        private object _input;

        [Output(name = "OUT", allowMultiple = true)]
        [SerializeField]
        private object _output;

        [SerializeField]
        private string _dependencyGuid;

        [SerializeField]
        private string _displayName;

        [SerializeField]
        private string _typeName;

        [SerializeField]
        private string _unityObjectPath;

        [SerializeField]
        private string _details;

        public string DependencyGuid => _dependencyGuid;
        public string DisplayName => _displayName;
        public string TypeName => _typeName;
        public string UnityObjectPath => _unityObjectPath;
        public string Details => _details;

        public abstract DependencyGraphNodeKind Kind { get; }

        public override string name => _displayName;

        public void SetGraphData(string dependencyGuid, string displayName, string typeName, string unityObjectPath, string details)
        {
            _dependencyGuid = dependencyGuid ?? string.Empty;
            _displayName = displayName ?? string.Empty;
            _typeName = typeName ?? string.Empty;
            _unityObjectPath = unityObjectPath ?? string.Empty;
            _details = details ?? string.Empty;
        }
    }

    [System.Serializable]
    public sealed class MonoBehaviourDependencyNode : DependencyGraphBaseNode
    {
        public override DependencyGraphNodeKind Kind => DependencyGraphNodeKind.MonoBehaviour;
    }

    [System.Serializable]
    public sealed class ScriptableObjectDependencyNode : DependencyGraphBaseNode
    {
        public override DependencyGraphNodeKind Kind => DependencyGraphNodeKind.ScriptableObject;
    }

    [System.Serializable]
    public sealed class ManagedObjectDependencyNode : DependencyGraphBaseNode
    {
        public override DependencyGraphNodeKind Kind => DependencyGraphNodeKind.ManagedObject;
    }
}
