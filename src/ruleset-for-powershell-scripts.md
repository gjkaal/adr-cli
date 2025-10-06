# PowerShell Script Development Ruleset

This document provides guidelines for creating reliable PowerShell scripts, especially when generating them programmatically or working across different environments.

## Character Encoding and Special Characters

### 1. Avoid Unicode Emojis and Special Symbols
**Problem**: Emojis and special Unicode characters can cause parsing errors, especially when files are created programmatically or transferred between systems.

❌ **BAD**:
```powershell
Write-Host "📝 Phase 2: Testing & Validation" -ForegroundColor Green
Write-Host "✅ Success!" -ForegroundColor Green
Write-Host "❌ Failed!" -ForegroundColor Red
```

✅ **GOOD**:
```powershell
Write-Host "Phase 2: Testing and Validation" -ForegroundColor Green
Write-Host "SUCCESS: Operation completed!" -ForegroundColor Green
Write-Host "ERROR: Operation failed!" -ForegroundColor Red
```

### 2. Escape Special Characters in Strings
**Problem**: Characters like `&`, `<`, `>`, `|` have special meaning in PowerShell and need proper escaping.

❌ **BAD**:
```powershell
Write-Host "Testing & Validation" -ForegroundColor Green
```

✅ **GOOD**:
```powershell
Write-Host "Testing and Validation" -ForegroundColor Green
# Or use proper escaping if & is required
Write-Host "Testing `& Validation" -ForegroundColor Green
```

### 3. File Encoding
**Best Practice**: Always use UTF-8 encoding without BOM for PowerShell scripts.

```powershell
# When creating files programmatically
$content | Out-File -FilePath "script.ps1" -Encoding UTF8
```

## Script Structure and Control Flow

### 4. Switch Statement Simplicity
**Problem**: Complex nested switch statements with large case blocks can cause parsing issues.

❌ **BAD**:
```powershell
switch ($value) {
    1 {
        # 50+ lines of complex logic
        if ($condition) {
            foreach ($item in $list) {
                # More nesting...
            }
        }
    }
    2 { # More complex cases... }
}
```

✅ **GOOD**:
```powershell
switch ($value) {
    1 { ProcessCase1 }
    2 { ProcessCase2 }
}

function ProcessCase1 {
    # Complex logic in a separate function
    if ($condition) {
        foreach ($item in $list) {
            # Processing...
        }
    }
}
```

### 5. Break Long Scripts into Functions
**Best Practice**: Keep scripts modular and testable.

✅ **GOOD**:
```powershell
function Build-Project {
    Write-Host "Building project..." -ForegroundColor Yellow
    dotnet build -q
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed"
    }
}

function Test-McpServer {
    param($testRequests)
    # Test logic here
}

# Main script
Build-Project
Test-McpServer -testRequests $requests
```

### 6. Avoid Deep Nesting
**Problem**: Deeply nested code is hard to read and maintain.

❌ **BAD**:
```powershell
if ($condition1) {
    if ($condition2) {
        foreach ($item in $items) {
            if ($item.Property) {
                # Do something
            }
        }
    }
}
```

✅ **GOOD**:
```powershell
if (-not $condition1) { return }
if (-not $condition2) { return }

