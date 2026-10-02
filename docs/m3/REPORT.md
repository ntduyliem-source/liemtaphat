# M3 — Word chuyển có xác nhận

Cập nhật ngày 2026-09-13 theo giờ máy; lượt chốt bằng chứng ghi ngày 2026-09-14 UTC. **M3-01 đến M3-04 DONE cho bản alpha trên baseline dưới đây. G1 đạt cho chuyển thủ công có xác nhận.** G2/G3 chưa đạt, W0 vẫn 4/6 mục; phản hồi A/B đã hẹn được giữ ở [checklist W0](../w0/PENDING-ACCEPTANCE.md).

## Bản dùng được

- Word có tab **Locus**, lệnh **Chuyển vùng chọn**, **Mở công thức Locus** và **Cặp bọc**. Entry point `Locus.Word.Manual` độc lập với `Locus.Word.W0`; tab sản phẩm không có nút nghiên cứu A/B hoặc pipe probe.
- Chọn vùng → preview core thật → xác nhận → equation native kèm snapshot. Kết quả trực tiếp ở đầu; fx mở các phương án còn lại khi có, tối đa ba theo core. Repair có nhãn và nút xác nhận riêng.
- Preview và commit cùng candidate. Nếu dựng ảnh lỗi, bảng xóa candidate/ảnh cũ và khóa xác nhận. Không có ảnh thay thế cho equation Word.
- Viết tiếp ngoài equation; Undo/Redo; mở lại bộ kết quả đã lưu; restore nguyên văn nguồn/cặp dấu; detach giữ native. Lưu/mở DOCX giữ metadata, mở khi add-in ngắt vẫn sửa native được.
- Cặp dấu mặc định `lc[` và `]`, đổi và lưu theo tài khoản. Chuyển marker ở M3 vẫn cần chọn vùng và xác nhận.

[Gói Word 0.3.0 alpha x86](../../artifacts/releases/Locus-Word-0.3.0-alpha-x86.zip), [cài/gỡ và sử dụng](QUICKSTART.md), [bản ghi nghiệm thu](../../artifacts/m3/acceptance.json).

## Baseline và ma trận hỗ trợ

Windows 10 x64 build 19045, Microsoft Word desktop x86 16.0.14026.20302, .NET Framework 4.8; core `locus-core/0.1`, grammar `vi-math-m0-proposal-0.1`. Gói add-in chạy theo tài khoản hiện tại; không cần Desktop hoặc server phân tích đang chạy. Đây chưa phải chứng nhận Word x64, Mac, Word Online, WPS, máy sạch hoặc installer có chữ ký.

| Ngữ cảnh | Hành vi M3 và bằng chứng |
| --- | --- |
| Vùng một đoạn trong thân tài liệu, gồm Unicode tổ hợp và cặp bọc | Preview/chuyển/Undo/restore đạt; emoji trước vùng được kiểm bằng bàn phím trên gói |
| Văn xuôi, URL, vùng không thành một công thức hoàn chỉnh | Từ chối, không thay nguồn |
| Read-only, protection, Track Changes | Từ chối chuyển; không tự tắt chế độ của Word |
| Bảng, header/footer, field/equation/control có sẵn | Ngoài phạm vi chọn nguồn của alpha; guard từ chối. M3 thử bảng/header/control; guard dùng chung còn có các bài W0 |
| Nguồn/vùng/tài liệu/cửa sổ/cấu hình đổi trước hoặc trong xác nhận | Phiên hết hiệu lực, không áp dụng kết quả cũ |
| Find lấy focus sau xác nhận | Bài pending giữ nguồn, hủy phiên; có assertion editor mất focus trước khi kết luận |
| Native bị sửa, Tag hỏng/phiên bản mới hơn, ID trùng | Không restore/detach bằng dữ liệu không còn khớp |
| Tài liệu mở khi add-in ngắt | Native hiển thị/sửa được; chưa thử máy không từng cài Locus |

Vùng nguồn tối đa 4.096 UTF-16 code unit; parser, ảnh và snapshot có giới hạn riêng. Thân tài liệu alpha tối đa 500.000 vị trí Word; preview hết hạn sau hai phút. Alpha kiểm fingerprint văn bản thân tài liệu, identity/range/selection và cờ ghi; không theo dõi mọi thay đổi định dạng trên toàn bộ tài liệu.

