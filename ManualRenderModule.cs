using System.Drawing.Imaging;
using PdfiumViewer;
using RT.PropellerApi;
using RT.Servers;
using RT.Util.ExtensionMethods;
using HttpMethod = RT.Servers.HttpMethod;

namespace KtaneManualRenderPropeller
{
    public sealed class ManualRenderModule : PropellerModuleBase<ManualRenderSettings>
    {
        public override string Name => "KTaNE Manual Renderer";

        private static Dictionary<string, PdfDocument> s_pdfs = new();

        private static PdfDocument LoadPdf(string name)
        {
            lock (s_pdfs)
            {
                if (s_pdfs.TryGetValue(name, out PdfDocument? value))
                    return value;

                var res = new HttpClient()
                    .GetAsync($"https://ktane.timwi.de/PDF/{name.UrlEscape()}.pdf")
                    .Result
                    .Content
                    .ReadAsStreamAsync()
                    .Result;

                var pdf = PdfDocument.Load(res);
                s_pdfs[name] = pdf;

                return pdf;
            }
        }

        public override HttpResponse Handle(HttpRequest req)
        {
            if (req.Method is not HttpMethod.Get)
                return HttpResponse.PlainText("method not allowed", HttpStatusCode._405_MethodNotAllowed);

            string name = req.Url["name"];
            if (name is null)
                return HttpResponse.PlainText("missing name", HttpStatusCode._400_BadRequest);

            string pageRaw = req.Url["page"];
            int? page = null;
            if (int.TryParse(pageRaw, out int pageParsed))
                page = pageParsed;

            try
            {
                var pdf = LoadPdf(name);
                
                if (page is null)
                    return HttpResponse.PlainText($"{pdf.PageCount}");

                if (page < 0 || page >= pdf.PageCount)
                    return HttpResponse.PlainText("page out of range", HttpStatusCode._400_BadRequest);

                var image = pdf.Render((int)page, 1024, 1024, 100, 100, false);
                var stream = new MemoryStream();
                image.Save(stream, ImageFormat.Png);
                stream.Position = 0;

                return HttpResponse.Create(stream.ToArray(), "image/png");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return HttpResponse.PlainText($"request failed: {e}", HttpStatusCode._500_InternalServerError);
            }
        }

        
    }

    public sealed class ManualRenderSettings { }
}
