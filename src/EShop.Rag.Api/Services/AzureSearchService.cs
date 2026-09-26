using System.Net.Http.Json;
using System.Text.Json;
using EShop.Rag.Api.Models;
using EShop.Rag.Api.Options;

namespace EShop.Rag.Api.Services;

public sealed class AzureSearchService
{
    private readonly HttpClient _http;
    private readonly AzureSearchOptions _options;
    private readonly AzureOpenAiOptions _openAiOptions;

    public AzureSearchService(
        HttpClient http,
        AzureSearchOptions options,
        AzureOpenAiOptions openAiOptions)
    {
        _http = http;
        _options = options;
        _openAiOptions = openAiOptions;
    }

    public async Task CreateOrUpdateIndexAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var body = new
        {
            name = _options.IndexName,
            fields = new object[]
            {
                new { name = "id", type = "Edm.String", key = true, searchable = false, filterable = true, retrievable = true },
                new { name = "title", type = "Edm.String", searchable = true, filterable = false, retrievable = true },
                new { name = "content", type = "Edm.String", searchable = true, filterable = false, retrievable = true },
                new { name = "category", type = "Edm.String", searchable = true, filterable = true, retrievable = true },
                new { name = "country", type = "Edm.String", searchable = false, filterable = true, retrievable = true },
                new { name = "productCategory", type = "Edm.String", searchable = false, filterable = true, retrievable = true },
                new { name = "source", type = "Edm.String", searchable = false, filterable = false, retrievable = true },
                new
                {
                    name = "contentVector",
                    type = "Collection(Edm.Single)",
                    searchable = true,
                    retrievable = false,
                    dimensions = _openAiOptions.EmbeddingDimensions,
                    vectorSearchProfile = "eshop-hnsw-profile"
                }
            },
            vectorSearch = new
            {
                algorithms = new object[]
                {
                    new
                    {
                        name = "eshop-hnsw",
                        kind = "hnsw",
                        hnswParameters = new { metric = "cosine" }
                    }
                },
                profiles = new object[]
                {
                    new
                    {
                        name = "eshop-hnsw-profile",
                        algorithm = "eshop-hnsw"
                    }
                }
            }
        };

        using var request = CreateRequest(
            HttpMethod.Put,
            $"/indexes('{_options.IndexName}')?api-version={_options.ApiVersion}",
            body);

        request.Headers.TryAddWithoutValidation("Prefer", "return=representation");

        using var response = await _http.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Azure AI Search index creation failed: {json}");
    }

    public async Task UploadAsync(
        IReadOnlyList<KnowledgeDocument> documents,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var values = documents.Select(document => new Dictionary<string, object?>
        {
            ["@search.action"] = "mergeOrUpload",
            ["id"] = document.Id,
            ["title"] = document.Title,
            ["content"] = document.Content,
            ["category"] = document.Category,
            ["country"] = document.Country,
            ["productCategory"] = document.ProductCategory,
            ["source"] = document.Source,
            ["contentVector"] = document.ContentVector
        }).ToArray();

        using var request = CreateRequest(
            HttpMethod.Post,
            $"/indexes('{_options.IndexName}')/docs/search.index?api-version={_options.ApiVersion}",
            new { value = values });

        using var response = await _http.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Azure AI Search document upload failed: {json}");
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(
        string question,
        float[] vector,
        int top = 5,
        bool hybrid = false,
        string? filter = null,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var body = new Dictionary<string, object?>
        {
            ["select"] = "id,title,content,category,country,productCategory,source",
            ["top"] = top,
            ["vectorFilterMode"] = "preFilter",
            ["vectorQueries"] = new object[]
            {
                new
                {
                    kind = "vector",
                    vector,
                    fields = "contentVector",
                    k = Math.Max(top, 10)
                }
            }
        };

        if (hybrid)
            body["search"] = question;

        if (!string.IsNullOrWhiteSpace(filter))
            body["filter"] = filter;

        using var request = CreateRequest(
            HttpMethod.Post,
            $"/indexes('{_options.IndexName}')/docs/search?api-version={_options.ApiVersion}",
            body);

        using var response = await _http.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Azure AI Search query failed: {json}");

        using var doc = JsonDocument.Parse(json);
        var hits = new List<SearchHit>();

        foreach (var item in doc.RootElement.GetProperty("value").EnumerateArray())
        {
            hits.Add(new SearchHit(
                GetString(item, "id"),
                GetString(item, "title"),
                GetString(item, "content"),
                GetString(item, "category"),
                GetString(item, "country"),
                GetString(item, "productCategory"),
                GetString(item, "source"),
                item.TryGetProperty("@search.score", out var score) ? score.GetDouble() : 0));
        }

        return hits;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, object body)
    {
        var request = new HttpRequestMessage(
            method,
            $"{_options.Endpoint.TrimEnd('/')}{path}");

        request.Headers.Add("api-key", _options.ApiKey);
        request.Content = JsonContent.Create(body);
        return request;
    }

    private static string GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var property)
            ? property.GetString() ?? string.Empty
            : string.Empty;

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint) ||
            string.IsNullOrWhiteSpace(_options.ApiKey) ||
            string.IsNullOrWhiteSpace(_options.IndexName))
        {
            throw new InvalidOperationException(
                "AzureSearch configuration is incomplete. Configure Endpoint, ApiKey and IndexName.");
        }
    }
}
