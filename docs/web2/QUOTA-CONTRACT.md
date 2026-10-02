# WEB2-01 — Hợp đồng lượt dùng Web (pilot)

Cập nhật 2026-09-24. Đây là quyết định sản phẩm và hợp đồng để triển khai WEB2-02/03, **chưa phải tính năng đang chạy**. Bản Web static local và Desktop hiện tại vẫn dùng không cần tài khoản và không bị hạn mức. Dịch vụ Web có tài khoản là một host riêng; không thay lõi Toán/Lý/Hóa, PlotDocument hoặc GeometryDocument.

## 1. Phạm vi pilot

| Gói | Công thức/ngày | Đồ thị/ngày | Hình 2D/ngày | Hình 3D/ngày |
| --- | ---: | ---: | ---: | ---: |
| Guest | 5 | 2 | 2 | 2 |
| Free | 21 | 10 | 10 | 10 |
| Premium | 150 | 100 | 100 | 100 |

Đây là bốn ngân sách độc lập, không cộng dồn ngày trước và không chuyển lượt giữa loại. Ngày quota bắt đầu lúc **00:00 Asia/Ho_Chi_Minh**; UI luôn hiển thị thời điểm reset theo múi giờ của thiết bị để người dùng không phải tự đổi giờ. Server là nguồn duy nhất quyết định thời gian, gói và số lượt còn lại. Nâng gói trong ngày đổi trần ngày đó và giữ số đã dùng, không xóa lịch sử.

Trong pilot, Premium là quyền được cấp thủ công để thử; không có giá, thanh toán hay cam kết thương mại. Nhà cung cấp đăng nhập, thanh toán và hosting cho website Internet được chọn khi chuẩn bị phát hành online ở WEB2-04. WEB2-02 có thể dùng danh tính thử nghiệm cục bộ để phát triển API, nhưng không được coi đó là cơ chế đăng nhập sản xuất.

## 2. Một lượt là gì

Quota tính khi **lần đầu sử dụng một thành phẩm mới** từ Web có tài khoản: sao chép hoặc tải kết quả đã chuyển đổi; xuất DOCX/HTML có công thức; xuất SVG/PNG của đồ thị hay hình. Xem preview, gõ/sửa nguồn, đổi candidate, kéo điểm/thanh trượt, Undo/Redo, lưu/mở nháp `.locus`, sao chép nguyên văn nguồn, thử hỗ trợ Hóa và lặp lại xuất cùng thành phẩm không tính thêm. Việc lưu `.locus` và lấy lại dữ liệu luôn khả dụng khi hết lượt.

| Loại | Đơn vị tính | Sau khi đã dùng một lượt |
| --- | --- | --- |
| Công thức | Một vùng công thức được chuyển thành kết quả dùng được; đoạn có N vùng mới cần N lượt | Sửa nguồn/cách hiểu/cân bằng trong cùng vùng, rồi xuất lại, không tính thêm |
| Đồ thị | Một tài liệu đồ thị (canvas chứa các đường và tham số) | Thêm/sửa đường, kéo slider, đổi style/camera và xuất lại cùng tài liệu không tính thêm |
| Hình 2D | Một tài liệu hình 2D | Kéo/dựng/sửa và xuất lại cùng tài liệu không tính thêm |
| Hình 3D | Một tài liệu hình 3D | Kéo đỉnh/đổi góc nhìn/sửa và xuất lại cùng tài liệu không tính thêm |

`creationId` là định danh quota riêng, ổn định qua lưu/mở/sửa của thành phẩm. Nó không được suy từ text, vị trí vùng, revision, hash ngắn hoặc ID scene hiện tại nếu ID đó có thể đổi khi phân tích lại. Tạo vùng/scene mới hoặc **Nhân bản thành bản mới** sinh `creationId` mới. Xóa rồi tạo lại là thành phẩm mới. Receipt đã dùng gắn với **chủ thể tài khoản/Guest**, không phải giấy phép chuyển được cho người khác qua tệp `.locus`. Nếu mở tệp từ người khác, lần xuất đầu tiên của từng thành phẩm ở chủ thể mới cần lượt của họ. Giữ `creationId` và receipt tham chiếu riêng khỏi nội dung tài liệu để không phá file cũ.

Quy tắc này ưu tiên việc sửa đi sửa lại mà không bị trừ lượt. Người dùng có thể tái dùng một canvas để làm nhiều biến thể; pilot chấp nhận tính chất quota mềm đó. Giới hạn tài nguyên của core vẫn độc lập với quota.

## 3. Đoạn dài và lúc hết lượt

Trước thao tác dùng kết quả, UI đếm **các vùng mới đủ điều kiện** trong đúng selection và thông báo `Cần N lượt · Còn M lượt`. Vùng là text, bị bỏ qua, mơ hồ chưa chọn, chỉ là đề nghị sửa chưa được chọn, hoặc đã có receipt của chủ thể hiện tại không cần lượt. Không tính công thức chỉ mới preview mà người dùng không xuất.

Nếu `N > M`, không âm thầm trừ một phần hay cắt đoạn. Người dùng chọn các vùng sẽ chuyển; phần còn lại giữ nguyên văn trong output, đúng thứ tự và xuống dòng. Có lựa chọn lưu `.locus` hoặc sao chép nguồn ngay, không mất đoạn đã nhập. Khi đủ lượt, một thao tác xuất cả đoạn giữ tính nguyên tử: nhận quyền cho toàn bộ N vùng hoặc không đổi ngân sách. Candidate/nguồn/selection phải còn đúng lúc xuất; nếu stale thì hủy trước khi trừ.

