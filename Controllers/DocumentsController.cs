using Microsoft.AspNetCore.Mvc;
using PDFAnalyzerApi.Models;
using PDFAnalyzerApi.Services;
using System;
using System.Threading.Tasks;

namespace PDFAnalyzerApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly IStorageService _storageService;
        private readonly IAnalysisService _analysisService;

        public DocumentsController(IStorageService storageService, IAnalysisService analysisService)
        {
            _storageService = storageService;
            _analysisService = analysisService;
        }

        // GET: api/documents
        [HttpGet]
        public async Task<IActionResult> GetDocuments()
        {
            try
            {
                var documents = await _storageService.GetAllDocumentsAsync();
                return Ok(documents);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Si è verificato un errore: {ex.Message}" });
            }
        }

        // POST: api/documents/upload
        [HttpPost("upload")]
        public async Task<IActionResult> UploadDocument(IFormFile file)
        {
            try
            {
                var response = await _storageService.UploadDocumentAsync(file);

                if (!response.Success)
                {
                    return BadRequest(response);
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new UploadResponse
                {
                    Success = false,
                    Message = $"Si è verificato un errore durante il caricamento: {ex.Message}"
                });
            }
        }

        // POST: api/documents/analyze/{fileName}
        [HttpPost("analyze/{fileName}")]
        public async Task<IActionResult> AnalyzeDocument(string fileName)
        {
            try
            {
                // Verifica se il documento esiste
                if (!await _storageService.DocumentExistsAsync(fileName))
                {
                    return NotFound(new AnalysisResponse
                    {
                        FileName = fileName,
                        Success = false,
                        Message = "Documento non trovato."
                    });
                }

                // Ottieni l'URL del documento
                var fileUrl = await _storageService.GetDocumentUrlAsync(fileName);

                // Analizza il documento
                var response = await _analysisService.AnalyzeDocumentAsync(fileName, fileUrl);

                if (!response.Success)
                {
                    return BadRequest(response);
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new AnalysisResponse
                {
                    FileName = fileName,
                    Success = false,
                    Message = $"Si è verificato un errore durante l'analisi: {ex.Message}"
                });
            }
        }
    }
}