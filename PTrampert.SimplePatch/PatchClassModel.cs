using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json.Serialization;

namespace PTrampert.SimplePatch;

/// <summary>
/// Describes what the patch class for a source type contains: how it builds the patched instance,
/// which properties it exposes as <see cref="Optional{T}"/>, and which attributes each carries.
/// </summary>
/// <remarks>
/// Kept apart from <see cref="RoslynPatchClassBuilder"/> and <see cref="EmitPatchClassBuilder"/> so
/// that every builder makes these decisions in one place. Builders that each made them inline could drift apart, and the patch class would then
/// stop matching the JSON contract System.Text.Json uses for the source type. Restrictions that
/// belong to a particular way of generating the class, such as the generated assembly only being
/// able to see public types, stay in that builder.
/// </remarks>
internal sealed class PatchClassModel
{
    private PatchClassModel(
        Type sourceType,
        bool isRecord,
        ConstructorInfo? constructor,
        IReadOnlyList<PropertyInfo> constructorProperties,
        IReadOnlyList<OptionalPropertyModel> optionalProperties,
        IReadOnlyList<PropertyInfo> ignoredProperties)
    {
        SourceType = sourceType;
        IsRecord = isRecord;
        Constructor = constructor;
        ConstructorProperties = constructorProperties;
        OptionalProperties = optionalProperties;
        IgnoredProperties = ignoredProperties;
    }

    /// <summary>The type the patch class patches.</summary>
    public Type SourceType { get; }

    /// <summary>
    /// Whether the source type is a record class. Records (including positional ones, which have
    /// no parameterless constructor) are patched with a <c>with</c> expression. It clones the
    /// target, so properties the patch doesn't assign, such as ignored or get-only ones, keep their
    /// values, as does the target's runtime type when it is a derived record.
    /// </summary>
    public bool IsRecord { get; }

    /// <summary>
    /// The constructor that builds the patched instance, chosen as System.Text.Json would. Null for
    /// a record, because <c>with</c> calls no constructor, and for a struct's implicit
    /// parameterless constructor.
    /// </summary>
    public ConstructorInfo? Constructor { get; }

    /// <summary>
    /// The property each of <see cref="Constructor"/>'s parameters binds to, in parameter order.
    /// </summary>
    public IReadOnlyList<PropertyInfo> ConstructorProperties { get; }

    /// <summary>
    /// The properties the patch class exposes as <see cref="Optional{T}"/>, in declaration order.
    /// </summary>
    public IReadOnlyList<OptionalPropertyModel> OptionalProperties { get; }

    /// <summary>
    /// Patchable properties that System.Text.Json never reads, so the patch class has no
    /// <see cref="Optional{T}"/> for them. A patched instance that isn't a <c>with</c> clone has to
    /// copy them from the target.
    /// </summary>
    public IReadOnlyList<PropertyInfo> IgnoredProperties { get; }

    /// <summary>
    /// Builds the model for <paramref name="type"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// No constructor can be chosen for <paramref name="type"/>, or a constructor parameter doesn't
    /// match a public property.
    /// </exception>
    public static PatchClassModel For(Type type)
    {
        // Static properties and indexers aren't part of the JSON contract (System.Text.Json
        // skips both), and neither can be assigned in an object initializer.
        // GetMostDerivedProperties leaves both out.
        var sourceProperties = type.GetMostDerivedProperties().ToArray();
        var isRecord = DetectRecord(type);
        var constructor = isRecord ? null : SelectConstructor(type);
        var constructorProperties = (constructor?.GetParameters() ?? [])
            .Select(parameter => GetConstructorParameterProperty(type, sourceProperties, parameter))
            .ToList();
        // Only public setters and init accessors can be assigned from outside the type. This
        // matches System.Text.Json, which also ignores non-public setters. A property set through
        // the constructor can still be patched, even if it is get-only.
        var patchedProperties = sourceProperties
            .Where(p => p.SetMethod is { IsPublic: true } || constructorProperties.Contains(p))
            .ToList();
        var ignoredProperties = patchedProperties
            .Where(IsIgnoredOnRead)
            .ToList();
        var optionalProperties = patchedProperties
            .Where(p => !IsIgnoredOnRead(p))
            .Select(OptionalPropertyModel.For)
            .ToList();

        return new PatchClassModel(
            type, isRecord, constructor, constructorProperties, optionalProperties, ignoredProperties);
    }