Guest hết lượt thấy lời mời tạo tài khoản; nháp, thành phẩm đã dùng, sửa và khôi phục dữ liệu vẫn truy cập được. Tạo tài khoản trong cùng trình duyệt chuyển số lượt Guest đã dùng trong ngày sang Free; ví dụ Guest đã dùng 5 công thức thì Free còn 16. Nếu tài khoản đã dùng trên thiết bị khác, hai số cộng vào cùng ngày; nếu vượt trần hiện tại, giữ kết quả cũ và chỉ chặn thành phẩm mới. Nâng Premium từ Free cũng giữ số đã dùng.

## 4. Quyền riêng tư và giới hạn kiểm soát

Lõi phân tích/dựng tiếp tục chạy cục bộ trong trình duyệt. API quota chỉ nhận chủ thể phiên, loại, `creationId` ngẫu nhiên, số đơn vị, khóa thao tác idempotent và thời gian; **không gửi raw công thức, tài liệu, AST, ảnh, SVG hoặc hash của nguồn ngắn** chỉ để đếm lượt. Guest được cấp ID bằng cookie phiên từ server; xóa cookie/đổi trình duyệt có thể nhận allowance mới. Tài khoản mới cho quota xuyên thiết bị.

Vì mã và tính toán chạy ở client, quota không thể cưỡng chế tuyệt đối trước người sửa client hoặc dùng bản local/portable. WEB2 chỉ giới hạn dịch vụ Web do Locus vận hành, không hứa khóa lõi offline và không gọi cơ chế cookie là chống lạm dụng mạnh. Không thay đổi quyền dùng Web local/Desktop hiện tại.

## 5. Hợp đồng ledger cho WEB2-02/03

1. Client gửi yêu cầu `prepare` với một `operationId` ngẫu nhiên ổn định cho retry, chủ thể, loại, danh sách `creationId` chưa có receipt và phiên bản tài liệu/selection. Server xác thực chủ thể, gói, ngày quota và cấp reservation nguyên tử nếu đủ lượt. Cùng `operationId` trả cùng kết quả khi nhiều tab/retry; khác `operationId` nhưng cùng thành phẩm đã được tính không trừ lại.
2. Client kiểm lại nguồn/selection rồi thực hiện clipboard/download. Chỉ sau thành công mới `commit` reservation; lỗi, người dùng hủy file picker hoặc snapshot stale thì `cancel`. Reservation chưa commit tự hết hạn và trả lượt. Một phần batch lỗi phải rollback cả batch hoặc xuất đúng tập đã được cấp quyền, không dùng tập khác.
3. Nếu mạng mất sau clipboard/download thành công nhưng trước `commit`, client lưu `operationId` để đối soát khi trở lại. Server dùng trạng thái reservation/receipt chống trừ đôi; pilot chấp nhận trường hợp tác vụ cục bộ thành công nhưng reservation cuối cùng hết hạn mà không ghi lượt. Không giữ tài liệu làm con tin khi mạng lỗi.
4. Quota và receipt lưu bền ở server với ràng buộc duy nhất `(subject, category, creationId)` và `(subject, operationId)`; cấp quyền nhiều tab phải dùng giao dịch nguyên tử. UI chỉ hiển thị trạng thái từ server, không dùng counter local làm sự thật.
5. Web local không gọi API quota. Host có tài khoản nếu mất dịch vụ vẫn cho xem/sửa/nháp/lưu `.locus` và xuất nguyên văn nguồn hoặc những thành phẩm đã có receipt cục bộ đáng tin; thành phẩm mới cần kết nối để chuẩn bị lượt. Trường hợp receipt không xác minh được thì giữ nội dung, báo rõ lý do và cho thử lại.

WEB2-02 cần quyết định cách giữ `creationId` khi `ContentRegion` được phân tích lại; không được dựa vào span hoặc `ContentRegion.Id` mà không chứng minh tính ổn định. WEB2-03 hiện thực ledger và UI theo hợp đồng trên. WEB2-04 đo mức dùng thực tế, cân nhắc lại các trần, đăng nhập/hosting/giá/thanh toán; không tự bật quota trên gói local.

## 6. Ca nghiệm thu bắt buộc

| Ca | Kết quả cần đạt |
| --- | --- |
| Gõ/sửa `x mũ 2`, đổi candidate, copy hai lần | Lần copy đầu tính 1; mọi sửa và copy lại cùng vùng giữ 1 |
| Dán 30 vùng, Free còn 21 | Báo cần 30/còn 21; không chuyển dở; có thể chọn 21 vùng, 9 vùng còn text |
| Một đoạn có 4 vùng, 2 đã dùng, 1 bỏ qua, 1 mới | Chỉ cần 1 lượt; text và xuống dòng ngoài vùng giữ nguyên |
| Lưu/mở `.locus`, Undo/Redo, xuất lại | Không trừ thêm nếu cùng chủ thể và `creationId` |
| Guest dùng 5 rồi tạo Free, Free sau đó nâng Premium | Còn lần lượt 0, 16, 145 công thức trong cùng ngày |
| Hai tab cùng xuất một thành phẩm; retry cùng `operationId` | Một receipt, một lượt |
| File picker hủy, clipboard lỗi, snapshot stale, mạng đứt trước prepare | Không mất lượt; nguồn/nháp vẫn truy cập được |
| Đồ thị 3 đường với 20 slider, hình 2D/3D chỉnh nhiều lần | Mỗi tài liệu chỉ tính một lượt ở lần xuất đầu, không tính từng đường/điểm/slider |
| Mở tệp từ tài khoản khác, hoặc tạo bản sao mới | Lần xuất của chủ thể/bản mới cần lượt mới |
| Qua 00:00 giờ Việt Nam | Ngày mới có trần mới; receipt cũ vẫn mở/xuất lại được |

WEB2-01 chỉ đóng hợp đồng sản phẩm. Chưa có account, ledger, quota UI hoặc website Internet ở build G.
