# WEB1 · nghiệm thu alpha local

Ngày 2026-09-14. **WEB1 đã hoàn thành alpha local, 4/4 task nghiệm thu.** Theo yêu cầu mới nhất, đưa lên Sites được hoãn. Bản Web và Desktop dùng chung editor công thức, nháp, định dạng tài liệu và renderer.

Tiến độ nghiệm thu: **4/4 task = 100% WEB1 trong phạm vi alpha local đã công bố**, không phải 100% toàn dự án. WEB1-04 đổi từ phát hành online sang bàn giao local theo [quyết định phạm vi](delivery-scope.json). WEB1-03 được đóng sau khi thu được phím UniKey Telex/VNI thật trên đúng bản WASM; phạm vi OS là Windows WebView2, ghi rõ tại [IME](IME.md).

## Chức năng đã triển khai

- Cùng editor C#/Razor trên Web và Desktop: nguồn nguyên văn, trực tiếp trước, tối đa ba candidate, repair có nhãn; Undo/Redo, phím tắt/focus, tùy chọn cặp bọc/cỡ chữ/nền được lưu.
- SVG/PNG đúng hình đang xem; copy SVG theo capability hoặc mã SVG, PNG, LaTeX, nguồn, MathML, OMML. Có tải dự phòng khi clipboard bị từ chối. Hủy/lỗi/chỉnh nguồn trong hộp lưu không ghi tệp từ phiên cũ.
- `.locus` Web ↔ Desktop giữ Unicode, Int64, vùng và candidate đã chọn. Nháp IndexedDB theo tab, journal đồng bộ giữ ký tự trước debounce, phục hồi sau reload, tách tab trùng và nháp chưa hỗ trợ. Nháp Desktop giữ định danh qua lần chạy. Tự lưu tắt/đầy bộ nhớ có thông báo đúng.
- Static build: toàn bộ app nằm dưới URL phiên bản riêng. Service worker kiểm đủ tệp và hash trước khi kích hoạt; offline có parser/worker/font/renderer. Cập nhật do người dùng bấm sau khi lưu nháp. Tab cũ tiếp tục dùng đúng tài nguyên của bản cũ.

## Bằng chứng

| Kiểm tra | Kết quả / bằng chứng |
| --- | --- |
| Core + Application | 238 core contracts trong 35 nhóm Application; [application-tests](../../artifacts/web1/application-tests.json) |
| Worker | 104/104 nguồn trùng native trên mỗi Chromium/Firefox, timeout/cancel/retry đạt; [Chromium](../../artifacts/web1/worker-chromium.json), [Firefox](../../artifacts/web1/worker-firefox.json) |
| Hồi quy UI | 14/14 nhóm trên mỗi Chromium, Firefox và WebView2; thư mục [runs](../../artifacts/web1/runs) |
| WEB1 | 16/16 nhóm mỗi Chromium và Firefox: journal, nháp, clipboard, picker, offline, quota, responsive; [Chromium](../../artifacts/web1/runs/chromium/web1.json), [Firefox](../../artifacts/web1/runs/firefox/web1.json) |
| Cập nhật | 5/5: bản tải thiếu bị loại, chờ xác nhận, tự lưu tắt vẫn lưu một lần khi cập nhật, tab cũ, reload offline bản mới; [update](../../artifacts/web1/update.json) |
| Xuất | 5/5 safety; PNG đang encode bị hủy khi nguồn đổi, chặn xuất trước interop, alpha, từ chối clipboard, giới hạn kích thước; [export-safety](../../artifacts/web1/export-safety.json) |
| Native | 11/11: file ba host, Unicode/Int64, PNG bitmap, 7 công thức tương đương M2; [native-integration](../../artifacts/web1/native-integration.json) |
| Khởi động lại Desktop | Nguồn, candidate, cỡ chữ, độ nét và nền phục hồi sau dừng/mở lại host; [native-restart](../../artifacts/web1/native-restart.json) |
| Clipboard Windows | Đã đọc lại PNG/CF_BITMAP và SVG/mã Unicode từ clipboard thật; bytes trùng tệp xuất; [PNG](../../artifacts/web1/clipboard-os.json), [SVG](../../artifacts/web1/clipboard-svg-os.json) |
| Bàn giao local | 13/13: giải nén ZIP, hash/HTTP/MIME, mở lặp, dừng đúng phiên, Chromium/Firefox phục hồi và xuất SVG/PNG/.locus khi server thực sự tắt, mở lại giữ sửa đổi; [local-validation](../../artifacts/web1/local-validation.json) |
| In-app browser | Đã mở bản local trong Codex, kiểm trực tiếp/repair, giữ nguồn và phục hồi nháp; [in-app-validation](../../artifacts/web1/in-app-validation.json). Không tính thao tác browser là Telex/VNI OS |
| Bộ gõ thật | 19 kiểm tra bằng chứng: Telex/VNI, sửa dấu, Backspace/Undo/Redo, focus, SVG/PNG/.locus/reload, trả về Telex; [IME](../../artifacts/web1/ime/status.json), [phạm vi](IME.md) |

Giao diện đã kiểm ở 1280 px và 390 px; nguồn dài/toolbar không làm tràn ngang toàn trang. Browser đã kiểm: Chromium 153, Firefox 155, WebView2 152. Đây là ma trận môi trường đã chạy, chưa phải cam kết mọi phiên bản trình duyệt.

