using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using PDFAnalyzerApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PDFAnalyzerApi.Services
{
    public interface ISearchService
    {
        Task<bool> EnsureIndexExistsAsync();
        Task IndexDocumentAsync(DocumentIndex document);
        Task<SearchResponse> SearchDocumentsAsync(SearchRequest request);
    }

    public class SearchService : ISearchService
    {
        private readonly SearchIndexClient _searchIndexClient;
        private readonly IConfiguration _configuration;
        private readonly string _indexName;

        public SearchService(SearchIndexClient searchIndexClient, IConfiguration configuration)
        {
            _searchIndexClient = searchIndexClient;
            _configuration = configuration;
            _indexName = _configuration["Azure:Search:IndexName"];
        }

        public async Task<bool> EnsureIndexExistsAsync()
        {
            try
            {
                // Verifica se l'indice esiste
                await _searchIndexClient.GetIndexAsync(_indexName);
                return true;
            }
            catch (RequestFailedException)
            {
                // L'indice non esiste, crealo
                var searchFields = new FieldBuilder().Build(typeof(DocumentIndex));

                var definition = new SearchIndex(_indexName, searchFields)
                {
                    SemanticSearch = new SemanticSearch
                    {
                        Configurations =
                        {
                            new SemanticConfiguration("default", new SemanticPrioritizedFields
                            {
                                TitleField = new SemanticField("FileName"),
                             //   ContentFields = new SemanticField ("Content" ),
                               // KeywordsFields = new SemanticField ("Entities" )
                            })
                        }
                    }
                };

                await _searchIndexClient.CreateOrUpdateIndexAsync(definition);
                return true;
            }
        }

        public async Task IndexDocumentAsync(DocumentIndex document)
        {
            // Assicurati che l'indice esista
            await EnsureIndexExistsAsync();

            // Crea il client di ricerca
            var searchEndpoint = new Uri(_configuration["Azure:Search:Endpoint"]);
            var searchApiKey = _configuration["Azure:Search:ApiKey"];
            var searchClient = new SearchClient(
                searchEndpoint,
                _indexName,
                new AzureKeyCredential(searchApiKey));

            // Indicizza il documento
            await searchClient.IndexDocumentsAsync(IndexDocumentsBatch.Upload(new[] { document }));
        }

        public async Task<SearchResponse> SearchDocumentsAsync(SearchRequest request)
        {
            // Assicurati che l'indice esista
            await EnsureIndexExistsAsync();

            // Crea il client di ricerca
            var searchEndpoint = new Uri(_configuration["Azure:Search:Endpoint"]);
            var searchApiKey = _configuration["Azure:Search:ApiKey"];
            var searchClient = new SearchClient(
                searchEndpoint,
                _indexName,
                new AzureKeyCredential(searchApiKey));
          
            // Configura le opzioni di ricerca semantica
            var options = new SearchOptions
            {
                QueryType = SearchQueryType.Semantic,
                //SemanticConfigurationName = "default",
                //QueryLanguage = "it-IT",
                Size = request.PageSize,
                Skip = request.PageNumber * request.PageSize,
                IncludeTotalCount = true,
                Select = { "Id", "FileName", "FileUrl", "Content", "Entities", "KeyPhrases", "UploadDate" },
                //QueryCaption = new QueryCaption(QueryCaptionType.Extractive)
            };

            // Esegui la ricerca
            var searchResults = await searchClient.SearchAsync<DocumentIndex>(request.Query, options);

            // Prepara la risposta
            var response = new SearchResponse
            {
                TotalCount = searchResults.Value.TotalCount ?? 0,
                Results = new List<SearchResponse.SearchResult>()
            };

            // Aggiungi i risultati alla risposta
            await foreach (var result in searchResults.Value.GetResultsAsync())
            {
                response.Results.Add(new SearchResponse.SearchResult
                {
                    Document = result.Document,
                    Score = result.Score ?? 0,
                    Caption = result.SemanticSearch.Captions?.FirstOrDefault()?.Text
                });
            }

            return response;
        }
    }
}