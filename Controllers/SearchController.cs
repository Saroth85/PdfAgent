using Microsoft.AspNetCore.Mvc;
using PDFAnalyzerApi.Models;
using PDFAnalyzerApi.Services;
using System;
using System.Threading.Tasks;

namespace PDFAnalyzerApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly ISearchService _searchService;

        public SearchController(ISearchService searchService)
        {
            _searchService = searchService;
        }

        // POST: api/search
        [HttpPost]
        public async Task<IActionResult> SearchDocuments([FromBody] SearchRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new { message = "La query di ricerca non può essere vuota." });
            }

            try
            {
                var response = await _searchService.SearchDocumentsAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Si è verificato un errore durante la ricerca: {ex.Message}" });
            }
        }
    }
}