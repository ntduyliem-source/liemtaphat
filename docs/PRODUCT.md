# Hợp đồng sản phẩm Locus

Ngày lập: 2026-09-12. Cập nhật kế hoạch: 2026-09-15. Đây là đường cơ sở để triển khai; các đề xuất và quyết định còn mở được đánh dấu riêng.

**Baseline và kế hoạch mới:** [thành quả hiện có](BASELINE-20260915.md), [roadmap](ROADMAP.md), [đợt tiếp theo](NEXT-STEPS.md). Người dùng yêu cầu viết kế hoạch, chưa code. Các yêu cầu đũa thần/đoạn dài/tray/quét Word/quota trong phần 3.9 là mở rộng sau baseline, chưa được gọi là tính năng đã nghiệm thu.

**Triển khai hiện tại: Web/Desktop dùng chung editor; E3 đã có nhập Hóa/Lý cơ bản và Word alpha thủ công trên baseline x86.** [WEB1](web1/REPORT.md) hỗ trợ nhập/chọn, cặp dấu, xuất SVG/PNG/văn bản, lưu/mở, nháp và offline trong phạm vi local; [E3](e3/REPORT.md) thêm ba checkbox và quy tắc Hóa/Lý. [M3](m3/REPORT.md) và E3 Word hỗ trợ chọn vùng, preview/xác nhận, native, Undo/restore/detach. G1 đạt cho alpha thủ công; G2/G3 và các mở rộng bên dưới tiếp tục theo roadmap.

**WEB0 đã đạt prototype kiến trúc; SH/WEB1 đã hoàn thành editor công thức chung:** core WASM và WPF Hybrid có bằng chứng parity/nhập liệu theo baseline đã kiểm. [Bằng chứng WEB0](web/COMPATIBILITY.md), [editor chung](sh/REPORT.md). Hosting online hiện hoãn theo yêu cầu; đồ thị thử chưa đồng nghĩa E1 đã phát hành.

## 1. Mục tiêu và người dùng

Lời hứa cốt lõi: **gõ theo thói quen → thấy công thức đúng ý → đưa vào tài liệu → sửa hoặc khôi phục được**.

Sản phẩm phục vụ giáo viên, học sinh/sinh viên và người viết tài liệu kỹ thuật. Không giới hạn quyền sử dụng theo nhóm người dùng. Kế hoạch ưu tiên theo tác vụ dùng chung, trước tiên là nhập công thức Toán và sử dụng trong Microsoft Word.

Desktop và Web hoạt động độc lập với Word. Sản phẩm có ba không gian theo từng đợt phát hành: Công thức, Đồ thị và Hình học; Toán/Lý/Hóa là các miền trong không gian công thức.

**Kế hoạch cập nhật ngày 2026-09-15:** M3/WEB0/SH/WEB1/E3 và SC1-01…07 đã đạt phạm vi tương ứng. Tiếp theo UX1 giao diện gọn → DOC1 nguyên đoạn → BAL1 đũa thần/hoàn tác/auto → export/tray/SC1 Word và gói → WD1 quét tài liệu → E1 đồ thị/thanh trượt → E2A/E2B → WEB2 tài khoản/quota. [Roadmap](ROADMAP.md) phân biệt ưu tiên với phụ thuộc. Web vẫn local, cùng core/logic với Desktop; Word auto giữ cổng riêng, không để W0 chặn các phần độc lập.

## 2. Quy ước về mức độ chốt

- **Yêu cầu nền:** có trong mô tả của người dùng và được dùng làm ràng buộc triển khai.
- **Đề xuất:** phương án thiết kế hiện tại, cần được kiểm tra bằng ví dụ hoặc prototype.
- **Baseline kỹ thuật:** lựa chọn đủ cụ thể để triển khai một phiên bản và kiểm tra bằng corpus; có thể điều chỉnh theo bằng chứng. Trạng thái này không có nghĩa người dùng đã phê duyệt riêng từng alias hoặc tính năng đã chạy đạt.
- **Còn mở:** chưa có quyết định; không được âm thầm biến thành hành vi mặc định.

Việc người dùng yêu cầu lập roadmap không tự động chốt mọi đề xuất từ cuộc brainstorming trước.

## 3. Yêu cầu nền

### 3.1. Nguồn, nhận diện và phân tích

