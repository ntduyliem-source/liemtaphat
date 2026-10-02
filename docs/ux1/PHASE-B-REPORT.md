# Đợt B — Editor gọn và tài liệu nguyên đoạn

Ngày 2026-09-15. **DONE trong phạm vi B**; [receipt và hash nguồn/bằng chứng](../../artifacts/ux1/verification/receipt.json). Phụ trách: Codex, một luồng triển khai. Người dùng yêu cầu “Làm bước tiếp theo”, lấy thiết kế đợt A làm đầu vào để triển khai B. Phạm vi: UX1-02, DOC1-01 và DOC1-02; không lấy việc chờ test Word/OS làm điều kiện cho ba task này.

## Kết quả

Editor Web/Desktop dùng chung đã đổi sang nguồn → kết quả → copy/options. Một ô nhận công thức đơn hoặc cả đoạn; kết quả chứa nguyên văn bản và công thức inline, có fx riêng. Chọn cách đọc, giữ text, sửa nguồn vùng đó, Undo/Redo dùng cùng session.

ContentDocument giữ raw, vùng có ID/revision/span, snapshot và lựa chọn; vùng còn nguyên giữ quyết định khi sửa lời dẫn bên cạnh. Dữ liệu result override và command trước/sau đã có để BAL1 dùng tiếp. File v5/v6 đọc lại snapshot, không chạy lại parser hoặc kho phản ứng. B chưa nối solver vào result override hay tạo các lệnh cân bằng mới.

Selection hỗ trợ click trọn, kéo ngược, chọn dở và Ctrl+A trong kết quả. Công thức render bị chọn dở có hành động Chọn trọn rõ ràng trước khi copy; vùng đang là text giữ offset từng chữ. Copy đoạn là text với công thức LaTeX. PNG/SVG chỉ bật cho đúng một công thức được chọn.

Một lỗi core Hóa được sửa trong đợt này: span thông báo lỗi ở ký tự emoji có thể cắt đôi surrogate pair và làm phân tích đoạn thất bại. Phạm vi sửa là ranh giới thông báo lỗi, không đổi quy tắc phản ứng/solver.

## Bản local

- Build Web/Worker/Desktop: `20260915-153811-217`, publish Release từ cùng mã.
- [Mở Web](http://127.0.0.1:4187/), [cách dùng](QUICKSTART.md), [current-build](../../artifacts/ux1/current-build.json).
- [Nhật ký publish](../../artifacts/ux1/final-build.log). Có cảnh báo trim IL2026 trên serializer hiện có; các assembly liên quan được giữ trong cấu hình publish và có kiểm bản publish bằng in-app browser.
- [Hợp đồng model, lựa chọn và version file](../doc1/MODEL.md).

## Kiểm tra trong phạm vi

| Nhóm | Kết quả và giới hạn |
| --- | --- |
| Dữ liệu đoạn/model | [20/20](../../artifacts/ux1/verification/content-tests.json): ba môn, URL/email/path, NFD/emoji/CRLF, selection, danh tính vùng trùng chữ, repair, dòng dài, giới hạn/hủy/stale, v5/v6, override và history |
| Session/file/core liên quan | [35/35](../../artifacts/ux1/regression/application-tests.json), gồm [238/238 core contracts](../../artifacts/ux1/regression/core-contracts.json); không coi là test Word write/fx/OS |
| Web thật | Ghi quan sát thao tác, build và phạm vi trong [browser-checks](../../artifacts/ux1/verification/browser-checks.json) |
| Desktop | Publish thành công cùng editor/model; chưa chạy nghiệm thu native IME/clipboard/tray của bản mới |

Không chạy lại toàn bộ solver, kho phản ứng hoặc bộ OS sau mỗi thay đổi UI. Các test model mới tập trung vào nguy cơ mất nguồn/lựa chọn, stale và migration.

## Phần chưa thuộc kết quả này

- BAL1-01…04: chuẩn bị/commit lệnh cân bằng, hủy đúng hệ số trước, lượt tuần tự, hai batch/quick undo và checkbox auto.
- DOC1-03: xuất/copy nguyên đoạn có công thức trong định dạng giàu cấu trúc; DOCX OMML, xác minh file qua hai host sau BAL1.
- UX1-03/SC1-08: tray/bung/ẩn và bộ gõ/focus/trợ năng OS. B mới có responsive và vùng focus/nút có tên trong browser.
- SC1-09/10, WD1, E1/E2, quota/tài khoản/online giữ nguyên hàng đợi. SC1 vẫn 7/10; không lấy B làm tăng % SC1.

Hỗ trợ Hóa công thức đơn SC1 cũ còn trong phần thu gọn, theo đường nhận và thay nguồn cũ. Nó chưa phải đũa thần theo selection mới. Lịch sử Undo chung chỉ ở phiên hiện tại; file giữ command vùng nhưng chưa phát lại toàn bộ Undo sau mở.

## Tiếp theo

Lấy BAL1-01 trước: lập lệnh bằng ID/revision và snapshot đang hiển thị, lưu hệ số trước/sau riêng với sản phẩm; kiểm source/selection/config trước commit; hủy không chạy lại solver. Sau đó BAL1-02 nối nút đơn/tuần tự, BAL1-03 batch nguyên tử và BAL1-04 auto/bỏ qua theo vùng. Dùng schema hiện có, không thay toàn textarea.
