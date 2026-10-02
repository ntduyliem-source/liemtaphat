using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Locus.Application;

namespace Locus.Worker;

[SupportedOSPlatform("browser")]
public static partial class WorkerMethods
{
    public static void Main() { }
    [JSExport] public static string Analyze(string request) => AnalysisWire.Run(request);
}
