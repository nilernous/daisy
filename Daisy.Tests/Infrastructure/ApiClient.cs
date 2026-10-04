using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daisy.Api.Infrastructure;

namespace Daisy.Tests.Infrastructure;

/// <summary>Small JSON helper over HttpClient for the tests.</summary>
public sealed class ApiClient
{
    private static readonly JsonSerializerOptions Json = ApiJson.Options;

    private readonly HttpClient _http;

    public ApiClient(HttpClient http, string? token = null, string? cartSession = null)
    {
        _http = http;
        if (token is not null)
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (cartSession is not null)
        {
            _http.DefaultRequestHeaders.Add(ApiControllerBase.CartSessionHeader, cartSession);
        }
    }

    public async Task<(HttpStatusCode Status, JsonNode? Body)> SendAsync(HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        using var response = await _http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text));
    }

    public Task<(HttpStatusCode Status, JsonNode? Body)> GetAsync(string url) => SendAsync(HttpMethod.Get, url);

    public Task<(HttpStatusCode Status, JsonNode? Body)> PostAsync(string url, object? body = null) => SendAsync(HttpMethod.Post, url, body ?? new { });

    public Task<(HttpStatusCode Status, JsonNode? Body)> PutAsync(string url, object body) => SendAsync(HttpMethod.Put, url, body);

    public Task<(HttpStatusCode Status, JsonNode? Body)> PatchAsync(string url, object body) => SendAsync(HttpMethod.Patch, url, body);

    public Task<(HttpStatusCode Status, JsonNode? Body)> DeleteAsync(string url) => SendAsync(HttpMethod.Delete, url);

    /// <summary>Asserts the status and returns the body.</summary>
    public async Task<JsonNode?> ExpectAsync(HttpStatusCode expected, Task<(HttpStatusCode Status, JsonNode? Body)> call)
    {
        var (status, body) = await call;
        Assert.True(status == expected, $"Expected {(int)expected} but got {(int)status}: {body?.ToJsonString()}");
        return body;
    }
}