foreach ($item in $items) {
    if (-not $item.Property) { continue }
    # Do something
}
```

## Error Handling and Validation

### 7. Always Include Error Handling
**Best Practice**: Use try-catch blocks and validate inputs.

✅ **GOOD**:
```powershell
try {
    $response = $line | ConvertFrom-Json
    if ($response.result) {
        # Process result
    }
} catch {
    Write-Host "ERROR: Failed to parse response" -ForegroundColor Red
    Write-Host "  $_" -ForegroundColor Yellow
}
```

### 8. Validate File Paths Before Use
**Best Practice**: Always check if files and directories exist.

✅ **GOOD**:
```powershell
$configPath = Join-Path $PSScriptRoot "config.json"
if (-not (Test-Path $configPath)) {
    Write-Host "ERROR: Config file not found: $configPath" -ForegroundColor Red
    return
}
```

### 9. Check Command Results
**Best Practice**: Always verify that commands succeeded.

✅ **GOOD**:
```powershell
dotnet build -q
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit 1
}
```

## String Handling

### 10. Use Here-Strings for Multi-Line Content
**Best Practice**: Use here-strings for multi-line JSON or complex strings.

✅ **GOOD**:
```powershell
$jsonRequest = @"
{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize"
}
"@
```

### 11. Array of Strings for Test Data
**Best Practice**: Use string arrays for single-line JSON requests.

✅ **GOOD**:
```powershell
$testRequests = @(
    '{"jsonrpc":"2.0","id":1,"method":"initialize"}'
    '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
)
```

### 12. Avoid String Truncation Errors
**Problem**: Using `Substring()` without checking length can cause errors.

❌ **BAD**:
```powershell
$preview = $text.Substring(0, 100)
```

✅ **GOOD**:
```powershell
$maxLength = [Math]::Min(100, $text.Length)
$preview = $text.Substring(0, $maxLength)
```

## Output and Formatting

### 13. Use Consistent Output Formatting
**Best Practice**: Use clear, consistent formatting for user output.

✅ **GOOD**:
```powershell
Write-Host "`nTest Results:" -ForegroundColor Cyan
Write-Host "=============" -ForegroundColor Cyan
Write-Host "  Test 1: PASSED" -ForegroundColor Green
Write-Host "  Test 2: FAILED" -ForegroundColor Red
```

### 14. Suppress Unwanted Output
**Best Practice**: Redirect or suppress output you don't want to display.

✅ **GOOD**:
```powershell
# Suppress errors but capture output
$output = Get-Content $inputFile | adr-cli.exe mcp 2>$null

# Suppress all output
$null = dotnet build -q

# Quiet build
dotnet build -q
```

## Variables and Parameters

### 15. Use Clear, Descriptive Variable Names
**Best Practice**: Variable names should be self-documenting.

❌ **BAD**:
```powershell
$r = $line | ConvertFrom-Json
$c = $r.result.content[0].text
```

✅ **GOOD**:
```powershell
$response = $line | ConvertFrom-Json
$content = $response.result.content[0].text
```

### 16. Initialize Counters and Collections
**Best Practice**: Always initialize variables before use.

✅ **GOOD**:
```powershell
$responseCount = 0
$errors = @()
$results = @{}
```

## Testing and Validation

### 17. Include Build Step in Tests
**Best Practice**: Always build before running tests to ensure latest code.

✅ **GOOD**:
```powershell
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build -q
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}
```

### 18. Clean Up Temporary Files
**Best Practice**: Always remove temporary files, even if script fails.

✅ **GOOD**:
```powershell
$inputFile = "test_input.txt"
try {
    $testRequests | Out-File -FilePath $inputFile -Encoding UTF8
    # Run tests...
} finally {
    Remove-Item $inputFile -ErrorAction SilentlyContinue
}
```

## Path Handling

### 19. Use Join-Path for Path Construction
**Best Practice**: Never concatenate paths with string operations.

❌ **BAD**:
```powershell
$configPath = $baseDir + "\config.json"
$outputPath = "$baseDir\output\results.txt"
```

✅ **GOOD**:
```powershell
$configPath = Join-Path $baseDir "config.json"
$outputPath = Join-Path $baseDir "output" "results.txt"
```

### 20. Use $PSScriptRoot for Relative Paths
**Best Practice**: Reference files relative to script location.

✅ **GOOD**:
```powershell
$configPath = Join-Path $PSScriptRoot "config.json"
$binPath = Join-Path $PSScriptRoot "bin" "Debug" "app.exe"
```

## Comments and Documentation

### 21. Add Script Headers
**Best Practice**: Include a header describing the script's purpose.

✅ **GOOD**:
```powershell
# Test script for MCP server functionality
# Tests: initialize, tools list, basic operations
# Requirements: dotnet build must succeed first
# Output: Test results with color-coded status

