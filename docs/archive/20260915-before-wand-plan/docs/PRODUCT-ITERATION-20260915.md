# Điều chỉnh sản phẩm theo trao đổi ngày 2026-09-15

Người dùng yêu cầu báo thành quả, brainstorming các luồng mới và ưu tiên triển khai theo trải nghiệm trước lượt kiểm tổng hợp tiếp theo. Tài liệu này ghi yêu cầu mới, phương án đề xuất và thứ tự cụ thể. Chưa coi các thiết kế dưới đây là tính năng đã phát hành hoặc UX đã được người dùng nghiệm thu.

## 1. Đang ở đâu

| Phần | Kết quả đã có |
| --- | --- |
| M0–M3 | Hợp đồng/core, Desktop độc lập và chuyển vùng chọn thành công thức native Word; sửa/khôi phục/Undo trong phạm vi alpha |
| WEB0, SH, WEB1 | Core WASM, editor dùng chung Web/Desktop; lưu/mở, nháp, offline, xuất SVG/PNG/văn bản; Web hiện local |
| E3 | Toán/Lý/Hóa trong cùng giao diện, ba checkbox; Hóa/Lý cơ bản trên Web/Desktop và Word thủ công; 8/8 task alpha |
| SC1-01…07 | Cặp chung/riêng tùy chỉnh; alias Hóa; cân bằng; kho offline 45 bản ghi có điều kiện; ghost/Enter, Undo và nguồn trước hỗ trợ |
| SC1-08 | Đang kiểm bàn phím/IME. Telex có bằng chứng trên Desktop và WASM WebView2; VNI và một số bài nhập nối/focus còn mở. Dừng lượt kiểm dài theo yêu cầu mới |
| SC1-09/10 | Chưa tích hợp Hóa thông minh vào Word và chưa nghiệm thu gói SC1 đầy đủ |
| W0 | 4/6 (67% mốc); G2/G3 và phản hồi A/B giữ mở, người dùng báo sau |
| E1/E2A/E2B, thương mại Web | Có kế hoạch; chưa có editor đồ thị/hình học sản phẩm, tài khoản hoặc thu phí |

SC1 chính có 7/10 task đã nghiệm thu, tương đương **70% theo số task của riêng mốc**, không phải 70% khối lượng hoặc toàn dự án. SC1-11 ghost inline Word là nhánh có cổng riêng. Chưa có cơ sở báo phần trăm toàn dự án, đặc biệt sau khi thêm tài liệu dài và thương mại.

[Ghost đã đạt](sc1/GHOST-REPORT.md), [kho phản ứng](sc1/CATALOG-REPORT.md), [backlog](BACKLOG.md). Các báo cáo cũ giữ nguyên phạm vi; không dùng bằng chứng WEB1/E3 thay cho SC1.

## 2. Giữ nguyên và hỗ trợ cân bằng

Yêu cầu: cho phép người dùng tạo hoặc giữ phương trình chưa cân bằng, thậm chí sai về nội dung khoa học. Locus hỗ trợ soạn thảo, không ép bài viết phải là một phản ứng đúng. Điều này hữu ích cả khi soạn câu hỏi tìm lỗi.

Đề xuất hai ô trong cùng khu vực kết quả, chỉ thêm ô thứ hai khi có đề xuất:

- **Theo nội dung đã gõ**: `H₂ + O₂ → H₂O`. Mặc định chọn; được copy/xuất/chèn Word như các công thức có cấu trúc hợp lệ khác. Có thể ghi nhãn nhẹ “Chưa cân bằng”; không khóa nút chỉ vì hệ số sai.
- **Hỗ trợ cân bằng**: `2H₂ + O₂ → 2H₂O`. Hiện các hệ số thay đổi và nút **Dùng bản này**. Không tự thay ô thứ nhất hoặc source.
- **Giữ nguyên / Bỏ gợi ý** hủy đề xuất cho vùng và phiên hiện tại. Sau khi nhận có **Trở về trước cân bằng** và Undo. Nếu đã sửa tiếp, khôi phục phải chỉ rõ nội dung sẽ quay về, không lén dùng bản cũ đè đoạn mới.

