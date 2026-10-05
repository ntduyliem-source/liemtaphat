# Kế hoạch thực hiện từ baseline hiện tại

**Ưu tiên cập nhật 05/10/2026:** CT0–CT4 đã triển khai và kiểm Công thức Web local; [biên bản CT4](design-system/CT4-VERIFICATION.md) ghi build, bằng chứng và giới hạn. [Catalog 0.2.0 và source](design-system/README.md). Tiếp theo review bản này, chọn đợt bổ sung core từ C01–C08 trong [audit](design-system/CT4-CORE-AUDIT.md), và nghiệm thu/phát hành Desktop riêng khi mở đợt đó. Web/Desktop có shell, tích hợp và phát hành riêng; chưa tạo bộ cài mới từ lượt kiểm Web. Thứ tự H/WEB2 và các chi tiết UI lịch sử bên dưới không ghi đè ưu tiên này.

Cập nhật 2026-09-30. **G đã đạt alpha local 14/14**; Web/Desktop và connector Word đã có gói riêng. Bước sản phẩm kế tiếp là H/WEB2 khi được yêu cầu. D-FILE còn download IAB; SC1/UX1 còn ma trận IME; WD1 còn highlight nhiều vùng và đa DPI/màn hình. [Biên bản hiện tại](host-review/20260930.md), [roadmap](ROADMAP.md), [backlog](BACKLOG.md). Các phần dưới giữ chi tiết thứ tự A–H; không phải mọi đoạn lịch sử đều là trạng thái hiện tại.

## 1. Đầu vào đã có và phần cần làm tiếp

Dùng lại core Toán/Lý/Hóa, cặp bọc, solver/kho offline, proposal/snapshot, renderer, session/Undo, Web/Desktop và Word. SC1-01…07 giữ DONE theo build/bằng chứng cũ; SC1-09 đã đạt phạm vi Word thủ công ở E. SC1-08 giữ phần kiểm còn thiếu; SC1-10 còn nghiệm thu host và bàn giao. Không làm lại parser, solver, kho hoặc dựng một editor môn Hóa khác.

Bản trước dùng preview SVG của công thức riêng và nhận hỗ trợ bằng thay source. Yêu cầu mới chọn ở ô kết quả/cả đoạn cần model tài liệu nhiều vùng, map selection và history theo vùng. Vì thế phải làm DOC1-01 trước BAL1; không nối trực tiếp nút đũa thần vào thao tác thay toàn textarea.

Một luồng triển khai chính; chờ Word không chặn việc đủ đầu vào. Đầu mỗi cụm ghi phạm vi, bài demo, rủi ro cần kiểm và ước lượng sau khi đã đọc mã. Không hứa ngày phát hành từ số task.

## 2. Đợt A — Chốt trải nghiệm trước code

**Task: UX1-01.** Đầu vào: trao đổi người dùng, baseline và đặc tả BAL1. Đầu ra là wireframe có chú thích, bảng trạng thái và chuỗi thao tác; chưa là component chạy thật.

**Trạng thái: DONE — thiết kế được dùng làm đầu vào cho yêu cầu triển khai bước tiếp theo của người dùng.** Đã tạo [bốn wireframe và bốn walkthrough](ux1/DESIGN-REVIEW.md), [quy tắc UI/bảng trạng thái](ux1/UI-SPEC.md); đã xem và kiểm các nút ví dụ trong in-app browser. Wireframe vẫn chỉ là thiết kế; kết quả code B được ghi riêng, không lấy wireframe làm bằng chứng BAL1/clipboard/tray/Word đã chạy.

