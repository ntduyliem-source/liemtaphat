# M0 shared assembly và IPC probe

Đây là thử nghiệm kiến trúc với fixture `x mũ 2`, không phải parser hoặc ứng dụng Locus.

- `Shared`: một thư viện `netstandard2.0`.
- `ModernHost`: tiến trình `net10.0`, đại diện khả năng gọi từ runtime Desktop hiện đại.
- `FrameworkHost`: tiến trình `net48` x86, đại diện khả năng gọi từ runtime .NET Framework của hướng VSTO.
- Cả hai dùng cùng DLL; script so SHA-256 của các bản DLL thực sự chạy.

Từ thư mục gốc dự án:

```powershell
./tools/m0/shared_core_probe.ps1
./tools/m0/ipc_probe.ps1
```

Kết quả tại [shared-core.json](../../artifacts/m0/shared-core.json) và [ipc.json](../../artifacts/m0/ipc.json).

Probe đầu cho cùng cấu trúc từ nguồn NFC/NFD nhưng giữ nguyên hai chuỗi nguồn khác nhau. Probe IPC dùng named pipe cục bộ giữa hai tiến trình thực, gửi fixture, từ chối các thông điệp không khớp và khởi động lại server với phiên mới. Script chỉ dừng tiến trình server do chính nó tạo.

Focus, composition, revision và cấu hình trong thông điệp IPC là **giá trị tổng hợp để thử định tuyến và từ chối**, không phải tín hiệu đọc từ Word. Không có thao tác ghi Word trong probe này. Giao thức phân tách ký tự là giao thức thí nghiệm; M1/M3 cần hợp đồng versioned, framing/size limits, cancellation và kiểm soát truy cập pipe phù hợp production.

Build lần đầu có thể cần restore gói tham chiếu. Hai chương trình đã build chỉ chạy fixture cục bộ; không gọi dịch vụ mạng. Điều này chưa chứng minh cài đặt/khởi động lạnh offline của toàn ứng dụng.

Microsoft hướng dẫn dùng .NET Standard 2.0 khi cần chia sẻ thư viện giữa .NET Framework và .NET hiện đại: [.NET Standard](https://learn.microsoft.com/en-us/dotnet/standard/net-standard). Việc chọn target production được ghi trong quyết định kiến trúc sau khi tổng hợp Word probe.
