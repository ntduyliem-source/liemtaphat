# SC1 — Cặp bọc theo môn và Hóa thông minh

Kế hoạch ngày 2026-09-15, theo trao đổi tiếp sau E3. Người dùng đã yêu cầu triển khai SC1. E3 vẫn đạt 8/8 trong phạm vi đã nghiệm thu. Trạng thái công việc chỉ ghi tại [backlog](../BACKLOG.md); bộ bài cần đạt nằm ở [ACCEPTANCE](ACCEPTANCE.md), quy tắc/version triển khai nằm ở [CONTRACT](CONTRACT.md).

## 1. Kết quả cần đạt và thứ tự thực hiện

Người dùng chọn cách gõ thuận tay: dùng cặp chung để tự nhận diện, hoặc cặp riêng để chỉ định Toán/Lý/Hóa. Trong vùng Hóa, người dùng có thể viết đủ hai vế để nhận bản cân bằng, hoặc viết chất ban đầu và dấu phân cách để nhận gợi ý sản phẩm đã cân bằng. Nguồn đang gõ, gợi ý và kết quả đã nhận phải phân biệt được.

Thứ tự theo kế hoạch mới: **SC1 nền/cặp bọc → cân bằng → suy sản phẩm có điều kiện → ghost trên Web/Desktop → Word thủ công và gói thử → E1 đồ thị/thanh trượt.** Ghost trực tiếp trong thân Word theo nhánh SC1-11, sau các cổng Word liên quan. Đó là thứ tự ưu tiên, không tạo phụ thuộc kỹ thuật giả cho E1.

Web tiếp tục local và xử lý trên thiết bị; Desktop dùng cùng core và logic editor. Không mở thêm editor môn học. Kho phản ứng và quy tắc được đóng gói để dùng offline. E3, WEB1 và gói cũ được giữ để đối chiếu.

## 2. Yêu cầu đã có và baseline cần thử

| Nội dung | Mức độ chốt cho kế hoạch |
| --- | --- |
| Cặp chung và cặp riêng theo môn, tùy chỉnh dấu mở/đóng | Hướng đã được người dùng đồng ý đưa vào kế hoạch |
| Cặp riêng chỉ định môn cho đúng vùng, kể cả khi checkbox tự nhận diện môn đó tắt | Quy tắc đã giải thích và được đưa vào kế hoạch; không đổi checkbox toàn cục |
| Cân bằng phương trình đủ hai vế; gợi ý sản phẩm khi chỉ có vế đầu | Yêu cầu chức năng mới; độ phủ phải công bố bằng corpus |
| Ghost sau dấu `=`/mũi tên, nhận để sử dụng kết quả và cân bằng | Trải nghiệm người dùng yêu cầu; chi tiết phím cần prototype |
| Enter nhận; Space thường là khoảng trắng; Space nhận là tùy chọn riêng | Baseline đề xuất để thử, không gọi là quyết định người dùng đã nghiệm thu |
| Tên cặp `toan-[…]`, `ly-[…]`, `hoa-[…]` | Preset minh họa, có thể chỉnh trong cài đặt |
| Default cũ `lc[...]` | Giữ nguyên khi nâng cấp. Ví dụ `lc-[…]` không tự đổi cấu hình đang có |
| Suffix `::hoa`, `::ly`, `::toan` | Không làm trong SC1; người dùng đã làm rõ rằng họ nói về cặp bọc |

Triển khai theo thứ tự SC1-01 rồi SC1-02; chỉ gọi chức năng đã đạt khi có bằng chứng thực tế, không từ riêng bản kế hoạch.

## 3. Cặp bọc và cách định tuyến

### 3.1. Bốn mục trong cài đặt cặp bọc hiện có

| Mục | Ví dụ | Cách đọc |
| --- | --- | --- |
| Chung | `lc[x mũ 2]`; có thể cấu hình thành `lc-[x mũ 2]` | Phân tích trong các môn có checkbox đang bật |
| Toán | `toan-[x+1=2]` | Chỉ parser Toán |
| Lý | `ly-[v=10 m/s]` | Parser Lý, bao gồm đại số chung cần thiết |
| Hóa | `hoa-[H2SO4]` | Chỉ parser Hóa và hỗ trợ Hóa trong phạm vi công bố |

