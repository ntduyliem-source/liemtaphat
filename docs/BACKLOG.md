# Backlog thực hiện Locus

Cập nhật 2026-09-30. **G đạt 14/14 task alpha local.** Desktop đã kiểm chuột/phím, kéo/Undo, lưu/mở, Telex/VNI, tray và Copy sang Microsoft Word. D-CLIP-01 đã đạt; D-FILE/ma trận IME và WD1 highlight/nhiều DPI còn mở. SC1 giữ 8/10, W0 4/6; không lấy tỷ lệ G làm tiến độ toàn dự án. [Biên bản và giới hạn](host-review/20260930.md), [roadmap](ROADMAP.md).

Các task đã DONE giữ nguyên ID và bằng chứng. UX1/DOC1/BAL1/WD1/WEB2 là phần mở rộng mới; viết kế hoạch không tính là hoàn thành tính năng. Không cộng số task mới vào mẫu số SC1 hoặc lấy % SC1 làm % toàn dự án.

**Chốt G, 2026-09-30 — Codex:** Web local và Desktop x64 portable đã hoàn tất phạm vi alpha. Cập nhật theo người dùng: bỏ fx khỏi ô kết quả Web/Desktop; chọn công thức để mở Chi tiết bên ngoài. [Báo cáo G](phase-g/REPORT.md), [gói hiện tại](../artifacts/phase-g/packages.json). H/WEB2 chưa triển khai.

**WEB2-01, 2026-09-24 — Codex:** đã chốt hợp đồng lượt dùng cho pilot: mốc ngày giờ Việt Nam, lượt theo vùng công thức/tài liệu scene khi xuất lần đầu, thao tác miễn phí, đoạn nhiều vùng, Guest→Free, quyền riêng tư và giao dịch idempotent. [Hợp đồng và ca nghiệm thu](web2/QUOTA-CONTRACT.md). Đây là đầu ra thiết kế, chưa có quota/account chạy trên Web. Giá, đăng nhập sản xuất, thanh toán và hosting được giữ ở WEB2-04; WEB2-02 là bước triển khai kế tiếp.

## 1. Quy tắc trạng thái

- `READY`: đủ đầu vào để bắt đầu.
- `TODO`: chưa bắt đầu, cần kiểm tra các phụ thuộc ghi trong bảng.
- `DOING`: đang làm, phải ghi người phụ trách và kết quả đang hướng tới.
- `REVIEW`: có đầu ra và kiểm tra trong phạm vi, chờ review được yêu cầu; chưa tính DONE.
- `BLOCKED`: có trở ngại cụ thể; ghi điều kiện để tiếp tục.
- `PAUSED`: đã làm một phần, tạm dừng theo ưu tiên/người dùng; giữ bằng chứng và phần còn thiếu, không tính DONE.
- `DONE`: tiêu chí đạt và có bằng chứng được liên kết.

Trước khi chuyển task sang `DOING`, ghi người phụ trách, phạm vi và ước lượng sau khi đọc đầu vào. Không gán thời gian giả khi chưa có dữ liệu. Baseline kế hoạch là một luồng triển khai chính; READY không có nghĩa phải mở nhiều task cùng lúc. W0-05/06 đang chờ phần nghiệm thu được giữ nguyên bằng chứng và không tính là hai luồng lập trình đang hoạt động.

Mỗi task `DONE` cần đường dẫn đến artifact/mã và kết quả nghiệm thu. Các cột bằng chứng đang để `—` có nghĩa là chưa có, không có nghĩa đã đạt.

### Hàng đợi lấy việc từ baseline

1. **UX1-01 — DONE**: thiết kế được dùng làm đầu vào triển khai theo yêu cầu bước tiếp theo.
2. **UX1-02 + DOC1-01/02 — DONE trong phạm vi B**: editor chung, model/file v5/v6, đoạn inline, selection và fx; OS/rich export riêng ở D.
3. **BAL1-01…04 — DONE trong phạm vi C**: snapshot v7, hủy hệ số, nút đơn/tuần tự, hai batch/hoàn tác, auto và bỏ qua. [Bằng chứng](bal1/REPORT.md).
4. **DOC1-03 + UX1-03 → SC1-08**: file/copy nguyên đoạn, tray/bung, gom kiểm OS/trợ năng trên UI mới.
5. **SC1-09 → SC1-10**: Hóa thông minh Word thủ công và gói SC1; không thêm WD1 vào điều kiện đóng SC1.
6. **WD1-01…05**: quét Word cũ, highlight/fx, chuyển từng vùng/tất cả, giữ text và nghiệm thu.
7. **E1-01 → E1-04 → E1-02 → E1-06 → E1-03 → E1-05**: đồ thị và thanh trượt.
8. **E2A-01 → E2A-02 → E2A-04 → E2A-03 → E2A-05 → E2B-01…03**: hình 2D rồi 3D.
9. **WEB2-01…04**: chốt quota/tài khoản, backend/ledger, pilot rồi thương mại/online theo quyết định riêng.
10. **M6** theo capability phát hành; **M4/M5A/M5B/SC1-11** lấy khi đủ các cổng riêng.

Ưu tiên không tạo phụ thuộc giả: nếu Word/OS đang chờ, giữ task thiếu mở và chuyển E1 hoặc phần độc lập đủ đầu vào; không lặp yêu cầu người dùng thử W0. UI/nguồn/Undo/batch phải kiểm đúng cụm trước bàn giao, không chạy lại bộ lớn sau từng thay đổi nhỏ. B/C có báo cáo theo từng build; publish Desktop chưa tính là kiểm OS.

## 2. Đợt thực hiện đầu tiên

Đợt M0 đã thực hiện bởi Codex với các nhánh corpus/hợp đồng, UX và Word probe; thao tác COM chạy tuần tự. `DONE` dưới đây là đầu ra nghiên cứu có kết luận, bao gồm cả FAIL/UNTESTED và fallback. Xem [báo cáo M0](m0/REPORT.md) và các việc Word còn mở ngay sau bảng.

