namespace Dikerma.Windows.Services;

public sealed class AssetService
{
    public AssetService() => AppPaths.Ensure();

    public string Import(string sourcePath, string category)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Selected file was not found.", sourcePath);
        }

        var categoryPath = Path.Combine(AppPaths.Assets, Sanitize(category));
        Directory.CreateDirectory(categoryPath);

        var extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension)) extension = ".bin";

        var destination = Path.Combine(categoryPath, $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}");
        File.Copy(sourcePath, destination, overwrite: false);
        return destination;
    }

    public string CreateOutputPath(string category, string extension)
    {
        var categoryPath = Path.Combine(AppPaths.Assets, Sanitize(category));
        Directory.CreateDirectory(categoryPath);
        if (!extension.StartsWith('.')) extension = "." + extension;
        return Path.Combine(categoryPath, $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}");
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Where(c => !invalid.Contains(c))).Trim().Replace(' ', '-').ToLowerInvariant();
    }
}