Mỗi mục lưu ID ổn định, mục đích `auto/math/physics/chemistry`, dấu mở/đóng literal và trạng thái hiệu lực. Cài đặt có tối đa bốn mục trong bản đầu; không tạo các cặp ẩn hoặc alias ngoài danh sách người dùng thấy. Ô nguồn, preview, fx, xuất, nháp và ba checkbox vẫn ở vị trí hiện tại. Mở rộng hộp cài đặt cặp bọc, không thêm tab môn học.

Ưu tiên theo vùng: cặp riêng hợp lệ → môn đã chỉ định; cặp chung/không có chỉ định → tập checkbox. Tắt cả ba checkbox dừng tự nhận diện nguồn mới, nhưng cặp riêng là yêu cầu tường minh cho vùng đó. Đây là ngoại lệ có chủ đích cho cú pháp mới; chuỗi thường và cặp chung khi tắt hết vẫn giữ hành vi E3. File chứa snapshot của môn đang tắt tiếp tục xem/xuất được.

Chỉ định Hóa không làm một chuỗi sai trở thành đúng, không chọn hộ `co`, không cấp quyền suy sản phẩm hoặc tự ghi Word. Gõ cặp không đăng ký giữ nguyên văn bản; không đoán tên môn gần giống.

Giữ ba Cách đọc hiện có. Trong **Một công thức**, cho phép toàn bộ nguồn là một cặp hợp lệ, hoặc một cặp đang soạn bắt đầu ở đầu nguồn; văn xuôi/nhiều cặp cần chuyển sang chế độ thích hợp. Trong **Vùng có cặp bọc**, quét các mục đã đăng ký. Trong **Nhận diện trong câu**, cặp rõ có ưu tiên trên vùng của nó, phần ngoài theo checkbox; vùng cặp lỗi/chưa đóng được giữ để bộ dò không lén chuyển một đoạn con. Mở rộng này cần corpus delta riêng, không đổi nghĩa snapshot cũ.

### 3.2. Ranh giới và cấu hình không hợp lệ

- Giữ các kiểm tra cũ: dấu không rỗng, độ dài trong giới hạn, Unicode hợp lệ, không CR/LF/backslash; mở và đóng không xung đột. Kiểm cả tập cấu hình trước khi lưu, không áp dụng nửa cấu hình.
- Dấu mở giữa các mục phải phân định được: không trùng, không prefix hoặc chồng lấn khiến cùng vị trí khớp nhiều mục. Dấu đóng có thể dùng chung `]` vì cặp mở đã xác định mục. Trường hợp cấu hình không phân định được phải bị từ chối với vị trí xung đột.
- Không hỗ trợ cặp bọc Locus lồng nhau trong SC1. Gặp cặp riêng lồng trong cặp chung hoặc ngược lại thì giữ toàn vùng, không chuyển riêng phần bên trong.
- Ngoặc của công thức khác cặp bọc Locus. Với dấu đóng `]`, phải nhận đủ `hoa-[K4[Fe(CN)6]]`: `]` của nhóm Hóa không kết thúc vùng bên ngoài. Thêm scanner có theo dõi ngoặc thuộc nội dung; nếu cấu hình tùy chỉnh làm ranh giới không xác định được, báo mơ hồ và giữ nguồn. Không lấy dấu đóng đầu tiên một cách máy móc.
- Giữ `ContentSpan` cho phần thân và `ReplacementSpan` cho cả cặp; raw UTF-16/NFC/NFD/emoji, khoảng trắng, tên cặp và vị trí đều phải khôi phục được.
- URL/email/path được bảo vệ. Nhiều vùng cạnh nhau không đè lên nhau; không chuyển hàng loạt nội dung cũ khi đổi cài đặt.

### 3.3. Vùng đang gõ và vùng đã đóng

Cần hai trạng thái khác nhau: vùng đang soạn có thể cung cấp ngữ cảnh Hóa để hiện ghost; vùng đã đóng mới đủ đầu vào cho giao dịch thay nguyên vùng. Không dùng vùng đang soạn làm `CandidateSet` hoàn chỉnh hoặc làm điều kiện auto Word.

