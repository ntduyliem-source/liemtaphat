# SC1-03/04/05 — Cách gõ và cân bằng

Đạt local trên build **20260915-042320-654**. [Bằng chứng đã chụp riêng](../../artifacts/sc1/evidence/20260915-042320-654/balance/receipt.json) giữ báo cáo đúng build, độc lập với các lượt kiểm tiếp theo.

Đã có draft/proposal có phiên nguồn và điều kiện, kết quả sinh mới sở hữu source/span riêng, lịch sử nhận v4 và bộ đọc snapshot cũ. Chữ thường đơn nghĩa, `=` trong Hóa và vùng thiếu sản phẩm đã có grammar riêng. `co` còn mơ hồ; `h20` không tự đổi thành `h2o`.

Solver dùng phân số chính xác và kiểm bảo toàn nguyên tố/điện tích độc lập. Chỉ đề xuất khi có một tỉ lệ hệ số nguyên dương tối giản duy nhất. Phân biệt đã cân bằng, vô nghiệm, nhiều nghiệm và vượt giới hạn. Solver không chứng minh phản ứng xảy ra.

Trong Web và Desktop, ƒx hiển thị toàn bản cân bằng; bấm nhận mới sửa nguồn, giữ cặp bọc, một lần Undo quay lại nguyên trạng. Tệp đã nhận lưu cả nguồn trước và kết quả; mở lại không tính toán lại bằng solver mới.

| Phạm vi | Kết quả |
| --- | --- |
| Hợp đồng cặp và hồi quy wire cũ | 109/109; gồm đối chiếu 504 wire cũ |
| Model / serializer / lịch sử | 20/20 |
| Cách gõ Hóa và draft | 38/38 |
| Cân bằng / wire | 31/31 |
| Giao dịch nhận, Undo, stale, file | 7/7 |
| Chromium | 24/24 nhóm |
| Firefox | 24/24 nhóm |
| Desktop WebView2 | 22/22 nhóm |
| Native ↔ WASM | 660 đầu vào giống nhau trên mỗi trình duyệt |

Đây là nghiệm thu SC1-03/04/05 cùng thao tác nhận thủ công ở editor chung. Chưa bao gồm kho suy sản phẩm, ghost Enter/Space, IME OS cho luồng mới hay SC1 trong Word. W0 G2/G3 vẫn chờ kết quả người dùng thử.
