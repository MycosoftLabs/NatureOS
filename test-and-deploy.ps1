#!/usr/bin/env pwsh

# NatureOS Complete System Test & Azure Deployment Script
# Comprehensive testing and deployment for the entire fungal intelligence platform

param(
    [Parameter(Mandatory=$false)]
    [string]$Environment = "staging",
    
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroup = "natureos-rg",
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "westus2",
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipTests = $false,
    
    [Parameter(Mandatory=$false)]
    [switch]$DeployOnly = $false,
    
    [Parameter(Mandatory=$false)]
    [switch]$TestOnly = $false
)

# Configuration
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Colors for output
$Green = "Green"
$Red = "Red"
$Yellow = "Yellow"
$Cyan = "Cyan"
$Blue = "Blue"

function Write-Step {
    param([string]$Message)
    Write-Host "➤ $Message" -ForegroundColor $Yellow
}

function Write-Success {
    param([string]$Message)
    Write-Host "✅ $Message" -ForegroundColor $Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "⚠️  $Message" -ForegroundColor $Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "❌ $Message" -ForegroundColor $Red
}

function Write-Header {
    param([string]$Title)
    Write-Host "`n🌟 $Title" -ForegroundColor $Cyan
    Write-Host ("=" * 80) -ForegroundColor $Cyan
}

function Test-Prerequisites {
    Write-Header "Testing Prerequisites"
    
    # Test .NET SDK
    Write-Step "Checking .NET SDK"
    try {
        $dotnetVersion = dotnet --version
        Write-Success ".NET SDK found: $dotnetVersion"
    } catch {
        Write-Error ".NET SDK not found. Please install .NET 8 SDK."
        return $false
    }
    
    # Test Azure CLI
    Write-Step "Checking Azure CLI"
    try {
        $azVersion = az version --output table | Select-Object -First 1
        Write-Success "Azure CLI found: $azVersion"
    } catch {
        Write-Error "Azure CLI not found. Please install Azure CLI."
        return $false
    }
    
    # Test PowerShell version
    Write-Step "Checking PowerShell version"
    if ($PSVersionTable.PSVersion.Major -ge 7) {
        Write-Success "PowerShell version: $($PSVersionTable.PSVersion)"
    } else {
        Write-Warning "PowerShell 7+ recommended. Current: $($PSVersionTable.PSVersion)"
    }
    
    return $true
}

function Build-Solution {
    Write-Header "Building NatureOS Solution"
    
    Write-Step "Cleaning previous builds"
    dotnet clean NatureOS.sln
    
    Write-Step "Restoring NuGet packages"
    try {
        dotnet restore NatureOS.sln
        Write-Success "Packages restored successfully"
    } catch {
        Write-Error "Failed to restore packages: $_"
        return $false
    }
    
    Write-Step "Building solution in Release mode"
    try {
        dotnet build NatureOS.sln --configuration Release --no-restore
        Write-Success "Solution built successfully"
    } catch {
        Write-Error "Failed to build solution: $_"
        return $false
    }
    
    return $true
}

function Test-CoreComponents {
    Write-Header "Testing Core Components"
    
    $testResults = @{}
    
    # Test Core API
    Write-Step "Testing Core API compilation and services"
    try {
        dotnet build src/core-api/NatureOS.CoreApi.csproj --configuration Release
        $testResults["CoreAPI"] = $true
        Write-Success "Core API builds successfully"
    } catch {
        $testResults["CoreAPI"] = $false
        Write-Error "Core API build failed: $_"
    }
    
    # Test Ingestion Function
    Write-Step "Testing Ingestion Function compilation"
    try {
        dotnet build src/ingestion/NatureOS.Ingestion.csproj --configuration Release
        $testResults["Ingestion"] = $true
        Write-Success "Ingestion Function builds successfully"
    } catch {
        $testResults["Ingestion"] = $false
        Write-Error "Ingestion Function build failed: $_"
    }
    
    # Test MINDEX models
    Write-Step "Testing MINDEX data models"
    try {
        dotnet build src/mindex/NatureOS.MINDEX.csproj --configuration Release
        $testResults["MINDEX"] = $true
        Write-Success "MINDEX models build successfully"
    } catch {
        $testResults["MINDEX"] = $false
        Write-Error "MINDEX models build failed: $_"
    }
    
    # Test Mycorrhizae protocol
    Write-Step "Testing Mycorrhizae protocol"
    try {
        dotnet build src/mycorrhizae/NatureOS.Mycorrhizae.csproj --configuration Release
        $testResults["Mycorrhizae"] = $true
        Write-Success "Mycorrhizae protocol builds successfully"
    } catch {
        $testResults["Mycorrhizae"] = $false
        Write-Error "Mycorrhizae protocol build failed: $_"
    }
    
    return $testResults
}

