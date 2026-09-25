using System.Net.Http.Json;
using System.Text.Json;

namespace Tashkeela.IntegrationTests;

/// <summary>The OpenAPI document is generated at runtime, so a broken transformer only shows up here.</summary>
public sealed class ApiDocumentationTests(ApiFactory factory)
{
    [Fact]
    public async Task The_openapi_document_is_public_and_describes_the_contract()
    {
        var doc = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);

        doc.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _).ShouldBeTrue();
        var eventRequest = doc.GetProperty("components").GetProperty("schemas").GetProperty("EventRequest");
        eventRequest.GetProperty("properties").GetProperty("title").GetProperty("maxLength").GetInt32().ShouldBe(100);
        eventRequest.GetProperty("required").EnumerateArray().Select(e => e.GetString())
            .ShouldBe(["type", "title", "startsAt", "endsAt", "location"], ignoreOrder: true);
    }
}
