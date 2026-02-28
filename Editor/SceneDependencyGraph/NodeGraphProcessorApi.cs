using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

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

    public bool TryExportAdapterSnapshot(string graphAssetPath, DependencyModel model, out string snapshotPath)
    {
        snapshotPath = null;
        if (string.IsNullOrWhiteSpace(graphAssetPath))
        {
            return false;
        }

        if (!TryCreateGraphAsset(graphAssetPath))
        {
            return false;
        }

        var adapted = DependencyGraphAdapterBuilder.Build(model);
        var wrapper = new DependencyGraphAdapterModelWrapper { Graph = adapted };
        var json = JsonUtility.ToJson(wrapper, true);

        var projectRoot = Path.GetDirectoryName(Application.dataPath);
        var snapshotRelativePath = Path.ChangeExtension(graphAssetPath, ".adapter.json");
        var snapshotAbsolutePath = Path.Combine(projectRoot ?? string.Empty, snapshotRelativePath);
        var snapshotDirectory = Path.GetDirectoryName(snapshotAbsolutePath);
        if (!string.IsNullOrWhiteSpace(snapshotDirectory))
        {
            Directory.CreateDirectory(snapshotDirectory);
        }

        File.WriteAllText(snapshotAbsolutePath, json);
        AssetDatabase.ImportAsset(snapshotRelativePath);
        snapshotPath = snapshotRelativePath;
        return true;
    }

    [Serializable]
    private sealed class DependencyGraphAdapterModelWrapper
    {
        public DependencyGraphAdapterModel Graph;
    }
}
