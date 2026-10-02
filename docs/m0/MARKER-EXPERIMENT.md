# M0 — Dấu vùng tùy chỉnh và tự chuyển khi đóng

Ngày: 2026-09-12. Trạng thái: **prototype local đã có, kiểm thử state machine và HTTP đạt; chưa có bằng chứng Word từ thử nghiệm này**.

Yêu cầu mới của người dùng đặt mặc định thành **`lc[...]`**: hai chuỗi dấu có thể chỉnh trong cài đặt, tự nhận diện/chuyển khi dấu đóng đã đầy đủ. Trang [`markers.html`](../../prototypes/m0-space/markers.html) cụ thể hóa yêu cầu này bằng demo trình duyệt; [trang Space](../../prototypes/m0-space/index.html) có liên kết sang đây. D-01 về gõ nối tiếp sau Space vẫn mở, không phải điều kiện chặn thử nghiệm dấu vùng.

## Ranh giới mô phỏng

Textarea giữ nguyên nguồn để đối chiếu. “Tài liệu xem trước” thay đúng vùng có cả dấu bao bằng MathML từ candidate của bảng fixture; đây là lớp hiển thị trong trình duyệt, **không phải native equation Word hoặc thay trực tiếp textarea**. Các câu văn trước/sau vùng vẫn giữ nguyên trong preview.

`marker-state.js` là bộ trạng thái pure dùng lại fixture mapper trong `state-machine.js`. Nó không cài đặt grammar production hay source map production. Thêm một fixture `x^2` để dùng ví dụ `lc[x^2]`; tổng bảng có bảy nguồn mẫu. Input khác như `lc[x^3]` giữ văn bản và ghi “ngoài bộ mẫu”, không suy luận Locus sẽ từ chối nó trong bản thật.

Không framework, package ngoài, CDN, lưu trữ trình duyệt hay kết nối Word. Nhật ký là tổng hợp từ state machine, không phải dữ liệu phím thật. Các kiểm tra URL/email/path dùng một tập nhận diện nhỏ cho bài thử, không chứng minh độ bao phủ mọi dạng địa chỉ hoặc đường dẫn. Chưa có kiểm chứng focus, bộ gõ Windows, Word Range hoặc transaction native.

## Hành vi đã dựng

| Tình huống | Kết quả mô phỏng |
| --- | --- |
| `Ta có lc[x^2` | Giữ nguyên, chờ dấu `]` |
| `Ta có lc[x^2] và tiếp tục viết.` | Preview tự hiện `Ta có x² và tiếp tục viết.`; giữ nguyên khoảng trắng và văn xuôi ngoài vùng |
| Dấu tùy chỉnh `<<` và `>>`; mới gõ `<<x^2>` | Chưa chuyển vì dấu đóng chưa đủ |
| Dấu tùy chỉnh `<<` và `>>`; gõ `<<x^2>>` | Có một direct đủ điều kiện, tự dựng trong preview |
| `lc[căn x cộng 1]` | Direct `√x + 1` đứng đầu; repair `√(x + 1)` chỉ hiện sau `fx`. Không auto do có đề nghị sửa |
| `lc[2/3x]` | Direct và interpretation cạnh tranh; cần mở `fx` và chọn |
| Chọn một candidate qua `fx` | Chuyển theo đúng candidate; lưu toàn bộ bộ kết quả, không chỉ phương án đang thấy |
| Khôi phục nguồn | Preview trả nguyên văn cả hai dấu và whitespace trong vùng; không tự chuyển lại từ sự kiện restore |
| Sửa văn xuôi ngoài vùng vừa restore | Vùng đó tiếp tục được giữ nguồn; tránh chuyển lại chỉ vì thay chữ ở chỗ khác |
| Sửa bên trong vùng đã restore, hoặc bấm “Nhận diện lại · chủ động” | Có thể xét lại vùng, vẫn phải qua các điều kiện hiện tại |
| Đổi cài đặt dấu khi tác vụ đang chờ | Hủy pending. Gửi lại tác vụ cũ bị từ chối vì config/source/task không còn khớp |
| Composition DOM đang mở | Hủy pending, không nhận diện mới và không chuyển; xét lại sau sự kiện kết thúc |
| Undo conversion | Khôi phục nguồn có wrapper trong preview, không chạy lại conversion do chính Undo |

Hai dấu là **literal**: không rỗng, không trùng nhau, không dấu nào là prefix của dấu kia; không có CR/LF/backslash. Cài đặt lỗi giữ cấu hình hợp lệ đang dùng và hủy pending; không quét/chuyển do nút áp dụng lỗi. Cài đặt hợp lệ mới cũng không tự quét tài liệu cũ; chờ nhập tiếp hoặc lệnh nhận diện lại.