Write-Host "Testing MCP Server..." -ForegroundColor Green
```

### 22. Comment Complex Logic
**Best Practice**: Explain non-obvious code sections.

✅ **GOOD**:
```powershell
# Extract task files from output, skipping header lines
$lines = $content -split "`n" | Where-Object { $_ -match "^\d{5}" }

# Check if tasks folder is initialized before proceeding
if (-not (Test-Path $tasksFolder)) {
    Write-Host "Tasks folder not initialized" -ForegroundColor Yellow
    return
}
```

## Performance Considerations

### 23. Avoid Unnecessary Pipeline Operations
**Best Practice**: Use efficient filtering and processing.

❌ **BAD**:
```powershell
$items | ForEach-Object { $_ } | Where-Object { $_.Name } | ForEach-Object { $_.Name }
```

✅ **GOOD**:
```powershell
$items | Where-Object { $_.Name } | Select-Object -ExpandProperty Name
```

### 24. Use -ErrorAction Appropriately
**Best Practice**: Specify error handling behavior explicitly.

✅ **GOOD**:
```powershell
Remove-Item $tempFile -ErrorAction SilentlyContinue
$config = Get-Content $configFile -ErrorAction Stop
Test-Path $optional -ErrorAction Ignore
```

## Summary Checklist

When creating PowerShell scripts:

- [ ] No emojis or special Unicode characters
- [ ] All special characters properly escaped
- [ ] UTF-8 encoding without BOM
- [ ] Functions instead of complex nested blocks
- [ ] Maximum 3 levels of nesting
- [ ] Error handling with try-catch
- [ ] File path validation
- [ ] Command result checking
- [ ] Proper use of here-strings
- [ ] String length validation before Substring()
- [ ] Consistent output formatting
- [ ] Clear variable names
- [ ] Variables initialized
- [ ] Build step included
- [ ] Temporary files cleaned up
- [ ] Join-Path for all paths
- [ ] $PSScriptRoot for relative paths
- [ ] Script header comments
- [ ] Complex logic explained
- [ ] Efficient pipeline operations
- [ ] Explicit -ErrorAction settings

## Testing Your Script

Before committing a PowerShell script, validate it:

```powershell
# Syntax validation
$errors = $null
$null = [System.Management.Automation.PSParser]::Tokenize(
    (Get-Content script.ps1 -Raw),
    [ref]$errors
)
if ($errors) {
    $errors | Format-List
}

# Or use PowerShell's built-in parser
$scriptContent = Get-Content script.ps1 -Raw
$tokens = $null
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseInput(
    $scriptContent,
    [ref]$tokens,
    [ref]$parseErrors
)
if ($parseErrors) {
    $parseErrors
}
```

## Common Pitfalls to Avoid

1. **Mixing single and double quotes inconsistently** - Use single quotes for literal strings, double quotes when you need variable expansion
2. **Forgetting to escape backticks in strings** - Backtick is PowerShell's escape character
3. **Not handling null or empty results** - Always check if collections or objects are null before accessing properties
4. **Using wrong comparison operators** - Use `-eq`, `-ne`, `-gt`, `-lt` not `==`, `!=`, `>`, `<`
5. **Not setting execution policy context** - Remember scripts may fail in restricted environments
6. **Assuming paths are Windows-style** - Use platform-agnostic path operations when possible

## Resources

- [PowerShell Best Practices](https://docs.microsoft.com/en-us/powershell/scripting/developer/cmdlet/cmdlet-development-guidelines)
- [PowerShell Style Guide](https://poshcode.gitbook.io/powershell-practice-and-style/)
- [PowerShell Approved Verbs](https://docs.microsoft.com/en-us/powershell/scripting/developer/cmdlet/approved-verbs-for-windows-powershell-commands)
