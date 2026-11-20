namespace Wms.ArchitectureTests;

internal static class RepoPaths
{
    /// <summary>Repository root: the first folder above the test binaries that holds global.json.</summary>
    public static string Root { get; } = FindRoot();

    public static string Source => Path.Combine(Root, "src");

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root (global.json) not found.");
    }
}
