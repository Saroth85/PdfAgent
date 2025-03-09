using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using PDFAnalyzerApi.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PDFAnalyzerApi.Services
{
    public interface IStorageService
    {
        Task<List<DocumentMetadata>> GetAllDocumentsAsync();
        Task<UploadResponse> UploadDocumentAsync(IFormFile file);
        Task<string> GetDocumentUrlAsync(string fileName);
        Task<bool> DocumentExistsAsync(string fileName);
    }

    public class StorageService : IStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly IConfiguration _configuration;
        private readonly string _containerName;

        public StorageService(BlobServiceClient blobServiceClient, IConfiguration configuration)
        {
            _blobServiceClient = blobServiceClient;
            _configuration = configuration;
            _containerName = _configuration["Azure:Storage:ContainerName"];
        }

        public async Task<List<DocumentMetadata>> GetAllDocumentsAsync()
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync();

            var documents = new List<DocumentMetadata>();

            await foreach (var blobItem in containerClient.GetBlobsAsync())
            {
                var blobClient = containerClient.GetBlobClient(blobItem.Name);
                documents.Add(new DocumentMetadata
                {
                    Name = blobItem.Name,
                    Url = blobClient.Uri.ToString(),
                    Size = blobItem.Properties.ContentLength ?? 0,
                    LastModified = blobItem.Properties.LastModified
                });
            }

            return documents;
        }

        public async Task<UploadResponse> UploadDocumentAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return new UploadResponse
                {
                    Success = false,
                    Message = "Nessun file fornito o file vuoto."
                };
            }

            if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                return new UploadResponse
                {
                    Success = false,
                    Message = "Il file deve essere un PDF."
                };
            }

            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync();

            // Genera un nome univoco per il blob
            string fileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}-{Guid.NewGuid()}.pdf";
            BlobClient blobClient = containerClient.GetBlobClient(fileName);

            // Carica il file su Azure Blob Storage
            using (var stream = file.OpenReadStream())
            {
                await blobClient.UploadAsync(stream, new BlobHttpHeaders
                {
                    ContentType = "application/pdf"
                });
            }

            return new UploadResponse
            {
                FileName = fileName,
                FileUrl = blobClient.Uri.ToString(),
                Success = true,
                Message = "File caricato con successo."
            };
        }

        public async Task<string> GetDocumentUrlAsync(string fileName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(fileName);

            if (await blobClient.ExistsAsync())
            {
                return blobClient.Uri.ToString();
            }

            return null;
        }

        public async Task<bool> DocumentExistsAsync(string fileName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(fileName);
            return await blobClient.ExistsAsync();
        }
    }
}