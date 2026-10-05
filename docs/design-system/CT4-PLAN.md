# Kế hoạch CT4 cho trang Công thức

Ngày 05/10/2026. **Trạng thái: CT4-01…08 đã triển khai và kiểm Web local.** Kết quả, build và giới hạn tại [CT4-VERIFICATION.md](CT4-VERIFICATION.md). Mục tiêu là hoàn chỉnh luồng nhập → xem đúng cách hiểu → hỗ trợ Hóa có thể hủy → chọn và xuất ảnh trên Locus Web. Đầu vào là phản hồi mới nhất của người dùng và [kết quả rà soát Core/Application](CT4-CORE-AUDIT.md).

Baseline để so sánh: Web local `20261005-083635-640`, design system/catalog `0.1.0`, working tree hiện tại sau CT0–CT3. CT0–CT3 đã tách nền tảng và component; không được hiểu là các hành vi CT4 đã hoàn tất.

## Phạm vi và thứ tự ưu tiên

CT4 gồm sửa nhận diện/gợi ý còn thiếu, làm đúng Cân bằng/Hủy tự điền, thống nhất lựa chọn trong kết quả, và thêm xuất ảnh đoạn hỗn hợp. Giữ bố cục Studio, panel Nhận diện, vị trí Undo/Redo, typography và hiệu ứng đã duyệt. Không đưa lại fx trong kết quả, sample trong “Ý bạn là”, auto-balance checkbox, nháp, bộ chọn phạm vi hay HTML/TXT trên thanh tải chính.

Web là website. Desktop vẫn là ứng dụng cài riêng; thay thư viện phải kiểm tương thích, nhưng CT4 không phát hành bộ cài Desktop mới hoặc lấy kiểm Web thay nghiệm thu Windows. Đồ thị/Hình học chỉ kiểm không bị ảnh hưởng; Word, tài khoản, quota và hosting Internet nằm ngoài đợt.

Các yêu cầu người dùng đã nêu là đầu vào bắt buộc: tên và trạng thái nút Hóa; hủy hệ số/hủy tự điền đúng nguồn; gợi ý sửa `x^2+1/2`; bỏ Phạm vi xuất; Chọn tất cả tự bỏ trạng thái khi chọn riêng; tải ảnh. Phần dưới giữ đặc tả đã duyệt để đối chiếu; các quy ước từng là đề xuất ở lượt lập kế hoạch không phải hành vi baseline đã có. Kết quả thực hiện ghi riêng trong biên bản CT4.

| Thứ tự | Công việc | Phụ thuộc | Đầu ra |
| --- | --- | --- | --- |
| CT4-01 | Thống nhất trạng thái chọn và hợp đồng thao tác | Baseline và audit | Một selection state, bảng nút và ca hồi quy ban đầu. |
| CT4-02 | Cân bằng, tự điền, hủy và draft trong câu | CT4-01 | Lệnh có phạm vi rõ, hủy đúng nguồn, UI đúng môn. |
| CT4-03 | Sửa detector và mở rộng repair của Toán | Baseline; quyết định tương thích snapshot | Nhận đúng phép trừ/chia; gợi ý cấu trúc có giới hạn và có edit. |
| CT4-04 | Hoàn chỉnh hàng Ý bạn là | CT4-01, CT4-03 | Gợi ý của nội dung thật, không lặp direct/sample. |
| CT4-05 | Đổi môn và nhận diện lại | CT4-01; phối hợp CT4-02/03 | Một giao dịch đổi mode + kết quả, giữ quyết định hợp lệ. |
| CT4-06 | Dựng ảnh từ phần kết quả được chọn | CT4-01; chốt dạng snapshot | Composer text + công thức → SVG → PNG. Đây là phần bổ sung lớn nhất. |
| CT4-07 | Thanh Chọn tất cả, tải và copy ảnh | CT4-06 | Nút ảnh thống nhất cho một vùng, đoạn và toàn bộ kết quả. |
| CT4-08 | Catalog, kiểm tích hợp và bàn giao local | Các mục trên | Build Web, catalog, bằng chứng và danh sách giới hạn cùng phiên bản. |

