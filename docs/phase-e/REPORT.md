# Đợt E — Hóa thông minh trong Word

Cập nhật 2026-09-16. Build local `20260916-052303-165`, Word connector `0.4.0`. **SC1-09 đã triển khai và đạt các bài native thủ công trong phạm vi dưới đây. SC1-10 còn mở do nghiệm thu host của D/SC1-08 chưa đủ. SC1 đạt 8/10 task, không phải 80% toàn dự án.** Gói E là bản review local. [Cách dùng](QUICKSTART.md), [phạm vi triển khai](PLAN.md).

## Hành vi đã có

- Word đọc bốn cặp chung/Toán/Lý/Hóa từ core và có tùy chỉnh từng cặp. Đọc cài đặt cũ; giữ cặp chung nguyên văn, tắt preset mới nếu xung đột. Checkbox nhận diện vẫn độc lập.
- Giữ phương trình chưa cân bằng vẫn chèn được. Nút Cân bằng/Hủy cân bằng đổi riêng kết quả; hủy trả đúng hệ số trước đó và lưu dấu bỏ auto.
- Bản nháp vế đầu chỉ có sản phẩm sau khi chọn điều kiện và xem/nhận đề xuất. Hủy cân bằng giữ sản phẩm; hủy bổ sung sản phẩm trả về bản nháp. Các hỗ trợ không được trộn vào danh sách cách hiểu của nguồn.
- Công thức đang quản lý có **Cập nhật công thức** để ghi bản vừa xem. Khôi phục nguồn trả cả cặp bọc và chuỗi gốc; Tách quản lý giữ equation native. Mỗi giao dịch một Undo.
- Metadata v2 giữ nguồn/candidate hoặc draft, kết quả, trước cân bằng, proposal/điều kiện/provenance, dấu bỏ auto. Metadata v1 tiếp tục đọc/ghi nguyên trạng. Mở lại dùng snapshot đã lưu, không giải lại phản ứng. Giới hạn metadata 65.536 UTF-16; quá giới hạn bị từ chối trước ghi.
- Lệnh đợi giữ cả identity của snapshot preview, không chỉ candidate ID. Mọi kiểm tra nguồn/tài liệu/selection/settings/IME/focus của M3 giữ nguyên. Native đã sửa bị từ chối dùng lịch sử cũ.

Phép dựng sản phẩm trước cân bằng nằm trong Core; Word và Application dùng cùng phép đó. Word chưa có tự cân bằng khi nhập, ghost trong thân tài liệu hay auto-Space. Các tính năng này vẫn cần cổng riêng.

## Bằng chứng

| Phạm vi | Kết quả |
|---|---|
| Model/metadata/bảng hỗ trợ | **13/13** [hợp đồng bản cuối](../../artifacts/phase-e/passive-fix/contracts/report.json): định tuyến, migration/xung đột, nguồn không hợp lệ, cân bằng/hủy, nguyên trạng đã cân bằng, sản phẩm và hệ số riêng, v1, corruption/budget, preview/nhận tường minh |
| Bảng Toán/Lý/Hóa | **19/19** [render control](../../artifacts/phase-e/science-panel/report.json), 9 mẫu × 2 kích thước và lỗi giới hạn. Đây là ảnh WinForms trong bộ kiểm, không phải screenshot Word |
| Word thủ công | [Lượt native đầu](../../artifacts/phase-e/native-20260915-231854/report.json) có 6 PASS và 1 FAIL: 4 nhóm cặp/native/Undo/restore, sản phẩm/save/reopen khi tắt add-in và stale/native drift đã qua. Lỗi cập nhật được sửa và [kiểm riêng đạt 1/1](../../artifacts/phase-e/native-update-20260915-232508/report.json). Không gọi lượt đầu là 7/7 |
| Word trên build cuối | [Lượt cuối](../../artifacts/phase-e/passive-fix/native/report.json) có 6 PASS/1 FAIL; bài direct Hóa mất selection/focus trong lượt chung, add-in từ chối trước ghi. [Kiểm riêng bài này](../../artifacts/phase-e/passive-fix/native-chem-direct/report.json) đạt 1/1. Cân bằng/cập nhật/Undo, sản phẩm/save/reopen/restore và stale/native drift đều đạt trên build cuối. Giữ nguyên báo cáo thất bại, không gộp thành một lượt 7/7 |
| Giao dịch native cập nhật | **9/9** [transaction](../../artifacts/phase-e/transactions-20260915-232727/report.json): lỗi ở 4 bước đều rollback, chữ/emoji/khoảng trắng ngoài vùng, Undo/Redo, nâng v1 và cách sửa, từ chối nguồn khác. Dùng connector nghiên cứu chỉ trong fixture; các bài focus/preview dùng ManualConnect riêng |
| Application ảnh hưởng | **20/20 BAL1** [receipt](../../artifacts/phase-e/regression/balance-tests.json); **7/7 xuất đoạn** [receipt](../../artifacts/phase-e/export-regression/document-export-tests.json). Phần đổi chung chỉ tách phép chiếu AST sang Core |
| D DOCX trong Word | [Vòng cuối đạt](../../artifacts/doc1/verification/word-final-20260915-232937/report.json): cả mẫu hỗn hợp 5 equation và mẫu sản phẩm đã hủy cân bằng 1 equation qua open/save/close/reopen; text và native signatures giữ đúng; PDF đã tạo. Mẫu sản phẩm đã xem hình, giữ `3H₂ + O₂ → H₂O` |
| Nhận diện trong câu | **431/431 core/miền + 19/19 Application khoa học**, [báo cáo](../../artifacts/phase-e/passive-fix/science/core-verification.json); **21/21 đoạn văn**, [báo cáo](../../artifacts/phase-e/passive-fix/content/content-tests.json). Gồm câu có số thứ tự qua mọi tổ hợp checkbox, cặp riêng, nguồn explicit và 100 dòng |
| Build | Web/Worker/Desktop/Word đều Release, [log cuối](../../artifacts/phase-e/build-passive-fix.log). DLL đo hiệu năng/Word native khớp build cuối; giao dịch rollback 9/9 giữ bằng chứng build trước vì logic Word không đổi, không gọi đó là lần chạy trên DLL mới |

