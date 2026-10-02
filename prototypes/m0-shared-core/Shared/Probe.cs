using System;
using System.Text;

namespace Locus.M0
{
    // Fixed fixture only. This is not the Locus parser or production contract.
    public static class SharedProbe
    {
        public static string Evaluate(string source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            string normalized = source.Normalize(NormalizationForm.FormC);
            if (normalized != "x mũ 2") throw new ArgumentException("M0 fixture supports only x mũ 2");
            return "fixture-v1|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(source))
                + "|utf16-length=" + source.Length
                + "|candidate=fixed-power|ast=power(symbol(x),number(2))";
        }
    }
}
