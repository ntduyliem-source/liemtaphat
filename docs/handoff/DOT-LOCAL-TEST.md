# Locus — mã nguồn để kiểm tra trên máy riêng

Đây là snapshot mã nguồn của G alpha local, sau sửa ngày 30/09/2026: vùng **Kết quả** trên Web/Desktop không chứa nút fx; chọn một công thức để mở **Chi tiết công thức** ở bên dưới. Source, test, corpus, fixture, prototype, tài liệu và script build nằm trong ZIP. File build, cache, hồ sơ trình duyệt, ảnh và tài liệu Word thử của máy gửi không nằm trong ZIP.

## Dựng Web và Desktop từ source

Máy kiểm nên dùng Windows x64, PowerShell 7, .NET SDK 10.0.400, Node.js 22+ và Edge WebView2 Runtime. Lần restore đầu cần tải các gói NuGet/npm theo lockfile. Mở PowerShell ở thư mục vừa giải nén, rồi chạy:

```powershell
npm --prefix tools/sh ci --ignore-scripts
node tools/web1/vendor.mjs
New-Item -ItemType Directory -Force artifacts/phase-g | Out-Null
./tools/phase-g/build.ps1
./tools/phase-g/local.ps1 -Port 4193
```

Lệnh cuối mở server Web local ở `http://127.0.0.1:4193/`; giữ terminal đó nếu server chưa tự tách tiến trình. Trong PowerShell khác, tại cùng thư mục, chạy `./tools/phase-g/desktop.ps1` để mở Desktop. Nếu cổng 4193 đã dùng, đổi thành một cổng trống như 4293. Dừng server bằng `./tools/phase-g/local.ps1 -Action stop -Port 4193` với đúng cổng đã mở.

## Kiểm nhanh logic

Chạy các lệnh sau từ thư mục giải nén. Mỗi bộ kiểm tạo báo cáo JSON dưới `artifacts/qa`:

```powershell
dotnet run --project tests/Locus.Core.Tests -c Release -- --root (Get-Location).Path
dotnet run --project tests/Locus.Application.Tests -c Release -- --content-only artifacts/qa/content
dotnet run --project tests/Locus.Application.Tests -c Release -- --balance-only artifacts/qa/balance
dotnet run --project tests/Locus.Application.Tests -c Release -- --plot-only artifacts/qa/plot
dotnet run --project tests/Locus.Application.Tests -c Release -- --geometry-only artifacts/qa/geometry
```

Trên UI, thử `x mũ 2 + 1`: nội dung dựng đúng, không có fx trong vùng kết quả; chọn công thức thì thấy **Chi tiết công thức** ở dưới và có thể đưa vùng về text rồi Undo. Dán đoạn có văn bản và công thức để kiểm phần văn bản được giữ nguyên. Trong Đồ thị, kéo slider rồi Undo; ở Hình học 2D, kéo điểm vào trung điểm và thả; ở 3D, kéo đỉnh theo mặt phẳng XZ. Có thể lưu `.locus`, mở lại và so sánh scene.

Connector Word thuộc build riêng `src/Locus.Word` và `tests/Locus.Word.Tests`; kiểm trên Windows có Microsoft Word x86 và Office interop assemblies. Dựng bằng `./tools/phase-f/build.ps1` khi đủ điều kiện. Đăng ký add-in thay đổi môi trường Word, nên chỉ làm trong máy thử dành cho Word theo [hướng dẫn Word](../phase-f/QUICKSTART.md). Bản fx cạnh công thức trong Word vẫn là thử nghiệm và tắt mặc định.

## Phạm vi và báo lỗi

[Báo cáo alpha G](../phase-g/REPORT.md) và [biên bản ngày 30/09](../host-review/20260930.md) giải thích tính năng/giới hạn. Một số đường dẫn tới `artifacts/` trong các tài liệu này là bằng chứng ở máy gửi, không có trong ZIP. Mọi `artifacts/` ở máy kiểm sẽ được tạo mới. Chưa kiểm trọn đường tải tệp Web bằng in-app browser, các tình huống IME đang ghép khi ẩn/thoát và Word nhiều DPI/màn hình; đừng dùng các bài đã qua để suy ra chúng đã đạt.

Khi báo lỗi, gửi chuỗi nguồn, checkbox nhận diện, các bước thao tác, kết quả mong đợi/thực tế, phiên bản Windows/Word nếu liên quan và báo cáo JSON vừa tạo. Không cần gửi tài liệu cá nhân hay profile trình duyệt.
