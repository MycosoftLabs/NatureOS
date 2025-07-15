#!/usr/bin/env pwsh

# NatureOS Comprehensive Testing Script
# Tests all system components in a systematic CI/CD manner

param(
    [Parameter(Mandatory=$false)]
    [string]$Environment = "local",
    
    [Parameter(Mandatory=$false)]
    [string]$ApiBaseUrl = "http://localhost:8080",
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipBuild = $false,
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipExternalTests = $false,
    
    [Parameter(Mandatory=$false)]
    [switch]$Verbose = $false
)

# Script configuration
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Test configuration
$TestResults = @{
    StartTime = Get-Date
    Environment = $Environment
    ApiBaseUrl = $ApiBaseUrl
    Tests = @{}
    OverallSuccess = $true
    Summary = @{}
}

# Logging functions
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
    $TestResults.OverallSuccess = $false
}

function Write-TestInfo {
    param([string]$Message)
    if ($Verbose) {
        Write-Host "  ℹ️  $Message" -ForegroundColor Blue
    }
}

# API testing functions
function Invoke-ApiTest {
    param(
        [string]$Endpoint,
        [string]$Method = "GET",
        [object]$Body = $null,
        [hashtable]$Headers = @{},
        [int]$ExpectedStatusCode = 200
    )
    
    try {
        $uri = "$ApiBaseUrl$Endpoint"
        $params = @{
            Uri = $uri
            Method = $Method
            Headers = $Headers
            ContentType = "application/json"
        }
        
        if ($Body) {
            $params.Body = ($Body | ConvertTo-Json -Depth 10)
        }
        
        $response = Invoke-RestMethod @params
        return @{
            Success = $true
            Data = $response
            StatusCode = 200
        }
    }
    catch {
        return @{
            Success = $false
            Error = $_.Exception.Message
            StatusCode = $_.Exception.Response.StatusCode.value__
        }
    }
}

function Test-ApiEndpoint {
    param(
        [string]$Name,
        [string]$Endpoint,
        [string]$Method = "GET",
        [object]$Body = $null
    )
    
    Write-TestStep "Testing $Name endpoint: $Method $Endpoint"
    
    $result = Invoke-ApiTest -Endpoint $Endpoint -Method $Method -Body $Body
    
    if ($result.Success) {
        Write-TestSuccess "$Name endpoint responded successfully"
        Write-TestInfo "Response received with data"
        return $true
    } else {
        Write-TestFailure "$Name endpoint failed: $($result.Error)"
        return $false
    }
}

# Main testing functions
function Test-Prerequisites {
    Write-TestHeader "Testing Prerequisites"
    
    # Test .NET SDK
    Write-TestStep "Checking .NET SDK"
    try {
        $dotnetVersion = dotnet --version
        Write-TestSuccess ".NET SDK found: $dotnetVersion"
    } catch {
        Write-TestFailure ".NET SDK not found"
        return $false
    }
    
    # Test PowerShell version
    Write-TestStep "Checking PowerShell version"
    if ($PSVersionTable.PSVersion.Major -ge 7) {
        Write-TestSuccess "PowerShell version: $($PSVersionTable.PSVersion)"
    } else {
        Write-TestFailure "PowerShell 7+ required. Current: $($PSVersionTable.PSVersion)"
        return $false
    }
    
    return $true
}

function Build-Solution {
    if ($SkipBuild) {
        Write-TestInfo "Skipping build (SkipBuild flag set)"
        return $true
    }
    
    Write-TestHeader "Building NatureOS Solution"
    
    Write-TestStep "Restoring NuGet packages"
    try {
        dotnet restore NatureOS.sln | Out-Null
        Write-TestSuccess "Packages restored successfully"
    } catch {
        Write-TestFailure "Failed to restore packages: $_"
        return $false
    }
    
    Write-TestStep "Building solution"
    try {
        dotnet build NatureOS.sln --configuration Release --no-restore | Out-Null
        Write-TestSuccess "Solution built successfully"
    } catch {
        Write-TestFailure "Failed to build solution: $_"
        return $false
    }
    
    return $true
}

function Test-CoreInfrastructure {
    Write-TestHeader "Testing Core Infrastructure"
    
    $tests = @(
        @{ Name = "Health Check"; Endpoint = "/health" }
        @{ Name = "API Root"; Endpoint = "/" }
        @{ Name = "Events API"; Endpoint = "/api/events" }
        @{ Name = "Devices API"; Endpoint = "/api/devices" }
        @{ Name = "Funga API"; Endpoint = "/api/funga" }
    )
    
    $successCount = 0
    foreach ($test in $tests) {
        if (Test-ApiEndpoint -Name $test.Name -Endpoint $test.Endpoint) {
            $successCount++
        }
    }
    
    $TestResults.Tests.CoreInfrastructure = @{
        Total = $tests.Count
        Passed = $successCount
        Success = ($successCount -eq $tests.Count)
    }
    
    return $TestResults.Tests.CoreInfrastructure.Success
}

