# E1 — Người dùng nhập, thấy và chỉnh gì?

Ngày: 2026-09-14. Bản đề xuất sau yêu cầu đi từ trải nghiệm người dùng. Chưa phải giao diện đã triển khai. Đây là đầu vào cho E1-01 và các bài demo; các quyết định dữ liệu/engine trong PLAN phải phục vụ luồng này.

**Bổ sung ngày 2026-09-17:** xem [Đặc tả editor đợt G](../phase-g/EDITOR-SPEC.md). Nhập → LaTeX ở trên; bảng tham số cuộn ở dưới, mỗi tham số một dòng: tên — giá trị | min — slider — max. Các dạng đồ thị và công cụ phân tích ngoài phạm vi E1 được liệt kê riêng; chưa tính là đã triển khai.

Theo yêu cầu ưu tiên mới, E1 tiếp tục sau Lý/Hóa. Giữ đề xuất UX này; [E3-01](../e3/PLAN.md) là công việc lấy trước, chưa dựng wireframe hoặc triển khai đồ thị trong đợt Lý/Hóa.

## 1. Công việc người dùng muốn hoàn thành

**Nhập hàm → thấy đường đồ thị → chỉnh nội dung hoặc cách trình bày → lấy hình dùng ngay.**

Trường hợp chuẩn không cần chọn mẫu parabol, chọn loại hàm, nhấn Phân tích hoặc nhấn Vẽ. Ô nhập trong tab Đồ thị đã mang ngữ cảnh toán; không cần thêm checkbox auto detect riêng. Tự hiểu ở đây là hiểu biểu thức và cập nhật preview, không phải quyền tự sửa công thức hoặc ghi vào Word.

## 2. Màn hình ban đầu

- **Thanh trên:** tab Công thức / Đồ thị; Mở, Lưu, Undo, Redo, Xuất/Copy. Chỉ đưa tab tính năng đã có vào sản phẩm; không bày Lý/Hóa/3D chưa làm như các tab dùng được.
- **Cột hàm bên trái:** một dòng trống có gợi ý `Nhập hàm, ví dụ: x mũ 2`; nút `+ Thêm hàm`. Nguồn gõ vẫn sửa trực tiếp được. Sau khi hiểu, hiện công thức trình bày ngay dưới nguồn và trạng thái của dòng.
- **Canvas chiếm phần lớn màn hình:** trục x/y và lưới nhẹ; có phóng/thu, Về gốc, Khung nhìn. Kéo vùng trống di chuyển khung nhìn, lăn chuột zoom theo vị trí trỏ. Không tự fit liên tục khi gõ/kéo tham số.
- **Thuộc tính theo lựa chọn:** mở ngay dưới dòng hàm được chọn, không thêm một cột công cụ thường trực. Cài đặt trục/lưới/khung nhìn mở từ canvas. Phần Tham số chỉ xuất hiện khi biểu thức cần nó.
- **Bảng tham số:** nằm dưới vùng nhập/công thức, cuộn độc lập; mỗi hàng đúng tên — ô giá trị | ô min — slider — ô max. Không dùng thẻ nhiều dòng hoặc giấu min/max. Bước trượt là tùy chọn phụ. Danh mục công cụ canvas và ca góc/giao điểm/tiếp tuyến xem đặc tả đợt G.
- **Màn hình hẹp:** cột hàm chuyển lên trên canvas, phần thuộc tính thu gọn. Các tác vụ chính không phụ thuộc chuột phải hoặc hover.

## 3. Một lượt dùng chuẩn

