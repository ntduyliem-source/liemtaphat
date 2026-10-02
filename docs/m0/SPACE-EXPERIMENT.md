# S-05 — Thử nghiệm UX nhập nối tiếp sau Space

Ngày: 2026-09-12. Trạng thái: **đã có prototype trình duyệt và kết quả mô phỏng; D-01 vẫn mở, S-05 Word chưa đạt**.

## Phạm vi bằng chứng

Prototype tại [`prototypes/m0-space/index.html`](../../prototypes/m0-space/index.html) so sánh hai chính sách nhập bằng một state machine cục bộ. Đây không phải add-in, không kết nối Word, không phát hiện vùng công thức trong câu, không ghi OMML và không phải parser sản phẩm. Không dùng kết quả này để xác nhận Undo của Word, IME Windows, focus Word hay native equation.

UI dùng HTML/CSS/JavaScript và MathML có sẵn của trình duyệt. Không framework, package npm, CDN, tài khoản hay mạng bên ngoài. Các mẫu nguồn do nhóm phát triển tạo; nội dung nhập chỉ ở bộ nhớ tab.

Bảy fixture được hỗ trợ sau khi thêm thử nghiệm dấu vùng: `x^2`, `x mũ 2`, `x mũ 2 cộng 1`, `1 trên 2`, `căn x`, `căn x cộng 1`, `2/3x`. Mapper chỉ NFC, trim và gộp khoảng trắng để tra bảng; nguồn nguyên văn được giữ riêng. Nó không cài đặt ngữ pháp chung. Các prefix của fixture có thể được đánh dấu đang thiếu; các chuỗi khác giữ văn bản. Nhãn `incomplete` ở demo không phải kết quả nhận diện production.

Fixture `căn x cộng 1` tuân theo [baseline cú pháp M0](GRAMMAR.md): `√x + 1` là `direct`; `√(x + 1)` là `repair` đổi phạm vi căn. Không gọi chúng là hai cách hiểu ngang hàng. Fixture `2/3x` dùng để thử mơ hồ chia–nhân ngầm: `(2/3) × x` đặt trước, `2/(3x)` là `interpretation`. Đây vẫn là các kết quả cố định cho bài thử.

## Hai phương án

| Thao tác | A — Giữ phiên nguồn | B — Chốt ngay ở Space |
| --- | --- | --- |
| Gõ chữ | Cập nhật nguồn và preview khi có fixture | Cập nhật phần nguồn chưa chốt |
| Space ở cuối nguồn đơn nghĩa | Giữ nguồn; preview cập nhật, tiếp tục cùng công thức | Chốt equation mô phỏng; đưa khoảng trắng ra sau; nguồn kế tiếp bắt đầu ngoài equation |
| Space giữa nguồn | Chèn khoảng trắng; giữ phiên | Không chốt giữa nguồn |
| Enter/nút Kết thúc | Chốt nếu có một kết quả đủ điều kiện, hoặc đã chọn một candidate khớp nguồn | Cùng quy tắc chốt thủ công |
| Có nhiều cách hiểu hoặc đề nghị sửa | Không chốt tự động; giữ nguồn cho người dùng chọn | Không chốt tự động nếu toàn nguồn hiện tại có trạng thái đó |
| Esc/nút Giữ văn bản | Đưa nguyên văn nguồn đang nhập vào văn bản mô phỏng | Cùng hành vi, không tự hoàn nguyên những equation đã chốt trước đó |
| Undo/nút hoàn tác | Khôi phục snapshot trước một action | Có thể hoàn tác lần chốt về nguồn và vị trí trước Space |

**A đang trì hoãn việc thay nội dung tới Enter.** Vì vậy A chỉ là một phương án để đánh giá thao tác nối tiếp, chưa đáp ứng yêu cầu mong muốn “Space thay native ngay trong Word”. Một biến thể cập nhật equation native trong khi giữ phiên nguồn sẽ cần thử riêng ở Word. Không coi đề xuất A là thay đổi đã được người dùng phê duyệt.

