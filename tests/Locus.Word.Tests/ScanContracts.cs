using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Locus.Core;
using Locus.Core.Export;
using Locus.Word;

internal static partial class Program
{
    private static int RunScanContracts(string[] args)
    {
        directory = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/phase-f/contracts"); Directory.CreateDirectory(directory);
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        Test("scan/source-offsets-and-domains", () => {
            string raw = "Trước 👩‍🏫 e\u0301 lc[x mu\u0303 2]; ly-[v=10 m/s]; hoa-[3H2+O2=H2O]. Sau.";
            var found = ScanAnalysis.Analyze(raw, smartSettings);
            Check(found.Count == 3, "Expected three explicit regions.");
            Check(found.Select(r => r.Item2.Selected!.Document.Domain).SequenceEqual(new[] { "math", "physics", "chemistry" }), "Domain mismatch.");
            foreach (var r in found) Check(raw.Substring(r.Item1.Start, r.Item1.Length) == r.Item2.OriginalSource && !r.Item2.CanCancelBalance, "Rebase lost raw or applied balance.");
        });
        Test("scan/opaque-prose-and-duplicates", () => {
            Check(ScanAnalysis.Analyze("Câu văn thông thường. https://example.org/x a@example.org C:\\temp\\a.txt", smartSettings).Count == 0, "Prose was admitted.");
            var found = ScanAnalysis.Analyze("lc[x^2] và lc[x^2]", smartSettings);
            Check(found.Count == 2 && found[0].Item1.Start != found[1].Item1.Start && found[0].Item2.Selected!.Id == found[1].Item2.Selected!.Id, "Repeated formula was deduplicated or changed.");
        });
        Test("scan/eligibility-and-explicit-repair", () => {
            var direct = new ScanChoice(Smart("lc[x^2]")); Check(direct.Ready, "Direct formula should be ready.");
            Check(!new ScanChoice(direct.Formula, ignored: true).Ready && !new ScanChoice(direct.Formula, native: true).Ready, "Ignored/native was admitted.");
            var ambiguous = new ScanChoice(Smart("lc[x+1/2]"));
            Check(ScanAnalysis.Analyze("lc[x+1/2]\r", smartSettings).Count == 1, "Passive marker with repair disappeared.");
            var repair = ambiguous.Formula.Readings!.Candidates.First(c => c.Kind == "repair");
            Check(ambiguous.Formula.Selected!.Kind == "direct", "Repair became default.");
            ambiguous.Choose(repair.Id); Check(ambiguous.Confirmed && ambiguous.Ready && ambiguous.Formula.Selected!.Id == repair.Id, "Explicit repair choice lost.");
            Reject(() => new ScanChoice(direct.Formula, native: true).Choose(direct.Formula.Selected!.Id));
            var multiple = new ScanChoice(Smart("lc[1/2x]")); Check(!multiple.Ready, "Ambiguous formula entered default batch.");
            multiple.Choose(multiple.Formula.Readings!.Candidates.Last().Id); Check(multiple.Ready, "Confirmed interpretation not eligible.");
        });
        Test("scan/text-decision-roundtrip-and-id", () => {
            var snapshot = new ManagedSnapshot(Smart("hoa-[3H2+O2=H2O]"));
            foreach (bool ignored in new[] { false, true }) foreach (bool confirmed in new[] { false, true })
            {
                string tag = new ScanTextSnapshot(snapshot, ignored, confirmed).Encode(); var result = ScanTextSnapshot.Decode(tag);
                Check(result.Ignored == ignored && result.Confirmed == confirmed && result.Snapshot.Encode() == snapshot.Encode(), "Decision roundtrip differs.");
                Check(ScanTextSnapshot.HasEntry(tag, snapshot.EntryId) && ScanTextSnapshot.HasEntry(snapshot.Encode(), snapshot.EntryId), "Cross-kind duplicate detection failed.");
                Check(CandidateExporter.ToOmml(result.Snapshot.Selected) == CandidateExporter.ToOmml(snapshot.Selected), "Stored projection differs.");
            }
        });
        Test("scan/text-corruption-and-budget", () => {
            string tag = new ScanTextSnapshot(new ManagedSnapshot(Smart("lc[x^2]")), true, false).Encode();
            Reject(() => ScanTextSnapshot.Decode(tag.Substring(0, tag.Length - 3) + "BAD"));
            Reject(() => ScanTextSnapshot.Decode(tag.Replace("locus:text:1:", "locus:text:9:")));
            Reject(() => ScanTextSnapshot.Decode(new string('x', ManagedSnapshot.MaxTagCodeUnits + 1)));
            Check(!ScanTextSnapshot.HasEntry(tag.Substring(0, tag.Length - 3) + "BAD", "invalid"), "Corrupt tag was trusted.");
        });
        Test("scan/badge-keeps-first-anchor", () => {
            using var badge = new ProbeBadge();
            var anchor = new Rectangle(420, 280, 42, 26);
            badge.Bounds = anchor;
            badge.Show(); Application.DoEvents();
            Check(badge.Bounds == anchor, "First Show moved the badge away from its document anchor.");
        });
        Test("scan/panel-layout", () => {
            using var panel = new ScanPanel(); panel.Report("Quét chỉ đọc. Chọn vùng để xem công thức.", true); panel.Show();
            foreach (var size in new[] { new Size(980, 670), new Size(780, 570) })
            {
                panel.Size = size; panel.PerformLayout();
                using var bitmap = new Bitmap(panel.Width, panel.Height); panel.DrawToBitmap(bitmap, new Rectangle(Point.Empty, panel.Size)); bitmap.Save(Path.Combine(directory, "panel-" + size.Width + ".png"), ImageFormat.Png);
                foreach (var control in Descendants(panel).Where(c => c.Visible))
                    Check(control.Left >= 0 && control.Top >= 0 && control.Right <= control.Parent!.ClientSize.Width && control.Bottom <= control.Parent.ClientSize.Height, "Clipped control: " + control.Name + " " + control.Bounds);
                Check(!panel.Controls.Find("scanConvertAll", true).Single().Enabled && panel.AcceptButton == null, "Empty/stale panel may convert.");
            }
        });
        Write("report.json", new { capturedAtUtc = DateTime.UtcNow.ToString("o"), passed = results.Count - failures, failed = failures, results,
            limits = new[] { "Pure contracts and isolated WinForms layout; not native Word or DPI acceptance." } });
        Console.WriteLine($"Scan contracts: {results.Count - failures} PASS / {failures} FAIL"); return failures == 0 ? 0 : 1;
    }
}
