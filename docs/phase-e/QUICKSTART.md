# Dùng thử đợt E trên máy này

Bản local, không đưa lên Sites. Xem [báo cáo nghiệm thu](REPORT.md) trước khi dùng bản Word mới; các bài D về tray, clipboard, file qua hai host và bộ gõ còn được theo dõi riêng.

## Word

1. Chọn riêng một công thức trong thân tài liệu, ví dụ `hoa-[3H2+O2=H2O]`, rồi Ribbon **Locus → Chuyển vùng chọn**.
2. Giữ nguyên hệ số vẫn chuyển được. Bấm **Cân bằng** để xem hệ số mới, hoặc **Hủy cân bằng** để lấy lại đúng hệ số trước đó.
3. Bấm **Chuyển thành công thức** để ghi native. Đặt con trỏ trong công thức → **Mở công thức Locus** để cân bằng/hủy/chọn cách đọc khác, rồi **Cập nhật công thức**. **Khôi phục nguồn** trả nguyên chuỗi đã gõ, gồm cặp bọc.
4. Với `hoa-[3H2+O2=]`, chọn điều kiện tác động phù hợp bài toán, bấm **Xem sản phẩm**, rồi **Nhận sản phẩm và cân bằng**. Sau khi hủy cân bằng, kết quả là `3H2+O2 → H2O`; phần sản phẩm đã nhận vẫn còn. **Hủy bổ sung sản phẩm** đưa preview về bản nháp; bản nháp chưa thể chèn thành một phương trình đầy đủ.
5. Ribbon **Cặp bọc** có bốn cặp và ba checkbox nhận diện. Mặc định chung `lc[...]`, riêng `toan-[...]`, `ly-[...]`, `hoa-[...]`. Cặp riêng chỉ định môn cho vùng đó, không tự bật checkbox toàn cục.

Mọi thao tác ghi Word vẫn cần xác nhận. Dữ liệu mơ hồ hoặc sửa lỗi phải được chọn rõ. Không có auto-Space hay ghost trực tiếp trong thân Word ở đợt E. Hộp preview có thể hủy bằng Esc/Đóng; Enter không tự chấp nhận đề nghị sửa.

## Web và Desktop

Build bằng `tools/phase-e/build.ps1`; chạy Web bằng `tools/phase-e/local.ps1 start`, dừng bằng `tools/phase-e/local.ps1 stop`. URL local: http://127.0.0.1:4191/. Vị trí binary chính xác được ghi trong `artifacts/phase-e/current-build.json`.

Web/Desktop giữ editor của D, `.locus` v7 và các lệnh cân bằng/đoạn. DOCX chứa native OMML, HTML chứa MathML; lưu `.locus` khi cần giữ nguồn và trạng thái hỗ trợ để mở lại Locus.

Gói Desktop hiện phụ thuộc .NET 10 Desktop Runtime và WebView2 Runtime trên Windows x64; bản này không kèm toàn bộ runtime. Gói Web có launcher Node.js cho local; có thể phục vụ thư mục `wwwroot` bằng static host tương thích. Word connector hiện nghiệm thu với Word x86 trên Windows và .NET Framework 4.8.

Gói Word dùng `register.ps1 -Action Install`/`Uninstall`, đóng Word trước khi thay đăng ký. Đăng ký kiểm ownership của thư mục; muốn chuyển thư mục phải gỡ đúng bản cũ rồi cài bản mới. Metadata v2 cần Word connector E để quản lý; khi không có connector vẫn xem/sửa equation native, nhưng sửa native làm snapshot cũ mất hiệu lực.

## Gói bàn giao hiện tại

Build `20260916-052303-165`, review local. Giải nén ZIP trước khi chạy:

- [Desktop Windows x64](../../artifacts/releases/Locus-E-20260916-052303-165-review-local-Desktop-win-x64.zip): mở `Locus.Desktop.Shared.exe` trong thư mục vừa giải nén.
- [Web static](../../artifacts/releases/Locus-E-20260916-052303-165-review-local-Web-static.zip): có Node.js trên PATH, mở `Start-Locus-Web.cmd`; dừng bằng `Stop-Locus-Web.cmd`. Nếu cổng 4191 đang chạy bản ở thư mục khác, launcher từ chối chiếm cổng; dùng bản đang mở hoặc chọn cổng khác qua `node local/local.mjs start --port 4193` trong thư mục gói.
- [Word x86](../../artifacts/releases/Locus-E-20260916-052303-165-review-local-Word-x86.zip): đóng Word; tại thư mục gói chạy `./register.ps1 -Action Install`. Trên máy phát triển này đã đăng ký đúng bản E cuối, không cần cài lại.

[Biên bản bàn giao](../../artifacts/phase-e/delivery.json) ghi hash và phạm vi đã kiểm. UI file picker, clipboard/download, tray và bộ gõ còn theo các bài D/SC1-08; không dùng gói review để suy ra các bài đó đã đạt.
