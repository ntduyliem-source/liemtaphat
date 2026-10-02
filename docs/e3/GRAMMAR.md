# E3 — Hợp đồng nhận diện và cú pháp đầu

Baseline triển khai ngày 2026-09-14, giữ UI Toán và thêm đúng ba checkbox. Đây là hợp đồng parser/kiểm thử; phạm vi đã chạy đạt được ghi riêng trong REPORT. Core Toán v0.1, candidate ID và snapshot cũ giữ nguyên khi chỉ bật Toán.

## Nhận diện

Ba cờ độc lập Math=1, Physics=2, Chemistry=4; tập 0…7 đều hợp lệ. Mặc định 1. Tập 0 không tạo candidate cho nguồn mới; lưu/xem snapshot đã chọn không cần bật detector. InputMode Explicit/Passive/Markers giữ nguyên. Bật cờ không cấp quyền ghi Word.

Chỉ gọi parser miền có dấu hiệu đủ mạnh. Không chuyển URL/email/path hoặc thành phần bên trong chúng. Giữ raw UTF-16, nguồn NFC/NFD và spans; không xóa ký tự lạ để cứu một phần biểu thức Explicit/Markers. Passive giữ nguyên chữ và dấu câu ngoài vùng được nhận.

Toán chỉ bật: giữ đúng corpus hiện hành. Physics bật có thể dùng đại số nền dù Math tắt. Biểu thức chung chỉ một cấu trúc được xuất một lần. Với dấu hiệu Lý rõ và một cách đọc đại số khác (ví dụ số kèm đơn vị), ưu tiên cách đọc có cấu trúc đơn vị rồi cho cách đọc đại số ở candidate interpretation. Xung đột có warning, không đủ điều kiện auto. Không chọn theo thứ tự bật checkbox. Tổng tối đa ba; direct tối đa một, trước interpretation rồi repair. Không tạo thêm để lấp chỗ.

## Hóa

- Ký hiệu nguyên tố theo bảng IUPAC; phân biệt hoa/thường. Nhận công thức ghép nguyên tố, chỉ số nguyên dương, nhóm `(...)` và `[...]` lồng hữu hạn. Ví dụ H2SO4, Ca(OH)2, K4[Fe(CN)6]. Chỉ số Unicode H₂SO₄ có cùng nghĩa nhưng giữ nguồn.
- Hệ số nguyên dương ở đầu species: `2H2O` hoặc `2 H2O`. Hệ số không biến thành chỉ số. Không tự cân bằng hoặc sửa hệ số.
- Điện tích tường minh: `Na^+`, `Ca^2+`, `SO4^2-`, `NH4^{+}`, `SO₄²⁻`. `Fe3+` không được đoán là Fe³⁺: yêu cầu viết rõ điện tích; không tự chuyển số cuối từ chỉ số thành điện tích. Trạng thái chất/các chú thích trên mũi tên chưa thuộc grammar đầu.
- Phản ứng: species ngăn bởi `+`, hai vế ngăn `->`, `→`, `<->`, `⇌`; giữ hệ số, thứ tự và kiểu mũi tên. Một phản ứng chỉ một mũi tên. Một vế thiếu hoặc dấu điện tích không rõ báo lỗi, không tạo phản ứng một phần.
- Explicit/Markers: Co, CO, NaCl có thể là công thức nếu Hóa bật, với warning cho chuỗi chữ đơn giản thiếu dấu hiệu cấu trúc. Passive: chữ nguyên tố/chuỗi in hoa đơn thuần chưa đủ; cần chỉ số, nhóm, điện tích hoặc phản ứng. Không lấy Co trong một câu làm cớ đổi chữ.
- Chỉ số/hệ số 0, số âm/thập phân, nguyên tố lạ, ngoặc rỗng/lỗi và phần nối chữ thường bị từ chối. Không sửa hoa/thường hoặc rút gọn nhóm.

## Lý

