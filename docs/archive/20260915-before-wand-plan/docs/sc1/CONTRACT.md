# SC1 — Hợp đồng triển khai 0.1

Chốt ngày 2026-09-15 theo [PLAN](PLAN.md). Người dùng đã yêu cầu triển khai. Tài liệu này xác định dữ liệu và kỳ vọng; trạng thái chạy thật nằm trong [BACKLOG](../BACKLOG.md) và báo cáo `artifacts/sc1`, không suy PASS từ bản hợp đồng.

## Phiên bản và tính tương thích

| Thành phần | SC1 | Quy tắc đọc bản cũ |
| --- | --- | --- |
| Cặp vùng | `sc1-markers/0.1` | `MarkerProfiles=null` dùng tuyến cũ, giữ nguyên wire và candidate ID |
| CandidateSet có RegionIntent | `locus-candidate-set/0.3`, header core `locus-core/0.3` | Reader giữ bộ 0.1/0.2; không đổi ID/AST của kết quả cũ |
| CandidateSet dùng cách gõ Hóa mới | `locus-candidate-set/0.4`, header core `locus-core/0.4` | Chỉ dùng khi có candidate grammar `sc1-chemistry/0.1`; giữ nguyên wire cũ |
| Preferences / tệp công thức có cặp theo môn | v3 | Đọc v1/v2; thêm preset khi bắt đầu dùng editor, không phân tích lại snapshot đã hoàn thành |
| Preferences có tùy chọn Space nhận Hóa | v4 | Đọc v1/v2/v3 với Space mặc định tắt; đây là preference editor, không phải quyền auto Word |
| Grammar Hóa hỗ trợ cách gõ mới (dành cho SC1-04) | `sc1-chemistry/0.1` | Opt-in có version; grammar E3 giữ nguyên |
| Draft/proposal (dành cho SC1-03) | `locus-assistance/0.1` | Tách khỏi CandidateSet, kết quả sinh mới không mang span giả của nguồn cũ |
| Tệp chứa lịch sử nhận (dành cho SC1-03/07) | v4 | Giữ v1/v2/v3; mở lịch sử không chạy lại solver/kho |
| Solver / kho (dành cho SC1-05/06) | `exact-balance/0.1` / `reaction-catalog/0.1` | Proposal lưu version và bản kết quả; nâng kho không thay snapshot đã nhận |

Các codec trong bảng đã triển khai ở SC1-01…07. Đọc version tương lai, thiếu trường bắt buộc, checksum hoặc quan hệ ID/span sai phải từ chối trước khi thay phiên đang mở.

## Cặp vùng

`MarkerProfile = { id, domain, open, close, enabled }`. ID và domain cố định: common/auto, math/math, physics/physics, chemistry/chemistry. Tối đa bốn mục, common bắt buộc bật. Các literal hiển thị trong settings là toàn bộ danh sách, không có alias ẩn.

Preset: chung `lc[` / `]`; Toán `toan-[` / `]`; Lý `ly-[` / `]`; Hóa `hoa-[` / `]`. Mỗi literal tối đa 64 UTF-16, Unicode hợp lệ, không rỗng/CR/LF/backslash. Kiểm toàn cấu hình; các opener đang bật không trùng, chứa/prefix hoặc chồng lấn nhau. Closer được dùng chung. Mục tắt vẫn phải có literal hợp lệ nhưng không gây xung đột với mục đang bật.

Migration giữ nguyên cặp chung cũ, thêm từng preset nếu không xung đột; mục xung đột được giữ ở trạng thái tắt và báo rõ. Đổi checkbox giữ snapshot đã hoàn thành. Đổi cặp làm yêu cầu đang chạy hết hiệu lực; không tự sửa văn bản nguồn.

`RegionIntent = { profileId, domain, grammarVersion }` đi cùng cả hai literal trong snapshot. Nó mô tả cách đọc đã dùng, không lấy môn từ settings hiện tại để đọc lại file. Cặp riêng override tập detector trong vùng đó. Cặp common vẫn theo detector; common và nguồn không cặp khi all-off đều không nhận diện mới.

`ContentSpan` chỉ phần thân, `ReplacementSpan` gồm nguyên dấu mở/đóng. Offset là UTF-16 trên raw, không lấy offset của bản chuẩn hóa. Chuỗi raw, NFD/emoji/khoảng trắng được giữ nguyên. Cặp chưa đóng có `Draft` và vùng được giữ chỗ, chưa có CandidateSet. Không hỗ trợ Locus lồng Locus; ngoặc nhóm `()[]{}` trong thân được theo dõi riêng để `hoa-[K4[Fe(CN)6]]` kết thúc đúng chỗ.

Giới hạn scanner: 128 mức ngoặc, vùng tối đa 4096 UTF-16, tối đa 32 vùng tại editor; hủy được trong vòng quét. Quá số vùng không trả một tập con như thể toàn nguồn đã được xử lý. URL/email/path, vùng rỗng/lỗi/lồng/chưa đóng không được bộ dò Passive lấy lại một đoạn con.

