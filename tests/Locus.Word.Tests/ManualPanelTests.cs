using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Locus.Core;
using Locus.Core.Detection;
using Locus.Word;

internal static partial class Program
{
    // Software render of our own control, independent of Word and Windows screenshot capture.
    private static int RunManualPanel(string[] args)
    {
        directory=Path.GetFullPath(args.Length>0?args[0]:"artifacts/m3/panel");Directory.CreateDirectory(directory);
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        int index=0;
        bool science=Environment.GetEnvironmentVariable("LOCUS_WORD_TEST_SUITE")=="science-panel";
        foreach(var raw in science?new[]{"H2SO4","Ca(OH)2","SO₄²⁻","2H2 + O2 -> 2H2O","K4[Fe(CN)6]","v_0 = 10 m/s^2","vec(v_0)","alpha = 2","F = 10 kg m/s^2"}:new[]{"x mũ 2","x+1/2","(x+1)/(sqrt(2)+3)"})
        foreach(var size in new[]{new Size(740,430),new Size(580,380)})
        Test("panel/"+raw+"/"+size.Width,()=>
        {
            var set=new AnalysisEngine().Analyze(new SourceSnapshot(raw),new AnalysisOptions(InputMode.Explicit,enabledDomains:science?DetectionDomains.All:DetectionDomains.Math)).Regions.Single();
            using var panel=new ManualPanel();panel.Present(set,"source","layout-fixture");panel.Size=size;panel.Show();panel.PerformLayout();
            if(set.Candidates.Count>1) ((Button)panel.Controls.Find("showCandidates",true).Single()).PerformClick();
            foreach(var candidate in set.Candidates)
            {
                panel.SelectCandidate(candidate.Id);panel.PerformLayout();
                Check(panel.CandidateId==candidate.Id&&panel.PreviewCandidateId==candidate.Id,"Selected candidate differs from the rendered image.");
                var confirm=(Button)panel.Controls.Find("confirmFormula",true).Single();
                Check(confirm.Visible&&confirm.Enabled,"Confirmation is unavailable.");
                Check(candidate.Kind!="repair"||confirm.Text.Contains("đề nghị sửa"),"Repair has no explicit confirmation label.");
                foreach(var control in Descendants(panel).Where(c=>c.Visible))
                    Check(control.Left>=0&&control.Top>=0&&control.Right<=control.Parent!.ClientSize.Width&&control.Bottom<=control.Parent.ClientSize.Height,"Clipped control: "+control.Text);
                using var bitmap=new Bitmap(panel.Width,panel.Height);panel.DrawToBitmap(bitmap,new Rectangle(Point.Empty,panel.Size));
                bitmap.Save(Path.Combine(directory,"panel-"+(++index)+".png"),ImageFormat.Png);
            }
            panel.Present(set.Select(set.Candidates[0].Id),"managed","managed-fixture");
            Check(panel.Controls.Find("confirmFormula",true).Single().Visible&&panel.Controls.Find("restoreSource",true).Single().Visible&&panel.Controls.Find("detachFormula",true).Single().Visible,"Managed actions differ.");
            panel.Close();
        });
        Test("panel/render-limit-clears-preview",()=>
        {
            var set=new AnalysisEngine().Analyze(new SourceSnapshot(new string('1',2048)+"^2"),new AnalysisOptions(InputMode.Explicit)).Regions.Single();
            using var panel=new ManualPanel();
            Reject(()=>panel.Present(set,"source","oversized-fixture"));
            Check(panel.CandidateId==""&&panel.PreviewCandidateId==""&&!panel.Controls.Find("confirmFormula",true).Single().Enabled,"A failed render left a confirmable candidate.");
        });
        Write("report.json",new {capturedAtUtc=DateTime.UtcNow.ToString("o"),suite="manual-panel",passed=results.Count-failures,failed=failures,images=index,results,limits=new[]{"Control.DrawToBitmap in an isolated WinForms test host. These images are not screenshots of Word.","Default and minimum window sizes at this host DPI. Physical multi-monitor/DPI changes and pointer clicks remain unverified."}});
        Console.WriteLine($"Manual panel: {results.Count-failures} PASS / {failures} FAIL");return failures==0?0:1;
    }
    private static IEnumerable<Control> Descendants(Control root)
    { foreach(Control child in root.Controls) {yield return child;foreach(var nested in Descendants(child))yield return nested;} }
}
