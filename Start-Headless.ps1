# E:\\ecoit\\sohoax10\\sohoa.backend\\Start-Headless.ps1
$services = @(
    "EvnHanoi.ApiGateway",
    "EvnHanoi.IdentityService",
    "EvnHanoi.EquipmentService",
    "EvnHanoi.DigitizationService",
    "EvnHanoi.NotificationService",
    "EvnHanoi.SyncService",
    "EvnHanoi.WorkflowService",
    "EvnHanoi.ReportService"
)

Write-Host "Khởi động các Microservices chạy ngầm (Headless)..." -ForegroundColor Green

$logDir = "C:\Users\tanha\.gemini\antigravity\brain\5d189adb-710f-4faa-ae60-0bb80be31bb6\scratch\logs"
if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Force -Path $logDir | Out-Null
}

foreach ($service in $services) {
    $servicePath = Join-Path -Path $PWD -ChildPath "Microservices\$service"
    if (-not (Test-Path $servicePath)) {
        $servicePath = Join-Path -Path $PWD -ChildPath "ApiGateway\$service"
    }
    if (Test-Path $servicePath) {
        Write-Host "Đang khởi động $service ngầm..." -ForegroundColor Cyan
        $outLog = Join-Path -Path $logDir -ChildPath "$service.out.log"
        $errLog = Join-Path -Path $logDir -ChildPath "$service.err.log"
        Start-Process -FilePath "dotnet" -ArgumentList "run" -WorkingDirectory $servicePath -WindowStyle Hidden -RedirectStandardOutput $outLog -RedirectStandardError $errLog
    } else {
        Write-Host "Không tìm thấy đường dẫn: $servicePath" -ForegroundColor Red
    }
}

Write-Host "Hoàn tất yêu cầu khởi động chạy ngầm Backend!" -ForegroundColor Green
