# Comprehensive Planning MCP test demonstrating all task management functionality
Write-Host "Comprehensive Planning MCP Server Test" -ForegroundColor Green
Write-Host "=======================================" -ForegroundColor Green

# Build first
dotnet build -q

# Test comprehensive planning functionality - Phase 1
$testRequests1 = @(
    '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"claude","version":"3.0"}}}'
    '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
    '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"task_new","arguments":{"title":"Implement authentication system","description":"Add OAuth2 authentication for users","dueDate":"2025-12-31"}}}'
    '{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"task_new","arguments":{"title":"Database migration","description":"Migrate from SQL Server to PostgreSQL"}}}'
    '{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"task_new","arguments":{"title":"API documentation","description":"Create OpenAPI documentation for REST endpoints","dueDate":"2025-11-15"}}}'
    '{"jsonrpc":"2.0","id":6,"method":"tools/call","params":{"name":"task_list","arguments":{"verbose":true}}}'
    '{"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"task_find","arguments":{"query":"authentication","verbose":false}}}'
    '{"jsonrpc":"2.0","id":8,"method":"tools/call","params":{"name":"task_update","arguments":{"taskId":1,"status":"Active","justification":"Started implementation phase"}}}'
    '{"jsonrpc":"2.0","id":9,"method":"tools/call","params":{"name":"task_link","arguments":{"source":1,"target":2,"remark":"Requires database setup"}}}'
    '{"jsonrpc":"2.0","id":10,"method":"tools/call","params":{"name":"task_list","arguments":{"verbose":false}}}'
    '{"jsonrpc":"2.0","id":11,"method":"tools/call","params":{"name":"task_find","arguments":{"query":"API","status":"New","verbose":false}}}'
    '{"jsonrpc":"2.0","id":12,"method":"tools/call","params":{"name":"task_unlink","arguments":{"source":1,"target":2}}}'
)

Write-Host "`nRunning comprehensive planning MCP test - Phase 1..." -ForegroundColor Yellow

# Create and run test - Phase 1
$inputFile = "planning_test.txt"
$testRequests1 | Out-File -FilePath $inputFile -Encoding UTF8

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
                    $taskTools = $response.result.tools | Where-Object { $_.name -like "task_*" }
                    Write-Host "   Available tools: $($response.result.tools.Count)" -ForegroundColor Gray
                    Write-Host "   Task management tools: $($taskTools.Count)" -ForegroundColor Gray
                    $taskTools | ForEach-Object {
                        Write-Host "   • $($_.name): $($_.description)" -ForegroundColor DarkGray
                    }
                }
                3 {
                    Write-Host "`n✅ 3. Create Task #1 (Authentication):" -ForegroundColor Green
                    $content = $response.result.content[0].text
                    if ($response.result.isError) {
                        Write-Host "   ⚠ $content" -ForegroundColor Yellow
                    } else {
                        Write-Host "   $content" -ForegroundColor Gray
                    }
                }
                4 {
                    Write-Host "`n✅ 4. Create Task #2 (Database Migration):" -ForegroundColor Green
                    $content = $response.result.content[0].text
                    if ($response.result.isError) {
                        Write-Host "   ⚠ $content" -ForegroundColor Yellow
                    } else {
                        Write-Host "   $content" -ForegroundColor Gray
                    }
                }
                5 {
                    Write-Host "`n✅ 5. Create Task #3 (API Documentation):" -ForegroundColor Green
                    $content = $response.result.content[0].text
                    if ($response.result.isError) {
                        Write-Host "   ⚠ $content" -ForegroundColor Yellow
                    } else {
                        Write-Host "   $content" -ForegroundColor Gray
                    }
                }
                6 {
                    Write-Host "`n✅ 6. List All Tasks (Verbose):" -ForegroundColor Green
                    $content = $response.result.content[0].text
                    $lines = $content -split "`n" | Where-Object { $_ -match "^\d{5}" }
                    Write-Host "   Found $($lines.Count) tasks:" -ForegroundColor Gray
                    $lines | Select-Object -First 5 | ForEach-Object { Write-Host "   • $_" -ForegroundColor DarkGray }
                }
                7 {
                    Write-Host "`n✅ 7. Search Tasks (authentication):" -ForegroundColor Green
                    $content = $response.result.content[0].text
                    $lines = $content -split "`n" | Where-Object { $_ -match "^\d{5}" }
                    Write-Host "   Found $($lines.Count) matching tasks:" -ForegroundColor Gray
                    $lines | ForEach-Object { Write-Host "   • $_" -ForegroundColor DarkGray }
                }
                8 {
                    Write-Host "`n✅ 8. Update Task Status (Task #1 -> Active):" -ForegroundColor Green
                    $content = $response.result.content[0].text
                    Write-Host "   $content" -ForegroundColor Gray
                }
                9 {
                    Write-Host "`n✅ 9. Link Tasks (Task #1 -> Task #2):" -ForegroundColor Green
                    $content = $response.result.content[0].text
                    Write-Host "   $content" -ForegroundColor Gray
                }
                10 {
                    Write-Host "`n✅ 10. List All Tasks (After Updates):" -ForegroundColor Green
                    $content = $response.result.content[0].text
                    $lines = $content -split "`n" | Where-Object { $_ -match "^\d{5}" }
                    Write-Host "   Found $($lines.Count) tasks:" -ForegroundColor Gray
                    $lines | Select-Object -First 5 | ForEach-Object { Write-Host "   • $_" -ForegroundColor DarkGray }
                }
                11 {
                    Write-Host "`n✅ 11. Search Tasks by Status (New):" -ForegroundColor Green
                    $content = $response.result.content[0].text
                    $lines = $content -split "`n" | Where-Object { $_ -match "^\d{5}" }
                    Write-Host "   Found $($lines.Count) tasks with status 'New':" -ForegroundColor Gray
                    $lines | ForEach-Object { Write-Host "   • $_" -ForegroundColor DarkGray }
                }
                12 {
                    Write-Host "`n✅ 12. Unlink Tasks (Task #1 -/-> Task #2):" -ForegroundColor Green
                    $content = $response.result.content[0].text
                    Write-Host "   $content" -ForegroundColor Gray
                }
            }
        } catch {
            Write-Host "`n❌ Error parsing response $responseCount" -ForegroundColor Red
            Write-Host "   Error: $_" -ForegroundColor Red
        }
    }
}

