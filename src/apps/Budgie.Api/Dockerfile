FROM oven/bun:1 AS frontend-build
WORKDIR /src
COPY src/apps/Budgie.Web/package.json src/apps/Budgie.Web/bun.lock* ./
RUN bun install
COPY src/apps/Budgie.Web/ .
RUN bun run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /src
COPY src/ .
RUN dotnet restore "apps/Budgie.Api/Budgie.Api.csproj"
RUN dotnet publish "apps/Budgie.Api/Budgie.Api.csproj" -c Release -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 80

ENV ASPNETCORE_URLS=http://0.0.0.0:80
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_gcServer=1

# curl is required by the compose healthcheck (test: curl -f http://localhost:80/health).
# Installed in a single layer and the apt lists are removed in the same RUN so they never
# add a separate layer of their own.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

COPY --from=api-build /app/publish .
COPY --from=frontend-build /src/dist wwwroot
COPY src/apps/Budgie.Api/entrypoint.sh /entrypoint.sh

# Person photos live in wwwroot/uploads, which is backed by a volume so they survive
# rebuilds. Photos baked into the build context are moved to /seed so the entrypoint can
# restore them into the volume on first start (never overwriting existing files).
# Everything (curl is already installed above) happens in ONE RUN to avoid extra layers.
RUN chmod +x /entrypoint.sh \
 && mkdir -p /seed/uploads \
 && if [ -d /app/wwwroot/uploads ]; then cp -a /app/wwwroot/uploads/. /seed/uploads/ 2>/dev/null || true; rm -rf /app/wwwroot/uploads; fi \
 && mkdir -p /app/wwwroot/uploads

ENTRYPOINT ["/entrypoint.sh"]