| ID | Trạng thái | Công việc và đầu ra | Phụ thuộc | Điều kiện hoàn thành | Bằng chứng |
| --- | --- | --- | --- | --- | --- |
| M0-01 | DONE | Corpus nguồn/vùng/AST/candidate/policy cho cả ba nhóm; grammar đầu | PRODUCT.md | Kỳ vọng rõ và pending riêng | [117 case, validator và giới hạn](../corpus/m0/README.md); [grammar](m0/GRAMMAR.md) |
| M0-02 | DONE | Ma trận máy thử, Word/bitness/bộ gõ và giới hạn | S-01 đến S-07 | Baseline thực tế, tổ hợp chưa kiểm tra ghi riêng | [ENVIRONMENT](m0/ENVIRONMENT.md), [inventory](../artifacts/m0/environment.json) |
| M0-03 | DONE | Native từ candidate cố định qua COM; Office.js đối chiếu tài liệu | M0-01/02; S-01 | Native chỉnh sửa qua API, đúng cấu trúc, ngoài vùng và save/reopen | [100 assertion native đạt](../fixtures/m0/word/README.md); UI keyboard còn UNTESTED |
| M0-04 | DONE | Nghiên cứu Undo/metadata/native edit/restore/copy/fault | M0-03; S-02/04 | Có kết luận theo từng cơ chế; chưa đủ mở CW thì ghi blocker | [Word evidence](../fixtures/m0/word/README.md): Tag restore/Undo đạt, CustomXML/selection/copy có FAIL; W0-01/03 tiếp tục |
| M0-05 | DONE | Guard tổng hợp + Word stale, quan sát startup focus và đánh giá giới hạn UI | M0-02; S-03/06 | Ghi rõ điều quan sát được, chưa xác minh được và cách hủy | [REPORT](m0/REPORT.md), [ENVIRONMENT](m0/ENVIRONMENT.md); IME/editor focus/fx/lifecycle chưa đạt, W0-02/04/05 tiếp tục |
| M0-06 | DONE | Hai prototype Space; bổ sung marker tùy chỉnh theo user | M0-01; S-05, D-05 | Có demo/kết quả; D-01 được giữ mở cùng cổng M5B | [Space](m0/SPACE-EXPERIMENT.md), [marker](m0/MARKER-EXPERIMENT.md); 39 test UX đạt |
| M0-07 | DONE | Chọn core C# chung, hướng Word, IPC và snapshot | S-01/02/03/04/07 có kết luận | Quyết định có bằng chứng, không nhân đôi parser; Word giữ cổng chưa đạt | [DECISIONS](m0/DECISIONS.md), [shared/IPC](../prototypes/m0-shared-core/README.md) |
| M0-08 | DONE | Trạng thái cổng và thứ tự/ước lượng M1–M3 | M0-01 đến M0-07 | C0 đạt, CW chưa đạt có việc cụ thể; M1 đủ đầu vào | [REPORT](m0/REPORT.md), [NEXT-STEPS](m0/NEXT-STEPS.md) |

M0-06 có thể kết thúc bằng kết luận “chưa chốt UX Space” nếu có bằng chứng thử nghiệm, danh sách câu hỏi và cổng chặn M5B rõ ràng. Hoàn thành việc nghiên cứu không có nghĩa hoàn thành tính năng.

Có thể ghi C0 đạt ngay khi M0-01 và phần hợp đồng core đã đủ rõ, trước khi M0-08 tổng kết nghiên cứu. M1–M2 không cần đợi cổng CW của Word.

### Các việc Word còn thiếu sau nghiên cứu

Tiêu chí gốc và fallback tại [NEXT-STEPS](m0/NEXT-STEPS.md). Codex đã triển khai đợt W0 trong Word x86 thực; bảng dưới ghi kết quả hiện tại. Badge và hai biến thể giữ phiên đã có mã/bằng chứng. Công cụ gõ được phím nhưng capture/click còn lỗi; W0-05 cần quan sát hình/DPI, W0-06 cần người thử và D-01. Người dùng đang bận; [checklist chờ nghiệm thu](w0/PENDING-ACCEPTANCE.md) ghi các bước để làm khi rảnh.

| ID | Trạng thái | Công việc | Phụ thuộc/cổng | Bằng chứng hiện tại |
| --- | --- | --- | --- | --- |
| W0-01 | DONE | Codex: native + snapshot, một Undo khôi phục nguồn/selection, rollback; con trỏ sau chuyển thủ công | CW/G1; auto-Space riêng ở W0-06 | [Bộ adapter](w0/REPORT.md), [30 ca nhập tiếp và equation thứ hai](w0/CONTINUATION.md), phím VNI ngoài equation |
| W0-02 | DONE | Codex: quan sát Telex/VNI, sửa dấu, Backspace/Undo/Redo; từ chối focus ngoài editor | CW đủ cho thủ công; G2 chưa chứng minh hoàn tất nhập để auto | [8 nhóm phím/focus/clipboard](../artifacts/w0/native/followup-verification.json), Telex/Find đợt trước; Ribbon/Font/UniKey giữ nguyên nguồn |
| W0-03 | DONE | Codex: metadata/version/payload/native drift, clipboard/ID trùng, phạm vi context công bố | CW/G1; ma trận môi trường rộng tiếp tục M3/M6 | [API và Ctrl+C/Ctrl+V trong/cross document](w0/REPORT.md); nguồn/candidate đúng, ID trùng từ chối, FormattedText mất Tag |
| W0-04 | DONE | Codex: build/cài/gỡ COM add-in và kiểm lifecycle/reconnect/nhiều tài liệu | Research connector .NET Framework 4.8 x86; production IPC ở M3/M6 | [7 bài add-in và 2 kịch bản Desktop–Word đạt](w0/REPORT.md); không tự bật add-in đã tắt |
| W0-05 | DOING | Codex: badge gắn document/window/source, clipping/zoom/scroll và hết hạn; chờ nghiệm thu trực quan | W0-02/04; M4/G2; chưa ước lượng thời gian chờ người thử | [Hai nhóm badge đạt trên baseline và layout tổng hợp](w0/REPORT.md); DPI thực 96, nhiều DPI/màn hình và click/quan sát còn chờ |
| W0-06 | DOING | Codex: A giữ nguồn và B cập nhật native cùng phiên đã chạy; chờ người thử/chốt D-01 | G3/M5B; không tự chọn phím kết thúc sản phẩm | [Native Space và lỗi đã sửa](w0/SPACE-NATIVE.md), [6 nhóm Windows input](../artifacts/w0/native/space-verification.json), [checklist chờ lúc rảnh](w0/PENDING-ACCEPTANCE.md) |

## 3. Backlog trục chính

M1/M2 giữ bằng chứng lịch sử. M3 trở đi là task triển khai; phạm vi từng bước và demo ở NEXT-STEPS.md. WEB0/SH/WEB1/E1/E3/E2A là các mốc chính ở phần 4, không còn mặc định chờ M6.

