using System.Globalization;
using System.Text;
using Locus.Application;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Locus.Editor;

public partial class PlotWorkspace
{
    [Parameter] public string Host { get; set; } = "browser";
    [Parameter] public IEditorWindow? DesktopWindow { get; set; }
    private PlotDocument Doc => Session.Document;
    private PlotAxes Axes => Doc.Axes ?? new();
    private ElementReference root;
    private IJSObjectReference? module, renderer;
    private DotNetObjectReference<PlotWorkspace>? reference;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? delay, saveDelay;
    private PlotResult? result;
    private bool disposed, composing, busy, running, queued, contextMenu, onlyMissing, onlySelected, autoSave = true;
    private int parameterRender;
    private string? selected;
    private string search = "", notice = "", draftStatus = "", stepParameter = "", batchValues = "";
    private byte[]? retained;
    private string retainedName = "";
    private PlotViewport? panStart;
    private readonly HashSet<string> invalidNumbers = [];
    private readonly HashSet<string> expandedReadings = [];
    private sealed record Startup(string? Document, bool AutoSave, string Message);
    private sealed record Saved(bool SavedOk, string Message);
    private List<PlotParameter> ActiveParameters => (result?.Curves.SelectMany(c => c.Parameters).Distinct(StringComparer.Ordinal) ?? [])
        .Select(name => (Doc.Parameters ?? []).FirstOrDefault(p => p.Name == name) ?? new(name)).ToList();
    private ICollection<PlotParameter> FilteredParameters => ActiveParameters.Where(p => p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) && (!onlyMissing || p.Value == null) && (!onlySelected || selected != null && ResultFor(selected)?.Parameters.Contains(p.Name) == true)).OrderByDescending(p => p.Pinned).ToList();
    private string UsedBy(string name) => "Dùng trong hàm " + string.Join(", ", Doc.Curves.Select((curve,index)=>(curve,index)).Where(item=>ResultFor(item.curve.Id)?.Parameters.Contains(name)==true).Select(item=>item.index+1));
    private bool CanExport => !busy && !composing && invalidNumbers.Count == 0 && result?.Version == Session.Version && Doc.Curves.Any(c => c.Visible && !string.IsNullOrWhiteSpace(c.Raw)) && result.Curves.Where(r => Doc.Curves.Any(c => c.Id == r.Id && c.Visible)).All(r => r.Problem == null && !r.Limited);
    private PlotCurveResult? ResultFor(string id) => result?.Curves.FirstOrDefault(c => c.Id == id);
    private static string N(double? value) => value?.ToString("R", CultureInfo.InvariantCulture) ?? "";
    private static bool TryNumber(ChangeEventArgs e, out double value) => double.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        module = await Js.InvokeAsync<IJSObjectReference>("import", "./_content/Locus.Editor/plot.js");
        reference = DotNetObjectReference.Create(this);
        var startup = await module.InvokeAsync<Startup>("start", Host);
        autoSave = startup.AutoSave; draftStatus = startup.Message;
        if (Session.Version == 0 && startup.Document != null)
        {
            if (DocumentCodec.Open(startup.Document).Document is PlotDocument plot) Session.Load(plot);
            else { retained = Encoding.UTF8.GetBytes(startup.Document); retainedName = "nhap-do-thi.locus"; notice = "Nháp chưa mở được. Đã giữ bản gốc để tải lại."; await module.InvokeVoidAsync("fork"); }
        }
        await module.InvokeVoidAsync("wire", root, reference);
        selected = Doc.Curves.FirstOrDefault()?.Id;
        if (DesktopWindow != null) DesktopWindow.PrepareClose = PrepareClose;
        QueueWork(0); StateHasChanged();
    }
    private void Apply(PlotDocument next)
    {
        if (composing) return;
        try { Session.Apply(next); notice = ""; Changed(); }
        catch (FormatException) { notice = "Giá trị không hợp lệ. Min phải nhỏ hơn Max; các số phải hữu hạn."; }
    }
    private void Changed(int ms = 120)
    {
        busy = true; contextMenu = false; QueueWork(ms); QueueSave();
    }
    private void QueueWork(int ms)
    {
        delay?.Cancel(); delay?.Dispose(); delay = new(); queued = true; busy = true;
        _ = AfterPause(ms, delay.Token);
    }
    private async Task AfterPause(int ms, CancellationToken token)
    {
        try { await Task.Delay(ms, token); if (!token.IsCancellationRequested && !disposed && !composing) { if (panStart == null && !sliderGesture) Session.EndGesture(); await Run(); } }
        catch (OperationCanceledException) { }
    }
    private async Task Run()
    {
        if (running || disposed || composing) return;
        running = true;
        try
        {
            while (queued && !disposed && !composing)
            {
                queued = false; var request = new PlotRequest(Doc, Session.Version);
                try
                {
                    var reply = await ((IPlotScheduler)Scheduler).PlotAsync(request, lifetime.Token);
                    if (!disposed && reply.Version == Session.Version)
                    {
                        result = reply; busy = false;
                        var names=reply.Curves.SelectMany(c=>c.Parameters).ToHashSet(StringComparer.Ordinal);
                        invalidNumbers.RemoveWhere(key=>!names.Contains(key.Split(':')[0]));
                    }
                }
                catch (Exception ex) when (ex is JSException or TimeoutException or OperationCanceledException or FormatException)
                {
                    if (!disposed && request.Version == Session.Version) { result = null; busy = false; notice = "Chưa dựng được đồ thị trong thời gian cho phép. Nguồn vẫn được giữ; thử thu hẹp miền hoặc Dựng lại."; }
                }
                if (!disposed) await InvokeAsync(StateHasChanged);
            }
        }
        finally { running = false; }
    }
    [JSInvokable] public Task SourceInput(string id, string raw, bool isComposing)
    {
        if (disposed) return Task.CompletedTask;
        var curve = Doc.Curves.FirstOrDefault(c => c.Id == id); if (curve == null) return Task.CompletedTask;
        bool current = result?.Version == Session.Version;
        Session.BeginGesture(); composing = isComposing; selected = id;
        Session.UpdateCurve(curve with { Raw = raw, AstSnapshot = null, InterpretedRaw = null, InterpretationKind = null });
        if (current && result != null)
        {
            var kept = result.Curves.Where(c => c.Id != id).ToArray();
            result = new(Session.Version, kept, PlotSvg.Render(Doc, kept));
        }
        else result = null;
        notice = ""; Changed(220); StateHasChanged(); return Task.CompletedTask;
    }
    private void AddCurve() { if (composing) return; Session.EndGesture(); try { Session.AddCurve(); selected = Doc.Curves[^1].Id; Changed(); } catch (FormatException ex) { notice = ex.Message; } }
    private void Sample(string raw) { Session.EndGesture(); try { Session.AddCurve(raw); selected = Doc.Curves[^1].Id; Changed(); } catch (FormatException ex) { notice = ex.Message; } }
    private void Remove(string id) { Session.EndGesture(); Apply(Doc with { Curves = Doc.Curves.Where(c => c.Id != id).ToArray() }); selected = Doc.Curves.FirstOrDefault()?.Id; }
    private void CurveChange(PlotCurve curve) { Session.EndGesture(); try { Session.UpdateCurve(curve); Changed(); } catch (FormatException) { notice = "Thuộc tính đường chưa hợp lệ."; } }
    private bool ShowReadings(PlotCurveResult value) => expandedReadings.Contains(value.Id) || value.SelectedReadingId == null && value.Readings.Length > 0;
    private void ToggleReadings(string id) { if (!expandedReadings.Add(id)) expandedReadings.Remove(id); }
    private static string ReadingLabel(string kind) => kind switch { "direct" => "Theo cú pháp đã gõ", "alternative" => "Cách hiểu khác", _ => "Đề nghị sửa" };
    private void ChooseReading(PlotCurve curve, PlotReading reading)
    {
        Session.EndGesture();
        var next = reading.Kind == "direct"
            ? curve with { InterpretedRaw = null, InterpretationKind = null, AstSnapshot = null }
            : curve with { InterpretedRaw = reading.Raw, InterpretationKind = reading.Kind, AstSnapshot = null };
        try
        {
            Session.UpdateCurve(next);
            notice = reading.Kind == "repair" ? "Đã chọn đề nghị sửa để vẽ; chuỗi bạn gõ vẫn được giữ nguyên." : "Đã đổi cách hiểu của hàm; chuỗi bạn gõ vẫn được giữ nguyên.";
            Changed(0);
        }
        catch (FormatException) { notice = "Cách hiểu này không còn khớp với nguồn. Hãy nhập lại hoặc dựng lại."; }
    }
    private void CurveNumber(PlotCurve curve, string field, ChangeEventArgs e)
    {
        double? value = null;
        if (!string.IsNullOrWhiteSpace(e.Value?.ToString())) { if (!TryNumber(e, out double parsed)) { notice = "Cần một số hữu hạn."; return; } value = parsed; }
        if (field == "width" && !value.HasValue) return;
        CurveChange(field == "width" ? curve with { Width = value!.Value } : field == "min" ? curve with { DomainMin = value } : curve with { DomainMax = value });
    }
    private void Parameter(PlotParameter value)
    {
        try { Session.UpdateParameter(value); notice = ""; Changed(60); } catch (FormatException) { notice = "Tham số chưa hợp lệ: Min < Max, bước > 0, giá trị hữu hạn."; }
    }
    private void ParameterNumber(PlotParameter parameter, string field, ChangeEventArgs e, bool slider = false)
    {
        if (!slider) Session.EndGesture();
        // Read the current record because range input events can arrive before the next render.
        var p = (Doc.Parameters ?? []).FirstOrDefault(p => p.Name == parameter.Name) ?? parameter;
        string key = p.Name + ":" + field;
        invalidNumbers.Remove(key);
        if (field == "value" && string.IsNullOrWhiteSpace(e.Value?.ToString())) { Parameter(p with { Value = null }); return; }
        if (!TryNumber(e, out double value)) { invalidNumbers.Add(key); notice = "Cần một số hữu hạn."; return; }
        var next = field switch { "min" => p with { Min = value }, "max" => p with { Max = value }, "step" => p with { Step = value }, _ => p with { Value = value } };
        if (next.Min >= next.Max || next.Step <= 0 || !double.IsFinite(next.Max-next.Min)) { invalidNumbers.Add(key); notice = "Min phải nhỏ hơn Max, bước phải dương. Chưa xuất hình khi ô số còn lỗi."; return; }
        Parameter(next);
    }
    private bool sliderGesture;
    private void AssignBatch()
    {
        try
        {
            var active = ActiveParameters.ToDictionary(p => p.Name, StringComparer.Ordinal);
            var values = (Doc.Parameters ?? []).ToDictionary(p => p.Name, StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in batchValues.Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var pair = item.Split('=', StringSplitOptions.TrimEntries);
                if (pair.Length != 2 || !active.TryGetValue(pair[0], out var p) || !names.Add(pair[0]) || !double.TryParse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value)) throw new FormatException();
                values[p.Name] = p with { Value = value };
            }
            if (names.Count == 0) throw new FormatException();
            Session.EndGesture(); Apply(Doc with { Parameters = values.Values.ToArray() });
            invalidNumbers.RemoveWhere(key=>names.Any(name=>key==name+":value"));parameterRender++;
        }
        catch (FormatException) { notice = "Nhập tên=giá trị, cách nhau bằng ; hoặc xuống dòng. Không có giá trị nào bị đổi khi nhóm chưa hợp lệ."; }
    }
    private void BeginParameterGesture() { if (!sliderGesture) { Session.EndGesture(); Session.BeginGesture(); sliderGesture = true; } }
    private void EndParameterGesture() { Session.EndGesture(); sliderGesture = false; QueueSave(); }
    private void ParameterKey(KeyboardEventArgs e) { if (e.Key == "Escape") CancelGesture(); else if (e.Key.StartsWith("Arrow", StringComparison.Ordinal) || e.Key is "Home" or "End" or "PageUp" or "PageDown") BeginParameterGesture(); }
    private void CancelGesture() { Session.EndGesture(true); sliderGesture = false; panStart = null; Changed(0); }
    private void History(bool forward)
    {
        if(composing)return;
        if(!forward&&invalidNumbers.Count>0){invalidNumbers.Clear();parameterRender++;notice="Đã trả các ô số chưa hợp lệ về giá trị trước khi sửa.";return;}
        if(Session.History(forward)){result=null;invalidNumbers.Clear();parameterRender++;Changed(0);}
    }
    [JSInvokable] public Task SelectCurve(string id){if(Doc.Curves.Any(c=>c.Id==id))selected=id;StateHasChanged();return Task.CompletedTask;}
    [JSInvokable] public Task Command(string command)
    {
        if (composing || disposed) return Task.CompletedTask;
        if (command == "undo") History(false); else if (command == "redo") History(true);
        else if (command == "escape") { if (sliderGesture || panStart != null) CancelGesture(); if(invalidNumbers.Count>0)History(false); contextMenu = false; }
        else if (command == "save") return Save();
        StateHasChanged(); return Task.CompletedTask;
    }
    [JSInvokable] public Task Pan(string phase, double dx, double dy)
    {
        if (composing || disposed) return Task.CompletedTask;
        if (phase == "start") { Session.EndGesture(); Session.BeginGesture(); panStart = Doc.Viewport; }
        else if (phase == "cancel") CancelGesture();
        else if (phase == "end") { Session.EndGesture(); panStart = null; QueueSave(); }
        else if (panStart is {} v)
        {
            double x = dx * (v.Right - v.Left), y = dy * (v.Top - v.Bottom);
            Apply(Doc with { Viewport = new(v.Left - x, v.Top + y, v.Right - x, v.Bottom + y) });
        }
        StateHasChanged(); return Task.CompletedTask;
    }
    private void Zoom(double factor)
    {
        Session.EndGesture(); var v = Doc.Viewport; double x = (v.Left + v.Right) / 2, y = (v.Top + v.Bottom) / 2, w = (v.Right - v.Left) * factor / 2, h = (v.Top - v.Bottom) * factor / 2;
        if (w < 1e-8 || h < 1e-8) return;
        Apply(Doc with { Viewport = new(x - w, y + h, x + w, y - h) });
    }
    private static string ViewLabel(string field) => field switch { "Left" => "Khung x nhỏ nhất", "Right" => "Khung x lớn nhất", "Bottom" => "Khung y nhỏ nhất", _ => "Khung y lớn nhất" };
    private double ViewValue(string field) => field switch { "Left" => Doc.Viewport.Left, "Right" => Doc.Viewport.Right, "Bottom" => Doc.Viewport.Bottom, _ => Doc.Viewport.Top };
    private void ViewportNumber(string field, ChangeEventArgs e)
    {
        if (!TryNumber(e, out var value)) { notice = "Cần một số hữu hạn."; return; } var v = Doc.Viewport;
        Apply(Doc with { Viewport = field switch { "Left" => v with { Left = value }, "Right" => v with { Right = value }, "Bottom" => v with { Bottom = value }, _ => v with { Top = value } } });
    }
    private void Refresh() => QueueWork(0);
    private async Task Save()
    {
        if (composing || invalidNumbers.Count > 0) { notice="Hoàn tất ô số đang sửa trước khi lưu."; return; } Session.EndGesture(); long version = Session.Version;
        var transfer = await Files.SaveAsync("do-thi.locus", "application/json", Encoding.UTF8.GetBytes(Session.Serialize()), () => !disposed && version == Session.Version && !composing);
        notice = TransferMessage(transfer); if (transfer.Status == TransferStatus.Completed && module != null) await module.InvokeVoidAsync("downloaded");
    }
    private static string TransferMessage(TransferResult t) => t.Message.Length > 0 ? t.Message : t.Status == TransferStatus.Completed ? "Đã chuyển dữ liệu." : t.Status == TransferStatus.Cancelled ? "Đã hủy; nội dung được giữ." : "Chưa chuyển được. Bạn có thể thử nút Tải SVG/PNG.";
    private async Task Open(InputFileChangeEventArgs e)
    {
        if (composing) return; var version = Session.Version;
        try
        {
            using var stream = e.File.OpenReadStream(DocumentCodec.MaxBytes); using var bytes = new MemoryStream(); await stream.CopyToAsync(bytes);
            var content = bytes.ToArray(); var opened = DocumentCodec.Open(new UTF8Encoding(false, true).GetString(content));
            if (version != Session.Version || composing) { notice = "Nội dung đã đổi trong lúc mở tệp. Hãy chọn lại tệp."; return; }
            if (opened.Document is not PlotDocument plot) { retained = content; retainedName = Path.GetFileName(e.File.Name); notice = "Tệp này chưa dùng được trong tab Đồ thị. Phiên hiện tại và tệp gốc được giữ."; return; }
            await Persist(); if (version != Session.Version || composing) { notice="Nội dung vừa đổi. Hãy chọn lại tệp."; return; } Session.Load(plot); selected = Doc.Curves.FirstOrDefault()?.Id; retained = null; result = null; invalidNumbers.Clear();search="";onlyMissing=false;onlySelected=false;batchValues="";stepParameter="";parameterRender++;
            if (module != null) await module.InvokeVoidAsync("fork"); Changed(0); notice = "Đã mở đồ thị.";
        }
        catch (Exception ex) when (ex is IOException or FormatException or DecoderFallbackException) { notice = "Chưa mở được tệp. Phiên hiện tại được giữ."; }
    }
    private Task<TransferResult> Recover() => Files.SaveAsync(retainedName, "application/octet-stream", retained!, () => !disposed);
    private async Task Export(string format, bool copy = false)
    {
        if (!CanExport || result == null) return; long version = Session.Version; string svg = result.Svg;
        bool Current() => !disposed && CanExport && Session.Version == version;
        try
        {
            byte[] bytes;
            if (format == "png") { renderer ??= await Js.InvokeAsync<IJSObjectReference>("import", "./_content/Locus.Editor/renderer.js"); bytes = await renderer.InvokeAsync<byte[]>("png", svg, 2); }
            else bytes = Encoding.UTF8.GetBytes(svg);
            if (!Current()) return;
            var transfer = copy ? format == "png" ? await Clipboard.WritePngAsync(bytes, Current) : await Clipboard.WriteSvgAsync(svg, Current) : await Files.SaveAsync("do-thi." + format, format == "png" ? "image/png" : "image/svg+xml", bytes, Current);
            if (Current()) notice = TransferMessage(transfer); contextMenu = false;
        }
        catch (JSException) { notice = "Chưa xuất được ảnh. Nguồn vẫn được giữ; thử Tải SVG."; }
    }
    private void QueueSave()
    {
        saveDelay?.Cancel(); saveDelay?.Dispose(); saveDelay = new();
        if (module != null) _ = module.InvokeVoidAsync("dirty");
        _ = SaveAfterPause(saveDelay.Token);
    }
    private async Task SaveAfterPause(CancellationToken token)
    {
        try { await Task.Delay(350, token); if (!disposed && !token.IsCancellationRequested) { await Persist(); await InvokeAsync(StateHasChanged); } } catch (OperationCanceledException) { }
    }
    private async Task<bool> Persist()
    {
        if (module == null) return false;
        if (!autoSave) { draftStatus = "Tự lưu đang tắt. Hãy lưu .locus trước khi đóng."; return false; }
        long version = Session.Version;
        try { var saved = await module.InvokeAsync<Saved>("save", Session.Serialize()); if (version == Session.Version) draftStatus = saved.Message; return saved.SavedOk && version == Session.Version; }
        catch (Exception ex) when (ex is JSException or FormatException) { draftStatus = "Chưa lưu được nháp; hãy lưu .locus."; return false; }
    }
    private async Task AutoSaveChanged(ChangeEventArgs e) { autoSave = e.Value is true; if (module != null) await module.InvokeVoidAsync("configure", autoSave); await Persist(); }
    private async Task<DesktopClosePreparation> PrepareClose(bool exit)
    {
        DesktopClosePreparation preparation=new(false);
        await InvokeAsync(async()=>
        {
            if(module==null||disposed)return;
            try
            {
                if(!await CanLeaveAsync())return;
                long version=Session.Version;bool saved=await Persist();await module.InvokeVoidAsync("settle",root);
                if(version!=Session.Version||composing||invalidNumbers.Count>0||sliderGesture||panStart!=null)return;
                preparation=new(saved||!exit||!autoSave,exit&&!autoSave);
            }
            catch(Exception ex)when(ex is JSException or InvalidOperationException){notice="Chưa lưu xong phiên. Cửa sổ vẫn được giữ.";}
            StateHasChanged();
        });
        return preparation;
    }
    public async Task<bool> CanLeaveAsync()
    {
        if(module==null||disposed)return false;
        await module.InvokeVoidAsync("settle",root);
        if(composing||invalidNumbers.Count>0||sliderGesture||panStart!=null){notice="Hoàn tất ô nhập hoặc thả/hủy thao tác kéo trước khi đổi tab.";StateHasChanged();return false;}
        return true;
    }
    public async ValueTask DisposeAsync()
    {
        delay?.Cancel(); saveDelay?.Cancel(); lifetime.Cancel();
        if (module != null) { await module.InvokeVoidAsync("settle", root); await Persist(); await module.InvokeVoidAsync("unwire", root); }
        disposed = true; Session.EndGesture(); reference?.Dispose();
        if (DesktopWindow?.PrepareClose == PrepareClose) DesktopWindow.PrepareClose = null;
        if (module != null) await module.DisposeAsync(); if (renderer != null) await renderer.DisposeAsync();
        delay?.Dispose(); saveDelay?.Dispose(); lifetime.Dispose();
    }
}
