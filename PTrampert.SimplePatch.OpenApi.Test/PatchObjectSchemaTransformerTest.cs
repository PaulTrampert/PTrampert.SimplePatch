using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using PTrampert.SimplePatch.OpenApi.Test.TestObjects;

namespace PTrampert.SimplePatch.OpenApi.Test;

public class PatchObjectSchemaTransformerTest
{
    private const string DefaultPatchSchemaId = "IPatchObjectOfPersonTestModel";

    [Test]
    public async Task PatchSchema_DescribesThePatchedModelsProperties()
    {
        var (patchSchema, _) = await GeneratePatchSchemaAsync();

        Assert.That(patchSchema.Properties?.Keys,
            Is.EquivalentTo(new[] { "name", "dateOfBirth", "email", "nick_name" }));
    }

    [Test]
    public async Task PatchSchema_KeepsTheValidationConstraintsOfThePatchedModel()
    {
        var (patchSchema, _) = await GeneratePatchSchemaAsync();

        var name = patchSchema.Properties!["name"];
        Assert.Multiple((Action)(() =>
        {
            Assert.That(name.MaxLength, Is.EqualTo(255));
            Assert.That(name.MinLength, Is.EqualTo(3));
        }));
    }

    [Test]
    public async Task PatchSchema_MakesEveryPropertyOptional()
    {
        var (patchSchema, document) = await GeneratePatchSchemaAsync();

        Assert.Multiple((Action)(() =>
        {
            Assert.That(patchSchema.Required, Is.Null.Or.Empty);
            Assert.That(document.Components!.Schemas![nameof(PersonTestModel)].Required, Does.Contain("name"),
                "The patched model's own schema must keep its required properties — only the patch body is partial.");
        }));
    }

    [Test]
    public async Task PatchSchema_OmitsPropertiesThePatchObjectCannotSet()
    {
        var (patchSchema, _) = await GeneratePatchSchemaAsync();

        Assert.Multiple((Action)(() =>
        {
            Assert.That(patchSchema.Properties, Does.Not.ContainKey("display"),
                "Get-only properties are not patchable.");
            Assert.That(patchSchema.Properties, Does.Not.ContainKey("secret"),
                "[JsonIgnore] properties are not patchable.");
        }));
    }

    [Test]
    public async Task PatchSchema_DescribesItselfAsAPartialUpdate()
    {
        var (patchSchema, _) = await GeneratePatchSchemaAsync();

        Assert.That(patchSchema.Description,
            Is.EqualTo("Partial update of PersonTestModel. Omitted properties are left unchanged."));
    }

    [Test]
    public async Task PatchSchema_UsesTheSuppliedExample()
    {
        var (patchSchema, _) = await GeneratePatchSchemaAsync(options =>
            options.Example = _ => new JsonObject { ["name"] = "New Name" });

        Assert.That(patchSchema.Example?.ToJsonString(), Is.EqualTo("""{"name":"New Name"}"""));
    }

    [Test]
    public async Task PatchSchema_IsNamedByTheSchemaIdOptionWhenSupplied()
    {
        var (_, document) = await GeneratePatchSchemaAsync(
            options => options.SchemaId = type => $"{type.Name}Patch",
            patchSchemaId: "PersonTestModelPatch");

        Assert.Multiple((Action)(() =>
        {
            Assert.That(document.Components!.Schemas!, Does.ContainKey("PersonTestModelPatch"));
            Assert.That(document.Components!.Schemas!, Does.Not.ContainKey(DefaultPatchSchemaId));
            Assert.That(document.Components!.Schemas!, Does.ContainKey(nameof(PersonTestModel)),
                "The schema id override must only apply to patch bodies.");
        }));
    }

    [Test]
    public async Task PutBody_IsLeftAlone()
    {
        var (_, document) = await GeneratePatchSchemaAsync();

        var put = document.Paths!["/people/{id}"].Operations![HttpMethod.Put];
        var putBody = put.RequestBody!.Content!["application/json"].Schema!;

        Assert.That(putBody, Is.InstanceOf<OpenApiSchemaReference>()
            .With.Property(nameof(OpenApiSchemaReference.Reference)).Property("Id").EqualTo(nameof(PersonTestModel)));
    }

    private static async Task<(IOpenApiSchema PatchSchema, OpenApiDocument Document)> GeneratePatchSchemaAsync(
        Action<SimplePatchSchemaOptions>? configure = null,
        string patchSchemaId = DefaultPatchSchemaId)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.AddSimplePatchConverters());
        builder.Services.AddOpenApi(options => options.AddSimplePatchSchemas(configure));

        await using var app = builder.Build();
        app.MapPatch("/people/{id:int}", (int id, IPatchObject<PersonTestModel> patch) => Results.Ok());
        app.MapPut("/people/{id:int}", (int id, PersonTestModel person) => Results.Ok());

        // Endpoints only reach the document generator once the app has started.
        await app.StartAsync();

        // AddOpenApi registers one provider per document, keyed by document name.
        var document = await app.Services
            .GetRequiredKeyedService<IOpenApiDocumentProvider>("v1")
            .GetOpenApiDocumentAsync();

        await app.StopAsync();

        return (document.Components!.Schemas![patchSchemaId], document);
    }
}
