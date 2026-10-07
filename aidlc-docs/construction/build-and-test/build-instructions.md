# Build Instructions

## Prerequisites
- **Build Tool**: .NET 10.0 SDK
- **Dependencies**: Restored automatically via `dotnet build`/`dotnet test` (NuGet: xunit, Moq, Microsoft.AspNetCore.Mvc.Testing — test projects only; no extra packages for application code)
- **Environment Variables**: None required to build; `LISTEN_PORT`, `LOG_LEVEL`, `MCP_SERVER_URL`, `ASPNETCORE_URLS` are runtime-only (see `.env.example`)
- **System Requirements**: Any OS supported by .NET 10.0 SDK (Windows/Linux/Mac); no special memory/disk requirements for this POC

## Build Steps

### 1. Install Dependencies
```bash
# No separate install step — dotnet restore runs automatically as part of build/test
```

### 2. Configure Environment
```bash
# Not required for building. For running the services, see DEPLOYMENT.md.
cp .env.example .env
```

### 3. Build All Units

There is no top-level solution file; build each unit's projects individually (each unit is self-contained):

```bash
# Unit 1: Lightning MCP Server
cd unit-1
dotnet build src/LightningMcpServer/LightningMcpServer.csproj
dotnet build tests/LightningMcpServer.Tests/LightningMcpServer.Tests.csproj

# Unit 2: Coordinator Agent
cd ../unit-2
dotnet build src/CoordinatorAgent/CoordinatorAgent.csproj
dotnet build tests/CoordinatorAgent.Tests/CoordinatorAgent.Tests.csproj
```

(Building the test project also builds its referenced application project via `ProjectReference`, so the two build commands per unit are equivalent to building both — they're listed separately for clarity.)

### 4. Verify Build Success
- **Expected Output**: `Build succeeded. 0 Warning(s). 0 Error(s).` for each project
- **Build Artifacts**:
  - `unit-1/src/LightningMcpServer/bin/Debug/net10.0/LightningMcpServer.dll`
  - `unit-1/src/LightningCommon/bin/Debug/net10.0/LightningCommon.dll`
  - `unit-2/src/CoordinatorAgent/bin/Debug/net10.0/CoordinatorAgent.dll`
- **Common Warnings**: `NU1900` (NuGet vulnerability-data lookup failing against a private feed not reachable from this environment) is expected/benign in this environment and does not indicate a build problem

## Verified Results (this session)

| Unit | Command | Result |
|---|---|---|
| Unit 1 | `dotnet build src/LightningMcpServer/LightningMcpServer.csproj` | ✅ 0 Warnings, 0 Errors |
| Unit 2 | `dotnet build src/CoordinatorAgent/CoordinatorAgent.csproj` | ✅ 0 Warnings, 0 Errors |

## Troubleshooting

### Build Fails with Dependency Errors
- **Cause**: No internet access to restore NuGet packages, or a corporate NuGet feed (`EarthNetworks@Local`) is unreachable
- **Solution**: Confirm `nuget.org` is reachable, or add `--source https://api.nuget.org/v3/index.json` explicitly if a misconfigured private feed is blocking restore

### Build Fails with Compilation Errors
- **Cause**: Wrong .NET SDK version installed
- **Solution**: Run `dotnet --version` and confirm a 10.0.x SDK is installed; install from https://dotnet.microsoft.com/download/dotnet/10.0 if missing
