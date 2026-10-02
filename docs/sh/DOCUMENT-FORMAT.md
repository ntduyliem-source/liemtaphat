# Hợp đồng tệp `.locus` phiên bản 1

Codec nằm trong `Locus.Application/WorkspaceDocument.cs`; Web và Desktop dùng cùng assembly. MIME là `application/json`, UTF-8. Đây là format trao đổi cục bộ, chưa có cloud sync.

Envelope có `format: locus-document`, `version: 1`, `kind: formula | plot | geometry`, `id` UUID, `revision` Int64 không âm, `payload` chuỗi JSON và `sha256` của UTF-8 payload. Checksum phát hiện hỏng dữ liệu, không xác thực tác giả. Giới hạn 8.000.000 byte; thuộc tính trùng/không biết, kiểu/phiên bản mới và identity không hợp lệ đều bị từ chối. Không tự nâng version hoặc ghi đè dữ liệu cũ.

| Loại | Nội dung |
| --- | --- |
| Formula | Raw nguyên văn, source revision, mode/cặp bọc, snapshot của toàn bộ vùng/candidate/diagnostics, vùng và candidate đang chọn, cỡ chữ/PNG scale/nền |
| Plot | Mảng đường có ID/nguồn/màu/nét/visible; viewport riêng. E1 bổ sung evaluator, miền lấy mẫu và candidate theo schema tiếp theo |
| Geometry | Điểm có ID/toạ độ/nhãn; đoạn có ID/tham chiếu đầu-cuối/nét. E2 bổ sung constraint/scene, không nhét chúng vào MathDocument |

Formula khôi phục snapshot, không parse bằng grammar hiện tại. Source/candidate/config phải khớp; ID lựa chọn sai hoặc source bị thay mà snapshot cũ không đổi làm tệp không được áp dụng. Revision được xử lý bởi C#; worker trả JSON dưới dạng chuỗi, tránh mất số nguyên lớn qua JavaScript Number. Bài kiểm dùng `9007199254740993`, NFD và emoji trước vùng.

Raw quá giới hạn phân tích 4.096 vẫn được giữ/lưu tới 65.536 ký tự khi không có analysis. Đây là cơ chế giữ nguồn, không mở rộng giới hạn parser. FormulaView kiểm font 16–96 và scale 1–4; PNG giới hạn 24 triệu pixel.

Plot/Geometry là schema nền riêng biệt, chưa tuyên bố khả năng vẽ/constraint trong SH. Nhập hai loại này vào tab công thức giữ nguyên phiên và tệp nguồn. Với phiên bản chưa hỗ trợ hoặc tệp hỏng, `OpenDocumentResult` giữ `OriginalJson`; UI giữ bytes đã nhận để tải lại nguyên tệp.

ID/revision của tài liệu độc lập với revision nguồn và epoch phiên. Lưu tăng revision tài liệu; chỉnh nguồn tăng revision nguồn; Undo có thể trả lại snapshot cũ nhưng epoch mới khiến lệnh xuất cũ hết hiệu lực. Mở tệp bắt đầu lịch sử mới của tài liệu đó. Đóng/mở lại tab chỉ thay view, không tạo tài liệu mới.
