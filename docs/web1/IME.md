# WEB1 · phạm vi bộ gõ đã nghiệm thu

Ngày 2026-09-14, build ứng dụng `20260914-070957-601`.

**Đã đạt bài phím thật với UniKey 4.3 RC5, Unicode, Telex và VNI trên Windows 10 x64; WebView2 152.0.4191.66 nạp đúng website WASM ở cổng 4183.** Core chạy trong browser, `data-host=browser`; shell thử không gọi parser native. Đây là phạm vi OS đã kiểm, không phải chứng nhận mọi browser, bộ gõ hay hệ điều hành.

- Telex: phím `x`, Space, `m`, `u`, `x`, Space, `2` tạo `x mũ 2` và LaTeX `{x}^{2}`. VNI dùng phím `4` để đặt dấu ngã.
- Cả hai kiểu: đổi dấu ngã → sắc → ngã ngay trong từ; nguồn giữ đúng `mu → mũ → mú → mũ`.
- Backspace bỏ số mũ làm công thức chưa hoàn chỉnh và ngừng cho xuất kết quả cũ. Ctrl+Z/Ctrl+Y khôi phục đúng nguồn và phương án.
- Phím Tab/Alt+I chuyển focus ra ngoài/vào ô nhập mà không đổi công thức, được kiểm trong lượt Telex.
- Nguồn được gõ thật của từng kiểu đã xuất SVG/PNG, lưu `.locus` có hash hợp lệ và tải lại từ nháp giữ nguyên nguồn/candidate. PNG của hai kiểu trùng bytes.
- Sau thử VNI đã trả UniKey về Telex và gõ lại để xác nhận; không đổi bảng mã Unicode.

Trong baseline này UniKey phát Backspace + ký tự Unicode thay thế, không phát `compositionstart/end`. Các bài composition giả lập, nhập nhanh và kết quả cũ trên Chromium/Firefox/WebView2 được ghi riêng, không được gọi là phím OS thật. In-app browser của Codex đã kiểm luồng nhập/chọn/marker/copy/reload bằng công cụ browser; chưa có bài UniKey OS trực tiếp trong Codex.

19 kiểm tra bằng chứng được tổng hợp trong `artifacts/web1/ime/status.json`; nguồn/keyboard events và ảnh nằm dưới `artifacts/web1/ime/real-os/` trong workspace. `tools/web1/verify-ime.mjs` kiểm hash, trạng thái công thức, phím dấu đặc trưng và khôi phục cài đặt. Các lỗi công cụ trước lần nhận được phím thật được giữ ở `status-before-real-os.json`.

Kết quả này không mở cổng tự chuyển Word, không thay nghiệm thu W0/G2/G3 và không bảo đảm mọi chuỗi nhập tiếng Việt.
