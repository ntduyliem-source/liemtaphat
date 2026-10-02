# Hướng sản phẩm đã đưa vào kế hoạch — 2026-09-15

Cập nhật sau trao đổi về đũa thần và yêu cầu viết lại kế hoạch. **Chỉ là kế hoạch, chưa code các tính năng mới.** [Baseline](BASELINE-20260915.md) ghi thành quả; [roadmap](ROADMAP.md) và [NEXT-STEPS](NEXT-STEPS.md) thay thứ tự thực hiện cũ. Bản brainstorming trước được giữ trong archive.

## 1. Những ý người dùng đã nêu

- Web nhận đoạn dài và trả nguyên đoạn; chỉ các chỗ nhận diện được dựng thành công thức.
- Người dùng được giữ nội dung/phương trình sai; có cách cân bằng và hủy cân bằng.
- Bôi chọn phương trình trong ô kết quả để bật nút đũa thần; chọn nhiều thì xử lý từ trên xuống, hết lượt mới đổi sang hủy.
- Dropdown cạnh nút có Cân bằng tất cả/Hủy cân bằng tất cả theo selection; mỗi mục bấm lại phục hồi đúng trạng thái trước lệnh đó.
- Checkbox auto cân bằng; dán đoạn chỉ cân bằng khi bật.
- Desktop tiện ích gọn ở tray, input/result/copy/options, bung/thu; Web tương tự về nội dung. Biểu đồ và Hình học có không gian riêng.
- Web có các hạn mức Guest/Free/Premium đã đề nghị.
- Word quét tài liệu sẵn có, đánh dấu, chuyển từng vùng hoặc chuyển tất cả từ fx.

## 2. Baseline tương tác để thiết kế

“Nút chính lần lượt” được hiểu là **mỗi bấm một phương trình**; dropdown mới thực hiện cả vùng. Bấm lại batch là hoàn tác chính lệnh vừa chạy, không gọi lệnh đối lập. Hủy phải trả hệ số gốc và quyết định auto trước lệnh, không đặt tất cả hệ số về 1.

Các trường hợp được cụ thể hóa tại [BALANCE-INTERACTION](ux1/BALANCE-INTERACTION.md): selection dở/ngược/trộn trạng thái, phương trình cân bằng sẵn, lỗi/mơ hồ, batch hủy hoặc stale, quick undo hết hiệu lực, auto và lịch sử sản phẩm. Đây là baseline kỹ thuật để review wireframe, không phải nghiệm thu mọi chi tiết UX.

Lựa chọn nút chính thay cho yêu cầu hiển thị thường trực hai ô hỗ trợ song song ở bản đề xuất đầu. Nguồn và kết quả vẫn phân biệt rõ; có thể xem trước/sau trong fx. Không thêm danh sách ba phương án mới ngoài giới hạn ba của mỗi vùng.

## 3. Editor nhỏ gọn và tài liệu nguyên đoạn

Một ô nhập chung cho công thức đơn và đoạn dài; kết quả giữ toàn văn bản với vùng công thức tại chỗ. Bản mới dự kiến giữ raw trong ô nhập, lưu phần cân bằng như kết quả của từng vùng. Đây là thay đổi model so với SC1 hiện tại thay source khi nhận hỗ trợ, cần migration và lệnh/history phù hợp.

Bố cục mục tiêu:

```text
Locus                         [Bung/thu chỉ Desktop] [Đóng]
[Công thức] [Biểu đồ] [Hình học → 2D / 3D]
┌ Ô nhập: gõ công thức hoặc dán cả đoạn ──────────────┐
└───────────────────────────────────────────────────┘
┌ Ô kết quả: nguyên đoạn + các vùng công thức ───────┐
│ Chọn một phương trình hoặc một đoạn               │
└───────────────────────────────────────────────────┘
[🪄 Cân bằng tiếp · 1/3 ▾] [Copy PNG] [Copy SVG] [Tải ▾]
[✓ Toán] [✓ Lý] [✓ Hóa] [ ] Tự cân bằng     [Tùy chọn]
```

Wireframe UX1-01 phải làm rõ nút copy đang áp dụng công thức chọn hay cả đoạn; PNG/SVG toàn đoạn chỉ bật khi thật sự hỗ trợ, không dùng một icon làm cả hai hành vi ngầm. Tab/công cụ chưa có chỉ nằm trong thiết kế hoặc được ghi chưa hỗ trợ, không giả sản phẩm đã chạy.

Desktop tray/bung giữ cùng session/Undo/nháp, đóng về tray khác Thoát. Web không có nút bung Desktop. Bộ quy tắc chữ/khoảng cách/icon/focus tối thiểu phục vụ trực tiếp các màn này, không mở dự án template riêng.

