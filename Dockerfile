FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/EShop.Rag.Api/EShop.Rag.Api.csproj src/EShop.Rag.Api/
RUN dotnet restore src/EShop.Rag.Api/EShop.Rag.Api.csproj

COPY . .
RUN dotnet publish src/EShop.Rag.Api/EShop.Rag.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "EShop.Rag.Api.dll"]
