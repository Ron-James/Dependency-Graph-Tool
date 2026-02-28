using System;

namespace RonJames.DependencyGraphTool;

internal static class GraphNamingUtility
{
    public static string BuildNodeDisplayName(object owner, string fallbackName)
    {
        if (owner is INamedEntry named && !string.IsNullOrWhiteSpace(named.Name))
        {
            return $"{named.Name} ({TypeUtility.GetFriendlyTypeName(owner.GetType())})";
        }

        return fallbackName;
    }

    public static string BuildCollectionItemLabel(string memberPath, int index, object item)
    {
        if (item is INamedEntry named && !string.IsNullOrWhiteSpace(named.Name))
        {
            return $"{memberPath}[{named.Name}]";
        }

        return $"{memberPath}[{index}]";
    }
}
