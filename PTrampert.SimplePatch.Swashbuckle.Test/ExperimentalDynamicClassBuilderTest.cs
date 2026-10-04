using Microsoft.Extensions.DependencyInjection;
using PTrampert.SimplePatch.Swashbuckle.Test.TestObjects;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PTrampert.SimplePatch.Swashbuckle.Test;

// PatchClassBuilder.UseExperimentalDynamicClassBuilder is process-wide, so these tests must not
// overlap with others, and each one puts it back afterwards.
[NonParallelizable]
public class ExperimentalDynamicClassBuilderTest
{
    [TearDown]
    public void TearDown() => PatchClassBuilder.UseExperimentalDynamicClassBuilder = false;

    [Test]
    public void PatchSchema_DescribesAnInternalModelWhenTheFlagIsOn()
    {
        PatchClassBuilder.UseExperimentalDynamicClassBuilder = true;
        var services = new ServiceCollection();
        services.AddSwaggerGen(options => options.AddSimplePatchSchemas());
        using var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<ISchemaGenerator>();

        var repository = new SchemaRepository();
        generator.GenerateSchema(typeof(IPatchObject<InternalPersonTestModel>), repository);

        var patchSchema = repository.Schemas["InternalPersonTestModelIPatchObject"];
        Assert.Multiple((Action)(() =>
        {
            Assert.That(patchSchema.Properties?.Keys, Is.EquivalentTo(new[] { "name", "email" }));
            Assert.That(patchSchema.Properties!["name"].MaxLength, Is.EqualTo(255));
        }));
    }
}
