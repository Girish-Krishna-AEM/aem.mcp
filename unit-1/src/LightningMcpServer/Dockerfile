# Multi-stage build for Lightning MCP Server

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder
WORKDIR /src

# Copy project files
COPY unit-1/src/LightningCommon/LightningCommon.csproj ./LightningCommon/
COPY unit-1/src/LightningMcpServer/LightningMcpServer.csproj ./LightningMcpServer/

# Restore dependencies
RUN cd LightningCommon && dotnet restore
RUN cd LightningMcpServer && dotnet restore

# Copy source code
COPY unit-1/src/LightningCommon/ ./LightningCommon/
COPY unit-1/src/LightningMcpServer/ ./LightningMcpServer/

# Publish
RUN cd LightningMcpServer && dotnet publish -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 8000

# curl is required by the HEALTHCHECK below but is not present in the base aspnet image
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=builder /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8000/health || exit 1

# Environment variables
ENV LISTEN_PORT=8000
ENV LOG_LEVEL=INFO
ENV ASPNETCORE_URLS=http://0.0.0.0:8000

# Run application
ENTRYPOINT ["dotnet", "LightningMcpServer.dll"]
