using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PDFAnalyzerAzure
{
    public class Program
    {
        // Configurazione delle chiavi Azure
        private static string cognitiveServicesEndpoint = "https://tuo-resource.cognitiveservices.azure.com/";
        private static string cognitiveServicesKey = "la-tua-chiave-cognitive-services";
        private static string storageConnectionString = "la-tua-connection-string";
        private static string containerName = "documenti-pdf";
        private static string searchServiceEndpoint = "https://tuo-search.search.windows.net";
        private static string searchApiKey = "la-tua-api-key-search";
        private static string searchIndexName = "pdf-index";

        static async Task Main(string[] args)
        {
            Console.WriteLine("PDF Analyzer con Azure - Demo");
            Console.WriteLine("1. Carica PDF su Azure Blob Storage");
            Console.WriteLine("2. Analizza PDF con Form Recognizer");
            Console.WriteLine("3. Esegui query semantiche");
            Console.WriteLine("Seleziona un'opzione (1-3): ");

            var scelta = Console.ReadLine();
            switch (scelta)
            {
                case "1":
                    await CaricaPDF();
                    break;
                case "2":
                    await AnalizzaPDF();
                    break;
                case "3":
                    await EseguiQuerySemantica();
                    break;
                default:
                    Console.WriteLine("Opzione non valida.");
                    break;
            }
        }

        // Carica un PDF su Azure Blob Storage
        private static async Task CaricaPDF()
        {
            Console.WriteLine("Inserisci il percorso del file PDF: ");
            var filePath = Console.ReadLine();

            try
            {
                // Crea il client di Azure Blob Storage
                BlobServiceClient blobServiceClient = new BlobServiceClient(storageConnectionString);
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

                // Crea il container se non esiste
                await containerClient.CreateIfNotExistsAsync();

                // Ottieni il nome del file dal percorso
                string fileName = Path.GetFileName(filePath);
                BlobClient blobClient = containerClient.GetBlobClient(fileName);

                // Carica il file PDF
                using FileStream fileStream = File.OpenRead(filePath);
                await blobClient.UploadAsync(fileStream, true);

                Console.WriteLine($"PDF '{fileName}' caricato con successo!");
                Console.WriteLine($"URL: {blobClient.Uri}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore durante il caricamento: {ex.Message}");
            }
        }

        // Analizza i PDF utilizzando Form Recognizer
        private static async Task AnalizzaPDF()
        {
            try
            {
                // Crea i client
                AzureKeyCredential credential = new AzureKeyCredential(cognitiveServicesKey);
                DocumentAnalysisClient formRecognizerClient = new DocumentAnalysisClient(new Uri(cognitiveServicesEndpoint), credential);
                BlobServiceClient blobServiceClient = new BlobServiceClient(storageConnectionString);
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

                // Ottieni la lista dei blob
                await foreach (var blobItem in containerClient.GetBlobsAsync())
                {
                    BlobClient blobClient = containerClient.GetBlobClient(blobItem.Name);

                    Console.WriteLine($"Analisi del documento: {blobItem.Name}");

                    // Analizza il documento
                    AnalyzeDocumentOperation operation = await formRecognizerClient.AnalyzeDocumentFromUriAsync(
                        WaitUntil.Completed,
                        "prebuilt-document",
                        blobClient.Uri);

                    Azure.AI.FormRecognizer.DocumentAnalysis.AnalyzeResult result = operation.Value;

                    // Estrai il contenuto
                    string documentContent = string.Join(" ", result.Content);

                    // Salva il contenuto estratto in Azure Cognitive Search
                    await SalvaContenutoInSearchIndex(blobItem.Name, blobClient.Uri.ToString(), documentContent, result);

                    Console.WriteLine($"Documento {blobItem.Name} analizzato e indicizzato con successo!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore durante l'analisi: {ex.Message}");
            }
        }

        // Salva il contenuto estratto in Azure Cognitive Search
        private static async Task SalvaContenutoInSearchIndex(string fileName, string fileUrl, string contenuto, Azure.AI.FormRecognizer.DocumentAnalysis.AnalyzeResult analyzeResult)
        {
            // Crea il client di Azure Cognitive Search
            Uri searchEndpoint = new Uri(searchServiceEndpoint);
            AzureKeyCredential searchCredential = new AzureKeyCredential(searchApiKey);
            SearchIndexClient indexClient = new SearchIndexClient(searchEndpoint, searchCredential);

            // Verifica se l'indice esiste, altrimenti crealo
            try
            {
                await indexClient.GetIndexAsync(searchIndexName);
            }
            catch (RequestFailedException)
            {
                // L'indice non esiste, crealo
                FieldBuilder fieldBuilder = new FieldBuilder();
                var searchFields = fieldBuilder.Build(typeof(DocumentIndex));

                var definition = new SearchIndex(searchIndexName, searchFields);

                // Abilita la ricerca semantica
                definition.SemanticSearch = new SemanticSearch
                {
                    Configurations = { new SemanticConfiguration("default", new SemanticPrioritizedFields
                    {
                        TitleField = new ("fileName" ),
                        //ContentFields = { new SemanticField { FieldName = "content" } },
                        //KeywordsFields = { new SemanticField { FieldName = "entities" } }

                    })}
                };

                await indexClient.CreateOrUpdateIndexAsync(definition);
            }

            // Crea il client di ricerca
            SearchClient searchClient = new SearchClient(searchEndpoint, searchIndexName, searchCredential);

            // Estrai entità e concetti chiave
            var entities = analyzeResult.Documents.Select(e => e.Fields + ": " + e.Fields).ToList();
            var keyPhrases = analyzeResult.KeyValuePairs.ToList();

            // Crea il documento da indicizzare
            var document = new DocumentIndex
            {
                Id = fileName,
                FileName = fileName,
                FileUrl = fileUrl,
                Content = contenuto,
                Entities = string.Join(", ", entities),
                KeyPhrases = string.Join(", ", keyPhrases),
                UploadDate = DateTime.UtcNow
            };

            // Carica il documento nell'indice
            await searchClient.IndexDocumentsAsync(IndexDocumentsBatch.Upload(new[] { document }));
        }

        // Esegui query semantiche sull'indice
        private static async Task EseguiQuerySemantica()
        {
            Console.WriteLine("Inserisci la tua query: ");
            var queryText = Console.ReadLine();

            try
            {
                // Crea il client di ricerca
                Uri searchEndpoint = new Uri(searchServiceEndpoint);
                AzureKeyCredential searchCredential = new AzureKeyCredential(searchApiKey);
                SearchClient searchClient = new SearchClient(searchEndpoint, searchIndexName, searchCredential);

                // Opzioni per la ricerca semantica
                SearchOptions options = new SearchOptions
                {
                    QueryType = SearchQueryType.Semantic,
                    //SemanticConfigurationName = "default",
                    //QueryLanguage = "it-IT",
                    Size = 10,
                    IncludeTotalCount = true,
                    Select = { "id", "fileName", "fileUrl", "content", "entities", "keyPhrases" },
                    //QueryCaption = new QueryCaption(QueryCaptionType.Extractive)
                };

                // Esegui la ricerca
                SearchResults<DocumentIndex> results = await searchClient.SearchAsync<DocumentIndex>(queryText, options);

                Console.WriteLine($"Trovati {results.TotalCount} risultati per la query: '{queryText}'");

                // Visualizza i risultati
                await foreach (var result in results.GetResultsAsync())
                {
                    Console.WriteLine($"\nDocumento: {result.Document.FileName}");
                    Console.WriteLine($"URL: {result.Document.FileUrl}");

                    if (result.SemanticSearch.Captions != null && result.SemanticSearch.Captions.Count > 0)
                    {
                        Console.WriteLine($"Riassunto: {result.SemanticSearch.Captions[0].Text}");
                        Console.WriteLine($"Punteggio: {result.SemanticSearch.Captions[0].Highlights}");
                    }

                    Console.WriteLine($"Entità: {result.Document.Entities}");
                    Console.WriteLine($"Frasi chiave: {result.Document.KeyPhrases}");
                    Console.WriteLine("-----------------------------------");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore durante la ricerca: {ex.Message}");
            }
        }
    }

    // Classe di modello per l'indice di ricerca
    public class DocumentIndex
    {
        [SimpleField(IsKey = true, IsFilterable = true)]
        public string Id { get; set; }

        [SearchableField(IsSortable = true)]
        public string FileName { get; set; }

        [SimpleField(IsFilterable = false)]
        public string FileUrl { get; set; }

        [SearchableField(IsFilterable = true, AnalyzerName = "it.lucene")]
        public string Content { get; set; }

        [SearchableField(IsFilterable = true, AnalyzerName = "it.lucene")]
        public string Entities { get; set; }

        [SearchableField(IsFilterable = true, AnalyzerName = "it.lucene")]
        public string KeyPhrases { get; set; }

        [SimpleField(IsFilterable = true, IsSortable = true)]
        public DateTime UploadDate { get; set; }
    }
}