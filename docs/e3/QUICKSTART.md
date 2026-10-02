# Locus E3 — Hóa và Lý trong cùng editor

Mở [bản local E3](http://127.0.0.1:4184/) trên máy này. Web và Desktop dùng cùng ô nhập, preview, fx, copy, tệp và nháp. Chỉ bổ sung ba checkbox **Nhận diện: Toán / Lý / Hóa**. Không cần Word để dùng Web/Desktop.

## Dùng thử

1. Bật **Hóa** rồi gõ `H2SO4`, `Ca(OH)2`, `SO₄²⁻` hoặc `2H2 + O2 -> 2H2O`.
2. Bật **Lý** rồi gõ `v_0 = 10 m/s^2`, `vec(v)`, `alpha = 2`, `v = 15 km/h`. Giá trị cách đơn vị bằng khoảng trắng. `m/s` đứng riêng còn là đại số; `unit(m/s)` chỉ rõ biểu thức đơn vị.
3. Có thể bật nhiều môn hoặc tắt hết. Thay checkbox giữ công thức đã dựng; sửa nguồn hoặc **Ctrl+Enter** để dựng lại theo tập mới. Nguồn không bị thay đổi.
4. Xem các cách hiểu qua **fx** như trước. Tối đa ba phương án tổng; một đề nghị sửa có nhãn riêng.
5. Copy/tải SVG, PNG, LaTeX hoặc nguồn. **Lưu tệp** tạo `.locus` v2, mở lại trên hai host giữ snapshot/candidate, kể cả khi detector tương ứng đã tắt. Đọc được tệp Toán v1; bản WEB1 cũ không đọc tệp v2.
6. Tự lưu nháp, phím tắt và offline giữ luồng WEB1. Khi thấy **Sẵn sàng dùng offline**, có thể tải lại trang đã cache mà không cần server. Nháp theo hồ sơ và địa chỉ; đổi cổng sẽ dùng kho khác.

`Co` là cobalt, `CO` là công thức gồm C và O: không tự sửa hoa/thường. Gõ điện tích rõ `Ca^2+`, `NH4^{+}` hoặc Unicode; `Fe3+` không được đoán. Alias `mu` giữ nghĩa mũ Toán; ký hiệu μ dùng `μ` hoặc `micro`. Vector `vec(v_0)` phủ mũi tên lên ký hiệu có chỉ số; `vec(v)_0` đặt chỉ số ngoài vector.

## Chạy từ workspace

```powershell
./tools/e3/build.ps1
./tools/e3/local.ps1 -Action start
```

Bộ mở dùng Node.js, chỉ nghe loopback cổng 4184. Dừng bằng `./tools/e3/local.ps1 -Action stop`. Không dừng server WEB1 cổng 4183.

Desktop mới nằm trong thư mục `desktop` được ghi tại `artifacts/e3/current-build.json`. Chạy `Locus.Desktop.Shared.exe`; cần .NET 10 Desktop Runtime và Edge WebView2, Windows x64. Khi kiểm thử tự động dùng `--profile <thư-mục-riêng>` để không dùng nháp của phiên thường.

Trong gói Web ZIP, giải nén toàn bộ, bấm `Start-Locus-Web.cmd`; Node.js 22+ phải có trên máy. Dừng bằng `Stop-Locus-Web.cmd`. Gói Desktop chạy file exe. [Báo cáo](REPORT.md) ghi phạm vi từng host và bằng chứng.

Nếu workspace đang phục vụ cổng 4184, dừng bằng `tools/e3/local.ps1 -Action stop` trước khi mở một gói Web ở thư mục khác. Bộ mở không dừng một server thuộc thư mục khác.

## Word thủ công

Gói Word E3 dành cho Word Windows x86 + .NET Framework 4.8. Đóng Word trước khi cài/đổi gói. Chạy `register.ps1 -Action Install` trong thư mục gói. Nếu đã cài gói M3 từ thư mục khác, chạy `register.ps1 -Action Uninstall` ở đúng gói cũ trước; bản cũ và ZIP được giữ để quay lại.

Word → tab **Locus** → **Cặp bọc**, chọn ba checkbox và lưu. Chọn nguồn trong thân tài liệu → **Chuyển vùng chọn** → xem trước → xác nhận. **Mở công thức Locus** đọc snapshot đã lưu, khôi phục nguồn hoặc tách quản lý. Bật detector không bật auto-Space hay tự chuyển. Vòng native có nghiệm thu riêng trong báo cáo.

## Giới hạn của đợt này

Đây là nhập/trình bày công thức cơ bản. Chưa cân bằng phản ứng, kiểm đúng/sai Hóa, cấu trúc phân tử, trạng thái chất/nhãn mũi tên; chưa đổi đơn vị, kiểm thứ nguyên, giải bài hoặc thêm scientific notation. Chưa có đồ thị/thanh trượt/hình học. [Cú pháp đầy đủ và nguồn chuẩn](GRAMMAR.md). Telex/VNI OS giữ bằng chứng WEB1; bài composition E3 không thay cho một đợt nghiệm thu OS mới.
