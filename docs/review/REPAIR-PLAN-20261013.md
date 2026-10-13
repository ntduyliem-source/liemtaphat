# Kế hoạch sửa lỗi sau rà soát Locus

Ngày 13/10/2026. **Trạng thái: FIX0–FIX4 đã triển khai; FIX5 đã kiểm logic và Web, nghiệm thu native Desktop/Word còn mở.** [Biên bản triển khai](REPAIR-VERIFICATION-20261013.md) ghi kết quả, bằng chứng và giới hạn. Baseline mã nguồn: `4d4f91e4ca587dece7909596568f01e545b19a46`. Review xác nhận **13 lỗi: 3 P1 và 10 P2**. P1 trong kế hoạch này là lỗi có thể âm thầm làm sai nội dung công thức; P2 là lỗi nhận diện, thao tác hoặc phát hành ảnh hưởng sử dụng.

Kế hoạch này là ưu tiên tiếp theo của [roadmap](../ROADMAP.md), trước mở rộng core C01–C08 và H/WEB2. Các kết quả nghiệm thu cũ giữ nguyên phạm vi và ngày; không dùng chúng để kết luận 13 lỗi mới đã được xử lý. Bằng chứng review trên máy nằm trong `artifacts/review-20261013/review.json`; các ca tái hiện thiết yếu được ghi đầy đủ bên dưới để kế hoạch vẫn dùng được khi clone repo không có artifacts.

## 1. Mục tiêu và giới hạn

1. Không làm sai nghĩa công thức hoặc tự thêm kết quả khi người dùng đã nhập vế phải.
2. Nhận diện nhất quán giữa công thức riêng, công thức trong đoạn, cặp bọc và các chế độ môn học; giữ chính xác nguồn và vị trí.
3. Sửa ba lỗi hình học đã xác nhận và giữ link Web dùng được qua lần phát hành tiếp theo.
4. Đưa ca lỗi vào kiểm thử tự động, rồi nghiệm thu luồng người dùng trên bản build thực tế.

Giữ thiết kế Studio đã duyệt: bố cục, cỡ chữ, vị trí Undo/Redo, nút Hóa, cách chọn và thanh xuất ảnh. Không thêm lại fx trong kết quả, checkbox tự cân bằng, nháp, bộ chọn phạm vi hoặc chi tiết thừa trong “Ý bạn là”. Gợi ý vẫn chỉ có ô công thức; phân biệt cách hiểu/sửa lỗi được giữ trong dữ liệu và quy tắc tự động.

Web là website; Desktop là ứng dụng cài riêng; Word là connector riêng. Việc sửa thư viện có thể ảnh hưởng nhiều sản phẩm, nhưng **mỗi sản phẩm có bản build, nghiệm thu và phát hành riêng**. Kết quả kiểm Web không thay thế kiểm Desktop/Word.

Không gộp việc thêm hàm `sin/log`, hydrate, dạng số khoa học, kiểm thứ nguyên hoặc suy phản ứng tổng quát vào đợt này. Chặn an toàn cú pháp chưa hỗ trợ là sửa lỗi; triển khai thêm grammar là đợt mở rộng sau.

## 2. Thứ tự thực hiện

| Đợt | Việc chính | Lỗi phụ trách | Phụ thuộc | Trạng thái |
| --- | --- | --- | --- | --- |
| FIX0 | Chốt ca hồi quy và tương thích dữ liệu cũ | Chuẩn bị cho 13 lỗi | Baseline review | DONE — 4 fixture lịch sử |
| FIX1 | Sửa lỗi làm sai công thức và hai lỗi cùng đường phân tích | R01, R02, R03, R07, R08 | FIX0 | DONE |
| FIX2 | Sửa ranh giới nhận diện và chế độ Tất cả | R04, R05, R06, R09 | FIX1 | DONE |
| FIX3 | Sửa dựng hình, nét khuất và Undo | R10, R11, R12 | FIX0; ưu tiên sau Công thức | DONE |
| FIX4 | Giữ URL Web ổn định qua phát hành | R13 | FIX0; có thể làm song song FIX3 | DONE — cả root và project path |
| FIX5 | Kiểm tích hợp, chặn phát hành lỗi và bàn giao | Nghiệm thu FIX1–FIX4 | Các sửa lỗi đã đạt kiểm riêng | Web/logic đạt; native Desktop/Word còn mở |

