# Đợt D — Xuất nguyên đoạn và Desktop

> Cập nhật 30/09: xem [biên bản mới](../host-review/20260930.md). Copy Desktop → Microsoft Word 10/10, file native, pointer/IME cơ bản và tray đã có bằng chứng. Receipt 6/6 cũ dùng COM có thể trỏ WPS, không dùng làm bằng chứng Microsoft Word. Bảng bên dưới là lịch sử trước lượt nghiệm thu mới.


Bổ sung 2026-09-28: trên build G `20260928-055505-677`, Web copy toàn đoạn/một công thức đã đọc đúng trong clipboard IAB; adapter Windows → Word đạt 6/6 text/PNG/SVG/hủy/stale và save/reopen. Hộp Save As thật hủy bằng Escape đạt; lưu thành công chưa kiểm được. D-CLIP-01 chưa đóng toàn bộ vì thiếu bấm Copy trên Desktop; D-HOST/D-FILE/IME vẫn mở do công cụ native. [Báo cáo và receipt mới](../host-review/20260928.md). Nội dung dưới đây giữ lịch sử nghiệm thu của build D.

Cập nhật 2026-09-16. **D-WORD-01 đã đạt; đợt D còn nghiệm thu host.** Build D `20260915-181724-011`; [bản E kế tiếp](../phase-e/REPORT.md) giữ chức năng này. DOC1-03 và UX1-03 còn phần kiểm host bên dưới; SC1-08 chưa được nâng trạng thái.

## Đầu ra đã có

- Menu **Tải → DOCX với công thức Word / HTML với công thức**. Xuất toàn đoạn hoặc vùng đang chọn từ đúng kết quả hiện tại, giữ câu chữ, xuống dòng, tab và vùng giữ text. DOCX dùng OMML; HTML dùng MathML. Không chạy lại parser/solver khi xuất. Công thức chọn dở bị từ chối; sửa nguồn/selection trong lúc chuẩn bị thì hủy kết quả cũ.
- `.locus` v7 tiếp tục giữ nguồn, kết quả, hệ số trước cân bằng, sản phẩm đã nhận và quyết định bỏ auto. DOCX/HTML là bản xuất nội dung; muốn mở lại đầy đủ trạng thái Locus thì dùng `.locus`. File không phục hồi toàn bộ stack Undo của phiên hoặc quick undo batch, như baseline C.
- Desktop có nút **Bung/Thu**, **Ẩn về khay hệ thống**, **Thoát** và menu tray. Một Window/WebView/session được giữ qua bung/thu/ẩn. X và Minimize đi về tray; mở lại bằng tray hoặc chạy lại ứng dụng cùng profile. Không tự bật Windows startup hay mở Word.
- Desktop lưu qua hộp chọn tệp native. Trước ẩn/thoát, đợi hàng đợi nhập, kiểm ghép dấu và lưu nháp; kiểm lại nội dung sau khi chờ. Thoát bị giữ nếu nháp chưa lưu được; khi tắt tự lưu phải xác nhận bỏ thay đổi chưa lưu. Ẩn vẫn giữ dữ liệu trong phiên. Đã sửa việc phục hồi ảnh preview khi mở lại/hủy Thoát và bỏ yêu cầu ẩn cũ khi có yêu cầu mở mới. Phép so sánh ô nhập tính đến CR/CRLF hiển thị thành LF, không sửa raw trong file.

## Bằng chứng đã kiểm

| Phần | Kết quả |
|---|---|
| Build Release | Web, Worker, Desktop thành công; 185 tài sản Web, [log](../../artifacts/doc1/build-final.log) |
| Xuất tài liệu | **7/7 nhóm**: ZIP/XML, OMML/MathML, phạm vi, nguyên trạng/hỗ trợ, file v7, Unicode, dữ liệu không hợp lệ, hủy; [receipt](../../artifacts/doc1/verification/document-export-tests.json) |
| Regression liên quan | **20/20 BAL1 + 20/20 content + 35/35 Application**, nhóm Application bao gồm 238 contracts core; [receipts](../../artifacts/doc1/regression/). DLL Core/Application trong bản cuối trùng SHA-256 với bản đã kiểm; sửa cuối chỉ thuộc đường đóng/mở Desktop, vẫn giữ nghiệm thu OS còn thiếu |
| Web thật trong in-app browser | Kiểm các lệnh trên build `180121-517`; smoke bản cuối `181724-011` đã mở đúng fixture và 5 MathML, marker đúng build. Đã mở hai fixture có text/Toán/Lý/Hóa, hệ số đã cân bằng và sản phẩm đã nhận; cập nhật giữ nguồn; chọn một vùng; DOCX/HTML/Lưu chạy tới thông báo hoàn tất, không có console error được ghi ở lượt kiểm lệnh. Sửa cuối chỉ là guard đóng Desktop; [quan sát theo build](../../artifacts/doc1/verification/browser-checks.json) |
| Word | **PASS D-WORD-01**: cả mẫu hỗn hợp **5 OMath** và mẫu sản phẩm **1 OMath** qua mở/lưu/đóng/mở lại, text và native signature không đổi; PDF tạo được. [Vòng cuối](../../artifacts/doc1/verification/word-final-20260915-232937/report.json), [hình mẫu sản phẩm](../../artifacts/doc1/verification/word-final-20260915-232937/products-ignore.png). Đã xem hình, đúng hệ số 3 trước cân bằng |
| Desktop thật | Build cuối đã được khởi động. Nhãn các nút và mở lần hai đã kiểm ở các build D trước: cùng profile giữ đúng tiến trình đầu, không tạo phiên thứ hai; [receipt có build](../../artifacts/doc1/verification/desktop-instance.json). Lệnh kiểm lại lần hai trên build cuối bị automatic approval review từ chối |
| Đo sơ bộ | 100 công thức, 4.799 đơn vị UTF-16: đóng gói DOCX khoảng **3,45 ms**, 3.115 byte trong một lần đo .NET Release; không bao gồm parse/Word/UI và không phải cam kết độ trễ; [số đo](../../artifacts/doc1/verification/export-timing.json) |

