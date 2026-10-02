# E1 — Đồ thị 2D và thanh trượt tham số

Cập nhật thực hiện: 2026-09-30. E1-01/04/02/06/03 và E1-05 đã DONE trong alpha G; walkthrough Desktop thật đã hoàn tất. [Báo cáo G](../phase-g/REPORT.md) và [receipt gói](../../artifacts/phase-g/packages.json) là trạng thái mới nhất.

**Thứ tự:** E3, UX1/DOC1/BAL1 và Word thủ công/WD1 đã có baseline; G triển khai tiếp các phần độc lập, không chờ đóng SC1-10/WD1/SC1-11. Giữ quyết định thanh trượt/nhiều tham số/UX bên dưới và trạng thái từng task trong [backlog](../BACKLOG.md).

## 1. Kết quả hướng tới

Trong tab **Đồ thị** của cùng editor Web/Desktop, người dùng nhập hàm theo cú pháp Locus, nhìn thấy công thức đã được hiểu, chỉnh tham số bằng kéo hoặc nhập số, chỉnh bản vẽ rồi copy SVG/PNG. Web tiếp tục chạy local trong đợt này. Dữ liệu xử lý tại máy; chuyển giữa hai host bằng cùng file `.locus`.

Bài dùng xuyên suốt: nhập `y = a*x^2 + b*x + c` → tạo tham số a/b/c → thêm `y = a*x` → kéo a và thấy cả hai đường thay đổi → Undo một lần → đổi nét/khung nhìn → lưu Web, mở Desktop → copy SVG có cùng đường và giá trị tham số đã chọn.

## 2. Đề xuất trải nghiệm

**Đọc trước:** [Luồng người dùng và danh sách thao tác chỉnh sửa](USER-EXPERIENCE.md). Theo yêu cầu mới, các bài nhập → tự vẽ → chọn/chỉnh → copy là đầu vào dẫn dắt các task phía dưới. Phân biệt sửa hàm/tham số, sửa cách trình bày và sửa khung nhìn; không gom thành một lệnh “chỉnh sửa” hoặc “free move”.

- Trái: các dòng hàm, công thức preview, bật/tắt từng đường; vùng **Tham số** dùng chung bên dưới. Phải thấy a đang được dùng bởi những hàm nào. Ít tham số có thể mở ngay các thanh trượt; nhiều tham số dùng bảng có tìm kiếm/lọc theo hàm và ghim những thanh trượt cần thao tác. Màn hình hẹp xếp phần nhập trước khung vẽ.
- Phải: khung tọa độ lớn; kéo để di chuyển khung nhìn, zoom, chỉnh nhãn. Màu/nét/độ dày ở phần thuộc tính của đường được chọn. Lưới/trục và tỷ lệ hai trục là lựa chọn hiển thị rõ ràng.
- Mỗi dòng giữ nguồn và candidate đã chọn. Cách hiểu trực tiếp đứng đầu; alternative và repair giữ phân loại của core, tối đa ba candidate. Repair chỉ được vẽ sau khi người dùng chọn rõ ràng, không âm thầm sửa nguồn.
- Trong tab Đồ thị, một dòng là một đường: khi biểu thức hợp lệ, đủ giá trị và cách hiểu đã xác định thì tự preview/cập nhật, không cần nút Vẽ hoặc checkbox detect riêng. Trường hợp nhiều cách hiểu hợp lệ chưa chọn thì hiện lựa chọn trước khi vẽ; sửa cùng dòng cập nhật đúng đường, thêm dòng mới tạo đường mới. Enter chỉ thêm dòng sau khi bộ gõ đã hoàn tất.
- Khi nguồn đang ghép dấu hoặc không hợp lệ, dòng đó có trạng thái chờ/lỗi; không gắn đường cũ với công thức mới. Những đường hợp lệ khác vẫn dùng được. Xuất phải cho biết chính xác những đường nào thuộc bản vẽ hiện tại; không tự xuất một bản đầy đủ cũ.
- Chuột phải trong khung vẽ có Copy SVG/PNG. Các lệnh xuất cũng có nút dùng bằng bàn phím/chạm. Thanh trượt và nút điều khiển không nằm trong hình xuất; chú thích công thức/giá trị tham số có thể bật để hình tự giải thích được.

## 3. Quy tắc tham số đề nghị

### Nhận diện và dùng chung

