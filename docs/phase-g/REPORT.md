# Đợt G — Alpha local Đồ thị / Hình học 2D / 3D

Cập nhật 2026-09-30. G đạt **14/14 task trong phạm vi alpha local**, gồm ba walkthrough Desktop đã hoàn tất. Build hiện tại `20260930-042902-862` giữ canvas ổn định khi kéo, sửa thoát Desktop, bỏ fx khỏi kết quả Web/Desktop và sửa header ở khung hẹp. [Cách dùng](QUICKSTART.md), [receipt và checksum gói](../../artifacts/phase-g/packages.json), [nghiệm thu và giới hạn các cổng khác](../host-review/20260930.md).

## Thành quả

| Phần | Khả năng trong alpha G |
| --- | --- |
| Đồ thị | Canvas trống, Vẽ mới; nhập `y=f(x)` hoặc vế phải bằng cú pháp Việt; đa thức, phân thức, căn, sin/cos/tan/abs/ln/exp; nhiều đường dùng chung tham số |
| Cách hiểu | Preview LaTeX/MathML từ cùng AST dùng để vẽ; `x+1/2` giữ nghĩa trực tiếp và có nút **Cách hiểu** cho `(x+1)/2`; `1/2x` không vẽ trước khi chọn; thiếu ngoặc chỉ là đề nghị sửa; nguồn gốc luôn được giữ |
| Tham số | Tên đơn và chỉ số như `a_100`; giá trị/min/slider/max trên một hàng; cuộn ảo, tìm, lọc, ghim, gán nhóm; giá trị ngoài slider vẫn được dùng; một drag/một Undo và Esc |
| Trình bày đồ thị | Màu/nét/độ dày/ẩn hiện; miền riêng từng hàm; trục/lưới/nhãn, pan/zoom; tách cực và điểm khuyết; khóa xuất khi nguồn, cách hiểu hoặc sampling chưa an toàn |
| Hình 2D | Điểm tự do, đoạn/đường/tia/vector, tròn, tam giác/đa giác, chữ; kéo trực tiếp điểm/cạnh/nhãn; bắt điểm/trung điểm có nấc, Alt bỏ bắt; màu/nét/độ dày/dấu bằng nhau |
| Phép dựng và góc | Trung điểm, hình chiếu/đường cao, song song/vuông góc, điểm trên đường tròn, góc cho trước; góc nhỏ/lớn, độ/radian, cung/nhãn/số đo; quan hệ cập nhật khi kéo cha; tách quan hệ và xóa phụ thuộc có Undo |
| Hình 3D | Khung riêng; hình hộp/chóp/lăng trụ, mặt và cạnh; camera chiếu song song, xoay/pan/zoom; kéo đỉnh theo XY/XZ/YZ; góc không gian; cạnh khuất theo mặt che hoặc do người dùng chọn |
| Tài liệu | `.locus` v10 cho đồ thị, v9 cho hình học; giữ nguồn/cách hiểu/snapshot AST, tham số, style, quan hệ/camera; file plot v8 cũ vẫn mở; Undo/Redo, nháp riêng, SVG/PNG từ đúng scene preview |

Core C# giữ grammar/evaluator/sampler và cách hiểu. Application giữ tài liệu, history, quan hệ, camera và SVG. Razor editor được dùng chung cho Web/Desktop; worker Web chạy C# ngoài UI. JavaScript chỉ nối input, pointer, nháp và adapter xuất; không có `eval` hoặc parser toán thứ hai.

Quy tắc thao tác D-06 đã chốt cho alpha: điểm và cạnh tự do kéo trực tiếp; snap chỉ dính khi thả gần mục tiêu và có tín hiệu nấc; quan hệ hình học chỉ sinh khi người dùng chọn công cụ; điểm phụ thuộc phải được tách quan hệ trước khi kéo tự do; xóa cha xử lý phụ thuộc trong một lệnh có Undo.

## Bằng chứng

