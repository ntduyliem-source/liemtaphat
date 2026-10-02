# Dùng thử F — Quét tài liệu Word

Word connector 0.5.0, bản review local. Dùng Word x86 trên Windows với .NET Framework 4.8. Web/Desktop dùng alpha G; F bổ sung thao tác trên tài liệu Word có sẵn.

## Cách dùng

1. Trong Word, mở tab **Locus**. Chọn **Quét thân tài liệu**, hoặc bôi chọn một đoạn rồi **Quét vùng chọn**.
2. Bảng công thức hiện phạm vi, danh sách vùng, nguồn và hình xem trước. Quét không đổi nội dung, màu đánh dấu hay định dạng của tài liệu.
3. Chọn một hàng để xem. **Đi tới vùng / fx** bôi chọn vùng đó bằng selection của Word. Nếu có nhiều cách hiểu, chọn phương án rồi bấm **Dùng cách hiểu này**.
4. Bấm **Chuyển vùng này** hoặc **Chuyển N vùng đã nhận diện**. Số N chỉ gồm các vùng đủ điều kiện trong phạm vi đã quét. Công thức mơ hồ chưa chốt và vùng giữ text bị bỏ qua; không tự cân bằng Hóa hay tự nhận đề nghị sửa.
5. **Về text, giữ nhận diện** trả nguồn nguyên văn và cho phép chuyển lại. **Giữ text, bỏ qua** trả/giữ nguồn, loại vùng đó khỏi chuyển nhóm. **Cho phép chuyển lại** gỡ quyết định bỏ qua. Các quyết định được lưu cùng file `.docx`.
6. Mỗi lệnh ghi tương ứng một Undo. Sau khi sửa nội dung, đổi cài đặt, Undo hoặc đổi tài liệu, bấm **Quét lại phạm vi này** hoặc dùng lại lệnh quét trên Ribbon.

Ví dụ một đoạn: `Cho x mũ 2; vận tốc v=10 m/s; dung dịch H2SO4.` Với tất cả checkbox đang bật, `v=10 m/s` có thể có cả cách đọc Toán và Lý; cần chọn rõ. Cặp `ly-[v=10 m/s]` chỉ định Lý cho vùng đó. `lc[1/2x]` cũng cần chọn cách hiểu trước khi đưa vào nhóm.

**Thử dấu fx cạnh vùng** là tùy chọn thử nghiệm, tắt mặc định. Bản này hỗ trợ bảng điều hướng; đã kiểm click, zoom 75/100/150% ở một màn hình 96 DPI; chưa nghiệm thu ma trận DPI/nhiều màn hình. Không có highlight màu cố định được ghi vào văn bản. Phiên quét hết hạn sau 5 phút; có thể quét lại.

## Phạm vi

- Thân tài liệu ngoài bảng; bỏ qua field, hình, công thức native không do Locus quản lý và content control của ứng dụng khác.
- Tối đa 100.000 vị trí Word, 64 vùng mỗi lượt. Đoạn/gap dài hơn 4.096 UTF-16 được bỏ qua và báo trong bảng; tài liệu lớn có thể chia vùng nhưng vẫn phải nằm trong giới hạn tài liệu.
- Không ghi khi Read-only, Protected hoặc Track Changes. Không tự tắt các chế độ đó.
- Vùng được quản lý mà người dùng sửa text/native hoặc làm trùng ID sẽ được giữ nguyên, báo cần kiểm tra. Locus không lấy snapshot cũ ghi đè lên bản đã sửa.
- Luồng cân bằng/suy sản phẩm từng công thức của E vẫn nằm ở **Mở công thức Locus**. F không tự áp dụng chúng khi chuyển nhóm.

## Cài/gỡ và chạy từ source

Trên máy phát triển đã đăng ký bản F được ghi trong `artifacts/phase-f/delivery.json`. Chỉ cần mở Word → tab Locus.

Gói ZIP phải giải nén trước. Đóng Word, chạy `./register.ps1 -Action Install` trong thư mục gói. Khi chuyển từ thư mục E/F cũ, chạy `./register.ps1 -Action Uninstall` trong đúng thư mục cũ trước; trình đăng ký kiểm ownership và không chiếm đăng ký của bản khác. Gỡ F cũng dùng `Uninstall` tại thư mục đã cài.

Build bằng `tools/phase-f/build.ps1`. Hướng dẫn kiểm, hash và phần chưa đạt nằm trong [REPORT.md](REPORT.md). File demo trong workspace: `artifacts/phase-f/demo/Locus-F-demo.docx`.