1. Thiết kế ô nhập dùng chung cho công thức đơn/đoạn; ô kết quả có chọn công thức/chọn text, toolbar copy và đũa thần. Không buộc người dùng chọn “một công thức/đoạn dài” trước khi dán.
2. Vẽ Desktop gọn, Desktop bung và Web rộng/hẹp. Tab Công thức/Biểu đồ/Hình học; 2D/3D trong Hình học; tab chưa làm được ghi rõ ở bản thiết kế và không giả dùng được.
3. Chốt trạng thái selection không có/chọn dở/chọn đủ; nút disabled, lý do, bước kế tiếp 2/3, dropdown batch/hoàn tác và auto.
4. Chốt wireframe khi nguồn sai, dữ liệu mơ hồ, đang xử lý, bị hủy, file cũ và có nội dung sửa sau hỗ trợ.
5. Review 4 chuỗi: một phương trình; ba phương trình trộn trạng thái; dán cả đoạn auto tắt/bật; ghost có sản phẩm rồi hủy riêng cân bằng. Làm rõ trạng thái trung gian sản phẩm/hệ số trước khi code.
6. Bộ quy tắc UI tối thiểu gồm chữ/khoảng cách/icon/màu/focus và component dùng ở các màn này; không mở rộng thành dự án template.

**Đạt khi:** người xem chỉ ra được vùng sẽ đổi, nút tiếp theo làm gì, bản copy là gì và cách quay về. Các chi tiết quota/giá không liên quan không chặn đợt này.

## 3. Đợt B — Editor gọn và trả nguyên đoạn

**Thứ tự: UX1-02 → DOC1-01 → DOC1-02.** Đã triển khai theo yêu cầu bước tiếp theo. [Báo cáo/mã/bằng chứng](ux1/PHASE-B-REPORT.md), [schema v5/v6](doc1/MODEL.md).

- UX1-02: thay bố cục editor chung bằng input → result → copy/options; icon PNG/SVG/download có label/tooltip, phím và trạng thái disabled rõ. Giữ session, phím, nháp và các checkbox/cặp đang có; chưa nhét graph giả vào UI.
- DOC1-01: chốt schema tài liệu gồm source, text blocks, formula regions có ID/revision/spans, selected reading, result override, decision giữ text/giữ khỏi auto và command history. Đã có v5 nhiều vùng và v6 override/history; đọc file v1…v4 không reparse snapshot, file mới không downgrade mất dữ liệu.
- DOC1-02: dán text dài, chia theo đoạn/ranh giới, chạy core từng vùng, trả toàn đoạn với công thức inline. Selection kết quả map về các ID công thức, không tính từ chiều rộng ảnh hoặc textContent của MathML.
- Giữ source gốc của phiên nhập, cùng câu chữ/xuống dòng ngoài vùng. Ngân sách khởi điểm đề xuất 100.000 UTF-16/tài liệu, 4096/vùng; đo trước khi công bố trần hỗ trợ. Không cắt raw khi vượt giới hạn, hủy giữa chừng vẫn lấy lại được đoạn.
- Dùng cách đọc trực tiếp duy nhất cho vùng rõ. Vùng mơ hồ/lỗi/chọn giữ text để nguyên và có fx. Chọn trọn công thức mới đưa vào lệnh biến đổi; một phía selection cắt dở không kéo thêm chữ ngoài vùng.

**Demo:** dán đoạn có Toán/Lý/Hóa, URL, emoji và xuống dòng → preview cả đoạn → chọn riêng/selection ngược/all → giữ một vùng là text → sửa câu bên cạnh, vùng giữ text vẫn nguyên.

**Kiểm trong cụm:** map nguồn/selection, không mất text, file cũ và smoke nhập/xuất. Không chạy OS/full suite ở mỗi lần sửa CSS.

## 4. Đợt C — Đũa thần cân bằng và hủy

**Thứ tự: BAL1-01 → BAL1-02 → BAL1-03 → BAL1-04.** Phụ thuộc model/chọn vùng DOC1. Reuse solver SC1-05, giữ kho SC1-06 cho suy sản phẩm riêng.

