# CT0 — Baseline Công thức

Mốc: 05/10/2026, trước CT1–CT3. UI đang chạy: `20261003-115834-995`. Reference: `ui/locus_studio (2).html`. Yêu cầu mới nhất là triển khai CT0–CT3; **CT4 chờ bàn, không triển khai**.

Ảnh và thông số đo: `artifacts/design-system/ct0/formula-before.png`, `metrics.json`; bản nguồn UI trước refactor và SHA-256 của Core/Application/reference/interop ở cùng thư mục. Không dùng Git HEAD làm baseline vì workspace có các thay đổi đã được duyệt nhưng chưa commit.

## Inventory và chủ sở hữu

| Trước | Sau | Trách nhiệm |
| --- | --- | --- |
| Editor/studio-tokens.css và fonts | DesignSystem/wwwroot/styles/tokens.css, fonts | Một nguồn palette, semantic roles, type, spacing, motion, giấy phép font |
| Editor/studio.css: header | DesignSystem/Components/StudioHeader và styles/header.css | Thành phần header; vị trí tab, Undo/Redo, theme |
| Editor/studio.css + quy tắc Formula kế thừa editor/ux1.css | Editor/wwwroot/formula/*.css | Layout, detection, markers, panels, export, feedback, responsive; selector giới hạn `.formula-studio` |
| editor/web1/ux1/plot/geometry.css toàn cục | Editor/wwwroot/legacy/*.css | Giữ giao diện các tab vẽ; mọi selector trong `.legacy-editors` |
| Workspace.razor nguồn/kết quả/gợi ý/xuất/chi tiết | Editor/Formula/Components | UI nhận trạng thái và phát sự kiện; không cân bằng hoặc suy lại candidate |
| EditorShell chứa cả chrome | Web/WebShell + Desktop.Shared/DesktopShell | Điểm vào và layout host riêng; EditorShell giữ navigation guards và vòng đời workspace |
| Chưa có catalog | Locus.Catalog | Công cụ nội bộ local, origin riêng; component và tokens thật |

Web là website; Desktop là ứng dụng Windows cài độc lập, dùng WPF/WebView2 và tài nguyên đóng gói. Tái sử dụng thư viện không tạo phiên chung, không đồng bộ dữ liệu và không buộc Desktop chạy Web server. Đợt này chỉ build kiểm tương thích Desktop, không phát hành bộ cài mới.

## Hợp đồng trạng thái giữ nguyên

| Tình huống / điều khiển | Hành vi baseline CT0–CT3 |
| --- | --- |
| Nguồn trống / đang khởi động | Placeholder; nguồn readonly tới khi interop sẵn sàng; chưa xuất ảnh |
| Gõ tiếng Việt / IME | Input qua editor.js; đợi ghép dấu, debounce và lease hiện hành; không xử lý ký tự dở |
| ALL / Toán / Lý / Hóa | Đổi settings cho lần nhận diện tiếp; snapshot đang có vẫn giữ. Chưa làm CT-UX1 |
| MANUAL / cặp bọc | Hiện cặp chung và cặp theo môn. Cặp chung dùng domains đang bật. Validate trước lưu |
| Kết quả đơn | Chỉ xuất ảnh sau khi chọn trọn công thức; chưa tự chọn |
| Đoạn hỗn hợp | Render đúng ContentDocument; giữ chữ ngoài vùng công thức và offset nguyên văn |
| Click vùng / Enter / Space | Chọn vùng qua ID; tô chọn ngoài MathML. Không có fx trong nội dung |
| Chọn dở công thức | Chặn xuất sai phạm vi; giữ lệnh Chọn trọn công thức |
| Ý bạn là | Candidate thật, repair có nhãn riêng; ví dụ hiện khi chưa chọn và thay toàn nguồn, có Undo |
| Giữ text / Chi tiết | Giữ nguồn và lựa chọn; chi tiết ngoài kết quả xuất |
| Cân bằng không chọn | Điền sản phẩm duy nhất đã biết rồi cân bằng cả bài; nguồn không đổi |
| Hủy cân bằng | Hủy hệ số quản lý; giữ sản phẩm đã điền. Undo khôi phục cả giao dịch |
| Cân bằng vùng chọn | Giữ nguyên quy tắc chọn hiện có, kể cả bất cập phân loại vùng chờ của Lý |
| Xuất | Giữ nguyên nhãn, điều kiện enable/disable và adapter Web/Windows |
| Undo/Redo | Vẫn trên header; settle input trước history; không chuyển sang footer |
| Theme / hover | Giữ màu, chữ, kích thước và hiệu ứng đã duyệt; bổ sung nguồn tra cứu |
| Đổi tab | Giữ guard và session; không sửa editor đồ thị/hình học |

## Wireframe trách nhiệm

```text
WebShell / DesktopShell (mỗi sản phẩm sở hữu riêng)
┌ StudioHeader: brand | Công thức / Đồ thị / Hình học | Undo Redo Theme ┐
└────────────────────────────────────────────────────────────────────┘
  Hero (giữ nguyên)
┌ FormulaDetectionBar: ALL / Toán / Lý / Hóa / MANUAL | Cân bằng      ┐
│ MarkerSettingsEditor (khi MANUAL)                                  │
├ FormulaSourcePanel ──────────┬ FormulaResult ──────────────────────┤
│ Nguồn + ghost/IME            │ ContentDocument, MathML, source spans│
│ Số từ / trạng thái           │ FormulaSuggestions: Ý bạn là        │
├ FormulaExportBar: phạm vi | tải SVG/PNG hoặc DOCX/HTML/TXT | copy   ┤
└─────────────────────────────┴─────────────────────────────────────┘
  Status / toast / FormulaDetails / hỗ trợ Hóa / footer
```

CT4 dự kiến thảo luận tại các điểm: nhận diện lại snapshot khi đổi bộ lọc; chọn sẵn công thức đơn; nhãn phạm vi; ví dụ chèn tại con trỏ; xác định vùng Hóa chờ; quy tắc cặp chung MANUAL. Đây là chú thích kế hoạch, không phải acceptance của đợt refactor.

Không đưa lại fx, Tùy chọn & nháp, Nháp gần đây, Định dạng công thức đang chọn, Phím tắt, auto-balance checkbox hay khu Thử nhanh riêng.
