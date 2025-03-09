using System;
using System.Collections.Generic;

namespace PDFAnalyzerApi.Models
{
    // Modello per l'indice di ricerca
    public class DocumentIndex
    {
        public string Id { get; set; }
        public string FileName { get; set; }
        public string FileUrl { get; set; }
        public string Content { get; set; }
        public string Entities { get; set; }
        public string KeyPhrases { get; set; }
        public DateTime UploadDate { get; set; }
    }

    // Risposta per il caricamento di un documento
    public class UploadResponse
    {
        public string FileName { get; set; }
        public string FileUrl { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    // Risposta per l'analisi di un documento
    public class AnalysisResponse
    {
        public string FileName { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public DocumentIndex Document { get; set; }
    }

    // Richiesta di ricerca
    public class SearchRequest
    {
        public string Query { get; set; }
        public int PageSize { get; set; } = 10;
        public int PageNumber { get; set; } = 0;
    }

    // Risposta di ricerca
    public class SearchResponse
    {
        public long TotalCount { get; set; }
        public List<SearchResult> Results { get; set; }

        public class SearchResult
        {
            public DocumentIndex Document { get; set; }
            public double Score { get; set; }
            public string Caption { get; set; }
        }
    }

    // Metadati del documento
    public class DocumentMetadata
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public long Size { get; set; }
        public DateTimeOffset? LastModified { get; set; }
    }
}