Ví dụ `hoa-[h2+o2=` có thể hiện gợi ý khi đủ dữ kiện. Nhận gợi ý trong Web/Desktop chỉ cập nhật phần thân đang soạn; không tự thêm `]`. Người dùng đóng cặp để kết thúc vùng. Nếu cặp đã đóng, nhận gợi ý giữ nguyên hai dấu đã dùng. Không xóa cặp hoặc kéo thêm chữ ngoài vùng khi điền kết quả.

## 4. Cách gõ Hóa

### 4.1. Chữ hoa/thường và lỗi gõ

- Công thức chuẩn hợp lệ giữ nguyên cách phân tách nguyên tố: `Co` không đổi thành `CO`; `No` không đổi thành `NO`.
- Thêm cách gõ chữ thường khi chỉ có một cách phân tích hợp lệ trong ngữ cảnh Hóa, như `h2`, `o2`, `h2o`. Giữ nguyên chuỗi đã gõ; cách viết chuẩn nằm trong cấu trúc và kết quả xuất. Các cách gõ mới có phiên bản grammar riêng.
- `co`, `co2`, `no` có nhiều cách phân tách: không chọn theo độ phổ biến rồi gọi là chắc chắn. Hiện lựa chọn hoặc yêu cầu viết rõ ký hiệu. Không sinh hàng loạt tổ hợp hoa/thường khi chuỗi dài; đặt ngân sách phân tích.
- `h20` chứa số 0; không đồng nhất với `h2o`. Nếu có gợi ý sửa thì ghi rõ ký tự đổi. Chỉ sau khi người dùng chọn bản sửa mới tính bước cân bằng/suy phản ứng; không gộp sửa lỗi âm thầm vào ghost một phím.
- Không suy tên chất tự do như “axit…” hoặc cấu trúc phân tử từ công thức tổng quát trong SC1. Các alias Toán/Lý cũ vẫn giữ nghĩa.

### 4.2. Dấu phân cách phản ứng

| Đầu vào | Xử lý dự kiến |
| --- | --- |
| `H2+O2->H2O`, `H2+O2→H2O` | Phản ứng đủ hai vế; có thể cân bằng |
| `h2+o2=h2o` trong vùng Hóa | `=` là cách gõ mũi tên một chiều; output chuẩn dùng `→`, raw giữ nguyên |
| `h2+o2=` trong vùng Hóa | Bản nháp thiếu sản phẩm; xét hỗ trợ suy phản ứng, không giả là phương trình đã hoàn chỉnh |
| `x+1=2` ở Toán, `v=10 m/s` ở Lý | Dấu bằng giữ nghĩa cũ, không gọi bộ suy Hóa |
| `⇌` / `<->` | Giữ mũi tên thuận nghịch; không suy cân bằng hóa học hoặc tự đổi sang một chiều |
| Hai dấu phân cách hoặc vế trái chưa đủ | Không hoàn thành một phần; yêu cầu sửa nguồn |

Ngoài cặp Hóa, chỉ kích hoạt hỗ trợ nếu ngữ cảnh đã xác định là Hóa. Dấu `=` tự nó không đủ. Trong cặp chung có nhiều miền hợp lệ, phải chọn miền/cách đọc trước khi ghost nhận một phím được bật.

Tách hệ số phương trình khỏi lượng chất thực tế. Hệ số đã gõ trong phương trình đầy đủ là dữ liệu cần kiểm/cân lại; không mặc định coi đó là số mol hoặc bằng chứng “chất dư” khi suy sản phẩm.

## 5. Cân bằng đủ hai vế

Đầu vào là cấu trúc Hóa đã được xác định, có danh sách chất ở cả hai vế. Dùng đúng AST hiện có để đếm nguyên tố, nhóm và điện tích; không viết parser thứ hai trong bộ cân bằng.

