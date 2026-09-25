using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Tashkeela.API.Auth;
using Tashkeela.API.Errors;
using Tashkeela.API.OpenApi;
using Tashkeela.Application;
using Tashkeela.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ---- Layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddSingleton(TimeProvider.System);

// ---- HTTP
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services
    .AddControllers(options =>
        // Validation error keys use JSON names ("startsAt"), matching what clients send.
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider()))
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails(); // RFC 7807 bodies with traceId, for errors and bodiless status codes
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddApiDocumentation();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.MapOpenApi("/swagger/{documentName}/swagger.json").AllowAnonymous();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Tashkeela API v1");
    options.RoutePrefix = "swagger";
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
if (app.Configuration.GetValue<bool>("DevAuth:Enabled"))
{
    app.MapDevTokenEndpoint();
}

await app.RunAsync();

// Exposes the implicit Program class to WebApplicationFactory in integration tests.
public partial class Program;
