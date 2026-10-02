# W0-06 — Hai biến thể nhập nối trong Word thật

Ngày: 2026-09-13. Đã triển khai và thử bằng core M1, Microsoft Word x86 16.0.14026.20302. **Chưa có phản hồi người tham gia; D-01 và G3 vẫn mở.** [Bài thử cho người dùng](TRIAL-GUIDE.md).

## Hai biến thể đã chạy

| Hành vi | A — Giữ nguồn | B — Native theo Space |
| --- | --- | --- |
| Nơi gõ | Editor của Word, trong vùng nguồn đã khởi tạo | Cùng editor Word |
| Space | Cập nhật preview, giữ nguồn | Cập nhật một equation nếu CandidateSet có eligibility hợp lệ |
| Gõ nối sau khi đã có native | Chưa tạo native trước khi chốt | Ghép phần chữ mới với nguồn đã lưu, phân tích lại toàn phiên |
| Nguồn thiếu | Giữ nguồn | Giữ equation trước đó và phần chữ đang gõ |
| Có repair/interpretation/cảnh báo | Chờ chọn | Không tự cập nhật; chờ chọn |
| Chốt | Chèn native từ candidate đã chọn rồi dừng phiên | Cập nhật native từ candidate đã chọn rồi dừng phiên |
| Giữ văn bản | Giữ nguồn rồi dừng | Khôi phục toàn bộ nguồn, gồm khoảng trắng/NFD, rồi dừng |
| Undo cập nhật | Word Undo thao tác chốt | Trả về equation trước và phần chữ vừa nhập; dừng phiên |
| Enter/Esc | Dừng phiên, không chốt thêm; Enter vẫn xuống dòng | Cùng quy tắc |

Các nút trong tab **Locus W0** tạo sandbox mới, có tiền tố/hậu tố để kiểm nội dung ngoài vùng. Chúng không bật quan sát tự động ở tài liệu thường. B dùng khoảng yên lặng 300 ms cùng các tín hiệu input để chạy thí nghiệm; đây không phải bằng chứng UniKey đã hoàn tất ghép chữ. Cờ cho phép auto sản phẩm vẫn false.

Khung xem trước dùng chính cây candidate. Renderer GDI nghiên cứu có căn/mũ/phân số/ngoặc; chưa thay thế renderer Desktop đã nghiệm thu. Khi lựa chọn đã hết hạn, nút chốt từ chối. Metadata là CandidateSet đầy đủ, không parse lại nguồn để dựng phương án lịch sử.

## Bằng chứng

- [Suite UX cuối: 7 nhóm PASS](../../artifacts/w0/adapter-20260913T063845852Z/report.json): 5 nhóm Space, 2 nhóm badge. Phần Space replay từng từ qua Word API và **không đặt lại con trỏ trước khi TypeText**. Hai biến thể × ba chuỗi bắt buộc; chốt/Undo/Redo, repair không auto, nguồn NFD, phục hồi, Undo phiên cũ, đổi tài liệu, thay phần ngoài vùng và hủy đóng tài liệu.
- [6 nhóm nhập qua Windows PASS](../../artifacts/w0/native/space-verification.json): Telex nối `x mũ 2 cộng 1`; Ctrl+Z/Ctrl+Y; phân số và giữ văn bản; căn có repair; A giữ nguồn đến khi chốt; Esc/Enter giữ thao tác của Word.
- [Nhật ký Windows sau khi tiếp tục phiên](../../artifacts/w0/native/space-trial-keys-resumed.json), [quan sát Word của bản sửa cuối](../../artifacts/w0/native/space-observations-final.json). Các từ ở bài phân số/căn/A được nhập bằng Unicode nguyên văn qua Windows; không tính là kiểm composition Telex/VNI. Phần mũ dùng từng phím Telex.
- [Script kiểm bằng chứng đã ghi](../../tools/w0/record-space-evidence.ps1) đối chiếu nguồn/candidate/native và tạo chỉ mục có hash. Đây là kiểm chứng artifact đã thu, không tự chạy lại bàn phím.

Nguồn Telex được giữ đúng ở cả prefix `x mũ ` và `x mũ 2 cộng `; chúng không kích hoạt lần chuyển mới. Sau chuỗi hoàn chỉnh chỉ có một OMath và một control, nguồn trong Tag là `x mũ 2 cộng 1 `. Với `căn x cộng 1 `, native cũ vẫn là căn x cho đến khi chọn trực tiếp; đề nghị mở rộng phạm vi không bị tự chọn.

## Hai lỗi đã phát hiện và sửa

1. Import một wrapper `w:sdt` chứa cả metadata/native làm phần chữ gõ tiếp nở vào control ở mép phải trên baseline. Guard phát hiện `managed-range-expanded` và dừng. Cách tạo hiện tại chèn native rồi gắn content control vào range toán do Word trả, trong cùng custom Undo. Không nới guard để chấp nhận nội dung đã lệch. [Lần thử lỗi giữ lại](../../artifacts/w0/native/trial-b-physical-failure.json).
2. Trong một lần thử phân số, log có trạng thái dừng vì đổi cửa sổ rồi bị trạng thái của lượt quan sát bên ngoài ghi đè. Đã chặn observer/Tick chạy lồng nhau, dùng COM identity để kiểm tài liệu và gắn panel vào cửa sổ Word sở hữu nó. Sau sửa, bài nhập phân số/căn/A và suite UX đạt. Không quy kết một nguyên nhân nội bộ Word duy nhất từ log này. [Log trước sửa](../../artifacts/w0/native/space-observations-before-reentrancy-fix.json).

Các bài API đầu tiên đặt lại selection trước mỗi cụm nhập nên không kiểm đúng cách con trỏ được để lại sau native. Suite cuối bỏ bước đó và chia từng từ, để lỗi ranh giới không bị che bởi việc chuẩn bị test.

Rà cuối bổ sung guard DocumentBeforeClose: kể cả hủy đóng, phiên nhập đã hết hiệu lực. Phép thử phát sự kiện đóng Word thật và hủy ngay trên luồng add-in; tài liệu/native/Tag nguyên vẹn và ba lệnh replay/chốt/giữ nguồn từ phiên cũ đều bị từ chối. Các bài Windows trước đó không kiểm guard mới này; suite UX cuối có assertion riêng.

## Điều còn mở

W0-06 còn bước người dùng tự làm ba bài, viết tiếp câu văn, thử sửa/Undo và phản hồi cách kết thúc dễ hiểu. Không dùng kết quả Codex gõ tự động làm dữ liệu người tham gia. B phù hợp hướng mong muốn cập nhật native sau Space; đây là đề xuất cho vòng thử, chưa tự đóng D-01.

Chưa chứng nhận tốc độ gõ nhanh, mọi bộ gõ, sửa giữa cấu trúc native, giữ phiên sau Undo, tài liệu phức tạp hoặc crash recovery. Telex/Undo được ghi trước bản sửa observer cuối; các bài Windows sau đó và hồi quy tự động dùng bản có sửa này. Đoạn đầu nhật ký phím Telex sửa đúng bị mất khi phiên công cụ reset; snapshot Word tương ứng vẫn còn. Không tuyên bố có video/screenshot hay dữ liệu nhiều người thử.

Quyết định D-01 cần ghi rõ: biến thể được chọn; cách tiếp tục/kết thúc; cách Undo và trở về nguồn; bằng chứng người thử; các trường hợp bị từ chối. Nếu chưa có dữ liệu đó, M5B/G3 tiếp tục giữ cổng.
