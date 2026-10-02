# Các bước thực hiện tiếp theo

Cập nhật: 2026-09-15. Desktop và Web dùng chung logic; M3, WEB0, SH, WEB1 và E3 đã đạt phạm vi tương ứng. E3 giữ 8/8 alpha local. Sau trao đổi về cặp bọc theo môn, cân bằng/suy sản phẩm và ghost, kế hoạch mới lấy SC1-01 trước, hoàn thành SC1 alpha rồi tiếp E1 đồ thị/thanh trượt và hình học. SC1-01…06 đã đạt trên Web/Desktop: cặp bọc, cân bằng và kho sản phẩm có điều kiện; đang nghiệm thu ghost SC1-07. [Kế hoạch SC1](sc1/PLAN.md), [kết quả E3](e3/REPORT.md).

[Roadmap](ROADMAP.md) quy định các mốc và phụ thuộc; [backlog](BACKLOG.md) là nơi duy nhất ghi trạng thái từng task. Tài liệu này quy định thứ tự lấy việc, phạm vi từng đợt và bài demo để nghiệm thu.

## 1. Xuất phát và cách lấy việc

- M1/M2/M3 đã đạt phạm vi alpha; M3 đạt 4/4 task, CW/G1 mở cho chuyển thủ công trên Word x86 đã thử. W0 vẫn 4/6 mục; G2/G3 chưa đạt. [Nghiệm thu M3](m3/REPORT.md).
- **WEB1 đã DONE alpha local (4/4, 100% mốc WEB1).** Người dùng hoãn Sites; nháp, clipboard, offline, cập nhật và Telex/VNI OS trên đúng WASM trong Windows WebView2 đã có bằng chứng. [Kết quả](web1/REPORT.md), [phạm vi IME](web1/IME.md), [dùng thử](web1/QUICKSTART.md). E3 đã triển khai; SC1-07 đang được nghiệm thu; kế tiếp SC1-08 kiểm bàn phím/IME và SC1-09 Word thủ công.
- W0-05/06 giữ nguyên trạng thái và bằng chứng. Người dùng sẽ phản hồi theo [checklist W0](w0/PENDING-ACCEPTANCE.md); không yêu cầu lại trong mỗi lượt làm việc.
- Một task triển khai chính tại một thời điểm. Hoàn thành bài demo của đợt rồi chuyển sang đợt tiếp theo; thứ tự dưới đây giả định một luồng thực hiện, không dựa vào nhiều agent hay đội ngũ chưa có.
- Nếu một task bị chặn bởi môi trường hoặc phản hồi, ghi chính xác phần còn thiếu và lấy task độc lập kế tiếp. Không ghi DONE cho phần bị chặn, không để cả Web/Lý/Hóa chờ D-01.
- Chỉ ước lượng thời gian sau khi đọc mã và đầu vào của task; cập nhật dự kiến sau mỗi demo. Chưa gán ngày phát hành hoặc tính phần trăm toàn sản phẩm từ số task mới thêm.

## 2. Hàng đợi triển khai mặc định

