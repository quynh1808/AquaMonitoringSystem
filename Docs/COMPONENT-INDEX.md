# Chỉ mục tài liệu component và module

## Khởi động và cấu hình

- `Program.md`
- `Configuration/AquacultureOptions.md`
- `Configuration/ConfigurationValidation.md`
- `Configuration/DashboardSimulationOptions.md`
- `Configuration/DeviceControlOptions.md`
- `Config/README.md`
- `Config/dashboard-simulation.md`

## MongoDB và runtime

- `Models/AquacultureModels.md`
- `Data/MongoContext.md`
- `Data/FarmRepository.md`
- `Data/MongoDatabaseInitializer.md`
- `Infrastructure/MongoWriteQueue.md`
- `Infrastructure/MongoDbHealthCheck.md`
- `Runtime/FarmRuntimeState.md`
- `Runtime/MongoBootstrapHostedService.md`

## Sensor pipeline

- `Sensors/SensorEnvelope.md`
- `Sensors/SensorMessageBus.md`
- `Sensors/SensorSourceCoordinator.md`
- `Sensors/SimulatedSensorSource.md`
- `Sensors/MqttSensorSource.md`
- `Sensors/ModbusTcpSensorSource.md`
- `Sensors/ModbusRtuSensorSource.md`
- `Sensors/ModbusCodec.md`
- `Sensors/SensorIngestionWorker.md`
- `Sensors/SensorOfflineMonitor.md`
- `Sensors/AssetMaintenanceMonitor.md`
- `Sensors/DataSourceHealthService.md`

## Nghiệp vụ

- `Services/FarmStateService.md`
- `Services/AlertEngineService.md`
- `Services/AutomationEngineService.md`
- `Services/DashboardAnalyticsService.md`
- `Services/FarmAccessService.md`
- `Services/DeviceCommandService.md`
- `Services/ProductionCycleService.md`
- `Devices/README.md`
- `Models/DashboardModels.md`

## UI

Mỗi file `.razor` trong `Components/` có file `.md` cùng tên. Các trang quan trọng:

- `Components/Pages/Dashboard.md`
- `Components/Pages/Analytics.md`
- `Components/Layout/MainLayout.md`
- `Components/Shared/DashboardKpiCard.md`
- `Components/Shared/AnalyticsBarChart.md`
- `Components/Shared/WaterQualityHeatmap.md`
- `Components/Shared/PondLiveCard.md`
- `Components/Pages/PondDetail.md`
- `Components/Pages/Telemetry.md`
- `Components/Pages/Alerts.md`
- `Components/Pages/ChangePassword.md`
- `Components/Shared/SensorMetricCard.md`
- `Components/Shared/CalibrationModal.md`

## Yêu cầu và thiết kế Dashboard

- `Docs/DASHBOARD-REQUIREMENTS.md`
- `Docs/DASHBOARD-IMPLEMENTATION.md`
- `Docs/DASHBOARD-CHANGELOG.md`

## Chuyển sang phần cứng

- `Docs/REPLACE-SIMULATION-WITH-HARDWARE.md`
- `Docs/HARDWARE-INTEGRATION.md`
- `Docs/DEPLOYMENT-GHCR-UBUNTU.md`
- `Docs/SECURITY-HARDENING.md`
- `Docs/GIT-PUBLISH.md`
- `Docs/BUILD-AND-VERIFY.md`
- `Docs/VALIDATION-REPORT.md`
- `Examples/README.md`

## Vận hành MongoDB

- `scripts/MongoBackup.md`
- `scripts/README.md`
