# F — Quét công thức trong tài liệu Word

Bắt đầu từ E `20260916-052303-165`. Phạm vi WD1-01…05: lệnh thủ công trên Word x86, thân tài liệu. Core/parser/solver và metadata native v2 được dùng lại. D/SC1-08, W0 và auto-Space giữ trạng thái riêng.

1. Quét vùng chọn hoặc thân tài liệu, tối đa 100.000 vị trí Word và 64 vùng mỗi lần. Chỉ đọc. Bỏ qua bảng, field, hình và cấu trúc không hỗ trợ; hiện thông báo giới hạn. Phân tích từng đoạn/gap bằng core; ánh xạ UTF-16 sang Range qua nội dung Word, kiểm nguồn hai chiều.
2. Bảng điều hướng hiện nguồn, trạng thái, preview và tối đa ba cách đọc. Chỉ direct đơn nghĩa không cảnh báo hoặc lựa chọn đã xác nhận được vào batch. Không gọi solver/suy sản phẩm. Dấu fx cạnh vùng đang chọn là tùy chọn thử nghiệm; bảng luôn dùng được khi inline không có tọa độ.
3. Chuyển một vùng hoặc “Chuyển N vùng đã nhận diện” trong phạm vi quét. Freeze snapshot preview và toàn kế hoạch; revalidate document/window/settings/selection/IME/source, ghi từ cuối lên đầu trong một Undo. Lỗi hoặc hủy giữa các bước rollback cả nhóm.
4. “Về text, giữ nhận diện” và “Giữ text, bỏ qua” dùng rich-text content control với metadata `locus:text:1:` chứa snapshot, checksum, ID và quyết định. Quét không tạo control; chỉ lệnh người dùng mới ghi. Lưu/mở lại đọc snapshot, không tự parse lại vùng quản lý. Sửa native/text, trùng ID hoặc metadata hỏng hiện stale, không ghi đè.
5. Kiểm model/panel, range Unicode, tài liệu trộn, nhiều công thức giống nhau, Undo/Redo, lỗi giữa batch, selection/settings/source stale, ignore/rescan/save/reopen. Lưu bằng chứng native và phạm vi công cụ Windows riêng; không tuyên bố inline đa DPI đạt từ test model.

Các hạn chế ban đầu được hiện ngay trong panel: thân tài liệu ngoài bảng, tối đa 64 vùng; đoạn dài hơn 4.096 UTF-16 bỏ qua. Bản đầu dùng quét tường minh; không tự thay tài liệu khi gõ hay khi mở file.