Một công thức chỉ nhận một cặp phủ toàn nguồn. Markers xét nhiều cặp đã đăng ký. Passive ưu tiên cặp trên các vùng được giữ chỗ và dò phần ngoài theo detector. Không có cặp mới được đăng ký trong Explicit/Passive thì dùng tuyến phân tích cũ.

## Hỗ trợ Hóa và kết quả sinh mới

Parser xác định chất; solver không phân tích lại văn bản. Chữ chuẩn phân biệt `Co/CO`, `No/NO`. Chữ thường chỉ chuẩn hóa khi đúng một cách phân tách; `co/co2/no` không chọn theo độ phổ biến. Không sửa `h20` thành `h2o` trong một lần nhận gợi ý. `=` chỉ là phản ứng khi Hóa đã được chỉ định/giải quyết miền. Thiếu sản phẩm là draft; dấu cộng cuối hoặc nhiều separator là lỗi, không đoán phần con.

Draft phải ghi source ID/revision, span thân/thay thế, intent và loại mũi tên; các chất trái vẫn giữ span nguồn thật. Proposal ghi ID, kind (`balance`/`complete-reaction`), source/revision, input span + raw trước đổi, settings/context fingerprint, conditions, result snapshot trên **nguồn kết quả riêng**, replacements và solver/catalog provenance. Không dùng kind `direct` cho việc tính toán hoặc tự thêm sản phẩm. Tổng lựa chọn của một vùng tối đa ba; mơ hồ còn lại được báo, không cắt danh sách để kết luận chắc chắn.

AcceptedTransformation lưu proposal đã nhận, nguyên nguồn trước/sau, selection/caret và snapshot đã preview. Một lần nhận là một lệnh trong history. Undo lấy state cũ nguyên vẹn; restore nguyên nguồn trước hỗ trợ. Chỉ công thức sau khi được nhận mới dùng cho clipboard/tệp/native; ghost chưa nhận không được serialize như nguồn thật.

Solver dùng số hữu tỉ chính xác và bảo toàn nguyên tố/điện tích. Tối đa 32 chất tổng cộng, 128 nguyên tố/hàng, 4096 bit cho giá trị trung gian/hệ số; kiểm cancellation và deadline host 5 giây. Chỉ nullspace một chiều với toàn hệ số cùng dấu, khác 0 được chuẩn hóa thành số nguyên dương tối giản. Trả `already-balanced`, `balanced`, `no-solution`, `non-unique`, `limit` hoặc `invalid`. Species trùng không bị gộp hoặc xóa; không tự thêm chất. Kết quả phải qua đếm bảo toàn độc lập trước khi đưa proposal.

Kho phản ứng là dữ liệu cục bộ có ID/version/nguồn/điều kiện/ngoại lệ. Matching không được suy lượng dư từ hệ số người gõ. Phân biệt có đề xuất, cần điều kiện, nhiều khả năng, không phản ứng trong điều kiện đã kiểm, không hỗ trợ và giới hạn. SC1-06 có 45 bản ghi trong phạm vi công bố tại [báo cáo kho](CATALOG-REPORT.md); số này không biểu thị độ chính xác với toàn bộ hóa học.

## Nhận và bàn phím

Trước nhận phải khớp source ID/revision, span, settings, điều kiện, proposal ID, caret/focus và trạng thái IME. Kết quả đến muộn hoặc phím bấm trước khi ghost hiện không được dùng để nhận. Preview phải hiển thị toàn phương trình, kể cả hệ số đổi ở trái.

Enter nhận ghost hợp lệ; Space mặc định nhập trắng, opt-in riêng mới nhận và thêm một trắng cùng Undo. Shift+Enter xuống dòng, Tab chuyển focus, Ctrl+Enter giữ dựng lại, Esc bỏ ghost cho đúng phiên nguồn/điều kiện. Khi composition, selection, key-repeat hoặc blur: không nhận. Cặp chưa đóng chỉ thay thân, không tự đóng; cặp đã đóng giữ literal và đặt caret sau closer.

SC1 này không mở lại cổng auto Word. Ghost inline Word là SC1-11; W0 G2/G3 vẫn chờ người dùng thử.

## Corpus và bằng chứng

- [66 tình huống](../../corpus/sc1/requirements.json) có ID/kỳ vọng/phase, trạng thái mặc định `specified`.
- [Corpus scanner](../../corpus/sc1/markers.json) là fixture executable có mode, flags, literal và expected regions/spans/diagnostics.
- `tests/Locus.SmartChemistry.Tests` đối chiếu raw/span/intent, cấu hình/migration, tamper, 504 wire cũ và 238 hợp đồng Toán.
- Worker parity, browser/Desktop, IME OS, Word native và gói phát hành có báo cáo riêng. Bằng chứng một host không thay cho host khác.
