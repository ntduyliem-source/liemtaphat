# Rà soát lõi phục vụ CT4

**Tài liệu baseline lịch sử.** A01–A13 đã được xử lý trong CT4; xem [CT4-VERIFICATION.md](CT4-VERIFICATION.md) để biết kết quả thực hiện. C01–C08 vẫn là backlog. Nội dung quan sát bên dưới được giữ nguyên như thời điểm trước sửa.

Ngày 05/10/2026. Rà soát working tree hiện tại sau CT0–CT3, đối chiếu Web release `20261005-083635-640`. **Kết luận: nền nhận diện, mô hình công thức và solver đã có; CT4 còn việc ở Core, Application và renderer, không chỉ ở UI. Chưa triển khai các sửa đổi trong tài liệu này.**

Kế hoạch thực hiện và nghiệm thu: [CT4-PLAN.md](CT4-PLAN.md). Tài liệu này giữ bằng chứng baseline; không sửa các kết quả quan sát thành kết quả mong muốn sau khi triển khai.

## Phạm vi và bằng chứng

Đã đọc đường đi từ `AnalysisWire` → detection/parser → candidate, `ContentAnalyzer` → `FormulaSession`, balance/completion/provenance, selection và export. Kiểm thêm sự khác nhau giữa grammar Công thức và Đồ thị. Không đánh giá lại tính đúng hóa học của toàn bộ tài liệu tham khảo trong kho; không kiểm host Word/Windows hay xuất ảnh cả đoạn vốn chưa có.

Một chương trình thăm dò tạm chạy qua API thật ghi **50 quan sát**, gồm 31 đầu vào phân tích, 6 yêu cầu tự điền, thống kê kho, một chuỗi đổi môn, 5 chuỗi cân bằng/hủy, 5 đoạn hỗn hợp và một phép phân tích cô lập. Đây là quan sát hiện trạng, **không phải 50/50 ca nghiệm thu CT4**.

- [Kết quả JSON](../../artifacts/design-system/ct4-audit/observations.json).
- [Mã thăm dò](../../artifacts/design-system/ct4-audit/probe/Program.cs), [project](../../artifacts/design-system/ct4-audit/probe/Probe.csproj).
- [Danh sách hash trước rà soát](../../artifacts/design-system/ct4-audit/source-before.json), [đối chiếu sau rà soát](../../artifacts/design-system/ct4-audit/source-check.json).
- Bằng chứng CT0–CT3 tại [biên bản riêng](CT0-CT3-VERIFICATION.md); không dùng số kiểm cũ để tuyên bố các yêu cầu CT4 đã đạt.

Artifacts là dữ liệu local, không đưa vào Git. Chạy lại từ thư mục gốc với `dotnet run --project artifacts/design-system/ct4-audit/probe/Probe.csproj -- artifacts/design-system/ct4-audit/observations.json` khi còn project thăm dò. Những ví dụ và kết quả quan trọng được ghi ngay bên dưới để tài liệu vẫn dùng được khi không mang theo artifacts.

## Năng lực đã có

