# Locus M1: CLI và bản xem trước cục bộ

Ứng dụng này gọi trực tiếp `AnalysisEngine` trong assembly `Locus.Core`.
MathML, OMML, LaTeX và nguồn nguyên văn đều lấy từ chính candidate được trả về;
không có parser JavaScript hoặc tra cứu đáp án trong fixture.

Chạy tại thư mục gốc bằng .NET SDK 10:

```powershell
dotnet run --project src/Locus.Cli -- --input '2/3x' --mode explicit
dotnet run --project src/Locus.Cli -- --input 'Đề: lc[x^2]' --mode marked
dotnet run --project src/Locus.Cli -- --input 'Nội dung ⟦x mũ 2⟧' --mode marked --open '⟦' --close '⟧'
dotnet run --project src/Locus.Cli -- --serve 4180
```

CLI xuất JSON ra stdout; lỗi tham số ra stderr với exit code 2. Ba chế độ là
`explicit` (cả vùng), `passive` (tìm trong văn bản) và `marked` (cặp dấu).

Chế độ `--serve` mở HTTP trên `127.0.0.1`, mặc định cổng 4180. Mở
[bản xem trước](http://127.0.0.1:4180/) và dùng Ctrl+C tại tiến trình server để dừng.
Trang cho phép đổi nguồn, chế độ và cặp dấu; xem các candidate và sao chép LaTeX,
OMML hoặc nguồn, bao gồm dấu bao nguyên bản khi có.

Mỗi thay đổi nguồn hoặc cấu hình xóa ngay bản xem trước và nút sao chép cũ trước
khoảng chờ 180 ms. Request trước được hủy; revision và raw source của phản hồi
được đối chiếu trước khi hiển thị. Sự kiện composition trong trình duyệt tạm hoãn
phân tích; đây không phải bằng chứng về bộ gõ hoặc focus trong Microsoft Word.

Server chỉ phục vụ ba asset cố định và endpoint JSON `/analyze`. Host và Origin
phải là địa chỉ loopback của cổng đang chạy. POST thiếu Origin hoặc không dùng
JSON bị từ chối. Giới hạn HTTP body là 256 KiB; nguồn tối đa 65.536 code unit
UTF-16, từng vùng parse tối đa 4.096. Nội dung được xử lý trong bộ nhớ, không ghi
log hoặc lưu nguồn. Checksum của snapshot core phát hiện hỏng dữ liệu, không có
ý nghĩa xác thực người gửi.

MathML là bản thử renderer trên trình duyệt. Nó chưa chứng minh độ tương đồng
pixel với Word, SVG/PNG export, kết nối Word hoặc clipboard native equation.
`Copy OMML` sao chép chuỗi XML; việc chèn equation native thuộc connector Word.