Triển khai theo từng đầu ra có thể xem được, không đợi mọi phần xong mới có demo. CT4-03 có thể chuẩn bị sau khi chốt hợp đồng mà không đợi phần xuất ảnh.

## Hợp đồng lựa chọn và thao tác

Một nguồn trạng thái `None / Region / Range / All` giữ span, ID vùng và epoch. Không giữ thêm `articleScope` độc lập rồi suy phạm vi lần nữa trong mỗi component. Phân biệt ý định All với việc một formula tình cờ chiếm toàn bộ chiều dài tài liệu.

| Trạng thái | Cân bằng và gợi ý | Copy và tải ảnh |
| --- | --- | --- |
| Không chọn | Cân bằng xét toàn kết quả. Nếu chỉ có một vùng công thức thì “Ý bạn là” dùng vùng đó. | Đề xuất mặc định xuất toàn kết quả; một công thức đơn dùng ngay được. |
| Chọn một công thức | Hóa tác động riêng vùng đủ điều kiện; gợi ý theo vùng đó. Chọn Toán/Lý không chuyển lệnh sang một phản ứng khác ngoài vùng chọn. | Chỉ ảnh công thức được chọn. Chọn tất cả về trạng thái thường. |
| Bôi chọn đoạn không cắt dở công thức | Hóa chỉ xử lý các vùng trọn vẹn trong đoạn; không tự chọn một vùng ngoài đoạn. “Ý bạn là” chỉ hiện khi xác định đúng một mục tiêu. | Ảnh gồm đúng text và công thức trong đoạn. |
| Chọn tất cả | Xét toàn bộ kết quả; giữ nguyên vùng không đủ điều kiện. | Toàn bộ kết quả, bao gồm chữ/xuống dòng. |
| Chọn dở một công thức | Chưa thực hiện lệnh Hóa trên selection này; nhắc chọn trọn, không mở rộng phạm vi ngầm. | Chặn xuất cắt dở công thức đang dựng và nhắc chọn trọn; không chuyển sang xuất cả bài. |

Click nền/Escape bỏ chọn; nút Chọn tất cả phản ánh đúng trạng thái All. Đề xuất bấm lại nút All để bỏ chọn. Bôi chọn text bằng chuột và chọn bằng bàn phím vẫn dùng được. Nguồn mới, đổi tài liệu hoặc vị trí vùng hết hiệu lực làm mất selection cũ. Chuyển từ All sang Region phải cập nhật đồng thời highlight, gợi ý, lệnh Hóa và export.

Không dựa vào `innerText` của MathML để suy offset nguồn. Tiếp tục dùng source span và ID có sẵn. Không gắn nút hoặc nhãn trạng thái vào nội dung sẽ được xuất.

## CT4-01 Thống nhất trạng thái chọn

**Việc làm:** thay hai biến phạm vi hiện tại bằng state thống nhất; tạo phép chiếu mục tiêu cho gợi ý, Hóa và xuất. Target của Hóa có thể là danh sách ID chụp tại thời điểm bấm, không chỉ `Guid?` với null nghĩa toàn bài. Candidate lỗi/chưa có Display không mặc nhiên là Hóa.

**Nơi sửa:** `Workspace.Content.cs`, `Workspace.Studio.cs`, `FormulaResult`, `Formula/Presentation`, bridge `result-selection.js`; phần xác thực mục tiêu thuộc Application.

**Đạt khi:** một click formula sau All đồng thời đổi mọi thao tác về formula đó; kéo selection ngược, emoji và công thức dài không sai offset; selection dở không bị nới ngầm; chọn đối tượng không ghi một bước Undo nội dung giả.

**Đối chiếu audit:** A09, A12.

## CT4-02 Cân bằng và hủy có ý nghĩa

### Nút và thông điệp