- E1 vẽ hàm tường minh `y=f(x)`: nhận biểu thức bên phải hoặc quan hệ có vế trái đúng `y`. x là biến độc lập; không tạo thanh trượt cho x hoặc nhãn y.
- Core mở rộng có phiên bản để nhận `sin`, `cos`, hằng số `pi`/`π` và `e` trong ngữ cảnh đồ thị. Các tên này không trở thành tham số. Không đổi nghĩa snapshot/corpus grammar cũ.
- Các ký hiệu tự do hợp lệ còn lại trong AST được liệt kê như a, b, c, k. Sau phản biện về hàng trăm biến, bỏ đề xuất giới hạn tên một chữ: E1-04 cần hỗ trợ ký hiệu có chỉ số nguyên như `a_1`, `a_100`, hiển thị a₁, a₁₀₀ và giữ nguồn/spans. Đây là các tham số vô hướng có tên riêng, chưa phải truy cập mảng. Phân biệt hoa/thường; dạng chỉ số/alias được chốt trong grammar có phiên bản, không tự hiểu `a1` theo nghĩa mới trên snapshot cũ. Từ lỗi như `sni(x)` phải báo lỗi, không tự tách thành các thanh trượt s/n/i. Tên chữ dài tùy ý cần hợp đồng riêng nếu bổ sung.
- Khi gặp tên mới, hiện số lượng và đề nghị **Khai báo tham số** ngay dưới công thức. Cho nhập giá trị trong bảng và gán giá trị theo nhóm đã chọn; hành động gán hàng loạt có preview rõ và một Undo. Chỉ vẽ khi các tham số cần thiết đã có giá trị. Tham số chưa gán không ngầm là 0 hoặc 1; không tạo hàng trăm slider chỉ vì phát hiện hàng trăm tên.
- Cùng tên trong cùng PlotDocument dùng chung một giá trị. Muốn độc lập thì dùng tên khác. Thêm hàm đã dùng a không đặt lại a. Xóa/ẩn một hàm không xóa tham số đang được hàm khác dùng.
- Tham số không còn được dùng được đánh dấu; giữ giá trị để Undo/nhập lại. Chỉ dọn khi có lệnh xóa rõ ràng. Khi đổi candidate, cập nhật danh sách phụ thuộc và không tự gán giá trị cho ký hiệu mới.

### Kéo và nhập số

- Mỗi tham số có tên/ID, trạng thái đã gán và giá trị số. Cấu hình thanh trượt (min/max/bước) và trạng thái ghim là dữ liệu tùy chọn, tách khỏi giá trị. Tham số nhập bằng ô số vẫn hoạt động khi không bật slider. Với ít tham số, thao tác tạo có thể đề nghị luôn slider; giá trị đề nghị 1, khoảng −5…5, bước 0,1 đều hiển thị rõ trước khi áp dụng. Mẫu parabol có thể đặt riêng a=1, b=0, c=0 và phải thể hiện các giá trị đó.
- Cho phép nhập số chính xác. Bước kéo không được âm thầm làm tròn giá trị đã nhập/lưu. Giá trị phải hữu hạn; slider khi bật cần min/max hữu hạn, min < max, bước > 0. Nếu giá trị ngoài khoảng kéo, giữ nguyên giá trị hợp lệ, báo cần mở rộng khoảng trước khi dùng slider hoặc cho tắt slider; không kẹp giá trị vào biên và không chặn cách dùng ô số. Hiển thị ít slider không có nghĩa chỉ các tham số đang thấy mới được tính.
- Một lần kéo là một bước Undo. Pointer move cập nhật tạm để xem; thả mới commit. Esc hủy về giá trị đầu, Ctrl+Z/Redo của tài liệu phục hồi cả các đường liên quan. Phím mũi tên hỗ trợ thay giá trị; Undo khi đang sửa ô văn bản không được bất ngờ tác động cả tài liệu.
- Khi kéo, giữ khung nhìn ổn định để thấy đồ thị thay đổi. Chỉ fit lại khi người dùng yêu cầu. Tham số làm dịch tiệm cận hoặc biên miền thì phải tính lại các đoạn tương ứng.
- Bản lưu giữ tên/ID, trạng thái đã gán, giá trị, cấu hình slider nếu có, ghim và nguồn/candidate của các hàm. Cùng file trên hai host phải phục hồi giá trị số, cấu hình kéo và các tham số đang ẩn.
- Nút Play tự chạy, quay phim/GIF, tham số tính từ tham số khác và đường quỹ tích chưa thuộc E1. Yêu cầu thanh trượt hiện tại là kéo trực tiếp/nhập số.

### Nhiều tham số và hàm nhiều biến

Bổ sung từ câu hỏi người dùng ngày 2026-09-14. Bản minh họa a/b/c chỉ thử thao tác với hai hàm cố định; không phải bằng chứng có parser đồ thị tổng quát hoặc có khả năng xử lý hàng trăm biến.

