# Nút đũa thần và quyền cân bằng

Hợp đồng hành vi sau trao đổi ngày 2026-09-15. **Đã triển khai trong đợt C cho editor/model chung và Web local**; [báo cáo, build và phạm vi kiểm](../bal1/REPORT.md). Solver/kho SC1 vẫn được dùng lại. Kiểm file/clipboard qua hai host, bộ gõ OS và Word thuộc các đợt tiếp theo. [Kế hoạch](../NEXT-STEPS.md), [sản phẩm](../PRODUCT.md).

## 1. Phạm vi và nguồn được giữ

Ô nhập giữ văn bản người dùng gõ/dán. Ô kết quả hiển thị nguyên nội dung với các vùng công thức; cân bằng tác động vào kết quả của vùng được chọn. Nút này không tự ghi lại câu chữ trong ô nhập. Copy/xuất sử dụng kết quả đang thấy; copy nguồn lấy đúng ô nhập. Khác với baseline SC1 nhận proposal bằng thay source, C lưu kết quả và bản trước cân bằng trong ContentDocument v7.

Mỗi vùng giữ nguồn gốc, cách đọc đã chọn, kết quả đang dùng, lịch sử hỗ trợ và quyết định auto. Người dùng có thể giữ phương trình có cấu trúc hợp lệ nhưng chưa cân bằng hoặc sai về nội dung khoa học; không khóa copy/xuất vì lý do đó. Chuỗi không parse được vẫn là text, không bịa AST/native.

Chọn bằng bôi đen trong ô kết quả hoặc chọn nguyên khối công thức. Chỉ phương trình được chọn trọn mới thuộc lệnh; phần công thức bị chọn dở được bỏ qua và báo cách chọn trọn. Selection ngược được sắp lại theo thứ tự tài liệu. Không có selection thì nút mờ, không tự suy là toàn tài liệu. Click thanh công cụ không làm mất selection đang giữ; nguồn/phiên/selection thay đổi thực sự làm lệnh cũ hết hiệu lực.

## 2. Nút chính khi chọn một phương trình

| Trạng thái | Nhãn/hành động |
| --- | --- |
| Có một bản đọc Hóa rõ, đủ hai vế và nghiệm cân bằng duy nhất làm thay đổi hệ số | **🪄 Cân bằng** sáng; bấm áp dụng đúng kết quả solver |
| Bản hiện tại do Locus cân bằng, còn khớp bản sau đã lưu | **↶ Hủy cân bằng** sáng; bấm khôi phục đúng trạng thái trước cân bằng |
| Chỉ có chất như H2SO4; chưa đủ hai vế; mơ hồ; không có nghiệm; quá giới hạn | Nút mờ, lý do ngắn; nội dung giữ nguyên |
| Người dùng đã tự nhập phương trình cân bằng, Locus chưa đổi gì | “Đã cân bằng”; không có thao tác hủy giả |
| Nội dung đã được người dùng sửa sau cân bằng | Không dùng snapshot cũ hủy đè bản mới; tính khả năng cân bằng từ nội dung mới |

Hủy phục hồi cả hệ số người dùng từng gõ, dù đó là hệ số sai. Không đặt tất cả hệ số về 1, không chạy solver nghịch đảo, không dùng một bản nguồn cũ của cả tài liệu. Một lần bấm là một lệnh Undo; trạng thái UI và lịch sử cùng tham chiếu vùng/kết quả đó. Việc normalize ký hiệu để render thuộc cách đọc; bản text nguồn không bị mất.

Trong UX mới, không bắt buộc hai ô “nguyên trạng/hỗ trợ” luôn nằm cạnh nhau. Kết quả hiện tại và nút toggle là đường chính; fx vẫn có thể xem nguồn, cách đọc và các hệ số thay đổi. Tối đa ba phương án cho một vùng vẫn giữ nguyên; không tạo danh sách ba lựa chọn riêng cho hỗ trợ.

## 3. Nút chính khi chọn nhiều phương trình

**Mỗi lần bấm xử lý một phương trình**, từ trên xuống. Dropdown cung cấp lệnh chạy cả vùng.

1. Đóng băng danh sách ID các vùng chọn trọn và thứ tự đọc. Tính tập có thể cân bằng, tập có thể hủy cân bằng, và lý do bỏ qua. Không giữ offset DOM làm định danh.
2. Nếu có vùng chưa cân bằng đủ điều kiện, bắt đầu lượt **Cân bằng**; nếu không có nhưng có vùng Locus đã cân bằng còn có thể hủy, bắt đầu lượt **Hủy cân bằng**.
3. Tô nhẹ vùng sắp xử lý, ghi “Cân bằng tiếp · 2/3” hoặc “Hủy cân bằng tiếp · 1/3”. Sau mỗi bấm cập nhật đúng một vùng rồi chuyển con trỏ xử lý xuống dưới.
4. Hết lượt cân bằng mới chuyển sang lượt hủy từ trên xuống; hết lượt hủy mới quay lại cân bằng. Chỉ hủy vùng có lịch sử cân bằng Locus còn khớp, bao gồm vùng được Locus cân bằng từ trước trong selection. Phương trình tự cân bằng sẵn của người dùng luôn giữ nguyên.
5. Lượt bắt đầu từ selection có trạng thái trộn không phải thao tác khôi phục toàn trạng thái trộn đó. Muốn về chính xác trạng thái trước một lệnh dùng Undo hoặc “Hoàn tác…” của batch, không dùng việc chạy cả vòng làm thay thế.
6. Đổi selection/nguồn/tài liệu/candidate, dùng dropdown hoặc Undo/Redo ngoài lượt thì tính lại lượt; không bất ngờ tiếp tục ở số cũ. Các thay đổi do chính lượt bấm chỉ cập nhật phiên của lượt, không làm mất selection logic.