- Nhãn lúc sẵn sàng: **Cân bằng phương trình hóa học**; giữ vị trí nhóm điều khiển theo thiết kế.
- Chỉ All/Hóa cho phép lệnh. Toán/Lý hiển thị ghost mờ và không kích hoạt qua chuột/phím. Với MANUAL, phương án bám đúng yêu cầu “chỉ All/Hóa” là ghost; cặp Hóa vẫn nhận diện được, người dùng chuyển sang All/Hóa để dùng nút. Không dùng bộ lọc ẩn từ trước để quyết định trạng thái nút.
- Hint: **Nhập a+b= rồi bấm Cân bằng để tự điền sản phẩm phản ứng và cân bằng phương trình.**
- Chưa chọn vẫn dùng được ở All/Hóa. Không có phản ứng phù hợp thì báo ngắn, không sửa vùng khác hoặc tạo history giả. Busy/IME có trạng thái riêng và chặn kết quả dở.
- Domain guard nằm cả ở lệnh Studio và UI; kiểm lại trước commit. Không đổi luật routing cặp chỉ định môn của core/Word chỉ để làm mờ nút Web.

### Trạng thái từng phương trình

| Nguồn và lịch sử | Hành động đưa ra | Kết quả |
| --- | --- | --- |
| Có đủ hai vế, Locus chưa đổi hệ số | Cân bằng phương trình hóa học | Đổi hệ số nếu cần, giữ chất/thứ tự và nguồn. |
| Nguồn `H2+O2=` có một mẫu phù hợp | Cân bằng phương trình hóa học | Điền sản phẩm và cân bằng trong một giao dịch; ghi riêng sản phẩm và hệ số. |
| Locus đã đổi hệ số | Hủy cân bằng | Khôi phục hệ số trước lệnh, giữ sản phẩm đã điền. |
| Sau hủy hệ số và còn sản phẩm do Locus điền | Hủy tự điền | Trở lại nội dung vùng gốc, gồm cặp bọc nếu có; các vùng khác không đổi. |
| Chỉ điền sản phẩm, hệ số không cần đổi, ví dụ `HCl+NaOH=` | Hủy tự điền | Không bắt người dùng qua một bước hủy hệ số không làm gì. |
| Nguồn đã đủ sản phẩm và cân bằng sẵn | Báo Đã cân bằng | Không gắn ManagedBalance mới; không xuất hiện Hủy cân bằng/Hủy tự điền giả. |
| Sản phẩm có sẵn trong input | Không có Hủy tự điền | Tuyệt đối không xóa sản phẩm người dùng gõ. |

Hủy tự điền xuất hiện trực tiếp trong nhóm thao tác của vùng được chọn, không giấu sau hai lần mở Chi tiết. Sau hủy hệ số vẫn có thể cân bằng lại bằng nút chính; hành động Hủy tự điền nằm cùng nhóm điều khiển, ngoài nội dung kết quả. Không thêm checkbox tự cân bằng.

Khi không chọn/All và đang có hệ số do Locus quản lý, giữ hành vi đã yêu cầu: nút chuyển Hủy cân bằng, hủy những hệ số đó trong phạm vi, giữ sản phẩm. Với trạng thái trộn, lần hủy không tác động các phương trình chưa cân bằng; muốn cân bằng riêng một phương trình mới có thể chọn nó. Hủy tự điền chỉ áp dụng vùng được chọn trong đợt này, không tự thêm lệnh xóa sản phẩm cả bài.

**Điều kiện bất biến:** nguồn không bị thay vì bấm hỗ trợ; thông tin phục hồi lấy từ provenance/snapshot, không suy đoán bằng so sánh chuỗi hiển thị. Nếu input/selection/mode thay trong lúc chờ, hủy commit. Một Undo trả đúng trạng thái trước giao dịch; lỗi/hủy không ghi dở vài vùng.

### Việc còn thiếu phía trong

