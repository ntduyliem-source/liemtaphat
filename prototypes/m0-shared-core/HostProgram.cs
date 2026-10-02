using System;
using System.Text;
using System.IO;
using System.IO.Pipes;
using Locus.M0;

internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        if (args.Length > 0 && args[0] == "server") return Serve(args[1], args[2]);
        if (args.Length > 0 && args[0] == "client") return Exercise(args[1], args[2]);
        Console.WriteLine(SharedProbe.Evaluate("x m\u0169 2"));
        Console.WriteLine(SharedProbe.Evaluate("x mu\u0303 2"));
        return 0;
    }

    // Transport-only spike with synthetic host state. No Word document is opened.
    private static int Serve(string pipeName, string session)
    {
        bool done = false;
        while (!done)
        {
            using (var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte))
            {
                pipe.WaitForConnection();
                using (var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, true))
                using (var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, true) { AutoFlush = true })
                {
                    string message = reader.ReadLine();
                    if (message == "shutdown") { writer.WriteLine("STOPPED"); done = true; continue; }
                    string[] parts = (message ?? "").Split('|');
                    if (parts.Length != 9) { writer.WriteLine("REJECT:message"); continue; }
                    string rejection = parts[0] != "v1" ? "protocol"
                        : parts[1] != session ? "session"
                        : parts[2] != "scratch-document-A" ? "document"
                        : parts[3] != "revision-1" ? "revision"
                        : parts[4] != "fixed-power" ? "candidate"
                        : parts[5] != Convert.ToBase64String(Encoding.UTF8.GetBytes("x m\u0169 2")) ? "source"
                        : parts[6] != "config-1" ? "configuration"
                        : parts[7] != "idle" ? "composition"
                        : parts[8] != "editor" ? "focus" : null;
                    if (rejection != null) writer.WriteLine("REJECT:" + rejection);
                    else writer.WriteLine("RESULT:" + SharedProbe.Evaluate(Encoding.UTF8.GetString(Convert.FromBase64String(parts[5]))));
                }
            }
        }
        return 0;
    }

    private static string Exchange(string pipeName, string message)
    {
        using (var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut))
        {
            pipe.Connect(4000);
            using (var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, true) { AutoFlush = true })
            using (var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, true))
            {
                writer.WriteLine(message);
                return reader.ReadLine();
            }
        }
    }

    private static int Exercise(string pipeName, string session)
    {
        string[] valid = { "v1", session, "scratch-document-A", "revision-1", "fixed-power",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("x m\u0169 2")), "config-1", "idle", "editor" };
        string result = Exchange(pipeName, string.Join("|", valid));
        bool allPassed = result != null && result.StartsWith("RESULT:fixture-v1|", StringComparison.Ordinal);
        Console.WriteLine((allPassed ? "PASS" : "FAIL") + "|valid-roundtrip|" + result);
        string[] reasons = { "protocol", "session", "document", "revision", "candidate", "source", "configuration", "composition", "focus" };
        for (int index = 0; index < valid.Length; index++)
        {
            string[] stale = (string[])valid.Clone();
            stale[index] = "stale-or-unknown";
            string response = Exchange(pipeName, string.Join("|", stale));
            bool passed = response == "REJECT:" + reasons[index];
            allPassed &= passed;
            Console.WriteLine((passed ? "PASS" : "FAIL") + "|reject-" + reasons[index] + "|" + response);
        }
        if (session != "session-1")
        {
            valid[1] = "session-1";
            string response = Exchange(pipeName, string.Join("|", valid));
            bool passed = response == "REJECT:session";
            allPassed &= passed;
            Console.WriteLine((passed ? "PASS" : "FAIL") + "|old-session-after-process-restart|" + response);
        }
        Exchange(pipeName, "shutdown");
        return allPassed ? 0 : 1;
    }
}
