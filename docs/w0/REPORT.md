# W0 — kiểm chứng connector Word

Ngày thực hiện: **2026-09-12 đến 2026-09-13** theo giờ máy; artifact ghi UTC. Người thực hiện: Codex. Phạm vi: Windows x64, Microsoft Word **x86 16.0.14026.20302**, .NET Framework 4.8 và core M1 thật.

**Đã có add-in nghiên cứu chạy trong Word, giao dịch native/metadata/Undo và kết nối chẩn đoán Desktop.** Đã thêm badge bám vùng có clipping/hết hạn và hai biến thể nhập nối A/B bằng core thật. **W0 đã nghiệm thu 4/6 hạng mục (67%); CW đạt cho đường chuyển thủ công trên baseline.** W0-05/06 còn bước trực quan/DPI thực, người thử và D-01. Người dùng đang bận; [checklist để làm khi rảnh](PENDING-ACCEPTANCE.md). G1/G2/G3 chưa đạt. Bản B tự cập nhật native chỉ trong sandbox được mở rõ ràng; auto cặp bọc/Space của sản phẩm chưa bật.

## Kết quả và bằng chứng

[Chỉ mục nghiệm thu](../../artifacts/w0/acceptance.json) ghi các run cuối, hash mã và phạm vi từng bài. Không cộng những lần chạy cũ thành số kiểm tra độc lập.

| Phần | Kết quả | Giới hạn |
| --- | --- | --- |
| Adapter trong Word | 36 bài PASS; 1 bài OBSERVED; gồm ma trận 30 trường hợp nhập tiếp và equation thứ hai | Ghi trong add-in trên luồng Word; chuẩn bị fixture và đọc kết quả bằng COM; không phải thao tác bàn phím người dùng |
| Lifecycle add-in | 7 bài PASS | Startup, nhiều tài liệu/cửa sổ, IPC, disconnect/reconnect, phiên cũ, đóng tài liệu, Undo sau clipboard |
| Lifecycle Desktop–Word | 2 kịch bản PASS | Cửa sổ chẩn đoán của Desktop thật; hai thứ tự mở, khởi động lại Desktop, Word đóng, tôn trọng add-in đã tắt |
| Desktop hồi quy | 40 nhóm PASS | Kiểm tra source build sau khi thêm entry point chẩn đoán; gói M2 đã phát hành không bị thay |
| UX nghiên cứu trong Word | 7 nhóm PASS: 5 nhóm Space, 2 nhóm badge | Replay bằng API; badge kiểm WinForms/Word thật tại DPI 96 và layout tổng hợp; không thay phản hồi người thử |
| Nhập nối qua Windows | 6 nhóm PASS | Telex chuỗi mũ, Undo/Redo; Unicode nguyên văn + Space cho phân số/căn/A; Esc/Enter; không phải mọi bộ gõ hoặc gõ nhanh |
| Phím Telex/VNI trong Word | `X mũ 2` đạt ở cả hai kiểu gõ; VNI có Backspace/Undo/Redo và gõ ` cộng 1` sau equation | Word tự viết hoa chữ đầu câu; không sửa AutoCorrect. Chưa phải ma trận mọi bộ gõ hoặc gõ nhanh |
| Focus | Find, Ribbon, hộp Font và UniKey đều bị từ chối, giữ nguồn | Đổi tài liệu/cửa sổ/session còn có suite lifecycle; chưa bao phủ mọi dialog Word |
| Clipboard bằng phím | Ctrl+C/Ctrl+V trong và khác tài liệu giữ native/Tag | ID trùng trong cùng tài liệu bị từ chối; sang tài liệu mới đọc được đúng nguồn/candidate |
| Badge fx | Bám range/document/window, cuộn khuất thì ẩn, quay lại hiện, đổi nguồn thì hết hiệu lực; không thay source/selection | Có khung nhìn thực để loại tọa độ GetPoint ngoài viewport. Quan sát/click trực tiếp và nhiều DPI/màn hình chưa nghiệm thu |
| Nhập tiếp sau chuyển thủ công | **Đạt trong các ca API và phím VNI đã thử** | Đã sửa vị trí/chế độ nhập. Ý định tiếp tục cùng công thức sau auto-Space vẫn chưa chốt |