Lệnh đang chạy khóa việc bấm lặp cùng nút; có tiến độ khi cần. Kết quả cũ bị hủy nếu nguồn thay đổi. Mỗi bước có thể Undo riêng; không ghi một vùng khác vì độ dài phương trình trước vừa đổi.

## 4. Dropdown và hoàn tác batch

Mũi tên nhỏ hiện khi chọn nhiều phương trình; nhãn menu có phạm vi và số lượng. Hai mục là **Cân bằng tất cả trong vùng chọn** và **Hủy cân bằng tất cả trong vùng chọn**. Ctrl+A/chọn tất cả mới có phạm vi toàn tài liệu; menu không mở rộng phạm vi ngầm.

Mỗi lệnh tạo bản chụp trước/sau của các vùng bị tác động, kể cả trạng thái “giữ nguyên”, nguồn hỗ trợ và quyết định auto. Sau khi hoàn thành, chính mục vừa bấm đổi thành **Hoàn tác cân bằng tất cả** hoặc **Hoàn tác hủy cân bằng tất cả**. Bấm tiếp phục hồi đúng bản chụp trước lệnh, rồi mục trở lại hành động ban đầu.

| Trước lệnh | Thao tác | Kết quả | Bấm lại đúng mục |
| --- | --- | --- | --- |
| A đã được Locus cân bằng; B/C chưa | Cân bằng tất cả | A/B/C cân bằng | A vẫn cân bằng; B/C như trước lệnh |
| A/B được Locus cân bằng; C tự cân bằng từ lúc gõ | Hủy cân bằng tất cả | A/B về trạng thái trước hỗ trợ; C giữ nguyên | A/B trở lại cân bằng; C vẫn giữ nguyên |
| Có vùng người dùng bấm hủy và giữ nguyên khỏi auto | Cân bằng tất cả có chủ đích | Cân bằng vùng đủ điều kiện, gồm vùng từng bỏ auto | Bấm hoàn tác trả cả kết quả và quyết định bỏ auto như trước |

Hai mục không giữ hai bản hoàn tác nhanh độc lập có thể ghi chồng nhau. Chỉ **batch vừa thực hiện, chưa có sửa đổi/lệnh khác và selection vẫn tương ứng** có quyền bấm lại để hoàn tác nhanh. Đổi selection làm mất đường hoàn tác nhanh, không xóa lịch sử Undo. Dùng mục đối lập là lệnh mới trên trạng thái hiện tại; Undo thông thường phục hồi theo thứ tự lệnh.

Batch tính tuần tự từ trên xuống nhưng chưa ghi lẻ trong lúc tính. Trình bày “Đang xét 4/12”; xong mới commit tất cả vùng đủ điều kiện bằng một lệnh. Vô nghiệm/mơ hồ/lỗi cú pháp được giữ và liệt kê số bỏ qua. Bấm Hủy trước commit giữ nguyên toàn bộ. Nếu nguồn/selection/cài đặt liên quan đổi hoặc có lỗi giao dịch, hủy batch thay vì commit một phần. Chỉ đổi nhãn sang Hoàn tác sau khi commit thành công; không tạo lịch sử nếu không có thay đổi.

Trong Word, thứ tự người dùng thấy vẫn từ trên xuống; ghi các Range từ cuối về đầu là chi tiết thực thi để không dịch vị trí. Cân bằng batch và chuyển text thành equation native là hai lệnh khác nhau.

## 5. Checkbox Tự cân bằng