- Mọi kênh nhập đi qua cùng lõi xử lý trên Desktop, Web và Word. Thư viện editor/đồ thị nhận cấu trúc hoặc dữ liệu đã phân tích, không diễn giải lại nguồn người dùng bằng một parser cạnh tranh.
- Giữ nguyên chuỗi nguồn, kể cả dấu tiếng Việt, khoảng trắng và cách viết ban đầu. Bản chuẩn hóa chỉ phục vụ xử lý.
- Mỗi thành phần có ánh xạ về nguồn; mọi offset phải công bố hệ quy chiếu. Không giả định offset của chuỗi chuẩn hóa bằng offset trong Word.
- Nhận diện có quyền từ chối. Văn bản thông thường, URL, email và đường dẫn không được chuyển chỉ vì có chữ số hoặc dấu giống toán.
- Xử lý được công thức nằm xen trong câu. Đơn vị quyết định là vùng nguồn cụ thể; không loại toàn bộ câu chỉ vì câu có văn xuôi.
- Phạm vi cú pháp được công bố theo phiên bản. Không đồng nhất “gõ tự nhiên” với hiểu mọi câu tiếng Việt.
- Không suy ra khả năng giải bài, kiểm chứng định lý hoặc sửa kiến thức khoa học từ khả năng dựng công thức.

### 3.2. MathDocument và các kết quả

- Cách hiểu trực tiếp theo quy tắc cú pháp được đặt đầu tiên khi chuỗi có một cách hiểu như vậy.
- Có thể thêm cách hiểu hợp lệ khi cú pháp thực sự mơ hồ; có thể đề nghị sửa nhỏ khi có bằng chứng cụ thể.
- Tổng số kết quả tối đa là **3**, tính cả kết quả đầu tiên. Không tạo thêm để đủ số lượng.
- Cách hiểu hợp lệ và đề nghị sửa là hai loại khác nhau. Ví dụ `x+1/2` → `(x+1)/2` là đổi phạm vi, không phải cách hiểu ngang hàng theo quy tắc toán thông thường.
- Mỗi candidate gắn với cấu trúc hoàn chỉnh, vùng nguồn, loại kết quả, cảnh báo và phiên bản xử lý. Preview, lựa chọn và đầu ra phải cùng tham chiếu candidate đó.
- Candidate sửa lỗi không bao giờ được dùng cho tự động chuyển. Một bộ kết quả còn repair, cách hiểu cạnh tranh hoặc cảnh báo cũng không đủ điều kiện auto, dù candidate trực tiếp đang đứng đầu. Với chuỗi chưa có cách hiểu trực tiếp hợp lệ, không được trình bày một bản sửa như thể đó là kết quả nguyên văn.
- Đổi phiên bản parser không được tự ý diễn giải lại các công thức đã lưu.
- Khi mở lại công thức, người dùng truy cập được những kết quả đã biết của lần chuyển trước, kể cả sau khi core đổi phiên bản. Cách lưu snapshot hoặc tái hiện chính xác bộ kết quả thuộc discovery; không được chỉ chạy parser mới rồi gọi đó là bộ kết quả cũ.

### 3.3. Desktop

- Nhập độc lập, xem trước, chọn kết quả và sao chép dưới dạng SVG, PNG hoặc văn bản.
- Tên tùy chọn xuất phải rõ: chuỗi nguồn, văn bản công thức hoặc LaTeX nếu định dạng đó được hỗ trợ. LaTeX là định dạng xuất/nhập, không phải nguồn chân lý thay thế MathDocument.
- Phân biệt rõ chuỗi đang nhập, cảnh báo và kết quả đang chọn.
- Tab Đồ thị và Hình học phát hành tăng dần theo E1/E2A/E2B và dùng cùng logic editor với Web. M2 đã có vòng công thức trước; từng bản tiếp theo công bố đúng tab và khả năng đã nghiệm thu.

### 3.4. Word

