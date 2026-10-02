using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Locus.Core;
using Locus.Core.Detection;
using Locus.Desktop.Rendering;
using Microsoft.Win32;
using RenderOptions = Locus.Desktop.Rendering.RenderOptions;
using InputMode = Locus.Core.Detection.InputMode;

namespace Locus.Desktop;

public partial class MainWindow : Window
{
    private readonly EditorSession session = new();
    private readonly DispatcherTimer debounce = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private FormulaScene? scene;
    private bool ready, changingUi, closed;
    private readonly bool persistPreferences;
    private long analysisTicket;
    private long compositionEpoch;
    internal EditorSession Session => session;
    internal FormulaScene? Scene => scene;

    public MainWindow() : this(true) { }
    internal MainWindow(bool persistPreferences)
    {
        this.persistPreferences = persistPreferences;
        InitializeComponent();
        var preferences = persistPreferences ? Preferences.Load(Preferences.DefaultPath) : new Preferences();
        ModeBox.SelectedIndex = preferences.Mode;
        OpenMarker.Text = preferences.Open; CloseMarker.Text = preferences.Close;
        FontBox.SelectedIndex = preferences.FontIndex; ScaleBox.SelectedIndex = preferences.ScaleIndex;
        WhiteBackground.IsChecked = preferences.WhiteBackground;
        foreach (var box in new[] { Editor, OpenMarker, CloseMarker })
        {
            TextCompositionManager.AddPreviewTextInputStartHandler(box, CompositionStarted);
            TextCompositionManager.AddPreviewTextInputUpdateHandler(box, CompositionStarted);
            box.AddHandler(TextCompositionManager.TextInputEvent, new TextCompositionEventHandler(CompositionCompleted), true);
            box.LostKeyboardFocus += (_, _) =>
            {
                long epoch = compositionEpoch;
                if (session.IsComposing) Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => { if (epoch == compositionEpoch) FinishComposition(); });
            };
        }
        debounce.Tick += async (_, _) => { debounce.Stop(); await AnalyzeNowAsync(); };
        PreviewKeyDown += HandleShortcut;
        Closed += (_, _) => { closed = true; debounce.Stop(); session.Dispose(); };
        Closing += (_, _) => SavePreferences();
        PreviewFrame.ContextMenu = new ContextMenu();
        foreach (var (label, format) in new[] { ("Copy SVG", CopyFormat.Svg), ("Copy PNG", CopyFormat.Png), ("Copy LaTeX", CopyFormat.Latex) })
        {
            var item = new MenuItem { Header = label, Tag = format };
            item.Click += async (_, _) => await CopyAsync(format);
            PreviewFrame.ContextMenu.Items.Add(item);
        }
        PreviewFrame.ContextMenuOpening += (_, e) => { if (scene == null || !session.CanExport) e.Handled = true; };
        ready = true;
        Editor.Text = "can2 + x mũ 2";
        SyncSource();
    }

    private void SourceChanged(object sender, TextChangedEventArgs e) { if (ready) SyncSource(); }
    private void ConfigurationChanged(object sender, SelectionChangedEventArgs e) { if (ready) SyncSource(); }

    private void SyncSource()
    {
        if (closed) return;
        debounce.Stop(); analysisTicket++;
        session.Update(Editor.Text, (InputMode)Math.Max(0, ModeBox.SelectedIndex), new MarkerConfiguration(OpenMarker.Text, CloseMarker.Text));
        MarkerFields.Visibility = session.Mode == InputMode.Markers ? Visibility.Visible : Visibility.Collapsed;
        InputCount.Text = Editor.Text.Length.ToString("N0") + " ký tự";
        ClearResults();
        if (session.IsComposing) { InputStatus.Text = "Đang ghép ký tự…"; AnalyzeButton.IsEnabled = false; return; }
        if (Editor.Text.Length > 65536) { InputStatus.Text = "Đoạn nhập vượt 65.536 ký tự. Hãy tách nhỏ nội dung; phần đã gõ vẫn được giữ lại."; AnalyzeButton.IsEnabled = false; return; }
        AnalyzeButton.IsEnabled = true;
        InputStatus.Text = string.IsNullOrWhiteSpace(Editor.Text) ? "Gõ một công thức để bắt đầu." : "Đang chờ bạn nhập…";
        if (!string.IsNullOrWhiteSpace(Editor.Text)) debounce.Start();
    }

    private void ClearResults()
    {
        scene = null; Preview.Source = null; EmptyPreview.Visibility = Visibility.Visible;
        EmptyPreview.Text = "Công thức sẽ xuất hiện ở đây";
        CandidatePanel.Children.Clear(); SourcePreview.Text = ""; ResultCount.Text = ""; SelectionNote.Text = ""; ExportStatus.Text = "";
        ExportButtons.IsEnabled = false; TextExportButtons.IsEnabled = false;
        changingUi = true; RegionBox.Items.Clear(); RegionBox.Visibility = Visibility.Collapsed; changingUi = false;
    }

    private void CompositionStarted(object sender, TextCompositionEventArgs e)
    {
        if (!ready || closed) return;
        debounce.Stop(); analysisTicket++; compositionEpoch++; session.BeginComposition(); ClearResults();
        InputStatus.Text = "Đang ghép ký tự…"; AnalyzeButton.IsEnabled = false;
        // Never mark the event handled or rewrite the TextBox; the input method owns composition.
    }
    private void CompositionCompleted(object sender, TextCompositionEventArgs e)
    {
        if (!ready || closed || !session.IsComposing) return;
        long epoch = compositionEpoch;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () => { if (epoch == compositionEpoch) FinishComposition(); });
    }
    private void FinishComposition()
    {
        if (closed || !session.IsComposing) return;
        session.EndComposition(); SyncSource();
    }

    private async void AnalyzeClick(object sender, RoutedEventArgs e) { debounce.Stop(); await AnalyzeNowAsync(); }
    internal async Task AnalyzeNowAsync()
    {
        debounce.Stop();
        if (closed || session.IsComposing || Editor.Text.Length > 65536) return;
        long ticket = ++analysisTicket, revision = session.Revision;
        InputStatus.Text = "Đang phân tích…";
        try
        {
            if (!await session.AnalyzeAsync() || closed || ticket != analysisTicket || session.Revision != revision) return;
            RenderResults();
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            if (ticket != analysisTicket || closed) return;
            ClearResults(); InputStatus.Text = error.Message;
        }
    }

    private void RenderResults()
    {
        var result = session.Result;
        if (result == null) return;
        ResultCount.Text = $"{result.Regions.Count} vùng";
        var diagnostics = result.Diagnostics.Concat(result.Regions.SelectMany(r => r.Diagnostics)).Select(Describe).Distinct().ToArray();
        InputStatus.Text = diagnostics.Length > 0 ? string.Join("\n", diagnostics) : "Đã phân tích. Nội dung gốc được giữ nguyên.";
        changingUi = true;
        RegionBox.Items.Clear();
        foreach (var region in result.Regions)
        {
            var raw = region.OriginalContent.Replace("\r", " ").Replace("\n", " ");
            RegionBox.Items.Add(raw.Length > 60 ? raw[..60] + "…" : raw);
        }
        RegionBox.SelectedIndex = result.Regions.Count > 0 ? 0 : -1;
        RegionBox.Visibility = result.Regions.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        changingUi = false;
        ShowRegion();
        if (result.Regions.Count == 0) EmptyPreview.Text = "Chưa có công thức phù hợp";
    }

    private void RegionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || changingUi || session.Result == null) return;
        var index = RegionBox.SelectedIndex;
        if (index < 0 || index >= session.Result.Regions.Count) return;
        var direct = session.Result.Regions[index].Candidates.FirstOrDefault(c => c.Kind == "direct");
        // A repair requires the user to select its own card, including when changing regions.
        session.ClearSelection();
        if (direct != null) session.Select(direct.Id);
        ShowRegion();
    }

    private RenderOptions CurrentRenderOptions() => new(new[] { 24d, 32d, 48d, 64d }[Math.Clamp(FontBox.SelectedIndex, 0, 3)],
        Math.Clamp(ScaleBox.SelectedIndex + 1, 1, 3), WhiteBackground.IsChecked == true);

    private void ShowRegion()
    {
        scene = null; Preview.Source = null; ExportButtons.IsEnabled = false; TextExportButtons.IsEnabled = false;
        CandidatePanel.Children.Clear(); EmptyPreview.Visibility = Visibility.Visible; SelectionNote.Text = ""; SourcePreview.Text = "";
        var result = session.Result;
        var index = Math.Max(0, RegionBox.SelectedIndex);
        if (result == null || index >= result.Regions.Count) return;
        var region = result.Regions[index];
        foreach (var candidate in region.Candidates)
        {
            bool selected = candidate.Id == session.SelectedCandidate?.Id;
            string label = candidate.Kind switch { "direct" => "Đúng theo chuỗi đã nhập", "interpretation" => "Cách hiểu khác", _ => "Đề nghị sửa" };
            var panel = new DockPanel();
            panel.Children.Add(new TextBlock { Text = (selected ? "●  " : "○  ") + label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            FormulaScene? candidateScene = null;
            try
            {
                candidateScene = FormulaRenderer.Render(candidate, CurrentRenderOptions());
                panel.Children.Add(new Image { Source = candidateScene.Preview, MaxHeight = 42, MaxWidth = 180, MinWidth = 60, Margin = new Thickness(10, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Right, Stretch = Stretch.Uniform });
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException) { panel.Children.Add(new TextBlock { Text = error.Message, FontSize = 11 }); }
            var button = new Button { Content = panel, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Background = selected ? new SolidColorBrush(Color.FromRgb(231, 241, 231)) : Brushes.White, Margin = new Thickness(0, 0, 0, 7), Tag = candidate.Id };
            System.Windows.Automation.AutomationProperties.SetName(button, label + (selected ? ", đang chọn" : ", chọn kết quả"));
            button.Click += (_, _) => { if (session.Select(candidate.Id)) { ExportStatus.Text = ""; ShowRegion(); } };
            CandidatePanel.Children.Add(button);
            if (selected && candidateScene != null)
            {
                scene = candidateScene; Preview.Source = scene.Preview; EmptyPreview.Visibility = Visibility.Collapsed;
                SourcePreview.Text = candidate.Source.Slice(candidate.ReplacementSpan);
                SelectionNote.Text = candidate.Kind == "repair" ? "Đang chọn đề nghị sửa. Chuỗi đã gõ vẫn được giữ nguyên." : "Chọn kết quả rồi sao chép hoặc lưu ảnh.";
                if (candidate.Diagnostics.Count > 0) SelectionNote.Text += "\n" + string.Join("\n", candidate.Diagnostics.Select(Describe).Distinct());
                ExportButtons.IsEnabled = session.CanExport; TextExportButtons.IsEnabled = session.CanExport;
                ExportStatus.Text = $"Ảnh {Math.Ceiling(scene.Width * scene.Options.PixelScale):0} × {Math.Ceiling(scene.Height * scene.Options.PixelScale):0} px · SVG co giãn theo cỡ bạn cần.";
            }
        }
        if (scene == null) { EmptyPreview.Text = "Chọn một phương án để xem và xuất"; SelectionNote.Text = "Bản sửa chỉ được dùng khi bạn chọn rõ phương án đó."; }
    }

    private void RenderChanged(object sender, SelectionChangedEventArgs e) { if (ready) ShowRegion(); }
    private void BackgroundChanged(object sender, RoutedEventArgs e) { if (ready) ShowRegion(); }
    private async void CopyClick(object sender, RoutedEventArgs e) => await CopyAsync(Enum.Parse<CopyFormat>((string)((Button)sender).Tag));
    internal async Task CopyAsync(CopyFormat format)
    {
        var selected = session.SelectedCandidate; var currentScene = scene;
        if (selected == null || currentScene == null || !session.CanExport) return;
        try
        {
            await ClipboardService.CopyAsync(session, selected, currentScene, format, () => !closed && ReferenceEquals(scene, currentScene));
            if (!closed && ReferenceEquals(scene, currentScene)) ExportStatus.Text = format == CopyFormat.Svg
                ? "Đã copy SVG. Nếu nơi dán không nhận hình vector, dùng Lưu SVG hoặc Copy PNG." : "Đã sao chép " + format + ".";
        }
        catch (Exception error) when (error is System.Runtime.InteropServices.COMException or InvalidOperationException or System.Security.SecurityException)
        { if (!closed) ExportStatus.Text = "Chưa sao chép được: " + error.Message; }
    }

    private void SaveClick(object sender, RoutedEventArgs e)
        => SaveImage((string)((Button)sender).Tag);

    private void SaveImage(string extension)
    {
        var candidate = session.SelectedCandidate; var currentScene = scene; long revision = session.Revision;
        if (candidate == null || currentScene == null || !session.CanExport) return;
        var dialog = new SaveFileDialog { FileName = "cong-thuc." + extension, DefaultExt = "." + extension,
            Filter = extension == "svg" ? "Hình vector SVG (*.svg)|*.svg" : "Ảnh PNG (*.png)|*.png", AddExtension = true, OverwritePrompt = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            session.RequireCurrent(candidate.Id, revision);
            if (!ReferenceEquals(scene, currentScene)) throw new InvalidOperationException("Thiết lập xuất đã đổi.");
            ImageFileExporter.Save(dialog.FileName, currentScene, extension);
            ExportStatus.Text = "Đã lưu " + Path.GetFileName(dialog.FileName) + ".";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException) { ExportStatus.Text = "Chưa lưu được: " + error.Message; }
    }

    private void SampleClick(object sender, RoutedEventArgs e)
    {
        if (session.IsComposing) return;
        string value = (string)((Button)sender).Tag;
        if (value == "marker")
        {
            ModeBox.SelectedIndex = 2;
            Editor.Text = $"Ta có {OpenMarker.Text}x mũ 2{CloseMarker.Text} và {OpenMarker.Text}1 trên 2{CloseMarker.Text}.";
        }
        else { ModeBox.SelectedIndex = 0; Editor.Text = value; }
        Editor.Focus(); Editor.CaretIndex = Editor.Text.Length;
    }
    private async void HandleShortcut(object sender, KeyEventArgs e)
    {
        if (session.IsComposing) return;
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Enter) { e.Handled = true; debounce.Stop(); await AnalyzeNowAsync(); }
        else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.C) { e.Handled = true; await CopyAsync(CopyFormat.Png); }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.L) { e.Handled = true; Editor.Focus(); }
        else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.S) { e.Handled = true; await CopyAsync(CopyFormat.Svg); }
        else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.T) { e.Handled = true; await CopyAsync(CopyFormat.Latex); }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S) { e.Handled = true; SaveImage("svg"); }
        else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.P) { e.Handled = true; SaveImage("png"); }
        else if (Keyboard.Modifiers == ModifierKeys.Alt)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            int index = key == Key.D1 ? 0 : key == Key.D2 ? 1 : key == Key.D3 ? 2 : -1;
            if (index >= 0 && index < CandidatePanel.Children.Count && CandidatePanel.Children[index] is Button button)
            { e.Handled = true; button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
        }
    }
    private void SavePreferences()
    {
        if (!persistPreferences) return;
        try
        {
            var value = new Preferences(OpenMarker.Text, CloseMarker.Text, ModeBox.SelectedIndex, FontBox.SelectedIndex, ScaleBox.SelectedIndex, WhiteBackground.IsChecked == true);
            if (value.IsValid) value.Save(Preferences.DefaultPath);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { /* Preferences failure never discards the editor during use. */ }
    }
    private void WordStatusClick(object sender, RoutedEventArgs e) => MessageBox.Show(this,
        "Locus Desktop hoạt động độc lập. Bản alpha này chưa có connector Word. Bạn có thể copy ảnh hoặc văn bản để dùng trong ứng dụng khác.", "Kết nối Word", MessageBoxButton.OK, MessageBoxImage.Information);
    private static string Describe(Diagnostic diagnostic) => diagnostic.Code switch
    {
        "INSUFFICIENT_MATH_EVIDENCE" => "Chưa đủ dấu hiệu công thức. Thử chế độ cả vùng hoặc thêm dấu toán.",
        "UNCLOSED_MARKER" => "Cặp dấu chưa đóng. Nội dung đang gõ được giữ nguyên.",
        "INVALID_MARKER_CONFIGURATION" => "Cặp dấu chưa hợp lệ. Dùng hai dấu khác nhau, không bao hàm nhau.",
        "REGION_LENGTH_LIMIT" => "Một công thức vượt 4.096 ký tự; hãy tách nhỏ.",
        "REGION_COUNT_LIMIT" => "Đoạn văn có quá nhiều vùng công thức; hãy tách nhỏ.",
        _ => diagnostic.Message == diagnostic.Code ? "Chưa nhận ra công thức trong vùng này. Kiểm tra cú pháp và cặp dấu." : diagnostic.Message
    };
}
