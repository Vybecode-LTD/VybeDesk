namespace ClaudePM.Services;

/// <summary>Resolves the per-user application data directory.</summary>
public static class Paths
{
    public static string AppDataDir()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClaudePM");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