    /// <summary>
    /// Detects record classes by their compiler-generated clone method, as ASP.NET Core model
    /// binding does.
    /// </summary>
    private static bool DetectRecord(Type type) =>
        type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) != null;

    /// <summary>
    /// Selects the constructor the way System.Text.Json does: the public one marked with
    /// <see cref="JsonConstructorAttribute"/>, otherwise the public parameterless one, otherwise the
    /// single public one. Returns null for a struct's implicit parameterless constructor.
    /// </summary>
    private static ConstructorInfo? SelectConstructor(Type type)
    {
        var constructors = type.GetConstructors();
        var annotated = constructors.FirstOrDefault(c => c.IsDefined(typeof(JsonConstructorAttribute)));
        if (annotated != null)
        {
            return annotated;
        }

        var parameterless = constructors.FirstOrDefault(c => c.GetParameters().Length == 0);
        if (parameterless != null || type.IsValueType)
        {
            return parameterless;
        }

        if (constructors.Length == 1)
        {
            return constructors[0];
        }

        throw new NotSupportedException(
            $"Cannot choose a constructor for {type.FullName}. Give it a public parameterless constructor, "
            + $"a single public constructor, or mark one with [{nameof(JsonConstructorAttribute)}].");
    }

    /// <summary>
    /// Gets the property a constructor parameter binds to: the one with the same name, ignoring case,
    /// as in System.Text.Json.
    /// </summary>
    private static PropertyInfo GetConstructorParameterProperty(
        Type type, PropertyInfo[] properties, ParameterInfo parameter)
    {
        return properties.FirstOrDefault(p => string.Equals(p.Name, parameter.Name, StringComparison.OrdinalIgnoreCase))
               ?? throw new NotSupportedException(
                   $"Constructor parameter '{parameter.Name}' of {type.FullName} does not match a public property.");
    }

    // Only JsonIgnoreCondition.Always (the default for a bare [JsonIgnore]) stops System.Text.Json
    // from deserializing a property. Never forces it in, and the WhenWriting* conditions only affect
    // serialization, so those properties are patchable. The attribute itself isn't carried onto the
    // Optional<T> property, because its write-side conditions don't map onto the wrapper.
    private static bool IsIgnoredOnRead(PropertyInfo property) =>
        property.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition == JsonIgnoreCondition.Always;
}

/// <summary>
/// A source property the patch class exposes as <see cref="Optional{T}"/>, with the attributes the
/// <see cref="Optional{T}"/> property carries over from it.
/// </summary>
internal sealed class OptionalPropertyModel
{
    private OptionalPropertyModel(
        PropertyInfo property,
        bool hasConverter,
        string? jsonPropertyName,
        IReadOnlyList<OptionalValidatorModel> validators)
    {
        Property = property;
        HasConverter = hasConverter;
        JsonPropertyName = jsonPropertyName;
        Validators = validators;
    }

    /// <summary>The source property.</summary>
    public PropertyInfo Property { get; }

    /// <summary>
    /// Whether the source property has a <see cref="JsonConverterAttribute"/>. If so, the
    /// <see cref="Optional{T}"/> property gets an <see cref="OptionalConverterAttribute"/> that
    /// names the source type and this property, so the converter is resolved from the source.
    /// </summary>
    public bool HasConverter { get; }

    /// <summary>
    /// The name from the source property's <see cref="JsonPropertyNameAttribute"/>, copied onto the
    /// <see cref="Optional{T}"/> property, or null if it has none.
    /// </summary>
    public string? JsonPropertyName { get; }

    /// <summary>
    /// One entry per <see cref="ValidationAttribute"/> on the source property, in the order
    /// reflection returns them. Each becomes an <see cref="OptionalValidationAttribute"/>.
    /// </summary>
    public IReadOnlyList<OptionalValidatorModel> Validators { get; }

    internal static OptionalPropertyModel For(PropertyInfo property)
    {
        // OptionalValidationAttribute finds its validator by type and by position among the
        // source property's validators of that type, so repeated validators of one type each
        // need their own index.
        var validatorTypeCounts = new Dictionary<Type, int>();
        var validators = new List<OptionalValidatorModel>();
        foreach (var validationAttribute in property.GetCustomAttributes<ValidationAttribute>())
        {
            var validatorType = validationAttribute.GetType();
            var index = validatorTypeCounts.GetValueOrDefault(validatorType);
            validatorTypeCounts[validatorType] = index + 1;
            validators.Add(new OptionalValidatorModel(validatorType, index));
        }

        return new OptionalPropertyModel(
            property,
            property.GetCustomAttribute<JsonConverterAttribute>() != null,
            property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name,
            validators);
    }
}

/// <summary>
/// The arguments of an <see cref="OptionalValidationAttribute"/>: the validator's type, and its
/// position among the source property's validators of that type.
/// </summary>
internal sealed record OptionalValidatorModel(Type ValidatorType, int Index);
