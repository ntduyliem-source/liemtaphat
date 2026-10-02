# Dùng thử G tại máy

Build alpha hiện tại: `20260930-042902-862`. Hai ZIP đã kiểm hash và startup nằm trong [receipt gói](../../artifacts/phase-g/packages.json): một Web static local và một Desktop Windows x64 portable self-contained. Với ZIP, đọc `README.txt` sau khi giải nén; Web chạy `Start-Locus-Web.cmd`, Desktop chạy `Locus.Desktop.Shared.exe`.

Từ thư mục repository, mở Web bằng `./tools/phase-g/local.ps1`. Địa chỉ mặc định: http://127.0.0.1:4193/. Script chỉ mở đúng build ghi trong `artifacts/phase-g/current-build.json`; gặp server khác thì giữ nguyên server đó và báo lỗi. Có thể chọn cổng khác bằng `-Port 4194`. Dừng đúng server có receipt bằng `-Action stop`.

Bản alpha hiện tại: `20260930-042902-862` (tên theo UTC). Bản sửa đang mở để review tại http://127.0.0.1:4195/. Nếu browser đang dùng cache bản trước, về tab Công thức và chọn **Lưu nháp và cập nhật** khi thông báo xuất hiện; nháp các tab được giữ. Không xóa dữ liệu trình duyệt để cập nhật. [Kết quả kiểm tại máy và giới hạn còn lại](../host-review/20260930.md).

Mở Desktop trong repository bằng `./tools/phase-g/desktop.ps1`; profile riêng ở `artifacts/phase-g/desktop-profile`. Gói portable đã kèm .NET và chỉ còn phụ thuộc WebView2 Runtime. Build lại bằng `./tools/phase-g/build.ps1`, rồi đóng gói bằng `./tools/phase-g/package.ps1`; dừng server cũ trước khi đổi con trỏ build hoặc dùng cổng khác.

## Đồ thị

1. Vào **Đồ thị → Vẽ mới**, gõ `y = a*x mũ 2 + b*x + c`.
2. Nhập a=1, b=0, c=1 ở bảng dưới. Thêm `a*x` để thử tham số dùng chung. Có thể mở **Gán nhóm và bước nhảy**, gõ `a=1; b=0; c=1`, bấm **Áp dụng nhóm**.
3. Kéo thanh trượt a, Undo; chỉnh màu/nét/miền hoặc trục/lưới. Tìm tên, ghim, lọc theo hàm khi có nhiều tham số. Tham số không có giá trị sẽ không được ngầm gán 0.
4. **Copy SVG/PNG**, **Tải SVG/PNG**, hoặc chuột phải canvas. **Lưu .locus** để tiếp tục sửa; ảnh xuất chỉ chứa bản vẽ. Khi nguồn hiện tại chưa dựng xong hoặc còn lỗi, xuất tạm khóa để tránh lấy hình cũ.
5. Với `x+1/2`, bấm **Cách hiểu** nếu muốn xem phương án phòng thủ `(x+1)/2`. Với `1/2x` hoặc nguồn thiếu ngoặc, Locus chờ bạn chọn một cách hiểu/đề nghị trước khi vẽ. Việc chọn không đổi chuỗi đã gõ và có Undo.

## Hình học

1. Vào **Hình học → 2D**. Bấm công cụ ngay trên thanh hoặc từ canvas trống, rồi đặt điểm trên canvas. Menu **Đường**, **Đa giác**, **Góc / dựng** có công cụ tương ứng.
2. Chọn **Tam giác** và đặt ba đỉnh. Chọn **Hình chiếu / đường cao**, bấm điểm cần chiếu rồi hai đầu cạnh chuẩn. Kéo đỉnh C, đường cao cập nhật theo cạnh.
3. Ở **Chọn/kéo**, kéo trực tiếp điểm/cạnh/nhãn; cạnh tự do dịch cả hai đầu. Nấc hiện khi tới điểm/trung điểm; giữ Alt bỏ bắt. Chọn đối tượng để đổi nét/màu/độ dày hoặc góc.
4. **3D → Hình hộp/Hình chóp**, chỉnh kích thước rồi bấm canvas. Kéo nền để xoay; Shift+kéo để pan; chọn XY/XZ/YZ trước khi kéo đỉnh. Các góc nhìn Trước/Trên/Bên giúp định hướng. Mặt bị lệch phẳng có cảnh báo; Undo hoặc chỉnh lại trước khi xuất.
5. Mỗi lần kéo hoặc dựng là một Undo. Esc hủy nét đang vẽ. Hoàn tất hoặc hủy nét trước khi đổi tab/lưu. Canvas có bàn phím: mũi tên di chuyển vị trí đặt, Shift+mũi tên bước lớn, Space đặt, Enter khép đa giác.

Lưu 2D/3D thành từng tệp `.locus`; nháp hai không gian lưu riêng với nháp đồ thị/công thức. **Mở** tệp đúng tab tương ứng. Tệp chưa hỗ trợ được giữ để tải lại. Nháp trình duyệt gắn với tab, origin và thiết bị; nên lưu `.locus` để chuyển host hoặc giữ bản quan trọng.

[Phạm vi và bằng chứng nghiệm thu](REPORT.md). G là alpha cho phạm vi đã công bố, chưa phải bản phát hành đầy đủ của mọi công cụ trong wireframe.
