using Azure.AI.DocumentIntelligence;
using Azure.Search.Documents;
using Azure;

namespace PdfAgent.Controllers
{
    public class PdfIndexerService
    {
        private readonly DocumentIntelligenceClient _docClient;
        private readonly SearchClient _searchClient;

        public PdfIndexerService(IConfiguration config)
        {
            _docClient = new DocumentIntelligenceClient(
                new Uri(config["Azure:DocumentIntelligenceEndpoint"]),
                new AzureKeyCredential(config["Azure:DocumentIntelligenceKey"]));

            _searchClient = new SearchClient(
                new Uri(config["Azure:AISearchEndpoint"]),
                "pdf-index",
                new AzureKeyCredential(config["Azure:AISearchKey"]));
        }

        public async Task IndexPdfAsync(Uri pdfUri)
        {
            var operation = await _docClient.AnalyzeDocumentFromUriAsync(
                WaitUntil.Completed, "prebuilt-layout", pdfUri);

            var extractedText = string.Join("\n", operation.Value.Pages.SelectMany(p => p.Lines.Select(l => l.Content)));

            var doc = new { id = Guid.NewGuid().ToString(), content = extractedText };
            await _searchClient.UploadDocumentsAsync(new[] { doc });
        }
    }
}