**Locus Web — brief chức năng cho thiết kế giao diện**

Locus là công cụ nhập công thức Toán, Lý, Hóa bằng cách gõ tự nhiên, đồng thời vẽ đồ thị và hình học phục vụ học tập, giảng dạy và soạn tài liệu. Người dùng nhập hoặc vẽ, xem kết quả, điều chỉnh rồi sao chép/tải để sử dụng ở nơi khác.

- **Khung giao diện:** ba tab Công thức, Đồ thị, Hình học; trong Hình học có 2D/3D. Công thức ưu tiên hai vùng Nguồn và Kết quả; editor vẽ ưu tiên canvas, công cụ và bảng thuộc tính. Màn hình rộng đặt cạnh nhau, màn hình hẹp xếp dọc. Undo/Redo, Mở/Lưu và xuất kết quả phải dễ tìm.

- **Nhập và nhận diện:** gõ một công thức hoặc dán cả đoạn văn. Locus dựng những vùng nhận ra là công thức, giữ nguyên câu chữ và xuống dòng còn lại. Toán hỗ trợ phân số, căn, mũ/chỉ số; Lý có đại lượng và đơn vị; Hóa có công thức chất và phương trình. Ba checkbox Toán/Lý/Hóa bật độc lập. Có chế độ đọc tự nhiên, nhận diện trong câu hoặc chỉ đọc vùng có cặp bọc. Người dùng tùy chỉnh dấu mở/đóng: chung `lc[…]`, riêng `toan-[…]`, `ly-[…]`, `hoa-[…]`.

- **Kiểm soát kết quả:** chọn một công thức, bôi chọn một đoạn hoặc chọn tất cả. Với một công thức, nút Chi tiết công thức bên ngoài ô kết quả mở nguồn gốc, tối đa ba cách hiểu, sửa nguồn, giữ text hoặc dựng lại. Cách đọc trực tiếp đứng đầu; phương án sửa được ghi rõ và công thức mơ hồ chờ người dùng chọn. Ô kết quả chỉ chứa nội dung; nút fx thuộc luồng Word.

- **Gợi ý sản phẩm Hóa:** nhập vế trái kết thúc bằng `=`; khi kho phản ứng có kết quả phù hợp, người dùng chọn điều kiện và xem toàn phương trình. Gợi ý mờ chưa trở thành nội dung cho tới khi nhận bằng Enter/nút nhận; Esc bỏ gợi ý, Space chỉ nhận khi bật tùy chọn. Có thể xem điều kiện/nguồn tham khảo và bỏ sản phẩm đã nhận để trở về bản trước hỗ trợ.

- **Cân bằng và hủy cân bằng:** nút đũa thần hoạt động với phương trình Hóa được chọn. Cân bằng rồi hủy sẽ trả đúng hệ số trước đó, kể cả hệ số sai. Chọn nhiều phương trình: nút chính cân bằng lần lượt từ trên xuống, hết lượt mới chuyển sang hủy. Menu bên cạnh có Cân bằng tất cả/Hủy cân bằng tất cả trong vùng chọn; bấm lại mục vừa dùng phục hồi trạng thái trước lệnh. Checkbox Tự cân bằng mặc định tắt, áp dụng cho lần nhập/dán tiếp theo; vùng đã hủy thủ công được giữ khỏi tự cân bằng.

- **Đồ thị:** bắt đầu bằng canvas trống và nút Vẽ mới. Nhập `y=f(x)` bằng cú pháp tự nhiên, xem công thức rồi dựng đường khi hợp lệ; thêm/xóa/ẩn nhiều hàm trên cùng canvas. Tham số dùng chung, mỗi hàng gồm tên — giá trị — min — slider — max; bảng có cuộn, tìm, lọc, ghim, gán nhóm và bước nhảy. Chỉnh màu, nét, độ dày, miền từng hàm, trục/lưới/nhãn và khung nhìn; kéo nền, zoom, về gốc. Phạm vi hiện tại là hàm thực tường minh theo x.

- **Hình học 2D:** chọn công cụ rồi đặt trên canvas: điểm, đoạn/đường/tia/vector, đường tròn, tam giác/đa giác, chữ; dựng trung điểm, hình chiếu/đường cao, song song, vuông góc và góc cho trước. Kéo trực tiếp điểm/cạnh/nhãn; gần điểm hoặc trung điểm có tín hiệu nấc, thả để dính. Bảng đối tượng/thuộc tính có tên, ẩn/hiện, màu, nét liền/đứt/chấm, độ dày và ký hiệu bằng nhau. Góc chỉnh góc nhỏ/lớn, độ/radian, cung, nhãn và số đo. Quan hệ cập nhật khi kéo; có tách điểm thành tự do và xóa kèm phần phụ thuộc.

- **Hình học 3D:** tạo hình hộp, chóp, lăng trụ hoặc mặt qua các đỉnh; chỉnh kích thước và kéo đỉnh theo XY/XZ/YZ. Kéo nền để xoay, pan/zoom; có góc nhìn Trước/Trên/Bên. Chỉnh cạnh, nét khuất tự động/thủ công, màu/độ trong của mặt và góc không gian. Nếu hình bị suy biến hoặc mặt lệch phẳng, chỉ rõ vấn đề để người dùng sửa/Undo.

- **Sao chép và tải:** công thức chọn riêng có Copy/Tải PNG, SVG; tùy chỉnh cỡ chữ, độ phân giải PNG và nền. Cả đoạn hoặc vùng chọn có copy văn bản với LaTeX, tải DOCX với công thức Word chỉnh sửa được, HTML có công thức và nguồn TXT. Tùy chọn nâng cao có LaTeX/MathML/OMML. Đồ thị/2D/3D xuất PNG/SVG từ đúng canvas; chuột phải có lệnh copy. Luôn thể hiện rõ đang xuất một công thức, vùng chọn hay toàn đoạn.

- **Lưu và trạng thái:** lưu/mở `.locus` để tiếp tục chỉnh sửa; tự lưu nháp, nháp gần đây và nháp riêng từng không gian. Phân tích diễn ra trên thiết bị; có trạng thái offline và lưu nháp trước cập nhật. Thiết kế đủ các trạng thái trống, đang xử lý, đang ghép dấu, mơ hồ, lỗi và copy thất bại có tải thay thế; giải thích nút bị khóa, giữ nguồn khi lỗi và cho dừng xử lý/Undo. Dùng bàn phím được, icon có tên rõ và thông báo không che nội dung.

**Phần dành cho bản online tiếp theo, chưa chạy trong bản local:** tài khoản, đăng nhập, số lượt còn lại và nâng gói. Hạn mức ngày theo thứ tự công thức/đồ thị/hình 2D/hình 3D: Guest **5/2/2/2**, Free **21/10/10/10**, Premium **150/100/100/100**. Tính lượt khi xuất thành phẩm mới lần đầu; sửa/preview/Undo và xuất lại cùng thành phẩm không trừ thêm. Hết lượt vẫn giữ nháp, sửa và lưu được; đoạn vượt số lượt cho chọn các vùng sẽ chuyển. Guest thấy lời mời tạo tài khoản, Free thấy lựa chọn nâng gói.

Tổng thể cần gọn, dễ hiểu: hành động thường dùng nằm gần nội dung, tùy chọn nâng cao thu gọn và thuộc tính xuất hiện theo đối tượng đang chọn. Người dùng giữ quyền quyết định cách hiểu, cân bằng và nội dung xuất. Web có các không gian làm việc này; kết nối trực tiếp với Word thuộc ứng dụng và add-in riêng.
