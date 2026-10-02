# Dùng thử editor SH

Web: [mở Locus SH](http://127.0.0.1:4182/) khi server đang chạy. Đây là URL cục bộ trên máy này. Parser, renderer và nguồn đều xử lý trên thiết bị.

Desktop: giải nén `Locus-SH-Desktop-win-x64.zip`, mở `Locus.Desktop.Shared.exe`. Bản nền này cần .NET 10 Desktop Runtime và WebView2 Runtime trên Windows x64; máy nghiệm thu đã có các runtime đó. Gói M2 portable trước đây vẫn dùng riêng. [Phạm vi đã kiểm](REPORT.md).

1. Gõ `x mũ 2`, `1 trên 2`, `căn((x+1)/2)` hoặc `x+1/2`. Mở **ƒx · Các phương án** để chọn repair nếu có.
2. Chọn **Vùng có cặp bọc** để dùng `lc[x^2]`. Đổi được dấu mở/đóng, ví dụ `toan[[` và `]]`.
3. **Hoàn tác/Làm lại** hoặc Ctrl+Z/Ctrl+Y trong ô nguồn giữ cả nguồn và lựa chọn. Đang ghép dấu thì chưa xuất kết quả.
4. **Lưu tệp** tạo `.locus`. **Mở tệp .locus** trên Web hoặc Desktop giữ đúng nguồn/candidate đã lưu. Mở tài liệu thay phiên hiện tại và bắt đầu lịch sử của tài liệu đó; lưu tài liệu hiện tại trước khi mở tài liệu khác.
5. **Tải SVG/PNG**, **Copy PNG/LaTeX/nguồn** theo browser/clipboard cho phép. Tệp ảnh dùng công thức đang xem. Đổi cỡ chữ, độ nét PNG hoặc nền trắng ở dưới preview.
6. Đóng tab bằng × rồi mở lại giữ phiên trong lần chạy hiện tại. **Lưu `.locus` trước khi reload trang hoặc thoát app.** Nháp tự phục hồi thuộc WEB1.

Tệp hỏng hoặc chưa hỗ trợ không thay nguồn đang mở. Với tệp đã đọc được nhưng chưa hỗ trợ, có nút tải lại nguyên tệp. Các tệp Plot/Geometry sẽ dùng ở editor tương ứng về sau.

## Build và kiểm tra trong repository

Yêu cầu SDK theo `global.json`, workload `wasm-tools`, Node và runtime Windows nêu trên. Khôi phục dependency bằng lock file; không cần cài Node trên máy chỉ dùng website.

```powershell
./tools/sh/build.ps1
node tools/sh/serve.mjs
```

Build tạo thư mục mới trong `artifacts/sh/builds`, ghi đường dẫn vào `artifacts/sh/current-build.json`. Server đọc đường dẫn này; không trộn tệp runtime từ hai lần publish. Publish static dùng thư mục `web/wwwroot` làm root website qua HTTP(S); không mở bằng `file://`. Chưa có service worker/offline reload.

```powershell
dotnet run --project tests/Locus.Application.Tests -c Release
node tools/sh/verify-ui.mjs chromium
node tools/sh/verify-ui.mjs firefox
node tools/sh/verify-worker.mjs chromium
node tools/sh/verify-worker.mjs firefox
node tools/sh/verify-export-safety.mjs
```

Kiểm Desktop bằng app SH được mở với `--debug-port 9331`, rồi chạy `node tools/sh/verify-ui.mjs desktop` và `dotnet run --project tests/Locus.Shared.Desktop.Tests -c Release`. Cổng debug chỉ bật khi truyền tham số; mở bình thường không bật. Script clipboard `--clipboard` chỉ đọc dữ liệu PNG vừa copy trong bài thử SH, không dùng khi clipboard đang chứa nội dung khác của người dùng.
