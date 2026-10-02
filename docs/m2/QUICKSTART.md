# Locus Desktop 0.2 alpha

Giải nén cả thư mục rồi mở **Locus.Desktop.exe**. Gói Windows x64 mang theo .NET runtime; không cần cài SDK, mở Word hay kết nối Internet để nhập và dựng công thức. Bản alpha chưa có chữ ký số hoặc installer.

1. Nhập `can2`, `x mũ 2`, `1 trên 2` hoặc một biểu thức trong grammar hiện tại.
2. Xem cách hiểu trực tiếp. Nếu có phương án khác hoặc đề nghị sửa, chọn rõ kết quả muốn dùng.
3. Chọn cỡ chữ và độ nét PNG. Dùng Copy hoặc Lưu SVG/PNG để đưa công thức vào ứng dụng khác.

Chọn **Chỉ vùng nằm trong cặp dấu** để dùng `lc[...]`. Có thể đổi riêng dấu mở và dấu đóng, ví dụ `<<` / `>>`, rồi nhập `Đặt <<x mũ 2>> và <<1 trên 2>>.`. Nếu có nhiều vùng, chọn vùng cần xuất ở danh sách bên phải. Chưa đóng dấu hoặc nguồn còn lỗi thì không tự thay nội dung. Desktop này chỉ nhận diện và xem trước; chuyển tự động trong Word thuộc connector ở mốc sau.

**Copy PNG** cung cấp dữ liệu PNG và bitmap nền trắng cho ứng dụng nhận bitmap. PNG lưu ra file giữ nền trong suốt, trừ khi bật nền trắng. **Copy SVG** cung cấp `image/svg+xml` cùng chuỗi SVG; ứng dụng đích có thể chỉ dán mã nguồn. Khi đó chọn **Lưu SVG** và nhập file, hoặc dùng PNG. SVG chứa đường vector nên không cần font ở máy nhận. LaTeX, nguồn, MathML và OMML là văn bản; OMML chưa phải thao tác chèn equation native vào Word.

Phím tắt: **Ctrl+L** về ô nhập; **Ctrl+Enter** phân tích ngay; **Alt+1/2/3** chọn phương án; **Ctrl+Z / Ctrl+Y** hoàn tác/làm lại trong ô nhập. **Ctrl+Shift+C** copy PNG, **Ctrl+Shift+S** copy SVG, **Ctrl+Shift+T** copy LaTeX. **Ctrl+S** lưu SVG; **Ctrl+Shift+P** lưu PNG. Chuột phải preview cũng có lệnh copy. Các kết quả cũ bị gỡ ngay khi sửa nguồn hoặc cấu hình nhận diện.

Trong Word đã thử, PNG nhận được đúng hình. Word có thể raster hóa SVG nhập từ file và tự diễn giải MathML text thành equation; giữ file SVG gốc nếu cần hình vector. Muốn dán mã SVG/LaTeX/OMML dưới dạng chữ, dùng Paste Special → Unformatted Text. Việc Word tự nhận MathML không kèm nguồn/metadata do Locus quản lý.

Ứng dụng chỉ lưu lựa chọn cặp dấu/chế độ/cỡ xuất tại `%LOCALAPPDATA%\Locus\preferences.json`. Nội dung nhập không được lưu khi đóng. Toàn bộ nhận diện/dựng hình chạy cục bộ. Chưa có đồ thị, hình học, Lý/Hóa hoặc connector Word.

Baseline kiểm tra là Windows 10 x64 build 19045. Các hệ điều hành và ứng dụng đích khác cần thử riêng. Tính năng Desktop được kiểm bằng bộ kiểm tra WPF và các thao tác thực tế ghi trong báo cáo M2; các test sự kiện composition tổng hợp không thay thế kiểm chứng đầy đủ các bộ gõ tiếng Việt.
