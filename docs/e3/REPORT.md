# E3 — Báo cáo Hóa/Lý trong editor chung

Triển khai 2026-09-14/15. Bản Web/Desktop: `20260915-014830-417`, .NET SDK 10.0.400. Web chỉ chạy local theo yêu cầu. [Cách dùng](QUICKSTART.md), [grammar và nguồn chuẩn](GRAMMAR.md), [backlog](../BACKLOG.md).

E3 đạt **8/8 task trong phạm vi alpha local**. [Biên bản nghiệm thu](../../artifacts/e3/acceptance.json) khóa hash của 18 nhóm bằng chứng, mã nguồn và binary. Task tiếp theo là E1-01 về đồ thị và thanh trượt; W0 G2/G3 vẫn chờ kết quả người dùng.

## Kết quả

Giữ một editor Công thức và toàn bộ luồng Toán, thêm ba checkbox độc lập. Hóa hỗ trợ nguyên tố đúng hoa/thường, nhóm/chỉ số, hệ số, điện tích tường minh và phản ứng. Lý dùng lại đại số, thêm chỉ số, Greek, vector và giá trị kèm đơn vị. Một tập tối đa ba candidate tổng; các miền trùng nghĩa được gộp. Repair vẫn tách biệt, mơ hồ chặn điều kiện auto.

Raw Unicode và spans được giữ; tắt detector không xóa snapshot đã dựng. Mỗi lần đổi cài đặt hủy việc chờ và lease xuất cũ. Tệp công thức/preferences mới ghi v2, đọc được v1 với Math mặc định; snapshot Math vẫn nguyên phiên bản/ID/ngữ nghĩa cũ. Snapshot chứa science dùng schema mới và kiểm domain/node/ID/checksum.

Word thủ công có cùng checkbox trong phần cặp bọc. Bảng preview dựng từ candidate đã chọn; native chèn OMML cùng metadata trong một Undo. Chữ đơn vị/nguyên tố thẳng đứng, dấu vector được đưa vào phép so sánh nội dung native để phát hiện chỉnh sửa làm đổi biểu diễn khoa học. Không cấp quyền auto-Space từ checkbox.

## Bằng chứng

| Phạm vi | Kết quả / tệp |
| --- | --- |
| Core mới | [416/416](../../artifacts/e3/core-verification.json): 50 case × 8 tổ hợp và trường hợp ngữ cảnh, marker, nguồn, giới hạn, xung đột |
| Toán cũ | [238 hợp đồng](../../artifacts/e3/math-regression.json) đạt, chạy bằng API trong bộ kiểm E3, không ghi đè bằng chứng M1 |
| Application E3 | [19/19](../../artifacts/e3/application-verification.json): flags/preference/wire, tệp cũ, tắt/mở snapshot, sửa/Undo, stale và composition |
| Application hồi quy | [35/35](../../artifacts/e3/application-regression/application-tests.json), báo cáo core và fixture mới trong cùng thư mục |
| Desktop/preview Toán hồi quy | [40/40 Desktop](../../artifacts/e3/desktop-math-regression/verification.json) và [7/7 bảng Word](../../artifacts/e3/word-math-panel-regression/report.json) |
| Chromium / Firefox | [14 nhóm Chromium](../../artifacts/e3/runs/chromium/ui.json), [14 nhóm Firefox](../../artifacts/e3/runs/firefox/ui.json): checkbox, source/fx, native-authored file, tệp WEB1, marker/prose, IME event, SVG/PNG, nháp/reload/mobile/offline |
| WASM/native | [504/504 mỗi Chromium](../../artifacts/e3/runs/chromium/worker-parity.json) và [Firefox](../../artifacts/e3/runs/firefox/worker-parity.json), so sánh toàn bộ bytes đáp ứng: 400 E3 + 104 Toán |
| Desktop Hybrid | [12/12 nhóm](../../artifacts/e3/runs/desktop/ui.json) trên WebView2 152.0.4191.66; dùng thư mục profile riêng |
| Hình xuất chung | [14/14 mẫu](../../artifacts/e3/renderer-parity.json): SVG artwork và LaTeX giống trên ba host. Bỏ đúng thuộc tính ID gắn phiên nguồn khi so hình, vì Firefox tạo lịch sử composition khác; mỗi host kiểm ID SVG bằng candidate hiện tại |
| Bảng preview Word | [19/19](../../artifacts/e3/word-panel/report.json): chín mẫu ở hai kích thước và bài vượt giới hạn; kiểm control/ảnh, không phải screenshot Word |
| Word native | [33/33 vòng kiểm cuối](../../artifacts/e3/word-native-final/report.json): chuyển/native+metadata, 17 mẫu, Undo/Redo, save/reopen, tắt detector rồi restore, 8 flags, stale, nguồn lỗi và kiểm dấu vector/kiểu chữ |
| Chạy gói sau giải nén | [5/5 Web](../../artifacts/e3/delivery-web.json), [4/4 Desktop](../../artifacts/e3/delivery-desktop.json): dựng Hóa/Lý, PNG, mở snapshot host kia, reload và offline Web |