| Đợt | Task theo thứ tự lấy việc | Đầu ra cho người dùng | Không phải điều kiện chờ |
| --- | --- | --- | --- |
| 1 — DONE alpha | M3-01 → M3-02 → M3-03 → M3-04 | Chuyển thủ công trong Word, Undo và khôi phục nguồn; gói thử/hướng dẫn đã có | W0-05/06, lựa chọn A/B, auto-Space |
| 2 — DONE prototype | WEB0-01 → WEB0-02 → WEB0-03 | Core thật chạy trong browser; báo cáo tương đương và quyết định cách dùng chung UI đã có | M4/M5 hoặc hoàn tất W0 |
| 3 — DONE alpha local | SH + WEB1 hoàn thành | Editor có nháp, đầy đủ clipboard, bộ gõ trên baseline đã kiểm, gói mở local và offline; online hoãn theo yêu cầu | Sites, tài khoản, đồng bộ cloud, Word chạy cùng |
| 4 — DONE alpha local | E3-01 → E3-02; E3B-01 → E3B-02 → E3B-03; E3A-01 → E3A-02 → E3A-03 | Hóa cơ bản trước, rồi Lý cơ bản; mỗi miền có bản Desktop/Web và nghiệm thu Word riêng | E1, Word tự động, miền kia hoàn thành, editor 3D |
| 5 — Kế hoạch mới | SC1-01 → SC1-02 → SC1-03 → SC1-04 → SC1-05 → SC1-06 → SC1-07 → SC1-08 → SC1-09 → SC1-10 | Cặp theo môn, cân bằng/suy Hóa có điều kiện, ghost Web/Desktop, Word thủ công và gói local | Ghost inline Word, phản hồi W0, E1 |
| 6 | E1-01 → E1-04 → E1-02 → E1-06 → E1-03 → E1-05 | Đồ thị 2D và thanh trượt tham số trên cả hai host; giữ đề xuất UX đã có | Auto Word, SC1-11, hình học 3D |
| 7 | E2A-01 → E2A-02 → E2A-04 → E2A-03 → E2A-05 | Vẽ hình 2D, kéo chỉnh, dựng quan hệ, lưu/mở và copy SVG | Solver tổng quát, mô phỏng vật lý |
| 8 | M4-01 → M4-02 → M4-03; M5A-01 → M5A-02 | Gợi ý fx, chỉnh qua Desktop và auto vùng có cặp khi đạt cổng | Quyết định Space |
| 9 | M6-01 → M6-02 → M6-03 → M6-04 | Đóng gói, cập nhật, phục hồi và pilot của phạm vi đã công bố | Space hoặc 3D nếu chưa đưa vào bản phát hành |
| 10 | E2B-01 → E2B-02 → E2B-03 | Hình học 3D có camera và SVG hình chiếu | M5B; về kỹ thuật chỉ cần nền E2A đã đạt |
| Có điều kiện | SC1-11 sau SC1-07/09 và M4/G2; nhận bằng Space cần D-01/G3 | Ghost trực tiếp trong thân Word; nghiệm thu vị trí/focus/IME và nhận bằng phím riêng | Không giữ E1 để chờ nhánh này |
| Có điều kiện | M5B-01 → M5B-02 khi có phản hồi W0-06 và đạt các cổng liên quan | Auto-Space theo cách nhập nối đã được chốt | Không giữ các đợt khác để chờ nhánh này |

Thứ tự Hóa trước Lý là baseline thực hiện cho một luồng công việc, không phải phụ thuộc kỹ thuật. E1-04/06 và E2A-04 là task bổ sung vào các ID cũ nên số thứ tự ID không phải thứ tự chạy. Nếu M4/M5A chờ cổng Word, tiếp tục E2B hoặc các việc đóng gói Desktop/Web đủ đầu vào; giữ phần nghiệm thu Word mở.

## 3. Đợt 1 — Word chuyển thủ công

**Đã hoàn thành 4/4 task theo baseline alpha.** [Báo cáo](m3/REPORT.md) và [acceptance](../artifacts/m3/acceptance.json) ghi kiểm API, bàn phím, gói cài và giới hạn pointer/DPI. Các bước dưới đây giữ làm tiêu chí đối chiếu; hàng đợi hiện bắt đầu ở SC1-03.

### M3-01: đọc vùng và xem trước

1. Đọc `src/Locus.Word/WordOperations.cs`, `ManagedSnapshot.cs`, `Connect.cs` và bằng chứng W0; xác định phần dùng lại, phần chỉ phục vụ probe.
2. Tạo lệnh người dùng chọn vùng → mở bảng xem trước. Dùng core thật, giữ nguyên raw source, định danh tài liệu/cửa sổ/vùng và phiên yêu cầu.
3. Hiện kết quả trực tiếp trước; người dùng có thể mở các phương án còn lại, thấy rõ repair và hủy. Nguồn thay đổi phải làm phiên cũ hết hiệu lực.
4. Công bố phạm vi đầu: vùng văn bản trong thân tài liệu thông thường trên baseline Word x86. Ngữ cảnh chưa hỗ trợ phải được nhận biết và từ chối rõ.

