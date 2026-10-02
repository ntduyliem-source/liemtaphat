# Chạy bản thử nền tảng Web WEB0

Đây là prototype nền tảng, chạy công thức và một canvas nhỏ. Chưa phải website public hoặc bản Web có đủ chức năng M2. Đầu ra/baseline ở [COMPATIBILITY](COMPATIBILITY.md); kiến trúc và phần tiếp theo ở [ARCHITECTURE](ARCHITECTURE.md).

## Dùng bản đang mở

Mở [Locus WEB0 trên máy này](http://127.0.0.1:4181/). Server chỉ phục vụ tệp tĩnh; phân tích chạy bằng C# WebAssembly trong browser. URL localhost chỉ dùng trên máy này. Người dùng website sau khi được hosting không phải cài Node, .NET server hay Word.

Thử lần lượt:

1. Nhập `x mũ 2`, `1 trên 2` hoặc `can2`.
2. Nhập `x+1/2`: preview đầu là theo cú pháp; bấm **fx** để xem đề nghị `(x+1)/2`. Chọn rồi tải snapshot; nguồn vẫn giữ `x+1/2`.
3. Đổi **Cách đọc → Các vùng có cặp bọc**. Mặc định `lc[` và `]`; đổi sang `toan[[` và `]]`, thử `Ta có toan[[x^2]] và toan[[1 trên 2]].`.
4. Sang **Mặt phẳng thử**: kéo A/B/C, Hoàn tác/Làm lại, Esc để hủy một lần kéo, **Tải SVG**. Chuột phải gọi sao chép mã SVG dạng văn bản; chưa chứng nhận clipboard ảnh vector giữa các ứng dụng.
5. Mở **Đối chiếu JSXGraph** để xem curve mẫu nhận điểm số từ C#. Đây chưa phải ô nhập hàm E1.

Reload sẽ mất phiên thử; nháp và lưu/mở tài liệu đầy đủ thuộc SH/WEB1. Nguồn dài hơn 4.096 đơn vị UTF-16 vẫn được giữ nhưng không phân tích trong prototype.

## Build từ repository

Baseline đã dùng: Windows 10 x64, SDK 10.0.400, Node 24; WebView2 152 cho host Desktop. Đóng cửa sổ WEB0 Hybrid trước khi publish lại host đó. M2 và Word M3 không cần đóng để build Web.

Chạy tại thư mục repository:

```powershell
./tools/web0/build.ps1
node tools/web0/serve.mjs artifacts/web/browser/wwwroot 4181
```

Build dùng lock file NuGet/npm, tạo baseline native, dọn đúng thư mục publish browser được sinh tự động để tránh gom asset cũ, rồi publish Release. `-SkipHybrid` chỉ bỏ bước publish host WPF. Server giữ terminal đến khi Ctrl+C.

Host Desktop dùng cùng component:

```powershell
./artifacts/web/hybrid/Locus.WebProbe.Hybrid.exe
```

Host này cần .NET Desktop Runtime 10 và WebView2 đã cài; bản M2 portable hiện hành vẫn là gói Desktop cho người dùng. `Locus.Web.sln` tách các project thử khỏi solution M2/Word.

## Chạy kiểm chứng

Sau build, giữ server tĩnh ở cổng 4181 rồi chạy:

```powershell
node tools/web0/node_modules/playwright/cli.js install chromium firefox
node tools/web0/verify-ui.mjs chromium
node tools/web0/verify-ui.mjs firefox
node tools/web0/measure.mjs
```

Để kiểm Hybrid, mở riêng host thử với CDP loopback (flag này không bật trong M2):

```powershell
./artifacts/web/hybrid/Locus.WebProbe.Hybrid.exe --debug-port 9311 --receipt artifacts/web/hybrid-owned.json
```

Đợi cửa sổ hiện rồi chạy `node tools/web0/verify-ui.mjs hybrid`. Runner chọn đúng URL editor, bỏ qua trang Downloads của WebView. Đóng cửa sổ host khi xong; runner ngắt kết nối CDP mà không đóng WPF.

`--wasm-input-probe --debug-port 9313 --receipt artifacts/web/wasm-input-owned.json` mở cùng site static trong WebView2 để thử phím Windows/UniKey. `verify-ui.mjs webview-wasm` chạy suite trên trang WASM đó. Bài Telex thực cần phím OS; `composition` trong suite tự động là event được mô phỏng, không thay thế ma trận bộ gõ.

Kết quả ở `artifacts/web/native`, `artifacts/web/runs/<host>`, `performance.json`, `compatibility.json`. `measure.mjs` ghi tải nguội/ấm trên localhost và tổng các tệp publish; không dự báo tốc độ qua internet.

## Gói tĩnh

`./tools/web0/package.ps1` tạo `artifacts/releases/Locus-Web-WEB0-static.zip` gồm `wwwroot` và notices. Serve thư mục `wwwroot` qua HTTP/HTTPS; không mở `index.html` bằng `file://`. Bản này có base path `/`, cần đặt ở gốc site. URL online, base path khác, HTTPS, cache offline và cập nhật là phần WEB1 cần triển khai và nghiệm thu riêng.
