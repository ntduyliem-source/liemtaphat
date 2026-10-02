# SC1-01/02 — Cặp bọc theo môn

Ngày 2026-09-15. Build `20260915-033948-475`. Hoàn thành hợp đồng/fixture và cặp bọc trên Web/Desktop; toàn SC1 còn các task Hóa thông minh, ghost và Word. [Bằng chứng có hash](../../artifacts/sc1/markers-acceptance.json), [backlog](../BACKLOG.md).

## Kết quả

Cùng editor và ba checkbox hiện có, thêm hộp **Cặp bọc theo môn** với bốn mục: chung, Toán, Lý, Hóa. Mỗi mục sửa dấu mở/đóng; cặp riêng có thể bật/tắt. Nút Lưu kiểm cả tập cấu hình, báo xung đột mà không áp một phần.

Cặp riêng chỉ định môn trong vùng dù checkbox môn đó tắt. `hoa-[K4[Fe(CN)6]]` giữ ngoặc nhóm bên trong; nhiều vùng, NFD/emoji, raw và hai span được giữ. Cặp chung dùng checkbox, không có alias ẩn. Cặp chưa đóng/lỗi/lồng và nội dung được bảo vệ không bị dò lại một đoạn con.

Tệp/preferences v3 giữ cấu hình; snapshot 0.3 lưu ID/môn/literal đã dùng. File v1/v2 mở nguyên snapshot, không chạy lại parser. Nâng cặp cũ tùy chỉnh giữ nguyên nó; preset xung đột được tắt và báo rõ. Các yêu cầu đến muộn bị bỏ sau khi đổi nguồn/cấu hình. Đổi checkbox giữ kết quả đã hoàn thành để tiếp tục xuất.

## Kiểm chứng

| Nhóm | Kết quả |
| --- | --- |
| Core/application cặp vùng, migration, tamper và stale | 109/109 |
| Hợp đồng Toán cũ | 238/238 |
| E3 core và application | 416/416 và 19/19 |
| Application cũ: history, lease, codec, worker | 35/35 |
| Chromium / Firefox | 19/19 mỗi host |
| Desktop WebView2 | 17/17 |
| Native/WASM parity | 603/603 mỗi browser: 99 mới + 504 cũ |

Kiểm UI gồm cấu hình nguyên khối, lưu/mở cặp tùy chỉnh, tệp do native tạo khi all-off, Undo/Redo, SVG/PNG, composition mô phỏng, reload, mobile và offline trên browser. Có kiểm trực quan trong in-app browser cho ngoặc nhóm Hóa và cấu hình. Chưa dùng các bài này để tuyên bố kiểm lại Telex/VNI OS hoặc SC1 Word. W0 G2/G3 giữ trạng thái chờ người dùng.

## Chạy thử

Mở [SC1 local](http://127.0.0.1:4185/). Khi server đã dừng: chạy `./tools/sc1/local.ps1 start` từ thư mục dự án; `status`/`stop` xác minh instance thuộc bản đã chạy. E3 cũ vẫn ở cổng 4184.

Trong Một công thức, thử `toan-[x mũ 2]`, `ly-[v=10 m/s]`, `hoa-[H2SO4]`, `hoa-[K4[Fe(CN)6]]`. Trong Vùng có cặp bọc, thử `Bài toan-[x^2] và hoa-[H2O]`. Đổi Chung thành `lc-[` / `]` trong cài đặt để dùng `lc-[…]`.

Desktop cùng build: `artifacts/sc1/builds/20260915-033948-475/desktop/Locus.Desktop.Shared.exe`. Đây là build local có kiểm chứng, chưa là gói alpha SC1-10. Bộ cài Word E3 đang có giữ phạm vi cũ.

## Tiếp theo

SC1-03: dữ liệu draft/proposal/lịch sử nhận và codec; SC1-04: chữ thường Hóa và dấu `=`; SC1-05: cân bằng chính xác; SC1-06: kho sản phẩm có điều kiện; SC1-07/08: ghost/phím/IME; SC1-09/10: Word thủ công và gói nghiệm thu. [Hợp đồng](CONTRACT.md) ghi giới hạn và bất biến cần giữ.
