namespace StorageVisualizer.App.Models;

public sealed class StorageNode
{
    public StorageNode(string name, string path)
    {
        Name = name;
        Path = path;
    }

    public string Name { get; }

    public string Path { get; }

    public long Size { get; set; }

    public List<StorageNode> Children { get; } = [];

    public StorageNodeMetadata Metadata { get; } = new();
}
