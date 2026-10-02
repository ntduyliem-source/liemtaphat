# Báo cáo thực hiện M0 — Locus

Ngày hoàn tất nghiên cứu: **2026-09-12**. Người thực hiện: Codex, với các nhánh độc lập về hợp đồng/corpus, UX nhập liệu và probe Word. Các thao tác Word do một runner điều phối tuần tự trên tài liệu tổng hợp do đợt thử tạo.

**Báo cáo lịch sử M0.** Core M1 hiện đã triển khai: [kết quả M1](../m1/REPORT.md). Các bằng chứng và giới hạn Word trong báo cáo này vẫn được giữ nguyên; trạng thái task hiện tại theo [backlog](../BACKLOG.md).

**Kết luận:** M0 đã đủ đầu ra để bắt đầu M1 core. Đã có đường dựng công thức Word native và một hướng metadata đáng triển khai tiếp; **chưa đủ bằng chứng để mở cổng ghi Word cho sản phẩm hoặc bật tự động**. M0 hoàn thành nghiên cứu với các hạn chế được ghi cụ thể, không phải hoàn thành ứng dụng.

## 1. Đầu ra có thể xem và chạy lại

| Đầu ra | Kết quả và giới hạn |
| --- | --- |
| [Grammar](GRAMMAR.md), [hợp đồng core](CORE-CONTRACT.md), [quyết định](DECISIONS.md) | Đủ baseline kỹ thuật cho M1; D-01 Space còn mở |
| [Corpus 117 case](../../corpus/m0/README.md) | 109 đã đặc tả, 8 pending; validator schema/span/policy đạt, 15 bản sao cố ý làm hỏng bị từ chối. Chưa chạy parser thật |
| [Demo cặp bọc](../../prototypes/m0-space/markers.html), [bài thử](MARKER-EXPERIMENT.md) | Mặc định `lc[...]`, tùy chỉnh hai dấu; chuyển vùng đủ điều kiện, `fx`, restore và hủy tác vụ cũ; 19 test state machine đạt |
| [Demo Space](../../prototypes/m0-space/index.html), [bài thử](SPACE-EXPERIMENT.md) | Hai chính sách nhập nối tiếp; 20 test đạt; chưa chọn UX cuối hoặc chứng minh native Space |
| [Shared core/IPC](../../prototypes/m0-shared-core/README.md) | Cùng DLL chạy trên .NET 10 và .NET Framework 4.8 x86; 21 assertion IPC đạt giữa các tiến trình thật. Các cờ Word trong thông điệp là tổng hợp |
| [Word fixtures và runner](../../fixtures/m0/word/README.md) | 20 mẫu native qua COM, thử transaction/metadata/stale; kết quả chi tiết ở dưới |
| [Môi trường](ENVIRONMENT.md) | Ghi baseline, COM trỏ nhầm WPS theo bitness và giới hạn quan sát UI |
| [Đợt tiếp theo](NEXT-STEPS.md) | M1-01 sẵn sàng; việc còn thiếu cho Word có tiêu chí và ước lượng có điều kiện |