| ID | Trạng thái | Đầu ra và tiêu chí riêng | Phụ thuộc | Bằng chứng |
| --- | --- | --- | --- | --- |
| M1-01 | DONE | Khởi tạo repo/build theo stack đã chọn; có lệnh chạy/kiểm tra và quy tắc dependency | C0 đạt; ADR-001/002 | [Build](../tools/build.ps1), [dependency/API](m1/CORE-USAGE.md), [cùng DLL hai runtime](../artifacts/m1/runtime-check.json) |
| M1-02 | DONE | Nguồn bất biến và ánh xạ chuẩn hóa; các case Unicode/offset bắt buộc đạt | M1-01, M0-01 | [Model](../src/Locus.Core/Model.cs), [source map](../src/Locus.Core/SourceMapping.cs), [kiểm chứng](m1/REPORT.md) |
| M1-03 | DONE | Nhận diện vùng và marker lc[...] mặc định/tùy chỉnh; kiểm tra văn xuôi, URL/email/path và dấu câu ngoài vùng; chưa tự ghi Word | M1-02, D-05 | [Detection](../src/Locus.Core/Detection/AnalysisEngine.cs), [corpus và ca ngoài corpus](m1/REPORT.md) |
| M1-04 | DONE | Parser/MathDocument/candidate/chẩn đoán; grammar và quy tắc diễn giải/sửa rõ | M1-02, D-02 | [Parser](../src/Locus.Core/Parsing/FormulaParser.cs), [errata corpus](../corpus/m1/ERRATA.md), [nghiệm thu](m1/REPORT.md) |
| M1-05 | DONE | Preview MathML và bộ xuất OMML/LaTeX/nguồn cùng dùng candidate; snapshot lưu/mở không reparse | M1-04 | [Export](../src/Locus.Core/Export/CandidateExporter.cs), [serializer](../src/Locus.Core/Serialization/CandidateSetSerializer.cs), [demo](../src/Locus.Cli/README.md); SVG/PNG ở M2 |
| M1-06 | DONE | Corpus chạy tự động, kiểm tra bất biến, nguồn lỗi/dài và hủy kết quả hết hạn | M1-03 đến M1-05 | [238/238 nhóm](../artifacts/m1/verification.json), [runner](../tests/Locus.Core.Tests/Program.cs), [phạm vi core, không đóng guard Word](m1/REPORT.md) |
| M2-01 | DONE | Codex: Desktop WPF nhập/preview/chọn/sửa; composition và kết quả cũ không làm mất nguồn | M1, hợp đồng Desktop | [40 nhóm tests, Telex/VNI thật, Backspace/Undo/Redo và phím chọn repair](m2/REPORT.md) |
| M2-02 | DONE | Copy/export SVG/PNG/văn bản; file đích được giữ nếu ghi lỗi; chốt D-07 | M2-01, M1-05 | [26 cặp SVG/PNG, clipboard Word 8 bài và PNG khớp pixels](m2/REPORT.md); SVG import Word có giới hạn raster; Save As native chưa xác minh focus tên file |
| M2-03 | DONE | Gói alpha portable độc lập, cùng tác vụ nhập/chọn/xuất cho cả ba nhóm | M2-01, M2-02 | [ZIP/runtime smoke](m2/REPORT.md), [hướng dẫn](m2/QUICKSTART.md); chưa là pilot người dùng hoặc chứng nhận máy sạch |
| M3-01 | DONE | Chọn vùng → preview core thật; phiên document/window/range/raw/candidate, hủy và invalidation trên baseline Word x86 | M1; CW đã mở trên baseline | [38 nhóm Word và 7 nhóm preview](m3/REPORT.md), [ManualSession](../src/Locus.Word/ManualSession.cs) |
| M3-02 | DONE | Xác nhận đúng candidate; revalidate target/source/config/focus/IME trên UI thread; native+metadata trong một Undo, rollback và caret viết tiếp ngoài equation | M3-01; W0-01/02/03 | [Native bàn phím và hồi quy fault](m3/REPORT.md), [ManualConnect](../src/Locus.Word/ManualConnect.cs) |
| M3-03 | DONE | Mở snapshot cũ, restore nguyên nguồn/cặp bọc, detach; save/reopen, native edit, metadata lỗi/ID trùng và mở khi add-in ngắt | M3-02 | [manual-final](../artifacts/m3/manual-final/report.json), [NFD/emoji và restore bằng phím](../artifacts/m3/native/verification.json) |
| M3-04 | DONE | Entry point riêng; gói alpha cài/gỡ; Word thật qua bàn phím và ma trận baseline; G1 thủ công đạt, giới hạn capture/click/DPI công bố | M3-03; không chờ W0-05/06 | [REPORT](m3/REPORT.md), [QUICKSTART](m3/QUICKSTART.md), [acceptance](../artifacts/m3/acceptance.json), [gói kiểm đạt](../artifacts/m3/package-check.json) |
| M4-01 | TODO | Theo dõi vùng nhập, lọc sự kiện do Locus tạo, `fx` đúng vị trí; không loop hoặc gợi ý giữa composition; nghiệm thu focus/IME/vị trí sản phẩm | M3-04; W0-05; S-03/S-06 và phần G2 liên quan | — |
| M4-02 | TODO | Kết quả trực tiếp trước, bấm `fx` mới mở tối đa 3 kết quả; preview và commit cùng candidate | M4-01 | — |
| M4-03 | TODO | Chỉnh sửa qua Desktop và trở lại đúng equation; IPC phiên/version/candidate hết hạn bị từ chối; mở snapshot cũ, xử lý native drift, không ghi đè từ metadata cũ | M2, M4-02, D-04; dùng editor chung SH khi đã có | — |
| M5A-01 | TODO | Tích hợp parser marker M1 vào observer Word; cặp mặc định/tùy chỉnh, dán/lồng/escape theo D-05; chưa đóng giữ nguyên; không viết parser khác | M4-03, D-05 | — |
| M5A-02 | TODO | Auto đủ G1/G2, một candidate không repair/cảnh báo; config/stale/IME/focus hủy pending; fx restore nguyên nguồn và không kích hoạt vòng chuyển lại | M5A-01; G1/G2 đạt cho phạm vi này | — |
| M5B-01 | TODO | Tổng hợp phản hồi A/B và thử người dùng, chốt nhập nối/kết thúc, cập nhật D-01; không lấy việc người dùng bận làm lựa chọn | M4-03; W0-06 có kết quả người thử; M0-06 | — |
| M5B-02 | TODO | Auto-Space theo UX đã chốt; nhập nhanh/sửa/Undo/IME/stale không mất ký tự; `fx` sau chuyển còn truy cập được | M5B-01, M5A-02 | — |
| M6-01 | TODO | Chốt capability từng bản, gói Desktop/connector/Web, cài/gỡ/cập nhật/rollback và migration/nháp; nhánh chưa đưa vào giữ mở | Các mốc đã chọn cho bản; Word thủ công cần M3/SC1-09/WD1 tương ứng, auto riêng M4/M5 | — |
| M6-02 | TODO | Đo tải web/khởi động, p50/p95 core/preview/Word, lấy mẫu đồ thị và kéo hình; giới hạn nguồn/scene, hủy việc cũ trên baseline công bố | M6-01 | — |
| M6-03 | TODO | Pilot tác vụ của cả ba nhóm trên từng host/miền; sửa lỗi; tài liệu cú pháp, file, offline, hỗ trợ và khôi phục | M6-01, M6-02 | — |
| M6-04 | TODO | Review phát hành theo capability Web/Desktop/Word; G1/G2 và phạm vi miền đủ bằng chứng; Space/3D có trạng thái riêng; cách cập nhật và giới hạn công khai | M6-03; M5B/E2B nếu đưa vào phát hành | — |

