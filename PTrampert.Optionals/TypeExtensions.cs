namespace PTrampert.Optionals;

internal static class TypeExtensions
{
    public static bool IsPatchObjectType(this Type type)
    {
        var interfaces = type.GetInterfaces();
        return interfaces.Any(iface => iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IPatchObjectFor<>));
    }
    
    public static Type GetPatchObjectType(this Type type)
    {
        if (!type.IsPatchObjectType())
        {
            throw new InvalidOperationException($"Type {type.FullName} is not a patch object type.");
        }

        return type.GetInterfaces()
                   .First(iface => iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IPatchObjectFor<>))
                   .GetGenericArguments()[0];
    }
}