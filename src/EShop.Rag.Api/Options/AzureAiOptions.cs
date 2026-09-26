namespace EShop.Rag.Api.Options;

public sealed class AzureOpenAiOptions
{
    public const string SectionName = "AzureOpenAI";

    public string Endpoint { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string ChatDeployment { get; init; } = string.Empty;
    public string EmbeddingDeployment { get; init; } = string.Empty;
    public int EmbeddingDimensions { get; init; } = 1536;
}

public sealed class AzureSearchOptions
{
    public const string SectionName = "AzureSearch";

    public string Endpoint { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string IndexName { get; init; } = "eshop-knowledge";
    public string ApiVersion { get; init; } = "2026-04-01";
}
