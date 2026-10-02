# Ngữ pháp Toán đề xuất cho M0

Phiên bản: `vi-math-m0-proposal-0.1`. Trạng thái: **baseline kỹ thuật đã triển khai ở M1**, theo [ADR-002](DECISIONS.md); [kết quả kiểm chứng M1](../m1/REPORT.md). Tài liệu này giữ đặc tả lập ở M0 và tên phiên bản để truy nguyên corpus. Các lựa chọn đã được review trong phạm vi M0, không hàm ý người dùng phê duyệt riêng từng alias. Thay đổi lựa chọn phải đổi phiên bản grammar và cập nhật case liên quan.

## 1. Phạm vi và hai bước quyết định

Nhận diện quyết định **có nên xem vùng nguồn này như công thức**. Parser quyết định **vùng đã chọn có cấu trúc gì**. Parse thành công không tự cấp quyền thay Word.

- Desktop nhập công thức và Word chọn vùng rõ ràng: cho phép biểu thức đơn `x` hoặc `42`.
- Quan sát văn bản Word: số hoặc một chữ đơn không đủ bằng chứng. Biểu thức có toán tử hai ngôi, quan hệ, alias toán rõ hoặc cấu trúc như `2x` có thể được xét trong một vùng giới hạn. Đây là quy tắc nhận diện đề xuất, phải đo false positive bằng dữ liệu thực tế.
- Khi một token giống mã `A1`, từ `can`, từ ghép `canx`, URL, email hoặc đường dẫn được nhận diện là văn bản, không xé token để lấy một phần làm công thức. Không coi nhãn miền là quyền ghi.
- Câu có văn xuôi vẫn có thể chứa nhiều vùng toán độc lập; không lấy dấu câu hoặc từ nối ở ngoài vào replacement span. Corpus không yêu cầu tự chuyển cả tập vùng.
- Nguồn đầu vào lỗi hoặc chưa hoàn chỉnh có thể có chẩn đoán và đề nghị sửa. Không nhận một bản sửa là parse nguyên văn thành công.

## 2. Token, alias và số

| Nhóm | Các cách viết đề xuất |
| --- | --- |
| Cộng | `+`, `cộng`, `cong` |
| Trừ | `-`, `−` (U+2212), `trừ`, `tru` |
| Nhân | `*`, `×`, `·`, `nhân`, `nhan` |
| Chia/phân số | `/`, `÷`, `chia`, `trên`, `tren` |
| Lũy thừa | `^`, `mũ`, `mu` |
| Căn bậc hai | `sqrt`, `căn`, `can`, `√` |
| Quan hệ | `=`, `<`, `>`, `<=`, `>=`, `≤`, `≥`, `bằng`, `bang` |

Alias chữ có ranh giới token, nhận chữ thường; không tự lowercase toàn bộ đầu vào. Cho phép dạng gọn căn + số như `can2`, `căn2`, `sqrt2`. `canx` không tự tách thành `can x`. Ký hiệu `√` có thể đứng sát biến. Không bỏ dấu toàn bộ chuỗi để tìm alias: các alias không dấu là mục từ riêng.

Biến bản đầu là **một chữ Latin ASCII phân biệt hoa/thường**. `x` và `X` khác nhau. Chưa hỗ trợ tên biến nhiều chữ, chỉ số dưới, chữ Hy Lạp, lượng giác, log, tích phân, ma trận, hệ phương trình hay giải biểu thức. `xy` không tự tách thành `x*y` trong phiên bản này. Cần phép nhân rõ hoặc một dạng nhân ngầm được công bố dưới đây.

**Số thập phân đề xuất:** chấp nhận `1.5` và `1,5` khi dấu phân cách nằm sát giữa chữ số; AST dùng chuỗi giá trị `1.5`. Giữ nguyên nguồn `1,5`. Không suy diễn dấu phân cách hàng nghìn: `1.234` là số thập phân; `1.000,5` và `1,000.5` bị từ chối. `1, 2` không phải một số; danh sách/đối số nhiều phần tử nằm ngoài bản đầu. Dấu phẩy trong `1,5` đứng một mình vẫn không đủ để gợi ý trong văn xuôi. Chưa hỗ trợ `.5`, `1.`, số mũ khoa học và phân cách chữ số bằng khoảng trắng.

Chuẩn hóa nhận dạng NFC, các toán tử tương đương và khoảng trắng phục vụ token hóa. Không NFKC toàn bộ nguồn, không xóa zero-width character một cách âm thầm, không đổi hoa/thường. Xuống dòng kết thúc cửa sổ nhận diện thụ động; nguồn explicit nhiều dòng cần phạm vi riêng, chưa hỗ trợ trong grammar này.

