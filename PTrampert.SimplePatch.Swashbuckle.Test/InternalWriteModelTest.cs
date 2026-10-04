using Microsoft.Extensions.DependencyInjection;
using PTrampert.SimplePatch.Swashbuckle.Test.TestObjects;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PTrampert.SimplePatch.Swashbuckle.Test;

// An internal write model, which the test project grants to the generated assemblies with
// InternalsVisibleTo in its project file.
public class InternalWriteModelTest
{
    [Test]
    public void PatchSchema_DescribesAnInternalModel()
    {
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
