# SC1 — Bộ tình huống cần nghiệm thu

Kế hoạch ngày 2026-09-15. Tất cả hàng dưới là **kỳ vọng, chưa chạy**. Khi triển khai SC1-01, chuyển chúng thành fixture có cấu hình/nguồn/selection/điều kiện và output cụ thể. [Kế hoạch](PLAN.md), [trạng thái task](../BACKLOG.md).

## 1. Cặp bọc và định tuyến

| ID | Đầu vào/thao tác | Kỳ vọng |
| --- | --- | --- |
| MR-01 | Nâng preferences có cặp chung tùy chỉnh | Giữ nguyên mở/đóng; không tự đổi `lc[` thành `lc-[` |
| MR-02 | `lc[x mũ 2]`, Toán bật | Đọc x² theo cặp chung; giữ raw và đúng hai span |
| MR-03 | Cấu hình chung `lc-[` / `]`, nhập `lc-[x mũ 2]` | Chỉ cấu hình mới khớp; không có alias `lc[` ẩn |
| MR-04 | `toan-[x+1=2]`, chỉ Hóa bật | Đọc Toán đúng vùng; checkbox giữ nguyên |
| MR-05 | `ly-[v=10 m/s]`, tắt tất cả checkbox | Đọc Lý do chỉ định tường minh; nguồn ngoài cặp không tự nhận diện |
| MR-06 | `hoa-[H2SO4]`, Hóa tắt | Đọc Hóa trong vùng; không bật Hóa toàn cục |
| MR-07 | Tắt cả ba, nhập cặp chung hoặc chuỗi thường | Không nhận diện nguồn mới; snapshot cũ vẫn xuất được |
| MR-08 | Một đoạn có ba loại cặp riêng, emoji/NFD và câu văn xen giữa | Vùng không chồng nhau; giữ nguyên văn bản ngoài vùng và offset UTF-16 |
| MR-09 | `hoa-[K4[Fe(CN)6]]` | Ngoặc nhóm trong công thức không đóng nhầm cặp ngoài |
| MR-10 | Cặp chung lồng cặp riêng và ngược lại | Từ chối toàn vùng lồng; không chuyển riêng vùng con |
| MR-11 | Dấu mở trùng/prefix nhau; cặp rỗng, CR/LF, Unicode lỗi | Cài đặt bị từ chối toàn bộ; giữ cấu hình hợp lệ trước đó |
| MR-12 | Bốn cặp khác dấu mở cùng dấu đóng `]` | Cấu hình hợp lệ; mỗi closer đóng đúng mục đang mở |
| MR-13 | URL/email/path chứa hình giống cặp | Không chuyển phần bên trong nội dung được bảo vệ |
| MR-14 | Đổi cặp/checkbox trong lúc chờ, caret đi sang vùng khác | Kết quả cũ hết hiệu lực; không chạm nguồn mới |
| MR-15 | Cặp cũ tùy chỉnh trùng preset mới khi nâng cấp | Giữ cặp cũ; báo mục mới chưa thể kích hoạt, không ghi đè |
| MR-16 | Cặp chưa đóng hoặc đã đóng nhưng rỗng | Không auto; vùng đang gõ chỉ được dùng cho preview thích hợp |
| MR-17 | Cùng cặp qua ba Cách đọc; cặp lỗi trong câu | Một công thức chỉ nhận một vùng toàn nguồn; Markers quét cặp; Passive không nhận lại đoạn con của cặp lỗi |

## 2. Cách gõ và cân bằng

