# Đợt C — Cân bằng, hủy và giữ quyền quyết định

Ngày 2026-09-15. **BAL1-01…04 DONE trong phạm vi C: model/editor chung, Web local và publish Desktop.** Đây là 4/4 task BAL1, không phải phần trăm toàn dự án. [Receipt và hash](../../artifacts/bal1/verification/receipt.json), [cách thử](QUICKSTART.md), [hợp đồng hành vi](../ux1/BALANCE-INTERACTION.md).

## Thành quả

Đũa thần tác động vào phương trình được chọn trong ô kết quả. Một phương trình có thể cân bằng rồi hủy về đúng hệ số người dùng đã nhập, dù hệ số đó sai. Công thức tự cân bằng sẵn không có thao tác hủy giả. Phương trình không có nghiệm vẫn giữ nguyên và xuất được nếu đã có cách đọc hợp lệ.

Chọn nhiều phương trình: nút chính đi từng vùng từ trên xuống; hết lượt cân bằng mới sang lượt hủy. Hai mục trong menu chạy cả vùng chọn. Mục vừa dùng đổi thành “Hoàn tác…” và phục hồi chính xác trạng thái trước lệnh, gồm vùng đã cân bằng từ trước và quyết định bỏ auto. Mỗi batch commit một lần; hủy, đổi nguồn/selection/cài đặt hoặc lỗi khi đang chờ không ghi dở.

Checkbox Tự cân bằng mặc định tắt. Bật chỉ xét lần nhập/dán mới hoặc công thức vừa sửa; không quét ngược tài liệu đang có. Hủy thủ công đánh dấu vùng bỏ qua auto. Dán kèm auto dùng một Undo. Tắt auto, mở file, Undo/Redo hoặc ẩn view hủy yêu cầu còn chờ.

Nhận sản phẩm bằng ghost/nút giữ draft trong ô nguồn. File v7 lưu sản phẩm và bản hệ số trước solver riêng: `hoa-[3H2+O2=]` → kết quả `2H2+O2→2H2O` → hủy cân bằng còn `3H2+O2→H2O` → bỏ sản phẩm mới về draft. Tệp SC1 cũ chỉ có lịch sử gộp tiếp tục khôi phục gộp; không suy lại khi mở.

## Bản chạy

- Build Web/Worker/Desktop: **20260915-161929-816**, .NET SDK 10.0.400, Release; [current-build](../../artifacts/bal1/current-build.json).
- [Web local](http://127.0.0.1:4188/releases/20260915-161929-816/); mở lại bằng `tools/bal1/local.ps1 start`.
- [Nhật ký publish](../../artifacts/bal1/final-build.log). Cảnh báo trim của serializer hiện có vẫn được ghi trong log; kiểm Web dùng chính bản publish.
- [Model v7](../doc1/MODEL.md), [lệnh cân bằng](../../src/Locus.Application/FormulaSession.Balance.cs), [nhận sản phẩm](../../src/Locus.Application/FormulaSession.ContentProducts.cs), [worker wire](../../src/Locus.Application/ContentBalanceWire.cs).

## Kiểm chứng và giới hạn

| Nhóm | Kết quả |
| --- | --- |
| BAL1 model/lệnh | [20/20 nhóm](../../artifacts/bal1/verification/balance-tests.json): selection/dở/ngược, hệ số gốc, vòng tuần tự, trạng thái trộn, hai quick undo, atomic/cancel/stale/worker lỗi, auto/Undo/bỏ qua, sản phẩm, v7 và protocol |
| Content liên quan | [20/20](../../artifacts/bal1/regression-content/content-tests.json), gồm nguồn/selection/vùng trùng chữ/Unicode/giới hạn/file cũ; giữ phạm vi riêng của B |
| Session/core liên quan | [35/35](../../artifacts/bal1/regression/application-tests.json), trong đó [238/238 core contracts](../../artifacts/bal1/regression/core-contracts.json) |
| UI Web | [Quan sát theo build](../../artifacts/bal1/verification/browser-checks.json): toggle, hai menu và trạng thái trộn, auto+Undo, ghost Enter, hủy giữ sản phẩm, bỏ sản phẩm, nháp/cập nhật và bản publish |
| Desktop | Publish cùng model/editor thành công; chưa nghiệm thu lại clipboard native, Telex/VNI, tray và file đi qua hai host thực của bản C |

BAL-UX-01…11 có kiểm model/lệnh và luồng UI liên quan. BAL-UX-12 đạt phần codec/session và Web; file Web ↔ Desktop, paste ảnh vào ứng dụng Windows và rich export tiếp tục ở DOC1-03. Không dùng số test C để đóng các bài OS/Word còn thiếu. Các phép cân bằng/kho phản ứng SC1 đã đạt được dùng lại; không chạy lại toàn bộ kho sau mỗi sửa UI.

Copy cả đoạn hiện là text chứa công thức LaTeX; PNG/SVG cho một công thức được chọn. Nguồn vẫn là plain text, chưa giữ định dạng bảng/font/hình khi dán. Stack Undo nằm trong phiên; file giữ snapshot/command phục vụ hủy cân bằng/bỏ sản phẩm, không khôi phục toàn stack Undo hoặc trạng thái quick undo sau mở.

## Bước tiếp theo

Lấy **DOC1-03 của đợt D**: xuất toàn đoạn với công thức, lưu/mở v7 qua Web/Desktop và kiểm phạm vi copy. Tiếp theo UX1-03 tray/bung cùng session, rồi gom phần kiểm host SC1-08. SC1 vẫn 7/10 task; W0 4/6 chờ các phần nghiệm thu đã ghi. Word SC1-09/10, WD1 quét tài liệu, E1 đồ thị/thanh trượt, hình 2D/3D và quota giữ đúng hàng đợi trong [NEXT-STEPS](../NEXT-STEPS.md).

Bản B và SC1 cũ giữ build/receipt riêng. Đợt C không cài lại connector Word và vẫn chạy local theo yêu cầu.
