# Dùng thử SC1 local

Mở Web tại **http://127.0.0.1:4185/** khi server đang chạy. Để mở lại, chạy `./tools/sc1/local.ps1 start` từ thư mục dự án. Web xử lý trên máy và dùng cùng editor với Desktop; chưa xuất bản online.

1. Nhập `hoa-[h2+o2=h2o]`, bấm **ƒx · Hỗ trợ Hóa** để xem bản cân bằng. Bấm đề xuất để nhận; Hoàn tác trả lại chuỗi đã gõ.
2. Nhập `hoa-[h2+o2=]`. Chọn điều kiện **Mồi phản ứng** trong phần hỗ trợ, rồi trở về cuối ô nhập. Khi sản phẩm mờ và toàn phương trình hiện, **Enter** nhận. Locus chỉ hỗ trợ các phản ứng đã có trong kho và đủ điều kiện.
3. **Esc** bỏ gợi ý. **Space** mặc định là khoảng trắng; có thể bật nhận bằng Space trong **Phím tắt và cách dùng**. Space khi bật nhận toàn phương trình và thêm một khoảng trắng, cùng một lần Hoàn tác.
4. Thử `toan-[x mũ 2]`, `ly-[v=10 m/s]`, `hoa-[H2SO4]`. Mở **Cặp bọc theo môn** để chỉnh dấu mở/đóng. Cặp riêng chỉ định môn cho vùng đó kể cả checkbox nhận diện môn đang tắt; cặp chung `lc[...]` theo các checkbox bật.
5. Sau khi nhận, dùng Lưu tệp `.locus`, SVG/PNG hoặc văn bản như editor hiện có. Tệp lưu nguồn trước nhận, đề xuất, điều kiện và kết quả đã chọn; mở lại không suy lại phản ứng.

`h20` có số 0 sẽ không tự sửa thành `h2o`. `co`/`no` mơ hồ cần viết rõ ký hiệu. `CO2+NaOH=` cần khai báo điều kiện và tỉ lệ; hệ số gõ trong phương trình không được coi là lượng mol thực tế. Không có dữ liệu hỗ trợ được phân biệt với không có phản ứng trong điều kiện đã kiểm.

SC1-01…07 đã đạt local; xem [báo cáo ghost](GHOST-REPORT.md), [cân bằng](BALANCE-REPORT.md) và [kho phản ứng](CATALOG-REPORT.md). Kiểm bộ gõ OS, Word SC1 và gói sau giải nén còn các bước riêng trong [backlog](../BACKLOG.md).
