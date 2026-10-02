# M1 — Core dùng chung đã triển khai

Hoàn tất: **2026-09-12**. M1-01 đến M1-06 đạt trong phạm vi grammar đã chốt. M2 Desktop alpha là mốc tiếp theo; các cổng Word CW/G1/G2/G3 vẫn giữ trạng thái chưa đạt của M0.

**Báo cáo lịch sử M1.** Desktop hiện đã được triển khai ở [M2](../m2/REPORT.md); các giới hạn bên dưới mô tả riêng đầu ra M1.

## Kết quả

| Phần | Đã có | Mã / bằng chứng |
| --- | --- | --- |
| Build và dependency | Git repository cục bộ, solution, SDK pin 10.0.400, locked restore, build Release cảnh báo là lỗi | [Locus.sln](../../Locus.sln), [build.ps1](../../tools/build.ps1), [global.json](../../global.json) |
| Core độc lập | C# `netstandard2.0`, không tham chiếu Word/UI/HTTP; không có quyền ghi tài liệu | [Locus.Core](../../src/Locus.Core/Locus.Core.csproj) |
| Nguồn và vị trí | Raw bất biến, UTF-16 half-open, source revision/ID; NFC và ánh xạ compose/expand/replace; không cắt surrogate pair | [Model.cs](../../src/Locus.Core/Model.cs), [SourceMapping.cs](../../src/Locus.Core/SourceMapping.cs) |
| Parser | Tokenizer tiếng Việt, số/biến/ngoặc/toán tử, ưu tiên/kết hợp, lũy thừa, căn, phân số và quan hệ | [Parsing](../../src/Locus.Core/Parsing/FormulaParser.cs), [grammar](../m0/GRAMMAR.md) |
| Candidate | Direct trước; mơ hồ chia–nhân ngầm; ba heuristic repair có edit/provenance; tối đa ba candidate, không lấp danh sách | [FormulaParser](../../src/Locus.Core/Parsing/FormulaParser.cs) |
| Nhận diện | Vùng explicit, văn xuôi xen toán, bảo vệ URL/email/path/identifier; `lc[...]` và dấu tùy chỉnh, nhiều vùng độc lập | [AnalysisEngine](../../src/Locus.Core/Detection/AnalysisEngine.cs) |
| Xuất | MathML preview, OMML, LaTeX và nguồn nguyên văn từ đúng Candidate ID | [CandidateExporter](../../src/Locus.Core/Export/CandidateExporter.cs) |
| Snapshot | JSON có schema/version/checksum, raw/spans/all candidates/selection/markers/diagnostics/edits; đọc lại không gọi parser | [CandidateSetSerializer](../../src/Locus.Core/Serialization/CandidateSetSerializer.cs) |
| Hủy và kết quả cũ | Parser/normalizer nhận CancellationToken; gate theo phiên loại kết quả trả chậm dù worker bỏ qua cancellation | [LatestRequestGate](../../src/Locus.Core/LatestRequestGate.cs) |
| Công cụ xem thử | CLI xuất JSON và giao diện trình duyệt gọi chính core C#; không tra bảng fixture như M0 | [CLI / preview](../../src/Locus.Cli/README.md) |

Ví dụ ngoài corpus đã chạy: `q+7/11`, `q mũ 7`, `3 trên 5`, `7 cộng 12 nhân 3`, `7/8q`. Cặp bọc mặc định vẫn là `lc[` / `]`; đổi dấu không đổi grammar. Snapshot giữ đúng cấu hình đã dùng cùng toàn bộ wrapper gốc.

## Kiểm chứng

Lệnh **`./tools/build.ps1`** đã hoàn tất trên Windows 10.0.19045, .NET SDK 10.0.400/runtime .NET 10.0.11: **0 warning, 0 error; 238/238 nhóm kiểm tra đạt**. [Kết quả máy đọc được](../../artifacts/m1/verification.json).

- **104 case nội dung của corpus M0:** so đúng detection, span/nội dung/replacement, AST, loại/thứ tự candidate, diagnostic code/severity/span, repair edits và content eligibility.
- **5 case snapshot đã lưu:** dựng dữ liệu snapshot lịch sử làm đầu vào serialization, rồi đọc lại; không parse lại để thay candidate cũ.
- **8 case pending vẫn loại khỏi số đạt:** 4 Space UX và 4 Lý/Hóa. Không giả lập thành parser pass.
- **129 nhóm kiểm tra bổ sung:** nguồn Unicode, bất biến model, ca grammar/detection ngoài corpus, snapshot hỏng/version/ID/spans, cấu trúc xuất, cancellation, concurrent/delayed results và giới hạn. Trong đó hai nhóm thực hiện thêm 200 biểu thức theo grammar và 300 đầu vào ngẫu nhiên, gồm chuỗi lỗi, được sinh với seed cố định. Nhóm ngẫu nhiên kiểm tra không crash, kết quả xác định và giới hạn candidate; không giả định mọi chuỗi đều phải bị từ chối.

Các case M0 có bối cảnh IME/focus/native edit vẫn chỉ so **phần nội dung core hoặc snapshot**. Runner không kiểm trường `writeExpectation` hay trình bày `fx` như thể đang điều khiển Word. M1 chưa bổ sung bằng chứng cho các host guard của M3–M5.

