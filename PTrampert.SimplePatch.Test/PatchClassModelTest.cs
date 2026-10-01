using System.ComponentModel.DataAnnotations;
using PTrampert.SimplePatch.Test.TestObjects;

namespace PTrampert.SimplePatch.Test;

public class PatchClassModelTest
{
    private static string[] Names(IEnumerable<System.Reflection.PropertyInfo> properties) =>
        properties.Select(p => p.Name).ToArray();

    private static string[] OptionalNames(PatchClassModel model) =>
        Names(model.OptionalProperties.Select(p => p.Property));

    [Test]
    public void For_PositionalRecord_IsARecordWithNoConstructor()
    {
        var model = PatchClassModel.For(typeof(PositionalRecordTestObject));

        Assert.Multiple(() =>
        {
            Assert.That(model.SourceType, Is.EqualTo(typeof(PositionalRecordTestObject)));
            Assert.That(model.IsRecord, Is.True);
            Assert.That(model.Constructor, Is.Null, "A with expression calls no constructor.");
            Assert.That(model.ConstructorProperties, Is.Empty);
            Assert.That(OptionalNames(model), Is.EquivalentTo(new[] { "Name", "Count" }),
                "The get-only Tag is left to the with clone.");
            Assert.That(model.IgnoredProperties, Is.Empty);
        });
    }

    [Test]
    public void For_PlainClass_IsNotARecord()
    {
        var model = PatchClassModel.For(typeof(PlainClassTestObject));

        Assert.That(model.IsRecord, Is.False);
    }

    [Test]
    public void For_ConstructorBoundGetOnlyProperties_AreBoundInParameterOrder()
    {
        var model = PatchClassModel.For(typeof(ConstructorTestObject));

        Assert.Multiple(() =>
        {
            Assert.That(model.IsRecord, Is.False);
            Assert.That(model.Constructor, Is.Not.Null);
            Assert.That(model.Constructor!.GetParameters().Select(p => p.Name), Is.EqualTo(new[] { "NAME", "secret" }));
            Assert.That(Names(model.ConstructorProperties), Is.EqualTo(new[] { "Name", "Secret" }),
                "Parameters bind to properties by name, ignoring case.");
            Assert.That(OptionalNames(model), Is.EquivalentTo(new[] { "Name", "Color" }));
            Assert.That(Names(model.IgnoredProperties), Is.EqualTo(new[] { "Secret" }),
                "A constructor-bound [JsonIgnore] property is patchable but not optional.");
        });
    }

    [Test]
    public void For_GetOnlyPropertyNotInTheConstructor_IsLeftOut()
    {
        var model = PatchClassModel.For(typeof(ConstructorAndPrivateSetterTestObject));

        Assert.Multiple(() =>
        {
            Assert.That(Names(model.ConstructorProperties), Is.EqualTo(new[] { "Name" }));
            Assert.That(OptionalNames(model), Is.EquivalentTo(new[] { "Name", "Color" }),
                "Version has a private setter and isn't constructor-bound.");
        });
    }

    [Test]
    public void For_JsonConstructor_IsPreferredOverTheParameterlessOne()
    {
        var model = PatchClassModel.For(typeof(JsonConstructorTestObject));

        Assert.Multiple(() =>
        {
            Assert.That(model.Constructor!.GetParameters().Select(p => p.Name), Is.EqualTo(new[] { "id" }));
            Assert.That(Names(model.ConstructorProperties), Is.EqualTo(new[] { "Id" }));
            Assert.That(OptionalNames(model), Is.EquivalentTo(new[] { "Id", "Name" }));
            Assert.That(model.IgnoredProperties, Is.Empty,
                "Origin is get-only and not constructor-bound, so it isn't patchable at all.");
        });
    }

    [Test]
    public void For_AmbiguousConstructors_Throws()
    {
        Assert.That(() => PatchClassModel.For(typeof(AmbiguousConstructorTestObject)),
            Throws.TypeOf<NotSupportedException>());
    }

    [Test]
    public void For_HiddenProperty_UsesTheMostDerivedDeclaration()
    {
        var model = PatchClassModel.For(typeof(HiddenPropertyTestObject));

        var value = model.OptionalProperties.Single();
        Assert.Multiple(() =>
        {
            Assert.That(value.Property.DeclaringType, Is.EqualTo(typeof(HiddenPropertyTestObject)));
            Assert.That(value.Property.PropertyType, Is.EqualTo(typeof(string)));
            Assert.That(value.HasConverter, Is.True);
            Assert.That(value.Validators, Is.EqualTo(new[] { new OptionalValidatorModel(typeof(MaxLengthAttribute), 0) }));
        });
    }

    [Test]
    public void For_IgnoredProperties_OnlyAlwaysIsIgnored()
    {
        var model = PatchClassModel.For(typeof(JsonIgnoreConditionsTestObject));

        Assert.Multiple(() =>
        {
            Assert.That(Names(model.IgnoredProperties), Is.EqualTo(new[] { "Always" }));
            Assert.That(OptionalNames(model),
                Is.EquivalentTo(new[] { "Never", "WhenWritingNull", "WhenWritingDefault" }));
        });
    }

    [Test]
    public void For_CarriesConverterAndPropertyNameAndValidators()
    {
        var model = PatchClassModel.For(typeof(OptionalsBuilderTestObject));
        var byName = model.OptionalProperties.ToDictionary(p => p.Property.Name);

        Assert.Multiple(() =>
        {
            Assert.That(byName.Keys, Is.EquivalentTo(new[] { "Id", "Name", "FakeStringProp" }));
            Assert.That(byName["FakeStringProp"].HasConverter, Is.True);
            Assert.That(byName["Name"].HasConverter, Is.False);
            Assert.That(byName["Name"].JsonPropertyName, Is.EqualTo("name_field"));
            Assert.That(byName["Id"].JsonPropertyName, Is.Null);
            Assert.That(byName["Id"].Validators, Is.EqualTo(new[] { new OptionalValidatorModel(typeof(System.ComponentModel.DataAnnotations.RangeAttribute), 0) }));
            Assert.That(byName["Name"].Validators, Is.EqualTo(new[] { new OptionalValidatorModel(typeof(RequiredAttribute), 0) }));
            Assert.That(byName["FakeStringProp"].Validators, Is.Empty);
            Assert.That(Names(model.IgnoredProperties), Is.EqualTo(new[] { "IgnoredProp" }));
        });
    }

    [Test]
    public void For_RepeatedValidatorsOfOneType_GetTheirOwnIndexes()
    {
        var model = PatchClassModel.For(typeof(MultipleValidatorsTestObject));
        var byName = model.OptionalProperties.ToDictionary(p => p.Property.Name);

        Assert.Multiple(() =>
        {
            Assert.That(byName["Tag"].Validators, Is.EqualTo(new[]
            {
                new OptionalValidatorModel(typeof(ForbiddenSubstringAttribute), 0),
                new OptionalValidatorModel(typeof(ForbiddenSubstringAttribute), 1),
            }));
            // Different validator types each count from zero.
            Assert.That(byName["RequiredFirst"].Validators.Select(v => v.Index), Is.All.EqualTo(0));
            Assert.That(byName["RequiredFirst"].Validators.Select(v => v.ValidatorType),
                Is.EquivalentTo(new[] { typeof(RequiredAttribute), typeof(MaxLengthAttribute) }));
        });
    }
}