| Thành phần | Có trong code | Giới hạn liên quan CT4 |
| --- | --- | --- |
| Nguồn và tọa độ | Giữ raw, revision, source spans UTF-16; chuẩn hóa token; nguồn sửa và kết quả hiển thị tách nhau. | Mọi gợi ý mới phải giữ span và source edit chính xác, kể cả chữ Việt/emoji. |
| Cấu trúc Toán | Số, biến một chữ, dấu âm, cộng/trừ/nhân/chia, lũy thừa, căn, một phép quan hệ, một số cách nhân ngầm. | Không phải parser LaTeX tổng quát; các hàm và cấu trúc nâng cao chưa có trong MathDocument hiện tại. |
| Candidate | Phân biệt `direct`, `interpretation`, `repair`; repair phải có edit; tối đa ba candidate. | Có quy tắc sửa nhưng rất hẹp; UI cũng đang trình bày sai mục đích. |
| Nhận diện môn | Route Toán/Lý/Hóa, loại candidate hiển thị trùng; bảo vệ URL/email/path; cặp chung và cặp môn. | Có ca nhận nhầm đường dẫn. Cặp chỉ định môn có thể vượt bộ lọc chung theo hợp đồng core cũ; không đồng nghĩa được phép chạy nút Hóa trong chế độ Toán. |
| Lý | Chỉ số, chữ Hy Lạp, vector, số kèm đơn vị, tích/chia/lũy thừa đơn vị. | Chưa nhận số dạng `3e8`; chưa đọc tích chữ liền `ma`; không thực hiện kiểm thứ nguyên hoặc giải bài Lý. |
| Hóa | Nguyên tố, chỉ số, nhóm `()`/`[]`, hệ số, điện tích, hai vế, mũi tên; alias chữ thường khi phân tách duy nhất. | Chưa có hydrate, trạng thái chất, nhãn điều kiện trên mũi tên. Chữ thường mơ hồ bị từ chối; số 0 không tự biến thành O. |
| Solver | Giải hệ bảo toàn bằng số hữu tỉ chính xác; kiểm độc lập nguyên tố/điện tích; không thêm/bớt chất. | Cần phương trình phù hợp có nghiệm dương duy nhất; không chứng minh phản ứng xảy ra trong thực tế. |
| Tự điền | Kho offline có điều kiện, provenance/rule ID, dự đoán sản phẩm rồi cân bằng. | Chỉ tra các tổ hợp đã biên soạn; hiện 45 bản ghi, có cả bản ghi không có phản ứng ion rút gọn. |
| Tài liệu hỗn hợp | Text blocks, formula regions, lựa chọn theo vùng, giữ text, Undo/Redo, phục hồi sản phẩm/hệ số riêng. | Draft Hóa nằm giữa câu chưa được phát hiện như draft nằm riêng dòng hoặc được bọc. |
| Xuất | Một candidate có LaTeX/MathML/OMML; renderer tạo SVG và PNG công thức riêng. `ContentExport` chụp đoạn text + formula để xuất DOCX/HTML. | Chưa có layout và ảnh cho toàn bộ đoạn hỗn hợp. |

Nguồn chính: [Model](../../src/Locus.Core/Model.cs), [ScientificDocument](../../src/Locus.Core/Domains/ScientificDocument.cs), [FormulaParser](../../src/Locus.Core/Parsing/FormulaParser.cs), [DomainAnalysisEngine](../../src/Locus.Core/Detection/DomainAnalysisEngine.cs), [ContentDocument](../../src/Locus.Application/ContentDocument.cs).

## Thiếu sót cần xử lý trong CT4

