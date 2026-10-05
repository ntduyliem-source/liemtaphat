# Kế hoạch tối ưu Công thức và design system Locus

Cập nhật 05/10/2026. **Trạng thái: CT0–CT4 đã triển khai và kiểm Web local; catalog 0.2.0.** Xem [kết quả CT4](CT4-VERIFICATION.md), [đặc tả](CT4-PLAN.md) và [audit baseline cùng backlog core](CT4-CORE-AUDIT.md). Desktop mới được build tương thích, chưa phát hành bộ cài mới.

Đầu vào: phản hồi người dùng ngày 05/10, lượt rà soát Web cùng ngày, build local 20261003-115834-995 và thiết kế gốc ui/locus_studio (2).html. Đây là kế hoạch hiện hành cho Công thức. [VERIFICATION.md](VERIFICATION.md) và [BALANCE-COMMAND.md](BALANCE-COMMAND.md) giữ nguyên ngày, phiên bản và phạm vi kiểm trước đây.

## 1. Hai sản phẩm và phạm vi

| Sản phẩm | Cách dùng | Trách nhiệm riêng |
| --- | --- | --- |
| Locus Web | Website mở trong trình duyệt; bản đang review được phục vụ ở localhost. | Shell/điều hướng Web, bố cục trình duyệt, tải tệp, clipboard và lưu dữ liệu trình duyệt, triển khai website. |
| Locus Desktop | Phần mềm cài trên Windows, chạy chương trình riêng. | Cửa sổ gọn/bung, tray, hộp thoại tệp, clipboard Windows, dữ liệu cục bộ, cài/gỡ/cập nhật ứng dụng. |

Hiện Desktop dùng WPF và WebView2 hiển thị tài nguyên đóng gói cục bộ, không cần chạy server Web của dự án hoặc mở website online. Bản Desktop mới nhất mới ở dạng publish chạy được, chưa có Setup/MSI mới cho Studio.

Sau CT3, WebShell và DesktopShell đã tách riêng; hai host còn tham chiếu thư viện Locus.Editor. Cần phân biệt:

- Lõi nhận diện, MathDocument, Hóa và định dạng tài liệu có thể được tái sử dụng để hai sản phẩm cho kết quả nhất quán.
- Design system là tiêu chuẩn thiết kế và thư viện có phiên bản; hai sản phẩm có thể áp dụng cùng thương hiệu.
- Shell, bố cục theo môi trường, cấu hình, phiên chạy, lưu trữ và phát hành thuộc từng sản phẩm. Chia sẻ thư viện không có nghĩa chia sẻ phiên, dữ liệu hoặc tự đồng bộ dữ liệu.

**Đợt tiếp theo ưu tiên trang Công thức trên Web.** Tách trách nhiệm shell Web/Desktop để website không bị thiết kế theo cửa sổ tray và Desktop không bị ép dùng bố cục website. Không bắt buộc nhân đôi parser hay mọi component để có hai sản phẩm độc lập.

Thay thư viện mà Desktop đang tham chiếu phải kiểm tương thích và ghi phạm vi ảnh hưởng. Không lấy kết quả Web thay nghiệm thu Windows; không phát hành Desktop mới chỉ vì Web đã đổi UI.

Đồ thị, Hình học, Word, tài khoản/quota, mở rộng kho phản ứng và bộ cài Desktop không thuộc đợt Công thức này. Hai tab vẽ hiện có cần được kiểm không bị thay đổi CSS/shell làm hỏng. Đợt này không tự triển khai website lên Internet.

## 2. Mục tiêu và thiết kế đã chốt

Luồng mục tiêu: nhập/dán → thấy phần nhận diện → hiểu chỗ cần quyết định → chỉnh/cân bằng nếu muốn → xuất đúng nội dung → có đường quay lại.

Giữ nguyên những yêu cầu người dùng đã duyệt:

- Header, vị trí Undo/Redo, panel Nhận diện và Cân bằng theo file mẫu.
- Bố cục nguồn/kết quả, typography, kích thước và hiệu ứng đã chốt. Refactor code không tự thay hình thức.
- Kết quả Web/Desktop không có fx; điều khiển ngoài nội dung; ảnh/tệp xuất không chứa viền chọn hoặc UI.
- Không đưa lại Tùy chọn & nháp, Nháp gần đây, Định dạng công thức đang chọn, Phím tắt, checkbox Tự cân bằng hay khu Thử nhanh riêng.
- Giữ nguồn, phân biệt cách hiểu hợp lệ và đề nghị sửa, không tự nhận repair.
- Cân bằng/hủy theo phản hồi 03/10: hủy hệ số giữ sản phẩm đã điền; Undo hoàn tác cả giao dịch.