- Công thức được chèn phải là công thức native có thể sửa trong Word.
- Chế độ mặc định chỉ gợi ý. Không thay văn bản nếu người dùng chưa bật chế độ tự động hoặc chưa xác nhận.
- Trình bày kết quả trực tiếp theo cú pháp trước; chỉ mở thêm các phương án khi người dùng bấm `fx`. Không tự bung một danh sách nhiều kết quả sau mỗi lần nhập.
- Không gợi ý/chuyển khi bộ gõ còn đang ghép ký tự. Không nhận nhầm thao tác trong tìm kiếm, Ribbon hoặc cửa sổ khác là nhập nội dung tài liệu.
- Trước khi ghi, xác minh lại tài liệu, vùng nguồn, nội dung, candidate, cấu hình và trạng thái nhập. Nếu dữ liệu đã cũ, hủy thao tác.
- Mỗi lần chuyển là một thao tác hoàn chỉnh có thể Undo về đúng trạng thái trước đó, gồm nội dung và metadata liên quan.
- Có thể mở lại, chỉnh sửa qua Desktop, đổi kết quả, khôi phục nguyên văn nguồn hoặc tách khỏi quản lý của Locus.
- Sau tự chuyển, công thức vẫn có điểm truy cập `fx` khi người dùng chọn/quay lại để đổi kết quả hoặc khôi phục văn bản.
- Công thức tiếp tục hiển thị và sửa native được khi người nhận tài liệu không cài Locus.

### 3.5. Tự động chuyển

| Chế độ | Điều kiện nền | Trạng thái thiết kế |
| --- | --- | --- |
| Chỉ gợi ý | Người dùng xác nhận candidate trước khi ghi | Mặc định, yêu cầu nền |
| Vùng đánh dấu | Vùng đã đóng, đúng một cách hiểu chính xác, không sửa lỗi/cảnh báo, đủ mọi kiểm tra trước khi ghi | Yêu cầu nền; thử nghiệm trước chế độ Space |
| Khi nhấn Space | Người dùng chủ động bật, có kết quả đủ điều kiện, xác định đúng vùng và trạng thái nhập | Tính năng mong muốn; cách gõ nối tiếp và quy tắc kết thúc còn mở |

Theo cập nhật của người dùng ngày 2026-09-12, vùng mặc định là `lc[...]`: dấu mở `lc[`, dấu đóng `]`. Người dùng có thể tùy chỉnh riêng hai dấu trong cài đặt. Khi dấu đóng được nhập hoàn chỉnh, Locus xét nội dung giữa cặp dấu và tự chuyển nếu chế độ vùng đánh dấu được bật cùng mọi điều kiện cần thiết đã đạt. Nếu mơ hồ hoặc có sửa lỗi/cảnh báo, giữ nguồn và dùng `fx` để lựa chọn.

Baseline D-05 yêu cầu hai dấu literal không rỗng, khác nhau, không dấu nào là prefix của dấu kia, không chứa CR/LF hoặc backslash. Không hỗ trợ lồng hoặc escape trong v0; gặp những trường hợp này thì giữ nguyên vùng thay vì chuyển một phần. Quy tắc cấu hình, vùng chưa đóng và bảo vệ URL/email/path được ghi tại [GRAMMAR.md](m0/GRAMMAR.md). Restore trả nguyên vùng đã thay, gồm đúng cặp dấu đã dùng, và không tự kích hoạt auto từ sự kiện do restore tạo. Chế độ quan sát ngoài vùng đánh dấu vẫn mặc định chỉ gợi ý.

Không coi khoảng trắng đơn lẻ là bằng chứng phổ quát rằng công thức đã kết thúc. Các chuỗi `x mũ 2 cộng 1`, `1 trên 2` và `căn x cộng 1` là tình huống bắt buộc trong prototype.

**Cặp bọc SC1-01/02 đã triển khai trên Web/Desktop:** giữ một cặp chung để tự nhận diện trong các checkbox bật; thêm cặp riêng Toán/Lý/Hóa tùy chỉnh, chỉ định miền cho đúng vùng kể cả checkbox miền đó tắt. Nâng cấp giữ nguyên cặp chung đang cài; `lc-[…]` là cách tùy chỉnh, không tự thay default `lc[...]`. Cặp riêng không cấp quyền sửa nguồn hay nhận kết quả suy; vùng chưa đóng chỉ có thể cung cấp ngữ cảnh cho preview. Ngoặc nhóm Hóa bên trong vùng phải được phân biệt với dấu đóng cặp. [Quy tắc và migration](sc1/PLAN.md).

### 3.6. Metadata và nội dung đã sửa

Metadata phải đủ để liên hệ công thức với nguồn, kết quả đã chọn, bộ kết quả đã biết và phiên bản tạo ra chúng. Theo [hợp đồng core M0](m0/CORE-CONTRACT.md), snapshot giữ toàn bộ CandidateSet đã sinh cho lần phân tích, kể cả các phương án người dùng chưa mở qua `fx`. Định dạng lưu và cơ chế định vị trong Word vẫn thuộc discovery, chưa được chốt chỉ bằng hợp đồng dữ liệu.