function Test-ExternalDataIntegration {
    if ($SkipExternalTests) {
        Write-TestInfo "Skipping external data tests (SkipExternalTests flag set)"
        return $true
    }
    
    Write-TestHeader "Testing External Data Integration"
    
    Write-TestStep "Testing comprehensive data enrichment"
    $result = Invoke-ApiTest -Endpoint "/api/system-test/test-external-data" -Method "POST"
    
    if ($result.Success) {
        Write-TestSuccess "External data integration test completed"
        Write-TestInfo "FungiDB: $($result.Data.fungiDbTest.success)"
        Write-TestInfo "iNaturalist: $($result.Data.iNaturalistTest.success)"
        Write-TestInfo "MycoBank: $($result.Data.mycoBankTest.success)"
        Write-TestInfo "Chemical DBs: $($result.Data.chemicalDatabasesTest.success)"
        
        $TestResults.Tests.ExternalDataIntegration = @{
            Success = $result.Data.overallSuccess
            Details = $result.Data
        }
        
        return $result.Data.overallSuccess
    } else {
        Write-TestFailure "External data integration test failed: $($result.Error)"
        $TestResults.Tests.ExternalDataIntegration = @{ Success = $false }
        return $false
    }
}

function Test-MasSystem {
    Write-TestHeader "Testing MAS (Mycelium Analysis System)"
    
    Write-TestStep "Testing MAS capabilities"
    $result = Invoke-ApiTest -Endpoint "/api/system-test/test-mas" -Method "POST"
    
    if ($result.Success) {
        Write-TestSuccess "MAS system test completed"
        Write-TestInfo "Network Analysis: $($result.Data.networkAnalysisTest.success)"
        Write-TestInfo "Signal Analysis: $($result.Data.signalAnalysisTest.success)"
        Write-TestInfo "Behavior Prediction: $($result.Data.behaviorPredictionTest.success)"
        Write-TestInfo "Biodiversity Analysis: $($result.Data.biodiversityAnalysisTest.success)"
        Write-TestInfo "System Status: $($result.Data.systemStatusTest.success)"
        
        $TestResults.Tests.MasSystem = @{
            Success = $result.Data.overallSuccess
            Details = $result.Data
        }
        
        return $result.Data.overallSuccess
    } else {
        Write-TestFailure "MAS system test failed: $($result.Error)"
        $TestResults.Tests.MasSystem = @{ Success = $false }
        return $false
    }
}

function Test-DeviceDataFlow {
    Write-TestHeader "Testing Device-to-Cloud Data Flow"
    
    $testRequest = @{
        deviceId = "test-device-comprehensive"
        testRealDevice = $false
        testDurationMinutes = 2
    }
    
    Write-TestStep "Testing device data flow"
    $result = Invoke-ApiTest -Endpoint "/api/system-test/test-device-flow" -Method "POST" -Body $testRequest
    
    if ($result.Success) {
        Write-TestSuccess "Device data flow test completed"
        Write-TestInfo "Connectivity: $($result.Data.connectivityTest.success)"
        Write-TestInfo "Telemetry Ingestion: $($result.Data.telemetryIngestionTest.success)"
        Write-TestInfo "Signal Processing: $($result.Data.signalProcessingTest.success)"
        Write-TestInfo "Data Enrichment: $($result.Data.dataEnrichmentTest.success)"
        Write-TestInfo "API Availability: $($result.Data.apiAvailabilityTest.success)"
        
        $TestResults.Tests.DeviceDataFlow = @{
            Success = $result.Data.overallSuccess
            Details = $result.Data
        }
        
        return $result.Data.overallSuccess
    } else {
        Write-TestFailure "Device data flow test failed: $($result.Error)"
        $TestResults.Tests.DeviceDataFlow = @{ Success = $false }
        return $false
    }
}

function Test-WebsiteIntegration {
    Write-TestHeader "Testing Website Integration"
    
    Write-TestStep "Testing website integration components"
    $result = Invoke-ApiTest -Endpoint "/api/system-test/test-website-integration" -Method "POST"
    
    if ($result.Success) {
        Write-TestSuccess "Website integration test completed"
        Write-TestInfo "Dashboard API: $($result.Data.dashboardApiTest.success)"
        Write-TestInfo "MYCA Integration: $($result.Data.mycaIntegrationTest.success)"
        Write-TestInfo "Live Data Streaming: $($result.Data.liveDataStreamingTest.success)"
        Write-TestInfo "Data Synchronization: $($result.Data.dataSynchronizationTest.success)"
        
        $TestResults.Tests.WebsiteIntegration = @{
            Success = $result.Data.overallSuccess
            Details = $result.Data
        }
        
        return $result.Data.overallSuccess
    } else {
        Write-TestFailure "Website integration test failed: $($result.Error)"
        $TestResults.Tests.WebsiteIntegration = @{ Success = $false }
        return $false
    }
}

