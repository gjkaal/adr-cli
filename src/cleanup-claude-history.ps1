# PowerShell script to clean up all project history from .claude.json
Write-Host "Cleaning up Claude Code project history..." -ForegroundColor Yellow

$configPath = "$env:USERPROFILE\.claude.json"

if (-not (Test-Path $configPath)) {
    Write-Host "ERROR: Config file not found at $configPath" -ForegroundColor Red
    exit 1
}

# Read the config
$config = Get-Content $configPath | ConvertFrom-Json

# Count history items before
$totalHistoryBefore = 0
foreach ($projectName in $config.projects.PSObject.Properties.Name) {
    $totalHistoryBefore += $config.projects.$projectName.history.Count
}

Write-Host "Found $totalHistoryBefore total history items across $($config.projects.PSObject.Properties.Count) projects" -ForegroundColor Cyan

# Clear all history
foreach ($projectName in $config.projects.PSObject.Properties.Name) {
    $historyCount = $config.projects.$projectName.history.Count
    if ($historyCount -gt 0) {
        Write-Host "  Clearing $historyCount items from: $projectName" -ForegroundColor Gray
        $config.projects.$projectName.history = @()
    }
}

# Save the config back
$config | ConvertTo-Json -Depth 100 | Set-Content $configPath -Encoding UTF8

Write-Host "SUCCESS: Cleared all project history" -ForegroundColor Green
Write-Host "Removed $totalHistoryBefore total history items" -ForegroundColor Green
