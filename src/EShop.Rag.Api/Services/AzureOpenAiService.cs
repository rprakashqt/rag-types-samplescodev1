using System.Net.Http.Json;
using System.Text.Json;
using EShop.Rag.Api.Options;

namespace EShop.Rag.Api.Services;

public sealed class AzureOpenAiService
{
    private readonly HttpClient _http;
    private readonly AzureOpenAiOptions _options;

    public AzureOpenAiService(HttpClient http, AzureOpenAiOptions options)
    {
        _http = http;
        _options = options;
    }

    public async Task<float[]> CreateEmbeddingAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_options.Endpoint.TrimEnd('/')}/openai/v1/embeddings");

        request.Headers.Add("api-key", _options.ApiKey);
        request.Content = JsonContent.Create(new
        {
            model = _options.EmbeddingDeployment,
            input,
            dimensions = _options.EmbeddingDimensions
        });

        using var response = await _http.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Azure OpenAI embeddings request failed: {json}");

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("data")[0]
            .GetProperty("embedding")
            .EnumerateArray()
            .Select(x => x.GetSingle())
            .ToArray();
    }

    public async Task<string> ChatAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_options.Endpoint.TrimEnd('/')}/openai/v1/chat/completions");

        request.Headers.Add("api-key", _options.ApiKey);
        request.Content = JsonContent.Create(new
        {
            model = _options.ChatDeployment,
            temperature = 0.1,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        });

        using var response = await _http.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Azure OpenAI chat request failed: {json}");

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint) ||
            string.IsNullOrWhiteSpace(_options.ApiKey) ||
            string.IsNullOrWhiteSpace(_options.ChatDeployment) ||
            string.IsNullOrWhiteSpace(_options.EmbeddingDeployment))
        {
            throw new InvalidOperationException(
                "AzureOpenAI configuration is incomplete. Configure Endpoint, ApiKey, ChatDeployment and EmbeddingDeployment.");
        }
    }
}
