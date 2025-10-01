using System;
using System.Globalization;
using System.Text;

namespace WebUI.Infrastructure
{
    public static class TurkishText
    {
        public static string KeepTurkish(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return value.Trim().Normalize(NormalizationForm.FormC);
        }

        public static readonly StringComparer TrIgnoreCase =
            StringComparer.Create(new CultureInfo("tr-TR"), ignoreCase: true);

        public static bool EqualsTr(string? a, string? b)
        {
            var aa = a == null ? null : a.Normalize(NormalizationForm.FormC);
            var bb = b == null ? null : b.Normalize(NormalizationForm.FormC);
            return TrIgnoreCase.Equals(aa, bb);
        }
    }
}
