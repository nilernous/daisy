using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Daisy.Mvc.Models.Api;

namespace Daisy.Mvc.Services;

/// <summary>Raised for every non-success API response; carries the standard error body (API_DESIGN §4.1).</summary>
public sealed class ApiException : Exception
{
    public ApiException(HttpStatusCode status, ApiError? error)
        : base(error?.Message ?? $"The server returned {(int)status}.")
    {
        Status = status;
        Error = error;
    }

    public HttpStatusCode Status { get; }

    public ApiError? Error { get; }

    public string Code => Error?.Code ?? string.Empty;

    public IReadOnlyDictionary<string, string[]> FieldErrors =>
        Error?.Errors ?? new Dictionary<string, string[]>();

    /// <summary>Reads a value from error.details, e.g. "newTotal" or "reason".</summary>
    public T? Detail<T>(string name) =>
        Error?.Details is { ValueKind: JsonValueKind.Object } details && details.TryGetProperty(name, out var value)
            ? value.Deserialize<T>(ApiClient.Json)
            : default;
}

/// <summary>
/// HttpClient wrapper used by every API service of Daisy.Mvc (ARCHITECTURE §25).
/// The Bearer token and X-Cart-Session header are added by <see cref="ApiAuthHandler"/>.
/// </summary>
public sealed class ApiClient
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _http;

    public ApiClient(HttpClient http)
    {
        _http = http;
    }

    public Task<T> GetAsync<T>(string url, CancellationToken ct = default) =>
        SendAsync<T>(new HttpRequestMessage(HttpMethod.Get, url), ct);

    public Task<T> PostAsync<T>(string url, object? body, CancellationToken ct = default) =>
        SendAsync<T>(WithJson(HttpMethod.Post, url, body), ct);

    public Task PostAsync(string url, object? body, CancellationToken ct = default) =>
        SendAsync<object?>(WithJson(HttpMethod.Post, url, body), ct);

    public Task<T> PutAsync<T>(string url, object body, CancellationToken ct = default) =>
        SendAsync<T>(WithJson(HttpMethod.Put, url, body), ct);

    public Task PutAsync(string url, object? body, CancellationToken ct = default) =>
        SendAsync<object?>(WithJson(HttpMethod.Put, url, body), ct);

    public Task PatchAsync(string url, object? body, CancellationToken ct = default) =>
        SendAsync<object?>(WithJson(HttpMethod.Patch, url, body), ct);

    public Task<T> PatchAsync<T>(string url, object? body, CancellationToken ct = default) =>
        SendAsync<T>(WithJson(HttpMethod.Patch, url, body), ct);

    public Task DeleteAsync(string url, CancellationToken ct = default) =>
        SendAsync<object?>(new HttpRequestMessage(HttpMethod.Delete, url), ct);

    public Task<T> DeleteAsync<T>(string url, CancellationToken ct = default) =>
        SendAsync<T>(new HttpRequestMessage(HttpMethod.Delete, url), ct);

    public Task<T> SendMultipartAsync<T>(HttpMethod method, string url, MultipartFormDataContent content, CancellationToken ct = default) =>
        SendAsync<T>(new HttpRequestMessage(method, url) { Content = content }, ct);

    /// <summary>GET an OData list; the query string is built by the caller with <see cref="ODataQuery"/>.</summary>
    public Task<ODataPage<T>> ODataAsync<T>(string entitySet, ODataQuery query, CancellationToken ct = default) =>
        GetAsync<ODataPage<T>>($"/odata/{entitySet}{query}", ct);

    private static HttpRequestMessage WithJson(HttpMethod method, string url, object? body) =>
        new(method, url) { Content = JsonContent.Create(body ?? new { }, options: Json) };

    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        using (request)
        using (var response = await _http.SendAsync(request, ct))
        {
            if (!response.IsSuccessStatusCode)
            {
                ApiError? error = null;
                try
                {
                    error = await response.Content.ReadFromJsonAsync<ApiError>(Json, ct);
                }
                catch (JsonException)
                {
                    // Not the standard error body (e.g. a proxy error page).
                }

                throw new ApiException(response.StatusCode, error);
            }

            if (response.StatusCode == HttpStatusCode.NoContent || typeof(T) == typeof(object))
            {
                return default!;
            }

            return (await response.Content.ReadFromJsonAsync<T>(Json, ct))!;
        }
    }
}

/// <summary>Builds OData query strings with proper escaping (API_DESIGN §7).</summary>
public sealed class ODataQuery
{
    private readonly List<string> _filters = [];
    private string? _orderBy;
    private int? _top;
    private int? _skip;
    private bool _count;
    private string? _select;

    public ODataQuery Filter(string expression)
    {
        _filters.Add($"({expression})");
        return this;
    }

    public ODataQuery OrderBy(string orderBy)
    {
        _orderBy = orderBy;
        return this;
    }

    public ODataQuery Page(int page, int pageSize)
    {
        _top = pageSize;
        _skip = (Math.Max(1, page) - 1) * pageSize;
        _count = true;
        return this;
    }

    public ODataQuery Top(int top)
    {
        _top = top;
        return this;
    }

    public ODataQuery Select(string fields)
    {
        _select = fields;
        return this;
    }

    /// <summary>OData string literal: single quotes are doubled.</summary>
    public static string Literal(string value) => "'" + value.Replace("'", "''") + "'";

    public override string ToString()
    {
        var parts = new List<string>();
        if (_filters.Count > 0)
        {
            parts.Add("$filter=" + Uri.EscapeDataString(string.Join(" and ", _filters)));
        }

        if (_orderBy is not null)
        {
            parts.Add("$orderby=" + Uri.EscapeDataString(_orderBy));
        }

        if (_top is not null)
        {
            parts.Add("$top=" + _top);
        }

        if (_skip is > 0)
        {
            parts.Add("$skip=" + _skip);
        }

        if (_count)
        {
            parts.Add("$count=true");
        }

        if (_select is not null)
        {
            parts.Add("$select=" + Uri.EscapeDataString(_select));
        }

        return parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts);
    }
}