Các thay đổi ở mục 6 và CT4-PLAN.md đã được triển khai; bằng chứng và giới hạn nằm trong CT4-VERIFICATION.md. Yêu cầu ngày 05/10 về Hủy tự điền, Ý bạn là và xuất ảnh thay thế các đề xuất UI cũ tương ứng.

## 3. Nguồn chuẩn và palette

| Cần biết | Nguồn và quy tắc |
| --- | --- |
| Thiết kế xuất phát từ đâu? | ui/locus_studio (2).html, giữ nguyên để đối chiếu. Ghi selector/vị trí của giá trị lấy từ mẫu. JavaScript mô phỏng trong mẫu không phải nghiệp vụ sản phẩm. |
| Yêu cầu nào ưu tiên? | Phản hồi mới nhất của người dùng; khác biệt với mẫu được ghi trong đặc tả. |
| Giá trị đang chạy nằm đâu? | Nguồn hiện hành: src/Locus.DesignSystem/wwwroot/styles/tokens.css; CSS Công thức trong src/Locus.Editor/wwwroot/formula. |
| Xem trực quan ở đâu? | Catalog đọc tokens và render component thật, có palette, kiểu chữ, trạng thái, pattern Công thức và file nguồn. Catalog hiện ở src/Locus.Catalog, chạy local riêng. |
| Biết nút phải làm gì ở đâu? | Đặc tả hành vi và bảng trạng thái Công thức; không suy nghiệp vụ từ màu hoặc demo HTML. |
| Bản nào đã đạt? | Manifest build và biên bản có ngày, phiên bản, phạm vi; Web/Desktop có kết quả riêng. |

Catalog không giữ một bản giá trị nhập tay thứ hai. Nếu thêm Figma về sau, tên token/component và quy trình đồng bộ phải được ghi rõ.

### Palette xuất phát

| Vai trò | Sáng | Tối |
| --- | --- | --- |
| Nền ứng dụng / paper | #FBFBF9 | #0E0E0D |
| Panel / surface | #FFFFFF | #161614 |
| Nền phụ / subtle | #F4F4F0 | #1E1E1B |
| Nền kết quả | #FAF9F5 | #121210 |
| Chữ chính / ink | #0D0D0D | #F3F3ED |
| Chữ phụ / muted | #686862 | #8E8E87 |
| Viền / line | #E5E5DE | #282824 |
| Nhấn / accent | #E8590C | #E8590C |
| Nền nhấn nhẹ / tint | #FFF4ED | #2C170B |
| Hover nút chính | #D44E08 | #D44E08 |
| Thành công | #15803D | Kiểm độ đọc trước khi chốt vai trò trên nền tối |
| Nguy hiểm / xóa | #E11D48 | Kiểm độ đọc trước khi chốt vai trò trên nền tối |

Nguồn là palette, màu nền kết quả và hover của HTML mẫu, đã được ghi một phần trong tokens hiện tại. Không tự thay màu thương hiệu. Màu cảnh báo còn thiếu phải có mẫu/nguồn quyết định trước khi đưa vào sản phẩm.

Mỗi swatch có: tên token, HEX/RGBA, vai trò, light/dark, nền áp dụng, ví dụ chữ/icon, độ tương phản, nguồn gốc và danh sách component sử dụng.

Ba cấp token: giá trị nền tảng → vai trò như text.primary/surface.result/action.primary → token component khi thật sự cần như formula.selection. Component dùng vai trò, không rải HEX. Màu nội dung do người dùng chọn là dữ liệu tài liệu, không bị đổi theo theme UI.

### Foundations còn lại