- **Một biến độc lập, nhiều tham số:** `y=f(x; a_1,…,a_100)` vẫn vẽ được trên mặt phẳng khi mọi aᵢ cần dùng có giá trị. Đây là ký hiệu giải thích, không cam kết E1 nhận cú pháp dấu chấm lửng hoặc định nghĩa hàm tổng quát. E1 nhận biểu thức đầy đủ thuộc grammar đã công bố.
- **Nhiều biến độc lập:** `f(x_1,…,x_100)` không có một đồ thị đường 2D biểu diễn đầy đủ. Để xem lát cắt đường, người dùng phải chọn một biến thay đổi và cố định 99 biến còn lại. E1 giữ đầu vào tường minh theo x; bộ chọn lát cắt cho hàm nhiều đối số là hướng mở rộng cần thiết kế riêng. Không tự biến mọi biến độc lập thành tham số hay tự chọn giá trị để vẽ.
- **Quản lý:** bảng tên/giá trị, tìm kiếm, lọc tham số của hàm đang chọn, ghim/bỏ ghim slider. Thu gọn/ẩn điều khiển không đổi dữ liệu, công thức hay phạm vi ảnh hưởng. Danh sách lớn chỉ dựng các hàng đang cần hiển thị; tìm kiếm, bàn phím, lưu và validation vẫn xét đủ bảng.
- **Tính toán:** dùng AST đã phân tích cùng bảng bindings, chỉ tính lại đường phụ thuộc vào tham số đổi. Có thể cache phần bất biến nếu giữ nguyên điều kiện miền; không dùng tối ưu rút gọn làm mất điểm khuyết. Gộp cập nhật khi kéo nhanh, kết quả mới nhất được ưu tiên. Bộ lấy mẫu có giới hạn thời gian/điểm trên cả hai host.
- **Chứng cứ quy mô:** thêm bài 10/100/300 tham số với biểu thức thuộc grammar, có cả dạng cộng đơn giản lẫn hàm lồng/phân thức/dao động. Đo thời gian phân tích, phản hồi kéo, bộ nhớ và số mẫu trên đúng bản publish. Số lượng tham số không tự quyết định chi phí; không hứa mọi biểu thức 100 biến đều chạy mượt.
- **Quá giới hạn:** giới hạn nguồn, độ sâu AST và ngân sách số vẫn được áp dụng rõ ràng. Giữ nguồn/file và các giá trị khi từ chối hoặc hết thời gian; báo phần chưa vẽ được. Không cắt bớt tham số, đổi nghĩa, khóa UI hoặc xuất đường cũ như kết quả mới. Ngưỡng hỗ trợ được công bố sau số đo; schema không mã hóa cứng ba tham số hoặc 26 chữ cái.

## 4. Tính đúng trước khi vẽ đẹp

Phân biệt ba việc: **khung nhìn** là vùng tọa độ đang xem; **khoảng x người dùng giới hạn** là phần muốn vẽ của từng hàm; **miền xác định thực** do chính biểu thức và tham số quyết định. Zoom không tự đổi khoảng x đã giới hạn hoặc làm mất điều kiện xác định.

- `sqrt(x)` chỉ vẽ phần có nghĩa trong số thực; `sqrt(x-a)` phải chuyển biên theo a.
- `1/x`, `1/(x-a)` và `1/(x-a)^2` tách đúng nhánh, kể cả hai phía cùng dấu. Không chỉ dựa vào đổi dấu hoặc kiểm hữu hạn tại hai đầu đoạn.
- `(x^2-1)/(x-1)` vẫn loại x=1 theo biểu thức gốc; không tự rút gọn rồi nối kín lỗ. Bộ nghiệm thu xác định cách biểu thị điểm khuyết khi hiển thị được ở tỷ lệ hiện tại.
- Khi a=0, `a*x^2+b*x+c` trở thành đường thẳng/hằng theo b; `1/(a*x)` không có giá trị hợp lệ khi a=0. Không giữ lại hình của a trước đó.
- Với lượng giác, UI ghi radian; bộ đầu là sin/cos. Lũy thừa, 0^0, mẫu bằng 0, căn âm, tràn số và giới hạn số mũ có quy tắc/diagnostic cụ thể trong hợp đồng evaluator trước khi triển khai.
- Lấy mẫu thích ứng theo sai số màn hình, kiểm điều kiện từ AST và chia khoảng hữu hạn. Có giới hạn công việc/điểm; vùng không đủ độ tin cậy thì ngắt và báo giới hạn, không nối để lấp chỗ trống. Không tuyên bố thuật toán lấy mẫu hữu hạn chứng minh được mọi gián đoạn.
- Kéo nhanh chỉ áp dụng kết quả thuộc nguồn/candidate, tham số và khung nhìn mới nhất. Không reparse toàn bộ nguồn theo từng nấc kéo; dùng AST đã chọn và bảng giá trị tham số mới.