- Dùng đại số Toán chung cùng cấu trúc mở rộng: chỉ số `v_0`, `v_{0}`, v₀; chỉ số một chữ hoặc số nguyên. Chữ Hy Lạp trực tiếp và tên chuẩn như `alpha`, `beta`, `theta`, `lambda`, `micro`, `rho`, `omega`, `Delta`, `Omega`. Alias Toán đang có giữ nghĩa cũ; `mu` đang là mũ nên dùng `μ` hoặc `micro` khi cần ký hiệu mu, không chiếm alias cũ.
- Vector: `vec(v)` và `vector(v)`, chỉ nhận một ký hiệu hoặc ký hiệu có chỉ số; không tự suy vector từ v viết hoa/in đậm. `vec(v_0)` phủ mũi tên lên ký hiệu có chỉ số; `vec(v)_0` đặt chỉ số ngoài vector.
- Số kèm đơn vị chỉ nhận khi có khoảng trắng rõ như `10 m/s`, `9,8 m/s^2`; nhận biểu thức đơn vị bằng `unit(m/s)` khi người dùng chỉ định. `m/s` tự nó còn là đại số, không tự gán hai chữ thành đơn vị.
- Danh mục đầu công bố trong mã: đơn vị SI nền và một tập đơn vị dẫn xuất/ký hiệu phổ biến, phân biệt hoa/thường; tiền tố được nhận theo bảng hữu hạn, không đoán tên đơn vị tùy ý. Phép nhân đơn vị dùng khoảng trắng, `*`, `·`; số mũ nguyên có thể âm. Dấu `/` chỉ có một cấp nếu không có ngoặc phân nhóm rõ. Nguồn mơ hồ có diagnostic, không tự đổi thành đơn vị khác.
- Giá trị và đơn vị tách bởi khoảng nhỏ khi dựng; ký hiệu đơn vị thẳng đứng, biến thường nghiêng; vector/chỉ số/Hy Lạp theo cùng cấu trúc khi xuất MathML/LaTeX/OMML. Không đổi đơn vị, kiểm thứ nguyên hoặc xác nhận phương trình vật lý.
- Scientific notation `1e3`, hàm lượng giác mới và các ký hiệu chưa thuộc danh mục không tự được thêm vào grammar Toán cũ. Các trường hợp mở rộng đều có phiên bản và case độc lập.

## Cấu trúc và lưu

Tài liệu miền có kiểu và version riêng; MathDocument cũ giữ nguyên tập node. Candidate vẫn tham chiếu nguồn, content/replacement spans, edits và provenance. Phiên bản snapshot mới chứa được miền mới; bộ đọc hỗ trợ cả snapshot cũ nguyên vẹn, không reparse để khôi phục. Cấu trúc bị tráo domain, node không đúng miền, checksum/ID/span sai phải bị từ chối.

MathDocument dùng core/grammar/snapshot 0.1 như cũ. ChemistryDocument và PhysicsDocument schema 0.1, core `locus-core/0.2`, grammar `vi-science-e3/0.1`, candidate-set `locus-candidate-set/0.2` khi có miền mới. Envelope `.locus` công thức ghi v2, đọc v1/v2; preferences ghi v2, đọc v1 mặc định Math. Đổi riêng checkbox giữ snapshot đã dựng và hủy lease/tác vụ chờ; sửa nguồn hoặc yêu cầu dựng lại mới phân tích theo flags mới. Tệp chứa snapshot miền đang tắt vẫn xem/xuất được.

Bộ lọc đường dẫn giữ nguyên với Toán. Khi Lý bật, chuỗi đơn vị trong whitelist đứng sau giá trị số (`15 km/h`, `10 kg/m^3`) có thể được đọc là đơn vị; `kg/m` đứng riêng và URL/email/đường dẫn tuyệt đối vẫn được bảo vệ. Phân tích số kèm đơn vị không cho phép diễn giải một đường dẫn tùy ý thành công thức.

Mọi phép chuẩn hóa đều ở token hoặc node, không sửa raw. Renderer chỉ dùng AST và escape ký hiệu; không nhúng raw như markup, không eval. Region tối đa 4096 UTF-16, depth tối đa 128, ngân sách/count/hủy theo nền hiện có; từ chối giữ nguồn. Word chưa nghiệm thu miền không được tự chuyển vì thấy exporter tạo được OMML.

## Nguồn chuẩn dùng để đối chiếu cách trình bày

- [IUPAC Red Book](https://iupac.org/what-we-do/books/redbook/) và [Brief Guide](https://iupac.org/cms/wp-content/uploads/2018/05/Inorganic-Brief-Guide-V1-3.pdf): thứ tự công thức, nhóm/chỉ số và điện tích. Cách gõ alias Locus là quy ước sản phẩm, không phải quy định IUPAC.
- [BIPM SI Brochure](https://www.bipm.org/en/publications/si-brochure): phân biệt ký hiệu đại lượng/đơn vị và cách trình bày số kèm đơn vị. Chưa triển khai kiểm chứng vật lý.
