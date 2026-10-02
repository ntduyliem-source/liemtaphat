# M2 — Desktop alpha Windows

Ngày: **2026-09-12**. **M2 DONE trong phạm vi Desktop alpha Windows x64.** Đợt nghiệm thu bổ sung đã kiểm VNI thật, chọn/copy bằng bàn phím, đọc lại ảnh đã dán vào Word và sửa lỗi phát hiện được. M2-01/02/03 được đối chiếu với bằng chứng dưới đây; các cổng Word CW/G1/G2/G3 chưa thay đổi.

## Bản chạy

- [Locus.Desktop.exe](../../artifacts/releases/Locus-0.2.0-alpha-win-x64/Locus.Desktop.exe): bản đã giải nén, có thể mở trực tiếp.
- [Gói ZIP Windows x64](../../artifacts/releases/Locus-0.2.0-alpha-win-x64.zip): khoảng 65 MB, mang theo .NET runtime. Giải nén cả thư mục rồi chạy exe.
- [Hướng dẫn sử dụng](QUICKSTART.md), [thông tin gói và SHA-256](../../artifacts/m2/package.json).
- [Tổng hợp nghiệm thu](../../artifacts/m2/acceptance.json), [thao tác native bổ sung](../../artifacts/m2/native-followup.json).

Đây là alpha portable, chưa ký số hoặc có installer. Không khởi động Word, không thay tài liệu Word và không dùng HTTP server để dựng công thức.

## Đã triển khai

| Thành phần | Hành vi | Mã |
| --- | --- | --- |
| Desktop WPF | Ô nhập Unicode, Undo/Redo, preview, chọn vùng và chọn candidate; cửa sổ dùng độc lập | [MainWindow](../../src/Locus.Desktop/MainWindow.xaml), [code](../../src/Locus.Desktop/MainWindow.xaml.cs) |
| Phiên nhập | Hủy và bỏ kết quả chậm; xóa preview/copy ngay khi nguồn hoặc cặp dấu thay đổi; composition không bị ghi đè | [EditorSession](../../src/Locus.Desktop/EditorSession.cs) |
| Marker | Mặc định `lc[` / `]`; tùy chỉnh hai dấu, nhiều vùng trong văn xuôi, chọn vùng để xuất | Dùng `AnalysisEngine` M1 |
| Lựa chọn | Direct được chọn trước; repair-only cần chọn rõ; chọn repair không sửa ô nhập hoặc cấp quyền auto | Cùng Candidate ID qua session, scene và export |
| Vector | Dựng AST thành glyph outlines, dấu căn, vạch phân số và ngoặc; preview/SVG/PNG dùng cùng scene | [FormulaRenderer](../../src/Locus.Desktop/Rendering/FormulaRenderer.cs) |
| Xuất ảnh | SVG không cần font bên ngoài; PNG 1×/2×/3×, cỡ chữ 18/24/36/48 pt; trong suốt hoặc nền trắng; lưu qua hộp thoại | Scene có giới hạn kích thước/độ sâu, không cắt âm thầm |
| Clipboard | PNG + bitmap nền trắng; SVG MIME + SVG text; Unicode text và fallback text cho Paste Special; retry kiểm lại candidate/scene | [ClipboardService](../../src/Locus.Desktop/ClipboardService.cs) |
| Lưu file | Render hoàn tất trước khi ghi, file tạm cùng thư mục rồi thay file đích; lỗi file bị khóa giữ nguyên bản cũ | [ImageFileExporter](../../src/Locus.Desktop/ImageFileExporter.cs) |
| Cài đặt | Lưu cặp dấu/chế độ/cỡ xuất cục bộ; không lưu nội dung gõ | [Preferences](../../src/Locus.Desktop/Preferences.cs) |
| Gói alpha | Self-contained `win-x64`, kiểm tra trong tiến trình đóng gói dùng runtime ngay trong thư mục | [publish-desktop.ps1](../../tools/publish-desktop.ps1), [PackageSmoke](../../src/Locus.Desktop/PackageSmoke.cs) |

Không có parser thứ hai trong Desktop. Renderer mới chỉ dựng đúng cây đã chọn, không diễn giải lại nguồn. SVG mang Candidate ID và từng primitive mang Node ID; SVG/PNG là hình, không phải công thức Word native.

## Bằng chứng

`./tools/build.ps1` đã đạt **0 warning, 0 error** và chạy:

- **238/238 kiểm tra core**: [kết quả](../../artifacts/m1/verification.json).
- **40/40 nhóm kiểm tra Desktop**: [kết quả](../../artifacts/m2/verification.json), [runner](../../tests/Locus.Desktop.Tests/Program.cs). Có 23 nguồn tạo 26 cặp SVG/PNG; kiểm bảo toàn grouping, primitive/ID, không cắt nét, alpha, kích thước và giới hạn. Có test session, stale/config/close, chọn repair/vùng, composition, sửa giữa chuỗi/Undo/Redo, file bị khóa và debounce không thay kết quả đã chọn.
- **Cùng core DLL trên .NET 10 và .NET Framework 4.8 x86**: [13 nguồn / 43 dòng](../../artifacts/m1/runtime-check.json).
- **6 nguồn trong tiến trình gói**: [package smoke](../../artifacts/m2/package-smoke.json) xác nhận runtime, PresentationFramework và core được nạp từ thư mục portable, cùng SVG/PNG được tạo thành công. Đây không phải phép thử trên máy Windows sạch hoặc khi ngắt adapter mạng vật lý.

