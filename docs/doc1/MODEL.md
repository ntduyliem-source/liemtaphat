# DOC1 — Tài liệu gồm văn bản và công thức

Ngày 2026-09-15. Hợp đồng đang triển khai ở [ContentDocument](../../src/Locus.Application/ContentDocument.cs), [ContentHistory](../../src/Locus.Application/ContentHistory.cs), [session](../../src/Locus.Application/FormulaSession.Content.cs) và [codec](../../src/Locus.Application/WorkspaceDocument.cs).

## Nguồn và kết quả

`ContentDocument.Raw` giữ đúng chuỗi nhận từ ô nhập, cùng revision nguồn. `Blocks()` phân hoạch toàn bộ chuỗi thành đoạn text và vùng công thức, không chồng lấn hoặc bỏ khoảng trống. Trình duyệt có thể chuẩn hóa CRLF thành LF khi đưa văn bản vào textarea; core giữ nguyên chuỗi nó nhận, không tự đổi Unicode hay xuống dòng.

Mỗi `ContentRegion` chứa:

| Dữ liệu | Vai trò |
| --- | --- |
| Id, Revision | Danh tính và revision của vùng; công thức giống chữ ở vị trí khác vẫn có ID khác |
| Start, End | Khoảng nửa mở `[start,end)` trong Raw, theo UTF-16 |
| Origin, Window | Cửa sổ nguồn tối đa 4096 UTF-16; tọa độ trong snapshot cộng Origin thành tọa độ tài liệu |
| Readings, SelectedId | CandidateSet nguyên vẹn từ core và cách đọc đã chọn |
| KeepText | Giữ vùng dưới dạng text; vẫn có fx và snapshot |
| KeepFromAuto | Quyết định bỏ qua auto theo vùng/nội dung sau khi hủy cân bằng; sửa câu bên cạnh giữ quyết định |
| Problem | Giải thích vùng chưa có cách đọc dùng được |
| ResultOverride | Kết quả riêng dựa trên SelectedId, CandidateSet đã chốt, candidate và provenance; không thay Raw/Readings |

Kết quả hiển thị lấy từ `Display`: KeepText → text; có override hợp lệ → candidate của override; còn lại → candidate đã chọn. Render, copy và export lease dùng chính candidate này. Snapshot của override có chuỗi/các tọa độ riêng, không giả nó là chuỗi gốc người dùng gõ.

Lựa chọn ban đầu chỉ dựng nếu có đúng một cách đọc hợp lệ loại direct và không có lỗi. Repair không được tự chọn. Nhiều cách đọc hợp lệ, repair-only hoặc marker chưa đọc được giữ text và fx. Candidate vẫn có giới hạn từ core; không sinh thêm kết quả ở UI.

## Phân tích và nhận diện lại

Một ô nhập dùng cho cả công thức đơn và đoạn. Đầu vào đơn có kết quả bao trọn chuỗi dùng đường explicit cũ. Các đoạn khác được quét marker trước, giữ vùng marker nguyên khối rồi phân tích các phần còn lại bằng nhận diện trong câu. Nhiều dòng được gom vào cửa sổ nhỏ, ưu tiên ranh giới xuống dòng. Dòng hoặc marker quá dài được giữ text, không cắt một token hay ngoặc để cứu riêng phần bên trong.

Quyết định giữ text/cách đọc/override chỉ đi theo vùng còn nguyên trong tiền tố hoặc hậu tố không đổi. Sửa riêng công thức làm nó nhận ID mới và không thừa hưởng quyết định cũ. Khi không xác định chắc danh tính, không ghép vùng bằng so sánh chữ giống nhau. Sửa nhiều điểm cùng lúc có thể làm quyết định của vùng nằm trong phần giữa mất theo cách bảo thủ; Undo vẫn giữ snapshot của phiên trước.

Worker trả sai cửa sổ/revision, token bị hủy hoặc phiên đã đổi không được commit. Nội dung nhận diện commit thành một snapshot hoàn chỉnh; hủy không để nửa kết quả ghi vào tài liệu.

Ngân sách hiện tại: 100.000 UTF-16 cho phân tích tài liệu, 4096 cho một cửa sổ, 256 vùng. Đây là trần bảo vệ, chưa phải cam kết thời gian phản hồi. Vượt trần phân tích vẫn giữ raw, báo lý do, cho copy/tải nguồn. Model/file nhận tối đa 1.048.576 UTF-16 và envelope tối đa 8 MB; lịch sử phức tạp có thể chạm trần file trước trần raw. UI giữ nguồn nếu lưu chưa được.

## Chọn trong kết quả

Selection logic gồm Start/End đã chuẩn hóa thứ tự, tập ID chọn trọn và chọn dở. Hai đầu được map bằng các span nguồn trên DOM. Text thường dùng offset ký tự của text; công thức render dùng ID/span và kiểm hai đầu DOM, không suy từ `textContent` MathML hoặc chiều rộng ảnh.

