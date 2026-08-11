# Mô hình xác thực và vụ nuôi

`AppUser` lưu hash/salt, role, phạm vi ao và trạng thái khóa. `ProductionCycle` tách chu kỳ khỏi ao để giữ được lịch sử nhiều vụ. `GrowthSample`, `MortalityRecord`, `HarvestRecord` là các sự kiện nghiệp vụ dùng để tính Biomass, tỷ lệ sống và FCR.