Hai ô phân biệt nguồn với hỗ trợ; không mở thêm ba lựa chọn cho từng ô. Tổng phương án của một vùng vẫn tối đa ba. Mơ hồ cú pháp giải quyết trước; nếu đã cân bằng thì không tạo bản trùng. `h20` hoặc cú pháp không parse được vẫn giữ văn bản nhưng không bịa AST/native hay tự sửa số 0 thành chữ O. Bỏ cân bằng không biến nó thành kết quả đúng về khoa học, cũng không cản người dùng giữ nội dung.

Suy sản phẩm sau `=` tiếp tục là hành động có nguồn/điều kiện và có thể bỏ. Trạng thái thiếu dữ liệu không ngăn người dùng tự viết sản phẩm. Checkbox nhận diện Hóa tách khỏi việc nhận cân bằng; bật detector không đồng nghĩa đồng ý chỉnh hệ số.

## 3. Giao diện nhỏ gọn và khay hệ thống

Hiểu “ms tray” là một cửa sổ tiện ích mở từ khay hệ thống Windows. Web có cùng bố cục nội dung, thích ứng màn hình, không có nút bung cửa sổ kiểu Desktop.

Wireframe đề xuất:

```text
Locus                              [Bung cửa sổ] [Đóng]
[Công thức] [Biểu đồ] [Hình học]
┌ Nhập công thức hoặc dán đoạn văn ─────────────────┐
│                                                  │
└──────────────────────────────────────────────────┘
[Theo nội dung đã gõ]       [Hỗ trợ cân bằng, nếu có]
              kết quả / toàn đoạn xem trước
                [Copy PNG] [Copy SVG] [Tải ↓]
[✓ Toán] [✓ Lý] [✓ Hóa]                  [Tùy chọn]
```

- Cửa sổ gọn: ưu tiên ô nhập, kết quả, xuất; bỏ phần giới thiệu lớn và sidebar không cần thiết. Chiều rộng/chiều cao chốt bằng prototype, không cố nhét cả canvas hình học vào hộp quá nhỏ.
- Icon có tooltip và tên trợ năng; trạng thái đang chọn và kết quả sắp copy phải rõ. Tùy chọn chứa cặp bọc, Space, kiểu copy, nháp; vẫn truy cập được bằng bàn phím.
- Desktop: icon tray mở/ẩn hộp gọn; nút bung mở cửa sổ lớn giữa màn hình, nút thu trở lại. Cùng một session/source/Undo khi đổi cỡ, không nhân đôi tài liệu. Menu tray có Mở và Thoát rõ ràng. Việc tự khởi động cùng Windows là tùy chọn riêng, không tự bật.
- Ba tab là Công thức, Biểu đồ, Hình học. Trong Hình học có chọn 2D/3D. Chức năng chưa triển khai không hiện như đã dùng được. E1 vẫn giữ thanh trượt; E2 giữ thao tác vẽ/kéo/nét liền-đứt và copy SVG.
- Chỉ cần bộ quy tắc giao diện tối thiểu phục vụ các màn này: chữ, khoảng cách, màu trạng thái, icon, focus và các component ô nhập/kết quả/tab/options. Không mở một dự án template/design system tách khỏi sản phẩm.

## 4. Dán cả đoạn trên Web

Yêu cầu: đầu ra vẫn là nguyên đoạn, chỉ những vùng nhận diện được đổi cách hiển thị thành công thức. Ví dụ:

`Cho x mũ 2 + 1. Nước là H2O. Tìm 1 trên 2.`

→ giữ câu chữ/xuống dòng và render công thức ngay tại vị trí tương ứng. Mỗi vùng có fx để giữ text, chọn cách đọc hoặc nhận hỗ trợ. Preview không tự cân bằng phương trình Hóa trong đoạn.

Thiết kế đề xuất:

