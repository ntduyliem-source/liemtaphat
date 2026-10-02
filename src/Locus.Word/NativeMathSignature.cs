using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Xml;
using System.Xml.Linq;
using Locus.Core.Export;

namespace Locus.Word;

// M1 OMML structure, text and semantic properties; formatting/run splits are intentionally ignored.
public static class NativeMathSignature
{
    private static readonly XNamespace M=CandidateExporter.OmmlNamespace;
    public static string FromXml(string xml,bool scientific=false)
    {
        if(xml.Length>4*1024*1024)throw new InvalidOperationException("native-xml-too-large");
        using var reader=XmlReader.Create(new StringReader(xml),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4*1024*1024});
        var document=XDocument.Load(reader); var maths=document.Descendants(M+"oMath").ToArray();
        if(maths.Length!=1)throw new InvalidOperationException("native-shape-changed");
        return Node(maths[0],scientific);
    }
    private static string Node(XElement node,bool scientific)
    {
        if(node.Name.Namespace!=M)return "";
        string name=node.Name.LocalName;
        if(name.EndsWith("Pr",StringComparison.Ordinal))return "";
        if(name=="t")return SecurityElement.Escape(node.Value);
        string content=string.Concat(node.Elements().Select(c=>Node(c,scientific)));
        if(name=="r")
        {
            if(!scientific)return content;
            var runProperties=node.Element(M+"rPr");string style=(string?)runProperties?.Element(M+"sty")?.Attribute(M+"val")??"i";
            if(runProperties?.Element(M+"nor") is XElement normal && !new[]{"0","false","off"}.Contains((string?)normal.Attribute(M+"val")))style="p";
            return string.Concat(string.Concat(node.Elements(M+"t").Select(t=>t.Value)).Select(c=>char.IsLetter(c)?"<letter sty=\""+SecurityElement.Escape(style)+"\">"+SecurityElement.Escape(c.ToString())+"</letter>":SecurityElement.Escape(c.ToString())));
        }
        var defaults=new SortedDictionary<string,string>(StringComparer.Ordinal);
        if(name=="f")defaults.Add("type","bar");
        if(name=="rad")defaults.Add("degHide","0");
        if(name=="acc")defaults.Add("chr","\u0302");
        if(name=="d"){defaults.Add("begChr","(");defaults.Add("endChr",")");defaults.Add("sepChr","|");}
        string properties="";
        foreach(var item in defaults)
        {
            string value=(string?)node.Element(M+(name+"Pr"))?.Element(M+item.Key)?.Attribute(M+"val")??item.Value;
            if(item.Key.EndsWith("Hide",StringComparison.Ordinal)){if(value=="true"||value=="on")value="1";if(value=="false"||value=="off")value="0";}
            properties+=" "+item.Key+"=\""+SecurityElement.Escape(value)+"\"";
        }
        return "<"+name+properties+">"+content+"</"+name+">";
    }
}