V0 từ chối toàn vùng lồng, backslash ngay trước dấu mở/đóng, vùng rỗng, nội dung nhiều dòng; không chuyển vùng con của một vùng lồng. Các mẫu marker trong URL/email/đường dẫn được bảo vệ trước khi dò nội dung. Dấu đóng quá sớm hoặc nội dung ngoài bảng fixture không được sửa để tự chuyển.

## Cách chạy và thử

Từ root dự án, dùng cổng mới để không ảnh hưởng preview Space đang chạy ở 4175:

```powershell
$env:LOCUS_SPACE_PORT = '4176'
node prototypes/m0-space/serve.cjs
```

Mở `http://127.0.0.1:4176/markers.html`. Server chỉ bind localhost và phục vụ asset trong allowlist; không phục vụ test hay file tùy ý. Có thể mở file HTML trực tiếp bằng trình duyệt vì các script là classic; đường file chưa được QA ở đợt này.

```powershell
node --test prototypes/m0-space/marker-state.test.cjs prototypes/m0-space/state-machine.test.cjs
```

Các bài trong UI dùng nút “Bước tiếp” để thấy thời điểm trước/sau khi đóng dấu. Thử thay dấu rồi bấm “Làm lại bài” để bài thử dùng cấu hình mới.

Để thử stale config: bật “Giữ tác vụ trước bước chuyển”, chạy bài văn xuôi, đổi dấu mở/đóng rồi bấm “Áp dụng dấu”, sau đó bấm “Áp dụng tác vụ đang giữ”. Nhật ký phải ghi từ chối tác vụ cũ và không thêm equation. Chế độ giữ task chỉ phục vụ thử nghiệm race; không phải một tính năng cài đặt sản phẩm đề xuất.

## Kết quả kiểm tra và phần chưa xác nhận

Node.js v24.15.0 trên Windows: **19/19 test marker pass, 20/20 test Space vẫn pass; tổng 39/39**. Test marker kiểm tra đóng đủ, literal marker, cấu hình lỗi/đổi cấu hình, source revision hết hạn, scope văn xuôi, vùng lồng/escape/protected token, repair/interpretation, restore, Undo, remap sau sửa phía trước, nhiều vùng độc lập và composition tổng hợp.

`node --check` đạt cho `marker-state.js` và `marker-app.js`. Kiểm tra HTTP tại localhost: HTML/CSS và bốn script/style phụ thuộc trả 200 đúng MIME; asset ngoài allowlist trả 404.

**QA giao diện marker đã thực hiện bởi root agent trong Codex in-app browser**, sau khi browser khả dụng trở lại. Trang tại cổng 4176 tải đúng; ảnh bố cục đã được kiểm trực quan, không thấy chồng lấn. Chạy bài văn xuôi dựng MathML `x²` đúng giữa hai phần câu văn giữ nguyên. “Khôi phục nguồn” trả lại cả `lc[x^2]`, trạng thái về 0 equation và 1 vùng giữ nguồn. Đổi cấu hình sang `<<` / `>>`, nhập `Ta có <<1 trên 2>> rồi tiếp.` cho phân số đúng và giữ nguyên phần ngoài vùng. Thay bằng `Ta có <<2/3x>> rồi tiếp.` giữ nguyên văn bản, có 0 equation và 1 vùng cần chọn; bấm `fx` hiện direct `(2/3) × x` và interpretation `2/(3x)`.

QA cấu hình hết hạn cũng đạt trong UI: bật giữ tác vụ, nhập `Ta có <<x^2>> rồi tiếp.` tạo 1 pending và 0 equation; đổi dấu về `lc[` / `]`, rồi bấm áp dụng tác vụ đang giữ. Pending về 0, nguồn `<<x^2>>` còn nguyên, vẫn 0 equation. Cuối lượt kiểm tra đã đưa trang về bài văn xuôi với dấu mặc định, preview `x²` đúng; tab được giữ làm kết quả cho người dùng.

Các quan sát trên là thao tác UI thật đối với **fixture demo trong browser**, không phải dữ liệu Word hoặc bộ gõ tiếng Việt hệ thống. QA giao diện Space ở báo cáo trước là bằng chứng riêng.

Undo trong demo theo snapshot của action, chưa có Redo hoặc gom theo từ. Các equation trong preview không sửa native trực tiếp; sửa textarea nguồn sẽ bỏ metadata mô phỏng của vùng bị tác động rồi nhận diện lại. Việc remap dựa trên khác biệt một đoạn nguồn, không thay thế thuật toán nguồn/Range trong Word. Bảng fixture không có node/source references hoặc repair edit đầy đủ như hợp đồng core production.

Các bước còn lại: thử gõ thực tế vào Word qua connector đã chọn; kiểm native equation, metadata cả bộ candidates, restore nguyên wrapper, focus/composition, dữ liệu cũ và một Undo hoàn chỉnh. Default `lc[...]` cùng cấu hình hai dấu là yêu cầu đã nhận; khả năng Word đạt các cổng G1/G2 vẫn phải dựa vào báo cáo Word riêng.