Nếu một nhánh Word chờ G1/G2/G3 hoặc người thử, tiếp tục các task Web/editor/miền đủ đầu vào. Các bản alpha độc lập có cổng riêng; không gộp chúng vào trạng thái nghiệm thu Word.

## 4. Web, editor và các miền trong kế hoạch chính

### WEB0 — Core/browser và quyết định công nghệ

| ID | Trạng thái | Đầu ra và điều kiện hoàn thành | Phụ thuộc | Bằng chứng |
| --- | --- | --- | --- | --- |
| WEB0-01 | DONE | Host WASM dùng đúng core C#, publish Release tĩnh; Unicode/hash/serializer qua bản publish | M1, M2; D-09 | [Compatibility](web/COMPATIBILITY.md), [receipt](../artifacts/web/compatibility.json) |
| WEB0-02 | DONE | 116 nguồn parity và 238 contracts mỗi host; đo tải/khởi động; Telex OS trên WASM WebView2; composition giả lập và offline sau tải được ghi riêng | WEB0-01 | [Bằng chứng/giới hạn bộ gõ, timer và payload](web/COMPATIBILITY.md); ma trận phát hành Telex/VNI ở WEB1-03 |
| WEB0-03 | DONE | Một editor/Scene dùng chung browser/WPF; kéo/Undo/Esc/SVG/reload; thử JSXGraph local, chốt Blazor WASM + WPF Hybrid | WEB0-02 | [Architecture](web/ARCHITECTURE.md), [gói thử](../artifacts/web/package.json), 17 nhóm UI WASM/16 Hybrid PASS |

### SH — Logic phiên, tài liệu và editor dùng chung

| ID | Trạng thái | Đầu ra và điều kiện hoàn thành | Phụ thuộc | Bằng chứng |
| --- | --- | --- | --- | --- |
| SH-01 | DONE | Session/history/composition/lease chung; adapter host; Web worker có deadline/cancel/restart, nguồn tối đa 4.096 | WEB0-03 | [32 nhóm Application và 104 worker parity/host](sh/REPORT.md) |
| SH-02 | DONE | Envelope version/kind/ID/revision, Formula/Plot/Geometry riêng; file hỏng/mới hơn giữ nguyên, không parse lại snapshot | SH-01 | [Format](sh/DOCUMENT-FORMAT.md), [11 native integration](../artifacts/sh/native-integration.json) |
| SH-03 | DONE | SVG/PNG chung, font NewCM local; 7 mẫu giống SVG trên 3 host và đối chiếu M2; bitmap/PNG Windows kiểm thực | SH-02; M2-02 | [Renderer comparison](../artifacts/sh/renderer-comparison.html), [clipboard](../artifacts/sh/clipboard-os.json) |
| SH-04 | DONE | Cùng editor trong Web và tab Desktop SH; giữ phiên khi đóng/mở tab, Undo/lưu-mở/xuất và bỏ tác vụ cũ | SH-03 | [14/14 UI mỗi host, phạm vi chuyển tiếp và gói](sh/REPORT.md), [cách chạy](sh/QUICKSTART.md) |

### WEB1 — Web công thức alpha

| ID | Trạng thái | Đầu ra và điều kiện hoàn thành | Phụ thuộc | Bằng chứng |
| --- | --- | --- | --- | --- |
| WEB1-01 | DONE | Phím tắt, focus, preferences, repair/marker/Undo/lưu-mở trên editor chung; 14 nhóm hồi quy mỗi host và kiểm WEB1 mới đạt | SH-04 đã đạt | [WEB1 report](web1/REPORT.md) |
| WEB1-02 | DONE | Copy SVG/PNG/text theo capability, tải dự phòng; picker hủy/lỗi/stale giữ nguồn; clipboard PNG/SVG Windows đọc lại đúng bytes | WEB1-01 | [WEB1 report](web1/REPORT.md), [export safety](../artifacts/web1/export-safety.json) |
| WEB1-03 | DONE | Nháp/reload/tách tab/file hai chiều/restart Desktop đạt; Telex/VNI OS, sửa dấu, Undo/Redo và xuất/reload trên đúng WASM trong Windows WebView2 đạt | WEB1-02; baseline OS theo IME.md | [Báo cáo](web1/REPORT.md), [19 kiểm tra bằng chứng IME](../artifacts/web1/ime/status.json) |
| WEB1-04 | DONE | Bàn giao local theo yêu cầu ngày 2026-09-14: bộ mở/dừng, gói hash, 13 bài từ ZIP và 5 bài update đạt; IME nghiệm thu riêng ở WEB1-03 | WEB1-01/02; scope local đã được người dùng chọn, online hoãn | [Local validation](../artifacts/web1/local-validation.json), [phạm vi](web1/delivery-scope.json), [hướng dẫn](web1/QUICKSTART.md) |

### SC1 — Cặp bọc theo môn và Hóa thông minh

[Kế hoạch chi tiết](sc1/PLAN.md), [corpus kỳ vọng ban đầu](sc1/ACCEPTANCE.md). [SC1-01/02 đã đạt local](sc1/MARKERS-REPORT.md); 109 kiểm core/application, Chromium/Firefox 19 nhóm mỗi host, Desktop 17 nhóm; parity 603 mỗi browser. SC1-01…10 là đợt alpha local chính; SC1-11 là nhánh Word nâng cao và không giữ E1 để chờ W0.

