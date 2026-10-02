# Hợp đồng dữ liệu core đề xuất — M0

Phiên bản: `locus-core-contract/0.1`, đã chọn làm baseline kỹ thuật M1 trong [ADR-002](DECISIONS.md). Tài liệu cụ thể hóa các bất biến của PRODUCT.md và phần thiết kế M0-01; runtime/connector/metadata được quyết định riêng. Core đã được triển khai: [API M1](../m1/CORE-USAGE.md), [kiểm chứng và giới hạn](../m1/REPORT.md). Kết quả prototype Word vẫn nằm riêng trong [báo cáo M0](REPORT.md).

## 1. Nguồn nguyên văn và hệ tọa độ

`SourceSnapshot` chứa `raw` bất biến, định danh snapshot, revision nguồn và phiên bản hợp đồng. Không thay `raw` bằng chuỗi NFC, LaTeX hoặc chuỗi tái dựng từ AST. Restore sử dụng nguồn nguyên bản đã lưu. Undo là giao dịch của ứng dụng đích và còn phải khôi phục phần trạng thái liên quan.

Mọi span công khai của core dùng **UTF-16 code unit, half-open `[start,end)`**, tương đối với `raw` của đúng snapshot. `start=0`; `end` có thể bằng độ dài UTF-16. Không dùng chỉ số code point của Python hoặc chỉ số byte UTF-8 làm offset core.

- `😀x^2`: emoji chiếm hai UTF-16 code unit; vùng `x^2` là `[2,5)`.
- `a\u0306`: hai code point và hai UTF-16 code unit; NFC `ă` có một code unit. Không dùng offset của NFC để ghi lại vào nguồn.
- Span không được cắt đôi surrogate pair. Span token cũng không được chủ ý cắt grapheme cluster; validator stdlib chỉ chứng minh ranh giới scalar UTF-16, không tuyên bố đã triển khai Unicode grapheme segmentation đầy đủ.
- `contentSpan` gồm toàn bộ nội dung giữa hai dấu nếu có, bao gồm whitespace; `replacementSpan` là phần thực sự thay. Với cặp bọc mặc định `lc[x^2]`, lần lượt là phần `x^2` tại `[3,6)` và toàn bộ vùng có dấu bao tại `[0,7)`. Người dùng được tùy chỉnh hai dấu; snapshot giữ cấu hình literal thực sự đã dùng, không suy ra từ mặc định của phiên bản hiện tại. Lưu cả `originalContent` và `originalReplacement` để không mất dấu bao. Baseline D-05 trong GRAMMAR.md chọn restore đúng `originalReplacement`, gồm dấu bao, đồng thời không auto lại từ sự kiện do restore tạo. Undo phải trả chính xác trạng thái trước commit.

Word adapter phải quy đổi bằng snapshot được xác thực của đúng document/story/range. Không giả định offset core bằng vị trí tuyệt đối của Word. Header, bảng, dấu đoạn, tracked revisions và content control cần ma trận riêng; chưa có chứng minh hỗ trợ từ tài liệu này.

## 2. Chuẩn hóa và ánh xạ nhiều–nhiều

Giữ song song `raw` và `normalized`; map là danh sách quan hệ giữa **tập span nguồn** và **tập span chuẩn hóa**, không chỉ một mảng cộng/trừ offset. Mỗi đoạn map có loại `identity`, `compose`, `expand`, `replace`, `elide` hoặc `synthetic`. Các tập span được sắp thứ tự, không chồng nhau trong từng tập; nhiều quan hệ có thể cùng tham chiếu một span nếu một token được triển khai thành nhiều phần.

Ví dụ đề xuất:

| Nguồn → chuẩn hóa | Quan hệ cần giữ |
| --- | --- |
| `ca\u0306n` → `căn` | Ghép nhiều code unit nguồn thành ít code unit chuẩn hóa |
| `x²` → `x^2` nếu phiên bản sau hỗ trợ | Một ký tự nguồn sinh nhiều token; không bật tính năng chỉ vì map mô tả được |
| `x\u00A0+\u00A01` → `x + 1` | Thay khoảng trắng; vẫn giữ chính xác NBSP trong nguồn |
| Alias `trên` → token DIV | Token liên hệ cả từ, không nhận offset của chuỗi `/` làm vị trí trong Word |
| Thêm `)` trong một repair | Ký tự synthetic neo ở `[n,n)`; chỉ thuộc candidate sửa, không thuộc nguồn hoặc parse nguyên văn |

Các vùng whitespace bỏ khỏi token stream vẫn có mapping/trivia đủ để truy nguyên. Diagnostic và source reference được chiếu về span nguồn; nếu không có một đoạn liên tục tương đương thì dùng nhiều span hoặc span bao có đánh dấu độ chính xác. Không tự chọn một offset gần đúng để thực hiện replacement.

AST production phải có stable node ID trong snapshot và source references cho các thành phần. AST trong corpus là **projection ngữ nghĩa** để so cấu trúc: cố ý không chứa node ID, glyph, vị trí pixel hoặc lựa chọn font. Validator corpus không kiểm tra source map của một parser chưa tồn tại.

## 3. MathDocument và candidate

MathDocument tối thiểu có `schemaVersion`, `domain`, root AST và bảng source references. Tập node semantic của corpus Toán gồm:

- `Number {value}`: chuỗi số hệ mười canonical cho parser, không dùng float nhị phân; không làm phép tính.
- `Symbol {name}`: phân biệt hoa/thường.
- `Unary {operator, operand}`: `plus` hoặc `minus`.
- `Binary {operator, left, right}`: `add`, `subtract`, `multiply`, `divide`.
- `Power {base, exponent}` và `Sqrt {radicand}`.
- `Relation {operator, left, right}`: `eq`, `lt`, `gt`, `le`, `ge`.

