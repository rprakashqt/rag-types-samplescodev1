using EShop.Rag.Api.Models;
using EShop.Rag.Api.Options;
using EShop.Rag.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var openAiOptions =
    builder.Configuration.GetSection(AzureOpenAiOptions.SectionName)
        .Get<AzureOpenAiOptions>() ?? new AzureOpenAiOptions();

var searchOptions =
    builder.Configuration.GetSection(AzureSearchOptions.SectionName)
        .Get<AzureSearchOptions>() ?? new AzureSearchOptions();

builder.Services.AddSingleton(openAiOptions);
builder.Services.AddSingleton(searchOptions);

builder.Services.AddHttpClient<AzureOpenAiService>();
builder.Services.AddHttpClient<AzureSearchService>();

builder.Services.AddScoped<KnowledgeInitializer>();
builder.Services.AddScoped<RagService>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    name = "eShop RAG Types Samples",
    framework = ".NET 8 / ASP.NET Core",
    ragTypes = new[]
    {
        "Classic Vector RAG",
        "Hybrid RAG",
        "Filtered Hybrid RAG",
        "Multi-query RAG",
        "Agentic-style RAG"
    }
}));

app.MapPost(
    "/api/knowledge/initialize",
    async (KnowledgeInitializer initializer, CancellationToken ct) =>
    {
        var count = await initializer.InitializeAsync(ct);
        return Results.Ok(new
        {
            message = "Azure AI Search index initialized.",
            documentsIndexed = count
        });
    });

app.MapPost(
    "/api/rag/classic",
    async (RagRequest request, RagService rag, CancellationToken ct) =>
        Results.Ok(await rag.ClassicAsync(request.Question, ct)));

app.MapPost(
    "/api/rag/hybrid",
    async (RagRequest request, RagService rag, CancellationToken ct) =>
        Results.Ok(await rag.HybridAsync(request.Question, ct)));

app.MapPost(
    "/api/rag/filtered",
    async (FilteredRagRequest request, RagService rag, CancellationToken ct) =>
        Results.Ok(await rag.FilteredAsync(request, ct)));

app.MapPost(
    "/api/rag/multi-query",
    async (RagRequest request, RagService rag, CancellationToken ct) =>
        Results.Ok(await rag.MultiQueryAsync(request.Question, ct)));

app.MapPost(
    "/api/rag/agentic",
    async (RagRequest request, RagService rag, CancellationToken ct) =>
        Results.Ok(await rag.AgenticAsync(request.Question, ct)));

app.Run();
