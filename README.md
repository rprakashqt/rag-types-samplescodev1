# eShop RAG Types Samples (.NET)

A practical ASP.NET Core sample demonstrating multiple Retrieval-Augmented Generation (RAG) patterns for an eShop/order-management domain using **C#**, **Azure OpenAI**, and **Azure AI Search**.

## Implemented RAG patterns

1. **Classic Vector RAG**
2. **Hybrid RAG**
3. **Filtered RAG**
4. **Multi-query RAG**
5. **Agentic-style RAG**

## Core architecture rule

Use RAG for enterprise knowledge such as return policies, warranty rules, shipping policies, promotions, FAQs, and product documentation.

Use normal application services/APIs for live transactional data such as order status, payment state, stock, and shipment tracking.

The source code in this repository keeps these boundaries explicit so the architecture is easy to understand and extend later with MCP tools and agentic workflows.
