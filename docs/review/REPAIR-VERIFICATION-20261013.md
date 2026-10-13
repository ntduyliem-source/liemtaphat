# Biên bản sửa lỗi Locus — 13/10/2026

Thực hiện [FIX0–FIX5](REPAIR-PLAN-20261013.md) trên baseline `4d4f91e4ca587dece7909596568f01e545b19a46`. Mã sửa nằm trong commit chứa biên bản này. FIX0–FIX4 đã triển khai; FIX5 đạt phạm vi logic/Web. Nghiệm thu native Desktop/Word còn mở. Các kết quả lịch sử không được dùng để thay bằng chứng của bản mới.

## Những gì đã thay đổi

| Lỗi | Hành vi sau sửa | Kiểm chứng chính |
| --- | --- | --- |
| R01 | Giữ nguyên lời gọi hàm chưa hỗ trợ như `sin (x+1)`; không lấy riêng đối số rồi làm mất ngoặc. `Tính (x+1)` và biểu thức độc lập phía sau vẫn được nhận. | Core detection; FormulaSession; Web |
| R02 | `10 kg*(m/s)^2` chỉ nâng mũ nhân tử `(m/s)`. `(10 kg)^2` vẫn nâng cả đại lượng. Lũy thừa đơn vị nối tiếp chưa hỗ trợ bị từ chối, không âm thầm đổi phạm vi. | Science AST và render; Web MathML |
| R03 | `H2+O2= 2 H2O` là một phương trình có vế phải; cân bằng không chèn thêm một sản phẩm cạnh sản phẩm cũ. RHS lỗi/không hiểu vẫn là RHS đã nhập. | SmartChemistry; CT4; Web cân bằng/Undo |
| R04 | Phân số trong ngoặc như `(x-1/2)` được phân biệt với đường dẫn; URL/email/path thực vẫn được bảo vệ. | Core; Application |
| R05 | Toán trong dấu nháy được nhận ở Toán và Tất cả; dấu nháy và câu ngoài công thức giữ nguyên. | Core; Application; Web |
| R06 | Không lấy `trừ x^2` hoặc `cộng x^2` thành công thức con khi prefix là token không hợp lệ. Dấu một ngôi hợp lệ vẫn dùng được. | Core; Application |
| R07 | Parser chỉ nhận nhân/chia vào đơn vị khi toàn nhân tử hợp lệ. `10 kg * 2`, `10 kg*(m+1)`, `10 kg/t` được trả về phép toán phù hợp. Đường dẫn như `10 kg/file.txt` vẫn được bảo vệ. | Science; detection/session; Web |
| R08 | Draft Hóa dùng số dưới Unicode như `H₂+O₂=` được nhận; giữ đúng vị trí và chuỗi nguồn. | SmartChemistry; Web tự điền/hủy |
| R09 | Các toán tử Unicode hiện có tham gia quy tắc nhận diện ngữ cảnh, nên `x · 28/09` không bị mất đuôi vì nhầm ngày. Ngày trong văn xuôi vẫn được bảo vệ. | Core; Application |
| R10 | Đường thẳng/tia giao với biên viewport thực; không kéo dài bằng hằng 3.000 px rồi biến mất khi điểm nằm xa. | Geometry AST/SVG và các viewport |
| R11 | Mặt lõm hợp lệ được chia tam giác bằng ear clipping; khe rỗng không che cạnh nằm phía sau. Mặt tự cắt/không phẳng/suy biến báo lỗi, giữ dữ liệu. | Geometry nét khuất và mặt lồi/lõm |
| R12 | Undo đầu tiên trong gesture chỉ hủy gesture. Điểm/thao tác đã hoàn tất trước đó còn nguyên; Redo không lùi/tiến lịch sử trong gesture. | Editor history; Web thao tác canvas |
| R13 | URL và HTML base là gốc ứng dụng ổn định; runtime/CSS/module/worker trỏ riêng vào release. Link release cũ được khôi phục bằng fallback giới hạn; asset thiếu vẫn báo lỗi. | Navigation; update với ứng dụng thật, root và `/liemtaphat/` |

