# SH — Editor và tài liệu dùng chung

Ngày nghiệm thu: 2026-09-14. **SH đạt 4/4 task trong phạm vi nền editor công thức.** Đây là tiến độ riêng của SH. WEB1, đồ thị, Lý/Hóa và hình học vẫn theo backlog; chưa tính phần trăm toàn sản phẩm từ số task.

Web và Desktop hiện dùng cùng `FormulaSession`, codec `.locus`, component editor và renderer. Web phân tích trong worker WebAssembly; Desktop phân tích trên .NET native. Cả hai dùng đúng `Locus.Core`, không có parser JavaScript thay thế. [Bản dùng thử](QUICKSTART.md), [receipt nghiệm thu](../../artifacts/sh/acceptance.json).

## Đầu ra theo task

| Task | Đã hoàn thành | Bằng chứng |
| --- | --- | --- |
| SH-01 | Session nguồn/cấu hình/candidate, history 100 bước, composition một bước Undo, cancellation, lease xuất; adapter scheduling/file/clipboard | [Application](../../src/Locus.Application/FormulaSession.cs), [32 nhóm kiểm tra](../../artifacts/sh/application-tests.json), [worker Chromium](../../artifacts/sh/worker-chromium.json), [worker Firefox](../../artifacts/sh/worker-firefox.json) |
| SH-02 | Format version/kind/ID/revision, Formula/Plot/Geometry riêng; lưu snapshot/candidate không parse lại; file mới hơn/hỏng giữ nguyên | [Hợp đồng tệp](DOCUMENT-FORMAT.md), [codec](../../src/Locus.Application/WorkspaceDocument.cs), [native integration](../../artifacts/sh/native-integration.json) |
| SH-03 | MathML từ candidate → SVG chứa glyph path; chính SVG đó tạo PNG; font local, cỡ chữ/scale/nền, kiểm giới hạn ảnh trước cấp phát canvas | [Renderer](../../src/Locus.Editor/wwwroot/renderer.js), [đối chiếu M2](../../artifacts/sh/renderer-comparison.html), [export safety](../../artifacts/sh/export-safety.json) |
| SH-04 | Editor chung trong Web và tab Desktop mới; đóng/mở tab giữ phiên, nguồn/lựa chọn/history, không nhân event; cùng lưu/mở/xuất | [Editor](../../src/Locus.Editor/Workspace.razor), [Web UI](../../artifacts/sh/runs/chromium/ui.json), [Firefox UI](../../artifacts/sh/runs/firefox/ui.json), [Desktop UI](../../artifacts/sh/runs/desktop/ui.json) |

## Kết quả kiểm tra

- **32/32 nhóm Application**, trong đó một nhóm chạy lại **238/238 contracts Core**. Kiểm kết quả đến muộn dù scheduler bỏ qua cancellation, lỗi đến muộn, composition, đổi marker, lease giữa các phiên, giới hạn nguồn và history, nhập tệp lỗi/mới hơn.
- **104/104 case worker khớp toàn bộ chuỗi wire với native trên Chromium và Firefox**. So sánh gồm source, revision, snapshot/hash, thứ tự/ID candidate, diagnostics. Wire giữ Int64 trong chuỗi JSON; JavaScript không giải mã rồi mã hóa lại revision.
- Worker giả lập vòng lặp chặn kiểm deadline 180 ms, startup timeout, cancel, dispose và khởi động lại. Trong lúc worker bị chặn, timer UI vẫn chạy 15–18 lần. Đây là fault injection; deadline sản phẩm là 5 giây xử lý và 20 giây khởi động. Native dùng cancellation hợp tác và timeout 5 giây; không tuyên bố có thể cưỡng bức kết thúc một thread native bất kỳ.
- **14/14 nhóm UI trên mỗi host**: Chromium 153.0.8010.12, Firefox 155.0 và Desktop WebView2 152.0.4191.66. Bao gồm lựa chọn repair, Undo/Redo, marker tùy chỉnh, nhiều vùng, đóng/mở tab ba lần, nhập nhanh/nguồn dài, tệp lỗi và preview/xuất. Firefox `fill()` sinh compositionstart/end; bộ kiểm tính đúng ba thay đổi phiên cho chuỗi event này.
- **11/11 nhóm native integration**: đọc lại tệp từ cả ba host, giữ NFD/emoji/Int64 và lựa chọn vùng; tạo dữ liệu clipboard PNG/bitmap; bảy công thức đối chiếu với renderer M2.
- **5/5 bài export safety**: đổi nguồn khi đang mã hóa PNG phải hủy tải, chặn xuất ngay khi DOM nhận input, PNG alpha, clipboard bị từ chối giữ nguồn, giới hạn ảnh. PNG nền trắng được sửa để phủ kín góc ảnh và giữ đúng tỷ lệ của SVG.
- **7/7 mẫu SVG giống nhau trên ba host**, bỏ riêng ID candidate do revision lịch sử khác nhau. M2 và SH cho cùng LaTeX; đã xem ảnh căn/phân số/mũ/ngoặc/NFD để đối chiếu. Font NewCM và Cambria Math có hình dáng khác nhau; không đặt mục tiêu pixel giống M2.
- Clipboard Windows thật có `PNG` và bitmap nền trắng; PNG đọc lại khớp byte tệp xuất, **592 × 418 px** cho bài mẫu. [Receipt](../../artifacts/sh/clipboard-os.json). Không sửa tài liệu Word trong đợt SH.
- Hồi quy M2 **40/40 nhóm** đạt. Runtime .NET 10/.NET Framework 4.8 x86 khớp với 13 nguồn; production Core và các ZIP M2/M3/WEB0 được giữ theo checksum gốc.

