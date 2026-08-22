# Build context: ../src (the solution src folder)
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG TARGETARCH
WORKDIR /src
COPY --link Mcp.Customers/Mcp.Customers.csproj Mcp.Customers/
COPY --link ServiceDefaults/ServiceDefaults.csproj ServiceDefaults/
RUN dotnet restore Mcp.Customers/Mcp.Customers.csproj -a $TARGETARCH
COPY --link Mcp.Customers/ Mcp.Customers/
COPY --link ServiceDefaults/ ServiceDefaults/
RUN dotnet publish Mcp.Customers/Mcp.Customers.csproj -c Release -a $TARGETARCH --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
COPY --link --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8083
EXPOSE 8083
USER $APP_UID
ENTRYPOINT ["./Mcp.Customers"]