**Bất biến triển khai D-04:** nội dung công thức hiện tại trong Word là nội dung người dùng đang làm việc. Nếu họ sửa native từ `x²` sang `x³`, metadata cũ không được dùng để ghi đè. “Chỉnh sửa hiện tại” và “Khôi phục chuỗi ban đầu” là hai thao tác riêng. UX xử lý xung đột, cơ chế định vị và migration metadata còn mở; các bất biến nguồn/candidate đã được đặc tả không tự chứng minh những cơ chế Word này đã đạt.

Nếu metadata thiếu, hỏng, thuộc phiên bản mới hơn hoặc không còn khớp: giữ công thức native, ngừng các thao tác quản lý không đủ dữ liệu và giải thích ngắn gọn trong UI. Không tự suy đoán lại nguồn cũ.

### 3.7. Hoạt động cục bộ và vòng học từ case

Nhận diện, phân tích và dựng công thức diễn ra cục bộ. Corpus phát triển lấy từ case tổng hợp và case người dùng chủ động cung cấp. Không đưa nội dung tài liệu vào báo cáo chẩn đoán mặc định.

Mỗi case cần có nguồn, vùng mong đợi, kết quả/cảnh báo mong đợi và quyền chuyển đổi. Sửa một case phải đi kèm kiểm tra không làm sai những case trước.

### 3.8. Web song song với Desktop

- Web chạy core trên thiết bị trong trình duyệt. Trang thử M1 gọi server localhost chưa đáp ứng yêu cầu này.
- Cùng nguồn, grammar và cấu hình miền phải cho cùng source spans, candidate, diagnostics và kết quả xuất theo hợp đồng trên các host. So sánh bằng corpus sau publish, không chỉ thấy preview giống nhau.
- Editor mới dùng chung logic phiên, chọn/kéo/Undo, scene và định dạng lưu. Các adapter host xử lý clipboard, file, focus và tích hợp hệ điều hành; UI phản ánh khả năng thật của host.
- File công thức/đồ thị/hình học mở và lưu qua lại Web–Desktop, có version và giữ nguồn/candidate. Snapshot đã lưu không tự đổi nghĩa khi nâng core. Dữ liệu chưa hỗ trợ được giữ an toàn thay vì bị ghi đè một phần.
- Bản web đầu có lưu nháp cục bộ và hướng offline sau lần tải/cache đầu; phải kiểm reload, cập nhật tài nguyên và phục hồi nháp trước khi công bố. Tải file là đường xuất khi clipboard không khả dụng.
- Cùng logic và cùng định dạng file là phạm vi đầu. Đồng bộ tài khoản/cloud không thuộc WEB1. Công thức native Word tiếp tục dùng connector cục bộ.
- Tái sử dụng UI theo từng phần có bằng chứng; không đổi toàn bộ Desktop M2 chỉ vì thêm Web. Công nghệ được chọn ở WEB0-03.

### 3.9. Mở rộng UX1/DOC1/BAL1/WD1/WEB2 — kế hoạch mới, chưa triển khai

