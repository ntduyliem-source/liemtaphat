# Nguồn chuẩn và cách truy vết

1. Yêu cầu mới nhất của người dùng quyết định hành vi. Mốc cân bằng 03/10 được giữ tại [BALANCE-COMMAND.md](BALANCE-COMMAND.md).
2. Thiết kế gốc: `ui/locus_studio (2).html` (giữ nguyên). Script demo trong HTML không được dùng làm nghiệp vụ.
3. Giá trị chạy: `src/Locus.DesignSystem/wwwroot/styles/tokens.css`. Catalog đọc trực tiếp declarations, computed values và selector sử dụng; không có bảng màu nhập tay thứ hai.
4. [CT0 baseline](CT0-BASELINE.md) giữ mốc trước thay đổi. [Đặc tả CT4](CT4-PLAN.md) và [biên bản CT4](CT4-VERIFICATION.md) ghi hành vi, build và phạm vi kiểm hiện hành.

## Ánh xạ nguồn → runtime

| Trong HTML gốc / baseline Studio | Token hoặc component hiện hành |
| --- | --- |
| Các màu paper, ink, accent, line, muted, tint; biến thể dark | `--palette-*` → `--studio-paper`, surface, subtle, result, ink, muted, line, accent, tint |
| Nút cam hover và nền chọn vùng công thức | action-hover, token-rest, token-hover, token-active, token-article |
| Gradient viền editor | gradient-mid; `formula/panels.css` |
| Newsreader / Inter / JetBrains Mono | serif / sans / mono; `fonts/fonts.css` và giấy phép OFL trong fonts |
| Hero 2.7rem / compact 1.8rem | size-hero / size-hero-compact |
| Input 14px, kết quả 16px, control 11px | size-input / size-result / size-control |
| Header, pane 520px, heading 40px, footer control 36px | StudioHeader; pane-height / panel-heading / control |
| Transition controls 150ms, theme 200ms | transition / motion-theme; reduced-motion trong CSS component |
| Thanh Nhận diện, cân bằng, hàng Ý bạn là, thanh xuất | Editor/Formula/Components và wwwroot/formula tương ứng |
| Tooltip đơn giản, cặp bọc mở rộng | title native; details/summary native trong MarkerSettingsEditor. Hàng Ý bạn là chỉ có ô công thức, không còn panel Chi tiết từ 0.2.1. |
| Thông báo thao tác của bản Studio trước CT0 | StudioNotice; toast, toast-border, toast-shadow; 4,5 giây |

`--palette-*` chứa giá trị vật lý, `--studio-*` là vai trò sử dụng. Component chỉ dùng vai trò; màu tài liệu do người dùng đặt không được đổi theo theme UI. Một số số đo cấu trúc (inset, border, breakpoint) giữ cạnh component, không tự biến mọi con số thành token.

Catalog hiển thị HEX/RGBA thực, định nghĩa gốc, tương phản với paper hiện tại và selector tiêu thụ. Màu accent/success/danger được giữ theo mẫu; swatch tương phản thấp không được mặc nhiên dùng làm chữ nhỏ. Success trong Công thức là chấm trang trí; không truyền ý nghĩa chỉ bằng màu. Việc thay palette để cải thiện cặp tương phản cần quyết định thiết kế riêng, không lén đổi trong CT1.

Nguồn layout Web: `src/Locus.Web/WebShell.razor`, `wwwroot/shell.css`. Nguồn Windows: `src/Locus.Desktop.Shared/DesktopShell.razor`, `wwwroot/shell.css`, `App.xaml.cs`. Cùng thư viện thương hiệu, độc lập về sản phẩm và release.
