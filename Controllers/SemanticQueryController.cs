using Azure.Search.Documents.Models;
using Azure.Search.Documents;
using Azure;
using Microsoft.AspNetCore.Mvc;
using OpenAI.Chat;
using OpenAI;

namespace PdfAgent.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SemanticQueryController : ControllerBase
    {
        private readonly SearchClient _searchClient;
        private readonly OpenAIClient _openAI;
        private readonly string _model;

        public SemanticQueryController(IConfiguration config)
        {
            _searchClient = new SearchClient(
                new Uri(config["Azure:AISearchEndpoint"]),
                "pdf-index",
                new AzureKeyCredential(config["Azure:AISearchKey"]));

            _openAI = new OpenAIClient(
                new Uri(config["Azure:OpenAIEndpoint"]),
                new AzureKeyCredential(config["Azure:OpenAIKey"]));

            _model = config["Azure:OpenAIModel"];
        }

        [HttpGet("ask")]
        public async Task<IActionResult> Ask(string question)
        {
            var results = await _searchClient.SearchAsync<SearchDocument>(question, new SearchOptions
            {
                QueryType = SearchQueryType.Semantic,
                SemanticConfigurationName = "semanticConfig",
                QueryLanguage = "it",
                Top = 3
            });

            var context = string.Join("\n", results.Value.GetResults().Select(r => r.Document["content"]));

            var completion = await _openAI.GetChatCompletionsAsync(_model, new ChatCompletionsOptions
            {
                Messages = {
                new ChatRequestSystemMessage("Rispondi usando il contesto fornito."),
                new ChatRequestUserMessage($"Contesto:\n{context}\n\nDomanda: {question}")
            }
            });

            return Ok(completion.Value.Choices[0].Message.Content);
        }
    }
}