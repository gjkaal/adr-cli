# PowerShell script to test MCP functionality
Write-Host "Testing ADR CLI MCP Server..." -ForegroundColor Green

# Build the project first
Write-Host "Building project..." -ForegroundColor Yellow
Push-Location "src"
dotnet build
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    Pop-Location
    exit 1
}
Pop-Location

# Test 1: Initialize request
Write-Host "`nTest 1: Testing initialize request..." -ForegroundColor Yellow
$initRequest = @{
    jsonrpc = "2.0"
    id = 1
    method = "initialize"
    params = @{
        protocolVersion = "2024-11-05"
        capabilities = @{}
        clientInfo = @{
            name = "test-client"
            version = "1.0.0"
        }
    }
} | ConvertTo-Json -Depth 10

Write-Host "Sending: $initRequest" -ForegroundColor Cyan

# Test 2: Tools list request
Write-Host "`nTest 2: Testing tools/list request..." -ForegroundColor Yellow
$toolsListRequest = @{
    jsonrpc = "2.0"
    id = 2
    method = "tools/list"
} | ConvertTo-Json

Write-Host "Sending: $toolsListRequest" -ForegroundColor Cyan

# Create test script to send both requests
$testScript = @"
$initRequest
$toolsListRequest
"@

Write-Host "`nRunning MCP server test..." -ForegroundColor Yellow
$testScript | src/Adr.Cli/bin/Debug/net9.0/adr-cli.exe mcp
Write-Host "`nMCP test completed!" -ForegroundColor Green