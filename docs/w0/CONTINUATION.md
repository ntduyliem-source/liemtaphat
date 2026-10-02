# W0 — Con trỏ sau khi chuyển equation

Ngày: 2026-09-12 theo giờ máy, artifact UTC ngày 2026-09-13. Phạm vi: chuyển thủ công trong sandbox Word x86, chưa phải chính sách tự chuyển khi Space.

## Lỗi và cách sửa

Sau khi chuyển `x mũ 2`, đặt selection ở `ContentControl.Range.End` khiến `TypeText(" cộng 1")` tiếp tục trong equation. Dời selection tới cuối OMath bằng `SetRange` hoặc `Collapse` cũng có thể giữ chế độ nhập toán: tọa độ con trỏ đúng chưa đủ để chứng minh chữ mới sẽ là văn bản.

[Thử tám biến thể](../../artifacts/w0/adapter-20260913T024454737Z/continuation-variants.json) phân biệt vị trí và chế độ nhập. Biến thể chọn ký tự kế tiếp rồi `Selection.MoveLeft(wdCharacter, 1)` giữ nguyên văn ` cộng 1` ở ngoài equation, không sửa native/metadata; Undo lần lượt bỏ chữ mới và hoàn tác chuyển công thức. Các biến thể khác không đạt đầy đủ điều kiện này trong lần thử.

Adapter lấy OMath bao chứa content control từ collection của tài liệu. OMath lấy qua `control.Range` có thể bị cắt ở ranh giới control; ở đầu đoạn, phạm vi tài liệu có thể bao cả dấu mở ẩn trước control. Vì vậy không dùng phép cộng một offset cố định để đoán điểm kết thúc.

Sau khi đóng custom Undo và xác minh native, adapter chọn một ký tự Word ngay sau OMath đầy đủ, rồi sang trái để lấy chế độ nhập của văn bản kế tiếp. Không thêm dấu cách, ký tự vô hình hoặc sửa văn bản ngoài vùng. Không tìm được ranh giới phù hợp thì rollback. Di chuyển selection không tạo thêm bước Undo.

## Kiểm chứng

Các kiểm tra hiện hành nằm trong [runner Word](../../tests/Locus.Word.Tests/Program.cs):

- `continuation/plain-text-api-matrix`: mũ NFC/NFD, phân số, phân số lồng và phép cộng; hậu tố rỗng, dấu cách, câu văn, dấu chấm, emoji/ký tự tổ hợp và tab. Kiểm nguyên hậu tố, cấu trúc native, association, Undo phần gõ và Undo chuyển công thức.
- `continuation/second-equation-api`: chọn đúng equation thứ hai, gõ tiếp không đổi equation thứ nhất, cả hai snapshot còn đọc được.
- Các bài Undo/rollback/restore và bảo toàn phần ngoài vùng vẫn phải đạt sau thay đổi vị trí con trỏ.

[Chỉ mục nghiệm thu](../../artifacts/w0/acceptance.json) ghi lần chạy cuối. Kết quả API không thay thế phím Telex/VNI, hành vi Backspace, Space, click ra ngoài hoặc người thử sản phẩm.

## Giới hạn quyết định

Sửa này phục vụ việc kết thúc một lần chuyển thủ công để viết tiếp câu văn. Nó chưa giải quyết ý định `x mũ 2` Space `cộng 1`: người dùng có thể muốn tiếp tục cùng công thức. Hai biến thể giữ phiên nguồn/native và cách kết thúc phiên vẫn cần thử; D-01, W0-06 và cổng G3 giữ mở.