Đã xem giao diện Web cuối bằng screenshot: nguồn và kết quả đặt cạnh nhau, công thức inline không đè chữ, thanh xuất và checkbox đọc được. Chưa có screenshot hợp lệ của cửa sổ Desktop gọn/bung.

## Chưa đạt và cách tiếp tục

| ID | Bài còn thiếu | Trở ngại / điều kiện tiếp tục |
|---|---|---|
| D-HOST-01 | Mở `.locus` Web → Desktop, sửa/hủy/bỏ sản phẩm → lưu native → mở lại Web, so raw/result/ignore; hủy Save picker không đổi phiên | Công cụ Windows click báo `coordinate input geometry is unavailable`; Activate, Alt+I và Tab không đưa focus khỏi cửa sổ gốc. Dừng thử lặp; cần phiên điều khiển native hoạt động hoặc kết quả người thử |
| D-HOST-02 | Bung/thu/ẩn/tray/X/Thoát thực tế; source, selection, Undo/Redo và PNG/SVG còn đúng, kể cả sau hủy Thoát; tệp CRLF ẩn được mà giữ raw; IME đang ghép dấu không bị cắt | Code đã có và đã kiểm mở ứng dụng lần hai, chưa thay thế được bài thao tác thật. Chụp native cũng lỗi `SetIsBorderRequired ... 0x80004002` |
| D-CLIP-01 | Copy toàn đoạn/một phương trình rồi dán ra đúng phạm vi | Trong IAB, giao diện báo copy thành công nhưng `clipboard.readText()` và Ctrl+V trả một mẫu cũ của đợt C. Đã Undo về nguồn mẫu. Chưa kết luận lỗi thuộc app hay clipboard của host; phải đối chiếu và sửa nếu do app, không coi là PASS |
| D-FILE-01 | Nhận tệp tải thực tế từ UI, so nội dung DOCX/HTML/`.locus` | IAB hiển thị hoàn tất nhưng sự kiện download không được nhận và không tìm thấy tệp tương ứng trong Downloads. Bộ đóng gói đã đạt trên .NET; chưa coi thông báo UI là bằng chứng tải/roundtrip trọn vẹn |
| D-WORD-01 — DONE | Hai mẫu DOCX mở, lưu, đóng, mở lại; số equation/text giữ; xem render mẫu sản phẩm | Bộ kiểm C# x86/STA cuối dùng tài liệu neo có nội dung; neo trống trước đó bị Word tự đóng và làm COM mất kết nối lúc cleanup. [Bằng chứng cuối](../../artifacts/doc1/verification/word-final-20260915-232937/) đạt cả roundtrip, PDF và cleanup |
| SC1-08 | Telex/VNI thật, Enter/Space/Esc/Tab, focus/nhập nối/selection/key-repeat/trợ năng trên UI mới | Giữ bài và bằng chứng cũ; các bài native chưa đạt không được thay bằng test model |

[Cách chạy và danh sách thử](QUICKSTART.md). Các thử Windows dùng profile riêng dưới `artifacts/doc1`; lần dừng build cũ để thay build là thao tác phát triển, **không** được tính là bài Thoát UI đạt. Word probe chỉ tác động các tài liệu thử do lượt này tạo, không cài hay sửa connector.

Automatic approval review từ chối lệnh kết hợp kiểm HTTP server và chạy Desktop lần hai trên build cuối với thông báo `blocked by policy`, không nêu lý do cụ thể. Lệnh đó không được thực thi; không dùng nó làm bằng chứng nghiệm thu và không chạy lại qua cơ chế khác.

Đã xác nhận bản Web cuối qua giao diện IAB trong tab mới bằng nút cập nhật của app và mở fixture. Hai lần điều hướng tab cũ trả `ERR_ABORTED`; tab mới hoạt động và có đúng build `181724-011`. Điều này không thay cho lệnh kiểm native lần hai bị từ chối.

## Bước tiếp theo

Khép D-HOST/D-CLIP/D-FILE khi host kiểm được. Lượt bổ sung vẫn gặp lỗi click geometry và UIA cache; IAB copy báo hoàn tất nhưng clipboard đọc rỗng, nên chưa xác nhận đường copy/download thật. D-WORD-01 đã đóng theo bằng chứng trên. [Đợt E](../phase-e/REPORT.md) đã có Word thủ công SC1-09 và gói review; SC1-10 vẫn chờ các bài host. W0 giữ 4/6, SC1 đạt 8/10 theo task. Sau đó WD1, E1 và các nhánh còn lại theo [NEXT-STEPS](../NEXT-STEPS.md).
