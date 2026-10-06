# Changelog

## GitHub Pages — 06/10/2026

- Thêm workflow build và deploy Locus Web từ `main` lên GitHub Pages công khai.
- Stage release nhận base path `/liemtaphat/`; service worker, cache offline và các release bất biến dùng đúng project path.
- Build local tiếp tục mặc định ở `/`; GitHub Pages tạo `.nojekyll` để phục vụ tài nguyên Blazor bắt đầu bằng dấu gạch dưới.

## 0.2.1 — 05/10/2026 · Gọn hàng gợi ý

- Theo yêu cầu mới, bỏ nút và panel Chi tiết khỏi Công thức, cùng state/callback/CSS không còn dùng và mục tương ứng trong catalog.
- Ô “Ý bạn là” chỉ hiển thị công thức, bỏ nhãn “Đề nghị sửa” / “Cách hiểu khác”. Giữ giải thích trong tooltip/nhãn truy cập; loại candidate và quy tắc không tự nhận repair vẫn giữ ở lõi.
- Giữ chọn gợi ý, trở về cú pháp gốc, Giữ text, Undo/Redo và Hủy tự điền ở panel Hóa.
- Đây là thay đổi sau mốc CT4-VERIFICATION.md; panel Chi tiết mô tả trong biên bản đó là hành vi lịch sử của 0.2.0.
- Build Web `20261005-112656-216`; Editor DOM và design-system check PASS, Desktop compatibility build 0 lỗi/0 cảnh báo. Kiểm browser: nhận `(x^2+1)/2`, Undo trả về direct; không có nút/panel Chi tiết hoặc nhãn loại trong ô gợi ý.

## 0.2.0 — 05/10/2026 · CT4

- Thống nhất selection None/Region/Range/All; Chọn tất cả, phím và bôi vùng dùng cùng trạng thái.
- Nút Cân bằng phương trình hóa học chỉ hoạt động All/Hóa; phân biệt đổi hệ số và tự điền sản phẩm, hủy đúng provenance, có kết quả theo vùng.
- Thêm repair cấu trúc phân số/căn/ngoặc và sửa nhận nhầm phép trừ/chia thành đường dẫn; giữ snapshot cũ.
- “Ý bạn là” theo nội dung thật, đổi môn nhận diện lại với một bước Undo.
- Dựng SVG/PNG/clipboard từ snapshot đoạn hỗn hợp; glyph Việt local, nền trong suốt, 2X/4X, trần PNG rõ ràng. Bỏ toolbar HTML/TXT và coordinator xuất cũ không dùng.
- Catalog cập nhật 18 trạng thái, mô tả API và tài liệu bảo trì; không đổi typography/vị trí Undo/Redo hoặc phát hành Desktop mới.

Build, bằng chứng và giới hạn: [CT4-VERIFICATION.md](CT4-VERIFICATION.md).

## 0.1.0 — 05/10/2026 · CT0–CT3

- Khóa baseline và hành vi Công thức; giữ CT4 để thảo luận riêng.
- Tạo Locus.DesignSystem: palette/semantic tokens, typography, local fonts, controls, icon/header/toast.
- Tạo catalog local dùng thư viện thật, tra token/consumer/API, mẫu trạng thái và preview responsive độc lập.
- Tách nguồn, kết quả, gợi ý, detection/balance, export, marker settings và chi tiết khỏi Workspace. Lệnh/phiên vẫn do Application và coordinator hiện hành quản lý.
- WebShell và DesktopShell có entry/layout host riêng. CSS legacy của Đồ thị/Hình học được giới hạn phạm vi.
- Tên `--studio-*` giữ tương thích trong phần đã chuyển; thêm lớp `--palette-*`. Không tạo aliases token mới cho Công thức ngoài nguồn chuẩn.
- Di chuyển `StudioIcon`, `StudioNotice`, fonts/theme từ Locus.Editor sang Locus.DesignSystem. Host phải load foundations.css và theme.js từ đường dẫn mới; các release cũ bất biến vẫn giữ asset riêng.

Kiểm chứng và giới hạn: xem CT0-CT3-VERIFICATION.md. Không coi build tương thích Desktop là bản cài mới hoặc nghiệm thu Windows.