Bắt đầu bằng FIX0 → FIX1 → FIX2. Có thể triển khai FIX3/FIX4 độc lập sau đó. Không chạy toàn bộ ma trận máy sau mỗi thay đổi nhỏ. Không ước lượng % hoàn thành hay ngày phát hành từ số lỗi vì độ khó khác nhau.

## 3. FIX0 — Ca hồi quy và tương thích

**Đầu ra:** danh mục R01–R13 có test hoặc probe tự động tái hiện lỗi trên baseline, cùng quyết định tương thích trước khi sửa semantics.

- Mỗi ca ghi input, mode, source span, kết quả/cấu trúc mong đợi và điểm kiểm. Với lỗi phân tích phải kiểm AST hoặc vùng nguồn, không chỉ ảnh chụp LaTeX. Với lỗi thao tác phải kiểm trạng thái trước/sau.
- Chuyển các probe review thành test trong project hiện có: Core, Science, SmartChemistry, Application, Editor; R13 đặt trong công cụ kiểm phát hành Web. Không tạo framework kiểm thử mới.
- Lưu fixture từ baseline: candidate có sửa gợi ý, tài liệu nhiều vùng, phương trình đã cân bằng/tự điền, lựa chọn của người dùng và file hình học. Kiểm mở/lưu lại bằng bản mới không làm mất raw text hoặc quyết định đã lưu.
- Rà [Model.cs](../../src/Locus.Core/Model.cs) và [CandidateSetSerializer.cs](../../src/Locus.Core/Serialization/CandidateSetSerializer.cs): ID candidate phụ thuộc Core/Grammar; serializer kiểm phiên bản chặt. Chốt có cần đổi grammar/schema/migration trước khi đổi hằng phiên bản. Không đổi phiên bản đơn lẻ rồi làm mọi snapshot cũ không mở được.
- Dữ liệu đã lưu là ảnh chụp của quyết định cũ, không được tự phân tích lại khi mở. Nếu một snapshot thuộc ca sai cần sửa, giữ bản gốc và cung cấp đường phân tích lại có chủ ý; migration phải rõ, kiểm được và có thể quay lại. Thao tác lưu/mở không được âm thầm đổi công thức.
- Ghi giới hạn kiểm chứng: review trước có các suite hiện hành đạt nhưng thiếu 13 ca này; không cộng số test chồng lặp để tuyên bố độ phủ. Word runtime trong lượt review chưa chạy được do COM `80070520`.

**Đạt khi:** từng ca chứng minh lỗi của baseline và có kỳ vọng cụ thể; test không bị bỏ qua để làm xanh; đã chọn chiến lược tương thích có fixture bảo vệ. Test hồi quy mới có thể đỏ ở bước này, không đưa baseline đỏ lên luồng tự triển khai production.

## 4. FIX1 — Ưu tiên không làm sai công thức

### FIX1-A · R01/P1: không tách mất ngoặc của lời gọi hàm

**Ca lỗi:** `sin (x+1)` và `log (x+1)` trong Toán/Tất cả đang thành `sin x+1`/`log x+1`. Locator lấy riêng phần trong ngoặc rồi renderer bỏ ngoặc ngoài.

**Sửa tại:** [PassiveRegionLocator.cs](../../src/Locus.Core/Detection/PassiveRegionLocator.cs), [DomainAnalysisEngine.cs](../../src/Locus.Core/Detection/DomainAnalysisEngine.cs). Nhận biết ngữ cảnh lời gọi hàm đã biết nhưng grammar chưa hỗ trợ, giữ nguyên cả lời gọi; không nhận riêng đối số. Dùng quy tắc chung cho hai locator, không vá hai danh sách lệch nhau.

**Đạt khi:** `sin(x+1)`, `sin (x+1)`, `log (x+1)` và khoảng trắng biến thể giữ nguyên khi chưa hỗ trợ. `Tính (x+1)` vẫn nhận được công thức; không chặn mọi từ đứng trước ngoặc. Trong `sin (x+1); y=2`, phần đầu giữ nguyên, phần sau vẫn được nhận độc lập. Công thức trong cặp bọc không được vượt qua giới hạn grammar bằng cách cắt nhỏ.

### FIX1-B · R02/P1 + R07/P2: phạm vi số mũ và toán tử sau đơn vị

**Ca lỗi:** `10 kg*(m/s)^2` đang bình phương cả đại lượng gồm `10` và `kg`. `10 kg * 2` bị mất nhận diện; `10 m / t` lỗi ở chế độ Lý hoặc mất cách hiểu đơn vị khi chuyển sang Toán.

