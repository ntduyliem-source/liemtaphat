# Bảo trì và nâng cấp

| Muốn sửa | Nơi sửa đầu tiên | Kiểm cần làm |
| --- | --- | --- |
| Palette, chữ, spacing dùng chung | DesignSystem/wwwroot/styles/tokens.css | Catalog cả hai theme; các selector ở Nguồn & nơi dùng |
| Button/icon/field/checkbox/select/toast | DesignSystem/Components + styles/controls.css | Component thật, hover/active/focus/disabled; chỗ dùng trên Formula |
| Header | StudioHeader + styles/header.css | Vị trí Undo/Redo; Web rộng/hẹp; Desktop build |
| Panel nguồn / kết quả / gợi ý / xuất | Editor/Formula/Components + wwwroot/formula | Pattern tương ứng và preview; offset/IME/selection khi đổi DOM |
| Cân bằng, candidate, nhận diện | Core + Application; Workspace chỉ điều phối | CT4-PLAN và contract tests; không suy trạng thái từ nhãn nút |
| Selection/phạm vi thao tác | ResultSelectionState + Workspace.Content + result-selection.js | All → Region, kéo ngược, UTF-16, partial, version/epoch |
| Ảnh đoạn chữ + công thức | ContentExport.Parts → Workspace.ResultImage → result-image.js/renderer.js | SVG độc lập, PNG 2X/4X, clipboard thật, thay input lúc xuất, trần ảnh |
| Glyph chữ dùng cho ảnh | IMAGE-RESOURCES.md + tools/design-system/export-font.py | Đọc nguồn/giấy phép/hash; tái tạo inter-outline.json; chữ Việt/emoji |
| Web shell / trình duyệt | Locus.Web + BrowserTransfers | Web release riêng; không suy kết quả Windows |
| Windows shell/native | Locus.Desktop.Shared | Build + kiểm app Windows và gói cài riêng |

## Quy trình thay đổi

1. Tra SOURCES và hợp đồng trạng thái. Xác định sửa thiết kế, UI hay nghiệp vụ.
2. Sửa nguồn chuẩn. Không thêm style inline/HEX vào component để chữa chồng CSS; màu nội dung tài liệu là ngoại lệ có chủ đích.
3. Khi thêm component, thêm vào CatalogIndex, mẫu thao tác và trạng thái cần thiết. Catalog phải dùng đúng type sản phẩm; API lấy từ Parameter, palette lấy từ CSS.
4. Với token đổi tên/xóa: tìm consumer bằng `rg`, chuyển toàn bộ và ghi mapping trong changelog. Không giữ hai palette cạnh nhau. Font mới phải có tài nguyên local và giấy phép.
5. Chạy `node tools/design-system/check.mjs`, build Web/Catalog và Desktop compatibility. Chạy test/ca UI theo phần thay đổi, không lặp toàn bộ bộ kiểm không liên quan.
6. Ghi build ID, ảnh, kết quả và giới hạn trong verification; tăng phiên bản thư viện/catalog cùng changelog. Publish từng sản phẩm sau nghiệm thu của sản phẩm đó.

## Ranh giới phải giữ

- `legacy.css` chỉ import stylesheet đã scope `.legacy-editors`. Không thêm selector global từ tab vẽ. Formula không phụ thuộc legacy.
- `formula.css` chỉ import CSS Công thức, consumer của controls/token. Header và primitives không được định nghĩa thêm lần nữa ở đây.
- `foundations.css` là reset toàn cục tối thiểu. `shell.css` thuộc host, không dùng để vá nghiệp vụ/component.
- `editor.js`, `result-selection.js` cần `.locus-app`, source/result IDs, data-source-start/end, data-text-start, revision/version và span trực tiếp trong kết quả. Thay DOM phải kiểm chọn trọn/chọn dở và source mapping.
- Textarea được wire qua ElementReference từ FormulaSourcePanel; không thêm handler input song song hoặc làm mất chuỗi sự kiện IME.
- Không lấy fixture catalog làm nội dung mặc định sản phẩm. Phiên catalog khác origin; không truy cập kho dữ liệu đang dùng trên Web.
- Đổi mode có chủ đích dùng ReanalyzeWithSettingsAsync; mở lịch sử không gọi reparse ngầm. Lệnh Hóa chụp version + danh sách ID rồi kiểm lại lúc commit; hủy hệ số và hủy tự điền là hai action riêng.
- Phần xuất ảnh lấy dữ liệu từ snapshot ContentExport bất biến, không đọc innerText hay chụp DOM kết quả. Renderer không được thêm viền chọn, nút hoặc nhãn UI vào ảnh.

## Phát triển local

`tools/design-system/build.ps1` tạo Web release và catalog có worker cùng build. `-SkipWebBuild` chỉ dùng khi đã biết build hiện tại chứa đúng Editor/worker; bình thường build lại toàn bộ đầu ra chịu ảnh hưởng. `packages.lock.json` phải được cập nhật có chủ đích khi đổi project reference.

`tools/design-system/check.mjs` kiểm ownership CSS, reference đã chuyển và DOM contract cơ bản. Đây không thay kiểm browser hay kiểm native. `CT4-VERIFICATION.md` ghi kết quả hiện hành; `CT0-CT3-VERIFICATION.md` giữ mốc lịch sử.
