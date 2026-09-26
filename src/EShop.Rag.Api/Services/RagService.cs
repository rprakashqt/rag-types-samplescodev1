using System.Text;
using System.Text.Json;
using EShop.Rag.Api.Models;

namespace EShop.Rag.Api.Services;

public sealed class RagService
{
    private readonly AzureOpenAiService _openAi;
    private readonly AzureSearchService _search;

    public RagService(
        AzureOpenAiService openAi,
        AzureSearchService search)
    {
        _openAi = openAi;
        _search = search;
    }

    public async Task<RagResponse> ClassicAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        var vector = await _openAi.CreateEmbeddingAsync(question, cancellationToken);
        var hits = await _search.SearchAsync(
            question,
            vector,
            top: 5,
            hybrid: false,
            cancellationToken: cancellationToken);

        return await BuildAnswerAsync("Classic Vector RAG", question, hits, null, cancellationToken);
    }

    public async Task<RagResponse> HybridAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        var vector = await _openAi.CreateEmbeddingAsync(question, cancellationToken);
        var hits = await _search.SearchAsync(
            question,
            vector,
            top: 5,
            hybrid: true,
            cancellationToken: cancellationToken);

        return await BuildAnswerAsync("Hybrid RAG", question, hits, null, cancellationToken);
    }

    public async Task<RagResponse> FilteredAsync(
        FilteredRagRequest request,
        CancellationToken cancellationToken = default)
    {
        var vector = await _openAi.CreateEmbeddingAsync(request.Question, cancellationToken);
        var country = EscapeOData(request.Country);
        var category = EscapeOData(request.ProductCategory);

        var filter =
            $"(country eq '{country}' or country eq 'GLOBAL') and " +
            $"(productCategory eq '{category}' or productCategory eq 'All')";

        var hits = await _search.SearchAsync(
            request.Question,
            vector,
            top: 5,
            hybrid: true,
            filter: filter,
            cancellationToken: cancellationToken);

        return await BuildAnswerAsync(
            "Filtered Hybrid RAG",
            request.Question,
            hits,
            new { request.Country, request.ProductCategory, filter },
            cancellationToken);
    }

    public async Task<RagResponse> MultiQueryAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        var prompt = $"""
Return ONLY a JSON array with 2 to 4 focused search queries.
Split the customer's question into independent eShop knowledge searches.
Do not answer the question.

Customer question:
{question}
""";

        var raw = await _openAi.ChatAsync(
            "You are a query-decomposition component for an eShop RAG system.",
            prompt,
            cancellationToken);

        var queries = ParseStringArray(raw);
        if (queries.Count == 0)
            queries.Add(question);

        var allHits = new List<SearchHit>();

        foreach (var query in queries.Take(4))
        {
            var vector = await _openAi.CreateEmbeddingAsync(query, cancellationToken);
            var hits = await _search.SearchAsync(
                query,
                vector,
                top: 4,
                hybrid: true,
                cancellationToken: cancellationToken);

            allHits.AddRange(hits);
        }

        var merged = allHits
            .GroupBy(x => x.Id)
            .Select(g => g.OrderByDescending(x => x.Score).First())
            .OrderByDescending(x => x.Score)
            .Take(10)
            .ToArray();

        return await BuildAnswerAsync(
            "Multi-query RAG",
            question,
            merged,
            new { Queries = queries },
            cancellationToken);
    }

    public async Task<RagResponse> AgenticAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        var plannerPrompt =
            "Plan the retrieval needed to answer the eShop customer question.\n\n" +
            "Available knowledge categories:\nReturns\nWarranty\nShipping\nPromotion\nProduct\n\n" +
            "Return ONLY JSON in this form:\n" +
            "{\n" +
            "  \"reason\": \"short explanation\",\n" +
            "  \"queries\": [\n" +
            "    { \"query\": \"focused search query\", \"category\": \"Returns\" }\n" +
            "  ]\n" +
            "}\n\n" +
            "Use 1 to 4 queries. Category must be one of the listed categories or null.\n\n" +
            "Customer question:\n" +
            question;

        var rawPlan = await _openAi.ChatAsync(
            "You are the retrieval planner for an agentic-style RAG pipeline.",
            plannerPrompt,
            cancellationToken);

        var plan = ParsePlan(rawPlan, question);
        var allHits = new List<SearchHit>();

        foreach (var plannedQuery in plan.Queries.Take(4))
        {
            var vector = await _openAi.CreateEmbeddingAsync(plannedQuery.Query, cancellationToken);
            string? filter = null;

            if (!string.IsNullOrWhiteSpace(plannedQuery.Category))
            {
                var category = EscapeOData(plannedQuery.Category!);
                filter = $"category eq '{category}'";
            }

            var hits = await _search.SearchAsync(
                plannedQuery.Query,
                vector,
                top: 4,
                hybrid: true,
                filter: filter,
                cancellationToken: cancellationToken);

            allHits.AddRange(hits);
        }

        var merged = allHits
            .GroupBy(x => x.Id)
            .Select(g => g.OrderByDescending(x => x.Score).First())
            .OrderByDescending(x => x.Score)
            .Take(10)
            .ToArray();

        return await BuildAnswerAsync(
            "Agentic-style RAG",
            question,
            merged,
            plan,
            cancellationToken);
    }

    private async Task<RagResponse> BuildAnswerAsync(
        string type,
        string question,
        IReadOnlyCollection<SearchHit> hits,
        object? diagnostics,
        CancellationToken cancellationToken)
    {
        if (hits.Count == 0)
        {
            return new RagResponse(
                type,
                "No relevant eShop knowledge was retrieved.",
                Array.Empty<string>(),
                diagnostics);
        }

        var context = new StringBuilder();

        foreach (var hit in hits)
        {
            context.AppendLine($"SOURCE: {hit.Source}");
            context.AppendLine($"TITLE: {hit.Title}");
            context.AppendLine($"CATEGORY: {hit.Category}");
            context.AppendLine(hit.Content);
            context.AppendLine();
        }

        var userPrompt = $"""
Use only the supplied eShop knowledge to answer the customer.

Rules:
1. Do not invent company policy.
2. If the evidence is insufficient, say what is missing.
3. Distinguish policy guidance from live order facts.
4. Include source file names in the answer.
5. Keep the answer clear and actionable.

KNOWLEDGE:
{context}

CUSTOMER QUESTION:
{question}
""";

        var answer = await _openAi.ChatAsync(
            "You are a grounded eShop support assistant.",
            userPrompt,
            cancellationToken);

        var sources = hits
            .Select(x => x.Source)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new RagResponse(type, answer, sources, diagnostics);
    }

    private static List<string> ParseStringArray(string raw)
    {
        try
        {
            var json = ExtractJson(raw, '[', ']');
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static AgenticPlan ParsePlan(string raw, string fallbackQuestion)
    {
        try
        {
            var json = ExtractJson(raw, '{', '}');
            return JsonSerializer.Deserialize<AgenticPlan>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? FallbackPlan(fallbackQuestion);
        }
        catch
        {
            return FallbackPlan(fallbackQuestion);
        }
    }

    private static AgenticPlan FallbackPlan(string question) =>
        new(
            "Planner output could not be parsed; using the original question.",
            new[] { new AgenticQuery(question, null) });

    private static string ExtractJson(string raw, char start, char end)
    {
        var first = raw.IndexOf(start);
        var last = raw.LastIndexOf(end);

        if (first < 0 || last < first)
            throw new FormatException("JSON payload not found.");

        return raw[first..(last + 1)];
    }

    private static string EscapeOData(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);
}
