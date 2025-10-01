using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace WebUI.Infrastructure
{
    public static class EncodingHelpers
    {
        public static async Task<string> ReadAllTextSmartAsync(IFormFile file)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms).ConfigureAwait(false);

            ms.Position = 0;
            using var rUtf8 = new StreamReader(ms, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                                               detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            var text = await rUtf8.ReadToEndAsync().ConfigureAwait(false);

            if (text.IndexOf('\uFFFD') >= 0)
            {
                ms.Position = 0;
                var trEnc = Encoding.GetEncoding("windows-1254");
                using var rTr = new StreamReader(ms, trEnc, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
                text = await rTr.ReadToEndAsync().ConfigureAwait(false);
            }

            return text;
        }
    }
}
