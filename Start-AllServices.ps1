$services = @(
    "EvnHanoi.ApiGateway",
    "EvnHanoi.IdentityService",
    "EvnHanoi.EquipmentService",
    "EvnHanoi.DigitizationService",
    "EvnHanoi.SearchService",
    "EvnHanoi.NotificationService",
    "EvnHanoi.SyncService",
    "EvnHanoi.WorkflowService",
    "EvnHanoi.ReportService"
)

Write-Host "Khởi động các Microservices..." -ForegroundColor Green

foreach ($service in $services) {
    $servicePath = Join-Path -Path $PWD -ChildPath "Microservices\$service"
    if (-not (Test-Path $servicePath)) {
        $servicePath = Join-Path -Path $PWD -ChildPath "ApiGateway\$service"
    }
    if (Test-Path $servicePath) {
        Write-Host "Đang khởi động $service..." -ForegroundColor Cyan
        Start-Process -FilePath "dotnet" -ArgumentList "run" -WorkingDirectory $servicePath -WindowStyle Normal
    } else {
        Write-Host "Không tìm thấy đường dẫn: $servicePath" -ForegroundColor Red
    }
}

Write-Host "Hoàn tất khởi động Backend!" -ForegroundColor Green
