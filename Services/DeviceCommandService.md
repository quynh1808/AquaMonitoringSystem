# DeviceCommandService

Lưu command intent trước khi gọi PLC, chặn lệnh khi safety switch tắt, áp timeout và không tự retry lệnh có thể đã tác động tải. Trạng thái runtime chỉ đổi sau khi transport đọc đúng feedback và fault đang OFF; mọi kết quả được audit.
