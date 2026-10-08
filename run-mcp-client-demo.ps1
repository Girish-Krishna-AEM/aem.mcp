<#
.SYNOPSIS
    Starts the Lightning MCP Server natively and opens MCP Inspector against it.

.DESCRIPTION
    Builds nothing new — just runs the already-built LightningMcpServer project
    with `dotnet run`, waits for its /health endpoint, then launches the official
    MCP Inspector (via npx) pointed at the server's spec-compliant /mcp endpoint.
    When you close Inspector (Ctrl+C), the server is stopped automatically.

.PARAMETER Port
    Port to run the server on. Defaults to 8000. If that port is already in use
    (e.g. by another process), the script automatically tries the next port up.

.EXAMPLE
    .\run-mcp-client-demo.ps1
    .\run-mcp-client-demo.ps1 -Port 8020
#>

param(
    [int]$Port = 8000
)

$ErrorActionPreference = "Stop"
$serverDir = Join-Path $PSScriptRoot "unit-1\src\LightningMcpServer"
$envFile = Join-Path $PSScriptRoot ".env"

if (-not (Test-Path $serverDir)) {
    Write-Error "Could not find LightningMcpServer project at: $serverDir"
    exit 1
}

if (Test-Path $envFile) {
    Write-Host "Loading environment variables from .env..." -ForegroundColor Cyan
    Get-Content $envFile | ForEach-Object {
        if ($_ -match '^\s*([^#=]+)=(.*)$') {
            $name = $matches[1].Trim()
            $value = $matches[2].Trim()
            [System.Environment]::SetEnvironmentVariable($name, $value)
        }
    }
} else {
    Write-Warning ".env file not found at $envFile - server will run without LIGHTNING_PULSE_API_KEY and upstream API calls will fail with 403."
}

function Test-PortFree([int]$p) {
    $inUse = Get-NetTCPConnection -LocalPort $p -State Listen -ErrorAction SilentlyContinue
    return -not $inUse
}

# Kills a process and its full descendant tree (e.g. `dotnet run`'s actual server child,
# or Inspector's detached sandbox/app-origin child processes) — plain Stop-Process only
# kills the single PID given, which leaves orphans that silently hold ports open.
function Stop-ProcessTree([int]$Id) {
    & taskkill /PID $Id /T /F *>$null
}

# Inspector's UI/sandbox/app-origin ports. If a previous run's process tree escaped
# cleanup (observed in practice on Windows), these stay bound and every future run
# falls back to unpredictable OS-assigned ports instead of failing loudly.
$InspectorPorts = 6274, 6275, 6278

function Clear-InspectorPorts {
    foreach ($p in $InspectorPorts) {
        Get-NetTCPConnection -LocalPort $p -State Listen -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty OwningProcess -Unique |
            ForEach-Object {
                Write-Host "Clearing stale process on Inspector port $p (PID $_)..." -ForegroundColor Yellow
                Stop-ProcessTree -Id $_
            }
    }
}

Clear-InspectorPorts

while (-not (Test-PortFree $Port)) {
    Write-Host "Port $Port is already in use, trying $($Port + 1)..." -ForegroundColor Yellow
    $Port++
}

Write-Host "Starting Lightning MCP Server on port $Port..." -ForegroundColor Cyan

# Child processes inherit these from this script's own environment.
$env:ASPNETCORE_URLS = "http://localhost:$Port"
$env:LISTEN_PORT = "$Port"

$serverProcess = Start-Process -FilePath "dotnet" `
    -ArgumentList "run", "--environment", "Development" `
    -WorkingDirectory $serverDir `
    -PassThru -WindowStyle Normal

try {
    Write-Host "Waiting for server to become healthy..." -ForegroundColor Cyan
    $ready = $false
    for ($i = 0; $i -lt 30; $i++) {
        try {
            $resp = Invoke-RestMethod -Uri "http://localhost:$Port/health" -TimeoutSec 2
            if ($resp.status -eq "healthy") { $ready = $true; break }
        } catch {}
        Start-Sleep -Milliseconds 500
    }

    if (-not $ready) {
        Write-Error "Server did not become healthy within 15 seconds. Check the server window for errors."
        exit 1
    }

    Write-Host "Server is healthy at http://localhost:$Port" -ForegroundColor Green
    Write-Host "Launching MCP Inspector against http://localhost:$Port/mcp ..." -ForegroundColor Cyan
    Write-Host "(Inspector opens in your browser. Close it or press Ctrl+C here when done.)" -ForegroundColor DarkGray

    # The inspector package ships two bins (mcp-inspector, mcpdo) with none matching
    # the package name, so npx can't auto-select one — the bin must be named explicitly.
    # Likewise, newer inspector versions take the target via --transport/--server-url
    # flags rather than a bare positional URL argument.
    # Run via Start-Process (not a direct call) so we keep the PID: npx/Inspector spawns
    # detached sandbox/app-origin child processes that otherwise survive this script
    # exiting and silently squat on ports 6274/6275/6278 for every future run.
    # npx is a .cmd shim, not a Win32 exe — Start-Process -NoNewWindow can't launch it
    # directly ("%1 is not a valid Win32 application"), so route it through cmd.exe /c.
    $npxArgs = "npx -p @modelcontextprotocol/inspector@latest mcp-inspector --transport http --server-url http://localhost:$Port/mcp"
    $inspectorProcess = Start-Process -FilePath "cmd.exe" -ArgumentList "/c", $npxArgs -NoNewWindow -PassThru
    Wait-Process -Id $inspectorProcess.Id
}
finally {
    Write-Host "Stopping Lightning MCP Server (PID $($serverProcess.Id))..." -ForegroundColor Cyan
    Stop-ProcessTree -Id $serverProcess.Id

    if ($inspectorProcess -and -not $inspectorProcess.HasExited) {
        Write-Host "Stopping MCP Inspector (PID $($inspectorProcess.Id))..." -ForegroundColor Cyan
        Stop-ProcessTree -Id $inspectorProcess.Id
    }

    # Belt-and-suspenders: catches detached Inspector child processes that escape the
    # tree-kill above (the actual cause of the "port already in use" failures seen in
    # past runs — this is what makes the next run predictable instead of a coin flip).
    Clear-InspectorPorts
}