## 3. Ưu tiên, kết hợp và phạm vi

Từ thấp đến cao:

1. Một phép quan hệ `=`, `<`, `>`, `<=`, `>=`; chưa hỗ trợ chuỗi `0 < x < 1`.
2. Cộng/trừ, kết hợp trái.
3. Nhân/chia và nhân ngầm được hỗ trợ, kết hợp trái cho kết quả trực tiếp.
4. Dấu đơn ngôi `+`/`-`.
5. Lũy thừa, kết hợp phải.
6. Nguyên tử và ngoặc `(...)`.

Ví dụ: `8/4/2` là `(8/4)/2`; `2^3^2` là `2^(3^2)`; `-x^2` là `-(x^2)`; `(-x)^2` giữ dấu trừ trong cơ số; `x^-2` có số mũ âm. Không tính ra đáp án hoặc rút gọn các ví dụ này.

**Căn có ngoặc** nhận đúng phần trong ngoặc: `sqrt(x+1)^2` là lũy thừa của căn `x+1`. **Căn không ngoặc** nhận nguyên tử tiếp theo và phần lũy thừa gắn với nguyên tử đó: `căn x mũ 2 cộng 1` là `sqrt(x^2)+1`. Muốn căn phủ tổng phải gõ `căn(x cộng 1)`. Muốn căn của một biểu thức âm dùng ngoặc, ví dụ `sqrt(-x)`. Chưa hỗ trợ căn trần lồng nhau `căn căn x`.

Với `căn x cộng 1`, kết quả nguyên văn theo grammar đề xuất là `sqrt(x)+1`. `sqrt(x+1)` nếu được đưa ra phải mang loại **repair**, vì nó thay phạm vi đã được grammar quy định. Đây không phải yêu cầu bắt buộc luôn phải sinh repair.

EBNF khái quát (token hóa và các hạn chế nhân ngầm áp dụng trước/sau EBNF):

```text
document   := sum [relation sum]
sum        := product {("+" | "-") product}
product    := unary {("*" | "/" | supportedJuxtaposition) unary}
unary      := ("+" | "-") unary | bareRoot | power
bareRoot   := ROOT (NUMBER | SYMBOL) ["^" unary]
power      := primary ["^" unary]
primary    := NUMBER | SYMBOL | "(" sum ")" | ROOT "(" sum ")"
relation   := "=" | "<" | ">" | "<=" | ">="
```

## 4. Nhân ngầm và mơ hồ thực sự

Đề xuất hỗ trợ các cặp liền nhau: số–biến (`2x`), số–ngoặc (`2(x+1)`), biến–ngoặc (`x(x+1)`), ngoặc–ngoặc (`(x+1)(x-1)`). Khoảng trắng giữa hai phần được phép trong vùng explicit nhưng không làm chuỗi văn xuôi thành toán. Chưa hỗ trợ biến–biến `xy`, biến–số `x2`, số–số `2 3` hoặc nhận `x(...)` là lời gọi hàm tùy ý.

Riêng mẫu phép chia nối ngay một tích ngầm, như `2/3x` hoặc `1/2(x+1)`, có hai quy ước người dùng thường có thể muốn áp dụng. **Đề xuất grammar cố ý bảo lưu hai cấu trúc**:

- Candidate `direct`: nhân/chia cùng mức ưu tiên và kết hợp trái, `(2/3)*x`.
- Candidate `interpretation`: tích ngầm gắn với mẫu số, `2/(3*x)`.

Gắn `AMBIGUOUS_IMPLICIT_DIVISION` và không auto. Đây là một mở rộng mơ hồ được khai báo trong grammar, không phải lý do sinh tùy ý các cấu trúc khác. `2/(3*x)` hoặc `(2/3)*x` có ngoặc/toán tử rõ thì chỉ có một kết quả. Mẫu dài chứa nhiều vị trí mơ hồ không được cắt danh sách còn ba rồi tuyên bố đủ rõ; từ chối auto với `AMBIGUITY_LIMIT` hoặc yêu cầu người dùng thêm ngoặc.

`x+1/2` không thuộc quy tắc trên: chỉ `x+(1/2)` là kết quả trực tiếp. Đề nghị `(x+1)/2` là `repair`. Tổng candidate của một vùng luôn ≤ 3; corpus đặt kỳ vọng sinh repair cụ thể chỉ ở các case có ghi rõ.

## 5. Sửa và bộ heuristic hữu hạn

