# WEB0 — Kết quả tương thích

Ngày: 2026-09-14. **WEB0 đạt phạm vi thử kiến trúc, 3/3 task.** Core thật đã chạy trong browser, kết quả tương đương native và cùng editor nhỏ chạy trong Web/Desktop. Đây chưa phải WEB1 hoặc website public. [Chỉ mục có hash](../../artifacts/web/compatibility.json), [kiến trúc chốt](ARCHITECTURE.md), [chạy thử](QUICKSTART.md).

## Những gì đã có

- Host Blazor WebAssembly publish Release dưới dạng tệp tĩnh. Toàn bộ việc parse công thức chạy trong browser; không cần `/analyze`, SignalR hoặc .NET server trên máy người dùng.
- Nhập tiếng Việt, marker mặc định `lc[...]` và marker tùy chỉnh, nhiều vùng, preview MathML, chọn trực tiếp/repair, LaTeX và tải snapshot từ candidate đang chọn. Raw/NFD giữ nguyên.
- Cùng component và Scene C# trong browser/WPF Hybrid: kéo A/B/C, một drag một Undo, Redo, Esc hủy kéo và tải SVG từ tọa độ đã commit.
- Bản đối chiếu JSXGraph tải local, nhận mảng điểm C# và vẽ curve mẫu. Chưa có editor nhập hàm E1, hình học dựng quan hệ E2A hoặc Lý/Hóa.

## Baseline và kiểm tra

Máy Windows 10 x64 build 19045, Xeon E5-2680 v4, 56 logical CPU, khoảng 48 GiB RAM. SDK 10.0.400; .NET/WASM 10.0.11; Playwright 1.63.0. Phiên bản đầy đủ ở từng `verification.json`.

| Host | Core contracts | Parity nguồn | Nhóm UI/lifecycle | Bằng chứng |
| --- | --- | --- | --- | --- |
| Native .NET 10 | 238 PASS | Baseline 116 | Không có UI | [native](../../artifacts/web/native/contracts.json) |
| Chromium 153.0.8010.12, WASM | 238 PASS | 116/116 trùng | 17 PASS | [Chromium](../../artifacts/web/runs/chromium/verification.json) |
| Firefox 155, WASM | 238 PASS | 116/116 trùng | 17 PASS | [Firefox](../../artifacts/web/runs/firefox/verification.json) |
| WebView2 152.0.4191.66, WASM | 238 PASS | 116/116 trùng | 17 PASS | [WebView WASM](../../artifacts/web/runs/webview-wasm/verification.json) |
| WPF Hybrid, core native | 238 PASS | 116/116 trùng | 16 PASS | [Hybrid](../../artifacts/web/runs/hybrid/verification.json) |

UI suite có nhóm parity/contracts, không cộng chúng lần nữa để quảng bá tổng số test. Hybrid không có bài ngắt mạng browser nên ít hơn một nhóm. Native/Browser cùng dùng các file kiểm tra core; một assertion timer đã được sửa như mô tả bên dưới. Tám case corpus còn pending không được tính đạt.

116 nguồn parity gồm 104 case phân tích đã chốt trong corpus và 12 case bổ sung: NFC/NFD, emoji, Hangul, marker tùy chỉnh/lỗi, nguồn rỗng/dài, repair và revision lớn. Năm case mở snapshot được kiểm ở bộ contracts. So sánh chuỗi JSON nguyên vẹn gồm raw/revision/hash, ánh xạ UTF-16, detection/eligibility, diagnostics, vùng thay thế, MathDocument, ID/thứ tự/loại candidate, edits, LaTeX/MathML/OMML và snapshot đã round-trip. Các lỗi đầu vào dự kiến cũng phải cùng loại/kết quả; parity không có nghĩa mọi nguồn đều được parser hỗ trợ.

Hồi quy `tools/build.ps1`: **238 core + 40 Desktop đạt**, cùng DLL core cho kết quả giống nhau trên .NET 10 và .NET Framework 4.8 x86 với 13 nguồn runtime probe. Mã sản xuất `src/Locus.Core` không đổi so baseline M3; chỉ thêm cách chạy test trong bộ nhớ và sửa assertion timer. Không chạy lại hoặc chứng nhận thêm cổng Word từ kết quả Web.

## Nhập tiếng Việt và sự kiện ghép dấu

Đã gõ các phím OS `x`, Space, `m`, `u`, `x`, Space, `2` qua Windows SendInput, với **UniKey 4.3 RC5, Telex, Unicode** đang chạy. Bản publish WASM được mở trong WebView2 shell riêng; quan sát `x mu → x mũ → x mũ 2`, preview `x²`, LaTeX `{x}^{2}`. Backspace xóa số mũ thì preview cũ biến mất; Ctrl+Z phục hồi nguồn và preview đúng. [Phím và events](../../artifacts/web/ime-wasm-telex.json), [Backspace](../../artifacts/web/ime-wasm-backspace.json), [Undo](../../artifacts/web/ime-wasm-undo.json).

Trong bài này UniKey phát Backspace + ký tự Unicode thay thế, không phát `compositionstart/end`. Suite tự động kiểm riêng chuỗi composition giả lập: không phân tích khi đang ghép, phân tích lại khi hoàn tất; thêm rapid input và nguồn quá dài không bị cắt. Hai loại bằng chứng này được ghi tách biệt.

