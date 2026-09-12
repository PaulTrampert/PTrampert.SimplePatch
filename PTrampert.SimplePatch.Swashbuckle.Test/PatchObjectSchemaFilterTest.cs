using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using PTrampert.SimplePatch.Swashbuckle.Test.TestObjects;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PTrampert.SimplePatch.Swashbuckle.Test;

public class PatchObjectSchemaFilterTest
{
    private const string DefaultPatchSchemaId = "PersonTestModelIPatchObject";

    [Test]
    public void PatchSchema_DescribesThePatchedModelsProperties()
    {
        var (patchSchema, _) = GeneratePatchSchema();

        Assert.That(patchSchema.Properties?.Keys,
            Is.EquivalentTo(new[] { "name", "dateOfBirth", "email", "nick_name" }));
    }

    [Test]
    public void PatchSchema_KeepsTheValidationConstraintsOfThePatchedModel()
    {
        var (patchSchema, _) = GeneratePatchSchema();

        var name = patchSchema.Properties!["name"];
        Assert.Multiple((Action)(() =>
        {
            Assert.That(name.MaxLength, Is.EqualTo(255));
            Assert.That(name.MinLength, Is.EqualTo(3));
            Assert.That(patchSchema.Properties!["email"].Format, Is.EqualTo("email"));
        }));
    }

    [Test]
    public void PatchSchema_MakesEveryPropertyOptional()
    {
        var (patchSchema, repository) = GeneratePatchSchema();

        Assert.Multiple((Action)(() =>
        {
            Assert.That(patchSchema.Required, Is.Null.Or.Empty);
            Assert.That(repository.Schemas[nameof(PersonTestModel)].Required, Does.Contain("name"),
                "The patched model's own schema must keep its required properties — only the patch body is partial.");
        }));
    }

    [Test]
    public void PatchSchema_OmitsPropertiesThePatchObjectCannotSet()
    {
        var (patchSchema, repository) = GeneratePatchSchema();

        Assert.Multiple((Action)(() =>
        {
            Assert.That(patchSchema.Properties, Does.Not.ContainKey("display"),
                "Get-only properties are not patchable.");
            Assert.That(repository.Schemas[nameof(PersonTestModel)].Properties, Does.ContainKey("display"),
                "Guard: the patched model's schema does describe the get-only property.");
            Assert.That(patchSchema.Properties, Does.Not.ContainKey("secret"),
                "[JsonIgnore] properties are not patchable.");
        }));
    }

    [Test]
    public void PatchSchema_DescribesItselfAsAPartialUpdate()
    {
        var (patchSchema, _) = GeneratePatchSchema();

        Assert.That(patchSchema.Description,
            Is.EqualTo("Partial update of PersonTestModel. Omitted properties are left unchanged."));
    }

    [Test]
    public void PatchSchema_KeepsTheGeneratedDescriptionWhenTheFormatIsCleared()
    {
        var (patchSchema, _) = GeneratePatchSchema(options => options.DescriptionFormat = null);

        Assert.That(patchSchema.Description, Is.Null);
    }

    [Test]
    public void PatchSchema_KeepsRequiredWhenClearRequiredIsOff()
    {
        var (patchSchema, _) = GeneratePatchSchema(options => options.ClearRequired = false);

        Assert.That(patchSchema.Required, Does.Contain("name"));
    }

    [Test]
    public void PatchSchema_UsesTheSuppliedExample()
    {
        var (patchSchema, _) = GeneratePatchSchema(options =>
            options.Example = _ => new JsonObject { ["name"] = "New Name" });

        Assert.That(patchSchema.Examples?.Select(example => example?.ToJsonString()),
            Is.EqualTo(new[] { """{"name":"New Name"}""" }));
    }

    [Test]
    public void PatchSchema_SerializesTheExampleAsTheSingularOpenApi30Keyword()
    {
        var (patchSchema, _) = GeneratePatchSchema(options =>
            options.Example = _ => new JsonObject { ["name"] = "New Name" });

        var writer = new StringWriter();
        patchSchema.SerializeAsV3(new OpenApiJsonWriter(writer));
        var document = JsonNode.Parse(writer.ToString())!.AsObject();

        // Swashbuckle emits OpenAPI 3.0, where the keyword is the singular "example". Setting
        // Examples is still correct: the serializer narrows it for this version. Asserted on the
        // serialized output rather than the object model, because that is what a reader and
        // Swagger UI actually see.
        Assert.Multiple((Action)(() =>
        {
            Assert.That(document["example"]?.ToJsonString(), Is.EqualTo("""{"name":"New Name"}"""));
            Assert.That(document.ContainsKey("examples"), Is.False);
        }));
    }

    [Test]
    public void PatchSchema_IsNamedByTheSchemaIdOptionWhenSupplied()
    {
        var (_, repository) = GeneratePatchSchema(options => options.SchemaId = type => $"{type.Name}Patch");

        Assert.Multiple((Action)(() =>
        {
            Assert.That(repository.Schemas, Does.ContainKey("PersonTestModelPatch"));
            Assert.That(repository.Schemas, Does.Not.ContainKey(DefaultPatchSchemaId));
            Assert.That(repository.Schemas, Does.ContainKey(nameof(PersonTestModel)),
                "The schema id override must only apply to patch bodies.");
        }));
    }

    [Test]
    public void PatchSchema_LeavesTheDocumentFreeOfOptionalSchemas()
    {
        var (_, repository) = GeneratePatchSchema();

        Assert.That(repository.Schemas.Keys.Where(id => id.Contains("Optional", StringComparison.Ordinal)),
            Is.Empty,
            "Resolving the patchable property names must not leak Optional<T> components into the document.");
    }

    private static (IOpenApiSchema PatchSchema, SchemaRepository Repository) GeneratePatchSchema(
        Action<SimplePatchSchemaOptions>? configure = null,
        string patchSchemaId = DefaultPatchSchemaId)
    {
        var services = new ServiceCollection();
        services.AddSwaggerGen(options => options.AddSimplePatchSchemas(configure));

        using var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<ISchemaGenerator>();

        var repository = new SchemaRepository();
        generator.GenerateSchema(typeof(IPatchObject<PersonTestModel>), repository);

        var id = repository.Schemas.ContainsKey(patchSchemaId)
            ? patchSchemaId
            : repository.Schemas.Keys.Single(key => key != nameof(PersonTestModel));
        return (repository.Schemas[id], repository);
    }
}
