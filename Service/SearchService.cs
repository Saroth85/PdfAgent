using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using PdfAgent.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PdfAgent.Services
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

                var definition = new SearchIndex(_indexName, searchFields);

                // Configurazione semantica (richiede Azure Cognitive Search Standard S1 o superiore)
                try
                {
                    // Verifica se la versione dell'SDK supporta la ricerca semantica
                    if (typeof(SearchIndex).GetProperty("SemanticSearch") != null)
                    {
                        definition.SemanticSearch = new SemanticSearch
                        {
                            Configurations =
                            {
                                new SemanticConfiguration("default", new SemanticPrioritizedFields
                                {
                                    TitleField = new SemanticField("FileName"),
                                    ContentFields = { new SemanticField("Content") }
                                })
                            }
                        };
                    }
                }
                catch (Exception ex)
                {
                    // Se la configurazione semantica fallisce, crea l'indice senza di essa
                    Console.WriteLine($"Avviso: La ricerca semantica non è supportata. Dettaglio: {ex.Message}");
                }

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

            // Configura le opzioni di ricerca base
            var options = new SearchOptions
            {
                Size = request.PageSize,
                Skip = request.PageNumber * request.PageSize,
                IncludeTotalCount = true,
                Select = { "Id", "FileName", "FileUrl", "Content", "Entities", "KeyPhrases", "UploadDate" }
            };

            // Prova a utilizzare la ricerca semantica se supportata
            bool useSemanticSearch = false;

            try
            {
                // Verifica se l'SDK supporta la ricerca semantica 
                var queryTypeProperty = typeof(SearchOptions).GetProperty("QueryType");
                if (queryTypeProperty != null)
                {
                    // Verifica se SearchQueryType ha un valore Semantic
                    if (Enum.IsDefined(typeof(SearchQueryType), "Semantic"))
                    {
                        options.QueryType = SearchQueryType.Semantic;

                        // Imposta le proprietà semantiche tramite reflection
                        var semanticPropertyInfo = typeof(SearchOptions).GetProperty("SemanticConfigurationName");
                        if (semanticPropertyInfo != null)
                        {
                            semanticPropertyInfo.SetValue(options, "default");
                            useSemanticSearch = true;
                        }

                        // Imposta QueryCaption tramite reflection
                        var queryCaptionProperty = typeof(SearchOptions).GetProperty("QueryCaption");
                        if (queryCaptionProperty != null)
                        {
                            var queryCaptionType = Type.GetType("Azure.Search.Documents.Models.QueryCaptionType, Azure.Search.Documents");
                            if (queryCaptionType != null)
                            {
                                var extractiveValue = Enum.Parse(queryCaptionType, "Extractive");
                                queryCaptionProperty.SetValue(options, extractiveValue);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log dell'errore
                Console.WriteLine($"Errore nella configurazione della ricerca semantica: {ex.Message}");
                useSemanticSearch = false;
            }

            // Se la ricerca semantica non è supportata, usa quella standard
            if (!useSemanticSearch)
            {
                options.QueryType = SearchQueryType.Simple;
            }

            // Esegui la ricerca
            SearchResults<DocumentIndex> searchResults;
            try
            {
                searchResults = await searchClient.SearchAsync<DocumentIndex>(request.Query, options);
            }
            catch (Exception ex)
            {
                // Se fallisce la ricerca semantica, prova con quella standard
                options.QueryType = SearchQueryType.Simple;
                // Rimuovi le proprietà semantiche che potrebbero causare problemi
                searchResults = await searchClient.SearchAsync<DocumentIndex>(request.Query, options);
            }

            // Prepara la risposta
            var response = new SearchResponse
            {
                TotalCount = searchResults.TotalCount ?? 0,
                Results = new List<SearchResponse.SearchResult>()
            };

            // Aggiungi i risultati alla risposta
            await foreach (var result in searchResults.GetResultsAsync())
            {
                var searchResult = new SearchResponse.SearchResult
                {
                    Document = result.Document,
                    Score = result.Score ?? 0
                };

                // Aggiungi il caption se disponibile (per ricerca semantica)
                try
                {
                    if (useSemanticSearch && result.GetType().GetProperty("SemanticSearch") != null)
                    {
                        var semanticSearch = result.GetType().GetProperty("SemanticSearch").GetValue(result);
                        if (semanticSearch != null)
                        {
                            var captions = semanticSearch.GetType().GetProperty("Captions").GetValue(semanticSearch) as System.Collections.IList;
                            if (captions != null && captions.Count > 0)
                            {
                                var caption = captions[0];
                                var text = caption.GetType().GetProperty("Text").GetValue(caption) as string;
                                searchResult.Caption = text;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Ignora errori nel recupero dei caption semantici
                    Console.WriteLine($"Errore nel recupero caption: {ex.Message}");
                }

                response.Results.Add(searchResult);
            }

            return response;
        }
    }
}