1. Lập các phương trình bảo toàn từng nguyên tố; có ion thì thêm bảo toàn tổng điện tích.
2. Giải bằng số nguyên/phân số chính xác, tránh quyết định hệ số bằng sai số số thực.
3. Nếu có một tỉ lệ nghiệm dương duy nhất, chuẩn hóa về hệ số nguyên dương tối giản.
4. Kiểm lại độc lập số nguyên tử và điện tích của kết quả trước khi phát hành đề xuất.
5. Phân biệt: đã cân bằng; có bản cân bằng; không có nghiệm giữ nguyên các chất; nhiều bậc tự do; vượt ngân sách. Với nghiệm không xác định duy nhất, không tự chọn “bộ số nhỏ nhất” rồi gọi là phản ứng duy nhất.

Chỉ thay hệ số, giữ công thức chất, điện tích, thứ tự hai vế và loại mũi tên. Không tự thêm H2O/H+/OH-/electron, bỏ chất có hệ số 0, đổi sản phẩm hoặc bổ sung nguyên tố để cứu một phương trình. Phương pháp bán phản ứng và tự bổ sung môi trường là đợt sau.

Ví dụ cần đạt: `H2+O2->H2O` thành `2H2+O2->2H2O`; ion `Fe^2++Ce^4+->Fe^3++Ce^3+` bảo toàn cả điện tích; `H2->H2O` báo không có nghiệm trong các chất đã nêu. Dạng đã cân bằng không sinh một đề xuất trùng để lấp danh sách.

Cân bằng xác nhận bảo toàn, không xác nhận phản ứng thực sự xảy ra. UI chỉ dùng nhãn “Đã cân bằng”, không dùng “Phản ứng đúng” từ riêng kết quả bộ giải.

## 6. Suy sản phẩm có điều kiện

### 6.1. Kho tri thức cục bộ và phạm vi đầu

Dùng kho phản ứng/quy tắc có nguồn kiểm chứng, cùng bộ cân bằng ở mục 5. Mục tiêu soạn bộ khởi đầu khoảng 40–60 bản ghi đã kiểm; đây là mục tiêu chuẩn bị dữ liệu, không phải số phản ứng đã hỗ trợ. Báo cáo độ phủ theo các bài giữ riêng để kiểm, không lấy việc khớp chính dữ liệu nguồn làm độ chính xác dự đoán.

Các nhóm để tuyển chọn: trung hòa; oxide với acid/base; carbonate với acid; tạo kết tủa; thế kim loại theo điều kiện được khai báo; tổng hợp đơn giản; phân hủy có điều kiện; cháy hoàn toàn của một tập hydrocarbon rõ cấu trúc. Mỗi nhóm chỉ được bật cho các quy tắc/chất/ngoại lệ đã kiểm; không áp một mẫu cho toàn bộ hóa học.

Mỗi bản ghi có ID/version, chất và danh tính cần thiết, kiểu phản ứng, điều kiện bắt buộc, sản phẩm, ngoại lệ, nguồn tham khảo, ví dụ dương và ví dụ không áp dụng. Dữ kiện phổ thông được tự biên soạn có dẫn nguồn; kiểm giấy phép trước khi nhập dữ liệu/code bên ngoài.

Không dùng một mô hình ngôn ngữ tự phát sinh sản phẩm rồi xem bảo toàn nguyên tử là bằng chứng đúng. Dự đoán phản ứng hữu cơ tổng quát từ công thức phân tử, cơ chế nhiều bước, hiệu suất và điều kiện thí nghiệm chi tiết chưa thuộc SC1.

### 6.2. Luồng quyết định

Nguồn Hóa đã rõ → xác định chất → tìm quy tắc phù hợp → xét điều kiện → tạo sản phẩm → cân bằng → kiểm bảo toàn → đưa đề xuất cùng căn cứ/điều kiện.

Phải trả về các trạng thái phân biệt: có một đề xuất đủ dữ kiện; nhiều khả năng; cần thêm điều kiện; không có phản ứng trong điều kiện và quy tắc đã kiểm; chưa có dữ liệu hỗ trợ; vượt giới hạn. “Không tìm thấy trong kho” không được hiển thị thành “Không phản ứng”.

Điều kiện có cấu trúc, gắn với vùng và phiên nguồn: môi trường, tỉ lệ/chất dư, nhiệt hoặc tác nhân cần thiết trong phạm vi bản ghi. Không lén thêm các điều kiện đó vào lập luận. Khi thiếu, mở phần lựa chọn nhỏ trong fx; không thêm một màn hình Hóa riêng và chưa thêm DSL điều kiện vào ô gõ.

