# E3 — Ưu tiên Hóa và Lý trước đồ thị

Ngày: 2026-09-14. Người dùng yêu cầu làm Lý/Hóa trước E1. Phạm vi dưới đây đã triển khai trong E3 alpha local; xem [REPORT](REPORT.md) và [grammar đã chốt](GRAMMAR.md). Cập nhật kế hoạch ngày 2026-09-15: lấy [SC1 cặp bọc theo môn và Hóa thông minh](../sc1/PLAN.md) trước khi quay lại E1. SC1 là mốc mới, đã triển khai cặp bọc Web/Desktop; không mở rộng ngầm phạm vi E3 đã nghiệm thu.

## 1. Thứ tự và phụ thuộc

**E3-01 → E3-02 → Hóa → Lý → SC1 → E1 đồ thị → E2A hình học 2D.** E3 đã hoàn tất; chi tiết SC1 ở kế hoạch riêng.

- Giữ Hóa trước Lý theo baseline đã có; việc đổi ưu tiên lần này đưa cả hai lên trước đồ thị, không tạo phụ thuộc parser Lý vào parser Hóa.
- E3-01/02 đã DONE dựa trên hợp đồng core M1. E3-02 làm nền miền/model/serialization; mỗi miền tiếp tục parser → Web/Desktop → nghiệm thu Word riêng.
- SH/WEB1 và M3 đã có phần nền tương ứng; không cần chờ E1, hình học hoặc auto-Space để bắt đầu.
- Nếu Word Hóa còn vướng môi trường/cổng riêng, ghi đúng trạng thái rồi tiếp tục parser/UI Lý. Không dùng việc vẽ đúng trên Web để công bố native Word đã hỗ trợ.
- Web tiếp tục local. W0 chờ phản hồi của người dùng; không yêu cầu thử lại để mở E3. Chỉ kiểm Word E3 bằng tài liệu thử thuộc tác vụ khi tới task tương ứng.

## 2. Bắt đầu từ trải nghiệm trong tab Công thức

**Yêu cầu UI mới nhất của người dùng:** giữ nguyên toàn bộ giao diện và cách dùng hiện tại của Toán; chỉ thêm ba checkbox độc lập **Nhận diện: Toán / Lý / Hóa** vào phần tùy chọn đang có. Đây là thay đổi duy nhất của bố cục E3. Ô nguồn, preview, cách sửa nguồn, lựa chọn candidate/fx, copy/xuất, mở/lưu, nháp và phím tắt dùng đúng luồng hiện tại trên Web/Desktop.

Bỏ đề xuất bộ chọn Tự nhận diện/Toán/Lý/Hóa loại trừ lẫn nhau, nhãn miền thường trực và công cụ chèn ký hiệu mới. Người dùng có thể bật một, hai hoặc cả ba checkbox. Ví dụ Hóa được bật thì `H2SO4` có thể trở thành H₂SO₄ qua chính preview công thức đang có, rồi copy/sửa như với Toán.

Luồng chuẩn vẫn là **gõ nguồn → xem công thức đã hiểu → sửa ngay tại nguồn → copy/xuất hoặc lưu**. Ba checkbox chỉ giới hạn những miền được phép tham gia nhận diện, không phải lệnh chuyển nội dung sang một môn. Chế độ Cách đọc hiện có (Một công thức / Vùng có cặp bọc / Nhận diện trong câu), marker và các cài đặt khác giữ nguyên tác dụng; checkbox miền không thay thế các chế độ này.

### Hành vi của checkbox

- Bật nhiều miền: nhận diện trong tập được bật, chỉ phân tích miền có dấu hiệu phù hợp. Chỉ bật Hóa không có nghĩa mọi câu chữ đều thành hóa; mọi bộ nhận diện vẫn cần kiểm nguồn theo hợp đồng hiện có.
- Cùng cách hiểu/cùng cấu trúc xuất được nhiều miền nhận ra chỉ hiện một kết quả tương đương. Nếu có cách hiểu khác nhau hợp lệ, dùng đúng cơ chế lựa chọn/candidate đang có, với **tối đa ba phương án tổng cộng**, không phải ba phương án mỗi môn. Không tự bung hộp chọn môn mới hoặc đổi nghĩa theo thứ tự checkbox.
- Nhận diện Lý có thể dùng lại phép toán/ký hiệu chung kể cả khi checkbox Toán tắt. Các checkbox bật/tắt nhận diện miền ở đầu vào, không gỡ năng lực tính cấu trúc Toán bên trong parser Lý/Hóa.
- Tắt cả ba: tạm ngừng nhận diện nguồn mới; giữ nguyên nguồn, nháp và file. Dùng vùng trạng thái hiện có để giải thích khi cần, không tạo kết quả mới hoặc xuất một kết quả cũ dưới nguồn mới.
- Đổi checkbox làm yêu cầu phân tích cũ hết hiệu lực; không sửa raw source. Snapshot/công thức đã lưu vẫn xem, xuất và khôi phục được theo phiên bản/cách hiểu đã chọn; không bị xóa hoặc tự diễn giải lại vì bộ nhận diện tương ứng đang tắt. Sửa nguồn mới áp dụng cấu hình hiện tại.
- Mặc định chuyển tiếp đề nghị: Toán bật, Lý/Hóa tắt để giữ hành vi bản Toán đang dùng; nhớ lựa chọn của người dùng theo cơ chế preferences hiện có. File/cài đặt cũ thiếu trường checkbox giữ nghĩa Toán cũ; quy tắc version/lưu mở được chốt ở E3-02.
- Bật nhận diện không bật tự chuyển trong Word. Candidate, repair, xác nhận/Undo, nguồn và các điều kiện chuyển đổi tiếp tục dùng đúng hợp đồng hiện tại.