**Sửa tại:** [PhysicsTokenizer.cs](../../src/Locus.Core/Parsing/PhysicsTokenizer.cs). Phân tích từng nhân tử đơn vị, áp số mũ lên chính nhân tử đó, sau đó mới ghép tích/thương. Chỉ nhận `*` hoặc `/` vào đơn vị khi phía sau thực sự là một nhân tử đơn vị hợp lệ; nếu là toán hạng số học thì trả quyền xử lý cho parser số học. Lookahead không được làm mất token khi quay về.

**Đạt khi:**

- `10 kg*(m/s)^2` có nghĩa `10 × kg × ((m/s)^2)`, không bình phương `10` hoặc `kg`.
- `(10 kg)^2` vẫn bình phương cả đại lượng vì người dùng đã bọc ngoặc như vậy.
- `10 kg * 2` là phép nhân một đại lượng với số; `10 m / t` giữ `m` là đơn vị và `t` là biến chia.
- Kiểm đơn vị đơn/ghép, mũ âm, ngoặc lồng, toán tử liên tiếp và ngoặc thiếu. Input lỗi không được trở thành một kết quả direct mang nghĩa khác.
- AST, LaTeX và xuất ảnh cùng phản ánh một cách hiểu ở Lý/Tất cả; kiểm source span sau chuẩn hóa.

### FIX1-C · R03/P1 + R08/P2: phân biệt vế phải trống và vế phải đã có

**Ca lỗi:** `Phản ứng H2+O2= 2 H2O.` sau cân bằng xuất thành `Phản ứng 2H2+O2->2H2O 2H2O.`. `Phản ứng H₂+O₂=` không được nhận là phản ứng thiếu sản phẩm dù dạng ASCII hoạt động.

**Sửa tại:** [ReactionDraftLocator.cs](../../src/Locus.Core/Assistance/ReactionDraftLocator.cs), cùng điểm gọi phân tích đoạn và hỗ trợ Hóa. Quét đủ vế sản phẩm, gồm hệ số tách bằng khoảng trắng. Phân biệt ba trạng thái: **thực sự trống / có và phân tích được / có nhưng chưa hiểu hoặc lỗi**. Chỉ trạng thái đầu được suy sản phẩm. Tái sử dụng quy tắc ký tự Hóa/chuẩn hóa hiện có cho số dưới Unicode, không làm sai offset nguồn.

**Đạt khi:**

- Ca `H2+O2= 2 H2O` trong câu là một phương trình với đúng một vế phải; sau cân bằng không còn vùng sản phẩm trùng.
- `H₂+O₂=` trong câu hoặc nhiều dòng được nhận tương đương dạng ASCII trong Hóa/Tất cả.
- Các vế phải như `2`, `H2O+` hoặc chất chưa hỗ trợ được giữ nguyên kèm trạng thái chưa xử lý; không tự coi là trống rồi chèn kết quả khác.
- Kiểm hệ số liền/tách, nhiều chất, điện tích ion đang được hỗ trợ, dấu câu cuối câu, dòng mới và cặp bọc. Không kéo nội dung phương trình sau thành vế phải của phương trình trước.
- Chỉ suy phản ứng có kết quả đủ điều kiện trong catalog hiện tại; không biến sửa locator thành suy phản ứng tổng quát.
- Nút Cân bằng/Hủy cân bằng/Hủy tự điền giữ hợp đồng CT4: hủy cân bằng giữ sản phẩm đã tự điền; hủy tự điền riêng mới khôi phục vế thiếu gốc. Với sản phẩm người dùng đã nhập thì không xuất hiện quyền hủy tự điền giả.
- Undo/Redo, đổi lựa chọn và xuất SVG/PNG dùng đúng cùng một kết quả; source gốc và phần câu văn còn nguyên.

**Cổng FIX1:** ba ca P1 và hai ca cùng parser đạt hồi quy; không còn biến đổi sai được xác nhận ở các input này. Không phát hành chỉ vì hình preview trông hợp lý nếu AST hoặc nội dung xuất còn sai.

## 5. FIX2 — Nhận diện nhất quán và giữ văn bản

