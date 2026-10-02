# Corpus M0

`cases.json` là dữ liệu kỳ vọng được soạn để triển khai Locus. Nó **không phải kết quả chạy parser hoặc Word**. Grammar được mô tả tại `docs/m0/GRAMMAR.md`; hợp đồng nguồn, candidate và auto tại `docs/m0/CORE-CONTRACT.md`.

M1 đã có [runner dùng core thật](../../tests/Locus.Core.Tests/Program.cs): đối chiếu 104 case nội dung và 5 case snapshot; 8 pending được loại riêng. [Báo cáo và phạm vi kiểm chứng](../../docs/m1/REPORT.md), [errata M1](../m1/ERRATA.md). Phần host policy/Word vẫn cần kiểm chứng tích hợp riêng.

Cặp bọc mặc định theo lựa chọn của người dùng là `lc[...]` (`open = lc[`, `close = ]`). User được tùy chỉnh cặp bọc; M0-072 dùng `<<...>>` và M0-117 dùng `toan[[...]]` với cấu hình rõ. Chỉ vùng đã đóng và nội dung đủ điều kiện mới được xét auto. Lựa chọn cặp bọc không chốt hành vi auto-Space; D-01 vẫn mở.

## Chạy kiểm tra dữ liệu

Từ thư mục gốc workspace, với Python 3.11 hoặc mới hơn và không cần thư viện bên ngoài:

```powershell
python tools/m0/validate_corpus.py
python tools/m0/validate_corpus.py --self-test
```

Có thể truyền đường dẫn JSON làm đối số đầu tiên. Exit code `0` nghĩa dữ liệu fixture hợp lệ theo validator; khác `0` nghĩa schema hoặc bất biến dữ liệu chưa đạt. `--self-test` thêm các kiểm soát âm tính bằng cách làm hỏng bản sao dữ liệu trong bộ nhớ, không sửa corpus trên đĩa.

## Đọc một case

| Trường | Ý nghĩa |
| --- | --- |
| `status` | `specified`: có kỳ vọng cụ thể; `pending`: chưa chốt hành vi phụ thuộc quyết định |
| `expectationBasis` | `product_requirement`, `grammar_baseline` hoặc `open_decision`; không gộp đề xuất với yêu cầu gốc |
| `personas` | Các nhóm hưởng lợi từ tình huống: giáo viên, học sinh/sinh viên, người viết tài liệu kỹ thuật; case dùng chung có cả ba |
| `source.raw` | Toàn bộ nguồn nguyên văn, giữ Unicode tổ hợp, NBSP và CRLF |
| `context` | Kênh nhập, chế độ nhận diện/auto, miền, IME, focus, trạng thái tài liệu giả lập và nguồn sự kiện |
| `expected.regions` | Các vùng biểu thức theo thứ tự; rỗng nếu nhận diện bị từ chối, hoãn hoặc pending |
| `sourceSpan` / `sourceText` | Span UTF-16 half-open và nội dung đối chiếu chính xác; có marker thì chỉ phần giữa hai dấu |
| `replacementSpan` / `replacementText` | Phạm vi thực sự thay, bao gồm dấu vùng nếu có |
| `candidates` | Tối đa ba kết quả mỗi vùng: `direct`, `interpretation`, `repair`; AST là projection ngữ nghĩa |
| `diagnostics` | Code, mức độ, span trên nguồn và mô tả; diagnostic của điều kiện host nằm trong `auto.reasons` khi không phải lỗi nội dung |
| `edits` | Chỉ repair có edit; span nguồn, sắp tăng dần, không chồng; chèn dùng `[n,n)` |
| `auto.contentEligibility` | Nội dung có đủ điều kiện hay bị chặn; tách biệt quyền commit |
| `auto.writeExpectation` | `forbidden`, `may_proceed_after_revalidation` hoặc `pending`; v0 chỉ case vùng đóng hiện hành mới có thể ở mức thứ hai |
| `presentation` | Kết quả trực tiếp, thông báo sửa, danh sách khi bấm fx, ẩn hoặc pending |
| `invariants` | Các bất biến cần kiểm tra khi có implementation; validator hiện tại không chạy chúng trên Word |
| `pendingDecisions` | Chỉ D-01 Space và E3-01 miền Lý/Hóa đang có case pending trong bộ này |

