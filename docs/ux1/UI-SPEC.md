# UX1 — Quy tắc giao diện A/v1

Đặc tả để triển khai sau review [đợt A](DESIGN-REVIEW.md). Trường hợp chi tiết về lịch sử, selection, batch và auto theo [BALANCE-INTERACTION](BALANCE-INTERACTION.md). Không thay các hợp đồng core/Word bằng hành vi đơn giản hóa trong wireframe.

## 1. Thứ tự vùng và phạm vi lệnh

1. **Tên ứng dụng:** Locus; Desktop có Bung/Thu và Ẩn về khay hệ thống. Menu tray có Mở Locus, Thoát Locus. Web không có các nút cửa sổ này.
2. **Tab:** Công thức, Biểu đồ, Hình học; Hình học có 2D/3D. Bản thiết kế cho thấy vị trí tab tương lai. Sản phẩm chỉ cho sử dụng tab đã có khả năng thật; không phát hành canvas trống giả editor.
3. **Nội dung:** label rõ, placeholder “Gõ công thức hoặc dán cả đoạn”. Không dùng placeholder thay label. Cặp chung và cặp theo môn giữ theo cấu hình; không bắt nhập cặp Hóa khi đã nhận diện rõ.
4. **Kết quả:** nguyên đoạn với công thức inline. Chỉ dấu nhận diện/fx nhỏ, không tô mọi công thức như lỗi. Một vùng có quyết định Giữ text vẫn có đường mở lại fx.
5. **Chọn và cân bằng:** dòng phạm vi, vùng kế tiếp, nút chính + mũi tên nếu nhiều phương trình; Undo. Lý do nút mờ nằm trong một dòng trạng thái có thể xuống dòng.
6. **Copy/xuất:** dòng phạm vi, Copy, PNG, SVG, Tải. Tùy chọn Copy nguồn luôn có tên riêng.
7. **Options:** checkbox Toán/Lý/Hóa; Tự cân bằng riêng; Tùy chọn mở cặp bọc, Space nhận ghost, các preference/đường lưu hiện có.

Nguồn và kết quả là hai vai trò. Sửa hệ số ở kết quả không thay ô nguồn; chọn candidate/repair là quyết định rõ trong fx. Khi cần chỉnh công thức, fx đưa về phần nguồn tương ứng rồi cập nhật đúng vùng, không ghi đè toàn paragraph bằng một công thức.

## 2. Bảng chọn vùng và trạng thái nút

| ID | Điều kiện | Nút chính | Phạm vi và thông báo |
| --- | --- | --- | --- |
| S00 | Ô nhập rỗng | Cân bằng · mờ | Kết quả sẽ xuất hiện tại đây; copy/xuất mờ |
| S01 | Có nội dung, chưa chọn | Cân bằng · mờ | Chọn phương trình trong ô kết quả; không suy thành chọn tất cả |
| S02 | Chỉ chọn dở công thức | Cân bằng · mờ | Chọn trọn phương trình để cân bằng; một hành động Chọn trọn có chủ đích |
| S03 | Chọn cả đoạn, đầu/cuối cắt công thức | Tính theo các phương trình chọn trọn | Báo N trọn, M chọn dở; không kéo dài selection hộ người dùng |
| S04 | Một phương trình Hóa rõ, đủ hai vế; cân bằng sẽ đổi hệ số | Cân bằng · sáng | Tô đúng phương trình; giữ nguồn và chất |
| S05 | Locus đã cân bằng, bản đang thấy khớp snapshot sau | Hủy cân bằng · sáng | Trả đúng hệ số trước đó, kể cả hệ số sai |
| S06 | Người dùng tự gõ sẵn phương trình đã cân bằng | Đã cân bằng · mờ | Locus chưa thay đổi gì; không tạo Undo/Hủy giả |
| S07 | Công thức Toán/Lý hoặc một chất Hóa | Cân bằng · mờ | “Cân bằng chỉ áp dụng cho phương trình Hóa” / “Cần phương trình có hai vế” |
| S08 | Thiếu vế, lỗi cú pháp, chưa chốt cách đọc | Cân bằng · mờ | Giữ text hoặc bản hiện tại; lý do cụ thể; fx cho lựa chọn có chủ đích |
| S09 | Không có nghiệm với các chất hiện tại / vượt giới hạn | Cân bằng · mờ | Không đổi chất; copy bản hiện tại vẫn được phép |
| S10 | Nhiều phương trình, có vùng cần cân bằng | Cân bằng tiếp · k/N | N đếm vùng sẽ đổi trong lượt, k là bước sắp làm; tô vùng đó |
| S11 | Hết lượt cân bằng, có vùng Locus còn hủy được | Hủy cân bằng tiếp · k/N | Bắt đầu từ trên; N chỉ đếm vùng còn lịch sử hợp lệ |
| S12 | Đang tính lệnh | Đang xét k/N · khóa bấm lặp | Dừng xử lý; chưa thay kết quả đến khi commit |
| S13 | Hủy hoặc nguồn/selection/candidate/config đổi khi chờ | Tính lại theo trạng thái hiện tại | “Đã dừng · nội dung đã đổi” hoặc “Đã hủy · chưa đổi công thức nào” |
| S14 | Người dùng sửa phương trình sau hỗ trợ | Theo khả năng của bản sửa | Lịch sử cũ không được ghi đè; giữ bản mới |
| S15 | File cũ không có snapshot hệ số riêng | Không có Hủy cân bằng riêng | fx: Về trước hỗ trợ, có preview gộp; không bịa bản trung gian |
| S16 | Cả đoạn quá ngân sách xử lý | Không chạy trên vùng chưa xét xong | Giữ toàn bộ nguồn, báo xử lý chưa xong; đường lấy nguyên nguồn còn dùng được |