**Sửa tại:** [ProtectedTextRecognizer.cs](../../src/Locus.Core/Detection/ProtectedTextRecognizer.cs), [DomainAnalysisEngine.cs](../../src/Locus.Core/Detection/DomainAnalysisEngine.cs) và phần quy tắc token/ranh giới liên quan. Tách phân loại ký tự/alias có thể tái sử dụng; giữ grammar từng môn. Không dồn tất cả heuristic vào một regex lớn hoặc viết thêm danh sách toán tử riêng ở từng host.

| Lỗi | Ca bắt buộc | Kết quả sau sửa |
| --- | --- | --- |
| R04/P2 · nhầm phân số trong ngoặc là đường dẫn | `(x-1/2)`, `lc[(x-1/2)]`, `toan-[(x-1/2)]` | Nhận công thức; kiểm ngoặc mở/đóng đối xứng khi áp ngoại lệ bảo vệ đường dẫn. |
| R05/P2 · Tất cả bỏ công thức trong dấu nháy | `Tính “x^2+1”.`, nháy kép/thẳng/đơn và alias Việt | Nhận đúng vùng bên trong, giữ nháy và câu văn ngoài vùng. |
| R06/P2 · alias Việt bị lấy thành công thức con sai | `A1 trừ x^2`, `abc cộng x^2` | Không lấy đuôi `trừ x^2`/`cộng x^2` thành một công thức độc lập. |
| R09/P2 · toán tử Unicode không ngăn nhầm ngày | `Xét x · 28/09.`, `Xét x ≤ 28/09.` | Nhận biểu thức như các dạng `×`/`<=` tương ứng; ngày đứng trong văn xuôi vẫn là ngày. |

**Ma trận kiểm gọn:** mỗi ca chính đi qua core và `FormulaSession`; biến thể chỉ chạy các mode có liên quan. Có đối chứng URL, email, đường dẫn Windows/Unix, ngày tháng, câu văn có ngoặc, dấu trừ một ngôi hợp lệ, Unicode và cặp bọc tùy chỉnh. Không nhân mọi input với mọi tổ hợp không liên quan.

**Nguyên tắc nghiệm thu:**

- Bật thêm Lý/Hóa không làm mất công thức Toán rõ nghĩa. Nếu thêm môn tạo mơ hồ thực sự thì giữ quyền lựa chọn; không ép Tất cả luôn có cùng candidate hoặc tự chọn như Toán.
- Raw text và source span UTF-16 không thay đổi do chuẩn hóa dấu nháy, số dưới hoặc toán tử; chuỗi ngoài công thức giữ nguyên cả khoảng trắng/xuống dòng.
- Tối đa ba candidate; direct trước; gợi ý sửa không tham gia tự động chuyển đổi. `x^2+1/2` vẫn có direct đúng thứ tự phép tính và gợi ý `(x^2+1)/2` theo đặc tả.
- Cặp bọc đóng chưa đủ hoặc có lỗi/mơ hồ không được tự chuyển. Thay đổi nguồn/mode khi xử lý đang chạy phải hủy kết quả cũ.

**Cổng FIX2:** cả bốn ca đạt, các đối chứng bảo vệ văn bản vẫn đạt và luồng dán đoạn không cắt nhầm công thức.

## 6. FIX3 — Hình học

### R10/P2 · đường thẳng và tia phải giao đúng viewport

[GeometrySvg.cs](../../src/Locus.Application/GeometrySvg.cs) đang kéo dài cố định 3.000 px. Thay bằng phép giao đường/tia với biên vùng nhìn, phân biệt đường thẳng vô hạn, tia một phía và đoạn hữu hạn.

**Đạt khi:** đường qua `A=(100,0)`, `B=(101,0)` vẫn hiện `y=0` qua canvas mặc định; pan/zoom không làm biến mất phần giao. Kiểm ngang/dọc/xiên/song song biên, tia quay ra khỏi canvas và điểm trùng. SVG xuất khớp preview, không sinh NaN hoặc tọa độ giả để né trường hợp suy biến.

### R11/P2 · mặt lõm không được che khuất phần rỗng

Thay chia tam giác kiểu chung một đỉnh cho mọi mặt bằng triangulation đúng đa giác lõm trên mặt phẳng của mặt, chẳng hạn ear clipping sau khi chiếu sang tọa độ 2D của mặt. Kiểm mặt suy biến, tự cắt hoặc không phẳng; giữ dữ liệu và báo trường hợp không hợp lệ, không âm thầm đổi hình.

