using System.Text.Json.Nodes;
using PTrampert.SimplePatch;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        // Configure PTrampert.SimplePatch to work with ASP.NET Core's JSON serialization
        opts.JsonSerializerOptions.AddSimplePatchConverters();
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts =>
{
    // Document IPatchObject<T> request bodies with the patched model's schema.
    opts.AddSimplePatchSchemas(patch =>
    {
        // Worth setting: Swagger UI builds its example from the schema's properties and ignores
        // "required", so without this the PATCH example lists every property — which reads as
        // "send all of these", and is what "Try it out" would submit.
        patch.Example = _ => new JsonObject { ["name"] = "New Name" };
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();