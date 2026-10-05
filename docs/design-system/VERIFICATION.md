# Locus Studio — kiểm chứng trang Công thức

Ngày: 03/10/2026. Tham chiếu: `ui/locus_studio (2).html` và phản hồi của người dùng về bố cục, typography, panel cũ, hover.

Sau đợt kiểm chứng giao diện dưới đây, luồng nút Cân bằng được thay theo phản hồi tiếp theo. Xem [hành vi và kiểm chứng mới](BALANCE-COMMAND.md). Các bước chọn trước trong bảng cũ là lịch sử của bản giao diện đầu.

Bản local bàn giao: `20261003-112447-436`, tại <http://127.0.0.1:4197/releases/20261003-112447-436/>. Log build: `artifacts/ui-design/build-complete.log`.

## Kết quả

- Đã đưa tokens, font đóng gói cục bộ, icon và theme vào editor chung Web/Desktop. File HTML tham chiếu được giữ nguyên.
- Trang Công thức dùng header, panel nhận diện/cân bằng, hai ô 520px, hàng Ý bạn là và thanh xuất gọn theo mẫu. Undo/Redo nằm trên header.
- Không còn các panel Tùy chọn & nháp, Nháp gần đây, Định dạng công thức đang chọn, Phím tắt, checkbox Tự cân bằng, vùng Thử nhanh riêng hay fx trong kết quả.
- Các tác vụ vẫn đi qua session và candidate thật. Hỗ trợ Hóa, chỉnh cách hiểu và giữ text gốc xuất hiện theo ngữ cảnh.
- Chưa đổi bố cục riêng bên trong editor Đồ thị/Hình học. Hai tab vẫn mở và quay lại Công thức được.

## Build và kiểm tra tự động

Build bằng `tools/web1/build.ps1 -SkipNpm`: Web, worker và Desktop shared xuất bản thành công. Desktop ở đây là bản publish phụ thuộc runtime, không phải bộ cài mới hay nghiệm thu native Windows.

Các kiểm tra Application hiện có đã chạy trong đợt thay giao diện, trước các tinh chỉnh bố cục/toast cuối; mã Application không thay đổi trong các tinh chỉnh đó:

| Nhóm | Kết quả | Bằng chứng local |
|---|---|---|
| Nội dung hỗn hợp | 22/22 đạt | `artifacts/ui-design/verification/content/content-tests.json` |
| Cân bằng và lịch sử | 20/20 đạt | `artifacts/ui-design/verification/balance/balance-tests.json` |
| Xuất tài liệu | 7/7 đạt | `artifacts/ui-design/verification/export/document-export-tests.json` |

`node --check` cho `studio-theme.js` và `editor.js` đạt. `git diff --check` không có lỗi khoảng trắng.

## Kiểm tra bằng in-app browser

| Tình huống | Quan sát |
|---|---|
| Cỡ chữ/bố cục | Hero 43.2px trên desktop, source 14px, result 16px, nút cân bằng 11px; Undo nằm trong header. Các selector panel bị yêu cầu bỏ có số lượng 0. |
| Hiệu ứng | CSS cho hover/selected của công thức, tab, nhận diện, tải/copy, xóa trắng, toast và scrollbar đối chiếu mẫu; transition control 150ms. Có focus-visible và reduced-motion. |
| Cách hiểu | `x+1/2`: chọn đề nghị `(x+1)/2` đổi kết quả; nguồn giữ nguyên. Undo trên header khôi phục cách hiểu trước đó. |
| Cân bằng chủ động | `H2+O2=H2O` giữ nguyên khi nhập; chọn và bấm cân bằng cho `2H2+O2→2H2O`; hủy trả về hệ số ban đầu; Undo khôi phục kết quả trước hủy. |
| MANUAL | `lc[x mũ 2 + 1]`, `ly-[v=10 m/s]`, `hoa-[H2+O2=H2O]` trong một đoạn cho ba công thức; chữ thường và xuống dòng giữ nguyên. |
| Phạm vi | Chọn toàn đoạn đổi toolbar sang DOCX/HTML/TXT/Copy văn bản. Bấm riêng một công thức đổi lại SVG/PNG/Copy ảnh. |
| Theme | Đã xem sáng/tối; trả lại sáng khi bàn giao. |
| Khung hẹp | CSS viewport thực tế 487px trong IAB: hai ô xếp dọc, không tràn ngang. IAB không áp dụng đúng các kích thước yêu cầu nhỏ hơn; chưa xác nhận 390px trên thiết bị thật. Đã reset viewport tạm. |
| Chuyển tab | Công thức → Đồ thị → Hình học → Công thức giữ nguyên nguồn `x mũ 2 + 1` và kết quả. Vùng chọn được chọn lại khi quay về. |
| Console | Không có warn/error trong lượt kiểm tra cuối. |
| Thông báo lặp | Sau khi toast SVG tự ẩn, bấm cùng nút làm toast hiện lại. Mỗi lần gán thông báo có revision riêng; re-render thông thường không kéo dài thời gian hiện. |

Ảnh bàn giao local: `artifacts/ui-design/studio-final.jpg`.

## Giới hạn xác minh xuất/copy trong IAB

SVG báo tạo tệp thành công trên UI, nhưng công cụ IAB không cung cấp tệp tải để đối chiếu. Chờ sự kiện tải PNG hết hạn; clipboard PNG không xác nhận được dữ liệu và app báo chưa chuyển được. Nút PNG 2X/4X đã nối vào renderer với tỷ lệ tương ứng, nhưng chưa xác minh byte ảnh đầu ra trên bản giao diện này.

Đây là giới hạn đã ghi ở `docs/host-review/20260930.md` (D-FILE-01). Không coi thông báo thành công trên UI là bằng chứng tệp đã đến máy, và không dùng kết quả native của bản cũ để nghiệm thu bản mới. Chưa chạy lại Word, bộ gõ native hay clipboard Windows trong đợt thay giao diện này.

Thanh xuất toàn bài dùng DOCX/HTML/TXT và văn bản có LaTeX theo khả năng hiện có. Xuất toàn bài thành ảnh chưa thuộc triển khai này.
