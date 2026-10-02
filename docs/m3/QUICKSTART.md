# Locus Word M3 — bản thử chuyển thủ công

Phạm vi đầu: Microsoft Word desktop **x86** trên Windows, .NET Framework 4.8. Baseline đã dùng để kiểm: Word 16.0.14026.20302 trên Windows x64. Đây là bản alpha cài theo tài khoản hiện tại; chưa là installer ký số hoặc chứng nhận các phiên bản Word khác.

## Cài và gỡ

1. Giải nén toàn bộ gói `Locus-Word-0.3.0-alpha-x86.zip` vào một thư mục giữ cố định.
2. Đóng Word, rồi chạy PowerShell trong thư mục gói: `./register.ps1 -Action Install`.
3. Mở Word. Dùng tab **Locus**, nút **Chuyển vùng chọn**. Add-in của bản này có ProgID `Locus.Word.Manual`; tab `Locus W0` là bản nghiên cứu riêng.

Để gỡ: đóng Word và chạy `./register.ps1 -Action Uninstall` từ chính thư mục đã cài. Script chỉ sửa ba nhánh đăng ký HKCU của bản này, không thay add-in khác hoặc cấu hình bảo vệ Word. Giữ nguyên thư mục cho tới khi gỡ; muốn chuyển thư mục thì gỡ bản cũ trước. Không có quyền admin là đủ cho đăng ký per-user.

Khi làm từ source: build `dotnet build Locus.Word.sln -c Release`, rồi `./tools/m3/register.ps1 -Action Install`. Cần các interop Office đã cài trên máy build; gói chạy không phân phối lại Office.

## Chuyển một công thức

1. Gõ `x mũ 2`, `1 trên 2` hoặc `x+1/2` trong một đoạn văn bản thông thường.
2. Bôi đen riêng chuỗi công thức, không chọn dấu xuống đoạn. Chọn **Locus → Chuyển vùng chọn**.
3. Kiểm nguồn và hình xem trước. **fx — Xem các phương án** mở những phương án còn lại nếu có; một đề nghị sửa được ghi rõ và có nhãn xác nhận riêng.
4. Nhấn **Chuyển thành công thức** hoặc **Áp dụng đề nghị sửa**. Word nhận equation native, con trỏ về sau equation để viết tiếp.
5. Ctrl+Z hoàn tác lần chuyển; Ctrl+Y làm lại. Preview hoặc nút Đóng không sửa tài liệu.

Đường bàn phím đã kiểm: nhấn và thả **F10**, lần lượt `L`, `C` để vào tab Locus, rồi `C` để mở vùng chọn hoặc `M` để mở công thức đã lưu. Tab di chuyển trong bảng, Alt+C kích hoạt chuyển trực tiếp, Alt+K khôi phục nguồn. Với repair, dùng nút mang nhãn áp dụng đề nghị sửa. Enter không tự xác nhận khi mở bảng; Enter trên một nút đã có focus vẫn kích hoạt nút đó theo chuẩn Windows. Esc đóng preview.

Khi nguồn/vùng/tài liệu/cửa sổ đổi, hoặc nhập tiếp/chuyển focus trong lúc đang xác nhận, phiên cũ bị hủy. Chọn lại vùng và mở Locus nếu cần tiếp tục; Locus không tự chọn lại nguồn mới để ghi.

## Mở lại, khôi phục, tách quản lý

- Đặt con trỏ **bên trong** một equation Locus và chọn **Mở công thức Locus**. Bảng đọc nguồn và bộ kết quả đã lưu, không parse lại bằng grammar mới.
- **Khôi phục nguồn** thay equation bằng đúng nguồn đã dùng, kể cả dấu tiếng Việt tổ hợp và cặp bọc. Có thể Undo thao tác này.
- **Tách quản lý** giữ equation native và bỏ liên kết Locus. Có thể Undo để lấy lại metadata.
- Người nhận file không cài Locus vẫn xem/sửa equation native được. Nếu đã sửa native hoặc metadata lệch/hỏng/trùng ID, bản này giữ nội dung hiện tại và từ chối thao tác quản lý không đủ dữ liệu.

Đổi candidate đã lưu hoặc sửa nguồn qua Desktop thuộc M4. M3 có thể xem lại các phương án cũ; muốn chuyển lại từ nguồn, khôi phục nguồn rồi mở một phiên chuyển mới.

M3 dùng trực tiếp tab Locus trong Word. Tab kết nối Word của gói Desktop M2 vẫn dành cho connector nghiên cứu W0; kết nối chỉnh sửa với M3 sẽ được tích hợp ở M4/M6.

## Cặp bọc và giới hạn

Tab **Locus → Cặp bọc** cho phép đổi hai dấu; mặc định `lc[` và `]`. Chọn đầy đủ `lc[x mũ 2]` rồi chuyển thủ công. Restore trả đúng cặp dấu đã lưu, kể cả sau khi cấu hình hiện tại đổi.

M3 không quan sát tự động nội dung đang gõ và không tự chuyển marker hoặc Space. Phạm vi đầu hỗ trợ vùng một đoạn trong thân tài liệu, ngoài bảng/header/footer/field/equation/control có sẵn; từ chối read-only, protection hoặc Track Changes. Thân tài liệu được giới hạn 500.000 vị trí Word cho kiểm phiên của alpha, vùng nguồn tối đa 4.096 ký tự trước giới hạn parser/metadata/renderer. Phiên hết hạn sau hai phút.

Nhận diện và dựng hình chạy cục bộ. Nội dung preview chỉ giữ trong bộ nhớ phiên; đóng/hủy sẽ bỏ nguồn của phiên. Cài đặt chỉ lưu cặp dấu; không có pipe chẩn đoán hoặc log nguồn tự động trong entry point M3.

## Kiểm lại từ source

`./tools/m3/verify.ps1` chạy bài M3 rồi kiểm hồi quy Word/Core/Desktop. Đóng Word trước khi chạy. Nếu Windows không cho Word tự lấy foreground, bộ thử chờ tối đa 55 giây ở cửa sổ **Locus M3 verification** để kích hoạt editor; không bỏ qua điều kiện focus khi chạy kiểm. Các tài liệu của bộ thử đều là dữ liệu tổng hợp.

`tools/m3/start-demo.ps1` mở một tài liệu thử riêng khi chưa có Word chạy, thông qua bộ thử x86 đã build, để thử Ribbon và bàn phím. Kết quả và phạm vi nghiệm thu cuối theo REPORT.md cùng `artifacts/m3/acceptance.json` trong repo.