- Font: Newsreader cho brand/hero/văn bản kết quả; Inter cho mô tả; JetBrains Mono cho controls/input; font toán theo renderer. Đóng gói local và giữ giấy phép.
- Giữ mốc hiện tại: hero 2.7rem trên khung rộng, nguồn 14px, văn bản kết quả 16px, controls 10–12px. Đưa thành token theo vai trò, không tự phóng cỡ.
- Catalog có mẫu chữ Việt đầy đủ dấu, công thức dài, phân số/căn/chỉ số và ký tự dễ nhầm.
- Ghi spacing, chiều cao control, viền, radius, shadow, breakpoint, lớp nổi và motion. Token phải có nơi dùng hoặc lý do tồn tại.
- Control 150ms, đổi nền/chữ theme 200ms, hover/selected công thức theo mẫu; focus bàn phím và reduced motion có mẫu riêng.
- Pattern Web rộng/hẹp và Desktop gọn/bung được định nghĩa riêng.

## 4. Catalog là đầu ra bắt buộc

Catalog là công cụ nội bộ cho designer/developer, đã có ở app riêng src/Locus.Catalog, chạy local. Không thêm tab catalog vào giao diện người dùng Locus.

| Nhóm | Nội dung |
| --- | --- |
| Foundations | Palette sáng/tối, typography, spacing, border/radius/shadow, icon, motion, layout, focus. |
| Component cơ bản | Button, IconButton, SegmentedControl, TextField/Textarea, Select, Checkbox, Tooltip, Popover/Disclosure, Status, Toast. Chỉ làm các thành phần cần cho Công thức. |
| Component Công thức | Nhận diện, nguồn nhập, đoạn kết quả/vùng công thức, hàng gợi ý, cân bằng, cặp bọc, chi tiết, phạm vi và thanh xuất. |
| Trạng thái | Default, hover, pressed, focus, selected, disabled, loading, error và trạng thái nghiệp vụ tương ứng. Có mẫu cố định và thao tác chuột/phím thật. |
| Pattern | Trang trống, công thức đơn, đoạn hỗn hợp, mơ hồ/lỗi, Hóa trước/sau cân bằng, lỗi xuất, Web rộng/hẹp. |
| Cách dùng | Khi dùng/không dùng, variant, đầu vào/sự kiện, nhãn/tooltip, file nguồn, token phụ thuộc, nơi sử dụng. |
| Bảo trì | Phiên bản, changelog, thành phần cũ đang thay, hướng dẫn chuyển đổi, kết quả kiểm. |

Có tìm theo tên token/component, xem sáng/tối và kích thước màn hình. Catalog render chính component sản phẩm, không chép một bản HTML gần giống. Ví dụ chạy trong phiên demo riêng, không sửa phiên làm việc thật.

Component chỉ được coi sẵn sàng khi có mục catalog, trạng thái cần thiết, nguồn rõ ràng và được sử dụng trên trang thật.

## 5. Tổ chức source và cách sửa/bảo trì

Cấu trúc dưới đây đã được tạo trong CT0–CT3; hướng dẫn hiện hành tại README.md, SOURCES.md và MAINTENANCE.md trong thư mục này:

| Vị trí đích | Trách nhiệm |
| --- | --- |
| docs/design-system/PLAN.md | Kế hoạch hiện hành này. |
| docs/design-system/README.md | Điểm vào catalog, phiên bản và hướng dẫn tìm source. |
| docs/design-system/SOURCES.md | Ánh xạ HTML gốc → token/component; quyết định bổ sung. |
| docs/design-system/MAINTENANCE.md, CHANGELOG.md | Quy trình sửa/nâng cấp và lịch sử thay đổi thiết kế. |
| src/Locus.DesignSystem/wwwroot/styles, fonts | Tokens, theme, typography, reset tối thiểu, font/giấy phép. |
| src/Locus.DesignSystem/Components | Thành phần cơ bản và CSS có phạm vi. |
| src/Locus.Editor/Formula/Components, Presentation | Các phần UI Công thức, trạng thái/lệnh và ánh xạ từ Application. |
| src/Locus.Catalog | Catalog dùng component thật. |
| src/Locus.Web | Shell, điều hướng và tích hợp trình duyệt. |
| src/Locus.Desktop.Shared | Shell/cửa sổ và tích hợp Windows hiện có. |

Core/Application tiếp tục giữ nhận diện, phân tích, quyết định và giao dịch tài liệu. UI nhận trạng thái, phát lệnh; không tự tính lại công thức hoặc hệ số cân bằng trong component.

Tách nguồn, kết quả, gợi ý, cân bằng, xuất khỏi workspace lớn. CSS giới hạn phạm vi component; reset toàn cục chỉ chứa nền tảng. Lập bản đồ selector trước khi bỏ CSS cũ, chuyển từng phần và giới hạn CSS của các editor chưa chuyển. Không thêm override mới để chữa chồng chéo.