Kiểm Telex trực tiếp trong Chrome thường chưa hoàn tất: công cụ Windows báo `coordinate input geometry is unavailable`, capture báo `SetIsBorderRequired ... 0x80004002`, và focus không chuyển ổn định khỏi thanh địa chỉ. Đã dùng WebView2 chạy **WASM thật**, không thay bằng bài nhập Hybrid native. Ma trận Telex/VNI đầy đủ trên Chrome/Firefox/browser phát hành, tốc độ gõ thường và trường hợp đổi focus vẫn thuộc WEB1-03; không lấy bài ngắn này làm chứng nhận mọi IME/browser.

## Khác biệt đã tìm và xử lý

1. **Serializer/trimming:** bản thử bật globalization đầy đủ, reflection serializer và preserve Core/Shared để chạy DataContract DTO/test entry points. Parity và snapshot chạy trên bản publish thật. Chưa chứng nhận khi tối ưu bỏ preservation; WEB1 phải đo/kiểm lại sau đổi trimming.
2. **Assertion timer 1 ms:** lần chạy browser đầu là 237/238, lỗi `cancel-during-long-combining-sequence`. [Kết quả gốc](../../artifacts/web/browser-contracts.json) được giữ. `CancelAfter(1)` không đảm bảo token đã được báo khi phép tính đồng bộ còn chạy; giả định thời gian này cũng không ổn định trên native. Test được đổi thành `pre-cancelled-long-combining-sequence` để kiểm token đã hủy một cách xác định. Đây là thay đổi test, không phải bằng chứng đã giải quyết hủy giữa phép tính trong browser.
3. **Hủy tác vụ đang chạy:** `Scheduling()` đo riêng, ghi OBSERVED và không tính PASS. Bản WASM không ngắt phép chuẩn hóa đồng bộ bằng timer ở bài quan sát; native cũng có thể nhận timer sau khi phép tính đã trả về. Prototype kiểm giới hạn 4.096 UTF-16 trước parse. Worker/deadline và bỏ kết quả cũ là việc bắt buộc của SH-01 trước mở rộng tác vụ nặng.
4. **Reload Hybrid:** gọi JS dọn DOM từ `DisposeAsync` có thể gọi vào object đã mất và làm thoát ứng dụng. Đã chuyển cleanup sang removal observer phía JS; giữ dispose reference riêng. Reload và đổi tab được kiểm lại. Runner phải đợi state/UI cập nhật qua interop, không assert ngay sau click; cũng phải chọn đúng URL editor vì WebView có trang Downloads riêng.
5. **Publish lặp:** output có thể giữ các file đã fingerprint từ build cũ. Script build hiện dọn đúng thư mục generated browser trước publish; số đo dưới đây chỉ lấy bản sạch cuối.

## Dung lượng và hiệu năng

[Dữ liệu đo](../../artifacts/web/performance.json), [danh sách tệp và SHA-256](../../artifacts/web/payload-manifest.json). Bản Release dùng IL interpreter; chưa cài workload `wasm-tools`, chưa AOT/native relinking. Bundle còn chứa corpus/entry points kiểm tra và preservation toàn assembly; đây chưa phải ngân sách tải cho bản phát hành.

| Phép đo | Kết quả trên localhost |
| --- | --- |
| Tệp nội dung chưa nén | 65 tệp, 11.713.348 byte (~11,17 MiB) |
| Tổng nếu phục vụ bản Brotli sẵn có | 3.422.255 byte (~3,26 MiB); là tổng file thay thế, chưa đo wire Brotli |
| Tổng nếu phục vụ bản gzip sẵn có | 4.275.726 byte (~4,08 MiB) |
| Toàn thư mục publish, gồm cả bản nén | 191 tệp, 19.403.675 byte |
| Khởi động với context browser mới, 7 lượt | p50 1,386 giây; p95 1,413 giây |
| Reload với cache, 7 lượt | p50 0,702 giây; p95 0,712 giây |
| Tải ban đầu đo qua Resource Timing | p50 10.755.048 byte; server thử không nén |

Context nguội không xóa cache đĩa của OS; warm reload cùng context, cache HTTP 60 giây. Không áp dụng số này cho mạng internet hoặc điện thoại.

`Timing()` đo 250 lượt core đã warm với 5 nguồn ngắn, không gồm UI/export/network. Lượt cuối: Chromium p50/p95 0,8/2,8 ms; Firefox 1/3 ms; WASM WebView2 0,7/2,7 ms; Hybrid 0,0655/0,1456 ms; native 0,0627/0,1417 ms. Dữ liệu gốc ở `runs/<host>/timing.json` và `native/timing.json`. Không lấy trung bình giữa các host hoặc coi đây là SLA của công thức dài.

Ngắt mạng bằng browser context sau khi tài nguyên đã tải vẫn phân tích được. Network log không có request phân tích hoặc backend .NET; JSXGraph được tải từ file local. **Chưa có offline reload/PWA/cache update**, chưa kiểm nhập đồ thị offline khi asset đồ thị chưa từng được tải.

## Phần chưa phải tính năng phát hành

Web đang được serve trên localhost; chưa có domain/URL online. Chưa có SVG/PNG công thức trên Web, lưu/mở tài liệu chung, lịch sử bền, nháp hoặc phục hồi sau reload. Copy SVG của canvas hiện là mã văn bản; chưa nghiệm thu clipboard ảnh vector giữa ứng dụng. MathML dựa trên font/browser, chưa chứng nhận trên Safari/mobile hoặc DPI/assistive technology đầy đủ.

Chốt WEB0 cho phép bắt đầu SH; không đánh dấu SH/WEB1/E1 đã xong. W0 giữ 4/6 mục và G2/G3 chưa đạt; phản hồi người dùng sẽ bổ sung sau theo checklist W0 cũ.
