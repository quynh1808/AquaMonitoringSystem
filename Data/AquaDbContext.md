# `AquaDbContext`

## Mục đích và vai trò

`AquaDbContext` là bản đồ quan hệ duy nhất giữa model C# và PostgreSQL. File này định nghĩa table,
primary key, foreign key, unique/partial index, BRIN index, kiểu `jsonb` và precision. Repository và
service sử dụng context; component Blazor không truy cập database trực tiếp.

## Vì sao cần file này

AMS có nhiều quan hệ ao → sensor/device/cycle và cycle → growth/mortality/harvest. Chỉ kiểm tra các
quan hệ bằng code có thể tạo bản ghi mồ côi khi một luồng lỗi hoặc service mới quên validation.
Constraint trong context biến các invariant cốt lõi thành quy tắc database.

## Cách triển khai

- Một `DbSet` cho mỗi bảng bền vững.
- `LatestSensorState` thay document sáu metric cố định: một hàng/sensor.
- `SensorReading.Metadata` là shape giao thức; các forwarding property `PondId`, `SensorId`,
  `Metric`... được lưu thành cột để index và FK.
- `EventId` có unique partial index khi khác rỗng để backfill/retry idempotent.
- `Timestamp` có BRIN; các query chính có B-tree theo sensor và ao/metric.
- `AppUser.Roles`, `AllowedPondIds`, MQTT/Modbus settings, capabilities và start history dùng JSONB
  vì là cấu trúc nhỏ/linh hoạt; khóa truy vấn chính không nằm trong JSONB.
- Delete behavior là `Restrict`; asset đã vận hành được archive thay vì hard-delete.
- `DbContextUpsertExtensions` cập nhật detached entity theo ID cho repository.

`IDbContextFactory<AquaDbContext>` tạo context ngắn hạn cho từng operation. Không giữ một DbContext
singleton vì DbContext không thread-safe và protocol worker chạy đồng thời.

## Quyết định về ID

SensorId, DeviceId, NodeId và PondId giữ dạng string dễ đọc vì chúng là identity giao thức do admin
commission. Bản ghi nội bộ tự sinh vẫn dùng chuỗi GUID không dấu gạch. Đây không phải database
integer identity nên firmware/log có thể đối chiếu cùng một ID.

## Thuật ngữ

- **Primary key**: khóa duy nhất nhận dạng row.
- **Foreign key**: buộc giá trị tham chiếu tồn tại ở bảng cha.
- **Partial index**: index chỉ chứa row thỏa điều kiện, ví dụ EventId khác rỗng.
- **BRIN**: index tóm tắt block dữ liệu, phù hợp timestamp tăng dần ở bảng lớn.
- **JSONB**: JSON nhị phân có thể validate/index, dùng có giới hạn cho cấu hình linh hoạt.
- **Detached entity**: object không được context hiện tại theo dõi.

## Quan hệ và kiểm tra

- `AquaDbContextFactory.cs` tạo options runtime/design-time.
- `Data/Migrations/` được sinh từ model này.
- `FarmRepository` và `SystemConfigurationRepository` tạo context qua factory.
- `PostgreSqlModelTests` kiểm tra latest-state, flattened metadata và core FK.

Sau mọi thay đổi model: tạo migration mới, review diff/SQL, build, test và chạy migration trên
database tạm. Không sửa migration đã chạy production.