**Demo:** chọn `x mũ 2`, `1 trên 2`, `x+1/2`; xem trước rồi hủy. Thử văn xuôi, emoji/NFD trước vùng, nhiều tài liệu và sửa nguồn khi bảng đang mở. Trước xác nhận tài liệu giữ nguyên. M3-01 chưa có quyền commit sản phẩm.

### M3-02: xác nhận và giao dịch native

1. Nút xác nhận gắn đúng candidate và phiên nguồn; không chọn lại candidate bằng cách parse khác ở thời điểm ghi.
2. Đưa giao dịch sang UI thread Word. Kiểm tra lại target/source/version/config/focus/trạng thái nhập; định nghĩa rõ luồng focus từ bảng xác nhận trở về editor, không chỉ kiểm cửa sổ Word đang foreground.
3. Chèn native và metadata trong cùng giao dịch Undo. Kế thừa cách đặt content control quanh range native đã được kiểm ở W0.
4. Hủy khi stale; thử lỗi ở các bước giao dịch và xác minh phục hồi. Con trỏ sau chuyển cho phép viết tiếp ngoài equation.

**Demo:** chuyển → gõ tiếp câu → Undo/Redo; sửa nguồn/đổi tài liệu trước xác nhận; focus Find/Ribbon/dialog; lỗi giữa native và metadata. Nội dung ngoài vùng và dữ liệu mới nhập phải được giữ đúng hợp đồng.

### M3-03: mở lại, khôi phục và tách quản lý

1. Mở bộ kết quả đã lưu mà không parse lại bằng grammar mới; hiển thị nguồn và phương án đã chọn.
2. Restore nguyên vùng nguồn, kể cả dấu bọc và dạng Unicode; detach giữ native và bỏ quản lý Locus. Mỗi thao tác có Undo riêng.
3. Kiểm save/reopen, bản sao công thức/ID trùng, metadata hỏng/mới hơn và sửa native trực tiếp. Dữ liệu lệch phải dừng thao tác quản lý tương ứng.
4. Xác minh tài liệu còn hiển thị và sửa native được khi không tải Locus. Vòng sửa nguồn qua Desktop đầy đủ thuộc M4-03.

### M3-04: bản thử và cổng G1

- Tách entry point sản phẩm khỏi nút thử A/B và lệnh nghiên cứu; xác định registration/lifecycle và cách gỡ bản thử, không đụng add-in khác trên máy.
- Kiểm luồng chọn → preview → xác nhận → native → viết tiếp → Undo → restore trên Word thật. Kiểm protected/read-only/Track Changes và các ngữ cảnh ngoài phạm vi để từ chối đúng.
- Đầu ra đã tạo: `docs/m3/REPORT.md`, `docs/m3/QUICKSTART.md`, `artifacts/m3/acceptance.json`, gói thử và các lệnh trong `tools/m3`.
- Chỉ ghi G1 đạt sau khi luồng sản phẩm và ma trận công bố đạt. CW hoặc test W0 không thay thế nghiệm thu này.

## 4. Đợt 2 — WEB0: chứng minh kiến trúc dùng chung

**Đã hoàn thành prototype ngày 2026-09-14.** 116 nguồn tương đương, 238 contracts trên mỗi host, shared editor/scene, Telex qua OS trên WASM WebView2 và số đo publish sạch. Các mục dưới đây giữ phạm vi nghiệm thu; giới hạn timer/IME và phần chuyển tiếp nằm trong [COMPATIBILITY](web/COMPATIBILITY.md).

### WEB0-01: core trong browser

