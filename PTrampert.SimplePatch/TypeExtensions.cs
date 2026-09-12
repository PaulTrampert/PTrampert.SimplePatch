using System.Diagnostics.CodeAnalysis;

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
