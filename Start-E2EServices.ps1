$servicePorts = @{
    "EvnHanoi.ApiGateway" = "http://localhost:5000"
    "EvnHanoi.IdentityService" = "https://localhost:5001"
    "EvnHanoi.EquipmentService" = "https://localhost:5002"
    "EvnHanoi.DigitizationService" = "https://localhost:5003"
    "EvnHanoi.NotificationService" = "https://localhost:5004"
    "EvnHanoi.SyncService" = "https://localhost:5005"
    "EvnHanoi.WorkflowService" = "https://localhost:5007"
    "EvnHanoi.ReportService" = "https://localhost:5006"
}

Write-Host "Khởi động các Microservices headlessly..." -ForegroundColor Green

$scratchDir = "C:\Users\tanha\.gemini\antigravity\brain\bef1064e-a5e6-4ace-aee2-08bbb227a4b1\scratch"
if (-not (Test-Path $scratchDir)) {
    New-Item -ItemType Directory -Force -Path $scratchDir | Out-Null
}

foreach ($service in $servicePorts.Keys) {
    $port = $servicePorts[$service]
    $servicePath = Join-Path -Path $PWD -ChildPath "Microservices\$service"
    if (-not (Test-Path $servicePath)) {
        $servicePath = Join-Path -Path $PWD -ChildPath "ApiGateway\$service"
    }
    if (Test-Path $servicePath) {
        Write-Host "Đang khởi động $service trên $port..." -ForegroundColor Cyan
        $outLog = Join-Path -Path $scratchDir -ChildPath "$service.out.log"
        $errLog = Join-Path -Path $scratchDir -ChildPath "$service.err.log"
        Start-Process -FilePath "dotnet" -ArgumentList "run --urls=`"$port`"" -WorkingDirectory $servicePath -WindowStyle Hidden -RedirectStandardOutput $outLog -RedirectStandardError $errLog
    } else {
        Write-Host "Không tìm thấy đường dẫn: $servicePath" -ForegroundColor Red
    }
}

Write-Host "Hoàn tất khởi động Backend!" -ForegroundColor Green