1. Thêm tài liệu gồm các đoạn text và vùng công thức, mỗi vùng có ID/span UTF-16/revision, raw, snapshot và quyết định của người dùng. Reuse core hiện có cho từng vùng; không nâng trần 4096 của một công thức rồi đưa nguyên tài liệu lớn vào parser.
2. Chia việc theo đoạn/ranh giới rõ, map offset về nguồn toàn tài liệu; không cắt giữa ký tự Unicode, công thức hoặc cặp bọc. Vượt giới hạn vùng thì giữ text và báo vị trí, không bỏ nội dung. Khởi điểm đề xuất tối đa 100.000 UTF-16/tài liệu và 4096/vùng, sẽ điều chỉnh bằng số đo.
3. Chỉ tự đưa vào preview bản đọc trực tiếp duy nhất đủ điều kiện. Vùng mơ hồ, cú pháp lỗi, URL/email/path hoặc đã được người dùng chọn giữ text không tự đổi. Tùy chỉnh detector/cặp áp dụng lại vùng chưa chốt, không xóa quyết định trước đó.
4. Nguồn gốc là bản text bất biến của phiên; mọi phần ngoài vùng được giữ chính xác. Văn bản đã được trình duyệt chuyển newline khi dán được coi là nguồn nhận vào; không tuyên bố giữ bytes gốc clipboard.
5. Đợt đầu hỗ trợ paste text và giữ nội dung/xuống dòng. Rich text giữ font/bảng/list/hình từ Word là phần tiếp theo, cần model và mapping riêng; không gọi paste text là bảo toàn toàn bộ định dạng.
6. Kết quả có **Copy cả đoạn**, **Tải DOCX** và các vùng copy riêng. Copy mixed HTML cần kiểm ứng dụng nhận; không mặc định rằng MathML/ảnh trong clipboard tạo equation native Word. Tải DOCX dùng OMML từ chính snapshot đã chọn để đảm bảo đường xuất native có thể kiểm. Có fallback text/LaTeX công khai.

Luồng dài có tiến độ và Hủy, xử lý theo phần, preview chỉ render phần cần nhìn. Bản nguồn đầy đủ vẫn tồn tại khi hủy/lỗi. Đây là DOC1 mới; chức năng Nhận diện trong câu hiện có chỉ là nền dò vùng, chưa phải bộ biên tập/xuất nguyên tài liệu dài.

## 5. Quét tài liệu Word đã có

Yêu cầu: quét vùng chọn hoặc cả tài liệu, đánh dấu những chỗ đã nhận diện, chuyển từng chỗ hoặc dùng **Chuyển tất cả vùng đã nhận diện** trong mọi fx.

Quyết định quan trọng: trạng thái “đã nhận diện, đang để text” được lưu trực tiếp. Không thực hiện chu trình đổi sang native rồi hoàn lại text để tạo metadata. Việc quét chỉ đọc và tạo danh sách vùng; người dùng chưa nhận thì nội dung Word không bị sửa.

| Trạng thái vùng | Ý nghĩa và hành động |
| --- | --- |
| Đã nhận diện · đang là text | Có nguồn/candidate và dấu nhận diện; có thể chuyển riêng hoặc tham gia chuyển hàng loạt |
| Cần chọn cách đọc | Có fx, chưa được chuyển hàng loạt cho đến khi người dùng chọn |
| Giữ text theo lựa chọn người dùng | Không bị chuyển lại bởi Convert all hoặc quét lại cùng nguồn; có lệnh đưa lại vào danh sách |
| Đã chuyển thành equation Locus | Mở snapshot, đổi lựa chọn, khôi phục text hoặc tách quản lý |
| Đã thay đổi từ lúc quét | Bỏ kết quả cũ, quét lại vùng; không ghi dựa trên offset cũ |

Từ equation quay về text nên có hai lựa chọn rõ: **Về text, giữ nhận diện** và **Giữ text, bỏ qua khi chuyển tất cả**. Như vậy không đánh đồng vùng chưa chuyển với vùng người dùng chủ động từ chối.