[Gallery SVG/PNG](../../artifacts/m2/gallery.html) đã được mở bằng trình duyệt. Các mẫu căn, lũy thừa, phân số và ngoặc lồng quan sát được thống nhất giữa SVG và PNG. [Bố cục 1120](../../artifacts/m2/desktop-1120.png) và [bố cục 850](../../artifacts/m2/desktop-850.png) được render từ WPF bằng test harness; khung nhỏ có cuộn từng pane. Đây là render nội dung ứng dụng, không phải ảnh chụp cửa sổ native.

### Thao tác trên bản portable thật

Computer Use đã đọc accessibility của cửa sổ `Locus — Công thức`, exe từ thư mục portable. [Biên bản quan sát](../../artifacts/m2/native-observations.json) tách thao tác thực khỏi test tổng hợp. Các bước sau được kiểm sau khi quyền điều khiển được trả lại:

| Thử | Kết quả quan sát | Giới hạn |
| --- | --- | --- |
| Nhập Unicode `x mũ 2` | Raw đúng, một vùng và preview sẵn sàng | Không suy ra mọi bộ gõ |
| Telex: từng phím `x`, Space, `m`, `u`, `x`, Space, `2` | `x mu` → `x mũ` → `x mũ 2`; preview và xuất sẵn sàng | UniKey 4.3 RC5 trên baseline hiện tại |
| VNI: từng phím `x`, Space, `m`, `u`, `4`, Space, `2` | `x mu` → `x mũ` → `x mũ 2`; direct được chọn, một vùng, preview và xuất sẵn sàng | Đã xác nhận InputMethod=1, Unicode; thử ban đầu `mu4` trước khi cấu hình được áp dụng không tính PASS |
| Backspace ở cuối | Thiếu toán hạng; Copy PNG disabled | Không coi đây là bằng chứng mọi vị trí sửa |
| Ctrl+Z | Nguồn trở lại `x mũ 2`, preview hợp lệ | Undo ô nhập Desktop; không phải Undo Word |
| VNI rồi Backspace / Ctrl+Z / Ctrl+Y | Nguồn thiếu toán hạng khóa Copy PNG; Undo khôi phục, Redo lặp lại đúng lần xóa | Thao tác phím thật, tách khỏi bài WPF tổng hợp |
| `x+1/2`, Alt+2, Ctrl+Shift+T | Chọn repair, nguồn vẫn `x+1/2`; đọc clipboard ngoài tiến trình được `\frac{x+1}{2}` | Preview/selection/export cùng candidate |
| Ctrl+Shift+C | Trạng thái báo đã sao chép PNG | Sau đó thử dán ở Paint |
| Ctrl+V trong Paint mới mở | Paint chuyển sang selection, Copy/Crop enabled sau dán | Xác minh Paint nhận ảnh; chưa lưu/chụp được pixels của canvas để đối chiếu thị giác |

Chụp native qua Computer Use vẫn lỗi `SetIsBorderRequired ... 0x80004002`; click cần geometry cũng lỗi. Ctrl+S đã mở đúng hộp lưu SVG, nhưng helper không xác minh được focus tên file; hộp được hủy bằng Alt+F4. Không báo hoàn thành Save As bằng phím trên hộp native. Phần ghi file thực tế qua chính `ImageFileExporter` dùng trong ứng dụng được kiểm độc lập, gồm đọc lại nội dung SVG/PNG và lỗi file đích bị khóa. Test WPF composition trong runner là sự kiện tổng hợp; Telex/VNI trong bảng là phím thật.

### Clipboard trong ứng dụng đích

[Runner Word](../../tools/m2/verify-word-clipboard.ps1) tạo tài liệu thử mới bằng Microsoft Word 16.0 x86, gọi đường copy của MainWindow và để tiến trình Word đọc clipboard hệ thống. **8/8 bài tích hợp đạt**; đường dẫn run cuối và từng kết quả nằm trong [acceptance.json](../../artifacts/m2/acceptance.json). Có đọc lại Unicode clipboard trong tiến trình khác, PNG direct, PNG repair, SVG import, SVG text, LaTeX, nguồn, MathML và OMML.

[Đối chiếu ảnh](../../tools/m2/verify-received-images.py) trích dữ liệu ảnh từ `WordOpenXML` của tài liệu đã dán: hai PNG khớp từng pixel trên nền trắng với scene đã chọn. SVG import tạo một hình đúng bố cục; dữ liệu Flat OPC trên baseline này chỉ có PNG, nên kiểm giữ nguyên vector được ghi **FAIL / giới hạn tương thích**, không gộp vào PASS của việc nhận ảnh. Đã xem ảnh SVG được Word raster hóa và so với ảnh xuất gốc.

