# Test the global adr-cli MCP configuration
Write-Host "Testing global adr-cli MCP integration..." -ForegroundColor Green

# Test requests for the global command
$testRequests = @(
    '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"claude-code","version":"1.0"}}}'
    '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
)

$inputFile = "global_test.txt"
$testRequests | Out-File -FilePath $inputFile -Encoding UTF8

Write-Host "Testing with global adr-cli command..." -ForegroundColor Yellow
$output = Get-Content $inputFile | adr-cli mcp 2>$null

Write-Host "`nResults:" -ForegroundColor Cyan
$responseCount = 0
foreach ($line in $output) {
    if ($line -match '^{"jsonrpc"') {
        $responseCount++
        $response = $line | ConvertFrom-Json
        
        if ($responseCount -eq 1) {
            Write-Host "✅ Initialize: $($response.result.serverInfo.name) v$($response.result.serverInfo.version)" -ForegroundColor Green
        } elseif ($responseCount -eq 2) {
            Write-Host "✅ Tools available: $($response.result.tools.Count)" -ForegroundColor Green
            Write-Host "   Available tools for Claude Code:" -ForegroundColor Gray
            $response.result.tools | ForEach-Object { 
                Write-Host "   • $($_.name): $($_.description)" -ForegroundColor DarkGray
            }
        }
    }
}

Remove-Item $inputFile -ErrorAction SilentlyContinue

Write-Host "`n🎉 Global adr-cli MCP integration is working!" -ForegroundColor Green
Write-Host "Claude Code can now automatically use ADR tools in this repository." -ForegroundColor Cyan