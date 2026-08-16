# Build context: ../src (the solution src folder)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Mcp.Customers/Mcp.Customers.csproj
RUN dotnet publish Mcp.Customers/Mcp.Customers.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8083
ENTRYPOINT ["dotnet", "Mcp.Customers.dll"]