| ID | Đầu vào/thao tác | Kỳ vọng |
| --- | --- | --- |
| CH-01 | `hoa-[h2+o2=h2o]` | Hiểu alias chữ thường đơn nghĩa; đề xuất `2H2+O2->2H2O`, không âm thầm thay nguồn |
| CH-02 | `hoa-[h2+o2=h20]` | Không đổi 0 thành O tự động; repair nếu có phải được chọn rõ trước hỗ trợ tiếp |
| CH-03 | `Co`, `CO`, `No`, `NO` trong cặp Hóa | Ký hiệu chuẩn giữ đúng nghĩa và hoa/thường |
| CH-04 | `co`, `co2`, `no` trong cặp Hóa | Báo mơ hồ hoặc yêu cầu viết rõ; không nhận ghost một phím |
| CH-05 | `toan-[x+1=2]`, `ly-[v=10 m/s]` | `=` không trở thành mũi tên, không suy sản phẩm |
| CH-06 | Vế trái thiếu chất/dấu cộng cuối/hai dấu phân cách | Chờ hoặc báo lỗi; không bỏ đoạn lỗi để đoán một phản ứng con |
| BL-01 | `H2+O2->H2O` | Hệ số 2,1,2; giữ công thức chất |
| BL-02 | `Ca(OH)2+HCl->CaCl2+H2O` | Hệ số 1,2,1,2; đếm đúng nhóm |
| BL-03 | `Fe^2++Ce^4+->Fe^3++Ce^3+` | Hệ số 1,1,1,1; bảo toàn điện tích |
| BL-04 | `Na^+->Na` | Không có nghiệm bảo toàn điện tích; không tự thêm electron |
| BL-05 | `H2->H2O` | Không có nghiệm với các chất đã nêu; không tự thêm O2 |
| BL-06 | `C+O2->CO+CO2` | Nhiều tỉ lệ nghiệm; không báo một kết quả chắc chắn từ mục tiêu số nhỏ nhất |
| BL-07 | Phương trình đã tối giản; hoặc các hệ số cùng nhân đôi | Không sinh đề xuất trùng; nếu rút gọn thì ghi đúng thao tác đề nghị |
| BL-08 | Hai species trùng nhau ở cùng vế hoặc hai vế | Phát hiện suy biến/không duy nhất; không lén gộp hoặc bỏ chất |
| BL-09 | Ion, ngoặc lồng, hệ số lớn và nguồn vượt ngân sách | Tính chính xác trong phạm vi; vượt giới hạn phải dừng có kiểm soát |
| BL-10 | `⇌` hoặc `<->` đã có đủ vế | Giữ loại mũi tên; không tuyên bố đã tính cân bằng nhiệt động |

## 3. Suy sản phẩm

| ID | Đầu vào/thao tác | Kỳ vọng |
| --- | --- | --- |
| PR-01 | `HCl+NaOH=` với điều kiện dung dịch đã chọn | Quy tắc được kiểm cho sản phẩm NaCl/H2O; cân bằng và có provenance |
| PR-02 | Cùng nguồn nhưng thiếu điều kiện bắt buộc của quy tắc | Báo cần điều kiện; không xem giả định của chương trình là dữ kiện người dùng |
| PR-03 | `CO2+NaOH=` thiếu tỉ lệ/chất dư | Không tự chọn carbonate hoặc bicarbonate; yêu cầu điều kiện phù hợp |
| PR-04 | Đổi điều kiện của PR-03 trong lúc kết quả trước đang chạy | Chỉ kết quả ứng với điều kiện mới được nhận |
| PR-05 | `H2+O2=` | Chỉ có ghost nhận được khi quy tắc và điều kiện đủ; kết quả đã qua cân bằng |
| PR-06 | Nguồn hợp lệ nhưng ngoài danh mục | “Chưa hỗ trợ/chưa có dữ liệu”; không trả “Không phản ứng” |
| PR-07 | Một quy tắc có ngoại lệ cụ thể | Ngoại lệ chặn khớp; bộ kiểm có case ngoài phạm vi gần giống |
| PR-08 | Kho chứa bản ghi tạo sản phẩm không bảo toàn | Kiểm độc lập từ chối; không đưa ra preview có thể nhận |
| PR-09 | Nhiều hơn ba khả năng hoặc miền đầu vào còn mơ hồ | Không coi danh sách bị cắt là đủ chắc chắn; yêu cầu thu hẹp điều kiện |
| PR-10 | Phản ứng đảo thứ tự chất ban đầu hoặc khác cách viết chỉ số Unicode | Cùng kết quả hóa học khi dữ kiện tương đương; giữ nguồn/ID phiên của từng lần gõ |
| PR-11 | Điều kiện đã biết thay đổi bản chất phản ứng | Bài kiểm giữ riêng xác minh engine xét điều kiện, không chỉ tra cặp tên chất |
| PR-12 | Máy offline, bộ quy tắc đã cài | Phân tích/suy/cân bằng được trong phạm vi kho; không gọi dịch vụ bên ngoài |

## 4. Ghost, dữ liệu và host