| Muốn sửa | Sửa ở đâu | Kiểm gì |
| --- | --- | --- |
| Màu/font/khoảng cách có ý nghĩa toàn hệ thống | Token/foundation tương ứng | Mẫu token, component đang dùng, light/dark. |
| Hover/focus/loading một loại nút | Button/IconButton | Các variant và trạng thái của nút. |
| Bố trí nguồn/kết quả trên Web | Pattern/layout Công thức Web | Rộng/hẹp, nội dung ngắn/dài, vị trí đã duyệt. |
| Quy tắc chọn/cân bằng/xuất | Presentation và lệnh Application | Ca sử dụng, phạm vi, nguồn và Undo/Redo. |
| Copy/tải trong trình duyệt | Adapter Web | Clipboard/tệp thực nhận được. |
| Tray/bung/thu/hộp thoại Windows | Host Desktop | Kiểm Windows và gói Desktop riêng. |

Quy trình nâng cấp: xác định nguồn và ảnh hưởng → sửa nguồn chuẩn → cập nhật catalog/đặc tả → kiểm phần bị ảnh hưởng → ghi changelog/phiên bản → phát hành riêng sản phẩm đạt. Đổi tên/xóa token có ánh xạ chuyển tiếp; không thêm token gần giống để né việc chuyển đổi.

## 6. Kế hoạch CT4 hiện hành

Chi tiết hành vi, file cần sửa, phụ thuộc và tiêu chí đạt nằm tại [CT4-PLAN.md](CT4-PLAN.md). [CT4-CORE-AUDIT.md](CT4-CORE-AUDIT.md) ghi 50 quan sát baseline, A01–A13 cần xử lý và C01–C08 thuộc đợt bổ sung năng lực sau đó.

| Cụm | Nội dung hiện hành |
| --- | --- |
| Lựa chọn | Một trạng thái None/Region/Range/All; Chọn tất cả tự về thường khi chọn riêng; không còn bộ chọn Phạm vi xuất. |
| Hóa | Cân bằng phương trình hóa học chỉ hoạt động ở All/Hóa; hủy hệ số và hủy tự điền có provenance, không tạo hủy giả khi nguồn đã cân bằng sẵn. |
| Core | Sửa nhận nhầm `x-1/2` là đường dẫn; mở rộng repair phạm vi phân số/căn và một ngoặc đóng; giữ precedence, nguồn và tương thích snapshot. |
| Ý bạn là | Gợi ý của công thức thật, ví dụ `(x^2+1)/2`; không lặp direct, không chèn sample vào hàng này. |
| Nhận diện | Đổi môn nhận diện lại trong một giao dịch có Undo; giữ quyết định còn hợp lệ, không sửa snapshot lịch sử lúc mở. |
| Xuất | Thanh Chọn tất cả/SVG/PNG 2X/PNG 4X/Copy ảnh; bổ sung ảnh đoạn hỗn hợp. Không thay nút ảnh bằng HTML/TXT khi chọn toàn bộ. |
| Bàn giao | Catalog đủ trạng thái, tệp/clipboard thật, kiểm phần bị tác động và build local cùng phiên bản. |

Không thay thế solver hoặc viết lại toàn bộ core. Hàm Toán, chỉ số ở chế độ Toán, tích chữ liền, số khoa học, ký hiệu Hóa mở rộng và kho phản ứng lớn hơn là các thiếu hụt đã được ghi riêng; chưa hứa là có sẵn nhờ CT0–CT3.

## 7. Thứ tự thực hiện

CT0–CT4 được người dùng yêu cầu triển khai ngày 05/10. Đã có source, catalog và host shell riêng; CT4 bổ sung luồng Hóa, gợi ý và ảnh hỗn hợp. **CT4 đã kiểm local theo CT4-VERIFICATION.md. CT5 chưa mở nghiệm thu đầy đủ; không lấy kết quả Web làm nghiệm thu Windows.**

