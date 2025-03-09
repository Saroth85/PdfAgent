using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using PDFAnalyzerApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PDFAnalyzerApi.Services
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

                Azure.AI.FormRecognizer.DocumentAnalysis.AnalyzeResult result = operation.Value;

                // Estrai testo e metadati
                string content = string.Join(" ", result.Content);
               // var entities = result.?.Select(e => e.Category + ": " + e.Content).ToList() ?? new List<string>();
               // var keyPhrases = result.KeyPhrases?.ToList() ?? new List<string>();

                // Crea l'oggetto DocumentIndex
                var document = new DocumentIndex
                {
                    Id = fileName,
                    FileName = fileName,
                    FileUrl = fileUrl,
                    Content = content,
                    Entities = "",
                    KeyPhrases = "",
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