Word nhận SVG/LaTeX/nguồn/OMML dưới dạng text qua Paste Special. Với MathML, Word tự chuyển thành equation ngay cả khi dùng Paste Special Text; cấu trúc OMML chuẩn hóa khớp candidate trong bài thử. Đây là hành vi nhập của Word, chưa phải connector quản lý nguồn/metadata/Undo của Locus. Các thử SaveAs2 ban đầu bị treo; run cuối đọc dữ liệu trong bộ nhớ rồi đóng tài liệu thử, **không** dùng kết quả này để kết luận save/reopen Word đạt.

### Lỗi đã sửa trong đợt nghiệm thu bổ sung

- Chuyển từ vùng direct sang vùng chỉ có repair phải xóa cả lựa chọn của phiên trước.
- Phân tích ngay phải hủy debounce đang đợi; nếu không, kết quả đã chọn có thể bị thay lại trong lúc clipboard retry.
- Bổ sung text fallback để Word nhận Paste Special; Unicode nguồn vẫn được cung cấp riêng.
- Lưu ảnh qua file tạm, bảo toàn file đích nếu ghi/thay thất bại.
- Bổ sung Ctrl+L, Alt+1/2/3, phím copy LaTeX/SVG và lưu SVG/PNG.

## Quyết định D-07 và tương thích clipboard

| Đầu ra | Cam kết alpha | Fallback / bằng chứng |
| --- | --- | --- |
| PNG | Định dạng clipboard PNG giữ alpha, kèm bitmap nền trắng | Paint nhận bitmap; Word nhận ảnh khớp pixels trên nền trắng. Không bảo đảm alpha ở mọi ứng dụng |
| SVG | `image/svg+xml` và text SVG; file chứa path độc lập với font | Trình duyệt mở file đạt; Word import hiển thị đúng nhưng Flat OPC chỉ có PNG. Giữ file SVG gốc khi cần vector; Word có thể dùng PNG |
| Nguồn | Phần replacement nguyên văn, gồm marker đã dùng | Test data contract và source span; văn xuôi ngoài vùng không bị copy lẫn |
| LaTeX / MathML / OMML | Unicode text của đúng candidate, kèm fallback text | Word có thể tự diễn giải MathML; LaTeX/OMML trong bài thử là text. Không cấp quyền auto hoặc metadata của Locus |

D-07 chốt định dạng và fallback cho alpha; ma trận ứng dụng đích vẫn mở rộng ở M6. Không mở WPS hoặc cổng Word chỉ vì có Copy OMML.

## Giới hạn và bước tiếp

Windows 10 x64 build 19045, UniKey 4.3 RC5 Telex/VNI và Word x86 là baseline đã thử. Chưa chứng nhận Windows 11, ARM64, high DPI đa màn hình hoặc mọi bộ gõ/ứng dụng đích. Renderer là layout tối thiểu cho grammar M1, chưa dùng toàn bộ thông số OpenType MATH. Bằng chứng hữu hạn không bảo đảm mọi công thức có bố cục tối ưu.

M2-01 đạt bằng editor/session, bài nhập phím thật và tests sửa/chọn. M2-02 đạt bằng cùng scene preview/export, clipboard ứng dụng đích và kiểm ghi file. M2-03 đạt bằng gói tự mang runtime và bộ tác vụ chung: nhập công thức đơn; soạn nhiều vùng trong câu bằng marker; chọn đề nghị sửa và lấy ảnh/text. Các tác vụ phục vụ cả ba nhóm người dùng, không phải chứng nhận pilot với người dùng bên ngoài (M6).

Không lưu source/history khi đóng; chỉ Undo trong phiên và cài đặt được lưu. Chưa có tab đồ thị, hình học, Lý/Hóa, auto-Space, Word connector, crash recovery hoặc migration metadata Word. Đóng gói lại bằng `./tools/publish-desktop.ps1`; build/test cần SDK trong `global.json`, bản ZIP không cần SDK.

Tiếp theo: các việc W0-01/02/03 cần đủ bằng chứng cho cổng CW, sau đó M3 chuyển vùng lựa chọn trong Word có xác nhận. Không tự mở M3 vì M2 đã có ứng dụng Desktop.

## Cơ sở kỹ thuật

WPF cung cấp các sự kiện bắt đầu/cập nhật/kết thúc text composition qua [TextCompositionManager](https://learn.microsoft.com/en-us/dotnet/api/system.windows.input.textcompositionmanager?view=windowsdesktop-10.0). Locus quan sát và trì hoãn phân tích, không đánh dấu handled hoặc viết lại ô nhập trong composition. WPF cũng cho phép [chuyển FormattedText thành Geometry](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/drawing-formatted-text); các glyph outlines này được tái sử dụng trong scene và SVG.
