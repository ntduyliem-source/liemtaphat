# Kho phản ứng SC1

`reaction-catalog/0.1` gồm **45 bản ghi khai báo riêng**: 44 phản ứng và một tổ hợp không có phương trình ion rút gọn. Chỉ các tổ hợp đã liệt kê được hỗ trợ. Có 14 trung hòa, 13 kết tủa, 7 carbonate với acid, 2 nhánh CO₂/NaOH, 1 tạo nước, 3 cháy hoàn toàn, 2 nhiệt phân, 1 kim loại–acid, 1 oxide–nước và 1 không có phản ứng ion rút gọn.

Dữ liệu nằm trong [ReactionCatalog.Data.cs](../../src/Locus.Core/Assistance/ReactionCatalog.Data.cs); manifest và bài chạy sinh tại `artifacts/sc1/catalog-manifest.json`. Bản ghi có ID/version, chất, sản phẩm, điều kiện, phạm vi, ngoại lệ, nguồn và loại căn cứ. `source-example` là ví dụ có trong nguồn; `derived-from-stated-principle` / `derived-from-solubility-table` là phương trình Locus tự biên soạn từ nguyên tắc hoặc bảng độ tan, không tuyên bố từng phương trình đã được thí nghiệm riêng.

## Căn cứ

Đối chiếu ngày 2026-09-15. Chỉ dùng dữ kiện hóa học để tự biên soạn danh sách; không nhập văn bản sách, hình, bài tập, mã hay database bên ngoài. Không dựa vào nhãn giấy phép cũ để sao chép nội dung: trang OpenStax hiện hiển thị CC BY-NC-SA và hạn chế tái sử dụng nội dung. Các link dưới đây là dẫn nguồn dữ kiện, không phải tuyên bố Locus có quyền phân phối sách.

- [OpenStax Chemistry 2e §4.2](https://openstax.org/books/chemistry-2e/pages/4-2-classifying-chemical-reactions): trung hòa, các ion tan/ít tan, phản ứng kết tủa. Danh sách Locus là suy ra có giới hạn từ nguyên tắc này, không mở quy tắc trao đổi cho mọi chất.
- [OpenStax §18.6](https://openstax.org/books/chemistry-2e/pages/18-6-occurrence-preparation-and-properties-of-carbonates): carbonate/bicarbonate với acid và hydroxide, CO₂ tạo carbonate hoặc bicarbonate. Hai điểm tỉ lệ CO₂/NaOH là mô hình phương trình tổng quát; không tính phân bố cân bằng các ion hoặc hỗn hợp.
- [OpenStax §18.5](https://openstax.org/books/chemistry-2e/pages/18-5-occurrence-preparation-and-compounds-of-hydrogen): H₂/O₂ cần mồi phản ứng; Fe/HCl loãng cho muối Fe(II).
- [OpenStax §20.1](https://openstax.org/books/chemistry-2e/pages/20-1-hydrocarbons): cháy hoàn toàn alkane. Locus giới hạn CH₄, C₂H₆, C₃H₈, không nhận dạng cấu trúc hữu cơ bất kỳ từ công thức phân tử.
- [RSC: calcium carbonate](https://edu.rsc.org/experiments/thermal-decomposition-of-calcium-carbonate/704.article): nhiệt phân CaCO₃ và CaO với nước.
- [RSC: baking powder](https://edu.rsc.org/download?ac=12358): NaHCO₃ nhiệt phân.

## Quy tắc áp dụng

So khớp cấu trúc chất chuẩn từ AST, bỏ hệ số ở ngoài từng chất và không phụ thuộc thứ tự các chất. Không gộp danh tính bằng tổng số nguyên tử. Chất lặp, chất bổ sung chưa biết, mũi tên thuận nghịch hoặc chữ thường mơ hồ không được tự suy sản phẩm.

Mọi điều kiện phải do người dùng chọn cho đúng phiên nguồn/vùng. Không suy tỉ lệ mol từ hệ số đã gõ. Thiếu điều kiện không cấp proposal; điều kiện ngoài phạm vi trả “chưa có dữ liệu”, không biến thành “không phản ứng”. Điều kiện đã chọn đi theo provenance trong tệp kết quả đã nhận.

Parser xử lý chất. Kho chỉ cung cấp cây sản phẩm. Solver dùng cây phản ứng đó, cân lại hệ số, kiểm bảo toàn độc lập rồi phát hành proposal. Toàn phương trình được preview trước nhận. Chỉ metadata/preview thay đổi khi chọn điều kiện; nguồn và file chưa nhận giữ nguyên.

## Cách đánh giá

45 bài toàn danh mục kiểm tính toàn vẹn và bỏ lần lượt từng điều kiện bắt buộc. [38 bài giữ riêng](../../corpus/sc1/reactions-heldout.json) kiểm biến thể chữ, thứ tự, hệ số, Unicode, thiếu/sai điều kiện và trường hợp ngoài phạm vi. Các bài này viết riêng với kỳ vọng cụ thể; nhiều bài vẫn dùng chất thuộc danh mục, nên kết quả không phải độ chính xác dự đoán trên hóa học bất kỳ. Kiểm thêm hủy, metadata sai, nhận/Undo/file với cặp mở/đóng; native/WASM và giao diện có báo cáo theo build.

Không dùng riêng số bài core đạt để đóng SC1-06 trước khi kiểm editor và WASM đã phát hành. Ghost và bàn phím theo SC1-07/08.