**Trạng thái: DONE trong phạm vi C.** Có lệnh/hủy đúng snapshot, lượt từng vùng, hai batch có hoàn tác, auto opt-in và file v7 tách sản phẩm/hệ số. Đã kiểm 20 nhóm BAL1, 20 nhóm content, 35 nhóm session/core và thao tác Web thật; Desktop publish cùng logic. [Bản local/cách thử](bal1/QUICKSTART.md), [bằng chứng và phần chưa kiểm host](bal1/REPORT.md). DOC1-03/SC1-08 vẫn giữ kiểm file hai host, clipboard native và bộ gõ thật riêng.

### BAL1-01 — Lệnh và dữ liệu phục hồi

Mỗi lệnh ghi vùng/phiên/source và result trước/sau, hệ số ban đầu, provenance, quyết định auto. Không dùng lệnh “hủy” để chạy lại solver hoặc đặt hệ số về 1. Ngăn snapshot cũ ghi đè nội dung đã sửa. Tách trạng thái trước cân bằng khỏi trước suy sản phẩm; file lịch sử gộp cũ giữ hành vi khôi phục gộp cũ.

### BAL1-02 — Chọn một vùng hoặc đi từng vùng

Nút chỉ sáng cho một phương trình Hóa đủ điều kiện trong selection, hoặc vùng do Locus cân bằng còn hủy được. Một bấm một phương trình từ trên xuống, hiện mục tiêu kế tiếp; xong lượt cân bằng mới chuyển hủy, xong hủy mới quay lại. Phương trình người dùng tự cân bằng sẵn không có hủy giả. Đổi selection hoặc dùng lệnh khác reset lượt.

### BAL1-03 — Hai batch có hoàn tác riêng

Cân bằng tất cả và Hủy cân bằng tất cả theo selection. Chụp đúng trạng thái trộn trước/sau, tính lần lượt, commit một lệnh. Mục vừa dùng đổi thành “Hoàn tác…”; bấm lại trả đúng trạng thái trước lệnh, không thực thi mục đối lập. Chỉ batch gần nhất còn khớp có quick undo; history thông thường vẫn tuần tự. Lỗi/hủy/stale không ghi dở; vùng không đủ điều kiện giữ nguyên và có tổng kết.

### BAL1-04 — Tự cân bằng và quyền bỏ qua

Checkbox mặc định tắt; auto chỉ chạy trên nguồn mới/đã sửa sau khi parser/IME ổn định. Dán đoạn chỉ cân bằng khi bật, không ảnh hưởng phần ngoài công thức. Hủy thủ công đánh dấu bỏ auto theo vùng/nội dung; render/reload/sửa câu bên cạnh không bật lại. Tắt checkbox không tự hủy các thay đổi cũ. Không tự nhận sản phẩm ghost hoặc repair.

**Demo chốt:** A đã cân bằng bởi Locus, B/C chưa, D tự cân bằng sẵn, E không có nghiệm → bấm tuần tự → thử cả hai batch hai lần → auto bật và hủy B → reload. A/B/C/D/E và dấu bỏ auto phải đúng [bảng hành vi](ux1/BALANCE-INTERACTION.md).

**Kiểm trong cụm:** chính các lệnh/Undo/batch và 12 tình huống BAL-UX; một lượt regression liên quan khi cả cụm ổn, không gọi bộ solver đã đạt lặp lại sau từng nút.

## 5. Đợt D — Dùng cả đoạn, tray và kiểm host

**Task: DOC1-03, UX1-03, SC1-08.** Đây là bản review Web/Desktop độc lập với Word.

**Trạng thái: có bản D, chưa DONE.** DOCX/HTML đã có, 7/7 nhóm kiểm xuất và 75/75 nhóm liên quan đạt; Desktop publish và mở lần hai cùng profile không tạo phiên mới. D-WORD-01 đã đạt cả hai mẫu mở/lưu/đóng/mở lại và PDF. Công cụ native chưa điều khiển được click/tray; IAB còn clipboard/download cần đối chiếu. [Danh sách D-HOST/D-CLIP/D-FILE và cách tiếp tục](doc1/REPORT.md), [bản local/cách thử](doc1/QUICKSTART.md). Khép đúng các bài thiếu khi host khả dụng.