DOC1 đợt đầu bảo toàn câu chữ và xuống dòng từ text đã dán, không hứa giữ font/bảng/list/hình của clipboard Word. Rich text là phần sau. Copy cả đoạn có fallback theo ứng dụng nhận; DOCX với OMML là đường xuất được đề xuất để kiểm equation native riêng, không coi HTML clipboard là cam kết native Word.

## 4. Quét Word và quyền giữ text

Quét tạo trạng thái **đã nhận diện, đang là text** trực tiếp; không convert rồi restore để giả lập. Có ba quyết định riêng: chưa chuyển, chuyển thành equation, giữ text bỏ qua. Từ native về text cũng cần phân biệt giữ nhận diện với bỏ qua lần Convert all.

Mọi fx có nút hiển thị số lượng/phạm vi chuyển. Convert all chỉ chuyển định dạng theo direct hoặc candidate đã chọn; không tự cân bằng/suy sản phẩm/repair. Batch có revalidate/Undo; scan không sửa văn bản. Dấu highlight ưu tiên lớp tạm, có spike định vị/zoom/DPI; panel điều hướng được công bố riêng nếu highlight chưa đạt.

WD1 bắt đầu trong thân tài liệu và context đã kiểm của M3. Story/bảng/Track Changes/protected có phạm vi riêng; không mở cổng auto Word từ việc có lệnh quét thủ công.

## 5. Hạn mức Web — đầu vào cho WEB2

| Gói | Công thức/ngày | Biểu đồ/ngày | Hình 2D/ngày | Hình 3D/ngày |
| --- | ---: | ---: | ---: | ---: |
| Guest | 5 | 2 | 2 | 2 |
| Free | 21 | 10 | 10 | 10 |
| Premium | 150 | 100 | 100 | 100 |

Các số là đề xuất người dùng; Guest tính theo ngày là baseline đang dùng để lập kế hoạch. Chưa quyết định giá Premium, múi giờ reset, chính sách offline/Desktop, nhà cung cấp đăng nhập/thanh toán/hosting.

Quy tắc tính lượt **đề xuất để chốt ở WEB2-01**:

- Tính khi xác nhận dùng/lưu/xuất một nội dung mới; không tính từng phím, render, kéo slider/điểm, preview hoặc copy lại cùng kết quả.
- Định nghĩa lượt scene biểu đồ/hình học và sửa cùng nội dung phải cụ thể trước ledger; giới hạn số đối tượng là giới hạn tài nguyên riêng.
- Dán nhiều công thức báo trước số lượt cần; khi không đủ thì user chọn vùng dùng, không âm thầm cắt nội dung/kết quả.
- Guest hết lượt mời đăng nhập/tạo tài khoản, vẫn xem/sửa nháp/lấy lại kết quả đã tạo. Free và Premium có hiển thị số còn lại/reset.
- Server quản lý account/quyền/ledger và retry chống trừ đôi, lỗi hoàn lượt. Không gửi raw tài liệu chỉ để tính quota.
- Core đã tải chạy local/offline không thể có quota cứng chỉ bằng cookie/client. Chốt phạm vi dịch vụ và mức kiểm soát thực tế; không quảng cáo 3D trước khi E2B có thật.

Local tiếp tục hoạt động; chưa bật paywall, chưa đưa lên Sites hoặc chạy tài khoản/thanh toán trong lượt lập kế hoạch này.

## 6. Thứ tự và các quyết định còn lại

Ưu tiên **UX1 thiết kế → editor gọn + DOC1 → BAL1 → export/tray/kiểm host → SC1 Word/gói → WD1 → E1 → E2A → E2B → WEB2**. Nếu Word chờ, chuyển phần độc lập/E1 đủ đầu vào; không mất trạng thái task đang thiếu. Nhánh Word auto theo cổng riêng và M6 ổn định theo phạm vi phát hành.

Các chi tiết cần quyết định đúng thời điểm, không hỏi dồn ngay: kích thước/layout ở UX1-01; schema/giới hạn và dạng export ở DOC1-01/03; UI sản phẩm/hệ số ở BAL1-01; highlight/persistence vùng ở WD1-02/04; cách tính lượt/offline/giá/provider ở WEB2-01. Không dùng phần còn mở của thương mại để chặn editor local.

Nhịp mới là demo sớm, build/smoke đúng thay đổi, kiểm tổng hợp một lần cuối cụm. Chỉ mở rộng/rerun khi có lỗi, sửa mới hoặc rủi ro chưa giải quyết. Các guard nguồn/Undo/Word phải đạt trước dữ liệu thật; phần chưa kiểm ghi rõ, không gọi DONE vì đã viết code hoặc đã có kế hoạch.