- Mặc định **tắt**, lưu như preference riêng; nâng file/cài đặt cũ không tự bật. Nhận diện Hóa, auto cân bằng và Space nhận ghost là ba tùy chọn khác nhau.
- Bật thì chỉ xét phương trình mới nhập/dán hoặc nội dung phương trình vừa thay đổi sau khi parser/IME đã ổn định. Bật/tắt không sửa lại toàn tài liệu; tắt không hủy các cân bằng đã nhận.
- Khi dán đoạn dài, dựng kết quả theo nguồn trước; chỉ khi auto bật mới áp dụng hệ số cho phương trình đủ điều kiện. Không có kết quả đến muộn sau khi người dùng tắt auto hoặc hủy/sửa đoạn.
- Hủy cân bằng thủ công đánh dấu **giữ nguyên khỏi auto** cho đúng vùng và nội dung hiện tại. Re-render, copy, reload và bật/tắt checkbox không cân bằng lại vùng ấy. Sửa thực sự nội dung phương trình tạo revision mới có thể được xét lại; sửa câu văn bên cạnh không xóa quyết định này.
- Bấm cân bằng có chủ đích trên vùng đã bỏ auto vẫn được phép. Hoàn tác batch phải trả đúng quyết định auto trước lệnh, không chỉ trả hình công thức.
- Auto không nhận repair, không chọn hộ miền mơ hồ, không đổi chất, tự thêm sản phẩm, hoặc suy từ số 0 thành O. Nó chỉ thay hệ số bằng solver đã kiểm.
- Một lần dán cùng auto cân bằng được gom vào một lệnh undoable của lần dán. Undo về trước khi dán; hủy riêng cân bằng giữ nguyên đoạn đã dán. Kết quả tính bất đồng bộ không được ghép ngược vào Undo của lần dán sau khi người dùng đã chỉnh tiếp; khi đó hủy tác vụ auto cũ và xét revision mới.

## 6. Quan hệ với ghost suy sản phẩm

Ghost SC1 vẫn là đề xuất sản phẩm có điều kiện, không được tự nhận chỉ vì bật auto cân bằng. Khi người dùng nhận ghost, preview toàn phản ứng được dùng và lưu provenance như hiện tại.

Model mới tách **bổ sung sản phẩm** và **thay hệ số** thành các phần có thể giải thích/khôi phục. “Hủy cân bằng” của kết quả có sản phẩm suy ra giữ lại sản phẩm đã nhận, trả hệ số về bản trước bước solver; “Bỏ sản phẩm đã nhận / Về nguồn trước hỗ trợ” mới bỏ cả sản phẩm. Hai lựa chọn phải có preview rõ và không chạy suy lại khi mở file. Khi nhận ghost, các phần vẫn được commit như một lần Undo theo SC1. File cũ chỉ có snapshot gộp thì giữ lệnh khôi phục gộp cũ, không bịa một trạng thái trung gian chưa được lưu.

Ví dụ đã kiểm: nguồn `hoa-[3H2+O2=]` → nhận sản phẩm/cân bằng thành `2H2+O2→2H2O` trong kết quả → hủy cân bằng còn `3H2+O2→H2O` → bỏ sản phẩm mới trở về draft. Nguồn gốc giữ nguyên xuyên suốt.

## 7. Tình huống chấp nhận của tương tác mới

Các hàng là hợp đồng chấp nhận. C có 20 nhóm kiểm model/lệnh và một lượt UI Web; xem [đối chiếu bằng chứng](../bal1/REPORT.md). BAL-UX-12 mới đạt codec/session và Web, phần chuyển file/clipboard qua hai host thực tiếp tục ở DOC1-03; không lấy publish Desktop để đóng phần này.

| ID | Bài demo | Điều kiện đạt |
| --- | --- | --- |
| BAL-UX-01 | Chọn H2SO4, Toán, phương trình đủ hai vế, công thức chọn dở | Enable/disable và lý do đúng, không mở rộng selection |
| BAL-UX-02 | Hệ số người dùng nhập sai → cân bằng → hủy | Trả đúng hệ số đầu, raw gốc và vùng ngoài không đổi |
| BAL-UX-03 | Ba phương trình chọn ngược | Mỗi bấm một vùng từ trên xuống, xong mới đổi chiều thao tác |
| BAL-UX-04 | Locus cân bằng trước + chưa cân bằng + tự cân bằng sẵn | Các trạng thái trộn xử lý đúng, vùng tự cân bằng không có hủy giả |
| BAL-UX-05 | Cân bằng tất cả rồi bấm lại | Trả đúng trạng thái trộn trước lệnh, một Undo/Redo |
| BAL-UX-06 | Hủy tất cả rồi bấm lại | Phục hồi đúng bản cân bằng trước lệnh, không tự cân bằng vùng chưa từng được thay |
| BAL-UX-07 | Batch → sửa nội dung/đổi vùng/dùng lệnh đối lập | Snapshot nhanh cũ không ghi đè nội dung mới; lịch sử tuần tự đúng |
| BAL-UX-08 | Đang tính batch → hủy, nguồn đổi, solver lỗi/limit | Không ghi một phần ngoài kết quả/báo bỏ qua đã xác định |
| BAL-UX-09 | Dán đoạn với auto tắt/bật, tắt giữa lúc chờ | Chỉ lần bật hợp lệ có thay hệ số; giữ toàn đoạn, undo lần dán đúng |
| BAL-UX-10 | Hủy khi auto bật → render/reload/sửa câu bên cạnh | Giữ quyết định người dùng, không cân bằng lại |
| BAL-UX-11 | Nhận sản phẩm → hủy cân bằng → bỏ sản phẩm | Hai phạm vi khôi phục rõ, metadata/snapshot đúng, file cũ không suy lại |
| BAL-UX-12 | Web ↔ Desktop, lưu/mở, chọn-copy PNG/SVG/cả đoạn | Preview và dữ liệu xuất dùng đúng kết quả, raw/đánh dấu auto/history được giữ |
