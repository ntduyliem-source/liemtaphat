# Kiểm chứng kỹ thuật trước khi chốt kiến trúc

Ngày tra cứu/cập nhật: 2026-09-12. Phạm vi: tài liệu chính thức Microsoft và thử nghiệm Locus. **M0 đã chạy probe COM trên Word thực**, shared assembly/IPC và demo UX. [Báo cáo kết quả](m0/REPORT.md) tách PASS/FAIL/UNTESTED; [quyết định kiến trúc](m0/DECISIONS.md) chọn C# core chung và hướng COM/VSTO. Danh mục bên dưới giữ tiêu chí gốc để đối chiếu, không phải tất cả đã đạt.

**Cập nhật sau M3:** [alpha Word thủ công](m3/REPORT.md) đã đạt G1 trên baseline x86 qua luồng preview/xác nhận/native/Undo/restore và gói cài/gỡ. [W0](w0/REPORT.md) vẫn 4/6 mục; badge/DPI và phản hồi A/B chờ theo [checklist](w0/PENDING-ACCEPTANCE.md). G2/G3 chưa đạt. Các tiêu chí gốc dưới đây dùng để đối chiếu phần còn lại.

## 1. Những gì tài liệu xác nhận

**Cập nhật WEB0 ngày 2026-09-14:** đã chạy core C# trong WASM và cùng editor nhỏ trong WPF Hybrid, so 116 nguồn/238 contracts, đo payload/khởi động và thử Telex OS trên WASM WebView2. Chốt stack dùng chung cho SH; giới hạn serializer/trimming, timer và nhập liệu ghi ở [COMPATIBILITY](web/COMPATIBILITY.md), [ARCHITECTURE](web/ARCHITECTURE.md). Kết quả Web không thay đổi cổng Word bên dưới.

Các API dưới đây là cơ sở để thử nghiệm, không phải bằng chứng rằng toàn bộ luồng Locus đã khả thi hoặc an toàn trên mọi bản Word.

