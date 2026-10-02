# SC1-07 — Gợi ý mờ và nhận bằng Enter

Đạt local trên Web và Desktop build **20260915-085430-384**. [Bằng chứng đã lưu riêng](../../artifacts/sc1/evidence/20260915-085430-384/ghost/receipt.json) gồm kết quả kiểm, tệp trước/sau nhận, ảnh và hash binary.

Gõ `hoa-[h2+o2=` để mở hỗ trợ Hóa. Khi đã chọn điều kiện mồi phản ứng, trở về ô nhập sẽ thấy sản phẩm mờ và toàn phương trình `2H2+O2→2H2O`, kèm phần hệ số vế trái sẽ đổi. Enter nhận đúng đề xuất này. Cặp đang mở vẫn mở; cặp đã đóng giữ nguyên dấu bọc. Một Undo trả lại nguyên nguồn trước khi nhận.

Ghost nằm ngoài ô nguồn: chưa nhận thì không đi vào nháp, tệp hoặc kết quả xuất. Esc bỏ đề xuất; phím bấm trước khi có ghost không được giữ để nhận sau. Đổi nguồn, điều kiện, cấu hình, vùng chọn, focus hoặc trạng thái ghép chữ làm đề xuất chờ hết hiệu lực. Giao dịch chuẩn bị không thay history; chỉ bước commit đã kiểm lại mới ghi. Chữ gõ tiếp khi commit đang trả về được giữ theo thứ tự nhập.

| Kiểm tra | Kết quả |
| --- | --- |
| Ghost, Enter/Space, Undo, tệp, phím và kết quả trễ | 14/14 nhóm trên từng Chromium, Firefox, Desktop |
| Giao dịch chuẩn bị/commit và preferences | 8/8 |
| Hồi quy cặp bọc, Hóa/Lý, cân bằng, kho, xuất/nháp | Chromium 29/29; Firefox 29/29; Desktop 27/27 |
| Native ↔ WASM | 743 đầu vào giống nhau trên mỗi browser |
| Trì hoãn bước chuẩn bị, thay đổi đầu vào trước commit | 7 tình huống trên mỗi host, nằm trong 14 nhóm trên |

Space mặc định vẫn gõ khoảng trắng. Tùy chọn nhận bằng Space đã có và lưu theo preferences v4; khi bật, nhận và thêm một khoảng trắng trong cùng Undo. Các bài composition và trì hoãn có phần giả lập được ghi rõ trong báo cáo, **không thay bằng chứng UniKey OS**. SC1-08 tiếp tục kiểm Telex/VNI và trợ năng. SC1-09 Word thủ công và SC1-10 gói local chưa nghiệm thu; W0/G2/G3 giữ trạng thái cũ.