1. Mở Đồ thị, gõ `x mũ 2` vào dòng đầu. Trong lúc ghép dấu chưa dùng chuỗi trung gian để thay đường.
2. Khi đầu vào hợp lệ, Locus hiện `y = x²` để người dùng thấy cách hiểu và tự vẽ parabol. Một dòng nhập ứng với một đường có ID ổn định.
3. Sửa cùng dòng thành `x mũ 2 cộng 1`: cập nhật chính đường đó. Không thêm một đường mới sau mỗi lần gõ/ngừng gõ.
4. Chọn `+ Thêm hàm` hoặc Enter khi bộ gõ đã hoàn tất để sang dòng mới; nhập `1/x`. Hai đường cùng tồn tại, mỗi dòng có màu/nét tương ứng, bật/tắt và xóa.
5. Bấm đường trên canvas hoặc dòng tương ứng để chọn. Đường được nhấn nhẹ để dễ nhận ra; phần thuộc tính của dòng mở. Nếu đường giao nhau, ưu tiên đường đang chọn; danh sách bên trái luôn cho chọn chính xác.
6. Đổi đường thứ hai sang nét đứt, sửa nhãn, chỉnh khung nhìn rồi Copy SVG. Hình gồm phần bản vẽ đang thấy; thao tác chọn, nút, ô nhập và slider không xuất vào hình.

Khi tạo một đường nằm ngoài khung nhìn, hiện `Ngoài khung nhìn` nếu xác định được cùng lệnh xem vùng phù hợp. Không tự kéo khung nhìn của mọi đường đi nơi khác. Trường hợp bộ lấy mẫu chưa đủ dữ liệu phải báo đang tính/giới hạn, không đoán thành ngoài khung nhìn.

## 4. Chỉnh sửa là chỉnh gì?

| Người dùng muốn | Chỉnh ở đâu | Kết quả |
| --- | --- | --- |
| Đổi hàm `x²` thành `x²+1` | Sửa dòng nguồn | Đường thay theo hàm mới, nguồn và công thức preview cùng phiên |
| Thử hệ số a của `a*x²` | Ô giá trị hoặc slider a | Hàm giữ dạng ký hiệu, đường đổi theo a; các hàm chung a cùng cập nhật |
| Làm đường dễ phân biệt | Thuộc tính đường: màu, nét liền/đứt, độ dày | Chỉ đổi cách trình bày, không đổi tọa độ toán |
| Chỉ vẽ trong một khoảng x | Mục Giới hạn x của đường; chọn cận và có/không lấy đầu mút | Chỉ vẽ phần thuộc khoảng đã chọn và miền xác định; kiểu đầu mút thể hiện khi nằm trong khung |
| Đặt tên/chú thích cho đường | Bật nhãn, nhập nhãn; kéo nhãn trên canvas | Di chuyển chữ độc lập với đường; có thể hiện công thức/giá trị tham số |
| Bố cục hình cho vừa tài liệu | Pan/zoom hoặc nhập khoảng x/y trong Khung nhìn | Đổi vùng nhìn, không đổi hàm hoặc khoảng x của từng đường |
| Bỏ ô vuông, đổi trục/tỷ lệ | Cài đặt canvas: lưới, trục, nhãn trục/tick, khóa tỷ lệ đơn vị hai trục | Đổi cách trình bày hệ tọa độ; tỷ lệ hiện tại phải rõ |
| Tạm bỏ một đường/xóa hẳn | Bật/tắt hoặc Xóa ở dòng hàm | Ẩn vẫn giữ nguồn/cấu hình; xóa có Undo |

Không gọi chung các thao tác này là “free move”. Trong E1, kéo vùng trống để dời khung nhìn và kéo nhãn để dời chữ. Muốn dời parabol lên 2 đơn vị thì đổi hàm thành `x²+2` hoặc đổi hệ số tự do tương ứng. Kéo trực tiếp đường/điểm điều khiển để biến đổi hàm là hướng riêng cần ánh xạ rõ sang công thức, chưa là thao tác mặc định E1. Kéo điểm/cạnh hình học thuộc E2A.

## 5. Khi auto detect chưa đủ dữ kiện