- Tạo host thử WebAssembly tham chiếu đúng `Locus.Core`; nhập/xem raw source, candidate, chẩn đoán và các dạng xuất trong browser.
- Build/publish bản thực tế và phục vụ như tệp tĩnh. Browser phải tự phân tích mà không gọi `/analyze` hay cần .NET server trên máy người dùng.
- Ghi lỗi tương thích thực: chuẩn hóa Unicode, SHA-256, serializer, trimming, runtime. Nếu cần sửa core, giữ hợp đồng và chạy lại hai runtime native đang được hỗ trợ.

### WEB0-02: cùng kết quả và giới hạn host

- Chạy các case core hiện có trên native và browser với cùng phiên bản; so raw/source spans, thứ tự/ID/loại candidate, diagnostics, snapshot và LaTeX/MathML/OMML. Chuẩn hóa cách báo kết quả, không bỏ qua khác biệt ngữ nghĩa.
- Thêm case NFC/NFD/emoji, nguồn lỗi/dài, marker tùy chỉnh, repair và thay nguồn khi đang xử lý. Thử nhập tiếng Việt thực trong browser và nhận event composition đúng.
- Ghi trình duyệt/máy/build, kích thước tải, thời gian khởi động nguội/ấm và p50/p95 phân tích. Không lấy hiệu năng debug làm số phát hành.
- Chặn phụ thuộc parser vào mạng; kiểm chạy sau khi tài nguyên đã tải. PWA tải lại offline và cập nhật cache được nghiệm thu ở WEB1-04.

### WEB0-03: cùng UI/canvas trong hai host và chốt công nghệ

- Thử một editor nhỏ trong browser và WPF qua host nhúng; dùng cùng state, chọn/kéo một điểm, Undo và xuất SVG.
- Đối chiếu khả năng dùng JSXGraph cho đồ thị/hình học; tham khảo Excalidraw cho thao tác. Thư viện nhận AST/callback/điểm từ Locus, không trở thành parser nguồn thứ hai.
- Thử Blazor WebAssembly + WPF Blazor Hybrid trước; chỉ chọn sau bằng chứng nhập/renderer/package tương đương. Nếu vướng, báo API gây lỗi, thử adapter hoặc cách nhúng khác vẫn dùng core chung. Demo server hiện có không được đổi tên thành web độc lập để đóng task.
- Đầu ra dự kiến: `docs/web/COMPATIBILITY.md`, `docs/web/ARCHITECTURE.md`, `artifacts/web/compatibility.json`; phiên bản và giấy phép dependency đã chọn được ghi lại.

## 5. Đợt 3 — editor dùng chung và web công thức

| Task | Công việc cụ thể | Bài nghiệm thu |
| --- | --- | --- |
| SH-01 | Tách phiên nguồn/candidate/selection/Undo khỏi WPF/component; adapter clipboard/file/scheduling; request ID/revision bỏ kết quả cũ; giữ giới hạn nguồn và kiểm worker/deadline cho việc nặng | Cùng chuỗi thao tác cho cùng trạng thái; sửa nguồn/composition khi việc cũ đang chạy không áp dụng kết quả cũ; không đưa COM/WPF vào core |
| SH-02 | Hợp đồng lưu có version/kind/ID/revision; FormulaDocument, PlotDocument, GeometryDocument là các loại riêng | Lưu/mở hai host giữ nguồn và candidate; kiểu/phiên bản chưa hỗ trợ không làm hỏng dữ liệu cũ |
| SH-03 | Renderer/scene nhãn toán dùng chung, xuất SVG/PNG từ cùng trạng thái; chuyển WPF renderer theo từng phần có đối chiếu | Căn/phân số/lũy thừa/ngoặc và Unicode đúng ở preview/export; font được đóng gói cho offline |
| SH-04 | Gói editor chung, host browser và tab Desktop, quản lý focus/vòng đời và hủy kết quả cũ | Cùng tạo/sửa/Undo/lưu-mở/xuất; mở/đóng tab không mất phiên hoặc đăng ký sự kiện lặp |
| WEB1-01 | Nhập công thức, chọn candidate, cảnh báo, marker và cấu hình, lịch sử cục bộ | Các bài nhập M2 tương ứng chạy trong Web và editor chung của Desktop |
| WEB1-02 | Adapter clipboard/file; SVG, PNG, nguồn/LaTeX và các dạng text được công bố | Dán/xuất ở trình duyệt hỗ trợ; khi clipboard bị từ chối vẫn tải được file; hủy hộp lưu không mất nguồn |
| WEB1-03 | Mở/lưu cùng file giữa hai host; lưu nháp cục bộ, phục hồi sau reload; keyboard/focus/Telex/VNI | Desktop lưu → Web mở/sửa/lưu → Desktop mở; giữ Unicode, candidate và cài đặt thuộc tài liệu |
| WEB1-04 | Build tĩnh, cache offline/cập nhật phiên bản; bộ mở/dừng local và gói ZIP, online hoãn theo yêu cầu ngày 2026-09-14 | Chạy từ ZIP trên loopback, mở lặp không tạo server thừa, dừng/mở lại, tải lại và xuất khi server tắt; cập nhật không trộn grammar/assets hoặc mất nháp |

