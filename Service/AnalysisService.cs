using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using PdfAgent.Models;
using System;
using System.Text;
using System.Threading.Tasks;

namespace PdfAgent.Services
{
    public interface IAnalysisService
    {
        Task<AnalysisResponse> AnalyzeDocumentAsync(string fileName, string fileUrl);
    }

    public class AnalysisService : IAnalysisService
    {
        private readonly DocumentAnalysisClient _documentAnalysisClient;
        private readonly ISearchService _searchService;

        public AnalysisService(DocumentAnalysisClient documentAnalysisClient, ISearchService searchService)
        {
            _documentAnalysisClient = documentAnalysisClient;
            _searchService = searchService;
        }

        public async Task<AnalysisResponse> AnalyzeDocumentAsync(string fileName, string fileUrl)
        {
            try
            {
                // Analizza il documento con Form Recognizer
                AnalyzeDocumentOperation operation = await _documentAnalysisClient.AnalyzeDocumentFromUriAsync(
                    WaitUntil.Completed,
                    "prebuilt-document",
                    new Uri(fileUrl));

                var result = operation.Value;

                // Estrai testo e metadati
                StringBuilder contentBuilder = new StringBuilder();

                // Raccoglie il testo dal risultato
                if (!string.IsNullOrEmpty(result.Content))
                {
                    contentBuilder.Append(result.Content);
                }

                string content = contentBuilder.ToString();

                // Per estrarre entità e frasi chiave, potresti usare Text Analytics in una implementazione più completa
                string entities = "";
                string keyPhrases = "";

                // Crea l'oggetto DocumentIndex
                var document = new DocumentIndex
                {
                    Id = fileName,
                    FileName = fileName,
                    FileUrl = fileUrl,
                    Content = content,
                    Entities = entities,
                    KeyPhrases = keyPhrases,
                    UploadDate = DateTime.UtcNow
                };

                // Indicizza il documento
                await _searchService.IndexDocumentAsync(document);

                return new AnalysisResponse
                {
                    FileName = fileName,
                    Success = true,
                    Message = "Documento analizzato con successo.",
                    Document = document
                };
            }
            catch (Exception ex)
            {
                return new AnalysisResponse
                {
                    FileName = fileName,
                    Success = false,
                    Message = $"Si è verificato un errore durante l'analisi: {ex.Message}"
                };
            }
        }
    }
}