Gói và hash được chốt ở [packages.json](../../artifacts/e3/packages.json); kiểm sau đóng gói ở [package-verification.json](../../artifacts/e3/package-verification.json). Các bằng chứng WEB1/M1/M3 đã phát hành và ZIP cũ giữ nguyên.

Add-in trên máy đã chuyển từ thư mục thử sang thư mục gói E3 đã phát hành. [Kiểm đăng ký Word](../../artifacts/e3/delivery-word.json) xác nhận CodeBase/LoadBehavior và hai DLL giống hệt binary đã đạt 33 kiểm tra native; đây là kiểm bàn giao, không tính thành một vòng Word native mới. Tab in-app browser đã cập nhật lên build trên và giữ bản nháp `Ca(OH)2`, preview chỉ số đúng.

## Phạm vi và sự cố trong kiểm thử

- Word: main story/đoạn văn thường, Word Windows x86 Office16 + .NET Framework 4.8. Dùng public COM API của add-in để xác nhận trên UI thread; Computer Use đưa focus vào tài liệu thử. Đây không phải một lượt nghiệm thu người dùng W0 hoặc bộ gõ OS mới.
- Lượt Word đầu bị chặn vì Desktop còn giữ focus. Một lượt có kiểm nhầm H₂SO₄ là ion nên yêu cầu điện tích không tồn tại; đã sửa điều kiện test. Bộ kiểm từng gặp trạng thái pending khi poll COM dày; tăng khoảng poll lên 250 ms và chờ tối đa 8 giây để Word có thời gian xử lý timer. Cổng ghi của sản phẩm giữ nguyên.
- Lượt Desktop đầu mở nháp thử WEB1 có sẵn. Đã khôi phục nguồn/cỡ chữ/nền của fixture theo [bằng chứng](../../artifacts/e3/default-profile-restored.json), rồi bổ sung `--profile` và chạy nghiệm thu trong thư mục riêng. Các kết quả lỗi ban đầu không tính đạt.
- PDF/in ấn, máy Word x64, DPI nhiều màn hình, nhập OS Telex/VNI trên đúng build E3 chưa được nghiệm thu riêng. Bằng chứng IME WEB1 vẫn giữ nguyên; kiểm composition E3 là sự kiện mô phỏng trên từng host.
- Chưa cân bằng phản ứng, xác nhận đúng Hóa/Lý, vẽ phân tử, trạng thái chất/nhãn mũi tên, đổi đơn vị/kiểm thứ nguyên, scientific notation. Giới hạn đầu vào 4096 UTF-16; việc vượt phạm vi giữ nguồn và từ chối.
- E1 đồ thị/thanh trượt và E2 hình học còn ở hàng đợi. W0 G2/G3 chờ phản hồi cũ của người dùng, không mở bằng E3.

## Chạy lại

```powershell
dotnet run --project tests/Locus.Science.Tests -c Release -- D:/duan/HMDA/locus
dotnet run --project tests/Locus.Application.Tests -c Release -- artifacts/e3/application-regression
./tools/e3/build.ps1
./tools/e3/local.ps1 -Action start
node tools/e3/verify-ui.mjs chromium
node tools/e3/verify-ui.mjs firefox
node tools/e3/acceptance.mjs
```

Desktop test: chạy exe đúng build với `--debug-port 9334 --profile <thư-mục-thử-trống> --receipt <tệp-ownership>`, rồi `node tools/e3/verify-ui.mjs desktop`. Chỉ kết nối process của tác vụ. `node tools/e3/verify-parity.mjs` đối chiếu ảnh ba host.

Word test: build `tests/Locus.Word.Tests`, đăng ký đúng DLL E3 khi Word đã đóng, đặt `LOCUS_WORD_TEST_SUITE=science-panel` hoặc `science`, chạy exe với thư mục đầu ra riêng. Suite native từ chối chạy khi đã có WINWORD; mỗi tài liệu thử thuộc suite, cài đặt cặp bọc/flags được phục hồi. Focus thật cần ở Word và không chạy một test UI khác tranh focus. Gói cũ có thể cài lại từ đường dẫn đã lưu trong `artifacts/e3/word-prior-registration.json`.
