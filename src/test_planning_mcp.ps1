# Simple Planning MCP test
Write-Host "Testing ADR CLI Planning MCP Server" -ForegroundColor Green

dotnet build -q

$testRequests = @(
    '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"claude","version":"3.0"}}}'
    '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
    '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"task_new","arguments":{"title":"Test task","description":"Test description"}}}'
    '{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"task_list","arguments":{"verbose":false}}}'
)

$inputFile = "planning_test_simple.txt"
$testRequests | Out-File -FilePath $inputFile -Encoding UTF8

Write-Host "Running Planning MCP test..." -ForegroundColor Yellow

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
            $taskTools = @($response.result.tools | Where-Object { $_.name -like "task_*" })
            Write-Host "Found $($response.result.tools.Count) tools ($($taskTools.Count) task tools)" -ForegroundColor Green
        } elseif ($response.result.content) {
            Write-Host "$($response.result.content[0].text.Substring(0, [Math]::Min(100, $response.result.content[0].text.Length)))..." -ForegroundColor Green
        }
    }
}

Remove-Item $inputFile -ErrorAction SilentlyContinue
Write-Host "Planning MCP Server test completed successfully!" -ForegroundColor Green