**Ca hồi quy:** mặt tại `z=1`, các đỉnh XY `[(0,0),(3,0),(3,3),(2,3),(2,1),(1,1),(1,3),(0,3)]`; nhìn từ trên, cạnh tại `z=0` nối `(1.4,2)` đến `(1.6,2)` nằm trong khe rỗng và phải nhìn thấy. Hiện validator chấp nhận mặt này nhưng renderer đánh dấu cạnh là khuất.

**Đạt khi:** ca mặt lõm đúng; mặt lồi vẫn đúng; đổi thứ tự chiều đỉnh/camera và cạnh nằm trên biên không phát sinh nét khuất giả. Không thu hẹp chức năng “Mặt qua các đỉnh” chỉ để che lỗi của mặt lõm hợp lệ.

### R12/P2 · Undo chỉ xử lý một thao tác

[GeometryWorkspace.razor.cs](../../src/Locus.Editor/GeometryWorkspace.razor.cs) hiện hủy gesture rồi tiếp tục Undo lịch sử, nên xóa thêm thao tác hoàn tất trước đó.

**Hành vi chốt:** khi đang dựng dở, Undo đầu tiên chỉ hủy thao tác đang dựng; Undo tiếp mới lùi lịch sử đã hoàn tất. Gesture bị hủy không tạo mục Redo vì chưa được commit. Trong lúc đang dựng, Redo không tự hủy gesture rồi tiến lịch sử khác. Sau khi kết thúc/hủy gesture, Redo lịch sử hoạt động bình thường.

**Đạt khi:** đặt xong một điểm, bắt đầu đoạn và đặt đầu thứ nhất, Undo vẫn còn điểm đã hoàn tất. Kiểm công cụ nhiều bước, kéo điểm, Escape, đổi công cụ và Undo/Redo tiếp theo. Save/restore không ghi dở một gesture đã hủy.

## 7. FIX4 — URL và cập nhật Web

**R13/P2:** [boot.js](../../src/Locus.Web/wwwroot/boot.js) thay URL hiển thị bằng `/releases/<id>/`, nhưng [stage.mjs](../../tools/web1/stage.mjs) chỉ xuất release mới. Sau deploy, người mở link release cũ bằng trình duyệt mới có thể gặp 404; cache ở máy cũ không giải quyết được.

**Hướng sửa:**

1. URL người dùng lưu/chia sẻ và HTML base là `https://ntduyliem-source.github.io/liemtaphat/`; các tài nguyên được trỏ riêng tới release cố định để tránh trộn phiên bản. Không đặt base vào release vì router Blazor yêu cầu URL ứng dụng nằm trong base.
2. Cung cấp đường khôi phục link `/liemtaphat/releases/<id-cũ>/` đã chia sẻ về trang ứng dụng ổn định trên GitHub Pages. Fallback chỉ áp dụng navigation ứng dụng nhận biết được; không biến mọi asset bị thiếu thành HTML hoặc che lỗi đường dẫn khác.
3. Giữ tab đang mở dùng bộ runtime/grammar/font của chính release đó. Cập nhật chỉ đổi cả bộ sau khi sẵn sàng; tải/cache lỗi không được kích hoạt một release thiếu file.
4. Giữ dữ liệu người dùng khi đổi release. Kiểm snapshot/migration FIX0; không dùng reload để âm thầm phân tích lại mọi kết quả đã lưu.

**Đạt khi:** dựng hai release liên tiếp và kiểm URL gốc, link cũ trên browser profile mới, tab cũ có service worker, refresh, offline và nút cập nhật. Không 404 ở link ứng dụng đã chia sẻ, không mất nội dung và không trộn asset. Kiểm cả base `/` local và `/liemtaphat/` như production. Chứng cứ phải gồm tình huống chưa có cache, không chỉ browser đã dùng bản cũ.

## 8. FIX5 — Kiểm tích hợp và phát hành

### Trình tự kiểm để không kéo dài vô ích

1. **Trong từng sửa lỗi:** chạy đúng ca hồi quy và đối chứng gần nhất.
2. **Kết thúc FIX1/FIX2:** chạy Core, Science, SmartChemistry và các nhóm Application/Editor bị tác động một lần trên cùng commit. FIX3 chạy kiểm hình học/history/export; FIX4 chạy kiểm staging/update.
3. **Trước phát hành:** chạy một lượt regression nền và một lượt sử dụng bản Web build từ đúng commit. Chỉ chạy lại khi có thay đổi, lỗi mới hoặc bằng chứng chưa đủ; không lặp toàn bộ vì số test chưa lớn.

