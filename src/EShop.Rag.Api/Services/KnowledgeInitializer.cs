using EShop.Rag.Api.Models;

namespace EShop.Rag.Api.Services;

public sealed class KnowledgeInitializer
{
    private readonly IWebHostEnvironment _environment;
    private readonly AzureOpenAiService _openAi;
    private readonly AzureSearchService _search;

    public KnowledgeInitializer(
        IWebHostEnvironment environment,
        AzureOpenAiService openAi,
        AzureSearchService search)
    {
        _environment = environment;
        _openAi = openAi;
        _search = search;
    }

    public async Task<int> InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        await _search.CreateOrUpdateIndexAsync(cancellationToken);

        var definitions = new[]
        {
            new PolicyDefinition("return-policy-in.txt", "India Laptop Return Policy", "Returns", "IN", "Laptop"),
            new PolicyDefinition("return-policy-us.txt", "US Laptop Return Policy", "Returns", "US", "Laptop"),
            new PolicyDefinition("warranty-policy.txt", "Laptop Warranty Policy", "Warranty", "GLOBAL", "Laptop"),
            new PolicyDefinition("shipping-policy-in.txt", "India Shipping Policy", "Shipping", "IN", "All"),
            new PolicyDefinition("festival-promotion-in.txt", "India Festival Promotion Terms", "Promotion", "IN", "All"),
            new PolicyDefinition("product-xps-9530.txt", "SKU-XPS-9530 Product Knowledge", "Product", "GLOBAL", "Laptop")
        };

        var documents = new List<KnowledgeDocument>();

        for (var i = 0; i < definitions.Length; i++)
        {
            var definition = definitions[i];
            var path = Path.Combine(
                _environment.ContentRootPath,
                "Data",
                "Policies",
                definition.FileName);

            var content = await File.ReadAllTextAsync(path, cancellationToken);
            var vector = await _openAi.CreateEmbeddingAsync(content, cancellationToken);

            documents.Add(new KnowledgeDocument(
                $"doc-{i + 1:000}",
                definition.Title,
                content,
                definition.Category,
                definition.Country,
                definition.ProductCategory,
                definition.FileName,
                vector));
        }

        await _search.UploadAsync(documents, cancellationToken);
        return documents.Count;
    }

    private sealed record PolicyDefinition(
        string FileName,
        string Title,
        string Category,
        string Country,
        string ProductCategory);
}
