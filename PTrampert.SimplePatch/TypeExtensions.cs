namespace PTrampert.SimplePatch;

internal static class TypeExtensions
{
    public static bool IsPatchObjectType(this Type type)
    {
        var interfaces = type.GetInterfaces();
        return interfaces.Any(iface => iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IPatchObject<>));
    }
    
    public static Type GetPatchObjectType(this Type type)
    {
        if (!type.IsPatchObjectType())
        {
            throw new InvalidOperationException($"Type {type.FullName} is not a patch object type.");
        }

        return type.GetInterfaces()
                   .First(iface => iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IPatchObject<>))
                   .GetGenericArguments()[0];
    }
}