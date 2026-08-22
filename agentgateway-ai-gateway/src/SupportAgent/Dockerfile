# Build context: ../src (the solution src folder)
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG TARGETARCH
WORKDIR /src
COPY --link SupportAgent/SupportAgent.csproj SupportAgent/
COPY --link ServiceDefaults/ServiceDefaults.csproj ServiceDefaults/
RUN dotnet restore SupportAgent/SupportAgent.csproj -a $TARGETARCH
COPY --link SupportAgent/ SupportAgent/
COPY --link ServiceDefaults/ ServiceDefaults/
RUN dotnet publish SupportAgent/SupportAgent.csproj -c Release -a $TARGETARCH --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
COPY --link --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=9999
EXPOSE 9999
USER $APP_UID
ENTRYPOINT ["./SupportAgent"]
