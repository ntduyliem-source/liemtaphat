# Locus Design System · 0.2.1

Điểm vào để designer và developer tìm đúng nguồn đang chạy. Phạm vi phiên bản này: nền tảng Studio và Công thức; các tab vẽ còn ở khu CSS legacy riêng.

`0.2.1` là phiên bản catalog/tài liệu và pattern Công thức sau khi bỏ panel Chi tiết và nhãn loại gợi ý theo yêu cầu. Package `Locus.DesignSystem` chứa foundations vẫn ở `0.1.0`; thay đổi này không đổi API của package đó.

- Catalog local: [http://127.0.0.1:4203/](http://127.0.0.1:4203/).
- Web hiện hành: [mở trang Công thức](http://127.0.0.1:4202/releases/20261005-112656-216/).
- [Nguồn thiết kế / palette](SOURCES.md), [bảo trì / nâng cấp](MAINTENANCE.md), [changelog](CHANGELOG.md).
- [Baseline và hợp đồng hành vi](CT0-BASELINE.md), [kế hoạch](PLAN.md), [kiểm chứng CT0–CT3](CT0-CT3-VERIFICATION.md).
- [Kế hoạch CT4](CT4-PLAN.md), [kết quả kiểm và giới hạn CT4](CT4-VERIFICATION.md), [rà soát lõi trước sửa và backlog năng lực](CT4-CORE-AUDIT.md). Tài liệu baseline/kiểm chứng cũ giữ trạng thái lịch sử của chúng.

Địa chỉ chỉ hoạt động khi server local đang chạy. Build receipt: `artifacts/design-system/current-build.json`. Catalog là app riêng, không thêm tab vào giao diện người dùng. Không có kết nối bên ngoài hoặc font CDN.

## Chạy lại

```powershell
# Tại thư mục repo. Restore lần đầu/cập nhật project rồi giữ packages.lock.json.
dotnet restore src/Locus.Web
dotnet restore src/Locus.Catalog
pwsh -File tools/design-system/build.ps1
node tools/web1/local.mjs start --port 4202
node tools/design-system/serve.mjs 4203
```

`build.ps1` dùng renderer đã khóa trong `tools/sh`; nếu máy mới chưa cài dependencies, chạy `npm --prefix tools/sh ci --ignore-scripts` trước. Catalog dùng worker từ cùng build Web. Server catalog chỉ bind 127.0.0.1, không có service worker, không chiếm origin của sản phẩm. Đóng process để dừng catalog; dừng Web bằng `node tools/web1/local.mjs stop --port 4202` với đúng receipt/build.

Catalog gồm palette đọc từ CSS thật, typography, component/API lấy từ Parameter thật, 18 trạng thái Công thức và iframe tương tác với viewport rộng/hẹp. Mẫu tĩnh và preview có nhãn riêng; mẫu xuất chỉ minh họa trạng thái, preview dùng adapter xuất thật. CT4 đã có SVG/PNG cho đoạn chữ và nhiều công thức. Tài nguyên chữ dùng cho ảnh và cách tái tạo tại `src/Locus.Editor/wwwroot/formula/IMAGE-RESOURCES.md`.
