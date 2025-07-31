#!/usr/bin/env pwsh
param(
    [Parameter(Mandatory=$false)]
    [string]$ApiBaseUrl = "https://natureos-api-production.azurewebsites.net",
    [Parameter(Mandatory=$false)]
    [switch]$Verbose = $false
)

# Test configuration
$script:TestResults = @{}
$script:TotalTests = 0
$script:PassedTests = 0
$script:FailedTests = 0

function Write-TestHeader {
    param([string]$Title)
    Write-Host "`n🧪 $Title" -ForegroundColor Cyan
    Write-Host "=" * 80 -ForegroundColor Gray
}

function Write-TestStep {
    param([string]$Message)
    Write-Host "➤ $Message" -ForegroundColor Yellow
}

function Write-TestResult {
    param([string]$Message, [bool]$Success)
    $script:TotalTests++
    if ($Success) {
        Write-Host "✅ $Message" -ForegroundColor Green
        $script:PassedTests++
    } else {
        Write-Host "❌ $Message" -ForegroundColor Red
        $script:FailedTests++
    }
}

function Test-ApiEndpoint {
    param(
        [string]$Endpoint,
        [string]$Method = "GET",
        [hashtable]$Headers = @{},
        [object]$Body = $null,
        [int]$ExpectedStatusCode = 200,
        [string]$Description
    )
    
    try {
        $uri = "$ApiBaseUrl$Endpoint"
        Write-TestStep "Testing: $Method $uri"
        
        $requestParams = @{
            Uri = $uri
            Method = $Method
            Headers = $Headers
            TimeoutSec = 30
        }
        
        if ($Body -and $Method -ne "GET") {
            $requestParams.Body = ($Body | ConvertTo-Json -Depth 10)
            $requestParams.ContentType = "application/json"
        }
        
        $response = Invoke-WebRequest @requestParams -ErrorAction Stop
        
        if ($response.StatusCode -eq $ExpectedStatusCode) {
            Write-TestResult "$Description - Status $($response.StatusCode)" $true
            return @{ Success = $true; Response = $response; Data = ($response.Content | ConvertFrom-Json -ErrorAction SilentlyContinue) }
        } else {
            Write-TestResult "$Description - Expected $ExpectedStatusCode, got $($response.StatusCode)" $false
            return @{ Success = $false; Response = $response }
        }
    }
    catch {
        Write-TestResult "$Description - Error: $($_.Exception.Message)" $false
        return @{ Success = $false; Error = $_.Exception.Message }
    }
}

function Test-CoreApiEndpoints {
    Write-TestHeader "Core API Endpoints Testing"
    
    # Test health endpoint
    $health = Test-ApiEndpoint -Endpoint "/health" -Description "Health check endpoint"
    
    # Test system status
    $status = Test-ApiEndpoint -Endpoint "/api/mycosoft/status" -Description "System status endpoint"
    
    # Test events endpoint
    $events = Test-ApiEndpoint -Endpoint "/api/events" -Description "Events endpoint"
    
    # Test devices endpoint
    $devices = Test-ApiEndpoint -Endpoint "/api/devices" -Description "Devices endpoint"
    
    # Test FUNGA endpoint
    $funga = Test-ApiEndpoint -Endpoint "/api/funga" -Description "FUNGA endpoint"
    
    return @{
        Health = $health.Success
        Status = $status.Success
        Events = $events.Success
        Devices = $devices.Success
        Funga = $funga.Success
    }
}

function Test-RealTimeEndpoints {
    Write-TestHeader "Real-Time Integration Testing"
    
    # Test event stream endpoint (should return stream headers)
    $eventStream = Test-ApiEndpoint -Endpoint "/api/mycosoft/events/stream" -Description "Event stream endpoint"
    
    # Test dashboard stream
    $dashboardStream = Test-ApiEndpoint -Endpoint "/api/mycosoft/dashboard/stream" -Description "Dashboard stream endpoint"
    
    return @{
        EventStream = $eventStream.Success
        DashboardStream = $dashboardStream.Success
    }
}

function Test-MycaIntegration {
    Write-TestHeader "MYCA AI Integration Testing"
    
    # Test MYCA query endpoint
    $mycaQuery = @{
        question = "What is the current system status?"
        context = "integration test"
        userId = "test-user"
    }
    
    $mycaResult = Test-ApiEndpoint -Endpoint "/api/mycosoft/myca/query" -Method "POST" -Body $mycaQuery -Description "MYCA query endpoint"
    
    return @{
        MycaQuery = $mycaResult.Success
    }
}

function Test-DeviceTelemetry {
    Write-TestHeader "Device Telemetry Integration Testing"
    
    # Test mushroom1 telemetry endpoint
    $telemetryData = @{
        timestamp = (Get-Date).ToString("o")
        bioelectric_channels = @(0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8)
        environmental = @{
            temperature = 23.5
            humidity = 65.2
            pressure = 1013.25
            gas_resistance = 45.6
        }
        device_id = "test-mushroom-1"
    }
    
    $telemetryResult = Test-ApiEndpoint -Endpoint "/api/mycosoft/mushroom1/telemetry" -Method "POST" -Body $telemetryData -Description "Mushroom1 telemetry endpoint"
    
    return @{
        TelemetryIngestion = $telemetryResult.Success
    }
}

