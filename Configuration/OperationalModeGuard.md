# OperationalModeGuard

Lớp kiểm tra fail-fast khi tiến trình khởi động. Chế độ `Simulation` cho phép các generator mặc định; chế độ `Production` bắt buộc tắt toàn bộ simulation, có ít nhất một sensor source thật, tắt device simulation và bật webhook cảnh báo. Mục tiêu là không thể vô tình trình bày dữ liệu giả hoặc gửi lệnh giả như dữ liệu production.

