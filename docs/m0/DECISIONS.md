# Quyết định kỹ thuật sau M0

Ngày: 2026-09-12. Đây là baseline để triển khai tiếp trong phạm vi người dùng đã giao. Quyết định có thể được sửa bằng bằng chứng mới; chưa phải chứng nhận phát hành. [Báo cáo M0](REPORT.md) ghi kết quả và các cổng còn thiếu.

**Ghi chú sau M1:** các bằng chứng fixture/probe trong tài liệu này là lịch sử M0. Core thật đã triển khai và kiểm chứng ở [báo cáo M1](../m1/REPORT.md); các cổng Word chưa thay đổi.

**Ghi chú sau W0:** [báo cáo W0](../w0/REPORT.md) bổ sung add-in thật và giao dịch trong tiến trình Word. Các quyết định M0 bên dưới được giữ để truy nguyên; phần bổ sung cuối tài liệu ghi cách triển khai hiện tại. CW đã mở cho thủ công trên baseline; G1/G2/G3 chưa đạt.

## ADR-001 — Một core C# dùng chung

**Chấp nhận cho M1:** core thuần C#, target `netstandard2.0`; không tham chiếu Word, UI, clipboard hoặc mạng. Desktop và connector dùng cùng assembly và cùng schema. Core không có quyền ghi tài liệu.

