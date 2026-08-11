# Quản lý vụ nuôi

Chu kỳ nuôi được lưu độc lập với `Pond`, hỗ trợ chuẩn bị, thả giống, sinh trưởng, thu hoạch, hoàn thành và hủy. Các bản ghi lấy mẫu, hao hụt và thu hoạch không ghi đè lịch sử. `ProductionCycleService` tính lại Biomass, tỷ lệ sống, tổng thức ăn, sản lượng đã thu và FCR. Worker mô phỏng chỉ tạo mẫu khi thiếu dữ liệu thực.