Tên module đề nghị để đánh giá: `Locus.Application` cho phiên dùng chung, `Locus.Documents` cho hợp đồng lưu, `Locus.Rendering` cho phần dựng độc lập host, `Locus.Editor` cho UI chung, `Locus.Web` cho host browser. Chốt tên và target framework ở WEB0-03; chưa tạo năm project chỉ để có cấu trúc. Các phần mới dùng chung trước, thay editor WPF hiện tại khi bộ so sánh đã đạt; gói M2 đã nghiệm thu vẫn là mốc đối chiếu.

Web xử lý công thức trên thiết bị. Cùng định dạng lưu là cơ chế chuyển tài liệu đầu tiên giữa Web/Desktop; đồng bộ tài khoản/cloud nằm ngoài đợt này. Word native vẫn cần connector cục bộ và không phải điều kiện để dùng Web.

## 6. Đợt 4 — Hóa và Lý cơ bản

**Được đưa lên trước E1 theo yêu cầu ngày 2026-09-14.** Theo chỉnh hướng mới nhất, giữ nguyên UI/luồng Toán trên Web/Desktop, chỉ thêm ba checkbox nhận diện Toán/Lý/Hóa độc lập trong tùy chọn. Nhập → preview → sửa → copy/lưu dùng đúng giao diện hiện tại; chọn được nhiều checkbox cùng lúc. [Quy tắc checkbox, bài dùng Hóa/Lý và đầu vào E3-01](e3/PLAN.md).

**E3-01/E3-02 làm nền:** tập miền nhận diện được bật bằng checkbox, quy tắc xung đột và gộp kết quả (tối đa ba candidate tổng), cấu trúc miền, source spans, snapshot/version và bộ xuất. Checkbox không thay Cách đọc/marker hiện có và không sửa snapshot đã chọn. Case rõ như `H2SO4` có thể nhận diện là Hóa khi Hóa bật; `Co` trong văn xuôi không tự cấp quyền chuyển. Hợp đồng alias/điện tích được ghi trước khi viết parser. MathDocument tiếp tục giữ vai trò của công thức Toán; dữ liệu miền mới có kiểu/version rõ và dùng cùng pipeline nguồn/candidate.

| Nhánh | Bước parser | Bước Web/Desktop | Bước Word riêng |
| --- | --- | --- | --- |
| E3B — Hóa trước | E3B-01: nguyên tố, nhóm ngoặc/chỉ số, hệ số, điện tích, mũi tên; phân biệt `Co` và `CO` | E3B-02: giữ UI Toán, checkbox Hóa; H2SO4, Ca(OH)2, ion/phản ứng qua preview, SVG/PNG/LaTeX/MathML và lưu/mở | E3B-03: OMML/native, metadata, Undo và restore trên luồng M3 |
| E3A — Lý kế tiếp | E3A-01: vector, chỉ số, ký hiệu Hy Lạp, giá trị kèm đơn vị; phân biệt biến với đơn vị | E3A-02: giữ UI Toán, checkbox Lý; `v = 10 m/s`, chỉ số/vector/đơn vị qua preview/export/lưu-mở | E3A-03: OMML/native và đầy đủ vòng khôi phục theo M3 |

