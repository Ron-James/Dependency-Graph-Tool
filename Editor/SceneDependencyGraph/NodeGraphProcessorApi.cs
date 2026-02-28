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
        private const string BaseGraphWindowTypeName = "GraphProcessor.BaseGraphWindow";
        private const string BaseGraphTypeName = "GraphProcessor.BaseGraph";

        private readonly Type _baseGraphWindowType;
        private readonly Type _baseGraphType;
        private readonly Type _concreteGraphWindowType;
        private readonly Type _concreteGraphType;
        private readonly Assembly _graphProcessorAssembly;

        private NodeGraphProcessorApi(
            Type baseGraphWindowType,
            Type baseGraphType,
            Type concreteGraphWindowType,
            Type concreteGraphType,
            Assembly graphProcessorAssembly)
        {
            _baseGraphWindowType = baseGraphWindowType;
            _baseGraphType = baseGraphType;
            _concreteGraphWindowType = concreteGraphWindowType;
            _concreteGraphType = concreteGraphType;
            _graphProcessorAssembly = graphProcessorAssembly;
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
            var concreteGraphWindowType = FindConcreteSubclass(baseGraphWindowType, graphProcessorAssembly);
            var concreteGraphType = FindConcreteSubclass(baseGraphType, graphProcessorAssembly);

            return new NodeGraphProcessorApi(
                baseGraphWindowType,
                baseGraphType,
                concreteGraphWindowType,
                concreteGraphType,
                graphProcessorAssembly);
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