function Test-WebsiteIntegration {
    Write-Header "Testing Website Integration Components"
    
    Write-Step "Checking website integration files"
    $websiteComponents = @(
        "website-integration/api/live-data.ts",
        "website-integration/api/myca-query.ts",
        "website-integration/components/LiveDataFeed.tsx",
        "website-integration/components/MYCAInterface.tsx"
    )
    
    $allPresent = $true
    foreach ($component in $websiteComponents) {
        if (Test-Path $component) {
            Write-Success "Found: $component"
        } else {
            Write-Warning "Missing: $component"
            $allPresent = $false
        }
    }
    
    return $allPresent
}

function Test-DeviceIntegration {
    Write-Header "Testing Device Integration"
    
    Write-Step "Checking Mushroom 1 device files"
    $deviceFiles = @(
        "devices/mushroom1/mushroom1.ino",
        "devices/mushroom1/config.h",
        "devices/mushroom1/sensors.h"
    )
    
    $allPresent = $true
    foreach ($file in $deviceFiles) {
        if (Test-Path $file) {
            Write-Success "Found: $file"
        } else {
            Write-Warning "Missing: $file"
            $allPresent = $false
        }
    }
    
    return $allPresent
}

function Test-Infrastructure {
    Write-Header "Testing Infrastructure Configuration"
    
    Write-Step "Checking infrastructure files"
    $infraFiles = @(
        "infrastructure/main.bicep",
        "infrastructure/parameters.json"
    )
    
    $allPresent = $true
    foreach ($file in $infraFiles) {
        if (Test-Path $file) {
            Write-Success "Found: $file"
        } else {
            Write-Warning "Missing: $file"
            $allPresent = $false
        }
    }
    
    return $allPresent
}

function Deploy-To-Azure {
    Write-Header "Deploying to Azure"
    
    Write-Step "Logging into Azure"
    try {
        az login --output none
        Write-Success "Logged into Azure successfully"
    } catch {
        Write-Error "Failed to login to Azure: $_"
        return $false
    }
    
    Write-Step "Creating resource group: $ResourceGroup"
    try {
        az group create --name $ResourceGroup --location $Location --output none
        Write-Success "Resource group created: $ResourceGroup"
    } catch {
        Write-Warning "Resource group may already exist"
    }
    
    Write-Step "Deploying infrastructure"
    try {
        az deployment group create `
            --resource-group $ResourceGroup `
            --template-file infrastructure/main.bicep `
            --parameters infrastructure/parameters.json `
            --output none
        Write-Success "Infrastructure deployed successfully"
    } catch {
        Write-Error "Failed to deploy infrastructure: $_"
        return $false
    }
    
    Write-Step "Deploying Core API"
    try {
        # Package the Core API
        dotnet publish src/core-api/NatureOS.CoreApi.csproj -c Release -o ./publish/core-api
        
        # Deploy to Azure App Service (assuming the app service is created by infrastructure)
        $webAppName = "natureos-api-$Environment"
        az webapp deployment source config-zip `
            --resource-group $ResourceGroup `
            --name $webAppName `
            --src "./publish/core-api.zip" `
            --output none
        Write-Success "Core API deployed to: $webAppName"
    } catch {
        Write-Error "Failed to deploy Core API: $_"
        return $false
    }
    
    Write-Step "Deploying Ingestion Function"
    try {
        # Package the Function App
        dotnet publish src/ingestion/NatureOS.Ingestion.csproj -c Release -o ./publish/ingestion
        
        # Deploy to Azure Function App
        $functionAppName = "natureos-ingestion-$Environment"
        az functionapp deployment source config-zip `
            --resource-group $ResourceGroup `
            --name $functionAppName `
            --src "./publish/ingestion.zip" `
            --output none
        Write-Success "Ingestion Function deployed to: $functionAppName"
    } catch {
        Write-Error "Failed to deploy Ingestion Function: $_"
        return $false
    }
    
    return $true
}