Ví dụ `CO2+NaOH=` cần phân biệt điều kiện/tỉ lệ trước khi chọn sản phẩm. `H2+O2=` chỉ hiện một ghost nhận được khi bộ quy tắc xác định được điều kiện cần thiết; nếu chưa đủ thì hiện trạng thái cần điều kiện. Một giả định được trình bày để người dùng chọn không phải dữ kiện họ đã nhập.

## 7. Ghost, các loại kết quả và thao tác nhận

### 7.1. Vị trí và nội dung

Giữ source editor và preview hiện có. Ghost nằm trên lớp hiển thị riêng, không nằm trong textarea/Word range, nháp hoặc clipboard trước khi nhận. Nó chỉ hiện ở cuối vế phải đang trống, con trỏ đúng vùng, không có selection/composition và đề xuất còn khớp nguồn. Nếu người dùng đã gõ sản phẩm, hỗ trợ cân bằng xuất hiện qua fx, không che chữ họ đang viết.

Ghost vế phải phải đi kèm preview toàn phương trình sau cân bằng, đánh dấu hệ số sẽ đổi ở vế trái. Không để người dùng chỉ thấy H2O rồi sau Enter mới phát hiện vế trái đã bị sửa. Thiết bị dùng screen reader được thông báo có đề xuất và cách nhận; focus không tự nhảy.

Tổng các phương án mà người dùng có thể chọn trong một vùng vẫn tối đa ba. Kết quả đọc đúng nguồn đặt trước nếu có; mơ hồ cách đọc cần giải quyết trước đề xuất tính toán. Các kết quả hỗ trợ có nhãn riêng như “Cân bằng” / “Bổ sung sản phẩm”, không giả làm `direct` hoặc `interpretation`, và không biến tất cả thành `repair`. Không tạo thêm một danh sách ba kết quả khác cạnh danh sách cũ. Nếu còn nhiều khả năng ngoài giới hạn, ghi thiếu điều kiện và không dùng việc cắt danh sách làm bằng chứng duy nhất.

### 7.2. Bàn phím cho prototype Web/Desktop

| Thao tác | Khi ghost hợp lệ đang hiển thị | Khi không có ghost hợp lệ |
| --- | --- | --- |
| Enter | Nhận đúng đề xuất đang thấy, tiêu thụ lần phím này; không xuống dòng đồng thời | Giữ hành vi nhập hiện có |
| Space mặc định | Gõ khoảng trắng; không nhận | Gõ khoảng trắng |
| Space khi người dùng bật tùy chọn riêng | Nhận và thêm một khoảng trắng sau kết quả, trong cùng một Undo | Gõ khoảng trắng |
| Esc | Bỏ ghost; không hiện lại cho cùng phiên nguồn/điều kiện trừ khi gọi fx hoặc sửa nguồn | Giữ hành vi cũ |
| Gõ tiếp, dán, sửa giữa, đổi selection | Hủy đề xuất đang treo và phân tích theo nguồn mới | Nhập bình thường |
| Shift+Enter | Xuống dòng, không nhận ghost | Giữ xuống dòng |
| Tab | Chuyển focus như WEB1, không chiếm phím này | Chuyển focus |
| Ctrl+Enter | Giữ lệnh dựng lại hiện có, không kiêm nhận ghost | Dựng lại |
| Click đề xuất trong fx | Nhận đề xuất đã chọn sau kiểm phiên | Không dùng một kết quả cũ |

Tùy chọn Space nằm trong cài đặt hỗ trợ nhập hiện có, mặc định tắt; không đồng nhất với M5B tự chuyển công thức Word theo Space. Giữ Enter/Space khi IME chưa kết thúc hoặc tín hiệu nhập chưa chắc chắn. Phím đã bấm khi kết quả chưa xuất hiện không được dùng để nhận một kết quả đến muộn. Key repeat không được nhận rồi chốt thêm lần nữa.