Nếu Word của một miền chưa đạt, có thể phát hành miền ấy trên Web/Desktop sau khi nghiệm thu hai host, ghi riêng trạng thái Word. Không để lỗi Word của Hóa chặn parser Lý. Mọi thay đổi miền chạy lại corpus Toán và kiểm cùng kết quả trên browser/native. Phạm vi này là nhập và trình bày ký hiệu/công thức; giải bài, cân bằng phản ứng và kiểm chứng khoa học có backlog riêng khi được yêu cầu.

## 7. Đợt 5 — cặp bọc theo môn và Hóa thông minh

[Kế hoạch SC1](sc1/PLAN.md) quy định cách gõ, cặp bọc, thuật toán/kho quy tắc, ghost, dữ liệu và phạm vi host. [Bộ nghiệm thu](sc1/ACCEPTANCE.md) ghi các case bắt buộc; tất cả còn là kỳ vọng, chưa có PASS.

1. **SC1-01…04:** đặc tả/corpus, nhiều cặp và override miền, source/proposal/version, chữ thường đơn nghĩa và `=` trong Hóa. Demo bốn loại cặp, tắt detector, `co`, `h20`, ngoặc nhóm và vùng chưa đóng trước khi làm suy luận.
2. **SC1-05/06:** cân bằng chính xác và kiểm lại bảo toàn; sau đó kho phản ứng có điều kiện/ngoại lệ/nguồn, suy sản phẩm rồi dùng cùng bộ cân bằng. Demo trường hợp đủ dữ kiện, thiếu điều kiện, vô nghiệm, nhiều nghiệm và chưa hỗ trợ.
3. **SC1-07/08:** ghost và preview toàn phản ứng, Enter nhận, Space tùy chọn mặc định tắt, Undo/restore/file/nháp trên Web/Desktop; kiểm IME thật và kết quả đến muộn. Không dùng suffix riêng vì người dùng đã làm rõ ý là cặp bọc.
4. **SC1-09/10:** đề xuất qua luồng Word thủ công, native/metadata/Undo; gói local và kiểm sau giải nén. Công bố từng host có bằng chứng. SC1-11 ghost inline Word chờ cổng riêng, không giữ E1.

Giữ nguyên cặp chung đang cài, kể cả `lc[...]`; người dùng có thể đổi sang `lc-[…]`. Cặp riêng chỉ khóa môn cho vùng và không tự chấp nhận kết quả suy/sửa. Lần cập nhật này chỉ lập kế hoạch, không đổi app đang chạy hoặc mở G2/G3.

## 8. Đợt 6 — đồ thị 2D đầu tiên

Người dùng đã chọn có thanh trượt ngay trong E1 ngày 2026-09-14. [Đề xuất trải nghiệm, quy tắc tham số và bài nghiệm thu chi tiết](e1/PLAN.md); đây là kế hoạch, chưa phải tính năng đã nghiệm thu.

Theo yêu cầu đi từ trải nghiệm, bắt đầu bằng [nhập → tự vẽ → chọn/chỉnh → copy](e1/USER-EXPERIENCE.md): chốt màn hình và các trạng thái trước, rồi E1-01 chuyển thành hợp đồng dữ liệu. Chỉnh sửa có đối tượng rõ: hàm/tham số, nét/nhãn, khoảng x từng đường, trục/lưới và khung nhìn. Tab Đồ thị tự preview biểu thức hợp lệ, đủ dữ kiện và cách hiểu đã xác định; một dòng cập nhật một đường.

