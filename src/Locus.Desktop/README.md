# Locus Desktop

WPF `net10.0-windows` dùng trực tiếp `Locus.Core`. Không gọi CLI/HTTP, không tham chiếu Office, không có parser riêng.

```powershell
./tools/build.ps1
dotnet run --project src/Locus.Desktop -c Release --no-build
./tools/publish-desktop.ps1
```

Script build chạy cả core tests và Desktop tests trên Windows. Script publish tạo gói Windows x64 self-contained; cần đóng phiên chạy từ thư mục gói trước khi ghi đè bản đang mở. Gói chưa ký số. Binary và ZIP được giữ trong `artifacts/releases/` và không commit vào Git.

- [Hướng dẫn người dùng](../../docs/m2/QUICKSTART.md)
- [Bằng chứng và giới hạn M2](../../docs/m2/REPORT.md)
- [Các mẫu SVG/PNG](../../artifacts/m2/gallery.html)

`EditorSession` thuộc một UI dispatcher; `LatestRequestGate` và revision chống publication cũ. Cấu hình nhận diện làm revision thay đổi; cấu hình ảnh thay scene, clipboard revalidate cả scene. Không chọn ngầm repair-only. `FormulaRenderer` chỉ nhận Candidate, không source text; scene giữ ID và geometry bất biến. File SVG có glyph paths, không cần font trên máy nhận; giới hạn 2.048 node, depth 64, cạnh 12.000 DIP và 24 triệu pixel cho bitmap.

Không dùng ứng dụng làm nơi phát lệnh Word. Nút trạng thái Word báo chưa có connector; quyền auto vẫn chưa được triển khai.

Nghiệm thu clipboard ngoài tiến trình là chế độ chạy riêng vì nó thay clipboard hệ thống và mở Word cho tài liệu thử. Trên baseline Office x86, dùng `C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tools/m2/verify-word-clipboard.ps1` sau build. Script từ chối khi Word đang chạy; không dùng tài liệu người dùng. `ExecutionPolicy` chỉ áp dụng tiến trình thử. Mỗi run ghi thư mục riêng dưới `artifacts/m2/clipboard`; chạy `tools/m2/verify-received-images.py <run-directory>` để so pixels. Các lần gọi Word đồng bộ có thể treo; `progress.json` ghi bước gần nhất. Bài này không chạy chung với thao tác clipboard bằng tay.