## 1. Những gì đã triển khai

- [Locus.Word](../../src/Locus.Word/README.md): COM add-in x86, ProgID `Locus.Word.W0`, dùng cùng project core; không có parser thứ hai. Word nạp assembly và khởi động observer trong tiến trình thật.
- [WordOperations](../../src/Locus.Word/WordOperations.cs): chuyển candidate đã chọn thành OMath, snapshot Tag, Undo/Redo, restore và detach. Bắt buộc thực hiện giao dịch trên đúng luồng UI của Word; yêu cầu từ tiến trình ngoài đi qua dispatcher trong add-in.
- [ManagedSnapshot](../../src/Locus.Word/ManagedSnapshot.cs): toàn bộ nguồn/candidate/lựa chọn/phiên bản dùng serializer có checksum của core; giới hạn nội bộ 65.536 UTF-16 code unit. Đây là ngân sách Locus dựa trên baseline thử, không phải giới hạn Word được Microsoft bảo đảm.
- Observer: định danh COM của tài liệu trong phiên, ranh giới vùng chọn, nguồn và revision của tài liệu thử, focus thực và tín hiệu bàn phím/IMM. Đóng hoặc reconnect làm mất quyền quản lý cũ; không tự gắn lại nguồn.
- Named pipe chỉ đọc, ACL theo người dùng Windows hiện tại. [Cửa sổ chẩn đoán Desktop](../../src/Locus.Desktop/WordProbeWindow.cs) nhận trạng thái với timeout/kích thước giới hạn; không gửi lệnh ghi hoặc bật lại add-in đã tắt.
- [Solution Word riêng](../../Locus.Word.sln), script build/cài/gỡ và [runner](../../tools/w0/verify.ps1). Solution Desktop vẫn độc lập với các PIA Office.
- [BadgeController](../../src/Locus.Word/BadgeController.cs) và [BadgePlacement](../../src/Locus.Word/BadgePlacement.cs): gắn với nguồn/cửa sổ đã xác minh, đo lại theo khung nhìn và DPI, ẩn khi nguồn/cửa sổ/focus không còn phù hợp.
- [SpaceTrial](../../src/Locus.Word/SpaceTrial.cs): nguồn liên tục qua nhiều lần native, chọn kết quả/giữ nguồn, dừng khi Undo/đổi tài liệu/đề nghị đóng; observer không xử lý lồng nhau. [Hướng dẫn thử](TRIAL-GUIDE.md).

Add-in nghiên cứu chỉ được phép ghi vào tài liệu tổng hợp do chính nó tạo và còn thuộc phiên hiện tại. API research dùng để chèn lỗi/kiểm giao dịch; nó không phải API chuyển công thức cho sản phẩm. Nội dung tài liệu thường không đi vào log observer. Không sửa add-in khác, COM mặc định, Trust Center hoặc chế độ bảo vệ của tài liệu người dùng.

## 2. Undo, khôi phục và các lỗi đã tìm được

Thử các đường `Range.InsertXML` và `Selection.InsertXML` cho thấy đường Selection giữ được vùng chọn gốc khi Undo. Bộ kiểm cuối kiểm native + Tag cùng một Undo/Redo cho 6 nguồn, trong đó có tiếng Việt NFC/NFD, căn, mũ, phân số và phân số lồng.

Việc xóa toàn bộ content control bằng `Delete(true)` có thể nuốt dấu cách ngoài vùng. Thay bằng `Range.Text` có thể vẫn giữ chế độ toán. Adapter hiện khôi phục bằng một run văn bản OOXML rồi bỏ wrapper metadata, trong cùng giao dịch trên luồng Word. Bộ kiểm xác nhận nguyên nguồn, dấu `lc[...]`, dấu cách/tab/emoji xung quanh và định dạng ngoài vùng.

