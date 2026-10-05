# Cân bằng chủ động trên trang Công thức — 03/10/2026

Thay yêu cầu chọn trước bằng một nút thao tác toàn bài. Giữ thiết kế thanh Nhận diện/Cân bằng hiện tại.

Bản local: `20261003-115834-995`, <http://127.0.0.1:4197/releases/20261003-115834-995/>. Log build: `artifacts/ui-design/build-balance-command.log`. Publish thành công, còn cảnh báo trimming IL2026 có sẵn ở serializers; đường điền/cân bằng đã chạy được trong worker của bản publish trên IAB.

## Hành vi

| Trạng thái | Nút | Khi bấm |
|---|---|---|
| Không chọn riêng phương trình | Cân bằng | Cân bằng các phương trình đã có hai vế; điền sản phẩm cho vế phải trống nếu có một trường hợp phù hợp duy nhất trong kho, rồi cân bằng. |
| Đã cân bằng, không chọn riêng | Hủy cân bằng | Hủy toàn bộ phần cân bằng do Locus quản lý; giữ sản phẩm đã điền. |
| Chọn một phương trình đã cân bằng | Hủy cân bằng phương trình này | Chỉ trả hệ số của phương trình đó về trước cân bằng. |
| Chọn một phương trình chưa cân bằng | Cân bằng phương trình này | Chỉ xử lý phương trình đó. |

Nút sáng cả khi chưa có selection. Tạm khóa trong khi ghép dấu, nhận diện hoặc đang thực hiện lệnh. Nút không có thêm menu chu kỳ cũ. Bấm nền ô kết quả hoặc Esc khi focus ở kết quả để bỏ chọn.

Ví dụ: `3H2+O2=` → bấm Cân bằng → kết quả `2H2+O2→2H2O`; bấm Hủy cân bằng → `3H2+O2→H2O`. Ô nguồn luôn giữ nguyên `3H2+O2=`. Undo trên header khôi phục toàn bộ lần xử lý, bao gồm cả sản phẩm đã điền.

## Phạm vi điền sản phẩm

- Dùng kho phản ứng cục bộ hiện có. Lệnh chủ động mới có thể lấy điều kiện của một trường hợp duy nhất trong kho; điều kiện đó được ghi vào metadata và nêu dưới kết quả. Luồng ghost cũ vẫn yêu cầu chọn điều kiện như trước.
- Không tự chọn giữa nhiều trường hợp (ví dụ `CO2+NaOH=`), không suy ra khi kho chưa có dữ liệu. Phương trình đó giữ nguyên và có thông báo.
- Phương trình đầy đủ đã cân bằng được ghi nhận bởi lần bấm chủ động, giữ nguyên hệ số người dùng. Phương trình không có nghiệm vẫn giữ nguyên và xuất được.
- Tôn trọng vùng Giữ text gốc và miền nhận diện. Vế phải trống không bọc được nhận diện khi nằm trên một dòng công thức riêng; cặp riêng Hóa vẫn dùng được trong đoạn văn.
- Số `0` không tự biến thành chữ `O`. `fe+o2=feo` được nhận diện, còn `fe+02=feo` không được âm thầm sửa thành chất khác.

## Thực hiện

`FormulaSession.StudioBalanceAsync` dùng chung trên Web/Desktop, gom thay đổi và commit một lần. Hủy cân bằng trả về snapshot hệ số trước đó, tách khỏi snapshot sản phẩm. Không sửa chuỗi nguồn.

Kết quả worker bị loại nếu nguồn, phiên, selection, cấu hình, trạng thái ghép dấu hoặc nội dung DOM đã đổi. Dừng xử lý/lỗi worker không để lại một phần của lệnh. Cặp chưa đóng được bỏ qua, không chặn xử lý các phương trình hợp lệ khác.

## Kiểm chứng

- 16/16 ca mới: không chọn, điền sản phẩm, hủy toàn bài/riêng, giữ hệ số gốc, Undo/Redo, lưu/mở lại, chữ thường trong đoạn, cặp chưa đóng, tổ hợp chưa biết/nhiều khả năng, nguồn/selection đổi, dừng, lỗi worker và đối chiếu wire.
- 49/49 ca Application hiện có: 22 nội dung, 20 cân bằng, 7 xuất tài liệu.
- Bằng chứng: `artifacts/ui-design/balance-command/studio-balance-tests.json` và các thư mục `content-only`, `balance-only`, `document-export-only` cùng cấp.
- Kiểm tra IAB: `H2+O2=` cùng `N2+H2=NH3`, cân bằng/hủy toàn bài, Undo, hủy riêng, bấm nền/Esc bỏ chọn; `fe+o2=feo` trong đoạn cho `2Fe+O2→2FeO`.
- Desktop shared đã build/publish; chưa lặp lại kiểm tra native Windows/Word trong thay đổi này. Không thay clipboard/tải tệp.
