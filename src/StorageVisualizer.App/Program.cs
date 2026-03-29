using StorageVisualizer.App.Configuration;
using StorageVisualizer.App.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? "http://127.0.0.1:5080");

builder.Services.AddProblemDetails();
builder.Services.Configure<StorageScanOptions>(
    builder.Configuration.GetSection(StorageScanOptions.SectionName));
builder.Services.Configure<PrivilegedAgentOptions>(
    builder.Configuration.GetSection(PrivilegedAgentOptions.SectionName));
builder.Services.Configure<ReviewWorkspaceOptions>(
    builder.Configuration.GetSection(ReviewWorkspaceOptions.SectionName));
builder.Services.AddSingleton<ScanPathGuard>();
builder.Services.AddSingleton<InstalledProgramCatalog>();
builder.Services.AddSingleton<FileLockInspector>();
builder.Services.AddSingleton<PrivilegedAgentClient>();
builder.Services.AddSingleton<SafeFileEnumerator>();
builder.Services.AddSingleton<DirectoryScanner>();
builder.Services.AddSingleton<CleanupRecommendationEngine>();
builder.Services.AddSingleton<DuplicateFileAnalysisService>();
builder.Services.AddSingleton<StaleFileAnalysisService>();
builder.Services.AddSingleton<FileInspectionService>();
builder.Services.AddSingleton<SunburstFlattener>();
builder.Services.AddSingleton<ReviewWorkspaceStore>();

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

app.MapGet("/api/review-state", async (
    string? rootPath,
    ReviewWorkspaceStore store,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(rootPath))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Root path is required",
            detail: "Provide the scan root path to load review notes.");
    }

    try
    {
        var state = await store.LoadAsync(rootPath, cancellationToken);
        return Results.Ok(state);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Review state request is invalid",
            detail: ex.Message);
    }
});

app.MapPost("/api/review-state", async (
    StorageVisualizer.App.Models.SaveReviewStateRequest request,
    ReviewWorkspaceStore store,
    CancellationToken cancellationToken) =>
{
    try
    {
        var state = await store.SaveAsync(request, cancellationToken);
        return Results.Ok(state);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Review state could not be saved",
            detail: ex.Message);
    }
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

app.MapGet("/api/analysis/duplicates", async (
    string? rootPath,
    DuplicateFileAnalysisService duplicateFileAnalysisService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(rootPath))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Root path is required",
            detail: "Provide the scan root path before running duplicate analysis.");
    }

    try
    {
        var response = await duplicateFileAnalysisService.AnalyzeAsync(rootPath, cancellationToken);
        return Results.Ok(response);
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
            title: "Analysis blocked by safety guardrail",
            detail: ex.Message);
    }
});

app.MapGet("/api/analysis/stale-files", async (
    string? rootPath,
    StaleFileAnalysisService staleFileAnalysisService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(rootPath))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Root path is required",
            detail: "Provide the scan root path before running stale-file analysis.");
    }

    try
    {
        var response = await staleFileAnalysisService.AnalyzeAsync(rootPath, cancellationToken);
        return Results.Ok(response);
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
            title: "Analysis blocked by safety guardrail",
            detail: ex.Message);
    }
});

app.MapGet("/api/analysis/file-inspect", async (
    string? rootPath,
    string? targetPath,
    FileInspectionService fileInspectionService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(targetPath))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Root path and target path are required",
            detail: "Provide both the scan root and the file path to inspect.");
    }

    try
    {
        var response = await fileInspectionService.InspectAsync(rootPath, targetPath, cancellationToken);
        return Results.Ok(response);
    }
    catch (FileNotFoundException ex)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "File not found",
            detail: ex.Message);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Inspection request is invalid",
            detail: ex.Message);
    }
    catch (ScanSafetyException ex)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Inspection blocked by safety guardrail",
            detail: ex.Message);
    }
});

app.MapFallbackToFile("index.html");

app.Run();