Hai demo chạy cục bộ bằng `node prototypes/m0-space/serve.cjs`; mở `/markers.html` hoặc `/index.html` trên cổng được in ra. Phiên bàn giao đang dùng [demo cặp bọc tại localhost:4176](http://127.0.0.1:4176/markers.html). Server không phải automation và chỉ phục vụ asset khai báo trên loopback. Có thể mở các HTML trực tiếp từ thư mục dự án.

## 2. Hành vi cặp bọc đã thống nhất

Theo yêu cầu mới của người dùng, thay mặc định `toan[[...]]` bằng **`lc[...]`**. Người dùng đổi được dấu mở và dấu đóng; `toan[[...]]` vẫn có thể dùng như cấu hình tùy chỉnh. Corpus đã cập nhật cả span UTF-16 của phần nội dung, phần thay thế và repair; không chỉ đổi chuỗi hiển thị.

- `Ta có lc[x^2] và tiếp tục viết.` → chỉ vùng `lc[x^2]` được dựng; văn xuôi hai bên giữ nguyên.
- Chưa gõ `]` → chưa chuyển. Dấu đóng nhiều ký tự phải hoàn chỉnh.
- `lc[2/3x]` → giữ nguồn, có `fx` để chọn cách hiểu; không auto.
- Nguồn cần sửa hoặc có cảnh báo → giữ nguồn để người dùng chọn.
- Restore → trả cả cặp bọc nguyên bản; sự kiện restore/Undo không lập tức gây chuyển lại.
- Đổi cấu hình hoặc nguồn trong khi tác vụ chờ → tác vụ cũ hết hiệu lực.

Đã QA giao diện thật trong trình duyệt: mẫu văn xuôi với MathML `x²`, restore, dấu `<<`/`>>` với phân số, mở `fx` cho mơ hồ và hủy tác vụ sau đổi dấu. Screenshot đại diện có bố cục rõ, công thức đọc được, không chồng chữ. Đây là **fixture UX trong browser**, textarea giữ nguồn để đối chiếu và preview mô phỏng tài liệu. Chưa phải editor sản phẩm hoặc add-in Word; chưa là bằng chứng bộ gõ Windows.

## 3. Word: bằng chứng native trên máy hiện tại

Baseline: Windows 10 Pro 10.0.19045 x64; Microsoft Word x86 **16.0.14026.20302**. Xem [environment.json](../../artifacts/m0/environment.json). Không có WINWORD trước khi thử; mỗi run tạo instance và tài liệu riêng, kiểm tra PID/thời điểm/path, rồi đóng instance sở hữu. Không sửa tài liệu người dùng, registry COM hoặc Trust Center.

### Native và tính toàn vẹn xung quanh

[Run All sau khi sửa bộ kiểm tra edit](../../artifacts/m0/word/run-20260912T051305104Z-e620d0/report.json) có **100/100 assertion native đạt**: 20 fixture × tạo OMath, so cấu trúc OMML, nội dung/định dạng ngoài vùng, save/reopen và sửa thành phần trong equation qua COM. Fixture bao gồm số học, phân số lồng, căn, mũ/chỉ số, tổng/tích phân, ma trận, vector và hệ dòng; các mẫu nâng cao chỉ thăm dò khả năng xuất Word, không mở rộng grammar M1.

Chỉnh sửa được qua `Word.Range` là bằng chứng native có thể sửa bằng API; chưa thay thế bài thử gõ tay trong Equation UI. So cấu trúc bỏ qua ranh giới run/font và chỉ xét các thuộc tính semantic đã khai báo, không phải kiểm chứng tương đương OOXML đầy đủ hoặc pixel-perfect. Một mẫu ma trận đã được Word xuất PDF và kiểm tra ảnh render trực quan: đủ hai hàng/hai cột, đúng chữ xung quanh, không clipping; chưa kiểm tra trực quan tất cả 20 mẫu hoặc đối chiếu renderer Desktop production.

Phát hiện có ích: Word trả chữ `x` trong OMath dưới dạng mathematical italic U+1D465 dù OMML nguồn dùng `x`. Khi xóa content control, chỉ số range cũng có thể đổi. Vì thế adapter phải sử dụng range thật và mapping đã xác minh, không lấy độ dài chuỗi normalized hoặc glyph native làm offset nguồn.

### Metadata, Undo, restore và lỗi giữa commit

| Thử nghiệm | Kết quả | Ý nghĩa |
| --- | --- | --- |
| CustomXML + content control | Native/source/candidate có thể lưu và mở lại; **FAIL** Undo vì CustomXML còn sót; fault sau MetadataAdded/SelectionMoved cũng để lại XML | Không chọn cơ chế này làm giao dịch production hiện tại |
| Snapshot trong ContentControl.Tag | Payload nhỏ, 1.024, 4.096, 16.384 và 65.536 UTF-16 code unit roundtrip chính xác qua write/read, Undo/Redo và save/reopen | Hướng ưu tiên thử tiếp; kích thước đã thử không phải giới hạn Word được bảo đảm |
| Tag fault injection tại 4 bước | **PASS** phục hồi text/native/control/metadata về snapshot trước bằng một Undo | Chỉ các bước/tài liệu được thử, chưa bao gồm crash hoặc tài liệu nhiều control |
| Tag save-as/candidate set | **PASS**, giữ đủ bộ kết quả và lựa chọn | Save As qua COM đã thử; thao tác UI Save As/copy trên tài liệu thật còn riêng |
| Sửa native rồi kiểm tra drift | **PASS**, phát hiện khác candidate đã lưu | Chưa có event handler production; không tự ghi đè sửa native |
| Restore nguồn và detach | **PASS** ở run cuối; Tag restore/detach một Undo và một Redo đạt | Restore trả exact nguồn và bỏ native/metadata; detach giữ native hiện hành |
| Tag Unicode | **PASS** NFC/NFD, emoji, CRLF, tab và khoảng trắng cuối trong payload qua save/reopen | Bằng chứng lưu metadata; không chứng minh Word body giữ mọi ký tự qua mọi phép đổi |
| Copy qua Range.FormattedText sang tài liệu khác | **FAIL** giữ association: equation còn nhưng content control/Tag mất | Không suy ra kết quả Ctrl+C/Ctrl+V; metadata thiếu phải ngừng quản lý, giữ native |
| Selection trước/sau một Undo | **FAIL** tiêu chí strict của probe: selection trước có vùng chọn, sau Undo co về caret | W0-01 còn mở; không gọi toàn bộ trạng thái Word đã được khôi phục chính xác |

Các run cuối để đọc theo từng phạm vi, **không cộng các lần chạy thành số test độc lập**:

- [All — e620d0](../../artifacts/m0/word/run-20260912T051305104Z-e620d0/report.json): 173 PASS, 7 FAIL, 10 UNTESTED; trong PASS có 20 kiểm tra fixture tĩnh và 100 assertion native. Hai lỗi restore của run này đã có run targeted phía dưới thay thế kết luận.
- [Lifecycle — ff4424](../../artifacts/m0/word/run-20260912T052645343Z-ff4424/report.json): 26 PASS, 2 FAIL, 10 UNTESTED; trong PASS có 20 fixture tĩnh. Restore đã đạt; còn CustomXML Undo và selection.
- [Tag — 4da475](../../artifacts/m0/word/run-20260912T052718842Z-4da475/report.json): 54 PASS, 1 FAIL, 10 UNTESTED; trong PASS có 20 fixture tĩnh. Restore/Undo/Redo đạt; còn giới hạn copy bằng FormattedText.

Restore được sửa bằng cách giữ một **live collapsed Word Range** trước khi xóa cả content control và nội dung, rồi chèn nguồn vào range đã được Word cập nhật trong cùng custom Undo. Boundary trace của Lifecycle cho thấy anchor tự đổi 27 → 26 sau khi wrapper bị xóa; không trừ offset cố định. Các bản thử thất bại trước đó vẫn giữ trong artifacts để truy nguyên.

### Stale, IME, focus và lifecycle

Trong run All, 13 tình huống guard đạt; có sửa/xóa nguồn, chèn trước vùng và đóng tài liệu trên Word thật. Candidate/config/session/timeout/focus/composition là trạng thái điều khiển tổng hợp trong probe. Chỉ có thể kết luận logic từ chối hoạt động với đầu vào đã cấp, **không kết luận đã quan sát đúng IME/focus thực**.

UI startup Word cho thấy focus ở ô tìm kiếm dù cửa sổ Word đang mở. Công cụ capture/click gặp lỗi trên môi trường hiện tại, nên chưa hoàn tất gõ thật UniKey, editor focus, `fx` theo scroll/DPI, clipboard người dùng, nhiều tài liệu/coauthoring và add-in startup. [ENVIRONMENT.md](ENVIRONMENT.md) ghi lỗi cụ thể và các tổ hợp chưa thử. Word có thể chạy native mà vẫn chưa đủ điều kiện tự thay chữ khi gõ.

## 4. Core chung và IPC

[shared-core.json](../../artifacts/m0/shared-core.json) xác nhận hai console host chạy DLL có cùng SHA-256 và cùng đầu ra; source NFC dài 6 và NFD dài 7 UTF-16 được giữ riêng. Build không có warning/error. Đây là fixture `x mũ 2`, chưa phải parser hoặc Desktop/VSTO cài đặt.

[ipc.json](../../artifacts/m0/ipc.json): 21 assertion đạt qua named pipe giữa host .NET 10 và server .NET Framework 4.8 x86, gồm từ chối session cũ sau restart server thật. Cần bổ sung framing, version, ACL, size/cancellation và revalidation đọc trạng thái Word thật ở M3. Chạy local của probe không chứng minh toàn bộ ứng dụng đã có cold startup offline.

## 5. Cổng và trạng thái cuối M0

| Cổng | Trạng thái | Căn cứ / việc còn thiếu |
| --- | --- | --- |
| C0 — đủ hợp đồng để làm core | **ĐẠT** | Grammar, raw/span/candidate contract và corpus đã review; M1-01 READY |
| G0 — chọn đường native | **ĐẠT trong baseline thử** | COM native có bằng chứng; metadata Tag có kết quả đủ để chọn hướng nghiên cứu |
| CW — đủ mở triển khai luồng Word đã cam kết | **CHƯA ĐẠT** | Cần W0-01/02/03 về selection, xác minh mục tiêu thật và phạm vi metadata/tài liệu |
| G1 — phát hành chuyển có xác nhận | **CHƯA ĐẠT** | Chưa có connector production và toàn bộ transaction/host guard theo phạm vi hỗ trợ |
| G2 — gợi ý khi gõ / auto vùng | **CHƯA ĐẠT** | IME/focus/lifecycle/inline fx thực còn thiếu; browser marker demo không đóng cổng này |
| G3 — Space | **CHƯA ĐẠT** | D-01 còn mở; cần native continuation và người thử theo bài đã ghi |

M0-01 đến M0-08 hoàn thành **đầu ra nghiên cứu**: có đặc tả, prototype, kết quả, giới hạn, quyết định và kế hoạch tiếp theo. Các ca FAIL/UNTESTED không đổi thành PASS khi đóng nghiên cứu. [NEXT-STEPS.md](NEXT-STEPS.md) và [backlog](../BACKLOG.md) giữ công việc Word còn thiếu độc lập với M1.

## 6. Chạy lại có kiểm soát

Từ thư mục gốc `D:\duan\HMDA\locus`:

```powershell
python tools/m0/validate_corpus.py --self-test
node --test prototypes/m0-space/state-machine.test.cjs prototypes/m0-space/marker-state.test.cjs
./tools/m0/shared_core_probe.ps1
./tools/m0/ipc_probe.ps1
```

Word probe phải chạy tuần tự, đóng các cửa sổ Word của người dùng trước khi chủ động chạy. Runner tự từ chối khi đã có WINWORD hoặc COM đăng ký không trỏ Microsoft Word. Trên **máy hiện tại**, dùng Windows PowerShell x86:

```powershell
& 'C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe' -NoProfile -ExecutionPolicy Bypass -STA -File '.\tools\m0\word_probe.ps1' -Suite All
```

`ExecutionPolicy` ở lệnh trên chỉ áp dụng tiến trình thử, không đổi chính sách lưu trên máy. Mỗi run có thư mục/ownership/report mới. Exit code khác 0 là bình thường khi các thử nghiệm giới hạn đã biết vẫn FAIL; không nới assertion để làm bảng xanh. Hướng dẫn suite nhỏ và giới hạn COM timeout nằm trong [README của probe](../../fixtures/m0/word/README.md).
