namespace EShop.Rag.Api.Models;

public sealed record RagRequest(string Question);

public sealed record FilteredRagRequest(
    string Question,
    string Country,
    string ProductCategory);

public sealed record RagResponse(
    string Type,
    string Answer,
    IReadOnlyList<string> Sources,
    object? Diagnostics = null);

public sealed record KnowledgeDocument(
    string Id,
    string Title,
    string Content,
    string Category,
    string Country,
    string ProductCategory,
    string Source,
    float[] ContentVector);

public sealed record SearchHit(
    string Id,
    string Title,
    string Content,
    string Category,
    string Country,
    string ProductCategory,
    string Source,
    double Score);

public sealed record AgenticQuery(string Query, string? Category);

public sealed record AgenticPlan(
    string Reason,
    IReadOnlyList<AgenticQuery> Queries);
