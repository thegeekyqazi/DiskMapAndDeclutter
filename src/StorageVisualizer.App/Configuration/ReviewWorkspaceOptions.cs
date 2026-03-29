namespace StorageVisualizer.App.Configuration;

public sealed class ReviewWorkspaceOptions
{
    public const string SectionName = "ReviewWorkspace";

    public string DataDirectory { get; init; } = "App_Data\\reviews";

    public int MaxEntriesPerRoot { get; init; } = 1000;

    public int MaxNoteLength { get; init; } = 800;
}
