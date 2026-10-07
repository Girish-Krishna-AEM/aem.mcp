# Multi-stage build for Coordinator Agent

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder
WORKDIR /src

# Copy project file
COPY unit-2/src/CoordinatorAgent/CoordinatorAgent.csproj ./CoordinatorAgent/

# Restore dependencies
RUN cd CoordinatorAgent && dotnet restore

# Copy source code
COPY unit-2/src/CoordinatorAgent/ ./CoordinatorAgent/

# Publish
RUN cd CoordinatorAgent && dotnet publish -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 8001

# curl is required by the HEALTHCHECK below but is not present in the base aspnet image
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=builder /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8001/health || exit 1

# Environment variables (no EC2-specific values — same image runs locally or on EC2)
ENV LISTEN_PORT=8001
ENV LOG_LEVEL=INFO
ENV MCP_SERVER_URL=http://lightning-mcp-server:8000
ENV ASPNETCORE_URLS=http://0.0.0.0:8001

# Run application
ENTRYPOINT ["dotnet", "CoordinatorAgent.dll"]
