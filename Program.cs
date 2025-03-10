using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure.Search.Documents.Indexes;
using Azure.Storage.Blobs;
using Microsoft.OpenApi.Models;
using PdfAgent.Services;


var builder = WebApplication.CreateBuilder(args);

// Aggiungi servizi al container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "PDF Agent API", Version = "v1" });
});

// Configura CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// Aggiungi servizi di Azure come Singleton
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var endpoint = config["Azure:CognitiveServices:Endpoint"];
    var key = config["Azure:CognitiveServices:Key"];
    return new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(key));
});

builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new BlobServiceClient(config["Azure:Storage:ConnectionString"]);
});

builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var endpoint = config["Azure:Search:Endpoint"];
    var key = config["Azure:Search:ApiKey"];
    return new SearchIndexClient(new Uri(endpoint), new AzureKeyCredential(key));
});

// Registra i servizi personalizzati
builder.Services.AddScoped<IStorageService, StorageService>();
builder.Services.AddScoped<IAnalysisService, AnalysisService>();
builder.Services.AddScoped<ISearchService, SearchService>();

var app = builder.Build();

// Configura la pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();