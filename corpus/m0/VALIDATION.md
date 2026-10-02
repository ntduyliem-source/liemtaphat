# Bằng chứng kiểm tra dữ liệu corpus M0

Ngày: 2026-09-12. Workspace: `D:\duan\HMDA\locus`. Runtime đã chạy: **Python 3.11.3**, executable `C:\Users\Admin\AppData\Local\Programs\Python\Python311\python.exe`.

Lệnh tái hiện:

```powershell
python tools/m0/validate_corpus.py --self-test
```

Kết quả kiểm tra dữ liệu:

```text
VALIDATOR NEGATIVE CONTROLS: 15 intentionally invalid copies rejected; corpus unchanged.
VALID FIXTURE DATA: 117 cases; 109 specified; 8 pending.
Validated schema, UTF-16 spans, AST shape, candidate kinds/order/count, and declared auto-policy invariants.
NOT RUN: parser, detector, renderer, production source mappings, Word, IME, or runtime roundtrip tests.
```

Exit code: `0`. Validator chỉ dùng Python stdlib. Negative controls gồm span cắt surrogate pair, span ngoài nguồn, offset boolean, nguồn không khớp, AST lạ, Number sai kiểu, hơn ba candidate, repair bị cho phép auto, focus sai nhưng vẫn auto, cấu hình dấu lỗi, ID trùng, pending được phép auto, AST miền sai, edit trên direct và interpretation thiếu direct.

Số `109 specified` có nghĩa đã viết kỳ vọng theo hợp đồng hoặc baseline grammar, không phải 109 parser tests đã pass. Tám ca pending gồm bốn ca Lý/Hóa (E3-01) và bốn ca Space (D-01). Chưa chọn production stack và chưa dùng COM hoặc UI trong phần việc này.

## Cập nhật cặp bọc theo lựa chọn của người dùng

Mặc định đã đổi thành `lc[...]`, vẫn cho phép tùy chỉnh. Đã chuyển 29 case chứa dấu mặc định bằng ánh xạ vị trí từ nguồn cũ sang nguồn mới và tính lại toàn bộ span UTF-16 liên quan: vùng nội dung, vùng thay thế, diagnostic và repair edit. Source text được lấy lại từ span trên nguồn mới; MathDocument giữ nguyên. Không thay chuỗi trong JSON rồi giữ nguyên offset cũ.

- M0-068: `lc[x^2]`, content `[3,6)`, replacement `[0,7)`.
- M0-070: `lc[can(2+3]`, chẩn đoán và edit thêm ngoặc cùng neo ở `[10,10)`.
- M0-117: thêm ca cấu hình tùy chỉnh `open = toan[[`, `close = ]]`; giữ nguồn `toan[[x^2]]` và span tương ứng cặp này.

Đã chạy lại cùng lệnh validator sau chuyển đổi: exit code `0`, 117 case hợp lệ và 15 negative controls bị từ chối. Quyết định này không chốt auto-Space; bốn ca D-01 vẫn pending.
