# Aquaculture Monitoring Systems
Aquaculture Monitoring Systems (AMS) về cơ bản đây là một hệ thống mà tôi muốn xây dựng để giám sát việc nuôi trồng thủy hải sản trong môi trường ao hồ. 
Cơ bản hệ thống phải hoạt động tương tư như một hệ thống điều hành sản xuất MES (Manufacturing Execution System) nhưng áp dụng vào trong sản xuất nông nghiệp, thủy hải sản.
Hệ thống này sẽ giám sát việc nuôi từ việc lên kế hoạch nuôi, chuẩn bị giống, xử lý ao nuôi, đến khi thu hoặc và bắt đầu một chu kỳ nuôi mới.
Về các thành phần xây dựng web:
+ Tôi muốn dùng Blazor Web App làm nền Server chính vì nó dễ dàng đóng gói, có thể lập trình trên Window và publish để chạy dưới hệ điều hành Linux.
+ Tôi muốn dùng MongoDB để làm cơ sở dữ liệu chính và tạo thêm một folder lưu trữ dữ liệu backup trong ổ cứng của thiết bị làm máy chủ ( máy chạy linux), các dữ liệu backup này sẽ không bao giờ được xóa trừ khi được yêu cầu dưới quyền quản trị viên.
Về cách publish xuống chạy chủ:
+ Hiện tại tôi đang xây dựng web server trên Blazor,sau đó đóng gói kèm với Dockerfile và git push lên github. Github Action  sẽ build docker image và tôi sẽ cần git pull từ máy chủ linux. ( tôi thấy vẫn chưa tối ưu nên ở phần này tôi muốn bạn gợi ý cách đẩy appdata này từ window lên linux một cách thuận tiện nhất có thể, với yêu cầu chính là phải dễ dàng update cho lần thứ hai mà không làm gián đoạn việc giám sát hay gây mất dữ liệu hiện có).

## Về các chức năng cần chỉnh sửa
Ở trang Dashboard tổng:
+ Trang này phải thực hiện được chức năng giám sát tổng quát về các ao nuôi đang được quản lý, nên cần phải có các số liệu biến thiên liên tục về ao nuôi và số liệu mặc định ngay từ đầu.
Ví dụ như ở mục Dashboard giám sát toàn bộ ao nuôi:  cần có tổng số ao đang quản lý, Tổng sản lượng ước tính (Biomass),Số ngày nuôi trung bình(tính đến thời điểm hiển thị) số lượng thiết bị đang hoạt động và trạng thái, Phụ Tải Điện Năng, Cảnh Báo Vượt Ngưỡng,Nồng độ Oxy trung bình, TỔng lượng thức ăn, Cảnh báo thời tiết,...( bạn hãy suy nghĩ thêm một vài thông số khác cần thiết phải giám sát tổng thể)
Bên cạnh đó các mục này phải thiết kế để có thể nhấn được, khi nhấn sẽ chuyển đến menu bên dưới có tên là (Phân tích tổng thể) ở trang này sẽ phân tích chung, nhưng mang tính cụ thể hơn về các mục giám sát được nêu ở trên. Ví dụ như tổng số ao nuôi quản lý thì có bao nhiêu ao nuôi tôm, bao nhiêu ao nuôi cá,TỔng lượng thức ăn, tổng sản lượng chia ra theo đối tượng được nuôi, các cảm biến, thiết bị thì có thể chia theo ao nuôi,... Và lưu ý mỗi mục được giám sát chung sẽ là một trang khác nhau và một menu khác nhau ( ví dụ Phân tích tổng thể -> tổng số ao nuôi, menu được thiết kế theo kiểu mở rộng- thu gọn).

+ Bên dưới sẽ thêm một vài biểu đồ  Analytics phân tích giai đoạn nuôi của ao theo ngày nuôi, các thông số cần phân tích, và thể hiện bạn hãy tự đề xuất. (có thể có nhiều biểu đồ sếp theo thứ tự trên dưới ) ( https://srbthemes.kcubeinfotech.com/domiex/html-dark-2.4.0/dashboards-analytics.html hãy tham khảo web của đường link này). Có thể thêm một vài biểu đồ theo điều xuất của bạn sao cho mục đích chính là khi nhìn vào người xem có thể nắm được đại khái tình hình thực tế của các ao nuôi.

Tiếp theo là phần LIVE MONITORING(Bản đồ trạng thái ao nuôi)
ở phần này hãy thiết kế lại mỗi ao nuôi là một Grid View chứa đầy đủ các thông tin quan trọng của một ao nuôi như  ao nuôi gì, các giá trị cảm biến, ngày thả, ngày thu hoạch dự kiến, mật độ hiện tại, số ngày nuôi, tổng lượng thức ăn v.v chỉ cần hiển thị đại khác giá trị, giá trị nào biến thiên thì hãy thiết kế chữ to và nhìn rõ hơn, còn giá trị nào không đổi như ngày thả, ngày thu hoạch dự kiến.. thì chỉ cần thể hiện chữ nhỏ. ( https://srbthemes.kcubeinfotech.com/domiex/html-dark-2.4.0/apps-projects-grid.html#! hãy tham khảo về bố cục của trang web này)


Cuối cùng, về phần menu
+ Phần danh sách ao nuôi nênn thiết kế theo kiểu mở rộng, thu gọn
+ Trung tâm điều hành sẽ gồm Dashboard tổng và Phân tích tổng thể như tôi đã nói bên trên
+ Ở menu phân tích tổng thể cũng sẽ chứa các trang bên trong theo kiểu mở rộng-thu gọn.

