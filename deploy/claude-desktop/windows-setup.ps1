#Requires -Version 5.1
<#
.SYNOPSIS
  Installs and configures SQL MCP Server for Claude Desktop on Windows.
.PARAMETER ProjectPath
  Absolute path to the SqlMcpServer solution root.
.PARAMETER ConnectionString
  SQL Server connection string (default: Windows Auth to local instance).
.PARAMETER AuthMode
  Authentication mode: WindowsAuth | SqlAuth | AzureManagedIdentity | AzureAD
.PARAMETER BuildFirst
  Build and publish a self-contained binary before registering (default: $true).
#>
param(
    [string]$ProjectPath      = (Get-Location).Path,
    [string]$ConnectionString = "Server=.;Database=master;Trusted_Connection=true;Encrypt=false;",
    [string]$AuthMode         = "WindowsAuth",
    [bool]$BuildFirst         = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$HostProject = Join-Path $ProjectPath "src\SqlMcpServer.Host"
$PublishDir  = Join-Path $ProjectPath "publish\win-x64"
$ClaudeConfig = Join-Path $env:APPDATA "Claude\claude_desktop_config.json"

Write-Host "[1/4] Checking prerequisites..." -ForegroundColor Cyan

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET 8 SDK is not installed. Download from https://dotnet.microsoft.com/download/dotnet/8.0"
}

$sdkVersion = (dotnet --version)
if (-not $sdkVersion.StartsWith("8.")) {
    Write-Warning "Detected .NET $sdkVersion — .NET 8 recommended. Proceeding anyway."
}

Write-Host "[2/4] Building self-contained binary..." -ForegroundColor Cyan

if ($BuildFirst) {
    dotnet publish $HostProject `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -o $PublishDir `
        /p:PublishSingleFile=true `
        /p:UseAppHost=true
    Write-Host "Published to: $PublishDir" -ForegroundColor Green
}

$ExePath = Join-Path $PublishDir "SqlMcpServer.Host.exe"
if (-not (Test-Path $ExePath)) {
    throw "Executable not found at $ExePath. Run with -BuildFirst `$true or build manually first."
}

Write-Host "[3/4] Writing Claude Desktop config..." -ForegroundColor Cyan

$ConfigDir = Split-Path $ClaudeConfig -Parent
if (-not (Test-Path $ConfigDir)) {
    New-Item -ItemType Directory -Force -Path $ConfigDir | Out-Null
}

$McpEntry = @{
    command = $ExePath
    args    = @()
    env     = @{
        SqlServer__ConnectionString = $ConnectionString
        SqlServer__AuthMode         = $AuthMode
        ASPNETCORE_ENVIRONMENT      = "Production"
        "Serilog__MinimumLevel__Default" = "Warning"
    }
}

$Config = @{ mcpServers = @{} }

if (Test-Path $ClaudeConfig) {
    $Existing = Get-Content $ClaudeConfig -Raw | ConvertFrom-Json
    if ($Existing.mcpServers) {
        $Existing.mcpServers.PSObject.Properties | ForEach-Object {
            $Config.mcpServers[$_.Name] = $_.Value
        }
    }
}

$Config.mcpServers["mssql"] = $McpEntry

$Config | ConvertTo-Json -Depth 10 | Set-Content $ClaudeConfig -Encoding UTF8
Write-Host "Claude Desktop config updated: $ClaudeConfig" -ForegroundColor Green

Write-Host "[4/4] Done!" -ForegroundColor Green
Write-Host ""
Write-Host "Restart Claude Desktop to load the MCP server." -ForegroundColor Yellow
Write-Host "Connection: $ConnectionString" -ForegroundColor Yellow