Trong demo, có đề nghị sửa cũng khiến Enter chờ người dùng chọn rõ, kể cả candidate trực tiếp đang đứng đầu. Đây là chính sách thí nghiệm để quan sát quyền lựa chọn, chưa phải quy tắc UX đã chốt. Một repair chỉ được chốt sau thao tác chọn repair và Enter; không được chọn thay người dùng.

Preview nền xanh/vàng là phiên đang nhập; gạch chân là kết quả đã chốt mô phỏng. Thanh con trỏ ở cạnh preview chỉ minh họa phiên, không ánh xạ vị trí trong MathML. Vị trí nguồn chính xác hiện trong textarea và dòng offset UTF-16; không phải Word Range. Các nút candidate hiện trực tiếp để so sánh trong bài thử, chưa mô phỏng popup `fx` theo hợp đồng sản phẩm.

## Cách chạy lại

Có thể mở `prototypes/m0-space/index.html` trực tiếp bằng trình duyệt; trang dùng script classic để không cần module server. Đường đã QA trong đợt này là localhost:

```powershell
node prototypes/m0-space/serve.cjs
```

Mở `http://127.0.0.1:4175`. Server chỉ bind `127.0.0.1`, chỉ phục vụ các asset khai báo (đã bổ sung trang dấu vùng). Đổi cổng bằng biến `LOCUS_SPACE_PORT` nếu cổng bận. Dừng server bằng Ctrl+C ở terminal chạy nó. Nếu server cũ chưa có route mới, khởi động lại hoặc dùng cổng 4176 theo [thử nghiệm dấu vùng](MARKER-EXPERIMENT.md).

Chạy kiểm tra và tạo lại trace tổng hợp từ root dự án:

```powershell
node --test prototypes/m0-space/state-machine.test.cjs
node prototypes/m0-space/record-observations.cjs
```

[`observations.json`](../../prototypes/m0-space/observations.json) lưu action, nguồn, vị trí, session, candidates và document mô phỏng sau từng bước cho cả hai chính sách. File ghi rõ `synthetic-state-machine-trace`; không phải nhật ký phím người dùng hay đo đạc Word.

## Kết quả đã quan sát

Node.js v24.15.0, Windows. **20/20 test state machine pass** ở đợt kiểm tra cuối ngày 2026-09-12. Test bao gồm bảo toàn nguồn ở mọi bước của cả bốn bài, continuation, split, nguồn thiếu, mơ hồ, sửa phạm vi, lựa chọn hết hạn, Undo, Backspace, sửa/chọn vùng ở giữa, Esc, không chốt trong composition tổng hợp và không sửa state cũ.

QA UI trên Codex in-app browser qua localhost: tải đủ trang; ảnh giao diện hai cột và MathML đã được kiểm tra trực quan; chạy mẫu mũ; chạy mẫu căn với nhãn `direct`/`repair`; chọn candidate trực tiếp, chốt rồi Undo; nhập tay `1 trên 2`, bật checkbox composition và bấm Space. Ở bước cuối, nguồn giữ nguyên và preview toán bị ẩn. Chưa dùng bộ gõ tiếng Việt hệ thống để tạo composition thật; handler DOM có trong code nhưng chưa đủ bằng chứng IME thực tế.