1. Tách xác định thao tác khỏi thực thi: balance/fill, cancel coefficients, drop products; kiểm state tại lúc commit. Các tên API mới được chọn khi triển khai, không nối nút bằng đổi nhãn rồi gọi lại lệnh toggle mơ hồ.
2. So sánh hệ số thực trước/sau; không biến `AlreadyBalanced` thành `ManagedBalance=true`. Nếu chỉ có sản phẩm mới thì ghi product override, không tạo balance override giả.
3. Nhận draft `a+b=` có kiểu rõ; bổ sung locator cho `Xét H2+O2= trong bài.`. Bao đúng dấu `=`, không lấy phần câu sau vào phương trình. Cặp chưa đóng giữ trạng thái chưa hoàn tất. Ngoài các ranh giới có bằng chứng rõ thì giữ text, không đoán.
4. Trả kết quả theo từng region ID: số vùng đổi hệ số, số vùng điền sản phẩm, đã cân bằng sẵn, cần điều kiện, chưa có dữ liệu và lỗi. UI có tổng kết gọn; lý do/điều kiện chi tiết tra được ở đúng vùng.
5. Dùng lại 45 record và solver. Mẫu duy nhất có thể điền theo điều kiện của mẫu như baseline, nhưng điều kiện đó phải đọc lại được; `NaOH+CO2=` chưa rõ tỉ lệ thì giữ nguyên. `N2+H2=` chưa có dữ liệu thì nói rõ, không gọi là không phản ứng.
6. Với dữ liệu cũ gắn ManagedBalance dù hệ số không đổi, lớp trạng thái không đưa ra hủy giả; giữ khả năng mở/Undo lịch sử cũ. Chỉ migration nếu cấu trúc file thật sự cần thay và đã có fixture.

**Nơi sửa:** `FormulaSession.StudioBalance.cs`, `FormulaSession.Balance.cs`, `FormulaSession.ContentProducts.cs`, `ContentDocument.cs`/draft locator, `ChemistryAssistanceWire.cs`, `Workspace.Balance.cs`, `FormulaDetectionBar`, `FormulaDetails` và presentation.

**Đạt khi:** chuỗi `H2+O2=` → cân bằng → chọn → hủy hệ số → hủy tự điền chạy đủ; `3H2+O2=` khôi phục hệ số 3, không đặt tất cả về 1; chọn riêng không ảnh hưởng vùng khác; phương trình có sản phẩm gõ sẵn và phương trình cân bằng sẵn không có hủy giả; Toán/Lý không thực thi lệnh Studio.

**Đối chiếu audit:** A06–A11.

## CT4-03 Sửa nhận diện và gợi ý ở Core

### Nhận diện đường dẫn

Sửa A02 trước khi mở rộng repair cho hiệu. `x-1/2` phải đi qua parser như `x - 1/2`; cặp `lc[x-1/2]` phải xác nhận ý định công thức. Tách rõ bằng chứng phép toán với đường dẫn tương đối. URL/email, đường dẫn ổ đĩa/UNC/absolute và `data-set/1` vẫn được bảo vệ; không bỏ toàn bộ lớp bảo vệ trong explicit mode.

### Quy tắc repair cần có trong đợt này

| Input | Kết quả trực tiếp | Ý bạn là |
| --- | --- | --- |
| `x^2+1/2` | `x^2 + (1/2)` | `(x^2+1)/2` |
| `x*y+1/2` | `x*y + (1/2)` | `(x*y+1)/2` |
| `x-1/2` | `x - (1/2)` | `(x-1)/2` |
| `x+1/y` | `x + (1/y)` | `(x+1)/y` |
| `can x+1` | `sqrt(x)+1` | `sqrt(x+1)` |
| `can x-1` | `sqrt(x)-1` | `sqrt(x-1)` |
| `(x+1` | Giữ text vì chưa có direct hợp lệ | `(x+1)` với nhãn Bổ sung ngoặc đóng |
| `can(x+1` | Giữ text vì chưa có direct hợp lệ | `sqrt(x+1)` với nhãn Bổ sung ngoặc đóng |
| `(x^2+1)/2` | Phân số người dùng đã nhóm | Không gợi ý tháo ngoặc hoặc lặp lại cùng kết quả |

