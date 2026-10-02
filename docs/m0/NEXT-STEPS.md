# Đợt thực hiện sau M0

Ngày: 2026-09-12. Đầu vào: [quyết định M0](DECISIONS.md), [báo cáo và trạng thái cổng](REPORT.md), [backlog](../BACKLOG.md). M0 tạo prototype phục vụ quyết định; chưa tạo sản phẩm Locus để cài dùng hằng ngày.

**Tài liệu lưu trữ kế hoạch lập sau M0.** M1 hiện đã hoàn thành: [báo cáo M1](../m1/REPORT.md). Mốc đủ điều kiện tiếp theo là M2-01; trạng thái công việc hiện hành theo [backlog](../BACKLOG.md). Những ước lượng và thứ tự M1 bên dưới được giữ để đối chiếu kế hoạch ban đầu.

## M1 — Bắt đầu từ core

Đợt tiếp theo đủ điều kiện là **M1-01**, rồi **M1-02**. Không cần chờ UX Space hoặc cổng Word để triển khai nguồn và parser.

| Thứ tự | Công việc | Bằng chứng để đóng |
| --- | --- | --- |
| 1 | Khởi tạo Git/build C#; tách core, test runner và adapters; pin SDK/dependency thích hợp | Một lệnh build/test tái hiện được; core không kéo Word/UI vào dependency |
| 2 | SourceSnapshot UTF-16 và normalization/source map | Case NFC/NFD, emoji, NBSP, CRLF và nhiều–nhiều đạt; raw serialize/deserialize nguyên vẹn |
| 3 | Tokenizer, grammar tối thiểu và MathDocument | Chạy parser thật lên các case grammar đã đặc tả; báo AST/span/diagnostic khác kỳ vọng |
| 4 | Candidate set và nhận diện vùng | Direct trước; repair có edit/provenance; tối đa ba; văn xuôi/URL/email/path không bị ăn vào vùng |
| 5 | Nhận diện cặp bọc mặc định và tùy chỉnh ở core | Span nội dung/thay thế riêng; cấu hình lưu theo snapshot; lồng/escape bị xử lý theo baseline |
| 6 | Serializer và bộ xuất từ cùng candidate; spike renderer Desktop | Không parse lại chuỗi nguồn; kiểm tra cấu trúc OMML/preview và lưu/mở snapshot |
| 7 | Runner hồi quy và giới hạn xử lý | Báo riêng các case core, host policy và pending; input dài/lỗi không làm treo; không tạo kết quả cũ sau cancel |

Tách việc nhận diện marker trong core ở M1 khỏi tự ghi Word ở M5A. M1 không cài listener bàn phím toàn hệ thống hay thay tài liệu Word khi gõ. M5A dùng lại kết quả core và bổ sung trigger, revalidation, transaction của host.

117 case M0 không đều là bài parser: có case policy Word và 8 case pending. Runner M1 phải phân loại phạm vi thực thi rõ, không báo “117 parser tests pass” bằng cách chỉ validate fixture hoặc bỏ qua các host gate. Thêm case mới khi gặp lỗi thực tế, giữ nguyên nguồn và loại dữ liệu nhạy cảm trước khi chia sẻ ra ngoài máy.

## Những việc phải giải quyết trước Word

Các việc sau là đầu vào còn thiếu của CW/G1/G2; có thể nghiên cứu song song với M1, nhưng chưa cho phép phát hành auto.

