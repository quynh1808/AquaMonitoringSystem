# ModbusCodec

Codec dùng chung cho nguồn Modbus TCP và Modbus RTU. Module tạo yêu cầu đọc Holding Register (function `03`), kiểm tra phản hồi và chuyển register thành giá trị số.

Mapping được khai báo ngoài code trong `Config/sensor-connections.json`:

- slave/unit ID;
- địa chỉ register và số lượng register;
- kiểu `UInt16`, `Int16`, `UInt32`, `Int32`, `Float32`, `Float64`;
- thứ tự byte cho dữ liệu 2/4/8 byte, ví dụ `AB`, `BADC`, `CDAB`, `DCBA`, `HGFEDCBA`;
- hệ số `scale` và `offset`.

Codec kiểm tra số byte tối thiểu theo kiểu dữ liệu và từ chối byte order không hợp lệ ngay khi ứng dụng khởi động. RTU CRC16 được xác minh trước khi chấp nhận dữ liệu. Khi có cảm biến thật, chỉ cần nhập register map từ manual vào JSON, không sửa component UI hoặc pipeline xử lý.