Các ví dụ xác lập nhóm cấu trúc, không triển khai bằng danh sách thay chuỗi đúng vài mẫu. Repair chạy trên AST và spans; mỗi đề xuất là một edit có giải thích. Giới hạn một vị trí sửa độc lập, tổng candidate không quá ba gồm direct/interpretation/repair; loại đề xuất trùng hình thức/cấu trúc. Không ghép nhiều lỗi thành một phép “đoán hộ” khó kiểm.

Ưu tiên direct hợp lệ, rồi cách hiểu hợp lệ, rồi repair. Những case có nhiều vị trí mơ hồ vẫn yêu cầu làm rõ. Gợi ý không làm thay đổi nguồn, không được tự áp dụng hoặc tự chuyển Word. Thêm repair có thể làm eligibility auto thay đổi; kiểm rõ thay vì nới điều kiện tự động.

Không đưa `sin(x)` vào ca phải đạt CT4: parser Công thức chưa có function node. C01 trong audit là công việc grammar riêng phải làm trước khi có repair chứa hàm.

### Version và snapshot

Trước sửa, lưu fixture candidate/direct/repair đã chọn cùng source/ID/MathML/OMML. Xác định thay đổi là chính sách gợi ý hay grammar/schema; ghi phiên bản triển khai tương ứng. Nếu bump core/grammar thì reader phải đọc được version cũ và giữ nguyên snapshot/candidate đã chọn; không chỉ đổi constant vì serializer hiện kiểm version chặt. Mở file/Undo không reparse theo luật mới. Chỉ nguồn mới hoặc thao tác nhận diện lại có chủ đích dùng luật mới.

**Nơi sửa:** `ProtectedTextRecognizer`, `ProfileAnalysisEngine`/`AnalysisEngine` khi cần truyền ý định, `FormulaParser` hoặc module scope-repair tách riêng; model/serializer chỉ sửa khi hợp đồng yêu cầu. Thêm corpus và parser/detector contract tests.

**Đạt khi:** các hàng trên đạt; direct tuân thủ precedence; input đã đặt ngoặc không bị gợi ý ngược; URI/path/email giữ nguyên; source edits và Unicode đúng; snapshots cũ mở đúng. UI không tự dựng một phiên bản repair khác với core.

**Đối chiếu audit:** A01–A03, A13.

## CT4-04 Ý bạn là đúng ngữ cảnh

- Bỏ sample và callback thay nguồn khỏi hàng này. Không đưa direct đang hiển thị thành một gợi ý lặp lại.
- Một công thức: tự lấy ngữ cảnh sau khi phân tích xong. Nhiều công thức: theo vùng được chọn; chưa chọn thì lời nhắc ngắn hoặc hàng trống, không đề xuất công thức ngẫu nhiên.
- Render candidate thật bằng cùng cấu trúc sẽ dùng khi người dùng nhận; giải thích ngắn như “Đưa cả tổng vào tử số”, “Mở rộng phạm vi căn”, “Bổ sung ngoặc đóng”. Cách hiểu hợp lệ và sửa cú pháp có nhãn phân biệt.
- Khi đã nhận repair, có thao tác trở lại “Theo cú pháp đã gõ” hoặc Undo; không ghi đè nguồn. Khi không có direct vì input thiếu ngoặc, trở về text gốc.
- Giữ thao tác Giữ text/Chi tiết đúng vùng, nhưng trình bày tách khỏi các candidate. Busy/IME không cho chọn một gợi ý thuộc kết quả cũ.

**Nơi sửa:** `FormulaSuggestions`, `CandidateLabels`, `Workspace.Studio.cs`, binding trong `Workspace.razor`, catalog case tương ứng. Dữ liệu gợi ý đến từ Core; labels không tái phân tích LaTeX.

**Đạt khi:** gõ `x^2+1/2` thấy output đúng quy tắc và gợi ý phân số mới; bấm gợi ý/Undo không mất text; bài nhiều vùng không nhận repair vào vùng khác; không còn sample hoặc bản direct lặp trong hàng.

## CT4-05 Đổi môn có kết quả ngay