| Bài thử | Kết quả A | Kết quả B | Kết luận trong phạm vi mô hình |
| --- | --- | --- | --- |
| `x` Space `mũ` Space `2` Space `cộng` Space `1` Space | Một phiên chứa `x mũ 2 cộng 1 `; preview `x² + 1`; Enter chốt một equation | Space sau `2` chốt `x²`; `cộng 1 ` thành nguồn ngoài equation | Chốt ngay mỗi Space tạo lỗi tách trong kịch bản này |
| `1` Space `trên` Space `2` Space | Giữ toàn nguồn, Enter chốt `½` | Chốt `½` ở Space cuối | Hai cách cho cùng cấu trúc sau khi A kết thúc |
| `căn` Space `x` Space `cộng` Space `1` Space | Giữ toàn nguồn; preview trực tiếp `√x + 1`, thêm repair phạm vi | Chốt `√x` sau Space ở `x`; `cộng 1 ` nằm ngoài | Chốt sớm làm mất ngữ cảnh của phần nhập tiếp |
| `2/3x` Space | Giữ nguồn, yêu cầu chọn | Giữ nguồn, yêu cầu chọn | Không tự chọn giữa chia–nhân ngầm |
| `1 trên` Space, Enter | Giữ nguyên nguồn thiếu | Giữ nguyên nguồn thiếu | Không bịa mẫu số |
| Chốt `x²`, Undo, nhập ` cộng 1` | Có thể tiếp tục từ nguồn khôi phục | Có thể tiếp tục từ nguồn trước Space | Snapshot mô phỏng giữ được nội dung và vị trí; không chứng minh undo stack Word |

Undo của demo theo action: nút Bước tiếp nhập một cụm chữ là một action, nhập tay nhận các action từ `input`. Chưa gom lịch sử theo từ, chưa hỗ trợ Redo. Có thể sửa nguồn chưa chốt, nhưng không sửa trực tiếp các equation đã chốt trong preview; Undo là cách quay lại phiên trước. Các giới hạn này phải được giữ khi diễn giải thử nghiệm.

## Đề xuất và những gì còn phải thử

Đề xuất ưu tiên thử biến thể **giữ phiên nguồn liên tục** ở Word. Bản B cho thấy không thể lấy “chuỗi đang đúng cú pháp + một Space” làm kết luận chung rằng người dùng đã viết xong. Điều này chưa xác định duy nhất tổ hợp phím kết thúc hoặc hình thức giữ phiên phù hợp.

D-01 chỉ đóng sau khi có bằng chứng và lựa chọn được ghi nhận:

1. Chạy các chuỗi trên bằng phím thật trong Word, vừa gõ vừa xem vị trí con trỏ: người dùng có tiếp tục trong cùng công thức hay không; có bị chuyển sang văn bản ngoài ý muốn không.
2. So sánh ít nhất preview giữ nguồn và biến thể native đang mở phiên. Ghi chính xác mỗi lần thay Word, granularity Undo và cách phục hồi nguồn. Thử Enter, Esc và thao tác click ra ngoài; không giả định Enter luôn rảnh trong Word.
3. Thử sửa giữa, chọn một phần rồi thay, Backspace ở ranh giới, Undo rồi gõ tiếp, mất focus và chuyển tài liệu. Mỗi hành vi phải giữ nội dung ngoài vùng và từ chối kết quả hết hạn.
4. Dùng bộ gõ tiếng Việt thực tế của baseline; ghi Telex/VNI hoặc cấu hình được thử. Không chuyển trong lúc chưa chắc composition đã kết thúc. Browser checkbox không thay thế được bước này.
5. Cho người thử thuộc các nhóm sử dụng tự hoàn thành ba chuỗi bắt buộc, tiếp tục câu văn và sửa lại. Ghi hiểu nhầm, thao tác sửa, chỗ bị tách, mất nội dung và cách kết thúc họ tự tìm được; đợt hiện tại chưa có dữ liệu người tham gia.
6. Chốt quy tắc có thể giải thích ngắn, cùng case nghiệm thu về việc kết thúc phiên, quyền chọn repair và khôi phục. Nếu chưa chọn được thì tiếp tục để D-01 mở, M5B/G3 chưa đủ điều kiện; các cổng chuyển thủ công/vùng đánh dấu được đánh giá riêng.

Task liên quan: M0/S-05, D-01, các cổng G2/G3 của [kiểm chứng kỹ thuật](../TECHNICAL-DISCOVERY.md). Không có kết quả nào trong báo cáo này tự đóng D-03 hoặc chọn connector.