Trong nghiệm thu Web phát hiện thêm **lỗi đổi môn rồi gõ ngay**: settings cũ được giữ trong lúc worker chạy, khiến input mới hủy lựa chọn môn. Đã sửa `ReanalyzeWithSettingsAsync` để ghi lựa chọn ngay, lưu một bước lịch sử và chỉ gắn kết quả khi nguồn/phiên còn hiện hành. Có ca kiểm trì hoãn worker, lỗi phân tích, Undo khi đang chờ và thao tác gõ nhanh trên Web.

Giữ thiết kế Studio đã duyệt. Không đưa nhãn fx, checkbox tự cân bằng, nháp hoặc chi tiết gợi ý trở lại UI.

## Tương thích và giới hạn grammar

- Bốn fixture được tạo từ DLL baseline trước khi sửa, kèm SHA-256: công thức chọn repair, đoạn Hóa có quyết định cân bằng/tự điền/giữ text, snapshot Lý lịch sử và mặt hình học lõm. Test kiểm mở/lưu byte-exact, lựa chọn/ID/nguồn và Undo sau mở; không gọi analyzer khi restore.
- Không đổi schema hoặc hằng grammar trong đợt này. Snapshot cũ giữ đúng quyết định đã lưu, kể cả snapshot Lý có phạm vi số mũ cũ; mở file không tự sửa. Muốn có cách hiểu mới phải phân tích lại có chủ ý.
- Validator mới nhận `Power` trên cây đơn vị ghép đang được biểu diễn bằng node hiện có. Bản đọc cũ có thể từ chối snapshot mới chứa dạng này. Không cam kết file mới mở được bằng mọi bản cũ.
- `sin/log` chưa trở thành grammar mới. Đây là chặn biến đổi sai, không phải bổ sung hàm.
- Tự điền chỉ áp dụng khi RHS thực sự trống. `H2+O2= trong bài.` có chữ phía sau `=` nên không còn bị đoán là draft trống. Dùng dấu phân cách rõ như `H2+O2=; trong bài.` hoặc cặp bọc khi đặt câu ngay sau draft. RHS `2`, `.5H2O`, `H2O+`, chất chưa hỗ trợ không bị thay bằng sản phẩm suy đoán.
- Catalog phản ứng giữ phạm vi hiện có; không suy phản ứng tổng quát từ mọi tổ hợp chất.

## Kiểm thử và cách chạy lại

Bằng chứng máy nằm trong `artifacts/repair-20261013` (không đưa build/binary/profile vào Git). Suite có phần chồng lặp nên không cộng thành một tổng độ phủ.

| Phạm vi | Kết quả | Bằng chứng |
| --- | --- | --- |
| Core | 246/246 | `portable-regression-final.log` |
| Science | 431 core + 19 application; 16 ca sửa Lý | `portable-regression-final.log` |
| SmartChemistry | Suite hiện hành và 30 ca sửa draft đạt | `portable-regression-final.log` |
| Application | Default, detection, content, CT4, balance, Studio, export, plot, geometry đạt | `portable-regression-final.log` |
| Snapshot lịch sử | 4/4 | `compatibility.json` tại đầu ra suite |
| Editor | DOM/selection/IME guard và history hình học đạt | `portable-regression-final.log` |
| Web sử dụng thực tế | Nhận diện, đổi môn/gõ nhanh, Hóa, Undo/Redo, gợi ý, SVG/PNG, clipboard PNG, Đồ thị, Hình học đạt | `web/results.json`, `web/*.png`, SVG/PNG xuất |
| Navigation | 10/10 trên root và project path | `navigation` |
| Update ứng dụng thật | 6/6 cho mỗi base; không có resource request ngoài release | `full-update/results.json`, `full-update/project-results.json` |
| Static release | Manifest/hash khớp đủ asset và renderer | `artifacts/web1/static-validation.json` |
| Design system | Check catalog/source đạt | `node tools/design-system/check.mjs` |

