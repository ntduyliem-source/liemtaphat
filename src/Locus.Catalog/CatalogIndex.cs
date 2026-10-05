using Locus.DesignSystem.Components;
using Locus.Editor.Formula.Components;
namespace Locus.Catalog;

public sealed record CatalogComponent(Type Type, string Role, string Usage, string States, string Source, string Styles);

public static class CatalogIndex
{
    public const string Version = "0.2.1 · CT4";
    private const string Ds = "src/Locus.DesignSystem/Components/";
    private const string Formula = "src/Locus.Editor/Formula/Components/";
    public static readonly CatalogComponent[] Components = [
        new(typeof(StudioButton), "Button / tooltip", "Lệnh có nhãn. Tooltip dùng title native; ý nghĩa chính phải có trong nhãn.", "default · hover · active · focus · disabled · primary · subtle", Ds+"StudioButton.razor", "controls.css"),
        new(typeof(StudioIconButton), "IconButton", "Undo/Redo trên header. Luôn có Label cho trình đọc màn hình.", "default · hover · focus · disabled", Ds+"StudioIconButton.razor", "controls.css, header.css"),
        new(typeof(StudioIcon), "Icon", "Bộ icon nét đơn đang dùng; icon trang trí không nhận focus.", "undo · redo · copy · download · file · sun · moon · close · image", Ds+"StudioIcon.razor", "controls.css"),
        new(typeof(StudioSegmentedControl), "SegmentedControl", "Nhóm Nhận diện; caller truyền nút và aria-pressed của trạng thái thật.", "selected · unselected · disabled · keyboard focus", Ds+"StudioSegmentedControl.razor", "formula/detection.css"),
        new(typeof(StudioField), "TextField", "Dấu mở/đóng. Phát ValueChanged khi commit thay đổi, giữ nhịp onchange cũ.", "default · focus · disabled · invalid", Ds+"StudioField.razor", "controls.css, formula/markers.css"),
        new(typeof(StudioCheckbox), "Checkbox", "Bật/tắt cặp bọc riêng. Nhãn nằm ngoài input.", "checked · unchecked · disabled · focus", Ds+"StudioCheckbox.razor", "controls.css"),
        new(typeof(StudioSelect), "Select", "Chọn điều kiện Hóa; options do nghiệp vụ cung cấp.", "default · selected · disabled · focus", Ds+"StudioSelect.razor", "controls.css"),
        new(typeof(StudioNotice), "Toast / status", "Thông báo kết quả thao tác; tự ẩn sau 4,5 giây. Không dùng làm nơi duy nhất chứa lỗi cần sửa.", "visible · hidden · repeated message · reduced motion", Ds+"StudioNotice.razor", "controls.css"),
        new(typeof(StudioHeader), "Header", "Giữ vị trí tab, Undo/Redo, theme. WebShell và DesktopShell tự sở hữu bố cục host.", "formula · plot · geometry · light · dark · compact", Ds+"StudioHeader.razor", "header.css + host shell.css"),
        new(typeof(FormulaDetectionBar), "Nhận diện + Cân bằng", "Chỉ phát lệnh; phạm vi và hệ số do FormulaSession quyết định.", "ALL · Toán · Lý · Hóa · MANUAL · IME · balance/cancel/loading", Formula+"FormulaDetectionBar.razor", "formula/detection.css"),
        new(typeof(FormulaSourcePanel), "Textarea / nguồn", "Giữ ElementReference cho bộ gõ. Không bind lại input bằng cơ chế khác.", "empty · filled · readonly · IME · busy · ghost", Formula+"FormulaSourcePanel.razor", "formula/panels.css"),
        new(typeof(FormulaResult), "Kết quả / vùng chọn", "Render ContentDocument và offset gốc. Click vùng phát OnSelect; không chứa fx.", "text · MathML · pending · selected · whole article · empty", Formula+"FormulaResult.razor", "formula/panels.css"),
        new(typeof(FormulaSuggestions), "Ý bạn là", "Ô gợi ý chỉ hiển thị công thức; giải thích qua tooltip và nhãn truy cập. Không có nút Chi tiết hay nhãn loại gợi ý. Có thể trở về cú pháp đã gõ.", "no context · fraction/root/close repair · direct restore · kept text · disabled", Formula+"FormulaSuggestions.razor", "formula/panels.css"),
        new(typeof(FormulaExportBar), "Chọn tất cả / xuất ảnh", "Một selection state cho SVG, PNG 2X/4X và Copy ảnh. Không thay định dạng theo số công thức.", "none · region · range · all · partial · exporting · error", Formula+"FormulaExportBar.razor", "formula/export.css"),
        new(typeof(MarkerSettingsEditor), "Cặp bọc / disclosure", "details/summary native; nhập cặp và lưu qua validation hiện hành.", "expanded · collapsed · invalid · saved · disabled", Formula+"MarkerSettingsEditor.razor", "formula/markers.css")
    ];
}
