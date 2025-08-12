using PTrampert.Optionals;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        // Configure PTrampert.Optionals to work with ASP.NET Core's JSON serialization
        opts.JsonSerializerOptions.Converters.Add(new OptionalJsonConverterFactory());
        opts.JsonSerializerOptions.Converters.Add(new PatchJsonConverterFactory());
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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