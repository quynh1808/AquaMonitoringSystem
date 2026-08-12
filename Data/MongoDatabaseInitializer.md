# MongoDatabaseInitializer

Tạo index, alarm rules và seed khi MongoDB trống. Seed P03 chỉ có `pond_01`, sáu sensor MQTT offline/no-data và ba thiết bị FX5U offline; không tạo reading, feed log, growth sample hay telemetry giả. Dữ liệu MongoDB hiện có không bị xóa tự động.
