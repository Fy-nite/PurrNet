# Build args — only defaults, override at run via env or compose
ARG GITHUB_CLIENT_ID=your-github-client-id-here
ARG GITHUB_CLIENT_SECRET=your-github-client-secret-here
ARG ASPNETCORE_ENVIRONMENT=Production

# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj first for better layer caching (compose context is repo root)
COPY PurrNet/purrnet.csproj ./PurrNet/
RUN dotnet restore "PurrNet/purrnet.csproj"

# Copy the rest of the PurrNet source
COPY PurrNet/ ./PurrNet/

WORKDIR /src/PurrNet
RUN dotnet publish "purrnet.csproj" -c Release -o /app/publish --no-restore

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
# curl is needed for HEALTHCHECK
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./

# Runtime env (overridable at `docker run -e` or compose)
ENV GITHUB_CLIENT_ID=${GITHUB_CLIENT_ID}
ENV GITHUB_CLIENT_SECRET=${GITHUB_CLIENT_SECRET}
ENV ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT}
# Default MariaDB string — override with MARIADB_CONNECTION_STRING in compose
ENV MARIADB_CONNECTION_STRING="Server=db;Database=PurrNet;User=purrnet;Password=purrnet;"

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "purrnet.dll"]
