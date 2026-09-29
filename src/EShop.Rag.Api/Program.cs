using EShop.Rag.Api.Models;
using EShop.Rag.Api.Options;
using EShop.Rag.Api.Services;
using Microsoft.OpenApi.Models;

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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "eShop RAG Types API",
            Version = "v1",
            Description =
                "Interactive API for learning and testing Classic Vector RAG, " +
                "Hybrid RAG, Filtered RAG, Multi-query RAG and Agentic-style RAG " +
                "with ASP.NET Core, Azure OpenAI and Azure AI Search."
        });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "eShop RAG Types API v1");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "eShop RAG API - Swagger";
    options.DisplayRequestDuration();
    options.EnableTryItOutByDefault();
});

app.MapGet("/", () => Results.Ok(new
{
    name = "eShop RAG Types Samples",
    framework = ".NET 8 / ASP.NET Core",
    swagger = "/swagger",
    ragTypes = new[]
    {
        "Classic Vector RAG",
        "Hybrid RAG",
        "Filtered Hybrid RAG",
        "Multi-query RAG",
        "Agentic-style RAG"
    }
}))
.WithName("GetApiInfo")
.WithTags("System");

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
    })
.WithName("InitializeKnowledge")
.WithTags("Knowledge");

app.MapPost(
    "/api/rag/classic",
    async (RagRequest request, RagService rag, CancellationToken ct) =>
        Results.Ok(await rag.ClassicAsync(request.Question, ct)))
.WithName("ClassicVectorRag")
.WithTags("RAG");

app.MapPost(
    "/api/rag/hybrid",
    async (RagRequest request, RagService rag, CancellationToken ct) =>
        Results.Ok(await rag.HybridAsync(request.Question, ct)))
.WithName("HybridRag")
.WithTags("RAG");

app.MapPost(
    "/api/rag/filtered",
    async (FilteredRagRequest request, RagService rag, CancellationToken ct) =>
        Results.Ok(await rag.FilteredAsync(request, ct)))
.WithName("FilteredRag")
.WithTags("RAG");

app.MapPost(
    "/api/rag/multi-query",
    async (RagRequest request, RagService rag, CancellationToken ct) =>
        Results.Ok(await rag.MultiQueryAsync(request.Question, ct)))
.WithName("MultiQueryRag")
.WithTags("RAG");

app.MapPost(
    "/api/rag/agentic",
    async (RagRequest request, RagService rag, CancellationToken ct) =>
        Results.Ok(await rag.AgenticAsync(request.Question, ct)))
.WithName("AgenticStyleRag")
.WithTags("RAG");

app.Run();
