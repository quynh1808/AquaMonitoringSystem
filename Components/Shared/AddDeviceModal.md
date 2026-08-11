# AddDeviceModal

Form dành cho Administrator/FarmManager để khai báo thiết bị cho một ao. Ngoài tên/loại/công suất, form lưu `ControlTransport`, `ExternalAddress` và `FailSafeState`, nhờ đó có thể chuyển từng thiết bị từ simulation sang adapter PLC/relay mà không đổi component. Service vẫn kiểm quyền và safety switch ở server.
