using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PTrampert.SimplePatch.Swashbuckle.Test.TestObjects;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PTrampert.SimplePatch.Swashbuckle.Test;

// Opting in to the Emit builder is process-wide, so these tests restore the default afterwards and
// don't run alongside the others.
[NonParallelizable]
public class EmitBuilderSchemaFilterTest
{
    [SetUp]
    [TearDown]
    public void UseTheDefaultBuilder() => PatchClassBuilder.UseEmitBuilder = false;

    [Test]
    public void PatchSchema_DescribesANonPublicModelWhenTheEmitBuilderIsEnabled()
    {
        var services = new ServiceCollection();
        services.Configure<JsonOptions>(options =>
            options.JsonSerializerOptions.AddSimplePatchConverters(useExperimentalDynamicClassBuilder: true));
        services.AddSwaggerGen(options => options.AddSimplePatchSchemas());

        using var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<ISchemaGenerator>();
        var repository = new SchemaRepository();
        generator.GenerateSchema(typeof(IPatchObject<InternalPersonTestModel>), repository);

        var patchSchema = repository.Schemas["InternalPersonTestModelIPatchObject"];
        Assert.Multiple((Action)(() =>
        {
            Assert.That(patchSchema.Properties?.Keys, Is.EquivalentTo(new[] { "name", "nick_name" }));
            Assert.That(patchSchema.Properties!["name"].MaxLength, Is.EqualTo(255));
        }));
    }
}