Bổ sung sau phản biện về hàng trăm biến: bảng tham số động, tên có chỉ số `a_1…a_100`, giá trị tách khỏi slider; tìm kiếm/lọc/ghim những điều khiển cần dùng, giữ và tính đủ tham số ẩn. Đo tải 10/100/300 theo độ phức tạp trước khi công bố giới hạn, giữ nguồn khi quá tải. E1 vẫn vẽ một biến độc lập x; giao diện lát cắt cho hàm nhiều biến độc lập cần phạm vi riêng. Không tự gán giá trị cho biến để làm ra một đường 2D.

- **E1-01:** chốt PlotDocument có nguồn/candidate, biến độc lập, bảng tham số dùng chung (giá trị/khoảng/bước/phụ thuộc), miền lấy mẫu, khung nhìn, trục, nhãn và kiểu nét. Phân biệt khung nhìn, khoảng x người dùng giới hạn và miền xác định thực. Phạm vi đầu là `y=f(x)`; nhiều đường bật/tắt và phân biệt màu/nét; payload có version/migration.
- **E1-04:** bổ sung grammar có version và evaluator từ AST/bảng giá trị tham số. Bộ đầu: đa thức, phân thức, căn, `sin`, `cos`; radian ghi rõ. Phân biệt biến/hằng số/tên hàm/tham số, không tự biến lỗi chữ thành slider. Các cú pháp mới có case riêng, không đổi nghĩa corpus/snapshot cũ.
- **E1-02:** lấy mẫu theo miền, tách tại điểm không xác định/gián đoạn; cập nhật pan/zoom và đổi tham số, hủy kết quả cũ. Renderer nhận các đoạn/điểm đã tính. Kiểm điểm chuẩn, cực cùng dấu và điểm khuyết; không nối qua nơi chưa đủ dữ liệu.
- **E1-06:** UI tạo/sửa tham số dùng chung, slider và ô số, min/max/bước; một drag một Undo, Esc hủy và hỗ trợ bàn phím. Chỉ tính lại đường phụ thuộc, giữ khung nhìn khi kéo; kiểm đổi nhanh, a=0 và tiệm cận/biên miền dịch theo a.
- **E1-03:** chỉnh miền/trục/màu/nét/nhãn, Undo/Redo, nháp và lưu/mở toàn bộ tham số/cấu hình; chuột phải copy SVG/PNG theo scene hiện tại, có lệnh bàn phím và tải file khi clipboard không khả dụng.
- **E1-05:** cùng bài trên Web/Desktop và gói alpha local. Case bắt buộc: `x^2`, `1/x`, `1/(x-1)`, `sqrt(x)`, `sin(x)` cùng hai đường dùng chung a; kéo/Undo, tiệm cận/biên miền đổi theo tham số, nguồn sai/đổi nhanh và lưu/xuất đúng phiên.

**Demo kết thúc:** vẽ `y=a*x^2+b*x+c` và `y=a*x`, kéo a cập nhật cả hai, Undo một lần; thêm `1/(x-a)` kiểm tiệm cận dịch; đổi khung nhìn/nét, lưu từ Web mở trên Desktop giữ cả giá trị/khoảng/bước, copy SVG vào tài liệu. Thanh trượt hệ số thuộc E1; đường cong tham số `x(t), y(t)`, hàm ẩn, tọa độ cực, Play tự chạy, solver và mặt 3D chưa nằm trong E1 đầu.

## 9. Đợt 7 — hình học 2D theo thao tác trên giấy

