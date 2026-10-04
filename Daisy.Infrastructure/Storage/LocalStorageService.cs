using Daisy.Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Daisy.Infrastructure.Storage;

/// <summary>Configuration section "Storage" (API_DESIGN §11).</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Folder for uploaded files, outside the API code folder. Relative paths are resolved from the content root.</summary>
    public string RootPath { get; set; } = "../storage";

    /// <summary>URL prefix under which the API serves the files.</summary>
    public string PublicBasePath { get; set; } = "/uploads";
}

/// <summary>Stores files on the local file system with generated names (API_DESIGN §10).</summary>
public sealed class LocalStorageService : IStorageService
{
    private readonly string _root;
    private readonly string _publicBase;

    public LocalStorageService(IOptions<StorageOptions> options, IHostEnvironment environment)
    {
        _root = ResolveRoot(options.Value, environment);
        _publicBase = "/" + options.Value.PublicBasePath.Trim('/');
        Directory.CreateDirectory(_root);
    }

    public static string ResolveRoot(StorageOptions options, IHostEnvironment environment) =>
        Path.GetFullPath(Path.IsPathRooted(options.RootPath)
            ? options.RootPath
            : Path.Combine(environment.ContentRootPath, options.RootPath));

    public async Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken cancellationToken = default)
    {
        var safeFolder = string.Join('/', folder.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => string.Concat(part.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'))));
        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var directory = Path.Combine(_root, safeFolder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, fileName);
        await using (var file = File.Create(path))
        {
            await content.CopyToAsync(file, cancellationToken);
        }

        return $"{_publicBase}/{safeFolder}/{fileName}";
    }

    public Task DeleteAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!url.StartsWith(_publicBase + "/", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        var relative = url[(_publicBase.Length + 1)..].Replace('/', Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(_root, relative));
        if (path.StartsWith(_root, StringComparison.OrdinalIgnoreCase) && File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }
}
