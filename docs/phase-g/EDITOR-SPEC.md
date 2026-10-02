# Đợt G — Đặc tả trải nghiệm editor

Thiết kế ngày 2026-09-17; cập nhật thực hiện 2026-09-18: đã có bản review sản phẩm sau khi người dùng yêu cầu làm G. Tài liệu này là danh mục thiết kế đích; [báo cáo G](REPORT.md) mới xác định phần thực sự đã làm/kiểm và phần còn thiếu.

Tài liệu này bổ sung E1/E2A/E2B. Những bản minh họa trước chỉ thử vài thao tác, không đại diện cho toàn bộ editor. Không dùng số màn hình/nút minh họa làm tiến độ sản phẩm.

## 1. Yêu cầu người dùng đã xác định

1. Đồ thị có ô nhập tự nhiên → nhận diện → công thức LaTeX được dựng để kiểm tra cách hiểu → canvas.
2. Khung tham số cuộn nằm dưới phần nhập/công thức. Mỗi tham số đúng một dòng: **tên — [giá trị] | [min] — slider — [max]**. Không dùng thẻ nhiều dòng; không giấu min/max trong phần mở rộng.
3. Điểm hình học kéo trực tiếp trên canvas, không dùng slider ngang/dọc làm cách di chuyển chính.
4. Bắt điểm có dấu hiệu nấc khi tới vị trí phù hợp, gồm trung điểm; thả mới gắn quan hệ. Có thể tắt/bỏ qua bắt điểm.
5. Chọn đoạn để đổi liền/đứt, màu; mở rộng độ dày, nhãn và ký hiệu.
6. Editor phải bao phủ các nhóm công việc, gồm góc, phép dựng, đo, trình bày, hoàn tác và xuất. Một tam giác kéo được hoặc một parabol có slider chưa phải toàn bộ editor.
7. Web/Desktop dùng chung logic. Giữ tab Công thức / Đồ thị / Hình học, với 2D và 3D trong Hình học.

Các danh mục và cách chia đợt bên dưới là đề xuất thiết kế, không tự chuyển thành cam kết đã được người dùng duyệt.

## 2. Đối chiếu nguồn đã đọc

Đọc nguồn chính ngày 2026-09-17; chưa tích hợp hoặc benchmark các engine trong lượt này.