| Chủ đề | Cơ sở đã tìm thấy | Điều chưa được xác nhận cho Locus |
| --- | --- | --- |
| Nền tảng | Office.js dùng webview và hỗ trợ nhiều nền tảng; COM/VSTO dành cho Office trên Windows. [Office Add-ins platform](https://learn.microsoft.com/en-us/office/dev/add-ins/overview/office-add-ins) | Chọn framework, distribution, chạy offline và tích hợp Desktop trên baseline thực tế |
| Native math qua COM | `OMaths.Add` tạo equation từ Range; ví dụ dùng `BuildUp`. [OMaths.Add](https://learn.microsoft.com/en-us/office/vba/api/word.omaths.add) | Cấu trúc phức tạp, giữ định dạng ngoài vùng, save/reopen và metadata |
| Native math qua Office.js | `Range.insertOoxml` có đường chèn OOXML và thuộc WordApi 1.1 đã phát hành. [Requirement set 1.1](https://learn.microsoft.com/en-us/javascript/api/requirement-sets/word/word-api-1-1-requirement-set) | Tạo package OMML đúng cho từng candidate, native chỉnh sửa được, ghi metadata và Undo nhất quán |
| Quan sát nội dung | `Document.onParagraphChanged` thuộc WordApi 1.6 đã phát hành. Event có thể đến từ người dùng, add-in hoặc đồng tác giả; handler cần đăng ký lại khi runtime tải lại. [Requirement set 1.6](https://learn.microsoft.com/en-us/javascript/api/requirement-sets/word/word-api-1-6-requirement-set), [Word events](https://learn.microsoft.com/en-us/office/dev/add-ins/word/word-add-ins-events) | Availability của từng event trên baseline; per-keystroke, Space, IME commit và focus thực sự không được suy ra chỉ từ paragraph event |
| Undo | COM có `UndoRecord.StartCustomRecord` và `EndCustomRecord` để nhóm thao tác. [UndoRecord](https://learn.microsoft.com/en-us/office/vba/api/word.undorecord.startcustomrecord) | Nhóm native, metadata và các bước khôi phục của Locus thành đúng một Undo; chưa có bằng chứng tương đương cho toàn transaction Office.js trong đợt tra cứu này |
| Metadata | Word hỗ trợ custom XML parts lưu dữ liệu tùy ý và có cơ chế liên kết với content controls. [Custom XML parts](https://learn.microsoft.com/en-us/visualstudio/vsto/custom-xml-parts-overview?view=vs-2022) | Association qua sửa native/copy-paste/Undo/Save As; khả năng API cụ thể của connector và môi trường được chọn |
| Startup VSTO | `LoadBehavior=3` yêu cầu tải khi Word khởi động; lỗi hoặc thao tác người dùng có thể đổi trạng thái. [VSTO registry entries](https://learn.microsoft.com/en-us/visualstudio/vsto/registry-entries-for-vsto-add-ins?view=visualstudio) | Đồng bộ với lifecycle Desktop, nhiều cửa sổ và reconnect; không tự bật lại add-in người dùng đã tắt |
| Startup Office.js | Có startup theo document/shared runtime; tài liệu còn mô tả `OnDocumentOpened` với yêu cầu triển khai bởi quản trị viên. [Run code on document open](https://learn.microsoft.com/en-us/office/dev/add-ins/develop/run-code-on-document-open) | Requirement set, mô hình phân phối cho người dùng cá nhân và hoạt động offline; mở document không đồng nghĩa mở Desktop Locus |

Không ghi “Office.js không theo dõi được text” hoặc “VSTO đảm bảo nhận đúng mọi bộ gõ”. Chỉ ghi điều đã kiểm chứng trong ma trận môi trường. API xuất hiện trong tài liệu preview không được coi là sẵn có ở bản phát hành của người dùng.

Kiểm tra requirement set ở runtime và đối chiếu [bảng availability Word API](https://learn.microsoft.com/en-us/javascript/api/requirement-sets/word/word-api-requirement-sets), không chỉ dựa vào tên Office. Tham số giao diện tài liệu `view=word-js-preview` tự nó không có nghĩa mọi API trên trang đều là preview; dùng bảng requirement set để phân biệt.

## 2. Phương án kiến trúc đem đi thử

| Phương án | Hướng thử | Câu hỏi quyết định |
| --- | --- | --- |
| A — Desktop + Office.js connector | Core/artifact dùng chung, candidate → OMML/OOXML, runtime và kết nối cục bộ | Có đáp ứng offline, phân phối, Undo và tín hiệu nhập/focus trên baseline không? |
| B — Desktop + VSTO/COM connector | Core/artifact dùng chung, adapter Word, custom Undo và kết nối cục bộ | Có đáp ứng deployment, IME/focus, lifecycle và chi phí liên thông core mà không sao chép parser không? |

M0 chọn hướng B cho baseline Windows hiện tại dựa trên native/Undo/metadata probe. Hướng A được đối chiếu tài liệu và requirement sets; chưa sideload hoặc thử transaction runtime. Không triển khai cả hai song song. CW/G1 và các cổng tự động vẫn chưa đạt theo báo cáo M0.

Đề xuất kiến trúc cần thử: **candidate → OMML trực tiếp**. Không đưa chuỗi tiếng Việt cho Word tự phân tích lần nữa vì có thể tạo một kết quả khác preview. `OMaths.Add/BuildUp` là đường API cần khảo sát, không thay thế hợp đồng candidate đã chọn.

Preview và Word cần cùng cấu trúc và nghĩa; không hứa cùng font/khoảng cách hay giống nhau từng pixel.

## 3. Danh mục spike

### S-01 — Native equation và độ trung thực

- Dùng bộ candidate cố định để tách rủi ro Word khỏi việc chưa có parser hoàn chỉnh.
- Bộ mẫu khoảng 20 cấu trúc, bao gồm phép toán, phân số lồng, căn, mũ/chỉ số; thêm mẫu thăm dò tổng/tích phân/ma trận cho khả năng mở rộng. Mẫu thăm dò không tự đưa các cú pháp đó vào phạm vi MVP.
- Chèn giữa câu và các ngữ cảnh trong ma trận; lưu/mở, sửa bằng Equation của Word; đối chiếu nội dung/cấu trúc.
- Đạt khi native chỉnh sửa được, cùng candidate và không thay nội dung ngoài vùng. Ghi rõ cấu trúc hoặc ngữ cảnh chưa đạt.

### S-02 — Giao dịch, Undo và lỗi giữa các bước

- Chạy `source → native → Undo → Redo`, cả trường hợp có metadata.
- Cố ý gây lỗi sau từng bước ghi, đổi selection và đóng tài liệu khi tác vụ đang chờ.
- Kiểm tra nội dung, định dạng ngoài vùng, metadata và vị trí con trỏ theo hợp đồng đã chọn.
- Đạt khi một Undo có nghĩa nhất quán và lỗi không để thao tác nửa chừng. Không phát hành thao tác ghi Word khi còn mất nội dung hoặc ghi sai vùng đã biết.

### S-03 — Nguồn, IME, focus và dữ liệu hết hạn

- Thử bộ gõ thực tế trên baseline: nhập tiếng Việt có dấu/không dấu, Telex/VNI nếu môi trường hỗ trợ, composition, Backspace và paste.
- Thử chuyển sang Find, Ribbon, dialog, tài liệu/cửa sổ khác và ứng dụng khác.
- Chèn độ trễ có chủ ý giữa nhận diện và commit; sửa/xóa/chèn trước vùng, đổi cấu hình và thay nội dung.
- Thử Unicode có emoji, ký tự tổ hợp, xuống dòng và vùng đặc biệt trong Word. Đo mapping thực tế; không đồng nhất Word Range với offset chuỗi nếu chưa chứng minh.
- Đạt khi kiểm tra cuối từ chối mọi mục tiêu không xác minh được trong bộ tình huống; không ghi giữa composition hoặc ngoài vùng nhập thực sự.

### S-04 — Vòng đời metadata và phiên bản

- Thử custom XML/content control hoặc cơ chế khác có bằng chứng; chưa chốt một cơ chế chỉ vì API tồn tại.
- Sửa native; copy trong cùng tài liệu và sang tài liệu khác; Save As; Undo/Redo; save/reopen.
- Thử ID trùng, nguồn thiếu, version parser khác và metadata không tương thích; chứng minh mở lại được bộ kết quả đã biết sau nâng core, không thay chúng bằng kết quả parser mới.
- Chứng minh mở không có Locus vẫn sửa được equation native; detach chỉ bỏ quản lý của Locus theo hợp đồng.
- Đạt khi không gắn sai nguồn/candidate và không ghi đè sửa mới. Metadata không tin cậy phải dẫn đến ngừng quản lý, giữ nội dung hiện tại.

### S-05 — Tự chuyển bằng Space và gõ tiếp

- Prototype tối thiểu cho `x mũ 2 cộng 1`, `1 trên 2`, `căn x cộng 1`, sửa ở giữa và Undo rồi gõ tiếp.
- So sánh cách tiếp tục trong cùng công thức, cách kết thúc và quay về văn bản; ghi chuỗi phím, trạng thái con trỏ và kết quả sau từng bước.
- Đạt khi có hành vi rõ, có thể giải thích ngắn và không mất/tách sai nội dung trong bộ bài thử. Nếu chưa chọn được, D-01 còn mở và M5B chưa đủ điều kiện.

### S-06 — Khởi động, kết nối và vị trí `fx`

- Locus mở trước Word; Word mở trước Locus; nhiều cửa sổ/tài liệu; add-in bị tắt; đóng/mở lại; offline và runtime tải lại.
- Gợi ý bên cạnh đúng vùng khi scroll, zoom, đổi DPI hoặc màn hình. Khảo sát khả năng UI của từng connector, không giả định có tọa độ/vị trí chèn UI tùy ý.
- Đạt khi trạng thái kết nối trung thực, sự kiện không đăng ký trùng và action cũ bị hủy khi session đổi.
- Nếu `fx` cạnh dòng chưa khả thi, ghi rõ giới hạn; task pane/Ribbon có thể phục vụ bản chuyển thủ công thử nghiệm. M4 vẫn chưa hoàn thành phần trải nghiệm `fx` theo yêu cầu.

### S-07 — Core chung, offline và trao đổi Desktop–Word

- Thử gọi cùng implementation/artifact core từ Desktop và đường Word đã chọn; quyết định runtime/ABI/IPC theo kết quả.
- Thông điệp phải gắn phiên tài liệu, vùng, phiên bản nguồn/candidate và trạng thái cần xác minh; không dùng một biến “tài liệu hiện tại” toàn cục cho nhiều cửa sổ.
- Thử kết nối mất, Desktop/connector restart, kết quả trả về sai thứ tự và request hết hạn.
- Đạt khi không sao chép logic parser, không cần mạng cho nhận diện/dựng và không commit kết quả của session cũ.

## 4. Cổng quyết định và fallback

| Cổng | Điều kiện | Nếu chưa đạt |
| --- | --- | --- |
| G0 — Chọn đường native | S-01 có bằng chứng trên baseline; S-02/S-04 đủ dữ liệu để đánh giá rủi ro | Chưa chốt connector; tiếp tục core/Desktop độc lập |
| G1 — Phát hành chuyển có xác nhận | S-01, S-02, S-04 và kiểm tra mục tiêu trước ghi đạt cho luồng thủ công | Không phát hành thao tác ghi Word |
| G2 — Gợi ý khi gõ và auto vùng | S-03 đạt; S-06/S-07 đủ ổn định; mọi điều kiện vùng đóng/candidate/config được kiểm tra | Giữ luồng chọn vùng → xác nhận → native; các tính năng chưa đạt chưa phát hành |
| G3 — Space | S-05 đạt cùng toàn bộ điều kiện G2 | Tiếp tục thử nghiệm Space; có thể phát hành explicit region và xác nhận thủ công khi chúng đạt cổng riêng |

Fallback nào cũng giữ yêu cầu equation native cho thao tác Word đã phát hành. Không dùng ảnh để tuyên bố đã đáp ứng chức năng Word. Các fallback thu hẹp tính năng phải được ghi rõ trong trạng thái mốc và giới hạn bản phát hành.

## 5. Mẫu báo cáo spike

```text
Spike / ngày / người thực hiện:
Giả thuyết cần kiểm chứng:
Windows / Word build / 32–64 bit / bộ gõ / connector / requirement sets:
Mã prototype và cách chạy lại:
Case và thao tác tái hiện:
Kết quả quan sát được:
Điều tài liệu xác nhận và URL:
Điều suy luận, chưa được chứng minh:
Pass / fail / chưa kết luận theo từng tiêu chí:
Quyết định đề xuất và các task bị ảnh hưởng:
```

Không đổi một kết luận “chưa thử” thành “được hỗ trợ”. Sau khi chọn kiến trúc, giữ báo cáo spike làm bằng chứng và cập nhật đường dẫn vào backlog.