Repair phải kèm mô tả và danh sách edit trên nguồn gốc: ví dụ thêm `)` ở cuối `can(2+3`. Vị trí chèn dùng span rỗng `[n,n)`. Không sửa ký tự nguyên bản trong snapshot và không dùng repair cho auto. Không thêm hàng loạt phép nhân, dấu ngoặc hoặc nội dung để biến văn xuôi thành toán.

Baseline có ba heuristic sửa được dùng trong corpus: thiếu đúng một ngoặc đóng ở cuối một lời gọi căn; mẫu một biến cộng một phân số số học như `x+1/2` cho phép đề nghị đưa biến vào tử; mẫu căn trần của một biến cộng một số như `căn x cộng 1` cho phép đề nghị căn phủ tổng. Heuristic không tự tổng quát hóa sang mọi tổng hoặc căn có cơ số/lũy thừa phức tạp. Bộ case quy định thứ tự và sửa cụ thể; parser/candidate engine cần cùng phiên bản với bộ heuristic này. Không sinh repair khi cấu trúc đã được người dùng viết ngoặc rõ.

## 6. Dấu vùng v0 — baseline D-05

- Hai dấu là chuỗi literal, không phải regex. Theo lựa chọn mới của người dùng, mặc định `open = lc[`, `close = ]`, tạo vùng `lc[...]`. Người dùng được tùy chỉnh cặp bọc; `toan[[...]]` vẫn hợp lệ khi cấu hình rõ `open = toan[[`, `close = ]]` (case M0-117).
- Cấu hình phải không rỗng, hai dấu khác nhau, không dấu nào là prefix của dấu còn lại. Không nhận CR/LF hoặc backslash trong cấu hình. Cấu hình lỗi không được kích hoạt nhận diện/chuyển; giữ cài đặt hợp lệ trước đó hoặc báo lỗi khi chưa có cấu hình hợp lệ.
- Một dấu mở hợp lệ chỉ tạo vùng khi có dấu đóng đầy đủ. Chưa đóng → giữ nguyên và chờ nhập; không sửa dấu bao bằng auto.
- Không hỗ trợ vùng lồng trong v0. Gặp dấu mở thứ hai trước dấu đóng của vùng ngoài → từ chối toàn vùng ngoài, không chuyển vùng con. Không cắt ở dấu đóng đầu tiên rồi ghi một phần vùng.
- Chưa hỗ trợ escape. Backslash sát trước dấu mở hoặc dấu đóng → `MARKER_ESCAPE_UNSUPPORTED`, giữ nguyên toàn vùng có liên quan. Không bỏ backslash để tiếp tục auto. Backslash bên trong biểu thức cũng chưa phải token grammar Toán.
- Bảo vệ URL/email/path được thực hiện trước dò marker bên trong các token đó; marker xuất hiện trong đường dẫn không cấp quyền sửa đường dẫn.
- Nội dung là toàn bộ chuỗi giữa hai dấu, kể cả whitespace; tokenizer có thể bỏ qua whitespace nhưng snapshot không bỏ. Nội dung rỗng không chuyển. Xuống dòng trong nội dung chưa được hỗ trợ ở v0.
- `replacementSpan` chứa cả hai dấu. Restore trả đúng `originalReplacement`, gồm dấu bao; Undo trả toàn bộ trạng thái trước giao dịch. Sự kiện do restore hoặc conversion của Locus tạo không tự kích hoạt một lần chuyển mới. Chỉ chỉnh sửa tiếp của người dùng hoặc một lệnh chủ động mới có thể làm vùng được xét lại.

Các quy tắc trên là baseline thực hiện D-05 đã chọn trong ADR-003. Nếu sau này bổ sung nesting hoặc escape thì tăng phiên bản và thêm corpus trước khi bật.

## 7. Những phần còn mở

- D-01: hành vi Space, con trỏ, nhập nối tiếp, Backspace và Undo rồi gõ tiếp vẫn pending.
- D-04: xử lý chỉnh sửa native là quyết định UX còn mở; không được ghi đè sửa mới bằng snapshot cũ.
- Lý/Hóa: không giả lập thành đại số chỉ để có AST. Case miền được giữ pending cho E3-01; không dùng `H2SO4`, `Co`, `CO` như bằng chứng grammar Toán đã hỗ trợ hóa học.

Không xem các file đặc tả này là bằng chứng implementation đã đạt. M0 đưa ra baseline đủ cụ thể để triển khai thử; khi root chốt cần ghi phiên bản và các lựa chọn tương ứng vào sổ quyết định.
