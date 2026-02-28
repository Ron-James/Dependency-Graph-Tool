using System;

namespace RonJames.DependencyGraphTool;

internal static class GraphNamingUtility
{
    public static string BuildNodeDisplayName(object owner, string fallbackName)
    {
        if (owner is IDependencyGraphNodeNameProvider customNameProvider &&
            !string.IsNullOrWhiteSpace(customNameProvider.DependencyGraphNodeName))
        {
            return $"{customNameProvider.DependencyGraphNodeName} ({TypeUtility.GetFriendlyTypeName(owner.GetType())})";
        }

        return fallbackName;
    }

    public static string BuildCollectionItemLabel(string memberPath, int index, object item)
    {
        if (item is IDependencyGraphNodeNameProvider customNameProvider &&
            !string.IsNullOrWhiteSpace(customNameProvider.DependencyGraphNodeName))
        {
            return $"{memberPath}[{customNameProvider.DependencyGraphNodeName}]";
        }

        return $"{memberPath}[{index}]";
    }
}
