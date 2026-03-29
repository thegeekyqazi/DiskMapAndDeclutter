namespace StorageVisualizer.App.Services;

public sealed class ScanSafetyException : Exception
{
    public ScanSafetyException(string message) : base(message)
    {
    }
}