function Test-Performance {
    Write-TestHeader "Testing Performance and Load"
    
    $performanceRequest = @{
        concurrentRequests = 10
        testDurationSeconds = 30
        endpointsToTest = @("/api/events", "/api/devices", "/api/funga")
    }
    
    Write-TestStep "Running performance tests"
    $result = Invoke-ApiTest -Endpoint "/api/system-test/test-performance" -Method "POST" -Body $performanceRequest
    
    if ($result.Success) {
        Write-TestSuccess "Performance test completed"
        Write-TestInfo "API Response Times: $($result.Data.apiResponseTimeTest.success)"
        Write-TestInfo "Database Performance: $($result.Data.databasePerformanceTest.success)"
        Write-TestInfo "Concurrent Load: $($result.Data.concurrentLoadTest.success)"
        Write-TestInfo "Resource Utilization: $($result.Data.resourceUtilizationTest.success)"
        
        $TestResults.Tests.Performance = @{
            Success = $result.Data.overallSuccess
            Details = $result.Data
        }
        
        return $result.Data.overallSuccess
    } else {
        Write-TestFailure "Performance test failed: $($result.Error)"
        $TestResults.Tests.Performance = @{ Success = $false }
        return $false
    }
}

function Test-ComprehensiveEndToEnd {
    Write-TestHeader "Running Comprehensive End-to-End Test"
    
    $comprehensiveRequest = @{
        includeExternalData = -not $SkipExternalTests
        includeDeviceTests = $true
        includeMasTests = $true
        includePerformanceTests = $true
        testDeviceIds = @("comprehensive-test-device-1", "comprehensive-test-device-2")
    }
    
    Write-TestStep "Executing comprehensive system test"
    $result = Invoke-ApiTest -Endpoint "/api/system-test/run-comprehensive" -Method "POST" -Body $comprehensiveRequest
    
    if ($result.Success) {
        Write-TestSuccess "Comprehensive end-to-end test completed"
        Write-TestInfo "Overall Score: $($result.Data.overallScore)%"
        Write-TestInfo "Total Duration: $($result.Data.totalDuration)"
        
        $TestResults.Tests.ComprehensiveEndToEnd = @{
            Success = $result.Data.overallSuccess
            Score = $result.Data.overallScore
            Details = $result.Data
        }
        
        return $result.Data.overallSuccess
    } else {
        Write-TestFailure "Comprehensive end-to-end test failed: $($result.Error)"
        $TestResults.Tests.ComprehensiveEndToEnd = @{ Success = $false }
        return $false
    }
}

function Test-SpecificIntegrations {
    Write-TestHeader "Testing Specific Integration Points"
    
    # Test Mycosoft website integration
    Write-TestStep "Testing Mycosoft website API endpoints"
    $websiteTests = @(
        @{ Name = "Dashboard Data"; Endpoint = "/api/mycosoft/website/dashboard" }
        @{ Name = "Live Data"; Endpoint = "/api/mycosoft/website/live-data" }
        @{ Name = "MYCA Query"; Endpoint = "/api/mycosoft/myca/query"; Method = "POST"; Body = @{ question = "What species are active?" } }
    )
    
    $successCount = 0
    foreach ($test in $websiteTests) {
        $method = if ($test.Method) { $test.Method } else { "GET" }
        if (Test-ApiEndpoint -Name $test.Name -Endpoint $test.Endpoint -Method $method -Body $test.Body) {
            $successCount++
        }
    }
    
    # Test Mushroom 1 device integration
    Write-TestStep "Testing Mushroom 1 device integration"
    $mushroomTelemetry = @{
        deviceId = "test-mushroom-1"
        timestamp = (Get-Date).ToString("o")
        bioelectricChannels = @(1.2, 0.8, 1.5, 0.9, 1.1, 0.7, 1.3, 1.0)
        temperature = 23.5
        humidity = 65.2
        pressure = 1013.25
        gasResistance = 25000
        vocIndex = 150
        location = @{
            latitude = 47.6062
            longitude = -122.3321
        }
    }
    
    if (Test-ApiEndpoint -Name "Mushroom 1 Telemetry" -Endpoint "/api/mycosoft/mushroom1/telemetry" -Method "POST" -Body $mushroomTelemetry) {
        $successCount++
    }
    
    $TestResults.Tests.SpecificIntegrations = @{
        Total = $websiteTests.Count + 1
        Passed = $successCount
        Success = ($successCount -eq ($websiteTests.Count + 1))
    }
    
    return $TestResults.Tests.SpecificIntegrations.Success
}

