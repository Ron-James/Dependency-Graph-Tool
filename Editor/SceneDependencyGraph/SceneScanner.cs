using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;


namespace RonJames.DependencyGraphTool
{
    public sealed class SceneScanner
    {
        private readonly UnityEventScanner _unityEventScanner = new();
        private readonly SerializedFieldScanner _serializedFieldScanner = new();
        private readonly ManagedObjectScanner _managedObjectScanner = new();

        public DependencyModel ScanScene()
        {
            var model = new DependencyModel();
            var registry = new DependencyNodeRegistry(model.Nodes);
            var components = EnumerateSceneComponents();

            foreach (var component in components)
            {
                if (component == null)
                {
                    continue;
                }

                _unityEventScanner.Scan(component, model.Edges, registry);
                _serializedFieldScanner.Scan(component, model.Edges, registry, _managedObjectScanner);
            }

            return model;
        }

        private static IEnumerable<Component> EnumerateSceneComponents()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                return Enumerable.Empty<Component>();
            }

            return activeScene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Component>(true));
        }
    }

    internal static class DependencyFieldSlotUtility
    {
        public static void Upsert(
            DependencyNode node,
            string name,
            bool isOutput,
            bool hasValue,
            bool isUnityEvent,
            string valueSummary)
        {
            if (node == null || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            var slot = node.FieldSlots.Find(existing => string.Equals(existing.Name, name, StringComparison.Ordinal));
            if (slot == null)
            {
                slot = new DependencyFieldSlot { Name = name };
                node.FieldSlots.Add(slot);
            }

            slot.IsOutput |= isOutput;
            slot.HasValue |= hasValue;
            slot.IsUnityEvent |= isUnityEvent;
            if (!string.IsNullOrWhiteSpace(valueSummary))
            {
                slot.ValueSummary = valueSummary;
            }
        }
    }

    internal sealed class UnityEventScanner
    {
        private static string SafeMethodName(string methodName)
        {
            return string.IsNullOrWhiteSpace(methodName) ? "<missing method>" : methodName;
        }

        private sealed class PersistentUnityEventListenerKey
        {
            private readonly int _ownerId;
            private readonly string _fieldName;
            private readonly int _listenerIndex;

            public PersistentUnityEventListenerKey(Component owner, FieldInfo field, int listenerIndex)
            {
                _ownerId = owner.GetInstanceID();
                _fieldName = field.Name;
                _listenerIndex = listenerIndex;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = 17;
                    hash = (hash * 31) + _ownerId;
                    hash = (hash * 31) + _listenerIndex;
                    hash = (hash * 31) + (_fieldName?.GetHashCode() ?? 0);
                    return hash;
                }
            }

            public override bool Equals(object obj)
            {
                var other = obj as PersistentUnityEventListenerKey;
                if (other == null)
                {
                    return false;
                }

                return _ownerId == other._ownerId
                       && _listenerIndex == other._listenerIndex
                       && string.Equals(_fieldName, other._fieldName, StringComparison.Ordinal);
            }
        }

        public void Scan(Component component, List<DependencyEdge> edges, DependencyNodeRegistry registry)
        {
            var fields = TypeUtility.GetAllInstanceFields(component.GetType());
            var fromNode = registry.GetOrCreateNode(component);

            foreach (var field in fields)
            {
                if (!typeof(UnityEventBase).IsAssignableFrom(field.FieldType))
                {
                    continue;
                }

                var eventValue = field.GetValue(component) as UnityEventBase;
                if (eventValue == null)
                {
                    continue;
                }

                var listenerCount = eventValue.GetPersistentEventCount();
                DependencyFieldSlotUtility.Upsert(
                    fromNode,
                    field.Name,
                    isOutput: true,
                    hasValue: listenerCount > 0,
                    isUnityEvent: true,
                    valueSummary: listenerCount == 0 ? "No listeners" : $"{listenerCount} listener(s)");

                for (var listenerIndex = 0; listenerIndex < listenerCount; listenerIndex++)
                {
                    var target = eventValue.GetPersistentTarget(listenerIndex);
                    var methodName = eventValue.GetPersistentMethodName(listenerIndex);
                    var targetLabel = target != null
                        ? $"{target.name} ({TypeUtility.GetFriendlyTypeName(target.GetType())})"
                        : "Missing Target";

                    var listenerNode = registry.GetOrCreateNode(
                        new PersistentUnityEventListenerKey(component, field, listenerIndex),
                        $"{field.Name}[{listenerIndex}] → {targetLabel}.{SafeMethodName(methodName)}");

                    var targetNode = target != null
                        ? registry.GetOrCreateNode(target)
                        : registry.GetOrCreateNode($"Missing Target ({SafeMethodName(methodName)})", "Missing Listener");

                    var listenerLabel = string.IsNullOrWhiteSpace(methodName)
                        ? $"{field.Name}[{listenerIndex}]"
                        : $"{field.Name}[{listenerIndex}].{methodName}";

                    edges.Add(new DependencyEdge
                    {
                        From = fromNode,
                        To = listenerNode,
                        FieldName = listenerLabel,
                        Type = DependencyType.UnityEvent,
                        IsBroken = target == null || string.IsNullOrEmpty(methodName),
                        Details = $"Persistent listener on {field.Name}",
                        ActionContext = new DependencyActionContext
                        {
                            OwnerObject = component,
                            FieldInfo = field,
                            PersistentListenerIndex = listenerIndex,
                            UnityReferenceValue = target,
                        },
                    });

                    edges.Add(new DependencyEdge
                    {
                        From = listenerNode,
                        To = targetNode,
                        FieldName = $"listener[{listenerIndex}]",
                        Type = DependencyType.UnityEvent,
                        IsBroken = target == null || string.IsNullOrEmpty(methodName),
                        Details = string.IsNullOrEmpty(methodName) ? "Missing method" : methodName,
                    });
                }
            }
        }
    }

    internal sealed class SerializedFieldScanner
    {
        public void Scan(
            Component component,
            List<DependencyEdge> edges,
            DependencyNodeRegistry registry,
            ManagedObjectScanner managedObjectScanner)
        {
            var ownerNode = registry.GetOrCreateNode(component);
            foreach (var field in TypeUtility.GetAllInstanceFields(component.GetType()))
            {
                if (!TypeUtility.IsSerializedField(field))
                {
                    continue;
                }

                var value = field.GetValue(component);
                DependencyFieldSlotUtility.Upsert(
                    ownerNode,
                    field.Name,
                    isOutput: true,
                    hasValue: value != null,
                    isUnityEvent: typeof(UnityEventBase).IsAssignableFrom(field.FieldType),
                    valueSummary: DescribeSerializedValue(value));

                if (value == null)
                {
                    continue;
                }

                if (value is UnityEngine.Object unityRef)
                {
                    edges.Add(new DependencyEdge
                    {
                        From = ownerNode,
                        To = registry.GetOrCreateNode(unityRef),
                        FieldName = field.Name,
                        Type = TypeUtility.HasOdinSerializeAttribute(field) ? DependencyType.OdinSerializedRef : DependencyType.SerializedUnityRef,
                        ActionContext = new DependencyActionContext
                        {
                            OwnerObject = component,
                            FieldInfo = field,
                            UnityReferenceValue = unityRef,
                        },
                    });
                    continue;
                }

                if (value is UnityEventBase)
                {
                    // UnityEvent internals create noisy implementation-detail nodes.
                    // Dedicated UnityEventScanner already captures listener dependencies.
                    continue;
                }

                managedObjectScanner.ScanManagedField(component, ownerNode, field, value, edges, registry);
            }
        }

        private static string DescribeSerializedValue(object value)
        {
            if (value == null)
            {
                return "Empty";
            }

            if (value is UnityEngine.Object unityObject)
            {
                return unityObject != null
                    ? $"{unityObject.name} ({TypeUtility.GetFriendlyTypeName(unityObject.GetType())})"
                    : "Missing Unity Object";
            }

            if (value is UnityEventBase unityEvent)
            {
                return $"{unityEvent.GetPersistentEventCount()} listener(s)";
            }

            return TypeUtility.GetFriendlyTypeName(value.GetType());
        }
    }

    internal sealed class ManagedObjectScanner
    {
        public void ScanManagedField(
            Component owner,
            DependencyNode ownerNode,
            FieldInfo field,
            object rootValue,
            List<DependencyEdge> edges,
            DependencyNodeRegistry registry)
        {
            var isManagedRoot = TypeUtility.HasSerializeReferenceAttribute(field) || TypeUtility.HasOdinSerializeAttribute(field);
            if (!isManagedRoot || TypeUtility.IsTerminalType(rootValue.GetType()))
            {
                return;
            }

            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            ScanObjectRecursive(owner, ownerNode, field, field.Name, rootValue, edges, registry, visited, 0);
        }

        private void ScanObjectRecursive(
            Component owner,
            DependencyNode fromNode,
            FieldInfo rootField,
            string memberPath,
            object current,
            List<DependencyEdge> edges,
            DependencyNodeRegistry registry,
            HashSet<object> visited,
            int depth)
        {
            if (current == null || depth > 16)
            {
                return;
            }

            if (current is UnityEngine.Object unityObject)
            {
                edges.Add(new DependencyEdge
                {
                    From = fromNode,
                    To = registry.GetOrCreateNode(unityObject),
                    FieldName = memberPath,
                    Type = DependencyType.SerializeReferenceManaged,
                    ActionContext = new DependencyActionContext
                    {
                        OwnerObject = owner,
                        FieldInfo = rootField,
                        UnityReferenceValue = unityObject,
                    },
                });
                return;
            }

            if (TypeUtility.ShouldSkipManagedGraphNode(current.GetType()))
            {
                return;
            }

            if (TypeUtility.IsTerminalType(current.GetType()) || !visited.Add(current))
            {
                return;
            }

            if (current is IEnumerable enumerable && current is not string)
            {
                var itemIndex = 0;
                foreach (var item in enumerable)
                {
                    var itemPath = GraphNamingUtility.BuildCollectionItemLabel(memberPath, itemIndex, item);
                    ScanObjectRecursive(owner, fromNode, rootField, itemPath, item, edges, registry, visited, depth + 1);
                    itemIndex++;
                }
                return;
            }

            var currentNode = registry.GetOrCreateNode(current, TypeUtility.GetFriendlyTypeName(current.GetType()));
            if (currentNode != fromNode)
            {
                edges.Add(new DependencyEdge
                {
                    From = fromNode,
                    To = currentNode,
                    FieldName = memberPath,
                    Type = DependencyType.SerializeReferenceManaged,
                    Details = current.GetType().Name,
                });
            }

            foreach (var field in TypeUtility.GetAllInstanceFields(current.GetType()))
            {
                if (!TypeUtility.CanParticipateInManagedScan(field))
                {
                    continue;
                }

                var childPath = $"{memberPath}.{field.Name}";
                var childValue = field.GetValue(current);
                if (childValue is UnityEventBase unityEvent)
                {
                    ScanManagedUnityEvent(currentNode, current, field, childPath, unityEvent, edges, registry);
                    continue;
                }

                ScanObjectRecursive(owner, currentNode, rootField, childPath, childValue, edges, registry, visited, depth + 1);
            }
        }

        private static void ScanManagedUnityEvent(
            DependencyNode fromNode,
            object owner,
            FieldInfo eventField,
            string memberPath,
            UnityEventBase eventValue,
            List<DependencyEdge> edges,
            DependencyNodeRegistry registry)
        {
            var listenerCount = eventValue.GetPersistentEventCount();
            for (var listenerIndex = 0; listenerIndex < listenerCount; listenerIndex++)
            {
                var methodName = eventValue.GetPersistentMethodName(listenerIndex);
                var target = eventValue.GetPersistentTarget(listenerIndex);

                var listenerLabel = string.IsNullOrWhiteSpace(methodName)
                    ? memberPath
                    : $"{memberPath}.{methodName}";

                var targetNode = target != null
                    ? registry.GetOrCreateNode(target)
                    : registry.GetOrCreateNode($"Missing Target ({SafeMethodName(methodName)})", "Missing Listener");

                edges.Add(new DependencyEdge
                {
                    From = fromNode,
                    To = targetNode,
                    FieldName = listenerLabel,
                    Type = DependencyType.UnityEvent,
                    IsBroken = target == null || string.IsNullOrWhiteSpace(methodName),
                    Details = $"Persistent listener on {memberPath}",
                    ActionContext = new DependencyActionContext
                    {
                        OwnerObject = owner as Object,
                        FieldInfo = eventField,
                        PersistentListenerIndex = listenerIndex,
                        UnityReferenceValue = target,
                    },
                });
            }
        }

        private static string SafeMethodName(string methodName)
        {
            return string.IsNullOrWhiteSpace(methodName) ? "<missing method>" : methodName;
        }
    }
}