Sau nhận, caret nằm cuối phần thân nếu cặp chưa đóng, hoặc sau dấu đóng nếu cặp đã đóng; nguồn không có cặp thì caret ở cuối kết quả. Space opt-in thêm đúng một khoảng trắng tại vị trí caret đó. Nhận chỉ một vùng đang có focus, không điền hàng loạt các vùng trong đoạn.

### 7.3. Ghi nhận, Undo và khôi phục

Khi nhận trong Web/Desktop, thay phần thân bằng chuỗi tuyến tính chuẩn của đúng kết quả đã preview và cập nhật snapshot trong một lệnh. Đây là một thao tác chỉnh sửa người dùng xác nhận: lưu nguyên nguồn trước nhận, các thay đổi, kết quả đã nhận và phiên bản để Undo/restore. Không gọi parser mới để quyết định lại kết quả trong bước nhận.

Một Undo trở về nguồn và con trỏ trước nhận, kể cả hệ số vế trái, cặp bọc, dấu `=` và chữ thường. Redo khôi phục đúng bản đã nhận. fx cho xem nguồn trước hỗ trợ và phục hồi; Copy nguồn trả đúng chuỗi đang hiển thị, không âm thầm trả phiên cũ.

Đóng cặp bọc không tự chấp nhận một kết quả cân bằng, sửa lỗi hoặc suy sản phẩm. Tự tính gợi ý, nhận gợi ý và tự chuyển định dạng là ba thao tác khác nhau. Sau khi người dùng đã nhận một bản hỗ trợ, mọi chuyển native vẫn theo luồng xác nhận/guard của host; marker không cấp thêm quyền ghi.

Khi triển khai auto định dạng ở M5A, vùng còn đề xuất hỗ trợ chưa nhận, repair, mơ hồ hoặc cảnh báo không đủ điều kiện tự chuyển. Không dùng bản hỗ trợ làm candidate trực tiếp để vượt qua kiểm tra này. SC1 không bật M5A từ việc đã thêm scanner cặp riêng.

## 8. Kiến trúc, dữ liệu và tương thích

| Thành phần hiện có | Thay đổi cần đánh giá |
| --- | --- |
| `Locus.Core/Detection` | Tập cấu hình cặp, scanner nhiều loại, vùng đang soạn/đã đóng, ngữ cảnh và ưu tiên miền |
| `Locus.Core/Parsing/ChemistryParser.cs` và model miền | Cách gõ chữ thường có kiểm mơ hồ, `=` theo ngữ cảnh, bản nháp phản ứng; giữ parser chuẩn và cây chất làm nền |
| Module hỗ trợ Hóa mới trong core | Bộ cân bằng chính xác; kho quy tắc/điều kiện; tạo đề xuất có nguồn gốc; không có UI/COM hoặc gọi mạng |
| `Locus.Application` | Yêu cầu hỗ trợ có revision; chọn/nhận/Undo; snapshot nguồn trước và sau; xuất chỉ kết quả đã nhận |
| `Locus.Editor` | Ghost/nhãn/điều kiện trong fx; cài đặt cặp và Space; cùng tương tác Web/Desktop |
| Worker và host Desktop | Wire contract/version, hủy và deadline cho tác vụ hỗ trợ; không chạy vòng tìm kiếm dài trên UI |
| `Locus.Word` | Cặp bọc mới trong chọn vùng thủ công; preview đề xuất/xác nhận; metadata/Undo/restore; ghost inline có task riêng |

Tên dữ liệu đề nghị để chốt ở SC1-01/03: `MarkerProfile`, `RegionIntent`, `ReactionDraft`, `AssistanceProposal`, `AcceptedTransformation`. Đề xuất chứa ID, loại thao tác, source ID/revision, vùng, ngữ cảnh, điều kiện, AST kết quả, các thay đổi và phiên bản solver/kho quy tắc.

Chất hoặc hệ số được sinh mới không có vị trí như thể người dùng đã gõ nó trong nguồn cũ. Lưu ánh xạ nguồn và phần sinh mới tách biệt; sau nhận có snapshot mới với vị trí của nguồn đã cập nhật. Kết quả đọc trực tiếp của nguồn cũ và lịch sử hỗ trợ không dùng chung ID giả.

