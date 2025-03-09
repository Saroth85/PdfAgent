using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;

namespace PdfAgent.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PdfController : ControllerBase
    {
        private readonly BlobContainerClient _blobContainer;

        public PdfController(IConfiguration config)
        {
            _blobContainer = new BlobContainerClient(
                config["Azure:BlobStorageConnectionString"],
                config["Azure:BlobContainerName"]);
            _blobContainer.CreateIfNotExists();
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadPdf(IFormFile file)
        {
            var blobClient = _blobContainer.GetBlobClient(file.FileName);
            await blobClient.UploadAsync(file.OpenReadStream(), overwrite: true);

            return Ok(new { blobUrl = blobClient.Uri });
        }
    }
}