- [29/29 kiểm tra đồ thị](../../artifacts/phase-g/verification/plot-verification.json): grammar Việt/NFD/spans, evaluator, miền thực/cực/điểm khuyết, giới hạn/hủy, 10/100/300 tham số, gesture/history, cách hiểu trực tiếp/alternative/repair, `.locus` v10/migration/tamper và SVG.
- [16/16 kiểm tra hình học](../../artifacts/phase-g/verification/geometry-verification.json): phụ thuộc/chu trình/suy biến, snap, góc 3D, plane, Undo/xóa cha, nét khuất từng phần, mặt lệch phẳng, file/roundtrip và SVG.
- [35/35 hồi quy Application](../../artifacts/phase-g/regression-final/application-tests.json), kèm 240 hợp đồng core và 104 fixture worker native trên mã sửa ngày; thêm [22/22 content](../../artifacts/host-review/20260928/date-content-published/content-tests.json). Các guard Word riêng không được tính vào G.
- [UI Web và nguồn bằng chứng](../../artifacts/phase-g/verification/ui-browser.json): bản cuối bỏ fx khỏi nội dung, chi tiết ngoài ô, chọn bằng bàn phím và giữ text/Undo; header 2D/3D được kiểm trên build UI cùng ngày, trước thay đổi hai câu hướng dẫn. Các bài ngày/URL/email, copy, PNG và nháp giữ build gốc trong receipt. [Bằng chứng G trước sửa](../../artifacts/host-review/20260928/baseline-ui-browser.json) giữ các vòng 300 tham số, file/nháp/SVG/PNG, quan hệ 2D/plane 3D, `ƒx`, mơ hồ/repair và Undo; không coi chúng là các bài vừa chạy lại. Lượt sửa UI ngày 30/09 có thêm [22/22 content](../../artifacts/host-review/20260930-final/content/content-tests.json) và [20/20 balance](../../artifacts/host-review/20260930-final/balance/balance-tests.json).
- [10/10 Copy thật trong Desktop](../../artifacts/host-review/20260928-complete/clipboard-word-verified/report.json): text đúng phạm vi, PNG/SVG của bốn công cụ; PNG dán Microsoft Word, lưu/mở DOCX. Báo cáo adapter 6/6 trước đó có COM trỏ WPS và không dùng làm bằng chứng Microsoft Word nữa.
- [Gói đã giải nén và đối chiếu từng hash](../../artifacts/phase-g/packages.json): Web local khởi động từ bản giải nén; Desktop self-contained khởi động từ bản giải nén.
- Desktop startup trong lượt đóng gói: đúng executable sau giải nén, receipt PID khớp, có cửa sổ và phản hồi. Đường dẫn receipt/executable nằm trong [packages.json](../../artifacts/phase-g/packages.json); [startup ngày 19/09](../../artifacts/phase-g/verification/desktop-startup.json) là bằng chứng baseline. Startup không thay cho thao tác pointer/IME thật.

## Gói dùng thử

| Gói | Trạng thái | Điều kiện |
| --- | --- | --- |
| Web static local | VERIFIED, ZIP khoảng 13 MB | Giải nén, Node.js 22+, chạy `Start-Locus-Web.cmd`; đây chưa phải website Internet |
| Desktop Windows x64 portable | VERIFIED, ZIP khoảng 96 MB | Giải nén và chạy `Locus.Desktop.Shared.exe`; đã kèm .NET, cần WebView2 Runtime; đây chưa phải MSI/Setup |

Tên, đường dẫn, dung lượng và SHA-256 chính xác nằm trong [packages.json](../../artifacts/phase-g/packages.json). Gói Desktop không tạo shortcut/Start Menu, không tự cập nhật và không đăng ký connector Word. Website public, tài khoản và quota thuộc H/WEB2.

## Giới hạn công khai

- E1 chỉ nhận hàm thực tường minh theo `x`. `x=2`, phương trình ẩn, hàm từng khoảng, cực, dữ liệu điểm và hàm nhiều biến chưa hỗ trợ. Góc lượng giác dùng radian.
- Chưa có tiếp tuyến/giao điểm/tô miền/đo đồ thị, khóa tỷ lệ hai trục hoặc nhãn toán kéo riêng. Đây là backlog sau alpha, không phải nút giả trong G.
- Hình học chưa có chọn nhóm, biến hình, giao điểm tổng quát, phân giác hay cung tròn. Chữ tự do hiện là văn bản. Camera 3D là phép chiếu song song; chưa có gizmo hoặc phối cảnh.
- Kéo tự do có thể làm mặt 3D lệch phẳng. Editor báo và khóa xuất cho tới khi sửa hoặc Undo.
- Giới hạn tài nguyên: 32 đường/tài liệu, 32.768 ký tự/nguồn, 4.096 token/nút, 1.024 tham số, 12.000 lượt đánh giá mỗi đường; hình học tối đa 2.048 điểm, 4.096 cạnh và 512 đối tượng mỗi loại còn lại.

## Chốt nghiệm thu G

E1-05, E2A-05 và E2B-03 chuyển DONE: slider/kéo/Undo, snap trung điểm và quan hệ 2D, kéo theo XZ/camera 3D, Save/Open, Telex/VNI và Copy PNG/SVG đã có thao tác Windows thật. [Biên bản và artifact](../host-review/20260930.md) phân biệt build được kiểm với hồi quy kế thừa; không tuyên bố chạy lại toàn bộ bộ kiểm sau mỗi sửa CSS.

H/WEB2 chưa triển khai. D-FILE vẫn thiếu tệp download do IAB; SC1/IME và Word nhiều DPI/màn hình, highlight nhiều vùng, phản hồi W0 A/B giữ trạng thái riêng. Đây là bản portable/local đã dùng thử được, chưa phải Setup/MSI hoặc website Internet.
