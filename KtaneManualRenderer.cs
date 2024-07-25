using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using PdfiumViewer;
using RT.PropellerApi;
using RT.Servers;
using RT.Util.ExtensionMethods;
using HttpMethod = RT.Servers.HttpMethod;

namespace Propeller.KTANE
{
    public sealed class KtaneManualRenderer : PropellerModuleBase<KtaneManualRendererSettings>
    {
        public override string Name => "KTANE Manual Renderer";

        private readonly Dictionary<string, (PdfDocument pdf, byte[][] pages, DateTime lastAccess)> _pdfs = new();
        private UrlResolver _resolver;

        public override void Init()
        {
            _resolver = new UrlResolver(
                new UrlMapping(path: "/debug", handler: DumpDebugInfo),
                new UrlMapping(path: "/get", handler: GetPage),
                new UrlMapping(path: "/purge", handler: Purge));
            base.Init();
        }

        public override HttpResponse Handle(HttpRequest req) => _resolver.Handle(req);

        private HttpResponse GetPage(HttpRequest req)
        {
            if (req.Method != HttpMethod.Get)
                return HttpResponse.PlainText("Method not allowed.", HttpStatusCode._405_MethodNotAllowed);

            string name = req.Url["name"];
            if (name is null)
                return HttpResponse.PlainText("Missing ‘name’ query parameter.", HttpStatusCode._400_BadRequest);

            string pageRaw = req.Url["page"];
            int? page = null;
            if (int.TryParse(pageRaw, out int pageParsed))
                page = pageParsed;

            PdfDocument pdf;
            byte[][] pages;

            lock (_pdfs)
            {
                if (_pdfs.TryGetValue(name, out var tup))
                    (pdf, pages, _) = tup;
                else
                {
                    var res = new HttpClient()
                        .GetAsync(string.Format(Settings.UrlTemplate, name.UrlEscape()))
                        .Result
                        .Content
                        .ReadAsStreamAsync()
                        .Result;

                    pdf = PdfDocument.Load(res);
                    pages = new byte[pdf.PageCount][];
                }

                _pdfs.RemoveAllByValue(v => (DateTime.UtcNow - v.lastAccess).TotalHours > 24);
            }

            try
            {
                if (page == null)
                {
                    lock (_pdfs)
                        _pdfs[name] = (pdf, pages, DateTime.UtcNow);
                    return HttpResponse.PlainText(pages.Length.ToString());
                }

                if (page < 0 || page >= pages.Length)
                    return HttpResponse.PlainText("Page number out of range.", HttpStatusCode._400_BadRequest);

                if (pages[page.Value] == null)
                {
                    var image = pdf.Render(page.Value, 792, 1024, 100, 100, false);
                    using var stream = new MemoryStream();
                    image.Save(stream, ImageFormat.Png);
                    pages[page.Value] = stream.ToArray();
                    if (pages.All(b => b != null))
                        pdf = null;
                }
                lock (_pdfs)
                    _pdfs[name] = (pdf, pages, DateTime.UtcNow);
                return HttpResponse.Create(pages[page.Value], "image/png");
            }
            catch (Exception e)
            {
#if DEBUG
                Console.WriteLine(e);
#endif
                return HttpResponse.PlainText($"Request failed: {e}", HttpStatusCode._500_InternalServerError);
            }
        }

        private HttpResponse Purge(HttpRequest req)
        {
            if (req.Method != HttpMethod.Get)
                return HttpResponse.PlainText("Method not allowed.", HttpStatusCode._405_MethodNotAllowed);

            lock (_pdfs)
                _pdfs.Clear();
            return HttpResponse.PlainText("Done.");
        }

        private HttpResponse DumpDebugInfo(HttpRequest req)
        {
            string info;
            lock (_pdfs)
                info = _pdfs.Select(kvp => $"“{kvp.Key}” = ({(kvp.Value.pdf == null ? "null" : "PDF")}, {(kvp.Value.pages == null ? "<null>" : $"[{(kvp.Value.pages).Select(p => p == null ? "_" : p.Length.ToString()).JoinString(", ")}]")}, {kvp.Value.lastAccess})").JoinString("\n");
            return HttpResponse.PlainText(info);
        }
    }

    public sealed class KtaneManualRendererSettings
    {
        public string UrlTemplate = @"https://ktane.timwi.de/PDF/{0}.pdf";
    }
}