| Bước | Công việc và đầu ra | Đạt khi |
| --- | --- | --- |
| CT0 — Baseline và hành vi | Inventory UI/CSS, nguồn token, ranh giới sản phẩm, bảng trạng thái và wireframe chú thích các thay đổi mục 6. | Mỗi điều khiển rõ tác dụng, phạm vi và kết quả; hết quy tắc mâu thuẫn. |
| CT1 — Foundations + catalog | Palette, typography, spacing/motion, thư viện thiết kế và catalog tối thiểu. | Xem được catalog; biết lấy/sửa giá trị ở đâu; catalog và runtime cùng nguồn. |
| CT2 — Component Công thức | Thành phần cơ bản và riêng Công thức, đủ trạng thái; tách workspace. | Catalog dùng component thật; giữ bố cục/cỡ chữ/hover đã chốt. |
| CT3 — Tổ chức UI Web | Tách trách nhiệm shell Web/Desktop; đưa Công thức ra khỏi CSS chồng cũ theo inventory. | Không thêm override chữa chồng; tab vẽ không hỏng; thay thư viện có kiểm tương thích Desktop, chưa phát hành Desktop mới. |
| CT4 — Luồng sử dụng | Thực hiện CT4-01…08 theo CT4-PLAN.md, xử lý A01–A13 trong audit. | Nhập → gợi ý/chọn → cân bằng/hủy → xuất ảnh một vùng/cả đoạn, có Undo và bằng chứng cùng build. |
| CT5 — Bàn giao Web | Kiểm UI/tệp/clipboard; ghi phiên bản, catalog, bảo trì và changelog. | Người dùng hoàn thành tác vụ; designer/dev tra được nguồn; tài liệu/build cùng phiên bản và có bản trước để quay lại. |

Sau CT5 mới lên đợt Công thức Desktop: shell gọn/bung, tray, native clipboard/files, IME Windows, bộ cài/gỡ/cập nhật. Đợt đó có yêu cầu và nghiệm thu riêng.

## 8. Kiểm chứng có giới hạn và tiêu chí hoàn tất

Kiểm từng cụm; chạy lại khi có thay đổi/failure cụ thể. Không lặp toàn bộ parser/solver sau mỗi sửa CSS. Phân biệt kiểm visual/catalog với giao dịch/xuất.

| Ca đại diện | Kết quả cần thấy |
| --- | --- |
| x mũ 2 + 1; 1 trên 2 | Giữ nguồn, dựng và xuất nhanh được. |
| Hóa → Toán với nguồn Toán có sẵn | Nhận diện lại; Undo về chế độ/kết quả cũ. |
| x^2+1/2, x-1/2, mơ hồ, repair | Đúng precedence; không nhận nhầm path; đề nghị sửa đúng cấu trúc, người dùng quyết định. |
| Đoạn có chữ, URL/email, công thức, xuống dòng | Giữ văn bản, phạm vi chọn và xuất khớp. |
| v=10 m/s nhiều cách hiểu | Báo cần chọn; không gắn lệnh Hóa riêng vào vận tốc. |
| MANUAL sau các bộ lọc khác nhau | Cặp chung/cặp môn nhất quán, giữ cấu hình tùy chỉnh. |
| H2+O2= và N2+H2=NH3 | Điền/cân bằng; hủy hệ số giữ sản phẩm; hủy tự điền đúng nguồn; Undo cả giao dịch. N2+H2= chưa có mẫu tự điền. |
| Chọn riêng, nguồn không xử lý được | Đúng phạm vi; phần bỏ qua giữ nguyên. |
| Sửa nguồn khi đang dựng/xuất | Không lấy kết quả cũ, có phản hồi thao tác lại. |
| Chọn một công thức sau Chọn tất cả | Trạng thái All tắt, gợi ý/lệnh/xuất cùng vùng, không sample thay nguồn. |
| SVG/PNG/copy ảnh của một vùng và cả đoạn | Kiểm dữ liệu/tệp thật, font/xuống dòng/scale; toast không thay chứng cứ. |
| Sáng/tối, rộng/hẹp, bàn phím, chữ Việt/IME | Đọc được, focus rõ, không mất chữ hoặc chức năng. |

Hoàn tất khi catalog thực có; nguồn và cách sửa rõ; Công thức thoát các override cũ thuộc phạm vi; luồng chính đạt; không sai nguồn/sai vùng/mất quyết định; tệp/clipboard Web có bằng chứng; build và tài liệu cùng phiên bản. Mục chưa kiểm ghi riêng, không đổi thành đã đạt nhờ kết quả bản cũ hoặc host khác.