Gói hiện hành giữ build ứng dụng `20260914-070957-601`: [Web alpha local](../../artifacts/releases/Locus-WEB1-20260914-070957-601-alpha-local-Web-static.zip), [Desktop alpha](../../artifacts/releases/Locus-WEB1-20260914-070957-601-alpha-local-Desktop-win-x64.zip). Tất cả 186/268 tệp trong hai ZIP đã kiểm hash; 172 tài nguyên phiên bản và 64 tài nguyên renderer khớp bản đã thử. Gói alpha thêm phạm vi IME đã nghiệm thu và cập nhật hướng dẫn; không build lại app hoặc đổi core. [Biên bản nghiệm thu](../../artifacts/web1/acceptance.json), [đối chiếu static](../../artifacts/web1/static-validation.json).

## Phạm vi nghiệm thu và phần đã hoãn

**WEB1-03 / DONE:** đã thu phím OS trên Windows WebView2 152.0.4191.66 nạp đúng website WASM hiện tại. `x mux 2` (Telex) và `x mu4 2` (VNI) cùng tạo `x mũ 2` / `{x}^{2}`. Cả hai đổi dấu trong từ, Backspace/Undo/Redo, xuất ba định dạng và phục hồi nháp đúng; PNG trùng bytes. Lượt Telex kiểm thêm Tab/Alt+I giữ nội dung. Đã trả UniKey về Telex và gõ lại để xác nhận. [Bằng chứng](../../artifacts/web1/ime/status.json).

Các lỗi ownership/geometry trước đó vẫn được giữ trong [lịch sử](../../artifacts/web1/ime/status-before-real-os.json). Đường nhận phím thành công dùng điều hướng Windows Tab/Alt+I vào nội dung WebView2. Không suy rộng thành nghiệm thu phím OS trên mọi Chromium/Firefox hay in-app browser; các host này có bộ kiểm tự động riêng. UniKey trong baseline phát Backspace và Unicode thay thế, nên bài composition giả lập vẫn được ghi tách biệt.

**Online / hoãn theo yêu cầu:** không phải điều kiện chờ của WEB1 local. Chưa đẩy mã, lưu phiên bản hay deploy. Giữ [lịch sử kết nối](../../artifacts/web1/hosting-status.json) và cấu hình đã đăng ký để tham khảo khi người dùng yêu cầu phát hành tiếp. Lựa chọn riêng tư trước đây vẫn áp dụng khi quay lại, nếu chưa có thay đổi.

**WEB1-04 / DONE local:** mở `tools/web1/Start-Locus-Web.cmd` hoặc bộ mở trong ZIP; địa chỉ `http://127.0.0.1:4183/`. Server chỉ phục vụ tệp trên loopback, phân tích vẫn chạy trong browser. Bộ mở cần Node.js (đã kiểm 24.15.0), không tự chạy cùng Windows. Mở lặp dùng đúng phiên đang chạy; cổng bận bởi bản khác báo lỗi và giữ tiến trình đó. Bộ dừng xác minh định danh phiên trước khi gửi yêu cầu dừng. Chromium/Firefox đã tải lại, nhập công thức mới và xuất khi tiến trình server đã tắt thật.

## Cách tiếp tục

1. E1-01 là công việc kế tiếp: hợp đồng PlotDocument, lớp adapter đồ thị và prototype nhiều hàm. Sau đó E1-04 bổ sung evaluator/grammar, E1-02 lấy mẫu, E1-03 chỉnh/xuất và E1-05 nghiệm thu Web/Desktop.
2. Hóa/Lý và hình học tiếp tục theo roadmap; các cổng Word W0/G2/G3 giữ độc lập.
3. Khi đổi build ứng dụng, cần kiểm lại IME và các luồng bị ảnh hưởng trước khi nghiệm thu bản mới. Online chỉ tiếp tục khi người dùng yêu cầu; thiết kế template đã gác lại không đưa vào WEB1.

## Giới hạn và phát hiện đã sửa

- Nháp không phải cloud backup; trình duyệt có thể xóa dữ liệu. Không khôi phục lịch sử Undo qua reload. Mục gần đây hiển thị 10 nháp; không tự xóa các nháp khác.
- Giữ các cache ứng dụng cũ để phục vụ tab cũ; chưa tự thu dọn. Lỗi cài cache/bộ nhớ không xóa nguồn hoặc bản đang chạy. Bài picker cancel/disk-full là mô phỏng lỗi API có chủ đích; PNG/SVG clipboard Windows là đọc thật.
- Đã sửa lỗi địa chỉ root không nằm dưới base URL phiên bản của Blazor, và định danh nháp Desktop mất sau khi khởi động lại. Gói `Locus-WEB1-preview-*` tạo trước lần sửa native đã bị thay thế, không phải gói bàn giao. [Gói hiện hành](../../artifacts/web1/package.json) và [biên bản](../../artifacts/web1/acceptance.json) là nguồn đối chiếu.
- Core sản xuất và gói M2/M3/WEB0/SH được giữ nguyên. Cảnh báo IL2026 của serializer khi trim Worker còn được ghi nhận; các assembly serializer được root và đã kiểm runtime parity.
- Chưa có Word trong website, đồ thị, hình học, Lý hoặc Hóa. W0 vẫn 4/6, G2/G3 và quyết định A/B chưa đổi; không yêu cầu lại bài người dùng đang để sau.