## Cấu trúc và giới hạn

`Locus.Application` chứa session và các loại tài liệu, không tham chiếu WPF/COM. `Locus.Editor` chứa Razor và renderer dùng chung. `Locus.Web` đăng ký worker scheduler; `Locus.Desktop.Shared` đăng ký native scheduler và clipboard Windows. File input dùng cùng Blazor component; lệnh tải tệp đi qua adapter và cơ chế download của browser/WebView2. `FormulaWorkspace` giữ phiên khi view được đóng/mở; thoát app hoặc reload trang cần tệp đã lưu để khôi phục.

Desktop SH là entry point chuyển tiếp riêng. M2 và renderer WPF còn nguyên cho Word/đối chiếu; chưa thay gói M2 đang dùng. SH chưa có đầy đủ phím tắt, Copy SVG/MathML/OMML, nháp tự phục hồi hoặc preferences lưu qua lần mở như phạm vi WEB1. Plot/Geometry có schema nền để mở rộng; editor tương tác của chúng thuộc E1/E2.

UI được kiểm qua Playwright/CDP trên app thật. Bài composition SH là event mô phỏng và hành vi trình duyệt khi nhập bằng automation; **không thay cho ma trận UniKey Telex/VNI thực của WEB1-03**. Bằng chứng nhập OS trước đây nằm ở M2/WEB0 và không được ghi lại thành chứng nhận SH.

MathJax 4.1.3 và NewCM 4.1.3 được pin và đóng gói local. SVG xuất chứa path, không cần font của máy nhận. Publish hiện bảo toàn toàn bộ Core/Application/Editor để serializer reflection hoạt động; log Worker còn cảnh báo IL2026. Bản publish đã được kiểm snapshot/tệp, chưa nhận là đã tối ưu trimming/AOT. [Microsoft về .NET worker](https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-on-webworkers?view=aspnetcore-10.0), [MathJax chuyển MathML](https://docs.mathjax.org/en/latest/web/convert.html), [font local](https://docs.mathjax.org/en/latest/web/hosting.html).

Font local không có nghĩa là trang tải lại offline đã hoạt động. Cache/PWA/update và website public thuộc WEB1-04. W0 giữ 4/6, phản hồi A/B đã để lại trong checklist; cổng Word G2/G3 không đổi.

## Thứ tự tiếp theo

1. WEB1-01: hoàn thiện trải nghiệm M2 trên editor mới, phím tắt/focus, preferences và thông báo.
2. WEB1-02: Copy SVG/MathML/OMML, capability rõ cho từng browser, fallback tải tệp và xử lý hủy/lỗi.
3. WEB1-03: nháp cục bộ, phục hồi reload, ma trận UniKey Telex/VNI thực và roundtrip file mở rộng.
4. WEB1-04: website preview online, offline reload và cập nhật đồng bộ assets/grammar, gói alpha.
5. E1: evaluator và đồ thị 2D; tiếp theo Hóa cơ bản → Lý cơ bản → hình học 2D.