## 3. Hóa đầu tiên: người dùng nhập và chỉnh gì?

| Bài dùng | Kết quả cần thấy | Chỉnh sửa |
| --- | --- | --- |
| Nhập `H2SO4` | H₂SO₄ với chỉ số đúng | Sửa nguồn; chỉ số thay theo công thức mới |
| Nhập `Ca(OH)2` | Ngoặc nhóm và chỉ số được giữ đúng | Sửa nguyên tố, nhóm hoặc chỉ số |
| Nhập hệ số, ion hoặc phản ứng | Hệ số, điện tích, dấu cộng và mũi tên ở đúng vị trí | Sửa nội dung/điện tích/hệ số/mũi tên ngay trong nguồn |
| Gặp chuỗi dễ nhầm như `Co`, `CO` | Giữ hoa/thường, nhận diện theo những checkbox đã bật | Sửa nguồn hoặc chọn cách hiểu qua cơ chế candidate hiện có; không tự đổi chữ |
| Copy/lưu rồi mở lại | Nguồn, cách hiểu và hình thức công thức vẫn khớp | Tiếp tục chỉnh trong cùng editor |

E3-01 phải chốt cách gõ điện tích, mũi tên, khoảng trắng và alias qua corpus trước khi viết parser. Không mặc định mọi chuỗi chữ/số đều là hóa. Bản đầu xử lý ký hiệu/công thức/phản ứng dạng văn bản; vẽ cấu trúc phân tử, cân bằng phản ứng và kiểm tra tính đúng hóa học cần phạm vi khác.

## 4. Lý đầu tiên: người dùng nhập và chỉnh gì?

| Bài dùng | Kết quả cần thấy | Chỉnh sửa |
| --- | --- | --- |
| Nhập `v = 10 m/s` | Biến, giá trị và đơn vị được phân biệt rõ | Sửa giá trị/đơn vị trực tiếp |
| Dùng chỉ số và lũy thừa | Ký hiệu như v₀ hoặc đơn vị m/s² ở đúng cấu trúc | Sửa chỉ số/số mũ, nguồn vẫn được giữ |
| Dùng vector và chữ Hy Lạp | Vector và ký hiệu được trình bày đúng | Gõ ký hiệu/alias đã công bố vào chính ô nguồn hiện có |
| Gặp tên có thể là biến hoặc đơn vị | Nhận diện trong những miền đã bật, cách hiểu dùng preview hiện có | Người dùng chọn candidate khi cần, không tự đoán khoa học |

Alias cụ thể cho vector/chỉ số/Hy Lạp thuộc E3-01 và phải có ví dụ người dùng nhìn được; chưa lấy các cách gõ minh họa làm cú pháp đã hỗ trợ. Đây là nhập/trình bày công thức; giải bài, đổi đơn vị tự động, kiểm thứ nguyên và mô phỏng vật lý là phạm vi riêng.

## 5. Công việc lấy tiếp và tiêu chí

| Task | Việc cần làm | Đầu ra để đánh giá |
| --- | --- | --- |
| E3-01 | Giữ UX Toán, chốt ba checkbox nhận diện độc lập; cú pháp và case nhận diện/xung đột | Corpus cho tám tổ hợp checkbox, candidate tối đa ba tổng, nguồn/spans/repair và trường hợp từ chối rõ; không thêm luồng chọn môn |
| E3-02 | Hợp đồng dữ liệu miền, tập nhận diện được bật và phiên bản; nền snapshot/export | Lưu/mở cấu trúc/cấu hình mới; cài đặt Toán cũ có mặc định tương thích; snapshot không đổi khi tắt detector; host chưa hỗ trợ báo đúng |
| E3B-01/02 | Parser Hóa và tích hợp vào editor chung | Gõ các bài Hóa ở trên, preview, SVG/PNG/LaTeX/MathML, nháp và file qua Web/Desktop |
| E3B-03 | Hóa trong Word thủ công | Native, metadata, Undo, restore và save/reopen trên phạm vi công bố |
| E3A-01/02 | Parser Lý và tích hợp vào editor chung | Đơn vị/vector/chỉ số/Hy Lạp qua preview, xuất và file hai host |
| E3A-03 | Lý trong Word thủ công | Cùng vòng native/metadata/Undo/restore được kiểm riêng |

Trước mỗi task triển khai, xác định phạm vi runtime/host và tài liệu chuẩn cần đối chiếu. Chạy hồi quy nguồn/candidate/marker/snapshot Toán hiện có cùng các bài mới. Không công bố % hoặc ngày hoàn thành Lý/Hóa từ việc đổi thứ tự kế hoạch.

Bài kiểm UI E3 phải xác nhận thay đổi giao diện chỉ là ba checkbox nhận diện: cùng bố cục, ô nhập, preview/candidate và thao tác xuất như bản Toán. Kiểm bật riêng/kết hợp/tắt hết, nhập dở/ghép dấu khi đổi cấu hình, thiếu preferences mới, lưu/mở snapshot khi detector tắt và không có kết quả cũ áp nhầm.

## 6. Quay lại E1

Theo cập nhật SC1 ngày 2026-09-15, sau đợt Hóa thông minh alpha tiếp tục [UX E1](../e1/USER-EXPERIENCE.md) và [kế hoạch E1](../e1/PLAN.md). Thanh trượt và cách quản lý nhiều tham số vẫn thuộc phạm vi đã yêu cầu; không chờ ghost inline Word. Khi bắt đầu E1-01, đọc lại cấu trúc chỉ số/ký hiệu và phiên bản do E3 bổ sung để dùng chung phần phù hợp, không mặc định bỏ qua nghiệm thu riêng của đồ thị.
