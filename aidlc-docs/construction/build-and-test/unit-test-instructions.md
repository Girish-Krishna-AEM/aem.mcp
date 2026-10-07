# Unit Test Execution

## Run Unit Tests

### 1. Execute All Unit Tests

```bash
# Unit 1: Lightning MCP Server
cd unit-1
dotnet test tests/LightningMcpServer.Tests/LightningMcpServer.Tests.csproj

# Unit 2: Coordinator Agent
cd ../unit-2
dotnet test tests/CoordinatorAgent.Tests/CoordinatorAgent.Tests.csproj
```

Each `dotnet test` run includes that unit's domain-module/service unit tests AND its in-process integration tests (via `WebApplicationFactory<Program>`) in a single pass — there is no separate "unit-only" filter configured, since both test categories are fast (no real network/Docker dependency) and run together in seconds.

### 2. Review Test Results

**Verified this session**:

| Unit | Total | Passed | Failed | Duration |
|---|---|---|---|---|
| Unit 1 (LightningMcpServer.Tests) | 25 | 25 | 0 | ~6s |
| Unit 2 (CoordinatorAgent.Tests) | 29 | 29 | 0 | ~1s |

- **Test Report Location**: Console output (`dotnet test`); add `--logger "trx;LogFileName=results.trx"` for a machine-readable report if needed
- **Test Coverage**: Not measured with a coverage tool in this POC (no `coverlet`/`reportgenerator` configured); coverage targets (≥80% domain modules, ≥90% endpoints) were design-time goals reflected in the breadth of test cases per module, not an enforced gate

### 3. Fix Failing Tests

If tests fail:
1. Review the xUnit console output — it names the failing test and shows expected vs. actual values
2. Reproduce the specific test with `dotnet test --filter "FullyQualifiedName~<TestClassName>"`
3. Fix the implementation (or the test, if the test's expectation was wrong — see the audit trail entry from the Unit 2 code generation session for a real example: a test used "strikes" where the implementation's keyword regex required the singular "strike")
4. Rerun `dotnet test` until all pass

## Test Breakdown by Unit

### Unit 1 (25 tests)
- `DomainModules/StrikeDetectionModuleTests.cs`, `WeatherForecastModuleTests.cs`, `SensorDiagnosticsModuleTests.cs`, `InformerStatusModuleTests.cs` — deterministic stub-data and validation tests
- `Integration/McpServerIntegrationTests.cs` — `tools/list`, `tools/call` per tool, error handling, health check

### Unit 2 (29 tests)
- `Services/IntentClassifierTests.cs` (9) — keyword classification per intent + unrecognized query
- `Services/ParameterExtractorTests.cs` (9) — location/radius/forecastType/sensorId/informerId/zone extraction, success and error paths
- `Services/McpClientTests.cs` (6) — success, 400, 500, network failure, health success/failure (fake `HttpMessageHandler`)
- `Integration/CoordinatorIntegrationTests.cs` (5) — end-to-end `/query` and `/health` via `WebApplicationFactory<Program>` with a faked MCP Server handler
