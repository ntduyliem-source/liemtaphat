using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

internal static class MicrosoftWordHost
{
    // WPS may own the 64-bit Word.Application registration on a machine with
    // 32-bit Microsoft Office. Select the verified server without rewriting COM.
    internal static dynamic Create()
    {
        using var classes = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Registry32);
        using var server = classes.OpenSubKey(@"CLSID\{000209FF-0000-0000-C000-000000000046}\LocalServer32");
        string command = server?.GetValue("") as string ?? throw new InvalidOperationException("No 32-bit Word server.");
        int end = command.IndexOf(".EXE", StringComparison.OrdinalIgnoreCase);
        string path = end < 0 ? "" : command[..(end + 4)].Trim(' ', '"');
        if (!File.Exists(path) || !Path.GetFileName(path).Equals("WINWORD.EXE", StringComparison.OrdinalIgnoreCase) ||
            FileVersionInfo.GetVersionInfo(path).CompanyName != "Microsoft Corporation")
            throw new InvalidOperationException("COM server is not Microsoft Word; refusing activation.");
        var clsid = new Guid("000209FF-0000-0000-C000-000000000046");
        var iid = new Guid("00020400-0000-0000-C000-000000000046");
        Marshal.ThrowExceptionForHR(CoCreateInstance(ref clsid, 0, 4 | 0x40000, ref iid, out object instance));
        dynamic word = instance;
        if (!string.Equals((string)word.Path, Path.GetDirectoryName(path), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Activated application is not the verified Microsoft Word installation.");
        return word;
    }
    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(ref Guid clsid, nint outer, uint context, ref Guid iid, [MarshalAs(UnmanagedType.IDispatch)] out object instance);
}
