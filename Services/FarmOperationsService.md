# FarmOperationsService

Ranh giới nghiệp vụ và authorization của trang vận hành P2. Service kiểm tra quyền theo role/ao, xác thực cycle thuộc pond, cập nhật tồn kho nguyên tử và ghi audit cho mọi mutation. Trang Razor chỉ gọi service này, không truy cập MongoDB trực tiếp.

