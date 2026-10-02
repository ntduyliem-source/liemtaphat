using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Locus.Core;
using Locus.Desktop.Rendering;

namespace Locus.Word;

internal sealed class ScanPanel : Form
{
    private readonly Label status = new() { Dock = DockStyle.Top, Height = 70, Padding = new Padding(10), AutoEllipsis = true };
    private readonly ListBox rows = new() { Dock = DockStyle.Fill, Name = "scanRegions", IntegralHeight = false, HorizontalScrollbar = true };
    private readonly TextBox source = new() { Dock = DockStyle.Top, Height = 66, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Name = "scanSource" };
    private readonly PictureBox preview = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White, Padding = new Padding(10) };
    private readonly ComboBox readings = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 245, Name = "scanReadings" };
    private readonly Button choose = new() { Text = "Dùng cách hiểu này", AutoSize = true, Name = "scanChoose" };
    private readonly Button convert = new() { Text = "Chuyển vùng này", AutoSize = true, Name = "scanConvert" };
    private readonly Button batch = new() { AutoSize = true, Name = "scanConvertAll" };
    private readonly Button restore = new() { Text = "Về text, giữ nhận diện", AutoSize = true, Name = "scanRestore" };
    private readonly Button ignore = new() { Text = "Giữ text, bỏ qua", AutoSize = true, Name = "scanIgnore" };
    private readonly Button include = new() { Text = "Cho phép chuyển lại", AutoSize = true, Name = "scanInclude" };
    private readonly Button go = new() { Text = "Đi tới vùng / fx", AutoSize = true, Name = "scanGo" };
    private readonly CheckBox inline = new() { Text = "Thử dấu fx cạnh vùng", AutoSize = true, Name = "scanInline" };
    private readonly Label detail = new() { Dock = DockStyle.Bottom, Height = 62, Padding = new Padding(8) };
    private ScanSession? session;
    private bool updating, stale;
    public string RegionId => rows.SelectedItem is Row row ? row.Region.Id : "";
    public string PreviewCandidateId { get; private set; } = "";
    public bool InlineEnabled => inline.Checked;
    public event Action<string, string, string>? Requested;
    public event Action? Rescan;
    public event Action? Cancelled;
    public ScanPanel()
    {
        Text = "Locus — Công thức trong tài liệu"; Name = "LocusScanPanel"; Size = new Size(980, 670); MinimumSize = new Size(780, 570);
        Font = new Font("Segoe UI", 10); AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi; StartPosition = FormStartPosition.CenterParent;
        var split = new SplitContainer { Dock = DockStyle.Fill, Width = 950, SplitterDistance = 320, Panel1MinSize = 210, Panel2MinSize = 420 };
        split.Panel1.Controls.Add(rows); split.Panel2.Controls.Add(preview); split.Panel2.Controls.Add(source); split.Panel2.Controls.Add(detail);
        var choices = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(6) };
        choices.Controls.Add(readings); choices.Controls.Add(choose); split.Panel2.Controls.Add(choices);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
        foreach (var button in new[] { convert, batch, restore, ignore, include, go }) actions.Controls.Add(button);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
        var again = new Button { Text = "Quét lại phạm vi này", AutoSize = true, Name = "scanAgain" };
        var close = new Button { Text = "Đóng", AutoSize = true, DialogResult = DialogResult.Cancel };
        footer.Controls.Add(again); footer.Controls.Add(inline); footer.Controls.Add(close);
        Controls.Add(split); Controls.Add(status); Controls.Add(actions); Controls.Add(footer);
        CancelButton = close; AcceptButton = null;
        rows.SelectedIndexChanged += (_, _) => RenderRow(); readings.SelectedIndexChanged += (_, _) => { if (!updating) RenderCandidate(); };
        choose.Click += (_, _) => Request("choose", readings.SelectedItem is Reading item ? item.Candidate.Id : "");
        convert.Click += (_, _) => Request("convert"); batch.Click += (_, _) => Request("convert-all");
        restore.Click += (_, _) => Request("restore"); ignore.Click += (_, _) => Request("ignore"); include.Click += (_, _) => Request("include"); go.Click += (_, _) => Request("go");
        inline.CheckedChanged += (_, _) => Request("inline"); again.Click += (_, _) => Rescan?.Invoke(); close.Click += (_, _) => Close();
        FormClosed += (_, _) => { preview.Image?.Dispose(); Cancelled?.Invoke(); };
    }
    private void Request(string action, string argument = "") { if (session != null && (!stale || action == "inline")) Requested?.Invoke(action, RegionId, argument); }
    public void Present(ScanSession value, string selected = "", string message = "")
    {
        session = value; stale = false; updating = true; rows.Items.Clear();
        foreach (var region in value.Regions) rows.Items.Add(new Row(region));
        int index = value.Regions.ToList().FindIndex(r => r.Id == selected); rows.SelectedIndex = rows.Items.Count == 0 ? -1 : Math.Max(0, index); updating = false;
        status.Text = (value.WholeBody ? "Toàn thân tài liệu" : "Trong vùng đã chọn") + " · " + value.Regions.Count + " vùng. " + message + "\r\n" +
            "Ngoài bảng · tối đa 64 vùng. Quét chỉ đọc; dấu fx trực tiếp đang thử nghiệm. " + value.Notice;
        RenderRow();
    }
    private void RenderRow()
    {
        if (updating) return;
        var region = session?.Regions.SingleOrDefault(r => r.Id == RegionId);
        updating = true; readings.Items.Clear();
        source.Text = region?.Source ?? "Không tìm thấy công thức trong phạm vi này.";
        if (region?.Choice?.Formula.Readings is {} set)
        {
            foreach (var c in set.Candidates) readings.Items.Add(new Reading(c));
            readings.SelectedIndex = set.Candidates.ToList().FindIndex(c => c.Id == region.Choice.Formula.Selected?.Id);
        }
        updating = false;
        RenderCandidate();
    }
    private void RenderCandidate()
    {
        PreviewCandidateId = "";
        var region = session?.Regions.SingleOrDefault(r => r.Id == RegionId); var choice = region?.Choice;
        Candidate? candidate = readings.SelectedItem is Reading reading ? reading.Candidate : choice?.Formula.Selected;
        var previous = preview.Image; preview.Image = null; previous?.Dispose();
        try
        {
            if (candidate != null)
            {
                var scene = FormulaRenderer.Render(candidate, new RenderOptions(36, 2, true));
                using var stream = new MemoryStream(scene.ToPng()); using var image = Image.FromStream(stream); preview.Image = new Bitmap(image);
                PreviewCandidateId = scene.CandidateId;
            }
        }
        catch { detail.Text = "Không dựng được preview. Vùng này chưa thể chuyển."; SetButtons(false); return; }
        detail.Text = region?.Problem.Length > 0 ? "Vùng đã bị sửa hoặc metadata không hợp lệ. Locus giữ nội dung hiện tại." :
            (region?.Status ?? "") + (candidate?.Kind == "repair" ? "\r\nĐề nghị sửa; chỉ đưa vào nhóm sau khi bấm Dùng cách hiểu này." : "\r\nCân bằng và suy sản phẩm không tự áp dụng khi chuyển nhóm.");
        SetButtons(!stale);
    }
    private void SetButtons(bool available)
    {
        var region = session?.Regions.SingleOrDefault(r => r.Id == RegionId); var choice = region?.Choice;
        bool row = available && choice != null && region!.Problem.Length == 0 && PreviewCandidateId.Length != 0;
        choose.Enabled = row && !choice!.Native && readings.Items.Count > 0;
        convert.Enabled = row && choice!.Ready && PreviewCandidateId == choice.Formula.Selected?.Id;
        int count = session?.Regions.Count(r => r.Problem.Length == 0 && r.Choice?.Ready == true) ?? 0;
        batch.Text = "Chuyển " + count + " vùng đã nhận diện"; batch.Enabled = available && count > 0 && (choice == null || PreviewCandidateId == choice.Formula.Selected?.Id);
        restore.Enabled = row && choice!.Native; ignore.Enabled = row && !choice!.Ignored;
        include.Enabled = row && choice!.Ignored; go.Enabled = available && region != null;
        readings.Enabled = row && !choice!.Native;
    }
    public void Report(string message, bool invalid)
    { status.Text = message; stale = invalid; SetButtons(!invalid); }
    public void SelectRegion(string id)
    {
        int index = session?.Regions.ToList().FindIndex(r => r.Id == id) ?? -1;
        if (index < 0) throw new InvalidOperationException("scan-stale");
        rows.SelectedIndex = index;
    }
    public void PreviewReading(string id)
    {
        int index = readings.Items.Cast<Reading>().ToList().FindIndex(r => r.Candidate.Id == id);
        if (index < 0) throw new InvalidOperationException("candidate-changed");
        readings.SelectedIndex = index;
    }
    private sealed class Row
    {
        public ScanRegion Region { get; }
        public Row(ScanRegion region) { Region = region; }
        public override string ToString() => "fx · " + Region.Status + " — " + Region.Source;
    }
    private sealed class Reading
    {
        public Candidate Candidate { get; }
        public Reading(Candidate candidate) { Candidate = candidate; }
        public override string ToString() => Candidate.Kind == "direct" ? "Theo cú pháp đã gõ" : Candidate.Kind == "repair" ? "Đề nghị sửa" : "Cách hiểu khác";
    }
}