Thêm lệnh nhận diện lại theo settings hiện tại, bao gồm commit settings và kết quả thành một bước Undo. Không sửa toàn bộ ý nghĩa `Configure` đang dùng cho phục hồi lịch sử chỉ để đáp ứng UI Web. IME chờ hoàn tất; đổi mode liên tiếp hủy yêu cầu cũ.

Giữ source nguyên văn. Vùng còn khớp raw/span/cấu trúc giữ lựa chọn, giữ text và lịch sử hỗ trợ hợp lệ. Vùng đổi ý nghĩa hoặc hết đủ điều kiện không mang theo override Hóa cũ; giữ text và cho người dùng xem lại. Kiểm `ContentDocument.Reconcile` vì giữ snapshot mù quáng sẽ làm phản ứng cũ lọt qua bộ lọc mới.

Phương án MANUAL cho đợt này: chỉ xét cặp đã đóng; cặp chung dùng ba môn nhất quán, không phụ thuộc môn được chọn trước khi vào MANUAL; cặp riêng chỉ định môn như core đã có. Giữ các cặp người dùng tùy chỉnh. Nút Hóa trong MANUAL tuân theo bảng ở CT4-02, tách khỏi việc marker có nhận diện được hay không.

**Đạt khi:** All → Toán với `H2SO4` bỏ nhận diện Hóa trên nguồn không bọc; Undo khôi phục All và đúng kết quả trước; đổi qua lại giữ text/repair còn hợp lệ; cặp chỉ định môn và file lịch sử không bị sửa ngoài ý muốn.

**Đối chiếu audit:** A05, A06, A13.

## CT4-06 Ảnh cho cả đoạn kết quả

Đây là tính năng còn thiếu, không thể hoàn thành bằng việc hiện lại nút PNG. Dùng các khối sẵn có như sau:

1. Chụp `ContentExport` hoặc snapshot tương đương từ state và selection hiện tại: text runs, candidate đã chọn, line breaks, ID/revision. Chia sẻ phép chiếu bất biến với các exporter hiện có; không expose collection có thể sửa.
2. Renderer mỗi công thức trả SVG và số đo cần layout gồm chiều rộng, ascent/descent/baseline. Dùng outline hiện có của MathJax, không reparse source để chọn lại kết quả.
3. Bổ sung compositor cho text + SVG công thức: giữ xuống dòng, khoảng trắng và baseline; xuống dòng mềm theo chiều rộng nội dung preview được chụp lúc xuất. Một công thức riêng dùng kích thước tự nhiên như hiện tại.
4. Spike kỹ thuật bắt buộc trước khi gắn UI: hai dòng tiếng Việt + phân số/căn + phản ứng Hóa + emoji. Chọn cách đóng gói chữ/font để SVG mở độc lập không phụ thuộc font máy nhận/CDN. Ưu tiên glyph path; nếu cần fallback phải kiểm SVG độc lập và PNG thực. Không dùng chụp màn hình vùng đang thấy làm ảnh cả bài.
5. Sinh SVG độc lập và PNG 2X/4X/copy PNG từ cùng bố cục. SVG không chứa selection, nút UI, fx hoặc tham chiếu glyph chéo bị trùng ID. Theme tối không làm chữ xuất ra không đọc được.
6. Giữ trần ảnh hiện hành làm điểm khởi đầu; kiểm kích thước trước cấp phát. Vượt trần PNG báo rõ và cho dùng SVG/chọn đoạn ngắn hơn, không cắt nội dung hoặc âm thầm hạ 4X xuống 2X.
7. Chụp lease cho source/candidate/selection/render options; kiểm lại trước tải/clipboard. Nếu đang xử lý, nguồn/selection đổi hoặc renderer lỗi, không xuất một ảnh cũ.

**Nơi sửa:** `ContentExport.cs` hoặc module image projection trong Application, `Formula/Presentation/FormulaImageRequest`, module compositor mới cạnh `renderer.js`, `Workspace.Core.cs` hoặc coordinator export tách riêng. Adapter Web tiếp tục quản lý download/clipboard; Core không nhận DOM/font/UI.

