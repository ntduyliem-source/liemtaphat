# Thử W0 trong Word

Ngày: 2026-09-13. Đây là bản nghiên cứu trên Microsoft Word x86 đang cài ở máy thử. Gõ trong tài liệu Word do **Locus W0** tạo. Mọi bài dưới đây dùng nguồn tổng hợp, không cần mở tài liệu cá nhân.

## Mở bài thử

Đóng Word, chạy từ thư mục dự án:

```powershell
./tools/w0/prepare-trial.ps1
```

Script mở bài `fx`. Trong Ribbon, dùng đúng tab **Locus W0**. Tại máy này còn có một add-in tên gần giống trong Home; các nút đó không thuộc bài thử này.

## 1. Dấu fx

1. Đưa focus vào vùng soạn thảo của tài liệu vừa mở. `fx` cần nằm cạnh `x^2`, không che công thức.
2. Cuộn xuống cho công thức khuất rồi cuộn về: dấu cần ẩn và xuất hiện lại đúng chỗ.
3. Đổi zoom 100% → 150% → 80%. Dấu cần tiếp tục bám đúng vùng.
4. Bấm `fx`: cửa sổ trạng thái W0 cần mở. Đóng cửa sổ trạng thái rồi tiếp tục bài dưới.

Nếu sửa nguồn, dấu cũ sẽ hết hiệu lực. Chọn nguồn mới rồi bấm **Gắn fx vào vùng chọn** để thử lại. Việc ẩn dấu khi chuyển cửa sổ hoặc rời editor là hành vi của bản thử.

Kết quả cần ghi: có nhìn thấy dấu hay không; có che chữ/lệch vùng không; scroll/zoom có giữ đúng vị trí không; click có mở trạng thái không. Máy hoặc DPI chưa thử phải ghi rõ, không suy ra từ phép tính tọa độ mô phỏng.

## 2. Nhập nối công thức

Mỗi nút **A — Giữ nguồn** hoặc **B — Native theo Space** tạo một tài liệu mới và đặt con trỏ giữa `Source: ` với ` | Outside remains.`. Gõ ngay tại con trỏ đó. Khung Locus hiện nguyên nguồn và kết quả để đối chiếu.

| Biến thể | Khi bấm Space | Khi muốn chốt |
| --- | --- | --- |
| A — Giữ nguồn | Giữ văn bản trong Word, cập nhật preview | Bấm **Chốt 1 — trực tiếp**, hoặc chọn một phương án khác |
| B — Native theo Space | Nếu có một kết quả đủ điều kiện, cập nhật một equation nhưng tiếp tục giữ toàn bộ nguồn của phiên | Bấm **Chốt** để kết thúc phiên, rồi viết câu văn |

Với mỗi biến thể, tự gõ đủ ba chuỗi sau bằng bộ gõ đang dùng, gồm Space giữa các từ và ở cuối:

1. `x mũ 2 cộng 1 `
2. `1 trên 2 `
3. `căn x cộng 1 `

Ở chuỗi cuối, kết quả trực tiếp là căn x rồi cộng 1; mở rộng căn sang cả `x+1` là **đề nghị sửa**. Space phải chờ lựa chọn khi xuất hiện đề nghị sửa. Chọn trực tiếp để kết thúc bài.

Sau mỗi bài, thử một Ctrl+Z, Ctrl+Y, gõ tiếp một câu văn và xem phần `Outside remains.` có nguyên vẹn không. Bắt đầu bài mới bằng nút A/B; không xóa hoặc di chuyển phần khung `Source:`/`Outside remains.` trong cùng phiên.

**Giữ văn bản** khôi phục nguyên nguồn của phiên rồi dừng. **Dừng thử**, Esc hoặc Enter dừng phiên và giữ nội dung hiện tại; Enter vẫn xuống dòng theo Word. Đây là hành vi để so sánh trong W0, chưa chốt phím kết thúc cho sản phẩm. Undo một lần cập nhật B trả lại equation trước đó và phần chữ vừa gõ; phiên dừng để tránh áp lại kết quả cũ.

Kết quả cần ghi: ba chuỗi có gõ trọn được không; có mất chữ/tách công thức ngoài ý muốn không; Undo có dễ hiểu không; sau khi chốt có viết tiếp câu văn được không; A hay B dễ dùng hơn và điểm nào cần đổi. Đây là dữ liệu người thử; nhật ký do Codex gõ tự động không thay thế phản hồi này.

## Lưu bằng chứng và kết thúc

```powershell
./tools/w0/read-probe.ps1 -Label participant-observation
& C:/Windows/SysWOW64/WindowsPowerShell/v1.0/powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File tools/w0/invoke-probe.ps1 -Action SaveObservations -Label participant-log
```

`interactive-owner.json` chỉ đến thư mục quan sát của đúng phiên. Nguồn chỉ được ghi cho tài liệu thử thuộc phiên đó. Sau khi đã ghi nhận phản hồi, đóng tài liệu thử hoặc dùng `finish-probe.ps1` qua PowerShell x86; script chỉ đóng sandbox được xác minh, rồi gỡ đăng ký bằng `register-probe.ps1 -Action Uninstall` khi Word đã thoát.

Không dùng bài thử này làm bằng chứng hoàn tất bộ gõ để bật auto cho tài liệu thường. Bản B chỉ được bật bằng nút thử riêng trong sandbox. Auto cặp bọc `lc[...]` và auto sản phẩm vẫn thuộc các mốc sau.
