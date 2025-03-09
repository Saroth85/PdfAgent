using Azure.AI.DocumentIntelligence;
using Azure;
using Azure.Search.Documents;
using Azure.Storage.Blobs;
using Azure.AI.OpenAI;
using PdfAgent.Controllers;

var builder = WebApplication.CreateBuilder(args);

// Add controllers
builder.Services.AddControllers();

// Configure Swagger/OpenAPI (opzionale, utile per test API)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Registrazione Servizi
builder.Services.AddSingleton<PdfIndexerService>();

// Configurazioni aggiuntive (opzionale, per semplificare accesso IConfiguration)
var config = builder.Configuration;

// Build dell'applicazione
var app = builder.Build();

// Configure HTTP request pipeline (middleware)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