**Đạt khi:** một công thức, nhiều công thức, đoạn chỉ có chữ và đoạn chữ + công thức đều tải/copy đúng phần chọn; giữ chữ Việt/xuống dòng/emoji; PNG có số đo đúng scale; SVG mở độc lập; đoạn dài ngoài viewport không bị mất. Không có glyph thay thế hoặc dòng bị cắt trong các mẫu nghiệm thu.

**Đối chiếu audit:** A12.

## CT4-07 Thanh xuất gọn

Thanh chính chỉ còn **Chọn tất cả · SVG · PNG 2X · PNG 4X · Copy ảnh**. Giữ phong cách, font và hover của mẫu. Bỏ nhãn Phạm vi xuất, nút Công thức đang trỏ, nhãn Toàn bộ bài viết và nhánh HTML/TXT/copy văn bản thay thế ảnh.

Nút ảnh áp dụng cho cùng selection state; không đổi tập định dạng theo số công thức. Tooltip nói đúng nội dung sẽ xuất. Chọn một formula sau All bỏ highlight toàn bộ và chỉ xuất formula đó. Copy bị trình duyệt từ chối có thông báo và đường tải PNG sẵn trên thanh. Chỉ báo thành công khi adapter xác nhận.

Các exporter DOCX/HTML/text ở Application không bị xóa chỉ vì thanh Công thức đổi; chúng có thể còn phục vụ luồng khác. Không tự thêm chúng lại vào một menu mới của trang này. Quyết định đưa DOCX vào sản phẩm về sau có đặc tả riêng.

**Nơi sửa:** `FormulaExportBar`, selection presentation, coordinator export, adapter Web nếu cần. Xóa binding/sample/state cũ không còn dùng ở trang, không để nhánh UI chết chồng lên thiết kế mới.

**Đạt khi:** không còn Phạm vi xuất/HTML/TXT trên thanh; All/Region/Range/None đồng nhất giữa highlight và file/clipboard; thao tác không cần vào Chi tiết mới tải được ảnh.

## CT4-08 Catalog và bàn giao

Catalog tiếp tục render component thật, thêm đủ trạng thái: nút Hóa ghost/ready/loading; đủ sản phẩm/chỉ tự điền/đổi hệ số/đã cân bằng sẵn; đề nghị sửa phân số/căn/ngoặc; selection none/all/region/range/dở; xuất ảnh thành công/thất bại/vượt trần.

Kiểm sáng/tối, rộng/hẹp, bàn phím/focus và giảm chuyển động cho component bị đổi. Không thay typography để che tràn chữ của nhãn nút dài. Cập nhật mô tả component, nguồn token, changelog và hướng dẫn bảo trì. Web và catalog dùng cùng build tài nguyên.

## Ma trận nghiệm thu

Các ca này là yêu cầu cho build CT4; kết quả thực tế và phần chưa kiểm xem CT4-VERIFICATION.md. Chạy kiểm liên quan khi hoàn tất từng cụm; một lượt tích hợp cuối trên cùng build, không lặp toàn bộ solver sau mỗi sửa CSS.

