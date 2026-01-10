using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using PTrampert.SimplePatch;
using PTrampert.SimplePatch.Test.TestObjects;
var json = """
{
    "id": 1,
    "name_field": "Test Name",
    "ignoredProp": "This should not be included",
    "fakeStringProp": "Fake Value"
}
""";
var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
};
options.Converters.Add(new OptionalJsonConverterFactory());
var builder = new PatchClassBuilder();
var optionalsType = builder.GetPatchClassFor(typeof(OptionalsBuilderTestObject));
Console.WriteLine($"Optionals Type: {optionalsType.FullName}");
Console.WriteLine($"Optionals Type Assembly: {optionalsType.Assembly.FullName}");
var instance = JsonSerializer.Deserialize(json, optionalsType, options);
if (instance == null)
{
    Console.WriteLine("Instance is null!");
}
else
{
    Console.WriteLine($"Instance Type: {instance.GetType().FullName}");
    Console.WriteLine($"Instance Assembly: {instance.GetType().Assembly.FullName}");
    var ifaces = instance.GetType().GetInterfaces();
    Console.WriteLine($"Interfaces: {string.Join(", ", ifaces.Select(i => i.FullName))}");
    var targetInterfaceType = typeof(IPatchObject<OptionalsBuilderTestObject>);
    Console.WriteLine($"Target Interface: {targetInterfaceType.FullName}");
    Console.WriteLine($"Target Interface Assembly: {targetInterfaceType.Assembly.FullName}");
    Console.WriteLine($"Is instance IPatchObject<>: {instance is IPatchObject<OptionalsBuilderTestObject>}");
    Console.WriteLine($"Can cast to IPatchObject<>: {instance as IPatchObject<OptionalsBuilderTestObject> != null}");
    // Check if any interface matches
    foreach (var iface in ifaces)
    {
        Console.WriteLine($"Interface: {iface.FullName}");
        Console.WriteLine($"  Assembly: {iface.Assembly.FullName}");
        Console.WriteLine($"  Is Generic: {iface.IsGenericType}");
        if (iface.IsGenericType)
        {
            Console.WriteLine($"  Generic Type Def: {iface.GetGenericTypeDefinition().FullName}");
            Console.WriteLine($"  Generic Args: {string.Join(", ", iface.GetGenericArguments().Select(t => t.FullName))}");
            foreach (var arg in iface.GetGenericArguments())
            {
                Console.WriteLine($"    Arg Assembly: {arg.Assembly.FullName}");
            }
        }
    }
}