## Cách giữ giao dịch đúng mục tiêu

`ManualSession` giữ document/window identity, live Word range, selection, raw source, bộ candidate, cấu hình và fingerprint của thân tài liệu. Word offsets được lấy từ Word, không cộng offset chuỗi .NET để suy ra vị trí thay thế. Chọn đúng toàn bộ một vùng phân tích mới được mở phiên.

Nút xác nhận gửi session ID, action và candidate ID đang được renderer hiển thị. Add-in kiểm focus của bảng/editor, chuyển focus về editor rồi chờ 350 ms để phát hiện đầu vào mới. Ngay trước giao dịch, nó kiểm lại session, editor `_WwG`, hook nhập, trạng thái composition/IMM và khoảng yên nhập. Trạng thái không chắc chắn hủy thao tác. Khoảng chờ này chỉ phục vụ xác nhận thủ công, không chứng minh bộ gõ đã hoàn tất để cho phép auto.

Native và metadata nằm trong một custom Undo record; kiểm sau chèn hoặc lỗi từng bước dẫn tới rollback. Restore đã bổ sung phát hiện lỗi ngay sau khi Word thay nguồn nhưng trước khi cờ `changed` được đặt. Việc đặt caret dùng cùng giao dịch Word đã được kiểm ở W0; di chuyển caret không tạo thêm một bước Undo.

`ManualPanel` dùng chung mã `FormulaRenderer` của Desktop để dựng ảnh từ candidate. Snapshot được mở lại từ dữ liệu đã lưu, không phân tích lại bằng grammar hiện hành. Timer chỉ kiểm khi có phiên preview; M3 không có quan sát văn bản thụ động, auto-Space hoặc auto-marker. Nguồn phiên bị bỏ khi đóng/hủy; chỉ cặp bọc được lưu trong tệp cài đặt.

## Kết quả kiểm

| Bộ kiểm | Kết quả | Artifact |
| --- | --- | --- |
| M3 trong Word thật | 38 PASS, 0 FAIL | [manual-final](../../artifacts/m3/manual-final/report.json) |
| Bảng preview: ảnh theo candidate, kích thước thường/nhỏ nhất, repair, lỗi render | 7 PASS, 0 FAIL; 8 ảnh phần mềm | [panel-final](../../artifacts/m3/panel-final/report.json) |
| Bàn phím trên bản đóng gói, kèm assertion COM chỉ đọc | 10 PASS, 0 FAIL | [native verification](../../artifacts/m3/native/verification.json) |
| DLL trong gói: chuyển/Undo/Redo/restore cho bảy nguồn | 7 PASS, 0 FAIL | [package smoke](../../artifacts/m3/package-smoke/report.json) |
| ZIP/hash, cài/cài lại/gỡ, đăng ký add-in khác | PASS | [package check](../../artifacts/m3/package-check.json) |
| Giao dịch dùng chung, fault injection, metadata/clipboard/context | 37 PASS, 0 FAIL, 1 OBSERVED | [adapter](../../artifacts/w0/adapter-20260914T015837775Z/report.json) |
| Vòng đời connector nghiên cứu dùng chung | 7 PASS, 0 FAIL | [lifecycle](../../artifacts/w0/adapter-20260914T020016765Z/report.json) |
| Desktop–Word W0 hai thứ tự mở/reconnect | 2 PASS, 0 FAIL | [desktop lifecycle](../../artifacts/w0/desktop-lifecycle-20260914T020028871Z/report.json) |
| Core và Desktop | 238/238 và 40/40 | [core](../../artifacts/m3/regression/core.json), [Desktop](../../artifacts/m3/regression/desktop.json) |
| Cùng DLL core trên .NET 10 và .NET Framework 4.8 x86 | 13 nguồn cho kết quả trùng | [runtime comparison](../../artifacts/m3/regression/runtimes.json) |

Build Release hai solution đạt, không warning/error. Các hàng là các loại bằng chứng khác nhau; không cộng chúng thành số kịch bản người dùng độc lập. Dòng OBSERVED là nghiên cứu nhập nối, không phải một tính năng được nghiệm thu. Bản ghi W0 cũ và hash của nó được giữ nguyên; hồi quy mới được liên kết riêng trong nghiệm thu M3.