`documentState` và các trường focus/IME là **đầu vào giả lập cho chính sách**, không phải trạng thái được thu từ máy. Có AST captured trong ca stale hoặc composing không có nghĩa UI được phép hiển thị candidate đó. `presentation: hidden` và `writeExpectation: forbidden` ghi giới hạn tương ứng.

Mỗi case accepted tham chiếu source snapshot của chính nó. ID candidate trong corpus là định danh fixture để đối chiếu; core M1 có [identity từ nội dung và phiên bản](../../docs/m1/CORE-USAGE.md) riêng. Không so theo ảnh hoặc chuỗi LaTeX để quyết định AST bằng nhau.

## Liên hệ với bộ tình huống trong backlog

| Đầu vào backlog | Case hoặc giới hạn tương ứng |
| --- | --- |
| C-01 đến C-04: alias, scope, mơ hồ và sửa | M0-001 đến M0-040; M0-070, M0-071, M0-108, M0-109, M0-111 |
| C-05 đến C-07: nhận diện và Unicode | M0-041 đến M0-063; M0-110, M0-113 đến M0-115 |
| C-08 đến C-10: IME, stale, focus | M0-079 đến M0-089; M0-092, M0-093. Mới là policy fixtures, cần spike Word thật |
| C-11 đến C-13: roundtrip, native edit và metadata | M0-095 đến M0-100 là kỳ vọng; bằng chứng COM riêng tại [báo cáo M0](../../docs/m0/REPORT.md), gồm kết quả đạt và các giới hạn selection/copy/CustomXML |
| C-14: vùng đánh dấu | M0-068 đến M0-078; M0-106, M0-112 đến M0-114, M0-117 |
| C-15: Space | M0-101 đến M0-104 pending D-01 |
| C-16: cấu hình/phiên hết hạn | M0-087 đến M0-089; M0-107 |
| C-17: lỗi giữa commit | Bất biến trong CORE-CONTRACT.md; probe COM đã có fault injection ở [bộ Word](../../fixtures/m0/word/README.md). Chưa có Word adapter production và không suy ra PASS cho mọi lỗi |
| C-18: bối cảnh Word đặc biệt | M0-090, M0-091, M0-094 là quy tắc từ chối khi chưa hỗ trợ; Track Changes/bảng/header/footer/content control vẫn cần ma trận môi trường, không xem các ca này là đã kiểm chứng |
| C-19, C-20: đồ thị/hình học | Cần PlotDocument/GeometryDocument và corpus riêng ở E1/E2; không tạo MathDocument giả cho camera hoặc thao tác kéo |
| C-21: Lý/Hóa | M0-064 đến M0-067 pending E3-01; điện tích/phản ứng cần corpus miền đầy đủ khi triển khai |
| C-22: fx và quyền chủ động | M0-105, M0-106, M0-116; khả năng hiển thị/nhấn fx thật cần connector/UI |

Các phần được ghi là chưa có ở bảng này vẫn là công việc cần thực hiện ở các task tương ứng; validator dữ liệu không đóng chúng.

## Cách mở rộng

1. Thêm/sửa case trực tiếp trong `cases.json`; giữ ID ổn định và ghi grammar version khi đổi nghĩa.
2. Bổ sung kiểu AST hoặc trường dữ liệu thì sửa hợp đồng và validator trong cùng đợt.
3. Chạy validator dữ liệu. Case pending không được biến thành pass bằng cách bỏ qua quyết định còn mở.
4. Chạy `./tools/build.ps1` để runner M1 đối chiếu parser thật: projection AST, vùng, loại candidate và diagnostics. Báo kết quả riêng theo grammar; không gọi kết quả validator dữ liệu là parser tests.
5. Khi có Word, dùng các ca policy/roundtrip làm đầu vào kịch bản integration thực tế. Metadata, Undo, source map và focus cần bằng chứng riêng.

Validator không chứng minh chất lượng nhận diện, ánh xạ normalize thực tế, vị trí grapheme đầy đủ, preview/export hoặc giao dịch Word. Các ca future domain không có AST giả; chúng không đóng góp vào tỷ lệ parser đạt.