| Đầu vào/trạng thái | Dòng hàm hiển thị | Canvas và bước tiếp |
| --- | --- | --- |
| `x mũ 2`, một cách hiểu chính xác và đủ giá trị | Công thức đã hiểu | Tự vẽ/cập nhật dòng hiện tại |
| `x+1/2` có cách hiểu trực tiếp theo ưu tiên phép toán, thêm gợi ý sửa phạm vi | Preview x + ½; fx có gợi ý được phân loại | Vẽ cách hiểu trực tiếp; `(x+1)/2` chỉ thay sau khi người dùng chọn sửa |
| Có nhiều cách hiểu hợp lệ thực sự, chưa chọn | `Chọn cách hiểu`, tối đa ba candidate của core | Chưa chốt đường mới cho tới khi chọn; không coi điểm xếp hạng là quyền tự quyết |
| `a*x^2`, a chưa có giá trị | `Cần giá trị a`; ô số và lựa chọn Tạo thanh trượt | Gán giá trị rõ rồi vẽ; không đoán a=1 âm thầm |
| Nhiều tham số chưa gán | Số lượng thiếu, nút mở bảng tham số | Nhập/gán nhóm có chủ đích, ghim slider cần dùng; không phủ canvas bằng hàng trăm control |
| Đang ghép dấu/nguồn tạm dở như `x^` | Trạng thái đang nhập, giữ nguyên nguồn | Không gắn đường cũ với nguồn mới; chỉ tạm bỏ đường của dòng đang sửa, các đường khác giữ nguyên |
| Lỗi sau nhập hoặc cú pháp chưa hỗ trợ | Nêu vị trí và lý do cụ thể, sửa ngay ở dòng đó | Không tự sửa nguồn; gợi ý repair là thao tác có lựa chọn |
| Đang tính lâu/đã chạm giới hạn | Trạng thái và nguyên nhân trên dòng | Giữ nguồn, UI vẫn thao tác được, không áp kết quả hết hạn |

Không bật menu candidate mỗi lần gõ. fx xuất hiện khi có lựa chọn/gợi ý cần xem; sửa lỗi và diễn giải hợp lệ vẫn được phân biệt. Vùng nhập Đồ thị không cần `lc[...]`; marker tiếp tục dành cho luồng công thức/ngữ cảnh phù hợp của sản phẩm.

## 6. Xuất và hoàn tác

- Copy SVG là lệnh nhanh chính; có PNG và tải tệp qua menu Xuất, đồng thời có menu chuột phải trên canvas. Xuất giữ khung nhìn, các đường đang bật, nhãn, nét, trục/lưới đã chọn; chọn nền trong suốt hoặc màu nền trong phần tùy chọn xuất.
- Trong lúc còn dòng chưa vẽ được, phải cho thấy rõ trạng thái đó và những đường thuộc hình xuất. Xuất các đường hợp lệ hiện có không được giả vờ là ảnh đầy đủ của mọi dòng. Không xuất kết quả cũ dưới nguồn/giá trị mới.
- Copy không biến bản vẽ thành đối tượng liên kết với Word. Sửa tiếp bằng file `.locus`; dán SVG/PNG vào tài liệu là cách dùng hình của E1.
- Undo: một lần kéo/thay kiểu nét/xóa là một hành động; Ctrl+Z trong ô nguồn đang sửa ưu tiên lịch sử nhập của ô. Không tạo hàng trăm bước Undo theo từng pixel kéo.

## 7. Demo đánh giá trải nghiệm trước nghiệm thu kỹ thuật

1. **Từ trắng đến đồ thị:** nhập → tự hiện công thức/đường → sửa cùng dòng → thêm đường; không cần chọn loại hàm.
2. **Từ đồ thị đến hình dùng được:** chọn đúng đường → đổi nét/nhãn → giới hạn x → chỉnh khung nhìn → Copy SVG/PNG đúng preview.
3. **Tham số xuất hiện đúng lúc:** thiếu a có hướng xử lý ngay tại dòng; nhập số/kéo; nhiều tham số có bảng và ghim, giữ canvas thông thoáng.
4. **Sửa dở vẫn kiểm soát được:** Telex/VNI, nhập thiếu, đổi nhanh, candidate/repair, Undo; không lẫn đường cũ với nguồn mới.
5. **Tiếp tục công việc:** lưu/đóng/mở lại trên Web và Desktop giữ đủ nguồn, giá trị, nét, nhãn, miền và khung nhìn.

Sau khi đánh giá luồng này mới hoàn thiện bố cục/khoảng cách/hành vi chi tiết. Không mở một dự án template/design system riêng. Trạng thái hoàn thành E1 vẫn theo BACKLOG và bằng chứng trên sản phẩm thật, không theo số màn hình mô tả.
