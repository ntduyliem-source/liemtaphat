# Dùng thử đợt D

Build `20260915-181724-011`, local. [Bản Web](http://127.0.0.1:4189/releases/20260915-181724-011/). [Báo cáo và những phần chưa đạt](REPORT.md).

## Chạy

Tại gốc repository:

```powershell
./tools/doc1/local.ps1 start
```

Desktop: mở `artifacts/doc1/builds/20260915-181724-011/desktop/Locus.Desktop.Shared.exe`. Bản đang chạy để kiểm dùng `--profile D:\duan\HMDA\locus\artifacts\doc1\desktop-profile`; mở lại cùng profile sẽ đưa phiên cũ lên. Mở không có tham số dùng profile mặc định, không phải profile thử này.

## Xuất nguyên đoạn

1. Bấm **Mở**, chọn `artifacts/doc1/verification/cross-host.locus`. Mẫu có đoạn văn, Toán/Lý/Hóa, phương trình đã cân bằng và `hoa-[H2SO4]` được giữ là text.
2. **Tải → DOCX với công thức Word** xuất nguyên đoạn; **HTML với công thức** xuất bản để xem trong browser. Bấm fx của một vùng rồi xuất để lấy riêng vùng đó; **Bỏ chọn** trở về toàn đoạn. Chọn dở công thức thì chọn trọn lại trước khi xuất.
3. **Lưu** tạo `.locus` để giữ toàn bộ nguồn và các quyết định của Locus. DOCX/HTML chỉ là bản xuất nội dung, không mang lịch sử để mở lại trong Locus. PNG/SVG hiện dành cho một công thức.
4. Mẫu `artifacts/doc1/verification/products-ignore.locus` có nguồn `hoa-[3H2+O2=]`, sản phẩm đã nhận nhưng đã hủy cân bằng. Kết quả cần giữ `3H2+O2 → H2O`, chấp nhận hệ số chưa đúng.

Có sẵn [DOCX mẫu](../../artifacts/doc1/verification/mixed-native.docx), [HTML mẫu](../../artifacts/doc1/verification/mixed-native.html) và [PDF đã render bằng Word](../../artifacts/doc1/verification/mixed-native.pdf). Đây là đầu ra của bộ kiểm cùng exporter, không phải tệp đã nhận lại từ download UI.

## Gom bài Windows còn thiếu

| Bài | Thao tác và kết quả cần thấy |
|---|---|
| File hai chiều | Web lưu → Desktop mở → hủy cân bằng → native Save → Web mở: giữ nguyên raw, sản phẩm và hệ số trước cân bằng; không tự cân bằng lại |
| Cửa sổ | Chọn công thức, tạo một thay đổi, bung/thu/ẩn/X rồi mở bằng tray/chạy lại: cùng nguồn/selection/Undo; PNG/SVG còn dùng được |
| Lưu và thoát | Hủy hộp lưu giữ phiên; Thoát chờ nháp; tắt tự lưu thì có lựa chọn ở lại hoặc thoát bỏ thay đổi chưa lưu |
| Bộ gõ | Telex/VNI nhập `x mũ 2`, đổi focus, Enter/Space/Esc/Tab; không mất/tách chữ và không nhận ghost khi đang ghép dấu |
| Clipboard/download | Copy một vùng/cả đoạn → dán đúng phạm vi; kiểm tệp tải được có đúng equation. Nếu báo hoàn tất nhưng dán ra dữ liệu cũ, ghi host/build và nguồn để xử lý D-CLIP-01 |
| DOCX | Mở cả hai DOCX mẫu trong Word, sửa được equation native, SaveAs/đóng/mở lại vẫn giữ số công thức và chữ |

Không cần chạy lại corpus hóa học cho các bài này. Không chạy lặp probe Word nếu COM tiếp tục treo; ưu tiên thao tác trực tiếp khi công cụ native hoặc người thử sẵn sàng.