Thử ghi từ tiến trình ngoài có lúc đạt ở case nhỏ nhưng thất bại sau clipboard: giữa restore, tên custom Undo đổi sang giao dịch convert trước và record kết thúc sớm. Chuyển toàn bộ giao dịch vào add-in chạy liền trên luồng Word giải quyết trường hợp tái hiện này. Đây là kết luận thực nghiệm trên baseline, không khẳng định nguyên nhân nội bộ Word. Code từ chối gọi đường ghi trên luồng/tiến trình khác.

Đọc `WordOpenXML` trong một custom Undo cũng đã làm các bài Undo/rollback thất bại. Kiểm cấu trúc native được thực hiện sau khi đóng record, vẫn trong lượt thực thi của add-in; lỗi xác minh thì Undo giao dịch. Không chặn hay thay thế Ctrl+Z của Word.

Chèn lỗi sau insert, thêm control, ghi Tag, di chuyển selection và các bước restore được kiểm bằng snapshot tài liệu. Rollback hoàn chỉnh có bằng chứng ở các điểm chèn lỗi này; chưa phải bảo đảm khôi phục sau crash/process termination hoặc mọi dạng lỗi COM có thể xảy ra.

## 3. Metadata và phạm vi tài liệu

Toàn bộ CandidateSet đã chọn roundtrip qua Tag và file DOCX save/reopen, gồm raw NFD. Thiếu/hỏng/phiên bản mới hơn, payload vượt ngân sách và ID trùng được từ chối. Không cắt candidate cho vừa payload, không tự parse lại để đoán nguồn đã mất.

Copy/Paste bằng **Word clipboard API và Ctrl+C/Ctrl+V** trong cùng và khác tài liệu giữ equation/control/Tag trong bài thử. Copy cùng tài liệu tạo ID trùng; adapter từ chối association đó. `Range.FormattedText` giữ equation nhưng mất control/Tag trên baseline. [Bằng chứng phím thật](../../artifacts/w0/native/followup-verification.json).

Native đổi số mũ từ 2 thành 3 được phát hiện trước restore. So sánh dùng tập OMML M1: cấu trúc, nội dung và thuộc tính ngữ nghĩa liên quan; bỏ qua font và ranh giới run. Chưa phải bộ so sánh tổng quát cho mọi equation Word. Native đã thay đổi được giữ lại; UX cho phép người dùng chủ động lấy nguồn lịch sử thuộc M3/M4.

| Context | W0 |
| --- | --- |
| Văn bản được chọn trong thân tài liệu | Đã thử chuyển có xác nhận trong sandbox |
| Read-only/protected/Track Changes | Từ chối; không tắt bảo vệ/tracking |
| Bảng/header và story khác | Từ chối; không tuyên bố hỗ trợ |
| Content control/equation/field/shape sẵn trong vùng nguồn | Guard từ chối; ma trận tương tác rộng còn ở M3 |
| Metadata trùng/mất/hỏng/native sửa | Không dùng association cũ để ghi đè |
| Coauthoring, tài liệu phức tạp, Word x64/máy sạch | Chưa kiểm |

## 4. Focus và bộ gõ

[Telex hoàn chỉnh](../../artifacts/w0/native/telex-complete.json) ghi `X mũ 2`, focus class `_WwG`, hook đang chạy, nhưng `ImeStarts=0`, `ImeEnds=0`. [Trạng thái lúc thêm dấu ngã](../../artifacts/w0/native/telex-tilde.json) cũng không có composition chuẩn. Do đó không thể dùng riêng IMM/WM_IME hoặc đợi một khoảng yên lặng để kết luận UniKey đã xong.

[Find](../../artifacts/w0/native/find-focus-fixed.json) cho thấy ô tìm kiếm khác editor dù selection tài liệu vẫn còn. Đã sửa lỗi kiểm focus quá muộn: chụp điều kiện lúc nhận yêu cầu, rồi kiểm lại trước ghi. [Thử từ chối sau sửa](../../artifacts/w0/native/find-refusal-fixed.json) giữ nguyên `x^2`, không tạo OMath/control. Bản thử thất bại trước đó được giữ để truy nguyên, không tính là PASS.