- Ô nhập dùng cho cả công thức đơn và đoạn dài; ô kết quả trả nguyên đoạn với vùng công thức, giữ câu chữ/xuống dòng ngoài vùng. Model mới dự kiến giữ raw và result từng vùng riêng; không chỉ thay textarea bằng kết quả cân bằng.
- Nút đũa thần chỉ sáng khi selection ô kết quả có phương trình Hóa đủ điều kiện hoặc bản Locus đã cân bằng còn khôi phục được. Một bấm một phương trình từ trên xuống; hết lượt mới đổi cân bằng ↔ hủy. Cú pháp hợp lệ nhưng nội dung khoa học sai vẫn có thể giữ/copy/xuất.
- Dropdown có Cân bằng tất cả/Hủy cân bằng tất cả **trong selection**; mục vừa dùng bấm lại phục hồi đúng bản chụp trước lệnh, không gọi lệnh đối lập. Một batch một Undo; snapshot cũ không được đè sửa mới. [Toàn bộ trạng thái và tình huống](ux1/BALANCE-INTERACTION.md).
- Auto cân bằng mặc định tắt, tách khỏi detector và Space nhận ghost. Khi bật, nguồn mới/dán/sửa đủ điều kiện mới thay hệ số ở kết quả; bỏ thủ công giữ quyết định theo vùng/revision, không bị auto lặp lại. Không tự nhận repair hay suy sản phẩm; bật/tắt checkbox không xử lý lại cả tài liệu.
- Desktop gọn/tray/bung và Web thích ứng dùng cùng session/history; ba tab Công thức/Biểu đồ/Hình học, 2D/3D trong Hình học. PNG/SVG/download có tên và phạm vi rõ. Không tự bật startup Windows hoặc Word.
- WD1 quét tài liệu sẵn có không ghi nội dung. Phân biệt detected-text với user-ignore và native; fx có Convert all theo phạm vi/count, bỏ qua quyết định giữ text. Highlight có spike/kiểm riêng, không sửa màu gốc để giả dấu tạm.
- WEB2 giữ đề xuất Guest 5/2/2/2, Free 21/10/10/10, Premium 150/100/100/100 theo thứ tự công thức/biểu đồ/hình 2D/hình 3D. Cách tính lượt/reset/offline/Desktop/giá/provider cần chốt ở WEB2-01. Không áp quota vào local ở đợt này; không tính từng phím/render/drag.

[Hướng sản phẩm và phần còn mở](PRODUCT-ITERATION-20260915.md) ghi các mức độ chốt; không suy có tài khoản hay mọi định dạng rich text từ kế hoạch này.

## 4. Ranh giới kiến trúc cần giữ

| Thành phần | Trách nhiệm |
| --- | --- |
| Core | Chuẩn hóa có ánh xạ, nhận diện vùng, phân tích, candidate và chẩn đoán |
| Bộ xuất/renderer | Tạo preview, SVG/PNG/văn bản và cấu trúc Word từ cùng candidate |
| Logic ứng dụng và tài liệu chung | Phiên nguồn/candidate/selection, lệnh/Undo, schema lưu có version; Formula/Plot/Geometry là các loại tài liệu riêng |
| Editor chung | Giao diện và tương tác công thức/đồ thị/hình học, scene, nhãn và trạng thái xuất dùng trên Web/Desktop |
| Desktop | Host editor, tích hợp clipboard/file/runtime cục bộ và các không gian làm việc |
| Web | Host browser, adapter clipboard/file, nháp/cache/offline; core thực thi trên thiết bị |
| Word connector | Đọc vùng, kiểm tra trạng thái nhập, hiển thị gợi ý, giao dịch thay thế, metadata và Undo |
| Kết nối Desktop–Word | Trao đổi phiên làm việc, tài liệu/vùng/candidate/phiên bản; từ chối yêu cầu hết hạn |

M0 đã chọn C# core dùng chung và hướng COM/VSTO trên Windows theo [quyết định có bằng chứng](m0/DECISIONS.md). M1 đã triển khai parser, nguồn/candidate, nhận diện và các bộ xuất; cùng DLL core thật chạy qua .NET 10 và .NET Framework 4.8 x86 với kết quả trùng trên các nguồn thử. M2 chọn WPF và renderer vector riêng cho grammar hiện tại; preview/SVG/PNG dùng một scene của candidate, không thêm parser. [Bằng chứng M2](m2/REPORT.md). M3 dùng cùng mã renderer và core trong entry point COM riêng, đạt G1 cho alpha thủ công theo [báo cáo](m3/REPORT.md); G2/G3 còn mở. Core trong browser và editor đa host là công việc WEB0/SH, chưa được chứng minh chỉ từ việc core dùng .NET Standard.

Yêu cầu nền là khởi động Locus Desktop đồng thời làm add-in Word sẵn sàng. **Baseline nghiên cứu W0:** kiểm hai thứ tự khởi động và reconnect; khi Word chưa mở, Desktop vẫn dùng độc lập. M3 đã có đăng ký per-user để Word tải entry point Manual khi mở; dùng qua Ribbon, chưa nối tab Desktop M2. Tích hợp chỉnh sửa/kết nối sản phẩm tiếp tục ở M4/M6; không tự mở Word hoặc bật lại add-in người dùng đã tắt chỉ vì khởi động Web/Desktop.

## 5. Các miền và công cụ mở rộng

### Đồ thị