| Nhóm | Ca bắt buộc | Chứng cứ |
| --- | --- | --- |
| Core precedence/repair | `x^2+1/2`, `x*y+1/2`, `x-1/2`, `x+1/y`, căn +/−, một ngoặc thiếu, ngoặc đã đủ, nhiều vị trí mơ hồ | Candidate kind/order/AST/edits, snapshot và exporter. |
| Detector | Các ca trên có/không cặp; URL/email/`data-set/1`/đường dẫn ổ đĩa/ngày tháng, đoạn văn có emoji | Span chính xác, false positive không tăng trong corpus đối chứng. |
| Domain/IME | All/Hóa/Toán/Lý/MANUAL; đổi mode khi đang gõ ghép dấu và khi request cũ chưa trả | UI trạng thái đúng, chỉ kết quả phiên mới được nhận. |
| Hóa cơ bản | `H2+O2=`, `3H2+O2=`, nguồn đủ sản phẩm, nguồn đã cân bằng sẵn, `HCl+NaOH=` | Snapshot trước/sau, số vùng thực đổi, không hủy giả. |
| Hóa giới hạn | `NaOH+CO2=`, `N2+H2=`, `h2+o2=h20`, phản ứng không cân bằng được, cặp chưa đóng | Giữ nguyên; lý do đúng; vẫn chọn/xuất được nội dung hiện có. |
| Draft và vùng | Draft đầu/cuối/giữa câu, draft riêng dòng/có cặp; chọn một trong nhiều phản ứng; chọn text hoặc Toán trong bài có Hóa | Đúng vùng, không nuốt câu, không tác động ngoài selection. |
| Hủy và lịch sử | Cân bằng → hủy hệ số → hủy tự điền; cân bằng lại; Undo/Redo; lưu/khôi phục snapshot cũ và mới | Hệ số, sản phẩm, raw, decisions và candidate khớp. |
| Ý bạn là | Không chọn một formula đơn; chọn một trong nhiều vùng; nhận repair rồi quay về direct; input chỉ có repair | UI/candidate/output cùng một kết quả, không sample/trùng. |
| Selection | All → Region, chọn ngược, text-only, partial formula, đổi nguồn khi chọn | Highlight, phạm vi lệnh và phạm vi xuất đồng nhất. |
| Ảnh | Một formula, nhiều formula, text + formula, chữ Việt/emoji, đoạn dài ngoài viewport, 2X/4X và vượt trần | Tệp SVG/PNG thực, kích thước và kiểm ảnh mở độc lập. |
| Clipboard/lỗi | Dán PNG sang nơi nhận hỗ trợ ảnh; clipboard bị chặn, font/render lỗi, sửa nguồn khi đang xuất | Dữ liệu clipboard thật hoặc ghi rõ chưa kiểm; không dùng toast thay bằng chứng. |
| Hồi quy | Core/Application/serialization liên quan; Web worker tương đương native; Desktop build; smoke hai tab vẽ | Báo cáo đúng phạm vi, không nhận Desktop/Word host đã đạt chỉ từ build. |

Chạy runner sẵn có theo phạm vi: Core parser/detector/serialization; Science; SmartChemistry input/balance/catalog; Application content/studio-balance/document-export; Editor DOM và bridge. Khi mở rộng grammar phải bổ sung corpus và kiểm worker dùng đúng assembly/fixture cùng release. Không cập nhật kỳ vọng test để chấp nhận lỗi cũ như “AlreadyBalanced cũng được quản lý”.

## Điều kiện hoàn tất và công việc tiếp sau

CT4 chỉ hoàn tất khi cả tám đầu ra đạt ma trận tương ứng, A01–A13 có bằng chứng xử lý, giao diện/candidate/file xuất nhất quán và mở lại được snapshot cũ. Hạng mục chưa kiểm phải ghi rõ, không dùng tỷ lệ task để che phần xuất ảnh hoặc hủy còn thiếu. Bàn giao một Web local + catalog cùng build, biên bản và danh sách giới hạn. CT5 là kiểm/bàn giao sản phẩm tiếp theo theo [kế hoạch tổng](PLAN.md), không trùng việc chạy vô hạn các test đã qua.

Sau CT4, lấy backlog C01–C08 của audit để lập đợt mở rộng lõi: ưu tiên hàm/chỉ số/tích chữ liền/số khoa học và ký hiệu Hóa phổ biến; kho phản ứng có đợt biên soạn riêng. Mỗi cú pháp mới phải đi đủ tokenizer/parser → model/serialization → MathML/LaTeX/OMML → UI/corpus, không chỉ hiển thị được một ví dụ.

**Đã thực hiện:** CT4-01…08, Web `20261005-104949-048`, catalog `0.2.0`. Không sửa bằng chứng baseline thành kết quả mới. Các giới hạn native Windows, grammar nâng cao và kiểm lỗi cưỡng bức được phân biệt trong biên bản bàn giao.
