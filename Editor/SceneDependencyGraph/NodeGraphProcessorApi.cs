using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.PackageManager;
using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;


namespace RonJames.DependencyGraphTool
{
    internal sealed class NodeGraphProcessorApi
    {
        private const string DefaultGraphAssetPath = "Assets/SceneDependencyGraph.asset";
        private const string BaseGraphWindowTypeName = "GraphProcessor.BaseGraphWindow";
        private const string BaseGraphTypeName = "GraphProcessor.BaseGraph";
        private const string GraphBridgeTypeName = "RonJames.DependencyGraphTool.NodeGraphProcessorIntegration.NodeGraphProcessorBridge";
        private const string SceneDependencyGraphWindowTypeName = "RonJames.DependencyGraphTool.NodeGraphProcessorIntegration.SceneDependencyGraphGraphWindow";
        private const string SceneDependencyGraphAssetTypeName = "RonJames.DependencyGraphTool.NodeGraphProcessorIntegration.SceneDependencyGraphAsset";

        private readonly Type _baseGraphWindowType;
        private readonly Type _baseGraphType;
        private readonly Type _concreteGraphWindowType;
        private readonly Type _concreteGraphType;
        private readonly Assembly _graphProcessorAssembly;
        private readonly Type _graphBridgeType;

        private NodeGraphProcessorApi(
            Type baseGraphWindowType,
            Type baseGraphType,
            Type concreteGraphWindowType,
            Type concreteGraphType,
            Assembly graphProcessorAssembly,
            Type graphBridgeType)
        {
            _baseGraphWindowType = baseGraphWindowType;
            _baseGraphType = baseGraphType;
            _concreteGraphWindowType = concreteGraphWindowType;
            _concreteGraphType = concreteGraphType;
            _graphProcessorAssembly = graphProcessorAssembly;
            _graphBridgeType = graphBridgeType;
        }

        public static NodeGraphProcessorApi Detect()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var baseGraphWindowType = assemblies
                .Select(assembly => assembly.GetType(BaseGraphWindowTypeName, false))
                .FirstOrDefault(type => type != null);

            var baseGraphType = assemblies
                .Select(assembly => assembly.GetType(BaseGraphTypeName, false))
                .FirstOrDefault(type => type != null);

            var graphProcessorAssembly = baseGraphWindowType?.Assembly ?? baseGraphType?.Assembly;
            var graphBridgeType = assemblies
                .Select(assembly => assembly.GetType(GraphBridgeTypeName, false))
                .FirstOrDefault(type => type != null);
            var sceneGraphWindowType = assemblies
                .Select(assembly => assembly.GetType(SceneDependencyGraphWindowTypeName, false))
                .FirstOrDefault(type => type != null);
            var sceneGraphType = assemblies
                .Select(assembly => assembly.GetType(SceneDependencyGraphAssetTypeName, false))
                .FirstOrDefault(type => type != null);

            var concreteGraphWindowType = ResolveConcreteType(baseGraphWindowType, sceneGraphWindowType, graphProcessorAssembly);
            var concreteGraphType = ResolveConcreteType(baseGraphType, sceneGraphType, graphProcessorAssembly);

            return new NodeGraphProcessorApi(
                baseGraphWindowType,
                baseGraphType,
                concreteGraphWindowType,
                concreteGraphType,
                graphProcessorAssembly,
                graphBridgeType);
        }

        public bool IsAvailable => _baseGraphWindowType != null || _baseGraphType != null;

        public string DisplayVersion
        {
            get
            {
                if (_graphProcessorAssembly == null)
                {
                    return "Not Installed";
                }

                var packageVersion = TryGetPackageVersion();
                if (!string.IsNullOrWhiteSpace(packageVersion))
                {
                    return $"Installed ({packageVersion})";
                }

                var version = _graphProcessorAssembly.GetName().Version;
                return version == null ? "Installed" : $"Installed ({version})";
            }
        }

        private string TryGetPackageVersion()
        {
            var packageInfo = PackageManagerPackageInfo.FindForAssembly(_graphProcessorAssembly);
            return packageInfo?.version;
        }

