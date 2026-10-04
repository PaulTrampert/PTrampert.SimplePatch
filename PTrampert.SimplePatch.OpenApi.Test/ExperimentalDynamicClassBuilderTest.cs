using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using PTrampert.SimplePatch.OpenApi.Test.TestObjects;

namespace PTrampert.SimplePatch.OpenApi.Test;

// PatchClassBuilder.UseExperimentalDynamicClassBuilder is process-wide, so these tests must not
// overlap with others, and each one puts it back afterwards.
[NonParallelizable]
public class ExperimentalDynamicClassBuilderTest
{
    [TearDown]
    public void TearDown() => PatchClassBuilder.UseExperimentalDynamicClassBuilder = false;

    [Test]
    public async Task PatchSchema_DescribesAnInternalModelWhenTheFlagIsOn()
    {
        PatchClassBuilder.UseExperimentalDynamicClassBuilder = true;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.AddSimplePatchConverters());
        builder.Services.AddOpenApi(options => options.AddSimplePatchSchemas());

        await using var app = builder.Build();
        app.MapPatch("/people/{id:int}", (int id, IPatchObject<InternalPersonTestModel> patch) => Results.Ok());

        // Endpoints only reach the document generator once the app has started.
        await app.StartAsync();
        var document = await app.Services
            .GetRequiredKeyedService<IOpenApiDocumentProvider>("v1")
            .GetOpenApiDocumentAsync();
        await app.StopAsync();

        var patchSchema = document.Components!.Schemas!["IPatchObjectOfInternalPersonTestModel"];
        Assert.Multiple((Action)(() =>
        {
            Assert.That(patchSchema.Properties?.Keys, Is.EquivalentTo(new[] { "name", "email" }));
            Assert.That(patchSchema.Properties!["name"].MaxLength, Is.EqualTo(255));
        }));
    }
}
