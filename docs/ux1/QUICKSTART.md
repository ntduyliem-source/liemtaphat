# Đợt B — Dùng thử local

Mở [Locus local](http://127.0.0.1:4187/). Nếu thấy thông báo bản mới, dùng **Lưu nháp và cập nhật**; nháp của tab được giữ. Server cũ có thể đã dừng sau khi khởi động lại máy:

```powershell
./tools/ux1/local.ps1 start
```

1. Mở **Thử nhanh → Đoạn Toán / Lý / Hóa**, hoặc dán đoạn của bạn vào Nội dung.
2. Ô Kết quả giữ cả đoạn và dựng các công thức nhận diện được. `lc[...]` dùng nhận diện chung; `toan-[...]`, `ly-[...]`, `hoa-[...]` chỉ định môn, tùy chỉnh trong Tùy chọn.
3. Click công thức để chọn trọn, bôi một đoạn hoặc dùng Chọn tất cả. PNG/SVG chỉ dành cho một công thức đang chọn; Copy đoạn xuất text với công thức LaTeX.
4. Bấm fx → **Giữ vùng này là text**. Thêm câu ở đầu hoặc cuối nguồn: vùng giữ text vẫn giữ lựa chọn khi định vị được chắc chắn. Chọn lại trong fx hoặc Undo để quay lại.
5. Bôi dở công thức sẽ báo rõ; **Chọn trọn công thức** mở rộng selection có chủ đích rồi mới cho copy. Vùng đang là text vẫn copy được từng chữ.
6. **Lưu** tạo `.locus`, **Mở** đọc snapshot đã lưu. Nháp trên cùng thiết bị giữ các quyết định khi tải lại trang. Tải → Nguồn .txt và Tùy chọn → Copy nguồn gốc lấy nguyên nguồn.

Desktop dùng cùng editor và model; thư mục bản publish nằm ở `desktop` trong [current-build](../../artifacts/ux1/current-build.json). Đợt này build host Windows nhưng chưa kiểm lại bộ gõ OS/tray; không coi là nghiệm thu SC1-08.

Chưa có nút đũa thần theo vùng, batch/auto, tray/bung hoặc xuất nguyên đoạn thành Word native. Với công thức Hóa đơn, mục hỗ trợ SC1 cũ vẫn có; nhận hỗ trợ ở đó thay nguồn theo hành vi cũ và có khôi phục. Luồng mới giữ nguồn sẽ được nối ở đợt C.
