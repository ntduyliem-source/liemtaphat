# Locus WEB1 · bản thử trên thiết bị

Mở [Locus WEB1](http://127.0.0.1:4183/) trên máy này hoặc trong in-app browser của Codex. **WEB1 đã đạt alpha trong phạm vi local; đưa lên Sites được hoãn theo yêu cầu.** Không cần tài khoản. Phím UniKey Telex/VNI thật đã được kiểm trên đúng bản WASM trong Windows WebView2; [phạm vi bộ gõ đã kiểm](IME.md) ghi rõ giới hạn từng môi trường.

Trong workspace, bấm `tools/web1/Start-Locus-Web.cmd`. Với gói Web ZIP, giải nén toàn bộ rồi bấm `Start-Locus-Web.cmd` trong thư mục vừa giải nén. Bộ mở cần Node.js 22 trở lên có trên máy (máy phát triển đang dùng Node 24); không cần SDK .NET, Word hay tải thư viện từ CDN. Bấm lại sẽ dùng server đang chạy đúng bản. `Stop-Locus-Web.cmd` dừng server do bộ mở quản lý. Nếu cổng 4183 đang phục vụ thư mục khác, bộ mở báo lỗi; dừng bản cũ bằng bộ dừng của chính nó trước khi đổi gói.

Giữ nguyên địa chỉ `http://127.0.0.1:4183/` và hồ sơ trình duyệt để giữ nháp. Đổi sang `localhost`, cổng khác hoặc hồ sơ khác sẽ có kho nháp riêng. Bộ mở không khởi động cùng Windows; bấm lại sau khi bật máy. Tệp `.locus` giúp mang nội dung giữa các hồ sơ hoặc máy.

1. Gõ `x mũ 2`, `can2`, `1 trên 2` hoặc `x+1/2`. Công thức trực tiếp hiện trước; mở **ƒx** để xem các phương án khác nếu có. Sửa lỗi luôn có nhãn riêng.
2. Chọn **Vùng có cặp bọc** để dùng `lc[x^2]`; sửa dấu mở/đóng thành `toan[[` và `]]` hoặc cặp hợp lệ khác. Locus Web phân tích vùng được bọc; không có connector Word.
3. **Tải SVG/PNG**, **Copy SVG/PNG/LaTeX/nguồn**. Chuột phải preview mở nút copy. Khi SVG không được clipboard hỗ trợ, nút ghi **Copy mã SVG**. Khi clipboard bị từ chối, dùng nút tải dự phòng. MathML/OMML nằm trong phần định dạng văn bản; copy mã OMML không tự chèn native vào Word.
4. **Lưu tệp** tải `.locus`. Trình duyệt hỗ trợ có thêm **Lưu .locus vào thư mục…**; hủy hộp thoại giữ nguyên nội dung. Mở cùng tệp trên Web hoặc Desktop giữ nguồn và cách hiểu đã lưu.
5. **Tự lưu nháp** mặc định bật. Tải lại tab phục hồi nguồn/lựa chọn/cỡ chữ. Mỗi tab có nháp riêng; mục nháp gần đây hiện tối đa 10 bản gần nhất. Mở một nháp khác tạo bản riêng cho tab hiện tại. Không đồng bộ nguồn lên tài khoản. Lịch sử Undo trong phiên không được khôi phục sau khi tải lại.
6. Đợi thông báo **Sẵn sàng dùng offline** sau lần mở đầu tiên. Sau đó có thể tắt server, tải lại và tiếp tục dựng/xuất công thức trong hồ sơ đã cache. Bản chưa được cache cần mở server trước. Trình duyệt có thể xóa dữ liệu trang; `.locus` là bản sao riêng để giữ lâu dài.
7. Khi có bản mới, bấm **Lưu nháp và cập nhật**. Nút này lưu một lần cả khi tự lưu đang tắt. Nếu lưu không thành công, giữ ứng dụng hiện tại. Những tab khác tiếp tục dùng phiên bản đang mở.

Phím tắt: **Alt+I** về ô nhập, **Ctrl+Enter** dựng lại, **Ctrl+S** lưu `.locus`, **Alt+1/2/3** chọn phương án có sẵn, **Ctrl+Z/Ctrl+Y** trong ô nhập. Khi đang ghép ký tự, Locus chờ hoàn tất.

Desktop: mở `Locus.Desktop.Shared.exe` trong gói WEB1. Cần .NET 10 Desktop Runtime và Edge WebView2 Runtime, Windows x64. Đây là host dùng chung, không thay gói M2/M3 đã phát hành.

## Build và kiểm tra

```powershell
./tools/web1/build.ps1
node tools/web1/local.mjs start
dotnet run --project tests/Locus.Application.Tests -c Release -- artifacts/web1
node tools/web1/prepare-regression.mjs
node tools/web1/verify-regression.mjs chromium
node tools/web1/verify-regression.mjs firefox
node tools/web1/verify-web1.mjs chromium
node tools/web1/verify-web1.mjs firefox
node tools/web1/verify-worker.mjs chromium
node tools/web1/verify-worker.mjs firefox
node tools/web1/verify-export-safety.mjs
node tools/web1/verify-update.mjs
node tools/web1/verify-ime.mjs
./tools/web1/package.ps1
./tools/web1/validate-package.ps1 -ExtractWeb
node tools/web1/verify-local.mjs
node tools/web1/verify-static.mjs
node tools/web1/acceptance.mjs
```

Build tĩnh dùng thư mục `site` được ghi trong `artifacts/web1/current-build.json`, chứa `index.html`, `service-worker.js` và `releases/<version>/`. Phục vụ nguyên thư mục đó tại root HTTP(S), không mở `file://` và không chỉ copy riêng một số tệp. Sau build mới, chạy `node tools/web1/local.mjs stop` trước khi đổi build hoặc dừng bản đang chạy bằng đúng gói cũ; sau đó `start` bản mới trên cùng cổng. Lệnh `status` cho biết bản đang phục vụ. Cache ứng dụng giữ các phiên cũ để tab đang mở tiếp tục chạy; hiện chưa tự thu dọn các bản cache cũ. Thiếu dung lượng khi cập nhật giữ bản đang chạy và thử lại sau.

Kiểm native: mở host với `--debug-port 9333 --receipt <đường-dẫn>`, chạy `verify-regression.mjs desktop`; cổng chỉ bật khi truyền tham số. Runtime/grammar/font đều đóng gói, không gọi backend phân tích hay CDN. Giới hạn parser là 4.096 ký tự; nguồn dài vẫn được giữ và có thể lưu tệp trong giới hạn định dạng.

`verify-ime.mjs` đối chiếu bằng chứng phím OS đã thu trên build hiện hành, không tự gõ UniKey. Sau build ứng dụng mới cần thu lại các bài OS trước khi đóng gói alpha; gói đã có không được ghi đè. Khi đổi tài liệu đóng gói, truyền `-Label` mới và kiểm lại gói đó.

Việc phát hành online không phải điều kiện của lần dùng thử local này. Cấu hình Sites cũ được giữ để tham khảo khi người dùng yêu cầu tiếp tục; chưa có URL online. Khi quay lại phát hành, lựa chọn riêng tư trước đây vẫn áp dụng nếu chưa có yêu cầu thay đổi.