1. **E2A-01:** GeometryDocument với ID, điểm/đoạn/đường/tia/đường tròn/đa giác/nhãn; prototype đặt A/B/C, nối cạnh, kéo đỉnh và cạnh. Chốt D-06 từ demo trực tiếp.
2. **E2A-02:** chọn/kéo/xóa, snap bật/tắt, pan/zoom, nét liền/đứt, độ dày, nhãn kéo độc lập. Kéo cạnh tự do dịch hai đầu, các cạnh chung đỉnh cập nhật. Một drag là một Undo; Esc hủy drag về vị trí trước.
3. **E2A-04:** công cụ tạo trung điểm, song song, vuông góc và điểm trên đường tròn. Quan hệ chỉ sinh khi người dùng chọn công cụ. Bắt đầu bằng quan hệ phụ thuộc có hướng; kiểm chu trình, hình suy biến và xóa đối tượng cha, không hứa solver ràng buộc tổng quát.
4. **E2A-03:** lưu/mở scene và history cần thiết; chuột phải copy SVG bất kỳ lúc nào scene hợp lệ, xuất PNG; cùng cấu trúc ở preview và file.
5. **E2A-05:** nghiệm thu Web/Desktop bằng tam giác có đường cao AH nét đứt, kéo C giữ AH vuông góc, Undo/Redo, kéo nhãn, lưu/mở, copy SVG. Thêm một hình minh họa khối bằng các đoạn 2D có nét khuất do người dùng đặt.

Lưu lệnh có nghĩa để hỗ trợ Undo và mở đường cho phát lại các bước dựng sau này. Nét phác tay và animation bàn tay không phải điều kiện hoàn thành editor đầu.

## 10. Word nâng cao, phát hành và 3D

- **M4:** quan sát vùng nhỏ, fx đúng mục tiêu/vị trí, candidate khi được yêu cầu; sửa nguồn qua Desktop và trở lại đúng equation. W0-05 cung cấp bằng chứng trực quan; phần IME và focus phải đủ theo G2. Nếu chờ nghiệm thu, luồng M3 vẫn là khả năng Word đã công bố.
- **M5A:** dùng parser marker đã có; chỉ tích hợp trigger/vùng/cấu hình và giao dịch Word. Đóng dấu, đổi cặp bọc, restore hoặc Undo phải tuân cùng quyền chuyển; restore không tự kích hoạt vòng chuyển lại.
- **M6:** đóng gói Desktop/connector và web; cập nhật/rollback/migration/lưu nháp, runtime nhúng, hiệu năng và pilot tác vụ với cả ba nhóm người dùng. Ghi capability của từng host/miền và giới hạn thực; kiểm phạm vi phát hành đã chọn, không lấy một bản Web chạy được để chứng nhận Word.
- **E2B:** prototype camera/chọn/kéo theo mặt phẳng trước; sau đó khối đa diện, hình chiếu và nét khuất trong phạm vi đã chốt; cuối cùng lưu/mở camera, Undo và SVG đúng góc nhìn. Quan hệ 2D và cơ chế lệnh được dùng lại; phép chọn/chiều sâu phải có kiểm riêng.
- **M5B:** chỉ chốt D-01 khi có kết quả dùng thử thực; triển khai theo lựa chọn đã chốt và G3. Không suy ra lựa chọn A/B từ việc người dùng đang bận.

## 11. Bằng chứng cần có khi kết thúc mỗi đợt

| Nội dung | Cách ghi |
| --- | --- |
| Phạm vi và trạng thái | Task nào DONE, tính năng nào dùng trên Web/Desktop/Word; phần còn chờ ghi riêng |
| Kết quả chạy lại | Lệnh, build/version, máy/trình duyệt/Word, corpus và số ca đạt/lỗi |
| Bài sử dụng thực | Artifact xuất, file lưu/mở và kết quả thao tác; phân biệt test tự động với quan sát/người thử |
| Thay đổi dữ liệu | Version mới, case tương thích cũ, cách đọc dữ liệu chưa hỗ trợ |
| Demo tiếp theo | Một đầu ra có thể dùng thử và task đầu tiên đủ điều kiện |

M3, WEB0, SH và WEB1 đã có artifact nghiệm thu theo phạm vi riêng. [WEB1](web1/REPORT.md) đạt 4/4 alpha local; các đường dẫn đồ thị/hình học/Lý/Hóa là nơi dự kiến ghi bằng chứng khi triển khai, không tăng tiến độ chỉ từ việc cập nhật kế hoạch.