| ID | Quan sát hoặc bằng chứng | Tầng cần sửa | Đầu ra cần bổ sung |
| --- | --- | --- | --- |
| A01 | `x+1/2` có repair `(x+1)/2`; `x^2+1/2`, `x*y+1/2`, `x+1/y` chỉ có direct. | Core | Quy tắc phạm vi phân số theo cấu trúc biểu thức, không chỉ biến đơn và hai số. |
| A02 | `x-1/2` và `lc[x-1/2]` bị `PROTECTED_PATH`; `x - 1/2` nhận được. Gọi FormulaParser trực tiếp cũng nhận `x-1/2`. | Core detection | Phân biệt đường dẫn với phép trừ/chia có bằng chứng Toán. Giữ bảo vệ `data-set/1`, URL/email và đường dẫn thật. |
| A03 | Repair căn chỉ phủ mẫu hẹp; thiếu một ngoặc cuối chỉ được đề xuất cho lời gọi căn như `can(x+1`, chưa có với `(x+1`. | Core | Mở rộng có giới hạn cho tổng/hiệu dưới căn và đúng một ngoặc đóng cuối; không đoán nhiều sửa cùng lúc. |
| A04 | Hàng “Ý bạn là” lặp direct; không chọn vùng thì hiện sample thay nguồn; chỉ hiện candidate khi người dùng đã chọn riêng vùng. | Presentation/UI | Tự lấy ngữ cảnh công thức đơn; chỉ hiện đề nghị khác kết quả hiện tại; giải thích edit; bỏ sample khỏi hàng này. |
| A05 | `Configure(All → Math)` với `H2SO4` giữ snapshot Hóa; explicit `AnalyzeContentAsync()` sau đó mới bỏ vùng Hóa. | Application/UI | Lệnh đổi bộ lọc và nhận diện lại có một bước Undo; giữ nguồn và các quyết định còn hợp lệ. Không tự diễn giải lại file lịch sử khi mở. |
| A06 | `CanRunStudioBalance` chỉ chặn busy/IME/disposed. Thăm dò với Math + `hoa-[H2+O2=H2O]` vẫn cân bằng được qua lệnh Studio. | Application/Presentation | Chính sách lệnh Studio chặn Toán/Lý/MANUAL theo nút chỉ hoạt động ở All/Hóa, kiểm lại trước commit. Không phá core routing cặp Hóa ở connector khác. |
| A07 | `2H2+O2=2H2O` vốn đã cân bằng vẫn tạo `ManagedBalance=true`; hủy không đổi công thức. `HCl+NaOH=` cũng bị gắn cân bằng dù chỉ điền sản phẩm, hệ số đều 1. | Application | Phân biệt có đổi hệ số và chỉ điền sản phẩm. Không tạo lịch sử/hủy hệ số giả; tổng kết đúng số việc thực sự đã làm. |
| A08 | Chuỗi `H2+O2=` → cân bằng → hủy hệ số → `DropContentProducts` đã hoạt động và giữ raw. Nhưng Hủy tự điền bị giấu trong Chi tiết; nhãn chưa đúng yêu cầu. | Presentation/UI | Hiện Hủy cân bằng cho thay đổi hệ số thực; hiện Hủy tự điền cho sản phẩm có provenance. Sản phẩm do người dùng nhập không bao giờ bị hủy nhầm. |
| A09 | `StudioBalanceRegionId` coi `Display == null` là có thể nhắm Hóa; API lệnh chỉ nhận một ID hoặc toàn bài, không nhận danh sách ID vùng chọn. | Application/Presentation | Mô hình mục tiêu có kiểu rõ: một vùng, tập vùng hoặc toàn kết quả; xác định draft Hóa thật, không coi mọi vùng lỗi là Hóa. |
| A10 | `Xét H2+O2= trong bài.` không có vùng; `Xét hoa-[H2+O2=] trong bài.` điền được; draft riêng dòng điền được. | Core/Application | Locator draft trong câu có ranh giới rõ, span bao cả dấu `=`. Không lấy chữ câu văn làm chất phản ứng. |
| A11 | Cân bằng gom thông báo thành chuỗi, chỉ giữ hai message khác nhau; không có kết quả xử lý cấu trúc cho từng vùng. | Application/UI | Kết quả theo ID: đổi hệ số, điền sản phẩm, đã cân bằng, cần điều kiện, không có dữ liệu, giữ nguyên. Điều kiện giả định đọc lại được đúng vùng. |
| A12 | `articleScope` và `resultSelection` là hai nguồn trạng thái; đổi nhánh export làm nút SVG/PNG biến mất. Ảnh bị buộc `CanExportSingle`. | Application/Presentation/renderer | Một trạng thái chọn thống nhất; capture kết quả chọn; compositor text + nhiều công thức; ảnh SVG/PNG/copy cho mọi phạm vi hợp lệ. |
| A13 | Serializer kiểm core/grammar version chặt, ID candidate phụ thuộc version và cấu trúc. | Core/Application | Quyết định version và kiểm tương thích snapshot trước khi mở rộng repair; không tăng constant làm file đã lưu không mở được. |

