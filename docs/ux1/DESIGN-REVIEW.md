# UX1-01 — Thiết kế đợt A

Ngày 2026-09-15 · phiên bản A/v1 · **bản thiết kế A/v1; sau đó được dùng làm đầu vào cho đợt B theo yêu cầu bước tiếp theo.** Dựa trên [baseline](../BASELINE-20260915.md), [thứ tự triển khai](../NEXT-STEPS.md) và [hành vi cân bằng đã thống nhất](BALANCE-INTERACTION.md).

## 1. Đầu ra của đợt A

- [Wireframe tương tác](wireframes.html): bốn khung Web rộng/hẹp, Desktop gọn/bung; bốn luồng chính và các trạng thái phụ. Dữ liệu là ví dụ cố định; chọn luồng/bước rồi thử các nút trong khung.
- [Bảng trạng thái và quy tắc UI](UI-SPEC.md): điều kiện bật nút, phạm vi chọn/copy, nội dung thông báo, menu và cách bố trí.
- Bốn walkthrough bên dưới để kiểm ý nghĩa thao tác, kèm bảng bàn giao sang B/C/D.

Bản xem đang phục vụ ở [in-app browser local](http://127.0.0.1:4186/) trong phiên thiết kế. Chọn **Khung** để đổi kích thước; **Luồng/Bước** để mở tình huống. Trong khung có thể chọn công thức, cân bằng, hủy, chọn tất cả, dùng hai mục batch, bật/tắt auto, xem nội dung copy dự kiến và bung/thu/ẩn Desktop. Các lựa chọn Khung/Luồng/Bước nằm ngoài cửa sổ Locus, chỉ phục vụ review.

Wireframe không có parser/solver thật, không ghi clipboard, không xuất ảnh/file, không mở Word hoặc tray Windows. Ô nguồn khóa ở ví dụ đang xem. Chọn dở được dựng thành một bước mẫu; chưa triển khai kéo bôi đen, IME, đồng bộ host, migration hoặc rollback thật. Checkbox môn và tab tương lai là hình dáng dự kiến, không phải tính năng đã chạy. Trạng thái processing/cancel và paste/ghost là storyboard của ví dụ, không phải bằng chứng nghiệm thu những luồng sản phẩm đó.

`wireframes.html` là nguồn thiết kế trong repo. Bản hiển thị trong hội thoại được sao từ nguồn này; dùng trình render của visualize để xem nội bộ, không đưa vào bản phát hành Locus. URL preview là tạm trong phiên local, tài liệu và nguồn thiết kế là phần bàn giao lâu dài.

## 2. Những quyết định thiết kế đề xuất

| Mục | Quyết định A/v1 | Ý nghĩa khi review |
| --- | --- | --- |
| Ô nhập | Một ô cho một công thức hoặc cả đoạn; nguồn gốc còn nguyên | Không phải chọn loại tài liệu trước khi dán |
| Kết quả | Công thức nằm trong nguyên đoạn; click công thức chọn trọn, kéo chọn đoạn khi triển khai | Văn bản ngoài công thức không mất; vùng mơ hồ/lỗi vẫn là text |
| Bố cục | Rộng: nguồn và kết quả cạnh nhau. Hẹp/gọn: nguồn trên, kết quả dưới | Hai cách bố trí dùng chung vùng chọn và các lệnh |
| Nút chính | Đũa thần + chữ Cân bằng; đổi thành Hủy cân bằng khi có thay đổi còn phục hồi được | Không buộc mở hai ô nguyên trạng/hỗ trợ thường trực |
| Nhiều phương trình | Mỗi bấm một phương trình theo thứ tự trên xuống; tô vùng kế tiếp và ghi số bước | Dropdown mới là đường chạy cả vùng |
| Menu hàng loạt | Hai mục có tên và số công thức sẽ đổi; mục vừa dùng đổi thành Hoàn tác… | Bấm lại trả đúng trước chính lệnh đó |
| Auto | Checkbox Tự cân bằng nằm dưới kết quả, mặc định tắt | Chỉ lần nhập/dán mới được xét; tắt không trả ngược kết quả cũ |
| Copy | Một dòng “Xuất: …” luôn cạnh toolbar; PNG/SVG chỉ cho đúng một công thức | Người dùng biết đang lấy công thức chọn hay cả đoạn |
| fx | Mở nguồn/cách đọc/hiện tại, xem trước hỗ trợ và khôi phục | Không tạo thêm bộ ba candidate bên ngoài giới hạn hiện có |
| Desktop | Hộp gọn ở góc, icon bung/thu; đóng cửa sổ là ẩn về tray, Thoát là mục riêng | Nháp, vùng chọn và Undo giữ cùng một phiên |
| Môn học | Toán/Lý/Hóa vẫn là checkbox, cùng ô nhập/kết quả | Không tạo màn soạn Hóa riêng |
| Công cụ sau | Công thức · Biểu đồ · Hình học; 2D/3D bên trong Hình học | Không hứa tab đồ thị/hình học hoạt động ở đợt A/B |

Kích thước và cách xếp rộng là đề xuất của bản A/v1, chưa phải lựa chọn đã được người dùng nghiệm thu. Bản xem trong hội thoại có tùy chỉnh thiết kế để so hai ô cạnh nhau/xếp dọc và khoảng cách 10–16 px; các tùy chỉnh này không đi vào menu sản phẩm.

## 3. Chú thích bốn wireframe

| Khung | Bố trí và kích thước đề xuất | Điều giữ được |
| --- | --- | --- |
| Web rộng | Nội dung tối đa 960 px; hai cột khi vùng cửa sổ trên 620 px | Nguồn và kết quả cùng nhìn thấy; toolbar xuất phía dưới, không có icon bung Desktop |
| Web hẹp | Mẫu 360 px; co tới 320 px; nguồn → kết quả → hành động → xuất → options | Nút có chữ, menu được xuống dòng, không thu chữ để nhét ngang |
| Desktop gọn | Mẫu rộng 420 px, đặt về góc phải; hai ô xếp dọc | Cửa sổ ngắn cho công thức đơn, tăng chiều cao trong giới hạn màn hình khi dán đoạn; phần nội dung dài cuộn khi triển khai |
| Desktop bung | Mẫu tối đa 900 px ở giữa, hai cột nếu đủ rộng | Chỉ thay kích thước/bố cục, không tạo tài liệu hay lịch sử mới; icon thu trở về hộp gọn |

Wireframe tự giãn theo nội dung để xem hết trong hội thoại. Cửa sổ thật phải giới hạn trong work area, hỗ trợ phóng chữ/DPI và cuộn nội dung dài ở đợt UX1-03/SC1-08; chưa dùng kích thước mockup làm bằng chứng OS đã đạt.

Thứ tự các vùng có thể đọc theo chiều dọc: tên Locus và hành động cửa sổ → tab → nguồn/kết quả → vùng chọn và lệnh → phạm vi copy → checkbox/options. Nút cân bằng ở ngay sau kết quả, không nằm ở đầu ô nguồn. Trong bản rộng, toolbar xuất phục vụ chính kết quả bên phải dù trải dưới hai cột; nhãn phạm vi là bắt buộc.

## 4. Walkthrough 1 — Một phương trình, có hệ số nhập sai

Nguồn cố định: `hoa-[3h2+o2=h2o]`. Đây là ví dụ hệ số sai được phép giữ, không dùng làm kiến thức Hóa.

| Bước | Thấy và làm | Kết quả cần hiểu |
| --- | --- | --- |
| 1 | Kết quả `3H₂ + O₂ → H₂O`, chưa chọn; đũa thần mờ | Nhận diện công thức không đồng nghĩa tự cân bằng |
| 2 | Chọn một phần công thức | Nút mờ; “Chọn trọn phương trình để cân bằng”; có cách chọn trọn rõ ràng |
| 3 | Chọn toàn phương trình | Cân bằng sáng; PNG/SVG có phạm vi một công thức |
| 4 | Bấm Cân bằng | Kết quả `2H₂ + O₂ → 2H₂O`; nguồn vẫn là chuỗi đã gõ; nút đổi Hủy cân bằng |
| 5 | Bấm Hủy cân bằng | Trả `3H₂ + O₂ → H₂O`, không đặt hệ số đầu thành 1; ghi giữ lựa chọn khỏi auto |
| 6 | Bấm Copy / PNG / SVG | Xem đúng kết quả hiện tại; Copy nguồn trong Tùy chọn vẫn lấy chuỗi gốc có cặp bọc |

Một bước cân bằng hoặc hủy là một Undo. Nếu tự gõ sẵn `2H2+O2=2H2O`, trạng thái là Đã cân bằng, không tạo nút hủy giả. Nếu sửa thành `5H2+O2=H2O` sau hỗ trợ, snapshot cũ hết hiệu lực; lần cân bằng tiếp dựa trên bản mới và hủy về hệ số 5 vừa gõ.

## 5. Walkthrough 2 — Ba phương trình trộn trạng thái

Gọi A/B/C theo nhãn các dòng trong ví dụ. A đã được Locus cân bằng ở thao tác trước; B/C chưa cân bằng. Đây là ba vùng của cùng một đoạn, cùng selection.

| Vùng | Trước cân bằng của vùng | Trạng thái đầu walkthrough |
| --- | --- | --- |
| A | `3H₂ + O₂ → H₂O` | `2H₂ + O₂ → 2H₂O`, có lịch sử Locus |
| B | `Fe + O₂ → Fe₂O₃` | Nguyên trạng |
| C | `Al + O₂ → Al₂O₃` | Nguyên trạng |

Đường nút chính:

1. Chọn cả ba: **Cân bằng tiếp · 1/2**, tô B. Mẫu số 2 đếm hai phương trình cần đổi trong lượt cân bằng, không đếm A đã cân bằng.
2. Bấm: B thành `4Fe + 3O₂ → 2Fe₂O₃`; **Cân bằng tiếp · 2/2**, tô C.
3. Bấm: C thành `4Al + 3O₂ → 2Al₂O₃`; chuyển **Hủy cân bằng tiếp · 1/3**, tô A.
4. Bấm tiếp chỉ hủy A; rồi đến B, C. Không bắt đầu cân bằng lại A giữa lượt hủy.
5. Hủy xong cả ba thì bắt đầu lượt cân bằng mới. Vòng này không phải cách khôi phục trạng thái trộn ban đầu.

Đường dropdown, khởi động lại ở trạng thái đầu:

| Hành động | A | B | C | Mục vừa bấm |
| --- | --- | --- | --- | --- |
| Cân bằng tất cả · 2 | Cân bằng như trước | Cân bằng | Cân bằng | Hoàn tác cân bằng tất cả |
| Bấm lại mục trên | Vẫn cân bằng | Nguyên trạng | Nguyên trạng | Cân bằng tất cả · 2 |
| Hủy cân bằng tất cả · 1 | Trả hệ số 3,1,1 | Nguyên trạng | Nguyên trạng | Hoàn tác hủy cân bằng tất cả |
| Bấm lại mục trên | Cân bằng như trước | Nguyên trạng | Nguyên trạng | Hủy cân bằng tất cả · 1 |

Nếu chạy mục đối lập ngay sau một batch, đó là lệnh mới; đường Hoàn tác nhanh của batch cũ hết hiệu lực. Undo thông thường vẫn theo lịch sử. Đổi vùng chọn cũng bỏ đường Hoàn tác nhanh, không xóa Undo.

Mở **Các trạng thái phụ → Đang tính batch** để xem tiến độ và Dừng xử lý. Khi tính chưa xong, hình A/B/C chưa thay đổi. Hủy, sửa nguồn hay đổi vùng chọn trước commit đều giữ nội dung hiện tại. Đây là trạng thái thiết kế; giao dịch thật và giới hạn batch phải kiểm ở BAL1-03.

Trường hợp mở rộng cần giữ khi code: thêm D do người dùng cân bằng sẵn và E không có nghiệm. D không được hủy; E giữ nguyên kèm lý do. Mẫu số từng lượt chỉ đếm vùng đủ điều kiện, menu báo số giữ nguyên/bỏ qua.

## 6. Walkthrough 3 — Dán nguyên đoạn và auto

Ví dụ có câu tiếng Việt, emoji, công thức Toán, hai phương trình Hóa, URL và xuống dòng. Mỗi bước là một snapshot độc lập để so sánh cùng đầu vào.

| Tình huống | Kết quả |
| --- | --- |
| Dán khi auto tắt | Cả đoạn xuất hiện; dựng công thức, chưa thay hệ số |
| Bật auto sau khi đoạn đã có | Cả đoạn giữ nguyên; chỉ lần nhập/dán hoặc sửa phương trình tiếp theo được xét |
| Dán mới khi auto đã bật | Hai phương trình được cân bằng; Toán và văn xuôi giữ nguyên |
| Chọn phương trình đầu và hủy | Chỉ phương trình ấy trả về hệ số đã gõ; đánh dấu giữ hệ số dù auto còn bật |
| Tải lại, sửa câu bên cạnh, bật/tắt auto | Quyết định giữ công thức không tự bị xóa; file/persistence thật kiểm tại DOC1-03/BAL1-04 |
| Sửa chính phương trình đã giữ | Nội dung mới có thể được xét auto lại; lệnh tay luôn có thể cân bằng lại vùng được chọn |

Copy khi không chọn gì có phạm vi rõ **Toàn bộ đoạn kết quả**. Chọn công thức thì PNG/SVG dành riêng cho công thức đó. Chọn cả đoạn có văn bản thì PNG/SVG mờ trong giai đoạn đầu, không giả xuất nguyên đoạn thành ảnh. Copy văn bản hay định dạng có công thức và đường `.docx` chứa OMML được thực hiện/kiểm tại DOC1-03.

Với lần dán + auto, Undo trở về trước lần dán; Hủy cân bằng chỉ trả hệ số và giữ đoạn vừa dán. Kết quả đến muộn không được ghép ngược vào Undo của một lần dán cũ sau khi đã có sửa đổi mới.

## 7. Walkthrough 4 — Sản phẩm khác hệ số

Ví dụ cố định về phản ứng tạo nước; ghost có điều kiện của bản ghi, người dùng chủ động nhận. Bật auto cân bằng không nhận ghost hộ người dùng.

| Trạng thái | Ô nhập | Ô kết quả | Đường quay về |
| --- | --- | --- | --- |
| Trước nhận | `hoa-[h2+o2=]` | `H₂ + O₂ =`; ghost `2H₂ + O₂ → 2H₂O` riêng | Bỏ qua/Esc vẫn giữ nguồn |
| Đã nhận | Giữ chuỗi gốc | `2H₂ + O₂ → 2H₂O` | Undo lần nhận bỏ cả sản phẩm và hệ số; Hủy cân bằng chỉ bỏ hệ số hỗ trợ |
| Hủy cân bằng | Giữ chuỗi gốc | `H₂ + O₂ → H₂O` | H₂O được giữ; fx có Bỏ sản phẩm đã nhận với preview |
| Bỏ sản phẩm | Giữ chuỗi gốc | `H₂ + O₂ =` | Có thể nhận gợi ý mới; không lén cân bằng hay suy lại khi mở file |

**Trạng thái trung gian cần lưu:** sau khi đã thêm sản phẩm, trước solver, hệ số trái giữ từ nguồn và hệ số phải lấy đúng proposal đã nhận. Ví dụ này là 1,1,1; không tổng quát hóa “hủy là về 1”. Nếu nguồn trái có hệ số 3, trung gian phải giữ 3. Intermediate snapshot không nhất thiết là một bước UI/Undo riêng: nhận ghost vẫn commit một lệnh; Hủy riêng là lệnh tiếp theo.

**File cũ:** nếu chỉ có trước/sau gộp, không bật Hủy cân bằng riêng. fx giải thích “File này chỉ lưu trạng thái trước toàn bộ hỗ trợ”, xem trước và dùng **Về trước hỗ trợ**. Không suy lại phản ứng hoặc bịa hệ số trung gian bằng parser/solver mới.

## 8. Kiểm tra đợt A và giới hạn

Đã xem trực quan và thao tác trên bản ví dụ trong **Codex in-app browser**, ngày 2026-09-15, giao diện tối đang dùng. Kiểm wireframe là kiểm độ rõ của thiết kế và nút ví dụ, không phải nghiệm thu core, Word, file/Undo hoặc cân bằng sản phẩm.

- [x] Xem Web rộng, Web hẹp, Desktop gọn và Desktop bung; vùng nguồn/kết quả chuyển từ dọc sang hai cột.
- [x] Đo nội dung rộng 320 px: không có điều khiển/vùng kết quả tràn ngang, ba tab cùng hàng. Đã xem toolbar và menu dài trên khung hẹp; menu được xuống dòng. Đây không phải kiểm mọi độ phóng chữ/DPI/thiết bị.
- [x] Bấm cân bằng rồi hủy một phương trình: hệ số đầu trở lại 3. Bấm cả hai mục hàng loạt và Hoàn tác: A trở lại cân bằng, B/C nguyên trạng đúng ví dụ trước lệnh.
- [x] Nút tuần tự đổi B rồi C; nhãn từ Cân bằng tiếp 1/2 → 2/2 → Hủy cân bằng tiếp 1/3.
- [x] Tự cân bằng bật sau khi dán không đổi kết quả đang có. Bước dán mới auto bật hiển thị hai phương trình đã cân bằng.
- [x] Nhận ghost → hủy cân bằng còn H2O → bỏ sản phẩm về draft; Undo của nút mẫu khôi phục sản phẩm không kèm hệ số hỗ trợ.
- [x] Chọn dở làm nút cân bằng mờ; fx của ví dụ phân số ghi rõ cách đọc trực tiếp và gợi ý sửa, chỉ hai phương án.
- [x] Ẩn/mở cửa sổ Desktop trong mockup giữ nội dung và vùng chọn; không thao tác tray Windows thật.
- [x] Mã tương tác wireframe qua kiểm cú pháp; không trùng ID hoặc thiếu mục tiêu DOM khai báo; các liên kết nội bộ của bộ bàn giao đã kiểm.

Bản thiết kế gồm 4 khung, 4 walkthrough (20 bước mẫu) và 14 trạng thái phụ. Các số này đếm đầu ra thiết kế, không phải số test sản phẩm đạt. Chưa có phản hồi nghiệm thu của người dùng. Chưa kiểm riêng palette sáng, trình đọc màn hình, IME hay clipboards thật trong đợt A.

Không chạy lại corpus/solver/OS/Word để ghi tiến độ cho đợt thiết kế này. Độ đúng khoa học của solver, mapping selection thật, IME, clipboard, file cũ và quyền khôi phục theo revision sẽ kiểm đúng task triển khai.

## 9. Bàn giao và điểm review

| Cụm tiếp | Dùng thiết kế này để làm | Điều chưa thể lấy wireframe làm bằng chứng |
| --- | --- | --- |
| UX1-02 | Shell gọn, các vùng, toolbar, checkbox/options, focus | Tính đúng của parser/selection/history |
| DOC1-01/02 | Source bất biến, vùng công thức, result override, selected reading, selection source-map | Không mất text, selection dở/ngược, invalidation thật |
| BAL1-01/02 | Before/after, hệ số, products provenance, quyết định giữ khỏi auto; lệnh đơn/tuần tự | Hủy đúng revision, giới hạn solver và Undo thật |
| BAL1-03/04 | Batch snapshot/quick undo/atomic commit; auto theo lần nhập | Cancel/stale/error, không ghi dở, lưu/khôi phục ignore |
| DOC1-03/UX1-03/SC1-08 | File/copy cả đoạn, tray, phím/IME/trợ năng hai host | Clipboard native, save/reopen, tray Windows và Telex/VNI thật |

Ba điểm để người dùng review bằng bản xem: bố cục nguồn/kết quả; nhìn vùng tô và nhãn nút có đoán được lần bấm kế tiếp; phân biệt được Hủy cân bằng với Hoàn tác batch và Bỏ sản phẩm. Trạng thái UX1-01 chuyển **DONE** khi người dùng yêu cầu bước tiếp theo và dùng thiết kế làm đầu vào B. Điều đó không thay cho nghiệm thu trực tiếp các tính năng cân bằng/tray/Word. [Kết quả B](PHASE-B-REPORT.md) được ghi riêng; C chưa triển khai.