        public bool TryOpenGraphWindow()
        {
            if (_concreteGraphWindowType == null)
            {
                return false;
            }

            EditorWindow.GetWindow(_concreteGraphWindowType);
            return true;
        }

        public bool TryCreateGraphAsset(string path)
        {
            if (_concreteGraphType == null)
            {
                return false;
            }

            var graphAsset = ScriptableObject.CreateInstance(_concreteGraphType);
            AssetDatabase.CreateAsset(graphAsset, path);
            EditorUtility.SetDirty(graphAsset);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(graphAsset);
            Selection.activeObject = graphAsset;
            return true;
        }

        public bool TryViewGraph(DependencyModel model, out string error)
        {
            error = null;
            if (model == null)
            {
                error = "No scene dependency model is available.";
                return false;
            }

            if (_graphBridgeType == null)
            {
                if (TryOpenOrCreateFallbackGraphAsset(DefaultGraphAssetPath, out error))
                {
                    return true;
                }

                error = string.IsNullOrWhiteSpace(error)
                    ? "NodeGraphProcessor integration bridge was not found and a fallback graph asset could not be opened."
                    : error;
                return false;
            }

            var viewMethod = _graphBridgeType.GetMethod(
                "TryOpenOrCreateGraph",
                BindingFlags.Public | BindingFlags.Static);

            if (viewMethod == null)
            {
                error = "NodeGraphProcessor integration bridge does not expose TryOpenOrCreateGraph.";
                return false;
            }

            try
            {
                var args = new object[] { model, null };
                var result = viewMethod.Invoke(null, args);
                error = args[1] as string;
                if (result is bool success && success)
                {
                    return true;
                }
            }
            catch (TargetInvocationException exception)
            {
                error = exception.InnerException?.Message ?? exception.Message;
            }

            return TryOpenOrCreateFallbackGraphAsset(DefaultGraphAssetPath, out error);
        }

        private bool TryOpenOrCreateFallbackGraphAsset(string path, out string error)
        {
            error = null;
            if (_concreteGraphType == null)
            {
                error = "No compatible NodeGraphProcessor graph type was found for fallback graph asset creation.";
                return false;
            }

            var graphAsset = AssetDatabase.LoadAssetAtPath(path, _concreteGraphType);
            if (graphAsset == null)
            {
                graphAsset = ScriptableObject.CreateInstance(_concreteGraphType);
                AssetDatabase.CreateAsset(graphAsset, path);
            }

            EditorUtility.SetDirty(graphAsset);
            AssetDatabase.SaveAssets();
            Selection.activeObject = graphAsset;
            EditorGUIUtility.PingObject(graphAsset);

            if (_concreteGraphWindowType != null)
            {
                EditorWindow.GetWindow(_concreteGraphWindowType);
            }

            return true;
        }

        private static Type ResolveConcreteType(Type baseType, Type preferredType, Assembly preferredAssembly)
        {
            if (baseType == null)
            {
                return null;
            }

            if (preferredType != null && baseType.IsAssignableFrom(preferredType) && !preferredType.IsAbstract)
            {
                return preferredType;
            }

            return FindConcreteSubclass(baseType, preferredAssembly);
        }

        private static Type FindConcreteSubclass(Type baseType, Assembly preferredAssembly)
        {
            if (baseType == null)
            {
                return null;
            }

            if (!baseType.IsAbstract)
            {
                return baseType;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var typesInPreferredAssembly = preferredAssembly == null
                ? Array.Empty<Type>()
                : GetLoadableTypes(preferredAssembly);

            var matchingType = typesInPreferredAssembly.FirstOrDefault(type =>
                baseType.IsAssignableFrom(type) &&
                !type.IsAbstract &&
                !type.IsGenericTypeDefinition);

            if (matchingType != null)
            {
                return matchingType;
            }

            foreach (var assembly in assemblies)
            {
                if (assembly == preferredAssembly)
                {
                    continue;
                }

                matchingType = GetLoadableTypes(assembly).FirstOrDefault(type =>
                    baseType.IsAssignableFrom(type) &&
                    !type.IsAbstract &&
                    !type.IsGenericTypeDefinition);

                if (matchingType != null)
                {
                    return matchingType;
                }
            }

            return null;
        }

        private static Type[] GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null).ToArray();
            }
        }
    }
}