- Click công thức/fx chọn trọn; bôi ngược vẫn cho thứ tự theo tài liệu.
- Ctrl+A khi focus kết quả chỉ chọn kết quả. Toolbar giữ selection logic khi nhận focus.
- Chọn dở công thức render: không bật xuất ảnh/copy kết quả; có nút **Chọn trọn công thức** để mở rộng có chủ đích.
- Chọn dở vùng đang là text: offset text được giữ chính xác; có thể copy đúng đoạn text đó.
- PNG/SVG chỉ bật khi phạm vi đúng một công thức đầy đủ và renderer của candidate đó đã sẵn sàng.
- Copy đoạn hiện là text với công thức LaTeX. Rich clipboard/DOCX thuộc DOC1-03.

## Lịch sử và phiên bản file

Session Undo/Redo lưu snapshot bất biến tối đa 100 bước trong phiên. `ContentCommand` lưu riêng ID/loại lệnh và từng vùng trước/sau, tối đa 100 lệnh; lệnh phải giữ nguyên ID/revision/anchor/Readings của vùng. B ghi lệnh đổi cách đọc/giữ text; C thêm balance/cancel/batch/auto/product/drop-product/undo-batch. Undo/Redo cũng trả lại danh sách lệnh tương ứng.

Lịch sử vùng là dữ liệu phục hồi có nguồn đối chiếu, không phải quyền áp dụng lại vào vùng hiện tại. Lệnh BAL1 kiểm session/version/selection, nguồn/cấu hình và snapshot hiện tại trước commit; thay nguồn hoặc hủy khi đang tính không ghi một phần. Snapshot được mở lại đủ để hủy riêng cân bằng hoặc bỏ sản phẩm; file chưa phát lại toàn bộ stack Undo của phiên trước. Hoàn tác nhanh của menu chỉ ở phiên/selection còn khớp.

| Version | Dữ liệu |
| --- | --- |
| 1…4 | Đọc theo hợp đồng cũ, gồm lịch sử hỗ trợ SC1 ở v4; không chạy lại parser/kho với snapshot đã lưu |
| 5 | Tài liệu nhiều vùng, ID/span/window, readings, selected, giữ text/auto; đọc được các nháp B đầu |
| 6 | Thêm result override và command history trước/sau theo vùng |
| 7 | ManagedBalance, BalanceBefore, ProductProposal trong override và command; phân biệt hệ số trước solver với sản phẩm đã nhận |

File không có dữ liệu mới vẫn dùng version tối thiểu cần thiết. Envelope có checksum; nguồn/cửa sổ, span, candidate và cấu trúc lịch sử được kiểm khi đọc. Hạ version nhưng giữ dữ liệu cần version mới bị từ chối. Tệp không hỗ trợ được giữ nguyên bytes để tải lại; không tự downgrade mất lựa chọn.

File cũ có Analysis được nâng thành ContentDocument từ chính snapshot, giữ candidate đã chọn kể cả repair; không gọi scheduler. Lịch sử SC1 đã nhận kiểu cũ giữ đường khôi phục gộp cũ, không bịa snapshot hệ số chưa từng được lưu.

## Lệnh BAL1 và dữ liệu v7

`ManagedBalance` đánh dấu kết quả đã được Locus thay hệ số. `BalanceBefore` là override ngay trước bước đó; null nghĩa là bản đọc nguồn đã chọn. Chỉ có một tầng trước cân bằng, không lồng lịch sử managed vô hạn. `ProductProposal` lưu proposal/kho/điều kiện đã nhận; nhận sản phẩm tạo bản trung gian giữ hệ số vế trái người dùng rồi mới áp dụng kết quả solver. Hủy cân bằng phục hồi bản trung gian; bỏ sản phẩm trả cách đọc nguồn, kể cả draft chưa có AST.

Codec kiểm cùng các chất và thứ tự/chiều phản ứng giữa trước/sau, bảo toàn nguyên tố/điện tích của bản managed, không gọi solver/kho để đoán lại. Chuyển nguồn/anchor còn nguyên phải reanchor cả override và BalanceBefore. File có dữ liệu v7 bị hạ thành v6 sẽ bị từ chối.

Auto chỉ áp dụng ticket của lần nhập mới và các ID vừa tạo sau phân tích. Hủy cân bằng gắn KeepFromAuto; manual balance có thể bỏ dấu này; quick undo trả lại cả dấu. Bật preference không xét lại tài liệu, Load/Undo/Redo/ẩn view hủy ticket còn chờ. Dán và kết quả auto dùng cùng một Undo. [Hành vi](../ux1/BALANCE-INTERACTION.md), [kiểm chứng C](../bal1/REPORT.md).