Corpus hiện hành còn 8 mục pending có sẵn; các guard native Word không được asserted bằng corpus logic. Những mục này không được đổi thành PASS.

Lệnh chính:

```powershell
npm --prefix tools/sh ci --ignore-scripts
node tools/web1/vendor.mjs
./tools/review/verify-repairs.ps1
./tools/web1/build.ps1 -WebOnly
node tools/web1/verify-static.mjs
node tools/web1/verify-release-navigation.mjs
node tools/web1/verify-update.mjs
```

`verify-update.mjs` nhận thêm `[outputRoot] [buildManifest]` để kiểm bản dựng khác hoặc project-path mà không ghi đè current-build. `build.ps1` có `-OutputRoot` để đặt output trên ổ đủ dung lượng. Smoke UI dùng server local đã chạy:

```powershell
$env:LOCUS_REPAIR_URL='http://127.0.0.1:4204/'
node tools/review/verify-web-repairs.mjs
```

Workflow Pages chuẩn bị dependency renderer từ lockfile trước khi build Editor tests trên checkout sạch, rồi chạy regression portable, static validation, navigation và cập nhật ứng dụng thật trước upload/deploy. Lượt CI đầu đã chặn vì thứ tự chuẩn bị renderer thiếu; đã sửa thứ tự, không bỏ cổng test. Lỗi kiểm phải dừng phát hành.

## Phạm vi từng sản phẩm

**Web:** bản local chốt `20261013-045105-617`, ứng dụng phục vụ ở `http://127.0.0.1:4204/` sau khi cập nhật server. Release online sẽ có ID riêng do workflow dựng từ commit; lấy commit, kết quả job và ID trên trang để xác định bản live. URL chia sẻ chính là `https://ntduyliem-source.github.io/liemtaphat/`. Các build trung gian không phải bộ cài Desktop.

**Desktop:** `Desktop.Shared` và Catalog build Release đạt, không cảnh báo/lỗi; Desktop tests 40 PASS/0 FAIL. Đây là build và logic host. Đợt này chưa tạo bộ cài mới hoặc nghiệm thu IME/clipboard/Undo/lưu mở trong cửa sổ Desktop chứa đúng bản sửa cuối. Phần đó vẫn mở.

**Word:** build và smart-contract 13/13 + scan-contract 7/7 đạt. Native harness đã mở tài liệu thử nhưng không đưa được focus vào editor Word, nên 0 PASS/1 lỗi điều kiện tiên quyết. Công cụ Windows báo không xác định được process của cửa sổ foreground. Tài liệu thử đã đóng; không sửa tài liệu người dùng.

Add-in đăng ký trên máy vẫn trỏ tới gói `Locus-F-20260929-051756-959-review-local-Word-x86`; Core DLL khác bản mới. Vì vậy không tính runtime Word hiện tại là nghiệm thu sửa lỗi. Bằng chứng: `word-native-focus-retry-elevated/report.json`, `computer-use-limitation.json`, `registered-addin-provenance.json`.

## Những việc còn lại

1. Phát hành Web bằng workflow đã có cổng kiểm; xác minh bản live và link release cũ từ browser chưa có cache. Nếu phát hành lỗi, dùng artifact/commit ổn định trước; không thay đổi dữ liệu người dùng khi rollback.
2. Đóng gói Desktop chứa core mới, rồi kiểm native IME, clipboard sang phần mềm khác, lưu/mở và Undo.
3. Đăng ký Word build mới bằng installer/luồng đóng gói hiện có; xác nhận DLL trước native test. Kiểm equation/metadata/Undo, focus/IME, stale source và Find state.
4. C01–C08/H/WEB2 tiếp tục là phạm vi riêng sau đợt ổn định; không tính cổng host còn thiếu là đã hoàn thành.
