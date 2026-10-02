# Đợt F — Quét tài liệu Word có sẵn

Cập nhật 30/09/2026. Connector 0.5.0, build 20260929-051756-959, dùng Microsoft Word x86 trên Windows. Quét/chuyển thủ công dùng được. **WD1-01/03/04 đã đạt; WD1-02/05 vẫn REVIEW** vì highlight nhiều vùng và ma trận DPI/màn hình/IME chưa đầy đủ. [Biên bản host](../host-review/20260930.md), [receipt có build/hash](../../artifacts/phase-f/host-acceptance.json).

## Khả năng hiện tại

- Quét vùng chọn hoặc thân tài liệu, dùng core và cặp bọc chung. Quét chỉ đọc; giữ Unicode, công thức lặp, màu và định dạng nguồn.
- Bảng nguồn/preview/tối đa ba cách hiểu; chuyển riêng hoặc cả tập đủ điều kiện. Mơ hồ cần chốt, sửa lỗi cần xác nhận riêng; không tự cân bằng/suy sản phẩm khi chuyển nhóm.
- Công thức native OMath có metadata trong content control. Một lệnh ghi tương ứng một Undo; kiểm document/window/source/selection/settings/focus/input, rollback khi lỗi.
- Về text giữ nhận diện khác với giữ text bỏ qua. Quyết định, nguồn và snapshot lưu cùng DOCX. Native/text đã sửa hoặc ID trùng được giữ nguyên và báo cần xem lại.
- Dấu fx cạnh một vùng là tùy chọn thử nghiệm tắt mặc định. Đã sửa lỗi mở lại bảng đang hiển thị và vị trí sai ở lần Show đầu. Đi tới vùng sẽ ẩn bảng để thấy tài liệu; bấm fx mở lại đúng vùng.

## Bằng chứng

| Phạm vi | Kết quả |
| --- | --- |
| Contracts hiện tại | [7/7 đạt](../../artifacts/host-review/20260928-complete/word-final-regression/contracts/report.json), gồm vị trí lần đầu của badge; WinForms layout không được gọi là nhiều DPI thật |
| Hóa thủ công | [13/13 contracts](../../artifacts/host-review/20260928-complete/word-final-regression/smart/report.json); không gọi đây là chạy lại native E |
| Native Word hiện tại | [8/8 đạt](../../artifacts/host-review/20260928-complete/word-final-regression/native/report.json): Unicode, scope, định dạng ngoài vùng, batch/Undo, ambiguity/repair, ignore/restore/include/save-reopen, drift/ID trùng, stale và giới hạn |
| Chuột/phím thật | [6/6 tiêu chí](../../artifacts/host-review/20260928-complete/word-final-ui/report.json): fx đúng vị trí/click mở bảng; Esc khi chờ hủy ghi; chuyển 4 native/control, một Ctrl+Z trả đúng nguồn |
| Zoom/cuộn/khôi phục | Build 20260929-024013-179 trước sửa vị trí đầu: zoom 75/100/150% tại 96 DPI; cuộn ngoài viewport ẩn, cuộn lại hiện; Redo và trả một vùng về text giữ nhận diện. Bằng chứng ở artifacts/host-review/20260928-complete/word-fixed/ |
| Rollback kế thừa | [8/8 trên baseline 17/09](../../artifacts/phase-f/transactions-delivery/report.json). Transaction implementation không đổi trong patch UI; không gán thành lần chạy mới |
| Gói | [ZIP/SHA-256](../../artifacts/phase-f/packages.json), [đối chiếu giải nén](../../artifacts/phase-f/package-verification.json), [đăng ký tại máy](../../artifacts/phase-f/delivery.json) |

Các fixture là tài liệu thử. Bộ native gọi lệnh trên UI thread của add-in và đọc guard thật. Walkthrough dùng chuột/phím Windows, COM chỉ tạo/quan sát tài liệu. Lần lỗi focus ban đầu được giữ ở word-regression/scan-native, không được tính pass; lần cuối 8/8 có focus Word thật.

## Giới hạn

Word 16.0.14026 x86, .NET Framework 4.8; thân tài liệu ngoài bảng/field/hình; tối đa 100.000 vị trí Word, 64 vùng, gap 4.096 UTF-16. Read-only/Protected/Track Changes bị từ chối.

Inline fx mới kiểm trên một màn hình 96 DPI; chưa có highlight đồng thời mọi vùng, chưa nghiệm thu DPI 125/150%, chuyển nhiều màn hình hay IME đang ghép ngay lúc commit. Vẫn tắt mặc định. Không bật auto-Space/ghost Word; phản hồi W0 A/B được hoãn theo người dùng. D-FILE/SC1 giữ cổng riêng.

Đính chính: receipt clipboard cũ dùng COM 64 bit có thể trỏ WPS, không chứng minh Microsoft Word. [Báo cáo thay thế](../../artifacts/host-review/20260928-complete/clipboard-word-verified/report.json) xác minh WINWORD 32 bit, đạt 10/10; không thay đăng ký Office/WPS.

[Cách dùng](QUICKSTART.md). Web/Desktop hiện ở alpha G và được đóng gói riêng; đây chưa phải bộ cài toàn bộ Locus.
