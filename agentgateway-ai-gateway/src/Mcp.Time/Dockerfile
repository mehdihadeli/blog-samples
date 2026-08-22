FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG TARGETARCH
WORKDIR /src
COPY --link Mcp.Time/Mcp.Time.csproj Mcp.Time/
COPY --link ServiceDefaults/ServiceDefaults.csproj ServiceDefaults/
RUN dotnet restore Mcp.Time/Mcp.Time.csproj -a $TARGETARCH
COPY --link Mcp.Time/ Mcp.Time/
COPY --link ServiceDefaults/ ServiceDefaults/
RUN dotnet publish Mcp.Time/Mcp.Time.csproj -c Release -a $TARGETARCH --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
COPY --link --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8084
EXPOSE 8084
USER $APP_UID
ENTRYPOINT ["./Mcp.Time"]