function Generate-TestReport {
    Write-TestHeader "Test Report Generation"
    
    $TestResults.EndTime = Get-Date
    $TestResults.TotalDuration = $TestResults.EndTime - $TestResults.StartTime
    
    # Calculate summary statistics
    $totalTests = 0
    $passedTests = 0
    
    foreach ($testCategory in $TestResults.Tests.Keys) {
        $test = $TestResults.Tests[$testCategory]
        if ($test.Total) {
            $totalTests += $test.Total
            $passedTests += $test.Passed
        } elseif ($test.Success) {
            $totalTests += 1
            $passedTests += 1
        } else {
            $totalTests += 1
        }
    }
    
    $TestResults.Summary = @{
        TotalTests = $totalTests
        PassedTests = $passedTests
        FailedTests = $totalTests - $passedTests
        SuccessRate = if ($totalTests -gt 0) { [math]::Round(($passedTests / $totalTests) * 100, 2) } else { 0 }
    }
    
    # Display report
    Write-Host "`n📊 Test Summary Report" -ForegroundColor Magenta
    Write-Host ("=" * 80) -ForegroundColor Magenta
    Write-Host "Environment: $($TestResults.Environment)" -ForegroundColor White
    Write-Host "API Base URL: $($TestResults.ApiBaseUrl)" -ForegroundColor White
    Write-Host "Start Time: $($TestResults.StartTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor White
    Write-Host "End Time: $($TestResults.EndTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor White
    Write-Host "Total Duration: $($TestResults.TotalDuration.ToString('hh\:mm\:ss'))" -ForegroundColor White
    Write-Host ""
    Write-Host "Test Results:" -ForegroundColor White
    Write-Host "  Total Tests: $($TestResults.Summary.TotalTests)" -ForegroundColor White
    Write-Host "  Passed: $($TestResults.Summary.PassedTests)" -ForegroundColor Green
    Write-Host "  Failed: $($TestResults.Summary.FailedTests)" -ForegroundColor Red
    Write-Host "  Success Rate: $($TestResults.Summary.SuccessRate)%" -ForegroundColor White
    Write-Host ""
    
    # Test category breakdown
    Write-Host "Test Category Breakdown:" -ForegroundColor White
    foreach ($testCategory in $TestResults.Tests.Keys) {
        $test = $TestResults.Tests[$testCategory]
        $status = if ($test.Success) { "✅ PASS" } else { "❌ FAIL" }
        Write-Host "  $testCategory`: $status" -ForegroundColor White
    }
    
    Write-Host ""
    $overallStatus = if ($TestResults.OverallSuccess) { "✅ SUCCESS" } else { "❌ FAILURE" }
    Write-Host "Overall Result: $overallStatus" -ForegroundColor $(if ($TestResults.OverallSuccess) { "Green" } else { "Red" })
    
    # Save report to file
    $reportPath = "test-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
    $TestResults | ConvertTo-Json -Depth 10 | Out-File -FilePath $reportPath -Encoding UTF8
    Write-Host "`n📄 Detailed report saved to: $reportPath" -ForegroundColor Blue
    
    return $TestResults.OverallSuccess
}

# Main execution
function Main {
    Write-Host "🌟 NatureOS Comprehensive Testing Suite" -ForegroundColor Magenta
    Write-Host "Starting systematic testing of the entire NatureOS ecosystem..." -ForegroundColor White
    Write-Host ""
    
    # Run all tests in sequence
    $testSuccess = $true
    
    $testSuccess = (Test-Prerequisites) -and $testSuccess
    $testSuccess = (Build-Solution) -and $testSuccess
    $testSuccess = (Test-CoreInfrastructure) -and $testSuccess
    $testSuccess = (Test-ExternalDataIntegration) -and $testSuccess
    $testSuccess = (Test-MasSystem) -and $testSuccess
    $testSuccess = (Test-DeviceDataFlow) -and $testSuccess
    $testSuccess = (Test-WebsiteIntegration) -and $testSuccess
    $testSuccess = (Test-Performance) -and $testSuccess
    $testSuccess = (Test-SpecificIntegrations) -and $testSuccess
    $testSuccess = (Test-ComprehensiveEndToEnd) -and $testSuccess
    
    # Generate final report
    $reportSuccess = Generate-TestReport
    
    # Exit with appropriate code
    if ($TestResults.OverallSuccess -and $reportSuccess) {
        Write-Host "`n🎉 All tests completed successfully! NatureOS system is ready for production." -ForegroundColor Green
        exit 0
    } else {
        Write-Host "`n💥 Some tests failed. Please review the report and fix issues before deployment." -ForegroundColor Red
        exit 1
    }
}

# Execute main function
Main 