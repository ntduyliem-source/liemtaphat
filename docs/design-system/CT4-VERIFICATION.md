# CT4 — Bàn giao Công thức Web

Ngày 05/10/2026. **CT4-01…08 đã triển khai và kiểm trên Web local.** Build sản phẩm `20261005-104949-048`, catalog/tài liệu thiết kế `0.2.0`. Thư viện foundations `Locus.DesignSystem` vẫn `0.1.0` vì CT4 thay thành phần Công thức trong Editor, không đổi API của foundations. Không phát hành bộ cài Desktop mới trong đợt này.

- [Mở Công thức Web](http://127.0.0.1:4202/releases/20261005-104949-048/).
- [Mở catalog](http://127.0.0.1:4203/): palette từ CSS thật, nguồn/component/API, trạng thái và preview tương tác.
- Đặc tả: [CT4-PLAN.md](CT4-PLAN.md). [CT4-CORE-AUDIT.md](CT4-CORE-AUDIT.md) giữ nguyên 50 quan sát **trước sửa**, không phải kết quả nghiệm thu này.
- Receipt: `artifacts/design-system/current-build.json`. Bằng chứng local: `artifacts/design-system/ct4/`.

## Kết quả theo đầu ra

| Mục | Hành vi đã làm | Nguồn chính / bằng chứng |
| --- | --- | --- |
| CT4-01 · Lựa chọn | Một trạng thái None/Region/Range/All. Chọn một công thức bỏ All; Ctrl+A và Escape hoạt động trong kết quả. Chọn dở công thức chặn cân bằng và xuất, không mở rộng phạm vi ngầm. | `ResultSelectionState`, `Workspace.Content`, `result-selection.js`; kiểm DOM và kéo chuột thuận/ngược trong browser. |
| CT4-02 · Hóa | Nút **Cân bằng phương trình hóa học** chỉ chạy ở All/Hóa. Không chọn vẫn xử lý toàn kết quả. Hủy hệ số giữ sản phẩm; chọn vùng tự điền rồi **Hủy tự điền** trả đúng nguồn. Hệ số vốn đúng không tạo Hủy giả. | `StudioChemistryCommand`, `FormulaSession.StudioBalance`, `ReactionDraftLocator`, `FormulaDetectionBar`; 19 ca CT4 và 16 ca Studio. |
| CT4-03 · Core | Repair phân số dựa trên AST cho lũy thừa/tích/hiệu/mẫu số biến; căn tổng/hiệu; thêm đúng một ngoặc cuối. `x-1/2` được nhận; đường dẫn thật vẫn được bảo vệ. Direct đứng trước, tối đa ba candidate, không nhận repair tự động. | `FormulaParser`, `ProtectedTextRecognizer`; Core 240 ca và fixture snapshot trước sửa. |
| CT4-04 · Ý bạn là | Công thức đơn tự có ngữ cảnh. Hàng chỉ chứa phương án khác/repair của nguồn thật, không sample hoặc direct lặp. Nhận repair rồi quay về **Theo cú pháp đã gõ** hoặc Undo; raw không đổi. | `FormulaSuggestions`, `CandidateLabels`, `Workspace.Studio`; kiểm browser `x^2+1/2`. |
| CT4-05 · Đổi môn | Đổi mode và nhận diện lại là một giao dịch Undo. Giữ quyết định còn hợp lệ, bỏ override không còn phù hợp; kết quả cũ trả muộn không thắng lần chọn mới. | `FormulaSession.Reanalyze`; kiểm All → Math → All khi request trước còn chờ và browser với `H2SO4`. |
| CT4-06 · Ảnh hỗn hợp | Dựng SVG từ snapshot kết quả: chữ + nhiều công thức + xuống dòng/khoảng trắng. Không chụp DOM, không lấy chữ từ MathML hiển thị. PNG 2X/4X dùng cùng ảnh; recheck nguồn/vùng chọn trước tải hoặc copy. | `ContentExport.Parts`, `Workspace.ResultImage`, `result-image.js`, `renderer.js`; ảnh thật và phép chiếu bất biến. |
| CT4-07 · Thanh ảnh | Chỉ giữ **Chọn tất cả · SVG · PNG 2X · PNG 4X · Copy ảnh**. Dùng cho vùng riêng, đoạn hoặc toàn kết quả; không còn menu Phạm vi xuất/HTML/TXT. | `FormulaExportBar`; tải thật, copy và dán PNG thực. |
| CT4-08 · Bàn giao | Catalog 0.2.0 có 18 trạng thái; có preview 390/720/1280px. Kiểm sáng/tối, component, Web worker, hai tab vẽ và build tương thích Desktop. | Web local trên, catalog, ảnh và các báo cáo bên dưới. |

Giữ bố cục Studio, typography, panel Nhận diện và vị trí Undo/Redo. Nhãn dài xuống hàng theo breakpoint, không thu nhỏ font để che tràn. Không thêm fx vào kết quả, checkbox Tự cân bằng, sample, nháp hoặc nút UI vào ảnh.

## Đối chiếu audit A01–A13

- **A01–A03:** các mẫu `x^2+1/2`, `x*y+1/2`, `x-1/2`, `x+1/y`, `can x+1`, `can x-1`, `(x+1` có sửa đúng cấu trúc và source edit; ngoặc đã đủ không bị gợi ý tháo bỏ. Với căn không ngoặc, giới hạn toán hạng căn là biến đơn để giữ quy tắc direct của các chuỗi tiếng Việt cũ; không mở rộng thành phép đoán nhiều bước.
- **A04:** candidate đúng ngữ cảnh, có giải thích sửa; không dùng danh sách ví dụ để lấp hàng.
- **A05:** reanalysis chủ động khi đổi mode, một bước Undo và chống request cũ. Mở snapshot lịch sử vẫn đọc snapshot, không reparse.
- **A06–A07:** chặn lệnh Hóa ở Application lẫn UI; so hệ số thật trước khi tạo trạng thái cân bằng. `HCl+NaOH=` chỉ ghi tự điền, `2H2+O2=2H2O` không ghi thao tác hệ số giả.
- **A08–A09:** lệnh nhận danh sách ID vùng cùng version; chọn Toán/text không nhảy sang Hóa ngoài selection. Hủy tự điền chỉ hiện cho vùng có provenance và đã hủy thay đổi hệ số.
- **A10–A11:** `Xét H2+O2= trong bài.` được định vị đúng, không nuốt lời văn. Có outcome theo từng ID và điều kiện của đề xuất đọc lại được trong Chi tiết, kể cả sau lưu/mở lại.
- **A12:** một selection state và một đường xuất ảnh; bỏ coordinator export cũ đã không còn dùng. Lỗi/copy bị chặn không sửa tài liệu.
- **A13:** giữ schema/core/grammar version vì không thêm node tài liệu; chính sách repair mới có provenance `/ct4-1`. Giữ chính sách và ID của các dạng cũ. Bốn snapshot trước sửa được kiểm exact candidate ID, MathML và OMML; Undo không phân tích lại theo luật mới.

## Các bộ kiểm đã chạy

Các số dưới là từng bộ kiểm, có phần dùng lại fixture; **không cộng thành một tổng ca độc lập**.

| Bộ kiểm | Kết quả |
| --- | --- |
| CT4 contracts | 19/19 |
| Studio balance | 16/16 |
| Application content / balance / document export | 22/22 · 20/20 · 7/7 |
| Application suite | 35/35; gồm kiểm wire và 104 fixture worker |
| Core | 240/240 |
| Science | 431/431 core · 19/19 application |
| Smart Chemistry | Markers 109/109; assistance 20/20; input 38/38; solver 31/31; session 7/7; catalog 87/87; ghost 8/8 |
| Editor DOM | PASS: selection/source spans, escaping, toolbar, repair, busy/IME guard |
| Design system ownership / token references | PASS |
| Web + worker + catalog publish | Thành công; worker có cảnh báo trimming IL2026 hiện hữu |
| Desktop.Shared compatibility build | Thành công, 0 lỗi / 0 cảnh báo; chưa phải nghiệm thu app Windows |

Nguồn ca CT4: `tests/Locus.Application.Tests/Ct4Verification.cs`; fixture lịch sử: `tests/Locus.Core.Tests/Fixtures/ct4/`. Báo cáo JSON Application trong `artifacts/design-system/ct4/verification/`. Không thay kỳ vọng để chấp nhận AlreadyBalanced quản lý giả; hai kỳ vọng Studio cũ được đổi theo hành vi mới có thay đổi thực.

Lệnh chạy trọng tâm:

```powershell
dotnet run --project tests/Locus.Application.Tests -- --ct4-only
dotnet run --project tests/Locus.Editor.Tests
node tools/design-system/check.mjs
dotnet build src/Locus.Desktop.Shared --no-restore
pwsh -File tools/design-system/build.ps1
```

## Kiểm browser và tệp thực

Đã thao tác trên in-app browser bằng UI, với dữ liệu thử riêng. Các lượt kiểm ảnh ban đầu chạy build `20261005-103613-787`; bản cuối `20261005-104949-048` đã kiểm lại nhập/cân bằng, đổi mode/Undo, chọn bằng chuột và bàn phím, tải SVG vùng và PNG toàn đoạn. Bản cuối dọn coordinator dư, không thay engine ảnh so với lượt ảnh đầu.

| Chứng cứ trong `artifacts/design-system/ct4/visual/` | Kết quả |
| --- | --- |
| `mixed.svg` | 600×317; chữ Việt, emoji, phân số, căn, phản ứng và xuống dòng. Mở độc lập đúng; không foreignObject/tham chiếu font ngoài. |
| `mixed-2x.png` / `mixed-4x.png` | 1200×634 / 2400×1268; PNG RGBA giữ nền trong suốt. Kiểm trên nền trắng. |
| `clipboard.png` / `paste-and-standalone.png` | Clipboard PNG thật 73.138 byte; dán Ctrl+V vào nơi nhận ảnh cho kết quả 1200×634, cùng byte với PNG 2X tải về. Khi browser chưa được focus, copy bị từ chối và UI báo tải tệp thay thế. |
| `selected-formula.svg` | Chọn riêng sau All xuất đúng một phân số, khoảng 102,272×73,336. |
| `selected-range.svg` | Kéo chọn ngược qua hai công thức xuất 446×134; không có dòng văn cuối nằm ngoài selection. |
| `final-mixed-2x.png` | Bản cuối xuất toàn đoạn với raw 73 ký tự, hai công thức và chữ Việt/emoji: 1032×510. |
| `long-120-lines.svg` | 120 dòng nguồn, 3.720 glyph không kể xuống dòng, 565×14.588. Nội dung ngoài viewport vẫn xuất đủ. PNG 4X vượt trần bị chặn, có hướng dẫn chọn SVG/đoạn ngắn hơn. |
| `partial-selection.png` | Bôi dở công thức: khóa ảnh và Hóa, nhắc chọn trọn; không xuất toàn bài thay thế. |
| `final-light.png`, `final-dark.png` | Kiểm giao diện Web sáng/tối. |
| `compact-top.png`, `compact-result.png`, `narrow-dark.png` | Preview thật trong catalog ở 390/720px: panel, nhãn nút và thanh ảnh xuống hàng; không cắt nút. |
| `catalog-repair.png` | Component và gợi ý thật trong catalog. |
| `export-font-failure.png`, `export-source-changed.png`, `fault-injection.json` | Proxy QA riêng trả 503 cho glyph rồi thử lại với chậm 10 giây. Lỗi font không đổi nguồn; đổi nguồn khi export đang chờ không tải ảnh cũ (số tệp PNG vẫn là 3). Bấm xuất lại tải đúng một tệp mới. |

Viewport override của công cụ không thay được kích thước Web theo yêu cầu, nên kiểm responsive bằng iframe có chiều rộng CSS xác định trong catalog. Không lấy screenshot rộng làm bằng chứng mobile. Các tài nguyên Editor dùng cùng source/build.

Đồ thị: nhập `y=x^2`, thấy đường vẽ và tải SVG khả dụng. Hình học: mở 2D và chuyển 3D, các điều khiển dựng hình/camera hiển thị. Đây là smoke không hồi quy shell/CSS, không phải chạy lại toàn bộ nghiệm thu editor vẽ.

## Giới hạn còn lại và cách nâng cấp

- Tự điền phản ứng dùng kho 45 record hiện có, không suy được mọi phản ứng. `NaOH+CO2=` thiếu tỉ lệ và `N2+H2=` chưa có dữ liệu được giữ nguyên với lý do; người dùng vẫn có thể gõ sản phẩm. Không tự chữa `h20` thành `H2O`.
- C01–C08 trong audit vẫn là backlog: hàm Toán, chỉ số/tích chữ liền, số khoa học, quan hệ chuỗi/cấu trúc nâng cao, ký hiệu Hóa mở rộng, sửa 0/O có xác nhận, kho phản ứng và năng lực Lý nâng cao.
- Chữ Latin/Việt xuất thành path từ Inter local. Emoji/ký tự ngoài bộ glyph dùng ảnh nhỏ nhúng trong SVG, phụ thuộc font có sẵn lúc tạo; không cam kết toàn bộ Unicode đều thành vector. Nguồn và cách tái tạo ở `src/Locus.Editor/wwwroot/formula/IMAGE-RESOURCES.md`.
- PNG giới hạn 24 triệu pixel hoặc 32.767 px mỗi cạnh; giữ SVG cho đoạn dài. Không tự cắt nội dung hoặc giảm chất lượng ngầm.
- Đã kiểm guard IME/async qua contract và DOM; lỗi tải font và đổi nguồn khi đang compose được fault-injection trên bản QA cùng release. **Không nghiệm thu lại UniKey/VNI/Word/native clipboard Windows** trong CT4. Chưa cưỡng bức riêng lỗi GPU/canvas hoặc đổi selection đúng thời điểm rasterize; coordinator kiểm cùng version/epoch sau cả compose và PNG, không cam kết đã tái hiện mọi lỗi môi trường.
- Reduced motion dùng foundation đã có; kiểm source vẫn giữ media query, chưa bật tắt thiết lập OS trong đợt này. Không công bố đã kiểm đầy đủ mọi cấu hình accessibility.
- Desktop là sản phẩm cài riêng. Đợt này chỉ build tương thích thư viện, không đưa Web local thành “bộ cài”, không tạo installer mới, không kiểm Word.

Tiếp theo là review trải nghiệm trên bản này và chọn phạm vi nâng cấp lõi từ C01–C08. Nghiệm thu/phát hành Desktop cần đợt riêng với app Windows và gói cài thật. Không cần chạy lại các bộ kiểm đã qua nếu chưa đổi source liên quan.