Trong fx bất kỳ, nút hàng loạt hiển thị phạm vi và số lượng, ví dụ **Chuyển 12 vùng trong tài liệu này**. Không áp dụng sang tài liệu khác, không nhận cân bằng/suy sản phẩm/sửa lỗi hàng loạt. Chỉ dùng bản trực tiếp hoặc kết quả cụ thể người dùng đã chọn.

Batch tạo kế hoạch immutable, kiểm lại document/window/source/config/selection trước ghi, đối chiếu từng Range; xử lý từ cuối tài liệu về đầu để tránh dịch vị trí. Một Undo cho batch trong phạm vi đã hỗ trợ; lỗi giữa chừng rollback và xác minh, không giữ trạng thái nửa chừng như thành công. Với tài liệu quá lớn, chia batch do người dùng nhìn thấy, không hứa một Undo vô hạn.

Dấu highlight ưu tiên lớp hiển thị tạm, không ghi màu highlight vào nội dung tài liệu gốc. Khả năng Word/COM vẽ dấu ở mọi zoom/DPI cần spike riêng; nếu chưa đạt, dùng danh sách điều hướng vùng trong panel và chưa gọi highlight inline là hoàn thành. Bảng/header/footer/footnote/textbox/Track Changes/protected file có phạm vi riêng, ban đầu giữ giới hạn thân tài liệu đã kiểm của M3.

WD1 là quét/chuyển thủ công tài liệu có sẵn, không cần tự mở cổng auto-Space W0/G2/G3. Dấu fx tự bám con trỏ nhập mới vẫn thuộc M4; ghost inline thuộc SC1-11.

## 6. Hạn mức Web và tài khoản

Các con số người dùng đề nghị:

| Gói | Công thức/ngày | Biểu đồ/ngày | Hình 2D/ngày | Hình 3D/ngày |
| --- | ---: | ---: | ---: | ---: |
| Guest | 5 | 2 | 2 | 2 |
| Free có tài khoản | 21 | 10 | 10 | 10 |
| Premium | 150 | 100 | 100 | 100 |

Guest theo ngày là cách hiểu đề xuất để đồng nhất ba gói; chưa có quyết định giá Premium, reset theo múi giờ nào hoặc chính sách Desktop/offline. Không suy “Premium” là vô hạn khi bảng có trần.

Đề xuất quy tắc tính lượt trước khi lập trình:

- Không tính mỗi phím gõ, lần auto detect, render, chạy slider, kéo điểm hoặc bản gợi ý chưa dùng.
- Tính khi lần đầu xác nhận dùng/lưu/xuất một nội dung công thức hay scene mới trong ngày. Giữ ID và dấu vân tay kết quả để copy PNG/SVG lại, đổi font/màu hoặc Undo/Redo không bị trừ thêm. Nội dung công thức thay đổi và được dùng như kết quả mới tính lượt mới; chỉ sửa nháp chưa tính.
- Đồ thị tính theo tài liệu/scene đã dùng, không tính từng điểm mẫu hoặc frame thanh trượt. Số đường/vật thể tối đa trong scene là giới hạn tài nguyên riêng.
- Đoạn dán gồm 20 công thức được dùng thì tính 20, không tính một lần dán. Báo trước “Cần 20 lượt, còn 5”; cho chọn 5 vùng hoặc nâng gói, không âm thầm trả đoạn thiếu công thức. Chỉ đưa ra quyết định giới hạn ở bước dùng, không cắt source/preview.
- Guest hết lượt: hiện Đăng nhập/Tạo tài khoản để dùng hạn mức Free; vẫn xem, sửa nháp và lấy lại nội dung đã tạo. Không xóa dữ liệu hoặc khóa đường khôi phục.
- Backend quản lý account/entitlement/usage ledger, request ID chống trừ đôi, hoàn lượt khi thao tác thất bại và đồng thời nhiều tab. Không gửi nội dung tài liệu/công thức lên server chỉ để tính lượt; định danh nhu cầu thu thập tối thiểu trong thiết kế riêng.

