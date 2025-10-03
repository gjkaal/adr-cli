# PowerShell script to test MCP functionality with single-line JSON
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

# Test requests - each JSON request must be on a single line
$testRequests = @(
    '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test-client","version":"1.0.0"}}}'
    '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
    '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"adr_list","arguments":{"verbose":true}}}'
)

Write-Host "`nPreparing test input..." -ForegroundColor Yellow

# Create temporary input file
$inputFile = "test_input.txt"
$testRequests | Out-File -FilePath $inputFile -Encoding UTF8

Write-Host "Test requests:" -ForegroundColor Cyan
foreach ($request in $testRequests) {
    Write-Host "  $request" -ForegroundColor Gray
}

Write-Host "`nRunning MCP server test..." -ForegroundColor Yellow
Get-Content $inputFile | src/Adr.Cli/bin/Debug/net9.0/adr-cli.exe mcp

# Clean up
Remove-Item $inputFile -ErrorAction SilentlyContinue

Write-Host "`nMCP test completed!" -ForegroundColor Green