| Nguồn | Điều đã kiểm tra | Áp dụng |
| --- | --- | --- |
| [Excalidraw Actions.tsx](https://github.com/excalidraw/excalidraw/blob/master/packages/excalidraw/components/Actions.tsx) | Thuộc tính theo loại lựa chọn: màu, độ dày, kiểu nét, đầu mũi tên, chữ, độ trong, lớp, nhóm, xóa | Tách công cụ tạo/chọn khỏi thuộc tính của lựa chọn |
| [JSXGraph Angle](https://jsxgraph.org/docs/symbols/Angle.html) | Ba điểm/hai đường; bán kính cung, hướng/miền góc, số đo; đặt và giải phóng số đo | Góc là đối tượng riêng, có hình học và cách trình bày |
| [GeoGebra Angle](https://geogebra.github.io/docs/manual/en/tools/Angle/) | Chọn ba điểm, hai đoạn/đường/vector hoặc đa giác; điểm thứ hai là đỉnh | Preview thứ tự chọn và miền góc |
| [GeoGebra Angle with Given Size](https://geogebra.github.io/docs/manual/en/tools/Angle_with_Given_Size/) | Chọn cạnh xuất phát/đỉnh, nhập số đo để tạo cạnh còn lại | Tách đo góc có sẵn và dựng góc theo số đo |
| [GeoGebra Angle Bisector](https://geogebra.github.io/docs/manual/en/tools/Angle_Bisector/) | Phân giác từ ba điểm hoặc hai đường | Chọn nhánh trong/ngoài |
| [CindyJS User Input](https://cindyjs.org/ref/User_Input.html) | Nhóm công cụ dựng, chuỗi down/drag/up; nhiều công cụ trong ngoặc chưa hỗ trợ | Học nhóm công cụ; không coi mọi mục tài liệu là chức năng có sẵn |
| [JSXGraph Curve](https://jsxgraph.org/docs/symbols/Curve.html), [ImplicitCurve](https://jsxgraph.org/docs/symbols/ImplicitCurve.html) | Đường tham số, cực, dữ liệu, phương trình ẩn có cách biểu diễn khác nhau | Không coi mọi chữ ngoài x là tham số |
| [Three.js TransformControls](https://threejs.org/docs/pages/TransformControls.html) | Tay nắm trục/mặt phẳng, tịnh tiến/quay/tỉ lệ, nấc | Kéo trực tiếp trong 3D; điều khiển không tự cung cấp quan hệ toán |
| [GeoGebra Angle command](https://geogebra.github.io/docs/manual/en/commands/Angle/) | Góc đường–đường, đường–mặt, mặt–mặt, có hướng | Chỉ rõ đối tượng, miền và mặt phẳng biểu diễn góc 3D |

Penrose vẫn là nguồn tham khảo tách nội dung/quan hệ khỏi bố cục. Nó không thay bộ công cụ chọn/kéo/chỉnh.

## 3. Editor đồ thị

### 3.1 Cột nhập và tham số

Từ trên xuống:

1. Một hoặc nhiều dòng nguồn có ID ổn định, ẩn/hiện, màu nhận diện, xóa.
2. Dưới mỗi dòng là công thức dựng bằng LaTeX từ candidate đã chọn. Giữ nguồn để sửa; không bắt người dùng viết LaTeX.
3. fx khi có cách hiểu/gợi ý cần chọn. Thiếu tham số hoặc chưa chọn cách hiểu thì trạng thái ở chính dòng ấy.
4. Bảng tham số dùng chung nằm dưới. Cuộn độc lập với phần nhập và canvas.

Mỗi hàng: **tên — ô giá trị | ô min — slider — ô max**.

- Không lặp nhãn các cột trên từng hàng. Bố cục desktop phải đủ rộng cho hàng này.
- Màn hình hẹp cho mở rộng vùng nhập; giữ thứ tự, không thu nhỏ chữ đến mức khó đọc.
- Tham số dùng chung nhiều hàm có một nguồn giá trị. Tham số mới giữ trạng thái chưa gán; gán nhóm có giá trị đề xuất rõ trước khi áp dụng.
- Danh sách dài có tìm tên, ghim và lọc theo hàm đang chọn.
- Chưa chốt trần số tham số; giới hạn tài nguyên dựa vào số đo khi triển khai. Không tự cắt danh sách hoặc bỏ tham số khỏi phép tính.
- Min/max là phạm vi slider, không phải miền xác định hay giới hạn trục.
- Nhập ngoài khoảng vẫn giữ giá trị và báo rõ, không âm thầm clamp.
- Min < max, bước > 0. Bước trượt nằm trong tùy chọn phụ, không làm cao mọi hàng.
- Cuộn tham số không zoom canvas; pan/zoom canvas không làm mất vị trí cuộn.

### 3.2 Thanh công cụ canvas

Nhóm công cụ nhìn thấy; công cụ con mở từ nhóm. Không trải hàng chục icon cùng lúc.

| Nhóm | Công cụ/thuộc tính | Thao tác |
| --- | --- | --- |
| Chọn / di chuyển | Chọn đường, điểm, nhãn; nhiều lựa chọn; pan/zoom/vừa khung | Bấm đường chọn đúng dòng hàm; kéo nền để pan |
| Điểm | Tự do, trên đường, theo dõi tọa độ | Chọn đường rồi đặt điểm; kéo điểm trượt theo đường |
| Giao điểm | Hai đồ thị; giao Ox/Oy | Hiện các nghiệm tìm được trong miền xét; chọn điểm cần giữ |
| Tiếp tuyến / pháp tuyến | Tại điểm hoặc giá trị x | Điểm di chuyển thì đường và nhãn cập nhật |
| Miền vẽ / tô miền | Giới hạn x, đầu mút mở/đóng; tô với trục hoặc giữa hai đường | Nhập/kéo cận; phân biệt giới hạn vẽ với miền xác định |
| Đo | Tọa độ, khoảng cách, hệ số góc, góc giữa đường/tiếp tuyến | Chọn đủ đối tượng; ghi xấp xỉ nếu tính số |
| Nhãn | Tên hàm, công thức, tọa độ, ghi chú toán | Kéo chữ độc lập; nhãn động bám đúng đối tượng |
| Trục / lưới | Khoảng x/y, tỉ lệ đơn vị, nhãn/tick, bước lưới | Có khóa tỉ lệ 1:1 |
| Trình bày | Màu, nét, độ dày, hiện/ẩn, lớp | Thuộc tính lựa chọn, không đổi công thức |
| Xuất | SVG/PNG, tải xuống, khung cắt, nền | Bỏ tay nắm và UI khỏi hình xuất |

Đây là danh mục đích. Công cụ phân tích cần evaluator/solver tương ứng, không giả đã có vì engine có API.

### 3.3 Các loại đầu vào

| Dạng | Ví dụ | Thuộc tính riêng |
| --- | --- | --- |
| Hàm một biến | y=a*x^2+b*x+c; sin(x); 1/x | Khoảng x, đơn vị đối số lượng giác rõ ràng |
| Theo từng khoảng | Nhiều nhánh và điều kiện | Sửa nhánh, điều kiện, đầu mút mở/đóng |
| Phương trình ẩn | x^2+y^2=r^2; x=2 | Miền xét hai trục; không biến y thành slider |
| Đường tham số | x(t), y(t) | Miền t; t là biến chạy |
| Tọa độ cực | r(theta) | Miền theta, đơn vị góc |
| Bất phương trình/hệ | y>x^2, y<2 | Biên mở/đóng, tô giao hoặc riêng các miền |
| Dữ liệu điểm | Các cặp (x,y) | Bảng dữ liệu, chỉ điểm/nối điểm; không tự hồi quy |

E1 hiện lên kế hoạch cho hàm một biến và tham số. Các dạng còn lại là thiết kế mở rộng, phải có bảng phạm vi và thông báo chưa hỗ trợ trong bản chỉ có E1. Không tự mở rộng parser trong lượt brainstorm.

## 4. Editor 2D

### 4.1 Thanh công cụ

**Chọn/kéo | Điểm ▾ | Đường ▾ | Tròn/cung ▾ | Đa giác ▾ | Góc/đo ▾ | Dựng ▾ | Biến hình ▾ | Nhãn/ký hiệu ▾**

| Nhóm | Công cụ cần thiết kế | Dữ kiện/chọn trên canvas |
| --- | --- | --- |
| Chọn/kéo | Chọn đơn/nhóm/chồng lấn; pan, xóa, sao chép, ẩn/hiện, lớp | Kéo điểm; kéo cạnh tự do dịch hai đầu và các phần chung đỉnh |
| Điểm | Tự do, trên đường/cung, giao điểm, trung điểm, chia theo tỉ số | Chọn đối tượng cha; nhiều giao điểm cho chọn nhánh |
| Đường | Đoạn, đường thẳng, tia, vector, gấp khúc | Dùng điểm cũ hoặc đặt điểm mới; phân biệt kéo dài/hữu hạn |
| Tròn/cung | Tâm–điểm, tâm–bán kính, qua ba điểm; cung, quạt tròn | Hiện tâm/bán kính/hướng cung trước khi chốt |
| Đa giác | Tự do, đều; tam giác, chữ nhật, vuông | Đóng về điểm đầu; mẫu chỉ rõ quan hệ đã tạo |
| Dựng | Song song, vuông góc, hình chiếu, trung trực, phân giác, tiếp tuyến, tròn nội/ngoại tiếp | Preview; nhiều nghiệm phải chọn |
| Góc/đo | Góc đo/góc cho trước, khoảng cách, độ dài, chu vi, diện tích, bán kính | Động theo hình; làm tròn chỉ ảnh hưởng hiển thị |
| Biến hình | Đối xứng trục/tâm, tịnh tiến, quay, vị tự | Chọn hình và tâm/trục/vector/hệ số; phân biệt tạo ảnh và dời bản gốc |
| Nhãn/ký hiệu | Chữ/công thức, tên điểm; dấu cạnh/góc bằng nhau, song song, góc vuông | Kéo nhãn/ký hiệu; trình bày không tự tạo ràng buộc số học |

### 4.2 Chọn xong sửa gì?

- Điểm: tên, kiểu/kích thước, nhãn, hiện/ẩn, quan hệ đang bám. Kéo là cách sửa vị trí chính; tọa độ là nhập chính xác tùy chọn.
- Đoạn/đường/tia/vector: màu, độ dày, nét, mũi tên, kéo dài, nhãn, vạch bằng nhau. Vạch chú thích khác lệnh làm hai đoạn bằng nhau.
- Tròn/cung: tâm, bán kính/điểm tạo; miền/hướng cung; nét, tô, nhãn.
- Đa giác: đỉnh/cạnh, viền, tô/độ trong, nhãn diện tích/chu vi.
- Góc: thuộc tính riêng ở phần 5.
- Nhãn: nội dung, công thức, cỡ chữ, vị trí; bám đối tượng hoặc tự do.
- Nhiều lựa chọn: thuộc tính chung và trạng thái hỗn hợp; không ghi đè trường khác nhau khi chưa chỉnh trường ấy.

### 4.3 Bắt điểm và quan hệ

Kéo tự do mặc định. Gần mục tiêu hợp lệ có dấu, tên loại bắt và nấc. Thả mới gắn. Ngưỡng vào/ra khác nhau để tránh giật; có cách chọn khi các mục tiêu gần nhau.

Mục tiêu: đầu mút, trung điểm, giao điểm, điểm trên đường/cung; lưới là tùy chọn. Alt/tắt bắt để đặt tự do. Không tự tạo vuông góc/bằng nhau chỉ vì hình trông gần đúng.

Quan hệ từ bắt điểm có thể tách bằng kéo đủ xa, preview “thả để tách”. Quan hệ dựng theo giả thiết (góc cố định, hình vuông...) chỉ rõ quan hệ nào sẽ bỏ; không âm thầm phá dữ kiện. Kéo/tách và cập nhật phụ thuộc là một Undo. Nếu khác giả thiết nguồn, cho thấy khác biệt; không tự sửa câu đề.

## 5. Góc — đối tượng và thao tác đầy đủ

### 5.1 Đo góc

1. Chọn Góc, chọn A → B → C; B là đỉnh. Hoặc chọn hai đoạn/tia/đường/vector.
2. Preview tô nhẹ miền và cung, thấy rõ góc được chọn.
3. Đổi miền nhỏ/lớn hoặc hướng; hai đường cắt nhau cho chọn vùng trên canvas.
4. Nhãn số đo cập nhật theo điểm. Cạnh độ dài 0 không có số đo hợp lệ.

### 5.2 Dựng góc theo số đo

- Chọn đỉnh và cạnh xuất phát, nhập 60° hoặc biểu thức góc hợp lệ.
- Chọn phía/hướng; preview tia mới rồi đặt điểm trên tia.
- Chỉnh góc đã tồn tại phải chỉ cạnh/điểm nào di chuyển, cái nào giữ nguyên.
- Nếu xung đột, nêu quan hệ liên quan; không bẻ các phần khác cho khớp.

### 5.3 Thuộc tính

| Thuộc tính | Hành vi |
| --- | --- |
| Miền góc | Nhỏ/lớn, trong/ngoài theo ngữ cảnh; hướng quay nhìn thấy |
| Số đo | Hiện/ẩn, độ/radian, số chữ số; đổi đơn vị hiển thị không đổi hình |
| Nhãn | Alpha/beta, ABC, giá trị hoặc biểu thức; kéo vị trí |
| Cung | Một/hai/ba cung, dấu gạch; kéo tay nắm bán kính cung tránh chồng chữ |
| Góc vuông | Dấu vuông khi quan hệ/góc vuông đã xác nhận; ký hiệu vẽ tay lưu là chú thích |
| Trình bày | Màu, nét, độ dày, tô miền/độ trong |
| Quan hệ | Đo tự do / giữ số đo / bằng góc khác; bỏ quan hệ rõ ràng |

Phân biệt **đo**, **dựng theo số đo**, **đánh dấu**. Gõ nhãn “60°” không giả thành số đo tính ra; lệnh giữ 60° phải tạo quan hệ thật.

### 5.4 Góc trong đồ thị và 3D

- Đồ thị: góc giữa đường hoặc giữa các tiếp tuyến tại điểm đã chọn. Đo bằng tọa độ toán, không đo pixel khi tỉ lệ trục khác nhau.
- Đường cong cần chọn vị trí/tiếp tuyến trước, không có một góc duy nhất cho cả cặp đường.
- 3D: đường–đường, đường–mặt, mặt–mặt; chỉ mặt phẳng dựng cung/hình phụ. Đo trong không gian, không trên hình chiếu.
- Đường chéo nhau không có giao điểm thật; đo theo phương thì đường phụ song song dùng để minh họa phải rõ.

## 6. Editor 3D

| Nhóm | Công cụ/điều khiển |
| --- | --- |
| Tạo | Điểm, đoạn/đường/tia/vector, mặt phẳng, đa giác, chóp/lăng trụ/hộp; mở rộng cầu/trụ/nón |
| Chọn/kéo | Điểm/cạnh/mặt; tay nắm trục/mặt phẳng; chọn đối tượng bị che qua danh sách |
| Dựng | Trung điểm, giao đường–mặt/hai mặt, vuông góc/song song, hình chiếu, thiết diện |
| Đo | Độ dài, góc, khoảng cách, diện tích mặt, thể tích; nhãn động |
| Góc nhìn | Orbit/pan/zoom, trên/trước/bên, vừa khung; phối cảnh/song song |
| Hiển thị | Cạnh thấy/khuất, hiện/ẩn mặt, độ trong, nét/màu từng cạnh, nhãn |
| Xuất | SVG vector theo góc nhìn, PNG, lưu scene; kiểm nét khuất riêng |

Kéo nền đổi camera; kéo điểm đổi hình. Tay nắm/mặt phẳng đang dùng nhìn thấy. Không thay kéo bằng slider X/Y/Z. Camera không sửa đỉnh/quan hệ. Nét khuất tự động và nét thủ công có trạng thái riêng; giữ lựa chọn thủ công. Bố trí nhãn không đổi giả thiết.

## 7. Các ca phải có trong thiết kế/kiểm tra

| Ca | Kết quả cần quan sát |
| --- | --- |
| x mũ 2 → x mũ 2 cộng 1 | Nguồn/LaTeX/đường cùng phiên; sửa một dòng không sinh nhiều đường |
| a*x^2+b*x+c | Ba hàng, mỗi hàng một dòng, đủ giá trị/min/slider/max |
| 100/300 tham số | Cuộn/tìm/ghim; canvas đứng yên; đo hiệu năng trước khi cam kết |
| x=2; x^2+y^2=1 | Đúng loại hoặc báo chưa hỗ trợ; không tạo slider y |
| 1/x, tan(x), sqrt(x), điểm khuyết | Không nối qua điểm không xác định; phân biệt miền vẽ/khung nhìn |
| Giao tiếp xúc/nhiều giao điểm | Không coi tập nghiệm tìm gần đúng là toàn bộ; miền xét rõ |
| Góc hai tiếp tuyến | Hai điểm được chọn, góc cập nhật |
| Thả M vào trung điểm BC, kéo B/C | Nấc, bám, Undo cả quan hệ |
| Kéo nhãn/bán kính cung | Chỉ đổi trình bày |
| Đo ABC rồi dựng 60° | Hai lệnh riêng; cạnh di chuyển có preview; số đo đúng |
| Góc lớn/đảo A,C | Đúng miền, không tự đổi sang góc nhỏ |
| Một cạnh góc co về 0 | Không ghi số đo giả |
| Chọn hai cạnh đổi nét/màu | Cùng cập nhật nét, không đổi hình học |
| Hai giao điểm đường tròn | Chọn nghiệm; kéo không nhảy nhánh vô cớ |
| Ràng buộc mâu thuẫn | Nêu xung đột, giữ trạng thái trước thao tác bị từ chối |
| Góc đường–mặt/mặt–mặt | Đúng không gian, hình phụ rõ |
| Xoay khối → SVG | Nét khuất/nhãn/hướng chiếu trùng preview |
| Lưu/mở Web ↔ Desktop | Giữ nguồn, quan hệ, giá trị, nét, nhãn, góc nhìn, ẩn/hiện |

## 8. Trình tự đề xuất

Giữ E1 → E2A → E2B. Danh mục đích không có nghĩa mọi tính năng đã làm hoặc phải phát hành cùng lúc.

1. Wireframe E1 đúng nhập → LaTeX → tham số một dòng, có thanh công cụ canvas và thuộc tính đường. Bao phủ trạng thái trống, thiếu tham số, lỗi, nhiều hàm/nhiều tham số.
2. E1 cơ bản theo PLAN: evaluator, miền/gián đoạn, tham số, chỉnh đường/trục/nhãn, Undo/lưu/xuất. Ghi riêng phân tích và dạng đồ thị mở rộng.
3. E2A cơ bản có chọn/vẽ/kéo, bắt điểm, đoạn/đường/tia, tròn, góc đo/góc cho trước, trung điểm/song song/vuông góc, ký hiệu, xuất. Không nghiệm thu bằng tam giác có sẵn.
4. E2A mở rộng cung/đa giác, dựng/biến hình; thử từ đề thật, nhiều nghiệm, xung đột.
5. E2B kéo trực tiếp, mặt phẳng/khối, góc không gian, nét khuất/hình chiếu; tách công cụ phức tạp như thiết diện thành phần triển khai rõ.

Trước khi code mỗi công cụ phải có: đầu vào, thứ tự chọn/kéo, preview, xác nhận, Esc/Undo, thuộc tính, phụ thuộc, tình huống không xác định, xuất/lưu. Tất cả có chỗ trong thiết kế, kể cả triển khai sau.