| ID | Thao tác | Kỳ vọng |
| --- | --- | --- |
| UX-01 | Ghost hiện cho nguồn `h2+o2=` trong ngữ cảnh đủ điều kiện | Thấy sản phẩm mờ và toàn phương trình sẽ nhận, gồm hệ số vế trái |
| UX-02 | Enter khi ghost hợp lệ đã hiện | Nhận đúng kết quả và hệ số; không xuống dòng thêm; một Undo |
| UX-03 | Enter trước khi ghost hiện, rồi kết quả đến muộn | Phím không bị dùng lại để nhận; nguồn mới giữ nguyên |
| UX-04 | Space mặc định ngay sau `=` | Chỉ thêm khoảng trắng; tiếp tục gõ sản phẩm được |
| UX-05 | Bật Space nhận; ghost hợp lệ đã hiện | Nhận và thêm một khoảng trắng trong một Undo; caret sau dấu đóng nếu cặp đã đóng, cuối thân nếu chưa đóng; không có ghost thì Space thường |
| UX-06 | Esc rồi chờ, không sửa nguồn/điều kiện | Ghost đã bỏ không tự bật lại; fx có thể yêu cầu lại |
| UX-07 | Gõ sản phẩm, sửa hệ số, dán, chọn hoặc di chuyển caret | Đề xuất cũ không ghi đè nội dung; không che phần đang sửa |
| UX-08 | `hoa-[h2+o2=` chưa có dấu đóng, rồi nhận | Chỉ thay phần thân; không tự thêm `]` hoặc chuyển native |
| UX-09 | Nguồn có cặp đã đóng, nhận rồi Undo/Redo/restore | Cặp, raw trước nhận, selection và kết quả đúng; không ăn chữ ngoài cặp |
| UX-10 | Copy/lưu/nháp khi ghost chưa nhận | Không có ghost trong dữ liệu; không xuất kết quả suy như thể đã chọn |
| UX-11 | Copy/lưu sau nhận, mở chéo Web/Desktop và tắt detector | Snapshot và nguồn trước hỗ trợ còn đủ; không cần suy lại bằng kho mới |
| UX-12 | Tab, Shift+Enter, Ctrl+Enter và screen reader | Giữ các phím theo hợp đồng; thông báo gợi ý không giành focus |
| UX-13 | Telex/VNI đang sửa dấu, Backspace, Enter/Space trong quá trình ghép chữ | Không chốt giữa thao tác bộ gõ; có bằng chứng OS riêng, không chỉ composition mô phỏng |
| UX-14 | Key repeat, blur, đổi tab/tài liệu/cấu hình, worker chậm | Không nhận lặp hoặc áp kết quả stale |
| DATA-01 | Tệp v1/v2, snapshot Math/E3 và cặp tùy chỉnh cũ | Đọc nguyên nghĩa, không chạy lại parser/solver lên snapshot |
| DATA-02 | Tệp có schema mới hơn, checksum sai, ID/span/provenance bị sửa | Từ chối có kiểm soát, không ghi đè tài liệu đang mở |
| DATA-03 | Nâng phiên bản kho sau khi đã lưu một kết quả | Mở lại bản đã nhận nguyên vẹn, gồm điều kiện và nguồn gốc cũ |
| WD-01 | Chọn vùng Word → xem đề xuất → xác nhận → Undo/Redo | Native và metadata cùng kết quả đã preview, một giao dịch trên UI thread |
| WD-02 | Save/reopen, sửa native, restore nguồn có cặp tùy chỉnh | Không ghi đè sửa native từ metadata cũ; restore tường minh khôi phục đúng nguồn trước hỗ trợ |
| WD-03 | Focus Find/Ribbon/dialog, đổi tài liệu hoặc sửa nguồn lúc chờ | Hủy ghi; không dựa vào riêng cửa sổ Word đang foreground |
| WD-04 | Đóng cặp hoặc gõ `=`/Space trong Word hiện tại | SC1 thủ công không tự mở G2/G3 hoặc bật ghost inline |

## 5. Cách ghi kết quả

- Chạy phần lõi trên native và WASM, so cấu trúc, nguyên tố/điện tích, thứ tự/loại kết quả, source/provenance và output.
- Kiểm UI trên Chromium, Firefox và Desktop WebView2 theo build thực tế; ghi khác biệt capability thay vì gộp thành một PASS chung.
- Word theo baseline x86 đang có; không lấy preview browser hoặc test renderer làm bằng chứng Word native.
- Đo độ trễ, bộ nhớ, khởi động/kho offline, nhập nhanh và trường hợp vượt giới hạn; ghi máy/runtime và p50/p95.
- Đóng gói, giải nén và chạy lại các demo chính; giữ báo cáo/hash trong thư mục artifact SC1 riêng khi bắt đầu triển khai.
- Các bài đối chiếu người dùng chưa làm phải ghi đang chờ; không tự đóng W0/D-01 bằng một bài kiểm do chương trình phát phím.