| ID | Trạng thái | Đầu ra và điều kiện hoàn thành | Phụ thuộc | Bằng chứng |
| --- | --- | --- | --- | --- |
| SC1-01 | DONE | Hợp đồng/corpus về bốn cặp, override miền, grammar, draft/proposal, phím và migration; chốt các delta so E3 trước code | E3 đạt; kế hoạch SC1 | [Hợp đồng](sc1/CONTRACT.md), [nghiệm thu](sc1/MARKERS-REPORT.md) |
| SC1-02 | DONE | Cặp chung/riêng và tùy chỉnh; scanner nhiều vùng/ngoặc Hóa, all-off, migration giữ cặp cũ và từ chối cấu hình xung đột | SC1-01 | [Báo cáo cặp bọc](sc1/MARKERS-REPORT.md) |
| SC1-03 | DONE | Model draft/proposal/nhận, nguồn gốc phần sinh mới, serialization/wire và snapshot cũ nguyên nghĩa | SC1-01/02 | [Báo cáo](sc1/BALANCE-REPORT.md) |
| SC1-04 | DONE | Chữ thường đơn nghĩa, Co/CO và h20/h2o; `=` đúng ngữ cảnh Hóa; bản nháp thiếu vế không thành candidate hoàn chỉnh | SC1-03 | [Báo cáo](sc1/BALANCE-REPORT.md) |
| SC1-05 | DONE | Cân bằng chính xác nguyên tố/điện tích; nghiệm dương tối giản khi duy nhất; vô nghiệm/nhiều nghiệm/giới hạn, kiểm độc lập và parity | SC1-04 | [Báo cáo](sc1/BALANCE-REPORT.md) |
| SC1-06 | DONE | Kho quy tắc có nguồn/điều kiện/ngoại lệ; suy sản phẩm rồi cân bằng; bài kiểm giữ riêng và phân biệt chưa hỗ trợ với không phản ứng | SC1-05 | [Báo cáo kho](sc1/CATALOG-REPORT.md) |
| SC1-07 | DONE | Ghost/fx/Enter trên Web/Desktop, preview cả hệ số vế trái, Undo/restore, nháp/file/xuất và stale | SC1-03/05/06 | [Báo cáo ghost](sc1/GHOST-REPORT.md) |
| SC1-08 | PAUSED | Codex đã có Space và một phần Telex OS; giữ VNI/nhập nối/focus/trợ năng chưa đạt để kiểm cùng UI mới, theo yêu cầu dừng test dài | SC1-07; kiểm tích hợp UX1-02/DOC1-02/BAL1-04 khi thay UI | [Baseline phần còn thiếu](BASELINE-20260915.md), [SC1-07](sc1/GHOST-REPORT.md) |
| SC1-09 | DONE | Codex: bốn cặp, hỗ trợ/cân bằng/hủy riêng, preview identity, native cập nhật một Undo, metadata v2/đọc v1, save/reopen/restore; luồng thủ công đã kiểm bằng ManualConnect | M3/G1; core/BAL1 đã có; D-WORD-01 đạt, host D còn nghiệm thu riêng | [Báo cáo E](phase-e/REPORT.md), [13 nhóm](../artifacts/phase-e/contracts-final/report.json), [9 native transaction](../artifacts/phase-e/transactions-20260915-232727/report.json) |
| SC1-10 | DOING | Codex: build bốn host, ba ZIP/hash và giải nén 3/3; Word đăng ký gói cuối, Desktop khởi động và Web cập nhật giữ nháp. Có p50/p95 native trên DLL bàn giao; còn host D/SC1-08 và số đo UI/WASM/Word, không coi ZIP là nghiệm thu trọn | SC1-01…09; còn D-HOST/D-CLIP/D-FILE/SC1-08 | [Báo cáo E](phase-e/REPORT.md), [gói](../artifacts/phase-e/packages.json), [bàn giao](../artifacts/phase-e/delivery.json) |
| SC1-11 | TODO | Ghost không ghi vào thân Word trước nhận; phím nhận, vị trí/focus/IME và Undo thực; không tự mở cổng từ kết quả Web/Desktop | SC1-07/09; M4/G2; Space cần D-01/G3 | — |

### E1 — Đồ thị 2D

Giữ ID E1-01/02/03 từ kế hoạch trước; E1-04 cho grammar/evaluator, E1-05 nghiệm thu hai host; E1-06 cho bảng tham số/slider. [Kế hoạch E1](e1/PLAN.md). Triển khai và nghiệm thu E1 đã khép ở alpha G với 29 nhóm kiểm tra, Web UI và walkthrough Desktop thật. [Báo cáo G](phase-g/REPORT.md).

| ID | Trạng thái | Đầu ra | Phụ thuộc và cổng | Bằng chứng |
| --- | --- | --- | --- | --- |
| E1-01 | DONE | Từ luồng UX nhập/tự vẽ/chỉnh/copy chốt PlotDocument: nguồn/candidate, biến, bảng tham số động/phụ thuộc, giá trị tách slider/khoảng/bước/ghim, miền tách khung nhìn, đường/trục/nhãn/nét; schema version/migration | [Đề xuất UX](e1/USER-EXPERIENCE.md); SH-02; core M1 | [G alpha, schema v10 và migration](phase-g/REPORT.md) |
| E1-04 | DONE | Grammar versioned và evaluator AST/bindings: đa thức/phân thức/căn/sin/cos, radian, hằng số/tên tham số có chỉ số a_100; thiếu giá trị/miền và hồi quy grammar cũ, không dùng parser thư viện | E1-01 | [29 nhóm](../artifacts/phase-g/verification/plot-verification.json) |
| E1-02 | DONE | Lấy mẫu/tách gián đoạn, cực cùng dấu/điểm khuyết; pan/zoom/đổi tham số và hủy việc cũ; dựng các đoạn từ evaluator, đạt điểm chuẩn và case biên | E1-01, E1-04, SH-04 | [29 nhóm](../artifacts/phase-g/verification/plot-verification.json) |
| E1-06 | DONE | Bảng tham số/tìm kiếm/lọc/ghim, slider/ô số/gán nhóm; một drag một Undo, Esc/phím; tham số ẩn vẫn tính; kéo nhanh/miền dịch, đo tải 10/100/300 và giữ nguồn khi quá giới hạn | E1-01, E1-04, E1-02 | [Model và UI 300 tham số](phase-g/REPORT.md) |
| E1-03 | DONE | Đổi miền/trục/màu/nét/nhãn, Undo/Redo, nháp và lưu/mở cả tham số; chuột phải copy SVG/PNG/tải file từ scene đúng nguồn/candidate/giá trị hiện tại | E1-06 | [G alpha và UI build cuối](phase-g/REPORT.md) |
| E1-05 | DONE | Slider thật/Undo, pan, Save/Open, Telex/VNI, Copy SVG/PNG và Word paste đã kiểm; Web/Desktop dùng cùng model | E1-03; SH-04 | [Nghiệm thu máy](host-review/20260930.md), [gói](../artifacts/phase-g/packages.json) |

### E3 — Hóa/Lý cơ bản