## 5. Cách xây trên nền hiện có

1. **Locus.Core:** một grammar có phiên bản và MathDocument/candidate chung; bổ sung cấu trúc lời gọi hàm, hằng số và hợp đồng đánh giá số. Các cấu trúc mới cần serializer/preview/export nhất quán; host chưa hỗ trợ phải báo rõ, không diễn giải lại snapshot cũ.
2. **Locus.Application:** PlotDocument đầy đủ, bảng tham số, phụ thuộc đường–tham số, lệnh/Undo và snapshot bản vẽ. Giữ evaluator/sampler trong mã C# độc lập host; chốt vị trí project khi làm E1-01/04, không tạo module thừa chỉ để đặt tên.
3. **Locus.Editor:** cùng tab Đồ thị và thanh trượt trong WASM/Hybrid. Công việc lấy mẫu chạy qua scheduling có giới hạn; worker browser và đường xử lý Desktop đều phải bỏ kết quả hết hạn.
4. **Adapter vẽ:** thử tiếp JSXGraph 1.13.3 đã có ở WEB0, nhận các đoạn tọa độ do Locus tính. Không truyền nguồn người dùng vào parser JSXGraph và không dùng eval. Thanh trượt UI và dữ liệu vẫn do Locus quản lý.
5. **Lưu/xuất:** scene chốt gắn với đúng phiên bản nguồn, candidate, tham số và khung nhìn. SVG/PNG lấy cùng scene; trong lúc tính lại chưa được xuất kết quả cũ dưới giá trị mới. Khi cần lấy mẫu kỹ hơn để xuất lớn, phải hoàn tất và đồng bộ preview/snapshot trước khi xuất.

`WorkspaceDocument.cs` hiện chỉ có PlotCurve nguồn/màu/nét và PlotViewport tối thiểu. E1-01 cần version payload/migration rõ ràng; file Plot thử cũ không có snapshot không được âm thầm coi là đã chọn cách hiểu. Các file Formula cũ phải mở như trước, định dạng lạ giữ nguyên dữ liệu gốc.