[Runtime check](../../artifacts/m1/runtime-check.json) chạy **DLL core thật**, không dùng shared fixture DLL của M0: .NET 10 và .NET Framework 4.8 x86 có cùng SHA-256 assembly và cùng 43 dòng kết quả trên 13 nguồn thử. So định danh candidate, MathML/OMML/LaTeX và normalization; mỗi host đọc lại snapshot thành công. Đây là console hosts, chưa phải Desktop/VSTO được cài.

[HTTP smoke](../../artifacts/m1/http-smoke.json) có 16 kiểm tra: endpoint dùng parser thật, dấu tùy chỉnh/NFD/raw/revision, MIME và asset, từ chối host/origin/content-type/JSON/kích thước không hợp lệ. Chạy lại bằng [verify_http.py](../../tools/m1/verify_http.py) khi local server đang chạy.

Đã thao tác và xem giao diện bản Release trong trình duyệt: `q+7/11` có direct và repair đúng cấu trúc; chọn repair đổi lựa chọn nhưng giữ nguyên nguồn. Thay nguồn thành `2e−3` gỡ ngay candidate/copy trước, sau đó hiện chẩn đoán không hỗ trợ. Cấu hình `<<` / `>>` với `Đặt <<q mũ 7>> và <<3 trên 5>>.` hiện hai vùng và hai preview đúng, văn xuôi không lọt vào vùng công thức. Ảnh kiểm tra trực quan cho thấy tiêu đề, editor, trường cấu hình và công thức không chồng/cắt nhau ở khung nhìn đã thử. Đây không phải bằng chứng Telex/VNI thật, clipboard hệ điều hành hoặc Word.

Validator dữ liệu corpus cũng đạt: 117 case, 109 specified, 8 pending; 15 bản sao cố ý làm hỏng đều bị từ chối. Kết quả này tách riêng khỏi kiểm tra core ở trên.

[Đo thời gian](../../artifacts/m1/timing.json): 350 lần phân tích sau warmup, trên 7 mẫu ngắn; lượt build nghiệm thu đo p50 khoảng **0,10 ms**, p95 khoảng **0,16 ms**. Đây là thời gian core trên bộ mẫu hẹp, chưa phải benchmark toàn grammar, giới hạn 200 ký tự, cold startup, preview hoặc Word. Giá trị cụ thể có thể đổi giữa các lần chạy.

## Những lỗi đã sửa trong quá trình kiểm chứng

- Candidate ID trước đó có thể trùng khi ghép chuỗi edit chứa ký tự phân cách. Hiện mỗi edit có mã hóa/hash riêng; diagnostic và phiên bản cũng tham gia identity.
- Callback cancellation bị lỗi có thể cản request mới. Gate hiện giữ cleanup và không cho callback của request cũ làm hỏng request mới.
- Normalization chuỗi nhiều dấu tổ hợp đã có kiểm tra cancellation bên trong vòng quét và quanh bước NFC.
- `2e−3` với dấu trừ Unicode bị hiểu như đại số; hiện scientific notation chưa hỗ trợ được từ chối, còn `2*e−3` vẫn là đại số hợp lệ.
- Nhận diện thụ động không lấy `x^2` ra khỏi identifier/biểu thức chưa hỗ trợ như `abc+ x^2` hoặc `x^2 +abc`.
- Hai case `fx` M0-105/M0-116 thiếu thông tin repair đã được sửa với [errata rõ ràng](../../corpus/m1/ERRATA.md); giữ nguyên AST, kind, edits và quyền auto. M0-099 lịch sử không bị sửa.
- Giao diện xóa candidate/copy cũ ngay khi nguồn/chế độ/cặp dấu thay đổi; không đợi hết debounce mới vô hiệu hóa kết quả trước.

## Phạm vi còn lại

M1 cung cấp **core và công cụ thử**, chưa phải Desktop alpha hoặc add-in Word. Không nhận phím toàn hệ thống, tự thay nội dung Word, triển khai installer hoặc mở khóa cổng auto. MathML là đường preview M1; SVG/PNG và clipboard vào ứng dụng đích thuộc M2. OMML mới được kiểm cấu trúc XML trong M1; cần tích hợp/kiểm chứng bộ xuất này qua connector ở M3.

Snapshot reader chỉ nhận những schema/core/grammar version hiện đã hỗ trợ; bản không tương thích bị từ chối, không reparse âm thầm. Thiết kế migration/version compatibility và native đã sửa tiếp tục ở M3/M4. SHA-256 trong JSON phát hiện corruption, không xác thực người tạo snapshot.

Normalization giữ nguồn và ánh xạ theo chuỗi canonical combining; không tuyên bố đã triển khai đầy đủ Unicode grapheme segmentation cho mọi emoji script. Parser dùng span raw trực tiếp; host không được dùng một projection rộng như thể là vị trí caret chính xác trong Word.

Giới hạn và API tại [CORE-USAGE.md](CORE-USAGE.md). Bản M1 chưa có đồ thị, hình học, Lý/Hóa hoặc WPS. Đây là các mốc riêng trong roadmap.