| ID | Trạng thái | Đầu ra và điều kiện hoàn thành | Phụ thuộc | Bằng chứng |
| --- | --- | --- | --- | --- |
| E3-01 | DONE | Codex: giữ UX Toán, chốt checkbox độc lập và corpus tám tổ hợp, hoa/thường/đơn vị/điện tích/alias, nguồn/candidate/xung đột; tối đa ba candidate tổng. Đầu ra một đợt hợp đồng/corpus trước model; chưa gán thời gian phát hành khi chưa đo parser mới | M1; D-08/D-09; [đầu vào UX/phạm vi](e3/PLAN.md) | [E3 report](e3/REPORT.md) |
| E3-02 | DONE | Nền model/router miền và hợp đồng serializer/export versioned; snapshot cũ không reparse; triển khai điểm mở rộng, kiểm corpus Toán trên native/browser khi host đã có | E3-01 | [E3 report](e3/REPORT.md) |
| E3B-01 | DONE | Parser Hóa: nguyên tố, nhóm/chỉ số, hệ số, điện tích và phản ứng; Co khác CO; văn xuôi/nguồn sai không tự thành hóa; spans/candidate đúng | E3-02 | [E3 report](e3/REPORT.md) |
| E3B-02 | DONE | Hóa qua đúng UI Toán và checkbox nhận diện: SVG/PNG/LaTeX/MathML, lưu/mở; H2SO4, Ca(OH)2, ion/phản ứng đúng; corpus Hóa/Toán và các tổ hợp checkbox đạt | E3B-01, SH-04 | [E3 report](e3/REPORT.md) |
| E3B-03 | DONE | Word Hóa: OMML/native, metadata/save-reopen/Undo/restore và nguồn Unicode nguyên vẹn; công bố phạm vi riêng | E3B-02, M3-04 | [33 native checks](../artifacts/e3/word-native-final/report.json), [report](e3/REPORT.md) |
| E3A-01 | DONE | Parser/ký hiệu Lý: vector, chỉ số, Hy Lạp, giá trị/đơn vị; quy tắc phân biệt biến/đơn vị, nguồn và candidate đúng | E3-02; không phụ thuộc E3B | [E3 report](e3/REPORT.md) |
| E3A-02 | DONE | Lý qua đúng UI Toán và checkbox nhận diện: preview/export/lưu-mở v=10 m/s, vector/chỉ số/đơn vị lũy thừa; corpus Lý/Toán và các tổ hợp checkbox đạt | E3A-01, SH-04 | [E3 report](e3/REPORT.md) |
| E3A-03 | DONE | Word Lý: OMML/native và vòng metadata/save-reopen/Undo/restore; trường hợp không hỗ trợ không bị chuyển một phần | E3A-02, M3-04 | [33 native checks](../artifacts/e3/word-native-final/report.json), [report](e3/REPORT.md) |

E3B-02/E3A-02 cho phép nghiệm thu Desktop/Web độc lập. Mốc đầy đủ từng miền bao gồm task Word -03; nếu Word chưa đạt thì ghi đúng capability, không để parser miền kia chờ theo. E3 đạt 8/8 trong phạm vi alpha local; SC1-01…07 đã đạt; các việc tiếp theo theo hàng đợi UX1/DOC1/BAL1/Word/E1 ở đầu backlog. Không dùng E3 hoặc kế hoạch SC1 để mở W0 G2/G3.

### E2A/E2B — Hình học theo thao tác trực tiếp

| ID | Trạng thái | Đầu ra và điều kiện hoàn thành | Phụ thuộc | Bằng chứng |
| --- | --- | --- | --- | --- |
| E2A-01 | DONE | GeometryDocument và prototype đặt/nối/kéo điểm/cạnh, nhãn; mode tự do/quan hệ, xóa cha và suy biến có quy tắc; D-06 chốt bằng demo | SH-02, SH-04; không ép scene vào MathDocument | [Quy tắc alpha G](phase-g/REPORT.md) |
| E2A-02 | DONE | Điểm/đoạn/đường/tia/tròn/đa giác/nhãn, chọn/kéo/xóa/snap/pan/zoom/nét; kéo cạnh dịch hai đầu, một drag một Undo, Esc phục hồi | E2A-01 | [16 nhóm và Web UI](phase-g/REPORT.md) |
| E2A-04 | DONE | Trung điểm/song song/vuông góc/điểm trên đường tròn từ lệnh rõ; quan hệ phụ thuộc có hướng, cập nhật khi kéo, xử lý chu trình/xóa cha/suy biến | E2A-02; D-06 | [16 nhóm](../artifacts/phase-g/verification/geometry-verification.json) |
| E2A-03 | DONE | Lưu/mở, Undo/Redo và chuột phải copy SVG từ mọi scene hợp lệ; PNG/file; nhãn/nét/quan hệ đúng sau xuất và mở lại | E2A-04 | [G alpha và UI](phase-g/REPORT.md) |
| E2A-05 | DONE | Tam giác/đường cao/góc, kéo điểm, snap trung điểm khi thả, Undo/Redo, Save/Open và SVG/PNG đạt trên Desktop; Web mở file giữ quan hệ | E2A-03 | [Bằng chứng và phạm vi](host-review/20260930.md) |
| E2B-01 | DONE | Prototype camera/phép chiếu/chọn/kéo theo mặt phẳng và chiều sâu; quy tắc thao tác/suy biến có demo | E2A-05; không phụ thuộc M6/M5B về kỹ thuật | [G alpha](phase-g/REPORT.md) |
| E2B-02 | DONE | Scene đa diện 3D, kiểu nét do user chọn/nét khuất tự tính có quy tắc, lưu camera, SVG hình chiếu hiện tại và Undo | E2B-01 | [16 nhóm và UI](phase-g/REPORT.md) |
| E2B-03 | DONE | Tạo khối, kéo XZ giữ Y, báo mặt lệch phẳng, Undo/camera/lưu mở/Copy đạt trên Desktop; Web mở cùng scene | E2B-02 | [Bằng chứng và phạm vi](host-review/20260930.md) |

### UX1 — Giao diện gọn và Desktop tray

| ID | Trạng thái | Đầu ra và tiêu chí | Phụ thuộc | Bằng chứng |
| --- | --- | --- | --- | --- |
| UX1-01 | DONE | Codex: bốn khung, bốn walkthrough, UI rules; người dùng yêu cầu bước tiếp theo, dùng A làm đầu vào B; không coi là nghiệm thu tính năng | Baseline và [hành vi BAL1](ux1/BALANCE-INTERACTION.md) | [Bộ thiết kế và phạm vi đã kiểm](ux1/DESIGN-REVIEW.md), [wireframe](ux1/wireframes.html), [bảng trạng thái](ux1/UI-SPEC.md); chưa tính là tính năng sản phẩm |
| UX1-02 | DONE | Codex: layout input/result/copy/options chung Web/Desktop, checkbox/cặp/phím/nháp giữ; Web được kiểm, Desktop publish; OS/tray thuộc D | UX1-01 | [Báo cáo B](ux1/PHASE-B-REPORT.md), [UI](../src/Locus.Editor/Workspace.razor) |
| UX1-03 | REVIEW | Bung/thu/ẩn/X/Minimize/tray/Thoát và phục hồi nháp đạt; sửa treo lúc thoát. Còn IME đang ghép trong chuyển trạng thái | UX1-02; SH-04 | [Biên bản host](host-review/20260930.md) |

### DOC1 — Dán và xuất nguyên đoạn

