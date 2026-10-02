# Sửa dữ liệu kỳ vọng M0 khi tích hợp M1

2026-09-12: M0-105 và M0-116 bị thiếu chẩn đoán `SUGGEST_FRACTION_SCOPE` mức `info`, span `[0,5)` trên candidate repair của `x+1/2`. M0-004 đã đặc tả cùng repair với chẩn đoán này. Hai case về cách mở `fx` được helper M0 soạn thiếu trường dữ liệu đó.

Đã bổ sung đúng chẩn đoán theo M0-004, sau review độc lập. Không thay nguồn, AST, loại/thứ tự candidate, repair edits, vùng thay thế hoặc quyền auto. Trạng thái UI không được cắt bỏ metadata của cùng kết quả core. Test vẫn so chính xác chẩn đoán thay vì bỏ kiểm tra hoặc cho phép mọi sai khác.

M0-099 là dữ liệu snapshot lịch sử có chẩn đoán rỗng và được giữ nguyên; test serialization đọc bộ kết quả đã lưu, không parse lại để thay lịch sử bằng kết quả hiện tại.
