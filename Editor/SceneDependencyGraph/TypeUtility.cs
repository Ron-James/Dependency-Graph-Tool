using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

namespace RonJames.DependencyGraphTool;

internal static class TypeUtility
{
    private const string OdinSerializeAttributeName = "Sirenix.Serialization.OdinSerializeAttribute";

    public static IEnumerable<FieldInfo> GetAllInstanceFields(Type type)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        for (var current = type; current != null; current = current.BaseType)
        {
            foreach (var field in current.GetFields(flags | BindingFlags.DeclaredOnly))
            {
                yield return field;
            }
        }
    }

    public static bool IsSerializedField(FieldInfo field)
    {
        if (field.IsStatic || field.IsLiteral || field.IsInitOnly || field.IsNotSerialized)
        {
            return false;
        }

        return field.IsPublic ||
               field.GetCustomAttribute<SerializeField>() != null ||
               HasSerializeReferenceAttribute(field) ||
               HasOdinSerializeAttribute(field);
    }

    public static bool HasSerializeReferenceAttribute(FieldInfo field)
    {
        return field.GetCustomAttribute<SerializeReference>() != null;
    }

    public static bool HasOdinSerializeAttribute(FieldInfo field)
    {
        return field.CustomAttributes.Any(a => a.AttributeType.FullName == OdinSerializeAttributeName);
    }

    public static bool CanParticipateInManagedScan(FieldInfo field)
    {
        return !field.IsStatic && !field.IsLiteral && !field.IsInitOnly && !field.IsNotSerialized;
    }

    public static bool IsTerminalType(Type type)
    {
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal);
    }

    public static bool ShouldSkipManagedGraphNode(Type type)
    {
        if (typeof(UnityEventBase).IsAssignableFrom(type))
        {
            return true;
        }

        var ns = type.Namespace ?? string.Empty;
        return ns.StartsWith("UnityEngine.Events", StringComparison.Ordinal) ||
               ns.StartsWith("System.Reflection", StringComparison.Ordinal);
    }

    public static DependencyType ParseCustomDependencyType(string dependencyKind)
    {
        if (string.IsNullOrWhiteSpace(dependencyKind))
        {
            return DependencyType.SerializedUnityRef;
        }

        return Enum.TryParse(dependencyKind.Trim(), ignoreCase: true, out DependencyType parsedType)
            ? parsedType
            : DependencyType.SerializedUnityRef;
    }

    public static string GetFriendlyTypeName(Type type)
    {
        if (type == null)
        {
            return "null";
        }

        if (type.IsArray)
        {
            return $"{GetFriendlyTypeName(type.GetElementType())}[]";
        }

        if (!type.IsGenericType)
        {
            return type.Name;
        }

        var baseName = type.Name;
        var tickIndex = baseName.IndexOf('`');
        if (tickIndex > 0)
        {
            baseName = baseName.Substring(0, tickIndex);
        }

        var genericArguments = type.GetGenericArguments().Select(GetFriendlyTypeName);
        return $"{baseName}<{string.Join(", ", genericArguments)}>";
    }
}
