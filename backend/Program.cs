using FigmaDiffBackend.Data;
using FigmaDiffBackend.Models;
using FigmaDiffBackend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<DiffContext>(options =>
    options.UseSqlite("Data Source=diffs.db"));

builder.Services.AddSingleton<IStorageService, LocalStorageService>();
builder.Services.AddScoped<DiffService>();
builder.Services.AddHttpClient<JiraService>();
builder.Services.AddHttpClient<SlackService>();

// Enable CORS for Figma Plugin (null origin or *)
builder.Services.AddCors(options =>
{
    options.AddPolicy("FigmaPolicy", b => b
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader());
});

var app = builder.Build();

// Migrate/Create DB
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DiffContext>();
    db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("FigmaPolicy");
// Ensure storage directory exists
var storagePath = Path.Combine(builder.Environment.ContentRootPath, "storage");
if (!Directory.Exists(storagePath))
{
    Directory.CreateDirectory(storagePath);
}

// Serve static files from 'wwwroot' folder
app.UseStaticFiles();

// Serve static files from 'storage' folder
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(storagePath),
    RequestPath = "/storage"
});

// Health
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", time = DateTime.UtcNow }));

// 1. Capture Baseline
app.MapPost("/api/baselines", async (
    [FromForm] IFormFile file,
    [FromForm] string metadataJson,
    [FromServices] DiffContext db,
    [FromServices] IStorageService storage) =>
{
    var meta = JsonSerializer.Deserialize<Baseline>(metadataJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    
    if (meta == null) return Results.BadRequest("Invalid metadata");
    if (file == null || file.Length == 0) return Results.BadRequest("No image");

    using var stream = file.OpenReadStream();
    var path = await storage.SaveImageAsync(stream, "baseline");
    
    meta.ImagePath = path;
    meta.CreatedAt = DateTime.UtcNow;
    meta.Id = Guid.NewGuid().ToString();
    
    db.Baselines.Add(meta);
    await db.SaveChangesAsync();
    
    return Results.Ok(new { baselineId = meta.Id, message = "Baseline captured" });
}).DisableAntiforgery();

// 2. Compare & Publish
app.MapPost("/api/comparisons", async (
    [FromForm] IFormFile file,
    [FromForm] string metadataJson,
    [FromServices] DiffContext db,
    [FromServices] IStorageService storage,
    [FromServices] DiffService diffService,
    [FromServices] JiraService jira,
    [FromServices] SlackService slack,
    HttpContext http) =>
{
    var meta = JsonSerializer.Deserialize<Comparison>(metadataJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    if (meta == null || string.IsNullOrEmpty(meta.BaselineId)) return Results.BadRequest("Invalid metadata or missing baselineId");
    
    var baseline = await db.Baselines.FindAsync(meta.BaselineId);
    if (baseline == null) return Results.NotFound("Baseline not found");

    using var afterStream = file.OpenReadStream();
    var afterPath = await storage.SaveImageAsync(afterStream, "after");
    
    var beforeBytes = await storage.GetImageBytesAsync(baseline.ImagePath);
    using var beforeStream = new MemoryStream(beforeBytes);
    var afterBytes = await storage.GetImageBytesAsync(afterPath);
    using var afterStreamForDiff = new MemoryStream(afterBytes);

    var diffResult = await diffService.CompareAsync(beforeStream, afterStreamForDiff);

    meta.Id = Guid.NewGuid().ToString();
    meta.AfterImagePath = afterPath;
    meta.ChangedPercent = diffResult.ChangedPercent;
    meta.ChangedPixels = diffResult.ChangedPixels;
    meta.IssueKey = baseline.IssueKey;
    meta.CreatedAt = DateTime.UtcNow;
    
    db.Comparisons.Add(meta);
    await db.SaveChangesAsync();

    var baseUrl = $"{http.Request.Scheme}://{http.Request.Host}";
    var reportUrl = $"{baseUrl}/reports/{meta.Id}";

    if (!string.IsNullOrEmpty(meta.IssueKey))
    {
        var comment = $"Visual Diff Published for {baseline.NodeName}.\nChange: {meta.ChangedPercent:F2}%\n[View Report|{reportUrl}]";
        meta.JiraCommentId = await jira.AddCommentAsync(meta.IssueKey, comment);
    }

    if (!string.IsNullOrEmpty(meta.SlackChannel) && !string.IsNullOrEmpty(meta.IssueKey))
    {
         var msg = $"*Visual Diff Update*\nIssue: {meta.IssueKey}\nNode: {baseline.NodeName}\nChange: {meta.ChangedPercent:F2}%\n<{reportUrl}|Open Report>";
         meta.SlackTs = await slack.PostDiffMessageAsync(meta.SlackChannel, meta.IssueKey, msg);
    }
    
    await db.SaveChangesAsync();

    return Results.Ok(new 
    { 
        reportUrl, 
        changedPercent = meta.ChangedPercent
    });
}).DisableAntiforgery();

app.MapGet("/reports/{id}", async (string id, DiffContext db, IWebHostEnvironment env) =>
{
    var comp = await db.Comparisons.FindAsync(id);
    if (comp == null) return Results.NotFound();
    
    var baseline = await db.Baselines.FindAsync(comp.BaselineId);
    
    var templatePath = Path.Combine(env.ContentRootPath, "Templates", "report.html");
    if (!File.Exists(templatePath)) return Results.Problem("Template not found");

    var html = await File.ReadAllTextAsync(templatePath);

    html = html.Replace("{{IssueKey}}", comp.IssueKey ?? "N/A")
               .Replace("{{NodeName}}", baseline?.NodeName ?? "Unknown")
               .Replace("{{Date}}", comp.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"))
               .Replace("{{DiffPercent}}", comp.ChangedPercent.ToString("F2"))
               .Replace("{{MetricClass}}", comp.ChangedPercent > 0 ? "metric--changed" : "metric--unchanged")
               .Replace("{{BaselinePath}}", baseline?.ImagePath ?? "")
               .Replace("{{AfterPath}}", comp.AfterImagePath ?? "");
    
    return Results.Content(html, "text/html");
});

app.Run();