1. DOC1-03: lưu/mở tài liệu hỗn hợp, source/result/history/ignore; Web ↔ Desktop đúng. Copy cả đoạn bằng định dạng hỗ trợ, copy công thức PNG/SVG riêng; phạm vi được chỉ rõ. Tải DOCX chứa OMML là đường xuất đề xuất để kiểm native, còn giữ font/bảng/hình của clipboard đầu vào để đợt rich text sau.
2. UX1-03: tray mở/ẩn hộp gọn, bung/thu cùng một phiên. Đóng về tray và Thoát có nhãn rõ; không tự mở Word/bật startup Windows. Ẩn cửa sổ không mất nháp, không chạy hai phiên Undo riêng.
3. SC1-08: giữ bằng chứng cũ, chỉ kiểm bổ sung/kiểm lại tương tác bị UI mới ảnh hưởng. Hoàn tất Telex/VNI thật, Enter/Space/Esc/Tab, focus, nhập nối, selection/key-repeat và label/focus trợ năng trên baseline host được ghi.
4. Nếu OS đang có người dùng/công cụ lỗi, dừng phần OS, giữ trạng thái chưa đạt và bàn giao demo các phần còn lại. Không để vòng lặp test kéo dài chiếm hết các lượt làm.

**Demo:** dán → auto/chọn/hủy → copy cả đoạn → lưu Web mở Desktop → ẩn/bung/thu → nguồn/selection/Undo còn nguyên. Đo khởi động/đoạn dài sơ bộ để thấy phần nghẽn trước gói.

## 6. Đợt E — Đưa SC1 sang Word và chốt gói

**Thứ tự: SC1-09 → SC1-10.**

**SC1-09 DONE trong phạm vi thủ công đã kiểm; SC1-10 DOING.** [Bản E và bằng chứng](phase-e/REPORT.md) có 13 nhóm model/panel, 19 nhóm render cũ, các luồng native qua ManualConnect và 9 nhóm transaction/rollback. Đọc v1, lưu metadata v2; nguồn trước sản phẩm và trước hệ số tách riêng. Gói local là review cho tới khi các bài D/SC1-08 và chạy host sau giải nén đạt.

SC1-09 dùng M3/G1: chọn vùng → preview bản đã chọn hoặc nhận hỗ trợ → native/metadata trong một Undo → save/reopen → khôi phục. Dùng đúng snapshot đã xem, giữ cả nguồn trước cân bằng/trước suy sản phẩm khi có; không tính lại phản ứng khi mở. Thử cả vùng cặp mới và file metadata cũ. Bản đã sửa native phải từ chối dùng lịch sử cũ ghi đè.

SC1-10 chốt build/gói/hash local, offline, migration, lần chạy sau giải nén và số đo. Gom nghiệm thu Web/Desktop/Word của SC1 thành một đợt, dùng lại bằng chứng đúng build/phạm vi thay vì chạy lặp vô cớ. Mục tiêu độ trễ chưa đo vẫn ghi mục tiêu, không báo thành kết quả.

**Demo:** nguyên trạng chưa cân bằng vẫn chèn được → chọn cân bằng → native trùng preview → Undo/restore đúng → mở lại khi không có Locus vẫn thấy equation. Không tự cài lại connector khi chỉ làm thiết kế.

SC1-09 đã đạt phạm vi thủ công. Nếu các bài host còn chờ, có thể lấy E1 đủ đầu vào; SC1-10 vẫn giữ mở. WD1 là mở rộng riêng, không làm số % SC1 đổi theo nó.

## 7. Đợt F — Quét tài liệu Word đã có

