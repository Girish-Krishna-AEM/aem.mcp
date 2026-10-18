namespace LightningMcpServer.Tests;

internal static class TestPaths
{
    // dotnet test's working directory is the test output folder, not the repo
    // root, so stub-file-backed tests need to walk up to find the real
    // data/informer/... files committed at the repo root.
    public static string ResolveRepoRootPath(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate '{relativePath}' by walking up from {AppContext.BaseDirectory}");
    }
}