Bộ kiểm native dùng API chính thức tạo tài liệu thử, gọi ManualConnect và chờ guard focus/input thật. Không giả `EditorFocus` hoặc tắt guard; điều khiển pointer Windows vẫn báo lỗi. Các lượt vướng prerequisite focus được lưu riêng và không tính FAIL của công thức hay PASS nghiệm thu. Bài keyboard/IME/tray còn nằm ở SC1-08/D.

Lỗi DOCX trước đó được xác định là bộ kiểm dùng tài liệu neo trống; Word tự đóng nó khi mở file và COM reference bị ngắt lúc cleanup. Bộ kiểm cuối dùng neo có nội dung, cả vòng và cleanup đã qua.

Phép đo phát hiện số thứ tự như `Dòng 1:` bị nhận nhầm khi bật Lý/Hóa. Bản cuối dùng lại điều kiện đủ dấu hiệu Toán trong câu; công thức Hóa rõ hoặc cấu trúc Lý vẫn được nhận. Số đơn lẻ nhập tường minh hoặc trong cặp bọc vẫn hợp lệ. Hai lượt đo đầu dừng vì phát hiện 200 vùng thay vì 100; sau sửa mới ghi nhận số đo dưới đây.

## Số đo trên DLL bàn giao

[Receipt và hash](../../artifacts/phase-e/performance.json), [dữ liệu](../../artifacts/phase-e/performance/20260916-052429-153.json). .NET 10.0.11, Windows 10.0.19045 x64, Intel Family 6 Model 79, 56 logical processors. Mỗi ca làm nóng 30 lần, sau đó 200 mẫu; đoạn 100 phương trình dùng 100 mẫu. Có kiểm kết quả trong phép đo.

| Công việc | p50 | p95 |
|---|---:|---:|
| Phân tích một công thức Toán/Lý/Hóa | 0,15–0,31 ms | 0,23–0,43 ms |
| Cân bằng và kiểm proposal | 7,62 ms | 8,81 ms |
| Suy sản phẩm và kiểm proposal | 4,62 ms | 4,95 ms |
| Đoạn 100 phương trình, 4.790 UTF-16 | 30,76 ms | 38,80 ms |

Đây là số đo native trên máy thử, không gồm debounce/renderer/Word/giao diện hoặc startup app. Lần gọi đầu ca cân bằng khoảng 344 ms; không đại diện phân phối cold start vì các ca chung tiến trình. Đoạn 100 phương trình cấp phát khoảng 10,1 MB/lượt; dữ liệu GC và bộ nhớ có trong báo cáo. Chưa dùng các số này để tuyên bố đạt mục tiêu ghost p95 300 ms trên mọi host.

## Bàn giao và cổng còn mở

Web E ở `http://127.0.0.1:4191/`. Cổng 4190 bị Fetch báo `bad port`; launcher E dùng 4191. Ba gói Web/Desktop/Word được theo dõi ở [danh sách và hash](../../artifacts/phase-e/packages.json), [kiểm ZIP](../../artifacts/phase-e/package-verification.json) và [bàn giao theo từng host](../../artifacts/phase-e/delivery.json). Chỉ receipt cùng build mới áp dụng cho gói hiện hành. Gói ghi rõ review-local và các capability còn chờ; đóng gói thành công không đóng SC1-10.

Cả ba ZIP đã giải nén và kiểm từng tệp/hash: 198 tệp Web, 276 Desktop, 6 Word. Desktop từ ZIP mở và dựng `x² + 1`; Word đã đăng ký đúng DLL cuối. [Web từ ZIP](../../artifacts/phase-e/passive-fix/web-unpacked.json) chạy Worker với nguồn mới; [Web 4191](../../artifacts/phase-e/passive-fix/browser-checks.json) cập nhật giữ nháp, kiểm số thứ tự và cân bằng/hủy. Nhãn/nội dung đọc được qua accessibility không thay nghiệm thu click/tray/IME.

D-HOST-01/02, D-CLIP-01, D-FILE-01 và SC1-08 còn thiếu: Web ↔ Desktop qua file picker thật; tray/bung/ẩn/thoát/IME; copy rồi paste đúng phạm vi; nhận download UI thực tế. Công cụ Windows báo thiếu geometry và UIA cache; IAB báo copy hoàn tất nhưng clipboard API chưa trả đúng. Chưa có căn cứ kết luận đây đều là lỗi Locus hoặc đều là lỗi host.

W0 vẫn 4/6, G2/G3 và phản hồi A/B vẫn chờ. Sau khi chốt các cổng trên, SC1-10 mới DONE. Hàng đợi tiếp theo giữ WD1 quét tài liệu và E1 đồ thị/thanh trượt; không tự mở rộng phạm vi của đợt E.

Automatic approval review từ chối lệnh kết hợp dừng server thử 4190 và khởi động server mới, chỉ nêu `blocked by policy`. Server cũ được giữ nguyên. Khởi động riêng bản 4191 đã thành công; không thử cách khác để dừng server bị từ chối.
