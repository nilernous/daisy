namespace Daisy.Application.Interfaces;

/// <summary>Stores uploaded files and returns their public relative URL, e.g. /uploads/products/1/x.webp.</summary>
public interface IStorageService
{
    Task<string> SaveAsync(
        Stream content,
        string folder,
        string extension,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string url, CancellationToken cancellationToken = default);
}
