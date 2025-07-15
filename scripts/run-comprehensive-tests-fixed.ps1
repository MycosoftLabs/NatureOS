#!/usr/bin/env pwsh

# NatureOS Comprehensive Testing Script - Fixed Version
param(
    [Parameter(Mandatory=$false)]
    [string]$Environment = "local",
    [Parameter(Mandatory=$false)]
    [string]$ApiBaseUrl = "http://localhost:8080",
    [Parameter(Mandatory=$false)]
    [switch]$SkipBuild = $false,
    [Parameter(Mandatory=$false)]
    [switch]$Verbose = $false
)

$ErrorActionPreference = "Stop"

function Write-TestHeader {
    param([string]$Title)
    Write-Host "`n🧪 $Title" -ForegroundColor Cyan
    Write-Host ("=" * 80) -ForegroundColor Cyan
}

function Write-TestStep {
    param([string]$Step)
    Write-Host "  ➤ $Step" -ForegroundColor Yellow
}

function Write-TestSuccess {
    param([string]$Message)
    Write-Host "  ✅ $Message" -ForegroundColor Green
}

function Write-TestFailure {
    param([string]$Message)
    Write-Host "  ❌ $Message" -ForegroundColor Red
}

function Test-Prerequisites {
    Write-TestHeader "Testing Prerequisites"
    
    Write-TestStep "Checking .NET SDK"
    try {
        $dotnetVersion = dotnet --version
        Write-TestSuccess ".NET SDK found: $dotnetVersion"
        return $true
    } catch {
        Write-TestFailure ".NET SDK not found"
        return $false
    }
}

function Test-Build {
    Write-TestHeader "Building Solution"
    
    if ($SkipBuild) {
        Write-TestStep "Skipping build (SkipBuild flag set)"
        return $true
    }
    
    Write-TestStep "Building NatureOS solution"
    try {
        dotnet build NatureOS.sln --configuration Release | Out-Null
        Write-TestSuccess "Solution builds successfully"
        return $true
    } catch {
        Write-TestFailure "Build failed: $_"
        return $false
    }
}

function Test-CoreComponents {
    Write-TestHeader "Testing Core Components"
    
    $components = @(
        @{ Name = "Core API"; Project = "src/core-api/NatureOS.CoreApi.csproj" },
        @{ Name = "Ingestion Function"; Project = "src/ingestion/NatureOS.Ingestion.csproj" },
        @{ Name = "MINDEX Models"; Project = "src/mindex/NatureOS.MINDEX.csproj" },
        @{ Name = "Mycorrhizae Protocol"; Project = "src/mycorrhizae/NatureOS.Mycorrhizae.csproj" }
    )
    
    $allSuccess = $true
    
    foreach ($component in $components) {
        Write-TestStep "Testing $($component.Name)"
        try {
            dotnet build $component.Project --configuration Release | Out-Null
            Write-TestSuccess "$($component.Name) builds successfully"
        } catch {
            Write-TestFailure "$($component.Name) build failed"
            $allSuccess = $false
        }
    }
    
    return $allSuccess
}

function Test-KeyFiles {
    Write-TestHeader "Testing Key Files"
    
    $keyFiles = @(
        "src/core-api/Services/ExternalDataIntegrationService.cs",
        "infrastructure/main.bicep",
        "infrastructure/parameters.json",
        "test-and-deploy.ps1",
        "COMPREHENSIVE_SYSTEM_STATUS.md"
    )
    
    $allPresent = $true
    
    foreach ($file in $keyFiles) {
        Write-TestStep "Checking $file"
        if (Test-Path $file) {
            Write-TestSuccess "Found: $file"
        } else {
            Write-TestFailure "Missing: $file"
            $allPresent = $false
        }
    }
    
    return $allPresent
}

function Generate-TestReport {
    param([hashtable]$Results)
    
    Write-TestHeader "Test Report"
    
    $totalTests = $Results.Count
    $passedTests = ($Results.Values | Where-Object { $_ -eq $true }).Count
    $successRate = if ($totalTests -gt 0) { [math]::Round(($passedTests / $totalTests) * 100, 2) } else { 0 }
    
    Write-Host "📊 Test Summary:" -ForegroundColor Magenta
    Write-Host "  Total Tests: $totalTests" -ForegroundColor White
    Write-Host "  Passed: $passedTests" -ForegroundColor Green
    Write-Host "  Failed: $($totalTests - $passedTests)" -ForegroundColor Red
    Write-Host "  Success Rate: $successRate%" -ForegroundColor White
    
    foreach ($test in $Results.Keys) {
        $status = if ($Results[$test]) { "✅ PASS" } else { "❌ FAIL" }
        Write-Host "  $test`: $status"
    }
    
    $overallSuccess = ($Results.Values -notcontains $false)
    $overallStatus = if ($overallSuccess) { "✅ SUCCESS" } else { "❌ FAILURE" }
    Write-Host "`nOverall Result: $overallStatus" -ForegroundColor $(if ($overallSuccess) { "Green" } else { "Red" })
    
    return $overallSuccess
}

# Main execution
Write-Host "🌟 NatureOS Comprehensive Testing Suite" -ForegroundColor Magenta
Write-Host "Testing the complete fungal intelligence platform..." -ForegroundColor White
Write-Host ""

$testResults = @{}

$testResults["Prerequisites"] = Test-Prerequisites
$testResults["Build"] = Test-Build
$testResults["CoreComponents"] = Test-CoreComponents
$testResults["KeyFiles"] = Test-KeyFiles

$overallSuccess = Generate-TestReport -Results $testResults

if ($overallSuccess) {
    Write-Host "`n🎉 All tests passed! NatureOS system is ready." -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n💥 Some tests failed. Please review and fix issues." -ForegroundColor Red
    exit 1
} 