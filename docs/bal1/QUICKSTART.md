# Đợt C — Cân bằng trong kết quả

Mở [Locus local](http://127.0.0.1:4188/). Nếu máy vừa khởi động lại, chạy `./tools/bal1/local.ps1 start` trong thư mục dự án. Bản B và SC1 cũ giữ nguyên build riêng.

## Một phương trình

Nhập `hoa-[3H2+O2=H2O]`. Click phương trình trong kết quả rồi bấm **Cân bằng**. Chỉ hệ số trong kết quả đổi; ô nguồn còn nguyên. Nút chuyển thành **Hủy cân bằng**, trả lại đúng hệ số `3, 1, 1`. Fx có bản trước khi cân bằng.

Công thức Toán/Lý, một chất Hóa hoặc phương trình chưa đọc được có lý do nút mờ. Phương trình tự cân bằng sẵn vẫn copy được và không có nút hủy giả. Vô nghiệm/không duy nhất/vượt giới hạn được giữ nguyên; không thêm hay đổi chất để tìm nghiệm.

## Nhiều phương trình

Dán:

```text
Phản ứng thứ nhất: hoa-[3H2+O2=H2O].
Phản ứng thứ hai: hoa-[N2+H2=NH3].
Đã cân bằng: hoa-[C+O2=CO2].
Giữ nguyên nếu không có nghiệm: hoa-[H2=CO2].
```

Chọn tất cả trong kết quả. Nút chính đổi từng phương trình từ trên xuống, có nhãn bước và viền vùng tiếp theo. Hết lượt cân bằng mới sang lượt hủy. Phương trình tự cân bằng sẵn và vùng không đủ điều kiện được bỏ qua.

Mở mũi tên cạnh nút để dùng **Cân bằng tất cả trong vùng chọn** hoặc **Hủy cân bằng tất cả trong vùng chọn**. Mục vừa dùng đổi thành **Hoàn tác…**; bấm lại trả đúng trạng thái trộn trước lệnh. Đổi vùng chọn hoặc thực hiện lệnh khác làm mất hoàn tác nhanh; Undo thường vẫn có.

## Tự cân bằng

Checkbox **Tự cân bằng** mặc định tắt, độc lập với Nhận diện Hóa. Bật chỉ áp dụng cho lần nhập/dán tiếp theo, không đổi cả đoạn đang có. Một lần dán kèm auto là một Undo; hủy riêng cân bằng giữ nguyên đoạn đã dán.

Hủy thủ công đánh dấu bỏ qua auto của vùng ấy. Reload, render hoặc sửa câu bên cạnh không cân bằng lại. Sửa công thức thật sự tạo vùng mới có thể xét lại. Bấm cân bằng có chủ đích được phép thay quyết định bỏ qua. Tắt auto không hủy kết quả đã có.

## Sản phẩm gợi ý và file

Với `hoa-[3H2+O2=]`, mở **Gợi ý sản phẩm cho công thức đơn**, xem hỗ trợ, chọn điều kiện của bài toán và xem toàn phương trình. Nhận kết quả bằng nút hoặc Enter khi ghost hiện ở ô nhập; Space chỉ khi bật tùy chọn riêng. Nguồn vẫn giữ draft đã gõ, sản phẩm nằm trong kết quả.

**Hủy cân bằng** giữ sản phẩm đã nhận và trả hệ số trước solver. Fx → **Bỏ sản phẩm đã nhận…** cho xem nguồn sẽ trở về, rồi mới bỏ sản phẩm. File cũ không có snapshot tách hệ số giữ đường “Nguồn trước hỗ trợ” gộp cũ.

Lưu `.locus` giữ nguồn, kết quả, trước cân bằng, sản phẩm/provenance và dấu bỏ auto trong version 7. Copy đoạn hiện là text với LaTeX; PNG/SVG dùng đúng công thức đang chọn. Xuất nguyên đoạn thành DOCX, tray/bung và kiểm bộ gõ OS thuộc đợt D.