Preferences/tệp/metadata cần version mới khi thêm dữ liệu; chốt số version trong hợp đồng trước code. Bộ đọc mới giữ được v1/v2 và candidate-set Math/E3 cũ nguyên nghĩa. Khi nâng cấp, một cặp cũ được đưa vào mục chung đúng nguyên văn; thêm preset riêng không sửa nó. Nếu cặp cũ trùng preset mới thì giữ cặp cũ, chưa kích hoạt mục mới bị xung đột và giải thích trong cài đặt.

Mở file không tự chạy solver/kho mới lên snapshot cũ; nguồn trước nhận và kết quả đã nhận tiếp tục đọc/xuất được khi detector tắt. Bản cũ không hiểu schema mới phải từ chối có kiểm soát; không tuyên bố tương thích ngược với phần mềm cũ.

Mọi yêu cầu nhận phải so source/revision/vùng/config/điều kiện/ID kết quả và trạng thái nhập. Đổi cặp, checkbox, điều kiện, nguồn hoặc focus làm yêu cầu chờ hết hiệu lực; kết quả tính xong muộn không ghi đè nguồn mới. Đổi checkbox vẫn giữ snapshot đã hoàn thành như E3.

Ngân sách đầu vào giữ giới hạn vùng 4096 UTF-16 của E3. SC1-01/05 chốt thêm giới hạn số chất, số nhánh cách đọc, độ lớn số nguyên và thời gian tìm nghiệm; có hủy/deadline và trả trạng thái vượt giới hạn. Mục tiêu đo ban đầu: ghost có đủ dữ kiện trong 300 ms ở p95 khi runtime đã nóng trên bộ phổ thông của máy thử. Đây là tiêu chí để đo/chỉnh, không phải số hiệu năng đã đạt.

## 9. Các đợt công việc và đầu ra

| Task | Việc thực hiện | Phụ thuộc | Đầu ra/điều kiện đóng |
| --- | --- | --- | --- |
| SC1-01 | Chốt hợp đồng vùng/cặp/cách gõ, loại đề xuất, phím prototype và corpus; đọc mã scanner/codec/IME | E3 đã đạt; kế hoạch này | Spec có version dự kiến, fixture có kỳ vọng và các delta so E3; đủ đầu vào lập trình, chưa gọi là nghiệm thu UX người dùng |
| SC1-02 | Tập cặp bọc, scanner nhiều loại, override miền, cài đặt/migration cấu hình | SC1-01 | Demo bốn cặp, nhiều vùng, ngoặc Hóa, all-off và cặp tùy chỉnh; hồi quy cặp cũ đạt |
| SC1-03 | Hợp đồng draft/proposal/lịch sử nhận, serializer/wire và bất biến nguồn | SC1-01/02 | Round-trip nguồn gốc và AST; dữ liệu/version sai bị từ chối; snapshot E3 không đổi |
| SC1-04 | Cách gõ Hóa: chữ thường đơn nghĩa, mơ hồ, `=` theo miền, phản ứng còn thiếu vế | SC1-03 | `h2+o2=`, `h2o`, `h20`, `co`, chữ chuẩn và Toán/Lý đều đúng kỳ vọng; chưa suy sản phẩm trong parser |
| SC1-05 | Bộ cân bằng bảo toàn nguyên tố/điện tích và kiểm kết quả độc lập | SC1-04 | Đủ hai vế cân bằng được; vô nghiệm/nhiều nghiệm/giới hạn trả đúng; parity native/WASM |
| SC1-06 | Kho phản ứng, điều kiện/ngoại lệ, tìm sản phẩm rồi cân bằng | SC1-05 | Danh mục có nguồn/version và bài giữ riêng; phân biệt thiếu điều kiện/không hỗ trợ/không phản ứng |
| SC1-07 | Ghost, fx, Enter nhận, toàn phương trình preview, Undo/restore và file/nháp trên hai host | SC1-03/05/06 | Một lần nhận sửa toàn bộ đúng kết quả; không đưa ghost vào copy/lưu; sửa nhanh/stale/selection đạt |
| SC1-08 | Tùy chọn nhận bằng Space, kiểm bàn phím/accessibility/IME thực | SC1-07 | Mặc định Space thường, opt-in đúng; Telex/VNI OS, Enter/Esc/Tab/key-repeat, mất focus và gõ tiếp có bằng chứng từng host |
| SC1-09 | Cặp/đề xuất trong Word thủ công, native/metadata/Undo/save-reopen | SC1-03/07; M3/G1 đã đạt | Bảng xác nhận đúng kết quả, khôi phục raw/cặp trước hỗ trợ, phát hiện sửa native và stale trên Word đã công bố |
| SC1-10 | Nghiệm thu tổng hợp, hiệu năng, gói local và tài liệu phạm vi | SC1-01…09 | Báo cáo core/host/kho/IME, gói/hash và kiểm sau giải nén; không ghi đạt một host chưa kiểm |
| SC1-11 | Ghost trực tiếp trong thân Word và nhận bằng phím trong tài liệu | SC1-07/09; M4, G2; nhận bằng Space cần D-01/G3 | Preview không ghi vào tài liệu, focus/IME/vị trí/Undo đủ bằng chứng; giữ TODO khi chưa đạt cổng |

