using System.Net;
using System.Text.Json;

namespace Etamid.Api.IntegrationTests;

public class ApiFoundationTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static async Task<JsonElement> Json(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    [Fact]
    public async Task Health_returns_200()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ping_returns_the_envelope_in_english_by_default()
    {
        var response = await _client.GetAsync("/api/v1/system/ping");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await Json(response);
        body.GetProperty("success").GetBoolean().Should().BeTrue();
        body.GetProperty("message").GetString().Should().Be("Service is running.");
        body.GetProperty("data").GetProperty("status").GetString().Should().Be("ok");
        body.TryGetProperty("errors", out _).Should().BeFalse();
        body.TryGetProperty("meta", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Ping_is_localized_when_arabic_is_requested()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/ping");
        request.Headers.Add("Accept-Language", "ar");

        var body = await Json(await _client.SendAsync(request));

        body.GetProperty("message").GetString().Should().Be("الخدمة تعمل.");
    }

    [Fact]
    public async Task Unknown_route_returns_404_with_the_envelope()
    {
        var response = await _client.GetAsync("/api/v1/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await Json(response);
        body.GetProperty("success").GetBoolean().Should().BeFalse();
        body.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task Every_response_carries_a_correlation_id()
    {
        var response = await _client.GetAsync("/api/v1/system/ping");
        response.Headers.Contains("X-Correlation-Id").Should().BeTrue();
    }
}