Các nguồn truy vết: [quy tắc sửa](../../src/Locus.Core/Parsing/FormulaParser.cs), [bảo vệ văn bản](../../src/Locus.Core/Detection/ProtectedTextRecognizer.cs), [Configure](../../src/Locus.Application/FormulaSession.cs), [lệnh Studio](../../src/Locus.Application/FormulaSession.StudioBalance.cs), [hủy sản phẩm](../../src/Locus.Application/FormulaSession.ContentProducts.cs), [trạng thái Studio](../../src/Locus.Editor/Workspace.Studio.cs), [selection](../../src/Locus.Editor/Workspace.Content.cs), [thanh xuất](../../src/Locus.Editor/Formula/Components/FormulaExportBar.razor), [renderer](../../src/Locus.Editor/wwwroot/renderer.js), [serializer](../../src/Locus.Core/Serialization/CandidateSetSerializer.cs).

## Giới hạn cú pháp cần có đợt bổ sung riêng

Các mục sau được ghi thành backlog cụ thể để không bị bỏ quên. Chúng **chưa được tính là khả năng của Công thức, và chưa tự đưa hết vào phạm vi CT4**.

| ID | Ví dụ và hiện trạng | Phần phải bổ sung | Thứ tự đề xuất sau CT4 |
| --- | --- | --- | --- |
| C01 | `sin(x)`, `log(x)` bị `UNSUPPORTED_FUNCTION`. Đồ thị có `sin/cos/tan/abs/ln/exp` trong `PlotExpression` riêng. | Token/hàm trong MathDocument, precedence, source spans, schema/serializer và ba exporter. Tham khảo grammar Đồ thị nhưng không coi nó là hỗ trợ sẵn của Công thức. | Ưu tiên 1 |
| C02 | `x_1` bị từ chối ở Toán, được nhận ở Lý. `F=ma` bị `UNSUPPORTED_IDENTIFIER`, `F=m*a` nhận được. | Chỉ số phù hợp ở Toán và quy tắc tích chữ liền có phân biệt tên hàm/từ văn bản/đơn vị. Không tách mọi từ thành tích. | Ưu tiên 1 |
| C03 | `3e8 m/s` bị `UNSUPPORTED_SCIENTIFIC_NOTATION`. | Số mũ khoa học và kết hợp Quantity; phân biệt biến e, đơn vị và phần mũ chưa gõ xong. | Ưu tiên 1 |
| C04 | `1<x<2` bị `UNSUPPORTED_RELATION_CHAIN`. AST hiện chỉ có một quan hệ. | Chuỗi quan hệ; sau đó đặc tả riêng hệ phương trình, ma trận, tổng, tích phân, giới hạn, đạo hàm. Những cấu trúc sau chưa có node hỗ trợ qua rà code, chưa chạy probe từng loại. | Ưu tiên 2 |
| C05 | `CuSO4·5H2O`, `H2(g)+O2(g)=H2O(l)` bị từ chối. | Hydrate, trạng thái chất, điều kiện/chất xúc tác trên mũi tên; counting/identity/export đúng cho cấu trúc mới. | Ưu tiên 1 cho Hóa |
| C06 | `co` bị mơ hồ; `h2+o2=h20` giữ H₂₀ và cảnh báo 0/O, không có repair H₂O. | Gợi ý phân biệt Co/CO và sửa 0/O theo ngữ cảnh, có edit/provenance, chỉ nhận khi người dùng chọn. Không tự sửa chuỗi gốc. | Ưu tiên 2 |
| C07 | `N2+H2=` trả `unsupported`; nhập đủ `N2+H2=NH3` mới có thể đưa cho solver. | Bổ sung kho theo nhóm bài phổ thông, mỗi record có nguồn, điều kiện, case đối chứng và bảo toàn. Không hứa mọi tổ hợp đều tự suy được. | Đợt kho Hóa sau CT4 |
| C08 | Lý hiện là dựng ký hiệu/đơn vị; chưa có bộ kiểm thứ nguyên, đổi đơn vị hoặc giải phương trình Lý. | Chỉ mở khi chốt nhu cầu sản phẩm và corpus riêng. | Chưa đưa vào đợt Công thức hiện tại |