**Đã bàn giao F 0.5.0 dạng review local:** WD1-01/03/04 đạt phạm vi panel, 8/8 luồng Word và 8/8 giao dịch/rollback. WD1-02 và phần inline của WD1-05 còn chờ kiểm host; công cụ Windows lỗi geometry. [Báo cáo và phạm vi](phase-f/REPORT.md), [cách dùng](phase-f/QUICKSTART.md). Không chờ phần inline để triển khai E1 khi người dùng chọn bước tiếp theo.

**Thứ tự: WD1-01 → WD1-02 → WD1-03 → WD1-04 → WD1-05.**

- WD1-01: quét vùng chọn/cả thân tài liệu, thu danh sách candidate/Range/revision mà không viết vào tài liệu. Trạng thái detected-text, cần chọn, giữ text, native-managed, stale được tách rõ.
- WD1-02: spike dấu highlight/fx theo trang/zoom/scroll/DPI mà không thay màu định dạng thật; panel điều hướng là fallback công khai nếu inline chưa đạt. Có dấu là phát hiện được, không thực hiện convert→restore để giả trạng thái.
- WD1-03: chuyển riêng; trong mỗi fx có “Chuyển N vùng đã nhận diện” và phạm vi rõ. Lập batch từ direct hoặc lựa chọn người dùng đã chốt, không tự nhận balance/repair/sản phẩm. Kiểm trước ghi và một Undo/rollback cho batch trong giới hạn.
- WD1-04: “Về text, giữ nhận diện” khác “Giữ text, bỏ qua khi chuyển tất cả”; giữ quyết định trong phiên và persistence được chọn, native drift/stale/re-scan không phục hồi sai. Snapshot chỉ làm bằng chứng cho chính revision của vùng.
- WD1-05: tài liệu trộn Toán/Lý/Hóa, bỏ qua một vùng, chuyển batch, hủy/Undo giữa lỗi, save/reopen và kiểu tài liệu/context được hỗ trợ. Phần chưa hỗ trợ như story ngoài thân/Track Changes/protected giữ rõ; không tắt cấu hình Word để test qua.

**Demo:** mở một tài liệu đang là text → quét → fx ở bất kỳ vùng nào → chuyển cả tập đủ điều kiện → trả một vùng về text và giữ bỏ qua → quét/chuyển lại không đè quyết định.

Không chờ D-01/G3 để làm lệnh thủ công này. M4/SC1-11 vẫn có cổng riêng cho quan sát và ghost khi nhập mới.

## 8. Đợt G — Đồ thị, hình 2D rồi 3D

Giữ nguyên ID và đầu vào đã bàn, xem [E1](e1/PLAN.md) và [thiết kế editor](WEB-AND-VISUAL-EDITORS.md).

**Cập nhật thực hiện 2026-09-19:** phần triển khai G đã khép ở build `20260919-064851-299`: PlotDocument v10 lưu cách hiểu, ba editor chạy thật, 29/29 + 16/16 + 35/35 kiểm tra, Web UI trên build cuối và hai ZIP đã verify. [Báo cáo G](phase-g/REPORT.md), [mở bản local](phase-g/QUICKSTART.md), [receipt gói](../artifacts/phase-g/packages.json). E1-05/E2A-05/E2B-03 ở REVIEW chờ người dùng làm checklist Desktop pointer/IME/file/clipboard; không còn code G độc lập nào phải chờ. WEB2-01 đã chốt hợp đồng pilot ngày 2026-09-24; bước code kế tiếp là WEB2-02.

