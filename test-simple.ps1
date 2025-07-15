#!/usr/bin/env pwsh

# Simple NatureOS Test Script
Write-Host "🧪 Running NatureOS System Tests" -ForegroundColor Cyan

# Test 1: Build validation
Write-Host "➤ Testing solution build..." -ForegroundColor Yellow
try {
    dotnet build NatureOS.sln --configuration Release | Out-Null
    Write-Host "✅ Solution builds successfully" -ForegroundColor Green
} catch {
    Write-Host "❌ Build failed: $_" -ForegroundColor Red
    exit 1
}

# Test 2: Core API project
Write-Host "➤ Testing Core API..." -ForegroundColor Yellow
try {
    dotnet build src/core-api/NatureOS.CoreApi.csproj --configuration Release | Out-Null
    Write-Host "✅ Core API builds successfully" -ForegroundColor Green
} catch {
    Write-Host "❌ Core API build failed: $_" -ForegroundColor Red
}

# Test 3: Ingestion Function
Write-Host "➤ Testing Ingestion Function..." -ForegroundColor Yellow
try {
    dotnet build src/ingestion/NatureOS.Ingestion.csproj --configuration Release | Out-Null
    Write-Host "✅ Ingestion Function builds successfully" -ForegroundColor Green
} catch {
    Write-Host "❌ Ingestion Function build failed: $_" -ForegroundColor Red
}

# Test 4: MINDEX models
Write-Host "➤ Testing MINDEX models..." -ForegroundColor Yellow
try {
    dotnet build src/mindex/NatureOS.MINDEX.csproj --configuration Release | Out-Null
    Write-Host "✅ MINDEX models build successfully" -ForegroundColor Green
} catch {
    Write-Host "❌ MINDEX models build failed: $_" -ForegroundColor Red
}

# Test 5: Check key files
Write-Host "➤ Checking key system files..." -ForegroundColor Yellow
$keyFiles = @(
    "src/core-api/Services/ExternalDataIntegrationService.cs",
    "infrastructure/main.bicep",
    "test-and-deploy.ps1",
    "COMPREHENSIVE_SYSTEM_STATUS.md"
)

$allPresent = $true
foreach ($file in $keyFiles) {
    if (Test-Path $file) {
        Write-Host "✅ Found: $file" -ForegroundColor Green
    } else {
        Write-Host "❌ Missing: $file" -ForegroundColor Red
        $allPresent = $false
    }
}

# Final result
if ($allPresent) {
    Write-Host "`n🎉 All tests passed! NatureOS system is ready." -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n💥 Some tests failed. Please review above." -ForegroundColor Red
    exit 1
} 