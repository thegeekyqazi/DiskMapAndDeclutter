using StorageVisualizer.App.Configuration;
using StorageVisualizer.App.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? "http://127.0.0.1:5080");

builder.Services.AddProblemDetails();
builder.Services.Configure<StorageScanOptions>(
    builder.Configuration.GetSection(StorageScanOptions.SectionName));
builder.Services.Configure<PrivilegedAgentOptions>(
    builder.Configuration.GetSection(PrivilegedAgentOptions.SectionName));
builder.Services.AddSingleton<InstalledProgramCatalog>();
builder.Services.AddSingleton<FileLockInspector>();
builder.Services.AddSingleton<PrivilegedAgentClient>();
builder.Services.AddSingleton<DirectoryScanner>();
builder.Services.AddSingleton<CleanupRecommendationEngine>();
builder.Services.AddSingleton<SunburstFlattener>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/agent/status", async (PrivilegedAgentClient client, CancellationToken cancellationToken) =>
{
    var status = await client.GetStatusAsync(cancellationToken);
    return Results.Ok(status);
});

app.MapGet("/api/scan", (
    string? targetPath,
    DirectoryScanner scanner,
    CleanupRecommendationEngine recommendationEngine,
    SunburstFlattener flattener) =>
{
    if (string.IsNullOrWhiteSpace(targetPath))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Target path is required",
            detail: "Provide a directory path to scan.");
    }

    try
    {
        var scanResult = scanner.BuildStorageTree(targetPath);
        var recommendations = recommendationEngine.Generate(scanResult.RootNode);
        var enrichedResult = new StorageVisualizer.App.Models.StorageScanResult
        {
            RootNode = scanResult.RootNode,
            Summary = scanResult.Summary,
            Recommendations = recommendations
        };

        return Results.Ok(flattener.Flatten(enrichedResult));
    }
    catch (DirectoryNotFoundException ex)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Directory not found",
            detail: ex.Message);
    }
    catch (ScanSafetyException ex)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Scan blocked by safety guardrail",
            detail: ex.Message);
    }
});

app.MapFallbackToFile("index.html");

app.Run();