Nhập hàm số → xem đồ thị → điều chỉnh miền xem, trục, nét và nhãn → sao chép/xuất. Cần xử lý miền xác định và gián đoạn để không vẽ đường nối sai. Tập loại hàm được hỗ trợ phải được công bố; tính năng giải đại số không tự phát sinh từ tab này.

Baseline E1 đầu: `y=f(x)`, đa thức/phân thức/căn/sin/cos, nhiều đường, góc lượng giác theo radian có nhãn rõ. Miền lấy mẫu và khung nhìn là dữ liệu riêng; pan/zoom không thay nguồn công thức. Evaluator từ AST Locus tính các đoạn/điểm để renderer dựng, hủy kết quả cũ khi nguồn/miền thay đổi. Lưu/mở/Undo và SVG/PNG giữ trạng thái hiện tại trên Web/Desktop.

### Hình học

Có đối tượng chỉnh sửa được, kéo/di chuyển, chọn nét đứt/liền. Người dùng có thể nhấn chuột phải trên preview để copy SVG của trạng thái hiện tại tại bất kỳ thời điểm nào có preview hợp lệ, không cần một bước hoàn tất bản vẽ riêng.

Đồ thị và hình học có tài liệu/scene riêng, tham chiếu biểu thức từ core khi cần. Không ép camera, điểm kéo hoặc quan hệ hình học thành các node công thức trong MathDocument.

**Hướng thực hiện:** đặt điểm → nối nét → kéo chỉnh → tạo quan hệ bằng công cụ được chọn → đổi nét/nhãn → copy SVG. Kéo tự do và dựng theo quan hệ cùng tồn tại; không tự khóa vuông góc/song song chỉ vì hai nét trông gần đúng. D-06 còn chốt chi tiết thao tác qua E2A-01.

Baseline prototype: kéo cạnh tự do dịch hai đầu và cập nhật các cạnh chung đỉnh; một lần kéo là một Undo; Esc hủy kéo. Công cụ quan hệ đầu gồm trung điểm, song song, vuông góc và điểm trên đường tròn, có quy tắc khi xóa cha hoặc hình suy biến. Lưu đối tượng có ID và lệnh có nghĩa; phát lại quá trình dựng là khả năng về sau, không phải điều kiện của E2A.

Với 3D, SVG là hình chiếu của góc nhìn hiện tại; scene chỉnh sửa được lưu cả đối tượng và camera. E2A có thể dùng các đoạn 2D để vẽ hình minh họa khối; khả năng xoay và thao tác chiều sâu thật chỉ được công bố ở E2B.

### Lý và Hóa

Dùng chung cơ chế nguồn, vị trí, kết quả và xuất; bổ sung quy tắc miền thay vì coi mọi chuỗi là đại số.

- Lý: đơn vị, vector và ký hiệu chuyên ngành; phân biệt đơn vị với biến khi đủ ngữ cảnh.
- Hóa: ký hiệu nguyên tố phân biệt hoa/thường, chỉ số, hệ số, điện tích và mũi tên phản ứng.
- `H2SO4` là case nhận diện quan trọng, không phải quy tắc cho phép thay mọi chuỗi tương tự.
- Không tự cân bằng phản ứng, kiểm chứng công thức hóa học hoặc kiểm tra thứ nguyên nếu chưa có phạm vi riêng cho các chức năng đó.

**Yêu cầu UI ngày 2026-09-14:** giữ nguyên mọi màn hình/thao tác như Toán, chỉ thêm ba checkbox nhận diện Toán/Lý/Hóa độc lập vào tùy chọn hiện có. Cho bật đồng thời nhiều môn; giữ Cách đọc/marker, preview, candidate/fx, copy/xuất, nháp và lưu/mở. E3 không thêm bộ chọn môn loại trừ nhau, editor riêng, nhãn miền thường trực hay công cụ ký hiệu mới.

E3-01 quy định tập nhận diện được bật và xử lý xung đột trước khi viết parser miền; tối đa ba candidate tổng qua đúng cơ chế đang có. Tắt detector không xóa hoặc diễn giải lại snapshot đã chọn. Hóa trước Lý là thứ tự ưu tiên của kế hoạch hiện tại, không phải phụ thuộc parser. Mỗi miền nghiệm thu Web/Desktop trước, rồi có task Word riêng kiểm native/metadata/Undo/restore; không dùng việc nhìn đúng trên Web làm bằng chứng đã hỗ trợ Word. [Hợp đồng checkbox](e3/PLAN.md).

