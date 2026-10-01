using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
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
    public void ServiceCollectionOverload_DescribesThePatchedModelsProperties()
    {
        var (patchSchema, _) = GeneratePatchSchema(services =>
        {
            services.AddSwaggerGen();
            services.AddSimplePatchSchemas();
        });

        Assert.That(patchSchema.Properties?.Keys,
            Is.EquivalentTo(new[] { "name", "dateOfBirth", "email", "nick_name" }));
    }

    [Test]
    public void ServiceCollectionOverload_AppliesSchemaIdOverACustomSelectorSetAfterwards()
    {
        var (_, repository) = GeneratePatchSchema(services =>
        {
            services.AddSimplePatchSchemas(options => options.SchemaId = type => $"{type.Name}Patch");
            services.AddSwaggerGen(options => options.CustomSchemaIds(AppSchemaId));
        });

        Assert.Multiple((Action)(() =>
        {
            Assert.That(repository.Schemas, Does.ContainKey("PersonTestModelPatch"));
            Assert.That(repository.Schemas, Does.Not.ContainKey("IPatchObjectOfPersonTestModel"));
            Assert.That(repository.Schemas, Does.ContainKey(nameof(PersonTestModel)),
                "The application's own selector must still name everything else.");
        }));
    }

    [Test]
    public void ServiceCollectionOverload_AppliesSchemaIdOverACustomSelectorSetBefore()
    {
        var (_, repository) = GeneratePatchSchema(services =>
        {
            services.AddSwaggerGen(options => options.CustomSchemaIds(AppSchemaId));
            services.AddSimplePatchSchemas(options => options.SchemaId = type => $"{type.Name}Patch");
        });

        Assert.That(repository.Schemas, Does.ContainKey("PersonTestModelPatch"));
    }

    // An application's own selector. Not just type.Name: that gives every Optional<T> the same id,
    // and resolving the patchable property names generates those.
    private static string AppSchemaId(Type type) =>
        type.IsConstructedGenericType
            ? $"{type.Name[..type.Name.IndexOf('`')]}Of{string.Join("And", type.GenericTypeArguments.Select(AppSchemaId))}"
            : type.Name;

    [Test]
    public void PatchSchema_LeavesTheDocumentFreeOfOptionalSchemas()
    {
        var (_, repository) = GeneratePatchSchema();

        Assert.That(repository.Schemas.Keys.Where(id => id.Contains("Optional", StringComparison.Ordinal)),
            Is.Empty,
            "Resolving the patchable property names must not leak Optional<T> components into the document.");
    }

    [Test]
    public void PatchSchema_UsesEachGeneratorsOwnNamingPolicy()
    {
        // Two generators in one process, as in a test suite with several hosts. The second must
        // not reuse the property names the first one resolved.
        var (camelSchema, _) = GeneratePatchSchema();
        var (snakeSchema, _) = GeneratePatchSchema(namingPolicy: JsonNamingPolicy.SnakeCaseLower);

        Assert.Multiple((Action)(() =>
        {
            Assert.That(camelSchema.Properties?.Keys,
                Is.EquivalentTo(new[] { "name", "dateOfBirth", "email", "nick_name" }));
            Assert.That(snakeSchema.Properties?.Keys,
                Is.EquivalentTo(new[] { "name", "date_of_birth", "email", "nick_name" }));
        }));
    }

    [Test]
    public void PatchSchema_DescribesInheritedPropertiesWithAllOfForInheritance()
    {
        var (patchSchema, repository) = GenerateDerivedPatchSchema();

        Assert.Multiple((Action)(() =>
        {
            Assert.That(patchSchema.Properties?.Keys,
                Is.EquivalentTo(new[] { "baseProp", "derivedProp" }));
            Assert.That(repository.Schemas[nameof(DerivedTestModel)].AllOf, Is.Not.Empty,
                "Guard: the derived model's schema is an allOf rather than a flat property list.");
        }));
    }

    [Test]
    public void PatchSchema_KeepsInheritedRequiredWhenClearRequiredIsOff()
    {
        var (patchSchema, _) = GenerateDerivedPatchSchema(options => options.ClearRequired = false);

        Assert.That(patchSchema.Required, Is.EquivalentTo(new[] { "baseProp", "derivedProp" }));
    }

    private static (IOpenApiSchema PatchSchema, SchemaRepository Repository) GenerateDerivedPatchSchema(
        Action<SimplePatchSchemaOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSwaggerGen(options =>
        {
            options.UseAllOfForInheritance();
            options.AddSimplePatchSchemas(configure);
        });

        using var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<ISchemaGenerator>();

        var repository = new SchemaRepository();
        generator.GenerateSchema(typeof(IPatchObject<DerivedTestModel>), repository);
        return (repository.Schemas["DerivedTestModelIPatchObject"], repository);
    }

    private static (IOpenApiSchema PatchSchema, SchemaRepository Repository) GeneratePatchSchema(
        Action<SimplePatchSchemaOptions>? configure = null,
        string patchSchemaId = DefaultPatchSchemaId,
        JsonNamingPolicy? namingPolicy = null) =>
        GeneratePatchSchema(services =>
            {
                if (namingPolicy is not null)
                {
                    services.Configure<JsonOptions>(options => options.JsonSerializerOptions.PropertyNamingPolicy = namingPolicy);
                }

                services.AddSwaggerGen(options => options.AddSimplePatchSchemas(configure));
            },
            patchSchemaId);

    private static (IOpenApiSchema PatchSchema, SchemaRepository Repository) GeneratePatchSchema(
        Action<IServiceCollection> register,
        string patchSchemaId = DefaultPatchSchemaId)
    {
        var services = new ServiceCollection();
        register(services);

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
