# SC1-06 — Suy sản phẩm có điều kiện

Đạt local trên Web và Desktop build **20260915-083057-747**. [Receipt và báo cáo đã lưu riêng](../../artifacts/sc1/evidence/20260915-083057-747/catalog/receipt.json) chứa hash, kiểm UI và parity.

Kho offline có **45 bản ghi** (44 phản ứng và một tổ hợp không có phản ứng ion rút gọn), cùng ID/version, phạm vi, ngoại lệ và nguồn. [Danh mục/phương pháp](REACTION-CATALOG.md) phân biệt phương trình có sẵn trong nguồn với các phương trình Locus tự biên soạn từ nguyên tắc hóa học. Chưa hỗ trợ suy phản ứng tùy ý.

Trong editor chung, `hoa-[h2+o2=]` → ƒx → chọn tác động mồi phản ứng → xem `2H2+O2→2H2O`. Chỉ khi nhận mới thay nguồn; giữ literal của cặp bọc và Undo về đúng nguồn trước đó. Cặp đang mở giữ nguyên trạng thái mở. `CO2+NaOH=` yêu cầu chọn môi trường và tỉ lệ mol, không suy chúng từ các hệ số đã gõ.

| Kiểm thử | Kết quả |
| --- | --- |
| Toàn danh mục, biến thể giữ riêng, dữ liệu sai, lịch sử | 87/87 |
| Web Chromium / Firefox | 29/29 nhóm mỗi host |
| Desktop WebView2 | 27/27 nhóm |
| Wire native ↔ WASM | 743 đầu vào giống nhau, mỗi trình duyệt |

Đã phân biệt thiếu điều kiện, chưa có dữ liệu và không có phản ứng ion rút gọn trong điều kiện đã khai báo. Không có gợi ý thì không điền phương án cho đủ số lượng. Phương trình đã nhận lưu provenance/điều kiện vào v4; mở file không suy lại.

Nghiệm thu này bao gồm ƒx thủ công. Ghost/Enter/Space thuộc SC1-07/08; SC1 Word chưa nằm trong báo cáo này. Không dùng kết quả các biến thể trong danh mục làm độ chính xác với toàn bộ hóa học.
