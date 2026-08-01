FROM mcr.microsoft.com/devcontainers/dotnet:1-10.0

RUN rm -f /etc/apt/sources.list.d/yarn.list \
    && apt-get update && export DEBIAN_FRONTEND=noninteractive \
    && apt-get install -y --no-install-recommends \
        postgresql-client unzip \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

RUN curl -fsSL https://bun.sh/install | bash \
    && mv ~/.bun /usr/local/share/bun \
    && ln -s /usr/local/share/bun/bin/bun /usr/local/bin/bun
