# Build context: ../src (the solution src folder)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Mcp.Catalog/Mcp.Catalog.csproj
RUN dotnet publish Mcp.Catalog/Mcp.Catalog.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8082
ENTRYPOINT ["dotnet", "Mcp.Catalog.dll"]
