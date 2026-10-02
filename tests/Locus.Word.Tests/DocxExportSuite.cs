using System;
using System.IO;
using System.Linq;
using Locus.Word;
using WordApi = Microsoft.Office.Interop.Word;

internal static partial class Program
{
    private static void RunDocxExport()
    {
        app.Visible = true;
        Test("docx-export/native-roundtrip", () =>
        {
            // Keep the application alive while the exported document closes/reopens.
            var anchor = app.Documents.Add(); documents.Add(anchor); anchor.Content.Text = "Locus DOCX verification anchor.\r";
            foreach (var fixture in new[] { new { Name = "mixed-native", Count = 5 }, new { Name = "products-ignore", Count = 1 } })
            {
            string source = Path.GetFullPath("artifacts/doc1/verification/" + fixture.Name + ".docx");
            Stage(fixture.Name + "/open");
            var doc = app.Documents.Open(source, ReadOnly: true, AddToRecentFiles: false); documents.Add(doc);
            Check(doc.OMaths.Count == fixture.Count, "Native equation count differs.");
            string text = doc.Content.Text;
            string[] signatures = doc.OMaths.Cast<WordApi.OMath>().Select(m => NativeMathSignature.FromXml(m.Range.WordOpenXML, true)).ToArray();
            Check(fixture.Name != "mixed-native" || text.Contains("Giữ câu chữ <b> & dấu tab:") && text.Contains("hoa-[H2SO4]") && text.Contains("https://example.org/bai"), "Surrounding text changed.");
            string saved = Path.Combine(directory, fixture.Name + "-roundtrip.docx");
            Stage(fixture.Name + "/save"); doc.SaveAs2(saved, WordApi.WdSaveFormat.wdFormatXMLDocument, AddToRecentFiles: false);
            Stage(fixture.Name + "/close"); Close(doc);
            Stage(fixture.Name + "/reopen"); doc = app.Documents.Open(saved, ReadOnly: true, AddToRecentFiles: false); documents.Add(doc);
            Check(doc.Content.Text == text && doc.OMaths.Count == fixture.Count, "Reopened text/equation count differs.");
            Check(doc.OMaths.Cast<WordApi.OMath>().Select(m => NativeMathSignature.FromXml(m.Range.WordOpenXML, true)).SequenceEqual(signatures), "Reopened native structure differs.");
            Write(fixture.Name + "-verified.json", new { source, saved, nativeEquations = fixture.Count, text, nativeSignatures = signatures, scope = "Word COM open/save/close/reopen; no keyboard or clipboard acceptance." });
            Stage(fixture.Name + "/render"); doc.ExportAsFixedFormat(Path.Combine(directory, fixture.Name + ".pdf"), WordApi.WdExportFormat.wdExportFormatPDF);
            Close(doc);
            }
            Close(anchor);
        });
    }
}
