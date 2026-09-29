# eShop RAG Types Samples (.NET)

A practical learning repository for implementing Retrieval-Augmented Generation (RAG) in an eShop/order-management scenario with ASP.NET Core, C#, Azure OpenAI, and Azure AI Search.

## Implemented RAG patterns

1. Classic Vector RAG
2. Hybrid RAG
3. Filtered Hybrid RAG
4. Multi-query RAG
5. Agentic-style RAG

## Mental model

RAG answers: What does our knowledge say?

MCP answers: Which external capabilities can an AI client call?

Agentic AI answers: Which knowledge and tools should I use, and in what order, to complete the goal?

This repository focuses on the RAG layer. A later extension can expose Order, Inventory, Payment, and Shipping application services as MCP tools.

## Architecture

    Angular / Mobile / API client
                 |
                 v
          ASP.NET Core API
                 |
          RAG orchestration
                 |
      +----------+-----------+
      |                      |
      v                      v
 Azure OpenAI          Azure AI Search
 Chat + Embeddings     Vector / Hybrid /
      |                Filtered retrieval
      +----------+-----------+
                 |
                 v
       Grounded response
       + source filenames

Important boundary:

    Policy / manual / FAQ        -> RAG
    Live order status            -> Order Service
    Live inventory               -> Inventory Service
    Payment state                -> Payment Service
    Shipment tracking            -> Shipping Service

Do not use a vector database as the system of record for live transactional state.

## Current Azure APIs used

- Azure OpenAI / Microsoft Foundry OpenAI-compatible v1 endpoints:
  - POST /openai/v1/chat/completions
  - POST /openai/v1/embeddings
- Azure AI Search stable REST API:
  - api-version=2026-04-01

The Azure AI Search implementation sends a raw vector in vectorQueries with kind=vector. Hybrid RAG adds normal search text in the same request so Azure AI Search can combine lexical and vector retrieval.

## Project structure

    rag-types-samplescodev1/
    |
    +-- src/EShop.Rag.Api/
    |   +-- Data/Policies/
    |   +-- Models/
    |   +-- Options/
    |   +-- Services/
    |   |   +-- AzureOpenAiService.cs
    |   |   +-- AzureSearchService.cs
    |   |   +-- KnowledgeInitializer.cs
    |   |   +-- RagService.cs
    |   +-- EShop.Rag.Api.http
    |   +-- Program.cs
    |   +-- appsettings.json
    |   +-- EShop.Rag.Api.csproj
    |
    +-- .github/workflows/ci.yml
    +-- Dockerfile
    +-- EShop.Rag.sln
    +-- README.md

## Prerequisites

- .NET 8 SDK
- Azure OpenAI resource
- A chat model deployment
- An embedding model deployment
- Azure AI Search service
- Azure AI Search admin key for this learning sample

The default embedding dimension is 1536. If your embedding deployment uses another dimension, update AzureOpenAI:EmbeddingDimensions before initializing the search index.

## Configuration

Do not commit real keys.

Use environment variables:

    AzureOpenAI__Endpoint=https://YOUR-RESOURCE.openai.azure.com
    AzureOpenAI__ApiKey=YOUR_AZURE_OPENAI_KEY
    AzureOpenAI__ChatDeployment=YOUR_CHAT_DEPLOYMENT
    AzureOpenAI__EmbeddingDeployment=YOUR_EMBEDDING_DEPLOYMENT
    AzureOpenAI__EmbeddingDimensions=1536

    AzureSearch__Endpoint=https://YOUR-SERVICE.search.windows.net
    AzureSearch__ApiKey=YOUR_SEARCH_ADMIN_KEY
    AzureSearch__IndexName=eshop-knowledge

Or use .NET user secrets:

    dotnet user-secrets set "AzureOpenAI:Endpoint" "https://YOUR-RESOURCE.openai.azure.com" --project src/EShop.Rag.Api
    dotnet user-secrets set "AzureOpenAI:ApiKey" "YOUR_KEY" --project src/EShop.Rag.Api
    dotnet user-secrets set "AzureOpenAI:ChatDeployment" "YOUR_CHAT_DEPLOYMENT" --project src/EShop.Rag.Api
    dotnet user-secrets set "AzureOpenAI:EmbeddingDeployment" "YOUR_EMBEDDING_DEPLOYMENT" --project src/EShop.Rag.Api
    dotnet user-secrets set "AzureSearch:Endpoint" "https://YOUR-SERVICE.search.windows.net" --project src/EShop.Rag.Api
    dotnet user-secrets set "AzureSearch:ApiKey" "YOUR_SEARCH_ADMIN_KEY" --project src/EShop.Rag.Api

For production, replace API keys with Managed Identity / Microsoft Entra ID wherever possible.

## Run locally

    dotnet restore
    dotnet run --project src/EShop.Rag.Api

### Swagger API testing

The application now includes Swagger UI through Swashbuckle.

When started locally, open:

    http://localhost:5000/swagger

Visual Studio / dotnet launch settings are configured to open Swagger automatically.

Recommended Swagger testing order:

1. Expand **Knowledge**
2. Execute `POST /api/knowledge/initialize`
3. Confirm that the sample policy documents were indexed
4. Expand **RAG**
5. Use **Try it out** on each RAG endpoint