WPS và các connector khác chưa thuộc bản đầu. Lý/Hóa không phụ thuộc vào việc hoàn thành hình học 3D.

**SC1 — đã có nền Hóa thông minh trên Web/Desktop:** cân bằng phương trình đủ hai vế bằng bảo toàn nguyên tố/điện tích; suy sản phẩm từ vế đầu bằng kho 45 bản ghi có điều kiện/ngoại lệ/nguồn. SC1-01…07 đạt, SC1-08/09/10 còn phần chưa đạt. BAL1 cập nhật cách dùng cân bằng theo selection và auto ở đợt mới. Không đồng nhất “đã cân bằng” với “phản ứng thực sự xảy ra”, hoặc “chưa có dữ liệu” với “không phản ứng”.

Ghost phải cho thấy sản phẩm và toàn bộ hệ số sẽ đổi, giữ raw trước nhận để Undo/restore. Kết quả tính toán/suy sản phẩm có loại và provenance riêng, không giả là cách đọc trực tiếp; tối đa ba phương án tổng. Enter nhận và Space opt-in là baseline thử trên Web/Desktop, chưa đóng quyết định UX người dùng hoặc W0/D-01. Word bắt đầu bằng luồng xác nhận thủ công; ghost trong thân tài liệu theo SC1-11/G2, Space theo G3. [Kế hoạch](sc1/PLAN.md), [bộ tình huống](sc1/ACCEPTANCE.md).

## 6. Sổ quyết định và trạng thái