| ID | Vấn đề cụ thể | Điều kiện đóng | Cổng |
| --- | --- | --- | --- |
| W0-01 | Undo trả nội dung nhưng chưa trả đúng selection trước thao tác | Chốt hợp đồng caret/selection, thử native + metadata + restore/detach; một Undo khôi phục đúng hợp đồng mà không tạo thao tác phụ ngoài lịch sử | CW/G1 |
| W0-02 | Tín hiệu editor focus và trạng thái nhập tiếng Việt thực chưa được kiểm chứng | Probe trong editor thật với UniKey Telex/VNI, Backspace/sửa dấu; chuyển Find/Ribbon/dialog/cửa sổ; thiếu tín hiệu chắc chắn phải hủy | CW/G1 cho target thủ công; G2 cho quan sát/auto |
| W0-03 | Metadata copy, ID trùng, payload/version và ngữ cảnh Word đặc biệt chưa đủ | Ctrl+C/Ctrl+V trong/cross document, Save As, metadata hỏng/mới hơn, payload giới hạn; native giữ được khi association mất; test định dạng ngoài vùng sau restore | CW/G1 theo phạm vi công bố |
| W0-04 | Chưa có add-in được cài và lifecycle thật | Build/deploy connector thử trên máy kiểm tra; Word/Desktop mở theo hai thứ tự, reconnect và nhiều tài liệu; không tự bật add-in người dùng đã tắt | M3 connector, G2 |
| W0-05 | Chưa chứng minh `fx` đúng vị trí khi scroll/zoom/DPI | Gắn UI đúng equation/range ở từng cửa sổ; candidate cũ không trỏ sai; nếu mới làm task pane thì M4 còn mở | G2/M4 |
| W0-06 | D-01 chưa chọn cách nối công thức sau Space | Native prototype + phím thật + người thử thực hiện các bài ở SPACE-EXPERIMENT; chốt kết thúc/tiếp tục/Undo | G3/M5B |

W0-02 hiện có hạn chế capture/click của công cụ UI trong phiên M0. Có thể tiếp tục bằng một môi trường quan sát hoạt động hoặc instrumentation của add-in thử; không suy ra PASS từ checkbox composition của demo. W0-03 phải thêm các trường hợp Word read-only, protected, Track Changes, bảng/header/footer; từ chối các tổ hợp ngoài phạm vi đã thử.

Nếu CW chưa đạt, M1 và M2 vẫn tiếp tục theo kế hoạch Desktop độc lập. Sau khi CW đạt, M3 bắt đầu bằng lệnh chọn vùng và xác nhận, rồi mới tới quan sát khi gõ. Cặp bọc `lc[...]` giữ là mục tiêu M5A đã thống nhất.

## Ước lượng có điều kiện

Giả định một người phát triển có kinh nghiệm C#/.NET dành trọn thời gian, có người thử để kiểm tra thao tác Word và không mở rộng grammar M1. Đơn vị là **ngày công tập trung**, không phải ngày lịch hoặc tốc độ đã đo của dự án. M0 mới giúp tách rủi ro; chưa có velocity triển khai production. Hiệu chỉnh sau M1-02 và demo đầu M1-04.

| Phần việc | Khoảng để lập kế hoạch | Giả định chính |
| --- | --- | --- |
| M1 core, corpus runner, bộ xuất đầu | 10–18 ngày công | Grammar M0; không thêm tích phân/ma trận chỉ vì fixture Word dựng được |
| M2 Desktop alpha | 8–15 ngày công | Renderer và clipboard spike được chốt; chưa gồm đồ thị/hình học |
| W0-01 đến W0-04 để mở CW | 5–10 ngày công nghiên cứu bổ sung | Có môi trường UI thử được; không hứa chắc kết quả mọi API |
| M3 chuyển Word có xác nhận | 10–18 ngày công sau CW | Một baseline Word/Windows công bố rõ; host guards và metadata thực được kiểm thử |

Không cộng máy móc các khoảng thành ngày phát hành: W0 có thể song song M1/M2, hoặc phải đổi hướng nếu bằng chứng mới phủ định cơ chế hiện tại. M4–M6 và các nhánh mở rộng tiếp tục giữ theo điều kiện roadmap; ước lượng chi tiết sau khi vòng nhập–chọn–restore thật ở M3 đạt.

Demo đầu tiên của M1 cần cho thấy `can2`, `x mũ 2`, `1 trên 2`, `x+1/2` và một câu có `lc[...]` đi qua **parser thật**. Với `x+1/2`, kết quả trực tiếp và repair đổi phạm vi phải có nhãn khác nhau; nguồn tiếng Việt tổ hợp phải khôi phục đúng từng code unit.
