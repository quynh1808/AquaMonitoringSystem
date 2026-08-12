# ProductionCycleService

Service bảo vệ một vụ mở trên mỗi ao, transition state, liên kết pond/cycle và giới hạn tổng hao hụt + số con thu hoạch. Feeding chỉ được tính khi có cùng CycleId. FCR dùng tăng biomass ròng; bootstrap recalculates toàn bộ derived metrics trước khi nạp runtime.

Quản lý nhiều vụ nuôi trên cùng một ao, đảm bảo mỗi ao chỉ có một vụ chưa kết thúc. Dịch vụ ghi nhận lấy mẫu, hao hụt, thu hoạch và tự tính lại số lượng sống, tỷ lệ sống, Biomass, tổng thức ăn và FCR. Mọi thay đổi quan trọng được ghi vào `audit_logs`.