Core đang chạy local và có offline, nên bộ đếm client hoặc cookie chỉ hạn chế thông thường, có thể bị đặt lại. Backend quản lý quyền dịch vụ và lượt tài khoản tốt hơn nhưng không biến core đã tải xuống thành chức năng không thể chạy nếu client bị sửa. Cần chọn mức kiểm soát phù hợp; không hứa quota cứng offline đồng thời không tin client. Quyền offline và Desktop giữ chưa quyết định.

Các mức 5/21/150 là giả thuyết sản phẩm để pilot, chưa có bằng chứng chúng tạo thói quen hoặc tối ưu doanh thu. Hạn mức chỉ bật cho chức năng đã có; không quảng cáo 3D trước E2B. WEB2 xử lý tài khoản/quota trước tích hợp thanh toán; giá, provider và xuất bản online cần quyết định riêng. Người dùng hiện vẫn chọn local, không triển khai Sites hoặc login/paywall trong đợt này.

## 7. Thứ tự triển khai đã điều chỉnh

| Đợt | Task cụ thể | Đầu ra review được |
| --- | --- | --- |
| 1 | UX1-01 | Wireframe gọn Web/Desktop, tray/bung, hai ô nguồn/hỗ trợ; các trạng thái loading, không nhận diện, mơ hồ, hết lượt chỉ được mô tả |
| 2 | UX1-02/03 | Nút Giữ nguyên/Bỏ hỗ trợ/Trở về trước cân bằng; layout chung gọn, icon xuất có nhãn, options và focus giữ đúng |
| 3 | DOC1-01/02 | Document model vùng/text, dán đoạn dài → trả nguyên đoạn với công thức inline; quyết định giữ text theo vùng |
| 4 | DOC1-03 + UX1-04 | Copy cả đoạn, xuất DOCX; Desktop tray/bung dùng cùng phiên, không nhân đôi history |
| 5 | SC1-08/09 + WD1-01/02 | Chốt kiểm OS còn thiếu; Hóa thông minh Word thủ công; quét tài liệu cũ và fx chuyển riêng/hàng loạt. WD1 không chờ auto-Space |
| 6 | SC1-10 + nghiệm thu UX1/DOC1/WD1 | Build/gói local, kiểm end-to-end một lượt cho phạm vi phát hành; chốt bằng chứng/giới hạn từng host |
| 7 | E1 → E2A → E2B | Biểu đồ/thanh trượt → hình 2D → hình 3D. E2B sau nền 2D; có thể lấy khi nhánh Word đang chờ |
| 8 | WEB2-01/02/03 | Tài khoản, quyền gói, usage ledger, pilot quota và sau đó thanh toán/online khi được quyết định |

Ưu tiên mới là trải nghiệm nhìn thấy được; task đủ đầu vào độc lập có thể tiếp tục khi Windows/Word chờ. Không bắt E1 chờ nhánh auto Word SC1-11. Backlog cũ giữ ID/bằng chứng, không xóa hoặc gọi DONE cho phần hoãn.

## 8. Nhịp kiểm tra từ đợt này

Ngừng lặp bộ test lớn sau mỗi thay đổi nhỏ. Mỗi cụm chức năng có build/smoke ngắn cùng kiểm đúng rủi ro vừa đổi; cuối cụm mới chạy một lượt hồi quy tổng hợp. Nguồn bị ghi đè, Undo, batch Word, metadata và quota trừ tiền/lượt phải được kiểm trước khi áp dụng lên dữ liệu thật; “test sau” không có nghĩa bỏ các guard đó.

Nếu công cụ OS không ổn định hoặc người dùng đang sử dụng cửa sổ thử, ghi rõ bài chưa đạt và tiếp tục việc độc lập; không liên tục chiếm focus hoặc yêu cầu người dùng thử lại. Chỉ rerun sau sửa lỗi mới hoặc có điều kiện môi trường phù hợp. Trạng thái triển khai, smoke, kiểm tổng hợp và nghiệm thu phải được phân biệt trong báo cáo.