Luồng bàn phím dùng nguồn NFD `x mũ 2` với `👩‍🏫 é` đứng trước: mở preview bằng API sản phẩm → Alt+C xác nhận → gõ Unicode literal ` tiếp tục` → Ctrl+Z phần gõ → Ctrl+Z lần chuyển → Ctrl+Y → Right vào equation → F10, L, C, M mở snapshot → Alt+K restore. Kiểm thêm riêng F10, L, C, C mở preview và Esc hủy. Văn bản ngoài vùng, raw NFD, candidate và metadata khớp. Native Redo giữ nội dung/metadata nhưng caret có thể ở đầu equation; cần đặt vào trong trước khi mở lại.

Các lượt `manual-01` đến `manual-04` giữ lỗi lịch sử: thiếu foreground của bộ thử, cast mảng JSON của harness và tên lệnh Find không hợp lệ. Bộ thử dùng `NavigationPaneFind` theo [danh sách control chính thức của Microsoft](https://github.com/OfficeDev/office-fluent-ui-command-identifiers/blob/main/Office%202013/wordcontrols.xlsx); `ExecuteMso` từ chối ID không hợp lệ theo [tài liệu Microsoft](https://learn.microsoft.com/en-us/office/vba/api/office.commandbars.executemso). Lượt cuối 38/38 đã chạy với guard focus nguyên vẹn. Một lượt package bị gián đoạn do không đưa Word vào foreground trong 55 giây đã được chạy lại, không bỏ qua guard để lấy PASS.

## Giới hạn còn mở

- Công cụ Windows capture báo `SetIsBorderRequired ... 0x80004002`; click báo `coordinate input geometry is unavailable`. Hình `panel-final` là `Control.DrawToBitmap` của control riêng, không phải ảnh chụp Word. Đã xem ảnh preview ở hai kích thước và thử bàn phím; pointer click, đổi DPI/màn hình thực chưa nghiệm thu.
- Literal Unicode của lượt M3 không phải một lượt Telex/VNI mới. Bằng chứng IME W0 vẫn được giữ riêng; chưa mở G2/G3 từ kết quả thủ công này.
- Đổi candidate của equation đang quản lý và sửa nguồn qua Desktop thuộc M4. Tab kết nối Word trong Desktop M2 vẫn dành cho W0; M3 dùng Ribbon Word. Installer hợp nhất và pilot môi trường rộng thuộc M6.
- Gói hiện tại dùng chung assembly chứa lớp nghiên cứu, nhưng script chỉ đăng ký entry point Manual. Không đăng ký hay bật lớp W0 từ gói này. Tách assembly để phân phối rộng hơn có thể làm ở M6.
- Chưa có Web độc lập, đồ thị, hình học, Lý/Hóa hoặc các chế độ Word tự động trong bản này.

Các giới hạn capture/click và phạm vi môi trường được công bố cùng alpha; G1 ở đây chỉ cho luồng thủ công trên baseline đã kiểm, không phải nghiệm thu W0-05/06 hay mọi tính năng Word.

## Chạy lại và bước tiếp

Đóng Word rồi chạy `./tools/m3/verify.ps1`. Nếu đã cài gói ở thư mục khác với source, gỡ bản đó bằng script của chính gói trước khi kiểm từ source. Bộ thử không tự đóng Word của người dùng. Khi cần, kích hoạt cửa sổ **Locus M3 verification** trong 55 giây; không thay đổi guard.

Đóng gói bằng `./tools/m3/package.ps1`; kiểm vòng cài/gỡ bằng `./tools/m3/verify-package.ps1 -KeepInstalled` khi chưa đăng ký Manual. Kịch bản này chạy từ DLL trong gói, kiểm hash ZIP và so đăng ký các add-in khác trước/sau. Bản bàn phím có fixture `start-demo.ps1`, bản ghi `demo-state.ps1` và assertion `verify-native.ps1`; đọc lại artifact không thay thế việc thực hiện bàn phím lần mới.

**Task tiếp theo: WEB0-01**, đưa chính core C# vào browser chạy cục bộ qua WebAssembly. WEB0-02 kiểm cùng kết quả; WEB0-03 thử editor chung trong browser/WPF trước khi chốt công nghệ. Chưa bắt đầu các task WEB0 trong đợt M3 này. [Các bước cụ thể](../NEXT-STEPS.md).