Một công thức được click chọn trọn có hình nền nhẹ. Trong vùng chọn nhiều công thức, công thức sẽ xử lý tiếp có nhãn “tiếp theo”/dấu định hướng. Không dùng một màu khác cho từng môn. Thứ tự xử lý luôn theo tài liệu, dù bôi ngược từ cuối lên đầu.

Toolbar giữ logical selection khi lấy focus. Bấm ngoài kết quả, chọn vùng mới, sửa nguồn/candidate hoặc dùng Undo/Redo phải cập nhật phạm vi trước khi cho lệnh tiếp. Copy/xem fx không tự làm biến mất vùng đang chọn; đổi cách đọc trong fx mới làm lượt cũ hết hiệu lực. Selection không dựa trên số pixel của SVG hoặc textContent của MathML.

Với công thức duy nhất nằm trọn trong selection và không kèm văn bản, PNG/SVG có thể bật, kể cả chọn bằng Ctrl+A trong chính ô kết quả. Ctrl+A trong ô nhập chỉ chọn nguồn; không được biến thành lệnh chọn tất cả công thức ở ô kết quả. Khi focus ở nơi khác, không chiếm Ctrl+A của host.

## 3. Menu và hoàn tác

| Ngữ cảnh | Mục 1 | Mục 2 |
| --- | --- | --- |
| Chọn nhiều phương trình | Cân bằng tất cả trong vùng chọn · N | Hủy cân bằng tất cả trong vùng chọn · M |
| Vừa cân bằng tất cả, còn khớp | Hoàn tác cân bằng tất cả | Hủy cân bằng tất cả trong vùng chọn · M mới |
| Vừa hủy cân bằng tất cả, còn khớp | Cân bằng tất cả trong vùng chọn · N mới | Hoàn tác hủy cân bằng tất cả |
| Đã đổi selection hoặc có lệnh/sửa đổi khác | Không dùng snapshot nhanh cũ; trở lại hành động trên vùng hiện tại | Undo thông thường còn lịch sử của các lệnh trước |

Menu có ít nhất hai nút native có tên đọc được, thứ tự focus tự nhiên, Escape đóng và trả focus về mũi tên khi triển khai. Có thể dùng popover bên trên nếu dưới không đủ chỗ; tại cửa sổ hẹp, menu vẫn xuống dòng và không tràn cạnh cửa sổ. Wireframe dùng menu trong luồng bố cục để xem toàn bộ.

Một batch có ba pha: tính tuần tự và báo tiến độ → commit một lệnh → tổng kết số đổi/giữ/bỏ qua. Không đổi từng phần trong lúc tính. Lỗi giao dịch hoặc stale không dẫn đến kết quả ghi dở. Menu chỉ đổi sang Hoàn tác sau commit; batch không thay gì không tạo mục Hoàn tác mới.

Hai mục không duy trì hai nhánh hoàn tác nhanh độc lập chồng lên nhau. Mục đối lập tạo lệnh mới. Hoàn tác nhanh trả cả hệ số, provenance hỗ trợ và quyết định giữ khỏi auto, không chỉ trả hình công thức.

Nút Undo của editor giữ ý nghĩa lịch sử chung, khác Hủy cân bằng theo vùng. Tổ hợp phím hiện có phải được giữ/kiểm theo host; không gán thêm hotkey toàn máy trong UX1.

