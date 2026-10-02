# W0 — Những việc cần làm khi có thời gian

Cập nhật: 2026-09-13. Người dùng đang bận và yêu cầu tập hợp việc cần làm để báo lại. **Chưa ghi nhận một bài người dùng tự thử nào; không có lịch nhắc tự động.**

W0 vẫn **4/6 hạng mục nghiệm thu, khoảng 67%**. Con số tính theo hạng mục đã đóng, không ước lượng thời gian hoặc tỷ lệ dòng mã. Phần triển khai của hai hạng mục cuối đã có bản chạy được; chúng chưa được đánh dấu DONE do còn các bước dưới đây.

## Checklist của bro

Mở lại bằng `./tools/w0/prepare-trial.ps1` khi Word đã đóng. Dùng tab **Locus W0**. [Hướng dẫn từng bước](TRIAL-GUIDE.md).

- [ ] **Nhìn và bấm fx:** công thức không bị che; dấu bám đúng khi cuộn đi/về và zoom 100% → 150% → 80%; bấm dấu mở được trạng thái.
- [ ] **Tự gõ A/B:** với mỗi biến thể, thử `x mũ 2 cộng 1`, `1 trên 2`, `căn x cộng 1`, có Space cuối. Ghi chỗ mất chữ, tách ngoài ý muốn hoặc khó sửa, nếu có.
- [ ] **Chốt, Undo/Redo và viết tiếp câu văn:** xác nhận kết quả dễ hiểu và phần ngoài công thức được giữ nguyên.
- [ ] **Chọn hướng nhập nối D-01:** A giữ nguồn đến khi chốt, hoặc B cập nhật native sau Space và vẫn giữ cùng phiên nguồn. Ghi cách kết thúc mong muốn; bản thử hiện có nút Chốt/Giữ văn bản, Enter/Esc dừng phiên mà không chốt thêm.

Không cần gửi tài liệu cá nhân. Có thể báo ngắn theo mẫu: “fx: …; A: …; B: …; Undo/viết tiếp: …; chọn … vì …”. Các ô chỉ được đánh dấu sau phản hồi thực tế.

## Việc còn lại của Codex

- [ ] Ghi lại phản hồi nguyên ý, tách quan sát của người dùng khỏi phép thử tự động; sửa lỗi nào được phát hiện và chạy hồi quy phù hợp.
- [ ] Thử hiển thị trên DPI/màn hình thực khác khi có môi trường. Các bài tính tọa độ ở DPI 120/144/192 hiện là mô phỏng; lần chạy Word thực là DPI 96.
- [ ] Chốt D-01 và cập nhật hợp đồng tiếp tục/kết thúc/Undo; chỉ đóng W0-05/06 khi đủ bằng chứng theo phạm vi đã thống nhất.

## Phần đã tự kiểm

Đã có COM add-in, core chung, native/metadata/Undo/restore/detach, lifecycle/reconnect, focus/Telex/VNI và clipboard. Đợt này thêm badge gắn đúng document/window/source, clipping khi cuộn, hai phiên nhập A/B, quyền chọn repair, khôi phục toàn nguồn, bảo vệ sau Undo và sau hủy đóng tài liệu. [Số kiểm tra và chỉ mục bằng chứng](REPORT.md), [kết quả Space](SPACE-NATIVE.md).

Các phép thử Windows do Codex thực hiện không được tính là phản hồi người dùng. Capture vẫn lỗi `SetIsBorderRequired 0x80004002`; không có chứng nhận trực quan hoặc nhiều màn hình từ các artifact tọa độ.

## Các mốc sau W0

| Mốc | Việc tiếp theo |
| --- | --- |
| M3 | Luồng sản phẩm chọn vùng → preview/chọn kết quả → xác nhận → native, metadata và restore/detach |
| M4 | Quan sát khi gõ, fx và chỉnh sửa qua Desktop |
| M5A | Tự chuyển vùng `lc[...]` mặc định hoặc cặp bọc tùy chỉnh, với kiểm tra lại trước ghi |
| M5B | Auto-Space theo D-01 sau khi đạt điều kiện bộ gõ/focus và các cổng liên quan |
| M6 | Bộ cài/cập nhật, máy sạch, hiệu năng, pilot cả ba nhóm và phát hành |

CW đã mở cho M3 thủ công trên baseline Word x86; G1/G2/G3 chưa đạt. Việc có prototype B không tự bật auto sản phẩm. Đồ thị, hình học 2D/3D và Lý/Hóa vẫn ở nhánh mở rộng sau nền sản phẩm theo [roadmap](../ROADMAP.md).