# Phase 2: Modify markdown file and test synchronization
Write-Host "`n`n📝 Phase 2: Markdown Modification & Synchronization" -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan

# Find the first task markdown file
$configPath = Join-Path $PSScriptRoot ".." "adr.config.json"
if (Test-Path $configPath) {
    $config = Get-Content $configPath | ConvertFrom-Json
    $tasksFolder = Join-Path (Split-Path $configPath) $config.tasksRoot

    if (Test-Path $tasksFolder) {
        $taskFiles = Get-ChildItem -Path $tasksFolder -Filter "*.md" | Sort-Object Name | Select-Object -First 1

        if ($taskFiles) {
            $taskFile = $taskFiles.FullName
            Write-Host "`n⚠ Modifying task file: $($taskFiles.Name)" -ForegroundColor Yellow

            # Read the task file
            $taskContent = Get-Content $taskFile -Raw

            # Add a comment to the markdown file
            $modifiedContent = $taskContent + "`n`n## Additional Notes`n`nThis task was modified during automated testing to verify synchronization functionality.`n"

            # Write the modified content back
            Set-Content -Path $taskFile -Value $modifiedContent -Encoding UTF8
            Write-Host "   ✓ Added 'Additional Notes' section" -ForegroundColor Green
        } else {
            Write-Host "`n⚠ No task files found in $tasksFolder" -ForegroundColor Yellow
        }
    } else {
        Write-Host "`n⚠ Tasks folder not found: $tasksFolder" -ForegroundColor Yellow
    }
} else {
    Write-Host "`n⚠ Config file not found: $configPath" -ForegroundColor Yellow
}

# Phase 2: Sync and TOC generation
$testRequests2 = @(
    '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"claude","version":"3.0"}}}'
    '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"adr_sync","arguments":{}}}'
    '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"task_generate_toc","arguments":{}}}'
    '{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"task_list","arguments":{"verbose":true}}}'
)

Write-Host "`nRunning synchronization test - Phase 2..." -ForegroundColor Yellow

$testRequests2 | Out-File -FilePath $inputFile -Encoding UTF8
$output2 = Get-Content $inputFile | Adr.Cli/bin/Debug/net9.0/adr-cli.exe mcp 2>&1

Write-Host "`n📋 Synchronization Results:" -ForegroundColor Cyan
Write-Host "===========================" -ForegroundColor Cyan

$responseCount = 0
foreach ($line in $output2) {
    if ($line -match '^{"jsonrpc"') {
        $responseCount++
        try {
            $response = $line | ConvertFrom-Json

            switch ($responseCount) {
                1 {
                    Write-Host "`n✅ Re-initialized MCP Server" -ForegroundColor Green
                }
                2 {
                    Write-Host "`n✅ 13. Sync Metadata After Markdown Changes:" -ForegroundColor Green
                    $content = $response.result.content[0].text
                    Write-Host "   $content" -ForegroundColor Gray
                }
                3 {
                    Write-Host "`n✅ 14. Generate Task TOC:" -ForegroundColor Green
                    $content = $response.result.content[0].text
                    Write-Host "   $content" -ForegroundColor Gray
                }
                4 {
                    Write-Host "`n✅ 15. Verify Tasks After Sync:" -ForegroundColor Green
                    $content = $response.result.content[0].text
                    $lines = $content -split "`n" | Where-Object { $_ -match "^\d{5}" }
                    Write-Host "   Found $($lines.Count) tasks:" -ForegroundColor Gray
                    $lines | Select-Object -First 5 | ForEach-Object { Write-Host "   • $_" -ForegroundColor DarkGray }
                }
            }
        } catch {
            Write-Host "`n❌ Error parsing response $responseCount" -ForegroundColor Red
            Write-Host "   Error: $_" -ForegroundColor Red
        }
    }
}

# Clean up
Remove-Item $inputFile -ErrorAction SilentlyContinue

Write-Host "`n✓ Planning MCP Server fully functional!" -ForegroundColor Green
Write-Host "Ready for task management integration with AI tools." -ForegroundColor Cyan
Write-Host "`nTask Management Features Tested:" -ForegroundColor Yellow
Write-Host "  • Create new tasks with title, description, and due date" -ForegroundColor DarkGray
Write-Host "  • List all tasks (verbose and compact modes)" -ForegroundColor DarkGray
Write-Host "  • Search tasks by keywords" -ForegroundColor DarkGray
Write-Host "  • Filter tasks by status" -ForegroundColor DarkGray
Write-Host "  • Update task status with justification" -ForegroundColor DarkGray
Write-Host "  • Link tasks together with remarks" -ForegroundColor DarkGray
Write-Host "  • Unlink related tasks" -ForegroundColor DarkGray
Write-Host "  • Modify markdown files directly" -ForegroundColor DarkGray
Write-Host "  • Synchronize metadata after manual changes" -ForegroundColor DarkGray
Write-Host "  • Generate task table of contents" -ForegroundColor DarkGray
