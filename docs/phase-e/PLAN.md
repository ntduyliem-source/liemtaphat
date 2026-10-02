# Đợt E — Word thủ công và gói local

Phạm vi được triển khai theo yêu cầu ngày 2026-09-15: SC1-09 rồi SC1-10. Bản D vẫn giữ nghiệm thu host riêng. Không mở rộng sang quét tài liệu WD1, ghost trong Word hay auto-Space.

1. Dùng core định tuyến bốn cặp chung/Toán/Lý/Hóa; chuyển cài đặt Word cũ mà giữ nguyên cặp người dùng. Cặp trùng preset mới làm preset đó tạm tắt, không sửa cặp cũ.
2. Bảng xem trước Word dùng candidate trực tiếp, các phương án fx và hỗ trợ riêng. Cân bằng chỉ thay kết quả. Nhận sản phẩm phải chọn điều kiện, xem toàn phương trình rồi bấm nhận. Có hủy cân bằng và hủy bổ sung sản phẩm riêng.
3. Metadata Word v2 chứa nguồn, cách đọc hoặc bản nháp phản ứng, kết quả, snapshot trước cân bằng, proposal/điều kiện/nguồn tham khảo và dấu bỏ auto. Đọc v1 nguyên trạng. Mở lại không chạy lại parser hoặc solver.
4. Chốt snapshot vừa xem trước khi xếp lệnh. Kiểm phiên, settings, source, selection, tài liệu, focus, composition và snapshot ngay trước ghi. Công thức đã sửa native bị từ chối dùng lịch sử cũ.
5. Chèn/cập nhật/khôi phục native trong một Undo. Kiểm fault giữa các bước, Unicode ngoài vùng, metadata cũ, sửa đề nghị, save/reopen và native vẫn hiện khi tắt add-in.
6. Build Web/Worker/Desktop/Word cùng source; gói review bất biến với manifest/hash và kiểm sau giải nén. Chỉ đóng SC1-10 khi các bài host SC1-08/D và chạy gói thực tế đã đạt; ZIP thành công không tự là nghiệm thu.

Các phép tạo trạng thái sản phẩm trước cân bằng đã đặt trong `Locus.Core.Assistance.ChemistryProjection`, cả Application và Word gọi cùng hàm. Word .NET Framework 4.8 không phụ thuộc Application .NET 10; không tạo parser hoặc solver thứ hai.