Ngoặc không phải node semantic riêng; cấu trúc cây ghi phạm vi. Cách viết ngoặc vẫn thuộc nguồn/trivia. MathDocument không chứa camera, điểm kéo hay toàn bộ scene hình học.

Mỗi `CandidateSnapshot` gồm ID, kind `direct | interpretation | repair`, MathDocument hoàn chỉnh, source snapshot/revision và các span, grammar/core/schema version, diagnostics, provenance quy tắc và repair edits khi có. Candidate repair không được rỗng danh sách edit nếu việc sửa có thể biểu diễn bằng thay nguồn trong corpus.

`CandidateSetSnapshot` lưu **toàn bộ bộ kết quả đã sinh cho lần phân tích, độc lập với trạng thái mở `fx`**, thứ tự, candidate đã chọn và chẩn đoán. UI mới hiển thị candidate trực tiếp không làm mất repair hoặc interpretation đã sinh nhưng chưa được mở xem; ví dụ M0-116 vẫn phải lưu đủ hai candidate. Tối đa ba candidate cho một vùng; tối đa một `direct` và phải đứng đầu nếu có. Khi chỉ còn repair thì UI nói rõ nguồn chưa parse nguyên văn thành công. Không gọi repair là interpretation và không tự chọn nó.

Preview, clipboard và Word export nhận đúng candidate ID + snapshot, không tự parse lại source hoặc LaTeX để tạo một lựa chọn mới. Renderer khác nhau có thể khác pixel nhưng phải giữ cấu trúc và nội dung.

Khi nâng core, mở lại snapshot đã lưu; không coi parse bằng grammar mới là bộ kết quả cũ. Có thể cung cấp thao tác phân tích lại rõ ràng thành snapshot mới, không ghi đè nguồn hoặc cấu trúc đang có trong Word nếu chưa xác nhận.

Nếu nội dung native bị sửa, metadata không khớp, ID bị trùng sau copy/paste hoặc phiên bản không đọc được: giữ native, ngừng thao tác quản lý không xác minh được. D-04 quyết định UX chỉnh sửa/restore; discovery quyết định cơ chế định vị và migration.

## 4. Tách đủ điều kiện nội dung khỏi quyền ghi

Core trả `contentEligibility`, không đưa ra lệnh ghi Word. Nội dung đủ điều kiện auto chỉ khi vùng hoàn chỉnh có đúng một parse nguyên văn, không interpretation cạnh tranh, không repair trong đề nghị của lần này, không warning/error và không mất thông tin ánh xạ. Sự tồn tại của repair không làm kết quả direct sai, nhưng lần phân tích có repair không đủ điều kiện auto theo yêu cầu “không chứa sửa lỗi hoặc cảnh báo”.

Host quyết định ngay trước commit:

1. Chế độ auto được bật đúng phạm vi; suggest luôn chờ xác nhận. Mọi hành vi Space chưa chốt không trở thành auto được phép.
2. Có trigger hợp lệ; vùng explicit đóng và cấu hình hiện tại khớp snapshot. Không quét/chuyển cả tài liệu khi chỉ bật công tắc.
3. Composition đã kết thúc; focus là vùng soạn thảo đúng cửa sổ và đúng tài liệu.
4. Document/story/range, nguồn, revision, selection/caret theo hợp đồng, candidate, cấu hình và phiên connector đều còn khớp. Dữ liệu không rõ hoặc đã đổi → hủy; không ghi vào vị trí cũ.
5. Target chỉnh sửa được và tổ hợp môi trường nằm trong ma trận đã chứng minh hỗ trợ.
6. Replacement native và metadata cùng một giao dịch, có Undo và xử lý lỗi giữa các bước đã được kiểm chứng.

Trong corpus, `may_proceed_after_revalidation` là **kỳ vọng chính sách cho tình huống giả lập**, tuyệt đối không phải xác nhận Word được ghi hoặc Word đã vượt các kiểm tra. `forbidden` là không auto trong tình huống đó; không cấm người dùng xem nguồn hoặc xử lý qua một thao tác rõ ràng khác.

## 5. Corpus và giới hạn bằng chứng

`corpus/m0/cases.json` chứa các case `specified` và `pending`. `specified` có nghĩa kỳ vọng đã được viết đầy đủ theo **yêu cầu sản phẩm hoặc baseline grammar đã chọn ở M0**, không có nghĩa tính năng đã được triển khai hay parser đã pass. `expectationBasis` phân biệt nguồn của kỳ vọng. Case pending có quyết định cần đóng và không bịa AST/cursor behavior. Không cần xin người dùng phê duyệt từng alias trong phạm vi xây baseline được giao.

Mỗi case có nguồn, ngữ cảnh, domain, vai trò người dùng, vùng với span UTF-16 và source text kiểm tra chéo, candidate semantic AST, diagnostics và chính sách auto. Diagnostic cũng dùng span nguồn của cùng snapshot. Repair edits dùng span nguồn, áp dụng đồng thời theo thứ tự từ phải sang trái nếu cần tái hiện đề nghị.

`tools/m0/validate_corpus.py` chỉ dùng Python stdlib để kiểm tra schema, offset, AST shape, ID, candidate kinds/ordering, giới hạn ba và tính nhất quán chính sách dữ liệu. Chạy thành công không chứng minh nhận diện, parser, renderer, Word, IME, source map production hay roundtrip thực tế đã đạt. M1 cần adapter chạy implementation lên corpus và báo riêng tỷ lệ đạt của các case không pending theo grammar được chọn.
