using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace RonJames.DependencyGraphTool;

internal sealed class NodeGraphProcessorApi
{
    private const string BaseGraphWindowTypeName = "GraphProcessor.BaseGraphWindow";
    private const string BaseGraphTypeName = "GraphProcessor.BaseGraph";

    private readonly Type _baseGraphWindowType;
    private readonly Type _baseGraphType;
    private readonly Assembly _graphProcessorAssembly;

    private NodeGraphProcessorApi(Type baseGraphWindowType, Type baseGraphType, Assembly graphProcessorAssembly)
    {
        _baseGraphWindowType = baseGraphWindowType;
        _baseGraphType = baseGraphType;
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
        return new NodeGraphProcessorApi(baseGraphWindowType, baseGraphType, graphProcessorAssembly);
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

            var version = _graphProcessorAssembly.GetName().Version;
            return version == null ? "Installed" : $"Installed ({version})";
        }
    }

    public bool TryOpenGraphWindow()
    {
        if (_baseGraphWindowType == null)
        {
            return false;
        }

        EditorWindow.GetWindow(_baseGraphWindowType);
        return true;
    }

    public bool TryCreateGraphAsset(string path)
    {
        if (_baseGraphType == null || _baseGraphType.IsAbstract)
        {
            return false;
        }

        var graphAsset = ScriptableObject.CreateInstance(_baseGraphType);
        AssetDatabase.CreateAsset(graphAsset, path);
        EditorUtility.SetDirty(graphAsset);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(graphAsset);
        Selection.activeObject = graphAsset;
        return true;
    }
}
