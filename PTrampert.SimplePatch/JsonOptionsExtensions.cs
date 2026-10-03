using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace PTrampert.SimplePatch;

/// <summary>
/// Extensions to easily add the required JSON converters to <see cref="JsonSerializerOptions"/>.
/// </summary>
public static class JsonOptionsExtensions
{
    /// <summary>
    /// Adds the converters required for (de)serializing <see cref="Optional{T}"/> and <see cref="IPatchObject{T}"/> types.
    /// Also makes serialization omit every <see cref="Optional{T}"/> property that has no value, so an unset
    /// optional is written as "not sent" rather than as null.
    /// </summary>
    /// <param name="options">The options to add converters to.</param>
    public static void AddSimplePatchConverters(this JsonSerializerOptions options)
    {
        options.Converters.Add(new OptionalJsonConverterFactory());
        options.Converters.Add(new PatchJsonConverterFactory());
        options.TypeInfoResolver = (options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver())
            .WithAddedModifier(SkipUnsetOptionals);
    }

    // A separate overload rather than an optional parameter on the existing method, because changing
    // that method's signature would break assemblies compiled against it.
    /// <summary>
    /// Adds the converters required for (de)serializing <see cref="Optional{T}"/> and <see cref="IPatchObject{T}"/>
    /// types, as <see cref="AddSimplePatchConverters(JsonSerializerOptions)"/> does, and optionally opts in to the
    /// experimental Reflection.Emit patch class builder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Experimental.</b> The Reflection.Emit builder emits the patch class as IL instead of compiling C# with
    /// Roslyn. Unlike Roslyn, it supports internal source types, provided their assembly declares
    /// <c>[assembly: InternalsVisibleTo("PTrampert.SimplePatch.Emitted")]</c>. Private and protected nested
    /// types are still not supported.
    /// </para>
    /// <para>
    /// The choice applies to the whole process, not just to <paramref name="options"/>, so the OpenAPI
    /// integrations generate their schemas with the same builder. Once enabled it stays enabled: passing
    /// <see langword="false"/>, or calling <see cref="AddSimplePatchConverters(JsonSerializerOptions)"/>, doesn't
    /// turn it off.
    /// </para>
    /// </remarks>
    /// <param name="options">The options to add converters to.</param>
    /// <param name="useExperimentalDynamicClassBuilder">
    /// <see langword="true"/> to have <see cref="PatchClassBuilder.GetPatchClassFor"/> emit patch classes with
    /// Reflection.Emit from now on. <see langword="false"/> leaves the builder as it is.
    /// </param>
    public static void AddSimplePatchConverters(this JsonSerializerOptions options, bool useExperimentalDynamicClassBuilder)
    {
        if (useExperimentalDynamicClassBuilder)
        {
            PatchClassBuilder.UseEmitBuilder = true;
        }

        options.AddSimplePatchConverters();
    }

    // A converter can't omit the property it converts (the name is already written when it runs),
    // so unset optionals are skipped at the contract level instead.
    private static void SkipUnsetOptionals(JsonTypeInfo typeInfo)
    {
        foreach (var property in typeInfo.Properties)
        {
            if (!property.PropertyType.IsGenericType ||
                property.PropertyType.GetGenericTypeDefinition() != typeof(Optional<>))
                continue;

            var existing = property.ShouldSerialize;
            property.ShouldSerialize = (obj, value) =>
                ((IOptional)value!).HasValue && (existing?.Invoke(obj, value) ?? true);
        }
    }
}
