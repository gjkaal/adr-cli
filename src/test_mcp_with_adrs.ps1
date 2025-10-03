# PowerShell script to test MCP functionality with actual ADRs in src folder
Write-Host "Testing ADR CLI MCP Server with existing ADRs..." -ForegroundColor Green

# Build the project first
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Current directory: $(Get-Location)" -ForegroundColor Cyan
Write-Host "Checking for ADR configuration..." -ForegroundColor Yellow
if (Test-Path "adr.config.json") {
    Write-Host "✅ Found adr.config.json" -ForegroundColor Green
    Get-Content "adr.config.json" | Write-Host -ForegroundColor Gray
} else {
    Write-Host "❌ No adr.config.json found" -ForegroundColor Red
}

if (Test-Path "doc/adr") {
    $adrCount = (Get-ChildItem "doc/adr" -Filter "*.md").Count
    Write-Host "✅ Found $adrCount ADR markdown files in doc/adr/" -ForegroundColor Green
} else {
    Write-Host "❌ No doc/adr directory found" -ForegroundColor Red
}

# Test requests - each JSON request must be on a single line
$testRequests = @(
    '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test-client","version":"1.0.0"}}}'
    '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
    '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"adr_list","arguments":{"verbose":true}}}'
    '{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"adr_find","arguments":{"query":"system.commandline","verbose":true}}}'
    '{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"adr_generate_toc","arguments":{}}}'
)

Write-Host "`nPreparing test input..." -ForegroundColor Yellow

# Create temporary input file
$inputFile = "test_input.txt"
$testRequests | Out-File -FilePath $inputFile -Encoding UTF8

Write-Host "Test requests:" -ForegroundColor Cyan
$i = 1
foreach ($request in $testRequests) {
    $decoded = $request | ConvertFrom-Json
    Write-Host "  $i. $($decoded.method)" -ForegroundColor Gray
    if ($decoded.params.name) { Write-Host "     Tool: $($decoded.params.name)" -ForegroundColor DarkGray }
    $i++
}

Write-Host "`nRunning MCP server test..." -ForegroundColor Yellow
Write-Host "=" * 80 -ForegroundColor DarkCyan

$output = Get-Content $inputFile | Adr.Cli/bin/Debug/net9.0/adr-cli.exe mcp

Write-Host "=" * 80 -ForegroundColor DarkCyan
Write-Host "`nMCP Responses:" -ForegroundColor Yellow

$responseIndex = 1
foreach ($line in $output) {
    if ($line.StartsWith('{"jsonrpc"')) {
        try {
            $response = $line | ConvertFrom-Json
            Write-Host "`nResponse $responseIndex (ID: $($response.id)):" -ForegroundColor Cyan
            
            if ($response.result) {
                if ($response.result.serverInfo) {
                    Write-Host "  Server: $($response.result.serverInfo.name) v$($response.result.serverInfo.version)" -ForegroundColor Green
                } elseif ($response.result.tools) {
                    Write-Host "  Available tools: $($response.result.tools.Count)" -ForegroundColor Green
                    $response.result.tools | ForEach-Object { Write-Host "    - $($_.name): $($_.description)" -ForegroundColor DarkGreen }
                } elseif ($response.result.content) {
                    Write-Host "  Tool result:" -ForegroundColor Green
                    $response.result.content | ForEach-Object { 
                        if ($_.text.Length -gt 200) {
                            Write-Host "    $($_.text.Substring(0, 200))..." -ForegroundColor DarkGreen
                        } else {
                            Write-Host "    $($_.text)" -ForegroundColor DarkGreen
                        }
                    }
                }
            } elseif ($response.error) {
                Write-Host "  Error: $($response.error.message)" -ForegroundColor Red
            }
            $responseIndex++
        } catch {
            Write-Host "  [Raw] $line" -ForegroundColor Gray
        }
    } else {
        Write-Host "  [Log] $line" -ForegroundColor DarkYellow
    }
}

# Clean up
Remove-Item $inputFile -ErrorAction SilentlyContinue

Write-Host "`n" + "=" * 80 -ForegroundColor DarkCyan
Write-Host "MCP test completed!" -ForegroundColor Green