Những câu ở lượt brainstorm trước như “mở rộng repair cho hàm” chỉ thực hiện được sau C01. CT4 mở rộng trên các cấu trúc parser hiện đã đọc được; không báo chức năng hàm là đã có.

## Kho tự điền Hóa hiện có

Thống kê trực tiếp `ReactionCatalog.Rules`: **45 bản ghi** gồm trung hòa 14, kết tủa 13, carbonate–acid 7, CO₂–base 2, tổng hợp 1, cháy 3, phân hủy 2, kim loại–acid 1, oxide–nước 1, không có phản ứng ion rút gọn 1. Đây là các record cụ thể, không phải 45 quy tắc tổng quát suy được vô hạn phản ứng.

| Input | Phản hồi hiện tại khi bấm điền/cân bằng |
| --- | --- |
| `H2+O2=` | Có kết quả theo mẫu đã mồi phản ứng. |
| `HCl+NaOH=` | Có kết quả theo mẫu trung hòa hoàn toàn trong nước. |
| `NaOH+CO2=` | Cần điều kiện/tỉ lệ; có hai mẫu carbonate và bicarbonate; chưa tự chọn. |
| `N2+H2=` | Chưa có mẫu. |
| `Xe+NaCl=` | Chưa có mẫu; không kết luận là không phản ứng. |
| `h2+o2=h20` | Cần làm rõ cách hiểu; không âm thầm sửa số 0 thành O. |

`UseUniqueCatalogConditions=true` đang cho phép nút Studio dùng điều kiện của mẫu duy nhất. CT4 phải giữ rõ điều kiện này trong kết quả theo vùng, tránh người dùng hiểu rằng đã xét mọi nhiệt độ/nồng độ/tỉ lệ. Ghost hỗ trợ vẫn có hợp đồng riêng, không tự bật auto-balance hoặc auto-accept repair.

Nguồn: [ChemistryAssistanceWire](../../src/Locus.Application/ChemistryAssistanceWire.cs), [ReactionCatalog](../../src/Locus.Core/Assistance/ReactionCatalog.cs), [dữ liệu kho](../../src/Locus.Core/Assistance/ReactionCatalog.Data.cs), [solver](../../src/Locus.Core/Assistance/ReactionBalancer.cs).

## Giới hạn kỹ thuật cần giữ khi sửa

- Parser công thức: 4096 UTF-16 code unit mỗi vùng, độ sâu tối đa 64; parser Hóa có giới hạn riêng cho nhóm/term.
- Tài liệu hỗn hợp: phân tích tối đa 100.000 UTF-16 code unit, tối đa 256 vùng; vượt giới hạn giữ nguồn và báo rõ.
- Solver: tối đa 32 chất, 128 hàng bảo toàn, integer budget 4096 bit, deadline mặc định 1000 ms. CT4 không nâng các mức này khi chưa đo.
- Renderer hiện tại kiểm kích thước và trần 24 triệu pixel khi rasterize. PNG 4X của đoạn dài có thể vượt trần; kế hoạch ảnh cả đoạn phải có lỗi rõ và đường dùng SVG/chọn đoạn ngắn hơn, không cắt nội dung âm thầm.
- Repair không được tự chuyển đổi; candidate mới không được làm mất direct hợp lệ hoặc vượt tổng ba kết quả. Việc tăng gợi ý có thể thay đổi eligibility tự động và phải kiểm.
- Chuyển môn, chọn vùng, sửa nguồn hoặc IME xảy ra trong lúc xử lý phải vô hiệu kết quả cũ trước commit/export.

## Kết luận cho phạm vi thực hiện

CT4 xử lý A01–A13, giữ các khối đã có và bổ sung phần thiếu theo tầng. C01–C08 là backlog năng lực riêng có ví dụ đầu vào, không bị gộp thành lời hứa “core đã đủ Toán/Lý/Hóa”. Bằng chứng hoàn tất CT4 sẽ được tạo trên build triển khai; tài liệu rà soát này không thay thế nghiệm thu đó.