## 4. Copy, tải và phạm vi hiển thị

| Người dùng đang chọn | Dòng phạm vi | Copy | PNG/SVG | Tải |
| --- | --- | --- | --- | --- |
| Không chọn gì | Toàn bộ đoạn kết quả | Toàn bộ kết quả, không phải ô nguồn | Mờ; mời chọn một công thức | Tệp chứa toàn tài liệu; chọn định dạng rõ |
| Đúng một công thức, không có văn xuôi | Công thức đang chọn | Bản đang thấy của công thức | Sáng, đúng kết quả sau/before hỗ trợ | Định dạng ảnh/công thức theo capability |
| Một đoạn nhiều công thức/text | Phần kết quả đang chọn · N công thức | Chính đoạn chọn, giữ text ngoài công thức | Mờ ở giai đoạn đầu | Đoạn chọn nếu định dạng hỗ trợ; không mở rộng ra toàn tài liệu ngầm |
| Chọn tất cả trong kết quả | Toàn bộ đoạn kết quả · N công thức | Toàn bộ kết quả | Chỉ sáng nếu selection thực chất là một công thức duy nhất | Toàn tài liệu theo định dạng đã nêu |
| Chọn dở công thức | Văn bản đang chọn · có công thức chọn dở | Chỉ định dạng text cho selection hợp lệ; không xuất ảnh của cả vùng bị cắt | Mờ | Không tạo tệp công thức/native từ phần cắt dở |
| Chọn vùng lỗi đang là text | Văn bản đang chọn | Lấy nguyên văn bản đó | Mờ | Text/tệp Locus, không bịa native equation |

Đề xuất **Copy** mở lựa chọn định dạng khi toàn đoạn có nhiều loại nội dung: **Đoạn có công thức** (khi clipboard HTML/MathML của DOC1-03 đã đạt), **Văn bản kết quả**, **LaTeX** theo phạm vi có khả năng xuất. Không đánh đồng clipboard HTML với native Word. Khi chưa có một đường copy đoạn có công thức đã kiểm, nhãn phải nói rõ **Copy văn bản**; không bật lựa chọn giả. Wireframe chỉ mở bản xem nội dung và phạm vi dự kiến, chưa là quyết định codec clipboard của DOC1-03.

PNG và SVG mang tên đọc được “Copy PNG công thức đang chọn”, “Copy SVG công thức đang chọn”. Download là menu định dạng (PNG/SVG cho một công thức; văn bản/tệp Locus; `.docx` với OMML khi được triển khai/kiểm). `.locus` luôn có nguồn và quyết định theo schema, không dùng ảnh làm bản lưu duy nhất.

**Copy nguồn gốc** ở Tùy chọn lấy đúng ô nhập, gồm cặp bọc, chữ thường, dấu cách và xuống dòng. fx phải phân biệt **Nguồn đã gõ**, **Trước cân bằng**, **Đang dùng**; chuỗi đã chuẩn hóa để render không được gắn nhãn nguồn gốc.

Đợt đầu nhận text dài bảo toàn câu chữ/xuống dòng. Không hứa bảo toàn font, bảng, hình hay danh sách rich text từ clipboard của Word. Nếu định dạng dán bị giản lược, phải cho người dùng biết ở đường dán đó.

## 5. Auto, ghost và nội dung cần giữ

Tự cân bằng mặc định tắt và độc lập với Nhận diện Hóa, Space nhận ghost, cặp bọc. Bật không cân bằng lại tài liệu cũ; tắt không hủy kết quả đã nhận. Thông báo bật: “Áp dụng cho lần nhập hoặc dán tiếp theo”.

Sau hủy thủ công, vùng hiện “Giữ hệ số đã nhập” khi chọn/xem fx; không bắt hiển thị badge dài trên tất cả công thức trong đoạn. Quyết định theo nội dung vùng, không mất bởi render/reload hoặc sửa câu bên cạnh. Bấm cân bằng có chủ đích được phép đổi quyết định. Không tạo hộp xác nhận cho từng lần cân bằng/hủy có Undo.

Ghost có kiểu chữ mờ và phần preview/điều kiện rõ, chưa nằm trong nội dung đã copy. Enter là nhận có chủ đích; Space chỉ khi bật tùy chọn riêng. Bản thiết kế dùng nút Nhận gợi ý để chuyển bước ví dụ, không bắt phím gõ thật. Escape bỏ ghost, giữ draft. Đổi điều kiện/cách đọc làm proposal cũ hết hiệu lực.

