using System.Text.Encodings.Web;
using System.Xml.Linq;
using Locus.Application;
using Locus.Core.Detection;
using Locus.Editor.Formula.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
Task<string> Render<T>(Dictionary<string,object?> values) where T:IComponent => renderer.Dispatcher.InvokeAsync(async () =>
    (await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(values))).ToHtmlString());
void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
using var session = new FormulaSession(new NativeAnalysisScheduler());
session.Configure(new(EnabledDomains:DetectionDomains.All));
var raw="Mở bài 🧪 <script>alert(1)</script>\nCho lc[x mũ 2 + 1]. Xét hoa-[H2+O2=H2O].\nGiữ nguyên phần cuối.";
session.UpdateSource(raw);
Assert(await session.AnalyzeContentAsync(),"Analysis fixture must complete");
var content=session.State.Content!;
Assert(content.Regions.Count>=2,"Fixture must contain separate regions");
var selected=content.Regions[0];
var props=new Dictionary<string,object?>{
    [nameof(FormulaResult.Content)]=content,[nameof(FormulaResult.Raw)]=raw,
    [nameof(FormulaResult.Revision)]=session.State.SourceRevision,
    [nameof(FormulaResult.SelectedIds)]=new[]{selected.Id}
};
var html=await Render<FormulaResult>(props);
Assert(!html.Contains("OnSelect.InvokeAsync"),"Blazor callback leaked into a native HTML attribute");
var root=XElement.Parse(html,LoadOptions.PreserveWhitespace);
Assert(root.Attribute("id")?.Value=="mixed-result","Bridge root identity changed");
Assert(!root.Descendants("script").Any()&&!root.Descendants("button").Any(),"Source/UI must not become executable or exported controls");
foreach(var block in content.Blocks())
{
    if(block.Region is {} region)
    {
        var span=root.Elements("span").Single(e=>e.Attribute("data-region-id")?.Value==region.Id.ToString());
        Assert(span.Attribute("data-source-start")?.Value==region.Start.ToString() && span.Attribute("data-source-end")?.Value==region.End.ToString(),"UTF-16 source offsets changed");
        Assert(span.Elements().Single().Attribute("class")?.Value=="region-content","Selection endpoint wrapper changed");
        Assert(span.Attribute("class")!.Value.Contains("is-selected")== (region.Id==selected.Id),"Selected region mapping changed");
        Assert(span.Descendants().Any(e=>e.Name.LocalName=="math")== (region.Display!=null),"Rendered candidate differs from document");
    }
    else
    {
        var span=root.Elements("span").Single(e=>e.Attribute("data-text-start")?.Value==block.Start.ToString());
        Assert(span.Value==raw[block.Start..block.End],"Untouched text/line breaks/emoji changed");
    }
}
foreach(var state in new[]{nameof(FormulaResult.IsBusy),nameof(FormulaResult.IsComposing)})
{
    props[state]=true;
    var pending=XElement.Parse(await Render<FormulaResult>(props),LoadOptions.PreserveWhitespace);
    Assert(!pending.Descendants().Any(e=>e.Name.LocalName=="math"),"Stale formula shown while processing/IME");
    Assert(pending.Elements("span").Single().Value==raw,"Pending view lost source");
    props[state]=false;
}
var export=await Render<FormulaExportBar>(new(){[nameof(FormulaExportBar.AllSelected)]=true,[nameof(FormulaExportBar.CanExportImage)]=true});
Assert(export.Contains("id=\"download-svg\"")&&export.Contains("id=\"download-png\"")&&export.Contains("id=\"copy-png\"")&&!export.Contains("download-docx")&&!export.Contains("download-html")&&!export.Contains("scope-formula"),"Image toolbar contract changed");
session.UpdateSource("x^2+1/2");await session.AnalyzeContentAsync();
var suggestionHtml=await Render<FormulaSuggestions>(new(){[nameof(FormulaSuggestions.SelectedRegion)]=session.State.Content!.Regions.Single()});
Assert(suggestionHtml.Split("<math").Length-1==1&&!suggestionHtml.Contains("Thử công thức"),"Suggestions repeat direct or contain samples");
var input=await Render<FormulaSourcePanel>(new(){[nameof(FormulaSourcePanel.Raw)]=raw,[nameof(FormulaSourcePanel.Ready)]=false});
Assert(input.Contains("id=\"source\"")&&input.Contains("readonly")&&input.Contains("source-ghost")&&input.Contains("ghost-hint"),"IME/ghost bridge contract changed");
Console.WriteLine("PASS: result DOM offsets/text/candidate/selection, busy/IME stale result guard, export scope and source bridge.");
