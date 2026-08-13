# MongoDatabaseInitializer

Tạo index, alarm rules và seed khi MongoDB trống. Seed chỉ tạo `pond_01`, không tạo sensor, thiết bị,
reading, feed log, growth sample hoặc telemetry giả. Khi nâng cấp, definition của sáu sensor và ba
thiết bị demo cũ được archive để không tạo card; reading, command và audit history vẫn được giữ.
