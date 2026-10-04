using Daisy.Application.Common;
using Daisy.Application.Interfaces;
using Daisy.Domain.Common;
using Daisy.Domain.Entities;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Daisy.Application.Features.Products;

/// <summary>UC-48 Manage Images: one thumbnail per product, ordered gallery, link to a variant (BR-16).</summary>
public sealed class ProductImageService
{
    private readonly IApplicationDbContext _db;
    private readonly IStorageService _storage;
    private readonly UploadOptions _uploads;

    public ProductImageService(IApplicationDbContext db, IStorageService storage, IOptions<UploadOptions> uploads)
    {
        _db = db;
        _storage = storage;
        _uploads = uploads.Value;
    }

    public async Task<IReadOnlyList<ProductImageDto>> UploadAsync(int productId, IReadOnlyList<FileUpload> files, CancellationToken ct)
    {
        if (files.Count == 0 || files.Count > _uploads.MaxFilesPerUpload)
        {
            throw new ValidationException([new ValidationFailure("files", $"Upload between 1 and {_uploads.MaxFilesPerUpload} images.")]);
        }

        if (!await _db.Products.AnyAsync(p => p.ProductId == productId, ct))
        {
            throw new NotFoundException("Product not found.");
        }

        var extensions = new List<string>();
        foreach (var file in files)
        {
            extensions.Add(await ImageFormat.EnsureImageAsync(file, _uploads.MaxImageBytes, ct));
        }

        var existing = await _db.ProductImages.Where(i => i.ProductId == productId).ToListAsync(ct);
        var nextOrder = existing.Count == 0 ? 0 : existing.Max(i => i.SortOrder) + 1;
        var hasThumbnail = existing.Any(i => i.IsThumbnail);
        var created = new List<ProductImage>();

        for (var i = 0; i < files.Count; i++)
        {
            await using var stream = files[i].OpenReadStream();
            var url = await _storage.SaveAsync(stream, $"products/{productId}", extensions[i], ct);
            var image = new ProductImage
            {
                ProductId = productId,
                ImageUrl = url,
                SortOrder = nextOrder++,
                IsThumbnail = !hasThumbnail && i == 0,
            };
            created.Add(image);
            _db.ProductImages.Add(image);
        }

        await _db.SaveChangesAsync(ct);
        return created.Select(i => ProductService.ToImageDto(i, new Dictionary<int, string?>())).ToList();
    }

    /// <summary>Clears the previous thumbnail in the same transaction (BR-16).</summary>
    public async Task SetThumbnailAsync(int imageId, CancellationToken ct)
    {
        var image = await FindAsync(imageId, ct);
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await _db.ProductImages
            .Where(i => i.ProductId == image.ProductId && i.ImageId != imageId && i.IsThumbnail)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.IsThumbnail, false), ct);
        image.IsThumbnail = true;
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    /// <summary>The ids must be exactly the product's images.</summary>
    public async Task ReorderAsync(int productId, ImageOrderRequest request, CancellationToken ct)
    {
        if (!await _db.Products.AnyAsync(p => p.ProductId == productId, ct))
        {
            throw new NotFoundException("Product not found.");
        }

        var images = await _db.ProductImages.Where(i => i.ProductId == productId).ToListAsync(ct);
        var ids = request.ImageIds ?? [];
        if (ids.Count != images.Count || ids.Distinct().Count() != ids.Count || !images.All(i => ids.Contains(i.ImageId)))
        {
            throw new ValidationException([new ValidationFailure("imageIds", "Image ids must list every image of the product exactly once.")]);
        }

        foreach (var image in images)
        {
            image.SortOrder = ids.ToList().IndexOf(image.ImageId);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task SetVariantAsync(int imageId, ImageVariantRequest request, CancellationToken ct)
    {
        var image = await FindAsync(imageId, ct);
        if (request.VariantId is int variantId
            && !await _db.ProductVariants.AnyAsync(v => v.VariantId == variantId && v.ProductId == image.ProductId, ct))
        {
            throw new ValidationException([new ValidationFailure("variantId", "The variant must belong to the same product.")]);
        }

        image.VariantId = request.VariantId;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Deletes the image. If it was the thumbnail, the next image becomes the thumbnail.
    /// The file is kept while an order snapshot still references it (BR-28).
    /// </summary>
    public async Task DeleteAsync(int imageId, CancellationToken ct)
    {
        var image = await FindAsync(imageId, ct);
        _db.ProductImages.Remove(image);

        if (image.IsThumbnail)
        {
            var next = await _db.ProductImages
                .Where(i => i.ProductId == image.ProductId && i.ImageId != imageId)
                .OrderBy(i => i.SortOrder).ThenBy(i => i.ImageId)
                .FirstOrDefaultAsync(ct);
            if (next is not null)
            {
                next.IsThumbnail = true;
            }
        }

        await _db.SaveChangesAsync(ct);

        var stillUsed = await _db.OrderItems.AnyAsync(o => o.ImageUrlSnapshot == image.ImageUrl, ct)
            || await _db.ProductImages.AnyAsync(i => i.ImageUrl == image.ImageUrl, ct);
        if (!stillUsed)
        {
            await _storage.DeleteAsync(image.ImageUrl, ct);
        }
    }

    private async Task<ProductImage> FindAsync(int imageId, CancellationToken ct) =>
        await _db.ProductImages.FirstOrDefaultAsync(i => i.ImageId == imageId, ct)
        ?? throw new NotFoundException("Image not found.");
}