Classic Vector RAG:

    POST /api/rag/classic

    {
      "question": "Can I return a laptop after 20 days?"
    }

Hybrid RAG:

    POST /api/rag/hybrid

    {
      "question": "What warranty applies to SKU-XPS-9530?"
    }

Filtered RAG:

    POST /api/rag/filtered

    {
      "question": "Can I return this laptop after 20 days?",
      "country": "IN",
      "productCategory": "Laptop"
    }

Multi-query RAG:

    POST /api/rag/multi-query

    {
      "question": "My laptop arrived damaged during the festival offer and its warranty expires next month. Can I return it or should I use warranty?"
    }

Agentic-style RAG:

    POST /api/rag/agentic

    {
      "question": "My laptop arrived damaged, delivery was late, and I bought it during a festival promotion. What policies should be considered?"
    }

The `.http` file remains available for developers who prefer Visual Studio or VS Code HTTP testing, but Swagger is the primary interactive testing UI for this sample.

## Step 1 - initialize knowledge

Call:

    POST /api/knowledge/initialize

Flow:

    Policy text files
          |
          v
    Azure OpenAI Embeddings
          |
          v
    float[] vectors
          |
          v
    Azure AI Search index
          |
          v
    Metadata + text + vector

## Classic Vector RAG

Endpoint:

    POST /api/rag/classic

Example question:

    Can I return a laptop after 20 days?

Use this for semantic knowledge questions when exact keywords are not essential.

## Hybrid RAG

Endpoint:

    POST /api/rag/hybrid

Example:

    What warranty applies to SKU-XPS-9530?

Hybrid RAG sends both the exact text query and an embedding vector. This is useful for SKU values, model names, promotion codes, and natural-language intent.

## Filtered RAG

Endpoint:

    POST /api/rag/filtered

Example request:

    {
      "question": "Can I return this laptop after 20 days?",
      "country": "IN",
      "productCategory": "Laptop"
    }

The service applies metadata filtering before retrieval so US policy does not accidentally ground an India answer.

Production filters commonly include tenant ID, entitlement, country, business unit, product category, classification, and effective date.

## Multi-query RAG

Endpoint:

    POST /api/rag/multi-query

The LLM first decomposes a complex user question into 2-4 focused knowledge searches. Each query uses Hybrid RAG, the results are deduplicated, and the final model answers from the combined evidence.

Use this when one user question contains several independent knowledge needs.

## Agentic-style RAG

Endpoint:

    POST /api/rag/agentic

The retrieval planner chooses among these knowledge categories:

- Returns
- Warranty
- Shipping
- Promotion
- Product

It creates a small retrieval plan and executes category-scoped Hybrid RAG queries.

This sample is intentionally called Agentic-style RAG. It demonstrates application-level planning and retrieval orchestration; it does not claim to implement every capability of Azure AI Search native agentic retrieval.

## Which RAG should you use?

Use Classic Vector RAG when the question is mainly semantic and one knowledge collection is enough.

Use Hybrid RAG when exact terms and semantic meaning both matter.

Use Filtered RAG when country, tenant, department, product, or authorization boundaries must constrain retrieval.

Use Multi-query RAG when one question contains multiple subquestions.

Use Agentic-style RAG when the system should decide which knowledge areas need investigation before retrieval.

Do not introduce an agent for simple deterministic questions just because it is available.

## Production improvements

This project is intentionally readable. For production, add:

1. Managed Identity / Entra ID authentication
2. Document chunking instead of one vector per whole file
3. Chunk overlap and parent-document metadata
4. Semantic ranking or reranking where appropriate
5. Retrieval score thresholds
6. Prompt-injection defenses for retrieved content
7. Tenant/security filters
8. OpenTelemetry and Application Insights
9. Token, latency, and retrieval diagnostics
10. RAG evaluation for groundedness, relevance, and retrieval recall
11. Caching where safe
12. Retry, timeout, and rate-limit handling
13. Content lifecycle/versioning and effective-date filters

## Next architecture step: RAG + MCP + Agent

    Customer request
          |
          v
       AI Agent
       /      \
      /        \
     v          v
    RAG        MCP tools
    |          |
    |          +--> GetOrderStatus
    |          +--> CheckInventory
    |          +--> GetShippingETA
    |          +--> CreateReturn
    |          +--> CancelOrder
    |
    +--> Return policy
    +--> Warranty
    +--> Promotions
    +--> Product knowledge

RAG supplies knowledge. MCP exposes controlled capabilities. The agent decides which knowledge and tools to use.

## Safety and architecture principle

The LLM should not own core business invariants.

Use:

    Agent -> CancelOrder tool -> Application Service -> Domain Rules -> Database

Do not use:

    Agent -> direct database update

Keep pricing, payment, stock reservation, refund eligibility, authorization, and audit rules in deterministic application/domain services.

## Docker

Build:

    docker build -t eshop-rag-samples .

## CI

.github/workflows/ci.yml restores, builds, and verifies the Docker image on pushes and pull requests to main.

## Sample-data notice

All policy and product text under Data/Policies is fictional demonstration content for learning this architecture. It is not a real retailer policy.
