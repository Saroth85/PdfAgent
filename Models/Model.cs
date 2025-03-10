using System;
using System.Collections.Generic;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

namespace PdfAgent.Models
{
    // Modello per l'indice di ricerca
    public class DocumentIndex
    {
        [SimpleField(IsKey = true, IsFilterable = true)]
        public string Id { get; set; }
        
        [SearchableField(IsSortable = true)]
        public string FileName { get; set; }
        
        [SimpleField(IsFilterable = false)]
        public string FileUrl { get; set; }
        
        [SearchableField(IsFilterable = true)]
        public string Content { get; set; }
        
        [SearchableField(IsFilterable = true)]
        public string Entities { get; set; }
        
        [SearchableField(IsFilterable = true)]
        public string KeyPhrases { get; set; }
        
        [SimpleField(IsFilterable = true, IsSortable = true)]
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
        public List<SearchResult> Results { get; set; } = new List<SearchResult>();

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