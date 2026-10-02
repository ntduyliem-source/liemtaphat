using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Locus.Core;
using Locus.Core.Export;
using Locus.Desktop.Rendering;

namespace Locus.Desktop;

public enum CopyFormat { Png, Svg, Latex, Source, MathMl, Omml }

public static class ClipboardService
{
    /// <summary>PNG bytes preserve alpha; bitmap fallback is white for apps that only accept CF_BITMAP.</summary>
    public static DataObject CreateData(Candidate candidate, FormulaScene scene, CopyFormat format)
    {
        if (candidate.Id != scene.CandidateId) throw new ArgumentException("Candidate and scene must match.");
        var data = new DataObject();
        switch (format)
        {
            case CopyFormat.Png:
                data.SetData("PNG", new MemoryStream(scene.ToPng()), false);
                data.SetImage(scene.Bitmap(whiteBackground: true));
                break;
            case CopyFormat.Svg:
                var svg = scene.ToSvg();
                data.SetData("image/svg+xml", new MemoryStream(Encoding.UTF8.GetBytes(svg)), false);
                SetText(data, svg);
                break;
            default:
                var text = format switch
                {
                    CopyFormat.Latex => CandidateExporter.ToLatex(candidate),
                    CopyFormat.Source => CandidateExporter.ToOriginalText(candidate),
                    CopyFormat.MathMl => CandidateExporter.ToMathMl(candidate),
                    CopyFormat.Omml => CandidateExporter.ToOmml(candidate),
                    _ => throw new ArgumentOutOfRangeException(nameof(format))
                };
                SetText(data, text);
                break;
        }
        return data;
    }

    private static void SetText(DataObject data, string text)
    {
        data.SetText(text, TextDataFormat.UnicodeText);
        // Some consumers require CF_TEXT for Paste Special, even when CF_UNICODETEXT is present.
        data.SetData(DataFormats.Text, text, true);
    }

    public static async Task CopyAsync(EditorSession session, Candidate candidate, FormulaScene scene, CopyFormat format, Func<bool> sceneIsCurrent)
    {
        long revision = session.Revision;
        session.RequireCurrent(candidate.Id, revision);
        var data = CreateData(candidate, scene, format);
        for (int attempt = 0; ; attempt++)
        {
            session.RequireCurrent(candidate.Id, revision);
            if (!sceneIsCurrent()) throw new InvalidOperationException("Thiết lập xuất đã thay đổi; hãy sao chép lại.");
            try { Clipboard.SetDataObject(data, true); return; }
            catch (COMException) when (attempt < 3) { await Task.Delay(60); }
        }
    }
}