Khi đã nhận sản phẩm và hệ số: Hủy cân bằng giữ sản phẩm; **Bỏ sản phẩm đã nhận** xem trước phần sẽ mất rồi trả về trước hỗ trợ. File cũ không đủ dữ liệu chỉ cho khôi phục gộp. Không cần ba màn thường trực; fx là nơi xem trước/sau và các cách đọc, tổng số candidate của một vùng vẫn tối đa ba.

## 6. Bộ quy tắc UI tối thiểu

Các token là đề xuất phục vụ đúng editor này, không phải dự án design system tổng quát.

| Nhóm | Quy tắc A/v1 |
| --- | --- |
| Chữ UI | Segoe UI trên Windows; sans-serif theo host nếu thiếu; 14 px, line-height 1.5, weight 400/500 |
| Ô nhập | 16 px, line-height 1.55; giữ chữ thường/Unicode, không biến textarea thành lớp công thức |
| Công thức | Renderer cùng MathDocument trong sản phẩm; wireframe dùng Cambria Math/serif và chỉ số HTML, 18 px để thấy bố cục |
| Chú thích | 12 px; thông tin quan trọng có thể xuống dòng, không thu nhỏ dưới 11 px |
| Khoảng cách | Bội số 4; mặc định 12 px viền nội dung, 8 px giữa nhóm nhỏ, 20 px giữa hai cột; khoảng trống tối thiểu 4 px |
| Điều khiển | Cao khoảng 34 px với chuột, vùng chạm 44 px; giữ chữ bên icon ở các hành động chính |
| Bo góc | 6 px ở input/nút/menu, 10 px cửa sổ; không card hóa từng dòng công thức |
| Màu sáng | Nền `#FFFFFF`, chữ `#20252B`, chữ phụ `#59636E`, viền `#D4D9DF`, hành động `#3158B8`, vùng chọn `#EAF0FF` |
| Màu tối | Nền `#202225`, chữ `#EDF0F3`, chữ phụ `#B3BBC6`, viền `#505761`, hành động `#B5C8FF`, vùng chọn `#263C66` |
| Ý nghĩa màu | Xanh: chọn/hành động. Cảnh báo: cần quyết định/không thực hiện được. Màu luôn đi với chữ, không phân môn chỉ bằng màu |
| Icon | Hệ icon thống nhất: wand, undo, chevron, copy, image, file-code, download, maximize/minimize, close, options; không trộn emoji nhiều kiểu vào toolbar |
| Focus | Giữ focus trình duyệt/host, tab order theo DOM; có tên cho nút chỉ icon, không thêm tabindex dương; thao tác toolbar không xóa selection logic |
| Thông báo | Một vùng aria-live polite cho hành động xong/đổi phạm vi; lỗi nhập dùng mô tả tại nguồn, không đọc lại mọi thay đổi hệ số/hover |
| Responsive | Hai cột khi cửa sổ trên 680 px; dưới đó xếp dọc (điều chỉnh ở B để toolbar và công thức vừa cột). Toolbar/options/menu wrap. Giữ phóng chữ, không scale cả UI bằng transform |
| Desktop | Bung/thu và ẩn giữ cùng phiên/Undo; startup tự động cần lựa chọn riêng, không tự bật hoặc mở Word trong đợt UX1 |

Component cần tạo ở UX1-02: AppShell, WorkspaceTabs, SourceInput, ResultDocument, SelectionStatus, BalanceSplitButton, ExportToolbar, DetectionOptions, FormulaDetails. Các tên là gợi ý tổ chức; không ép dùng một framework mới hoặc lặp model/solver tại component.

## 7. Điểm còn kiểm khi triển khai

- Clipboard nguyên đoạn và `.docx` cần DOC1-03 kiểm khả năng thực tế trước khi bật định dạng; wireframe chỉ chốt phạm vi/nội dung.
- Kéo chọn thật, lựa chọn cắt công thức, bàn phím, IME và focus trên Web/Desktop cần DOC1/SC1-08; chưa nghiệm thu bằng button mẫu.
- Khung gọn có đoạn dài cần giới hạn chiều cao, cuộn và vị trí popover theo work area thực/DPI tại UX1-03.
- Candidate ambiguous/repair dùng cấu trúc core có sẵn, không xây một danh sách đề xuất riêng cho cân bằng.
- Mọi lệnh sửa kết quả cần snapshot của đúng phiên/revision. UI không được là nguồn xác định tính hợp lệ duy nhất.
