using Daisy.Domain.Common;

namespace Daisy.Application.Common;

/// <summary>An uploaded file passed from the API layer to an application service.</summary>
public sealed record FileUpload(string FileName, long Length, Func<Stream> OpenReadStream);

/// <summary>
/// Detects image formats from the file signature, not the extension (API_DESIGN §10).
/// </summary>
public static class ImageFormat
{
    public static async Task<string?> DetectExtensionAsync(FileUpload file, CancellationToken cancellationToken)
    {
        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        if (read < 4)
        {
            return null;
        }

        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return ".jpg";
        }

        if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
        {
            return ".png";
        }

        if (read >= 12
            && header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F'
            && header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P')
        {
            return ".webp";
        }

        return null;
    }

    /// <summary>Returns the extension of a valid image, or throws 400 UNSUPPORTED_FILE.</summary>
    public static async Task<string> EnsureImageAsync(FileUpload file, long maxBytes, CancellationToken cancellationToken)
    {
        if (file.Length <= 0 || file.Length > maxBytes)
        {
            throw new BadRequestException(
                ErrorCodes.UnsupportedFile,
                $"'{file.FileName}' must be a JPEG, PNG or WebP image of at most {maxBytes / (1024 * 1024)} MB.");
        }

        return await DetectExtensionAsync(file, cancellationToken)
            ?? throw new BadRequestException(
                ErrorCodes.UnsupportedFile,
                $"'{file.FileName}' must be a JPEG, PNG or WebP image.");
    }
}