### Hành trình trên bản Web

- Nhập công thức đơn và dán đoạn có chữ, Toán/Lý/Hóa, URL, ngày và cặp bọc. Đổi mode, chọn gợi ý, chọn một vùng/Chọn tất cả; kết quả không lấy từ source revision cũ.
- Cân bằng phương trình đầy đủ; tự điền phương trình thiếu vế phải; hủy cân bằng, hủy tự điền, Undo/Redo. Bật Toán/Lý thì nút Hóa ở trạng thái ghost theo CT4; Hóa/Tất cả giữ đúng phạm vi lệnh.
- Copy/tải SVG và PNG cho một công thức và đoạn có chữ. File mở được, đúng nội dung đang thấy, không lẫn nút hay nhãn UI.
- Đồ thị: nhập hàm đang hỗ trợ, đổi tham số, kéo/zoom và xuất một ca kiểm không bị ảnh hưởng. Hình học: dựng, kéo, Undo, lưu/mở và xuất; gồm ba ca FIX3.
- Refresh/cập nhật với tài liệu đã lưu; các quyết định còn nguyên hoặc có quy trình tương thích tường minh đã kiểm ở FIX0.

### Các sản phẩm khác

- **Desktop:** build riêng khi đưa các sửa core vào bản Desktop; kiểm nhập có IME, clipboard PNG/SVG sang ứng dụng khác, lưu/mở và Undo ở cửa sổ thật. Ghi tên gói và commit, không coi test browser là test bộ cài.
- **Word:** kiểm native equation, metadata, vùng nguồn, focus/IME, từ chối kết quả cũ và một lần Undo trả đúng trạng thái trước chuyển. Kiểm thêm nghi vấn Find giữ tiêu chí định dạng của lần tìm trước; đây là mục xác minh, chưa tính vào 13 lỗi đã xác nhận.
- Nếu Word COM hoặc công cụ máy không chạy được, ghi chính xác phần chưa nghiệm thu. Không chặn phát hành Web đã đạt vì Word chưa sẵn sàng; không tuyên bố Desktop/Word đã đạt theo Web.

### Cổng tự động và hồ sơ bàn giao

- Bổ sung các suite logic có thể chạy độc lập vào [.github/workflows/deploy-pages.yml](../../.github/workflows/deploy-pages.yml) trước publish; hiện workflow chủ yếu build và kiểm asset. Native Windows/Word có job hoặc biên bản riêng, không giả chạy trên Ubuntu.
- Test lỗi, build lỗi hoặc thiếu asset phải dừng deploy. Kiểm URL/cấu trúc staging và ca update có bằng chứng trước khi xuất artifact Pages.
- Mỗi bản bàn giao ghi commit, release ID, R01–R13 đã sửa/chưa sửa, lệnh kiểm và kết quả, fixture tương thích và giới hạn theo từng sản phẩm. Gắn link bản Web ổn định, không quảng bá link release tạm làm URL chính.
- Chuẩn bị rollback bằng artifact của bản ổn định trước đó; không đảo dữ liệu người dùng một cách mù quáng. Nếu schema đổi, rollback phải đi cùng chiến lược tương thích đã kiểm.
- Trạng thái DONE chỉ áp dụng cho đầu ra đã sửa và kiểm trong phạm vi. FIX5 ghi riêng Web/Desktop/Word; muốn đóng toàn bộ cổng host phải có bằng chứng thực tế hoặc phạm vi hoãn được ghi rõ, không tính phần thiếu là đã đạt.

## 9. Bước sau đợt sửa

Sau khi Công thức đã ổn định, chọn từng năng lực từ [audit core C01–C08](../design-system/CT4-CORE-AUDIT.md) để đặc tả và triển khai riêng. Ưu tiên xét hàm Toán thông dụng, chỉ số dưới, dạng số khoa học, cách gõ Hóa và catalog phản ứng theo nhu cầu thực tế; không cam kết hỗ trợ ngoài grammar chỉ vì UI đã có ô nhập.

Các mục trên giữ đặc tả nghiệm thu đã duyệt. Kết quả thực hiện và các cổng còn mở được cập nhật riêng trong [biên bản triển khai](REPAIR-VERIFICATION-20261013.md), không suy nghiệm thu Desktop/Word từ kết quả Web.
