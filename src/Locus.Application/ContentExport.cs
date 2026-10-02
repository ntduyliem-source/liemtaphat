using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Locus.Core;
using Locus.Core.Export;

namespace Locus.Application;

public sealed record ContentExportPart(string? Text, Candidate? Formula);

/// <summary>A bounded, immutable projection of the selected result. No parsing, balancing or source mutation.</summary>
public sealed class ContentExport
{
    public const string DocxMediaType="application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace M=CandidateExporter.OmmlNamespace;
    private readonly IReadOnlyList<ContentExportPart> parts;
    public int FormulaCount=>parts.Count(p=>p.Formula!=null);
    private ContentExport(IEnumerable<ContentExportPart> parts)=>this.parts=Array.AsReadOnly(parts.ToArray());

    public static ContentExport Capture(FormulaState state,ContentSelection? selection=null)
    {
        var content=state.Content??new ContentDocument(state.Raw,state.SourceRevision,state.Analysis?.Regions.Select((set,index)=>
        {
            var region=ContentDocument.FromSet(set,0);
            return index==state.RegionIndex&&state.CandidateId!=null?region with{SelectedId=state.CandidateId}:region;
        })??[]);
        content.Validate();int start=selection?.Start??0,end=selection?.End??content.Raw.Length;
        var bounds=content.Select(start,end);
        if(start==end)throw new InvalidOperationException("Chọn nội dung cần xuất.");
        if(selection!=null&&(selection.Whole.Distinct().Count()!=selection.Whole.Count||selection.Partial.Distinct().Count()!=selection.Partial.Count||selection.Whole.Any(id=>!bounds.Whole.Contains(id))||selection.Partial.Any(id=>!bounds.Partial.Contains(id)&&!bounds.Whole.Contains(id))||selection.Whole.Intersect(selection.Partial).Any()))throw new ArgumentException("Invalid export selection.");
        if(content.Regions.Any(r=>r.Display!=null&&(bounds.Partial.Contains(r.Id)||selection?.Partial.Contains(r.Id)==true)))throw new InvalidOperationException("Chọn trọn công thức trước khi xuất.");
        if(selection!=null&&bounds.Whole.Any(id=>!selection.Whole.Contains(id)&&!selection.Partial.Contains(id)))throw new ArgumentException("Missing selected region.");
        var parts=new List<ContentExportPart>();int cursor=start;
        foreach(var region in content.Regions.Where(r=>r.Start>=start&&r.End<=end))
        {
            if(region.Start>cursor)parts.Add(new(content.Raw[cursor..region.Start],null));
            parts.Add(region.Display is {} candidate?new(null,candidate):new(region.Raw,null));cursor=region.End;
        }
        if(cursor<end)parts.Add(new(content.Raw[cursor..end],null));
        return new(parts);
    }

    public string ToHtml(CancellationToken token=default)
    {
        var body=new StringBuilder("<!doctype html><html lang=\"vi\"><head><meta charset=\"utf-8\"><title>Nội dung Locus</title><style>body{font:12pt Arial,sans-serif;line-height:1.6;max-width:52rem;margin:2rem auto;padding:0 1rem;color:#111}main{white-space:pre-wrap;overflow-wrap:anywhere}math{font-family:'Cambria Math',serif}</style></head><body><main>");
        foreach(var part in parts){token.ThrowIfCancellationRequested();body.Append(part.Formula is {} candidate?CandidateExporter.ToMathMl(candidate):System.Net.WebUtility.HtmlEncode(part.Text));}
        return body.Append("</main></body></html>").ToString();
    }

    public byte[] ToDocx(CancellationToken token=default)
    {
        var body=new XElement(W+"body");var paragraph=new XElement(W+"p");body.Add(paragraph);
        foreach(var part in parts)
        {
            token.ThrowIfCancellationRequested();
            if(part.Formula is {} candidate){paragraph.Add(XElement.Parse(CandidateExporter.ToOmml(candidate)));continue;}
            string text=part.Text!;XmlConvert.VerifyXmlChars(text);int start=0;
            for(int i=0;i<text.Length;i++)
            {
                if((i&1023)==0)token.ThrowIfCancellationRequested();
                if(text[i] is not ('\r' or '\n' or '\t'))continue;
                AddText(paragraph,text[start..i]);
                if(text[i]=='\t')paragraph.Add(new XElement(W+"r",new XElement(W+"tab")));
                else{if(text[i]=='\r'&&i+1<text.Length&&text[i+1]=='\n')i++;paragraph=new(W+"p");body.Add(paragraph);}
                start=i+1;
            }
            AddText(paragraph,text[start..]);
        }
        // Default page layout for a plain-text export; source content never gets an invented title/header.
        body.Add(new XElement(W+"sectPr",new XElement(W+"pgSz",new XAttribute(W+"w","12240"),new XAttribute(W+"h","15840")),
            new XElement(W+"pgMar",new XAttribute(W+"top","1440"),new XAttribute(W+"bottom","1440"),new XAttribute(W+"left","1440"),new XAttribute(W+"right","1440"))));
        var document=new XElement(W+"document",new XAttribute(XNamespace.Xmlns+"w",W),new XAttribute(XNamespace.Xmlns+"m",M),body);
        using var memory=new MemoryStream();
        using(var zip=new ZipArchive(memory,ZipArchiveMode.Create,true))
        {
            Add(zip,"[Content_Types].xml","<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/><Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/><Override PartName=\"/word/settings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml\"/></Types>",token);
            Add(zip,"_rels/.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>",token);
            Add(zip,"word/_rels/document.xml.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/settings\" Target=\"settings.xml\"/></Relationships>",token);
            Add(zip,"word/document.xml",document.ToString(SaveOptions.DisableFormatting),token);
            Add(zip,"word/styles.xml",$"<w:styles xmlns:w=\"{W}\"><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Arial\" w:hAnsi=\"Arial\" w:cs=\"Arial\"/><w:color w:val=\"000000\"/><w:sz w:val=\"24\"/><w:szCs w:val=\"24\"/><w:lang w:val=\"vi-VN\"/></w:rPr></w:rPrDefault><w:pPrDefault><w:pPr><w:spacing w:after=\"120\" w:line=\"300\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults><w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/></w:style></w:styles>",token);
            Add(zip,"word/settings.xml",$"<w:settings xmlns:w=\"{W}\" xmlns:m=\"{M}\"><m:mathPr><m:mathFont m:val=\"Cambria Math\"/></m:mathPr></w:settings>",token);
        }
        token.ThrowIfCancellationRequested();return memory.ToArray();
    }
    private static void AddText(XElement paragraph,string text){if(text.Length>0)paragraph.Add(new XElement(W+"r",new XElement(W+"t",new XAttribute(XNamespace.Xml+"space","preserve"),text)));}
    private static void Add(ZipArchive zip,string path,string xml,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();var entry=zip.CreateEntry(path,CompressionLevel.Fastest);entry.LastWriteTime=new DateTimeOffset(1980,1,1,0,0,0,TimeSpan.Zero);
        using var writer=new StreamWriter(entry.Open(),new UTF8Encoding(false));writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");writer.Write(xml);
    }
}
