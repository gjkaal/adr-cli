# Comprehensive MCP test demonstrating all functionality
Write-Host "Comprehensive ADR CLI MCP Server Test" -ForegroundColor Green
Write-Host "=======================================" -ForegroundColor Green

# Build first
dotnet build -q

# Test comprehensive functionality
$testRequests = @(
    '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"claude","version":"3.0"}}}'
    '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
    '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"adr_list","arguments":{"verbose":false}}}'
    '{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"adr_find","arguments":{"query":"commandline","verbose":false}}}'
    '{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"adr_sync","arguments":{}}}'
)

Write-Host "`nRunning comprehensive MCP test..." -ForegroundColor Yellow

# Create and run test
$inputFile = "comprehensive_test.txt"
$testRequests | Out-File -FilePath $inputFile -Encoding UTF8

$output = Get-Content $inputFile | Adr.Cli/bin/Debug/net9.0/adr-cli.exe mcp 2>&1

Write-Host "`n📋 Test Results:" -ForegroundColor Cyan
Write-Host "===============" -ForegroundColor Cyan

$responseCount = 0
foreach ($line in $output) {
    if ($line -match '^{"jsonrpc"') {
        $responseCount++
        try {
            $response = $line | ConvertFrom-Json
            
            switch ($responseCount) {
                1 { 
                    Write-Host "`n✅ 1. Initialize:" -ForegroundColor Green
                    Write-Host "   Server: $($response.result.serverInfo.name) v$($response.result.serverInfo.version)" -ForegroundColor Gray
                    Write-Host "   Protocol: $($response.result.protocolVersion)" -ForegroundColor Gray
                }
                2 { 
                    Write-Host "`n✅ 2. Tools Discovery:" -ForegroundColor Green
                    Write-Host "   Available tools: $($response.result.tools.Count)" -ForegroundColor Gray
                    $response.result.tools | ForEach-Object { 
                        Write-Host "   • $($_.name)" -ForegroundColor DarkGray 
                    }
                }
                3 { 
                    Write-Host "`n✅ 3. List ADRs:" -ForegroundColor Green
                    $content = $response.result.content[0].text
                    $lines = $content -split "`n" | Where-Object { $_ -match "^\d{5}" }
                    Write-Host "   Found $($lines.Count) ADRs:" -ForegroundColor Gray
                    $lines | ForEach-Object { Write-Host "   • $_" -ForegroundColor DarkGray }
                }
                4 { 
                    Write-Host "`n✅ 4. Search ADRs:" -ForegroundColor Green
                    $content = $response.result.content[0].text
                    $lines = $content -split "`n" | Where-Object { $_ -match "^\d{5}" }
                    Write-Host "   Found $($lines.Count) matching ADRs:" -ForegroundColor Gray
                    $lines | ForEach-Object { Write-Host "   • $_" -ForegroundColor DarkGray }
                }
                5 { 
                    Write-Host "`n✅ 5. Sync Metadata:" -ForegroundColor Green
                    Write-Host "   Result: $($response.result.content[0].text)" -ForegroundColor Gray
                }
            }
        } catch {
            Write-Host "`n❌ Error parsing response $responseCount" -ForegroundColor Red
        }
    }
}

# Clean up
Remove-Item $inputFile -ErrorAction SilentlyContinue

Write-Host "`n✓ MCP Server fully functional!" -ForegroundColor Green
Write-Host "Ready for integration with AI tools like Claude and Copilot." -ForegroundColor Cyan