SC1-01…10 là đợt alpha local chính. Có thể bàn giao Web/Desktop trước khi SC1-09 hoàn tất, nhưng phải ghi Word đang chờ và chưa đóng SC1-10 toàn mốc. SC1-11 là nhánh Word nâng cao, không giữ E1 để chờ phản hồi W0. Bước làm đầu tiên là SC1-01, sau đó lấy task theo bảng; chưa ước lượng ngày phát hành từ số task.

## 10. Demo và nghiệm thu

Ba lần demo có thể dùng để đánh giá sớm:

1. Sau SC1-02/04: cùng một đoạn chứa cặp Toán/Lý/Hóa, đổi cấu hình, tắt checkbox, nhập dở và sửa lỗi ký tự. Chứng minh phân vùng/cách đọc trước phần suy luận.
2. Sau SC1-05/06: nhập hai vế và vế đầu; đối chiếu cân bằng, nhiều nghiệm, điều kiện, trường hợp không có dữ liệu. Cho xem nguồn quy tắc, không chỉ hình công thức.
3. Sau SC1-07…10: gõ → ghost → Enter/Space opt-in → Undo → lưu/mở qua hai host → xuất → chèn Word thủ công → khôi phục nguồn. Kiểm nháp và cập nhật bằng gói đã phát hành.

[ACCEPTANCE](ACCEPTANCE.md) là danh sách kỳ vọng ban đầu, **chưa có bài nào được tính PASS từ việc viết kế hoạch**. Khi đóng task phải lưu build/hash/máy/runtime, corpus, kết quả thực và giới hạn. Không sửa artifact E3/WEB1 cũ thành bằng chứng SC1.

## 11. Nguồn và phần để sau

- [E3 grammar và nguồn chuẩn](../e3/GRAMMAR.md): nền nguyên tố, nhóm, điện tích, nguồn và miền.
- [Caltech — Chemical Reaction Balancer](https://cs1.caltech.codes/25sp/projects/05): tham khảo việc đưa bảo toàn nguyên tố về hệ phương trình; không lấy riêng bài này làm bằng chứng đúng phản ứng.
- [RSC — C, O and Co](https://edu.rsc.org/atoms-and-molecules/c-o-and-co-johnstones-triangle-11-14-years/4022847.article): lý do phải phân biệt chữ hoa/thường khi xác định nguyên tố và chất.
- [Nghiên cứu hấp thụ CO2](https://lutpub.lut.fi/bitstream/handle/10024/117806/Thesis_Final.pdf?sequence=2): ví dụ phụ thuộc điều kiện/tỉ lệ; từng bản ghi kho vẫn cần đối chiếu nguồn cụ thể trước khi phát hành.

Sau SC1 alpha: quay lại [E1](../e1/PLAN.md). Không đưa thêm solver Toán/Lý, cân bằng bán phản ứng tự thêm chất, dự đoán hữu cơ tổng quát, vẽ phân tử, cloud hoặc suffix riêng vào đợt này. Các yêu cầu ấy cần mốc/corpus riêng; khả năng suy Hóa không mặc nhiên tạo các khả năng đó.