| ID | Trạng thái | Đầu ra và tiêu chí | Phụ thuộc | Bằng chứng |
| --- | --- | --- | --- | --- |
| DOC1-01 | DONE | Codex: source/block/region, ID/revision/window/reading/decision, result override, command trước/sau; v5/v6 giữ snapshot cũ, giới hạn/cancel; lệnh balance ở BAL1 | UX1-01; core/SH-02 đã có | [Model](doc1/MODEL.md), [20 kiểm tra](../artifacts/ux1/verification/content-tests.json) |
| DOC1-02 | DONE | Codex: nguyên đoạn + công thức inline, fx/giữ text, chọn trọn/dở/ngược/all, nguồn và Unicode giữ; clipboard text LaTeX, rich export ở DOC1-03 | DOC1-01; UX1-02 | [Báo cáo B](ux1/PHASE-B-REPORT.md), [browser](../artifacts/ux1/verification/browser-checks.json) |
| DOC1-03 | REVIEW | Xuất model/DOCX và clipboard Desktop thực đã đạt; file native Save/Open đạt. Còn nhận tệp tải từ UI Web qua IAB | DOC1-02; BAL1-04 | [Biên bản và phần mở D-FILE](host-review/20260930.md) |

### BAL1 — Đũa thần theo vùng kết quả

| ID | Trạng thái | Đầu ra và tiêu chí | Phụ thuộc | Bằng chứng |
| --- | --- | --- | --- | --- |
| BAL1-01 | DONE | Codex: trước/sau theo vùng, nguồn nguyên vẹn, hệ số/provenance/auto, tách sản phẩm; v7 đọc lại snapshot, giữ khôi phục gộp file cũ | DOC1-01; SC1-03/05/06 | [Báo cáo C](bal1/REPORT.md), [20 nhóm kiểm BAL1](../artifacts/bal1/verification/balance-tests.json) |
| BAL1-02 | DONE | Codex: chọn trọn mới bật; toggle đúng hệ số, một bấm một vùng từ trên xuống, chỉ báo bước/vùng kế tiếp; tự cân bằng sẵn không có hủy giả | BAL1-01; DOC1-02 | [Lệnh](../src/Locus.Application/FormulaSession.Balance.cs), [Web smoke](../artifacts/bal1/verification/browser-checks.json) |
| BAL1-03 | DONE | Codex: hai batch nguyên tử theo selection; bấm lại phục hồi đúng trạng thái trộn và quyết định auto; hủy/stale không commit dở | BAL1-02 | [BAL-UX-05…08](../artifacts/bal1/verification/balance-tests.json), [Web smoke](../artifacts/bal1/verification/browser-checks.json) |
| BAL1-04 | DONE | Codex: auto opt-in, chỉ vùng mới/sửa; hủy đánh dấu bỏ auto, source/Undo/reload được giữ; mở file/đổi history/tắt auto hủy yêu cầu cũ | BAL1-03 | [BAL-UX-09…12 và v7](../artifacts/bal1/verification/balance-tests.json), [phạm vi host C/D](bal1/REPORT.md) |

Các tiêu chí chức năng mới ở [BAL-UX-01…12](ux1/BALANCE-INTERACTION.md). Chỉ chạy kiểm liên quan khi cụm được triển khai; chưa có bài PASS từ việc viết bảng.

### WD1 — Quét và chuyển tài liệu Word có sẵn

| ID | Trạng thái | Đầu ra và tiêu chí | Phụ thuộc | Bằng chứng |
| --- | --- | --- | --- | --- |
| WD1-01 | DONE | Quét chọn/body chỉ đọc; map UTF-16/Word CP, scope và ID/revision; 64 vùng, 100.000 Word positions, ngoài bảng/field/hình | SC1-09; DOC1-01; M3/G1 | [F native 8/8](../artifacts/phase-f/native-delivery/report.json), [phạm vi](phase-f/REPORT.md) |
| WD1-02 | REVIEW | Fx một vùng: click/cuộn/zoom 75/100/150% ở 96 DPI đạt, sửa vị trí Show đầu. Còn highlight đồng thời mọi vùng, nhiều DPI/màn hình; giữ thử nghiệm tắt mặc định | WD1-01 | [F hiện tại](phase-f/REPORT.md) |
| WD1-03 | DONE | Chuyển riêng/nhóm từ bảng fx, số lượng/phạm vi rõ, skip ignore/mơ hồ; xác nhận sửa lỗi riêng; một Undo, guard và rollback; không tự trợ giúp Hóa | WD1-01; SC1-09; dùng panel | [Native](../artifacts/phase-f/native-delivery/report.json), [8/8 transaction](../artifacts/phase-f/transactions-delivery/report.json) |
| WD1-04 | DONE | Text giữ nhận diện khác text bỏ qua; metadata trong content control theo DOCX; save/reopen/Undo/include; text/native drift và ID trùng bị giữ nguyên | WD1-03 | [Native persistence/drift](../artifacts/phase-f/native-delivery/report.json), [schema/contracts](../artifacts/phase-f/contracts-delivery/report.json) |
| WD1-05 | REVIEW | Gói Word mới, 7 contracts/8 native/6 UI đạt; Esc thật hủy ghi. Còn ma trận nhiều màn hình/DPI và IME đang ghép ngay lúc commit | WD1-02 | [F hiện tại](phase-f/REPORT.md), [receipt](../artifacts/phase-f/host-acceptance.json) |

### WEB2 — Tài khoản, quota và thương mại Web

| ID | Trạng thái | Đầu ra và tiêu chí | Phụ thuộc | Bằng chứng |
| --- | --- | --- | --- | --- |
| WEB2-01 | DONE (hợp đồng pilot) | Đã chốt lượt/reset/Guest/offline/Desktop, 5/21/150 công thức và 2/10/100 mỗi công cụ; Premium pilot cấp thủ công, giá/provider online để WEB2-04 | [Kế hoạch sản phẩm](PRODUCT-ITERATION-20260915.md); không chặn local | [Hợp đồng và ca nghiệm thu](web2/QUOTA-CONTRACT.md) |
| WEB2-02 | READY | Account/session/entitlement/backend dùng chung quyền gói; tính toán local, không gửi raw chỉ để đếm lượt; identity thử nghiệm không được coi là đăng nhập sản xuất | WEB2-01 pilot | [Đầu vào](web2/QUOTA-CONTRACT.md) |
| WEB2-03 | TODO | Ledger chống trừ đôi, nhiều tab/retry/lỗi, báo trước batch, không tính render/drag/copy lại; hết lượt giữ dữ liệu | WEB2-02; DOC1-03 và các công cụ đã phát hành | — |
| WEB2-04 | TODO | Pilot hạn mức và cách nâng gói; thanh toán/online theo quyết định riêng, chỉ quảng cáo khả năng đã có | WEB2-03; giá/provider/hosting được chốt trước tác vụ phụ thuộc | — |

## 5. Bộ tình huống nghiệm thu khởi đầu

Đây là danh sách tác vụ làm đầu vào cho corpus M0. Phần core của các ca đã đặc tả được kiểm trong M1; các ca Word/UI và miền mở rộng cần bằng chứng riêng. [Phạm vi runner và case pending](m1/REPORT.md). Không dùng bảng này để suy ra mọi tính năng đã đạt.

