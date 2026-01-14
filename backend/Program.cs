using FigmaDiffBackend.Data;
using FigmaDiffBackend.Models;
using FigmaDiffBackend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

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
    // Parse metadata
    // metadata: {issueKey, nodeId, nodeName, fileKey?, pageName, user, structureJson?}
    // For MVP, just accept as many fields as needed or parse JSON.
    // Let's assume metadataJson contains the fields.
    var meta = System.Text.Json.JsonSerializer.Deserialize<Baseline>(metadataJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    
    if (meta == null) return Results.BadRequest("Invalid metadata");
    if (file == null || file.Length == 0) return Results.BadRequest("No image");

    using var stream = file.OpenReadStream();
    var path = await storage.SaveImageAsync(stream, "baseline");
    
    meta.ImagePath = path;
    meta.CreatedAt = DateTime.UtcNow;
    meta.Id = Guid.NewGuid().ToString(); // Ensure new ID
    
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
    var meta = System.Text.Json.JsonSerializer.Deserialize<Comparison>(metadataJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    if (meta == null || string.IsNullOrEmpty(meta.BaselineId)) return Results.BadRequest("Invalid metadata or missing baselineId");
    
    var baseline = await db.Baselines.FindAsync(meta.BaselineId);
    if (baseline == null) return Results.NotFound("Baseline not found");

    // 1. Save After Image
    using var afterStream = file.OpenReadStream();
    var afterPath = await storage.SaveImageAsync(afterStream, "after");
    
    // 2. Generate Diff
    var beforeBytes = await storage.GetImageBytesAsync(baseline.ImagePath);
    using var beforeStream = new MemoryStream(beforeBytes);
    // Re-open after stream from file to ensure clean read or CopyTo memory first
    // Since we saved it, let's read it back or use Copy
    // Safe way: Read both from storage provided paths
    var afterBytes = await storage.GetImageBytesAsync(afterPath);
    using var afterStreamForDiff = new MemoryStream(afterBytes);

    var diffResult = await diffService.CompareAsync(beforeStream, afterStreamForDiff);
    var diffPath = "";
    if (diffResult.DiffImage != null)
    {
         diffPath = await storage.SaveImageAsync(diffResult.DiffImage, "diff");
    }

    // 3. Save Comparison
    meta.Id = Guid.NewGuid().ToString();
    meta.AfterImagePath = afterPath;
    meta.DiffImagePath = diffPath;
    meta.ChangedPercent = diffResult.ChangedPercent;
    meta.ChangedPixels = diffResult.ChangedPixels;
    meta.CreatedAt = DateTime.UtcNow;
    meta.IssueKey = baseline.IssueKey; // Inherit if not provided, or used from input
    
    db.Comparisons.Add(meta);
    await db.SaveChangesAsync();

    // 4. Generate Report URL
    // Assume Host is accessible
    var baseUrl = $"{http.Request.Scheme}://{http.Request.Host}";
    var reportUrl = $"{baseUrl}/reports/{meta.Id}";

    // 5. Integrations
    var errors = new List<string>();
    
    // Jira
    if (!string.IsNullOrEmpty(meta.IssueKey))
    {
        var comment = $"Visual Diff Published for {baseline.NodeName}.\nChange: {meta.ChangedPercent:F2}%\n[View Report|{reportUrl}]";
        meta.JiraCommentId = await jira.AddCommentAsync(meta.IssueKey, comment);
        
        // Upload Diff Image to Jira?
        if (diffResult.DiffImage != null)
        {
            diffResult.DiffImage.Position = 0;
            await jira.UploadAttachmentAsync(meta.IssueKey, diffResult.DiffImage, "diff.png");
        }
    }

    // Slack
    if (!string.IsNullOrEmpty(meta.SlackChannel) && !string.IsNullOrEmpty(meta.IssueKey))
    {
         var msg = $"*Visual Diff Update*\nIssue: {meta.IssueKey}\nNode: {baseline.NodeName}\nChange: {meta.ChangedPercent:F2}%\n<{reportUrl}|Open Report>";
         meta.SlackTs = await slack.PostDiffMessageAsync(meta.SlackChannel, meta.IssueKey, msg);
    }
    
    await db.SaveChangesAsync();

    return Results.Ok(new 
    { 
        reportUrl, 
        changedPercent = meta.ChangedPercent,
        jiraCommentId = meta.JiraCommentId,
        slackTs = meta.SlackTs
    });
}).DisableAntiforgery();

// 3. Get Report
app.MapGet("/reports/{id}", async (string id, DiffContext db) =>
{
    var comp = await db.Comparisons.FindAsync(id);
    if (comp == null) return Results.NotFound();
    
    var baseline = await db.Baselines.FindAsync(comp.BaselineId);
    
    // Simple HTML
    var html = $@"
    <!DOCTYPE html>
    <html>
    <head>
        <title>Diff Report {comp.IssueKey}</title>
        <style>
            body {{ font-family: sans-serif; padding: 20px; }}
            .container {{ display: flex; gap: 20px; }}
            .img-box {{ border: 1px solid #ccc; padding: 10px; }}
            img {{ max-width: 100%; border: 1px solid #eee; }}
            .header {{ margin-bottom: 20px; }}
            .metric {{ font-size: 1.5em; font-weight: bold; color: {(comp.ChangedPercent > 0 ? "red" : "green")}; }}
        </style>
    </head>
    <body>
        <div class='header'>
            <h1>Visual Comparison Report</h1>
            <p><strong>Issue:</strong> {comp.IssueKey}</p>
            <p><strong>Node:</strong> {baseline?.NodeName}</p>
            <p><strong>Date:</strong> {comp.CreatedAt}</p>
            <p class='metric'>Difference: {comp.ChangedPercent:F2}%</p>
        </div>
        <div class='container'>
            <div class='img-box'>
                <h3>Baseline</h3>
                <img src='/storage/{baseline?.ImagePath}' />
            </div>
            <div class='img-box'>
                <h3>After</h3>
                <img src='/storage/{comp.AfterImagePath}' />
            </div>
            <div class='img-box'>
                <h3>Diff</h3>
                <img src='/storage/{comp.DiffImagePath}' />
            </div>
        </div>
    </body>
    </html>";
    
    return Results.Content(html, "text/html");
});

app.Run();
