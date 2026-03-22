using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
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
            var scannedUnityObjects = new HashSet<int>();
            var scannedPrefabRoots = new HashSet<int>();
            var pendingUnityObjects = new Queue<UnityEngine.Object>();

            foreach (var component in EnumerateSceneComponents())
            {
                ScanUnityObject(component, model, registry, scannedUnityObjects, pendingUnityObjects);
            }

            EnqueueReferencedAssets(model, pendingUnityObjects, scannedUnityObjects, scannedPrefabRoots);

            while (pendingUnityObjects.Count > 0)
            {
                var pendingObject = pendingUnityObjects.Dequeue();
                if (pendingObject == null)
                {
                    continue;
                }

                if (pendingObject is ScriptableObject scriptableObject)
                {
                    ScanUnityObject(scriptableObject, model, registry, scannedUnityObjects, pendingUnityObjects);
                    EnqueueReferencedAssets(model, pendingUnityObjects, scannedUnityObjects, scannedPrefabRoots);
                    continue;
                }

                var prefabRoot = GetPrefabAssetRoot(pendingObject);
                if (prefabRoot == null)
                {
                    continue;
                }

                var prefabRootId = prefabRoot.GetInstanceID();
                if (!scannedPrefabRoots.Add(prefabRootId))
                {
                    continue;
                }

                ScanPrefabAsset(prefabRoot, model, registry, scannedUnityObjects, pendingUnityObjects);
                EnqueueReferencedAssets(model, pendingUnityObjects, scannedUnityObjects, scannedPrefabRoots);
            }

            return model;
        }

        private void ScanUnityObject(
            UnityEngine.Object unityObject,
            DependencyModel model,
            DependencyNodeRegistry registry,
            HashSet<int> scannedUnityObjects,
            Queue<UnityEngine.Object> pendingUnityObjects)
        {
            if (unityObject == null)
            {
                return;
            }

            var instanceId = unityObject.GetInstanceID();
            if (!scannedUnityObjects.Add(instanceId))
            {
                return;
            }

            _unityEventScanner.Scan(unityObject, model.Edges, registry);
            _serializedFieldScanner.Scan(unityObject, model.Edges, registry, _managedObjectScanner);
            pendingUnityObjects.Enqueue(unityObject);
        }

        private void ScanPrefabAsset(
            GameObject prefabRoot,
            DependencyModel model,
            DependencyNodeRegistry registry,
            HashSet<int> scannedUnityObjects,
            Queue<UnityEngine.Object> pendingUnityObjects)
        {
            if (prefabRoot == null)
            {
                return;
            }

            var prefabNode = registry.GetOrCreateNode(prefabRoot);
            var prefabComponents = prefabRoot.GetComponentsInChildren<Component>(true);
            foreach (var component in prefabComponents)
            {
                if (component == null)
                {
                    continue;
                }

                var componentNode = registry.GetOrCreateNode(component);
                var componentLabel = $"Component/{component.gameObject.name}/{TypeUtility.GetFriendlyTypeName(component.GetType())}";
                DependencyFieldSlotUtility.Upsert(
                    prefabNode,
                    componentLabel,
                    isOutput: true,
                    hasValue: true,
                    isUnityEvent: false,
                    valueSummary: component.gameObject == prefabRoot
                        ? component.GetType().Name
                        : $"{component.gameObject.name} ({component.GetType().Name})",
                    valueType: component.GetType(),
                    unityReferenceValue: component);

                model.Edges.Add(new DependencyEdge
                {
                    From = prefabNode,
                    To = componentNode,
                    FieldName = componentLabel,
                    Type = DependencyType.SerializedUnityRef,
                    Details = "Prefab component",
                    ActionContext = new DependencyActionContext
                    {
                        OwnerObject = prefabRoot,
                        UnityReferenceValue = component,
                    },
                });

                ScanUnityObject(component, model, registry, scannedUnityObjects, pendingUnityObjects);
            }
        }

        private static void EnqueueReferencedAssets(
            DependencyModel model,
            Queue<UnityEngine.Object> pendingUnityObjects,
            HashSet<int> scannedUnityObjects,
            HashSet<int> scannedPrefabRoots)
        {
            if (model?.Nodes == null)
            {
                return;
            }

            foreach (var node in model.Nodes)
            {
                if (node?.Owner is not UnityEngine.Object unityObject || unityObject == null)
                {
                    continue;
                }

                if (unityObject is ScriptableObject scriptableObject &&
                    EditorUtility.IsPersistent(scriptableObject) &&
                    !scannedUnityObjects.Contains(scriptableObject.GetInstanceID()))
                {
                    pendingUnityObjects.Enqueue(scriptableObject);
                    continue;
                }

                var prefabRoot = GetPrefabAssetRoot(unityObject);
                if (prefabRoot != null && !scannedPrefabRoots.Contains(prefabRoot.GetInstanceID()))
                {
                    pendingUnityObjects.Enqueue(prefabRoot);
                }
            }
        }

        private static GameObject GetPrefabAssetRoot(UnityEngine.Object unityObject)
        {
            if (unityObject == null || !EditorUtility.IsPersistent(unityObject))
            {
                return null;
            }

            var sourceGameObject = unityObject switch
            {
                GameObject gameObject => gameObject,
                Component component => component.gameObject,
                _ => null,
            };

            if (sourceGameObject == null)
            {
                return null;
            }

            var assetPath = AssetDatabase.GetAssetPath(sourceGameObject);
            if (string.IsNullOrWhiteSpace(assetPath) ||
                PrefabUtility.GetPrefabAssetType(sourceGameObject) == PrefabAssetType.NotAPrefab)
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
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
            string valueSummary,
            Type valueType = null,
            UnityEngine.Object unityReferenceValue = null)
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

            if (valueType != null)
            {
                slot.ValueType = valueType;
            }

            if (unityReferenceValue != null)
            {
                slot.UnityReferenceValue = unityReferenceValue;
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

            public PersistentUnityEventListenerKey(UnityEngine.Object owner, FieldInfo field, int listenerIndex)
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

        public void Scan(UnityEngine.Object ownerObject, List<DependencyEdge> edges, DependencyNodeRegistry registry)
        {
            if (ownerObject == null)
            {
                return;
            }

            var fields = TypeUtility.GetAllInstanceFields(ownerObject.GetType());
            var fromNode = registry.GetOrCreateNode(ownerObject);

            foreach (var field in fields)
            {
                if (!typeof(UnityEventBase).IsAssignableFrom(field.FieldType))
                {
                    continue;
                }

                var eventValue = field.GetValue(ownerObject) as UnityEventBase;
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
                    valueSummary: listenerCount == 0 ? "No listeners" : $"{listenerCount} listener(s)",
                    valueType: field.FieldType);

                for (var listenerIndex = 0; listenerIndex < listenerCount; listenerIndex++)
                {
                    var target = eventValue.GetPersistentTarget(listenerIndex);
                    var methodName = eventValue.GetPersistentMethodName(listenerIndex);
                    var targetLabel = target != null
                        ? $"{target.name} ({TypeUtility.GetFriendlyTypeName(target.GetType())})"
                        : "Missing Target";

                    var listenerNode = registry.GetOrCreateNode(
                        new PersistentUnityEventListenerKey(ownerObject, field, listenerIndex),
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
                            OwnerObject = ownerObject,
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
            UnityEngine.Object ownerObject,
            List<DependencyEdge> edges,
            DependencyNodeRegistry registry,
            ManagedObjectScanner managedObjectScanner)
        {
            if (ownerObject == null)
            {
                return;
            }

            var ownerNode = registry.GetOrCreateNode(ownerObject);
            foreach (var field in TypeUtility.GetAllInstanceFields(ownerObject.GetType()))
            {
                if (!TypeUtility.IsSerializedField(field))
                {
                    continue;
                }

                var value = field.GetValue(ownerObject);
                DependencyFieldSlotUtility.Upsert(
                    ownerNode,
                    field.Name,
                    isOutput: true,
                    hasValue: value != null,
                    isUnityEvent: typeof(UnityEventBase).IsAssignableFrom(field.FieldType),
                    valueSummary: DescribeSerializedValue(value),
                    valueType: field.FieldType,
                    unityReferenceValue: value as UnityEngine.Object);

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
                            OwnerObject = ownerObject,
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

                managedObjectScanner.ScanManagedField(ownerObject, ownerNode, field, value, edges, registry);
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
            UnityEngine.Object owner,
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
            UnityEngine.Object owner,
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