| Case | Tình huống | Kỳ vọng hoặc quyết định cần có |
| --- | --- | --- |
| C-01 | `can2`, `x mũ 2`, `1 trên 2` | Chốt alias và dựng lần lượt căn 2, x bình phương, một phần hai theo grammar bản đầu |
| C-02 | `x+1/2` | Kết quả trực tiếp là x + một phần hai; `(x+1)/2` nếu hiện phải là đề nghị đổi phạm vi |
| C-03 | `căn x cộng 1` | Chốt phạm vi ngữ pháp; nếu có nhiều cách hiểu hợp lệ thì không tự chuyển |
| C-04 | `can(2+3` | Nếu đề nghị thêm ngoặc, đó là sửa; không tự chuyển kể cả được bao trong vùng đánh dấu |
| C-05 | `Cho hàm số y = x^2 + 1 với x > 0.` | Chỉ nhận đúng hai vùng toán; giữ câu văn và dấu chấm ngoài vùng |
| C-06 | Câu văn có số; URL/email/đường dẫn có `/`, `_`, `+` và chữ số | Không đủ dấu hiệu thì không gợi ý hoặc chuyển |
| C-07 | Chữ Việt dạng tổ hợp/dựng sẵn, emoji trước công thức, khoảng trắng đặc biệt, xuống dòng | Ánh xạ đúng vùng; khôi phục nguyên chuỗi theo hệ quy chiếu đã chốt |
| C-08 | Telex/VNI đang sửa dấu, Backspace và gõ nhanh | Không ghi giữa composition; không mất ký tự |
| C-09 | Hiện gợi ý rồi nguồn đổi, chèn trước vùng, đổi selection/tài liệu | Hủy nếu kiểm tra lại không xác nhận được chính xác mục tiêu và kết quả |
| C-10 | Focus chuyển sang Find, Ribbon, dialog hoặc cửa sổ khác | Không ghi từ sự kiện nhập cũ |
| C-11 | Source → native → Undo → Redo → save/reopen → restore | Nội dung, metadata và candidate nhất quán; phần ngoài vùng nguyên vẹn |
| C-12 | Native `x²` được sửa thành `x³` | Không dùng nguồn `x mũ 2` để âm thầm ghi đè; D-04 quy định cách sửa/restore |
| C-13 | Copy/paste công thức có metadata, Save As, ID trùng, metadata thiếu/hỏng/mới hơn; nâng phiên bản core | Mở lại đúng bộ kết quả đã biết khi còn đủ dữ liệu; không gắn sai nguồn; giữ native và ngừng quản lý khi không đủ dữ liệu |
| C-14 | `lc[...]` chưa đóng, dấu tùy chỉnh, vùng lồng hoặc nằm trong chuỗi không phải toán | Chỉ auto khi đúng quy tắc D-05 và đủ các cổng; còn lại giữ nguyên |
| C-15 | `x mũ 2` + Space rồi gõ `cộng 1` | Chờ D-01; không tự đặt kỳ vọng về con trỏ hoặc cách nối công thức |
| C-16 | Tắt auto, reload connector hoặc đóng tài liệu trong lúc có candidate chờ | Pending action hết hiệu lực, không ghi muộn |
| C-17 | Lỗi sau chèn native nhưng trước lưu metadata, hoặc ngược lại | Không để thay thế nửa chừng; xác minh phục hồi cả nội dung và metadata |
| C-18 | Word read-only/protected, Track Changes, bảng, header/footer, content control có sẵn | Lập ma trận hỗ trợ; chưa hỗ trợ thì từ chối rõ, không tắt bảo vệ hoặc sửa cấu hình tài liệu |
| C-19 | `1/x`, `sqrt(x)` trong đồ thị | Không nối sai qua gián đoạn; tuân miền xác định |
| C-20 | Kéo hình có quan hệ; Undo; lưu/mở; xoay scene 3D rồi chuột phải copy SVG | Giữ quan hệ theo chế độ đã chọn, scene/camera đúng; SVG khớp hình chiếu hiện tại, không cần hoàn tất bản vẽ |
| C-21 | `v = 10 m/s`, `H2SO4`, `Co`, `CO`, điện tích và phản ứng | Dùng quy tắc miền, giữ hoa/thường; nhận diện không tự cấp quyền sửa |
| C-22 | Hiện kết quả trực tiếp, bấm `fx`; chọn lại công thức sau auto | Chỉ mở các phương án khi được yêu cầu; sau auto vẫn có `fx` để đổi kết quả/khôi phục text |
| C-23 | Cùng nguồn/grammar chạy trên native và browser sau publish | Raw/spans, candidate/loại/thứ tự/ID, diagnostics và snapshot/export trùng theo hợp đồng |
| C-24 | File từ Desktop → Web sửa/lưu → Desktop; file cũ/hỏng/mới hơn | Giữ nguồn/candidate/scene; không parse lại snapshot hoặc ghi đè dữ liệu chưa hỗ trợ |
| C-25 | Tải Web lần đầu, reload offline, cập nhật khi có nháp; clipboard bị từ chối | Phân tích tại browser; nháp được giữ, grammar/assets cùng version; vẫn xuất file được |
| C-26 | `1/(x-1)`, `sqrt(x)`, `sin(x)`; zoom và đổi nguồn liên tiếp | Đúng miền/radian, không nối qua gián đoạn; chỉ kết quả của phiên hiện tại được dựng |
| C-27 | Kéo cạnh chung đỉnh; một drag rồi Undo; Esc giữa drag | Di chuyển đúng đối tượng liên quan, một Undo trả trạng thái trước; Esc hủy drag |
| C-28 | Chọn công cụ vuông góc, kéo điểm cha, xóa cha hoặc tạo quan hệ chu trình | Quan hệ chỉ sinh khi chọn rõ; xử lý phụ thuộc/suy biến theo D-06, không âm thầm biến hình |
| C-29 | `H2SO4`, `Ca(OH)2`, `Co`/`CO`, ion và phản ứng ở các mode | Hoa/thường/chỉ số/điện tích đúng; auto nhận diện không cấp quyền sửa; candidate nguồn/export nhất quán |
| C-30 | Biến `m` trong Toán; giá trị kèm `m/s` trong Lý; vector và chỉ số | Phân biệt theo grammar/ngữ cảnh đã công bố; không mặc định mọi ký hiệu m là đơn vị |

## 6. Mẫu ghi kết quả khi đóng task

```text
Task:
Người thực hiện:
Trạng thái:
Phạm vi thực tế:
Artifact/mã:
Môi trường và cách chạy lại:
Case đã kiểm tra và kết quả:
Giới hạn hoặc lỗi còn mở:
Quyết định sản phẩm/kiến trúc bị ảnh hưởng:
Task tiếp theo đủ điều kiện:
```
