using GraphProcessor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RonJames.DependencyGraphTool.NodeGraphProcessorIntegration
{
    [NodeCustomEditor(typeof(DependencyGraphBaseNode))]
    internal sealed class DependencyGraphNodeView : BaseNodeView
    {
        public override void Enable()
        {
            base.Enable();
            RegisterCallback<MouseDownEvent>(OnMouseDown);
        }

        public override void Disable()
        {
            UnregisterCallback<MouseDownEvent>(OnMouseDown);
            base.Disable();
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0)
            {
                return;
            }

            if (nodeTarget is not DependencyGraphBaseNode dependencyNode || dependencyNode.UnityObjectInstanceId == 0)
            {
                return;
            }

            var unityObject = EditorUtility.InstanceIDToObject(dependencyNode.UnityObjectInstanceId);
            if (unityObject == null)
            {
                return;
            }

            Selection.activeObject = unityObject;
            EditorGUIUtility.PingObject(unityObject);
        }
    }
}