function Generate-Report {
    param(
        [hashtable]$TestResults,
        [bool]$DeploymentSuccess
    )
    
    Write-Header "Final Report"
    
    Write-Host "🧪 Test Results:" -ForegroundColor $Blue
    foreach ($test in $TestResults.Keys) {
        $status = if ($TestResults[$test]) { "✅ PASS" } else { "❌ FAIL" }
        Write-Host "  $test`: $status"
    }
    
    if ($DeploymentSuccess) {
        Write-Host "`n🚀 Deployment: ✅ SUCCESS" -ForegroundColor $Green
        Write-Host "  Resource Group: $ResourceGroup" -ForegroundColor $Blue
        Write-Host "  Environment: $Environment" -ForegroundColor $Blue
        Write-Host "  Core API: https://natureos-api-$Environment.azurewebsites.net" -ForegroundColor $Blue
        Write-Host "  Function App: https://natureos-ingestion-$Environment.azurewebsites.net" -ForegroundColor $Blue
    } else {
        Write-Host "`n🚀 Deployment: ❌ FAILED" -ForegroundColor $Red
    }
    
    $overallSuccess = ($TestResults.Values -notcontains $false) -and $DeploymentSuccess
    $status = if ($overallSuccess) { "✅ SUCCESS" } else { "❌ FAILURE" }
    Write-Host "`n🎯 Overall Result: $status" -ForegroundColor $(if ($overallSuccess) { $Green } else { $Red })
    
    # Save report
    $reportPath = "deployment-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
    $reportData = @{
        Environment = $Environment
        ResourceGroup = $ResourceGroup
        TestResults = $TestResults
        DeploymentSuccess = $DeploymentSuccess
        OverallSuccess = $overallSuccess
        Timestamp = Get-Date
        ApiUrl = "https://natureos-api-$Environment.azurewebsites.net"
        FunctionUrl = "https://natureos-ingestion-$Environment.azurewebsites.net"
    }
    
    $reportData | ConvertTo-Json -Depth 10 | Out-File -FilePath $reportPath -Encoding UTF8
    Write-Host "`n📄 Report saved to: $reportPath" -ForegroundColor $Blue
    
    return $overallSuccess
}

# Main execution
function Main {
    Write-Host "🌟 NatureOS Complete System Test & Azure Deployment" -ForegroundColor $Cyan
    Write-Host "Testing and deploying the entire fungal intelligence platform..." -ForegroundColor $Blue
    Write-Host ""
    
    # Initialize results
    $allTestResults = @{}
    [bool]$deploymentSuccess = $false
    
    # Prerequisites
    if (-not (Test-Prerequisites)) {
        Write-Error "Prerequisites check failed. Exiting."
        exit 1
    }
    
    # Build solution
    if (-not (Build-Solution)) {
        Write-Error "Build failed. Exiting."
        exit 1
    }
    
    # Run tests unless skipped or deploy-only
    if (-not $SkipTests -and -not $DeployOnly) {
        $coreComponentsResult = Test-CoreComponents
        $allTestResults["CoreComponents"] = ($coreComponentsResult.Values -notcontains $false)
        $allTestResults["WebsiteIntegration"] = Test-WebsiteIntegration
        $allTestResults["DeviceIntegration"] = Test-DeviceIntegration
        $allTestResults["Infrastructure"] = Test-Infrastructure
    }
    
    # Deploy to Azure unless test-only mode
    if (-not $TestOnly) {
        $deploymentSuccess = Deploy-To-Azure
    }
    
    # Generate final report
    $overallSuccess = Generate-Report -TestResults $allTestResults -DeploymentSuccess $deploymentSuccess
    
    if ($overallSuccess) {
        Write-Host "`n🎉 All systems operational! NatureOS is ready for production." -ForegroundColor $Green
        Write-Host "🌐 Access your deployment at: https://natureos-api-$Environment.azurewebsites.net" -ForegroundColor $Blue
        exit 0
    } else {
        Write-Host "`n💥 Some components failed. Please review the report and fix issues." -ForegroundColor $Red
        exit 1
    }
}

# Execute main function
Main 