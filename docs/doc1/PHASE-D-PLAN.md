# Đợt D

Đang thực hiện ngày 2026-09-15 bởi Codex, một luồng. Đầu vào là build C `20260915-161929-816`; giữ nguyên build/receipt C. Thứ tự DOC1-03 → UX1-03 → bổ sung SC1-08. Ước lượng triển khai theo ba cụm dưới; phần nghiệm thu OS phụ thuộc công cụ/phiên máy, không dùng thời gian chờ để báo tính năng đã đạt.

1. **Xuất nguyên đoạn:** thêm DOCX OMML và HTML MathML theo selection. Giữ raw/câu chữ, vùng text, hệ số đang dùng; từ chối công thức chọn dở. Một snapshot cho preview và dữ liệu xuất; đổi nguồn/selection trong lúc chuẩn bị thì hủy. Copy đoạn LaTeX và ảnh riêng tiếp tục dùng như hiện tại. Kiểm cấu trúc ZIP/XML, Unicode/xuống dòng, native equation và bản render. DOCX là tệp xuất để chỉnh trong Word; `.locus` là tệp giữ đầy đủ lịch sử/quyết định.
2. **Desktop:** lưu tệp qua hộp chọn native; cửa sổ gọn/bung/ẩn cùng WebView và session, tray mở lại/Thoát rõ. Hoàn tất hàng đợi nhập trước ẩn/thoát; không ép kết thúc IME. Tự lưu nháp bật thì chờ lưu; nếu chưa lưu được, từ chối thoát và báo, còn ẩn vẫn giữ phiên trong bộ nhớ. Không đăng ký startup Windows hay tự mở Word.
3. **Nghiệm thu host:** mở tệp C có balance/ignore/product trong Web và Desktop; xuất, hủy/lưu/mở lại đúng. Kiểm tray/ẩn/bung/Undo, bàn phím thật Telex/VNI/focus/Enter/Space/Esc/Tab trong phạm vi SC1-08 còn thiếu. Nếu công cụ OS lỗi, ghi rõ bài chưa đạt theo kế hoạch và dừng vòng thử lỗi đó; tiếp tục phần độc lập.

Rủi ro chính: biến selection thành phạm vi khác, mất text/đổi hệ số khi xuất, package DOCX không đọc được, tác vụ trễ ghi sau ẩn, IME bị cắt, nháp chưa lưu khi thoát. Chỉ thêm test bảo vệ các rủi ro này; không chạy lại toàn kho phản ứng.

Tham chiếu kỹ thuật: [WordprocessingML](https://learn.microsoft.com/en-us/office/open-xml/word/structure-of-a-wordprocessingml-document), [WPF ShutdownMode](https://learn.microsoft.com/en-us/dotnet/api/system.windows.application.shutdownmode?view=windowsdesktop-10.0). Không lấy HTML/MathML clipboard làm bằng chứng Word native; đường native là DOCX OMML đã kiểm.

Đã có đầu ra triển khai. Xem [REPORT](REPORT.md) để phân biệt phần đã kiểm với bài host còn thiếu; kế hoạch này không thay cho nghiệm thu.
