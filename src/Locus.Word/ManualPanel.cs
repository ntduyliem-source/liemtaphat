using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using Locus.Core;
using Locus.Core.Assistance;
using Locus.Core.Detection;
using Locus.Desktop.Rendering;

namespace Locus.Word;

internal sealed class ManualPanel : Form
{
    private readonly Label status = new() { Dock = DockStyle.Top, Height = 62, Padding = new Padding(12, 8, 12, 4) };
    private readonly TextBox source = new() { Dock = DockStyle.Top, Height = 54, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly Label kind = new() { Dock = DockStyle.Top, Height = 42, Padding = new Padding(12, 6, 12, 0) };
    private readonly FlowLayoutPanel choices = new() { Dock = DockStyle.Bottom, AutoSize = true, MinimumSize = new Size(0, 38), Padding = new Padding(8, 0, 8, 4) };
    private readonly FlowLayoutPanel actions = new() { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
    private readonly FlowLayoutPanel assistance = new() { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8), MaximumSize = new Size(0, 230), AutoScroll = true };
    private readonly FormulaPicture preview = new() { Dock = DockStyle.Fill, BackColor = Color.White };
    private readonly Button confirm = new() { Text = "&Chuyển thành công thức", AutoSize = true, Name = "confirmFormula", Visible = false };
    private readonly Button restore = new() { Text = "&Khôi phục nguồn", AutoSize = true, Name = "restoreSource", Visible = false };
    private readonly Button detach = new() { Text = "&Tách quản lý", AutoSize = true, Name = "detachFormula", Visible = false };
    private readonly Button close = new() { Text = "Đó&ng", AutoSize = true, DialogResult = DialogResult.Cancel };
    private CandidateSet? candidates;
    private WordDetectionSettings settings = WordDetectionSettings.Default;
    private readonly Dictionary<string, string> conditions = new();
    private AssistanceProposal? proposed;
    private IReadOnlyList<AssistanceProposal> proposals = Array.Empty<AssistanceProposal>();
    private string mode = "source";
    public ManualFormulaState? Formula { get; private set; }
    public string PreviewIdentity { get; private set; } = "";
    public IReadOnlyList<AssistanceProposal> Proposals => proposals;
    private string sessionId = "";
    public string CandidateId { get; private set; } = "";
    public string PreviewCandidateId { get; private set; } = "";
    public event Action<string, string, string>? Requested;
    public event Action? Cancelled;
    public ManualPanel()
    {
        Text = "Locus — Công thức Word"; Name = "LocusManualPanel";
        Font = new Font("Segoe UI", 10); Width = 780; Height = 620; MinimumSize = new Size(620, 520);
        StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = true; AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        Controls.Add(preview); Controls.Add(kind); Controls.Add(source); Controls.Add(status); Controls.Add(choices); Controls.Add(assistance); Controls.Add(actions);
        actions.Controls.Add(confirm); actions.Controls.Add(restore); actions.Controls.Add(detach); actions.Controls.Add(close);
        CancelButton = close;
        confirm.Click += (_, _) => Request(mode == "source" ? "convert" : "replace"); restore.Click += (_, _) => Request("restore"); detach.Click += (_, _) => Request("detach");
        close.Click += (_, _) => Close(); FormClosed += (_, _) => { preview.Image?.Dispose(); Cancelled?.Invoke(); };
    }
    public void Present(CandidateSet value, string mode, string id)
        => Present(ManualFormulaState.FromReadings(value), mode, id, WordDetectionSettings.Default);
    public void Present(ManualFormulaState state, string mode, string id, WordDetectionSettings settings)
    {
        this.mode = mode; this.settings = settings; Formula = state; conditions.Clear(); proposed = null;
        candidates = state.Readings; sessionId = id; source.Text = state.OriginalSource;
        status.Text = mode == "source" ? "Xem trước vùng đã chọn. Chọn đúng cách hiểu rồi xác nhận chuyển." : "Công thức đang được Locus quản lý. Nguồn và các cách hiểu được lấy từ lần lưu trước.";
        foreach (Control child in choices.Controls.Cast<Control>().ToArray()) child.Dispose(); choices.Controls.Clear();
        confirm.Enabled = restore.Enabled = detach.Enabled = choices.Enabled = true;
        RenderState();
        var alternatives = new Button { Text = "fx — Xem các phương án", AutoSize = true, Enabled = candidates?.Candidates.Count > 1, Name = "showCandidates" };
        alternatives.Click += (_, _) =>
        {
            alternatives.Visible = false;
            foreach (var candidate in candidates!.Candidates)
            {
                var button = new Button { AutoSize = true, Text = (candidates.Candidates.ToList().IndexOf(candidate) + 1) + ". " + Kind(candidate), Tag = candidate.Id };
                button.Click += (_, _) => { try { SelectCandidate((string)button.Tag); } catch { /* SelectCandidate has cleared the stale image and disabled confirmation. */ } };
                choices.Controls.Add(button);
            }
        };
        choices.Controls.Add(alternatives);
        confirm.Visible = true; restore.Visible = detach.Visible = mode == "managed";
        RefreshAssistance();
        actions.Enabled = true; AcceptButton = null; // Enter must not implicitly choose a repair or restore.
    }
    public void SelectCandidate(string id)
    {
        Formula = Formula!.SelectReading(id); proposed = null; RenderState(); RefreshAssistance();
    }
    private void RenderState()
    {
        var candidate = proposed?.Result.Candidates[0] ?? Formula!.Selected;
        if (candidate == null)
        {
            CandidateId = PreviewCandidateId = ""; PreviewIdentity = Formula!.Identity;
            var previous = preview.Image; preview.Image = null; previous?.Dispose();
            kind.Text = "Vế đầu chưa có sản phẩm. Chọn điều kiện và xem đề xuất trước khi nhận.";
            confirm.Enabled = false; return;
        }
        RenderCandidate(candidate);
        PreviewIdentity = proposed == null ? Formula!.Identity : "proposal:" + proposed.Id;
        confirm.Enabled = proposed == null;
        if (proposed != null) kind.Text = "Đề xuất sản phẩm và cân bằng — chưa nhận.\r\n" + string.Join("; ", proposed.Context.Conditions.Select(f => {
            var definition = ReactionCatalog.ConditionDefinitions.Single(d => d.Key == f.Key);
            return definition.Label + ": " + definition.Options.Single(o => o.Value == f.Value).Label;
        }));
        else if (Formula!.CanCancelBalance) kind.Text = "Đã cân bằng trong bản xem trước. Có thể hủy để lấy lại đúng hệ số trước đó.";
        else if (Formula.Products != null) kind.Text = "Đã nhận sản phẩm; giữ hệ số trước cân bằng.";
        if (mode == "managed") confirm.Text = "&Cập nhật công thức";
    }
    private void RenderCandidate(Candidate candidate)
    {
        try {
        var scene = FormulaRenderer.Render(candidate, new RenderOptions(36, 2, true));
        using var stream = new System.IO.MemoryStream(scene.ToPng());
        using var loaded = Image.FromStream(stream);
        var previous = preview.Image; preview.Image = new Bitmap(loaded); previous?.Dispose();
        CandidateId = candidate.Id; PreviewCandidateId = scene.CandidateId;
        kind.Text = Kind(candidate) + (candidate.Kind == "repair" ? " — thay đổi so với nguồn; chỉ áp dụng khi bạn xác nhận." : "") +
            (candidate.Diagnostics.Count > 0 ? "\r\n" + string.Join("; ", candidate.Diagnostics.Select(d => d.Message)) : "");
        confirm.Text = candidate.Kind == "repair" ? "Áp dụng &đề nghị sửa" : "&Chuyển thành công thức";
        } catch {
            CandidateId = PreviewCandidateId = "";
            var previous = preview.Image; preview.Image = null; previous?.Dispose();
            kind.Text = ""; Report("Không thể dựng phương án này. Đóng bảng và chọn một công thức ngắn hơn.", true);
            throw;
        }
    }
    public void AssistanceAction(string action, string argument = "")
    {
        if (Formula == null || !assistance.Enabled) throw new InvalidOperationException("preview-expired");
        if (action == "condition")
        {
            if (Formula.Draft == null || Formula.Products != null) throw new InvalidOperationException("No pending products.");
            int split = argument.IndexOf('='); if (split < 1) throw new ArgumentException("Invalid condition.");
            string key = argument.Substring(0, split), value = argument.Substring(split + 1);
            var definition = ReactionCatalog.ConditionDefinitions.Single(d => d.Key == key);
            if (value.Length == 0) conditions.Remove(key);
            else { if (!definition.Options.Any(o => o.Value == value)) throw new ArgumentException("Invalid condition choice."); conditions[key] = value; }
            proposed = null;
        }
        else if (action == "preview-product") proposed = proposals.Single(p => p.Id == argument);
        else if (action == "accept-product")
        {
            if (proposed == null || proposed.Id != argument || PreviewIdentity != "proposal:" + proposed.Id || PreviewCandidateId != proposed.Result.Candidates[0].Id)
                throw new InvalidOperationException("candidate-changed");
            Formula = Formula.AcceptProducts(proposed); proposed = null;
        }
        else if (action == "dismiss-product") proposed = null;
        else if (action == "drop-products") { Formula = Formula.DropProducts(); proposed = null; }
        else if (action == "balance") { Formula = Formula.Balance(out string message); status.Text = message; }
        else if (action == "cancel-balance") Formula = Formula.CancelBalance();
        else throw new InvalidOperationException("invalid-assistance-action");
        RenderState(); RefreshAssistance();
    }
    private void RefreshAssistance()
    {
        foreach (Control control in assistance.Controls.Cast<Control>().ToArray()) control.Dispose(); assistance.Controls.Clear();
        proposals = Array.Empty<AssistanceProposal>();
        if (Formula == null) return;
        void Add(string text, string name, Action action, bool enabled = true)
        {
            var button = new Button { Text = text, Name = name, AutoSize = true, Enabled = enabled };
            button.Click += (_, _) => { try { action(); } catch (Exception error) { Report(error.Message, false); } };
            assistance.Controls.Add(button);
        }
        bool eligible = Formula.Selected?.Document.Root.Type == "ChemReaction" && Formula.Selected.Kind == "direct" &&
            !(Formula.Selected.Diagnostics.Concat((Formula.Result ?? Formula.Readings)!.Diagnostics).Any(d => d.Severity == "warning" || d.Severity == "error"));
        Add(Formula.CanCancelBalance ? "Hủy cân bằng" : "✦ Cân bằng", "balanceFormula", () => AssistanceAction(Formula.CanCancelBalance ? "cancel-balance" : "balance"), proposed == null && eligible);
        if (Formula.Products != null) Add("Hủy bổ sung sản phẩm", "dropProducts", () => AssistanceAction("drop-products"));
        if (Formula.Draft == null || Formula.Products != null) return;
        var result = ChemistryAssistance.Analyze(Formula.Draft.Region, new AssistanceContext(settings.Fingerprint, conditions.Select(f => new ConditionFact(f.Key, f.Value))), DetectionDomains.Chemistry);
        proposals = result.Proposals;
        assistance.Controls.Add(new Label { Text = result.Message, AutoSize = true, MaximumSize = new Size(700, 0) });
        foreach (var definition in result.ConditionChoices)
        {
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, MinimumSize = new Size(0, 36) };
            row.Controls.Add(new Label { Text = definition.Label, AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
            var combo = new ComboBox { Width = 330, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = definition.Label };
            combo.Items.Add("Chưa chọn"); foreach (var option in definition.Options) combo.Items.Add(option.Label);
            combo.SelectedIndex = conditions.TryGetValue(definition.Key, out string value) ? definition.Options.ToList().FindIndex(o => o.Value == value) + 1 : 0;
            combo.SelectedIndexChanged += (_, _) => AssistanceAction("condition", definition.Key + "=" + (combo.SelectedIndex == 0 ? "" : definition.Options[combo.SelectedIndex - 1].Value));
            row.Controls.Add(combo); assistance.Controls.Add(row);
        }
        foreach (var proposal in proposals) Add("Xem sản phẩm " + (proposals.ToList().IndexOf(proposal) + 1), "previewProduct", () => AssistanceAction("preview-product", proposal.Id));
        if (proposed != null)
        {
            Add("Nhận sản phẩm và cân bằng", "acceptProduct", () => AssistanceAction("accept-product", proposed.Id));
            Add("Bỏ đề xuất", "dismissProduct", () => AssistanceAction("dismiss-product"));
            assistance.Controls.Add(new Label { Text = "Nguồn tham khảo: " + string.Join("; ", proposed.Provenance.References), AutoSize = true, MaximumSize = new Size(700, 0) });
        }
    }
    private static string Kind(Candidate candidate) => candidate.Kind == "direct" ? "Theo cú pháp đã gõ" : candidate.Kind == "repair" ? "Đề nghị sửa" : "Cách hiểu khác";
    private void Request(string action)
    { if (sessionId.Length != 0) Requested?.Invoke(sessionId, action, CandidateId); }
    public void Report(string message, bool invalidate)
    { status.Text = message; if (invalidate) { confirm.Enabled = restore.Enabled = detach.Enabled = false; choices.Enabled = assistance.Enabled = false; PreviewIdentity = ""; } }

    private sealed class FormulaPicture : PictureBox
    {
        protected override void OnPaint(PaintEventArgs pe)
        {
            if (Image == null) return;
            float scale = Math.Min(DeviceDpi / 192f, Math.Min((ClientSize.Width - 24f) / Image.Width, (ClientSize.Height - 24f) / Image.Height));
            if (scale <= 0) return;
            float width = Image.Width * scale, height = Image.Height * scale;
            pe.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            pe.Graphics.DrawImage(Image, (ClientSize.Width - width) / 2, (ClientSize.Height - height) / 2, width, height);
        }
    }
}