Đợt tiếp theo, công cụ Windows kết nối lại được. Đã đổi UniKey sang VNI bằng giao diện, đọc cấu hình `InputMethod=1`, gõ từng phím `x`, Space, `m`, `u`, `4`, Space, `2`; Word nhận `X mũ 2`. Backspace/Undo/Redo đúng nguồn; `co65ng` nhận `cộng`. Tương tự Telex, không có WM_IME start/end trong lần thử. Đã trả UniKey về Telex sau kiểm tra.

F10 đưa focus vào `NetUIHWND` của Ribbon; Ctrl+D đưa vào `RichEdit20W` của hộp Font; UniKey là ứng dụng khác. Cả ba yêu cầu chuyển đều bị từ chối trước ghi, nguồn `x^2` và số native/control giữ nguyên. [8 nhóm kiểm thực tế](../../artifacts/w0/native/followup-verification.json), [nhật ký phím](../../artifacts/w0/native/keyboard-followup-log.json). Các bài này dùng bản sửa con trỏ; guard kiểm toàn equation được bổ sung sau đó và kiểm bằng hồi quy API.

Lỗi native pipe của đợt trước đã hết, nhưng capture vẫn báo **`SetIsBorderRequired ... 0x80004002`**, click báo **`coordinate input geometry is unavailable`**. Bằng chứng mới dùng phím, accessibility và observer trong Word; chưa có nghiệm thu badge trực quan/DPI. Accessibility có thể bỏ qua equation hoặc trả text cũ, nên kết luận nội dung dựa thêm vào Word API.

## 5. `fx` và nhập nối tiếp

`Window.GetPoint` có thể trả rectangle ngoài màn hình dù không có COMException. Badge mới kiểm cả khung nhìn editor: range phải nằm hoàn toàn trong viewport, badge nằm bên phải/trái/dưới với đủ chỗ; nếu không đủ thì ẩn. Nó thuộc đúng cửa sổ Word, không yêu cầu kích hoạt và không thay source/selection. Thử Word thực kiểm zoom 150%, cuộn khuất/quay lại, cửa sổ thứ hai và sửa nguồn. Một run trước đó thiếu focus editor bị FAIL; run UX cuối kích hoạt đúng editor rồi đạt. DPI 96 là đo thực; DPI 120/144/192 và tọa độ màn hình âm chỉ là bài layout tổng hợp. Chưa có quan sát trực quan/click hay chứng nhận nhiều màn hình.

[Bản sửa nhập tiếp](CONTINUATION.md) chọn ký tự văn bản sau toàn bộ equation rồi sang trái để lấy chế độ nhập văn bản; không thêm ký tự phân cách. Ma trận 30 ca và equation thứ hai kiểm hậu tố/native/metadata/Undo; phím VNI đã gõ nguyên văn ` cộng 1` ngoài equation và snapshot vẫn hợp lệ. Guard metadata so cả OMath đầy đủ, vì phần nhìn qua content control có thể bị cắt ngắn.

Đường thủ công kết thúc một lần chuyển để viết tiếp câu văn. [Hai biến thể Space mới](SPACE-NATIVE.md) giữ toàn bộ nguồn qua các từ: A chỉ preview đến khi chốt; B cập nhật một native nếu đủ điều kiện. Chuỗi mũ được gõ Telex thật; phân số/căn/A dùng Unicode nguyên văn qua Windows với Space thật. Đã sửa lỗi control nở vào phần gõ nối và lỗi observer lồng nhau. Nguồn thiếu/có repair không tự cập nhật; chọn rõ một kết quả mới chốt.

Đã bổ sung việc hết hiệu lực khi Word phát DocumentBeforeClose, kể cả người dùng hủy đóng: bài thử chạy event cancel ngay trong add-in trên luồng Word, giữ nguyên tài liệu và từ chối mọi thao tác từ phiên cũ. Phép thử hủy qua event subscription ở tiến trình ngoài không giữ được cancel trên baseline; không lấy lần đó làm PASS.

**D-01 vẫn mở**: Enter/Esc ở bản thử chỉ dừng phiên, không tự chốt thêm, Enter vẫn xuống dòng theo Word. Chưa lấy lựa chọn nghiên cứu này làm quy tắc sản phẩm. Bản thử cần người dùng tự hoàn thành ba chuỗi, Undo/sửa/viết tiếp và phản hồi. Cặp bọc `lc[...]` giữ đúng yêu cầu, tự ghi Word thuộc M5A và chưa bật.

