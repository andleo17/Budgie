FROM oven/bun:1 AS frontend-build
WORKDIR /src
COPY src/apps/Budgie.Web/package.json src/apps/Budgie.Web/bun.lock* ./
RUN bun install
COPY src/apps/Budgie.Web/ .
RUN bun run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /src

# Native AOT requires clang + zlib
RUN apt-get update && apt-get install -y --no-install-recommends clang zlib1g-dev && \
    rm -rf /var/lib/apt/lists/*

COPY src/ .
RUN dotnet restore "apps/Budgie.Api/Budgie.Api.csproj"
RUN dotnet publish "apps/Budgie.Api/Budgie.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0
WORKDIR /app
EXPOSE 80

ENV ASPNETCORE_URLS=http://0.0.0.0:80
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

RUN apt-get update && apt-get install -y --no-install-recommends curl ca-certificates && \
    rm -rf /var/lib/apt/lists/*

COPY --from=api-build /app/publish .
COPY --from=frontend-build /src/dist wwwroot
COPY src/apps/Budgie.Api/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

ENTRYPOINT ["/entrypoint.sh"]
