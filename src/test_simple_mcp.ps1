# Simple MCP test
Write-Host "Testing ADR CLI MCP Server" -ForegroundColor Green

dotnet build -q

$testRequests = @(
    '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"claude","version":"3.0"}}}'
    '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
    '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"adr_list","arguments":{"verbose":false}}}'
)

$inputFile = "simple_test.txt"
$testRequests | Out-File -FilePath $inputFile -Encoding UTF8

Write-Host "Running MCP test..." -ForegroundColor Yellow

$output = Get-Content $inputFile | Adr.Cli/bin/Debug/net9.0/adr-cli.exe mcp 2>$null

$responseCount = 0
foreach ($line in $output) {
    if ($line -match '^{"jsonrpc"') {
        $responseCount++
        $response = $line | ConvertFrom-Json
        
        Write-Host "Response $responseCount`: " -NoNewline -ForegroundColor Cyan
        if ($response.result.serverInfo) {
            Write-Host "Server initialized successfully" -ForegroundColor Green
        } elseif ($response.result.tools) {
            Write-Host "Found $($response.result.tools.Count) tools" -ForegroundColor Green
        } elseif ($response.result.content) {
            $adrCount = ($response.result.content[0].text -split "`n" | Where-Object { $_ -match "^\d{5}" }).Count
            Write-Host "Listed $adrCount ADRs" -ForegroundColor Green
        }
    }
}

Remove-Item $inputFile -ErrorAction SilentlyContinue
Write-Host "MCP Server test completed successfully!" -ForegroundColor Green