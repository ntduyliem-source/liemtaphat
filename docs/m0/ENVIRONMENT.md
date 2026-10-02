# Môi trường đã quan sát trong M0

Ngày: 2026-09-12. Nguồn máy đọc được: [environment.json](../../artifacts/m0/environment.json). Chạy lại bằng `./tools/m0/environment.ps1` từ thư mục dự án. Script chỉ thu các thuộc tính cần cho thử nghiệm, không đọc nội dung tài liệu đang có.

| Thành phần | Quan sát được | Giới hạn |
| --- | --- | --- |
| Windows | Windows 10 Pro, 10.0.19045, 64-bit | Chưa thử Windows 11 hoặc các build khác |
| Word | WINWORD.EXE 16.0.14026.20302, Click-to-Run x86 | Chưa phải chứng nhận hỗ trợ mọi cấu hình Word 32-bit |
| Word trước khi thử | Không có tiến trình WINWORD đang chạy | Các tài liệu COM thử nghiệm do probe tạo riêng |
| Bộ gõ | UniKeyNT đang chạy; keyboard preload `00000409` | Chưa xác minh UniKey đang bật tiếng Việt, Telex/VNI hay tín hiệu composition thật |
| .NET Framework | Runtime báo 4.8.09037, Release 533325; reference assemblies v4.8 có sẵn | Probe host net48 không phải VSTO đã cài |
| VSTO runtime | Registry WOW6432Node báo 10.0.60910 | Chưa build/deploy add-in VSTO |
| .NET SDK | 7.0.203, 9.0.317, 10.0.400 | Prototype dùng SDK hiện có, chưa là quyết định mọi target production |
| Công cụ phụ trợ | Node 24.15.0, Python 3.11.3, Git executable có sẵn | Thư mục chưa được khởi tạo Git; bootstrap repo thuộc M1 |

## Availability Office.js

Theo [bảng requirement sets của Microsoft](https://learn.microsoft.com/en-us/javascript/api/requirement-sets/word/word-api-requirement-sets), WordApi 1.6 trên nhánh Windows Microsoft 365/retail bắt đầu từ build 16731.20234. Build đang có 14026.20302 thấp hơn mức này. `onParagraphChanged` thuộc [WordApi 1.6](https://learn.microsoft.com/en-us/javascript/api/requirement-sets/word/word-api-1-6-requirement-set).

**Suy luận để chọn spike:** không lấy event này làm nền tảng quan sát của connector chạy trên máy hiện tại. Điều đó không chứng minh mọi API Office.js đều không dùng được. `insertOoxml` nằm trong WordApi 1.1; runtime availability và native insertion qua Office.js vẫn chưa chạy trong M0 này.

Không nâng Office, đổi license hoặc sửa Trust Center trong đợt kiểm tra môi trường. Nếu sau này chọn baseline khác, cần chạy lại requirement checks ở runtime.

## Phát hiện COM bị phân tuyến theo bitness

Thử COM từ Windows PowerShell 64-bit ban đầu tạo tiến trình `wps`, trả `Application.Version=12.0`. Bộ kiểm tra ownership từ chối tiếp tục; không coi đây là kết quả Microsoft Word. Báo cáo bằng chứng: [ownership bị từ chối](../../artifacts/m0/word/run-20260912T045928410Z-02bb91/ownership.json).

Đọc registry cho thấy cùng CLSID Word `{000209FF-0000-0000-C000-000000000046}` nhưng `LocalServer32` ở view 64-bit trỏ WPS, còn view 32-bit trỏ `WINWORD.EXE` của Microsoft. `Word.Application.16` vẫn trỏ cùng CLSID, nên đổi tên ProgID không giải quyết trường hợp này.

Chạy probe bằng `C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe` đã tạo đúng WINWORD 16.0.14026.20302; PID, thời điểm khởi động và đường dẫn được ghi trong báo cáo. Đây là giải pháp cho môi trường thử hiện tại, không phải quy tắc mọi máy đều phải chạy x86. Không sửa registry hoặc cấu hình WPS/Office. Runner đã thêm bước đọc đăng ký trước activation và kiểm tra ứng dụng thực sau activation.

Điều kiện đưa vào connector: xác minh đúng Microsoft Word và kiến trúc của host, không tin riêng ProgID hoặc tên interface COM. [LocalServer32](https://learn.microsoft.com/en-us/windows/win32/com/localserver32) mô tả executable dùng cho COM server; [GetTypeFromCLSID](https://learn.microsoft.com/en-us/dotnet/api/system.type.gettypefromclsid) vẫn dựa vào đăng ký CLSID.

## Quan sát UI thực tế và hạn chế công cụ

Word được mở bằng Computer Use vào màn hình khởi động. Accessibility báo focus ở `HomePageSearchBox`; sau Ctrl+N, focus ở `Search for online templates`. Đây là ví dụ thực tế cho thấy cửa sổ Word đang foreground không có nghĩa con trỏ nằm trong nội dung tài liệu.

Phiên capture có lỗi `SetIsBorderRequired failed: No such interface supported (0x80004002)`. Đọc accessibility không ảnh vẫn hoạt động; thao tác click theo element báo `coordinate input geometry is unavailable`. Màn hình khởi động được đóng bằng Alt+F4 và xác minh không còn WINWORD trước bộ thử COM.

Hạn chế này thuộc công cụ quan sát trong phiên hiện tại, không phải kết luận Word hoặc connector Locus thiếu khả năng. Chưa có bằng chứng thao tác gõ tiếng Việt trực tiếp, thời điểm IME commit, tọa độ `fx` khi zoom/scroll/DPI hoặc đầy đủ focus Find/Ribbon/dialog. Những phần đó không được đánh dấu PASS từ các test dữ liệu mô phỏng.

## Ma trận còn cần mở rộng

| Tổ hợp/tình huống | Trạng thái |
| --- | --- |
| Build Word đang có, native qua COM trên tài liệu tổng hợp | Kết quả riêng trong báo cáo Word sau khi chạy probe |
| Office.js sideload và requirement check thật | UNTESTED |
| VSTO lifecycle khi Word/Desktop mở theo hai thứ tự | UNTESTED |
| UniKey Telex/VNI với phím gõ thật, sửa dấu và Backspace | UNTESTED |
| Nhiều cửa sổ, Find/Ribbon/dialog, IME composition | Chỉ quan sát được một phần startup; chưa đủ cổng tự động |
| Word 64-bit, Windows 11, Word build mới hơn | UNTESTED |
| Track Changes, protected/read-only, bảng/header/footer | Cần phân biệt từng tổ hợp trong báo cáo probe; không tự hỗ trợ mặc định |
| Cold offline startup và reconnect add-in đã cài | UNTESTED |