## 6. Trạng thái backlog và bước tiếp

| Task | Kết quả hiện tại | Để đóng phần còn lại |
| --- | --- | --- |
| W0-01 | DONE trong phạm vi giao dịch đã thử | G1 vẫn cần UX/caret sau chuyển, xác nhận và ma trận sản phẩm M3 |
| W0-02 | DONE cho nghiên cứu và mục tiêu thủ công trên baseline | G2 vẫn thiếu bằng chứng hoàn tất nhập để tự chuyển; không dùng riêng IMM/quiet period |
| W0-03 | DONE trong phạm vi công bố | API + clipboard bằng phím, association/native sau paste; ma trận Word rộng tiếp tục M3/M6 |
| W0-04 | DONE cho connector/chẩn đoán research | IPC candidate hai chiều và startup sản phẩm tiếp tục ở M3/M6 |
| W0-05 | DOING; badge và kiểm geometry/scroll/zoom/source-expiry đã có | Nghiệm thu trực quan/click và DPI/màn hình thực theo phạm vi công bố |
| W0-06 | DOING; hai biến thể native và bằng chứng nhập/Undo đã có | Người dùng đang bận; chờ thực hiện checklist và chốt D-01 |

CW mở cho đường **chọn vùng thân tài liệu → xác nhận thủ công** trên Word x86 đã thử: giao dịch, nguồn, metadata, target/focus có bằng chứng. G1 chưa có luồng sản phẩm hoàn chỉnh; G2/G3 chưa được cấp quyền auto. M3-01 đủ đầu vào triển khai. W0-05/06 vẫn cần nghiệm thu trước các tính năng liên quan ở M4/M5B.

## 7. Chạy lại

Đóng Word rồi chạy từ root:

```powershell
./tools/w0/verify.ps1
# Thêm các bài UX; kích hoạt editor khi tài liệu fx xuất hiện:
./tools/w0/verify.ps1 -IncludeUxResearch
```

Runner dùng source build, không tải installer Office. Cài tạm W0 dưới HKCU, tạo tài liệu riêng, chạy adapter/lifecycle/Desktop, đợi Word thoát rồi gỡ đúng đăng ký W0. Không cưỡng bức đóng Word hoặc tài liệu khác. [Hướng dẫn thử tương tác](../../src/Locus.Word/README.md).

[Script ghi chỉ mục](../../tools/w0/record-acceptance.ps1) nhận rõ bốn đường dẫn report qua `-AdapterReport`, `-LifecycleReport`, `-DesktopLifecycleReport`, `-UxResearchReport`; không tự chọn thư mục mới nhất. Nó kiểm số kết quả, hash file, việc gỡ đăng ký/Word đã thoát và hash gói M2. [Chỉ mục nhập Space](../../artifacts/w0/native/space-verification.json) ghi riêng các giới hạn về phương thức nhập và bản build của từng đợt. Các run lỗi trước đó được giữ riêng để truy nguyên.

`prepare-trial.ps1` đã mở thành công fixture fx để người dùng nghiệm thu. Người dùng báo đang bận; tài liệu thử được đóng khi còn nguyên nguồn ban đầu và chưa có phím nhập. Không ghi PASS cho người dùng và không cài lịch nhắc. Có thể mở lại bằng script theo [checklist](PENDING-ACCEPTANCE.md).

Nguồn Microsoft dùng để chọn phép thử: [UndoRecord](https://learn.microsoft.com/en-us/office/vba/word/concepts/working-with-word/working-with-the-undorecord-object), [Selection.InsertXML](https://learn.microsoft.com/en-us/office/vba/api/word.selection.insertxml), [OMath.Remove](https://learn.microsoft.com/en-us/office/vba/api/word.omath.remove), [COMAddIn](https://learn.microsoft.com/en-us/dotnet/api/microsoft.office.core.comaddin?view=office-pia). Các tài liệu này mô tả API; kết luận PASS/giới hạn ở trên dựa vào artifact chạy thật của Locus.
