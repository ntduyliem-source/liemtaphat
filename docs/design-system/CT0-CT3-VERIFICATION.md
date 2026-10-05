# CT0–CT3 — Kiểm chứng local

Ngày 05/10/2026. **CT0–CT3 đạt phạm vi refactor và catalog local. CT4 chưa triển khai.**

Web release: `20261005-083635-640`, SDK `10.0.400`. Catalog `0.1.0`, publish cuối `2026-10-05T08:43:09Z`, dùng Editor/worker cùng release Web. Receipt: `artifacts/design-system/current-build.json`.

- Web: http://127.0.0.1:4200/
- Catalog: http://127.0.0.1:4199/
- Bản Web cũ ở 4197 và tài liệu người dùng trong origin cũ không bị thay. Origin 4200 được dùng để kiểm release mới độc lập với cache bản trước.

## Kết quả theo đợt

| Đợt | Đầu ra và bằng chứng |
| --- | --- |
| CT0 | Inventory, bảng trạng thái, wireframe trách nhiệm và danh sách CT4 hoãn trong CT0-BASELINE.md. Lưu ảnh/thông số và SHA-256 nguồn trước khi sửa. |
| CT1 | Thư viện DesignSystem có palette/roles/type/spacing/motion/font local; catalog đọc token và consumer thật, tìm kiếm và sáng/tối hoạt động. |
| CT2 | 7 phần Công thức tách khỏi Workspace: detection/balance, source, result, suggestions, export, details, markers. Primitives thật có API/trạng thái trong catalog. DOM bridge được kiểm riêng. |
| CT3 | WebShell/DesktopShell riêng; Formula có bộ CSS riêng; CSS tab vẽ đều có boundary legacy. Web/Catalog publish thành công; Desktop build tương thích thành công. |

## Kiểm tự động đã chạy

| Kiểm | Kết quả |
| --- | --- |
| `dotnet build src/Locus.Web` | Qua, 0 lỗi/0 cảnh báo |
| `dotnet build src/Locus.Catalog` | Qua, 0 lỗi/0 cảnh báo |
| `dotnet build src/Locus.Desktop.Shared --no-restore` | Qua, 0 lỗi/0 cảnh báo; chỉ xác nhận tương thích build |
| `tools/design-system/build.ps1` | Publish Web/worker/catalog và stage release thành công |
| `node tools/design-system/check.mjs` | Qua: token hợp lệ, không rải palette vào CSS component, legacy selectors có boundary, host references và selection hooks |
| `tests/Locus.Editor.Tests` | Qua: offset UTF-16/emoji, chữ và xuống dòng ngoài công thức, MathML đúng candidate, vùng chọn, raw HTML được escape, busy/IME không hiện công thức cũ, export branch và source/ghost DOM hooks |
| Application `--content-only` | 22/22 |
| Application `--studio-balance-only` | 16/16 |
| Hash Core/Application/reference HTML | 60 tệp không đổi so với đầu CT0; `artifacts/design-system/ct0/frozen-source-check.json` |

Các thay đổi Application đang có trong Git trước CT0 được giữ nguyên; không tính chúng là sửa nghiệp vụ của đợt này.

## Dùng thật trong in-app browser

- `x mũ 2 + 1`: dựng đúng; nút ảnh chỉ bật sau chọn vùng, đúng baseline.
- `x+1/2`: trực tiếp và đề nghị sửa phân biệt; chọn repair dựng phân số khác nhưng giữ nguyên nguồn; Undo trên header hoạt động.
- `H2+O2=`: Cân bằng toàn bài điền nước và dựng `2H₂ + O₂ → 2H₂O`; Hủy trả hệ số nhưng giữ nước; Undo hai bước quay về trước hai lệnh. Chọn riêng vùng Hóa đổi nhãn sang “phương trình này”.
- Đoạn văn có emoji, xuống dòng, cặp Toán và Hóa: giữ nguyên văn bản, nhận hai vùng; selection, source offsets và xuất toàn bài hoạt động; nội dung kết quả không chứa button/SVG điều khiển/fx.
- MANUAL: nhập dấu mở trống bằng phím thật, Lưu báo lỗi và giữ settings; nhập lại `lc[` rồi Tab/Lưu thành công. Đây là kiểm binding form, không phải nghiệm thu bộ gõ Windows.
- Hai theme của Formula và catalog; input 14px, pane 520px, hero 2.7rem giữ mốc. Compact iframe 390px: hero 28,8px, một cột, body không tràn ngang; preview có cả khung 720/1280px.
- Catalog: tìm token, palette tính tương phản, xem nguồn/consumer, API component thật; fixture repair, IME và Hóa sau cân bằng đã mở. IME fixture không còn candidate action cũ; controls phụ thuộc trạng thái bị khóa.
- Smoke tab vẽ sau khi scope CSS: tạo `x^2` dựng đường SVG, nút xuất bật; đặt điểm A trên 2D và mở công cụ/camera 3D. Không sửa logic editor vẽ.

Ảnh: `artifacts/design-system/ct0-ct3/formula-light.png`, `formula-dark.png`, `catalog.png`; baseline tại `artifacts/design-system/ct0/`. Tệp build/ảnh không đưa vào Git; có thể tạo lại bằng script và checklist.

## Giới hạn và việc để sau

Đây không phải nghiệm thu đầy đủ CT5: chưa xác nhận tệp download/clipboard thực nhận trên mọi browser. Không công bố kiểm Windows IME/tray/native clipboard hoặc bộ cài mới từ kết quả Web. Không chỉnh CT4: tự reanalyze khi đổi bộ lọc, tự chọn công thức đơn, nhãn phạm vi mới, chèn ví dụ tại con trỏ, nhận diện đối tượng Hóa chờ và quy tắc MANUAL vẫn chờ bàn.

Màu được giữ theo mẫu; catalog hiển thị tương phản thực, không tuyên bố toàn palette đạt AA cho mọi cặp chữ/nền. Tab vẽ vẫn dùng thiết kế legacy có giới hạn phạm vi, chưa chuyển sang Studio design system.