| ID | Nội dung và trạng thái | Cơ sở hoặc phần còn phải quyết định |
| --- | --- | --- |
| D-01 | **CÒN MỞ:** sau tự chuyển bằng Space, nhập tiếp vào đâu và kết thúc công thức bằng cách nào? | [Thử nghiệm Space](m0/SPACE-EXPERIMENT.md) tách mô phỏng khỏi bằng chứng Word; chưa chọn UX nối tiếp. Đổi cặp bọc sang `lc[...]` không đóng quyết định này |
| D-02 | **BASELINE KỸ THUẬT ĐÃ TRIỂN KHAI M1:** alias, ưu tiên/kết hợp toán tử, phạm vi căn/phân số, nhân ngầm và số thập phân | `vi-math-m0-proposal-0.1` tại [GRAMMAR.md](m0/GRAMMAR.md) cùng [corpus M0](../corpus/m0/cases.json); [kiểm chứng M1](m1/REPORT.md). Có thể sửa theo phiên bản; không đồng nhất các ca đạt với hiểu mọi cách viết tiếng Việt |
| D-03 | **ĐÃ CHỌN HƯỚNG; MA TRẬN PHÁT HÀNH CÒN MỞ:** C# core chung, COM trên Windows | [Quyết định M0](m0/DECISIONS.md), [M3](m3/REPORT.md). CW/G1 mở cho alpha thủ công Word x86 đã kiểm; G2/G3 chưa đạt |
| D-04 | **SNAPSHOT CORE ĐÃ TRIỂN KHAI; UX VÀ CƠ CHẾ WORD CÒN MỞ:** bảo toàn nguồn, toàn bộ CandidateSet và không ghi đè sửa native bằng snapshot cũ | [CORE-CONTRACT.md](m0/CORE-CONTRACT.md), [serializer M1 và giới hạn version](m1/CORE-USAGE.md). Còn cần chốt UX chỉnh sửa/restore, định vị, migration và hành vi thực tế qua Undo/save/reopen |
| D-05 | **NHẬN DIỆN CORE ĐÃ TRIỂN KHAI M1; AUTO WORD CHƯA CÓ:** mặc định `lc[...]`, user tùy chỉnh cặp literal; không lồng/escape, chưa đóng thì không auto; restore nguyên cặp bọc và tránh vòng tự chuyển | [Grammar](m0/GRAMMAR.md), [core M1](m1/REPORT.md), các case vùng M0-068 đến M0-078 và M0-106/M0-112 đến M0-114/M0-117. Mặc định và quyền tùy chỉnh do user chốt; các guard Word, restore và tránh vòng tự chuyển còn cần tích hợp |
| D-06 | **ĐÃ CHỐT CHO ALPHA G:** kéo trực tiếp điểm/cạnh/nhãn; snap khi thả gần điểm/trung điểm với tín hiệu nấc, Alt bỏ snap; quan hệ chỉ sinh từ công cụ rõ; điểm phụ thuộc phải tách trước khi kéo tự do; xóa cha và phụ thuộc là một lệnh có Undo | [Báo cáo G](phase-g/REPORT.md), [16 nhóm hình học](../artifacts/phase-g/verification/geometry-verification.json). Ba walkthrough Desktop thật còn REVIEW, không thay đổi quy tắc |
| D-07 | **ĐÃ CHỐT CHO ALPHA M2; MA TRẬN ĐÍCH TIẾP TỤC:** PNG + bitmap fallback; SVG MIME + text và file; nguồn/LaTeX/MathML/OMML cung cấp dạng text | [Định dạng và giới hạn](m2/REPORT.md). Paint nhận bitmap; Word nhận PNG khớp pixels. SVG nhập vào Word có dữ liệu raster trong Flat OPC; giữ file SVG gốc khi cần vector. Word tự nhận MathML thành equation trong bài thử; đây chưa phải connector Locus |
| D-08 | **KẾ HOẠCH VIẾT LẠI TỪ BASELINE 2026-09-15:** UX1/DOC1 → BAL1 → export/tray/SC1 Word/gói → WD1 → E1 → E2A/E2B → WEB2; M6 theo capability, Word auto theo cổng riêng | [Roadmap](ROADMAP.md), [NEXT-STEPS](NEXT-STEPS.md), [backlog](BACKLOG.md); ưu tiên không tạo phụ thuộc giả, chỉ thiết kế trong lượt hiện tại |
| D-09 | **ĐÃ TRIỂN KHAI CHO EDITOR CÔNG THỨC:** Web/Desktop dùng chung core, định dạng lưu và logic editor | Blazor WebAssembly + WPF Hybrid đã đạt WEB0/SH/WEB1; E3 mở rộng Hóa/Lý qua cùng luồng. [Bằng chứng parity và giới hạn](e3/REPORT.md). Đồ thị/hình học tiếp tục theo E1/E2 |
| D-10 | **ĐÃ TRIỂN KHAI SC1-01/02 WEB/DESKTOP:** cặp chung/riêng tùy chỉnh, chỉ định theo vùng; không suffix | [Báo cáo cặp](sc1/MARKERS-REPORT.md); Word SC1 còn SC1-09 |
| D-11 | **SC1-03…07 ĐẠT WEB/DESKTOP; 08/09/10 CÒN MỞ:** cân bằng/kho/ghost/Undo; BAL1 thay tương tác ở đợt mới | [Ghost](sc1/GHOST-REPORT.md), [baseline](BASELINE-20260915.md); không đóng D-01/G2/G3 |
| D-12 | **ĐƯA VÀO KẾ HOẠCH, CHƯA CODE:** đũa thần theo selection, mỗi bấm một phương trình, vòng lượt, hai batch hoàn tác snapshot, auto mặc định tắt và quyền giữ sai | [BAL1](ux1/BALANCE-INTERACTION.md); UX1-01 review trạng thái/chi tiết trước triển khai |
| D-13 | **ĐƯA VÀO KẾ HOẠCH:** dán cả đoạn giữ text/hiển thị công thức tại chỗ; nguồn và kết quả từng vùng | DOC1; rich text đầy đủ và đường xuất đích có phạm vi riêng |
| D-14 | **ĐƯA VÀO KẾ HOẠCH:** UI gọn/tray/bung, input/result/copy/options; tab Công thức/Biểu đồ/Hình học | UX1; không tự bật startup, không triển khai chức năng tab tương lai trong lượt này |
| D-15 | **ĐƯA VÀO KẾ HOẠCH:** quét Word cũ, highlight/fx, convert riêng/tất cả, keep-text/ignore | WD1; source/Range/Undo/inline DPI kiểm riêng, không mở auto Word |
| D-16 | **ĐỀ XUẤT HẠN MỨC, CHƯA CÓ TÀI KHOẢN/THU PHÍ:** Guest/Free/Premium theo bảng đã nêu | WEB2-01 chốt lượt/reset/offline/Desktop/giá/provider; local vẫn giữ phạm vi hiện tại |

Mỗi quyết định đóng phải ghi: lựa chọn, ví dụ người dùng, bằng chứng, giới hạn và các task bị ảnh hưởng. Không chọn stack hoặc mở rộng phạm vi chỉ để làm biến mất một quyết định còn mở.
