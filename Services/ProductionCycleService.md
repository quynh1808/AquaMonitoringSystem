# ProductionCycleService

Quản lý nhiều vụ nuôi trên cùng một ao, đảm bảo mỗi ao chỉ có một vụ chưa kết thúc. Dịch vụ ghi nhận lấy mẫu, hao hụt, thu hoạch và tự tính lại số lượng sống, tỷ lệ sống, Biomass, tổng thức ăn và FCR. Mọi thay đổi quan trọng được ghi vào `audit_logs`.