| Mốc | Làm theo thứ tự | Demo |
| --- | --- | --- |
| E1 | PlotDocument → grammar/evaluator → lấy mẫu/gián đoạn → bảng tham số/slider → style/export/file → nghiệm thu hai host | Nhập parabol a/b/c, thêm đường dùng chung a, kéo/Undo, xét cực/điểm khuyết, copy SVG |
| E2A | GeometryDocument/UX → đối tượng và kéo tự do → quan hệ được chọn → file/export → nghiệm thu | Tam giác, đường cao nét đứt, kéo đỉnh/cạnh, Undo, lưu/mở, chuột phải copy |
| E2B | Camera/chọn/kéo theo mặt phẳng → scene 3D/nét/chiếu → nghiệm thu | Xoay khối, kéo đỉnh, Undo, SVG đúng góc nhìn |

E1 giữ bảng nhiều tham số, tìm kiếm/ghim slider, không tạo hàng trăm slider cùng lúc; biến thiếu giá trị không tự được gán. E2 thao tác giống vẽ trực tiếp, không tự khóa quan hệ vì nét trông gần vuông. Chỉ phát hành khả năng thực sự đã làm.

## 9. Đợt H — Tài khoản và hạn mức Web

**Thứ tự: WEB2-01 → WEB2-02 → WEB2-03 → WEB2-04.** WEB2-01 đã DONE cho pilot theo [hợp đồng lượt dùng](web2/QUOTA-CONTRACT.md); WEB2-02 là bước code tiếp theo. Chưa áp dụng paywall vào bản local.

| Gói | Công thức/ngày | Biểu đồ/ngày | Hình 2D/ngày | Hình 3D/ngày |
| --- | ---: | ---: | ---: | ---: |
| Guest | 5 | 2 | 2 | 2 |
| Free | 21 | 10 | 10 | 10 |
| Premium | 150 | 100 | 100 | 100 |

WEB2-01 đã chốt cho pilot: ngày quota 00:00 giờ Việt Nam, lần đầu xuất một vùng công thức hoặc một tài liệu đồ thị/hình là một lượt, sửa/copy lại thành phẩm đó miễn phí; đoạn dài báo trước số vùng mới. Guest chuyển sang Free giữ số đã dùng trong ngày; local/Desktop không có quota. Giá, provider đăng nhập/thanh toán/hosting Internet được quyết định khi chuẩn bị WEB2-04, không giả rằng đã có dịch vụ online. Các trần là đề xuất người dùng, chưa có bằng chứng tối ưu doanh thu.

WEB2-02 thiết kế/thực hiện account/session/entitlement và backend, giữ core local; không upload raw chỉ để đếm lượt. WEB2-03 ledger chống trừ đôi, lỗi không mất lượt, hết lượt giữ source và bản đã tạo; Guest hết lượt mời tạo tài khoản, không khóa đường khôi phục. WEB2-04 pilot/điều chỉnh, thanh toán/online khi đã quyết định; không tự đưa lên Sites hoặc chọn giá.

Giới hạn client/cookie có thể bị đặt lại; không hứa quota cứng của core offline có thể cưỡng chế như API server. Phải chốt rõ mức kiểm soát và phạm vi dịch vụ có quota trước code.

## 10. Cách làm và báo tiến độ

- Đợt A là đầu vào thiết kế đã dùng cho B/C; bằng chứng wireframe và bằng chứng sản phẩm được ghi riêng.
- B/C có báo cáo theo phạm vi; D tiếp tục phần xuất/tray/host còn thiếu, không lấy publish Desktop làm nghiệm thu thao tác Windows.
- Mỗi cụm có một demo sớm, build/smoke liên quan trong khi làm, một lượt kiểm tổng hợp lúc chốt. Lỗi nguồn/Undo/Word/batch vẫn kiểm trước khi dùng dữ liệu thật.
- Báo bốn điều: đã triển khai gì, đã kiểm gì, còn thiếu gì, task kế tiếp. Không dùng “test đang chạy” thay cho đầu ra hoặc lặp lại bộ đã qua khi không có thay đổi.
- Chỉ gọi DONE khi đạt đúng tiêu chí/bằng chứng của task. Tạm hoãn kiểm không làm mất code/bằng chứng đã có và không tự tính phần còn lại là đạt.
