using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PTrampert.SimplePatch;

/// <summary>
/// Reflection helpers for working with <see cref="IPatchObject{T}"/> types.
/// </summary>
public static class TypeExtensions
{
    /// <summary>
    /// Gets the type a patch object patches.
    /// Accepts both the open interface (<c>IPatchObject&lt;Person&gt;</c>) and any concrete
    /// implementation of it, such as the classes produced by <see cref="PatchClassBuilder"/>.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="sourceType">The patched type, or null if <paramref name="type"/> is not a patch object.</param>
    /// <returns>True if <paramref name="type"/> is, or implements, <see cref="IPatchObject{T}"/>.</returns>
    public static bool TryGetPatchSourceType(this Type type, [NotNullWhen(true)] out Type? sourceType)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (IsPatchObjectInterface(type))
        {
            sourceType = type.GetGenericArguments()[0];
            return true;
        }

        sourceType = type.GetInterfaces()
            .FirstOrDefault(IsPatchObjectInterface)
            ?.GetGenericArguments()[0];
        return sourceType != null;
    }

    private static bool IsPatchObjectInterface(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IPatchObject<>);

    /// <summary>
    /// Gets the type's public instance properties, excluding indexers and keeping only the
    /// most-derived declaration of each name. A property hidden with <c>new</c> and a different type
    /// is returned by <see cref="Type.GetProperties()"/> alongside the one hiding it; System.Text.Json
    /// uses the hiding one, so the patch class and the lookups against it must too.
    /// </summary>
    internal static IEnumerable<PropertyInfo> GetMostDerivedProperties(this Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0)
            .GroupBy(property => property.Name)
            .Select(properties => properties.MaxBy(property => InheritanceDepth(property.DeclaringType))!);

    /// <summary>
    /// Finds the property named <paramref name="name"/> among <see cref="GetMostDerivedProperties"/>,
    /// so a hidden property resolves to its most-derived declaration instead of throwing
    /// <see cref="AmbiguousMatchException"/> as <see cref="Type.GetProperty(string)"/> does.
    /// </summary>
    internal static PropertyInfo? GetMostDerivedProperty(this Type type, string name) =>
        type.GetMostDerivedProperties().FirstOrDefault(property => property.Name == name);

    private static int InheritanceDepth(Type? type)
    {
        var depth = 0;
        for (; type != null; type = type.BaseType)
        {
            depth++;
        }

        return depth;
    }

    internal static bool IsPatchObjectType(this Type type)
    {
        return type.GetInterfaces().Any(IsPatchObjectInterface);
    }

    internal static Type GetPatchObjectType(this Type type)
    {
        if (!type.IsPatchObjectType())
        {
            throw new InvalidOperationException($"Type {type.FullName} is not a patch object type.");
        }

        return type.GetInterfaces()
                   .First(IsPatchObjectInterface)
                   .GetGenericArguments()[0];
    }
}