[Probe shared assembly](../../prototypes/m0-shared-core/README.md) đã chạy cùng DLL từ host `net10.0` và `net48` x86, so hash DLL và đầu ra. Đây là bằng chứng tương thích runtime với fixture, chưa có parser production. Microsoft cũng hướng dẫn .NET Standard 2.0 cho việc chia sẻ giữa .NET Framework và .NET hiện đại: [.NET Standard](https://learn.microsoft.com/en-us/dotnet/standard/net-standard).

Desktop dự kiến dùng .NET hiện đại; framework UI và renderer SVG/PNG được chọn sau một spike nhỏ ở M1-05/M2-01. Probe console chưa quyết định được chất lượng nhập liệu hoặc xuất ảnh. JavaScript trong demo M0 chỉ để thử thao tác; không chuyển nó thành một parser thứ hai.

## ADR-002 — Baseline ngữ pháp và dữ liệu

**Chấp nhận làm baseline kỹ thuật M1:** [GRAMMAR.md](GRAMMAR.md) phiên bản `vi-math-m0-proposal-0.1` và [CORE-CONTRACT.md](CORE-CONTRACT.md) phiên bản `locus-core-contract/0.1`. Giữ tên phiên bản để truy nguyên bộ corpus đã soạn. Đây là quyết định triển khai của đợt M0, không hàm ý người dùng đã xét từng alias.

- Nguồn bất biến; offset công khai UTF-16 half-open của đúng snapshot; map chuẩn hóa nhiều–nhiều. Word adapter phải ánh xạ riêng theo document/story/range.
- Candidate `direct`, `interpretation`, `repair` phân biệt rõ; tối đa ba; giữ toàn bộ CandidateSet ngay cả khi chưa mở `fx`.
- Preview và các bộ xuất nhận snapshot đã chọn; không parse lại chuỗi để chèn Word.
- Không tính giá trị, rút gọn, sửa ý toán hoặc suy đoán Lý/Hóa trong grammar M1. `H2SO4` được giữ trong backlog miền Hóa.
- [Corpus](../../corpus/m0/README.md) gồm 117 tình huống: 109 đã đặc tả, 8 chờ quyết định Space/Lý/Hóa. Validator dữ liệu chưa phải kiểm thử parser.

## ADR-003 — Cặp bọc có thể cấu hình

**Theo lựa chọn người dùng ngày 2026-09-12:** dấu mở mặc định `lc[`, dấu đóng `]`. Cài đặt cho sửa cả hai bằng chuỗi literal và xem trước ví dụ. Đây là cơ chế thể hiện ý định rõ; đóng vùng là trigger, không phải bằng chứng duy nhất cấp quyền ghi.

Các ví dụ `lc[x mũ 2]`, `lc[1 trên 2]` đủ điều kiện nội dung theo grammar. `lc[2/3x]` có mơ hồ, `lc[can(2+3]` cần sửa, nên giữ nguồn và cho chọn. Phần văn bản ngoài cặp bọc vẫn dùng chế độ gợi ý mặc định. Đổi cặp bọc không quét/chuyển lại toàn bộ tài liệu.

Baseline không hỗ trợ lồng hoặc escape. Cấu hình rỗng, trùng, dấu này là prefix của dấu kia, xuống dòng hoặc chứa backslash bị từ chối theo GRAMMAR.md. Cấu hình đổi làm hết hiệu lực kết quả chờ. Restore trả lại cả cặp bọc nguyên bản và không tự chuyển lại từ chính sự kiện restore. Các hạn chế này có thể mở rộng theo corpus thực tế.

## ADR-004 — Hướng Word trên Windows

**Chọn hướng nghiên cứu/triển khai adapter:** COM Word, tiến tới connector VSTO/.NET Framework 4.8 trong Word. M0 chưa build, đăng ký hay cài add-in VSTO.

Lý do: native OMML, chỉnh sửa qua COM, save/reopen và khả năng custom Undo đã được chạy trên Microsoft Word thực. Baseline hiện tại là Word x86 `16.0.14026.20302`; event Office.js `onParagraphChanged` yêu cầu WordApi 1.6 trên build mới hơn. Không xây hai connector song song ở bản đầu. Office.js giữ là phương án có thể xét lại khi đổi baseline; runtime sideload và transaction của hướng đó chưa được thử. [Ma trận môi trường và nguồn Microsoft](ENVIRONMENT.md).

Môi trường hiện tại có COM 64-bit trỏ WPS, COM 32-bit trỏ Microsoft Word. Connector phải xác minh đúng ứng dụng/host; không dựa riêng vào `Word.Application`. Không sửa đăng ký ứng dụng mặc định của máy để ép kết quả thử.

G0 chọn được đường native không đồng nghĩa CW/G1 đã đạt. Hợp đồng Undo/selection, focus thực, mapping vùng và các ngữ cảnh tài liệu còn phải được chứng minh trước khi cho phép ghi vào tài liệu người dùng.

## ADR-005 — Metadata và native đã sửa

**Loại bỏ CustomXML độc lập làm nơi duy nhất lưu giao dịch của bản đầu theo prototype hiện tại.** Thử nghiệm cho thấy một Undo trả nguồn nhưng vẫn để lại CustomXML part; lỗi sau khi thêm metadata cũng để lại part. Không coi việc dọn XML sau đó là bằng chứng transaction ban đầu đã nguyên vẹn.

**Ưu tiên kiểm chứng tiếp snapshot đầy đủ trong `ContentControl.Tag`.** Các payload thử đã giữ nguyên qua Undo/Redo và save/reopen trên baseline. Lưu source, CandidateSet, lựa chọn, phiên bản và dấu vết cấu trúc native; không lưu riêng ID rồi hy vọng tìm được kết quả cũ bằng parser mới.

Giới hạn quan trọng:

- Các kích thước Tag đã thử không xác định giới hạn nền tảng. M3 phải chốt giới hạn payload có version/checksum, xác minh readback và từ chối giao dịch trước khi mất nguồn nếu không lưu đủ snapshot. Không âm thầm cắt bớt candidate để vừa Tag.
- Copy bằng `Range.FormattedText` trong probe giữ equation nhưng mất content control/Tag. Chưa kết luận về Ctrl+C/Ctrl+V thực. Khi metadata vắng hoặc không khớp, giữ native và ngừng quản lý association đó.
- Native được sửa là nội dung hiện hành. Nguồn cũ là lịch sử; không tự lấy nó ghi đè. Restore nguồn cũ phải là thao tác riêng có thông báo rõ và Undo; mở editor không tự parse ngược native rồi gán nguồn giả.
- ID trùng, nhiều control, metadata hỏng/mới hơn và các wrapper có sẵn cần suite riêng ở M3. Các probe tài liệu một control chưa chứng minh chúng.

## ADR-006 — IPC cục bộ có phiên

**Chọn named pipe cục bộ làm hướng IPC trên Windows.** [Probe](../../artifacts/m0/ipc.json) đã trao đổi giữa hai tiến trình thật và từ chối phiên cũ sau khi server restart; 21 assertion đạt. Parser vẫn ở cùng assembly, kết quả được gửi theo snapshot bất biến.

Hợp đồng production cần version, message framing, size limit, cancellation, kiểm soát truy cập theo người dùng, request ID, session/document/story/range identity, source/candidate/config revision và thời hạn. Host Word tái kiểm tra trạng thái thực ngay trước commit, không tin riêng trường `focus`/`composing` trong thông điệp Desktop. M0 mới dùng cờ tổng hợp để thử từ chối, chưa đọc tín hiệu IME/focus Word.

## ADR-007 — Space và điều kiện phát hành

**D-01 vẫn mở.** Demo chứng minh chốt mỗi Space có thể tách `x mũ 2 cộng 1` thành equation `x²` và chữ `cộng 1` bên ngoài. Giữ phiên nguồn là hướng cần thử tiếp, chưa chốt Enter làm thao tác sản phẩm và chưa đáp ứng native auto-Space thực.

M5A cặp bọc và M5B Space giữ hai cổng riêng. Việc có bản thử `lc[...]` không bật auto trong Word. Tiếp tục M1 khi C0 đạt; các hạn chế Word được ghi thành công việc có tiêu chí, không làm parser phụ thuộc vào chốt Space.

## Bổ sung W0 — Host, giao dịch và bằng chứng còn thiếu

**Host đã chạy:** COM add-in .NET Framework 4.8 x86 với `IDTExtensibility2` từ interop Office đã cài, dùng core M1 thật. Đây là triển khai nghiên cứu của hướng COM trong ADR-004; chưa chọn bộ cài VSTO cho sản phẩm. Đăng ký HKCU chỉ phục vụ workspace thử, không phải installer đã ký hoặc kiểm trên máy sạch.

**Giao dịch bắt buộc trên luồng UI Word:** convert/restore/detach chạy liền qua dispatcher của add-in. `Selection.InsertXML` cùng snapshot trong Tag đạt Undo/Redo nguồn, metadata và vùng chọn trong bộ kiểm. Các lời gọi ghi rời từ tiến trình ngoài đã có lỗi tái hiện sau clipboard; adapter nay từ chối đường gọi đó. Đọc WordOpenXML để xác minh native diễn ra sau EndCustomRecord, vẫn trong cùng lượt thực thi; lỗi xác minh thì rollback.

**Metadata đã bổ sung bằng chứng:** serializer M1 lưu toàn bộ CandidateSet/chosen/source/version/checksum; ngân sách Tag nội bộ 65.536 UTF-16 code unit, từ chối trước ghi nếu vượt. Bộ kiểm nhiều control, ID trùng, native drift, save/reopen và clipboard Word API đạt. Giới hạn nền tảng rộng và Ctrl+C/Ctrl+V bằng phím vẫn chưa được chứng minh.

**Lifecycle đã chạy với Desktop thật:** hai thứ tự mở, nhiều tài liệu/cửa sổ, reconnect, phiên cũ và add-in đã tắt được kiểm. Named pipe chỉ đọc trong bản nghiên cứu; giao thức candidate hai chiều và startup sản phẩm thuộc M3/M6.

**D-01 giữ mở:** TypeText tại mép equation có thể tiếp tục ở chế độ toán. Phím Telex cho thấy không có WM_IME chuẩn trong lần thử; không suy ra hoàn tất nhập từ riêng IMM hoặc thời gian yên lặng. VNI/focus đầy đủ, clipboard bằng phím, badge/DPI và người thử native Space còn thiếu. Không bật auto marker/Space từ kết quả W0 hiện tại.

**Bổ sung đợt tiếp theo W0:** đã sửa con trỏ sau chuyển thủ công bằng cách lấy chế độ nhập từ ký tự văn bản kế tiếp; 30 ca API và phím VNI đạt. Guard so cả equation trong tài liệu để phát hiện phần mở rộng vượt ngoài content control. VNI có dấu, Backspace/Undo/Redo, clipboard bằng phím và focus Ribbon/hộp Font/UniKey đã kiểm; nguồn/candidate giữ đúng và thao tác ngoài editor bị từ chối. CW mở cho M3 thủ công trên baseline. `fx`/DPI và UX giữ phiên auto-Space vẫn chưa đạt; D-01/G2/G3 giữ mở theo [báo cáo hiện hành](../w0/REPORT.md).

**Bổ sung ngày 2026-09-13 — bản thử giữ phiên thật:** [A giữ nguồn và B cập nhật native](../w0/SPACE-NATIVE.md) đã dùng cùng core/metadata trong Word; chuỗi mũ/phân số/căn giữ một nguồn xuyên các lần Space. Nguồn thiếu hoặc có repair không tự cập nhật. Cả hai biến thể có nút chốt/giữ văn bản; Enter/Esc chỉ dừng phiên và giữ thao tác Word trong nghiên cứu. Đây chưa phải quyết định phím của sản phẩm. Người dùng đang bận và yêu cầu tập hợp việc cần thử; [D-01 còn chờ phản hồi](../w0/PENDING-ACCEPTANCE.md). Không dùng phép thử do Codex thao tác để tự đóng yêu cầu người tham gia.