function Test-ExternalDataIntegration {
    Write-TestHeader "External Data Integration Testing"
    
    # Test data injection endpoints (these might take longer)
    Write-TestStep "Note: External data integration tests may take several minutes..."
    
    # Test FungiDB injection (with small limit for testing)
    $fungiDbResult = Test-ApiEndpoint -Endpoint "/api/external-data/inject/fungidb?limit=1" -Method "POST" -Description "FungiDB data injection" -ExpectedStatusCode 200
    
    return @{
        FungiDbInjection = $fungiDbResult.Success
    }
}

function Test-WebsiteIntegration {
    Write-TestHeader "Website Integration Testing"
    
    # Test website dashboard endpoint
    $websiteDashboard = Test-ApiEndpoint -Endpoint "/api/mycosoft/website/dashboard" -Description "Website dashboard endpoint"
    
    # Test ecosystem sync
    $ecosystemSync = Test-ApiEndpoint -Endpoint "/api/mycosoft/sync" -Method "POST" -Description "Ecosystem synchronization"
    
    return @{
        WebsiteDashboard = $websiteDashboard.Success
        EcosystemSync = $ecosystemSync.Success
    }
}

function Test-PerformanceMetrics {
    Write-TestHeader "Performance and Monitoring Testing"
    
    $performanceResults = @{}
    
    # Test response times
    $endpoints = @(
        "/health",
        "/api/mycosoft/status",
        "/api/events",
        "/api/devices"
    )
    
    foreach ($endpoint in $endpoints) {
        try {
            $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
            $response = Invoke-WebRequest -Uri "$ApiBaseUrl$endpoint" -TimeoutSec 10 -ErrorAction Stop
            $stopwatch.Stop()
            
            $responseTime = $stopwatch.ElapsedMilliseconds
            $performanceResults[$endpoint] = $responseTime
            
            if ($responseTime -lt 2000) {
                Write-TestResult "Response time for $endpoint : ${responseTime}ms" $true
            } else {
                Write-TestResult "Response time for $endpoint : ${responseTime}ms (slow)" $false
            }
        }
        catch {
            Write-TestResult "Performance test for $endpoint failed: $($_.Exception.Message)" $false
        }
    }
    
    return $performanceResults
}

function Generate-IntegrationReport {
    param([hashtable]$TestResults)
    
    Write-TestHeader "Integration Test Report"
    
    $report = @{
        Timestamp = (Get-Date).ToString("o")
        ApiBaseUrl = $ApiBaseUrl
        TotalTests = $script:TotalTests
        PassedTests = $script:PassedTests
        FailedTests = $script:FailedTests
        SuccessRate = if ($script:TotalTests -gt 0) { [math]::Round(($script:PassedTests / $script:TotalTests) * 100, 2) } else { 0 }
        TestResults = $TestResults
    }
    
    # Save detailed report
    $reportFile = "integration-test-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
    $report | ConvertTo-Json -Depth 10 | Out-File -FilePath $reportFile -Encoding UTF8
    
    # Display summary
    Write-Host "`n📊 Test Summary:" -ForegroundColor Cyan
    Write-Host "Total Tests: $($script:TotalTests)" -ForegroundColor White
    Write-Host "Passed: $($script:PassedTests)" -ForegroundColor Green
    Write-Host "Failed: $($script:FailedTests)" -ForegroundColor Red
    Write-Host "Success Rate: $($report.SuccessRate)%" -ForegroundColor $(if ($report.SuccessRate -ge 80) { "Green" } else { "Red" })
    Write-Host "`n📄 Detailed report saved to: $reportFile" -ForegroundColor Gray
    
    # Overall result
    $overallSuccess = $script:FailedTests -eq 0
    if ($overallSuccess) {
        Write-Host "`n🎉 All integration tests passed! NatureOS is fully operational." -ForegroundColor Green
    } else {
        Write-Host "`n💥 Some integration tests failed. Please review and fix issues." -ForegroundColor Red
    }
    
    return $overallSuccess
}

function Test-SignalRHub {
    Write-TestHeader "SignalR Hub Testing"
    
    # Test SignalR hub accessibility
    $hubResult = Test-ApiEndpoint -Endpoint "/natureos-hub/negotiate" -Method "POST" -Description "SignalR hub negotiate"
    
    return @{
        SignalRHub = $hubResult.Success
    }
}

# Main execution
Write-Host "🌟 NatureOS Integration Testing Suite" -ForegroundColor Magenta
Write-Host "Testing API at: $ApiBaseUrl" -ForegroundColor Cyan
Write-Host "Started at: $(Get-Date)" -ForegroundColor Gray

# Run all test suites
$allResults = @{}

try {
    $allResults["CoreApi"] = Test-CoreApiEndpoints
    $allResults["RealTime"] = Test-RealTimeEndpoints
    $allResults["Myca"] = Test-MycaIntegration
    $allResults["DeviceTelemetry"] = Test-DeviceTelemetry
    $allResults["ExternalData"] = Test-ExternalDataIntegration
    $allResults["Website"] = Test-WebsiteIntegration
    $allResults["SignalR"] = Test-SignalRHub
    $allResults["Performance"] = Test-PerformanceMetrics
}
catch {
    Write-Host "❌ Critical error during testing: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Generate final report
$success = Generate-IntegrationReport -TestResults $allResults

# Exit with appropriate code
exit $(if ($success) { 0 } else { 1 }) 