Nguồn tham khảo đã đọc trước đề xuất đầu: [JSXGraph Curve](https://jsxgraph.org/docs/symbols/Curve.html) nhận tập tọa độ; [Slider](https://jsxgraph.org/docs/symbols/Slider.html) để đối chiếu thao tác. Chọn dùng tiếp adapter là đề xuất từ prototype Locus và API chính thức; chưa phải chứng nhận hiệu năng/sai số của editor E1.

Sau câu hỏi về nguồn ý tưởng, đã đối chiếu thêm tài liệu chính thức ngày 2026-09-14:

- [Desmos — Sliders and Movable Points in a Graph](https://help.desmos.com/hc/en-us/articles/202529069-Sliders-and-Movable-Points-in-a-Graph): đề nghị tạo slider cho biến tự do, cùng tham số dùng trong nhiều biểu thức, khoảng và bước kéo. Đây là đối chiếu UX, không phải lấy parser hoặc toàn bộ engine Desmos làm dependency.
- [GeoGebra — Slider](https://geogebra.github.io/docs/manual/en/commands/Slider/): khoảng min/max, bước thay đổi và các lựa chọn slider. Chỉ học thao tác cần cho E1; Play chưa thuộc phạm vi đã chọn.
- Bảng tham số có tìm kiếm/ghim, tách giá trị khỏi slider, ngân sách tính toán và cách xử lý quá tải là đề xuất riêng cho Locus từ tình huống người dùng nêu. Các trang trên không phải bằng chứng Locus hay các công cụ ấy đã đạt một benchmark 100/300 tham số.

## 6. Thứ tự triển khai và đầu ra kiểm được

| Bước | Task | Đầu ra | Bài kiểm kết thúc bước |
| --- | --- | --- | --- |
| 1 | E1-01 | Chốt luồng nhập/tự vẽ/chỉnh/copy và trạng thái UX; từ đó thiết kế PlotDocument, tham số/phụ thuộc, slider tùy chọn và phiên bản | Hợp đồng bao phủ các thao tác trong USER-EXPERIENCE; lưu/mở đủ nguồn/candidate/miền/view/style/tham số ẩn, file cũ/lạ không mất dữ liệu |
| 2 | E1-04 | Grammar plot có phiên bản, tên chỉ số như a_100, evaluator dùng AST và bindings | x², phân thức, căn, sin/cos và tham số đạt điểm chuẩn; phát hiện thiếu bindings; không đổi corpus grammar cũ |
| 3 | E1-02 | Tab đồ thị tối thiểu, bộ lấy mẫu/tách đoạn, worker và adapter | x² và 1/x cùng khung; pan/zoom, tiệm cận, nguồn lỗi và kết quả hết hạn đúng |
| 4 | E1-06 | Bảng tham số/tìm kiếm/lọc/ghim, slider/ô số, gán nhóm, cập nhật liên quan và Undo | Hai hàm chung a đổi cùng nhau; một drag một Undo; Esc, phím, tham số ẩn vẫn tính; đo tải 10/100/300 và trường hợp từ chối rõ |
| 5 | E1-03 | Nét/màu/nhãn/trục/miền, nháp, lưu/mở, Copy SVG/PNG | Chỉnh và Undo; copy đúng scene/giá trị; không có control trong hình; clipboard bị từ chối vẫn tải file |
| 6 | E1-05 | Gói alpha local Web/Desktop, hướng dẫn và báo cáo giới hạn | Toàn bộ bài dưới trên đúng build phát hành; không ghi DONE từ prototype hoặc chỉ test model |

Không dùng số task để suy ra thời gian hoặc % toàn dự án. Ước lượng khi lấy task và có số đo; chưa đặt ngày phát hành từ bản kế hoạch này.

## 7. Bài nghiệm thu E1

- Cú pháp: `x mũ 2`, `y = a*x^2 + b*x + c`, `1/x`, `1/(x-1)`, `căn(x)`, `sin(x)`, `cos(x)`; nguồn sai, thiếu ngoặc, candidate/repair và Telex/VNI trong ô hàm.
- Giá trị/miền: điểm chuẩn kèm dung sai, tiệm cận không nằm đúng nút lấy mẫu, cực cùng dấu, điểm khuyết, biên căn và pan/zoom. Bao gồm tham số làm đổi miền, đổi bậc hoặc mất toàn bộ miền.
- Tham số: ký hiệu dùng chung/hoa thường, tên không hỗ trợ, thêm/xóa/ẩn hàm, đổi candidate, giữ giá trị cũ, khoảng/bước lỗi, số ngoài khoảng, nhiều lần kéo nhanh, keyboard và Esc.
- Quy mô: a_1/a_100 có danh tính và source spans đúng; 10/100/300 tham số trong bảng và biểu thức, tìm kiếm/lọc/ghim, gán nhóm/Undo, tham số ẩn vẫn tính và lưu đủ; thiếu giá trị không tự gán. Tên chỉ số là scalar, không truy cập mảng. Đo hiệu năng theo độ phức tạp và báo giới hạn đã kiểm, không suy từ demo ba slider.
- Phiên: nhập/sửa dấu trong lúc lấy mẫu, đóng/đổi tab khi đang kéo, Undo/Redo của thao tác và của ô nhập; không áp dụng kết quả nguồn/giá trị cũ. Không khóa UI khi hết ngân sách lấy mẫu.
- Qua host: Web lưu → Desktop mở/sửa/lưu → Web mở; so nguồn/candidate, tham số chính xác, khoảng/bước, style, trục/miền và scene theo dung sai công bố.
- Xuất: SVG/PNG theo scene hiện tại, nét đứt và nhãn đúng; không nối qua đoạn bị loại, không kèm slider/selection; xuất khi đang kéo/cập nhật có hành vi rõ. Kiểm fallback download và phục hồi nháp/offline với tài nguyên mới.
- Hồi quy: bộ Formula/marker/nguồn/snapshot đang dùng, các runtime core được hỗ trợ, worker, file cũ và cơ chế phiên bản/cache WEB1. Cấu trúc mới chưa nghiệm thu Word không được tự công bố là hỗ trợ Word.

## 8. Ranh giới với các mốc tiếp theo

Thanh trượt hệ số của `y=f(x)` thuộc E1. **Đường cong tham số** `x(t), y(t)`, tọa độ cực, hàm ẩn, CAS/solver và 3D vẫn ở đợt khác. Hình học E2A sẽ có đặt/kéo điểm, cạnh, quan hệ và nhãn theo thao tác vẽ; E1 giữ hình dạng đường đúng theo hàm và tham số.

Theo ưu tiên mới, nền miền → Hóa → Lý được làm trước E1; sau E1 tiếp tục hình học 2D theo roadmap. Phần design system/template đã được người dùng hoãn, không mở lại trong đợt E1. W0 chờ phản hồi vẫn giữ trạng thái riêng.
